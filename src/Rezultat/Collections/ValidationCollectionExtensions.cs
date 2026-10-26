using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rezultat.Collections;

public static class ValidationCollectionExtensions {
    /// <summary>
    /// Enumerates <paramref name="source"/> once, in order, invoking <paramref name="selector"/> once per visited
    /// element; Invalid does not stop the traversal. With any Invalid returns all their issues in element order and
    /// no partial array; otherwise returns a new array of the values in source order. The enumerator is disposed as
    /// by <see langword="foreach"/>; exceptions of the source, the selector and Dispose propagate unchanged and
    /// discard the accumulated issues.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> or <paramref name="selector"/> is null, checked in that order before enumeration.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The selector returned an uninitialized Validation for a visited element, even after accumulated issues;
    /// the traversal stops there.
    /// </exception>
    public static Validation<TResult[], TIssue> Traverse<TSource, TResult, TIssue>(
        this IEnumerable<TSource> source,
        Func<TSource, Validation<TResult, TIssue>> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return TraverseCore(source, selector, ready: false);
    }

    /// <summary>
    /// Enumerates <paramref name="source"/> once, in order; Invalid does not stop the traversal. With any Invalid
    /// returns all their issues in element order and no partial array; otherwise returns a new array of the values
    /// in source order. The enumerator is disposed as by <see langword="foreach"/>; exceptions of the source and
    /// Dispose propagate unchanged and discard the accumulated issues.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null, checked before enumeration.</exception>
    /// <exception cref="InvalidOperationException">
    /// A visited element is an uninitialized Validation, even after accumulated issues; the traversal stops there.
    /// </exception>
    public static Validation<T[], TIssue> Sequence<T, TIssue>(this IEnumerable<Validation<T, TIssue>> source) {
        ArgumentNullException.ThrowIfNull(source);
        return TraverseCore(source, static item => item, ready: true);
    }

    /// <summary>
    /// Enumerates <paramref name="source"/> once, in order, and accumulates only issues: success values are
    /// discarded, not collected. Returns Valid of <see cref="Unit.Value"/> when every element is Valid or the source
    /// is empty, otherwise all issues in element order. Invalid does not stop the traversal. The enumerator is
    /// disposed as by <see langword="foreach"/>; exceptions of the source and Dispose propagate unchanged and
    /// discard the accumulated issues.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="source"/> is null, checked before enumeration.</exception>
    /// <exception cref="InvalidOperationException">
    /// A visited element is an uninitialized Validation, even after accumulated issues; the traversal stops there.
    /// </exception>
    public static Validation<Unit, TIssue> ValidateAll<T, TIssue>(this IEnumerable<Validation<T, TIssue>> source) {
        ArgumentNullException.ThrowIfNull(source);
        return ValidateAllCore(source, static item => item, ready: true);
    }

    /// <summary>
    /// Enumerates <paramref name="source"/> once, in order, invoking <paramref name="selector"/> once per visited
    /// element, and accumulates only issues: success values are discarded, not collected. Returns Valid of
    /// <see cref="Unit.Value"/> when every outcome is Valid or the source is empty, otherwise all issues in element
    /// order. Invalid does not stop the traversal. The enumerator is disposed as by <see langword="foreach"/>;
    /// exceptions of the source, the selector and Dispose propagate unchanged and discard the accumulated issues.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="source"/> or <paramref name="selector"/> is null, checked in that order before enumeration.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// The selector returned an uninitialized Validation for a visited element, even after accumulated issues;
    /// the traversal stops there.
    /// </exception>
    public static Validation<Unit, TIssue> ValidateAll<TSource, TValue, TIssue>(
        this IEnumerable<TSource> source,
        Func<TSource, Validation<TValue, TIssue>> selector) {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        return ValidateAllCore(source, selector, ready: false);
    }

    private static Validation<TResult[], TIssue> TraverseCore<TSource, TResult, TIssue>(
        IEnumerable<TSource> source,
        Func<TSource, Validation<TResult, TIssue>> selector,
        bool ready) {
        List<TResult>? values = [];
        var firstIssues = default(ImmutableArray<TIssue>);
        ImmutableArray<TIssue>.Builder? accumulated = null;
        foreach (var item in source) {
            var outcome = Observe(selector(item), ready);
            if (outcome.TryGetValue(out var value, out var issues)) {
                values?.Add(value);
            } else {
                values = null;
                Accumulate(ref firstIssues, ref accumulated, issues);
            }
        }

        if (values is not null) {
            return Validation<TResult[], TIssue>.Valid([.. values]);
        }

        return Validation<TResult[], TIssue>.InvalidMany(accumulated?.ToImmutable() ?? firstIssues);
    }

    private static Validation<Unit, TIssue> ValidateAllCore<TSource, TValue, TIssue>(
        IEnumerable<TSource> source,
        Func<TSource, Validation<TValue, TIssue>> selector,
        bool ready) {
        var firstIssues = default(ImmutableArray<TIssue>);
        ImmutableArray<TIssue>.Builder? accumulated = null;
        foreach (var item in source) {
            if (!Observe(selector(item), ready).TryGetValue(out _, out var issues)) {
                Accumulate(ref firstIssues, ref accumulated, issues);
            }
        }

        if (firstIssues.IsDefault) {
            return Validation<Unit, TIssue>.Valid(Unit.Value);
        }

        return Validation<Unit, TIssue>.InvalidMany(accumulated?.ToImmutable() ?? firstIssues);
    }

    private static Validation<T, TIssue> Observe<T, TIssue>(Validation<T, TIssue> outcome, bool ready) {
        if (!outcome.IsInitialized) {
            if (ready) {
                ThrowHelper.ThrowUninitializedValidationElement();
            }

            ThrowHelper.ThrowUninitializedReturnedValidation("selector");
        }

        return outcome;
    }

    private static void Accumulate<TIssue>(
        ref ImmutableArray<TIssue> firstIssues,
        ref ImmutableArray<TIssue>.Builder? accumulated,
        ImmutableArray<TIssue> issues) {
        if (firstIssues.IsDefault) {
            firstIssues = issues;
            return;
        }

        if (accumulated is null) {
            accumulated = ImmutableArray.CreateBuilder<TIssue>(firstIssues.Length + issues.Length);
            accumulated.AddRange(firstIssues);
        }

        accumulated.AddRange(issues);
    }
}
