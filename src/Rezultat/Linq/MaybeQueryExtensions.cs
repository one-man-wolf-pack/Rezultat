using System;

namespace Rezultat.Linq;

public static class MaybeQueryExtensions {
    /// <summary>Query <c>select</c> and <c>let</c> form of <see cref="Maybe{T}.Map{TResult}"/> with its exact contract.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="selector"/> is null, even for None.</exception>
    public static Maybe<TResult> Select<T, TResult>(this Maybe<T> source, Func<T, TResult> selector) =>
        source.Map(selector);

    /// <summary>
    /// Query form of a dependent step: on Some calls <paramref name="next"/> once with the payload; when it is also
    /// Some, calls <paramref name="project"/> once with the original payload observed before any callback and the
    /// next payload. None, including a <c>default</c> one from <paramref name="next"/>, is returned without calling
    /// later callbacks. Callback exceptions propagate unchanged.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="next"/> or <paramref name="project"/> is null, checked in that order even for None.
    /// </exception>
    public static Maybe<TResult> SelectMany<T, TNext, TResult>(
        this Maybe<T> source,
        Func<T, Maybe<TNext>> next,
        Func<T, TNext, TResult> project) {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(project);
        if (!source.TryGetValue(out var value)) {
            return Maybe<TResult>.None;
        }

        return next(value).TryGetValue(out var nextValue)
            ? Maybe<TResult>.Some(project(value, nextValue))
            : Maybe<TResult>.None;
    }

    /// <summary>Query <c>where</c> form of <see cref="Maybe{T}.Filter"/> with its exact contract.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is null, even for None.</exception>
    public static Maybe<T> Where<T>(this Maybe<T> source, Func<T, bool> predicate) =>
        source.Filter(predicate);
}
