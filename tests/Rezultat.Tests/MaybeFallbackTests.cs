namespace Rezultat.Tests;

public sealed class MaybeFallbackTests {
    [Fact]
    public void ValueOr_returns_payload_for_some_and_fallback_for_none() {
        Maybe<int>.Some(1).ValueOr(2).ShouldBe(1);
        Maybe<int>.None.ValueOr(2).ShouldBe(2);
        default(Maybe<int>).ValueOr(2).ShouldBe(2);
    }

    [Fact]
    public void Present_null_empty_nullable_and_nested_none_do_not_trigger_fallback() {
        Maybe<string?>.Some(null).ValueOr("fallback").ShouldBeNull();
        Maybe<int?>.Some(null).ValueOr(5).ShouldBeNull();
        Maybe<Maybe<int>>.Some(Maybe<int>.None).ValueOr(Maybe<int>.Some(1)).IsSome.ShouldBeFalse();
        Maybe<string?>.Some(null).ValueOrElse(static () => "fallback").ShouldBeNull();
    }

    [Fact]
    public void Null_fallback_is_kept_for_nullable_payload() {
        Maybe<string?>.None.ValueOr(null).ShouldBeNull();
        Maybe<string?>.None.ValueOrElse(static () => null).ShouldBeNull();
    }

    [Fact]
    public void ValueOr_receives_an_eagerly_evaluated_argument() {
        var evaluations = 0;

        Maybe<int>.Some(1).ValueOr(++evaluations).ShouldBe(1);
        evaluations.ShouldBe(1);
    }

    [Fact]
    public void ValueOrElse_invokes_factory_once_only_for_none() {
        var calls = 0;

        Maybe<int>.Some(1).ValueOrElse(() => ++calls + 10).ShouldBe(1);
        calls.ShouldBe(0);
        Maybe<int>.None.ValueOrElse(() => ++calls + 10).ShouldBe(11);
        calls.ShouldBe(1);
    }

    [Fact]
    public void ValueOrElse_rejects_null_factory_even_for_some() {
        Should.Throw<ArgumentNullException>(() => Maybe<int>.Some(1).ValueOrElse(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("fallback");
        Should.Throw<ArgumentNullException>(() => Maybe<int>.None.ValueOrElse(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("fallback");
    }

    [Fact]
    public void Factory_exceptions_propagate_unchanged() {
        var fault = new ProbeException("fallback");
        var canceled = new OperationCanceledException();

        Should.Throw<ProbeException>(() => Maybe<int>.None.ValueOrElse(() => throw fault)).ShouldBeSameAs(fault);
        Should.Throw<OperationCanceledException>(() => Maybe<int>.None.ValueOrElse(() => throw canceled)).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(canceled);
    }
}
