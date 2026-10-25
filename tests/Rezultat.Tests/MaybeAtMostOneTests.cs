using Rezultat.Collections;

namespace Rezultat.Tests;

public sealed class MaybeAtMostOneTests {
    [Fact]
    public void Null_source_is_rejected_before_factory() {
        var exception = Should.Throw<ArgumentNullException>(() => MaybeCollectionExtensions.AtMostOne<int, string>(null!, null!)).ShouldBeOfType<ArgumentNullException>();

        exception.ParamName.ShouldBe("source");
    }

    [Fact]
    public void Null_factory_is_rejected_before_enumeration_even_for_empty_source() {
        var source = new TrackedSequence<int>();

        var exception = Should.Throw<ArgumentNullException>(() => source.AtMostOne<int, string>(null!)).ShouldBeOfType<ArgumentNullException>();

        exception.ParamName.ShouldBe("errorFactory");
        source.Enumerations.ShouldBe(0);
    }

    [Fact]
    public void Empty_source_gives_ok_none_without_factory_call() {
        var source = new TrackedSequence<int>();
        var calls = 0;

        var result = source.AtMostOne(() => { calls++; return "many"; });

        result.ShouldBe(Result<Maybe<int>, string>.Ok(Maybe<int>.None));
        (calls, source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((0, 1, 1, 0, 1));
    }

    [Fact]
    public void Single_element_gives_ok_some_of_the_exact_element_without_factory_call() {
        var payload = new ReferenceProbe(1);
        var source = new TrackedSequence<ReferenceProbe>(payload);
        var calls = 0;

        var result = source.AtMostOne(() => { calls++; return "many"; });

        result.TryGetValue(out var maybe, out _).ShouldBeTrue();
        maybe.TryGetValue(out var single).ShouldBeTrue();
        single.ShouldBeSameAs(payload);
        (calls, source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((0, 2, 1, 1));
        (payload.EqualsCalls + payload.HashCalls + payload.ToStringCalls).ShouldBe(0);
    }

    [Fact]
    public void Null_and_default_elements_are_some() {
        new string?[] { null }.AtMostOne(static () => 0).ShouldBe(Result<Maybe<string?>, int>.Ok(Maybe<string?>.Some(null)));
        new[] { 0 }.AtMostOne(static () => 1).ShouldBe(Result<Maybe<int>, int>.Ok(Maybe<int>.Some(0)));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void Second_element_gives_factory_error_without_reading_it_or_the_tail(int count) {
        var error = new ReferenceProbe(9);
        var source = new TrackedSequence<int>([.. Enumerable.Range(0, count)]) { PoisonFrom = 2 };
        var calls = 0;

        var result = source.AtMostOne(() => { calls++; return error; });

        result.TryGetValue(out _, out var observed).ShouldBeFalse();
        observed.ShouldBeSameAs(error);
        (calls, source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((1, 1, 2, 1, 1));
    }

    [Fact]
    public void Null_factory_error_is_kept() {
        var result = new[] { 1, 2 }.AtMostOne(static () => (string?)null);

        result.TryGetValue(out _, out var error).ShouldBeFalse();
        error.ShouldBeNull();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Factory_exception_and_cancellation_propagate_and_dispose(bool cancel) {
        Exception thrown = cancel ? new OperationCanceledException() : new ProbeException("factory");
        var source = new TrackedSequence<int>(1, 2);

        Record.Exception(() => source.AtMostOne<int, string>(() => throw thrown)).ShouldBeSameAs(thrown);
        source.Disposals.ShouldBe(1);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Source_exception_is_not_an_outcome_and_disposes(int poisonFrom) {
        var source = new TrackedSequence<int>(1, 2) { PoisonFrom = poisonFrom };
        var calls = 0;

        Should.Throw<ProbeException>(() => source.AtMostOne(() => { calls++; return "many"; }));
        (calls, source.Disposals).ShouldBe((0, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Dispose_exception_replaces_the_outcome_or_earlier_exception(int count) {
        var disposeException = new ProbeException("dispose");
        var source = new TrackedSequence<int>([.. Enumerable.Range(0, count)]) { DisposeException = disposeException };

        Should.Throw<ProbeException>(() => source.AtMostOne(static () => "many")).ShouldBeSameAs(disposeException);
    }

    [Fact]
    public void Caller_where_selects_matches_and_its_predicate_exception_is_not_an_outcome() {
        var source = new[] { 1, 2, 3, 4 };

        source.Where(static value => value == 3).AtMostOne(static () => "many").ShouldBe(Result<Maybe<int>, string>.Ok(Maybe<int>.Some(3)));
        source.Where(static value => value > 2).AtMostOne(static () => "many").ShouldBe(Result<Maybe<int>, string>.Error("many"));
        Should.Throw<ProbeException>(() => source.Where(static _ => throw new ProbeException("predicate")).AtMostOne(static () => "many"));
    }

    [Fact]
    public void Single_or_none_keeps_the_throwing_contract() {
        Should.Throw<InvalidOperationException>(() => new[] { 1, 2 }.SingleOrNone()).ShouldBeOfType<InvalidOperationException>();
    }
}
