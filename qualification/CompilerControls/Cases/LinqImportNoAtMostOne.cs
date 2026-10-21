using Rezultat;
using Rezultat.Linq;

internal static class Case
{
    internal static object Observe(int[] items) =>
        items.AtMostOne(static () => "many");
}
