using Rezultat;
using Rezultat.Linq;

internal static class Case
{
    internal static object Observe(Presence<int> presence) =>
        from value in presence select value + 1;
}
