using Rezultat;

internal static class Case
{
    internal static object Observe(int[] items) => items.Traverse<int, int, string>(null!);
}
