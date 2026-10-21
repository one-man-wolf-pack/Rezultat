using Rezultat;

internal static class Case
{
    internal static string Observe(Presence<int> presence) => presence.Match<string?>(static _ => "v", static () => null, static () => "m");
}
