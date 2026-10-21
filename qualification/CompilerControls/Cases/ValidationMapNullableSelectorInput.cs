using Rezultat;

internal static class Case
{
    internal static Validation<int, string> Observe(Validation<string?, string> validation) => validation.Map(static value => value.Length);
}
