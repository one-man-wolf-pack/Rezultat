using Rezultat;

internal static class Case
{
    internal static object Observe(Maybe<int>[] maybes) => maybes.Sequence();
}
