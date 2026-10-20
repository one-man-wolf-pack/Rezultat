using Rezultat;

internal static class Case
{
    internal static object Observe(Result<int, string> result) => result.AndThen(static value => Result<int, int>.Ok(value));
}
