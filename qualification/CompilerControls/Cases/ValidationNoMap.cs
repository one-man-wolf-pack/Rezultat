using Rezultat;

internal static class Case
{
    internal static object Observe(Validation<int, string> validation) => validation.Map(static value => value + 1);
}
