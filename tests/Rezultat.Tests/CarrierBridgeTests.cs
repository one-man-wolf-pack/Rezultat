using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Rezultat.Tests;

public sealed class CarrierBridgeTests
{
    [Fact]
    public void ToValidation_turns_ok_into_valid_with_the_exact_payload()
    {
        var payload = new ReferenceProbe(1);

        var validation = Result<ReferenceProbe, string>.Ok(payload).ToValidation();

        Assert.True(validation.TryGetValue(out var value, out _));
        Assert.Same(payload, value);
        Assert.Equal(0, payload.EqualsCalls + payload.HashCalls + payload.ToStringCalls);
    }

    [Fact]
    public void ToValidation_turns_error_into_exactly_one_issue()
    {
        var error = new ReferenceProbe(2);

        var validation = Result<int, ReferenceProbe>.Error(error).ToValidation();

        Assert.False(validation.TryGetValue(out _, out var issues));
        Assert.Same(error, Assert.Single(issues));
        Assert.Equal(0, error.EqualsCalls + error.HashCalls + error.ToStringCalls);
    }

    [Fact]
    public void ToValidation_keeps_null_and_default_payloads()
    {
        Assert.Equal(Validation<string?, int>.Valid(null), Result<string?, int>.Ok(null).ToValidation());
        Assert.Equal(Validation<int, string?>.Invalid(null), Result<int, string?>.Error(null).ToValidation());
        Assert.Equal(Validation<int, int>.Invalid(0), Result<int, int>.Error(0).ToValidation());
    }

    [Fact]
    public void ToValidation_keeps_a_collection_error_as_one_atomic_issue()
    {
        string[] error = ["a", "b"];
        ImmutableArray<string> emptyError = [];

        var validation = Result<int, string[]>.Error(error).ToValidation();
        var empty = Result<int, ImmutableArray<string>>.Error(emptyError).ToValidation();

        Assert.False(validation.TryGetValue(out _, out var issues));
        Assert.Same(error, Assert.Single(issues));
        Assert.False(empty.TryGetValue(out _, out var emptyIssues));
        Assert.True(Assert.Single(emptyIssues).IsEmpty);
    }

    [Fact]
    public void ToValidation_rejects_uninitialized_result()
    {
        Assert.Throws<InvalidOperationException>(() => default(Result<int, string>).ToValidation());
    }

    [Fact]
    public void ToResult_turns_valid_into_ok_with_the_exact_payload()
    {
        var payload = new ReferenceProbe(3);

        var result = Validation<ReferenceProbe, string>.Valid(payload).ToResult();

        Assert.True(result.TryGetValue(out var value, out _));
        Assert.Same(payload, value);
    }

    [Fact]
    public void ToResult_keeps_the_exact_issue_buffer_with_nulls_and_duplicates()
    {
        ImmutableArray<string?> issues = ["b", null, "a", "b"];

        var result = Validation<int, string?>.InvalidMany(issues).ToResult();

        Assert.False(result.TryGetValue(out _, out var error));
        Assert.Same(ImmutableCollectionsMarshal.AsArray(issues), ImmutableCollectionsMarshal.AsArray(error));
        Assert.Equal(["b", null, "a", "b"], error);
    }

    [Fact]
    public void ToResult_keeps_a_singleton_issue_as_a_one_element_array()
    {
        var result = Validation<int, string?>.Invalid(null).ToResult();

        Assert.False(result.TryGetValue(out _, out var error));
        Assert.Null(Assert.Single(error));
    }

    [Fact]
    public void ToResult_rejects_uninitialized_validation()
    {
        Assert.Throws<InvalidOperationException>(() => default(Validation<int, string>).ToResult());
    }

    [Fact]
    public void ToResult_does_not_give_result_the_positional_equality_of_validation()
    {
        var left = Validation<int, string>.InvalidMany(["a"]);
        var right = Validation<int, string>.InvalidMany(["a"]);

        Assert.Equal(left, right);
        Assert.NotEqual(left.ToResult(), right.ToResult());
        Assert.Equal(left.ToResult(), left.ToResult());
    }

    [Fact]
    public void ToResult_then_ToValidation_wraps_the_whole_array_as_one_issue()
    {
        ImmutableArray<string> issues = ["a", "b"];

        var roundTrip = Validation<int, string>.InvalidMany(issues).ToResult().ToValidation();

        Assert.False(roundTrip.TryGetValue(out _, out var wrapped));
        Assert.Same(ImmutableCollectionsMarshal.AsArray(issues), ImmutableCollectionsMarshal.AsArray(Assert.Single(wrapped)));
    }

    [Fact]
    public void Nested_default_carriers_and_tasks_stay_payloads()
    {
        var nestedResult = Result<Result<int, string>, string>.Ok(default).ToValidation();
        var nestedValidation = Validation<int, Validation<int, string>>.Invalid(default).ToResult();
        var task = Result<Task<int>, string>.Ok(Task.FromException<int>(new ProbeException("task"))).ToValidation();

        Assert.True(nestedResult.TryGetValue(out var inner, out _) && !inner.IsInitialized);
        Assert.False(nestedValidation.TryGetValue(out _, out var error));
        Assert.False(Assert.Single(error).IsInitialized);
        Assert.True(task.TryGetValue(out var pending, out _) && pending.IsFaulted);
    }
}
