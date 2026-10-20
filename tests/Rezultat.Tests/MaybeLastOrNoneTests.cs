namespace Rezultat.Tests;

public sealed class MaybeLastOrNoneTests
{
    [Fact]
    public void Null_source_is_rejected_before_predicate_and_null_predicate_before_access()
    {
        var list = new TrackedList<int>(1);

        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ((IEnumerable<int>)null!).LastOrNone()).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ((IEnumerable<int>)null!).LastOrNone(null!)).ParamName);
        Assert.Equal("predicate", Assert.Throws<ArgumentNullException>(() => list.LastOrNone(null!)).ParamName);
        Assert.Equal("predicate", Assert.Throws<ArgumentNullException>(() => new TrackedList<int>().LastOrNone(null!)).ParamName);
        Assert.Equal((0, 0, 0), (list.List.Counts, list.ReadOnly.Counts, list.List.Enumerations));
    }

    [Fact]
    public void List_without_predicate_reads_count_once_and_only_the_last_position()
    {
        var list = new TrackedList<int>(1, 2, 3);
        var empty = new TrackedList<int>();

        Assert.Equal(Maybe<int>.Some(3), list.LastOrNone());
        Assert.False(empty.LastOrNone().IsSome);
        Assert.Equal((1, 0), (list.List.Counts, list.List.Enumerations));
        Assert.Equal([2], list.List.Reads);
        Assert.Equal((1, 0), (empty.List.Counts, empty.List.Reads.Count));
    }

    [Fact]
    public void List_with_predicate_checks_backwards_and_stops_at_first_match()
    {
        var list = new TrackedList<int>(1, 2, 3, 4, 5);
        var visited = new List<int>();

        var found = list.LastOrNone(value => { visited.Add(value); return value % 2 == 0; });

        Assert.Equal(Maybe<int>.Some(4), found);
        Assert.Equal([5, 4], visited);
        Assert.Equal([4, 3], list.List.Reads);
        Assert.Equal((1, 0), (list.List.Counts, list.List.Enumerations));
    }

    [Fact]
    public void List_with_predicate_and_no_match_reads_every_position_once()
    {
        var list = new TrackedList<int>(1, 2, 3);
        var empty = new TrackedList<int>();
        var calls = 0;

        Assert.False(list.LastOrNone(_ => { calls++; return false; }).IsSome);
        Assert.False(empty.LastOrNone(_ => { calls++; return true; }).IsSome);
        Assert.Equal(3, calls);
        Assert.Equal([2, 1, 0], list.List.Reads);
        Assert.Equal(1, list.List.Counts);
    }

    [Fact]
    public void IList_takes_priority_over_diverging_IReadOnlyList()
    {
        var list = new TrackedList<string>(["list-a", "list-b"], ["read-only"]);

        Assert.Equal(Maybe<string>.Some("list-b"), list.LastOrNone());
        Assert.Equal(Maybe<string>.Some("list-a"), list.LastOrNone(static value => value.EndsWith('a')));
        Assert.Equal((0, 0), (list.ReadOnly.Counts, list.ReadOnly.Reads.Count));
    }

    [Fact]
    public void IReadOnlyList_without_IList_uses_its_count_and_indexer()
    {
        var inner = new TrackedList<int>([9, 9], [1, 2, 3]);
        var readOnly = new ReadOnlyListOnly<int>(inner);
        var visited = new List<int>();

        Assert.Equal(Maybe<int>.Some(3), readOnly.LastOrNone());
        Assert.Equal(Maybe<int>.Some(1), readOnly.LastOrNone(value => { visited.Add(value); return value == 1; }));
        Assert.Equal([3, 2, 1], visited);
        Assert.Equal(2, inner.ReadOnly.Counts);
        Assert.Equal([2, 2, 1, 0], inner.ReadOnly.Reads);
        Assert.Equal((0, 0), (inner.List.Counts, inner.List.Enumerations));
    }

    [Fact]
    public void Sequence_is_enumerated_forwards_to_the_end_reading_each_element_once()
    {
        var source = new TrackedSequence<int>(1, 2, 3, 4);
        var visited = new List<int>();

        Assert.Equal(Maybe<int>.Some(4), source.LastOrNone());
        Assert.Equal((5, 4, 1), (source.MoveNextCalls, source.CurrentReads, source.Disposals));
        Assert.Equal(Maybe<int>.Some(3), source.LastOrNone(value => { visited.Add(value); return value % 2 == 1; }));
        Assert.Equal([1, 2, 3, 4], visited);
        Assert.Equal((2, 10, 8, 2), (source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals));
    }

    [Fact]
    public void Stateful_predicate_observes_different_paths_on_list_and_sequence()
    {
        static Func<int, bool> FirstSeenOnly()
        {
            var seen = false;
            return _ => !seen && (seen = true);
        }

        Assert.Equal(Maybe<int>.Some(3), new TrackedList<int>(1, 2, 3).LastOrNone(FirstSeenOnly()));
        Assert.Equal(Maybe<int>.Some(1), new TrackedSequence<int>(1, 2, 3).LastOrNone(FirstSeenOnly()));
    }

    [Fact]
    public void Empty_sequence_is_none_without_predicate_calls()
    {
        var source = new TrackedSequence<int>();
        var calls = 0;

        Assert.False(source.LastOrNone().IsSome);
        Assert.False(source.LastOrNone(_ => ++calls > 0).IsSome);
        Assert.Equal((0, 2), (calls, source.Disposals));
    }

    [Fact]
    public void Found_null_default_and_nested_payloads_are_some()
    {
        Assert.Equal(Maybe<string?>.Some(null), new TrackedList<string?>("a", null).LastOrNone());
        Assert.Equal(Maybe<int?>.Some(null), new TrackedSequence<int?>(null, 1).LastOrNone(static value => !value.HasValue));
        Assert.Equal(Maybe<int>.Some(0), new TrackedSequence<int>(1, 0).LastOrNone());
        Assert.Equal(Maybe<string>.Some(""), new TrackedList<string>("").LastOrNone(static _ => true));
        Assert.Equal(Maybe<Maybe<int>>.Some(Maybe<int>.None), new TrackedList<Maybe<int>>(Maybe<int>.None).LastOrNone());
    }

    [Fact]
    public void Found_reference_payload_keeps_identity_without_payload_members()
    {
        var probe = new ReferenceProbe(1);

        Assert.True(new TrackedList<ReferenceProbe>(new ReferenceProbe(1), probe).LastOrNone().TryGetValue(out var found) && ReferenceEquals(probe, found));
        Assert.Equal((0, 0, 0), (probe.EqualsCalls, probe.HashCalls, probe.ToStringCalls));
    }

    [Fact]
    public void Indexer_predicate_source_and_dispose_exceptions_propagate()
    {
        var indexerFault = new ProbeException("indexer");
        var canceled = new OperationCanceledException();
        var disposeFault = new ProbeException("dispose");

        Assert.Same(indexerFault, Assert.Throws<ProbeException>(() => new TrackedList<int>(1) { IndexerException = indexerFault }.LastOrNone()));
        Assert.Same(canceled, Assert.Throws<OperationCanceledException>(() => new TrackedList<int>(1).LastOrNone(_ => throw canceled)));
        var faulty = new TrackedSequence<int>(1, 2) { PoisonFrom = 1 };
        Assert.Throws<ProbeException>(() => faulty.LastOrNone());
        Assert.Equal(1, faulty.Disposals);
        Assert.Same(disposeFault, Assert.Throws<ProbeException>(() => new TrackedSequence<int>(1) { DisposeException = disposeFault }.LastOrNone()));
        Assert.Same(disposeFault, Assert.Throws<ProbeException>(
            () => new TrackedSequence<int>(1) { DisposeException = disposeFault }.LastOrNone(_ => throw new ProbeException("predicate"))));
    }
}
