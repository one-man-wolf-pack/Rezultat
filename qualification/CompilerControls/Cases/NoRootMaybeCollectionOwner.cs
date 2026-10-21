using Rezultat;
using Rezultat.Collections;

internal static class Case
{
    internal static object Observe(int[] items) => MaybeExtensions.FirstOrNone<int>(items);
}
