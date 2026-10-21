using FsCheck.Xunit;
using Rezultat.Collections;
using Rezultat.Linq;

namespace Rezultat.Tests;

[Properties(Arbitrary = [typeof(InitializedResults), typeof(CompositionArbitraries)])]
public sealed class ResultReadyAndQueryLawTests
{
    [Property]
    public bool Select_is_map(Result<int, int> result, Func<int, int> selector) =>
        result.Select(selector) == result.Map(selector);

    [Property]
    public bool SelectMany_is_AndThen_of_mapped_next(Result<int, int> result, Func<int, Result<int, int>> next, Func<int, int, int> project) =>
        result.SelectMany(next, project) == result.AndThen(x => next(x).Map(y => project(x, y)));

    [Property]
    public bool Sequence_of_mapped_source_is_traverse(int[] source, Func<int, Result<int, int>> step) =>
        Normalize(source.Select(step).ToArray().Sequence()) == Normalize(source.Traverse(step));

    [Property]
    public bool AtMostOne_classifies_count(int[] source) =>
        source.AtMostOne(static () => -1) == source.Length switch
        {
            0 => Result<Maybe<int>, int>.Ok(Maybe<int>.None),
            1 => Result<Maybe<int>, int>.Ok(Maybe<int>.Some(source[0])),
            _ => Result<Maybe<int>, int>.Error(-1),
        };

    private static Result<string, int> Normalize(Result<int[], int> result) => result.Map(static values => string.Join(",", values));
}

[Properties(Arbitrary = [typeof(MaybeValues), typeof(MaybeCompositionArbitraries)])]
public sealed class MaybeReadyAndQueryLawTests
{
    [Property]
    public bool Select_is_map(Maybe<int> maybe, Func<int, int> selector) => maybe.Select(selector) == maybe.Map(selector);

    [Property]
    public bool Where_is_filter(Maybe<int> maybe, Func<int, bool> predicate) => maybe.Where(predicate) == maybe.Filter(predicate);

    [Property]
    public bool SelectMany_is_AndThen_of_mapped_next(Maybe<int> maybe, Func<int, Maybe<int>> next, Func<int, int, int> project) =>
        maybe.SelectMany(next, project) == maybe.AndThen(x => next(x).Map(y => project(x, y)));

    [Property]
    public bool Sequence_of_mapped_source_is_traverse(int[] source, Func<int, Maybe<int>> step) =>
        Normalize(source.Select(step).ToArray().Sequence()) == Normalize(source.Traverse(step));

    private static Maybe<string> Normalize(Maybe<int[]> maybe) => maybe.Map(static values => string.Join(",", values));
}

[Properties(Arbitrary = [typeof(ValidationValues)])]
public sealed class ValidationReadyLawTests
{
    [Property]
    public bool Sequence_of_mapped_source_is_traverse(int[] source, Func<int, Validation<int, int>> step) =>
        Normalize(source.Select(step).ToArray().Sequence()) == Normalize(source.Traverse(step));

    [Property]
    public bool ValidateAll_has_the_branch_and_issues_of_traverse_mapped_to_unit(int[] source, Func<int, Validation<int, int>> step) =>
        source.ValidateAll(step) == source.Traverse(step).Map(static _ => Unit.Value);

    [Property]
    public bool Ready_ValidateAll_is_identity_selector_form(Validation<int, int>[] outcomes) =>
        outcomes.ValidateAll() == outcomes.ValidateAll(static outcome => outcome);

    private static Validation<string, int> Normalize(Validation<int[], int> validation) =>
        validation.Match(static values => Validation<string, int>.Valid(string.Join(",", values)), Validation<string, int>.InvalidMany);
}
