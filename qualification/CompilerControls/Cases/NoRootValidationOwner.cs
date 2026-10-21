using Rezultat;
using Rezultat.Collections;

internal static class Case
{
    internal static object Observe(int[] items) => ValidationExtensions.Traverse(items, static item => Validation<int, string>.Valid(item));
}
