using Rezultat;

internal static class Case
{
    internal static object Observe(Maybe<string?> maybe) => maybe.Filter(static value => value is not null).Map(static value => value.Length);
}
