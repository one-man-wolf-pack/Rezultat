namespace Rezultat.Tests;

public sealed class ResultAndThenTests {
    [Fact]
    public void AndThen_on_success_invokes_next_once_and_returns_its_success() {
        var received = new List<int>();

        var result = Result<int, string>.Ok(2).AndThen(value => {
            received.Add(value);
            return Result<string, string>.Ok($"v{value}");
        });

        result.ShouldBe(Result<string, string>.Ok("v2"));
        received.ShouldBe([2]);
    }

    [Fact]
    public void AndThen_returns_failure_of_next_with_its_identity() {
        var error = new ReferenceProbe(1);

        var result = Result<int, ReferenceProbe>.Ok(2).AndThen(_ => Result<int, ReferenceProbe>.Error(error));

        result.TryGetValue(out _, out var observed).ShouldBeFalse();
        observed.ShouldBeSameAs(error);
    }

    [Fact]
    public void AndThen_on_failure_skips_next_and_keeps_error_with_new_success_type() {
        var error = new ReferenceProbe(2);
        int calls = 0;

        var result = Result<int, ReferenceProbe>.Error(error)
            .AndThen(value => Result<string, ReferenceProbe>.Ok($"{++calls + value}"));

        result.TryGetValue(out _, out var observed).ShouldBeFalse();
        observed.ShouldBeSameAs(error);
        calls.ShouldBe(0);
    }

    [Fact]
    public void AndThen_on_same_type_failure_keeps_failure_branch() {
        Result<int, int>.Error(3).AndThen(static value => Result<int, int>.Ok(value)).ShouldBe(Result<int, int>.Error(3));
    }

    [Fact]
    public void AndThen_rejects_uninitialized_outcome_returned_by_next() {
        int calls = 0;

        Should.Throw<InvalidOperationException>(() => Result<int, string>.Ok(1).AndThen(_ => {
            calls++;
            return default(Result<int, string>);
        })).ShouldBeOfType<InvalidOperationException>();
        calls.ShouldBe(1);
    }

    [Fact]
    public void AndThen_does_not_check_nested_carrier_inside_returned_payload() {
        var result = Result<int, string>.Ok(1)
            .AndThen(static _ => Result<Result<int, string>, string>.Ok(default));

        result.TryGetValue(out var inner, out _).ShouldBeTrue();
        inner.IsInitialized.ShouldBeFalse();
    }

    [Fact]
    public void AndThen_with_identity_flattens_nested_outcome() {
        var nested = Result<Result<int, string>, string>.Ok(Result<int, string>.Error("inner"));

        nested.AndThen(static inner => inner).ShouldBe(Result<int, string>.Error("inner"));
    }

    [Fact]
    public void AndThen_passes_null_payload_and_unit_flow() {
        var result = Result<string?, string>.Ok(null)
            .AndThen(static value => Result<Unit, string>.Ok(value is null ? Unit.Value : default));

        result.ShouldBe(Result<Unit, string>.Ok(Unit.Value));
    }

    [Fact]
    public void Next_exceptions_propagate_unchanged() {
        var cancelled = new OperationCanceledException();

        var observed = Should.Throw<OperationCanceledException>(() => Result<int, string>.Ok(1).AndThen<int>(_ => throw cancelled)).ShouldBeOfType<OperationCanceledException>();

        observed.ShouldBeSameAs(cancelled);
    }
}
