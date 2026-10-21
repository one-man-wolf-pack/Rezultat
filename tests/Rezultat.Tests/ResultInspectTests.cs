namespace Rezultat.Tests;

public sealed class ResultInspectTests
{
    [Fact]
    public void Inspect_invokes_action_once_on_success_with_the_exact_payload_and_returns_the_carrier()
    {
        var payload = new ReferenceProbe(1);
        var seen = new List<ReferenceProbe>();
        var source = Result<ReferenceProbe, string>.Ok(payload);

        var observed = source.Inspect(seen.Add);

        Assert.Same(payload, Assert.Single(seen));
        Assert.True(observed.TryGetValue(out var value, out _));
        Assert.Same(payload, value);
        Assert.Equal(0, payload.EqualsCalls + payload.HashCalls + payload.ToStringCalls);
    }

    [Fact]
    public void InspectError_invokes_action_once_on_failure_with_the_exact_payload_and_returns_the_carrier()
    {
        var payload = new ReferenceProbe(2);
        var seen = new List<ReferenceProbe>();

        var observed = Result<int, ReferenceProbe>.Error(payload).InspectError(seen.Add);

        Assert.Same(payload, Assert.Single(seen));
        Assert.False(observed.TryGetValue(out _, out var error));
        Assert.Same(payload, error);
    }

    [Fact]
    public void Inactive_branch_does_not_invoke_the_action()
    {
        var calls = 0;

        var success = Result<int, string>.Ok(1).InspectError(_ => calls++);
        var failure = Result<int, string>.Error("e").Inspect(_ => calls++);

        Assert.Equal(0, calls);
        Assert.Equal(Result<int, string>.Ok(1), success);
        Assert.Equal(Result<int, string>.Error("e"), failure);
    }

    [Fact]
    public void Null_and_default_active_payloads_are_passed_without_checks()
    {
        var seen = new List<string?>();

        _ = Result<string?, string?>.Ok(null).Inspect(seen.Add);
        _ = Result<string?, string?>.Error(null).InspectError(seen.Add);
        var zero = Result<int, int>.Ok(0).Inspect(value => seen.Add(value.ToString(System.Globalization.CultureInfo.InvariantCulture)));

        Assert.Equal([null, null, "0"], seen);
        Assert.Equal(Result<int, int>.Ok(0), zero);
    }

    [Fact]
    public void Uninitialized_receiver_is_rejected_before_the_action_guard()
    {
        Assert.Throws<InvalidOperationException>(() => default(Result<int, string>).Inspect(null!));
        Assert.Throws<InvalidOperationException>(() => default(Result<int, string>).InspectError(null!));
        Assert.Throws<InvalidOperationException>(() => default(Result<int, string>).Inspect(static _ => { }));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Null_action_is_rejected_on_both_branches(bool success)
    {
        var result = success ? Result<int, string>.Ok(1) : Result<int, string>.Error("e");

        Assert.Equal("action", Assert.Throws<ArgumentNullException>(() => result.Inspect(null!)).ParamName);
        Assert.Equal("action", Assert.Throws<ArgumentNullException>(() => result.InspectError(null!)).ParamName);
    }

    [Fact]
    public void Default_payload_inside_initialized_carrier_is_not_uninitialized()
    {
        var nested = Result<Result<int, string>, string>.Ok(default);
        var seen = new List<bool>();

        var observed = nested.Inspect(inner => seen.Add(inner.IsInitialized));

        Assert.Equal([false], seen);
        Assert.Equal(nested, observed);
    }

    [Fact]
    public void Action_exceptions_and_cancellation_propagate_unchanged()
    {
        var invalid = new InvalidOperationException("action");
        var canceled = new OperationCanceledException();

        Assert.Same(invalid, Assert.Throws<InvalidOperationException>(() => Result<int, string>.Ok(1).Inspect(_ => throw invalid)));
        Assert.Same(canceled, Assert.Throws<OperationCanceledException>(() => Result<int, string>.Error("e").InspectError(_ => throw canceled)));
    }

    [Fact]
    public void Inspect_returns_the_snapshot_taken_before_the_action_reassigned_the_receiver()
    {
        var receiver = Result<int, string>.Ok(1);

        var observed = receiver.Inspect(_ => receiver = Result<int, string>.Error("reassigned"));

        Assert.Equal(Result<int, string>.Ok(1), observed);
        Assert.Equal(Result<int, string>.Error("reassigned"), receiver);
    }

    [Fact]
    public void InspectError_returns_the_snapshot_taken_before_the_action_reassigned_the_receiver()
    {
        var receiver = Result<int, string>.Error("original");

        var observed = receiver.InspectError(_ => receiver = Result<int, string>.Ok(99));

        Assert.Equal(Result<int, string>.Error("original"), observed);
        Assert.Equal(Result<int, string>.Ok(99), receiver);
    }

    [Fact]
    public void Mutable_reference_payload_is_shared_not_frozen()
    {
        var payload = new List<int> { 1 };

        var observed = Result<List<int>, string>.Ok(payload).Inspect(list => list.Add(2));

        Assert.True(observed.TryGetValue(out var value, out _));
        Assert.Same(payload, value);
        Assert.Equal([1, 2], value);
    }

    [Fact]
    public void Async_lambda_returns_the_carrier_before_its_continuation_runs()
    {
        var gate = new TaskCompletionSource();
        var steps = new List<string>();

        var observed = Result<int, string>.Ok(1).Inspect(async _ =>
        {
            steps.Add("started");
            await gate.Task;
            steps.Add("resumed");
        });

        Assert.Equal(["started"], steps);
        Assert.Equal(Result<int, string>.Ok(1), observed);
        gate.SetResult();
    }
}
