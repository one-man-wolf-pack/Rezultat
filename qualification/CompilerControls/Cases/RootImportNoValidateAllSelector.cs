using Rezultat;

internal static class Case
{
    internal static object Observe(int[] items) =>
        items.ValidateAll(static item => Validation<int, string>.Valid(item));
}
