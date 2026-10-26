using Rezultat.Linq;

namespace Rezultat.Tests;

public sealed class ResultQueryTests {
    [Fact]
    public void Select_has_map_guards() {
        Result<int, string> uninitialized = default;

        Should.Throw<InvalidOperationException>(() => uninitialized.Select<int, int, string>(null!)).ShouldBeOfType<InvalidOperationException>();
        var exception = Should.Throw<ArgumentNullException>(() => Result<int, string>.Error("e").Select<int, int, string>(null!)).ShouldBeOfType<ArgumentNullException>();
        exception.ParamName.ShouldBe("selector");
    }

    [Fact]
    public void Select_maps_ok_and_keeps_error_without_selector_call() {
        var error = new ReferenceProbe(1);
        int calls = 0;

        Result<int, ReferenceProbe>.Ok(1).Select(_ => { calls++; return (string?)null; }).ShouldBe(Result<string?, ReferenceProbe>.Ok(null));
        Result<int, ReferenceProbe>.Error(error).Select(_ => { calls++; return 0; }).TryGetValue(out _, out var observed).ShouldBeFalse();
        observed.ShouldBeSameAs(error);
        calls.ShouldBe(1);
    }

    [Fact]
    public void SelectMany_rejects_uninitialized_source_before_null_callbacks() {
        Result<int, string> uninitialized = default;

        Should.Throw<InvalidOperationException>(() => uninitialized.SelectMany<int, int, int, string>(null!, null!)).ShouldBeOfType<InvalidOperationException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SelectMany_checks_next_then_project_regardless_of_branch(bool ok) {
        var source = ok ? Result<int, string>.Ok(1) : Result<int, string>.Error("e");

        var nextException = Should.Throw<ArgumentNullException>(() => source.SelectMany<int, int, int, string>(null!, null!)).ShouldBeOfType<ArgumentNullException>();
        var projectException = Should.Throw<ArgumentNullException>(() => source.SelectMany<int, int, int, string>(static value => Result<int, string>.Ok(value), null!)).ShouldBeOfType<ArgumentNullException>();

        nextException.ParamName.ShouldBe("next");
        projectException.ParamName.ShouldBe("project");
    }

    [Fact]
    public void SelectMany_keeps_source_error_without_callbacks() {
        var error = new ReferenceProbe(1);
        int calls = 0;

        var result = Result<int, ReferenceProbe>.Error(error).SelectMany(
            value => { calls++; return Result<int, ReferenceProbe>.Ok(value); },
            (x, y) => { calls++; return x + y; });

        result.TryGetValue(out _, out var observed).ShouldBeFalse();
        observed.ShouldBeSameAs(error);
        calls.ShouldBe(0);
    }

    [Fact]
    public void SelectMany_keeps_next_error_without_project_call() {
        var error = new ReferenceProbe(2);
        int nextCalls = 0;
        int projectCalls = 0;

        var result = Result<int, ReferenceProbe>.Ok(1).SelectMany(
            value => { nextCalls++; return Result<string, ReferenceProbe>.Error(error); },
            (x, y) => { projectCalls++; return y; });

        result.TryGetValue(out _, out var observed).ShouldBeFalse();
        observed.ShouldBeSameAs(error);
        (nextCalls, projectCalls).ShouldBe((1, 0));
    }

    [Fact]
    public void SelectMany_rejects_uninitialized_next_result_before_project() {
        int projectCalls = 0;

        Should.Throw<InvalidOperationException>(() => Result<int, string>.Ok(1).SelectMany(
            static _ => default(Result<int, string>),
            (x, y) => { projectCalls++; return x + y; })).ShouldBeOfType<InvalidOperationException>();
        projectCalls.ShouldBe(0);
    }

    [Fact]
    public void SelectMany_projects_original_and_next_values_once() {
        var first = new ReferenceProbe(1);
        var second = new ReferenceProbe(2);
        var calls = new List<string>();

        var result = Result<ReferenceProbe, string>.Ok(first).SelectMany(
            value => { calls.Add("next"); value.ShouldBeSameAs(first); return Result<ReferenceProbe, string>.Ok(second); },
            (x, y) => { calls.Add("project"); return (x, y); });

        result.TryGetValue(out var pair, out _).ShouldBeTrue();
        pair.x.ShouldBeSameAs(first);
        pair.y.ShouldBeSameAs(second);
        calls.ShouldBe(["next", "project"]);
    }

    [Fact]
    public void Null_payloads_errors_and_project_result_are_kept() {
        Result<string?, string>.Ok(null).SelectMany(static x => Result<string?, string>.Ok(x), static (x, y) => (string?)null).ShouldBe(Result<string?, string>.Ok(null));
        Result<int, string?>.Ok(1).SelectMany(static _ => Result<int, string?>.Error(null), static (x, y) => x + y).ShouldBe(Result<int, string?>.Error(null));
    }

    [Fact]
    public void Reassigning_the_source_from_callbacks_does_not_change_the_projected_value() {
        var source = Result<int, string>.Ok(1);

        var result = source.SelectMany(
            _ => { source = Result<int, string>.Ok(100); return Result<int, string>.Ok(10); },
            (x, y) => { source = Result<int, string>.Error("late"); return x + y; });

        result.ShouldBe(Result<int, string>.Ok(11));
    }

    [Theory]
    [InlineData("next", false)]
    [InlineData("next", true)]
    [InlineData("project", false)]
    [InlineData("project", true)]
    public void Callback_exceptions_and_cancellation_propagate(string thrower, bool cancel) {
        Exception thrown = cancel ? new OperationCanceledException() : new ProbeException(thrower);

        var observed = Record.Exception(() => Result<int, string>.Ok(1).SelectMany(
            value => thrower == "next" ? throw thrown : Result<int, string>.Ok(value),
            (x, y) => thrower == "project" ? throw thrown : x + y));

        observed.ShouldBeSameAs(thrown);
    }

    [Fact]
    public void Query_expressions_with_one_to_three_sources_and_let() {
        static Result<int, string> Parse(string text) =>
            int.TryParse(text, out int value) ? Result<int, string>.Ok(value) : Result<int, string>.Error($"bad {text}");

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

        one.ShouldBe(Result<int, string>.Ok(2));
        two.ShouldBe(Result<int, string>.Ok(3));
        three.ShouldBe(Result<int, string>.Ok(7));
        failed.ShouldBe(Result<int, string>.Error("bad x"));
        stopped.ShouldBeEmpty();
    }

    [Fact]
    public void Static_owner_resolves_calls_explicitly() {
        ResultQueryExtensions.SelectMany(Result<int, string>.Ok(1), static x => Result<int, string>.Ok(x + 1), static (x, y) => x + y).ShouldBe(Result<int, string>.Ok(3));
        ResultQueryExtensions.Select(Result<int, string>.Ok(1), static x => x + 1).ShouldBe(Result<int, string>.Ok(2));
    }

    private static Result<int, string> Track(List<string> calls) {
        calls.Add("evaluated");
        return Result<int, string>.Ok(0);
    }
}
