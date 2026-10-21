using Rezultat.Collections;

namespace Rezultat.Tests;

public sealed class MaybeFirstAndSingleOrNoneTests
{
    [Fact]
    public void Null_source_is_rejected_before_predicate()
    {
        IEnumerable<int> source = null!;

        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => source.FirstOrNone()).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => source.FirstOrNone(null!)).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => source.SingleOrNone()).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => source.SingleOrNone(null!)).ParamName);
    }

    [Fact]
    public void Null_predicate_is_rejected_before_enumeration_even_for_empty_source()
    {
        var source = new TrackedSequence<int>();

        Assert.Equal("predicate", Assert.Throws<ArgumentNullException>(() => source.FirstOrNone(null!)).ParamName);
        Assert.Equal("predicate", Assert.Throws<ArgumentNullException>(() => source.SingleOrNone(null!)).ParamName);
        Assert.Equal(0, source.Enumerations);
    }

    [Fact]
    public void Empty_source_is_none_without_predicate_calls()
    {
        var source = new TrackedSequence<int>();
        var calls = 0;

        Assert.False(source.FirstOrNone().IsSome);
        Assert.False(source.FirstOrNone(_ => ++calls > 0).IsSome);
        Assert.False(source.SingleOrNone().IsSome);
        Assert.False(source.SingleOrNone(_ => ++calls > 0).IsSome);
        Assert.Equal(0, calls);
        Assert.Equal((4, 4), (source.Enumerations, source.Disposals));
    }

    [Fact]
    public void FirstOrNone_reads_only_the_first_element()
    {
        var source = new TrackedSequence<int>(7, 8) { PoisonFrom = 1 };

        Assert.Equal(Maybe<int>.Some(7), source.FirstOrNone());
        Assert.Equal((1, 1, 1), (source.MoveNextCalls, source.CurrentReads, source.Disposals));
    }

    [Fact]
    public void FirstOrNone_with_predicate_stops_at_first_match_and_does_not_read_the_tail()
    {
        var source = new TrackedSequence<int>(1, 2, 3, 4) { PoisonFrom = 3 };
        var visited = new List<int>();

        var found = source.FirstOrNone(value => { visited.Add(value); return value % 2 == 0 && value > 1; });

        Assert.Equal(Maybe<int>.Some(2), found);
        Assert.Equal([1, 2], visited);
        Assert.Equal((2, 2, 1), (source.MoveNextCalls, source.CurrentReads, source.Disposals));
    }

    [Fact]
    public void FirstOrNone_with_predicate_and_no_match_visits_all_once()
    {
        var source = new TrackedSequence<int>(1, 3, 5);
        var calls = 0;

        Assert.False(source.FirstOrNone(_ => { calls++; return false; }).IsSome);
        Assert.Equal((3, 1, 4, 3, 1), (calls, source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals));
    }

    [Fact]
    public void Found_null_default_and_nested_payloads_are_some()
    {
        Assert.Equal(Maybe<string?>.Some(null), new string?[] { null, "a" }.FirstOrNone());
        Assert.Equal(Maybe<int?>.Some(null), new int?[] { 1, null }.FirstOrNone(static value => !value.HasValue));
        Assert.Equal(Maybe<int>.Some(0), new[] { 0 }.SingleOrNone());
        Assert.Equal(Maybe<bool>.Some(false), new[] { true, false }.SingleOrNone(static value => !value));
        Assert.Equal(Maybe<string>.Some(""), new[] { "" }.FirstOrNone());
        Assert.Equal(Maybe<Maybe<int>>.Some(Maybe<int>.None), new[] { Maybe<int>.None, Maybe<int>.Some(1) }.FirstOrNone());
        Assert.True(new[] { default(Result<int, string>) }.SingleOrNone().TryGetValue(out var inner) && !inner.IsInitialized);
    }

    [Fact]
    public void Found_reference_payload_keeps_identity_without_payload_members()
    {
        var first = new ReferenceProbe(1);
        var second = new ReferenceProbe(1);

        Assert.True(new[] { first, second }.FirstOrNone().TryGetValue(out var found) && ReferenceEquals(first, found));
        Assert.Throws<InvalidOperationException>(() => new[] { first, second }.SingleOrNone());
        Assert.Equal((0, 0, 0), (first.EqualsCalls, first.HashCalls, first.ToStringCalls));
    }

    [Fact]
    public void SingleOrNone_without_predicate_does_not_read_the_second_element()
    {
        var one = new TrackedSequence<int>(5);
        var two = new TrackedSequence<int>(5, 6, 7) { PoisonFrom = 2 };

        Assert.Equal(Maybe<int>.Some(5), one.SingleOrNone());
        Assert.Equal((2, 1, 1), (one.MoveNextCalls, one.CurrentReads, one.Disposals));
        Assert.Throws<InvalidOperationException>(() => two.SingleOrNone());
        Assert.Equal((2, 1, 1), (two.MoveNextCalls, two.CurrentReads, two.Disposals));
    }

    [Fact]
    public void SingleOrNone_with_predicate_stops_at_second_match_counting_equal_values()
    {
        var source = new TrackedSequence<int>(1, 2, 3, 2, 9) { PoisonFrom = 4 };
        var visited = new List<int>();

        Assert.Throws<InvalidOperationException>(() => source.SingleOrNone(value => { visited.Add(value); return value == 2; }));
        Assert.Equal([1, 2, 3, 2], visited);
        Assert.Equal(1, source.Disposals);
    }

    [Fact]
    public void SingleOrNone_with_predicate_confirms_uniqueness_after_the_source_ends()
    {
        var source = new TrackedSequence<int>(1, 2, 3);
        var calls = 0;

        Assert.Equal(Maybe<int>.Some(2), source.SingleOrNone(value => { calls++; return value == 2; }));
        Assert.False(source.SingleOrNone(static _ => false).IsSome);
        Assert.Equal(3, calls);
        Assert.Equal(2, source.Disposals);
    }

    [Fact]
    public void Source_and_predicate_exceptions_propagate_and_dispose()
    {
        var canceled = new OperationCanceledException();
        var faulty = new TrackedSequence<int>(1, 2) { PoisonFrom = 0 };
        var source = new TrackedSequence<int>(1, 2);

        Assert.Throws<ProbeException>(() => faulty.FirstOrNone());
        Assert.Throws<ProbeException>(() => faulty.SingleOrNone());
        Assert.Same(canceled, Assert.Throws<OperationCanceledException>(() => source.FirstOrNone(_ => throw canceled)));
        Assert.Same(canceled, Assert.Throws<OperationCanceledException>(() => source.SingleOrNone(_ => throw canceled)));
        Assert.Equal((2, 2), (faulty.Disposals, source.Disposals));
    }

    [Theory]
    [InlineData("first")]
    [InlineData("first none")]
    [InlineData("single")]
    [InlineData("single none")]
    [InlineData("multiple")]
    [InlineData("predicate exception")]
    public void Dispose_exception_replaces_the_outcome_or_earlier_exception(string exit)
    {
        var disposeException = new ProbeException("dispose");
        var source = new TrackedSequence<int>(exit == "multiple" ? [1, 2] : [1]) { DisposeException = disposeException };

        var observed = Assert.Throws<ProbeException>(() => _ = exit switch
        {
            "first" => source.FirstOrNone(),
            "first none" => source.FirstOrNone(static _ => false),
            "single" => source.SingleOrNone(),
            "single none" => source.SingleOrNone(static _ => false),
            "multiple" => source.SingleOrNone(),
            _ => source.SingleOrNone(_ => throw new ProbeException("predicate")),
        });

        Assert.Same(disposeException, observed);
    }

    [Fact]
    public void Repeated_call_accesses_the_source_again()
    {
        var source = new TrackedSequence<int>(1);

        source.FirstOrNone();
        source.FirstOrNone();
        source.SingleOrNone();

        Assert.Equal(3, source.Enumerations);
    }

    [Fact]
    public void Infinite_source_finds_first_match()
    {
        Assert.Equal(Maybe<int>.Some(10), Naturals().FirstOrNone(static value => value >= 10));
        Assert.Throws<InvalidOperationException>(() => Naturals().SingleOrNone());
    }

    private static IEnumerable<int> Naturals()
    {
        for (var value = 0; ; value++)
        {
            yield return value;
        }
    }
}
