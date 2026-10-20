using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Rezultat.Tests;

public static class MaybeValues
{
    public static Arbitrary<Maybe<string?>> NullableStrings() =>
        Gen.OneOf(
                Gen.Constant(Maybe<string?>.None),
                ArbMap.Default.GeneratorFor<string?>().Select(Maybe<string?>.Some))
            .ToArbitrary();

    public static Arbitrary<Maybe<int>> SmallInts() =>
        Gen.OneOf(
                Gen.Constant(Maybe<int>.None),
                Gen.Choose(-3, 3).Select(Maybe<int>.Some))
            .ToArbitrary();
}

[Properties(Arbitrary = [typeof(MaybeValues)])]
public sealed class MaybePropertyTests
{
    [Property]
    public bool Equality_is_reflexive(Maybe<string?> maybe)
    {
        var copy = maybe;

        return maybe.Equals(copy) && maybe == copy && !(maybe != copy) && maybe.GetHashCode() == copy.GetHashCode();
    }

    [Property]
    public bool Equality_forms_agree_and_are_symmetric(Maybe<string?> left, Maybe<string?> right) =>
        left.Equals(right) == (left == right)
        && (left != right) == !(left == right)
        && left.Equals((object)right) == left.Equals(right)
        && EqualityComparer<Maybe<string?>>.Default.Equals(left, right) == left.Equals(right)
        && left.Equals(right) == right.Equals(left);

    [Property]
    public bool Equal_values_have_equal_hashes(Maybe<int> left, Maybe<int> right) =>
        !left.Equals(right) || left.GetHashCode() == right.GetHashCode();

    [Property]
    public bool Values_are_equal_exactly_for_same_alternative_and_payload(Maybe<int> left, Maybe<int> right)
    {
        var leftSome = left.TryGetValue(out var leftValue);
        var rightSome = right.TryGetValue(out var rightValue);

        return left.Equals(right) == (leftSome == rightSome && (!leftSome || leftValue == rightValue));
    }

    [Property]
    public bool Some_round_trips_through_observation(string? value)
    {
        var maybe = Maybe<string?>.Some(value);

        return maybe.IsSome
            && maybe.TryGetValue(out var observed) && observed == value
            && maybe.ValueOr("fallback") == value
            && maybe.Match(v => v == value, () => false);
    }

    [Property]
    public bool Observations_agree_on_alternative(Maybe<int> maybe, int fallback)
    {
        var some = maybe.TryGetValue(out var value);

        return some == maybe.IsSome
            && maybe.ValueOr(fallback) == (some ? value : fallback)
            && maybe.ValueOrElse(() => fallback) == maybe.ValueOr(fallback)
            && maybe.Match(v => v, () => fallback) == maybe.ValueOr(fallback);
    }

    [Property]
    public bool FromNullable_matches_presence_of_reference(string? value) =>
        Maybe.FromNullable(value) == (value is null ? Maybe<string>.None : Maybe<string>.Some(value));

    [Property]
    public bool FromNullable_matches_presence_of_value(int? value) =>
        Maybe.FromNullable(value) == (value.HasValue ? Maybe<int>.Some(value.Value) : Maybe<int>.None);
}
