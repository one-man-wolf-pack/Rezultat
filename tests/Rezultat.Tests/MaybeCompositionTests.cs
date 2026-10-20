namespace Rezultat.Tests;

public sealed class MaybeCompositionTests
{
    [Fact]
    public void Map_invokes_selector_once_for_some_and_skips_none()
    {
        var calls = 0;

        Assert.Equal(Maybe<string>.Some("2"), Maybe<int>.Some(1).Map(value => { calls++; return $"{value + 1}"; }));
        Assert.Equal(1, calls);
        Assert.Equal(Maybe<string>.None, Maybe<int>.None.Map(value => { calls++; return $"{value}"; }));
        Assert.Equal(Maybe<string>.None, default(Maybe<int>).Map(value => { calls++; return $"{value}"; }));
        Assert.Equal(1, calls);
    }

    [Fact]
    public void Map_passes_present_null_and_keeps_null_result_present()
    {
        string? observed = "unset";

        var mapped = Maybe<string?>.Some(null).Map(value => { observed = value; return (string?)null; });
        var emptyNullable = Maybe<int>.Some(1).Map(static _ => (int?)null);

        Assert.Null(observed);
        Assert.True(mapped.TryGetValue(out var payload) && payload is null);
        Assert.True(emptyNullable.TryGetValue(out var empty) && !empty.HasValue);
    }

    [Fact]
    public void Map_keeps_nested_carriers_and_tasks_as_payload()
    {
        var nestedNone = Maybe<int>.Some(1).Map(static _ => Maybe<int>.None);
        var uninitialized = Maybe<int>.Some(1).Map(static _ => default(Result<int, string>));
        var faulted = Maybe<int>.Some(1).Map(static _ => Task.FromException<int>(new ProbeException("task")));

        Assert.Equal(Maybe<Maybe<int>>.Some(Maybe<int>.None), nestedNone);
        Assert.NotEqual(Maybe<Maybe<int>>.None, nestedNone);
        Assert.True(uninitialized.TryGetValue(out var inner) && !inner.IsInitialized);
        Assert.True(faulted.TryGetValue(out var task) && task.IsFaulted);
    }

    [Fact]
    public void Map_and_AndThen_keep_reference_identity()
    {
        var probe = new ReferenceProbe(1);
        ReferenceProbe? received = null;

        var mapped = Maybe<ReferenceProbe>.Some(probe).Map(value => received = value);
        var chained = Maybe<ReferenceProbe>.Some(probe).AndThen(Maybe<ReferenceProbe>.Some);

        Assert.Same(probe, received);
        Assert.True(mapped.TryGetValue(out var mappedValue) && ReferenceEquals(probe, mappedValue));
        Assert.True(chained.TryGetValue(out var chainedValue) && ReferenceEquals(probe, chainedValue));
        Assert.Equal((0, 0, 0), (probe.EqualsCalls, probe.HashCalls, probe.ToStringCalls));
    }

    [Fact]
    public void AndThen_returns_next_outcome_including_default_and_skips_none()
    {
        var calls = 0;

        Assert.Equal(Maybe<int>.Some(2), Maybe<int>.Some(1).AndThen(value => { calls++; return Maybe<int>.Some(value + 1); }));
        Assert.Equal(Maybe<int>.None, Maybe<int>.Some(1).AndThen(_ => { calls++; return default(Maybe<int>); }));
        Assert.Equal(Maybe<int>.None, Maybe<int>.None.AndThen(_ => { calls++; return Maybe<int>.Some(0); }));
        Assert.Equal(2, calls);
        Assert.True(Maybe<string?>.Some(null).AndThen(static value => Maybe<string?>.Some(value)).TryGetValue(out var payload) && payload is null);
    }

    [Fact]
    public void OrElse_keeps_some_without_fallback_and_returns_fallback_for_none()
    {
        var calls = 0;

        Assert.Equal(Maybe<int>.Some(1), Maybe<int>.Some(1).OrElse(() => { calls++; return Maybe<int>.Some(2); }));
        Assert.Equal(Maybe<string?>.Some(null), Maybe<string?>.Some(null).OrElse(() => { calls++; return Maybe<string?>.Some("x"); }));
        Assert.Equal(Maybe<Maybe<int>>.Some(Maybe<int>.None), Maybe<Maybe<int>>.Some(Maybe<int>.None).OrElse(() => { calls++; return default; }));
        Assert.Equal(0, calls);
        Assert.Equal(Maybe<int>.Some(2), Maybe<int>.None.OrElse(() => { calls++; return Maybe<int>.Some(2); }));
        Assert.Equal(Maybe<int>.None, default(Maybe<int>).OrElse(() => { calls++; return default; }));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Filter_keeps_some_on_true_and_gives_none_on_false_or_none()
    {
        var probe = new ReferenceProbe(1);
        var calls = 0;

        var kept = Maybe<ReferenceProbe>.Some(probe).Filter(_ => { calls++; return true; });
        var rejected = Maybe<ReferenceProbe>.Some(probe).Filter(_ => { calls++; return false; });
        var none = Maybe<ReferenceProbe>.None.Filter(_ => { calls++; return true; });

        Assert.True(kept.TryGetValue(out var keptValue) && ReferenceEquals(probe, keptValue));
        Assert.False(rejected.IsSome);
        Assert.False(none.IsSome);
        Assert.Equal(2, calls);
        Assert.Equal((0, 0, 0), (probe.EqualsCalls, probe.HashCalls, probe.ToStringCalls));
    }

    [Fact]
    public void Filter_passes_present_null_and_does_not_narrow()
    {
        string? observed = "unset";

        var kept = Maybe<string?>.Some(null).Filter(value => { observed = value; return true; });
        var nested = Maybe<Maybe<int>>.Some(Maybe<int>.None).Filter(static inner => !inner.IsSome);

        Assert.Null(observed);
        Assert.Equal(Maybe<string?>.Some(null), kept);
        Assert.Equal(Maybe<Maybe<int>>.Some(Maybe<int>.None), nested);
        Assert.Equal(Maybe<int?>.Some(null), Maybe<int?>.Some(null).Filter(static value => !value.HasValue));
    }

    [Fact]
    public void Filter_true_returns_the_receiver_observed_before_predicate_reassigned_it()
    {
        var maybe = Maybe<int>.Some(1);

        var filtered = maybe.Filter(_ =>
        {
            maybe = Maybe<int>.Some(2);
            return true;
        });

        Assert.Equal(Maybe<int>.Some(1), filtered);
        Assert.Equal(Maybe<int>.Some(2), maybe);
    }

    [Fact]
    public void Filter_effects_on_reference_payload_are_not_rolled_back()
    {
        var list = new List<int>();

        var rejected = Maybe<List<int>>.Some(list).Filter(value => { value.Add(1); return false; });

        Assert.False(rejected.IsSome);
        Assert.Equal([1], list);
    }

    [Fact]
    public void Zip_invokes_selector_once_with_receiver_first_only_for_two_somes()
    {
        var calls = new List<(int, string)>();

        var zipped = Maybe<int>.Some(1).Zip(Maybe<string>.Some("b"), (a, b) => { calls.Add((a, b)); return $"{a}{b}"; });

        Assert.Equal(Maybe<string>.Some("1b"), zipped);
        Assert.Equal([(1, "b")], calls);
        Assert.False(Maybe<int>.None.Zip(Maybe<string>.Some("b"), (a, b) => { calls.Add((a, b)); return 0; }).IsSome);
        Assert.False(Maybe<int>.Some(1).Zip(default(Maybe<string>), (a, b) => { calls.Add((a, b)); return 0; }).IsSome);
        Assert.False(default(Maybe<int>).Zip(Maybe<string>.None, (a, b) => { calls.Add((a, b)); return 0; }).IsSome);
        Assert.Single(calls);
    }

    [Fact]
    public void Zip_treats_present_null_and_nested_none_as_presence_and_keeps_null_result()
    {
        var zipped = Maybe<string?>.Some(null).Zip(Maybe<int?>.Some(null), static (a, b) => a is null && !b.HasValue);
        var nested = Maybe<Maybe<int>>.Some(Maybe<int>.None).Zip(Maybe<int>.Some(1), static (inner, _) => inner);
        var nullResult = Maybe<int>.Some(1).Zip(Maybe<int>.Some(2), static (_, _) => (string?)null);

        Assert.Equal(Maybe<bool>.Some(true), zipped);
        Assert.Equal(Maybe<Maybe<int>>.Some(Maybe<int>.None), nested);
        Assert.Equal(Maybe<string?>.Some(null), nullResult);
    }

    [Fact]
    public void Zip_other_argument_is_evaluated_even_when_receiver_is_none()
    {
        var evaluations = 0;
        Maybe<int> Find()
        {
            evaluations++;
            return Maybe<int>.Some(2);
        }

        Assert.False(Maybe<int>.None.Zip(Find(), static (a, b) => a + b).IsSome);
        Assert.Equal(1, evaluations);
    }

    [Fact]
    public void Zip_argument_exception_precedes_null_selector_guard()
    {
        var thrown = new ProbeException("other");
        Maybe<int> Find() => throw thrown;

        Assert.Same(thrown, Assert.Throws<ProbeException>(() => Maybe<int>.None.Zip<int, int>(Find(), null!)));
    }

    [Fact]
    public void Null_callbacks_are_rejected_on_both_alternatives()
    {
        foreach (var maybe in new[] { Maybe<int>.Some(1), Maybe<int>.None })
        {
            Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => maybe.Map<int>(null!)).ParamName);
            Assert.Equal("next", Assert.Throws<ArgumentNullException>(() => maybe.AndThen<int>(null!)).ParamName);
            Assert.Equal("fallback", Assert.Throws<ArgumentNullException>(() => maybe.OrElse(null!)).ParamName);
            Assert.Equal("predicate", Assert.Throws<ArgumentNullException>(() => maybe.Filter(null!)).ParamName);
            Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => maybe.Zip<int, int>(Maybe<int>.None, null!)).ParamName);
            Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => maybe.Zip<int, int>(Maybe<int>.Some(2), null!)).ParamName);
        }
    }

    [Fact]
    public void Callback_exceptions_propagate_unchanged()
    {
        var fault = new ProbeException("callback");
        var canceled = new OperationCanceledException();

        Assert.Same(fault, Assert.Throws<ProbeException>(() => Maybe<int>.Some(1).Map<int>(_ => throw fault)));
        Assert.Same(canceled, Assert.Throws<OperationCanceledException>(() => Maybe<int>.Some(1).AndThen<int>(_ => throw canceled)));
        Assert.Same(fault, Assert.Throws<ProbeException>(() => Maybe<int>.None.OrElse(() => throw fault)));
        Assert.Same(canceled, Assert.Throws<OperationCanceledException>(() => Maybe<int>.Some(1).Filter(_ => throw canceled)));
        Assert.Same(fault, Assert.Throws<ProbeException>(() => Maybe<int>.Some(1).Zip<int, int>(Maybe<int>.Some(2), (_, _) => throw fault)));
    }

    [Fact]
    public void OrElse_placement_selects_the_fallback_boundary()
    {
        var before = Maybe<int>.Some(1).OrElse(static () => Maybe<int>.Some(9)).AndThen(static _ => Maybe<int>.None);
        var after = Maybe<int>.Some(1).AndThen(static _ => Maybe<int>.None).OrElse(static () => Maybe<int>.Some(9));

        Assert.Equal(Maybe<int>.None, before);
        Assert.Equal(Maybe<int>.Some(9), after);
    }

    [Fact]
    public async Task Async_selectors_produce_task_payloads_without_awaiting()
    {
        var gate = new TaskCompletionSource<int>();

        var mapped = Maybe<int>.Some(1).Map(_ => gate.Task);
        var zipped = Maybe<int>.Some(1).Zip(Maybe<int>.Some(2), (_, _) => gate.Task);

        Assert.True(mapped.TryGetValue(out var mappedTask) && !mappedTask.IsCompleted);
        Assert.True(zipped.TryGetValue(out var zippedTask) && ReferenceEquals(mappedTask, zippedTask));
        gate.SetResult(3);
        Assert.Equal(3, await mappedTask);
    }
}
