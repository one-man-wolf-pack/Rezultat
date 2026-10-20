using Rezultat;

internal static class Case
{
    internal static Result<int, Base> Observe(Result<int, string> result) => result.MapError(7, static (_, _) => new Derived());
}

internal class Base;

internal sealed class Derived : Base;
