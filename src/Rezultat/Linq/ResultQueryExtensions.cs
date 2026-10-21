using System;

namespace Rezultat.Linq;

public static class ResultQueryExtensions
{
    /// <summary>Query <c>select</c> and <c>let</c> form of <see cref="Result{T, TError}.Map{TResult}"/> with its exact contract.</summary>
    /// <exception cref="InvalidOperationException"><paramref name="source"/> is uninitialized; checked before the selector.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is null, regardless of the active branch.</exception>
    public static Result<TResult, TError> Select<T, TResult, TError>(
        this Result<T, TError> source,
        Func<T, TResult> selector) =>
        source.Map(selector);

    /// <summary>
    /// Query form of a dependent step: on Ok calls <paramref name="next"/> once with the value; when it is also Ok,
    /// calls <paramref name="project"/> once with the original value observed before any callback and the next
    /// value. The first Error is returned without calling later callbacks. Callback exceptions propagate unchanged.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="source"/> is uninitialized, checked first; or <paramref name="next"/> returned an uninitialized
    /// Result, checked before <paramref name="project"/>.
    /// </exception>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="next"/> or <paramref name="project"/> is null, checked in that order regardless of the active
    /// branch.
    /// </exception>
    public static Result<TResult, TError> SelectMany<T, TNext, TResult, TError>(
        this Result<T, TError> source,
        Func<T, Result<TNext, TError>> next,
        Func<T, TNext, TResult> project)
    {
        if (!source.IsInitialized)
        {
            ThrowHelper.ThrowUninitializedResult();
        }

        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(project);
        if (!source.TryGetValue(out var value, out var error))
        {
            return Result<TResult, TError>.Error(error);
        }

        var second = next(value);
        if (!second.IsInitialized)
        {
            ThrowHelper.ThrowUninitializedReturnedResult(nameof(next));
        }

        if (!second.TryGetValue(out var nextValue, out var nextError))
        {
            return Result<TResult, TError>.Error(nextError);
        }

        return Result<TResult, TError>.Ok(project(value, nextValue));
    }
}
