using Rezultat.Collections;

namespace Rezultat.Tests;

public sealed class MaybeTraverseTests {
    [Fact]
    public void Null_source_is_rejected_before_selector() {
        var exception = Should.Throw<ArgumentNullException>(() => ((IEnumerable<int>)null!).Traverse<int, int>(null!)).ShouldBeOfType<ArgumentNullException>();

        exception.ParamName.ShouldBe("source");
    }

    [Fact]
    public void Null_selector_is_rejected_before_enumeration_even_for_empty_source() {
        var source = new TrackedSequence<int>();

        Should.Throw<ArgumentNullException>(() => source.Traverse<int, int>(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("selector");
        source.Enumerations.ShouldBe(0);
    }

    [Fact]
    public void Empty_source_gives_some_empty_array_without_selector_calls() {
        var source = new TrackedSequence<int>();
        var calls = 0;

        var result = source.Traverse(value => Maybe<int>.Some(++calls + value));

        result.TryGetValue(out var values).ShouldBeTrue();
        values.ShouldBeEmpty();
        (calls, source.Enumerations, source.Disposals).ShouldBe((0, 1, 1));
    }

    [Fact]
    public void All_somes_collect_payloads_in_source_order_in_one_pass() {
        var source = new TrackedSequence<int>(3, 1, 2);
        var visited = new List<int>();

        var result = source.Traverse(value => {
            visited.Add(value);
            return Maybe<string>.Some($"v{value}");
        });

        result.TryGetValue(out var values).ShouldBeTrue();
        values.ShouldBe(["v3", "v1", "v2"]);
        visited.ShouldBe([3, 1, 2]);
        (source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((1, 4, 3, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(4)]
    public void First_none_gives_none_and_tail_is_not_touched(int noneIndex) {
        var source = new TrackedSequence<int>(0, 1, 2, 3, 4) { PoisonFrom = noneIndex + 1 };
        var visited = new List<int>();

        var result = source.Traverse(index => {
            visited.Add(index);
            return index >= noneIndex ? default : Maybe<int>.Some(index);
        });

        result.IsSome.ShouldBeFalse();
        visited.ShouldBe(Enumerable.Range(0, noneIndex + 1));
        (source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((noneIndex + 1, noneIndex + 1, 1));
    }

    [Fact]
    public void Null_default_and_nested_payloads_are_kept_at_their_positions() {
        var references = new[] { "a", null, "" }.Traverse(static value => Maybe<string?>.Some(value));
        var nullables = new int?[] { null, 0 }.Traverse(static value => Maybe<int?>.Some(value));
        var nested = new[] { 1 }.Traverse(static _ => Maybe<Maybe<int>>.Some(Maybe<int>.None));
        var results = new[] { 1 }.Traverse(static _ => Maybe<Result<int, string>>.Some(default));
        var tasks = new[] { 1 }.Traverse(static _ => Maybe<Task<int>>.Some(Task.FromException<int>(new ProbeException("task"))));

        references.TryGetValue(out var referenceValues).ShouldBeTrue();
        referenceValues.ShouldBe(["a", null, ""]);
        nullables.TryGetValue(out var nullableValues).ShouldBeTrue();
        nullableValues.ShouldBe([null, 0]);
        (nested.TryGetValue(out var nestedValues) && !nestedValues.ShouldHaveSingleItem().IsSome).ShouldBeTrue();
        (results.TryGetValue(out var resultValues) && !resultValues.ShouldHaveSingleItem().IsInitialized).ShouldBeTrue();
        (tasks.TryGetValue(out var taskValues) && taskValues.ShouldHaveSingleItem().IsFaulted).ShouldBeTrue();
    }

    [Fact]
    public void Result_array_is_materialized_independently_of_the_source_and_keeps_identity() {
        var probe = new ReferenceProbe(1);
        var source = new List<ReferenceProbe> { probe };
        var calls = 0;

        var result = source.Traverse(value => { calls++; return Maybe<ReferenceProbe>.Some(value); });
        source[0] = new ReferenceProbe(2);
        source.Add(new ReferenceProbe(3));

        result.TryGetValue(out var values).ShouldBeTrue();
        values.ShouldHaveSingleItem().ShouldBeSameAs(probe);
        calls.ShouldBe(1);
        (probe.EqualsCalls, probe.HashCalls, probe.ToStringCalls).ShouldBe((0, 0, 0));
    }

    [Fact]
    public void Each_call_returns_a_new_array() {
        var source = new[] { 1, 2 };

        source.Traverse(Maybe<int>.Some).TryGetValue(out var first).ShouldBeTrue();
        source.Traverse(Maybe<int>.Some).TryGetValue(out var second).ShouldBeTrue();
        second.ShouldNotBeSameAs(first);
    }

    [Fact]
    public void Selector_and_source_exceptions_propagate_and_dispose() {
        var canceled = new OperationCanceledException();
        var source = new TrackedSequence<int>(1, 2) { PoisonFrom = 1 };
        var faulty = new TrackedSequence<int>(1, 2) { PoisonFrom = 1 };
        var calls = 0;

        Should.Throw<OperationCanceledException>(() => source.Traverse<int, int>(_ => throw canceled)).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(canceled);
        Should.Throw<ProbeException>(() => faulty.Traverse(value => Maybe<int>.Some(++calls + value)));
        (calls, source.Disposals, faulty.Disposals).ShouldBe((1, 1, 1));
    }

    [Theory]
    [InlineData("some")]
    [InlineData("none")]
    [InlineData("selector exception")]
    public void Dispose_exception_replaces_the_outcome_or_earlier_exception(string exit) {
        var disposeException = new ProbeException("dispose");
        var source = new TrackedSequence<int>(1) { DisposeException = disposeException };

        var observed = Should.Throw<ProbeException>(() => source.Traverse(value => exit switch {
            "some" => Maybe<int>.Some(value),
            "none" => Maybe<int>.None,
            _ => throw new ProbeException("selector"),
        }));

        observed.ShouldBeSameAs(disposeException);
    }

    [Fact]
    public void Ready_maybes_are_traversed_with_identity_selector() {
        Maybe<int>[] ready = [Maybe<int>.Some(1), Maybe<int>.Some(2)];
        Maybe<int>[] incomplete = [Maybe<int>.Some(1), default];

        ready.Traverse(static item => item).TryGetValue(out var values).ShouldBeTrue();
        values.ShouldBe([1, 2]);
        incomplete.Traverse(static item => item).IsSome.ShouldBeFalse();
    }
}
