using Rezultat;
using Rezultat.Linq;

internal static class Case
{
    internal static object Observe(Validation<int, string>[] items) =>
        items.ValidateAll();
}
