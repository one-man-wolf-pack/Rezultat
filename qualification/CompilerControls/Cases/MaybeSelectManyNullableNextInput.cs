using Rezultat;
using Rezultat.Linq;

internal static class Case
{
    internal static object Observe(Maybe<string?> maybe) =>
        maybe.SelectMany(static value => Maybe<int>.Some(value.Length), static (_, length) => length);
}
