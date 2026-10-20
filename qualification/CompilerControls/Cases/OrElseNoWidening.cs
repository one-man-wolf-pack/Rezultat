using Rezultat;

internal static class Case
{
    internal static Result<int, Base> Observe(Result<int, string> result) => result.OrElse(static _ => Result<int, Derived>.Error(new Derived()));
}

internal class Base;

internal sealed class Derived : Base;
