using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Rezultat.Tests;

public sealed class PublicSurfaceTests
{
    private const BindingFlags DeclaredPublic =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void Assembly_exports_only_result_result_extensions_and_unit()
    {
        var exported = typeof(Unit).Assembly.GetExportedTypes().Select(type => type.FullName).Order(StringComparer.Ordinal);

        Assert.Equal(["Rezultat.ResultExtensions", "Rezultat.Result`2", "Rezultat.Unit"], exported);
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
    public void ResultExtensions_is_static_and_declares_only_unconstrained_Traverse()
    {
        var type = typeof(ResultExtensions);
        var traverse = type.GetMethod(nameof(ResultExtensions.Traverse))!;

        Assert.True(type.IsAbstract && type.IsSealed);
        Assert.Equal(
            ["Rezultat.Result`2[TResult[],TError] Traverse[TSource,TResult,TError](System.Collections.Generic.IEnumerable`1[TSource], System.Func`2[TSource,Rezultat.Result`2[TResult,TError]])"],
            DeclaredMembers(type));
        Assert.True(traverse.IsDefined(typeof(System.Runtime.CompilerServices.ExtensionAttribute)));
        Assert.All(traverse.GetGenericArguments(), argument => Assert.Equal(GenericParameterAttributes.None, argument.GenericParameterAttributes));
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

    private static string[] DeclaredMembers(Type type) =>
        type.GetMembers(DeclaredPublic)
            .Where(member => member is not ConstructorInfo)
            .Select(member => member.ToString()!)
            .Order(StringComparer.Ordinal)
            .ToArray();
}
