using Rezultat;

internal static class Case
{
    internal static int Observe(Result<string?, int> result) => result.ToMaybe().ValueOr("none").Length;
}
