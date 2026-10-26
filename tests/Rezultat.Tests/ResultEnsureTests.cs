namespace Rezultat.Tests;

public sealed class ResultEnsureTests {
    [Fact]
    public void Ensure_true_keeps_success_payload_and_skips_error_factory() {
        var value = new ReferenceProbe(1);
        var calls = new List<string>();

        var result = Result<ReferenceProbe, string>.Ok(value).Ensure(
            candidate => { calls.Add($"predicate:{candidate.Id}"); return true; },
            candidate => { calls.Add($"factory:{candidate.Id}"); return "rejected"; });

        result.TryGetValue(out var observed, out _).ShouldBeTrue();
        observed.ShouldBeSameAs(value);
        calls.ShouldBe(["predicate:1"]);
    }

    [Fact]
    public void Ensure_false_creates_error_from_same_value_after_predicate() {
        var value = new ReferenceProbe(2);
        var calls = new List<(string Callback, ReferenceProbe Value)>();

        var result = Result<ReferenceProbe, string>.Ok(value).Ensure(
            candidate => { calls.Add(("predicate", candidate)); return false; },
            candidate => { calls.Add(("factory", candidate)); return $"rejected {candidate.Id}"; });

        result.ShouldBe(Result<ReferenceProbe, string>.Error("rejected 2"));
        calls.Select(call => call.Callback).ShouldBe(["predicate", "factory"]);
        foreach (var call in calls) {
            call.Value.ShouldBeSameAs(value);
        }
    }

    [Fact]
    public void Ensure_passes_value_type_payload_by_value_to_each_callback() {
        int factoryInput = -1;

        var result = Result<int, int>.Ok(5).Ensure(
            candidate => { candidate++; return candidate < 0; },
            candidate => factoryInput = candidate);

        factoryInput.ShouldBe(5);
        result.ShouldBe(Result<int, int>.Error(5));
    }

    [Fact]
    public void Ensure_false_creates_error_from_payload_observed_before_predicate_reassigned_receiver() {
        var receiver = Result<int, int>.Ok(1);

        var result = receiver.Ensure(
            _ => { receiver = Result<int, int>.Ok(99); return false; },
            static value => value);

        result.ShouldBe(Result<int, int>.Error(1));
    }

    [Fact]
    public void Ensure_true_keeps_original_success_when_predicate_reassigns_receiver() {
        var receiver = Result<int, int>.Ok(1);

        var result = receiver.Ensure(
            _ => { receiver = default; return true; },
            static value => value);

        result.ShouldBe(Result<int, int>.Ok(1));
    }

    [Fact]
    public void Ensure_on_failure_skips_both_callbacks_and_keeps_error_identity() {
        var error = new ReferenceProbe(3);
        int calls = 0;

        var result = Result<int, ReferenceProbe>.Error(error).Ensure(_ => ++calls > 0, _ => new ReferenceProbe(++calls));

        result.TryGetValue(out _, out var observed).ShouldBeFalse();
        observed.ShouldBeSameAs(error);
        calls.ShouldBe(0);
    }

    [Fact]
    public void Ensure_on_same_type_failure_keeps_failure_branch() {
        Result<int, int>.Error(4).Ensure(static _ => false, static value => value + 1).ShouldBe(Result<int, int>.Error(4));
    }

    [Fact]
    public void Ensure_passes_null_payload_and_keeps_null_error_as_failure() {
        var result = Result<string?, string?>.Ok(null).Ensure(static value => value is not null, static value => value);

        result.ShouldBe(Result<string?, string?>.Error(null));
    }

    [Fact]
    public void Ensure_keeps_uninitialized_carrier_error_as_ordinary_payload() {
        var result = Result<int, Result<int, string>>.Ok(1).Ensure(static _ => false, static _ => default);

        result.TryGetValue(out _, out var inner).ShouldBeFalse();
        inner.IsInitialized.ShouldBeFalse();
    }

    [Fact]
    public void Sequential_Ensure_keeps_first_failure_and_skips_later_checks() {
        int calls = 0;

        var result = Result<int, string>.Ok(1)
            .Ensure(static _ => false, static _ => "first")
            .Ensure(_ => ++calls > 0, _ => "second");

        result.ShouldBe(Result<int, string>.Error("first"));
        calls.ShouldBe(0);
    }

    [Fact]
    public void Callback_exceptions_propagate_unchanged() {
        var thrown = new ProbeException("predicate");
        var cancelled = new OperationCanceledException();

        Should.Throw<ProbeException>(() => Result<int, string>.Ok(1).Ensure(_ => throw thrown, static _ => "e")).ShouldBeSameAs(thrown);
        Should.Throw<OperationCanceledException>(() => Result<int, string>.Ok(1).Ensure(static _ => false, _ => throw cancelled)).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(cancelled);
    }
}
