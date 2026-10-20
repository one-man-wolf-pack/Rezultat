using Rezultat;

internal static class Case
{
    internal static object Observe(Result<int, string> result) => result.AndThen(static async value => { await Task.Yield(); return Result<int, string>.Ok(value); });
}
