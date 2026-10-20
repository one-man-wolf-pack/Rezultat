namespace Rezultat.Tests;

public sealed class MaybeMatchTests
{
    [Fact]
    public void Some_invokes_only_on_some_once_with_exact_payload()
    {
        var payload = new ReferenceProbe(1);
        var someCalls = 0;
        var noneCalls = 0;

        var result = Maybe<ReferenceProbe>.Some(payload).Match<ReferenceProbe?>(
            value =>
            {
                someCalls++;
                return value;
            },
            () =>
            {
                noneCalls++;
                return null;
            });

        Assert.Same(payload, result);
        Assert.Equal((1, 0), (someCalls, noneCalls));
    }

    [Fact]
    public void None_and_default_invoke_only_on_none_once()
    {
        foreach (var maybe in new[] { Maybe<int>.None, default })
        {
            var someCalls = 0;
            var noneCalls = 0;

            var result = maybe.Match(
                _ =>
                {
                    someCalls++;
                    return "some";
                },
                () =>
                {
                    noneCalls++;
                    return "none";
                });

            Assert.Equal("none", result);
            Assert.Equal((0, 1), (someCalls, noneCalls));
        }
    }

    [Fact]
    public void Present_null_selects_on_some()
    {
        Assert.Equal("null", Maybe<string?>.Some(null).Match(value => value ?? "null", () => "none"));
        Assert.Equal("empty", Maybe<int?>.Some(null).Match(value => value.HasValue ? "value" : "empty", () => "none"));
    }

    [Fact]
    public void Result_may_be_null()
    {
        Assert.Null(Maybe<int>.None.Match<string?>(_ => "some", () => null));
    }

    [Fact]
    public void Null_handlers_are_rejected_regardless_of_alternative()
    {
        Func<int, int> onSome = static value => value;
        Func<int> onNone = static () => 0;

        Assert.Equal("onSome", Assert.Throws<ArgumentNullException>(() => Maybe<int>.None.Match(null!, onNone)).ParamName);
        Assert.Equal("onNone", Assert.Throws<ArgumentNullException>(() => Maybe<int>.Some(1).Match(onSome, null!)).ParamName);
        Assert.Equal("onSome", Assert.Throws<ArgumentNullException>(() => Maybe<int>.Some(1).Match<int>(null!, null!)).ParamName);
    }

    [Fact]
    public void Null_handler_check_precedes_invoking_the_other_handler()
    {
        var calls = 0;

        Assert.Throws<ArgumentNullException>(() => Maybe<int>.Some(1).Match(_ => ++calls, null!));
        Assert.Throws<ArgumentNullException>(() => Maybe<int>.None.Match(null!, () => ++calls));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Handler_exceptions_propagate_unchanged()
    {
        var fault = new ProbeException("handler");
        var canceled = new OperationCanceledException();

        Assert.Same(fault, Assert.Throws<ProbeException>(() => Maybe<int>.Some(1).Match<int>(_ => throw fault, () => 0)));
        Assert.Same(canceled, Assert.Throws<OperationCanceledException>(() => Maybe<int>.None.Match<int>(_ => 0, () => throw canceled)));
    }
}
