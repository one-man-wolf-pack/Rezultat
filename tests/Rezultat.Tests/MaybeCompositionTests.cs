namespace Rezultat.Tests;

public sealed class MaybeCompositionTests {
    [Fact]
    public void Map_invokes_selector_once_for_some_and_skips_none() {
        int calls = 0;

        Maybe<int>.Some(1).Map(value => { calls++; return $"{value + 1}"; }).ShouldBe(Maybe<string>.Some("2"));
        calls.ShouldBe(1);
        Maybe<int>.None.Map(value => { calls++; return $"{value}"; }).ShouldBe(Maybe<string>.None);
        default(Maybe<int>).Map(value => { calls++; return $"{value}"; }).ShouldBe(Maybe<string>.None);
        calls.ShouldBe(1);
    }

    [Fact]
    public void Map_passes_present_null_and_keeps_null_result_present() {
        string? observed = "unset";

        var mapped = Maybe<string?>.Some(null).Map(value => { observed = value; return (string?)null; });
        var emptyNullable = Maybe<int>.Some(1).Map(static _ => (int?)null);

        observed.ShouldBeNull();
        (mapped.TryGetValue(out string? payload) && payload is null).ShouldBeTrue();
        (emptyNullable.TryGetValue(out int? empty) && !empty.HasValue).ShouldBeTrue();
    }

    [Fact]
    public void Map_keeps_nested_carriers_and_tasks_as_payload() {
        var nestedNone = Maybe<int>.Some(1).Map(static _ => Maybe<int>.None);
        var uninitialized = Maybe<int>.Some(1).Map(static _ => default(Result<int, string>));
        var faulted = Maybe<int>.Some(1).Map(static _ => Task.FromException<int>(new ProbeException("task")));

        nestedNone.ShouldBe(Maybe<Maybe<int>>.Some(Maybe<int>.None));
        nestedNone.ShouldNotBe(Maybe<Maybe<int>>.None);
        (uninitialized.TryGetValue(out var inner) && !inner.IsInitialized).ShouldBeTrue();
        (faulted.TryGetValue(out var task) && task.IsFaulted).ShouldBeTrue();
    }

    [Fact]
    public void Map_and_AndThen_keep_reference_identity() {
        var probe = new ReferenceProbe(1);
        ReferenceProbe? received = null;

        var mapped = Maybe<ReferenceProbe>.Some(probe).Map(value => received = value);
        var chained = Maybe<ReferenceProbe>.Some(probe).AndThen(Maybe<ReferenceProbe>.Some);

        received.ShouldBeSameAs(probe);
        (mapped.TryGetValue(out var mappedValue) && ReferenceEquals(probe, mappedValue)).ShouldBeTrue();
        (chained.TryGetValue(out var chainedValue) && ReferenceEquals(probe, chainedValue)).ShouldBeTrue();
        (probe.EqualsCalls, probe.HashCalls, probe.ToStringCalls).ShouldBe((0, 0, 0));
    }

    [Fact]
    public void AndThen_returns_next_outcome_including_default_and_skips_none() {
        int calls = 0;

        Maybe<int>.Some(1).AndThen(value => { calls++; return Maybe<int>.Some(value + 1); }).ShouldBe(Maybe<int>.Some(2));
        Maybe<int>.Some(1).AndThen(_ => { calls++; return default(Maybe<int>); }).ShouldBe(Maybe<int>.None);
        Maybe<int>.None.AndThen(_ => { calls++; return Maybe<int>.Some(0); }).ShouldBe(Maybe<int>.None);
        calls.ShouldBe(2);
        (Maybe<string?>.Some(null).AndThen(static value => Maybe<string?>.Some(value)).TryGetValue(out string? payload) && payload is null).ShouldBeTrue();
    }

    [Fact]
    public void OrElse_keeps_some_without_fallback_and_returns_fallback_for_none() {
        int calls = 0;

        Maybe<int>.Some(1).OrElse(() => { calls++; return Maybe<int>.Some(2); }).ShouldBe(Maybe<int>.Some(1));
        Maybe<string?>.Some(null).OrElse(() => { calls++; return Maybe<string?>.Some("x"); }).ShouldBe(Maybe<string?>.Some(null));
        Maybe<Maybe<int>>.Some(Maybe<int>.None).OrElse(() => { calls++; return default; }).ShouldBe(Maybe<Maybe<int>>.Some(Maybe<int>.None));
        calls.ShouldBe(0);
        Maybe<int>.None.OrElse(() => { calls++; return Maybe<int>.Some(2); }).ShouldBe(Maybe<int>.Some(2));
        default(Maybe<int>).OrElse(() => { calls++; return default; }).ShouldBe(Maybe<int>.None);
        calls.ShouldBe(2);
    }

    [Fact]
    public void Filter_keeps_some_on_true_and_gives_none_on_false_or_none() {
        var probe = new ReferenceProbe(1);
        int calls = 0;

        var kept = Maybe<ReferenceProbe>.Some(probe).Filter(_ => { calls++; return true; });
        var rejected = Maybe<ReferenceProbe>.Some(probe).Filter(_ => { calls++; return false; });
        var none = Maybe<ReferenceProbe>.None.Filter(_ => { calls++; return true; });

        (kept.TryGetValue(out var keptValue) && ReferenceEquals(probe, keptValue)).ShouldBeTrue();
        rejected.IsSome.ShouldBeFalse();
        none.IsSome.ShouldBeFalse();
        calls.ShouldBe(2);
        (probe.EqualsCalls, probe.HashCalls, probe.ToStringCalls).ShouldBe((0, 0, 0));
    }

    [Fact]
    public void Filter_passes_present_null_and_does_not_narrow() {
        string? observed = "unset";

        var kept = Maybe<string?>.Some(null).Filter(value => { observed = value; return true; });
        var nested = Maybe<Maybe<int>>.Some(Maybe<int>.None).Filter(static inner => !inner.IsSome);

        observed.ShouldBeNull();
        kept.ShouldBe(Maybe<string?>.Some(null));
        nested.ShouldBe(Maybe<Maybe<int>>.Some(Maybe<int>.None));
        Maybe<int?>.Some(null).Filter(static value => !value.HasValue).ShouldBe(Maybe<int?>.Some(null));
    }

    [Fact]
    public void Filter_true_returns_the_receiver_observed_before_predicate_reassigned_it() {
        var maybe = Maybe<int>.Some(1);

        var filtered = maybe.Filter(_ => {
            maybe = Maybe<int>.Some(2);
            return true;
        });

        filtered.ShouldBe(Maybe<int>.Some(1));
        maybe.ShouldBe(Maybe<int>.Some(2));
    }

    [Fact]
    public void Filter_effects_on_reference_payload_are_not_rolled_back() {
        var list = new List<int>();

        var rejected = Maybe<List<int>>.Some(list).Filter(value => { value.Add(1); return false; });

        rejected.IsSome.ShouldBeFalse();
        list.ShouldBe([1]);
    }

    [Fact]
    public void Zip_invokes_selector_once_with_receiver_first_only_for_two_somes() {
        var calls = new List<(int, string)>();

        var zipped = Maybe<int>.Some(1).Zip(Maybe<string>.Some("b"), (a, b) => { calls.Add((a, b)); return $"{a}{b}"; });

        zipped.ShouldBe(Maybe<string>.Some("1b"));
        calls.ShouldBe([(1, "b")]);
        Maybe<int>.None.Zip(Maybe<string>.Some("b"), (a, b) => { calls.Add((a, b)); return 0; }).IsSome.ShouldBeFalse();
        Maybe<int>.Some(1).Zip(default(Maybe<string>), (a, b) => { calls.Add((a, b)); return 0; }).IsSome.ShouldBeFalse();
        default(Maybe<int>).Zip(Maybe<string>.None, (a, b) => { calls.Add((a, b)); return 0; }).IsSome.ShouldBeFalse();
        calls.ShouldHaveSingleItem();
    }

    [Fact]
    public void Zip_treats_present_null_and_nested_none_as_presence_and_keeps_null_result() {
        var zipped = Maybe<string?>.Some(null).Zip(Maybe<int?>.Some(null), static (a, b) => a is null && !b.HasValue);
        var nested = Maybe<Maybe<int>>.Some(Maybe<int>.None).Zip(Maybe<int>.Some(1), static (inner, _) => inner);
        var nullResult = Maybe<int>.Some(1).Zip(Maybe<int>.Some(2), static (_, _) => (string?)null);

        zipped.ShouldBe(Maybe<bool>.Some(true));
        nested.ShouldBe(Maybe<Maybe<int>>.Some(Maybe<int>.None));
        nullResult.ShouldBe(Maybe<string?>.Some(null));
    }

    [Fact]
    public void Zip_other_argument_is_evaluated_even_when_receiver_is_none() {
        int evaluations = 0;
        Maybe<int> Find() {
            evaluations++;
            return Maybe<int>.Some(2);
        }

        Maybe<int>.None.Zip(Find(), static (a, b) => a + b).IsSome.ShouldBeFalse();
        evaluations.ShouldBe(1);
    }

    [Fact]
    public void Zip_argument_exception_precedes_null_selector_guard() {
        var thrown = new ProbeException("other");
        Maybe<int> Find() => throw thrown;

        Should.Throw<ProbeException>(() => Maybe<int>.None.Zip<int, int>(Find(), null!)).ShouldBeSameAs(thrown);
    }

    [Fact]
    public void Null_callbacks_are_rejected_on_both_alternatives() {
        foreach (var maybe in new[] { Maybe<int>.Some(1), Maybe<int>.None }) {
            Should.Throw<ArgumentNullException>(() => maybe.Map<int>(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("selector");
            Should.Throw<ArgumentNullException>(() => maybe.AndThen<int>(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("next");
            Should.Throw<ArgumentNullException>(() => maybe.OrElse(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("fallback");
            Should.Throw<ArgumentNullException>(() => maybe.Filter(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("predicate");
            Should.Throw<ArgumentNullException>(() => maybe.Zip<int, int>(Maybe<int>.None, null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("selector");
            Should.Throw<ArgumentNullException>(() => maybe.Zip<int, int>(Maybe<int>.Some(2), null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("selector");
        }
    }

    [Fact]
    public void Callback_exceptions_propagate_unchanged() {
        var fault = new ProbeException("callback");
        var canceled = new OperationCanceledException();

        Should.Throw<ProbeException>(() => Maybe<int>.Some(1).Map<int>(_ => throw fault)).ShouldBeSameAs(fault);
        Should.Throw<OperationCanceledException>(() => Maybe<int>.Some(1).AndThen<int>(_ => throw canceled)).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(canceled);
        Should.Throw<ProbeException>(() => Maybe<int>.None.OrElse(() => throw fault)).ShouldBeSameAs(fault);
        Should.Throw<OperationCanceledException>(() => Maybe<int>.Some(1).Filter(_ => throw canceled)).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(canceled);
        Should.Throw<ProbeException>(() => Maybe<int>.Some(1).Zip<int, int>(Maybe<int>.Some(2), (_, _) => throw fault)).ShouldBeSameAs(fault);
    }

    [Fact]
    public void OrElse_placement_selects_the_fallback_boundary() {
        var before = Maybe<int>.Some(1).OrElse(static () => Maybe<int>.Some(9)).AndThen(static _ => Maybe<int>.None);
        var after = Maybe<int>.Some(1).AndThen(static _ => Maybe<int>.None).OrElse(static () => Maybe<int>.Some(9));

        before.ShouldBe(Maybe<int>.None);
        after.ShouldBe(Maybe<int>.Some(9));
    }

    [Fact]
    public async Task Async_selectors_produce_task_payloads_without_awaiting() {
        var gate = new TaskCompletionSource<int>();

        var mapped = Maybe<int>.Some(1).Map(_ => gate.Task);
        var zipped = Maybe<int>.Some(1).Zip(Maybe<int>.Some(2), (_, _) => gate.Task);

        (mapped.TryGetValue(out var mappedTask) && !mappedTask.IsCompleted).ShouldBeTrue();
        (zipped.TryGetValue(out var zippedTask) && ReferenceEquals(mappedTask, zippedTask)).ShouldBeTrue();
        gate.SetResult(3);
        (await mappedTask).ShouldBe(3);
    }
}
