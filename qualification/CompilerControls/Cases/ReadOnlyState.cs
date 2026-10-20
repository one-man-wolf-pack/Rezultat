using Rezultat;

internal static class Case
{
    internal static void Mutate(Result<int, string> result) => result.IsSuccess = true;
}
