using Rezultat;

internal static class Case
{
    internal static string Observe(Validation<int, string> v) => v.Match<string?>(static _ => null, static _ => "x");
}
