using Rezultat;

internal static class Case
{
    internal static Validation<int, object> Observe(Validation<int, string> v) => v.OrElse(static _ => Validation<int, string>.Invalid("x"));
}
