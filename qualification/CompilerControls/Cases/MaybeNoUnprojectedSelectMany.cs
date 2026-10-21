using Rezultat;
using Rezultat.Linq;

internal static class Case
{
    internal static object Observe(Maybe<int> maybe) =>
        MaybeQueryExtensions.SelectMany(maybe, static value => Maybe<int>.Some(value));
}
