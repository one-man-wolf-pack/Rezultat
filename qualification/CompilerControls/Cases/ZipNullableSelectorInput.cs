using Rezultat;

internal static class Case
{
    internal static Result<int, string> Observe(Result<string?, string> left, Result<int, string> right) => left.Zip(right, static (a, b) => a.Length + b);
}
