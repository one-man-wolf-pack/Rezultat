using Rezultat;

internal static class Case
{
    internal static int Observe(Result<string, string> result) =>
        result.TryGetValue(out var value, out _) ? 0 : value.Length;
}
