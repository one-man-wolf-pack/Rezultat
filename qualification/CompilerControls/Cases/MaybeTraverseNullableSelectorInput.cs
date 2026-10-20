using Rezultat;

internal static class Case
{
    internal static object Observe(Maybe<string?> maybe) => maybe.Traverse(static value => Result<int, string>.Ok(value.Length));
}
