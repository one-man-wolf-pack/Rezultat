using Rezultat;

internal static class Case
{
    internal static object Observe(Maybe<int> maybe) => maybe.OrElse(static async () => { await Task.Yield(); return Maybe<int>.None; });
}
