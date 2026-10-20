namespace Rezultat.Tests;

public sealed class MaybeElementAtOrNoneTests
{
    [Fact]
    public void Null_source_is_rejected_even_for_negative_index()
    {
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ((IEnumerable<int>)null!).ElementAtOrNone(-1)).ParamName);
        Assert.Equal("source", Assert.Throws<ArgumentNullException>(() => ((IEnumerable<int>)null!).ElementAtOrNone(0)).ParamName);
    }

    [Fact]
    public void Negative_index_is_none_without_accessing_the_source()
    {
        var list = new TrackedList<int>(1);
        var source = new TrackedSequence<int>(1);

        Assert.False(list.ElementAtOrNone(-1).IsSome);
        Assert.False(source.ElementAtOrNone(int.MinValue).IsSome);
        Assert.Equal((0, 0, 0, 0), (list.List.Counts, list.ReadOnly.Counts, list.List.Enumerations, source.Enumerations));
    }

    [Fact]
    public void Sequence_skips_with_MoveNext_and_reads_only_the_found_element()
    {
        var source = new TrackedSequence<int>(10, 11, 12, 13) { PoisonFrom = 3 };

        Assert.Equal(Maybe<int>.Some(12), source.ElementAtOrNone(2));
        Assert.Equal((3, 1, 1), (source.MoveNextCalls, source.CurrentReads, source.Disposals));
    }

    [Fact]
    public void Position_past_the_end_of_sequence_is_none_without_reading_elements()
    {
        var source = new TrackedSequence<int>(10, 11);

        Assert.False(source.ElementAtOrNone(2).IsSome);
        Assert.False(source.ElementAtOrNone(int.MaxValue).IsSome);
        Assert.Equal((6, 0, 2), (source.MoveNextCalls, source.CurrentReads, source.Disposals));
    }

    [Fact]
    public void List_checks_count_before_reading_the_target_index()
    {
        var list = new TrackedList<int>(10, 11, 12);

        Assert.Equal(Maybe<int>.Some(11), list.ElementAtOrNone(1));
        Assert.False(list.ElementAtOrNone(3).IsSome);
        Assert.False(list.ElementAtOrNone(int.MaxValue).IsSome);
        Assert.Equal((3, 0), (list.List.Counts, list.List.Enumerations));
        Assert.Equal([1], list.List.Reads);
    }

    [Fact]
    public void IList_takes_priority_and_IReadOnlyList_is_used_without_it()
    {
        var both = new TrackedList<string>(["list"], ["read-only"]);
        var readOnlyInner = new TrackedList<string>(["list"], ["read-only"]);

        Assert.Equal(Maybe<string>.Some("list"), both.ElementAtOrNone(0));
        Assert.Equal(Maybe<string>.Some("read-only"), new ReadOnlyListOnly<string>(readOnlyInner).ElementAtOrNone(0));
        Assert.Equal((0, 0), (both.ReadOnly.Counts, readOnlyInner.List.Counts));
        Assert.Equal([0], readOnlyInner.ReadOnly.Reads);
    }

    [Fact]
    public void Found_null_default_and_nested_payloads_are_some()
    {
        Assert.Equal(Maybe<string?>.Some(null), new TrackedSequence<string?>("a", null).ElementAtOrNone(1));
        Assert.Equal(Maybe<int?>.Some(null), new TrackedList<int?>([null]).ElementAtOrNone(0));
        Assert.Equal(Maybe<bool>.Some(false), new TrackedSequence<bool>(false).ElementAtOrNone(0));
        Assert.Equal(Maybe<Maybe<int>>.Some(Maybe<int>.None), new TrackedSequence<Maybe<int>>(Maybe<int>.None).ElementAtOrNone(0));
    }

    [Fact]
    public void Index_near_int_max_is_reached_on_a_long_sequence_without_overflow()
    {
        Assert.Equal(Maybe<long>.Some(int.MaxValue), Longs().ElementAtOrNone(int.MaxValue));
    }

    [Fact]
    public void Source_indexer_and_dispose_exceptions_propagate()
    {
        var outOfRange = new ArgumentOutOfRangeException("index");
        var disposeFault = new ProbeException("dispose");
        var faulty = new TrackedSequence<int>(1, 2) { PoisonFrom = 1 };

        Assert.Same(outOfRange, Assert.Throws<ArgumentOutOfRangeException>(() => new TrackedList<int>(1) { IndexerException = outOfRange }.ElementAtOrNone(0)));
        Assert.Throws<ProbeException>(() => faulty.ElementAtOrNone(1));
        Assert.Equal(1, faulty.Disposals);
        Assert.Same(disposeFault, Assert.Throws<ProbeException>(() => new TrackedSequence<int>(1) { DisposeException = disposeFault }.ElementAtOrNone(0)));
        Assert.Same(disposeFault, Assert.Throws<ProbeException>(() => new TrackedSequence<int>(1) { DisposeException = disposeFault }.ElementAtOrNone(5)));
    }

    private static IEnumerable<long> Longs()
    {
        for (var value = 0L; ; value++)
        {
            yield return value;
        }
    }
}
