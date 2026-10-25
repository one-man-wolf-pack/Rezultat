using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Rezultat.Testing.Xunit.Tests;

public sealed class ValidationAssertionsTests {
    [Fact]
    public void ShouldBeValid_ReturnsTheExactPayloadWithoutTouchingIt() {
        var payload = new Probe();

        var extracted = Validation<Probe, Probe>.Valid(payload).ShouldBeValid();

        extracted.ShouldBeSameAs(payload);
        payload.Calls.ShouldBe(0);
    }

    [Fact]
    public void ShouldBeValid_ReturnsValueUnitAndNullPayloads() {
        Validation<int, string>.Valid(42).ShouldBeValid().ShouldBe(42);
        Validation<Unit, string>.Valid(Unit.Value).ShouldBeValid().ShouldBe(Unit.Value);
        Validation<string?, string>.Valid(null).ShouldBeValid().ShouldBeNull();
    }

    [Fact]
    public void ShouldBeInvalid_ReturnsTheStoredIssueBuffer() {
        var first = new Probe();
        var stored = ImmutableArray.Create<Probe?>(first, null, first);
        var validation = Validation<int, Probe?>.InvalidMany(stored);
        validation.TryGetValue(out _, out var observed);

        var extracted = validation.ShouldBeInvalid();

        ImmutableCollectionsMarshal.AsArray(extracted).ShouldBeSameAs(ImmutableCollectionsMarshal.AsArray(observed));
        ImmutableCollectionsMarshal.AsArray(extracted).ShouldBeSameAs(ImmutableCollectionsMarshal.AsArray(stored));
        first.Calls.ShouldBe(0);
    }

    [Fact]
    public void ShouldBeInvalid_KeepsASingleValueIssue() {
        ValueProbe.Reset();

        var extracted = Validation<string, ValueProbe>.Invalid(new ValueProbe(3)).ShouldBeInvalid();

        ValueProbe.Calls.ShouldBe(0);
        extracted.ShouldHaveSingleItem().Id.ShouldBe(3);
    }

    [Fact]
    public void ShouldBeValid_OnInvalidListsEveryIssueInOrder() {
        var issues = ImmutableArray.Create<string?>("bad name", "bad age", "bad name", null);

        var failure = Should.Throw<FailException>(() => Validation<int, string?>.InvalidMany(issues).ShouldBeValid()).ShouldBeOfType<FailException>();

        failure.Message.ShouldBe("Expected Validation.Valid; actual Validation.Invalid; issues=4 [0=bad name, 1=bad age, 2=bad name, 3=null].");
    }

    [Fact]
    public void ThrowingIssueFormatterDoesNotStopTheOtherIssues() {
        var formatterFault = new PoisonException();
        var issues = ImmutableArray.Create(
            new Probe("first"),
            new Probe { Throws = formatterFault },
            new Probe(null),
            new Probe { Throws = new OperationCanceledException() },
            new Probe("last"));

        var failure = Should.Throw<FailException>(() => Validation<int, Probe>.InvalidMany(issues).ShouldBeValid()).ShouldBeOfType<FailException>();

        failure.Message.ShouldBe("Expected Validation.Valid; actual Validation.Invalid; issues=5 [0=first, "
            + $"1=<ToString threw {typeof(PoisonException).FullName}>, 2=, 3=<ToString threw System.OperationCanceledException>, 4=last].");
        (formatterFault.MessageReads, formatterFault.TextReads).ShouldBe((0, 0));
        foreach (var issue in issues) {
            issue.Calls.ShouldBe(1);
        }
    }

    [Fact]
    public void IssueFormattingUsesTheCurrentCulture() {
        using var culture = new CultureScope("de-DE");

        var failure = Should.Throw<FailException>(() => Validation<int, double>.Invalid(2.5).ShouldBeValid()).ShouldBeOfType<FailException>();

        failure.Message.ShouldBe("Expected Validation.Valid; actual Validation.Invalid; issues=1 [0=2,5].");
    }

    [Fact]
    public void ShouldBeInvalid_OnValidFailsWithTheActivePayload() {
        var failure = Should.Throw<FailException>(() => Validation<Probe?, string>.Valid(null).ShouldBeInvalid()).ShouldBeOfType<FailException>();

        failure.Message.ShouldBe("Expected Validation.Invalid; actual Validation.Valid(null).");
    }

    [Fact]
    public void Uninitialized_FailsAsUninitializedWithoutPayloadEffects() {
        ValueProbe.Reset();

        var valid = Should.Throw<FailException>(() => default(Validation<ValueProbe, ValueProbe>).ShouldBeValid()).ShouldBeOfType<FailException>();
        var invalid = Should.Throw<FailException>(() => default(Validation<ValueProbe, ValueProbe>).ShouldBeInvalid()).ShouldBeOfType<FailException>();

        valid.Message.ShouldBe("Expected Validation.Valid; actual Validation.Uninitialized.");
        invalid.Message.ShouldBe("Expected Validation.Invalid; actual Validation.Uninitialized.");
        ValueProbe.Calls.ShouldBe(0);
    }

    [Fact]
    public void FailureStopsTheCurrentLambda() {
        var continued = false;

        Should.Throw<FailException>(() => {
            _ = Validation<int, string>.Valid(1).ShouldBeInvalid();
            continued = true;
        }).ShouldBeOfType<FailException>();

        continued.ShouldBeFalse();
    }

    [Fact]
    public void FailureKeepsTheCallerAsTheTopFrame() {
        var failure = Should.Throw<FailException>(ExtractFromValid).ShouldBeOfType<FailException>();

        var stack = failure.StackTrace ?? string.Empty;
        stack.ShouldStartWith($"   at {typeof(ValidationAssertionsTests).FullName}.{nameof(ExtractFromValid)}()", Case.Sensitive);
        stack.ShouldNotContain($"{typeof(ValidationAssertions).FullName}.", Case.Sensitive);
    }

    private static void ExtractFromValid() => Validation<int, string>.Valid(1).ShouldBeInvalid();
}
