using Rezultat;

internal static class Case
{
    internal static object Observe(Validation<int, string> v) => v.Zip(v, v, v, v, static (a, b, c, d, e) => a + b + c + d + e);
}
