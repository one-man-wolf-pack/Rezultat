using Rezultat;
using Rezultat.Linq;
using Rezultat.Qualification;

namespace PackageConsumer.LegacyQuery;

internal static class ProjectQueries {
    public static int Calls { get; set; }

    public static Result<U, E> Select<T, U, E>(this Result<T, E> source, Func<T, U> selector) {
        Calls++;
        return source.Map(selector);
    }

    public static Result<R, E> SelectMany<T, U, R, E>(this Result<T, E> source, Func<T, Result<U, E>> next, Func<T, U, R> project) {
        Calls++;
        return source.AndThen(x => next(x).Map(y => project(x, y)));
    }
}

internal static class LegacyQueryConsumer {
    public static void Run(Checks checks) {
        var query = from a in Parse("1") from b in Parse("2") select a + b;
        checks.Text("enclosing-namespace query recipe wins over imported facade", "Result.Ok(3)", query.ToString());
        checks.That("enclosing-namespace query recipe was bound", ProjectQueries.Calls == 1);

        ProjectQueries.Calls = 0;
        checks.Text("static owner reaches library query beside recipe", "Result.Ok(3)",
            ResultQueryExtensions.SelectMany(Parse("1"), static a => Parse("2"), static (a, b) => a + b).ToString());
        checks.Text("library maybe query is unaffected by result recipe", "Maybe.Some(2)", (from a in Maybe<int>.Some(1) select a + 1).ToString());
        checks.That("static owner call bypasses query recipe", ProjectQueries.Calls == 0);
    }

    private static Result<int, string> Parse(string text) =>
        int.TryParse(text, out var value) ? Result<int, string>.Ok(value) : Result<int, string>.Error(text);
}
