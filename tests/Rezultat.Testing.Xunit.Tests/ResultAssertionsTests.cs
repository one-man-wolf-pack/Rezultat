namespace Rezultat.Testing.Xunit.Tests;

public sealed class ResultAssertionsTests {
    [Fact]
    public void ShouldBeOk_ReturnsTheExactSuccessReferenceWithoutTouchingIt() {
        var payload = new Probe();
        var error = new Probe();

        var extracted = Result<Probe, Probe>.Ok(payload).ShouldBeOk();

        extracted.ShouldBeSameAs(payload);
        (payload.Calls + error.Calls).ShouldBe(0);
    }

    [Fact]
    public void ShouldBeOk_ReturnsValueUnitAndNullPayloads() {
        Result<int, string>.Ok(42).ShouldBeOk().ShouldBe(42);
        Result<Unit, string>.Ok(Unit.Value).ShouldBeOk().ShouldBe(Unit.Value);
        Result<string?, string>.Ok(null).ShouldBeOk().ShouldBeNull();
        Result<int?, string>.Ok(null).ShouldBeOk().ShouldBeNull();
    }

    [Fact]
    public void ShouldBeError_ReturnsTheExactErrorReferenceWithoutTouchingIt() {
        var error = new Probe();

        var extracted = Result<int, Probe>.Error(error).ShouldBeError();

        extracted.ShouldBeSameAs(error);
        error.Calls.ShouldBe(0);
    }

    [Fact]
    public void ShouldBeError_ReturnsValueAndNullErrors() {
        Result<string, int>.Error(7).ShouldBeError().ShouldBe(7);
        Result<int, string?>.Error(null).ShouldBeError().ShouldBeNull();
    }

    [Fact]
    public void ValuePayloadsAreNotTouchedOnSuccess() {
        ValueProbe.Reset();

        var ok = Result<ValueProbe, ValueProbe>.Ok(new ValueProbe(1)).ShouldBeOk();
        var error = Result<ValueProbe, ValueProbe>.Error(new ValueProbe(2)).ShouldBeError();

        ValueProbe.Calls.ShouldBe(0);
        (ok.Id, error.Id).ShouldBe((1, 2));
    }

    [Fact]
    public void ShouldBeOk_OnErrorFailsWithTheActiveError() {
        var failure = Should.Throw<FailException>(() => Result<int, string>.Error("server refused").ShouldBeOk()).ShouldBeOfType<FailException>();

        failure.Message.ShouldBe("Expected Result.Ok; actual Result.Error(server refused).");
    }

    [Fact]
    public void ShouldBeError_OnOkFailsWithTheActivePayload() {
        var failure = Should.Throw<FailException>(() => Result<int, string>.Ok(42).ShouldBeError()).ShouldBeOfType<FailException>();

        failure.Message.ShouldBe("Expected Result.Error; actual Result.Ok(42).");
    }

    [Fact]
    public void Uninitialized_FailsAsUninitializedWithoutPayloadEffects() {
        ValueProbe.Reset();

        var ok = Should.Throw<FailException>(() => default(Result<ValueProbe, ValueProbe>).ShouldBeOk()).ShouldBeOfType<FailException>();
        var error = Should.Throw<FailException>(() => default(Result<ValueProbe, ValueProbe>).ShouldBeError()).ShouldBeOfType<FailException>();

        ok.Message.ShouldBe("Expected Result.Ok; actual Result.Uninitialized.");
        error.Message.ShouldBe("Expected Result.Error; actual Result.Uninitialized.");
        ValueProbe.Calls.ShouldBe(0);
    }

    [Fact]
    public void FailureStopsTheCurrentLambda() {
        bool continued = false;

        Should.Throw<FailException>(() => {
            _ = Result<int, string>.Error("stop").ShouldBeOk();
            continued = true;
        }).ShouldBeOfType<FailException>();

        continued.ShouldBeFalse();
    }

    [Theory]
    [InlineData(false, null, "")]
    [InlineData(false, "line one\nline two", "line one\nline two")]
    [InlineData(false, "null", "null")]
    [InlineData(true, null, "null")]
    public void FailureShowsThePayloadThroughItsOwnText(bool nullPayload, string? text, string expected) {
        var payload = nullPayload ? null : new Probe(text);

        var failure = Should.Throw<FailException>(() => Result<int, Probe?>.Error(payload).ShouldBeOk()).ShouldBeOfType<FailException>();

        failure.Message.ShouldBe($"Expected Result.Ok; actual Result.Error({expected}).");
    }

    [Fact]
    public void FailureFormatsWithTheCurrentCulture() {
        using var culture = new CultureScope("de-DE");

        var failure = Should.Throw<FailException>(() => Result<double, string>.Ok(1.5).ShouldBeError()).ShouldBeOfType<FailException>();

        failure.Message.ShouldBe("Expected Result.Error; actual Result.Ok(1,5).");
    }

    [Fact]
    public void ThrowingFormatterIsMarkedByTypeWithoutReadingTheException() {
        var formatterFault = new PoisonException();
        var payload = new Probe { Throws = formatterFault };

        var failure = Should.Throw<FailException>(() => Result<Probe, int>.Ok(payload).ShouldBeError()).ShouldBeOfType<FailException>();

        failure.Message.ShouldBe($"Expected Result.Error; actual Result.Ok(<ToString threw {typeof(PoisonException).FullName}>).");
        (formatterFault.MessageReads, formatterFault.TextReads).ShouldBe((0, 0));
    }

    [Fact]
    public void CancellationFromTheFormatterIsOnlyADiagnosticMarker() {
        var payload = new Probe { Throws = new OperationCanceledException() };

        var failure = Should.Throw<FailException>(() => Result<int, Probe>.Error(payload).ShouldBeOk()).ShouldBeOfType<FailException>();

        failure.Message.ShouldBe("Expected Result.Ok; actual Result.Error(<ToString threw System.OperationCanceledException>).");
    }

    [Fact]
    public void LaterUserAssertionFailureIsNotIntercepted() {
        var failure = Should.Throw<Exception>(() => {
            int value = Result<int, string>.Ok(2).ShouldBeOk();
            // Native xUnit assertion on purpose: its EqualException is the later user failure under test.
            Assert.Equal(1, value);
        });

        failure.ShouldBeOfType<EqualException>();
    }

    [Fact]
    public void FailureKeepsTheCallerAsTheTopFrame() {
        var failure = Should.Throw<FailException>(ExtractFromError).ShouldBeOfType<FailException>();

        string stack = failure.StackTrace ?? string.Empty;
        stack.ShouldStartWith($"   at {typeof(ResultAssertionsTests).FullName}.{nameof(ExtractFromError)}()", Case.Sensitive);
        stack.ShouldNotContain($"{typeof(ResultAssertions).FullName}.", Case.Sensitive);
    }

    private static void ExtractFromError() => Result<int, string>.Error("caller").ShouldBeOk();
}
