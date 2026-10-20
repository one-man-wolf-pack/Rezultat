namespace Rezultat.Tests;

public sealed class MaybeFallbackTests
{
    [Fact]
    public void ValueOr_returns_payload_for_some_and_fallback_for_none()
    {
        Assert.Equal(1, Maybe<int>.Some(1).ValueOr(2));
        Assert.Equal(2, Maybe<int>.None.ValueOr(2));
        Assert.Equal(2, default(Maybe<int>).ValueOr(2));
    }

    [Fact]
    public void Present_null_empty_nullable_and_nested_none_do_not_trigger_fallback()
    {
        Assert.Null(Maybe<string?>.Some(null).ValueOr("fallback"));
        Assert.Null(Maybe<int?>.Some(null).ValueOr(5));
        Assert.False(Maybe<Maybe<int>>.Some(Maybe<int>.None).ValueOr(Maybe<int>.Some(1)).IsSome);
        Assert.Null(Maybe<string?>.Some(null).ValueOrElse(static () => "fallback"));
    }

    [Fact]
    public void Null_fallback_is_kept_for_nullable_payload()
    {
        Assert.Null(Maybe<string?>.None.ValueOr(null));
        Assert.Null(Maybe<string?>.None.ValueOrElse(static () => null));
    }

    [Fact]
    public void ValueOr_receives_an_eagerly_evaluated_argument()
    {
        var evaluations = 0;

        Assert.Equal(1, Maybe<int>.Some(1).ValueOr(++evaluations));
        Assert.Equal(1, evaluations);
    }

    [Fact]
    public void ValueOrElse_invokes_factory_once_only_for_none()
    {
        var calls = 0;

        Assert.Equal(1, Maybe<int>.Some(1).ValueOrElse(() => ++calls + 10));
        Assert.Equal(0, calls);
        Assert.Equal(11, Maybe<int>.None.ValueOrElse(() => ++calls + 10));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void ValueOrElse_rejects_null_factory_even_for_some()
    {
        Assert.Equal("fallback", Assert.Throws<ArgumentNullException>(() => Maybe<int>.Some(1).ValueOrElse(null!)).ParamName);
        Assert.Equal("fallback", Assert.Throws<ArgumentNullException>(() => Maybe<int>.None.ValueOrElse(null!)).ParamName);
    }

    [Fact]
    public void Factory_exceptions_propagate_unchanged()
    {
        var fault = new ProbeException("fallback");
        var canceled = new OperationCanceledException();

        Assert.Same(fault, Assert.Throws<ProbeException>(() => Maybe<int>.None.ValueOrElse(() => throw fault)));
        Assert.Same(canceled, Assert.Throws<OperationCanceledException>(() => Maybe<int>.None.ValueOrElse(() => throw canceled)));
    }
}
