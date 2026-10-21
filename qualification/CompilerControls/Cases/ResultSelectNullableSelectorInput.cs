using Rezultat;
using Rezultat.Linq;

internal static class Case
{
    internal static object Observe(Result<string?, string> result) =>
        result.Select(static value => value.Length);
}
