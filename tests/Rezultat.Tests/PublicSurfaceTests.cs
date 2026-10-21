using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Rezultat.Collections;
using Rezultat.Linq;

namespace Rezultat.Tests;

public sealed class PublicSurfaceTests
{
    private const BindingFlags DeclaredPublic =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void Assembly_exports_only_rfc_types()
    {
        var exported = typeof(Unit).Assembly.GetExportedTypes().Select(type => type.FullName).Order(StringComparer.Ordinal);

        Assert.Equal(
            [
                "Rezultat.Collections.MaybeCollectionExtensions", "Rezultat.Collections.ResultCollectionExtensions",
                "Rezultat.Collections.ValidationCollectionExtensions", "Rezultat.Linq.MaybeQueryExtensions", "Rezultat.Linq.ResultQueryExtensions",
                "Rezultat.Maybe", "Rezultat.MaybeExtensions", "Rezultat.Maybe`1",
                "Rezultat.PresenceExtensions", "Rezultat.Presence`1", "Rezultat.ResultExtensions", "Rezultat.Result`2", "Rezultat.Unit",
                "Rezultat.ValidationExtensions", "Rezultat.Validation`2",
            ],
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
            "Rezultat.Result`2[T,TError] Inspect(System.Action`1[T])",
            "Rezultat.Result`2[T,TError] InspectError(System.Action`1[TError])",
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
    public void ResultExtensions_is_static_and_declares_only_unconstrained_ToMaybe_and_ToValidation()
    {
        var type = typeof(ResultExtensions);

        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Equal(
            [
                "Rezultat.Maybe`1[T] ToMaybe[T,TError](Rezultat.Result`2[T,TError])",
                "Rezultat.Validation`2[T,TError] ToValidation[T,TError](Rezultat.Result`2[T,TError])",
            ],
            DeclaredMembers(type));
        AssertUnconstrainedExtensions(type);
        AssertNullableConstraints(type.GetMethod(nameof(ResultExtensions.ToValidation))!, MaybeNullable, MaybeNullable);
    }

    [Fact]
    public void ValidationExtensions_is_root_static_and_declares_only_unconstrained_ToResult()
    {
        var type = typeof(ValidationExtensions);

        Assert.Equal("Rezultat", type.Namespace);
        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Equal(
            ["Rezultat.Result`2[T,System.Collections.Immutable.ImmutableArray`1[TIssue]] ToResult[T,TIssue](Rezultat.Validation`2[T,TIssue])"],
            DeclaredMembers(type));
        AssertUnconstrainedExtensions(type);
        AssertNullableConstraints(type.GetMethod(nameof(ValidationExtensions.ToResult))!, MaybeNullable, MaybeNullable);
    }

    [Fact]
    public void PresenceExtensions_is_root_static_and_declares_only_Presence_receiver_Traverse_with_notnull_payloads()
    {
        var type = typeof(PresenceExtensions);
        var traverse = type.GetMethod(nameof(PresenceExtensions.Traverse))!;

        Assert.Equal("Rezultat", type.Namespace);
        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Equal(
            ["Rezultat.Result`2[Rezultat.Presence`1[TResult],TError] Traverse[T,TResult,TError](Rezultat.Presence`1[T], System.Func`2[T,Rezultat.Result`2[TResult,TError]])"],
            DeclaredMembers(type));
        AssertUnconstrainedExtensions(type);
        AssertNullableConstraints(traverse, NotNull, NotNull, MaybeNullable);
    }

    [Fact]
    public void ResultCollectionExtensions_is_static_and_declares_only_unconstrained_Traverse_and_Sequence()
    {
        var type = typeof(ResultCollectionExtensions);

        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Equal(
            [
                "Rezultat.Result`2[TResult[],TError] Traverse[TSource,TResult,TError](System.Collections.Generic.IEnumerable`1[TSource], System.Func`2[TSource,Rezultat.Result`2[TResult,TError]])",
                "Rezultat.Result`2[T[],TError] Sequence[T,TError](System.Collections.Generic.IEnumerable`1[Rezultat.Result`2[T,TError]])",
            ],
            DeclaredMembers(type));
        AssertUnconstrainedExtensions(type);
        AssertNonNullArrayPayload(type.GetMethod(nameof(ResultCollectionExtensions.Sequence))!);
    }

    [Fact]
    public void ResultQueryExtensions_is_static_and_declares_only_unconstrained_Select_and_SelectMany()
    {
        var type = typeof(ResultQueryExtensions);

        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Equal(
            [
                "Rezultat.Result`2[TResult,TError] SelectMany[T,TNext,TResult,TError](Rezultat.Result`2[T,TError], System.Func`2[T,Rezultat.Result`2[TNext,TError]], System.Func`3[T,TNext,TResult])",
                "Rezultat.Result`2[TResult,TError] Select[T,TResult,TError](Rezultat.Result`2[T,TError], System.Func`2[T,TResult])",
            ],
            DeclaredMembers(type));
        AssertUnconstrainedExtensions(type);
        Assert.All(type.GetMethods(DeclaredPublic), method => Assert.Equal(MaybeNullable, NullableFlag(method.GetGenericArguments()[0], method)));
    }

    [Fact]
    public void MaybeQueryExtensions_is_static_and_declares_only_unconstrained_Select_SelectMany_and_Where()
    {
        var type = typeof(MaybeQueryExtensions);

        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Equal(
            [
                "Rezultat.Maybe`1[TResult] SelectMany[T,TNext,TResult](Rezultat.Maybe`1[T], System.Func`2[T,Rezultat.Maybe`1[TNext]], System.Func`3[T,TNext,TResult])",
                "Rezultat.Maybe`1[TResult] Select[T,TResult](Rezultat.Maybe`1[T], System.Func`2[T,TResult])",
                "Rezultat.Maybe`1[T] Where[T](Rezultat.Maybe`1[T], System.Func`2[T,System.Boolean])",
            ],
            DeclaredMembers(type));
        AssertUnconstrainedExtensions(type);
        Assert.All(type.GetMethods(DeclaredPublic), method => Assert.Equal(MaybeNullable, NullableFlag(method.GetGenericArguments()[0], method)));
    }

    [Fact]
    public void MaybeExtensions_is_static_and_declares_only_unconstrained_Maybe_receiver_Traverse()
    {
        var type = typeof(MaybeExtensions);

        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Equal(
            ["Rezultat.Result`2[Rezultat.Maybe`1[TResult],TError] Traverse[T,TResult,TError](Rezultat.Maybe`1[T], System.Func`2[T,Rezultat.Result`2[TResult,TError]])"],
            DeclaredMembers(type));
        AssertUnconstrainedExtensions(type);
    }

    [Fact]
    public void MaybeCollectionExtensions_is_static_and_declares_only_rfc_collection_extensions()
    {
        var type = typeof(MaybeCollectionExtensions);
        string[] expected =
        [
            "Rezultat.Maybe`1[T] ElementAtOrNone[T](System.Collections.Generic.IEnumerable`1[T], Int32)",
            "Rezultat.Maybe`1[T] FirstOrNone[T](System.Collections.Generic.IEnumerable`1[T])",
            "Rezultat.Maybe`1[T] FirstOrNone[T](System.Collections.Generic.IEnumerable`1[T], System.Func`2[T,System.Boolean])",
            "Rezultat.Maybe`1[T] LastOrNone[T](System.Collections.Generic.IEnumerable`1[T])",
            "Rezultat.Maybe`1[T] LastOrNone[T](System.Collections.Generic.IEnumerable`1[T], System.Func`2[T,System.Boolean])",
            "Rezultat.Maybe`1[T] SingleOrNone[T](System.Collections.Generic.IEnumerable`1[T])",
            "Rezultat.Maybe`1[T] SingleOrNone[T](System.Collections.Generic.IEnumerable`1[T], System.Func`2[T,System.Boolean])",
            "Rezultat.Maybe`1[T[]] Sequence[T](System.Collections.Generic.IEnumerable`1[Rezultat.Maybe`1[T]])",
            "Rezultat.Maybe`1[TResult[]] Traverse[TSource,TResult](System.Collections.Generic.IEnumerable`1[TSource], System.Func`2[TSource,Rezultat.Maybe`1[TResult]])",
            "Rezultat.Maybe`1[TValue] GetOrNone[TKey,TValue](System.Collections.Generic.IDictionary`2[TKey,TValue], TKey)",
            "Rezultat.Maybe`1[TValue] GetOrNone[TKey,TValue](System.Collections.Generic.IReadOnlyDictionary`2[TKey,TValue], TKey)",
            "Rezultat.Result`2[Rezultat.Maybe`1[T],TError] AtMostOne[T,TError](System.Collections.Generic.IEnumerable`1[T], System.Func`1[TError])",
            "System.Collections.Generic.IEnumerable`1[TResult] Choose[TSource,TResult](System.Collections.Generic.IEnumerable`1[TSource], System.Func`2[TSource,Rezultat.Maybe`1[TResult]])",
        ];

        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Equal(expected.Order(StringComparer.Ordinal), DeclaredMembers(type));
        AssertUnconstrainedExtensions(type);
    }

    [Fact]
    public void Only_read_only_GetOrNone_carries_overload_priority()
    {
        var priorities = typeof(MaybeCollectionExtensions).GetMethods(DeclaredPublic)
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
        var traverse = typeof(MaybeCollectionExtensions).GetMethod(nameof(MaybeCollectionExtensions.Traverse))!;
        var returned = new NullabilityInfoContext().Create(traverse.ReturnParameter);

        Assert.Equal(NullabilityState.NotNull, returned.GenericTypeArguments[0].ReadState);
        Assert.Equal(
            NullabilityState.NotNull,
            new NullabilityInfoContext().Create(typeof(MaybeCollectionExtensions).GetMethod(nameof(MaybeCollectionExtensions.Sequence))!.ReturnParameter)
                .GenericTypeArguments[0].ReadState);
    }

    [Fact]
    public void Traverse_success_array_is_declared_non_null()
    {
        var returned = new NullabilityInfoContext().Create(typeof(ResultCollectionExtensions).GetMethod(nameof(ResultCollectionExtensions.Traverse))!.ReturnParameter);

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

    [Fact]
    public void Presence_declares_only_rfc_members()
    {
        string[] expected =
        [
            "Boolean Equals(Rezultat.Presence`1[T])",
            "Boolean Equals(System.Object)",
            "Boolean IsMissing",
            "Boolean IsNull",
            "Boolean IsValue",
            "Boolean TryGetValue(T ByRef)",
            "Boolean get_IsMissing()",
            "Boolean get_IsNull()",
            "Boolean get_IsValue()",
            "Boolean op_Equality(Rezultat.Presence`1[T], Rezultat.Presence`1[T])",
            "Boolean op_Inequality(Rezultat.Presence`1[T], Rezultat.Presence`1[T])",
            "Int32 GetHashCode()",
            "Rezultat.Presence`1[T] Missing",
            "Rezultat.Presence`1[T] Null",
            "Rezultat.Presence`1[T] Value(T)",
            "Rezultat.Presence`1[T] get_Missing()",
            "Rezultat.Presence`1[T] get_Null()",
            "Rezultat.Presence`1[T] FilterToMissing(System.Func`2[T,System.Boolean])",
            "Rezultat.Presence`1[T] FilterToNull(System.Func`2[T,System.Boolean])",
            "Rezultat.Presence`1[TResult] AndThen[TResult](System.Func`2[T,Rezultat.Presence`1[TResult]])",
            "Rezultat.Presence`1[TResult] Map[TResult](System.Func`2[T,TResult])",
            "System.String ToString()",
            "TResult Match[TResult](System.Func`2[T,TResult], System.Func`1[TResult], System.Func`1[TResult])",
        ];

        Assert.Equal(expected.Order(StringComparer.Ordinal), DeclaredMembers(typeof(Presence<>)));
        Assert.Equal(GenericParameterAttributes.None, typeof(Presence<>).GetGenericArguments()[0].GenericParameterAttributes);
        Assert.Equal([typeof(IEquatable<Presence<int>>)], typeof(Presence<int>).GetInterfaces());
        Assert.True(typeof(Presence<>).IsValueType);
        Assert.False(typeof(Presence<>).IsByRefLike);
        Assert.NotNull(typeof(Presence<>).GetCustomAttribute<System.Runtime.CompilerServices.IsReadOnlyAttribute>());
        Assert.Empty(typeof(Presence<>).GetConstructors());
    }

    [Fact]
    public void Presence_projection_results_are_notnull_and_Validation_Map_result_is_unconstrained()
    {
        AssertNullableConstraints(typeof(Presence<>).GetMethod(nameof(Presence<int>.AndThen))!, NotNull);
        AssertNullableConstraints(typeof(Presence<>).GetMethod(nameof(Presence<int>.Map))!, NotNull);
        AssertNullableConstraints(typeof(Validation<,>).GetMethod(nameof(Validation<int, int>.Map))!, MaybeNullable);
    }

    [Fact]
    public void Presence_TryGetValue_output_is_not_null_only_when_true()
    {
        var output = typeof(Presence<>).GetMethod(nameof(Presence<int>.TryGetValue))!.GetParameters()[0];

        Assert.False(output.GetCustomAttribute<MaybeNullWhenAttribute>()!.ReturnValue);
        Assert.True(output.GetCustomAttribute<NotNullWhenAttribute>()!.ReturnValue);
    }

    [Fact]
    public void Validation_declares_only_rfc_members()
    {
        string[] expected =
        [
            "Boolean Equals(Rezultat.Validation`2[T,TIssue])",
            "Boolean Equals(System.Object)",
            "Boolean IsInitialized",
            "Boolean IsValid",
            "Boolean TryGetValue(T ByRef, System.Collections.Immutable.ImmutableArray`1[TIssue] ByRef)",
            "Boolean get_IsInitialized()",
            "Boolean get_IsValid()",
            "Boolean op_Equality(Rezultat.Validation`2[T,TIssue], Rezultat.Validation`2[T,TIssue])",
            "Boolean op_Inequality(Rezultat.Validation`2[T,TIssue], Rezultat.Validation`2[T,TIssue])",
            "Int32 GetHashCode()",
            "Rezultat.Validation`2[T,TIssue] Ensure(System.Func`2[T,System.Boolean], System.Func`2[T,TIssue])",
            "Rezultat.Validation`2[T,TIssue] Invalid(TIssue)",
            "Rezultat.Validation`2[T,TIssue] InvalidMany(System.Collections.Immutable.ImmutableArray`1[TIssue])",
            "Rezultat.Validation`2[T,TIssue] Valid(T)",
            "Rezultat.Validation`2[T,TNewIssue] MapIssues[TNewIssue](System.Func`2[TIssue,TNewIssue])",
            "Rezultat.Validation`2[T,TNewIssue] OrElse[TNewIssue](System.Func`2[System.Collections.Immutable.ImmutableArray`1[TIssue],Rezultat.Validation`2[T,TNewIssue]])",
            "Rezultat.Validation`2[TResult,TIssue] AndThen[TResult](System.Func`2[T,Rezultat.Validation`2[TResult,TIssue]])",
            "Rezultat.Validation`2[TResult,TIssue] Map[TResult](System.Func`2[T,TResult])",
            "Rezultat.Validation`2[TResult,TIssue] Zip[TB,TC,TD,TResult](Rezultat.Validation`2[TB,TIssue], Rezultat.Validation`2[TC,TIssue], Rezultat.Validation`2[TD,TIssue], System.Func`5[T,TB,TC,TD,TResult])",
            "Rezultat.Validation`2[TResult,TIssue] Zip[TB,TC,TResult](Rezultat.Validation`2[TB,TIssue], Rezultat.Validation`2[TC,TIssue], System.Func`4[T,TB,TC,TResult])",
            "Rezultat.Validation`2[TResult,TIssue] Zip[TOther,TResult](Rezultat.Validation`2[TOther,TIssue], System.Func`3[T,TOther,TResult])",
            "System.String ToString()",
            "TResult Match[TResult](System.Func`2[T,TResult], System.Func`2[System.Collections.Immutable.ImmutableArray`1[TIssue],TResult])",
        ];

        Assert.Equal(expected.Order(StringComparer.Ordinal), DeclaredMembers(typeof(Validation<,>)));
        Assert.All(typeof(Validation<,>).GetGenericArguments(), argument => Assert.Equal(GenericParameterAttributes.None, argument.GenericParameterAttributes));
        Assert.All(
            typeof(Validation<,>).GetMethods(DeclaredPublic).SelectMany(method => method.GetGenericArguments()),
            argument => Assert.Equal(GenericParameterAttributes.None, argument.GenericParameterAttributes));
        Assert.Equal([typeof(IEquatable<Validation<int, string>>)], typeof(Validation<int, string>).GetInterfaces());
        Assert.True(typeof(Validation<,>).IsValueType);
        Assert.False(typeof(Validation<,>).IsByRefLike);
        Assert.NotNull(typeof(Validation<,>).GetCustomAttribute<System.Runtime.CompilerServices.IsReadOnlyAttribute>());
        Assert.Empty(typeof(Validation<,>).GetConstructors());
    }

    [Fact]
    public void Validation_TryGetValue_only_value_output_is_maybe_null_when_false()
    {
        var parameters = typeof(Validation<,>).GetMethod(nameof(Validation<int, int>.TryGetValue))!.GetParameters();

        Assert.False(parameters[0].GetCustomAttribute<MaybeNullWhenAttribute>()!.ReturnValue);
        Assert.DoesNotContain(parameters[1].GetCustomAttributes(), attribute => attribute.GetType().Namespace == typeof(MaybeNullWhenAttribute).Namespace);
    }

    [Fact]
    public void ValidationCollectionExtensions_is_static_and_declares_only_unconstrained_rfc_collection_extensions()
    {
        var type = typeof(ValidationCollectionExtensions);
        var traverse = type.GetMethod(nameof(ValidationCollectionExtensions.Traverse))!;

        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Equal(
            [
                "Rezultat.Validation`2[Rezultat.Unit,TIssue] ValidateAll[T,TIssue](System.Collections.Generic.IEnumerable`1[Rezultat.Validation`2[T,TIssue]])",
                "Rezultat.Validation`2[Rezultat.Unit,TIssue] ValidateAll[TSource,TValue,TIssue](System.Collections.Generic.IEnumerable`1[TSource], System.Func`2[TSource,Rezultat.Validation`2[TValue,TIssue]])",
                "Rezultat.Validation`2[TResult[],TIssue] Traverse[TSource,TResult,TIssue](System.Collections.Generic.IEnumerable`1[TSource], System.Func`2[TSource,Rezultat.Validation`2[TResult,TIssue]])",
                "Rezultat.Validation`2[T[],TIssue] Sequence[T,TIssue](System.Collections.Generic.IEnumerable`1[Rezultat.Validation`2[T,TIssue]])",
            ],
            DeclaredMembers(type));
        AssertUnconstrainedExtensions(type);
        Assert.Equal(NullabilityState.NotNull, new NullabilityInfoContext().Create(traverse.ReturnParameter).GenericTypeArguments[0].ReadState);
        AssertNonNullArrayPayload(type.GetMethod(nameof(ValidationCollectionExtensions.Sequence))!);
    }

    private static void AssertNonNullArrayPayload(MethodInfo method) =>
        Assert.Equal(NullabilityState.NotNull, new NullabilityInfoContext().Create(method.ReturnParameter).GenericTypeArguments[0].ReadState);

    private const byte NotNull = 1;
    private const byte MaybeNullable = 2;

    private static void AssertNullableConstraints(MethodInfo method, params byte[] expected) =>
        Assert.Equal(expected, method.GetGenericArguments().Select(argument => NullableFlag(argument, method)).ToArray());

    private static byte NullableFlag(Type parameter, MethodInfo method)
    {
        var flag = NullableValue(parameter.CustomAttributes, "System.Runtime.CompilerServices.NullableAttribute");
        for (MemberInfo? owner = method; flag is null && owner is not null; owner = owner.DeclaringType)
        {
            flag = NullableValue(owner.CustomAttributes, "System.Runtime.CompilerServices.NullableContextAttribute");
        }

        return flag ?? 0;
    }

    private static byte? NullableValue(IEnumerable<CustomAttributeData> attributes, string name) =>
        attributes.Where(attribute => attribute.AttributeType.FullName == name)
            .Select(static attribute => attribute.ConstructorArguments[0].Value as byte?)
            .FirstOrDefault();

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
