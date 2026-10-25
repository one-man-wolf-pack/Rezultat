using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using Rezultat.Collections;

namespace Rezultat.Tests;

public static class MaybeCompositionArbitraries {
    public static Arbitrary<Func<int, Maybe<int>>> Steps() =>
        (from isSome in ArbMap.Default.GeneratorFor<Func<int, bool>>()
         from value in ArbMap.Default.GeneratorFor<Func<int, int>>()
         select (Func<int, Maybe<int>>)(input => isSome(input) ? Maybe<int>.Some(value(input)) : Maybe<int>.None))
            .ToArbitrary();

    public static Arbitrary<Func<Maybe<int>>> Fallbacks() =>
        MaybeValues.SmallInts().Generator.Select(static maybe => (Func<Maybe<int>>)(() => maybe)).ToArbitrary();
}

[Properties(Arbitrary = [typeof(MaybeValues), typeof(MaybeCompositionArbitraries)])]
public sealed class MaybeCompositionLawTests {
    [Property]
    public bool Map_preserves_identity(Maybe<int> maybe) => maybe.Map(static value => value) == maybe;

    [Property]
    public bool Map_preserves_composition(Maybe<int> maybe, Func<int, int> f, Func<int, int> g) =>
        maybe.Map(f).Map(g) == maybe.Map(value => g(f(value)));

    [Property]
    public bool Map_equals_AndThen_with_Some(Maybe<int> maybe, Func<int, int> f) =>
        maybe.Map(f) == maybe.AndThen(value => Maybe<int>.Some(f(value)));

    [Property]
    public bool AndThen_has_left_identity(int value, Func<int, Maybe<int>> next) =>
        Maybe<int>.Some(value).AndThen(next) == next(value);

    [Property]
    public bool AndThen_has_right_identity(Maybe<int> maybe) => maybe.AndThen(Maybe<int>.Some) == maybe;

    [Property]
    public bool AndThen_is_associative(Maybe<int> maybe, Func<int, Maybe<int>> f, Func<int, Maybe<int>> g) =>
        maybe.AndThen(f).AndThen(g) == maybe.AndThen(value => f(value).AndThen(g));

    [Property]
    public bool None_OrElse_equals_fallback(Func<Maybe<int>> fallback) => Maybe<int>.None.OrElse(fallback) == fallback();

    [Property]
    public bool OrElse_with_None_fallback_is_identity(Maybe<int> maybe) => maybe.OrElse(static () => Maybe<int>.None) == maybe;

    [Property]
    public bool OrElse_is_associative(Maybe<int> maybe, Func<Maybe<int>> f, Func<Maybe<int>> g) =>
        maybe.OrElse(f).OrElse(g) == maybe.OrElse(() => f().OrElse(g));

    [Property]
    public bool Filter_with_constant_predicate_keeps_or_drops(Maybe<int> maybe) =>
        maybe.Filter(static _ => true) == maybe && maybe.Filter(static _ => false) == Maybe<int>.None;

    [Property]
    public bool Filter_composes_as_short_circuit_conjunction(Maybe<int> maybe, Func<int, bool> p, Func<int, bool> q) =>
        maybe.Filter(p).Filter(q) == maybe.Filter(value => p(value) && q(value));

    [Property]
    public bool Filter_is_idempotent_for_stable_predicate(Maybe<int> maybe, Func<int, bool> p) =>
        maybe.Filter(p).Filter(p) == maybe.Filter(p);

    [Property]
    public bool Filter_equals_conditional_AndThen(Maybe<int> maybe, Func<int, bool> p) =>
        maybe.Filter(p) == maybe.AndThen(value => p(value) ? Maybe<int>.Some(value) : Maybe<int>.None);

    [Property]
    public bool Zip_equals_nested_AndThen_and_Map(Maybe<int> a, Maybe<int> b, Func<int, int, int> f) =>
        a.Zip(b, f) == a.AndThen(x => b.Map(y => f(x, y)));

    [Property]
    public bool Zip_with_None_is_None(Maybe<int> maybe, Func<int, int, int> f) =>
        Maybe<int>.None.Zip(maybe, f) == Maybe<int>.None && maybe.Zip(Maybe<int>.None, f) == Maybe<int>.None;

    [Property]
    public bool Zip_projection_with_Some_keeps_the_other_side(int u, Maybe<int> maybe) =>
        Maybe<int>.Some(u).Zip(maybe, static (_, y) => y) == maybe && maybe.Zip(Maybe<int>.Some(u), static (x, _) => x) == maybe;

    [Property]
    public bool Zip_regrouping_keeps_outcome(Maybe<int> a, Maybe<int> b, Maybe<int> c) =>
        a.Zip(b, static (x, y) => (x, y)).Zip(c, static (xy, z) => (xy.x, xy.y, z))
        == a.Zip(b.Zip(c, static (y, z) => (y, z)), static (x, yz) => (x, yz.y, yz.z));

    [Property]
    public bool Zip_swapping_inputs_and_arguments_keeps_outcome(Maybe<int> a, Maybe<int> b, Func<int, int, int> f) =>
        a.Zip(b, f) == b.Zip(a, (y, x) => f(x, y));

    [Property]
    public bool Traverse_with_Ok_keeps_maybe_inside_Ok(Maybe<int> maybe) =>
        maybe.Traverse(static value => Result<int, string>.Ok(value)) == Result<Maybe<int>, string>.Ok(maybe);

    [Property]
    public bool Traverse_equals_Map_then_identity_Traverse(Maybe<int> maybe, Func<int, bool> isOk, Func<int, int> value) =>
        maybe.Traverse(Step(isOk, value)) == maybe.Map(Step(isOk, value)).Traverse(static result => result);

    [Property]
    public bool ToResult_then_ToMaybe_keeps_maybe(Maybe<int> maybe, string error) => maybe.ToResult(error).ToMaybe() == maybe;

    [Property]
    public bool Ok_ToMaybe_then_ToResult_keeps_success(int value, string error) =>
        Result<int, string>.Ok(value).ToMaybe().ToResult(error) == Result<int, string>.Ok(value);

    [Property]
    public bool Collection_Traverse_of_Somes_collects_source(int[] source) =>
        source.Traverse(Maybe<int>.Some).Map(static values => string.Join(',', values)) == Maybe<string>.Some(string.Join(',', source));

    [Property]
    public bool Singleton_Traverse_equals_Map_to_array(int item, Func<int, Maybe<int>> f) =>
        new[] { item }.Traverse(f).Map(static values => values.Single()) == f(item);

    private static Func<int, Result<int, int>> Step(Func<int, bool> isOk, Func<int, int> value) =>
        input => isOk(input) ? Result<int, int>.Ok(value(input)) : Result<int, int>.Error(input);
}
