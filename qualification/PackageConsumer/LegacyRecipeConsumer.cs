using Rezultat;
using Rezultat.Qualification;

namespace PackageConsumer.Legacy;

internal static class LegacyRecipes {
    public static int Calls { get; set; }

    public static Result<T, E> ObserveSuccess<T, E>(this Result<T, E> result, Action<T> action) {
        Calls++;
        ArgumentNullException.ThrowIfNull(action);
        var snapshot = result;
        if (snapshot.TryGetValue(out var value, out _)) {
            action(value);
        }

        return snapshot;
    }

    public static Result<Presence<U>, E> ConvertField<T, U, E>(this Presence<T> field, Func<T, Result<U, E>> convert)
        where T : notnull
        where U : notnull {
        Calls++;
        return field.Match(
            value => convert(value).Map(Presence<U>.Value),
            static () => Result<Presence<U>, E>.Ok(Presence<U>.Null),
            static () => Result<Presence<U>, E>.Ok(Presence<U>.Missing));
    }

    public static Result<T, E> Inspect<T, E>(this Result<T, E> result, Action<T> action) {
        Calls++;
        return result.ObserveSuccess(action);
    }

    public static Validation<T, E> ToValidation<T, E>(this Result<T, E> result) {
        Calls++;
        return result.Match(Validation<T, E>.Valid, Validation<T, E>.Invalid);
    }

    public static Result<Presence<U>, E> Traverse<T, U, E>(this Presence<T> field, Func<T, Result<U, E>> convert)
        where T : notnull
        where U : notnull {
        Calls++;
        return field.ConvertField(convert);
    }
}

internal static class LegacyRecipeConsumer {
    public static void Run(Checks checks) {
        var seen = 0;
        checks.Text("legacy observe success", "Result.Ok(1)", Result<int, string>.Ok(1).ObserveSuccess(_ => seen++).ToString());
        checks.Text("legacy convert field", "Result.Ok(Presence.Null)", Presence<string>.Null.ConvertField(Parse).ToString());
        checks.That("legacy recipes bind by their own names", LegacyRecipes.Calls == 2 && seen == 1);

        LegacyRecipes.Calls = 0;
        checks.Text("instance inspect wins over same-name recipe", "Result.Ok(2)", Result<int, string>.Ok(2).Inspect(_ => seen++).ToString());
        checks.That("same-name recipe is not called for instance member", LegacyRecipes.Calls == 0 && seen == 2);

        checks.Text("enclosing-namespace recipe wins over root to validation", "Validation.Invalid([x])", Result<int, string>.Error("x").ToValidation().ToString());
        checks.Text("enclosing-namespace recipe wins over root traverse", "Result.Ok(Presence.Value(3))", Presence<string>.Value("3").Traverse(Parse).ToString());
        checks.That("enclosing-namespace recipes were bound", LegacyRecipes.Calls == 3);

        LegacyRecipes.Calls = 0;
        checks.Text("static owner reaches library bridge beside recipe", "Validation.Invalid([y])", ResultExtensions.ToValidation(Result<int, string>.Error("y")).ToString());
        checks.That("static owner call bypasses recipe", LegacyRecipes.Calls == 0);
    }

    private static Result<int, string> Parse(string text) =>
        int.TryParse(text, out var value) ? Result<int, string>.Ok(value) : Result<int, string>.Error(text);
}
