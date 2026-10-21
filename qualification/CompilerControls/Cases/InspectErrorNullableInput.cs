using Rezultat;

internal static class Case
{
    internal static Result<int, string?> Observe(Result<int, string?> result) => result.InspectError(static error => _ = error.Length);
}
