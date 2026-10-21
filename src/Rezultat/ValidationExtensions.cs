using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Rezultat;

public static class ValidationExtensions
{
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
        Func<TSource, Validation<TResult, TIssue>> selector)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(selector);
        List<TResult>? values = [];
        var firstIssues = default(ImmutableArray<TIssue>);
        ImmutableArray<TIssue>.Builder? accumulated = null;
        foreach (var item in source)
        {
            var outcome = selector(item);
            if (!outcome.IsInitialized)
            {
                ThrowHelper.ThrowUninitializedReturnedValidation(nameof(selector));
            }

            if (outcome.TryGetValue(out var value, out var issues))
            {
                values?.Add(value);
            }
            else if (firstIssues.IsDefault)
            {
                values = null;
                firstIssues = issues;
            }
            else
            {
                if (accumulated is null)
                {
                    accumulated = ImmutableArray.CreateBuilder<TIssue>(firstIssues.Length + issues.Length);
                    accumulated.AddRange(firstIssues);
                }

                accumulated.AddRange(issues);
            }
        }

        if (values is not null)
        {
            return Validation<TResult[], TIssue>.Valid(values.ToArray());
        }

        return Validation<TResult[], TIssue>.InvalidMany(accumulated?.ToImmutable() ?? firstIssues);
    }
}
