using Rezultat;
using Rezultat.Collections;

internal static class Case
{
    internal static object Observe(string?[] items) =>
        items.AtMostOne(static () => "many").Map(static found => found.Map(static value => value.Length));
}
