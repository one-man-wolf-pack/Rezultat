using Rezultat;
using Rezultat.Testing.Xunit;

internal static class Case
{
    internal static int Observe() => Result<int, string?>.Error("x").ShouldBeError().Length;
}
