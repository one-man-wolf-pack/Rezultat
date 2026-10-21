using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Rezultat.Tests;

public sealed class ValidationMapEnsureTests
{
    [Fact]
    public void Map_invokes_selector_once_on_valid_and_keeps_its_exact_result()
    {
        var payload = new ReferenceProbe(1);
        var result = new ReferenceProbe(2);
        var seen = new List<ReferenceProbe>();

        var mapped = Validation<ReferenceProbe, string>.Valid(payload).Map(value => { seen.Add(value); return result; });

        Assert.Same(payload, Assert.Single(seen));
        Assert.True(mapped.TryGetValue(out var value, out _));
        Assert.Same(result, value);
    }

    [Fact]
    public void Map_keeps_null_and_default_results()
    {
        Assert.Equal(Validation<string?, int>.Valid(null), Validation<int, int>.Valid(1).Map(static _ => (string?)null));
        Assert.Equal(Validation<int, int>.Valid(0), Validation<string, int>.Valid("x").Map(static _ => 0));
    }

    [Fact]
    public void Map_on_invalid_keeps_the_issue_buffer_without_invoking_the_selector()
    {
        ImmutableArray<string?> issues = ["a", null, "a"];
        var calls = 0;

        var mapped = Validation<int, string?>.InvalidMany(issues).Map(value => ++calls);

        Assert.Equal(0, calls);
        Assert.False(mapped.TryGetValue(out _, out var kept));
        Assert.Same(Backing(issues), Backing(kept));
    }

    [Fact]
    public void Ensure_true_keeps_valid_payload_and_skips_the_issue_factory()
    {
        var payload = new ReferenceProbe(3);
        var calls = new List<string>();

        var checkedValue = Validation<ReferenceProbe, string>.Valid(payload).Ensure(
            value => { calls.Add($"predicate:{value.Id}"); return true; },
            value => { calls.Add($"factory:{value.Id}"); return "rejected"; });

        Assert.Equal(["predicate:3"], calls);
        Assert.True(checkedValue.TryGetValue(out var value, out _));
        Assert.Same(payload, value);
    }

    [Fact]
    public void Ensure_false_creates_exactly_one_issue_from_the_same_value()
    {
        var payload = new ReferenceProbe(4);
        var calls = new List<(string Callback, ReferenceProbe Value)>();

        var checkedValue = Validation<ReferenceProbe, string>.Valid(payload).Ensure(
            value => { calls.Add(("predicate", value)); return false; },
            value => { calls.Add(("factory", value)); return $"rejected {value.Id}"; });

        Assert.Equal(["predicate", "factory"], calls.Select(static call => call.Callback));
        Assert.All(calls, call => Assert.Same(payload, call.Value));
        Assert.False(checkedValue.TryGetValue(out _, out var issues));
        Assert.Equal(["rejected 4"], issues);
    }

    [Fact]
    public void Ensure_false_keeps_a_null_issue()
    {
        var checkedValue = Validation<int, string?>.Valid(1).Ensure(static _ => false, static _ => null);

        Assert.False(checkedValue.TryGetValue(out _, out var issues));
        Assert.Null(Assert.Single(issues));
    }

    [Fact]
    public void Ensure_on_invalid_keeps_the_issue_buffer_without_invoking_callbacks()
    {
        ImmutableArray<string> issues = ["a", "b"];
        var calls = 0;

        var checkedValue = Validation<int, string>.InvalidMany(issues).Ensure(_ => ++calls > 0, _ => $"{++calls}");

        Assert.Equal(0, calls);
        Assert.False(checkedValue.TryGetValue(out _, out var kept));
        Assert.Same(Backing(issues), Backing(kept));
    }

    [Fact]
    public void Chained_Ensure_is_dependent_and_does_not_accumulate_independent_issues()
    {
        var calls = new List<string>();

        var checkedValue = Validation<int, string>.Valid(-5)
            .Ensure(value => { calls.Add("positive"); return value > 0; }, static _ => "not positive")
            .Ensure(value => { calls.Add("even"); return value % 2 == 0; }, static _ => "odd");

        Assert.Equal(["positive"], calls);
        Assert.Equal(Validation<int, string>.Invalid("not positive"), checkedValue);
    }

    [Fact]
    public void Ensure_passes_value_type_payload_by_value_to_each_callback()
    {
        var factoryInput = -1;

        var checkedValue = Validation<int, int>.Valid(5).Ensure(
            value => { value++; return value < 0; },
            value => factoryInput = value);

        Assert.Equal(5, factoryInput);
        Assert.Equal(Validation<int, int>.Invalid(5), checkedValue);
    }

    [Fact]
    public void Ensure_true_returns_the_snapshot_taken_before_the_predicate_reassigned_the_receiver()
    {
        var receiver = Validation<int, string>.Valid(1);

        var checkedValue = receiver.Ensure(_ => { receiver = Validation<int, string>.Invalid("reassigned"); return true; }, static _ => "rejected");

        Assert.Equal(Validation<int, string>.Valid(1), checkedValue);
    }

    [Fact]
    public void Ensure_false_passes_the_value_observed_before_the_predicate_reassigned_the_receiver()
    {
        var receiver = Validation<int, int>.Valid(1);

        var checkedValue = receiver.Ensure(_ => { receiver = Validation<int, int>.Valid(99); return false; }, static value => value);

        Assert.Equal(Validation<int, int>.Invalid(1), checkedValue);
    }

    [Fact]
    public void Map_uses_the_value_observed_before_the_selector_reassigned_the_receiver()
    {
        var receiver = Validation<int, string>.Valid(1);
        var seen = new List<int>();

        var mapped = receiver.Map(value =>
        {
            receiver = Validation<int, string>.Valid(99);
            seen.Add(value);
            return value + 1;
        });

        Assert.Equal([1], seen);
        Assert.Equal(Validation<int, string>.Valid(2), mapped);
    }

    [Fact]
    public void Uninitialized_receiver_is_rejected_before_the_callback_guards()
    {
        Assert.Throws<InvalidOperationException>(() => default(Validation<int, string>).Map<int>(null!));
        Assert.Throws<InvalidOperationException>(() => default(Validation<int, string>).Ensure(null!, null!));
        Assert.Throws<InvalidOperationException>(() => default(Validation<int, string>).Ensure(static _ => true, static _ => "e"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Null_callbacks_are_rejected_on_both_outcomes_in_parameter_order(bool valid)
    {
        var validation = valid ? Validation<int, string>.Valid(1) : Validation<int, string>.Invalid("e");

        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => validation.Map<int>(null!)).ParamName);
        Assert.Equal("predicate", Assert.Throws<ArgumentNullException>(() => validation.Ensure(null!, null!)).ParamName);
        Assert.Equal("issueFactory", Assert.Throws<ArgumentNullException>(() => validation.Ensure(static _ => true, null!)).ParamName);
    }

    [Fact]
    public void Callback_exceptions_and_cancellation_propagate_unchanged()
    {
        var invalid = new InvalidOperationException("callback");
        var canceled = new OperationCanceledException();
        var valid = Validation<int, string>.Valid(1);

        Assert.Same(invalid, Assert.Throws<InvalidOperationException>(() => valid.Map<int>(_ => throw invalid)));
        Assert.Same(canceled, Assert.Throws<OperationCanceledException>(() => valid.Ensure(_ => throw canceled, static _ => "e")));
        Assert.Same(invalid, Assert.Throws<InvalidOperationException>(() => valid.Ensure(static _ => false, _ => throw invalid)));
    }

    [Fact]
    public void Nested_default_carriers_and_tasks_are_ordinary_payloads()
    {
        var nested = Validation<int, string>.Valid(1).Map(static _ => default(Validation<int, string>));
        var task = Validation<int, string>.Valid(1).Map(static _ => Task.FromException<int>(new ProbeException("task")));
        var checkedNested = Validation<Validation<int, string>, string>.Valid(default).Ensure(static inner => !inner.IsInitialized, static _ => "e");

        Assert.True(nested.TryGetValue(out var inner, out _) && !inner.IsInitialized);
        Assert.True(task.TryGetValue(out var pending, out _) && pending.IsFaulted);
        Assert.True(checkedNested.IsValid);
    }

    [Fact]
    public void Mutable_reference_payload_is_shared_not_frozen()
    {
        var payload = new List<int> { 1 };

        var checkedValue = Validation<List<int>, string>.Valid(payload).Ensure(list => { list.Add(2); return true; }, static _ => "e");

        Assert.True(checkedValue.TryGetValue(out var value, out _));
        Assert.Same(payload, value);
        Assert.Equal([1, 2], value);
    }

    private static TIssue[] Backing<TIssue>(ImmutableArray<TIssue> issues) => ImmutableCollectionsMarshal.AsArray(issues)!;
}
