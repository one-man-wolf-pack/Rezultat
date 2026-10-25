using System.Collections.Immutable;
using System.Diagnostics;
using Xunit.Sdk;

namespace Rezultat.Testing.Xunit;

/// <summary>
/// A failed assertion throws <c>Xunit.Sdk.FailException</c> from the xunit.v3.assert assembly, including in xUnit v2
/// tests, where it is not the v2 <c>Xunit.Sdk.XunitException</c>.
/// </summary>
public static class ValidationAssertions {
    /// <summary>
    /// Returns the exact valid payload, which stays nullable when <typeparamref name="T"/> is. A matching branch
    /// formats nothing; Invalid and an uninitialized <c>default</c> fail without returning. The failure message lists
    /// every issue, as with <c>includePayload: true</c>.
    /// </summary>
    [StackTraceHidden]
    public static T ShouldBeValid<T, TIssue>(this Validation<T, TIssue> validation) => ShouldBeValid(validation, includePayload: true);

    /// <summary>
    /// Returns the exact valid payload, which stays nullable when <typeparamref name="T"/> is. A matching branch
    /// formats nothing; Invalid and an uninitialized <c>default</c> fail without returning.
    /// With <c>includePayload: true</c> the failure message shows the issue count and every issue through its
    /// <c>ToString()</c>. With <c>false</c> it names only the expected and actual branches, without the issues or their
    /// count, and does not walk them; this limits only this failure message.
    /// </summary>
    [StackTraceHidden]
    public static T ShouldBeValid<T, TIssue>(this Validation<T, TIssue> validation, bool includePayload) {
        if (!validation.IsInitialized) {
            throw FailException.ForFailure(FailureText.Branch("Validation.Valid", "Validation.Uninitialized"));
        }

        if (validation.TryGetValue(out var value, out var issues)) {
            return value;
        }

        throw FailException.ForFailure(includePayload
            ? FailureText.Invalid("Validation.Valid", issues)
            : FailureText.Branch("Validation.Valid", "Validation.Invalid"));
    }

    /// <summary>
    /// Returns the stored issues as is: same buffer, original order, repeats and nulls kept. A matching branch
    /// formats nothing; Valid and an uninitialized <c>default</c> fail without returning. The failure message shows the
    /// active valid payload, as with <c>includePayload: true</c>.
    /// </summary>
    [StackTraceHidden]
    public static ImmutableArray<TIssue> ShouldBeInvalid<T, TIssue>(this Validation<T, TIssue> validation) =>
        ShouldBeInvalid(validation, includePayload: true);

    /// <summary>
    /// Returns the stored issues as is: same buffer, original order, repeats and nulls kept. A matching branch
    /// formats nothing; Valid and an uninitialized <c>default</c> fail without returning.
    /// With <c>includePayload: true</c> the failure message shows the active valid payload through its
    /// <c>ToString()</c>. With <c>false</c> it names only the expected and actual branches and does not format the
    /// payload; this limits only this failure message.
    /// </summary>
    [StackTraceHidden]
    public static ImmutableArray<TIssue> ShouldBeInvalid<T, TIssue>(this Validation<T, TIssue> validation, bool includePayload) {
        if (!validation.IsInitialized) {
            throw FailException.ForFailure(FailureText.Branch("Validation.Invalid", "Validation.Uninitialized"));
        }

        if (!validation.TryGetValue(out var value, out var issues)) {
            return issues;
        }

        throw FailException.ForFailure(includePayload
            ? FailureText.WrongBranch("Validation.Invalid", "Validation.Valid", value)
            : FailureText.Branch("Validation.Invalid", "Validation.Valid"));
    }
}
