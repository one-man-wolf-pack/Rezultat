using Rezultat;

internal static class Case
{
    internal static object Observe(Maybe<int> maybe) => maybe.Where<int>(static value => value > 0);
}
