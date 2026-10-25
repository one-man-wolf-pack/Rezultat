using System.Diagnostics;
using Xunit.Sdk;

namespace Rezultat.Testing.Xunit;

/// <summary>
/// A failed assertion throws <c>Xunit.Sdk.FailException</c> from the xunit.v3.assert assembly, including in xUnit v2
/// tests, where it is not the v2 <c>Xunit.Sdk.XunitException</c>.
/// </summary>
public static class ResultAssertions {
    /// <summary>
    /// Returns the exact success payload, which stays nullable when <typeparamref name="T"/> is. A matching branch
    /// formats nothing; Error and an uninitialized <c>default</c> fail without returning. The failure message shows the
    /// active error, as with <c>includePayload: true</c>.
    /// </summary>
    [StackTraceHidden]
    public static T ShouldBeOk<T, TError>(this Result<T, TError> result) => ShouldBeOk(result, includePayload: true);

    /// <summary>
    /// Returns the exact success payload, which stays nullable when <typeparamref name="T"/> is. A matching branch
    /// formats nothing; Error and an uninitialized <c>default</c> fail without returning.
    /// With <c>includePayload: true</c> the failure message shows the active error through its <c>ToString()</c>. With
    /// <c>false</c> it names only the expected and actual branches and does not format the error; this limits only this
    /// failure message.
    /// </summary>
    [StackTraceHidden]
    public static T ShouldBeOk<T, TError>(this Result<T, TError> result, bool includePayload) {
        if (!result.IsInitialized) {
            throw FailException.ForFailure(FailureText.Branch("Result.Ok", "Result.Uninitialized"));
        }

        if (result.TryGetValue(out var value, out var error)) {
            return value;
        }

        throw FailException.ForFailure(includePayload
            ? FailureText.WrongBranch("Result.Ok", "Result.Error", error)
            : FailureText.Branch("Result.Ok", "Result.Error"));
    }

    /// <summary>
    /// Returns the exact error payload, which stays nullable when <typeparamref name="TError"/> is. A matching
    /// branch formats nothing; Ok and an uninitialized <c>default</c> fail without returning. The failure message shows
    /// the active success payload, as with <c>includePayload: true</c>.
    /// </summary>
    [StackTraceHidden]
    public static TError ShouldBeError<T, TError>(this Result<T, TError> result) => ShouldBeError(result, includePayload: true);

    /// <summary>
    /// Returns the exact error payload, which stays nullable when <typeparamref name="TError"/> is. A matching
    /// branch formats nothing; Ok and an uninitialized <c>default</c> fail without returning.
    /// With <c>includePayload: true</c> the failure message shows the active success payload through its
    /// <c>ToString()</c>. With <c>false</c> it names only the expected and actual branches and does not format the
    /// payload; this limits only this failure message.
    /// </summary>
    [StackTraceHidden]
    public static TError ShouldBeError<T, TError>(this Result<T, TError> result, bool includePayload) {
        if (!result.IsInitialized) {
            throw FailException.ForFailure(FailureText.Branch("Result.Error", "Result.Uninitialized"));
        }

        if (!result.TryGetValue(out var value, out var error)) {
            return error;
        }

        throw FailException.ForFailure(includePayload
            ? FailureText.WrongBranch("Result.Error", "Result.Ok", value)
            : FailureText.Branch("Result.Error", "Result.Ok"));
    }
}
