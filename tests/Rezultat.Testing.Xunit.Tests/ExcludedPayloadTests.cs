using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Rezultat.Testing.Xunit.Tests;

public sealed class ExcludedPayloadTests
{
    [Fact]
    public void SuccessReturnsExactAndNullablePayloadsWithoutTouchingThem()
    {
        var payload = new Probe();

        Assert.Same(payload, Result<Probe, Probe>.Ok(payload).ShouldBeOk(includePayload: false));
        Assert.Same(payload, Result<int, Probe>.Error(payload).ShouldBeError(includePayload: false));
        Assert.Same(payload, Validation<Probe, Probe>.Valid(payload).ShouldBeValid(includePayload: false));
        Assert.Null(Result<string?, string>.Ok(null).ShouldBeOk(includePayload: false));
        Assert.Null(Result<int, string?>.Error(null).ShouldBeError(includePayload: false));
        Assert.Null(Validation<int?, string>.Valid(null).ShouldBeValid(includePayload: false));
        Assert.Equal(0, payload.Calls);
    }

    [Fact]
    public void ShouldBeInvalidReturnsTheStoredIssueBuffer()
    {
        var first = new Probe();
        var stored = ImmutableArray.Create<Probe?>(first, null, first);

        var extracted = Validation<int, Probe?>.InvalidMany(stored).ShouldBeInvalid(includePayload: false);

        Assert.Same(ImmutableCollectionsMarshal.AsArray(stored), ImmutableCollectionsMarshal.AsArray(extracted));
        Assert.Equal(0, first.Calls);
    }

    [Fact]
    public void ValuePayloadsAreNotTouchedOnSuccessOrFailure()
    {
        ValueProbe.Reset();

        _ = Result<ValueProbe, ValueProbe>.Ok(new ValueProbe(1)).ShouldBeOk(includePayload: false);
        _ = Validation<ValueProbe, ValueProbe>.Invalid(new ValueProbe(2)).ShouldBeInvalid(includePayload: false);
        Assert.Throws<FailException>(() => Result<ValueProbe, ValueProbe>.Ok(new ValueProbe(3)).ShouldBeError(includePayload: false));
        Assert.Throws<FailException>(() => Validation<ValueProbe, ValueProbe>.InvalidMany([new ValueProbe(4), new ValueProbe(5)]).ShouldBeValid(includePayload: false));

        Assert.Equal(0, ValueProbe.Calls);
    }

    [Fact]
    public void IncludedPayloadKeepsTheRichMessage()
    {
        var failure = Assert.Throws<FailException>(() => Result<int, string>.Error("server refused").ShouldBeOk(includePayload: true));
        var issues = Assert.Throws<FailException>(() => Validation<int, string?>.InvalidMany(["bad", null]).ShouldBeValid(includePayload: true));

        Assert.Contains("server refused", failure.Message, StringComparison.Ordinal);
        Assert.Contains("bad", issues.Message, StringComparison.Ordinal);
        Assert.Contains("null", issues.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void StaticOwnerCallFormTakesTheFlag()
    {
        Assert.Equal(5, ResultAssertions.ShouldBeOk(Result<int, string>.Ok(5), includePayload: false));
        Assert.Equal(6, ValidationAssertions.ShouldBeValid(Validation<int, string>.Valid(6), includePayload: false));
    }

    [Fact]
    public void FailureStopsTheCurrentLambda()
    {
        var continued = false;

        Assert.Throws<FailException>(() =>
        {
            _ = Validation<int, string>.Valid(1).ShouldBeInvalid(includePayload: false);
            continued = true;
        });

        Assert.False(continued);
    }

    [Fact]
    public void FailureKeepsTheCallerAsTheTopFrame()
    {
        var failure = Assert.Throws<FailException>(ExtractFromError);

        var stack = failure.StackTrace ?? string.Empty;
        Assert.StartsWith($"   at {typeof(ExcludedPayloadTests).FullName}.{nameof(ExtractFromError)}()", stack, StringComparison.Ordinal);
        Assert.DoesNotContain("Rezultat.Testing.Xunit.ResultAssertions.", stack, StringComparison.Ordinal);
        Assert.DoesNotContain("Rezultat.Testing.Xunit.ValidationAssertions.", stack, StringComparison.Ordinal);
    }

    private static void ExtractFromError() => Result<int, string>.Error("caller").ShouldBeOk(includePayload: false);
}
