using System;

namespace Rezultat;

public static class MaybeExtensions
{
    /// <summary>
    /// None becomes <c>Ok(None)</c> without invoking <paramref name="selector"/>; for Some the selector's Ok becomes
    /// <c>Ok(Some)</c> and its Error is returned as is.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is null, even for None.</exception>
    /// <exception cref="InvalidOperationException">
    /// The selector returned an uninitialized Result. Nested carriers inside its payload are not checked.
    /// </exception>
    public static Result<Maybe<TResult>, TError> Traverse<T, TResult, TError>(
        this Maybe<T> source,
        Func<T, Result<TResult, TError>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        if (!source.TryGetValue(out var value))
        {
            return Result<Maybe<TResult>, TError>.Ok(Maybe<TResult>.None);
        }

        var result = selector(value);
        if (!result.IsInitialized)
        {
            ThrowHelper.ThrowUninitializedReturnedResult(nameof(selector));
        }

        return result.TryGetValue(out var mapped, out var error)
            ? Result<Maybe<TResult>, TError>.Ok(Maybe<TResult>.Some(mapped))
            : Result<Maybe<TResult>, TError>.Error(error);
    }
}
