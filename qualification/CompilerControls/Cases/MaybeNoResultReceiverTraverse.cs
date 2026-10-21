using Rezultat;
using Rezultat.Collections;

internal static class Case
{
    internal static object Observe(Result<int, string> result) => result.Traverse<int, int>(static value => Maybe<int>.Some(value));
}
