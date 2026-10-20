using Rezultat;

internal static class Case
{
    internal static object Observe(Maybe<int> maybe) => maybe.Or(Maybe<int>.Some(1));
}
