using Rezultat;

internal static class Case
{
    internal static object Observe(Dictionary<string, int> source) => source.GetOrNone("key");
}
