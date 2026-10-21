using Rezultat.Collections;

namespace Rezultat.Tests;

public sealed class MaybeAtMostOneTests
{
    [Fact]
    public void Null_source_is_rejected_before_factory()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => MaybeCollectionExtensions.AtMostOne<int, string>(null!, null!));

        Assert.Equal("source", exception.ParamName);
    }

    [Fact]
    public void Null_factory_is_rejected_before_enumeration_even_for_empty_source()
    {
        var source = new TrackedSequence<int>();

        var exception = Assert.Throws<ArgumentNullException>(() => source.AtMostOne<int, string>(null!));

        Assert.Equal("errorFactory", exception.ParamName);
        Assert.Equal(0, source.Enumerations);
    }

    [Fact]
    public void Empty_source_gives_ok_none_without_factory_call()
    {
        var source = new TrackedSequence<int>();
        var calls = 0;

        var result = source.AtMostOne(() => { calls++; return "many"; });

        Assert.Equal(Result<Maybe<int>, string>.Ok(Maybe<int>.None), result);
        Assert.Equal((0, 1, 1, 0, 1), (calls, source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals));
    }

    [Fact]
    public void Single_element_gives_ok_some_of_the_exact_element_without_factory_call()
    {
        var payload = new ReferenceProbe(1);
        var source = new TrackedSequence<ReferenceProbe>(payload);
        var calls = 0;

        var result = source.AtMostOne(() => { calls++; return "many"; });

        Assert.True(result.TryGetValue(out var maybe, out _));
        Assert.True(maybe.TryGetValue(out var single));
        Assert.Same(payload, single);
        Assert.Equal((0, 2, 1, 1), (calls, source.MoveNextCalls, source.CurrentReads, source.Disposals));
        Assert.Equal(0, payload.EqualsCalls + payload.HashCalls + payload.ToStringCalls);
    }

    [Fact]
    public void Null_and_default_elements_are_some()
    {
        Assert.Equal(Result<Maybe<string?>, int>.Ok(Maybe<string?>.Some(null)), new string?[] { null }.AtMostOne(static () => 0));
        Assert.Equal(Result<Maybe<int>, int>.Ok(Maybe<int>.Some(0)), new[] { 0 }.AtMostOne(static () => 1));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(5)]
    public void Second_element_gives_factory_error_without_reading_it_or_the_tail(int count)
    {
        var error = new ReferenceProbe(9);
        var source = new TrackedSequence<int>([.. Enumerable.Range(0, count)]) { PoisonFrom = 2 };
        var calls = 0;

        var result = source.AtMostOne(() => { calls++; return error; });

        Assert.False(result.TryGetValue(out _, out var observed));
        Assert.Same(error, observed);
        Assert.Equal((1, 1, 2, 1, 1), (calls, source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals));
    }

    [Fact]
    public void Null_factory_error_is_kept()
    {
        var result = new[] { 1, 2 }.AtMostOne(static () => (string?)null);

        Assert.False(result.TryGetValue(out _, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Factory_exception_and_cancellation_propagate_and_dispose(bool cancel)
    {
        Exception thrown = cancel ? new OperationCanceledException() : new ProbeException("factory");
        var source = new TrackedSequence<int>(1, 2);

        Assert.Same(thrown, Record.Exception(() => source.AtMostOne<int, string>(() => throw thrown)));
        Assert.Equal(1, source.Disposals);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void Source_exception_is_not_an_outcome_and_disposes(int poisonFrom)
    {
        var source = new TrackedSequence<int>(1, 2) { PoisonFrom = poisonFrom };
        var calls = 0;

        Assert.Throws<ProbeException>(() => source.AtMostOne(() => { calls++; return "many"; }));
        Assert.Equal((0, 1), (calls, source.Disposals));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Dispose_exception_replaces_the_outcome_or_earlier_exception(int count)
    {
        var disposeException = new ProbeException("dispose");
        var source = new TrackedSequence<int>([.. Enumerable.Range(0, count)]) { DisposeException = disposeException };

        Assert.Same(disposeException, Assert.Throws<ProbeException>(() => source.AtMostOne(static () => "many")));
    }

    [Fact]
    public void Caller_where_selects_matches_and_its_predicate_exception_is_not_an_outcome()
    {
        var source = new[] { 1, 2, 3, 4 };

        Assert.Equal(Result<Maybe<int>, string>.Ok(Maybe<int>.Some(3)), source.Where(static value => value == 3).AtMostOne(static () => "many"));
        Assert.Equal(Result<Maybe<int>, string>.Error("many"), source.Where(static value => value > 2).AtMostOne(static () => "many"));
        Assert.Throws<ProbeException>(() => source.Where(static _ => throw new ProbeException("predicate")).AtMostOne(static () => "many"));
    }

    [Fact]
    public void Single_or_none_keeps_the_throwing_contract()
    {
        Assert.Throws<InvalidOperationException>(() => new[] { 1, 2 }.SingleOrNone());
    }
}
