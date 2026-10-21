using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Rezultat.Collections;

public static class MaybeCollectionExtensions
{
    /// <summary>
    /// Presence is decided by finding an element, not by its value: a <see langword="null"/> or <c>default</c> element is
    /// Some. Enumeration stops at the first element; source and Dispose exceptions propagate unchanged.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static Maybe<T> FirstOrNone<T>(this IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        foreach (var item in source)
        {
            return Maybe<T>.Some(item);
        }

        return Maybe<T>.None;
    }

    /// <summary>
    /// Returns the first element satisfying <paramref name="predicate"/>, even a <see langword="null"/> one; elements
    /// after the match are not requested. Source, predicate and Dispose exceptions propagate unchanged.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> or <paramref name="predicate"/> is null, checked in that order before enumeration.
    /// </exception>
    public static Maybe<T> FirstOrNone<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        foreach (var item in source)
        {
            if (predicate(item))
            {
                return Maybe<T>.Some(item);
            }
        }

        return Maybe<T>.None;
    }

    /// <summary>
    /// An <see cref="IList{T}"/>, otherwise an <see cref="IReadOnlyList{T}"/>, reads Count once and only the last
    /// position, without an enumerator; any other source is enumerated once to its end. A <see langword="null"/>
    /// element is Some.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static Maybe<T> LastOrNone<T>(this IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source is IList<T> list)
        {
            var count = list.Count;
            return count == 0 ? Maybe<T>.None : Maybe<T>.Some(list[count - 1]);
        }

        if (source is IReadOnlyList<T> readOnlyList)
        {
            var count = readOnlyList.Count;
            return count == 0 ? Maybe<T>.None : Maybe<T>.Some(readOnlyList[count - 1]);
        }

        var last = Maybe<T>.None;
        foreach (var item in source)
        {
            last = Maybe<T>.Some(item);
        }

        return last;
    }

    /// <summary>
    /// The search path is part of the contract: an <see cref="IList{T}"/>, otherwise an
    /// <see cref="IReadOnlyList{T}"/>, is checked backwards from the last position and stops at the first match;
    /// any other source is enumerated forwards to its end, invoking <paramref name="predicate"/> once per element.
    /// A stateful predicate can therefore observe different elements on a list and on an equivalent sequence.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> or <paramref name="predicate"/> is null, checked in that order before any access.
    /// </exception>
    public static Maybe<T> LastOrNone<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        if (source is IList<T> list)
        {
            for (var index = list.Count - 1; index >= 0; index--)
            {
                var item = list[index];
                if (predicate(item))
                {
                    return Maybe<T>.Some(item);
                }
            }

            return Maybe<T>.None;
        }

        if (source is IReadOnlyList<T> readOnlyList)
        {
            for (var index = readOnlyList.Count - 1; index >= 0; index--)
            {
                var item = readOnlyList[index];
                if (predicate(item))
                {
                    return Maybe<T>.Some(item);
                }
            }

            return Maybe<T>.None;
        }

        var last = Maybe<T>.None;
        foreach (var item in source)
        {
            if (predicate(item))
            {
                last = Maybe<T>.Some(item);
            }
        }

        return last;
    }

    /// <summary>
    /// None for an empty source, Some for exactly one element, even a <see langword="null"/> one. Multiplicity is not
    /// absence: a second element throws. The second element is detected by MoveNext only, without reading it.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The source has more than one element; a source or Dispose exception of the same type is not distinguished.
    /// </exception>
    public static Maybe<T> SingleOrNone<T>(this IEnumerable<T> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        using var enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return Maybe<T>.None;
        }

        var single = enumerator.Current;
        if (enumerator.MoveNext())
        {
            ThrowHelper.ThrowMoreThanOneElement();
        }

        return Maybe<T>.Some(single);
    }

    /// <summary>
    /// None for no match, Some for exactly one match, even a <see langword="null"/> one. Matches are counted per
    /// element, not per distinct value; enumeration stops at the second match, which throws.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> or <paramref name="predicate"/> is null, checked in that order before enumeration.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// More than one element matches; a source, predicate or Dispose exception of the same type is not distinguished.
    /// </exception>
    public static Maybe<T> SingleOrNone<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(predicate);
        var found = Maybe<T>.None;
        foreach (var item in source)
        {
            if (!predicate(item))
            {
                continue;
            }

            if (found.IsSome)
            {
                ThrowHelper.ThrowMoreThanOneMatch();
            }

            found = Maybe<T>.Some(item);
        }

        return found;
    }

    /// <summary>
    /// A negative <paramref name="index"/> or a position past the end is None; a negative index does not access the
    /// source. An <see cref="IList{T}"/>, otherwise an <see cref="IReadOnlyList{T}"/>, is read through Count and the
    /// indexer; any other source is enumerated up to the position, reading only the found element.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null, even for a negative index.</exception>
    public static Maybe<T> ElementAtOrNone<T>(this IEnumerable<T> source, int index)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (index < 0)
        {
            return Maybe<T>.None;
        }

        if (source is IList<T> list)
        {
            return index < list.Count ? Maybe<T>.Some(list[index]) : Maybe<T>.None;
        }

        if (source is IReadOnlyList<T> readOnlyList)
        {
            return index < readOnlyList.Count ? Maybe<T>.Some(readOnlyList[index]) : Maybe<T>.None;
        }

        using var enumerator = source.GetEnumerator();
        var remaining = index;
        while (enumerator.MoveNext())
        {
            if (remaining == 0)
            {
                return Maybe<T>.Some(enumerator.Current);
            }

            remaining--;
        }

        return Maybe<T>.None;
    }

    /// <summary>
    /// Calls TryGetValue once; its <see langword="false"/> is None regardless of the out value, and a found
    /// <see langword="null"/> is Some. Preferred over the <see cref="IDictionary{TKey, TValue}"/> overload when both
    /// apply; callers compiled with C# 12 or earlier may get an ambiguity error for types implementing both.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    [OverloadResolutionPriority(1)]
    public static Maybe<TValue> GetOrNone<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> source, TKey key)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out var value) ? Maybe<TValue>.Some(value) : Maybe<TValue>.None;
    }

    /// <summary>
    /// Calls TryGetValue once; its <see langword="false"/> is None regardless of the out value, and a found
    /// <see langword="null"/> is Some.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null.</exception>
    public static Maybe<TValue> GetOrNone<TKey, TValue>(this IDictionary<TKey, TValue> source, TKey key)
    {
        ArgumentNullException.ThrowIfNull(source);
        return source.TryGetValue(key, out var value) ? Maybe<TValue>.Some(value) : Maybe<TValue>.None;
    }

    /// <summary>
    /// Enumerates <paramref name="source"/> once, in order, invoking <paramref name="selector"/> once per visited
    /// element. Stops at the first None and returns None; no partial array is returned. Otherwise returns Some with
    /// a new array of the payloads in source order, including <see langword="null"/> ones.
    /// The enumerator is disposed as by <see langword="foreach"/>; exceptions of the source, the selector and
    /// Dispose propagate unchanged.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> or <paramref name="selector"/> is null, checked in that order before enumeration.
    /// </exception>
    public static Maybe<TResult[]> Traverse<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, Maybe<TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        var values = new List<TResult>();
        foreach (var item in source)
        {
            if (!selector(item).TryGetValue(out var value))
            {
                return Maybe<TResult[]>.None;
            }

            values.Add(value);
        }

        return Maybe<TResult[]>.Some(values.ToArray());
    }

    /// <summary>
    /// Arguments are checked immediately, but <paramref name="source"/> is enumerated and
    /// <paramref name="selector"/> invoked only while the result is enumerated, again on every enumeration and
    /// without caching. None is skipped; Some yields its payload, including <see langword="null"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> or <paramref name="selector"/> is null, checked in that order on the call.
    /// </exception>
    public static IEnumerable<TResult> Choose<TSource, TResult>(
        this IEnumerable<TSource> source,
        Func<TSource, Maybe<TResult>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return ChooseIterator(source, selector);
    }

    private static IEnumerable<TResult> ChooseIterator<TSource, TResult>(
        IEnumerable<TSource> source,
        Func<TSource, Maybe<TResult>> selector)
    {
        foreach (var item in source)
        {
            if (selector(item).TryGetValue(out var value))
            {
                yield return value;
            }
        }
    }
}
