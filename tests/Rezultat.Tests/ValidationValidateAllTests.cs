using Rezultat.Collections;

namespace Rezultat.Tests;

public sealed class ValidationValidateAllTests {
    [Fact]
    public void Ready_form_rejects_null_source() {
        var exception = Should.Throw<ArgumentNullException>(() => ValidationCollectionExtensions.ValidateAll<int, string>(null!)).ShouldBeOfType<ArgumentNullException>();

        exception.ParamName.ShouldBe("source");
    }

    [Fact]
    public void Selector_form_rejects_null_source_before_selector() {
        var exception = Should.Throw<ArgumentNullException>(() => ValidationCollectionExtensions.ValidateAll<int, int, string>(null!, null!)).ShouldBeOfType<ArgumentNullException>();

        exception.ParamName.ShouldBe("source");
    }

    [Fact]
    public void Selector_form_rejects_null_selector_before_enumeration_even_for_empty_source() {
        var source = new TrackedSequence<int>();

        var exception = Should.Throw<ArgumentNullException>(() => ValidationCollectionExtensions.ValidateAll<int, int, string>(source, null!)).ShouldBeOfType<ArgumentNullException>();

        exception.ParamName.ShouldBe("selector");
        source.Enumerations.ShouldBe(0);
    }

    [Fact]
    public void Empty_source_gives_valid_unit() {
        var ready = new TrackedSequence<Validation<int, string>>();
        var mapped = new TrackedSequence<int>();
        var calls = 0;

        ready.ValidateAll().ShouldBe(Validation<Unit, string>.Valid(Unit.Value));
        mapped.ValidateAll(value => { calls++; return Validation<int, string>.Valid(value); }).ShouldBe(Validation<Unit, string>.Valid(Unit.Value));
        (calls, ready.Enumerations, ready.Disposals, mapped.Enumerations, mapped.Disposals).ShouldBe((0, 1, 1, 1, 1));
    }

    [Fact]
    public void All_valid_gives_valid_unit_for_arbitrary_null_nested_and_large_payloads() {
        new[] { Validation<string?, int>.Valid(null) }.ValidateAll().ShouldBe(Validation<Unit, int>.Valid(Unit.Value));
        new[] { Validation<Validation<int, int>, int>.Valid(default) }.ValidateAll().ShouldBe(Validation<Unit, int>.Valid(Unit.Value));
        new[] { Validation<Unit, int>.Valid(Unit.Value) }.ValidateAll().ShouldBe(Validation<Unit, int>.Valid(Unit.Value));
        Enumerable.Range(0, 3).ValidateAll(static value => Validation<LargePayload, int>.Valid(new LargePayload(value, value, value, value))).ShouldBe(Validation<Unit, int>.Valid(Unit.Value));
        new[] { 1 }.ValidateAll(static _ => Validation<Task<int>, int>.Valid(Task.FromException<int>(new ProbeException("task")))).ShouldBe(Validation<Unit, int>.Valid(Unit.Value));
    }

    [Fact]
    public void Selector_is_called_once_per_element_and_invalid_does_not_stop_traversal() {
        var source = new TrackedSequence<int>(0, 1, 2, 3, 4);
        var visited = new List<int>();

        var result = source.ValidateAll(value => {
            visited.Add(value);
            return value switch {
                1 => Validation<string, string?>.InvalidMany(["1a", null]),
                3 => Validation<string, string?>.InvalidMany(["3a", "3a"]),
                _ => Validation<string, string?>.Valid($"v{value}"),
            };
        });

        result.ShouldBe(Validation<Unit, string?>.InvalidMany(["1a", null, "3a", "3a"]));
        visited.ShouldBe([0, 1, 2, 3, 4]);
        (source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((1, 6, 5, 1));
    }

    [Fact]
    public void Ready_form_accumulates_all_issues_in_order() {
        var source = new TrackedSequence<Validation<int, string?>>(
            Validation<int, string?>.Invalid(null),
            Validation<int, string?>.Valid(1),
            Validation<int, string?>.InvalidMany(["b", "b"]));

        source.ValidateAll().ShouldBe(Validation<Unit, string?>.InvalidMany([null, "b", "b"]));
        (source.Enumerations, source.MoveNextCalls, source.CurrentReads, source.Disposals).ShouldBe((1, 4, 3, 1));
    }

    [Fact]
    public void Identical_invalid_inputs_are_added_per_position_without_equality() {
        var issue = new ReferenceProbe(1);
        var invalid = Validation<int, ReferenceProbe>.Invalid(issue);

        new[] { invalid, invalid }.ValidateAll().TryGetValue(out _, out var issues).ShouldBeFalse();
        issues.Length.ShouldBe(2);
        foreach (var observed in issues) {
            observed.ShouldBeSameAs(issue);
        }
        (issue.EqualsCalls + issue.HashCalls + issue.ToStringCalls).ShouldBe(0);
    }

    [Fact]
    public void Single_invalid_keeps_its_issue_buffer_in_both_forms() {
        var invalid = Validation<int, string>.InvalidMany(["a", "b"]);
        invalid.TryGetValue(out _, out var expected).ShouldBeFalse();

        new[] { Validation<int, string>.Valid(1), invalid }.ValidateAll().TryGetValue(out _, out var ready).ShouldBeFalse();
        new[] { 0, 1 }.ValidateAll(value => value == 1 ? invalid : Validation<int, string>.Valid(value)).TryGetValue(out _, out var mapped).ShouldBeFalse();

        (ready == expected).ShouldBeTrue();
        (mapped == expected).ShouldBeTrue();
    }

    [Fact]
    public void Many_issues_are_accumulated_linearly_in_order() {
        Enumerable.Range(0, 1000).ValidateAll(static value => Validation<int, int>.Invalid(value)).TryGetValue(out _, out var issues).ShouldBeFalse();

        issues.ShouldBe(Enumerable.Range(0, 1000));
    }

    [Fact]
    public void Visited_default_throws_after_accumulated_issues_and_stops() {
        var ready = new TrackedSequence<Validation<int, string>>(
            Validation<int, string>.Invalid("first"), default, Validation<int, string>.Invalid("later")) { PoisonFrom = 2 };
        var mapped = new TrackedSequence<int>(0, 1, 2) { PoisonFrom = 2 };
        var calls = 0;

        Should.Throw<InvalidOperationException>(() => ready.ValidateAll()).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<InvalidOperationException>(() => mapped.ValidateAll(value => {
            calls++;
            return value == 0 ? Validation<int, string>.Invalid("first") : default;
        })).ShouldBeOfType<InvalidOperationException>();

        (ready.MoveNextCalls, ready.CurrentReads, ready.Disposals).ShouldBe((2, 2, 1));
        (calls, mapped.MoveNextCalls, mapped.Disposals).ShouldBe((2, 2, 1));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Selector_exception_and_cancellation_after_invalid_stop_and_discard_issues(bool cancel) {
        Exception thrown = cancel ? new OperationCanceledException() : new ProbeException("selector");
        var source = new TrackedSequence<int>(0, 1, 2) { PoisonFrom = 2 };

        var observed = Record.Exception(() => source.ValidateAll(value =>
            value == 0 ? Validation<int, string>.Invalid("zero") : throw thrown));

        observed.ShouldBeSameAs(thrown);
        (source.MoveNextCalls, source.Disposals).ShouldBe((2, 1));
    }

    [Fact]
    public void Source_exception_after_invalid_propagates_and_disposes() {
        var source = new TrackedSequence<Validation<int, string>>(Validation<int, string>.Invalid("e"), Validation<int, string>.Valid(1)) {
            PoisonFrom = 1,
        };

        Should.Throw<ProbeException>(() => source.ValidateAll());
        source.Disposals.ShouldBe(1);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("invalid")]
    [InlineData("default")]
    [InlineData("selector exception")]
    public void Dispose_exception_replaces_the_outcome_or_earlier_exception(string exit) {
        var disposeException = new ProbeException("dispose");
        var source = new TrackedSequence<int>(1) { DisposeException = disposeException };

        var observed = Should.Throw<ProbeException>(() => source.ValidateAll(value => exit switch {
            "valid" => Validation<int, string>.Valid(value),
            "invalid" => Validation<int, string>.Invalid("e"),
            "default" => default,
            _ => throw new ProbeException("selector"),
        }));

        observed.ShouldBeSameAs(disposeException);
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("invalid")]
    [InlineData("default")]
    [InlineData("source exception")]
    public void Ready_form_dispose_exception_replaces_the_outcome_or_earlier_exception(string exit) {
        var disposeException = new ProbeException("dispose");
        Validation<int, string> item = exit switch {
            "valid" => Validation<int, string>.Valid(1),
            "invalid" or "source exception" => Validation<int, string>.Invalid("e"),
            _ => default,
        };
        var source = new TrackedSequence<Validation<int, string>>(item) {
            DisposeException = disposeException,
            PoisonFrom = exit == "source exception" ? 1 : int.MaxValue,
        };

        Should.Throw<ProbeException>(() => source.ValidateAll()).ShouldBeSameAs(disposeException);
        source.Disposals.ShouldBe(1);
    }

    [Fact]
    public void Ready_form_matches_identity_selector_form() {
        Validation<int, string>[] outcomes = [Validation<int, string>.Invalid("a"), Validation<int, string>.Valid(2), Validation<int, string>.Invalid("b")];

        outcomes.ValidateAll().ShouldBe(outcomes.ValidateAll(static outcome => outcome));
    }

    private readonly record struct LargePayload(long A, long B, long C, long D);
}
