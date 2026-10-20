using Rezultat;

internal static class Case
{
    internal static int Observe(Maybe<string?> maybe) =>
        maybe.Match(value => value.Length, () => 0);
}
