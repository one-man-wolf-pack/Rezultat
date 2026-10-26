using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Rezultat;

/// <summary>
/// Validation outcome: Valid with <typeparamref name="T"/> or Invalid with a non-empty ordered sequence of
/// <typeparamref name="TIssue"/>, keeping duplicates and allowed <see langword="null"/> issues. <c>default</c> and
/// <c>new()</c> are an uninitialized carrier that equals neither outcome; observing it as an outcome throws
/// <see cref="InvalidOperationException"/>. Neither payload is null-checked by the carrier.
/// </summary>
public readonly struct Validation<T, TIssue> : IEquatable<Validation<T, TIssue>> {
    private const byte Uninitialized = 0;
    private const byte ValidState = 1;
    private const byte InvalidState = 2;

    private readonly T _value;
    private readonly ImmutableArray<TIssue> _issues;
    private readonly byte _state;

    private Validation(byte state, T value, ImmutableArray<TIssue> issues) {
        _state = state;
        _value = value;
        _issues = issues;
    }

    public static Validation<T, TIssue> Valid(T value) => new(ValidState, value, default);

    /// <summary>
    /// Creates Invalid with exactly one issue; a collection-typed issue stays one opaque element.
    /// </summary>
    public static Validation<T, TIssue> Invalid(TIssue issue) => new(InvalidState, default!, [issue]);

    /// <summary>
    /// Keeps <paramref name="issues"/> as is, without an additional snapshot; materialize a mutable or lazy source
    /// before the call.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="issues"/> is default or empty.</exception>
    public static Validation<T, TIssue> InvalidMany(ImmutableArray<TIssue> issues) {
        if (issues.IsDefaultOrEmpty) {
            ThrowHelper.ThrowEmptyIssues(nameof(issues));
        }

        return new(InvalidState, default!, issues);
    }

    /// <summary>
    /// Safe carrier check: <see langword="false"/> only for <c>default</c>; never inspects the payload.
    /// </summary>
    public bool IsInitialized => _state != Uninitialized;

    /// <summary>
    /// Reports the outcome without extracting the payload; it does not promise a non-null payload.
    /// </summary>
    /// <exception cref="InvalidOperationException">The carrier is uninitialized.</exception>
    public bool IsValid {
        get {
            EnsureInitialized();
            return _state == ValidState;
        }
    }

    /// <summary>
    /// On Valid outputs the exact value and initialized empty issues; on Invalid outputs <c>default</c> and the
    /// stored non-empty issues without copying them.
    /// </summary>
    /// <exception cref="InvalidOperationException">The carrier is uninitialized.</exception>
    public bool TryGetValue([MaybeNullWhen(false)] out T value, out ImmutableArray<TIssue> issues) {
        EnsureInitialized();
        value = _value;
        issues = _state == ValidState ? [] : _issues;
        return _state == ValidState;
    }

    /// <summary>
    /// Invokes only the handler of the active outcome, exactly once; its exceptions propagate unchanged.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The carrier is uninitialized; checked before the handlers, so null handlers do not change it.
    /// </exception>
    /// <exception cref="ArgumentNullException">Either handler is null, regardless of the active outcome.</exception>
    public TResult Match<TResult>(Func<T, TResult> onValid, Func<ImmutableArray<TIssue>, TResult> onInvalid) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(onValid);
        ArgumentNullException.ThrowIfNull(onInvalid);
        return _state == ValidState ? onValid(_value) : onInvalid(_issues);
    }

    /// <summary>
    /// Projects the Valid value; Invalid keeps the same issue storage without invoking <paramref name="selector"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The carrier is uninitialized; checked before the selector.</exception>
    /// <exception cref="ArgumentNullException">The selector is null, even for Invalid.</exception>
    public Validation<TResult, TIssue> Map<TResult>(Func<T, TResult> selector) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(selector);
        return _state == ValidState
            ? Validation<TResult, TIssue>.Valid(selector(_value))
            : Validation<TResult, TIssue>.FromIssues(_issues);
    }

    /// <summary>
    /// Dependent check of the Valid value: when <paramref name="predicate"/> fails, <paramref name="issueFactory"/>
    /// receives the same value and yields exactly one issue. Invalid is returned with the same issue storage
    /// without invoking either callback, so chained Ensure calls do not accumulate independent issues; use Zip or
    /// a collection Traverse for that. Reassigning the caller's variable from a callback changes neither the kept
    /// outcome nor the value passed to <paramref name="issueFactory"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The carrier is uninitialized; checked before the callbacks.</exception>
    /// <exception cref="ArgumentNullException">
    /// A callback is null, even for Invalid; <paramref name="predicate"/> is checked first.
    /// </exception>
    public Validation<T, TIssue> Ensure(Func<T, bool> predicate, Func<T, TIssue> issueFactory) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(issueFactory);
        if (_state != ValidState) {
            return this;
        }

        var value = _value;
        return predicate(value) ? Valid(value) : Invalid(issueFactory(value));
    }

    /// <summary>
    /// Translates every issue once, in order, keeping their number, duplicates and <see langword="null"/> results;
    /// Valid keeps its value without invoking <paramref name="selector"/>. Changing the number of issues is not
    /// MapIssues; use <see cref="OrElse{TNewIssue}"/> or <see cref="Match{TResult}"/>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The carrier is uninitialized; checked before the selector.</exception>
    /// <exception cref="ArgumentNullException">The selector is null, even for Valid.</exception>
    public Validation<T, TNewIssue> MapIssues<TNewIssue>(Func<TIssue, TNewIssue> selector) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(selector);
        return _state == ValidState
            ? Validation<T, TNewIssue>.Valid(_value)
            : new Validation<T, TNewIssue>(InvalidState, default!, ImmutableArray.CreateRange(_issues, selector));
    }

    /// <summary>
    /// Combines already computed outcomes; on failure accumulates the issues of every Invalid operand in operand
    /// order. Arguments are evaluated by the caller even when this outcome is Invalid.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This carrier or <paramref name="other"/> is uninitialized, even next to Invalid; checked before the selector.
    /// </exception>
    /// <exception cref="ArgumentNullException">The selector is null, regardless of the outcomes.</exception>
    public Validation<TResult, TIssue> Zip<TOther, TResult>(Validation<TOther, TIssue> other, Func<T, TOther, TResult> selector) {
        EnsureInitialized();
        other.EnsureInitialized();
        ArgumentNullException.ThrowIfNull(selector);
        return _state == ValidState && other._state == ValidState
            ? Validation<TResult, TIssue>.Valid(selector(_value, other._value))
            : Validation<TResult, TIssue>.FromIssues(Concat(IssuesOf(this), IssuesOf(other), default, default));
    }

    /// <inheritdoc cref="Zip{TOther, TResult}(Validation{TOther, TIssue}, Func{T, TOther, TResult})" path="/summary"/>
    /// <exception cref="InvalidOperationException">
    /// This carrier, <paramref name="b"/> or <paramref name="c"/> is uninitialized, checked in argument order before
    /// the selector, even next to Invalid.
    /// </exception>
    /// <exception cref="ArgumentNullException">The selector is null, regardless of the outcomes.</exception>
    public Validation<TResult, TIssue> Zip<TB, TC, TResult>(
        Validation<TB, TIssue> b,
        Validation<TC, TIssue> c,
        Func<T, TB, TC, TResult> selector) {
        EnsureInitialized();
        b.EnsureInitialized();
        c.EnsureInitialized();
        ArgumentNullException.ThrowIfNull(selector);
        return _state == ValidState && b._state == ValidState && c._state == ValidState
            ? Validation<TResult, TIssue>.Valid(selector(_value, b._value, c._value))
            : Validation<TResult, TIssue>.FromIssues(Concat(IssuesOf(this), IssuesOf(b), IssuesOf(c), default));
    }

    /// <inheritdoc cref="Zip{TOther, TResult}(Validation{TOther, TIssue}, Func{T, TOther, TResult})" path="/summary"/>
    /// <exception cref="InvalidOperationException">
    /// This carrier, <paramref name="b"/>, <paramref name="c"/> or <paramref name="d"/> is uninitialized, checked in
    /// argument order before the selector, even next to Invalid.
    /// </exception>
    /// <exception cref="ArgumentNullException">The selector is null, regardless of the outcomes.</exception>
    public Validation<TResult, TIssue> Zip<TB, TC, TD, TResult>(
        Validation<TB, TIssue> b,
        Validation<TC, TIssue> c,
        Validation<TD, TIssue> d,
        Func<T, TB, TC, TD, TResult> selector) {
        EnsureInitialized();
        b.EnsureInitialized();
        c.EnsureInitialized();
        d.EnsureInitialized();
        ArgumentNullException.ThrowIfNull(selector);
        return _state == ValidState && b._state == ValidState && c._state == ValidState && d._state == ValidState
            ? Validation<TResult, TIssue>.Valid(selector(_value, b._value, c._value, d._value))
            : Validation<TResult, TIssue>.FromIssues(Concat(IssuesOf(this), IssuesOf(b), IssuesOf(c), IssuesOf(d)));
    }

    /// <summary>
    /// Runs the dependent step only for Valid; Invalid keeps its issues. Independent checks accumulate through Zip,
    /// not through chained AndThen.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The carrier is uninitialized, checked before <paramref name="next"/>; or <paramref name="next"/> returned an
    /// uninitialized Validation. Nested carriers inside its payload are not checked.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="next"/> is null, regardless of the outcome.</exception>
    public Validation<TResult, TIssue> AndThen<TResult>(Func<T, Validation<TResult, TIssue>> next) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(next);
        if (_state != ValidState) {
            return Validation<TResult, TIssue>.FromIssues(_issues);
        }

        var result = next(_value);
        if (result._state == Uninitialized) {
            ThrowHelper.ThrowUninitializedReturnedValidation(nameof(next));
        }

        return result;
    }

    /// <summary>
    /// Replaces Invalid with the outcome returned by <paramref name="fallback"/>; the original issues are kept only
    /// if the fallback returns them.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The carrier is uninitialized, checked before <paramref name="fallback"/>; or <paramref name="fallback"/>
    /// returned an uninitialized Validation. Nested carriers inside its payload are not checked.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="fallback"/> is null, even for Valid.</exception>
    public Validation<T, TNewIssue> OrElse<TNewIssue>(Func<ImmutableArray<TIssue>, Validation<T, TNewIssue>> fallback) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(fallback);
        if (_state == ValidState) {
            return Validation<T, TNewIssue>.Valid(_value);
        }

        var result = fallback(_issues);
        if (result._state == Uninitialized) {
            ThrowHelper.ThrowUninitializedReturnedValidation(nameof(fallback));
        }

        return result;
    }

    /// <summary>
    /// Invalid outcomes are equal when their issues are equal position by position, independently of the backing
    /// storage.
    /// </summary>
    public bool Equals(Validation<T, TIssue> other) {
        if (_state != other._state) {
            return false;
        }

        return _state switch {
            ValidState => EqualityComparer<T>.Default.Equals(_value, other._value),
            InvalidState => IssuesEqual(_issues, other._issues),
            _ => true,
        };
    }

    public override bool Equals(object? obj) => obj is Validation<T, TIssue> other && Equals(other);

    public override int GetHashCode() => _state switch {
        ValidState => HashCode.Combine(ValidState, _value is null ? 0 : EqualityComparer<T>.Default.GetHashCode(_value)),
        InvalidState => IssuesHash(_issues),
        _ => 0,
    };

    public static bool operator ==(Validation<T, TIssue> left, Validation<T, TIssue> right) => left.Equals(right);

    public static bool operator !=(Validation<T, TIssue> left, Validation<T, TIssue> right) => !left.Equals(right);

    public override string ToString() => _state switch {
        ValidState => string.Concat("Validation.Valid(", Format(_value), ")"),
        InvalidState => FormatIssues(_issues),
        _ => "Validation.Uninitialized",
    };

    private static Validation<T, TIssue> FromIssues(ImmutableArray<TIssue> issues) => new(InvalidState, default!, issues);

    // A default array is the "no issues contributed" marker that the combining code tests with IsDefault;
    // an empty array would be a different value.
    private static ImmutableArray<TIssue> IssuesOf<TAny>(in Validation<TAny, TIssue> validation) =>
        validation._state == InvalidState ? validation._issues : default;

    private static ImmutableArray<TIssue> Concat(
        ImmutableArray<TIssue> a,
        ImmutableArray<TIssue> b,
        ImmutableArray<TIssue> c,
        ImmutableArray<TIssue> d) {
        ReadOnlySpan<ImmutableArray<TIssue>> parts = [a, b, c, d];
        int failed = 0;
        int total = 0;
        var single = default(ImmutableArray<TIssue>);
        foreach (var part in parts) {
            if (!part.IsDefault) {
                failed++;
                total += part.Length;
                single = part;
            }
        }

        if (failed == 1) {
            return single;
        }

        var builder = ImmutableArray.CreateBuilder<TIssue>(total);
        foreach (var part in parts) {
            if (!part.IsDefault) {
                builder.AddRange(part);
            }
        }

        return builder.MoveToImmutable();
    }

    private static bool IssuesEqual(ImmutableArray<TIssue> left, ImmutableArray<TIssue> right) {
        if (left.Length != right.Length) {
            return false;
        }

        var comparer = EqualityComparer<TIssue>.Default;
        for (int i = 0; i < left.Length; i++) {
            if (!comparer.Equals(left[i], right[i])) {
                return false;
            }
        }

        return true;
    }

    private static int IssuesHash(ImmutableArray<TIssue> issues) {
        var hash = default(HashCode);
        hash.Add(InvalidState);
        foreach (var issue in issues) {
            hash.Add(issue, EqualityComparer<TIssue>.Default);
        }

        return hash.ToHashCode();
    }

    private static string FormatIssues(ImmutableArray<TIssue> issues) {
        var text = new StringBuilder("Validation.Invalid([");
        for (int i = 0; i < issues.Length; i++) {
            if (i > 0) {
                text.Append(", ");
            }

            text.Append(Format(issues[i]));
        }

        return text.Append("])").ToString();
    }

    private static string Format<TPayload>(TPayload payload) =>
        payload is null ? "null" : payload.ToString() ?? string.Empty;

    private void EnsureInitialized() {
        if (_state == Uninitialized) {
            ThrowHelper.ThrowUninitializedValidation();
        }
    }
}
