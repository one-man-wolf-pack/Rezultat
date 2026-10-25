namespace Rezultat.Tests;

public sealed class ResultInspectTests {
    [Fact]
    public void Inspect_invokes_action_once_on_success_with_the_exact_payload_and_returns_the_carrier() {
        var payload = new ReferenceProbe(1);
        var seen = new List<ReferenceProbe>();
        var source = Result<ReferenceProbe, string>.Ok(payload);

        var observed = source.Inspect(seen.Add);

        seen.ShouldHaveSingleItem().ShouldBeSameAs(payload);
        observed.TryGetValue(out var value, out _).ShouldBeTrue();
        value.ShouldBeSameAs(payload);
        (payload.EqualsCalls + payload.HashCalls + payload.ToStringCalls).ShouldBe(0);
    }

    [Fact]
    public void InspectError_invokes_action_once_on_failure_with_the_exact_payload_and_returns_the_carrier() {
        var payload = new ReferenceProbe(2);
        var seen = new List<ReferenceProbe>();

        var observed = Result<int, ReferenceProbe>.Error(payload).InspectError(seen.Add);

        seen.ShouldHaveSingleItem().ShouldBeSameAs(payload);
        observed.TryGetValue(out _, out var error).ShouldBeFalse();
        error.ShouldBeSameAs(payload);
    }

    [Fact]
    public void Inactive_branch_does_not_invoke_the_action() {
        var calls = 0;

        var success = Result<int, string>.Ok(1).InspectError(_ => calls++);
        var failure = Result<int, string>.Error("e").Inspect(_ => calls++);

        calls.ShouldBe(0);
        success.ShouldBe(Result<int, string>.Ok(1));
        failure.ShouldBe(Result<int, string>.Error("e"));
    }

    [Fact]
    public void Null_and_default_active_payloads_are_passed_without_checks() {
        var seen = new List<string?>();

        _ = Result<string?, string?>.Ok(null).Inspect(seen.Add);
        _ = Result<string?, string?>.Error(null).InspectError(seen.Add);
        var zero = Result<int, int>.Ok(0).Inspect(value => seen.Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        seen.ShouldBe([null, null, "0"]);
        zero.ShouldBe(Result<int, int>.Ok(0));
    }

    [Fact]
    public void Uninitialized_receiver_is_rejected_before_the_action_guard() {
        Should.Throw<InvalidOperationException>(() => default(Result<int, string>).Inspect(null!)).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<InvalidOperationException>(() => default(Result<int, string>).InspectError(null!)).ShouldBeOfType<InvalidOperationException>();
        Should.Throw<InvalidOperationException>(() => default(Result<int, string>).Inspect(static _ => { })).ShouldBeOfType<InvalidOperationException>();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Null_action_is_rejected_on_both_branches(bool success) {
        var result = success ? Result<int, string>.Ok(1) : Result<int, string>.Error("e");

        Should.Throw<ArgumentNullException>(() => result.Inspect(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("action");
        Should.Throw<ArgumentNullException>(() => result.InspectError(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("action");
    }

    [Fact]
    public void Default_payload_inside_initialized_carrier_is_not_uninitialized() {
        var nested = Result<Result<int, string>, string>.Ok(default);
        var seen = new List<bool>();

        var observed = nested.Inspect(inner => seen.Add(inner.IsInitialized));

        seen.ShouldBe([false]);
        observed.ShouldBe(nested);
    }

    [Fact]
    public void Action_exceptions_and_cancellation_propagate_unchanged() {
        var invalid = new InvalidOperationException("action");
        var canceled = new OperationCanceledException();

        Should.Throw<InvalidOperationException>(() => Result<int, string>.Ok(1).Inspect(_ => throw invalid)).ShouldBeOfType<InvalidOperationException>().ShouldBeSameAs(invalid);
        Should.Throw<OperationCanceledException>(() => Result<int, string>.Error("e").InspectError(_ => throw canceled)).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(canceled);
    }

    [Fact]
    public void Inspect_returns_the_snapshot_taken_before_the_action_reassigned_the_receiver() {
        var receiver = Result<int, string>.Ok(1);

        var observed = receiver.Inspect(_ => receiver = Result<int, string>.Error("reassigned"));

        observed.ShouldBe(Result<int, string>.Ok(1));
        receiver.ShouldBe(Result<int, string>.Error("reassigned"));
    }

    [Fact]
    public void InspectError_returns_the_snapshot_taken_before_the_action_reassigned_the_receiver() {
        var receiver = Result<int, string>.Error("original");

        var observed = receiver.InspectError(_ => receiver = Result<int, string>.Ok(99));

        observed.ShouldBe(Result<int, string>.Error("original"));
        receiver.ShouldBe(Result<int, string>.Ok(99));
    }

    [Fact]
    public void Mutable_reference_payload_is_shared_not_frozen() {
        var payload = new List<int> { 1 };

        var observed = Result<List<int>, string>.Ok(payload).Inspect(list => list.Add(2));

        observed.TryGetValue(out var value, out _).ShouldBeTrue();
        value.ShouldBeSameAs(payload);
        value.ShouldBe([1, 2]);
    }

    [Fact]
    public void Async_lambda_returns_the_carrier_before_its_continuation_runs() {
        var gate = new TaskCompletionSource();
        var steps = new List<string>();

        var observed = Result<int, string>.Ok(1).Inspect(async _ => {
            steps.Add("started");
            await gate.Task;
            steps.Add("resumed");
        });

        steps.ShouldBe(["started"]);
        observed.ShouldBe(Result<int, string>.Ok(1));
        gate.SetResult();
    }
}
