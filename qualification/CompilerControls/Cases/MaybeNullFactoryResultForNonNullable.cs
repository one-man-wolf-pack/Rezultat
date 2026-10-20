using Rezultat;

internal static class Case
{
    internal static string Observe(Maybe<string> maybe) => maybe.ValueOrElse(() => null);
}
