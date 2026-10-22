using Rezultat;
using Rezultat.Testing.Xunit;

internal static class Case
{
    internal static int Observe() => Result<string?, int>.Ok("x").ShouldBeOk(includePayload: false).Length;
}
