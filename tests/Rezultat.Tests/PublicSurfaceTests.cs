using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Rezultat.Tests;

public sealed class PublicSurfaceTests
{
    private const BindingFlags DeclaredPublic =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    [Fact]
    public void Assembly_exports_only_result_and_unit()
    {
        var exported = typeof(Unit).Assembly.GetExportedTypes().Select(type => type.FullName).Order(StringComparer.Ordinal);

        Assert.Equal(["Rezultat.Result`2", "Rezultat.Unit"], exported);
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
            "Rezultat.Result`2[T,TError] Error(TError)",
            "Rezultat.Result`2[T,TError] Ok(T)",
            "System.String ToString()",
            "TResult Match[TResult](System.Func`2[T,TResult], System.Func`2[TError,TResult])",
        ];

        Assert.Equal(expected, DeclaredMembers(typeof(Result<,>)));
        Assert.Equal([typeof(IEquatable<Result<int, string>>)], typeof(Result<int, string>).GetInterfaces());
        Assert.True(typeof(Result<,>).IsValueType);
        Assert.NotNull(typeof(Result<,>).GetCustomAttribute<System.Runtime.CompilerServices.IsReadOnlyAttribute>());
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
