using System.Collections.Immutable;
using System.Runtime.InteropServices;

namespace Rezultat.Tests;

public sealed class CarrierBridgeTests {
    [Fact]
    public void ToValidation_turns_ok_into_valid_with_the_exact_payload() {
        var payload = new ReferenceProbe(1);

        var validation = Result<ReferenceProbe, string>.Ok(payload).ToValidation();

        validation.TryGetValue(out var value, out _).ShouldBeTrue();
        value.ShouldBeSameAs(payload);
        (payload.EqualsCalls + payload.HashCalls + payload.ToStringCalls).ShouldBe(0);
    }

    [Fact]
    public void ToValidation_turns_error_into_exactly_one_issue() {
        var error = new ReferenceProbe(2);

        var validation = Result<int, ReferenceProbe>.Error(error).ToValidation();

        validation.TryGetValue(out _, out var issues).ShouldBeFalse();
        issues.ShouldHaveSingleItem().ShouldBeSameAs(error);
        (error.EqualsCalls + error.HashCalls + error.ToStringCalls).ShouldBe(0);
    }

    [Fact]
    public void ToValidation_keeps_null_and_default_payloads() {
        Result<string?, int>.Ok(null).ToValidation().ShouldBe(Validation<string?, int>.Valid(null));
        Result<int, string?>.Error(null).ToValidation().ShouldBe(Validation<int, string?>.Invalid(null));
        Result<int, int>.Error(0).ToValidation().ShouldBe(Validation<int, int>.Invalid(0));
    }

    [Fact]
    public void ToValidation_keeps_a_collection_error_as_one_atomic_issue() {
        string[] error = ["a", "b"];
        ImmutableArray<string> emptyError = [];

        var validation = Result<int, string[]>.Error(error).ToValidation();
        var empty = Result<int, ImmutableArray<string>>.Error(emptyError).ToValidation();

        validation.TryGetValue(out _, out var issues).ShouldBeFalse();
        issues.ShouldHaveSingleItem().ShouldBeSameAs(error);
        empty.TryGetValue(out _, out var emptyIssues).ShouldBeFalse();
        emptyIssues.ShouldHaveSingleItem().IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public void ToValidation_rejects_uninitialized_result() {
        Should.Throw<InvalidOperationException>(() => default(Result<int, string>).ToValidation()).ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void ToResult_turns_valid_into_ok_with_the_exact_payload() {
        var payload = new ReferenceProbe(3);

        var result = Validation<ReferenceProbe, string>.Valid(payload).ToResult();

        result.TryGetValue(out var value, out _).ShouldBeTrue();
        value.ShouldBeSameAs(payload);
    }

    [Fact]
    public void ToResult_keeps_the_exact_issue_buffer_with_nulls_and_duplicates() {
        ImmutableArray<string?> issues = ["b", null, "a", "b"];

        var result = Validation<int, string?>.InvalidMany(issues).ToResult();

        result.TryGetValue(out _, out var error).ShouldBeFalse();
        ImmutableCollectionsMarshal.AsArray(error).ShouldBeSameAs(ImmutableCollectionsMarshal.AsArray(issues));
        error.ShouldBe(["b", null, "a", "b"]);
    }

    [Fact]
    public void ToResult_keeps_a_singleton_issue_as_a_one_element_array() {
        var result = Validation<int, string?>.Invalid(null).ToResult();

        result.TryGetValue(out _, out var error).ShouldBeFalse();
        error.ShouldHaveSingleItem().ShouldBeNull();
    }

    [Fact]
    public void ToResult_rejects_uninitialized_validation() {
        Should.Throw<InvalidOperationException>(() => default(Validation<int, string>).ToResult()).ShouldBeOfType<InvalidOperationException>();
    }

    [Fact]
    public void ToResult_does_not_give_result_the_positional_equality_of_validation() {
        var left = Validation<int, string>.InvalidMany(["a"]);
        var right = Validation<int, string>.InvalidMany(["a"]);

        right.ShouldBe(left);
        right.ToResult().ShouldNotBe(left.ToResult());
        left.ToResult().ShouldBe(left.ToResult());
    }

    [Fact]
    public void ToResult_then_ToValidation_wraps_the_whole_array_as_one_issue() {
        ImmutableArray<string> issues = ["a", "b"];

        var roundTrip = Validation<int, string>.InvalidMany(issues).ToResult().ToValidation();

        roundTrip.TryGetValue(out _, out var wrapped).ShouldBeFalse();
        ImmutableCollectionsMarshal.AsArray(wrapped.ShouldHaveSingleItem()).ShouldBeSameAs(ImmutableCollectionsMarshal.AsArray(issues));
    }

    [Fact]
    public void Nested_default_carriers_and_tasks_stay_payloads() {
        var nestedResult = Result<Result<int, string>, string>.Ok(default).ToValidation();
        var nestedValidation = Validation<int, Validation<int, string>>.Invalid(default).ToResult();
        var task = Result<Task<int>, string>.Ok(Task.FromException<int>(new ProbeException("task"))).ToValidation();

        (nestedResult.TryGetValue(out var inner, out _) && !inner.IsInitialized).ShouldBeTrue();
        nestedValidation.TryGetValue(out _, out var error).ShouldBeFalse();
        error.ShouldHaveSingleItem().IsInitialized.ShouldBeFalse();
        (task.TryGetValue(out var pending, out _) && pending.IsFaulted).ShouldBeTrue();
    }
}
