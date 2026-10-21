using System;

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
}
