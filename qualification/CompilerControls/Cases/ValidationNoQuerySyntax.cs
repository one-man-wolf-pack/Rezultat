using Rezultat;

internal static class Case
{
    internal static object Observe(Validation<int, string> v) => from x in v select x + 1;
}
