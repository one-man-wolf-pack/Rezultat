using System.Diagnostics;
using Xunit.Sdk;

namespace Rezultat.Testing.Xunit;

/// <summary>
/// A failed assertion throws <c>Xunit.Sdk.FailException</c> from the xunit.v3.assert assembly, including in xUnit v2
/// tests, where it is not the v2 <c>Xunit.Sdk.XunitException</c>.
/// </summary>
public static class ResultAssertions
{
    /// <summary>
    /// Returns the exact success payload, which stays nullable when <typeparamref name="T"/> is. A matching branch
    /// formats nothing; Error and an uninitialized <c>default</c> fail without returning.
    /// </summary>
    [StackTraceHidden]
    public static T ShouldBeOk<T, TError>(this Result<T, TError> result)
    {
        if (!result.IsInitialized)
        {
            throw FailException.ForFailure(FailureText.Uninitialized("Result.Ok", "Result"));
        }

        if (result.TryGetValue(out var value, out var error))
        {
            return value;
        }

        throw FailException.ForFailure(FailureText.WrongBranch("Result.Ok", "Result.Error", error));
    }

    /// <summary>
    /// Returns the exact error payload, which stays nullable when <typeparamref name="TError"/> is. A matching
    /// branch formats nothing; Ok and an uninitialized <c>default</c> fail without returning.
    /// </summary>
    [StackTraceHidden]
    public static TError ShouldBeError<T, TError>(this Result<T, TError> result)
    {
        if (!result.IsInitialized)
        {
            throw FailException.ForFailure(FailureText.Uninitialized("Result.Error", "Result"));
        }

        if (!result.TryGetValue(out var value, out var error))
        {
            return error;
        }

        throw FailException.ForFailure(FailureText.WrongBranch("Result.Error", "Result.Ok", value));
    }
}
