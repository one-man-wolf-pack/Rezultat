using Rezultat;

internal static class Case
{
    internal static object Observe(Validation<int, string> a, Validation<int, int> b) => a.Zip(b, static (x, y) => x + y);
}
