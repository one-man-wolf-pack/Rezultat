using Rezultat;

internal static class Case
{
    internal static int Observe(Result<int, string> result)
    {
        (int value, string error) = result;
        return value + error.Length;
    }
}
