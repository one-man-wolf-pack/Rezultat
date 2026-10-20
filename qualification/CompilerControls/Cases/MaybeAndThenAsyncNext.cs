using Rezultat;

internal static class Case
{
    internal static object Observe(Maybe<int> maybe) => maybe.AndThen(static async value => { await Task.Yield(); return Maybe<int>.Some(value); });
}
