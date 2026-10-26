namespace Rezultat.Tests;

public sealed class PresenceAndThenFilterTests {
    [Fact]
    public void AndThen_on_value_invokes_next_once_and_returns_its_exact_outcome() {
        var payload = new ReferenceProbe(1);
        var result = new ReferenceProbe(2);
        var seen = new List<ReferenceProbe>();

        var next = Presence<ReferenceProbe>.Value(payload).AndThen(value => { seen.Add(value); return Presence<ReferenceProbe>.Value(result); });

        seen.ShouldHaveSingleItem().ShouldBeSameAs(payload);
        next.TryGetValue(out var value).ShouldBeTrue();
        value.ShouldBeSameAs(result);
        Presence<int>.Value(1).AndThen(static _ => Presence<string>.Null).IsNull.ShouldBeTrue();
        Presence<int>.Value(1).AndThen(static _ => Presence<string>.Missing).IsMissing.ShouldBeTrue();
    }

    [Fact]
    public void AndThen_default_next_result_is_missing() {
        Presence<int>.Value(1).AndThen(static _ => default(Presence<string>)).IsMissing.ShouldBeTrue();
    }

    [Fact]
    public void AndThen_keeps_missing_and_null_in_the_new_type_without_invoking_next() {
        int calls = 0;

        Presence<int>.Missing.AndThen(_ => { calls++; return Presence<string>.Value("x"); }).IsMissing.ShouldBeTrue();
        default(Presence<int>).AndThen(_ => { calls++; return Presence<string>.Value("x"); }).IsMissing.ShouldBeTrue();
        Presence<int>.Null.AndThen(_ => { calls++; return Presence<string>.Value("x"); }).IsNull.ShouldBeTrue();
        calls.ShouldBe(0);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("value")]
    public void Null_callbacks_are_rejected_in_every_state(string state) {
        var presence = state switch { "value" => Presence<int>.Value(1), "null" => Presence<int>.Null, _ => Presence<int>.Missing };

        Should.Throw<ArgumentNullException>(() => presence.AndThen<int>(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("next");
        Should.Throw<ArgumentNullException>(() => presence.FilterToMissing(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("predicate");
        Should.Throw<ArgumentNullException>(() => presence.FilterToNull(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("predicate");
    }

    [Fact]
    public void Filters_keep_value_when_the_predicate_holds() {
        var payload = new ReferenceProbe(3);
        var seen = new List<ReferenceProbe>();

        var toMissing = Presence<ReferenceProbe>.Value(payload).FilterToMissing(value => { seen.Add(value); return true; });
        var toNull = Presence<ReferenceProbe>.Value(payload).FilterToNull(value => { seen.Add(value); return true; });

        seen.ShouldBe([payload, payload]);
        (toMissing.TryGetValue(out var kept) && ReferenceEquals(payload, kept)).ShouldBeTrue();
        (toNull.TryGetValue(out kept) && ReferenceEquals(payload, kept)).ShouldBeTrue();
        (payload.EqualsCalls + payload.HashCalls + payload.ToStringCalls).ShouldBe(0);
    }

    [Fact]
    public void Filters_return_the_state_named_by_the_method_when_the_predicate_fails() {
        Presence<int>.Value(1).FilterToMissing(static _ => false).IsMissing.ShouldBeTrue();
        Presence<int>.Value(1).FilterToNull(static _ => false).IsNull.ShouldBeTrue();
    }

    [Fact]
    public void Filters_keep_missing_and_null_without_invoking_the_predicate() {
        int calls = 0;

        Presence<int>.Missing.FilterToNull(_ => ++calls > 0).IsMissing.ShouldBeTrue();
        default(Presence<int>).FilterToNull(_ => ++calls > 0).IsMissing.ShouldBeTrue();
        Presence<int>.Null.FilterToMissing(_ => ++calls > 0).IsNull.ShouldBeTrue();
        Presence<int>.Null.FilterToNull(_ => ++calls < 0).IsNull.ShouldBeTrue();
        Presence<int>.Missing.FilterToMissing(_ => ++calls < 0).IsMissing.ShouldBeTrue();
        calls.ShouldBe(0);
    }

    [Fact]
    public void Filter_returns_the_snapshot_taken_before_the_predicate_reassigned_the_receiver() {
        var receiver = Presence<int>.Value(1);

        var toMissing = receiver.FilterToMissing(_ => { receiver = Presence<int>.Value(99); return true; });
        receiver = Presence<int>.Value(1);
        var toNull = receiver.FilterToNull(_ => { receiver = Presence<int>.Null; return true; });

        toMissing.ShouldBe(Presence<int>.Value(1));
        toNull.ShouldBe(Presence<int>.Value(1));
        receiver.IsNull.ShouldBeTrue();
    }

    [Fact]
    public void AndThen_uses_the_value_observed_before_next_reassigned_the_receiver() {
        var receiver = Presence<int>.Value(1);
        var seen = new List<int>();

        var next = receiver.AndThen(value => {
            receiver = Presence<int>.Value(99);
            seen.Add(value);
            return Presence<int>.Value(value + 1);
        });

        seen.ShouldBe([1]);
        next.ShouldBe(Presence<int>.Value(2));
    }

    [Fact]
    public void Callback_exceptions_and_cancellation_propagate_unchanged() {
        var invalid = new InvalidOperationException("callback");
        var canceled = new OperationCanceledException();
        var value = Presence<int>.Value(1);

        Should.Throw<InvalidOperationException>(() => value.AndThen<int>(_ => throw invalid)).ShouldBeOfType<InvalidOperationException>().ShouldBeSameAs(invalid);
        Should.Throw<OperationCanceledException>(() => value.FilterToMissing(_ => throw canceled)).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(canceled);
        Should.Throw<InvalidOperationException>(() => value.FilterToNull(_ => throw invalid)).ShouldBeOfType<InvalidOperationException>().ShouldBeSameAs(invalid);
    }

    [Fact]
    public void Nested_carriers_and_tasks_are_ordinary_payloads() {
        var nested = Presence<int>.Value(1).AndThen(static _ => Presence<Presence<int>>.Value(Presence<int>.Missing));
        var task = Presence<int>.Value(1).AndThen(static _ => Presence<Task<int>>.Value(Task.FromException<int>(new ProbeException("task"))));

        (nested.TryGetValue(out var inner) && inner.IsMissing).ShouldBeTrue();
        (task.TryGetValue(out var pending) && pending.IsFaulted).ShouldBeTrue();
        Presence<Presence<int>>.Value(Presence<int>.Null).FilterToMissing(static inner => inner.IsNull).IsValue.ShouldBeTrue();
    }

    [Fact]
    public void Mutable_reference_payload_is_shared_not_frozen() {
        var payload = new List<int> { 1 };

        var kept = Presence<List<int>>.Value(payload).FilterToNull(list => { list.Add(2); return true; });

        kept.TryGetValue(out var value).ShouldBeTrue();
        value.ShouldBeSameAs(payload);
        value.ShouldBe([1, 2]);
    }

    [Fact]
    public void Receiver_is_not_changed() {
        var presence = Presence<int>.Value(1);

        _ = presence.AndThen(static _ => Presence<int>.Null);
        _ = presence.FilterToMissing(static _ => false);
        _ = presence.FilterToNull(static _ => false);

        presence.ShouldBe(Presence<int>.Value(1));
    }
}
