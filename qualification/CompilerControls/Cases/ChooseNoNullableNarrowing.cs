using Rezultat;
using Rezultat.Collections;

internal static class Case
{
    internal static int Observe(string?[] items) => items.Choose(static value => Maybe<string?>.Some(value)).First().Length;
}
