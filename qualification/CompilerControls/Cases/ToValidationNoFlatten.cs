using Rezultat;

internal static class Case
{
    internal static Validation<int, string> Observe(Result<int, string[]> result) => result.ToValidation();
}
