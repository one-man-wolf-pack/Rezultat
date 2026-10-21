using Rezultat;
using Rezultat.Collections;

internal static class Case
{
    internal static object Observe(int[] items) => items.Choose(static _ => default);
}
