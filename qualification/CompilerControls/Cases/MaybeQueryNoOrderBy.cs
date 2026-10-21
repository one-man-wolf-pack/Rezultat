using Rezultat;
using Rezultat.Linq;

internal static class Case
{
    internal static object Observe(Maybe<int> maybe) =>
        from value in maybe orderby value select value;
}
