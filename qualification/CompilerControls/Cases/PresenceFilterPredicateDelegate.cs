using System;
using Rezultat;

internal static class Case
{
    internal static object Observe(Presence<int> presence, Predicate<int> predicate) => presence.FilterToMissing(predicate);
}
