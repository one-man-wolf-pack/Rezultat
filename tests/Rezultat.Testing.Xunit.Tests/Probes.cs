namespace Rezultat.Testing.Xunit.Tests;

public sealed class PoisonException : Exception {
    public int MessageReads { get; private set; }

    public int TextReads { get; private set; }

    public override string Message {
        get {
            MessageReads++;
            throw new InvalidOperationException("exception message must not be read");
        }
    }

    public override string ToString() {
        TextReads++;
        // S3877: poison text; reading it is the failure the tests rule out.
#pragma warning disable S3877
        throw new InvalidOperationException("exception text must not be read");
#pragma warning restore S3877
    }
}

internal sealed class Probe(string? text = "probe") {
    public int Calls { get; private set; }

    public Exception? Throws { get; init; }

    public override bool Equals(object? obj) {
        Calls++;
        return ReferenceEquals(this, obj);
    }

    public override int GetHashCode() {
        Calls++;
        return 0;
    }

    public override string? ToString() {
        Calls++;
        return Throws is null ? text : throw Throws;
    }
}

internal readonly struct ValueProbe(int id) {
    [ThreadStatic]
    private static int _calls;

    public static int Calls => _calls;

    public int Id { get; } = id;

    public static void Reset() => _calls = 0;

    public override bool Equals(object? obj) {
        _calls++;
        return obj is ValueProbe other && other.Id == Id;
    }

    public override int GetHashCode() {
        _calls++;
        return Id;
    }

    public override string ToString() {
        _calls++;
        return $"value{Id}";
    }
}

internal sealed class CultureScope : IDisposable {
    private readonly System.Globalization.CultureInfo _previous = System.Globalization.CultureInfo.CurrentCulture;

    public CultureScope(string name) => System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo(name);

    public void Dispose() => System.Globalization.CultureInfo.CurrentCulture = _previous;
}
