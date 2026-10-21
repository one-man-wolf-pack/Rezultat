using Rezultat;
using Rezultat.Collections;

internal static class Case
{
    internal static Result<int[], string> Observe(string?[] source) => source.Traverse(static value => Result<int, string>.Ok(value.Length));
}
