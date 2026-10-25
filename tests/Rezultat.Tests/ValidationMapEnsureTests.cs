using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Rezultat.Tests;

public sealed class ValidationMapEnsureTests {
    [Fact]
    public void Map_invokes_selector_once_on_valid_and_keeps_its_exact_result() {
        var payload = new ReferenceProbe(1);
        var result = new ReferenceProbe(2);
        var seen = new List<ReferenceProbe>();

        var mapped = Validation<ReferenceProbe, string>.Valid(payload).Map(value => { seen.Add(value); return result; });

        seen.ShouldHaveSingleItem().ShouldBeSameAs(payload);
        mapped.TryGetValue(out var value, out _).ShouldBeTrue();
        value.ShouldBeSameAs(result);
    }

    [Fact]
    public void Map_keeps_null_and_default_results() {
        Validation<int, int>.Valid(1).Map(static _ => (string?)null).ShouldBe(Validation<string?, int>.Valid(null));
        Validation<string, int>.Valid("x").Map(static _ => 0).ShouldBe(Validation<int, int>.Valid(0));
    }

    [Fact]
    public void Map_on_invalid_keeps_the_issue_buffer_without_invoking_the_selector() {
        ImmutableArray<string?> issues = ["a", null, "a"];
        var calls = 0;

        var mapped = Validation<int, string?>.InvalidMany(issues).Map(value => ++calls);

        calls.ShouldBe(0);
        mapped.TryGetValue(out _, out var kept).ShouldBeFalse();
        Backing(kept).ShouldBeSameAs(Backing(issues));
    }

    [Fact]
    public void Ensure_true_keeps_valid_payload_and_skips_the_issue_factory() {
        var payload = new ReferenceProbe(3);
        var calls = new List<string>();

        var checkedValue = Validation<ReferenceProbe, string>.Valid(payload).Ensure(
            value => { calls.Add($"predicate:{value.Id}"); return true; },
            value => { calls.Add($"factory:{value.Id}"); return "rejected"; });

        calls.ShouldBe(["predicate:3"]);
        checkedValue.TryGetValue(out var value, out _).ShouldBeTrue();
        value.ShouldBeSameAs(payload);
    }

    [Fact]
    public void Ensure_false_creates_exactly_one_issue_from_the_same_value() {
        var payload = new ReferenceProbe(4);
        var calls = new List<(string Callback, ReferenceProbe Value)>();

        var checkedValue = Validation<ReferenceProbe, string>.Valid(payload).Ensure(
            value => { calls.Add(("predicate", value)); return false; },
            value => { calls.Add(("factory", value)); return $"rejected {value.Id}"; });

        calls.Select(static call => call.Callback).ShouldBe(["predicate", "factory"]);
        foreach (var call in calls) {
            call.Value.ShouldBeSameAs(payload);
        }
        checkedValue.TryGetValue(out _, out var issues).ShouldBeFalse();
        issues.ShouldBe(["rejected 4"]);
    }

    [Fact]
    public void Ensure_false_keeps_a_null_issue() {
        var checkedValue = Validation<int, string?>.Valid(1).Ensure(static _ => false, static _ => null);

        checkedValue.TryGetValue(out _, out var issues).ShouldBeFalse();
        issues.ShouldHaveSingleItem().ShouldBeNull();
    }

    [Fact]
    public void Ensure_on_invalid_keeps_the_issue_buffer_without_invoking_callbacks() {
        ImmutableArray<string> issues = ["a", "b"];
        var calls = 0;

        var checkedValue = Validation<int, string>.InvalidMany(issues).Ensure(_ => ++calls > 0, _ => $"{++calls}");

        calls.ShouldBe(0);
        checkedValue.TryGetValue(out _, out var kept).ShouldBeFalse();
        Backing(kept).ShouldBeSameAs(Backing(issues));
    }

    [Fact]
    public void Chained_Ensure_is_dependent_and_does_not_accumulate_independent_issues() {
        var calls = new List<string>();

        var checkedValue = Validation<int, string>.Valid(-5)
            .Ensure(value => { calls.Add("positive"); return value > 0; }, static _ => "not positive")
            .Ensure(value => { calls.Add("even"); return value % 2 == 0; }, static _ => "odd");

        calls.ShouldBe(["positive"]);
        checkedValue.ShouldBe(Validation<int, string>.Invalid("not positive"));
    }

    [Fact]
    public void Ensure_passes_value_type_payload_by_value_to_each_callback() {
        var factoryInput = -1;

        var checkedValue = Validation<int, int>.Valid(5).Ensure(
            value => { value++; return value < 0; },
            value => factoryInput = value);

        factoryInput.ShouldBe(5);
        checkedValue.ShouldBe(Validation<int, int>.Invalid(5));
    }

    [Fact]
    public void Ensure_true_returns_the_snapshot_taken_before_the_predicate_reassigned_the_receiver() {
        var receiver = Validation<int, string>.Valid(1);

        var checkedValue = receiver.Ensure(_ => { receiver = Validation<int, string>.Invalid("reassigned"); return true; }, static _ => "rejected");

        checkedValue.ShouldBe(Validation<int, string>.Valid(1));
    }

    [Fact]
    public void Ensure_false_passes_the_value_observed_before_the_predicate_reassigned_the_receiver() {
        var receiver = Validation<int, int>.Valid(1);

        var checkedValue = receiver.Ensure(_ => { receiver = Validation<int, int>.Valid(99); return false; }, static value => value);

        checkedValue.ShouldBe(Validation<int, int>.Invalid(1));
    }

    [Fact]
    public void Map_uses_the_value_observed_before_the_selector_reassigned_the_receiver() {
        var receiver = Validation<int, string>.Valid(1);
        var seen = new List<int>();

        var mapped = receiver.Map(value => {
            receiver = Validation<int, string>.Valid(99);
            seen.Add(value);
            return value + 1;
        });

        seen.ShouldBe([1]);
        mapped.ShouldBe(Validation<int, string>.Valid(2));
    }

    [Fact]
    public void Uninitialized_receiver_is_rejected_before_the_callback_guards() {
        Should.Throw<InvalidOperationException>(() => default(Validation<int, string>).Map<int>(null!)).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<InvalidOperationException>(() => default(Validation<int, string>).Ensure(null!, null!)).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<InvalidOperationException>(() => default(Validation<int, string>).Ensure(static _ => true, static _ => "e")).ShouldBeOfType<InvalidOperationException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Null_callbacks_are_rejected_on_both_outcomes_in_parameter_order(bool valid) {
        var validation = valid ? Validation<int, string>.Valid(1) : Validation<int, string>.Invalid("e");

        Should.Throw<ArgumentNullException>(() => validation.Map<int>(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("selector");
        Should.Throw<ArgumentNullException>(() => validation.Ensure(null!, null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("predicate");
        Should.Throw<ArgumentNullException>(() => validation.Ensure(static _ => true, null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("issueFactory");
    }

    [Fact]
    public void Callback_exceptions_and_cancellation_propagate_unchanged() {
        var invalid = new InvalidOperationException("callback");
        var canceled = new OperationCanceledException();
        var valid = Validation<int, string>.Valid(1);

        Should.Throw<InvalidOperationException>(() => valid.Map<int>(_ => throw invalid)).ShouldBeOfType<InvalidOperationException>().ShouldBeSameAs(invalid);
        Should.Throw<OperationCanceledException>(() => valid.Ensure(_ => throw canceled, static _ => "e")).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(canceled);
        Should.Throw<InvalidOperationException>(() => valid.Ensure(static _ => false, _ => throw invalid)).ShouldBeOfType<InvalidOperationException>().ShouldBeSameAs(invalid);
    }

    [Fact]
    public void Nested_default_carriers_and_tasks_are_ordinary_payloads() {
        var nested = Validation<int, string>.Valid(1).Map(static _ => default(Validation<int, string>));
        var task = Validation<int, string>.Valid(1).Map(static _ => Task.FromException<int>(new ProbeException("task")));
        var checkedNested = Validation<Validation<int, string>, string>.Valid(default).Ensure(static inner => !inner.IsInitialized, static _ => "e");

        (nested.TryGetValue(out var inner, out _) && !inner.IsInitialized).ShouldBeTrue();
        (task.TryGetValue(out var pending, out _) && pending.IsFaulted).ShouldBeTrue();
        checkedNested.IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Mutable_reference_payload_is_shared_not_frozen() {
        var payload = new List<int> { 1 };

        var checkedValue = Validation<List<int>, string>.Valid(payload).Ensure(list => { list.Add(2); return true; }, static _ => "e");

        checkedValue.TryGetValue(out var value, out _).ShouldBeTrue();
        value.ShouldBeSameAs(payload);
        value.ShouldBe([1, 2]);
    }

    private static TIssue[] Backing<TIssue>(ImmutableArray<TIssue> issues) => ImmutableCollectionsMarshal.AsArray(issues)!;
}
