using System;
using Rezultat;

internal static class Case
{
    internal static object Observe(Validation<int, string> validation, Predicate<int> predicate) => validation.Ensure(predicate, static _ => "bad");
}
