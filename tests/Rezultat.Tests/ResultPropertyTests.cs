using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Rezultat.Tests;

public static class InitializedResults
{
    public static Arbitrary<Result<int, string?>> IntOrNullableString() =>
        Gen.OneOf(
                ArbMap.Default.GeneratorFor<int>().Select(Result<int, string?>.Ok),
                ArbMap.Default.GeneratorFor<string?>().Select(Result<int, string?>.Error))
            .ToArbitrary();

    public static Arbitrary<Result<int, int>> SameTypeInts() =>
        Gen.OneOf(
                Gen.Choose(-3, 3).Select(Result<int, int>.Ok),
                Gen.Choose(-3, 3).Select(Result<int, int>.Error))
            .ToArbitrary();
}

[Properties(Arbitrary = [typeof(InitializedResults)])]
public sealed class ResultPropertyTests
{
    [Property]
    public bool Equality_is_reflexive(Result<int, string?> result)
    {
        var copy = result;

        return result.Equals(copy) && result == copy && !(result != copy) && result.GetHashCode() == copy.GetHashCode();
    }

    [Property]
    public bool Equality_operators_and_object_equals_agree(Result<int, string?> left, Result<int, string?> right) =>
        left.Equals(right) == (left == right)
        && (left != right) == !(left == right)
        && left.Equals((object)right) == left.Equals(right)
        && left.Equals(right) == right.Equals(left);

    [Property]
    public bool Equal_results_have_equal_hashes(Result<int, int> left, Result<int, int> right) =>
        !left.Equals(right) || left.GetHashCode() == right.GetHashCode();

    [Property]
    public bool Same_type_results_are_equal_exactly_for_same_branch_and_payload(Result<int, int> left, Result<int, int> right)
    {
        var leftSuccess = left.TryGetValue(out var leftValue, out var leftError);
        var rightSuccess = right.TryGetValue(out var rightValue, out var rightError);
        var expected = leftSuccess == rightSuccess && (leftSuccess ? leftValue == rightValue : leftError == rightError);

        return left.Equals(right) == expected;
    }

    [Property]
    public bool Ok_round_trips_through_observation(string? value)
    {
        var result = Result<string?, int>.Ok(value);

        return result.IsInitialized
            && result.IsSuccess
            && result.TryGetValue(out var observed, out var error)
            && observed == value
            && error == 0
            && result.Match(v => v == value, _ => false);
    }

    [Property]
    public bool Error_round_trips_through_observation(string? error)
    {
        var result = Result<int, string?>.Error(error);

        return result.IsInitialized
            && !result.IsSuccess
            && !result.TryGetValue(out var value, out var observed)
            && observed == error
            && value == 0
            && result.Match(_ => false, e => e == error);
    }

    [Property]
    public bool Initialized_result_differs_from_uninitialized(Result<int, string?> result) =>
        result != default && !default(Result<int, string?>).Equals(result);
}
