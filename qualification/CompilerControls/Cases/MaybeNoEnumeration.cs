using Rezultat;

internal static class Case
{
    internal static int Observe(Maybe<int> maybe)
    {
        var sum = 0;
        foreach (var value in maybe)
        {
            sum += value;
        }

        return sum;
    }
}
