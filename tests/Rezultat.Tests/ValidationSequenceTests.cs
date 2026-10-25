using Rezultat.Collections;

namespace Rezultat.Tests;

public sealed class ValidationSequenceTests {
    [Fact]
    public void Null_source_is_rejected() {
        var exception = Should.Throw<ArgumentNullException>(() => ValidationCollectionExtensions.Sequence<int, string>(null!)).ShouldBeOfType<ArgumentNullException>();

        exception.ParamName.ShouldBe("source");
    }

    [Fact]
    public void Empty_source_gives_valid_empty_array() {
        var source = new TrackedSequence<Validation<int, string>>();

        source.Sequence().TryGetValue(out var values, out _).ShouldBeTrue();
        values.ShouldBeEmpty();
        (source.Enumerations, source.Disposals).ShouldBe((1, 1));
    }

    [Fact]
    public void All_valid_collects_values_including_null_in_one_pass() {
        var source = new TrackedSequence<Validation<string?, int>>(Validation<string?, int>.Valid("a"), Validation<string?, int>.Valid(null));

        source.Sequence().TryGetValue(out var values, out _).ShouldBeTrue();
        values.ShouldBe(["a", null]);
        (source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((1, 3, 2, 1));
    }

    [Fact]
    public void All_invalid_issues_are_accumulated_in_order_with_null_and_duplicates() {
        var source = new TrackedSequence<Validation<int, string?>>(
            Validation<int, string?>.Valid(0),
            Validation<int, string?>.InvalidMany(["1a", null]),
            Validation<int, string?>.Valid(2),
            Validation<int, string?>.InvalidMany(["3a", "3a"]));

        source.Sequence().ShouldBe(Validation<int[], string?>.InvalidMany(["1a", null, "3a", "3a"]));
        (source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((1, 5, 4, 1));
    }

    [Fact]
    public void Single_invalid_keeps_its_issue_buffer() {
        var invalid = Validation<int, string>.InvalidMany(["a", "b"]);
        Validation<int, string>[] items = [Validation<int, string>.Valid(1), invalid];
        invalid.TryGetValue(out _, out var expected).ShouldBeFalse();

        items.Sequence().TryGetValue(out _, out var issues).ShouldBeFalse();
        (issues == expected).ShouldBeTrue();
    }

    [Fact]
    public void Visited_default_throws_after_accumulated_issues_and_stops() {
        var source = new TrackedSequence<Validation<int, string>>(
            Validation<int, string>.Invalid("first"), default, Validation<int, string>.Invalid("later")) { PoisonFrom = 2 };

        Should.Throw<InvalidOperationException>(() => source.Sequence()).ShouldBeOfType<InvalidOperationException>();
        (source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((2, 2, 1));
    }

    [Fact]
    public void Source_exception_after_invalid_discards_issues_and_disposes() {
        var source = new TrackedSequence<Validation<int, string>>(Validation<int, string>.Invalid("e"), Validation<int, string>.Valid(1)) {
            PoisonFrom = 1,
        };

        Should.Throw<ProbeException>(() => source.Sequence());
        source.Disposals.ShouldBe(1);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("invalid")]
    [InlineData("default")]
    public void Dispose_exception_replaces_the_outcome_or_earlier_exception(string exit) {
        var disposeException = new ProbeException("dispose");
        Validation<int, string> item = exit switch {
            "valid" => Validation<int, string>.Valid(1),
            "invalid" => Validation<int, string>.Invalid("e"),
            _ => default,
        };
        var source = new TrackedSequence<Validation<int, string>>(item) { DisposeException = disposeException };

        Should.Throw<ProbeException>(() => source.Sequence()).ShouldBeSameAs(disposeException);
    }

    [Fact]
    public void Array_is_new_and_independent_of_later_source_changes() {
        var source = new List<Validation<int, string>> { Validation<int, string>.Valid(1) };

        var first = source.Sequence();
        var second = source.Sequence();
        source[0] = Validation<int, string>.Valid(10);

        first.TryGetValue(out var firstValues, out _).ShouldBeTrue();
        second.TryGetValue(out var secondValues, out _).ShouldBeTrue();
        secondValues.ShouldNotBeSameAs(firstValues);
        firstValues.ShouldBe([1]);
    }
}
