using Rezultat.Collections;

namespace Rezultat.Tests;

public sealed class MaybeElementAtOrNoneTests {
    [Fact]
    public void Null_source_is_rejected_even_for_negative_index() {
        Should.Throw<ArgumentNullException>(() => ((IEnumerable<int>)null!).ElementAtOrNone(-1)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("source");
        Should.Throw<ArgumentNullException>(() => ((IEnumerable<int>)null!).ElementAtOrNone(0)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("source");
    }

    [Fact]
    public void Negative_index_is_none_without_accessing_the_source() {
        var list = new TrackedList<int>(1);
        var source = new TrackedSequence<int>(1);

        list.ElementAtOrNone(-1).IsSome.ShouldBeFalse();
        source.ElementAtOrNone(int.MinValue).IsSome.ShouldBeFalse();
        (list.List.Counts, list.ReadOnly.Counts, list.List.Enumerations, source.Enumerations).ShouldBe((0, 0, 0, 0));
    }

    [Fact]
    public void Sequence_skips_with_MoveNext_and_reads_only_the_found_element() {
        var source = new TrackedSequence<int>(10, 11, 12, 13) { PoisonFrom = 3 };

        source.ElementAtOrNone(2).ShouldBe(Maybe<int>.Some(12));
        (source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((3, 1, 1));
    }

    [Fact]
    public void Position_past_the_end_of_sequence_is_none_without_reading_elements() {
        var source = new TrackedSequence<int>(10, 11);

        source.ElementAtOrNone(2).IsSome.ShouldBeFalse();
        source.ElementAtOrNone(int.MaxValue).IsSome.ShouldBeFalse();
        (source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((6, 0, 2));
    }

    [Fact]
    public void List_checks_count_before_reading_the_target_index() {
        var list = new TrackedList<int>(10, 11, 12);

        list.ElementAtOrNone(1).ShouldBe(Maybe<int>.Some(11));
        list.ElementAtOrNone(3).IsSome.ShouldBeFalse();
        list.ElementAtOrNone(int.MaxValue).IsSome.ShouldBeFalse();
        (list.List.Counts, list.List.Enumerations).ShouldBe((3, 0));
        list.List.Reads.ShouldBe([1]);
    }

    [Fact]
    public void IList_takes_priority_and_IReadOnlyList_is_used_without_it() {
        var both = new TrackedList<string>(["list"], ["read-only"]);
        var readOnlyInner = new TrackedList<string>(["list"], ["read-only"]);

        both.ElementAtOrNone(0).ShouldBe(Maybe<string>.Some("list"));
        new ReadOnlyListOnly<string>(readOnlyInner).ElementAtOrNone(0).ShouldBe(Maybe<string>.Some("read-only"));
        (both.ReadOnly.Counts, readOnlyInner.List.Counts).ShouldBe((0, 0));
        readOnlyInner.ReadOnly.Reads.ShouldBe([0]);
    }

    [Fact]
    public void Found_null_default_and_nested_payloads_are_some() {
        new TrackedSequence<string?>("a", null).ElementAtOrNone(1).ShouldBe(Maybe<string?>.Some(null));
        new TrackedList<int?>((int?)null).ElementAtOrNone(0).ShouldBe(Maybe<int?>.Some(null));
        new TrackedSequence<bool>(false).ElementAtOrNone(0).ShouldBe(Maybe<bool>.Some(false));
        new TrackedSequence<Maybe<int>>(Maybe<int>.None).ElementAtOrNone(0).ShouldBe(Maybe<Maybe<int>>.Some(Maybe<int>.None));
    }

    [Fact]
    public void Index_near_int_max_is_reached_on_a_long_sequence_without_overflow() {
        Longs().ElementAtOrNone(int.MaxValue).ShouldBe(Maybe<long>.Some(int.MaxValue));
    }

    [Fact]
    public void Source_indexer_and_dispose_exceptions_propagate() {
        // S3928: "index" names the parameter of the faulting list indexer, not of this test.
#pragma warning disable S3928
        var outOfRange = new ArgumentOutOfRangeException("index");
#pragma warning restore S3928
        var disposeFault = new ProbeException("dispose");
        var faulty = new TrackedSequence<int>(1, 2) { PoisonFrom = 1 };

        Should.Throw<ArgumentOutOfRangeException>(() => new TrackedList<int>(1) { IndexerException = outOfRange }.ElementAtOrNone(0)).ShouldBeOfType<ArgumentOutOfRangeException>().ShouldBeSameAs(outOfRange);
        Should.Throw<ProbeException>(() => faulty.ElementAtOrNone(1));
        faulty.Disposals.ShouldBe(1);
        Should.Throw<ProbeException>(() => new TrackedSequence<int>(1) { DisposeException = disposeFault }.ElementAtOrNone(0)).ShouldBeSameAs(disposeFault);
        Should.Throw<ProbeException>(() => new TrackedSequence<int>(1) { DisposeException = disposeFault }.ElementAtOrNone(5)).ShouldBeSameAs(disposeFault);
    }

    // S2190/S1994: a deliberately unbounded source proves that only the needed prefix is enumerated.
#pragma warning disable S2190, S1994
    private static IEnumerable<long> Longs() {
        for (var value = 0L; ; value++) {
            yield return value;
        }
    }
#pragma warning restore S2190, S1994
}
