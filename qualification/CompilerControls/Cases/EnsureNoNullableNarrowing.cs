using Rezultat;

internal static class Case
{
    internal static int Observe(Result<string?, string> result) => result.Ensure(static value => value is not null, static _ => "null").TryGetValue(out var value, out _) ? value.Length : 0;
}
