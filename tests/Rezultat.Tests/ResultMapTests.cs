namespace Rezultat.Tests;

public sealed class ResultMapTests {
    [Fact]
    public void Map_on_success_invokes_selector_once_and_wraps_its_value_as_success() {
        var received = new List<int>();

        var mapped = Result<int, string>.Ok(5).Map(value => { received.Add(value); return $"v{value}"; });

        mapped.ShouldBe(Result<string, string>.Ok("v5"));
        received.ShouldBe([5]);
    }

    [Fact]
    public void Map_on_failure_skips_selector_and_keeps_error_identity() {
        var error = new ReferenceProbe(1);
        var calls = 0;

        var mapped = Result<int, ReferenceProbe>.Error(error).Map(value => ++calls + value);

        mapped.TryGetValue(out _, out var observed).ShouldBeFalse();
        observed.ShouldBeSameAs(error);
        calls.ShouldBe(0);
        (error.EqualsCalls + error.HashCalls + error.ToStringCalls).ShouldBe(0);
    }

    [Fact]
    public void Map_on_same_type_failure_keeps_failure_branch() {
        Result<int, int>.Error(5).Map(static value => value + 1).ShouldBe(Result<int, int>.Error(5));
        Result<Unit, Unit>.Error(Unit.Value).Map(static value => value).ShouldBe(Result<Unit, Unit>.Error(Unit.Value));
    }

    [Fact]
    public void Map_passes_null_payload_and_accepts_null_result() {
        string? received = "unset";

        var mapped = Result<string?, int>.Ok(null).Map<string?>(value => { received = value; return null; });

        received.ShouldBeNull();
        mapped.ShouldBe(Result<string?, int>.Ok(null));
    }

    [Fact]
    public void Map_keeps_returned_reference_identity_and_unit_payload() {
        var payload = new ReferenceProbe(2);

        Result<Unit, string>.Ok(Unit.Value).Map(_ => payload).TryGetValue(out var value, out _).ShouldBeTrue();
        value.ShouldBeSameAs(payload);
    }

    [Fact]
    public void Map_does_not_check_or_unwrap_nested_carrier_or_task_payload() {
        var nested = Result<int, string>.Ok(1).Map(static _ => default(Result<int, string>));
        var faulted = Result<int, string>.Ok(1).Map(static _ => Task.FromException<int>(new ProbeException("task")));

        nested.TryGetValue(out var inner, out _).ShouldBeTrue();
        inner.IsInitialized.ShouldBeFalse();
        faulted.TryGetValue(out var task, out _).ShouldBeTrue();
        task.IsFaulted.ShouldBeTrue();
    }

    [Fact]
    public void Map_does_not_mutate_receiver() {
        var receiver = Result<int, string>.Ok(1);

        _ = receiver.Map(static value => value + 1);

        receiver.ShouldBe(Result<int, string>.Ok(1));
    }

    [Fact]
    public void MapError_on_failure_invokes_selector_once_and_stays_failure() {
        var received = new List<string>();

        var mapped = Result<int, string>.Error("e").MapError(error => { received.Add(error); return error.Length; });

        mapped.ShouldBe(Result<int, int>.Error(1));
        received.ShouldBe(["e"]);
    }

    [Fact]
    public void MapError_on_success_skips_selector_and_keeps_value_identity() {
        var value = new ReferenceProbe(3);
        var calls = 0;

        var mapped = Result<ReferenceProbe, string>.Ok(value).MapError(error => ++calls + error.Length);

        mapped.TryGetValue(out var observed, out _).ShouldBeTrue();
        observed.ShouldBeSameAs(value);
        calls.ShouldBe(0);
    }

    [Fact]
    public void MapError_on_same_type_success_keeps_success_branch() {
        Result<int, int>.Ok(5).MapError(static error => error + 1).ShouldBe(Result<int, int>.Ok(5));
    }

    [Fact]
    public void MapError_passes_null_error_and_keeps_null_new_error_as_failure() {
        string? received = "unset";

        var mapped = Result<int, string?>.Error(null).MapError<string?>(error => { received = error; return null; });

        received.ShouldBeNull();
        mapped.ShouldBe(Result<int, string?>.Error(null));
    }

    [Fact]
    public void MapError_does_not_check_nested_carrier_error_payload() {
        var mapped = Result<int, string>.Error("e").MapError(static _ => default(Result<int, string>));

        mapped.TryGetValue(out _, out var inner).ShouldBeFalse();
        inner.IsInitialized.ShouldBeFalse();
    }

    [Fact]
    public void Contextual_MapError_passes_error_first_and_state_second_once() {
        var state = new ReferenceProbe(4);
        var calls = new List<(string Error, ReferenceProbe State)>();

        var mapped = Result<int, string>.Error("e").MapError(state, (error, context) => {
            calls.Add((error, context));
            return context.Id + error.Length;
        });

        mapped.ShouldBe(Result<int, int>.Error(5));
        var call = calls.ShouldHaveSingleItem();
        call.Error.ShouldBe("e");
        call.State.ShouldBeSameAs(state);
    }

    [Fact]
    public void Contextual_MapError_on_success_skips_selector_and_keeps_value() {
        var calls = 0;

        var mapped = Result<int, string>.Ok(7).MapError(10, (error, state) => ++calls + state + error.Length);

        mapped.ShouldBe(Result<int, int>.Ok(7));
        calls.ShouldBe(0);
    }

    [Fact]
    public void Contextual_MapError_accepts_null_state_and_null_error() {
        var mapped = Result<int, string?>.Error(null).MapError<string, string?>(
            null,
            static (error, state) => $"{error ?? "no error"}/{state ?? "no state"}");

        mapped.ShouldBe(Result<int, string>.Error("no error/no state"));
    }

    [Fact]
    public void Contextual_MapError_passes_value_state_by_value() {
        var state = new ValueProbe(9);

        var mapped = Result<int, string>.Error("e").MapError(state, static (_, context) => context);

        mapped.ShouldBe(Result<int, ValueProbe>.Error(new ValueProbe(9)));
    }

    [Fact]
    public void Selector_exceptions_propagate_unchanged() {
        var thrown = new ProbeException("selector");
        var cancelled = new OperationCanceledException();

        Should.Throw<ProbeException>(() => Result<int, string>.Ok(1).Map<int>(_ => throw thrown)).ShouldBeSameAs(thrown);
        Should.Throw<ProbeException>(() => Result<int, string>.Error("e").MapError<int>(_ => throw thrown)).ShouldBeSameAs(thrown);
        Should.Throw<OperationCanceledException>(() => Result<int, string>.Error("e").MapError<int, int>(0, (_, _) => throw cancelled)).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(cancelled);
    }
}
