using Rezultat;

internal static class Case
{
    internal static Presence<string> Observe(Presence<int> presence) => presence.Map<string>(static _ => null);
}
