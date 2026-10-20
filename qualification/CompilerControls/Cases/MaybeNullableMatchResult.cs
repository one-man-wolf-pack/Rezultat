using Rezultat;

internal static class Case
{
    internal static string Observe(Maybe<int> maybe) =>
        maybe.Match<string?>(_ => "some", () => null);
}
