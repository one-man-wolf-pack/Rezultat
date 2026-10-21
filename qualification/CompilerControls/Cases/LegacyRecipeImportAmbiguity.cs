using Rezultat;
using Recipes;

internal static class Case
{
    internal static object Observe(Result<int, string> result) => result.ToValidation();
}

namespace Recipes
{
    internal static class CarrierConversions
    {
        internal static Validation<T, TError> ToValidation<T, TError>(this Result<T, TError> source) =>
            source.Match(Validation<T, TError>.Valid, Validation<T, TError>.Invalid);
    }
}
