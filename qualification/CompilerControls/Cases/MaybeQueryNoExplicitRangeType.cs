using Rezultat;
using Rezultat.Linq;

internal static class Case
{
    internal static object Observe(Maybe<int> maybe) =>
        from int value in maybe select value;
}
