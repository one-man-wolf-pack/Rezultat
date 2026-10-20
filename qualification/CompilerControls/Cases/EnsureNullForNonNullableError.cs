using Rezultat;

internal static class Case
{
    internal static Result<int, string> Observe(Result<int, string> result) => result.Ensure(static _ => false, static _ => null);
}
