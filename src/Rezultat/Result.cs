using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Rezultat;

/// <summary>
/// Exactly one outcome: success with <typeparamref name="T"/> or expected failure with
/// <typeparamref name="TError"/>. <c>default</c> and <c>new()</c> are an uninitialized carrier that
/// equals neither outcome; observing it as an outcome throws <see cref="InvalidOperationException"/>.
/// </summary>
public readonly struct Result<T, TError> : IEquatable<Result<T, TError>>
{
    private const byte Uninitialized = 0;
    private const byte Success = 1;
    private const byte Failure = 2;

    private readonly T _value;
    private readonly TError _error;
    private readonly byte _state;

    private Result(byte state, T value, TError error)
    {
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
    public bool IsSuccess
    {
        get
        {
            EnsureInitialized();
            return _state == Success;
        }
    }

    /// <summary>
    /// The returned value selects the branch regardless of the outputs' contents; the active output
    /// keeps the declared nullability of its payload, the inactive one receives <c>default</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">The carrier is uninitialized.</exception>
    public bool TryGetValue([MaybeNullWhen(false)] out T value, [MaybeNullWhen(true)] out TError error)
    {
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
    public TResult Match<TResult>(Func<T, TResult> onSuccess, Func<TError, TResult> onError)
    {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onError);
        return _state == Success ? onSuccess(_value) : onError(_error);
    }

    public bool Equals(Result<T, TError> other)
    {
        if (_state != other._state)
        {
            return false;
        }

        return _state switch
        {
            Success => EqualityComparer<T>.Default.Equals(_value, other._value),
            Failure => EqualityComparer<TError>.Default.Equals(_error, other._error),
            _ => true,
        };
    }

    public override bool Equals(object? obj) => obj is Result<T, TError> other && Equals(other);

    public override int GetHashCode() => _state switch
    {
        Success => HashCode.Combine(Success, _value is null ? 0 : EqualityComparer<T>.Default.GetHashCode(_value)),
        Failure => HashCode.Combine(Failure, _error is null ? 0 : EqualityComparer<TError>.Default.GetHashCode(_error)),
        _ => 0,
    };

    public static bool operator ==(Result<T, TError> left, Result<T, TError> right) => left.Equals(right);

    public static bool operator !=(Result<T, TError> left, Result<T, TError> right) => !left.Equals(right);

    public override string ToString() => _state switch
    {
        Success => string.Concat("Result.Ok(", Format(_value), ")"),
        Failure => string.Concat("Result.Error(", Format(_error), ")"),
        _ => "Result.Uninitialized",
    };

    private static string Format<TPayload>(TPayload payload) =>
        payload is null ? "null" : payload.ToString() ?? string.Empty;

    private void EnsureInitialized()
    {
        if (_state == Uninitialized)
        {
            ThrowHelper.ThrowUninitializedResult();
        }
    }
}
