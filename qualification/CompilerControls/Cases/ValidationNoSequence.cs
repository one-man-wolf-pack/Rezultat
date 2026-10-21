using Rezultat;

internal static class Case
{
    internal static object Observe(Validation<int, string>[] items) => items.Sequence();
}
