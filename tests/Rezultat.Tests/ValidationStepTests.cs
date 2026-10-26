using System.Collections.Immutable;

namespace Rezultat.Tests;

public sealed class ValidationStepTests {
    [Fact]
    public void AndThen_runs_next_once_on_valid_and_returns_its_outcome() {
        var seen = new List<int>();

        var valid = Validation<int, string>.Valid(2).AndThen(value => { seen.Add(value); return Validation<string, string>.Valid($"v{value}"); });
        var invalid = Validation<int, string>.Valid(2).AndThen(static _ => Validation<string, string>.Invalid("next"));

        valid.ShouldBe(Validation<string, string>.Valid("v2"));
        invalid.ShouldBe(Validation<string, string>.Invalid("next"));
        seen.ShouldBe([2]);
    }

    [Fact]
    public void AndThen_on_invalid_keeps_issues_without_invoking_next() {
        int calls = 0;

        var result = Validation<int, string>.InvalidMany(["a", "b"]).AndThen(value => { calls++; return Validation<string, string>.Valid("x"); });

        result.ShouldBe(Validation<string, string>.InvalidMany(["a", "b"]));
        calls.ShouldBe(0);
    }

    [Fact]
    public void AndThen_guards_receiver_then_next_then_returned_outcome() {
        Should.Throw<InvalidOperationException>(() => default(Validation<int, string>).AndThen<int>(null!)).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<ArgumentNullException>(() => Validation<int, string>.Invalid("e").AndThen<int>(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("next");
        Should.Throw<InvalidOperationException>(() => Validation<int, string>.Valid(1).AndThen(static _ => default(Validation<int, string>))).ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void AndThen_accepts_valid_null_and_nested_default_payloads() {
        Validation<int, string>.Valid(1).AndThen(static _ => Validation<string?, string>.Valid(null)).ShouldBe(Validation<string?, string>.Valid(null));
        var nested = Validation<int, string>.Valid(1).AndThen(static _ => Validation<Validation<int, string>, string>.Valid(default));
        (nested.TryGetValue(out var inner, out _) && !inner.IsInitialized).ShouldBeTrue();
    }

    [Fact]
    public void AndThen_exception_propagates_unchanged() {
        var thrown = new OperationCanceledException();

        Should.Throw<OperationCanceledException>(() => Validation<int, string>.Valid(1).AndThen<int>(_ => throw thrown)).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(thrown);
    }

    [Fact]
    public void OrElse_on_valid_keeps_value_with_new_issue_type_without_fallback() {
        int calls = 0;
        var payload = new ReferenceProbe(1);

        var result = Validation<ReferenceProbe, string>.Valid(payload).OrElse(issues => { calls++; return Validation<ReferenceProbe, int>.Invalid(issues.Length); });

        result.TryGetValue(out var value, out _).ShouldBeTrue();
        value.ShouldBeSameAs(payload);
        calls.ShouldBe(0);
    }

    [Fact]
    public void OrElse_on_invalid_passes_all_issues_once_and_replaces_the_outcome() {
        var received = new List<ImmutableArray<string?>>();
        var invalid = Validation<int, string?>.InvalidMany(["a", null, "a"]);

        var recovered = invalid.OrElse(issues => { received.Add(issues); return Validation<int, int>.Valid(issues.Length); });
        var replaced = invalid.OrElse(static _ => Validation<int, string?>.Invalid("only"));

        recovered.ShouldBe(Validation<int, int>.Valid(3));
        received.ShouldHaveSingleItem().ShouldBe(["a", null, "a"]);
        replaced.ShouldBe(Validation<int, string?>.Invalid("only"));
    }

    [Fact]
    public void OrElse_with_InvalidMany_keeps_the_original_failure_without_duplicates() {
        var invalid = Validation<int, string>.InvalidMany(["a", "b"]);

        invalid.OrElse(Validation<int, string>.InvalidMany).ShouldBe(invalid);
    }

    [Fact]
    public void OrElse_guards_receiver_then_fallback_then_returned_outcome() {
        Should.Throw<InvalidOperationException>(() => default(Validation<int, string>).OrElse<int>(null!)).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<ArgumentNullException>(() => Validation<int, string>.Valid(1).OrElse<int>(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("fallback");
        Should.Throw<InvalidOperationException>(() => Validation<int, string>.Invalid("e").OrElse(static _ => default(Validation<int, int>))).ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void OrElse_exception_propagates_unchanged() {
        var thrown = new ProbeException("fallback");

        Should.Throw<ProbeException>(() => Validation<int, string>.Invalid("e").OrElse<int>(_ => throw thrown)).ShouldBeSameAs(thrown);
    }

    [Fact]
    public void MapIssues_on_valid_keeps_value_without_invoking_selector() {
        int calls = 0;

        var result = Validation<string?, string>.Valid(null).MapIssues(issue => { calls++; return issue.Length; });

        result.ShouldBe(Validation<string?, int>.Valid(null));
        calls.ShouldBe(0);
    }

    [Fact]
    public void MapIssues_translates_every_position_once_in_order() {
        var seen = new List<string?>();

        var result = Validation<int, string?>.InvalidMany(["a", null, "a"]).MapIssues(issue => { seen.Add(issue); return issue?.Length; });

        result.ShouldBe(Validation<int, int?>.InvalidMany([1, null, 1]));
        seen.ShouldBe(["a", null, "a"]);
    }

    [Fact]
    public void MapIssues_keeps_positions_for_effectful_selector_and_default_results() {
        int counter = 0;

        var numbered = Validation<int, string>.InvalidMany(["x", "x"]).MapIssues(_ => ++counter);
        var defaults = Validation<int, string>.Invalid("x").MapIssues(static _ => (string?)null);

        numbered.ShouldBe(Validation<int, int>.InvalidMany([1, 2]));
        defaults.ShouldBe(Validation<int, string?>.Invalid(null));
    }

    [Fact]
    public void MapIssues_guards_receiver_then_selector_even_for_valid() {
        Should.Throw<InvalidOperationException>(() => default(Validation<int, string>).MapIssues<int>(null!)).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<ArgumentNullException>(() => Validation<int, string>.Valid(1).MapIssues<int>(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("selector");
    }

    [Fact]
    public void MapIssues_selector_exception_stops_translation_without_partial_outcome() {
        var seen = new List<string>();
        var thrown = new OperationCanceledException();

        var observed = Should.Throw<OperationCanceledException>(() => Validation<int, string>.InvalidMany(["a", "b", "c"]).MapIssues<int>(issue => {
            seen.Add(issue);
            return issue == "b" ? throw thrown : 0;
        })).ShouldBeOfType<OperationCanceledException>();

        observed.ShouldBeSameAs(thrown);
        seen.ShouldBe(["a", "b"]);
    }

    [Fact]
    public void MapIssues_uses_the_input_slice_even_when_the_receiver_variable_is_reassigned() {
        var receiver = Validation<int, string>.InvalidMany(["a", "b"]);
        var seen = new List<string>();

        var result = receiver.MapIssues(issue => {
            receiver = Validation<int, string>.InvalidMany(["z", "z", "z"]);
            seen.Add(issue);
            return issue.ToUpperInvariant();
        });

        result.ShouldBe(Validation<int, string>.InvalidMany(["A", "B"]));
        seen.ShouldBe(["a", "b"]);
    }

    [Fact]
    public void MapIssues_keeps_nested_and_task_issues_unawaited() {
        var result = Validation<int, string>.Invalid("x").MapIssues(static _ => default(Validation<int, int>));
        var tasks = Validation<int, string>.Invalid("x").MapIssues(static _ => Task.FromException(new ProbeException("task")));

        result.TryGetValue(out _, out var issues).ShouldBeFalse();
        issues.ShouldHaveSingleItem().IsInitialized.ShouldBeFalse();
        tasks.TryGetValue(out _, out var pending).ShouldBeFalse();
        pending.ShouldHaveSingleItem().IsFaulted.ShouldBeTrue();
    }
}
