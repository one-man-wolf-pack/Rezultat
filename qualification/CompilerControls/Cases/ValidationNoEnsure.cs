using Rezultat;

internal static class Case
{
    internal static object Observe(Validation<int, string> validation) => validation.Ensure(static value => value > 0, static _ => "e");
}
