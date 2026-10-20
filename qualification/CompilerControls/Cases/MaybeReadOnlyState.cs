using Rezultat;

internal static class Case
{
    internal static void Change(Maybe<int> maybe) => maybe.IsSome = true;
}
