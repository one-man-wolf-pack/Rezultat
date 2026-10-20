namespace Rezultat.Tests;

public sealed class ResultTraverseTests
{
    [Fact]
    public void Null_source_is_rejected_before_selector()
    {
        var exception = Assert.Throws<ArgumentNullException>(
            () => ((IEnumerable<int>)null!).Traverse<int, int, string>(null!));

        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public void Null_selector_is_rejected_before_enumeration_even_for_empty_source()
    {
        var source = new TrackedSequence<int>();

        var exception = Assert.Throws<ArgumentNullException>(() => source.Traverse<int, int, string>(null!));

        Assert.Equal("selector", exception.ParamName);
        Assert.Equal(0, source.Enumerations);
    }

    [Fact]
    public void Empty_source_gives_empty_success_without_selector_calls()
    {
        var source = new TrackedSequence<int>();
        var calls = 0;

        var result = source.Traverse(value => Result<int, string>.Ok(++calls + value));

        Assert.True(result.TryGetValue(out var values, out _));
        Assert.Empty(values);
        Assert.Equal(0, calls);
        Assert.Equal((1, 1), (source.Enumerations, source.Disposals));
    }

    [Fact]
    public void All_successes_collect_values_in_source_order_in_one_pass()
    {
        var source = new TrackedSequence<int>(3, 1, 2);
        var visited = new List<int>();

        var result = source.Traverse(value =>
        {
            visited.Add(value);
            return Result<string, int>.Ok($"v{value}");
        });

        Assert.True(result.TryGetValue(out var values, out _));
        Assert.Equal(["v3", "v1", "v2"], values);
        Assert.Equal([3, 1, 2], visited);
        Assert.Equal((1, 4, 3, 1), (source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void First_visited_failure_is_returned_and_tail_is_not_touched(int failureIndex)
    {
        var errors = Enumerable.Range(0, 5).Select(index => new ReferenceProbe(index)).ToArray();
        var source = new TrackedSequence<int>(0, 1, 2, 3, 4) { PoisonFrom = failureIndex + 1 };
        var visited = new List<int>();

        var result = source.Traverse(index =>
        {
            visited.Add(index);
            return index >= failureIndex ? Result<int, ReferenceProbe>.Error(errors[index]) : Result<int, ReferenceProbe>.Ok(index);
        });

        Assert.False(result.TryGetValue(out _, out var observed));
        Assert.Same(errors[failureIndex], observed);
        Assert.Equal(Enumerable.Range(0, failureIndex + 1), visited);
        Assert.Equal((failureIndex + 1, failureIndex + 1, 1), (source.MoveNextCalls, source.CurrentReads, source.Disposals));
    }

    [Fact]
    public void Failure_keeps_null_error()
    {
        var result = new[] { 1, 2 }.Traverse(static value =>
            value == 2 ? Result<int, string?>.Error(null) : Result<int, string?>.Ok(value));

        Assert.Equal(Result<int[], string?>.Error(null), result);
    }

    [Fact]
    public void Failure_before_uninitialized_outcome_wins_and_the_uninitialized_one_is_not_visited()
    {
        var outcomes = new[] { Result<int, string>.Error("first"), default };
        var source = new TrackedSequence<Result<int, string>>(outcomes) { PoisonFrom = 1 };

        var result = source.Traverse(static outcome => outcome);

        Assert.Equal(Result<int[], string>.Error("first"), result);
        Assert.Equal(1, source.Disposals);
    }

    [Fact]
    public void Visited_uninitialized_outcome_throws_before_later_failure_and_disposes()
    {
        var source = new TrackedSequence<Result<int, string>>(default, Result<int, string>.Error("later")) { PoisonFrom = 1 };
        var calls = 0;

        Assert.Throws<InvalidOperationException>(() => source.Traverse(outcome => { calls++; return outcome; }));

        Assert.Equal((1, 1), (calls, source.Disposals));
    }

    [Fact]
    public void Null_and_default_success_payloads_are_kept_as_elements()
    {
        var references = new[] { "a", null }.Traverse(static value => Result<string?, int>.Ok(value));
        var values = new[] { 0, 5 }.Traverse(static value => Result<int, int>.Ok(value));

        Assert.True(references.TryGetValue(out var referenceValues, out _));
        Assert.Equal(["a", null], referenceValues);
        Assert.True(values.TryGetValue(out var valueValues, out _));
        Assert.Equal([0, 5], valueValues);
    }

    [Fact]
    public void Nested_carrier_and_task_payloads_are_not_checked_or_awaited()
    {
        var nested = new[] { 1 }.Traverse(static _ => Result<Result<int, string>, string>.Ok(default));
        var tasks = new[] { 1 }.Traverse(static _ => Result<Task<int>, string>.Ok(Task.FromException<int>(new ProbeException("task"))));

        Assert.True(nested.TryGetValue(out var inner, out _));
        Assert.False(Assert.Single(inner).IsInitialized);
        Assert.True(tasks.TryGetValue(out var pending, out _));
        Assert.True(Assert.Single(pending).IsFaulted);
    }

    [Fact]
    public void Success_array_is_materialized_independently_of_the_source()
    {
        var source = new List<int> { 1, 2 };
        var calls = 0;

        var result = source.Traverse(value => { calls++; return Result<int, string>.Ok(value); });
        source[0] = 10;
        source.Add(3);

        Assert.True(result.TryGetValue(out var values, out _));
        Assert.Equal([1, 2], values);
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Selector_exception_propagates_and_disposes_enumerator()
    {
        var thrown = new OperationCanceledException();
        var source = new TrackedSequence<int>(1, 2) { PoisonFrom = 1 };

        var observed = Assert.Throws<OperationCanceledException>(() => source.Traverse<int, int, string>(_ => throw thrown));

        Assert.Same(thrown, observed);
        Assert.Equal(1, source.Disposals);
    }

    [Fact]
    public void Source_exception_propagates_and_disposes_enumerator()
    {
        var source = new TrackedSequence<int>(1, 2) { PoisonFrom = 1 };
        var calls = 0;

        Assert.Throws<ProbeException>(() => source.Traverse(value => Result<int, string>.Ok(++calls + value)));

        Assert.Equal((1, 1), (calls, source.Disposals));
    }

    [Theory]
    [InlineData("success")]
    [InlineData("failure")]
    [InlineData("selector exception")]
    public void Dispose_exception_replaces_the_outcome_or_earlier_exception(string exit)
    {
        var disposeException = new ProbeException("dispose");
        var source = new TrackedSequence<int>(1) { DisposeException = disposeException };

        var observed = Assert.Throws<ProbeException>(() => source.Traverse(value => exit switch
        {
            "success" => Result<int, string>.Ok(value),
            "failure" => Result<int, string>.Error("e"),
            _ => throw new ProbeException("selector"),
        }));

        Assert.Same(disposeException, observed);
    }

    [Fact]
    public void Ready_outcomes_are_traversed_with_identity_selector()
    {
        Result<int, string>[] outcomes = [Result<int, string>.Ok(1), Result<int, string>.Ok(2)];

        var result = outcomes.Traverse(static outcome => outcome);

        Assert.True(result.TryGetValue(out var values, out _));
        Assert.Equal([1, 2], values);
    }
}
