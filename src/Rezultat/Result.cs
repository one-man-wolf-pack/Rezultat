using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Rezultat;

/// <summary>
/// Exactly one outcome: success with <typeparamref name="T"/> or expected failure with
/// <typeparamref name="TError"/>. <c>default</c> and <c>new()</c> are an uninitialized carrier that
/// equals neither outcome; observing it as an outcome throws <see cref="InvalidOperationException"/>.
/// </summary>
public readonly struct Result<T, TError> : IEquatable<Result<T, TError>> {
    private const byte Uninitialized = 0;
    private const byte Success = 1;
    private const byte Failure = 2;

    private readonly T _value;
    private readonly TError _error;
    private readonly byte _state;

    private Result(byte state, T value, TError error) {
        _state = state;
        _value = value;
        _error = error;
    }

    public static Result<T, TError> Ok(T value) => new(Success, value, default!);

    public static Result<T, TError> Error(TError error) => new(Failure, default!, error);

    /// <summary>
    /// Safe carrier check: <see langword="false"/> only for <c>default</c>; never inspects the payload.
    /// </summary>
    public bool IsInitialized => _state != Uninitialized;

    /// <summary>
    /// Reports the branch without extracting or null-checking the payload.
    /// </summary>
    /// <exception cref="InvalidOperationException">The carrier is uninitialized.</exception>
    public bool IsSuccess {
        get {
            EnsureInitialized();
            return _state == Success;
        }
    }

    /// <summary>
    /// The returned value selects the branch regardless of the outputs' contents; the active output
    /// keeps the declared nullability of its payload, the inactive one receives <c>default</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The carrier is uninitialized.</exception>
    public bool TryGetValue([MaybeNullWhen(false)] out T value, [MaybeNullWhen(true)] out TError error) {
        EnsureInitialized();
        value = _value;
        error = _error;
        return _state == Success;
    }

    /// <summary>
    /// Invokes only the handler of the active branch, exactly once; its exceptions propagate unchanged.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The carrier is uninitialized; checked before the handlers, so null handlers do not change it.
    /// </exception>
    /// <exception cref="ArgumentNullException">Either handler is null, regardless of the active branch.</exception>
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<TError, TResult> onError) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onError);
        return _state == Success ? onSuccess(_value) : onError(_error);
    }

    /// <summary>
    /// Invokes <paramref name="action"/> once only on success and returns the carrier as it was before the call;
    /// reassigning the caller's variable from the action does not change the result, while a mutable payload is
    /// passed and kept by reference. The action has no async completion channel: an async lambda becomes
    /// <c>async void</c> and its faults are not observed here.
    /// </summary>
    /// <exception cref="InvalidOperationException">The carrier is uninitialized; checked before the action.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null, even on failure.</exception>
    public Result<T, TError> Inspect(Action<T> action) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(action);
        var snapshot = this;
        if (snapshot._state == Success) {
            action(snapshot._value);
        }

        return snapshot;
    }

    /// <summary>
    /// Invokes <paramref name="action"/> once only on failure and returns the carrier as it was before the call;
    /// reassigning the caller's variable from the action does not change the result, while a mutable payload is
    /// passed and kept by reference. The action has no async completion channel: an async lambda becomes
    /// <c>async void</c> and its faults are not observed here.
    /// </summary>
    /// <exception cref="InvalidOperationException">The carrier is uninitialized; checked before the action.</exception>
    /// <exception cref="ArgumentNullException"><paramref name="action"/> is null, even on success.</exception>
    public Result<T, TError> InspectError(Action<TError> action) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(action);
        var snapshot = this;
        if (snapshot._state == Failure) {
            action(snapshot._error);
        }

        return snapshot;
    }

    /// <exception cref="InvalidOperationException">The carrier is uninitialized; checked before the selector.</exception>
    /// <exception cref="ArgumentNullException">The selector is null, regardless of the active branch.</exception>
    public Result<TResult, TError> Map<TResult>(Func<T, TResult> selector) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(selector);
        return _state == Success ? Result<TResult, TError>.Ok(selector(_value)) : Result<TResult, TError>.Error(_error);
    }

    /// <summary>
    /// Translates the error and stays on the failure branch; this is not recovery into success.
    /// </summary>
    /// <exception cref="InvalidOperationException">The carrier is uninitialized; checked before the selector.</exception>
    /// <exception cref="ArgumentNullException">The selector is null, regardless of the active branch.</exception>
    public Result<T, TNewError> MapError<TNewError>(Func<TError, TNewError> selector) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(selector);
        return _state == Success ? Result<T, TNewError>.Ok(_value) : Result<T, TNewError>.Error(selector(_error));
    }

    /// <summary>
    /// Passes <paramref name="state"/> to the selector after the error; the state is evaluated by the caller
    /// even on success and is not checked for null.
    /// </summary>
    /// <exception cref="InvalidOperationException">The carrier is uninitialized; checked before the selector.</exception>
    /// <exception cref="ArgumentNullException">The selector is null, regardless of the active branch.</exception>
    public Result<T, TNewError> MapError<TNewError, TState>(TState state, Func<TError, TState, TNewError> selector) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(selector);
        return _state == Success ? Result<T, TNewError>.Ok(_value) : Result<T, TNewError>.Error(selector(_error, state));
    }

    /// <exception cref="InvalidOperationException">
    /// The carrier is uninitialized, checked before <paramref name="next"/>; or <paramref name="next"/> returned an
    /// uninitialized Result. Nested carriers inside its payload are not checked.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="next"/> is null, regardless of the active branch.</exception>
    public Result<TResult, TError> AndThen<TResult>(Func<T, Result<TResult, TError>> next) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(next);
        if (_state != Success) {
            return Result<TResult, TError>.Error(_error);
        }

        var result = next(_value);
        if (result._state == Uninitialized) {
            ThrowHelper.ThrowUninitializedReturnedResult(nameof(next));
        }

        return result;
    }

    /// <summary>
    /// Keeps success when <paramref name="predicate"/> holds; otherwise fails with the error created from the same
    /// value. An existing failure is kept without invoking either callback.
    /// </summary>
    /// <exception cref="InvalidOperationException">The carrier is uninitialized; checked before the callbacks.</exception>
    /// <exception cref="ArgumentNullException">
    /// A callback is null, regardless of the active branch; <paramref name="predicate"/> is checked first.
    /// </exception>
    public Result<T, TError> Ensure(Func<T, bool> predicate, Func<T, TError> errorFactory) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(predicate);
        ArgumentNullException.ThrowIfNull(errorFactory);
        if (_state != Success) {
            return this;
        }

        var value = _value;
        return predicate(value) ? Ok(value) : Error(errorFactory(value));
    }

    /// <summary>
    /// Replaces a failure with the outcome returned by <paramref name="fallback"/>; the original error is not kept.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The carrier is uninitialized, checked before <paramref name="fallback"/>; or <paramref name="fallback"/>
    /// returned an uninitialized Result. Nested carriers inside its payload are not checked.
    /// </exception>
    /// <exception cref="ArgumentNullException"><paramref name="fallback"/> is null, regardless of the active branch.</exception>
    public Result<T, TNewError> OrElse<TNewError>(Func<TError, Result<T, TNewError>> fallback) {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(fallback);
        if (_state == Success) {
            return Result<T, TNewError>.Ok(_value);
        }

        var result = fallback(_error);
        if (result._state == Uninitialized) {
            ThrowHelper.ThrowUninitializedReturnedResult(nameof(fallback));
        }

        return result;
    }

    /// <summary>
    /// Combines two already computed outcomes; on failure keeps the error of the first failed operand in operand
    /// order. <paramref name="other"/> is evaluated by the caller even when this outcome is a failure.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// This carrier or <paramref name="other"/> is uninitialized, even when this outcome is a failure; checked before
    /// the selector.
    /// </exception>
    /// <exception cref="ArgumentNullException">The selector is null, regardless of the branches.</exception>
    public Result<TResult, TError> Zip<TOther, TResult>(Result<TOther, TError> other, Func<T, TOther, TResult> selector) {
        EnsureInitialized();
        other.EnsureInitialized();
        ArgumentNullException.ThrowIfNull(selector);
        if (_state != Success) {
            return Result<TResult, TError>.Error(_error);
        }

        return other._state == Success
            ? Result<TResult, TError>.Ok(selector(_value, other._value))
            : Result<TResult, TError>.Error(other._error);
    }

    public bool Equals(Result<T, TError> other) {
        if (_state != other._state) {
            return false;
        }

        return _state switch {
            Success => EqualityComparer<T>.Default.Equals(_value, other._value),
            Failure => EqualityComparer<TError>.Default.Equals(_error, other._error),
            _ => true,
        };
    }

    public override bool Equals(object? obj) => obj is Result<T, TError> other && Equals(other);

    public override int GetHashCode() => _state switch {
        Success => HashCode.Combine(Success, _value is null ? 0 : EqualityComparer<T>.Default.GetHashCode(_value)),
        Failure => HashCode.Combine(Failure, _error is null ? 0 : EqualityComparer<TError>.Default.GetHashCode(_error)),
        _ => 0,
    };

    public static bool operator ==(Result<T, TError> left, Result<T, TError> right) => left.Equals(right);

    public static bool operator !=(Result<T, TError> left, Result<T, TError> right) => !left.Equals(right);

    public override string ToString() => _state switch {
        Success => string.Concat("Result.Ok(", Format(_value), ")"),
        Failure => string.Concat("Result.Error(", Format(_error), ")"),
        _ => "Result.Uninitialized",
    };

    private static string Format<TPayload>(TPayload payload) =>
        payload is null ? "null" : payload.ToString() ?? string.Empty;

    private void EnsureInitialized() {
        if (_state == Uninitialized) {
            ThrowHelper.ThrowUninitializedResult();
        }
    }
}
