using Rezultat;

internal static class Case
{
    internal static object Observe(Maybe<int> maybe) => from value in maybe select value + 1;
}
