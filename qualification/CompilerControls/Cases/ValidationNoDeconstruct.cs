using Rezultat;

internal static class Case
{
    internal static object Observe(Validation<int, string> validation) { var (value, issues) = validation; return value; }
}
