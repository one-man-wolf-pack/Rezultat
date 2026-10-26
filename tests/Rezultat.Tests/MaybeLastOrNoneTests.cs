using Rezultat.Collections;

namespace Rezultat.Tests;

public sealed class MaybeLastOrNoneTests {
    [Fact]
    public void Null_source_is_rejected_before_predicate_and_null_predicate_before_access() {
        var list = new TrackedList<int>(1);

        Should.Throw<ArgumentNullException>(() => ((IEnumerable<int>)null!).LastOrNone()).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("source");
        Should.Throw<ArgumentNullException>(() => ((IEnumerable<int>)null!).LastOrNone(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("source");
        Should.Throw<ArgumentNullException>(() => list.LastOrNone(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("predicate");
        Should.Throw<ArgumentNullException>(() => new TrackedList<int>().LastOrNone(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("predicate");
        (list.List.Counts, list.ReadOnly.Counts, list.List.Enumerations).ShouldBe((0, 0, 0));
    }

    [Fact]
    public void List_without_predicate_reads_count_once_and_only_the_last_position() {
        var list = new TrackedList<int>(1, 2, 3);
        var empty = new TrackedList<int>();

        list.LastOrNone().ShouldBe(Maybe<int>.Some(3));
        empty.LastOrNone().IsSome.ShouldBeFalse();
        (list.List.Counts, list.List.Enumerations).ShouldBe((1, 0));
        list.List.Reads.ShouldBe([2]);
        (empty.List.Counts, empty.List.Reads.Count).ShouldBe((1, 0));
    }

    [Fact]
    public void List_with_predicate_checks_backwards_and_stops_at_first_match() {
        var list = new TrackedList<int>(1, 2, 3, 4, 5);
        var visited = new List<int>();

        var found = list.LastOrNone(value => { visited.Add(value); return value % 2 == 0; });

        found.ShouldBe(Maybe<int>.Some(4));
        visited.ShouldBe([5, 4]);
        list.List.Reads.ShouldBe([4, 3]);
        (list.List.Counts, list.List.Enumerations).ShouldBe((1, 0));
    }

    [Fact]
    public void List_with_predicate_and_no_match_reads_every_position_once() {
        var list = new TrackedList<int>(1, 2, 3);
        var empty = new TrackedList<int>();
        int calls = 0;

        list.LastOrNone(_ => { calls++; return false; }).IsSome.ShouldBeFalse();
        empty.LastOrNone(_ => { calls++; return true; }).IsSome.ShouldBeFalse();
        calls.ShouldBe(3);
        list.List.Reads.ShouldBe([2, 1, 0]);
        list.List.Counts.ShouldBe(1);
    }

    [Fact]
    public void IList_takes_priority_over_diverging_IReadOnlyList() {
        var list = new TrackedList<string>(["list-a", "list-b"], ["read-only"]);

        list.LastOrNone().ShouldBe(Maybe<string>.Some("list-b"));
        list.LastOrNone(static value => value.EndsWith('a')).ShouldBe(Maybe<string>.Some("list-a"));
        (list.ReadOnly.Counts, list.ReadOnly.Reads.Count).ShouldBe((0, 0));
    }

    [Fact]
    public void IReadOnlyList_without_IList_uses_its_count_and_indexer() {
        var inner = new TrackedList<int>([9, 9], [1, 2, 3]);
        var readOnly = new ReadOnlyListOnly<int>(inner);
        var visited = new List<int>();

        readOnly.LastOrNone().ShouldBe(Maybe<int>.Some(3));
        readOnly.LastOrNone(value => { visited.Add(value); return value == 1; }).ShouldBe(Maybe<int>.Some(1));
        visited.ShouldBe([3, 2, 1]);
        inner.ReadOnly.Counts.ShouldBe(2);
        inner.ReadOnly.Reads.ShouldBe([2, 2, 1, 0]);
        (inner.List.Counts, inner.List.Enumerations).ShouldBe((0, 0));
    }

    [Fact]
    public void Sequence_is_enumerated_forwards_to_the_end_reading_each_element_once() {
        var source = new TrackedSequence<int>(1, 2, 3, 4);
        var visited = new List<int>();

        source.LastOrNone().ShouldBe(Maybe<int>.Some(4));
        (source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((5, 4, 1));
        source.LastOrNone(value => { visited.Add(value); return value % 2 == 1; }).ShouldBe(Maybe<int>.Some(3));
        visited.ShouldBe([1, 2, 3, 4]);
        (source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((2, 10, 8, 2));
    }

    [Fact]
    public void Stateful_predicate_observes_different_paths_on_list_and_sequence() {
        static Func<int, bool> FirstSeenOnly() {
            bool seen = false;
            return _ => {
                if (seen) {
                    return false;
                }

                seen = true;
                return true;
            };
        }

        new TrackedList<int>(1, 2, 3).LastOrNone(FirstSeenOnly()).ShouldBe(Maybe<int>.Some(3));
        new TrackedSequence<int>(1, 2, 3).LastOrNone(FirstSeenOnly()).ShouldBe(Maybe<int>.Some(1));
    }

    [Fact]
    public void Empty_sequence_is_none_without_predicate_calls() {
        var source = new TrackedSequence<int>();
        int calls = 0;

        source.LastOrNone().IsSome.ShouldBeFalse();
        source.LastOrNone(_ => ++calls > 0).IsSome.ShouldBeFalse();
        (calls, source.Disposals).ShouldBe((0, 2));
    }

    [Fact]
    public void Found_null_default_and_nested_payloads_are_some() {
        new TrackedList<string?>("a", null).LastOrNone().ShouldBe(Maybe<string?>.Some(null));
        new TrackedSequence<int?>(null, 1).LastOrNone(static value => !value.HasValue).ShouldBe(Maybe<int?>.Some(null));
        new TrackedSequence<int>(1, 0).LastOrNone().ShouldBe(Maybe<int>.Some(0));
        new TrackedList<string>("").LastOrNone(static _ => true).ShouldBe(Maybe<string>.Some(""));
        new TrackedList<Maybe<int>>(Maybe<int>.None).LastOrNone().ShouldBe(Maybe<Maybe<int>>.Some(Maybe<int>.None));
    }

    [Fact]
    public void Found_reference_payload_keeps_identity_without_payload_members() {
        var probe = new ReferenceProbe(1);

        (new TrackedList<ReferenceProbe>(new ReferenceProbe(1), probe).LastOrNone().TryGetValue(out var found) && ReferenceEquals(probe, found)).ShouldBeTrue();
        (probe.EqualsCalls, probe.HashCalls, probe.ToStringCalls).ShouldBe((0, 0, 0));
    }

    [Fact]
    public void Indexer_predicate_source_and_dispose_exceptions_propagate() {
        var indexerFault = new ProbeException("indexer");
        var canceled = new OperationCanceledException();
        var disposeFault = new ProbeException("dispose");

        Should.Throw<ProbeException>(() => new TrackedList<int>(1) { IndexerException = indexerFault }.LastOrNone()).ShouldBeSameAs(indexerFault);
        Should.Throw<OperationCanceledException>(() => new TrackedList<int>(1).LastOrNone(_ => throw canceled)).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(canceled);
        var faulty = new TrackedSequence<int>(1, 2) { PoisonFrom = 1 };
        Should.Throw<ProbeException>(() => faulty.LastOrNone());
        faulty.Disposals.ShouldBe(1);
        Should.Throw<ProbeException>(() => new TrackedSequence<int>(1) { DisposeException = disposeFault }.LastOrNone()).ShouldBeSameAs(disposeFault);
        Should.Throw<ProbeException>(() => new TrackedSequence<int>(1) { DisposeException = disposeFault }.LastOrNone(_ => throw new ProbeException("predicate"))).ShouldBeSameAs(disposeFault);
    }
}
