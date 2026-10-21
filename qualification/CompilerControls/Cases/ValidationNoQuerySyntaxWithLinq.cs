using Rezultat;
using Rezultat.Linq;

internal static class Case
{
    internal static object Observe(Validation<int, string> validation) =>
        from value in validation select value + 1;
}
