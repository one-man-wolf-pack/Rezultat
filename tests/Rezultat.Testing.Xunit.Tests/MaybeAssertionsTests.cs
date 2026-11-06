namespace Rezultat.Testing.Xunit.Tests;

public sealed class MaybeAssertionsTests {
    public enum Form {
        WithoutFlag,
        IncludePayload,
        ExcludePayload,
    }

    public static TheoryData<Form> Forms() => [.. Enum.GetValues<Form>()];

    [Theory]
    [MemberData(nameof(Forms))]
    public void ShouldBeSome_ReturnsTheExactReferenceWithoutTouchingIt(Form form) {
        var payload = new Probe("payload");

        Some(Maybe<Probe>.Some(payload), form).ShouldBeSameAs(payload);

        payload.Calls.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public void ShouldBeSome_ReturnsValueNullAndDefaultPayloads(Form form) {
        Some(Maybe<int>.Some(42), form).ShouldBe(42);
        Some(Maybe<int>.Some(0), form).ShouldBe(0);
        Some(Maybe<string?>.Some(null), form).ShouldBeNull();
        Some(Maybe<int?>.Some(null), form).ShouldBeNull();
        Some(Maybe<Unit>.Some(Unit.Value), form).ShouldBe(Unit.Value);
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public void ValuePayloadsAreNotTouchedOnSuccess(Form form) {
        ValueProbe.Reset();

        var extracted = Some(Maybe<ValueProbe>.Some(new ValueProbe(1)), form);
        None(Maybe<ValueProbe>.None, form);
        None(default(Maybe<ValueProbe>), form);

        ValueProbe.Calls.ShouldBe(0);
        extracted.Id.ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public void ShouldBeNone_PassesForNoneAndDefault(Form form) {
        Should.NotThrow(() => None(Maybe<Probe>.None, form));
        Should.NotThrow(() => None(default(Maybe<Probe>), form));
        Should.NotThrow(() => None(new Maybe<string?>(), form));
    }

    [Theory]
    [MemberData(nameof(Forms))]
    public void ShouldBeSome_OnNoneOrDefaultFailsWithoutPayloadInEveryForm(Form form) {
        ValueProbe.Reset();

        var none = Should.Throw<FailException>(() => Some(Maybe<ValueProbe>.None, form)).ShouldBeOfType<FailException>();
        var unset = Should.Throw<FailException>(() => Some(default(Maybe<ValueProbe>), form)).ShouldBeOfType<FailException>();

        none.Message.ShouldBe("Expected Maybe.Some; actual Maybe.None.");
        unset.Message.ShouldBe("Expected Maybe.Some; actual Maybe.None.");
        ValueProbe.Calls.ShouldBe(0);
    }

    [Theory]
    [InlineData(Form.WithoutFlag)]
    [InlineData(Form.IncludePayload)]
    public void ShouldBeNone_OnSomeShowsThePayload(Form form) {
        var some = Should.Throw<FailException>(() => None(Maybe<string>.Some("found"), form)).ShouldBeOfType<FailException>();
        var someNull = Should.Throw<FailException>(() => None(Maybe<string?>.Some(null), form)).ShouldBeOfType<FailException>();

        some.Message.ShouldBe("Expected Maybe.None; actual Maybe.Some(found).");
        someNull.Message.ShouldBe("Expected Maybe.None; actual Maybe.Some(null).");
    }

    [Fact]
    public void ShouldBeNone_WithoutFlagFailsExactlyLikeIncludePayloadTrue() {
        var withoutFlag = Should.Throw<FailException>(() => Maybe<Probe>.Some(new Probe("payload")).ShouldBeNone()).ShouldBeOfType<FailException>();
        var included = Should.Throw<FailException>(() => Maybe<Probe>.Some(new Probe("payload")).ShouldBeNone(includePayload: true)).ShouldBeOfType<FailException>();

        included.Message.ShouldBe(withoutFlag.Message);
    }

    [Fact]
    public void ShouldBeNone_ExcludedPayloadNamesOnlyTheBranches() {
        const string Secret = "secret-sentinel";
        var payload = new Probe(Secret) { Throws = new InvalidOperationException(Secret) };
        ValueProbe.Reset();

        var reference = Should.Throw<FailException>(() => Maybe<Probe>.Some(payload).ShouldBeNone(includePayload: false)).ShouldBeOfType<FailException>();
        var someNull = Should.Throw<FailException>(() => Maybe<string?>.Some(null).ShouldBeNone(includePayload: false)).ShouldBeOfType<FailException>();
        var value = Should.Throw<FailException>(() => Maybe<ValueProbe>.Some(new ValueProbe(7)).ShouldBeNone(includePayload: false)).ShouldBeOfType<FailException>();

        foreach (var failure in new[] { reference, someNull, value }) {
            failure.Message.ShouldBe("Expected Maybe.None; actual Maybe.Some.");
        }

        payload.Calls.ShouldBe(0);
        ValueProbe.Calls.ShouldBe(0);
    }

    [Fact]
    public void ThrowingFormatterIsMarkedByTypeWithoutReadingTheException() {
        var formatterFault = new PoisonException();
        var payload = new Probe { Throws = formatterFault };

        var failure = Should.Throw<FailException>(() => Maybe<Probe>.Some(payload).ShouldBeNone()).ShouldBeOfType<FailException>();

        failure.Message.ShouldBe($"Expected Maybe.None; actual Maybe.Some(<ToString threw {typeof(PoisonException).FullName}>).");
        (formatterFault.MessageReads, formatterFault.TextReads).ShouldBe((0, 0));
    }

    [Fact]
    public void CancellationFromTheFormatterIsOnlyADiagnosticMarker() {
        var payload = new Probe { Throws = new OperationCanceledException() };

        var failure = Should.Throw<FailException>(() => Maybe<Probe>.Some(payload).ShouldBeNone(includePayload: true)).ShouldBeOfType<FailException>();

        failure.Message.ShouldBe("Expected Maybe.None; actual Maybe.Some(<ToString threw System.OperationCanceledException>).");
    }

    [Fact]
    public void StaticOwnerCallFormTakesBothForms() {
        MaybeAssertions.ShouldBeSome(Maybe<int>.Some(5)).ShouldBe(5);
        MaybeAssertions.ShouldBeSome(Maybe<int>.Some(6), includePayload: false).ShouldBe(6);
        MaybeAssertions.ShouldBeNone(Maybe<int>.None);
        MaybeAssertions.ShouldBeNone(Maybe<int>.None, includePayload: false);
    }

    [Fact]
    public void FailureStopsTheCurrentLambda() {
        bool continued = false;

        Should.Throw<FailException>(() => {
            _ = Maybe<int>.None.ShouldBeSome(includePayload: false);
            continued = true;
        }).ShouldBeOfType<FailException>();

        continued.ShouldBeFalse();
    }

    [Fact]
    public void LaterUserAssertionFailureIsNotIntercepted() {
        var failure = Should.Throw<Exception>(() => {
            int value = Maybe<int>.Some(2).ShouldBeSome();
            // Native xUnit assertion on purpose: its EqualException is the later user failure under test.
            Assert.Equal(1, value);
        });

        failure.ShouldBeOfType<EqualException>();
    }

    [Theory]
    [InlineData(nameof(ExtractFromNone))]
    [InlineData(nameof(RejectSome))]
    [InlineData(nameof(RejectSomeWithoutPayload))]
    public void FailureKeepsTheCallerAsTheTopFrame(string caller) {
        Action act = caller switch {
            nameof(ExtractFromNone) => ExtractFromNone,
            nameof(RejectSome) => RejectSome,
            _ => RejectSomeWithoutPayload,
        };

        var failure = Should.Throw<FailException>(act).ShouldBeOfType<FailException>();

        string stack = failure.StackTrace ?? string.Empty;
        stack.ShouldStartWith($"   at {typeof(MaybeAssertionsTests).FullName}.{caller}()", Case.Sensitive);
        stack.ShouldNotContain($"{typeof(MaybeAssertions).FullName}.", Case.Sensitive);
    }

    private static void ExtractFromNone() => Maybe<int>.None.ShouldBeSome();

    private static void RejectSome() => Maybe<int>.Some(1).ShouldBeNone();

    private static void RejectSomeWithoutPayload() => Maybe<int>.Some(1).ShouldBeNone(includePayload: false);

    private static T Some<T>(Maybe<T> value, Form form) => form switch {
        Form.WithoutFlag => value.ShouldBeSome(),
        _ => value.ShouldBeSome(form == Form.IncludePayload),
    };

    private static void None<T>(Maybe<T> value, Form form) {
        if (form == Form.WithoutFlag) {
            value.ShouldBeNone();
        } else {
            value.ShouldBeNone(form == Form.IncludePayload);
        }
    }
}
