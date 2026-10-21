namespace Rezultat.Tests;

public sealed class PresenceTraverseTests
{
    [Fact]
    public void Missing_and_null_become_ok_with_the_same_state_without_invoking_the_selector()
    {
        var calls = 0;

        Assert.Equal(Result<Presence<string>, int>.Ok(Presence<string>.Missing), Presence<int>.Missing.Traverse(_ => { calls++; return Result<string, int>.Ok("x"); }));
        Assert.Equal(Result<Presence<string>, int>.Ok(Presence<string>.Missing), default(Presence<int>).Traverse(_ => { calls++; return Result<string, int>.Ok("x"); }));
        Assert.Equal(Result<Presence<string>, int>.Ok(Presence<string>.Null), Presence<int>.Null.Traverse(_ => { calls++; return Result<string, int>.Ok("x"); }));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Value_invokes_the_selector_once_and_wraps_its_ok_value()
    {
        var payload = new ReferenceProbe(1);
        var result = new ReferenceProbe(2);
        var seen = new List<ReferenceProbe>();

        var traversed = Presence<ReferenceProbe>.Value(payload).Traverse(value => { seen.Add(value); return Result<ReferenceProbe, string>.Ok(result); });

        Assert.Same(payload, Assert.Single(seen));
        Assert.True(traversed.TryGetValue(out var presence, out _));
        Assert.True(presence.TryGetValue(out var value));
        Assert.Same(result, value);
    }

    [Fact]
    public void Value_returns_the_selector_error_as_is_including_null()
    {
        var error = new ReferenceProbe(3);

        var failed = Presence<int>.Value(1).Traverse(_ => Result<string, ReferenceProbe>.Error(error));
        var nullError = Presence<int>.Value(1).Traverse(static _ => Result<string, string?>.Error(null));

        Assert.False(failed.TryGetValue(out _, out var observed));
        Assert.Same(error, observed);
        Assert.Equal(Result<Presence<string>, string?>.Error(null), nullError);
    }

    [Fact]
    public void Ok_null_from_the_selector_is_an_invalid_operation_not_null_or_missing()
    {
        Assert.Throws<InvalidOperationException>(() => Presence<int>.Value(1).Traverse(static _ => Result<string, int>.Ok(null!)));
    }

#pragma warning disable CS8714 // Bypassed notnull constraint: the runtime guard must hold without the warning.
    [Fact]
    public void Empty_nullable_ok_is_rejected_and_non_empty_one_is_kept()
    {
        Assert.Throws<InvalidOperationException>(() => Presence<int>.Value(1).Traverse(static _ => Result<int?, string>.Ok(null)));

        var kept = Presence<int>.Value(1).Traverse(static value => Result<int?, string>.Ok(value + 1));
        Assert.True(kept.TryGetValue(out var presence, out _) && presence.TryGetValue(out var value) && value == 2);
    }
#pragma warning restore CS8714

    [Fact]
    public void Uninitialized_selector_result_is_an_invalid_operation()
    {
        Assert.Throws<InvalidOperationException>(() => Presence<int>.Value(1).Traverse(static _ => default(Result<int, string>)));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("value")]
    public void Null_selector_is_rejected_in_every_state(string state)
    {
        var presence = state switch { "value" => Presence<int>.Value(1), "null" => Presence<int>.Null, _ => Presence<int>.Missing };

        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => presence.Traverse<int, int, string>(null!)).ParamName);
    }

    [Fact]
    public void Nested_carriers_in_the_ok_value_are_not_checked()
    {
        var nested = Presence<int>.Value(1).Traverse(static _ => Result<Result<int, string>, string>.Ok(default));
        var nestedPresence = Presence<int>.Value(1).Traverse(static _ => Result<Presence<int>, string>.Ok(Presence<int>.Null));

        Assert.True(nested.TryGetValue(out var outer, out _) && outer.TryGetValue(out var inner) && !inner.IsInitialized);
        Assert.True(nestedPresence.TryGetValue(out var wrapper, out _) && wrapper.TryGetValue(out var innerPresence) && innerPresence.IsNull);
    }

    [Fact]
    public void Selector_exceptions_and_cancellation_propagate_unchanged()
    {
        var invalid = new InvalidOperationException("selector");
        var canceled = new OperationCanceledException();

        Assert.Same(invalid, Assert.Throws<InvalidOperationException>(() => Presence<int>.Value(1).Traverse<int, int, string>(_ => throw invalid)));
        Assert.Same(canceled, Assert.Throws<OperationCanceledException>(() => Presence<int>.Value(1).Traverse<int, int, string>(_ => throw canceled)));
    }

    [Fact]
    public void Selector_reassigning_the_source_variable_does_not_change_the_traversed_snapshot()
    {
        var source = Presence<int>.Value(1);
        var seen = new List<int>();

        var traversed = source.Traverse(value =>
        {
            source = Presence<int>.Null;
            seen.Add(value);
            return Result<int, string>.Ok(value + 1);
        });

        Assert.Equal([1], seen);
        Assert.Equal(Result<Presence<int>, string>.Ok(Presence<int>.Value(2)), traversed);
    }
}
