using Rezultat;
using Rezultat.Collections;

internal static class Case
{
    internal static object Observe(int[] items) => items.Traverse(static async item => { await System.Threading.Tasks.Task.Yield(); return Validation<int, string>.Valid(item); });
}
