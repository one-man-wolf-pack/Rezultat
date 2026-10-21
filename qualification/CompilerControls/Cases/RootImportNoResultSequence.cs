using Rezultat;

internal static class Case
{
    internal static object Observe(Result<int, string>[] results) => results.Sequence();
}
