using Rezultat;

internal static class Case
{
    internal static object Observe(Maybe<Maybe<int>> nested) => nested.Flatten();
}
