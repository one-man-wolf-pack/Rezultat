using Rezultat;
using Rezultat.Collections;

internal static class Case
{
    internal static object Observe(Maybe<int> maybe) =>
        from value in maybe where value > 0 select value;
}
