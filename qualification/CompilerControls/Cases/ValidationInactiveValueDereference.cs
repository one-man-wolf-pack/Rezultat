using Rezultat;

internal static class Case
{
    internal static int Observe(Validation<string, string> v) { v.TryGetValue(out var value, out _); return value.Length; }
}
