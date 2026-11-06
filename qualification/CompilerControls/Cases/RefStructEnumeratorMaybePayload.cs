using System;
using Rezultat;

internal static class Case
{
    internal static bool Observe(string text) => Maybe<ReadOnlySpan<char>.Enumerator>.Some(text.AsSpan().GetEnumerator()).IsSome;
}
