using Rezultat;
using Rezultat.Qualification;

internal static class RootOnlyConsumer {
    public static void Run(Checks checks) {
        checks.Text("root only result", "Result.Ok(42)", Parse("40").Zip(Parse("2"), static (a, b) => a + b).ToString());
        checks.Text("root only unit", "Result.Ok(())", Result<Unit, string>.Ok(Unit.Value).ToString());
        checks.Text("root only presence", "Presence.Value(x)", Presence<string>.Value("x").ToString());
        checks.Text("root only validation", "Validation.Invalid([a, b])",
            Validation<int, string>.Invalid("a").Zip(Validation<int, string>.Invalid("b"), static (a, b) => a + b).ToString());
        checks.Text("root only from nullable", "Maybe.None", Maybe.FromNullable((string?)null).ToString());
        checks.Text("root only to maybe", "Maybe.Some(1)", Parse("1").ToMaybe().ToString());
        checks.Text("root only to result", "Result.Error(missing)", Maybe<int>.None.ToResult("missing").ToString());
        checks.Text("root only to result else", "Result.Ok(3)", Maybe<int>.Some(3).ToResultElse(static () => "missing").ToString());
        checks.Text("root only maybe receiver traverse", "Result.Ok(Maybe.Some(4))", Maybe<string>.Some("4").Traverse(Parse).ToString());
        checks.Text("root only maybe receiver traverse error", "Result.Error(x)", Maybe<string>.Some("x").Traverse(Parse).ToString());
        checks.Text("root only inspect", "Result.Ok(1)", Parse("1").Inspect(static _ => { }).InspectError(static _ => { }).ToString());
        checks.Text("root only validation map ensure", "Validation.Invalid([small: 2])",
            Validation<int, string>.Valid(1).Map(static n => n + 1).Ensure(static n => n > 2, static n => $"small: {n}").ToString());
        checks.Text("root only presence and then filters", "Presence.Null",
            Presence<string>.Value("1").AndThen(static text => Presence<int>.Value(text.Length)).FilterToMissing(static n => n > 0).FilterToNull(static n => n > 1).ToString());
        checks.Text("root only presence traverse", "Result.Ok(Presence.Value(4))", Presence<string>.Value("4").Traverse(Parse).ToString());
        checks.Text("root only to validation", "Validation.Invalid([x])", Parse("x").ToValidation().ToString());
        checks.That("root only to result", Validation<int, string>.Invalid("x").ToResult().TryGetValue(out _, out var issues) is false && issues.Length == 1);
    }

    private static Result<int, string> Parse(string text) =>
        int.TryParse(text, out var value) ? Result<int, string>.Ok(value) : Result<int, string>.Error(text);
}
