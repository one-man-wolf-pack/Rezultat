using Rezultat;

internal static class Case
{
    internal static object Observe(string[] issues) => Validation<int, string>.InvalidMany(issues);
}
