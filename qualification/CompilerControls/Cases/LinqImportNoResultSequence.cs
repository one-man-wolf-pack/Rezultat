using Rezultat;
using Rezultat.Linq;

internal static class Case
{
    internal static object Observe(Result<int, string>[] results) =>
        results.Sequence();
}
