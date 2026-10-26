using System.Collections.Immutable;
using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Rezultat.Collections;

namespace Rezultat.Tests;

public static class ValidationValues {
    public static Arbitrary<Validation<int, int>> Initialized() =>
        Gen.OneOf(
                Gen.Choose(-3, 3).Select(Validation<int, int>.Valid),
                Gen.NonEmptyListOf(Gen.Choose(-3, 3)).Select(issues => Validation<int, int>.InvalidMany([.. issues])))
            .ToArbitrary();

    public static Arbitrary<Func<int, Validation<int, int>>> Steps() =>
        (from isValid in ArbMap.Default.GeneratorFor<Func<int, bool>>()
         from value in ArbMap.Default.GeneratorFor<Func<int, int>>()
         from issue in ArbMap.Default.GeneratorFor<Func<int, int>>()
         select (Func<int, Validation<int, int>>)(input =>
             isValid(input) ? Validation<int, int>.Valid(value(input)) : Validation<int, int>.InvalidMany([issue(input), issue(input + 1)])))
            .ToArbitrary();

    public static Arbitrary<Func<ImmutableArray<int>, Validation<int, int>>> Fallbacks() =>
        (from isValid in ArbMap.Default.GeneratorFor<Func<int, bool>>()
         from value in ArbMap.Default.GeneratorFor<Func<int, int>>()
         select (Func<ImmutableArray<int>, Validation<int, int>>)(issues =>
             isValid(issues.Aggregate(0, static (sum, issue) => unchecked(sum + issue))) ? Validation<int, int>.Valid(value(issues.Length)) : Validation<int, int>.InvalidMany([.. issues.Reverse(), value(issues[0])])))
            .ToArbitrary();
}

[Properties(Arbitrary = [typeof(ValidationValues)])]
public sealed class ValidationPropertyTests {
    [Property]
    public bool Equality_forms_agree_and_are_symmetric(Validation<int, int> left, Validation<int, int> right) =>
        left.Equals(right) == (left == right)
        && left != right == !(left == right)
        && left.Equals((object)right) == left.Equals(right)
        && left.Equals(right) == right.Equals(left)
        && (!left.Equals(right) || left.GetHashCode() == right.GetHashCode());

    [Property]
    public bool Copied_issue_buffer_is_equal_with_equal_hash(Validation<int, int> validation) {
        var copy = validation.TryGetValue(out _, out var issues)
            ? validation
            : Validation<int, int>.InvalidMany([.. issues.ToArray()]);

        return copy == validation && copy.GetHashCode() == validation.GetHashCode();
    }

    [Property]
    public bool MapIssues_preserves_identity(Validation<int, int> validation) =>
        validation.MapIssues(static issue => issue) == validation;

    [Property]
    public bool MapIssues_preserves_composition(Validation<int, int> validation, Func<int, int> f, Func<int, int> g) =>
        validation.MapIssues(f).MapIssues(g) == validation.MapIssues(issue => g(f(issue)));

    [Property]
    public bool MapIssues_distributes_over_Zip(Validation<int, int> a, Validation<int, int> b, Func<int, int> f) =>
        a.MapIssues(f).Zip(b.MapIssues(f), static (x, y) => (x, y)) == a.Zip(b, static (x, y) => (x, y)).MapIssues(f);

    [Property]
    public bool Zip_regrouping_keeps_values_and_issue_order(Validation<int, int> a, Validation<int, int> b, Validation<int, int> c) =>
        a.Zip(b, static (x, y) => (x, y)).Zip(c, static (xy, z) => (xy.x, xy.y, z))
        == a.Zip(b.Zip(c, static (y, z) => (y, z)), static (x, yz) => (x, yz.y, yz.z));

    [Property]
    public bool Direct_arities_equal_binary_composition(Validation<int, int> a, Validation<int, int> b, Validation<int, int> c, Validation<int, int> d) =>
        a.Zip(b, c, static (x, y, z) => (x, y, z))
            == a.Zip(b, static (x, y) => (x, y)).Zip(c, static (xy, z) => (xy.x, xy.y, z))
        && a.Zip(b, c, d, static (w, x, y, z) => (w, x, y, z))
            == a.Zip(b, static (w, x) => (w, x)).Zip(c, static (wx, y) => (wx.w, wx.x, y)).Zip(d, static (wxy, z) => (wxy.w, wxy.x, wxy.y, z));

    [Property]
    public bool AndThen_has_left_identity(int value, Func<int, Validation<int, int>> next) =>
        Validation<int, int>.Valid(value).AndThen(next) == next(value);

    [Property]
    public bool AndThen_has_right_identity(Validation<int, int> validation) =>
        validation.AndThen(Validation<int, int>.Valid) == validation;

    [Property]
    public bool AndThen_is_associative(Validation<int, int> validation, Func<int, Validation<int, int>> f, Func<int, Validation<int, int>> g) =>
        validation.AndThen(f).AndThen(g) == validation.AndThen(value => f(value).AndThen(g));

    [Property]
    public bool Map_preserves_identity(Validation<int, int> validation) =>
        validation.Map(static value => value) == validation;

    [Property]
    public bool Map_preserves_composition(Validation<int, int> validation, Func<int, int> f, Func<int, int> g) =>
        validation.Map(f).Map(g) == validation.Map(value => g(f(value)));

    [Property]
    public bool Map_equals_AndThen_of_valid(Validation<int, int> validation, Func<int, int> f) =>
        validation.Map(f) == validation.AndThen(value => Validation<int, int>.Valid(f(value)));

    [Property]
    public bool Ensure_equals_AndThen_of_the_dependent_check(Validation<int, int> validation, Func<int, bool> p, Func<int, int> e) =>
        validation.Ensure(p, e) == validation.AndThen(value => p(value) ? Validation<int, int>.Valid(value) : Validation<int, int>.Invalid(e(value)));

    [Property]
    public bool ToResult_keeps_branch_value_and_issue_positions(Validation<int, int> validation) =>
        validation.ToResult().Match(
            value => validation == Validation<int, int>.Valid(value),
            issues => validation == Validation<int, int>.InvalidMany(issues));

    [Property]
    public bool ToValidation_after_ToResult_of_valid_is_identity(int value) =>
        Validation<int, int>.Valid(value).ToResult().ToValidation() == Validation<int, ImmutableArray<int>>.Valid(value);

    [Property]
    public bool OrElse_on_invalid_equals_fallback(NonEmptyArray<int> issues, Func<ImmutableArray<int>, Validation<int, int>> fallback) {
        ImmutableArray<int> input = [.. issues.Get];

        return Validation<int, int>.InvalidMany(input).OrElse(fallback) == fallback(input);
    }

    [Property]
    public bool OrElse_with_InvalidMany_is_identity(Validation<int, int> validation) =>
        validation.OrElse(Validation<int, int>.InvalidMany) == validation;

    [Property]
    public bool OrElse_is_associative(
        Validation<int, int> validation, Func<ImmutableArray<int>, Validation<int, int>> f, Func<ImmutableArray<int>, Validation<int, int>> g) =>
        validation.OrElse(f).OrElse(g) == validation.OrElse(issues => f(issues).OrElse(g));

    [Property]
    public bool Traverse_singleton_wraps_the_outcome(int item, Func<int, Validation<int, int>> step) =>
        Normalize(new[] { item }.Traverse(step)) == Normalize(step(item).Match(
            static value => Validation<int[], int>.Valid([value]),
            Validation<int[], int>.InvalidMany));

    [Property]
    public bool Traverse_of_concatenation_equals_Zip_of_parts(int[] left, int[] right, Func<int, Validation<int, int>> step) =>
        Normalize(left.Concat(right).Traverse(step))
        == Normalize(left.Traverse(step).Zip(right.Traverse(step), static (x, y) => x.Concat(y).ToArray()));

    [Theory]
    [InlineData(int.MaxValue, 1)]
    [InlineData(int.MinValue, -1)]
    public void Generated_fallbacks_are_total_and_lawful_at_overflow_boundaries(int first, int second) {
        ImmutableArray<int> issues = [first, second];
        var fallbacks = ValidationValues.Fallbacks().Generator.Sample(100);

        var outcomes = fallbacks.Select(fallback => fallback(issues)).ToArray();

        foreach (var outcome in outcomes) {
            outcome.IsInitialized.ShouldBeTrue();
        }
        outcomes.ShouldContain(outcome => outcome.IsValid);
        outcomes.ShouldContain(outcome => !outcome.IsValid);
        foreach (var fallback in fallbacks) {
            Validation<int, int>.InvalidMany(issues).OrElse(fallback).ShouldBe(fallback(issues));
        }
        foreach (var pair in fallbacks.Zip(fallbacks.Reverse())) {
            var (f, g) = pair;
            var invalid = Validation<int, int>.InvalidMany(issues);
            invalid.OrElse(xs => f(xs).OrElse(g)).ShouldBe(invalid.OrElse(f).OrElse(g));
        }
    }

    private static Validation<string, int> Normalize(Validation<int[], int> validation) =>
        validation.Match(static values => Validation<string, int>.Valid(string.Join(",", values)), Validation<string, int>.InvalidMany);
}
