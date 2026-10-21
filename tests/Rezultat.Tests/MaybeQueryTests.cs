using Rezultat.Linq;

namespace Rezultat.Tests;

public sealed class MaybeQueryTests
{
    [Fact]
    public void Select_and_where_have_map_and_filter_guards_even_for_none()
    {
        var selectException = Assert.Throws<ArgumentNullException>(() => Maybe<int>.None.Select<int, int>(null!));
        var whereException = Assert.Throws<ArgumentNullException>(() => Maybe<int>.None.Where(null!));

        Assert.Equal("selector", selectException.ParamName);
        Assert.Equal("predicate", whereException.ParamName);
    }

    [Fact]
    public void Select_maps_some_including_null()
    {
        Assert.Equal(Maybe<string?>.Some(null), Maybe<int>.Some(1).Select(static _ => (string?)null));
        Assert.Equal(Maybe<int>.None, Maybe<int>.None.Select<int, int>(static _ => throw new ProbeException("selector")));
    }

    [Fact]
    public void Where_keeps_matching_some_including_null_and_gives_none_otherwise()
    {
        var calls = 0;

        Assert.Equal(Maybe<string?>.Some(null), Maybe<string?>.Some(null).Where(value => { calls++; return value is null; }));
        Assert.Equal(Maybe<int>.None, Maybe<int>.Some(1).Where(_ => { calls++; return false; }));
        Assert.Equal(Maybe<int>.None, Maybe<int>.None.Where(_ => { calls++; return true; }));
        Assert.Equal(2, calls);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SelectMany_checks_next_then_project_even_for_none(bool some)
    {
        var source = some ? Maybe<int>.Some(1) : Maybe<int>.None;

        var nextException = Assert.Throws<ArgumentNullException>(() => source.SelectMany<int, int, int>(null!, null!));
        var projectException = Assert.Throws<ArgumentNullException>(
            () => source.SelectMany<int, int, int>(static value => Maybe<int>.Some(value), null!));

        Assert.Equal("next", nextException.ParamName);
        Assert.Equal("project", projectException.ParamName);
    }

    [Fact]
    public void SelectMany_over_none_and_default_calls_no_callbacks()
    {
        var calls = 0;

        Assert.Equal(Maybe<int>.None, default(Maybe<int>).SelectMany(
            value => { calls++; return Maybe<int>.Some(value); },
            (x, y) => { calls++; return x + y; }));
        Assert.Equal(0, calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SelectMany_with_none_or_default_next_does_not_project(bool useDefault)
    {
        var nextCalls = 0;
        var projectCalls = 0;

        var result = Maybe<int>.Some(1).SelectMany(
            _ => { nextCalls++; return useDefault ? default : Maybe<int>.None; },
            (x, y) => { projectCalls++; return x + y; });

        Assert.Equal(Maybe<int>.None, result);
        Assert.Equal((1, 0), (nextCalls, projectCalls));
    }

    [Fact]
    public void SelectMany_projects_original_and_next_payloads_once_including_null()
    {
        var calls = new List<string>();

        var result = Maybe<string?>.Some(null).SelectMany(
            value => { calls.Add("next"); Assert.Null(value); return Maybe<string?>.Some("y"); },
            (x, y) => { calls.Add("project"); return (string?)null; });

        Assert.Equal(Maybe<string?>.Some(null), result);
        Assert.Equal(["next", "project"], calls);
    }

    [Fact]
    public void Reassigning_the_source_from_callbacks_does_not_change_the_projected_value()
    {
        var source = Maybe<int>.Some(1);

        var result = source.SelectMany(
            _ => { source = Maybe<int>.Some(100); return Maybe<int>.Some(10); },
            (x, y) => { source = Maybe<int>.None; return x + y; });

        Assert.Equal(Maybe<int>.Some(11), result);
    }

    [Theory]
    [InlineData("next", false)]
    [InlineData("next", true)]
    [InlineData("project", false)]
    [InlineData("project", true)]
    [InlineData("where", false)]
    public void Callback_exceptions_and_cancellation_propagate(string thrower, bool cancel)
    {
        Exception thrown = cancel ? new OperationCanceledException() : new ProbeException(thrower);

        var observed = Record.Exception(() =>
            from x in Maybe<int>.Some(1)
            where thrower == "where" ? throw thrown : true
            from y in thrower == "next" ? throw thrown : Maybe<int>.Some(x)
            select thrower == "project" ? throw thrown : x + y);

        Assert.Same(thrown, observed);
    }

    [Fact]
    public void Query_expressions_with_one_to_three_sources_let_and_where()
    {
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

        Assert.Equal(Maybe<int>.Some(2), one);
        Assert.Equal(Maybe<int>.Some(3), two);
        Assert.Equal(Maybe<int>.Some(7), three);
        Assert.Equal(Maybe<int>.None, rejected);
        Assert.Equal(Maybe<int>.None, missing);
    }

    [Fact]
    public void Static_owner_resolves_calls_explicitly()
    {
        Assert.Equal(Maybe<int>.Some(3), MaybeQueryExtensions.SelectMany(Maybe<int>.Some(1), static x => Maybe<int>.Some(x + 1), static (x, y) => x + y));
        Assert.Equal(Maybe<int>.Some(2), MaybeQueryExtensions.Select(Maybe<int>.Some(1), static x => x + 1));
        Assert.Equal(Maybe<int>.None, MaybeQueryExtensions.Where(Maybe<int>.Some(1), static x => x > 1));
    }
}
