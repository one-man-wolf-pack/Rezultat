using Rezultat;

internal static class Case
{
    internal static int Observe(Presence<string> presence) { presence.TryGetValue(out var value); return value.Length; }
}
