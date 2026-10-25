using Rezultat.Linq;

namespace Rezultat.Tests;

public sealed class MaybeQueryTests {
    [Fact]
    public void Select_and_where_have_map_and_filter_guards_even_for_none() {
        var selectException = Should.Throw<ArgumentNullException>(() => Maybe<int>.None.Select<int, int>(null!)).ShouldBeOfType<ArgumentNullException>();
        var whereException = Should.Throw<ArgumentNullException>(() => Maybe<int>.None.Where(null!)).ShouldBeOfType<ArgumentNullException>();

        selectException.ParamName.ShouldBe("selector");
        whereException.ParamName.ShouldBe("predicate");
    }

    [Fact]
    public void Select_maps_some_including_null() {
        Maybe<int>.Some(1).Select(static _ => (string?)null).ShouldBe(Maybe<string?>.Some(null));
        Maybe<int>.None.Select<int, int>(static _ => throw new ProbeException("selector")).ShouldBe(Maybe<int>.None);
    }

    [Fact]
    public void Where_keeps_matching_some_including_null_and_gives_none_otherwise() {
        var calls = 0;

        Maybe<string?>.Some(null).Where(value => { calls++; return value is null; }).ShouldBe(Maybe<string?>.Some(null));
        Maybe<int>.Some(1).Where(_ => { calls++; return false; }).ShouldBe(Maybe<int>.None);
        Maybe<int>.None.Where(_ => { calls++; return true; }).ShouldBe(Maybe<int>.None);
        calls.ShouldBe(2);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SelectMany_checks_next_then_project_even_for_none(bool some) {
        var source = some ? Maybe<int>.Some(1) : Maybe<int>.None;

        var nextException = Should.Throw<ArgumentNullException>(() => source.SelectMany<int, int, int>(null!, null!)).ShouldBeOfType<ArgumentNullException>();
        var projectException = Should.Throw<ArgumentNullException>(() => source.SelectMany<int, int, int>(static value => Maybe<int>.Some(value), null!)).ShouldBeOfType<ArgumentNullException>();

        nextException.ParamName.ShouldBe("next");
        projectException.ParamName.ShouldBe("project");
    }

    [Fact]
    public void SelectMany_over_none_and_default_calls_no_callbacks() {
        var calls = 0;

        default(Maybe<int>).SelectMany(
            value => { calls++; return Maybe<int>.Some(value); },
            (x, y) => { calls++; return x + y; }).ShouldBe(Maybe<int>.None);
        calls.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectMany_with_none_or_default_next_does_not_project(bool useDefault) {
        var nextCalls = 0;
        var projectCalls = 0;

        var result = Maybe<int>.Some(1).SelectMany(
            _ => { nextCalls++; return useDefault ? default : Maybe<int>.None; },
            (x, y) => { projectCalls++; return x + y; });

        result.ShouldBe(Maybe<int>.None);
        (nextCalls, projectCalls).ShouldBe((1, 0));
    }

    [Fact]
    public void SelectMany_projects_original_and_next_payloads_once_including_null() {
        var calls = new List<string>();

        var result = Maybe<string?>.Some(null).SelectMany(
            value => { calls.Add("next"); value.ShouldBeNull(); return Maybe<string?>.Some("y"); },
            (x, y) => { calls.Add("project"); return (string?)null; });

        result.ShouldBe(Maybe<string?>.Some(null));
        calls.ShouldBe(["next", "project"]);
    }

    [Fact]
    public void Reassigning_the_source_from_callbacks_does_not_change_the_projected_value() {
        var source = Maybe<int>.Some(1);

        var result = source.SelectMany(
            _ => { source = Maybe<int>.Some(100); return Maybe<int>.Some(10); },
            (x, y) => { source = Maybe<int>.None; return x + y; });

        result.ShouldBe(Maybe<int>.Some(11));
    }

    [Theory]
    [InlineData("next", false)]
    [InlineData("next", true)]
    [InlineData("project", false)]
    [InlineData("project", true)]
    [InlineData("where", false)]
    public void Callback_exceptions_and_cancellation_propagate(string thrower, bool cancel) {
        Exception thrown = cancel ? new OperationCanceledException() : new ProbeException(thrower);

        var observed = Record.Exception(() =>
            from x in Maybe<int>.Some(1)
            where thrower == "where" ? throw thrown : true
            from y in thrower == "next" ? throw thrown : Maybe<int>.Some(x)
            select thrower == "project" ? throw thrown : x + y);

        observed.ShouldBeSameAs(thrown);
    }

    [Fact]
    public void Query_expressions_with_one_to_three_sources_let_and_where() {
        static Maybe<int> Parse(string text) => int.TryParse(text, out var value) ? Maybe<int>.Some(value) : Maybe<int>.None;

        var one = from x in Parse("1") select x + 1;
        var two = from x in Parse("1") from y in Parse("2") select x + y;
        var three =
            from x in Parse("1")
            let doubled = x * 2
            from y in Parse("2")
            where y > 1
            from z in Parse("3")
            select doubled + y + z;
        var rejected = from x in Parse("1") where x > 1 select x;
        var missing = from x in Parse("1") from y in Parse("x") select x + y;

        one.ShouldBe(Maybe<int>.Some(2));
        two.ShouldBe(Maybe<int>.Some(3));
        three.ShouldBe(Maybe<int>.Some(7));
        rejected.ShouldBe(Maybe<int>.None);
        missing.ShouldBe(Maybe<int>.None);
    }

    [Fact]
    public void Static_owner_resolves_calls_explicitly() {
        MaybeQueryExtensions.SelectMany(Maybe<int>.Some(1), static x => Maybe<int>.Some(x + 1), static (x, y) => x + y).ShouldBe(Maybe<int>.Some(3));
        MaybeQueryExtensions.Select(Maybe<int>.Some(1), static x => x + 1).ShouldBe(Maybe<int>.Some(2));
        MaybeQueryExtensions.Where(Maybe<int>.Some(1), static x => x > 1).ShouldBe(Maybe<int>.None);
    }
}
