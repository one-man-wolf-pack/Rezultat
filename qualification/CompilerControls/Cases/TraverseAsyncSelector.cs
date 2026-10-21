using Rezultat;
using Rezultat.Collections;

internal static class Case
{
    internal static object Observe(int[] source) => source.Traverse(static async value => { await Task.Yield(); return Result<int, string>.Ok(value); });
}
