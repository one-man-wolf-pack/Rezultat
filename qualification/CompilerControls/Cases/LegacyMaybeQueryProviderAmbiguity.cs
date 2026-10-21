using Rezultat;
using Rezultat.Linq;
using Recipes;

internal static class Case
{
    internal static object Observe(Maybe<int> maybe) =>
        from value in maybe where value > 0 select value;
}

namespace Recipes
{
    internal static class MaybeQuery
    {
        internal static Maybe<T> Where<T>(this Maybe<T> source, Func<T, bool> predicate) => source.Filter(predicate);
    }
}
