using Rezultat;

internal static class Case
{
    internal static object Observe(Result<int, string> result) => result.Ensure(static _ => false, static value => value);
}
