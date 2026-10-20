using Rezultat;

internal static class Case
{
    internal static object Observe(Result<int, string> left, Result<int, int> right) => left.Zip(right, static (a, b) => a + b);
}
