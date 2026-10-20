using Rezultat;

internal static class Case
{
    internal static Result<string?, string> Observe(Result<string?, string> result) => result.Ensure(static value => value.Length > 0, static _ => "empty");
}
