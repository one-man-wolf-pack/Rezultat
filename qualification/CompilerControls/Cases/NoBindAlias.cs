using Rezultat;

internal static class Case
{
    internal static object Observe(Result<int, string> result) => result.Bind(static value => Result<int, string>.Ok(value));
}
