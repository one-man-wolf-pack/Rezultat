using Rezultat.Collections;

namespace Rezultat.Tests;

public sealed class MaybeFirstAndSingleOrNoneTests {
    [Fact]
    public void Null_source_is_rejected_before_predicate() {
        IEnumerable<int> source = null!;

        Should.Throw<ArgumentNullException>(() => source.FirstOrNone()).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("source");
        Should.Throw<ArgumentNullException>(() => source.FirstOrNone(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("source");
        Should.Throw<ArgumentNullException>(() => source.SingleOrNone()).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("source");
        Should.Throw<ArgumentNullException>(() => source.SingleOrNone(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("source");
    }

    [Fact]
    public void Null_predicate_is_rejected_before_enumeration_even_for_empty_source() {
        var source = new TrackedSequence<int>();

        Should.Throw<ArgumentNullException>(() => source.FirstOrNone(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("predicate");
        Should.Throw<ArgumentNullException>(() => source.SingleOrNone(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("predicate");
        source.Enumerations.ShouldBe(0);
    }

    [Fact]
    public void Empty_source_is_none_without_predicate_calls() {
        var source = new TrackedSequence<int>();
        int calls = 0;

        source.FirstOrNone().IsSome.ShouldBeFalse();
        source.FirstOrNone(_ => ++calls > 0).IsSome.ShouldBeFalse();
        source.SingleOrNone().IsSome.ShouldBeFalse();
        source.SingleOrNone(_ => ++calls > 0).IsSome.ShouldBeFalse();
        calls.ShouldBe(0);
        (source.Enumerations, source.Disposals).ShouldBe((4, 4));
    }

    [Fact]
    public void FirstOrNone_reads_only_the_first_element() {
        var source = new TrackedSequence<int>(7, 8) { PoisonFrom = 1 };

        source.FirstOrNone().ShouldBe(Maybe<int>.Some(7));
        (source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((1, 1, 1));
    }

    [Fact]
    public void FirstOrNone_with_predicate_stops_at_first_match_and_does_not_read_the_tail() {
        var source = new TrackedSequence<int>(1, 2, 3, 4) { PoisonFrom = 3 };
        var visited = new List<int>();

        var found = source.FirstOrNone(value => { visited.Add(value); return value % 2 == 0 && value > 1; });

        found.ShouldBe(Maybe<int>.Some(2));
        visited.ShouldBe([1, 2]);
        (source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((2, 2, 1));
    }

    [Fact]
    public void FirstOrNone_with_predicate_and_no_match_visits_all_once() {
        var source = new TrackedSequence<int>(1, 3, 5);
        int calls = 0;

        source.FirstOrNone(_ => { calls++; return false; }).IsSome.ShouldBeFalse();
        (calls, source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((3, 1, 4, 3, 1));
    }

    [Fact]
    public void Found_null_default_and_nested_payloads_are_some() {
        new string?[] { null, "a" }.FirstOrNone().ShouldBe(Maybe<string?>.Some(null));
        new int?[] { 1, null }.FirstOrNone(static value => !value.HasValue).ShouldBe(Maybe<int?>.Some(null));
        new[] { 0 }.SingleOrNone().ShouldBe(Maybe<int>.Some(0));
        new[] { true, false }.SingleOrNone(static value => !value).ShouldBe(Maybe<bool>.Some(false));
        new[] { "" }.FirstOrNone().ShouldBe(Maybe<string>.Some(""));
        new[] { Maybe<int>.None, Maybe<int>.Some(1) }.FirstOrNone().ShouldBe(Maybe<Maybe<int>>.Some(Maybe<int>.None));
        (new[] { default(Result<int, string>) }.SingleOrNone().TryGetValue(out var inner) && !inner.IsInitialized).ShouldBeTrue();
    }

    [Fact]
    public void Found_reference_payload_keeps_identity_without_payload_members() {
        var first = new ReferenceProbe(1);
        var second = new ReferenceProbe(1);

        (new[] { first, second }.FirstOrNone().TryGetValue(out var found) && ReferenceEquals(first, found)).ShouldBeTrue();
        Should.Throw<InvalidOperationException>(() => new[] { first, second }.SingleOrNone()).ShouldBeOfType<InvalidOperationException>();
        (first.EqualsCalls, first.HashCalls, first.ToStringCalls).ShouldBe((0, 0, 0));
    }

    [Fact]
    public void SingleOrNone_without_predicate_does_not_read_the_second_element() {
        var one = new TrackedSequence<int>(5);
        var two = new TrackedSequence<int>(5, 6, 7) { PoisonFrom = 2 };

        one.SingleOrNone().ShouldBe(Maybe<int>.Some(5));
        (one.MoveNextCalls, one.CurrentReads, one.Disposals).ShouldBe((2, 1, 1));
        Should.Throw<InvalidOperationException>(() => two.SingleOrNone()).ShouldBeOfType<InvalidOperationException>();
        (two.MoveNextCalls, two.CurrentReads, two.Disposals).ShouldBe((2, 1, 1));
    }

    [Fact]
    public void SingleOrNone_with_predicate_stops_at_second_match_counting_equal_values() {
        var source = new TrackedSequence<int>(1, 2, 3, 2, 9) { PoisonFrom = 4 };
        var visited = new List<int>();

        Should.Throw<InvalidOperationException>(() => source.SingleOrNone(value => { visited.Add(value); return value == 2; })).ShouldBeOfType<InvalidOperationException>();
        visited.ShouldBe([1, 2, 3, 2]);
        source.Disposals.ShouldBe(1);
    }

    [Fact]
    public void SingleOrNone_with_predicate_confirms_uniqueness_after_the_source_ends() {
        var source = new TrackedSequence<int>(1, 2, 3);
        int calls = 0;

        source.SingleOrNone(value => { calls++; return value == 2; }).ShouldBe(Maybe<int>.Some(2));
        source.SingleOrNone(static _ => false).IsSome.ShouldBeFalse();
        calls.ShouldBe(3);
        source.Disposals.ShouldBe(2);
    }

    [Fact]
    public void Source_and_predicate_exceptions_propagate_and_dispose() {
        var canceled = new OperationCanceledException();
        var faulty = new TrackedSequence<int>(1, 2) { PoisonFrom = 0 };
        var source = new TrackedSequence<int>(1, 2);

        Should.Throw<ProbeException>(() => faulty.FirstOrNone());
        Should.Throw<ProbeException>(() => faulty.SingleOrNone());
        Should.Throw<OperationCanceledException>(() => source.FirstOrNone(_ => throw canceled)).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(canceled);
        Should.Throw<OperationCanceledException>(() => source.SingleOrNone(_ => throw canceled)).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(canceled);
        (faulty.Disposals, source.Disposals).ShouldBe((2, 2));
    }

    [Theory]
    [InlineData("first")]
    [InlineData("first none")]
    [InlineData("single")]
    [InlineData("single none")]
    [InlineData("multiple")]
    [InlineData("predicate exception")]
    public void Dispose_exception_replaces_the_outcome_or_earlier_exception(string exit) {
        var disposeException = new ProbeException("dispose");
        var source = new TrackedSequence<int>(exit == "multiple" ? [1, 2] : [1]) { DisposeException = disposeException };

        var observed = Should.Throw<ProbeException>(() => _ = exit switch {
            "first" => source.FirstOrNone(),
            "first none" => source.FirstOrNone(static _ => false),
            "single" => source.SingleOrNone(),
            "single none" => source.SingleOrNone(static _ => false),
            "multiple" => source.SingleOrNone(),
            _ => source.SingleOrNone(_ => throw new ProbeException("predicate")),
        });

        observed.ShouldBeSameAs(disposeException);
    }

    [Fact]
    public void Repeated_call_accesses_the_source_again() {
        var source = new TrackedSequence<int>(1);

        source.FirstOrNone();
        source.FirstOrNone();
        source.SingleOrNone();

        source.Enumerations.ShouldBe(3);
    }

    [Fact]
    public void Infinite_source_finds_first_match() {
        Naturals().FirstOrNone(static value => value >= 10).ShouldBe(Maybe<int>.Some(10));
        Should.Throw<InvalidOperationException>(() => Naturals().SingleOrNone()).ShouldBeOfType<InvalidOperationException>();
    }

    private static IEnumerable<int> Naturals() {
        for (int value = 0; ; value++) {
            yield return value;
        }
    }
}
