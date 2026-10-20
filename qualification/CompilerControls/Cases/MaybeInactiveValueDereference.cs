using Rezultat;

internal static class Case
{
    internal static int Observe(Maybe<string> maybe) =>
        maybe.TryGetValue(out var value) ? 0 : value.Length;
}
