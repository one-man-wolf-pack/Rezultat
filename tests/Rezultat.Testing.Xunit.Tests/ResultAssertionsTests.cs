namespace Rezultat.Testing.Xunit.Tests;

public sealed class ResultAssertionsTests
{
    [Fact]
    public void ShouldBeOk_ReturnsTheExactSuccessReferenceWithoutTouchingIt()
    {
        var payload = new Probe();
        var error = new Probe();

        var extracted = Result<Probe, Probe>.Ok(payload).ShouldBeOk();

        Assert.Same(payload, extracted);
        Assert.Equal(0, payload.Calls + error.Calls);
    }

    [Fact]
    public void ShouldBeOk_ReturnsValueUnitAndNullPayloads()
    {
        Assert.Equal(42, Result<int, string>.Ok(42).ShouldBeOk());
        Assert.Equal(Unit.Value, Result<Unit, string>.Ok(Unit.Value).ShouldBeOk());
        Assert.Null(Result<string?, string>.Ok(null).ShouldBeOk());
        Assert.Null(Result<int?, string>.Ok(null).ShouldBeOk());
    }

    [Fact]
    public void ShouldBeError_ReturnsTheExactErrorReferenceWithoutTouchingIt()
    {
        var error = new Probe();

        var extracted = Result<int, Probe>.Error(error).ShouldBeError();

        Assert.Same(error, extracted);
        Assert.Equal(0, error.Calls);
    }

    [Fact]
    public void ShouldBeError_ReturnsValueAndNullErrors()
    {
        Assert.Equal(7, Result<string, int>.Error(7).ShouldBeError());
        Assert.Null(Result<int, string?>.Error(null).ShouldBeError());
    }

    [Fact]
    public void ValuePayloadsAreNotTouchedOnSuccess()
    {
        ValueProbe.Reset();

        var ok = Result<ValueProbe, ValueProbe>.Ok(new ValueProbe(1)).ShouldBeOk();
        var error = Result<ValueProbe, ValueProbe>.Error(new ValueProbe(2)).ShouldBeError();

        Assert.Equal(0, ValueProbe.Calls);
        Assert.Equal((1, 2), (ok.Id, error.Id));
    }

    [Fact]
    public void ShouldBeOk_OnErrorFailsWithTheActiveError()
    {
        var failure = Assert.Throws<FailException>(() => Result<int, string>.Error("server refused").ShouldBeOk());

        Assert.Equal("Expected Result.Ok; actual Result.Error(server refused).", failure.Message);
    }

    [Fact]
    public void ShouldBeError_OnOkFailsWithTheActivePayload()
    {
        var failure = Assert.Throws<FailException>(() => Result<int, string>.Ok(42).ShouldBeError());

        Assert.Equal("Expected Result.Error; actual Result.Ok(42).", failure.Message);
    }

    [Fact]
    public void Uninitialized_FailsAsUninitializedWithoutPayloadEffects()
    {
        ValueProbe.Reset();

        var ok = Assert.Throws<FailException>(() => default(Result<ValueProbe, ValueProbe>).ShouldBeOk());
        var error = Assert.Throws<FailException>(() => default(Result<ValueProbe, ValueProbe>).ShouldBeError());

        Assert.Equal("Expected Result.Ok; actual Result.Uninitialized.", ok.Message);
        Assert.Equal("Expected Result.Error; actual Result.Uninitialized.", error.Message);
        Assert.Equal(0, ValueProbe.Calls);
    }

    [Fact]
    public void FailureStopsTheCurrentLambda()
    {
        var continued = false;

        Assert.Throws<FailException>(() =>
        {
            _ = Result<int, string>.Error("stop").ShouldBeOk();
            continued = true;
        });

        Assert.False(continued);
    }

    [Theory]
    [InlineData(false, null, "")]
    [InlineData(false, "line one\nline two", "line one\nline two")]
    [InlineData(false, "null", "null")]
    [InlineData(true, null, "null")]
    public void FailureShowsThePayloadThroughItsOwnText(bool nullPayload, string? text, string expected)
    {
        var payload = nullPayload ? null : new Probe(text);

        var failure = Assert.Throws<FailException>(() => Result<int, Probe?>.Error(payload).ShouldBeOk());

        Assert.Equal($"Expected Result.Ok; actual Result.Error({expected}).", failure.Message);
    }

    [Fact]
    public void FailureFormatsWithTheCurrentCulture()
    {
        using var culture = new CultureScope("de-DE");

        var failure = Assert.Throws<FailException>(() => Result<double, string>.Ok(1.5).ShouldBeError());

        Assert.Equal("Expected Result.Error; actual Result.Ok(1,5).", failure.Message);
    }

    [Fact]
    public void ThrowingFormatterIsMarkedByTypeWithoutReadingTheException()
    {
        var formatterFault = new PoisonException();
        var payload = new Probe { Throws = formatterFault };

        var failure = Assert.Throws<FailException>(() => Result<Probe, int>.Ok(payload).ShouldBeError());

        Assert.Equal(
            $"Expected Result.Error; actual Result.Ok(<ToString threw {typeof(PoisonException).FullName}>).",
            failure.Message);
        Assert.Equal((0, 0), (formatterFault.MessageReads, formatterFault.TextReads));
    }

    [Fact]
    public void CancellationFromTheFormatterIsOnlyADiagnosticMarker()
    {
        var payload = new Probe { Throws = new OperationCanceledException() };

        var failure = Assert.Throws<FailException>(() => Result<int, Probe>.Error(payload).ShouldBeOk());

        Assert.Equal("Expected Result.Ok; actual Result.Error(<ToString threw System.OperationCanceledException>).", failure.Message);
    }

    [Fact]
    public void LaterUserAssertionFailureIsNotIntercepted()
    {
        var failure = Assert.ThrowsAny<Exception>(() =>
        {
            var value = Result<int, string>.Ok(2).ShouldBeOk();
            Assert.Equal(1, value);
        });

        Assert.IsType<EqualException>(failure);
    }

    [Fact]
    public void FailureKeepsTheCallerAsTheTopFrame()
    {
        var failure = Assert.Throws<FailException>(ExtractFromError);

        var stack = failure.StackTrace ?? string.Empty;
        Assert.StartsWith($"   at {typeof(ResultAssertionsTests).FullName}.{nameof(ExtractFromError)}()", stack, StringComparison.Ordinal);
        Assert.DoesNotContain($"{typeof(ResultAssertions).FullName}.", stack, StringComparison.Ordinal);
    }

    private static void ExtractFromError() => Result<int, string>.Error("caller").ShouldBeOk();
}
