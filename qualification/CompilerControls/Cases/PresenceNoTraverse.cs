using Rezultat;

internal static class Case
{
    internal static object Observe(Presence<int>[] items) => items.Traverse(static item => item);
}
