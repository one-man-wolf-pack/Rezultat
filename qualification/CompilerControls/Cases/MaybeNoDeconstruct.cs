using Rezultat;

internal static class Case
{
    internal static int Observe(Maybe<int> maybe)
    {
        var (isSome, value) = maybe;
        return isSome ? value : 0;
    }
}
