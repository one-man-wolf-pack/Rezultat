using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Rezultat;

/// <summary>
/// Either None or Some with a <typeparamref name="T"/> payload. <c>default</c> and <c>new()</c> are None;
/// Some keeps any payload as present, including <see langword="null"/> or an empty <see cref="Nullable{T}"/>.
/// </summary>
public readonly struct Maybe<T> : IEquatable<Maybe<T>> {
    private readonly T _value;
    private readonly bool _isSome;

    private Maybe(T value) {
        _value = value;
        _isSome = true;
    }

    /// <summary>
    /// Keeps <paramref name="value"/> as present even when it is <see langword="null"/>; use
    /// <c>Maybe.FromNullable</c> to turn <see langword="null"/> into None.
    /// </summary>
    public static Maybe<T> Some(T value) => new(value);

    public static Maybe<T> None => default;

    /// <summary>
    /// Reports Some without extracting the payload; it does not promise a non-null payload.
    /// </summary>
    public bool IsSome => _isSome;

    /// <summary>
    /// The returned value reports Some regardless of the payload; on Some the output keeps the declared nullability
    /// of <typeparamref name="T"/>, on None it receives <c>default</c>.
    /// </summary>
    public bool TryGetValue([MaybeNullWhen(false)] out T value) {
        value = _value;
        return _isSome;
    }

    /// <summary>
    /// Invokes only the handler of the current alternative, exactly once; its exceptions propagate unchanged.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// Either handler is null, regardless of the alternative; <paramref name="onSome"/> is checked first.
    /// </exception>
    public TResult Match<TResult>(Func<T, TResult> onSome, Func<TResult> onNone) {
        ArgumentNullException.ThrowIfNull(onSome);
        ArgumentNullException.ThrowIfNull(onNone);
        return _isSome ? onSome(_value) : onNone();
    }

    public T ValueOr(T fallback) => _isSome ? _value : fallback;

    /// <summary>
    /// Invokes <paramref name="fallback"/> exactly once only for None; its exceptions propagate unchanged.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="fallback"/> is null, even for Some.</exception>
    public T ValueOrElse(Func<T> fallback) {
        ArgumentNullException.ThrowIfNull(fallback);
        return _isSome ? _value : fallback();
    }

    /// <exception cref="ArgumentNullException">The selector is null, even for None.</exception>
    public Maybe<TResult> Map<TResult>(Func<T, TResult> selector) {
        ArgumentNullException.ThrowIfNull(selector);
        return _isSome ? Maybe<TResult>.Some(selector(_value)) : Maybe<TResult>.None;
    }

    /// <summary>
    /// Returns the Maybe chosen by <paramref name="next"/> as is; a <c>default</c> result is a valid None.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="next"/> is null, even for None.</exception>
    public Maybe<TResult> AndThen<TResult>(Func<T, Maybe<TResult>> next) {
        ArgumentNullException.ThrowIfNull(next);
        return _isSome ? next(_value) : Maybe<TResult>.None;
    }

    /// <summary>
    /// Keeps this Some, including a present <see langword="null"/>; for None returns the result of
    /// <paramref name="fallback"/> as is, which may itself be None.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="fallback"/> is null, even for Some.</exception>
    public Maybe<T> OrElse(Func<Maybe<T>> fallback) {
        ArgumentNullException.ThrowIfNull(fallback);
        return _isSome ? this : fallback();
    }

    /// <summary>
    /// On <see langword="true"/> returns the Some observed before <paramref name="predicate"/> ran, so
    /// reassigning the receiver variable from the predicate does not change the result.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is null, even for None.</exception>
    public Maybe<T> Filter(Func<T, bool> predicate) {
        ArgumentNullException.ThrowIfNull(predicate);
        var self = this;
        return self._isSome && predicate(self._value) ? self : None;
    }

    /// <summary>
    /// Combines two already computed Maybe values; <paramref name="other"/> is evaluated by the caller even when
    /// this is None. Use <see cref="AndThen{TResult}"/> to compute the second value only for Some.
    /// </summary>
    /// <exception cref="ArgumentNullException">The selector is null, regardless of the alternatives.</exception>
    public Maybe<TResult> Zip<TOther, TResult>(Maybe<TOther> other, Func<T, TOther, TResult> selector) {
        ArgumentNullException.ThrowIfNull(selector);
        return _isSome && other.TryGetValue(out var otherValue)
            ? Maybe<TResult>.Some(selector(_value, otherValue))
            : Maybe<TResult>.None;
    }

    /// <summary>
    /// Keeps <paramref name="error"/> as an ordinary payload, never invoking it even when it is a delegate.
    /// </summary>
    public Result<T, TError> ToResult<TError>(TError error) =>
        _isSome ? Result<T, TError>.Ok(_value) : Result<T, TError>.Error(error);

    /// <exception cref="ArgumentNullException"><paramref name="errorFactory"/> is null, even for Some.</exception>
    public Result<T, TError> ToResultElse<TError>(Func<TError> errorFactory) {
        ArgumentNullException.ThrowIfNull(errorFactory);
        return _isSome ? Result<T, TError>.Ok(_value) : Result<T, TError>.Error(errorFactory());
    }

    public bool Equals(Maybe<T> other) {
        if (_isSome != other._isSome) {
            return false;
        }

        return !_isSome || EqualityComparer<T>.Default.Equals(_value, other._value);
    }

    public override bool Equals(object? obj) => obj is Maybe<T> other && Equals(other);

    public override int GetHashCode() {
        if (!_isSome) {
            return 0;
        }

        return HashCode.Combine(1, _value is null ? 0 : EqualityComparer<T>.Default.GetHashCode(_value));
    }

    public static bool operator ==(Maybe<T> left, Maybe<T> right) => left.Equals(right);

    public static bool operator !=(Maybe<T> left, Maybe<T> right) => !left.Equals(right);

    public override string ToString() {
        if (!_isSome) {
            return "Maybe.None";
        }

        return string.Concat("Maybe.Some(", _value is null ? "null" : _value.ToString() ?? string.Empty, ")");
    }
}

/// <summary>
/// Explicit nullable adaptation: a <see langword="null"/> input becomes None and the result drops the outer
/// nullable level, intentionally losing the difference between a present <see langword="null"/> and absence.
/// </summary>
public static class Maybe {
    public static Maybe<T> FromNullable<T>(T? value)
        where T : class =>
        value is null ? Maybe<T>.None : Maybe<T>.Some(value);

    public static Maybe<T> FromNullable<T>(T? value)
        where T : struct =>
        value.HasValue ? Maybe<T>.Some(value.GetValueOrDefault()) : Maybe<T>.None;
}
