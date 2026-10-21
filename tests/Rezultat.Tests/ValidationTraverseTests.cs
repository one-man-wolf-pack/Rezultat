using System.Collections.Immutable;
using Rezultat.Collections;

namespace Rezultat.Tests;

public sealed class ValidationTraverseTests
{
    [Fact]
    public void Null_source_is_rejected_before_selector()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => ValidationCollectionExtensions.Traverse<int, int, string>(null!, null!));

        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public void Null_selector_is_rejected_before_enumeration_even_for_empty_source()
    {
        var source = new TrackedSequence<int>();

        var exception = Assert.Throws<ArgumentNullException>(() => ValidationCollectionExtensions.Traverse<int, int, string>(source, null!));

        Assert.Equal("selector", exception.ParamName);
        Assert.Equal(0, source.Enumerations);
    }

    [Fact]
    public void Empty_source_gives_valid_empty_array()
    {
        var source = new TrackedSequence<int>();
        var calls = 0;

        var result = source.Traverse(value => Validation<int, string>.Valid(++calls + value));

        Assert.True(result.TryGetValue(out var values, out _));
        Assert.Empty(values);
        Assert.Equal((0, 1, 1), (calls, source.Enumerations, source.Disposals));
    }

    [Fact]
    public void All_valid_collects_values_in_source_order_in_one_pass()
    {
        var source = new TrackedSequence<int>(3, 1, 2);
        var visited = new List<int>();

        var result = source.Traverse(value => { visited.Add(value); return Validation<string, int>.Valid($"v{value}"); });

        Assert.True(result.TryGetValue(out var values, out _));
        Assert.Equal(["v3", "v1", "v2"], values);
        Assert.Equal([3, 1, 2], visited);
        Assert.Equal((1, 4, 3, 1), (source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals));
    }

    [Fact]
    public void Invalid_does_not_stop_traversal_and_issues_follow_element_then_inner_order()
    {
        var source = new TrackedSequence<int>(0, 1, 2, 3, 4);
        var visited = new List<int>();

        var result = source.Traverse(value =>
        {
            visited.Add(value);
            return value switch
            {
                1 => Validation<int, string?>.InvalidMany(["1a", null]),
                3 => Validation<int, string?>.InvalidMany(["3a", "3a"]),
                _ => Validation<int, string?>.Valid(value),
            };
        });

        Assert.Equal(Validation<int[], string?>.InvalidMany(["1a", null, "3a", "3a"]), result);
        Assert.Equal([0, 1, 2, 3, 4], visited);
        Assert.Equal((1, 6, 1), (source.Enumerations, source.MoveNextCalls, source.Disposals));
    }

    [Fact]
    public void Single_invalid_is_the_result_without_a_partial_target()
    {
        var result = new[] { 1, 2, 3 }.Traverse(static value =>
            value == 2 ? Validation<int, string>.Invalid("two") : Validation<int, string>.Valid(value));

        Assert.False(result.TryGetValue(out var values, out var issues));
        Assert.Null(values);
        Assert.Equal(["two"], issues);
    }

    [Fact]
    public void Visited_uninitialized_outcome_throws_after_accumulated_issues_and_stops()
    {
        var source = new TrackedSequence<Validation<int, string>>(
            Validation<int, string>.Invalid("first"), default, Validation<int, string>.Invalid("later"))
        { PoisonFrom = 2 };
        var calls = 0;

        Assert.Throws<InvalidOperationException>(() => source.Traverse(outcome => { calls++; return outcome; }));

        Assert.Equal((2, 2, 1), (calls, source.MoveNextCalls, source.Disposals));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Selector_exception_and_cancellation_stop_traversal_and_discard_issues(bool cancel)
    {
        Exception thrown = cancel ? new OperationCanceledException() : new ProbeException("selector");
        var source = new TrackedSequence<int>(0, 1, 2) { PoisonFrom = 2 };

        var observed = Record.Exception(() => source.Traverse(value =>
            value == 0 ? Validation<int, string>.Invalid("zero") : throw thrown));

        Assert.Same(thrown, observed);
        Assert.Equal((2, 1), (source.MoveNextCalls, source.Disposals));
    }

    [Fact]
    public void Source_exception_propagates_after_invalid_and_disposes()
    {
        var source = new TrackedSequence<int>(0, 1) { PoisonFrom = 1 };

        Assert.Throws<ProbeException>(() => source.Traverse(static _ => Validation<int, string>.Invalid("e")));

        Assert.Equal(1, source.Disposals);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("invalid")]
    [InlineData("selector exception")]
    public void Dispose_exception_replaces_the_outcome_or_earlier_exception(string exit)
    {
        var disposeException = new ProbeException("dispose");
        var source = new TrackedSequence<int>(1) { DisposeException = disposeException };

        var observed = Assert.Throws<ProbeException>(() => source.Traverse(value => exit switch
        {
            "valid" => Validation<int, string>.Valid(value),
            "invalid" => Validation<int, string>.Invalid("e"),
            _ => throw new ProbeException("selector"),
        }));

        Assert.Same(disposeException, observed);
    }

    [Fact]
    public void Null_default_and_nested_payloads_are_kept_as_values()
    {
        var references = new[] { "a", null }.Traverse(static value => Validation<string?, int>.Valid(value));
        var nested = new[] { 1 }.Traverse(static _ => Validation<Validation<int, int>, int>.Valid(default));
        var tasks = new[] { 1 }.Traverse(static _ => Validation<Task<int>, int>.Valid(Task.FromException<int>(new ProbeException("task"))));

        Assert.True(references.TryGetValue(out var referenceValues, out _));
        Assert.Equal(["a", null], referenceValues);
        Assert.True(nested.TryGetValue(out var inner, out _) && !Assert.Single(inner).IsInitialized);
        Assert.True(tasks.TryGetValue(out var pending, out _) && Assert.Single(pending).IsFaulted);
    }

    [Fact]
    public void Success_array_is_independent_of_later_source_changes()
    {
        var source = new List<int> { 1, 2 };

        var result = source.Traverse(static value => Validation<int, string>.Valid(value));
        source[0] = 10;

        Assert.True(result.TryGetValue(out var values, out _));
        Assert.Equal([1, 2], values);
    }

    [Fact]
    public void Ready_outcomes_use_identity_selector()
    {
        Validation<int, string>[] outcomes = [Validation<int, string>.Invalid("a"), Validation<int, string>.Valid(2), Validation<int, string>.Invalid("b")];

        Assert.Equal(Validation<int[], string>.InvalidMany(["a", "b"]), outcomes.Traverse(static outcome => outcome));
    }

    [Fact]
    public void Result_and_maybe_traverse_families_keep_binding_next_to_validation()
    {
        var results = new[] { 1 }.Traverse(static value => Result<int, string>.Ok(value));
        var maybes = new[] { 1 }.Traverse(static value => Maybe<int>.Some(value));
        var validations = new[] { 1 }.Traverse(static value => Validation<int, string>.Valid(value));
        var maybeReceiver = Maybe<int>.Some(1).Traverse(static value => Result<int, string>.Ok(value));

        Assert.IsType<Result<int[], string>>(results);
        Assert.IsType<Maybe<int[]>>(maybes);
        Assert.IsType<Validation<int[], string>>(validations);
        Assert.IsType<Result<Maybe<int>, string>>(maybeReceiver);
    }

    [Fact]
    public void Traverse_families_bind_method_groups_and_identity_over_ready_outcomes()
    {
        Assert.IsType<Result<int[], string>>(new[] { 1 }.Traverse(ToResult));
        Assert.IsType<Maybe<int[]>>(new[] { 1 }.Traverse(ToMaybe));
        Assert.IsType<Validation<int[], string>>(new[] { 1 }.Traverse(ToValidation));
        Assert.IsType<Result<Maybe<int>, string>>(Maybe<int>.Some(1).Traverse(ToResult));
        Assert.IsType<Result<int[], string>>(new[] { Result<int, string>.Ok(1) }.Traverse(static outcome => outcome));
        Assert.IsType<Maybe<int[]>>(new[] { Maybe<int>.Some(1) }.Traverse(static outcome => outcome));
        Assert.IsType<Validation<int[], string>>(new[] { Validation<int, string>.Valid(1) }.Traverse(static outcome => outcome));
        Assert.IsType<Result<Maybe<int>, string>>(Maybe<Result<int, string>>.Some(Result<int, string>.Ok(1)).Traverse(static outcome => outcome));
    }

    [Fact]
    public void Issues_from_many_invalids_are_accumulated_in_order()
    {
        var result = Enumerable.Range(0, 100).Traverse(static value => Validation<int, int>.Invalid(value));

        Assert.False(result.TryGetValue(out _, out var issues));
        Assert.Equal(Enumerable.Range(0, 100), issues);
    }

    [Fact]
    public void Generic_issue_type_is_chosen_by_the_selector()
    {
        var result = new[] { "x" }.Traverse(static text => Validation<int, ImmutableArray<string>>.Invalid([text]));

        Assert.False(result.TryGetValue(out _, out var issues));
        Assert.Equal(["x"], Assert.Single(issues));
    }

    private static Result<int, string> ToResult(int value) => Result<int, string>.Ok(value);

    private static Maybe<int> ToMaybe(int value) => Maybe<int>.Some(value);

    private static Validation<int, string> ToValidation(int value) => Validation<int, string>.Valid(value);
}
