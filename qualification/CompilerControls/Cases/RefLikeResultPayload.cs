using System;
using Rezultat;

internal static class Case
{
    internal static Result<ReadOnlySpan<char>, string> Observe(string text) => Result<ReadOnlySpan<char>, string>.Ok(text.AsSpan());
}
