using Rezultat;

internal static class Case
{
    internal static Result<int, int> Observe(Result<int, string?> result) => result.MapError(static error => error.Length);
}
