using Rezultat;
using Rezultat.Testing.Xunit;

internal static class Case
{
    internal static int Observe() => Maybe<string?>.Some("x").ShouldBeSome(includePayload: false).Length;
}
