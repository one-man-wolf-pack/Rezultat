using Rezultat;

internal static class Case
{
    internal static object Observe(Validation<int, string> validation) => validation.Ensure(static _ => false, static value => value);
}
