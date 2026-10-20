using System;
using System.Collections.Generic;

namespace Rezultat;

public static class ResultExtensions
{
    /// <summary>
    /// Ok becomes Some, including a <see langword="null"/> payload; Error becomes None, intentionally discarding the
    /// error without observing it.
    /// </summary>
    /// <exception cref="InvalidOperationException">The Result is uninitialized; it does not become None.</exception>
    public static Maybe<T> ToMaybe<T, TError>(this Result<T, TError> result) =>
        result.TryGetValue(out var value, out _) ? Maybe<T>.Some(value) : Maybe<T>.None;

    /// <summary>
    /// Enumerates <paramref name="source"/> once, in order, invoking <paramref name="selector"/> once per visited
    /// element. Stops at the first failure and returns its error; the rest of the sequence is not enumerated and
    /// no partial array is returned. On success returns a new array of the values in source order.
    /// The enumerator is disposed as by <see langword="foreach"/>; exceptions of the source, the selector and
    /// Dispose propagate unchanged.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> or <paramref name="selector"/> is null, checked in that order before enumeration.
    /// </exception>
    /// <exception cref="InvalidOperationException">The selector returned an uninitialized Result for a visited element.</exception>
    public static Result<TResult[], TError> Traverse<TSource, TResult, TError>(
        this IEnumerable<TSource> source,
        Func<TSource, Result<TResult, TError>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        var values = new List<TResult>();
        foreach (var item in source)
        {
            var result = selector(item);
            if (!result.IsInitialized)
            {
                ThrowHelper.ThrowUninitializedReturnedResult(nameof(selector));
            }

            if (!result.TryGetValue(out var value, out var error))
            {
                return Result<TResult[], TError>.Error(error);
            }

            values.Add(value);
        }

        return Result<TResult[], TError>.Ok(values.ToArray());
    }
}
