using Rezultat;

internal static class Case
{
    internal static Result<int, Base> Observe(Result<int, Derived> left, Result<int, Derived> right) => left.Zip(right, static (a, b) => a + b);
}

internal class Base;

internal sealed class Derived : Base;
