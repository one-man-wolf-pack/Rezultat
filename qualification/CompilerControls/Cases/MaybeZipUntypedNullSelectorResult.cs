using Rezultat;

internal static class Case
{
    internal static object Observe(Maybe<int> left, Maybe<int> right) => left.Zip(right, static (_, _) => null);
}
