using Rezultat.Collections;

namespace Rezultat.Tests;

public sealed class MaybeChooseTests {
    [Fact]
    public void Arguments_are_checked_on_the_call_without_enumeration() {
        var source = new TrackedSequence<int>();

        Should.Throw<ArgumentNullException>(() => ((IEnumerable<int>)null!).Choose<int, int>(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("source");
        Should.Throw<ArgumentNullException>(() => source.Choose<int, int>(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("selector");
        source.Enumerations.ShouldBe(0);
    }

    [Fact]
    public void Call_neither_enumerates_source_nor_invokes_selector() {
        var source = new TrackedSequence<int>(1, 2);
        var calls = 0;

        _ = source.Choose(value => { calls++; return Maybe<int>.Some(value); });

        (source.Enumerations, calls).ShouldBe((0, 0));
    }

    [Fact]
    public void Somes_are_yielded_in_order_and_nones_skipped() {
        var source = new TrackedSequence<int>(1, 2, 3, 4, 5);
        var visited = new List<int>();

        var chosen = source.Choose(value => {
            visited.Add(value);
            return value % 2 == 1 ? Maybe<string>.Some($"v{value}") : default;
        }).ToArray();

        chosen.ShouldBe(["v1", "v3", "v5"]);
        visited.ShouldBe([1, 2, 3, 4, 5]);
        (source.Enumerations, source.Disposals).ShouldBe((1, 1));
    }

    [Fact]
    public void Present_null_default_and_nested_payloads_are_yielded() {
        new[] { "a", null, "" }.Choose(static value => Maybe<string?>.Some(value)).ShouldBe(["a", null, ""]);
        new int?[] { null, 0 }.Choose(static value => Maybe<int?>.Some(value)).ShouldBe([null, 0]);
        new[] { false }.Choose(static value => Maybe<bool>.Some(value)).ShouldBe([false]);
        new[] { 1 }.Choose(static _ => Maybe<Maybe<int>>.Some(Maybe<int>.None)).ShouldHaveSingleItem().IsSome.ShouldBeFalse();
        new[] { 1 }.Choose(static _ => Maybe<Result<int, string>>.Some(default)).ShouldHaveSingleItem().IsInitialized.ShouldBeFalse();
    }

    [Fact]
    public void Empty_and_all_none_sources_give_empty_sequences() {
        Array.Empty<int>().Choose(Maybe<int>.Some).ShouldBeEmpty();
        new[] { 1, 2 }.Choose(static _ => Maybe<int>.None).ShouldBeEmpty();
    }

    [Fact]
    public void Elements_are_read_only_on_demand_and_one_request_may_skip_several_nones() {
        var source = new TrackedSequence<int>(1, 2, 3, 4, 5);
        var visited = new List<int>();

        using var enumerator = source.Choose(value => {
            visited.Add(value);
            return value is 1 or 4 ? Maybe<int>.Some(value) : Maybe<int>.None;
        }).GetEnumerator();

        visited.ShouldBeEmpty();
        enumerator.MoveNext().ShouldBeTrue();
        enumerator.Current.ShouldBe(1);
        visited.ShouldBe([1]);
        enumerator.MoveNext().ShouldBeTrue();
        enumerator.Current.ShouldBe(4);
        visited.ShouldBe([1, 2, 3, 4]);
        source.CurrentReads.ShouldBe(4);
    }

    [Fact]
    public void Early_dispose_disposes_the_source_enumerator() {
        var source = new TrackedSequence<int>(1, 2, 3) { PoisonFrom = 1 };

        source.Choose(Maybe<int>.Some).First().ShouldBe(1);
        (source.MoveNextCalls, source.Disposals).ShouldBe((1, 1));
    }

    [Fact]
    public void Each_enumeration_accesses_source_and_selector_again_without_caching() {
        var source = new TrackedSequence<int>(1, 2);
        var calls = 0;
        var chosen = source.Choose(value => { calls++; return Maybe<int>.Some(value + calls); });

        chosen.ToArray().ShouldBe([2, 4]);
        chosen.ToArray().ShouldBe([4, 6]);
        (source.Enumerations, calls).ShouldBe((2, 4));
    }

    [Fact]
    public void Infinite_source_yields_on_demand() {
        Naturals().Choose(static value => value % 2 == 0 ? Maybe<int>.Some(value) : Maybe<int>.None).Take(3).ShouldBe([0, 2, 4]);
    }

    [Fact]
    public void Source_and_selector_exceptions_propagate_after_earlier_yields_and_dispose() {
        var canceled = new OperationCanceledException();
        var source = new TrackedSequence<int>(1, 2);
        var faulty = new TrackedSequence<int>(1, 2) { PoisonFrom = 1 };
        var yielded = new List<int>();

        Should.Throw<OperationCanceledException>(() => {
            foreach (var value in source.Choose(value => value == 2 ? throw canceled : Maybe<int>.Some(value))) {
                yielded.Add(value);
            }
        }).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(canceled);
        Should.Throw<ProbeException>(() => {
            foreach (var value in faulty.Choose(Maybe<int>.Some)) {
                yielded.Add(value);
            }
        });

        yielded.ShouldBe([1, 1]);
        (source.Disposals, faulty.Disposals).ShouldBe((1, 1));
    }

    [Theory]
    [InlineData("complete")]
    [InlineData("early stop")]
    [InlineData("selector exception")]
    public void Dispose_exception_replaces_the_outcome_or_earlier_exception(string exit) {
        var disposeException = new ProbeException("dispose");
        var source = new TrackedSequence<int>(1, 2) { DisposeException = disposeException };
        var chosen = source.Choose(value => exit == "selector exception" ? throw new ProbeException("selector") : Maybe<int>.Some(value));

        var observed = Should.Throw<ProbeException>(() => _ = exit == "early stop" ? chosen.First() : chosen.Count());

        observed.ShouldBeSameAs(disposeException);
    }

    [Fact]
    public void Ready_maybes_are_chosen_with_identity_selector() {
        Maybe<int>[] ready = [Maybe<int>.Some(1), default, Maybe<int>.Some(3)];

        ready.Choose(static item => item).ShouldBe([1, 3]);
    }

    // S2190/S1994: a deliberately unbounded source proves that only the needed prefix is enumerated.
#pragma warning disable S2190, S1994
    private static IEnumerable<int> Naturals() {
        for (var value = 0; ; value++) {
            yield return value;
        }
    }
#pragma warning restore S2190, S1994
}
