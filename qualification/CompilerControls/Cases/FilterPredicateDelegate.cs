using Rezultat;

internal static class Case
{
    internal static object Observe(Maybe<int> maybe, Predicate<int> predicate) => maybe.Filter(predicate);
}
