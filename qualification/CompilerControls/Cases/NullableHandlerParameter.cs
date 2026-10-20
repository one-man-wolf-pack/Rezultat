using Rezultat;

internal static class Case
{
    internal static int Observe(Result<string?, int> result) => result.Match(value => value.Length, error => error);
}
