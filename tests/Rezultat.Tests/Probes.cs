namespace Rezultat.Tests;

public sealed class ProbeException(string message) : Exception(message);

internal sealed class ReferenceProbe(int id, string? text = null) {
    public int Id { get; } = id;

    public int EqualsCalls { get; private set; }

    public int HashCalls { get; private set; }

    public int ToStringCalls { get; private set; }

    public bool ThrowOnEquals { get; init; }

    public bool ThrowOnHash { get; init; }

    public bool ThrowOnToString { get; init; }

    public bool ReturnNullText { get; init; }

    public override bool Equals(object? obj) {
        EqualsCalls++;
        if (ThrowOnEquals) {
            // S3877: an opt-in poison payload; the test asserts this failure propagates or is never reached.
#pragma warning disable S3877
            throw new ProbeException("equals");
#pragma warning restore S3877
        }

        return obj is ReferenceProbe other && other.Id == Id;
    }

    public override int GetHashCode() {
        HashCalls++;
        if (ThrowOnHash) {
            // S3877: an opt-in poison payload; the test asserts this failure propagates or is never reached.
#pragma warning disable S3877
            throw new ProbeException("hash");
#pragma warning restore S3877
        }

        return Id;
    }

    public override string? ToString() {
        ToStringCalls++;
        if (ThrowOnToString) {
            // S3877: an opt-in poison payload; the test asserts this failure propagates or is never reached.
#pragma warning disable S3877
            throw new ProbeException("format");
#pragma warning restore S3877
        }

        return ReturnNullText ? null : text ?? $"probe{Id}";
    }
}

internal readonly struct ValueProbe(int id) : IEquatable<ValueProbe> {
    [ThreadStatic]
    private static int _touches;

    public int Id { get; } = id;

    public static int Touches => _touches;

    public static void Reset() => _touches = 0;

    public bool Equals(ValueProbe other) {
        _touches++;
        return other.Id == Id;
    }

    public override bool Equals(object? obj) => obj is ValueProbe other && Equals(other);

    public override int GetHashCode() {
        _touches++;
        return Id;
    }

    public override string ToString() {
        _touches++;
        return $"value{Id}";
    }
}
