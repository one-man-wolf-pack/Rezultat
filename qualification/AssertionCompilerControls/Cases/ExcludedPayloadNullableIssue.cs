using Rezultat;
using Rezultat.Testing.Xunit;

internal static class Case
{
    internal static int Observe() => Validation<int, string?>.Invalid("x").ShouldBeInvalid(includePayload: false)[0].Length;
}
