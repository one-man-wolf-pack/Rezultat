using Rezultat;

internal static class Case
{
    internal static object Observe(Validation<int, string> v) => v.OrElse(static _ => default);
}
