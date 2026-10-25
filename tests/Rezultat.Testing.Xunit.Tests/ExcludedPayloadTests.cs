using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Rezultat.Testing.Xunit.Tests;

public sealed class ExcludedPayloadTests {
    [Fact]
    public void SuccessReturnsExactAndNullablePayloadsWithoutTouchingThem() {
        var payload = new Probe();

        Result<Probe, Probe>.Ok(payload).ShouldBeOk(includePayload: false).ShouldBeSameAs(payload);
        Result<int, Probe>.Error(payload).ShouldBeError(includePayload: false).ShouldBeSameAs(payload);
        Validation<Probe, Probe>.Valid(payload).ShouldBeValid(includePayload: false).ShouldBeSameAs(payload);
        Result<string?, string>.Ok(null).ShouldBeOk(includePayload: false).ShouldBeNull();
        Result<int, string?>.Error(null).ShouldBeError(includePayload: false).ShouldBeNull();
        Validation<int?, string>.Valid(null).ShouldBeValid(includePayload: false).ShouldBeNull();
        payload.Calls.ShouldBe(0);
    }

    [Fact]
    public void ShouldBeInvalidReturnsTheStoredIssueBuffer() {
        var first = new Probe();
        var stored = ImmutableArray.Create<Probe?>(first, null, first);

        var extracted = Validation<int, Probe?>.InvalidMany(stored).ShouldBeInvalid(includePayload: false);

        ImmutableCollectionsMarshal.AsArray(extracted).ShouldBeSameAs(ImmutableCollectionsMarshal.AsArray(stored));
        first.Calls.ShouldBe(0);
    }

    [Fact]
    public void ValuePayloadsAreNotTouchedOnSuccessOrFailure() {
        ValueProbe.Reset();

        _ = Result<ValueProbe, ValueProbe>.Ok(new ValueProbe(1)).ShouldBeOk(includePayload: false);
        _ = Validation<ValueProbe, ValueProbe>.Invalid(new ValueProbe(2)).ShouldBeInvalid(includePayload: false);
        Should.Throw<FailException>(() => Result<ValueProbe, ValueProbe>.Ok(new ValueProbe(3)).ShouldBeError(includePayload: false)).ShouldBeOfType<FailException>();
        Should.Throw<FailException>(() => Validation<ValueProbe, ValueProbe>.InvalidMany([new ValueProbe(4), new ValueProbe(5)]).ShouldBeValid(includePayload: false)).ShouldBeOfType<FailException>();

        ValueProbe.Calls.ShouldBe(0);
    }

    [Fact]
    public void IncludedPayloadKeepsTheRichMessage() {
        var failure = Should.Throw<FailException>(() => Result<int, string>.Error("server refused").ShouldBeOk(includePayload: true)).ShouldBeOfType<FailException>();
        var issues = Should.Throw<FailException>(() => Validation<int, string?>.InvalidMany(["bad", null]).ShouldBeValid(includePayload: true)).ShouldBeOfType<FailException>();

        failure.Message.ShouldContain("server refused", Case.Sensitive);
        issues.Message.ShouldContain("bad", Case.Sensitive);
        issues.Message.ShouldContain("null", Case.Sensitive);
    }

    [Fact]
    public void StaticOwnerCallFormTakesTheFlag() {
        ResultAssertions.ShouldBeOk(Result<int, string>.Ok(5), includePayload: false).ShouldBe(5);
        ValidationAssertions.ShouldBeValid(Validation<int, string>.Valid(6), includePayload: false).ShouldBe(6);
    }

    [Fact]
    public void FailureStopsTheCurrentLambda() {
        var continued = false;

        Should.Throw<FailException>(() => {
            _ = Validation<int, string>.Valid(1).ShouldBeInvalid(includePayload: false);
            continued = true;
        }).ShouldBeOfType<FailException>();

        continued.ShouldBeFalse();
    }

    [Fact]
    public void FailureKeepsTheCallerAsTheTopFrame() {
        var failure = Should.Throw<FailException>(ExtractFromError).ShouldBeOfType<FailException>();

        var stack = failure.StackTrace ?? string.Empty;
        stack.ShouldStartWith($"   at {typeof(ExcludedPayloadTests).FullName}.{nameof(ExtractFromError)}()", Case.Sensitive);
        stack.ShouldNotContain("Rezultat.Testing.Xunit.ResultAssertions.", Case.Sensitive);
        stack.ShouldNotContain("Rezultat.Testing.Xunit.ValidationAssertions.", Case.Sensitive);
    }

    private static void ExtractFromError() => Result<int, string>.Error("caller").ShouldBeOk(includePayload: false);
}
