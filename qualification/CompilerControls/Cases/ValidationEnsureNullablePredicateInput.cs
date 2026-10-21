using Rezultat;

internal static class Case
{
    internal static Validation<string?, string> Observe(Validation<string?, string> validation) => validation.Ensure(static value => value.Length > 0, static _ => "empty");
}
