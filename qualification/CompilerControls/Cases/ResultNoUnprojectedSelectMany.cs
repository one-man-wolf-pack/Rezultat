using Rezultat;
using Rezultat.Linq;

internal static class Case
{
    internal static object Observe(Result<int, string> result) =>
        ResultQueryExtensions.SelectMany(result, static value => Result<int, string>.Ok(value));
}
