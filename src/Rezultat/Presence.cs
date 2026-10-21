using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Rezultat;

/// <summary>
/// Field presence: Missing (not transferred), Null (explicitly transferred null) or Value with a non-null
/// <typeparamref name="T"/> payload. <c>default</c> and <c>new()</c> are Missing; an omitted initialization is not
/// detected. Presence does not choose a PATCH action or whether Null is acceptable for a field.
/// </summary>
public readonly struct Presence<T> : IEquatable<Presence<T>>
    where T : notnull
{
    private const byte MissingState = 0;
    private const byte NullState = 1;
    private const byte ValueState = 2;

    private readonly T _value;
    private readonly byte _state;

    private Presence(byte state, T value)
    {
        _state = state;
        _value = value;
    }

    public static Presence<T> Missing => default;

    public static Presence<T> Null => new(NullState, default!);

    /// <summary>
    /// Keeps the exact non-null payload, including <c>0</c>, <see langword="false"/> and an empty string; a
    /// <see langword="null"/> argument never becomes Null or Missing.
    /// </summary>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="value"/> is <see langword="null"/> or an empty <see cref="Nullable{T}"/>, even when
    /// nullable warnings were bypassed.
    /// </exception>
    public static Presence<T> Value(T value)
    {
        if (value is null)
        {
            ThrowHelper.ThrowArgumentNull(nameof(value));
        }

        return new(ValueState, value);
    }

    public bool IsMissing => _state == MissingState;

    public bool IsNull => _state == NullState;

    public bool IsValue => _state == ValueState;

    /// <summary>
    /// Returns <see langword="true"/> only for Value; <see langword="false"/> alone does not tell Missing from Null.
    /// </summary>
    public bool TryGetValue([MaybeNullWhen(false), NotNullWhen(true)] out T value)
    {
        value = _value;
        return _state == ValueState;
    }

    /// <summary>
    /// Invokes only the handler of the current state, exactly once; its exceptions propagate unchanged.
    /// <paramref name="onNull"/> and <paramref name="onMissing"/> share a type, so swapping them still compiles.
    /// </summary>
    /// <exception cref="ArgumentNullException">Any handler is null, regardless of the state.</exception>
    public TResult Match<TResult>(Func<T, TResult> onValue, Func<TResult> onNull, Func<TResult> onMissing)
    {
        ArgumentNullException.ThrowIfNull(onValue);
        ArgumentNullException.ThrowIfNull(onNull);
        ArgumentNullException.ThrowIfNull(onMissing);
        return _state switch
        {
            ValueState => onValue(_value),
            NullState => onNull(),
            _ => onMissing(),
        };
    }

    /// <summary>
    /// Projects Value and keeps Missing and Null without invoking <paramref name="selector"/>. A nullable mapping
    /// policy is chosen by the caller through <see cref="Match{TResult}"/> or branching, not by Map.
    /// </summary>
    /// <exception cref="ArgumentNullException">The selector is null, even for Missing and Null.</exception>
    /// <exception cref="InvalidOperationException">
    /// The selector returned <see langword="null"/> or an empty <see cref="Nullable{T}"/>.
    /// </exception>
    public Presence<TResult> Map<TResult>(Func<T, TResult> selector)
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(selector);
        switch (_state)
        {
            case ValueState:
                var result = selector(_value);
                if (result is null)
                {
                    ThrowHelper.ThrowNullPresenceMapResult();
                }

                return new Presence<TResult>(ValueState, result);
            case NullState:
                return Presence<TResult>.Null;
            default:
                return Presence<TResult>.Missing;
        }
    }

    public bool Equals(Presence<T> other)
    {
        if (_state != other._state)
        {
            return false;
        }

        return _state != ValueState || EqualityComparer<T>.Default.Equals(_value, other._value);
    }

    public override bool Equals(object? obj) => obj is Presence<T> other && Equals(other);

    public override int GetHashCode() =>
        _state == ValueState ? HashCode.Combine(ValueState, EqualityComparer<T>.Default.GetHashCode(_value)) : _state;

    public static bool operator ==(Presence<T> left, Presence<T> right) => left.Equals(right);

    public static bool operator !=(Presence<T> left, Presence<T> right) => !left.Equals(right);

    public override string ToString() => _state switch
    {
        ValueState => string.Concat("Presence.Value(", _value.ToString() ?? string.Empty, ")"),
        NullState => "Presence.Null",
        _ => "Presence.Missing",
    };
}
