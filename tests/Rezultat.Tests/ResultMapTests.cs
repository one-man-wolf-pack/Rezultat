namespace Rezultat.Tests;

public sealed class ResultMapTests
{
    [Fact]
    public void Map_on_success_invokes_selector_once_and_wraps_its_value_as_success()
    {
        var received = new List<int>();

        var mapped = Result<int, string>.Ok(5).Map(value => { received.Add(value); return $"v{value}"; });

        Assert.Equal(Result<string, string>.Ok("v5"), mapped);
        Assert.Equal([5], received);
    }

    [Fact]
    public void Map_on_failure_skips_selector_and_keeps_error_identity()
    {
        var error = new ReferenceProbe(1);
        var calls = 0;

        var mapped = Result<int, ReferenceProbe>.Error(error).Map(value => ++calls + value);

        Assert.False(mapped.TryGetValue(out _, out var observed));
        Assert.Same(error, observed);
        Assert.Equal(0, calls);
        Assert.Equal(0, error.EqualsCalls + error.HashCalls + error.ToStringCalls);
    }

    [Fact]
    public void Map_on_same_type_failure_keeps_failure_branch()
    {
        Assert.Equal(Result<int, int>.Error(5), Result<int, int>.Error(5).Map(static value => value + 1));
        Assert.Equal(Result<Unit, Unit>.Error(Unit.Value), Result<Unit, Unit>.Error(Unit.Value).Map(static value => value));
    }

    [Fact]
    public void Map_passes_null_payload_and_accepts_null_result()
    {
        string? received = "unset";

        var mapped = Result<string?, int>.Ok(null).Map<string?>(value => { received = value; return null; });

        Assert.Null(received);
        Assert.Equal(Result<string?, int>.Ok(null), mapped);
    }

    [Fact]
    public void Map_keeps_returned_reference_identity_and_unit_payload()
    {
        var payload = new ReferenceProbe(2);

        Assert.True(Result<Unit, string>.Ok(Unit.Value).Map(_ => payload).TryGetValue(out var value, out _));
        Assert.Same(payload, value);
    }

    [Fact]
    public void Map_does_not_check_or_unwrap_nested_carrier_or_task_payload()
    {
        var nested = Result<int, string>.Ok(1).Map(static _ => default(Result<int, string>));
        var faulted = Result<int, string>.Ok(1).Map(static _ => Task.FromException<int>(new ProbeException("task")));

        Assert.True(nested.TryGetValue(out var inner, out _));
        Assert.False(inner.IsInitialized);
        Assert.True(faulted.TryGetValue(out var task, out _));
        Assert.True(task.IsFaulted);
    }

    [Fact]
    public void Map_does_not_mutate_receiver()
    {
        var receiver = Result<int, string>.Ok(1);

        _ = receiver.Map(static value => value + 1);

        Assert.Equal(Result<int, string>.Ok(1), receiver);
    }

    [Fact]
    public void MapError_on_failure_invokes_selector_once_and_stays_failure()
    {
        var received = new List<string>();

        var mapped = Result<int, string>.Error("e").MapError(error => { received.Add(error); return error.Length; });

        Assert.Equal(Result<int, int>.Error(1), mapped);
        Assert.Equal(["e"], received);
    }

    [Fact]
    public void MapError_on_success_skips_selector_and_keeps_value_identity()
    {
        var value = new ReferenceProbe(3);
        var calls = 0;

        var mapped = Result<ReferenceProbe, string>.Ok(value).MapError(error => ++calls + error.Length);

        Assert.True(mapped.TryGetValue(out var observed, out _));
        Assert.Same(value, observed);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void MapError_on_same_type_success_keeps_success_branch()
    {
        Assert.Equal(Result<int, int>.Ok(5), Result<int, int>.Ok(5).MapError(static error => error + 1));
    }

    [Fact]
    public void MapError_passes_null_error_and_keeps_null_new_error_as_failure()
    {
        string? received = "unset";

        var mapped = Result<int, string?>.Error(null).MapError<string?>(error => { received = error; return null; });

        Assert.Null(received);
        Assert.Equal(Result<int, string?>.Error(null), mapped);
    }

    [Fact]
    public void MapError_does_not_check_nested_carrier_error_payload()
    {
        var mapped = Result<int, string>.Error("e").MapError(static _ => default(Result<int, string>));

        Assert.False(mapped.TryGetValue(out _, out var inner));
        Assert.False(inner.IsInitialized);
    }

    [Fact]
    public void Contextual_MapError_passes_error_first_and_state_second_once()
    {
        var state = new ReferenceProbe(4);
        var calls = new List<(string Error, ReferenceProbe State)>();

        var mapped = Result<int, string>.Error("e").MapError(state, (error, context) =>
        {
            calls.Add((error, context));
            return context.Id + error.Length;
        });

        Assert.Equal(Result<int, int>.Error(5), mapped);
        var call = Assert.Single(calls);
        Assert.Equal("e", call.Error);
        Assert.Same(state, call.State);
    }

    [Fact]
    public void Contextual_MapError_on_success_skips_selector_and_keeps_value()
    {
        var calls = 0;

        var mapped = Result<int, string>.Ok(7).MapError(10, (error, state) => ++calls + state + error.Length);

        Assert.Equal(Result<int, int>.Ok(7), mapped);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Contextual_MapError_accepts_null_state_and_null_error()
    {
        var mapped = Result<int, string?>.Error(null).MapError<string, string?>(
            null,
            static (error, state) => $"{error ?? "no error"}/{state ?? "no state"}");

        Assert.Equal(Result<int, string>.Error("no error/no state"), mapped);
    }

    [Fact]
    public void Contextual_MapError_passes_value_state_by_value()
    {
        var state = new ValueProbe(9);

        var mapped = Result<int, string>.Error("e").MapError(state, static (_, context) => context);

        Assert.Equal(Result<int, ValueProbe>.Error(new ValueProbe(9)), mapped);
    }

    [Fact]
    public void Selector_exceptions_propagate_unchanged()
    {
        var thrown = new ProbeException("selector");
        var cancelled = new OperationCanceledException();

        Assert.Same(thrown, Assert.Throws<ProbeException>(() => Result<int, string>.Ok(1).Map<int>(_ => throw thrown)));
        Assert.Same(thrown, Assert.Throws<ProbeException>(() => Result<int, string>.Error("e").MapError<int>(_ => throw thrown)));
        Assert.Same(cancelled, Assert.Throws<OperationCanceledException>(
            () => Result<int, string>.Error("e").MapError<int, int>(0, (_, _) => throw cancelled)));
    }
}
