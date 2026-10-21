using Rezultat;

internal static class Case
{
    internal static object Observe(Presence<int> presence) => presence.Map(static _ => (string?)null);
}
