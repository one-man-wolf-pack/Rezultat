using Rezultat;

internal static class Case
{
    internal static int Observe(Dictionary<string, string?> source) => source.GetOrNone("key").ValueOr("none").Length;
}
