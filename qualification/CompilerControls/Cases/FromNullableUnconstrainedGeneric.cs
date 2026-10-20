using Rezultat;

internal static class Case
{
    internal static Maybe<T> Create<T>(T? value) => Maybe.FromNullable(value);
}
