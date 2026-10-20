using Rezultat;

internal static class Case
{
    internal static int Observe(Maybe<int> maybe) => maybe.ValueOrElse(1, static state => state);
}
