using Rezultat;

internal static class Case
{
    internal static object Observe(Maybe<string?> left, Maybe<int> right) => left.Zip(right, static (text, number) => text.Length + number);
}
