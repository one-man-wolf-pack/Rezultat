using Rezultat;

internal static class Case
{
    internal static object Observe(Result<int, string> left, Result<int, string> right) => left.Zip(right, static (_, _) => null);
}
