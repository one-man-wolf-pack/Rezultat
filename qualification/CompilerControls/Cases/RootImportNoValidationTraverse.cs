using Rezultat;

internal static class Case
{
    internal static object Observe(int[] items) => items.Traverse(static item => Validation<int, string>.Valid(item));
}
