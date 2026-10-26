namespace Rezultat.Tests;

public sealed class PresenceTraverseTests {
    [Fact]
    public void Missing_and_null_become_ok_with_the_same_state_without_invoking_the_selector() {
        int calls = 0;

        Presence<int>.Missing.Traverse(_ => { calls++; return Result<string, int>.Ok("x"); }).ShouldBe(Result<Presence<string>, int>.Ok(Presence<string>.Missing));
        default(Presence<int>).Traverse(_ => { calls++; return Result<string, int>.Ok("x"); }).ShouldBe(Result<Presence<string>, int>.Ok(Presence<string>.Missing));
        Presence<int>.Null.Traverse(_ => { calls++; return Result<string, int>.Ok("x"); }).ShouldBe(Result<Presence<string>, int>.Ok(Presence<string>.Null));
        calls.ShouldBe(0);
    }

    [Fact]
    public void Value_invokes_the_selector_once_and_wraps_its_ok_value() {
        var payload = new ReferenceProbe(1);
        var result = new ReferenceProbe(2);
        var seen = new List<ReferenceProbe>();

        var traversed = Presence<ReferenceProbe>.Value(payload).Traverse(value => { seen.Add(value); return Result<ReferenceProbe, string>.Ok(result); });

        seen.ShouldHaveSingleItem().ShouldBeSameAs(payload);
        traversed.TryGetValue(out var presence, out _).ShouldBeTrue();
        presence.TryGetValue(out var value).ShouldBeTrue();
        value.ShouldBeSameAs(result);
    }

    [Fact]
    public void Value_returns_the_selector_error_as_is_including_null() {
        var error = new ReferenceProbe(3);

        var failed = Presence<int>.Value(1).Traverse(_ => Result<string, ReferenceProbe>.Error(error));
        var nullError = Presence<int>.Value(1).Traverse(static _ => Result<string, string?>.Error(null));

        failed.TryGetValue(out _, out var observed).ShouldBeFalse();
        observed.ShouldBeSameAs(error);
        nullError.ShouldBe(Result<Presence<string>, string?>.Error(null));
    }

    [Fact]
    public void Ok_null_from_the_selector_is_an_invalid_operation_not_null_or_missing() {
        Should.Throw<InvalidOperationException>(() => Presence<int>.Value(1).Traverse(static _ => Result<string, int>.Ok(null!))).ShouldBeOfType<InvalidOperationException>();
    }

#pragma warning disable CS8714 // Bypassed notnull constraint: the runtime guard must hold without the warning.
    [Fact]
    public void Empty_nullable_ok_is_rejected_and_non_empty_one_is_kept() {
        Should.Throw<InvalidOperationException>(() => Presence<int>.Value(1).Traverse(static _ => Result<int?, string>.Ok(null))).ShouldBeOfType<InvalidOperationException>();

        var kept = Presence<int>.Value(1).Traverse(static value => Result<int?, string>.Ok(value + 1));
        (kept.TryGetValue(out var presence, out _) && presence.TryGetValue(out int? value) && value == 2).ShouldBeTrue();
    }
#pragma warning restore CS8714

    [Fact]
    public void Uninitialized_selector_result_is_an_invalid_operation() {
        Should.Throw<InvalidOperationException>(() => Presence<int>.Value(1).Traverse(static _ => default(Result<int, string>))).ShouldBeOfType<InvalidOperationException>();
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("null")]
    [InlineData("value")]
    public void Null_selector_is_rejected_in_every_state(string state) {
        var presence = state switch { "value" => Presence<int>.Value(1), "null" => Presence<int>.Null, _ => Presence<int>.Missing };

        Should.Throw<ArgumentNullException>(() => presence.Traverse<int, int, string>(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("selector");
    }

    [Fact]
    public void Nested_carriers_in_the_ok_value_are_not_checked() {
        var nested = Presence<int>.Value(1).Traverse(static _ => Result<Result<int, string>, string>.Ok(default));
        var nestedPresence = Presence<int>.Value(1).Traverse(static _ => Result<Presence<int>, string>.Ok(Presence<int>.Null));

        (nested.TryGetValue(out var outer, out _) && outer.TryGetValue(out var inner) && !inner.IsInitialized).ShouldBeTrue();
        (nestedPresence.TryGetValue(out var wrapper, out _) && wrapper.TryGetValue(out var innerPresence) && innerPresence.IsNull).ShouldBeTrue();
    }

    [Fact]
    public void Selector_exceptions_and_cancellation_propagate_unchanged() {
        var invalid = new InvalidOperationException("selector");
        var canceled = new OperationCanceledException();

        Should.Throw<InvalidOperationException>(() => Presence<int>.Value(1).Traverse<int, int, string>(_ => throw invalid)).ShouldBeOfType<InvalidOperationException>().ShouldBeSameAs(invalid);
        Should.Throw<OperationCanceledException>(() => Presence<int>.Value(1).Traverse<int, int, string>(_ => throw canceled)).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(canceled);
    }

    [Fact]
    public void Selector_reassigning_the_source_variable_does_not_change_the_traversed_snapshot() {
        var source = Presence<int>.Value(1);
        var seen = new List<int>();

        var traversed = source.Traverse(value => {
            source = Presence<int>.Null;
            seen.Add(value);
            return Result<int, string>.Ok(value + 1);
        });

        seen.ShouldBe([1]);
        traversed.ShouldBe(Result<Presence<int>, string>.Ok(Presence<int>.Value(2)));
    }
}
