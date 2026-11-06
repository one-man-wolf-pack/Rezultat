using System.Diagnostics;
using Xunit.Sdk;

namespace Rezultat.Testing.Xunit;

/// <summary>
/// A failed assertion throws <c>Xunit.Sdk.FailException</c> from the xunit.v3.assert assembly, including in xUnit v2
/// tests, where it is not the v2 <c>Xunit.Sdk.XunitException</c>. <c>default</c> is an ordinary None, and
/// <c>Some(null)</c> is Some.
/// </summary>
public static class MaybeAssertions {
    /// <summary>
    /// Returns the exact payload of Some, which stays nullable when <typeparamref name="T"/> is: <c>Some(null)</c>
    /// returns <c>null</c>. A matching branch formats nothing; None and <c>default</c> fail without returning, and their
    /// message has no payload.
    /// </summary>
    [StackTraceHidden]
    public static T ShouldBeSome<T>(this Maybe<T> value) => ShouldBeSome(value, includePayload: true);

    /// <summary>
    /// Returns the exact payload of Some, which stays nullable when <typeparamref name="T"/> is: <c>Some(null)</c>
    /// returns <c>null</c>. A matching branch formats nothing; None and <c>default</c> fail without returning. None has
    /// no payload, so the failure message is the same for both values of <paramref name="includePayload"/>.
    /// </summary>
    [StackTraceHidden]
    public static T ShouldBeSome<T>(this Maybe<T> value, bool includePayload) {
        if (value.TryGetValue(out var payload)) {
            return payload;
        }

        throw FailException.ForFailure(FailureText.Branch("Maybe.Some", "Maybe.None"));
    }

    /// <summary>
    /// Passes for None and <c>default</c>, formatting nothing. Any Some fails, including <c>Some(null)</c>; the failure
    /// message shows its payload, as with <c>includePayload: true</c>.
    /// </summary>
    [StackTraceHidden]
    public static void ShouldBeNone<T>(this Maybe<T> value) => ShouldBeNone(value, includePayload: true);

    /// <summary>
    /// Passes for None and <c>default</c>, formatting nothing. Any Some fails, including <c>Some(null)</c>.
    /// With <c>includePayload: true</c> the failure message shows the payload through its <c>ToString()</c>. With
    /// <c>false</c> it names only the expected and actual branches and does not format the payload; this limits only
    /// this failure message.
    /// </summary>
    [StackTraceHidden]
    public static void ShouldBeNone<T>(this Maybe<T> value, bool includePayload) {
        if (!value.TryGetValue(out var payload)) {
            return;
        }

        throw FailException.ForFailure(includePayload
            ? FailureText.WrongBranch("Maybe.None", "Maybe.Some", payload)
            : FailureText.Branch("Maybe.None", "Maybe.Some"));
    }
}
