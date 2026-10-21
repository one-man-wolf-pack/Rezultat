using Rezultat.Linq;

namespace Rezultat.Tests;

public sealed class ResultQueryTests
{
    [Fact]
    public void Select_has_map_guards()
    {
        Result<int, string> uninitialized = default;

        Assert.Throws<InvalidOperationException>(() => uninitialized.Select<int, int, string>(null!));
        var exception = Assert.Throws<ArgumentNullException>(() => Result<int, string>.Error("e").Select<int, int, string>(null!));
        Assert.Equal("selector", exception.ParamName);
    }

    [Fact]
    public void Select_maps_ok_and_keeps_error_without_selector_call()
    {
        var error = new ReferenceProbe(1);
        var calls = 0;

        Assert.Equal(Result<string?, ReferenceProbe>.Ok(null), Result<int, ReferenceProbe>.Ok(1).Select(_ => { calls++; return (string?)null; }));
        Assert.False(Result<int, ReferenceProbe>.Error(error).Select(_ => { calls++; return 0; }).TryGetValue(out _, out var observed));
        Assert.Same(error, observed);
        Assert.Equal(1, calls);
    }

    [Fact]
    public void SelectMany_rejects_uninitialized_source_before_null_callbacks()
    {
        Result<int, string> uninitialized = default;

        Assert.Throws<InvalidOperationException>(() => uninitialized.SelectMany<int, int, int, string>(null!, null!));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SelectMany_checks_next_then_project_regardless_of_branch(bool ok)
    {
        var source = ok ? Result<int, string>.Ok(1) : Result<int, string>.Error("e");

        var nextException = Assert.Throws<ArgumentNullException>(() => source.SelectMany<int, int, int, string>(null!, null!));
        var projectException = Assert.Throws<ArgumentNullException>(
            () => source.SelectMany<int, int, int, string>(static value => Result<int, string>.Ok(value), null!));

        Assert.Equal("next", nextException.ParamName);
        Assert.Equal("project", projectException.ParamName);
    }

    [Fact]
    public void SelectMany_keeps_source_error_without_callbacks()
    {
        var error = new ReferenceProbe(1);
        var calls = 0;

        var result = Result<int, ReferenceProbe>.Error(error).SelectMany(
            value => { calls++; return Result<int, ReferenceProbe>.Ok(value); },
            (x, y) => { calls++; return x + y; });

        Assert.False(result.TryGetValue(out _, out var observed));
        Assert.Same(error, observed);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void SelectMany_keeps_next_error_without_project_call()
    {
        var error = new ReferenceProbe(2);
        var nextCalls = 0;
        var projectCalls = 0;

        var result = Result<int, ReferenceProbe>.Ok(1).SelectMany(
            value => { nextCalls++; return Result<string, ReferenceProbe>.Error(error); },
            (x, y) => { projectCalls++; return y; });

        Assert.False(result.TryGetValue(out _, out var observed));
        Assert.Same(error, observed);
        Assert.Equal((1, 0), (nextCalls, projectCalls));
    }

    [Fact]
    public void SelectMany_rejects_uninitialized_next_result_before_project()
    {
        var projectCalls = 0;

        Assert.Throws<InvalidOperationException>(() => Result<int, string>.Ok(1).SelectMany(
            static _ => default(Result<int, string>),
            (x, y) => { projectCalls++; return x + y; }));
        Assert.Equal(0, projectCalls);
    }

    [Fact]
    public void SelectMany_projects_original_and_next_values_once()
    {
        var first = new ReferenceProbe(1);
        var second = new ReferenceProbe(2);
        var calls = new List<string>();

        var result = Result<ReferenceProbe, string>.Ok(first).SelectMany(
            value => { calls.Add("next"); Assert.Same(first, value); return Result<ReferenceProbe, string>.Ok(second); },
            (x, y) => { calls.Add("project"); return (x, y); });

        Assert.True(result.TryGetValue(out var pair, out _));
        Assert.Same(first, pair.x);
        Assert.Same(second, pair.y);
        Assert.Equal(["next", "project"], calls);
    }

    [Fact]
    public void Null_payloads_errors_and_project_result_are_kept()
    {
        Assert.Equal(
            Result<string?, string>.Ok(null),
            Result<string?, string>.Ok(null).SelectMany(static x => Result<string?, string>.Ok(x), static (x, y) => (string?)null));
        Assert.Equal(
            Result<int, string?>.Error(null),
            Result<int, string?>.Ok(1).SelectMany(static _ => Result<int, string?>.Error(null), static (x, y) => x + y));
    }

    [Fact]
    public void Reassigning_the_source_from_callbacks_does_not_change_the_projected_value()
    {
        var source = Result<int, string>.Ok(1);

        var result = source.SelectMany(
            _ => { source = Result<int, string>.Ok(100); return Result<int, string>.Ok(10); },
            (x, y) => { source = Result<int, string>.Error("late"); return x + y; });

        Assert.Equal(Result<int, string>.Ok(11), result);
    }

    [Theory]
    [InlineData("next", false)]
    [InlineData("next", true)]
    [InlineData("project", false)]
    [InlineData("project", true)]
    public void Callback_exceptions_and_cancellation_propagate(string thrower, bool cancel)
    {
        Exception thrown = cancel ? new OperationCanceledException() : new ProbeException(thrower);

        var observed = Record.Exception(() => Result<int, string>.Ok(1).SelectMany(
            value => thrower == "next" ? throw thrown : Result<int, string>.Ok(value),
            (x, y) => thrower == "project" ? throw thrown : x + y));

        Assert.Same(thrown, observed);
    }

    [Fact]
    public void Query_expressions_with_one_to_three_sources_and_let()
    {
        static Result<int, string> Parse(string text) =>
            int.TryParse(text, out var value) ? Result<int, string>.Ok(value) : Result<int, string>.Error($"bad {text}");

        var one = from x in Parse("1") select x + 1;
        var two = from x in Parse("1") from y in Parse("2") select x + y;
        var three =
            from x in Parse("1")
            let doubled = x * 2
            from y in Parse("2")
            from z in Parse("3")
            select doubled + y + z;
        var stopped = new List<string>();
        var failed =
            from x in Parse("1")
            from y in Parse("x")
            from z in Track(stopped)
            select x + y + z;

        Assert.Equal(Result<int, string>.Ok(2), one);
        Assert.Equal(Result<int, string>.Ok(3), two);
        Assert.Equal(Result<int, string>.Ok(7), three);
        Assert.Equal(Result<int, string>.Error("bad x"), failed);
        Assert.Empty(stopped);
    }

    [Fact]
    public void Static_owner_resolves_calls_explicitly()
    {
        Assert.Equal(
            Result<int, string>.Ok(3),
            ResultQueryExtensions.SelectMany(Result<int, string>.Ok(1), static x => Result<int, string>.Ok(x + 1), static (x, y) => x + y));
        Assert.Equal(Result<int, string>.Ok(2), ResultQueryExtensions.Select(Result<int, string>.Ok(1), static x => x + 1));
    }

    private static Result<int, string> Track(List<string> calls)
    {
        calls.Add("evaluated");
        return Result<int, string>.Ok(0);
    }
}
