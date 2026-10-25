using System;

namespace Rezultat;

public static class PresenceExtensions {
    /// <summary>
    /// Missing and Null become <c>Ok</c> with the same state without invoking <paramref name="selector"/>; for Value
    /// the selector's Ok becomes <c>Ok(Value)</c> and its Error, including <see langword="null"/>, is returned as is.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is null, even for Missing and Null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The selector returned an uninitialized Result, or Ok with <see langword="null"/> or an empty
    /// <see cref="Nullable{T}"/>, even when nullable warnings were bypassed; it does not become Null or Missing.
    /// Nested carriers inside its payload are not checked.
    /// </exception>
    public static Result<Presence<TResult>, TError> Traverse<T, TResult, TError>(
        this Presence<T> source,
        Func<T, Result<TResult, TError>> selector)
        where T : notnull
        where TResult : notnull {
        ArgumentNullException.ThrowIfNull(selector);
        if (!source.TryGetValue(out var value)) {
            return Result<Presence<TResult>, TError>.Ok(source.IsNull ? Presence<TResult>.Null : Presence<TResult>.Missing);
        }

        var result = selector(value);
        if (!result.IsInitialized) {
            ThrowHelper.ThrowUninitializedReturnedResult(nameof(selector));
        }

        if (!result.TryGetValue(out var mapped, out var error)) {
            return Result<Presence<TResult>, TError>.Error(error);
        }

        if (mapped is null) {
            ThrowHelper.ThrowNullPresenceTraverseResult();
        }

        return Result<Presence<TResult>, TError>.Ok(Presence<TResult>.Value(mapped));
    }
}
