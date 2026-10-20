using Rezultat;

internal static class Case
{
    internal static Result<int, string> Create() => new Result<int, string>(5);
}
