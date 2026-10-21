using Rezultat;

internal static class Case
{
    internal static object Observe(Presence<int> presence) => presence.Filter(static value => value > 0);
}
