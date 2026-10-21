using Rezultat;

internal static class Case
{
    internal static Result<string?, string> Observe(Result<string?, string> result) => result.Inspect(static value => _ = value.Length);
}
