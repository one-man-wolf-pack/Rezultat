using System.Threading.Tasks;
using Rezultat;

internal static class Case
{
    internal static object Observe(Presence<int> presence) => presence.AndThen(static async value => { await Task.Yield(); return Presence<int>.Value(value); });
}
