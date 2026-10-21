using Rezultat;
using Rezultat.Linq;

internal static class Case
{
    internal static object Observe(Maybe<string?> maybe) =>
        maybe.Where(static value => value is not null).Map(static value => value.Length);
}
