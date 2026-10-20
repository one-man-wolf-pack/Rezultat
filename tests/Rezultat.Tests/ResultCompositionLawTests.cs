using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;

namespace Rezultat.Tests;

public static class CompositionArbitraries
{
    public static Arbitrary<Func<int, Result<int, int>>> Steps() =>
        (from isSuccess in ArbMap.Default.GeneratorFor<Func<int, bool>>()
         from value in ArbMap.Default.GeneratorFor<Func<int, int>>()
         from error in ArbMap.Default.GeneratorFor<Func<int, int>>()
         select (Func<int, Result<int, int>>)(input =>
             isSuccess(input) ? Result<int, int>.Ok(value(input)) : Result<int, int>.Error(error(input))))
            .ToArbitrary();
}

[Properties(Arbitrary = [typeof(InitializedResults), typeof(CompositionArbitraries)])]
public sealed class ResultCompositionLawTests
{
    [Property]
    public bool Map_preserves_identity(Result<int, string?> result) => result.Map(static value => value) == result;

    [Property]
    public bool Map_preserves_composition(Result<int, string?> result, Func<int, int> f, Func<int, int> g) =>
        result.Map(f).Map(g) == result.Map(value => g(f(value)));

    [Property]
    public bool MapError_preserves_identity(Result<int, string?> result) => result.MapError(static error => error) == result;

    [Property]
    public bool MapError_preserves_composition(Result<int, string?> result, Func<string?, int> f, Func<int, int> g) =>
        result.MapError(f).MapError(g) == result.MapError(error => g(f(error)));

    [Property]
    public bool Contextual_MapError_equals_capturing_MapError(Result<int, string?> result, int state, Func<string?, int, int> f) =>
        result.MapError(state, f) == result.MapError(error => f(error, state));

    [Property]
    public bool Map_and_MapError_commute(Result<int, string?> result, Func<int, int> f, Func<string?, int> g) =>
        result.Map(f).MapError(g) == result.MapError(g).Map(f);

    [Property]
    public bool Map_equals_AndThen_with_Ok(Result<int, int> result, Func<int, int> f) =>
        result.Map(f) == result.AndThen(value => Result<int, int>.Ok(f(value)));

    [Property]
    public bool AndThen_has_left_identity(int value, Func<int, Result<int, int>> next) =>
        Result<int, int>.Ok(value).AndThen(next) == next(value);

    [Property]
    public bool AndThen_has_right_identity(Result<int, int> result) => result.AndThen(Result<int, int>.Ok) == result;

    [Property]
    public bool AndThen_is_associative(Result<int, int> result, Func<int, Result<int, int>> f, Func<int, Result<int, int>> g) =>
        result.AndThen(f).AndThen(g) == result.AndThen(value => f(value).AndThen(g));

    [Property]
    public bool Ensure_equals_conditional_AndThen(Result<int, int> result, Func<int, bool> p, Func<int, int> e) =>
        result.Ensure(p, e) == result.AndThen(value => p(value) ? Result<int, int>.Ok(value) : Result<int, int>.Error(e(value)));

    [Property]
    public bool Ensure_with_true_predicate_keeps_outcome(Result<int, int> result, Func<int, int> e) =>
        result.Ensure(static _ => true, e) == result;

    [Property]
    public bool Ensure_keeps_failure_for_any_predicate(int error, Func<int, bool> p, Func<int, int> e) =>
        Result<int, int>.Error(error).Ensure(p, e) == Result<int, int>.Error(error);

    [Property]
    public bool Repeated_Ensure_is_idempotent(Result<int, int> result, Func<int, bool> p, Func<int, int> e) =>
        result.Ensure(p, e).Ensure(p, e) == result.Ensure(p, e);

    [Property]
    public bool Sequential_Ensure_with_same_factory_equals_conjunction(
        Result<int, int> result, Func<int, bool> p, Func<int, bool> q, Func<int, int> e) =>
        result.Ensure(p, e).Ensure(q, e) == result.Ensure(value => p(value) && q(value), e);

    [Property]
    public bool OrElse_on_failure_equals_fallback(int error, Func<int, Result<int, int>> fallback) =>
        Result<int, int>.Error(error).OrElse(fallback) == fallback(error);

    [Property]
    public bool OrElse_on_success_keeps_success(int value, Func<string?, Result<int, int>> fallback) =>
        Result<int, string?>.Ok(value).OrElse(fallback) == Result<int, int>.Ok(value);

    [Property]
    public bool OrElse_with_Error_is_identity(Result<int, string?> result) =>
        result.OrElse(Result<int, string?>.Error) == result;

    [Property]
    public bool OrElse_is_associative(Result<int, int> result, Func<int, Result<int, int>> f, Func<int, Result<int, int>> g) =>
        result.OrElse(f).OrElse(g) == result.OrElse(error => f(error).OrElse(g));

    [Property]
    public bool MapError_equals_OrElse_with_Error(Result<int, string?> result, Func<string?, int> f) =>
        result.MapError(f) == result.OrElse(error => Result<int, int>.Error(f(error)));

    [Property]
    public bool Zip_equals_nested_AndThen_and_Map(Result<int, int> a, Result<int, int> b, Func<int, int, int> f) =>
        a.Zip(b, f) == a.AndThen(x => b.Map(y => f(x, y)));

    [Property]
    public bool Zip_with_success_operand_keeps_the_other(Result<int, int> other, int unit) =>
        Result<int, int>.Ok(unit).Zip(other, static (_, y) => y) == other
        && other.Zip(Result<int, int>.Ok(unit), static (x, _) => x) == other;

    [Property]
    public bool Zip_regrouping_keeps_values_and_first_error(Result<int, int> a, Result<int, int> b, Result<int, int> c) =>
        a.Zip(b, static (x, y) => (x, y)).Zip(c, static (xy, z) => (xy.x, xy.y, z))
        == a.Zip(b.Zip(c, static (y, z) => (y, z)), static (x, yz) => (x, yz.y, yz.z));

    [Property]
    public bool Traverse_returns_first_failure_in_source_order_or_all_values(int[] source, Func<int, Result<int, int>> step)
    {
        var expected = Result<int[], int>.Ok([.. source.Select(item => Value(step(item)))]);
        foreach (var item in source)
        {
            if (!step(item).IsSuccess)
            {
                expected = Result<int[], int>.Error(Error(step(item)));
                break;
            }
        }

        return SameContent(source.Traverse(step), expected);
    }

    [Property]
    public bool Traverse_with_Ok_collects_source(int[] source) =>
        SameContent(source.Traverse(Result<int, int>.Ok), Result<int[], int>.Ok(source));

    [Property]
    public bool Traverse_of_singleton_equals_mapped_step(int item, Func<int, Result<int, int>> step) =>
        SameContent(new[] { item }.Traverse(step), step(item).Map(static value => new[] { value }));

    private static bool SameContent(Result<int[], int> actual, Result<int[], int> expected) =>
        actual.TryGetValue(out var actualValues, out var actualError) == expected.TryGetValue(out var expectedValues, out var expectedError)
        && (actual.IsSuccess ? actualValues!.SequenceEqual(expectedValues!) : actualError == expectedError);

    private static int Value(Result<int, int> result) => result.Match(static value => value, static _ => 0);

    private static int Error(Result<int, int> result) => result.Match(static _ => 0, static error => error);
}
