using Rezultat;

internal static class Case
{
    internal static int Observe(Result<int, string?> result) => result.ToValidation().Match(static value => value, static issues => issues[0].Length);
}
