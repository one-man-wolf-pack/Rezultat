namespace Rezultat.Tests;

public sealed class ResultOrElseTests {
    [Fact]
    public void OrElse_on_failure_invokes_fallback_once_and_returns_its_success() {
        var received = new List<string>();

        var result = Result<int, string>.Error("e").OrElse(error => {
            received.Add(error);
            return Result<int, int>.Ok(error.Length);
        });

        result.ShouldBe(Result<int, int>.Ok(1));
        received.ShouldBe(["e"]);
    }

    [Fact]
    public void OrElse_returns_failure_of_fallback_without_keeping_original_error() {
        var replacement = new ReferenceProbe(1);

        var result = Result<int, string>.Error("original").OrElse(_ => Result<int, ReferenceProbe>.Error(replacement));

        result.TryGetValue(out _, out var observed).ShouldBeFalse();
        observed.ShouldBeSameAs(replacement);
    }

    [Fact]
    public void OrElse_on_success_skips_fallback_and_keeps_value_identity_in_target_type() {
        var value = new ReferenceProbe(2);
        var calls = 0;

        Result<ReferenceProbe, int> result = Result<ReferenceProbe, string>.Ok(value)
            .OrElse(_ => Result<ReferenceProbe, int>.Error(++calls));

        result.TryGetValue(out var observed, out _).ShouldBeTrue();
        observed.ShouldBeSameAs(value);
        calls.ShouldBe(0);
    }

    [Fact]
    public void OrElse_on_same_type_success_keeps_success_branch() {
        Result<int, int>.Ok(3).OrElse(static error => Result<int, int>.Ok(error + 1)).ShouldBe(Result<int, int>.Ok(3));
    }

    [Fact]
    public void OrElse_passes_null_error_and_accepts_null_payloads_of_returned_outcome() {
        string? received = "unset";

        var result = Result<string?, string?>.Error(null).OrElse(error => {
            received = error;
            return Result<string?, int>.Ok(null);
        });

        received.ShouldBeNull();
        result.ShouldBe(Result<string?, int>.Ok(null));
    }

    [Fact]
    public void OrElse_rejects_uninitialized_outcome_returned_by_fallback() {
        var calls = 0;

        Should.Throw<InvalidOperationException>(() => Result<int, string>.Error("e").OrElse(_ => {
            calls++;
            return default(Result<int, int>);
        })).ShouldBeOfType<InvalidOperationException>();
        calls.ShouldBe(1);
    }

    [Fact]
    public void OrElse_does_not_check_nested_carrier_inside_returned_payload() {
        var result = Result<int, string>.Error("e")
            .OrElse(static _ => Result<int, Result<int, string>>.Error(default));

        result.TryGetValue(out _, out var inner).ShouldBeFalse();
        inner.IsInitialized.ShouldBeFalse();
    }

    [Fact]
    public void Fallback_exceptions_propagate_unchanged() {
        var thrown = new ProbeException("fallback");

        var observed = Should.Throw<ProbeException>(() => Result<int, string>.Error("e").OrElse<int>(_ => throw thrown));

        observed.ShouldBeSameAs(thrown);
    }
}
