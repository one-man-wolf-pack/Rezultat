using Rezultat;
using Rezultat.Collections;

internal static class Case
{
    internal static int Observe(string?[] items) => items.FirstOrNone().ValueOr("none").Length;
}
