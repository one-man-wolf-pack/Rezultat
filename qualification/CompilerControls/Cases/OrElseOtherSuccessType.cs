using Rezultat;

internal static class Case
{
    internal static object Observe(Result<int, string> result) => result.OrElse(static _ => Result<string, int>.Ok("x"));
}
