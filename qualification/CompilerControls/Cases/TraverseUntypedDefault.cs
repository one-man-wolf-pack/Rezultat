using Rezultat;

internal static class Case
{
    internal static object Observe(int[] source) => source.Traverse(static _ => default);
}
