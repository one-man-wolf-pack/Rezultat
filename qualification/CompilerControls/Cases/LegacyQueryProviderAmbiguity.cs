using Rezultat;
using Rezultat.Linq;
using Recipes;

internal static class Case
{
    internal static object Observe(Result<int, string> result) =>
        result.Select(static value => value + 1);
}

namespace Recipes
{
    internal static class ResultQuery
    {
        internal static Result<TResult, TError> Select<T, TResult, TError>(this Result<T, TError> source, Func<T, TResult> selector) =>
            source.Map(selector);
    }
}
