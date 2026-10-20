using Rezultat;

internal static class Case
{
    internal static object Observe(Maybe<int> maybe) => maybe.Bind(static value => Maybe<int>.Some(value));
}
