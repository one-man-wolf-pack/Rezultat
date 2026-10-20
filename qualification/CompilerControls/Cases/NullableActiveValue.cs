using Rezultat;

internal static class Case
{
    internal static int Observe(Result<string?, int> result) =>
        result.TryGetValue(out var value, out _) ? value.Length : 0;
}
