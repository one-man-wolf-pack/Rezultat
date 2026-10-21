using Rezultat;
using Rezultat.Linq;

internal static class Case
{
    internal static object Observe(Result<int, string> first, Result<int, int> second) =>
        from x in first from y in second select x + y;
}
