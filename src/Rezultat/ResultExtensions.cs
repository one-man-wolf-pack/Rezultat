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

    /// <summary>
    /// Ok becomes Valid and Error becomes Invalid with exactly one issue, including a <see langword="null"/> error;
    /// a collection-typed error stays one opaque issue and is not flattened or checked for emptiness.
    /// </summary>
    /// <exception cref="InvalidOperationException">The Result is uninitialized.</exception>
    public static Validation<T, TError> ToValidation<T, TError>(this Result<T, TError> source) =>
        source.TryGetValue(out var value, out var error) ? Validation<T, TError>.Valid(value) : Validation<T, TError>.Invalid(error);
}
