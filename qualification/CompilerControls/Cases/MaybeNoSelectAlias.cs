using Rezultat;

internal static class Case
{
    internal static object Observe(Maybe<int> maybe) => maybe.Select<int, int>(static value => value + 1);
}
