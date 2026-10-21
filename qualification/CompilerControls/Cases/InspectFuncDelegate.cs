using System;
using Rezultat;

internal static class Case
{
    internal static object Observe(Result<int, string> result, Func<int, int> selector) => result.Inspect(selector);
}
