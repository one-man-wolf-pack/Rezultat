using Rezultat;

internal static class Case
{
    internal static object Observe(Presence<int> presence) => presence.Traverse(static _ => default);
}
