using Rezultat;

internal static class Case
{
    internal static bool Observe(Validation<int, string> validation) => validation.IsInvalid;
}
