using Rezultat;

internal static class Case
{
    internal static Result<int, int> Observe(Result<int, string?> result) => result.OrElse(static error => Result<int, int>.Ok(error.Length));
}
