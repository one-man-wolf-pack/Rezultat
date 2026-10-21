using Rezultat;
using Rezultat.Collections;

internal static class Case
{
    internal static object Observe(Result<string?, string>[] results) =>
        results.Sequence().Map(static values => values[0].Length);
}
