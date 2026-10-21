using Rezultat.Collections;

namespace Rezultat.Tests;

public sealed class ValidationValidateAllTests
{
    [Fact]
    public void Ready_form_rejects_null_source()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => ValidationCollectionExtensions.ValidateAll<int, string>(null!));

        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public void Selector_form_rejects_null_source_before_selector()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => ValidationCollectionExtensions.ValidateAll<int, int, string>(null!, null!));

        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public void Selector_form_rejects_null_selector_before_enumeration_even_for_empty_source()
    {
        var source = new TrackedSequence<int>();

        var exception = Assert.Throws<ArgumentNullException>(
            () => ValidationCollectionExtensions.ValidateAll<int, int, string>(source, null!));

        Assert.Equal("selector", exception.ParamName);
        Assert.Equal(0, source.Enumerations);
    }

    [Fact]
    public void Empty_source_gives_valid_unit()
    {
        var ready = new TrackedSequence<Validation<int, string>>();
        var mapped = new TrackedSequence<int>();
        var calls = 0;

        Assert.Equal(Validation<Unit, string>.Valid(Unit.Value), ready.ValidateAll());
        Assert.Equal(Validation<Unit, string>.Valid(Unit.Value), mapped.ValidateAll(value => { calls++; return Validation<int, string>.Valid(value); }));
        Assert.Equal((0, 1, 1, 1, 1), (calls, ready.Enumerations, ready.Disposals, mapped.Enumerations, mapped.Disposals));
    }

    [Fact]
    public void All_valid_gives_valid_unit_for_arbitrary_null_nested_and_large_payloads()
    {
        Assert.Equal(Validation<Unit, int>.Valid(Unit.Value), new[] { Validation<string?, int>.Valid(null) }.ValidateAll());
        Assert.Equal(Validation<Unit, int>.Valid(Unit.Value), new[] { Validation<Validation<int, int>, int>.Valid(default) }.ValidateAll());
        Assert.Equal(Validation<Unit, int>.Valid(Unit.Value), new[] { Validation<Unit, int>.Valid(Unit.Value) }.ValidateAll());
        Assert.Equal(
            Validation<Unit, int>.Valid(Unit.Value),
            Enumerable.Range(0, 3).ValidateAll(static value => Validation<LargePayload, int>.Valid(new LargePayload(value, value, value, value))));
        Assert.Equal(
            Validation<Unit, int>.Valid(Unit.Value),
            new[] { 1 }.ValidateAll(static _ => Validation<Task<int>, int>.Valid(Task.FromException<int>(new ProbeException("task")))));
    }

    [Fact]
    public void Selector_is_called_once_per_element_and_invalid_does_not_stop_traversal()
    {
        var source = new TrackedSequence<int>(0, 1, 2, 3, 4);
        var visited = new List<int>();

        var result = source.ValidateAll(value =>
        {
            visited.Add(value);
            return value switch
            {
                1 => Validation<string, string?>.InvalidMany(["1a", null]),
                3 => Validation<string, string?>.InvalidMany(["3a", "3a"]),
                _ => Validation<string, string?>.Valid($"v{value}"),
            };
        });

        Assert.Equal(Validation<Unit, string?>.InvalidMany(["1a", null, "3a", "3a"]), result);
        Assert.Equal([0, 1, 2, 3, 4], visited);
        Assert.Equal((1, 6, 5, 1), (source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals));
    }

    [Fact]
    public void Ready_form_accumulates_all_issues_in_order()
    {
        var source = new TrackedSequence<Validation<int, string?>>(
            Validation<int, string?>.Invalid(null),
            Validation<int, string?>.Valid(1),
            Validation<int, string?>.InvalidMany(["b", "b"]));

        Assert.Equal(Validation<Unit, string?>.InvalidMany([null, "b", "b"]), source.ValidateAll());
        Assert.Equal((1, 4, 3, 1), (source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals));
    }

    [Fact]
    public void Identical_invalid_inputs_are_added_per_position_without_equality()
    {
        var issue = new ReferenceProbe(1);
        var invalid = Validation<int, ReferenceProbe>.Invalid(issue);

        Assert.False(new[] { invalid, invalid }.ValidateAll().TryGetValue(out _, out var issues));
        Assert.Equal(2, issues.Length);
        Assert.All(issues, observed => Assert.Same(issue, observed));
        Assert.Equal(0, issue.EqualsCalls + issue.HashCalls + issue.ToStringCalls);
    }

    [Fact]
    public void Single_invalid_keeps_its_issue_buffer_in_both_forms()
    {
        var invalid = Validation<int, string>.InvalidMany(["a", "b"]);
        Assert.False(invalid.TryGetValue(out _, out var expected));

        Assert.False(new[] { Validation<int, string>.Valid(1), invalid }.ValidateAll().TryGetValue(out _, out var ready));
        Assert.False(new[] { 0, 1 }.ValidateAll(value => value == 1 ? invalid : Validation<int, string>.Valid(value)).TryGetValue(out _, out var mapped));

        Assert.True(ready == expected);
        Assert.True(mapped == expected);
    }

    [Fact]
    public void Many_issues_are_accumulated_linearly_in_order()
    {
        Assert.False(Enumerable.Range(0, 1000).ValidateAll(static value => Validation<int, int>.Invalid(value)).TryGetValue(out _, out var issues));

        Assert.Equal(Enumerable.Range(0, 1000), issues);
    }

    [Fact]
    public void Visited_default_throws_after_accumulated_issues_and_stops()
    {
        var ready = new TrackedSequence<Validation<int, string>>(
            Validation<int, string>.Invalid("first"), default, Validation<int, string>.Invalid("later"))
        { PoisonFrom = 2 };
        var mapped = new TrackedSequence<int>(0, 1, 2) { PoisonFrom = 2 };
        var calls = 0;

        Assert.Throws<InvalidOperationException>(() => ready.ValidateAll());
        Assert.Throws<InvalidOperationException>(() => mapped.ValidateAll(value =>
        {
            calls++;
            return value == 0 ? Validation<int, string>.Invalid("first") : default;
        }));

        Assert.Equal((2, 2, 1), (ready.MoveNextCalls, ready.CurrentReads, ready.Disposals));
        Assert.Equal((2, 2, 1), (calls, mapped.MoveNextCalls, mapped.Disposals));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Selector_exception_and_cancellation_after_invalid_stop_and_discard_issues(bool cancel)
    {
        Exception thrown = cancel ? new OperationCanceledException() : new ProbeException("selector");
        var source = new TrackedSequence<int>(0, 1, 2) { PoisonFrom = 2 };

        var observed = Record.Exception(() => source.ValidateAll(value =>
            value == 0 ? Validation<int, string>.Invalid("zero") : throw thrown));

        Assert.Same(thrown, observed);
        Assert.Equal((2, 1), (source.MoveNextCalls, source.Disposals));
    }

    [Fact]
    public void Source_exception_after_invalid_propagates_and_disposes()
    {
        var source = new TrackedSequence<Validation<int, string>>(Validation<int, string>.Invalid("e"), Validation<int, string>.Valid(1))
        {
            PoisonFrom = 1,
        };

        Assert.Throws<ProbeException>(() => source.ValidateAll());
        Assert.Equal(1, source.Disposals);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("invalid")]
    [InlineData("default")]
    [InlineData("selector exception")]
    public void Dispose_exception_replaces_the_outcome_or_earlier_exception(string exit)
    {
        var disposeException = new ProbeException("dispose");
        var source = new TrackedSequence<int>(1) { DisposeException = disposeException };

        var observed = Assert.Throws<ProbeException>(() => source.ValidateAll(value => exit switch
        {
            "valid" => Validation<int, string>.Valid(value),
            "invalid" => Validation<int, string>.Invalid("e"),
            "default" => default,
            _ => throw new ProbeException("selector"),
        }));

        Assert.Same(disposeException, observed);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("invalid")]
    [InlineData("default")]
    [InlineData("source exception")]
    public void Ready_form_dispose_exception_replaces_the_outcome_or_earlier_exception(string exit)
    {
        var disposeException = new ProbeException("dispose");
        Validation<int, string> item = exit switch
        {
            "valid" => Validation<int, string>.Valid(1),
            "invalid" or "source exception" => Validation<int, string>.Invalid("e"),
            _ => default,
        };
        var source = new TrackedSequence<Validation<int, string>>(item)
        {
            DisposeException = disposeException,
            PoisonFrom = exit == "source exception" ? 1 : int.MaxValue,
        };

        Assert.Same(disposeException, Assert.Throws<ProbeException>(() => source.ValidateAll()));
        Assert.Equal(1, source.Disposals);
    }

    [Fact]
    public void Ready_form_matches_identity_selector_form()
    {
        Validation<int, string>[] outcomes = [Validation<int, string>.Invalid("a"), Validation<int, string>.Valid(2), Validation<int, string>.Invalid("b")];

        Assert.Equal(outcomes.ValidateAll(static outcome => outcome), outcomes.ValidateAll());
    }

    private readonly record struct LargePayload(long A, long B, long C, long D);
}
