using Rezultat;
using Rezultat.Linq;

internal static class Case
{
    internal static object Observe(Result<int, string> result) =>
        from value in result where value > 0 select value;
}
