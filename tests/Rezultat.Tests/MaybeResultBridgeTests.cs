namespace Rezultat.Tests;

public sealed class MaybeResultBridgeTests
{
    [Fact]
    public void ToResult_maps_some_to_ok_and_none_to_the_given_error()
    {
        Assert.Equal(Result<int, string>.Ok(1), Maybe<int>.Some(1).ToResult("missing"));
        Assert.Equal(Result<int, string>.Error("missing"), Maybe<int>.None.ToResult("missing"));
        Assert.Equal(Result<int, string>.Error("missing"), default(Maybe<int>).ToResult("missing"));
    }

    [Fact]
    public void ToResult_keeps_present_null_and_null_error()
    {
        Assert.Equal(Result<string?, string>.Ok(null), Maybe<string?>.Some(null).ToResult("missing"));
        Assert.Equal(Result<int?, string>.Ok(null), Maybe<int?>.Some(null).ToResult("missing"));
        Assert.Equal(Result<int, string?>.Error(null), Maybe<int>.None.ToResult<string?>(null));
    }

    [Fact]
    public void ToResult_keeps_a_delegate_error_as_payload_without_invoking_it()
    {
        var calls = 0;
        Func<string> error = () => $"{++calls}";

        var result = Maybe<int>.None.ToResult(error);

        Assert.False(result.TryGetValue(out _, out var observed));
        Assert.Same(error, observed);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void ToResult_error_argument_is_evaluated_even_for_some()
    {
        var evaluations = 0;

        Assert.Equal(Result<int, int>.Ok(1), Maybe<int>.Some(1).ToResult(++evaluations));
        Assert.Equal(1, evaluations);
    }

    [Fact]
    public void ToResultElse_invokes_factory_once_only_for_none()
    {
        var calls = 0;

        Assert.Equal(Result<int, string>.Ok(1), Maybe<int>.Some(1).ToResultElse(() => $"{++calls}"));
        Assert.Equal(0, calls);
        Assert.Equal(Result<int, string>.Error("1"), Maybe<int>.None.ToResultElse(() => $"{++calls}"));
        Assert.Equal(1, calls);
        Assert.Equal(Result<int, string?>.Error(null), Maybe<int>.None.ToResultElse(static () => (string?)null));
    }

    [Fact]
    public void ToResultElse_rejects_null_factory_even_for_some_and_propagates_its_exceptions()
    {
        var canceled = new OperationCanceledException();

        Assert.Equal("errorFactory", Assert.Throws<ArgumentNullException>(() => Maybe<int>.Some(1).ToResultElse<string>(null!)).ParamName);
        Assert.Equal("errorFactory", Assert.Throws<ArgumentNullException>(() => Maybe<int>.None.ToResultElse<string>(null!)).ParamName);
        Assert.Same(canceled, Assert.Throws<OperationCanceledException>(() => Maybe<int>.None.ToResultElse<string>(() => throw canceled)));
    }

    [Fact]
    public void Bridges_keep_nested_carriers_and_reference_identity()
    {
        var probe = new ReferenceProbe(1);
        var error = new ReferenceProbe(2);

        Assert.True(Maybe<Maybe<int>>.Some(Maybe<int>.None).ToResult("e").TryGetValue(out var nested, out _) && !nested.IsSome);
        Assert.True(Maybe<Result<int, string>>.Some(default).ToResult("e").TryGetValue(out var inner, out _) && !inner.IsInitialized);
        Assert.True(Maybe<ReferenceProbe>.Some(probe).ToResult("e").TryGetValue(out var value, out _) && ReferenceEquals(probe, value));
        Assert.False(Maybe<int>.None.ToResult(error).TryGetValue(out _, out var observedError));
        Assert.Same(error, observedError);
        Assert.True(Result<ReferenceProbe, string>.Ok(probe).ToMaybe().TryGetValue(out var back) && ReferenceEquals(probe, back));
        Assert.Equal(Maybe<Result<int, string>>.Some(Result<int, string>.Error("inner")), Result<Result<int, string>, int>.Ok(Result<int, string>.Error("inner")).ToMaybe());
    }

    [Fact]
    public void ToMaybe_maps_ok_to_some_and_error_to_none_losing_the_reason()
    {
        var error = new ReferenceProbe(1);

        Assert.Equal(Maybe<int>.Some(1), Result<int, string>.Ok(1).ToMaybe());
        Assert.Equal(Maybe<string?>.Some(null), Result<string?, int>.Ok(null).ToMaybe());
        Assert.Equal(Maybe<int>.None, Result<int, ReferenceProbe>.Error(error).ToMaybe());
        Assert.Equal(Maybe<int>.None, Result<int, string?>.Error(null).ToMaybe());
        Assert.Equal((0, 0, 0), (error.EqualsCalls, error.HashCalls, error.ToStringCalls));
    }

    [Fact]
    public void ToMaybe_rejects_uninitialized_result()
    {
        Assert.Throws<InvalidOperationException>(() => default(Result<int, string>).ToMaybe());
    }

    [Fact]
    public void Round_trips_hold_in_their_own_domain()
    {
        Assert.Equal(Maybe<string?>.Some(null), Maybe<string?>.Some(null).ToResult("e").ToMaybe());
        Assert.Equal(Maybe<int>.None, Maybe<int>.None.ToResult("e").ToMaybe());
        Assert.Equal(Result<int, string>.Ok(1), Result<int, string>.Ok(1).ToMaybe().ToResult("replacement"));
        Assert.Equal(Result<int, string>.Error("replacement"), Result<int, string>.Error("original").ToMaybe().ToResult("replacement"));
    }

    [Fact]
    public void Traverse_maps_none_to_ok_none_without_selector()
    {
        var calls = 0;

        Assert.Equal(Result<Maybe<int>, string>.Ok(Maybe<int>.None), Maybe<int>.None.Traverse(_ => { calls++; return Result<int, string>.Ok(1); }));
        Assert.Equal(Result<Maybe<int>, string>.Ok(Maybe<int>.None), default(Maybe<int>).Traverse(_ => { calls++; return default(Result<int, string>); }));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void Traverse_maps_some_through_selector_once()
    {
        var calls = 0;
        var error = new ReferenceProbe(1);

        Assert.Equal(Result<Maybe<string>, string>.Ok(Maybe<string>.Some("2")), Maybe<int>.Some(1).Traverse(value => { calls++; return Result<string, string>.Ok($"{value + 1}"); }));
        Assert.False(Maybe<int>.Some(1).Traverse(_ => { calls++; return Result<int, ReferenceProbe>.Error(error); }).TryGetValue(out _, out var observed));
        Assert.Same(error, observed);
        Assert.Equal(2, calls);
        Assert.Equal((0, 0, 0), (error.EqualsCalls, error.HashCalls, error.ToStringCalls));
    }

    [Fact]
    public void Traverse_keeps_present_null_null_ok_and_null_error()
    {
        string? observed = "unset";

        var ok = Maybe<string?>.Some(null).Traverse(value => { observed = value; return Result<string?, int>.Ok(null); });

        Assert.Null(observed);
        Assert.Equal(Result<Maybe<string?>, int>.Ok(Maybe<string?>.Some(null)), ok);
        Assert.Equal(Result<Maybe<int>, string?>.Error(null), Maybe<int>.Some(1).Traverse(static _ => Result<int, string?>.Error(null)));
        Assert.Equal(Result<Maybe<int?>, int>.Ok(Maybe<int?>.Some(null)), Maybe<int?>.Some(null).Traverse(static value => Result<int?, int>.Ok(value)));
    }

    [Fact]
    public void Traverse_rejects_uninitialized_selector_result_but_not_nested_payload()
    {
        var nested = Maybe<int>.Some(1).Traverse(static _ => Result<Result<int, string>, string>.Ok(default));

        Assert.Throws<InvalidOperationException>(() => Maybe<int>.Some(1).Traverse(static _ => default(Result<int, string>)));
        Assert.True(nested.TryGetValue(out var maybe, out _) && maybe.TryGetValue(out var inner) && !inner.IsInitialized);
    }

    [Fact]
    public void Traverse_with_identity_selector_turns_ready_maybe_of_result_inside_out()
    {
        Assert.Equal(Result<Maybe<int>, string>.Ok(Maybe<int>.Some(1)), Maybe<Result<int, string>>.Some(Result<int, string>.Ok(1)).Traverse(static r => r));
        Assert.Equal(Result<Maybe<int>, string>.Error("e"), Maybe<Result<int, string>>.Some(Result<int, string>.Error("e")).Traverse(static r => r));
        Assert.Equal(Result<Maybe<int>, string>.Ok(Maybe<int>.None), Maybe<Result<int, string>>.None.Traverse(static r => r));
        Assert.Throws<InvalidOperationException>(() => Maybe<Result<int, string>>.Some(default).Traverse(static r => r));
    }

    [Fact]
    public void Traverse_rejects_null_selector_even_for_none_and_propagates_its_exceptions()
    {
        var canceled = new OperationCanceledException();

        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => Maybe<int>.None.Traverse<int, int, string>(null!)).ParamName);
        Assert.Equal("selector", Assert.Throws<ArgumentNullException>(() => Maybe<int>.Some(1).Traverse<int, int, string>(null!)).ParamName);
        Assert.Same(canceled, Assert.Throws<OperationCanceledException>(() => Maybe<int>.Some(1).Traverse<int, int, string>(_ => throw canceled)));
    }

    [Fact]
    public void Traverse_keeps_task_payload_without_awaiting()
    {
        var faulted = Task.FromException<int>(new ProbeException("task"));

        var result = Maybe<int>.Some(1).Traverse(_ => Result<Task<int>, string>.Ok(faulted));

        Assert.True(result.TryGetValue(out var maybe, out _) && maybe.TryGetValue(out var task) && ReferenceEquals(faulted, task));
    }

    [Fact]
    public void Nested_recipes_use_public_api_without_extra_operations()
    {
        var flattenedMaybe = Maybe<Maybe<int>>.Some(Maybe<int>.Some(1)).AndThen(static inner => inner);
        var flattenedResult = Result<Result<int, string>, string>.Ok(Result<int, string>.Error("e")).AndThen(static inner => inner);
        var swapped = Result<Maybe<int>, string>.Error("e").Match(
            static maybe => maybe.Map(Result<int, string>.Ok),
            static error => Maybe<Result<int, string>>.Some(Result<int, string>.Error(error)));

        Assert.Equal(Maybe<int>.Some(1), flattenedMaybe);
        Assert.Equal(Result<int, string>.Error("e"), flattenedResult);
        Assert.Equal(Maybe<Result<int, string>>.Some(Result<int, string>.Error("e")), swapped);
    }
}
