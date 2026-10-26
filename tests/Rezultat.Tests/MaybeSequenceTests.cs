using Rezultat.Collections;

namespace Rezultat.Tests;

public sealed class MaybeSequenceTests {
    [Fact]
    public void Null_source_is_rejected() {
        var exception = Should.Throw<ArgumentNullException>(() => MaybeCollectionExtensions.Sequence<int>(null!)).ShouldBeOfType<ArgumentNullException>();

        exception.ParamName.ShouldBe("source");
    }

    [Fact]
    public void Empty_source_gives_some_empty_array() {
        var source = new TrackedSequence<Maybe<int>>();

        source.Sequence().TryGetValue(out int[]? values).ShouldBeTrue();
        values.ShouldBeEmpty();
        (source.Enumerations, source.MoveNextCalls, source.Disposals).ShouldBe((1, 1, 1));
    }

    [Fact]
    public void All_some_collects_payloads_including_null_in_one_pass() {
        var source = new TrackedSequence<Maybe<string?>>(Maybe<string?>.Some("a"), Maybe<string?>.Some(null));

        source.Sequence().TryGetValue(out string?[]? values).ShouldBeTrue();
        values.ShouldBe(["a", null]);
        (source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((1, 3, 2, 1));
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    public void First_none_or_default_gives_none_and_tail_is_not_touched(int noneIndex, bool useDefault) {
        var items = Enumerable.Range(0, 3)
            .Select(index => index == noneIndex ? Absent(useDefault) : Maybe<int>.Some(index))
            .ToArray();
        var source = new TrackedSequence<Maybe<int>>(items) { PoisonFrom = noneIndex + 1 };

        source.Sequence().ShouldBe(Maybe<int[]>.None);
        (source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((noneIndex + 1, noneIndex + 1, 1));
    }

    [Fact]
    public void Array_is_new_and_independent_of_later_source_changes() {
        var payload = new ReferenceProbe(1);
        var source = new List<Maybe<ReferenceProbe>> { Maybe<ReferenceProbe>.Some(payload) };

        var first = source.Sequence();
        var second = source.Sequence();
        source[0] = Maybe<ReferenceProbe>.None;

        first.TryGetValue(out var firstValues).ShouldBeTrue();
        second.TryGetValue(out var secondValues).ShouldBeTrue();
        secondValues.ShouldNotBeSameAs(firstValues);
        firstValues.ShouldHaveSingleItem().ShouldBeSameAs(payload);
    }

    [Theory]
    [InlineData("some")]
    [InlineData("none")]
    [InlineData("source exception")]
    public void Dispose_exception_replaces_the_outcome_or_earlier_exception(string exit) {
        var disposeException = new ProbeException("dispose");
        var source = new TrackedSequence<Maybe<int>>(exit == "none" ? Maybe<int>.None : Maybe<int>.Some(1)) {
            DisposeException = disposeException,
            PoisonFrom = exit == "source exception" ? 0 : int.MaxValue,
        };

        Should.Throw<ProbeException>(() => source.Sequence()).ShouldBeSameAs(disposeException);
    }

    [Fact]
    public void Nested_none_payload_is_not_checked() {
        Maybe<Maybe<int>>[] items = [Maybe<Maybe<int>>.Some(Maybe<int>.None)];

        items.Sequence().TryGetValue(out var values).ShouldBeTrue();
        values.ShouldHaveSingleItem().ShouldBe(Maybe<int>.None);
    }

    private static Maybe<int> Absent(bool useDefault) => useDefault ? default : Maybe<int>.None;
}
