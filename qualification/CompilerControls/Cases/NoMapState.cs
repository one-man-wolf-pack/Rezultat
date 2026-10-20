using Rezultat;

internal static class Case
{
    internal static object Observe(Result<int, string> result) => result.Map(7, static (value, state) => value + state);
}
