using Rezultat;

internal static class Case
{
    internal static object Observe(Maybe<int> maybe) => maybe.Map(static value => value + 1);
}
