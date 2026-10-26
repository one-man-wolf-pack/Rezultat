using Rezultat.Collections;

namespace Rezultat.Tests;

public sealed class ResultSequenceTests {
    [Fact]
    public void Null_source_is_rejected() {
        var exception = Should.Throw<ArgumentNullException>(() => ResultCollectionExtensions.Sequence<int, string>(null!)).ShouldBeOfType<ArgumentNullException>();

        exception.ParamName.ShouldBe("source");
    }

    [Fact]
    public void Empty_source_gives_ok_empty_array() {
        var source = new TrackedSequence<Result<int, string>>();

        var result = source.Sequence();

        result.TryGetValue(out int[]? values, out _).ShouldBeTrue();
        values.ShouldBeEmpty();
        (source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((1, 1, 0, 1));
    }

    [Fact]
    public void All_ok_collects_values_in_source_order_in_one_pass() {
        var source = new TrackedSequence<Result<string?, string>>(
            Result<string?, string>.Ok("a"), Result<string?, string>.Ok(null), Result<string?, string>.Ok("c"));

        var result = source.Sequence();

        result.TryGetValue(out string?[]? values, out _).ShouldBeTrue();
        values.ShouldBe(["a", null, "c"]);
        (source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((1, 4, 3, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void First_error_is_returned_with_identity_and_tail_is_not_touched(int failureIndex) {
        var errors = Enumerable.Range(0, 3).Select(index => new ReferenceProbe(index)).ToArray();
        var items = Enumerable.Range(0, 3)
            .Select(index => index >= failureIndex ? Result<int, ReferenceProbe>.Error(errors[index]) : Result<int, ReferenceProbe>.Ok(index))
            .ToArray();
        var source = new TrackedSequence<Result<int, ReferenceProbe>>(items) { PoisonFrom = failureIndex + 1 };

        var result = source.Sequence();

        result.TryGetValue(out _, out var error).ShouldBeFalse();
        error.ShouldBeSameAs(errors[failureIndex]);
        (source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((failureIndex + 1, failureIndex + 1, 1));
        errors.Sum(static probe => probe.EqualsCalls + probe.HashCalls + probe.ToStringCalls).ShouldBe(0);
    }

    [Fact]
    public void Null_error_is_kept() {
        Result<int, string?>[] items = [Result<int, string?>.Ok(1), Result<int, string?>.Error(null)];

        var result = items.Sequence();

        result.TryGetValue(out _, out string? error).ShouldBeFalse();
        error.ShouldBeNull();
    }

    [Fact]
    public void Error_before_unreachable_default_wins() {
        var source = new TrackedSequence<Result<int, string>>(Result<int, string>.Error("e"), default);

        source.Sequence().ShouldBe(Result<int[], string>.Error("e"));
        (source.CurrentReads, source.Disposals).ShouldBe((1, 1));
    }

    [Fact]
    public void Visited_default_throws_before_later_error_and_disposes() {
        var source = new TrackedSequence<Result<int, string>>(Result<int, string>.Ok(1), default, Result<int, string>.Error("e")) {
            PoisonFrom = 2,
        };

        Should.Throw<InvalidOperationException>(() => source.Sequence()).ShouldBeOfType<InvalidOperationException>();
        (source.CurrentReads, source.Disposals).ShouldBe((2, 1));
    }

    [Fact]
    public void Array_is_new_and_independent_of_later_source_changes() {
        var payload = new ReferenceProbe(1);
        var source = new List<Result<ReferenceProbe, string>> { Result<ReferenceProbe, string>.Ok(payload) };

        var first = source.Sequence();
        var second = source.Sequence();
        source[0] = Result<ReferenceProbe, string>.Ok(new ReferenceProbe(2));

        first.TryGetValue(out var firstValues, out _).ShouldBeTrue();
        second.TryGetValue(out var secondValues, out _).ShouldBeTrue();
        secondValues.ShouldNotBeSameAs(firstValues);
        firstValues.ShouldHaveSingleItem().ShouldBeSameAs(payload);
    }

    [Theory]
    [InlineData("ok")]
    [InlineData("error")]
    [InlineData("default")]
    public void Dispose_exception_replaces_the_outcome_or_earlier_exception(string exit) {
        var disposeException = new ProbeException("dispose");
        var item = exit switch {
            "ok" => Result<int, string>.Ok(1),
            "error" => Result<int, string>.Error("e"),
            _ => default,
        };
        var source = new TrackedSequence<Result<int, string>>(item) { DisposeException = disposeException };

        Should.Throw<ProbeException>(() => source.Sequence()).ShouldBeSameAs(disposeException);
    }

    [Fact]
    public void Source_exception_propagates_and_disposes() {
        var source = new TrackedSequence<Result<int, string>>(Result<int, string>.Ok(1), Result<int, string>.Ok(2)) { PoisonFrom = 1 };

        Should.Throw<ProbeException>(() => source.Sequence());
        source.Disposals.ShouldBe(1);
    }

    [Fact]
    public void Nested_payloads_are_not_checked() {
        Result<Result<int, string>, string>[] items = [Result<Result<int, string>, string>.Ok(default)];

        items.Sequence().TryGetValue(out var values, out _).ShouldBeTrue();
        values.ShouldHaveSingleItem().IsInitialized.ShouldBeFalse();
    }
}
