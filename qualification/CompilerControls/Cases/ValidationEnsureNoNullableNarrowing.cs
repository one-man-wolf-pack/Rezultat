using Rezultat;

internal static class Case
{
    internal static int Observe(Validation<string?, string> validation) => validation.Ensure(static value => value is not null, static _ => "null").TryGetValue(out var value, out _) ? value.Length : 0;
}
