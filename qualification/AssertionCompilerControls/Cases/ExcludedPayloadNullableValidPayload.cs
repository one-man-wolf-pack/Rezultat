using Rezultat;
using Rezultat.Testing.Xunit;

internal static class Case
{
    internal static int Observe() => Validation<string?, int>.Valid("x").ShouldBeValid(includePayload: false).Length;
}
