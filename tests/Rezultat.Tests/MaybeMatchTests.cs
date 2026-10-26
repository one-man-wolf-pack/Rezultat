namespace Rezultat.Tests;

public sealed class MaybeMatchTests {
    [Fact]
    public void Some_invokes_only_on_some_once_with_exact_payload() {
        var payload = new ReferenceProbe(1);
        int someCalls = 0;
        int noneCalls = 0;

        var result = Maybe<ReferenceProbe>.Some(payload).Match<ReferenceProbe?>(
            value => {
                someCalls++;
                return value;
            },
            () => {
                noneCalls++;
                return null;
            });

        result.ShouldBeSameAs(payload);
        (someCalls, noneCalls).ShouldBe((1, 0));
    }

    [Fact]
    public void None_and_default_invoke_only_on_none_once() {
        foreach (var maybe in new[] { Maybe<int>.None, default }) {
            int someCalls = 0;
            int noneCalls = 0;

            string result = maybe.Match(
                _ => {
                    someCalls++;
                    return "some";
                },
                () => {
                    noneCalls++;
                    return "none";
                });

            result.ShouldBe("none");
            (someCalls, noneCalls).ShouldBe((0, 1));
        }
    }

    [Fact]
    public void Present_null_selects_on_some() {
        Maybe<string?>.Some(null).Match(value => value ?? "null", () => "none").ShouldBe("null");
        Maybe<int?>.Some(null).Match(value => value.HasValue ? "value" : "empty", () => "none").ShouldBe("empty");
    }

    [Fact]
    public void Result_may_be_null() {
        Maybe<int>.None.Match<string?>(_ => "some", () => null).ShouldBeNull();
    }

    [Fact]
    public void Null_handlers_are_rejected_regardless_of_alternative() {
        Func<int, int> onSome = static value => value;
        Func<int> onNone = static () => 0;

        Should.Throw<ArgumentNullException>(() => Maybe<int>.None.Match(null!, onNone)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("onSome");
        Should.Throw<ArgumentNullException>(() => Maybe<int>.Some(1).Match(onSome, null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("onNone");
        Should.Throw<ArgumentNullException>(() => Maybe<int>.Some(1).Match<int>(null!, null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("onSome");
    }

    [Fact]
    public void Null_handler_check_precedes_invoking_the_other_handler() {
        int calls = 0;

        Should.Throw<ArgumentNullException>(() => Maybe<int>.Some(1).Match(_ => ++calls, null!)).ShouldBeOfType<ArgumentNullException>();
        Should.Throw<ArgumentNullException>(() => Maybe<int>.None.Match(null!, () => ++calls)).ShouldBeOfType<ArgumentNullException>();
        calls.ShouldBe(0);
    }

    [Fact]
    public void Handler_exceptions_propagate_unchanged() {
        var fault = new ProbeException("handler");
        var canceled = new OperationCanceledException();

        Should.Throw<ProbeException>(() => Maybe<int>.Some(1).Match<int>(_ => throw fault, () => 0)).ShouldBeSameAs(fault);
        Should.Throw<OperationCanceledException>(() => Maybe<int>.None.Match<int>(_ => 0, () => throw canceled)).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(canceled);
    }
}
