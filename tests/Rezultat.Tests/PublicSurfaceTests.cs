using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Rezultat.Tests;

public sealed class PublicSurfaceTests
{
    private const BindingFlags DeclaredPublic =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void Assembly_exports_only_result_maybe_and_unit_types()
    {
        var exported = typeof(Unit).Assembly.GetExportedTypes().Select(type => type.FullName).Order(StringComparer.Ordinal);

        Assert.Equal(
            ["Rezultat.Maybe", "Rezultat.MaybeExtensions", "Rezultat.Maybe`1", "Rezultat.ResultExtensions", "Rezultat.Result`2", "Rezultat.Unit"],
            exported);
    }

    [Fact]
    public void Result_declares_only_rfc_members()
    {
        string[] expected =
        [
            "Boolean Equals(Rezultat.Result`2[T,TError])",
            "Boolean Equals(System.Object)",
            "Boolean IsInitialized",
            "Boolean IsSuccess",
            "Boolean TryGetValue(T ByRef, TError ByRef)",
            "Boolean get_IsInitialized()",
            "Boolean get_IsSuccess()",
            "Boolean op_Equality(Rezultat.Result`2[T,TError], Rezultat.Result`2[T,TError])",
            "Boolean op_Inequality(Rezultat.Result`2[T,TError], Rezultat.Result`2[T,TError])",
            "Int32 GetHashCode()",
            "Rezultat.Result`2[T,TError] Ensure(System.Func`2[T,System.Boolean], System.Func`2[T,TError])",
            "Rezultat.Result`2[T,TError] Error(TError)",
            "Rezultat.Result`2[T,TError] Ok(T)",
            "Rezultat.Result`2[T,TNewError] MapError[TNewError,TState](TState, System.Func`3[TError,TState,TNewError])",
            "Rezultat.Result`2[T,TNewError] MapError[TNewError](System.Func`2[TError,TNewError])",
            "Rezultat.Result`2[T,TNewError] OrElse[TNewError](System.Func`2[TError,Rezultat.Result`2[T,TNewError]])",
            "Rezultat.Result`2[TResult,TError] AndThen[TResult](System.Func`2[T,Rezultat.Result`2[TResult,TError]])",
            "Rezultat.Result`2[TResult,TError] Map[TResult](System.Func`2[T,TResult])",
            "Rezultat.Result`2[TResult,TError] Zip[TOther,TResult](Rezultat.Result`2[TOther,TError], System.Func`3[T,TOther,TResult])",
            "System.String ToString()",
            "TResult Match[TResult](System.Func`2[T,TResult], System.Func`2[TError,TResult])",
        ];

        Assert.Equal(expected, DeclaredMembers(typeof(Result<,>)));
        Assert.All(
            typeof(Result<,>).GetMethods(DeclaredPublic).SelectMany(method => method.GetGenericArguments()),
            argument => Assert.Equal(GenericParameterAttributes.None, argument.GenericParameterAttributes));
        Assert.Equal([typeof(IEquatable<Result<int, string>>)], typeof(Result<int, string>).GetInterfaces());
        Assert.True(typeof(Result<,>).IsValueType);
        Assert.NotNull(typeof(Result<,>).GetCustomAttribute<System.Runtime.CompilerServices.IsReadOnlyAttribute>());
    }

    [Fact]
    public void ResultExtensions_is_static_and_declares_only_unconstrained_Traverse_and_ToMaybe()
    {
        var type = typeof(ResultExtensions);

        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Equal(
            [
                "Rezultat.Maybe`1[T] ToMaybe[T,TError](Rezultat.Result`2[T,TError])",
                "Rezultat.Result`2[TResult[],TError] Traverse[TSource,TResult,TError](System.Collections.Generic.IEnumerable`1[TSource], System.Func`2[TSource,Rezultat.Result`2[TResult,TError]])",
            ],
            DeclaredMembers(type));
        AssertUnconstrainedExtensions(type);
    }

    [Fact]
    public void MaybeExtensions_is_static_and_declares_only_rfc_extensions()
    {
        var type = typeof(MaybeExtensions);
        string[] expected =
        [
            "Rezultat.Maybe`1[T] ElementAtOrNone[T](System.Collections.Generic.IEnumerable`1[T], Int32)",
            "Rezultat.Maybe`1[T] FirstOrNone[T](System.Collections.Generic.IEnumerable`1[T])",
            "Rezultat.Maybe`1[T] FirstOrNone[T](System.Collections.Generic.IEnumerable`1[T], System.Func`2[T,System.Boolean])",
            "Rezultat.Maybe`1[T] LastOrNone[T](System.Collections.Generic.IEnumerable`1[T])",
            "Rezultat.Maybe`1[T] LastOrNone[T](System.Collections.Generic.IEnumerable`1[T], System.Func`2[T,System.Boolean])",
            "Rezultat.Maybe`1[T] SingleOrNone[T](System.Collections.Generic.IEnumerable`1[T])",
            "Rezultat.Maybe`1[T] SingleOrNone[T](System.Collections.Generic.IEnumerable`1[T], System.Func`2[T,System.Boolean])",
            "Rezultat.Maybe`1[TResult[]] Traverse[TSource,TResult](System.Collections.Generic.IEnumerable`1[TSource], System.Func`2[TSource,Rezultat.Maybe`1[TResult]])",
            "Rezultat.Maybe`1[TValue] GetOrNone[TKey,TValue](System.Collections.Generic.IDictionary`2[TKey,TValue], TKey)",
            "Rezultat.Maybe`1[TValue] GetOrNone[TKey,TValue](System.Collections.Generic.IReadOnlyDictionary`2[TKey,TValue], TKey)",
            "Rezultat.Result`2[Rezultat.Maybe`1[TResult],TError] Traverse[T,TResult,TError](Rezultat.Maybe`1[T], System.Func`2[T,Rezultat.Result`2[TResult,TError]])",
            "System.Collections.Generic.IEnumerable`1[TResult] Choose[TSource,TResult](System.Collections.Generic.IEnumerable`1[TSource], System.Func`2[TSource,Rezultat.Maybe`1[TResult]])",
        ];

        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Equal(expected.Order(StringComparer.Ordinal), DeclaredMembers(type));
        AssertUnconstrainedExtensions(type);
    }

    [Fact]
    public void Only_read_only_GetOrNone_carries_overload_priority()
    {
        var priorities = typeof(MaybeExtensions).GetMethods(DeclaredPublic)
            .Select(method => (method.ToString()!, method.GetCustomAttribute<System.Runtime.CompilerServices.OverloadResolutionPriorityAttribute>()?.Priority))
            .Where(entry => entry.Priority is not null)
            .ToArray();

        var (method, priority) = Assert.Single(priorities);
        Assert.Contains("IReadOnlyDictionary", method, StringComparison.Ordinal);
        Assert.Equal(1, priority);
    }

    [Fact]
    public void Maybe_Traverse_success_array_is_declared_non_null()
    {
        var traverse = typeof(MaybeExtensions).GetMethods(DeclaredPublic)
            .Single(method => method.Name == nameof(MaybeExtensions.Traverse) && method.ReturnType.GetGenericTypeDefinition() == typeof(Maybe<>));
        var returned = new NullabilityInfoContext().Create(traverse.ReturnParameter);

        Assert.Equal(NullabilityState.NotNull, returned.GenericTypeArguments[0].ReadState);
    }

    [Fact]
    public void Traverse_success_array_is_declared_non_null()
    {
        var returned = new NullabilityInfoContext().Create(typeof(ResultExtensions).GetMethod(nameof(ResultExtensions.Traverse))!.ReturnParameter);

        Assert.Equal(NullabilityState.NotNull, returned.GenericTypeArguments[0].ReadState);
    }

    [Fact]
    public void Unit_declares_only_rfc_members()
    {
        string[] expected =
        [
            "Boolean Equals(System.Object)",
            "Boolean Equals(Rezultat.Unit)",
            "Boolean op_Equality(Rezultat.Unit, Rezultat.Unit)",
            "Boolean op_Inequality(Rezultat.Unit, Rezultat.Unit)",
            "Int32 GetHashCode()",
            "Rezultat.Unit Value",
            "Rezultat.Unit get_Value()",
            "System.String ToString()",
        ];

        Assert.Equal(expected.Order(StringComparer.Ordinal), DeclaredMembers(typeof(Unit)));
        Assert.Equal([typeof(IEquatable<Unit>)], typeof(Unit).GetInterfaces());
        Assert.NotNull(typeof(Unit).GetCustomAttribute<System.Runtime.CompilerServices.IsReadOnlyAttribute>());
    }

    [Fact]
    public void TryGetValue_outputs_carry_branch_conditional_maybe_null()
    {
        var parameters = typeof(Result<,>).GetMethod(nameof(Result<int, int>.TryGetValue))!.GetParameters();

        Assert.False(parameters[0].GetCustomAttribute<MaybeNullWhenAttribute>()!.ReturnValue);
        Assert.True(parameters[1].GetCustomAttribute<MaybeNullWhenAttribute>()!.ReturnValue);
    }

    [Fact]
    public void Maybe_declares_only_rfc_members()
    {
        string[] expected =
        [
            "Boolean Equals(Rezultat.Maybe`1[T])",
            "Boolean Equals(System.Object)",
            "Boolean IsSome",
            "Boolean TryGetValue(T ByRef)",
            "Boolean get_IsSome()",
            "Boolean op_Equality(Rezultat.Maybe`1[T], Rezultat.Maybe`1[T])",
            "Boolean op_Inequality(Rezultat.Maybe`1[T], Rezultat.Maybe`1[T])",
            "Int32 GetHashCode()",
            "Rezultat.Maybe`1[T] Filter(System.Func`2[T,System.Boolean])",
            "Rezultat.Maybe`1[T] None",
            "Rezultat.Maybe`1[T] OrElse(System.Func`1[Rezultat.Maybe`1[T]])",
            "Rezultat.Maybe`1[T] Some(T)",
            "Rezultat.Maybe`1[T] get_None()",
            "Rezultat.Maybe`1[TResult] AndThen[TResult](System.Func`2[T,Rezultat.Maybe`1[TResult]])",
            "Rezultat.Maybe`1[TResult] Map[TResult](System.Func`2[T,TResult])",
            "Rezultat.Maybe`1[TResult] Zip[TOther,TResult](Rezultat.Maybe`1[TOther], System.Func`3[T,TOther,TResult])",
            "Rezultat.Result`2[T,TError] ToResult[TError](TError)",
            "Rezultat.Result`2[T,TError] ToResultElse[TError](System.Func`1[TError])",
            "System.String ToString()",
            "T ValueOr(T)",
            "T ValueOrElse(System.Func`1[T])",
            "TResult Match[TResult](System.Func`2[T,TResult], System.Func`1[TResult])",
        ];

        Assert.Equal(expected.Order(StringComparer.Ordinal), DeclaredMembers(typeof(Maybe<>)));
        Assert.Equal(GenericParameterAttributes.None, typeof(Maybe<>).GetGenericArguments()[0].GenericParameterAttributes);
        Assert.All(
            typeof(Maybe<>).GetMethods(DeclaredPublic).SelectMany(method => method.GetGenericArguments()),
            argument => Assert.Equal(GenericParameterAttributes.None, argument.GenericParameterAttributes));
        Assert.Equal([typeof(IEquatable<Maybe<int>>)], typeof(Maybe<int>).GetInterfaces());
        Assert.True(typeof(Maybe<>).IsValueType);
        Assert.False(typeof(Maybe<>).IsByRefLike);
        Assert.NotNull(typeof(Maybe<>).GetCustomAttribute<System.Runtime.CompilerServices.IsReadOnlyAttribute>());
        Assert.Empty(typeof(Maybe<>).GetConstructors());
    }

    [Fact]
    public void Maybe_companion_is_static_and_declares_only_constrained_FromNullable()
    {
        var type = typeof(Maybe);
        var overloads = type.GetMethods(DeclaredPublic).OrderBy(method => method.GetParameters()[0].ParameterType.IsGenericType).ToArray();

        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Equal(
            ["Rezultat.Maybe`1[T] FromNullable[T](System.Nullable`1[T])", "Rezultat.Maybe`1[T] FromNullable[T](T)"],
            DeclaredMembers(type));
        Assert.Equal(GenericParameterAttributes.ReferenceTypeConstraint, overloads[0].GetGenericArguments()[0].GenericParameterAttributes);
        Assert.Equal(
            GenericParameterAttributes.NotNullableValueTypeConstraint | GenericParameterAttributes.DefaultConstructorConstraint,
            overloads[1].GetGenericArguments()[0].GenericParameterAttributes);
    }

    [Fact]
    public void FromNullable_accepts_nullable_reference_and_returns_non_null_payload()
    {
        var method = typeof(Maybe).GetMethods(DeclaredPublic).Single(method => !method.GetParameters()[0].ParameterType.IsGenericType);
        var context = new NullabilityInfoContext();

        Assert.Equal(NullabilityState.Nullable, context.Create(method.GetParameters()[0]).ReadState);
        Assert.Equal(NullabilityState.NotNull, context.Create(method.ReturnParameter).GenericTypeArguments[0].ReadState);
    }

    [Fact]
    public void Maybe_TryGetValue_output_is_maybe_null_only_when_false()
    {
        var output = typeof(Maybe<>).GetMethod(nameof(Maybe<int>.TryGetValue))!.GetParameters()[0];

        Assert.False(output.GetCustomAttribute<MaybeNullWhenAttribute>()!.ReturnValue);
        Assert.Null(output.GetCustomAttribute<NotNullWhenAttribute>());
    }

    private static void AssertUnconstrainedExtensions(Type type) =>
        Assert.All(type.GetMethods(DeclaredPublic), method =>
        {
            Assert.True(method.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute)));
            Assert.All(method.GetGenericArguments(), argument => Assert.Equal(GenericParameterAttributes.None, argument.GenericParameterAttributes));
        });

    private static string[] DeclaredMembers(Type type) =>
        type.GetMembers(DeclaredPublic)
            .Where(member => member is not ConstructorInfo)
            .Select(member => member.ToString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
