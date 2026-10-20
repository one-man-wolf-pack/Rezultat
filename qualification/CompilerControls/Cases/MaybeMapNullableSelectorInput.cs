using Rezultat;

internal static class Case
{
    internal static object Observe(Maybe<string?> maybe) => maybe.Map(static value => value.Length);
}
