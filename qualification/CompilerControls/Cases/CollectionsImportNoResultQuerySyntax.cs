using Rezultat;
using Rezultat.Collections;

internal static class Case
{
    internal static object Observe(Result<int, string> result) =>
        from value in result select value + 1;
}
