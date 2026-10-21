using System.Collections.Immutable;
using System.Diagnostics;
using Xunit.Sdk;

namespace Rezultat.Testing.Xunit;

/// <summary>
/// A failed assertion throws <c>Xunit.Sdk.FailException</c> from the xunit.v3.assert assembly, including in xUnit v2
/// tests, where it is not the v2 <c>Xunit.Sdk.XunitException</c>.
/// </summary>
public static class ValidationAssertions
{
    /// <summary>
    /// Returns the exact valid payload, which stays nullable when <typeparamref name="T"/> is. A matching branch
    /// formats nothing; Invalid and an uninitialized <c>default</c> fail without returning.
    /// </summary>
    [StackTraceHidden]
    public static T ShouldBeValid<T, TIssue>(this Validation<T, TIssue> validation)
    {
        if (!validation.IsInitialized)
        {
            throw FailException.ForFailure(FailureText.Uninitialized("Validation.Valid", "Validation"));
        }

        if (validation.TryGetValue(out var value, out var issues))
        {
            return value;
        }

        throw FailException.ForFailure(FailureText.Invalid("Validation.Valid", issues));
    }

    /// <summary>
    /// Returns the stored issues as is: same buffer, original order, repeats and nulls kept. A matching branch
    /// formats nothing; Valid and an uninitialized <c>default</c> fail without returning.
    /// </summary>
    [StackTraceHidden]
    public static ImmutableArray<TIssue> ShouldBeInvalid<T, TIssue>(this Validation<T, TIssue> validation)
    {
        if (!validation.IsInitialized)
        {
            throw FailException.ForFailure(FailureText.Uninitialized("Validation.Invalid", "Validation"));
        }

        if (!validation.TryGetValue(out var value, out var issues))
        {
            return issues;
        }

        throw FailException.ForFailure(FailureText.WrongBranch("Validation.Invalid", "Validation.Valid", value));
    }
}
