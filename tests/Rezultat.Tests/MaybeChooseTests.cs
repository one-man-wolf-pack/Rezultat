using Rezultat.Collections;

namespace Rezultat.Tests;

public sealed class MaybeChooseTests
{
    [Fact]
    public void Arguments_are_checked_on_the_call_without_enumeration()
    {
        var source = new TrackedSequence<int>();

        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ((IEnumerable<int>)null!).Choose<int, int>(null!)).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => source.Choose<int, int>(null!)).ParamName);
        Assert.Equal(0, source.Enumerations);
    }

    [Fact]
    public void Call_neither_enumerates_source_nor_invokes_selector()
    {
        var source = new TrackedSequence<int>(1, 2);
        var calls = 0;

        _ = source.Choose(value => { calls++; return Maybe<int>.Some(value); });

        Assert.Equal((0, 0), (source.Enumerations, calls));
    }

    [Fact]
    public void Somes_are_yielded_in_order_and_nones_skipped()
    {
        var source = new TrackedSequence<int>(1, 2, 3, 4, 5);
        var visited = new List<int>();

        var chosen = source.Choose(value =>
        {
            visited.Add(value);
            return value % 2 == 1 ? Maybe<string>.Some($"v{value}") : default;
        }).ToArray();

        Assert.Equal(["v1", "v3", "v5"], chosen);
        Assert.Equal([1, 2, 3, 4, 5], visited);
        Assert.Equal((1, 1), (source.Enumerations, source.Disposals));
    }

    [Fact]
    public void Present_null_default_and_nested_payloads_are_yielded()
    {
        Assert.Equal(["a", null, ""], new[] { "a", null, "" }.Choose(static value => Maybe<string?>.Some(value)));
        Assert.Equal([null, 0], new int?[] { null, 0 }.Choose(static value => Maybe<int?>.Some(value)));
        Assert.Equal([false], new[] { false }.Choose(static value => Maybe<bool>.Some(value)));
        Assert.False(Assert.Single(new[] { 1 }.Choose(static _ => Maybe<Maybe<int>>.Some(Maybe<int>.None))).IsSome);
        Assert.False(Assert.Single(new[] { 1 }.Choose(static _ => Maybe<Result<int, string>>.Some(default))).IsInitialized);
    }

    [Fact]
    public void Empty_and_all_none_sources_give_empty_sequences()
    {
        Assert.Empty(Array.Empty<int>().Choose(Maybe<int>.Some));
        Assert.Empty(new[] { 1, 2 }.Choose(static _ => Maybe<int>.None));
    }

    [Fact]
    public void Elements_are_read_only_on_demand_and_one_request_may_skip_several_nones()
    {
        var source = new TrackedSequence<int>(1, 2, 3, 4, 5);
        var visited = new List<int>();

        using var enumerator = source.Choose(value =>
        {
            visited.Add(value);
            return value is 1 or 4 ? Maybe<int>.Some(value) : Maybe<int>.None;
        }).GetEnumerator();

        Assert.Empty(visited);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(1, enumerator.Current);
        Assert.Equal([1], visited);
        Assert.True(enumerator.MoveNext());
        Assert.Equal(4, enumerator.Current);
        Assert.Equal([1, 2, 3, 4], visited);
        Assert.Equal(4, source.CurrentReads);
    }

    [Fact]
    public void Early_dispose_disposes_the_source_enumerator()
    {
        var source = new TrackedSequence<int>(1, 2, 3) { PoisonFrom = 1 };

        Assert.Equal(1, source.Choose(Maybe<int>.Some).First());
        Assert.Equal((1, 1), (source.MoveNextCalls, source.Disposals));
    }

    [Fact]
    public void Each_enumeration_accesses_source_and_selector_again_without_caching()
    {
        var source = new TrackedSequence<int>(1, 2);
        var calls = 0;
        var chosen = source.Choose(value => { calls++; return Maybe<int>.Some(value + calls); });

        Assert.Equal([2, 4], chosen.ToArray());
        Assert.Equal([4, 6], chosen.ToArray());
        Assert.Equal((2, 4), (source.Enumerations, calls));
    }

    [Fact]
    public void Infinite_source_yields_on_demand()
    {
        Assert.Equal([0, 2, 4], Naturals().Choose(static value => value % 2 == 0 ? Maybe<int>.Some(value) : Maybe<int>.None).Take(3));
    }

    [Fact]
    public void Source_and_selector_exceptions_propagate_after_earlier_yields_and_dispose()
    {
        var canceled = new OperationCanceledException();
        var source = new TrackedSequence<int>(1, 2);
        var faulty = new TrackedSequence<int>(1, 2) { PoisonFrom = 1 };
        var yielded = new List<int>();

        Assert.Same(canceled, Assert.Throws<OperationCanceledException>(() =>
        {
            foreach (var value in source.Choose(value => value == 2 ? throw canceled : Maybe<int>.Some(value)))
            {
                yielded.Add(value);
            }
        }));
        Assert.Throws<ProbeException>(() =>
        {
            foreach (var value in faulty.Choose(Maybe<int>.Some))
            {
                yielded.Add(value);
            }
        });

        Assert.Equal([1, 1], yielded);
        Assert.Equal((1, 1), (source.Disposals, faulty.Disposals));
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("early stop")]
    [InlineData("selector exception")]
    public void Dispose_exception_replaces_the_outcome_or_earlier_exception(string exit)
    {
        var disposeException = new ProbeException("dispose");
        var source = new TrackedSequence<int>(1, 2) { DisposeException = disposeException };
        var chosen = source.Choose(value => exit == "selector exception" ? throw new ProbeException("selector") : Maybe<int>.Some(value));

        var observed = Assert.Throws<ProbeException>(() => _ = exit == "early stop" ? chosen.First() : chosen.Count());

        Assert.Same(disposeException, observed);
    }

    [Fact]
    public void Ready_maybes_are_chosen_with_identity_selector()
    {
        Maybe<int>[] ready = [Maybe<int>.Some(1), default, Maybe<int>.Some(3)];

        Assert.Equal([1, 3], ready.Choose(static item => item));
    }

    private static IEnumerable<int> Naturals()
    {
        for (var value = 0; ; value++)
        {
            yield return value;
        }
    }
}
