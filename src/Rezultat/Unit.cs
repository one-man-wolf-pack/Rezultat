using System;

namespace Rezultat;

/// <summary>
/// The single value of an outcome without content. <c>default</c>, <c>new()</c> and
/// <see cref="Value"/> are the same valid value; only a boxed Rezultat <see cref="Unit"/> equals it.
/// </summary>
public readonly struct Unit : IEquatable<Unit>
{
    public static Unit Value => default;

    public bool Equals(Unit other) => true;

    public override bool Equals(object? other) => other is Unit;

    public override int GetHashCode() => 0;

    public static bool operator ==(Unit left, Unit right) => true;

    public static bool operator !=(Unit left, Unit right) => false;

    public override string ToString() => "()";
}
