using Rezultat;

internal static class Case
{
    internal static int Observe(Maybe<string?> maybe) =>
        maybe.TryGetValue(out var value) ? value.Length : 0;
}
