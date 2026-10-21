using Rezultat;

internal static class Case
{
    internal static object Observe(Validation<int, string> v) => v.AndThen(static x => Validation<int, int>.Valid(x));
}
