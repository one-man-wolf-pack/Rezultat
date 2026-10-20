using Rezultat;

internal static class Case
{
    internal static int Observe(Result<string, string> result) =>
        result.TryGetValue(out _, out var error) ? error.Length : 0;
}
