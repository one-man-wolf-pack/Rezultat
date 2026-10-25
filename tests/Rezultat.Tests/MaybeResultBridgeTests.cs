namespace Rezultat.Tests;

public sealed class MaybeResultBridgeTests {
    [Fact]
    public void ToResult_maps_some_to_ok_and_none_to_the_given_error() {
        Maybe<int>.Some(1).ToResult("missing").ShouldBe(Result<int, string>.Ok(1));
        Maybe<int>.None.ToResult("missing").ShouldBe(Result<int, string>.Error("missing"));
        default(Maybe<int>).ToResult("missing").ShouldBe(Result<int, string>.Error("missing"));
    }

    [Fact]
    public void ToResult_keeps_present_null_and_null_error() {
        Maybe<string?>.Some(null).ToResult("missing").ShouldBe(Result<string?, string>.Ok(null));
        Maybe<int?>.Some(null).ToResult("missing").ShouldBe(Result<int?, string>.Ok(null));
        Maybe<int>.None.ToResult<string?>(null).ShouldBe(Result<int, string?>.Error(null));
    }

    [Fact]
    public void ToResult_keeps_a_delegate_error_as_payload_without_invoking_it() {
        var calls = 0;
        Func<string> error = () => $"{++calls}";

        var result = Maybe<int>.None.ToResult(error);

        result.TryGetValue(out _, out var observed).ShouldBeFalse();
        observed.ShouldBeSameAs(error);
        calls.ShouldBe(0);
    }

    [Fact]
    public void ToResult_error_argument_is_evaluated_even_for_some() {
        var evaluations = 0;

        Maybe<int>.Some(1).ToResult(++evaluations).ShouldBe(Result<int, int>.Ok(1));
        evaluations.ShouldBe(1);
    }

    [Fact]
    public void ToResultElse_invokes_factory_once_only_for_none() {
        var calls = 0;

        Maybe<int>.Some(1).ToResultElse(() => $"{++calls}").ShouldBe(Result<int, string>.Ok(1));
        calls.ShouldBe(0);
        Maybe<int>.None.ToResultElse(() => $"{++calls}").ShouldBe(Result<int, string>.Error("1"));
        calls.ShouldBe(1);
        Maybe<int>.None.ToResultElse(static () => (string?)null).ShouldBe(Result<int, string?>.Error(null));
    }

    [Fact]
    public void ToResultElse_rejects_null_factory_even_for_some_and_propagates_its_exceptions() {
        var canceled = new OperationCanceledException();

        Should.Throw<ArgumentNullException>(() => Maybe<int>.Some(1).ToResultElse<string>(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("errorFactory");
        Should.Throw<ArgumentNullException>(() => Maybe<int>.None.ToResultElse<string>(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("errorFactory");
        Should.Throw<OperationCanceledException>(() => Maybe<int>.None.ToResultElse<string>(() => throw canceled)).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(canceled);
    }

    [Fact]
    public void Bridges_keep_nested_carriers_and_reference_identity() {
        var probe = new ReferenceProbe(1);
        var error = new ReferenceProbe(2);

        (Maybe<Maybe<int>>.Some(Maybe<int>.None).ToResult("e").TryGetValue(out var nested, out _) && !nested.IsSome).ShouldBeTrue();
        (Maybe<Result<int, string>>.Some(default).ToResult("e").TryGetValue(out var inner, out _) && !inner.IsInitialized).ShouldBeTrue();
        (Maybe<ReferenceProbe>.Some(probe).ToResult("e").TryGetValue(out var value, out _) && ReferenceEquals(probe, value)).ShouldBeTrue();
        Maybe<int>.None.ToResult(error).TryGetValue(out _, out var observedError).ShouldBeFalse();
        observedError.ShouldBeSameAs(error);
        (Result<ReferenceProbe, string>.Ok(probe).ToMaybe().TryGetValue(out var back) && ReferenceEquals(probe, back)).ShouldBeTrue();
        Result<Result<int, string>, int>.Ok(Result<int, string>.Error("inner")).ToMaybe().ShouldBe(Maybe<Result<int, string>>.Some(Result<int, string>.Error("inner")));
    }

    [Fact]
    public void ToMaybe_maps_ok_to_some_and_error_to_none_losing_the_reason() {
        var error = new ReferenceProbe(1);

        Result<int, string>.Ok(1).ToMaybe().ShouldBe(Maybe<int>.Some(1));
        Result<string?, int>.Ok(null).ToMaybe().ShouldBe(Maybe<string?>.Some(null));
        Result<int, ReferenceProbe>.Error(error).ToMaybe().ShouldBe(Maybe<int>.None);
        Result<int, string?>.Error(null).ToMaybe().ShouldBe(Maybe<int>.None);
        (error.EqualsCalls, error.HashCalls, error.ToStringCalls).ShouldBe((0, 0, 0));
    }

    [Fact]
    public void ToMaybe_rejects_uninitialized_result() {
        Should.Throw<InvalidOperationException>(() => default(Result<int, string>).ToMaybe()).ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Round_trips_hold_in_their_own_domain() {
        Maybe<string?>.Some(null).ToResult("e").ToMaybe().ShouldBe(Maybe<string?>.Some(null));
        Maybe<int>.None.ToResult("e").ToMaybe().ShouldBe(Maybe<int>.None);
        Result<int, string>.Ok(1).ToMaybe().ToResult("replacement").ShouldBe(Result<int, string>.Ok(1));
        Result<int, string>.Error("original").ToMaybe().ToResult("replacement").ShouldBe(Result<int, string>.Error("replacement"));
    }

    [Fact]
    public void Traverse_maps_none_to_ok_none_without_selector() {
        var calls = 0;

        Maybe<int>.None.Traverse(_ => { calls++; return Result<int, string>.Ok(1); }).ShouldBe(Result<Maybe<int>, string>.Ok(Maybe<int>.None));
        default(Maybe<int>).Traverse(_ => { calls++; return default(Result<int, string>); }).ShouldBe(Result<Maybe<int>, string>.Ok(Maybe<int>.None));
        calls.ShouldBe(0);
    }

    [Fact]
    public void Traverse_maps_some_through_selector_once() {
        var calls = 0;
        var error = new ReferenceProbe(1);

        Maybe<int>.Some(1).Traverse(value => { calls++; return Result<string, string>.Ok($"{value + 1}"); }).ShouldBe(Result<Maybe<string>, string>.Ok(Maybe<string>.Some("2")));
        Maybe<int>.Some(1).Traverse(_ => { calls++; return Result<int, ReferenceProbe>.Error(error); }).TryGetValue(out _, out var observed).ShouldBeFalse();
        observed.ShouldBeSameAs(error);
        calls.ShouldBe(2);
        (error.EqualsCalls, error.HashCalls, error.ToStringCalls).ShouldBe((0, 0, 0));
    }

    [Fact]
    public void Traverse_keeps_present_null_null_ok_and_null_error() {
        string? observed = "unset";

        var ok = Maybe<string?>.Some(null).Traverse(value => { observed = value; return Result<string?, int>.Ok(null); });

        observed.ShouldBeNull();
        ok.ShouldBe(Result<Maybe<string?>, int>.Ok(Maybe<string?>.Some(null)));
        Maybe<int>.Some(1).Traverse(static _ => Result<int, string?>.Error(null)).ShouldBe(Result<Maybe<int>, string?>.Error(null));
        Maybe<int?>.Some(null).Traverse(static value => Result<int?, int>.Ok(value)).ShouldBe(Result<Maybe<int?>, int>.Ok(Maybe<int?>.Some(null)));
    }

    [Fact]
    public void Traverse_rejects_uninitialized_selector_result_but_not_nested_payload() {
        var nested = Maybe<int>.Some(1).Traverse(static _ => Result<Result<int, string>, string>.Ok(default));

        Should.Throw<InvalidOperationException>(() => Maybe<int>.Some(1).Traverse(static _ => default(Result<int, string>))).ShouldBeOfType<InvalidOperationException>();
        (nested.TryGetValue(out var maybe, out _) && maybe.TryGetValue(out var inner) && !inner.IsInitialized).ShouldBeTrue();
    }

    [Fact]
    public void Traverse_with_identity_selector_turns_ready_maybe_of_result_inside_out() {
        Maybe<Result<int, string>>.Some(Result<int, string>.Ok(1)).Traverse(static r => r).ShouldBe(Result<Maybe<int>, string>.Ok(Maybe<int>.Some(1)));
        Maybe<Result<int, string>>.Some(Result<int, string>.Error("e")).Traverse(static r => r).ShouldBe(Result<Maybe<int>, string>.Error("e"));
        Maybe<Result<int, string>>.None.Traverse(static r => r).ShouldBe(Result<Maybe<int>, string>.Ok(Maybe<int>.None));
        Should.Throw<InvalidOperationException>(() => Maybe<Result<int, string>>.Some(default).Traverse(static r => r)).ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void Traverse_rejects_null_selector_even_for_none_and_propagates_its_exceptions() {
        var canceled = new OperationCanceledException();

        Should.Throw<ArgumentNullException>(() => Maybe<int>.None.Traverse<int, int, string>(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("selector");
        Should.Throw<ArgumentNullException>(() => Maybe<int>.Some(1).Traverse<int, int, string>(null!)).ShouldBeOfType<ArgumentNullException>().ParamName.ShouldBe("selector");
        Should.Throw<OperationCanceledException>(() => Maybe<int>.Some(1).Traverse<int, int, string>(_ => throw canceled)).ShouldBeOfType<OperationCanceledException>().ShouldBeSameAs(canceled);
    }

    [Fact]
    public void Traverse_keeps_task_payload_without_awaiting() {
        var faulted = Task.FromException<int>(new ProbeException("task"));

        var result = Maybe<int>.Some(1).Traverse(_ => Result<Task<int>, string>.Ok(faulted));

        (result.TryGetValue(out var maybe, out _) && maybe.TryGetValue(out var task) && ReferenceEquals(faulted, task)).ShouldBeTrue();
    }

    [Fact]
    public void Nested_recipes_use_public_api_without_extra_operations() {
        var flattenedMaybe = Maybe<Maybe<int>>.Some(Maybe<int>.Some(1)).AndThen(static inner => inner);
        var flattenedResult = Result<Result<int, string>, string>.Ok(Result<int, string>.Error("e")).AndThen(static inner => inner);
        var swapped = Result<Maybe<int>, string>.Error("e").Match(
            static maybe => maybe.Map(Result<int, string>.Ok),
            static error => Maybe<Result<int, string>>.Some(Result<int, string>.Error(error)));

        flattenedMaybe.ShouldBe(Maybe<int>.Some(1));
        flattenedResult.ShouldBe(Result<int, string>.Error("e"));
        swapped.ShouldBe(Maybe<Result<int, string>>.Some(Result<int, string>.Error("e")));
    }
}
