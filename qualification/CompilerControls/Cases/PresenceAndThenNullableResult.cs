using Rezultat;

internal static class Case
{
    internal static object Observe(Presence<int> presence) => presence.AndThen(static _ => Presence<string?>.Null);
}
