using Rezultat;

internal static class Case
{
    internal static Result<int, string> Observe(Validation<int, string> validation) => validation.ToResult();
}
