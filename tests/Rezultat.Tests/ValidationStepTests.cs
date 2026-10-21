using System.Collections.Immutable;

namespace Rezultat.Tests;

public sealed class ValidationStepTests
{
    [Fact]
    public void AndThen_runs_next_once_on_valid_and_returns_its_outcome()
    {
        var seen = new List<int>();

        var valid = Validation<int, string>.Valid(2).AndThen(value => { seen.Add(value); return Validation<string, string>.Valid($"v{value}"); });
        var invalid = Validation<int, string>.Valid(2).AndThen(static _ => Validation<string, string>.Invalid("next"));

        Assert.Equal(Validation<string, string>.Valid("v2"), valid);
        Assert.Equal(Validation<string, string>.Invalid("next"), invalid);
        Assert.Equal([2], seen);
    }

    [Fact]
    public void AndThen_on_invalid_keeps_issues_without_invoking_next()
    {
        var calls = 0;

        var result = Validation<int, string>.InvalidMany(["a", "b"]).AndThen(value => { calls++; return Validation<string, string>.Valid("x"); });

        Assert.Equal(Validation<string, string>.InvalidMany(["a", "b"]), result);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void AndThen_guards_receiver_then_next_then_returned_outcome()
    {
        Assert.Throws<InvalidOperationException>(() => default(Validation<int, string>).AndThen<int>(null!));
        Assert.Equal("next", Assert.Throws<ArgumentNullException>(() => Validation<int, string>.Invalid("e").AndThen<int>(null!)).ParamName);
        Assert.Throws<InvalidOperationException>(() => Validation<int, string>.Valid(1).AndThen(static _ => default(Validation<int, string>)));
    }

    [Fact]
    public void AndThen_accepts_valid_null_and_nested_default_payloads()
    {
        Assert.Equal(Validation<string?, string>.Valid(null), Validation<int, string>.Valid(1).AndThen(static _ => Validation<string?, string>.Valid(null)));
        var nested = Validation<int, string>.Valid(1).AndThen(static _ => Validation<Validation<int, string>, string>.Valid(default));
        Assert.True(nested.TryGetValue(out var inner, out _) && !inner.IsInitialized);
    }

    [Fact]
    public void AndThen_exception_propagates_unchanged()
    {
        var thrown = new OperationCanceledException();

        Assert.Same(thrown, Assert.Throws<OperationCanceledException>(() => Validation<int, string>.Valid(1).AndThen<int>(_ => throw thrown)));
    }

    [Fact]
    public void OrElse_on_valid_keeps_value_with_new_issue_type_without_fallback()
    {
        var calls = 0;
        var payload = new ReferenceProbe(1);

        var result = Validation<ReferenceProbe, string>.Valid(payload).OrElse(issues => { calls++; return Validation<ReferenceProbe, int>.Invalid(issues.Length); });

        Assert.True(result.TryGetValue(out var value, out _));
        Assert.Same(payload, value);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void OrElse_on_invalid_passes_all_issues_once_and_replaces_the_outcome()
    {
        var received = new List<ImmutableArray<string?>>();
        var invalid = Validation<int, string?>.InvalidMany(["a", null, "a"]);

        var recovered = invalid.OrElse(issues => { received.Add(issues); return Validation<int, int>.Valid(issues.Length); });
        var replaced = invalid.OrElse(static _ => Validation<int, string?>.Invalid("only"));

        Assert.Equal(Validation<int, int>.Valid(3), recovered);
        Assert.Equal(["a", null, "a"], Assert.Single(received));
        Assert.Equal(Validation<int, string?>.Invalid("only"), replaced);
    }

    [Fact]
    public void OrElse_with_InvalidMany_keeps_the_original_failure_without_duplicates()
    {
        var invalid = Validation<int, string>.InvalidMany(["a", "b"]);

        Assert.Equal(invalid, invalid.OrElse(Validation<int, string>.InvalidMany));
    }

    [Fact]
    public void OrElse_guards_receiver_then_fallback_then_returned_outcome()
    {
        Assert.Throws<InvalidOperationException>(() => default(Validation<int, string>).OrElse<int>(null!));
        Assert.Equal("fallback", Assert.Throws<ArgumentNullException>(() => Validation<int, string>.Valid(1).OrElse<int>(null!)).ParamName);
        Assert.Throws<InvalidOperationException>(() => Validation<int, string>.Invalid("e").OrElse(static _ => default(Validation<int, int>)));
    }

    [Fact]
    public void OrElse_exception_propagates_unchanged()
    {
        var thrown = new ProbeException("fallback");

        Assert.Same(thrown, Assert.Throws<ProbeException>(() => Validation<int, string>.Invalid("e").OrElse<int>(_ => throw thrown)));
    }

    [Fact]
    public void MapIssues_on_valid_keeps_value_without_invoking_selector()
    {
        var calls = 0;

        var result = Validation<string?, string>.Valid(null).MapIssues(issue => { calls++; return issue.Length; });

        Assert.Equal(Validation<string?, int>.Valid(null), result);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void MapIssues_translates_every_position_once_in_order()
    {
        var seen = new List<string?>();

        var result = Validation<int, string?>.InvalidMany(["a", null, "a"]).MapIssues(issue => { seen.Add(issue); return issue?.Length; });

        Assert.Equal(Validation<int, int?>.InvalidMany([1, null, 1]), result);
        Assert.Equal(["a", null, "a"], seen);
    }

    [Fact]
    public void MapIssues_keeps_positions_for_effectful_selector_and_default_results()
    {
        var counter = 0;

        var numbered = Validation<int, string>.InvalidMany(["x", "x"]).MapIssues(_ => ++counter);
        var defaults = Validation<int, string>.Invalid("x").MapIssues(static _ => (string?)null);

        Assert.Equal(Validation<int, int>.InvalidMany([1, 2]), numbered);
        Assert.Equal(Validation<int, string?>.Invalid(null), defaults);
    }

    [Fact]
    public void MapIssues_guards_receiver_then_selector_even_for_valid()
    {
        Assert.Throws<InvalidOperationException>(() => default(Validation<int, string>).MapIssues<int>(null!));
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => Validation<int, string>.Valid(1).MapIssues<int>(null!)).ParamName);
    }

    [Fact]
    public void MapIssues_selector_exception_stops_translation_without_partial_outcome()
    {
        var seen = new List<string>();
        var thrown = new OperationCanceledException();

        var observed = Assert.Throws<OperationCanceledException>(() => Validation<int, string>.InvalidMany(["a", "b", "c"]).MapIssues<int>(issue =>
        {
            seen.Add(issue);
            return issue == "b" ? throw thrown : 0;
        }));

        Assert.Same(thrown, observed);
        Assert.Equal(["a", "b"], seen);
    }

    [Fact]
    public void MapIssues_uses_the_input_slice_even_when_the_receiver_variable_is_reassigned()
    {
        var receiver = Validation<int, string>.InvalidMany(["a", "b"]);
        var seen = new List<string>();

        var result = receiver.MapIssues(issue =>
        {
            receiver = Validation<int, string>.InvalidMany(["z", "z", "z"]);
            seen.Add(issue);
            return issue.ToUpperInvariant();
        });

        Assert.Equal(Validation<int, string>.InvalidMany(["A", "B"]), result);
        Assert.Equal(["a", "b"], seen);
    }

    [Fact]
    public void MapIssues_keeps_nested_and_task_issues_unawaited()
    {
        var result = Validation<int, string>.Invalid("x").MapIssues(static _ => default(Validation<int, int>));
        var tasks = Validation<int, string>.Invalid("x").MapIssues(static _ => Task.FromException(new ProbeException("task")));

        Assert.False(result.TryGetValue(out _, out var issues));
        Assert.False(Assert.Single(issues).IsInitialized);
        Assert.False(tasks.TryGetValue(out _, out var pending));
        Assert.True(Assert.Single(pending).IsFaulted);
    }
}
