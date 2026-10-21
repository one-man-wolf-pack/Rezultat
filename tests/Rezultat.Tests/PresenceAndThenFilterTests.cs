namespace Rezultat.Tests;

public sealed class PresenceAndThenFilterTests
{
    [Fact]
    public void AndThen_on_value_invokes_next_once_and_returns_its_exact_outcome()
    {
        var payload = new ReferenceProbe(1);
        var result = new ReferenceProbe(2);
        var seen = new List<ReferenceProbe>();

        var next = Presence<ReferenceProbe>.Value(payload).AndThen(value => { seen.Add(value); return Presence<ReferenceProbe>.Value(result); });

        Assert.Same(payload, Assert.Single(seen));
        Assert.True(next.TryGetValue(out var value));
        Assert.Same(result, value);
        Assert.True(Presence<int>.Value(1).AndThen(static _ => Presence<string>.Null).IsNull);
        Assert.True(Presence<int>.Value(1).AndThen(static _ => Presence<string>.Missing).IsMissing);
    }

    [Fact]
    public void AndThen_default_next_result_is_missing()
    {
        Assert.True(Presence<int>.Value(1).AndThen(static _ => default(Presence<string>)).IsMissing);
    }

    [Fact]
    public void AndThen_keeps_missing_and_null_in_the_new_type_without_invoking_next()
    {
        var calls = 0;

        Assert.True(Presence<int>.Missing.AndThen(_ => { calls++; return Presence<string>.Value("x"); }).IsMissing);
        Assert.True(default(Presence<int>).AndThen(_ => { calls++; return Presence<string>.Value("x"); }).IsMissing);
        Assert.True(Presence<int>.Null.AndThen(_ => { calls++; return Presence<string>.Value("x"); }).IsNull);
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("value")]
    public void Null_callbacks_are_rejected_in_every_state(string state)
    {
        var presence = state switch { "value" => Presence<int>.Value(1), "null" => Presence<int>.Null, _ => Presence<int>.Missing };

        Assert.Equal("next", Assert.Throws<ArgumentNullException>(() => presence.AndThen<int>(null!)).ParamName);
        Assert.Equal("predicate", Assert.Throws<ArgumentNullException>(() => presence.FilterToMissing(null!)).ParamName);
        Assert.Equal("predicate", Assert.Throws<ArgumentNullException>(() => presence.FilterToNull(null!)).ParamName);
    }

    [Fact]
    public void Filters_keep_value_when_the_predicate_holds()
    {
        var payload = new ReferenceProbe(3);
        var seen = new List<ReferenceProbe>();

        var toMissing = Presence<ReferenceProbe>.Value(payload).FilterToMissing(value => { seen.Add(value); return true; });
        var toNull = Presence<ReferenceProbe>.Value(payload).FilterToNull(value => { seen.Add(value); return true; });

        Assert.Equal([payload, payload], seen);
        Assert.True(toMissing.TryGetValue(out var kept) && ReferenceEquals(payload, kept));
        Assert.True(toNull.TryGetValue(out kept) && ReferenceEquals(payload, kept));
        Assert.Equal(0, payload.EqualsCalls + payload.HashCalls + payload.ToStringCalls);
    }

    [Fact]
    public void Filters_return_the_state_named_by_the_method_when_the_predicate_fails()
    {
        Assert.True(Presence<int>.Value(1).FilterToMissing(static _ => false).IsMissing);
        Assert.True(Presence<int>.Value(1).FilterToNull(static _ => false).IsNull);
    }

    [Fact]
    public void Filters_keep_missing_and_null_without_invoking_the_predicate()
    {
        var calls = 0;

        Assert.True(Presence<int>.Missing.FilterToNull(_ => ++calls > 0).IsMissing);
        Assert.True(default(Presence<int>).FilterToNull(_ => ++calls > 0).IsMissing);
        Assert.True(Presence<int>.Null.FilterToMissing(_ => ++calls > 0).IsNull);
        Assert.True(Presence<int>.Null.FilterToNull(_ => ++calls < 0).IsNull);
        Assert.True(Presence<int>.Missing.FilterToMissing(_ => ++calls < 0).IsMissing);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Filter_returns_the_snapshot_taken_before_the_predicate_reassigned_the_receiver()
    {
        var receiver = Presence<int>.Value(1);

        var toMissing = receiver.FilterToMissing(_ => { receiver = Presence<int>.Value(99); return true; });
        receiver = Presence<int>.Value(1);
        var toNull = receiver.FilterToNull(_ => { receiver = Presence<int>.Null; return true; });

        Assert.Equal(Presence<int>.Value(1), toMissing);
        Assert.Equal(Presence<int>.Value(1), toNull);
        Assert.True(receiver.IsNull);
    }

    [Fact]
    public void AndThen_uses_the_value_observed_before_next_reassigned_the_receiver()
    {
        var receiver = Presence<int>.Value(1);
        var seen = new List<int>();

        var next = receiver.AndThen(value =>
        {
            receiver = Presence<int>.Value(99);
            seen.Add(value);
            return Presence<int>.Value(value + 1);
        });

        Assert.Equal([1], seen);
        Assert.Equal(Presence<int>.Value(2), next);
    }

    [Fact]
    public void Callback_exceptions_and_cancellation_propagate_unchanged()
    {
        var invalid = new InvalidOperationException("callback");
        var canceled = new OperationCanceledException();
        var value = Presence<int>.Value(1);

        Assert.Same(invalid, Assert.Throws<InvalidOperationException>(() => value.AndThen<int>(_ => throw invalid)));
        Assert.Same(canceled, Assert.Throws<OperationCanceledException>(() => value.FilterToMissing(_ => throw canceled)));
        Assert.Same(invalid, Assert.Throws<InvalidOperationException>(() => value.FilterToNull(_ => throw invalid)));
    }

    [Fact]
    public void Nested_carriers_and_tasks_are_ordinary_payloads()
    {
        var nested = Presence<int>.Value(1).AndThen(static _ => Presence<Presence<int>>.Value(Presence<int>.Missing));
        var task = Presence<int>.Value(1).AndThen(static _ => Presence<Task<int>>.Value(Task.FromException<int>(new ProbeException("task"))));

        Assert.True(nested.TryGetValue(out var inner) && inner.IsMissing);
        Assert.True(task.TryGetValue(out var pending) && pending.IsFaulted);
        Assert.True(Presence<Presence<int>>.Value(Presence<int>.Null).FilterToMissing(static inner => inner.IsNull).IsValue);
    }

    [Fact]
    public void Mutable_reference_payload_is_shared_not_frozen()
    {
        var payload = new List<int> { 1 };

        var kept = Presence<List<int>>.Value(payload).FilterToNull(list => { list.Add(2); return true; });

        Assert.True(kept.TryGetValue(out var value));
        Assert.Same(payload, value);
        Assert.Equal([1, 2], value);
    }

    [Fact]
    public void Receiver_is_not_changed()
    {
        var presence = Presence<int>.Value(1);

        _ = presence.AndThen(static _ => Presence<int>.Null);
        _ = presence.FilterToMissing(static _ => false);
        _ = presence.FilterToNull(static _ => false);

        Assert.Equal(Presence<int>.Value(1), presence);
    }
}
