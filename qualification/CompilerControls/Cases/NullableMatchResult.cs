using Rezultat;

internal static class Case
{
    internal static string Observe(Result<int, int> result) => result.Match<string?>(_ => null, _ => null);
}
