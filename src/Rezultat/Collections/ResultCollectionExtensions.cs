using System;
using System.Collections.Generic;

namespace Rezultat.Collections;

public static class ResultCollectionExtensions
{
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
        return TraverseCore(source, selector, ready: false);
    }

    /// <summary>
    /// Enumerates <paramref name="source"/> once, in order, and stops at the first Error, returning it without
    /// enumerating the rest of the sequence or returning a partial array. Otherwise returns a new array of the
    /// values in source order. The enumerator is disposed as by <see langword="foreach"/>; exceptions of the source
    /// and Dispose propagate unchanged.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null, checked before enumeration.</exception>
    /// <exception cref="InvalidOperationException">A visited element is an uninitialized Result.</exception>
    public static Result<T[], TError> Sequence<T, TError>(this IEnumerable<Result<T, TError>> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return TraverseCore(source, static item => item, ready: true);
    }

    private static Result<TResult[], TError> TraverseCore<TSource, TResult, TError>(
        IEnumerable<TSource> source,
        Func<TSource, Result<TResult, TError>> selector,
        bool ready)
    {
        var values = new List<TResult>();
        foreach (var item in source)
        {
            var result = selector(item);
            if (!result.IsInitialized)
            {
                if (ready)
                {
                    ThrowHelper.ThrowUninitializedResultElement();
                }

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
