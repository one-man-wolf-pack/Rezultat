using System;
using System.Collections.Immutable;

namespace Rezultat;

public static class ValidationExtensions {
    /// <summary>
    /// Valid becomes Ok; Invalid becomes Error with the same issue storage, keeping order, duplicates and
    /// <see langword="null"/> issues. The Error compares its array by <see cref="ImmutableArray{T}"/> equality, not
    /// position by position as Validation does, and <c>ToValidation</c> afterwards wraps the whole array as one issue.
    /// </summary>
    /// <exception cref="InvalidOperationException">The Validation is uninitialized.</exception>
    public static Result<T, ImmutableArray<TIssue>> ToResult<T, TIssue>(this Validation<T, TIssue> source) =>
        source.TryGetValue(out var value, out var issues)
            ? Result<T, ImmutableArray<TIssue>>.Ok(value)
            : Result<T, ImmutableArray<TIssue>>.Error(issues);
}
