using Rezultat;

internal static class Case
{
    internal static Result<int, string> Observe(Result<int, string> result, Predicate<int> predicate) => result.Ensure(predicate, static _ => "bad");
}
