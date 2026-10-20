using Rezultat;

internal static class Case
{
    internal static Result<int, string> Observe(Result<string?, string> result) => result.Map(static value => value.Length);
}
