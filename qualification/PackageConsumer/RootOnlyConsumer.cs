using Rezultat;

internal static class RootOnlyConsumer
{
    public static void Run(Checks checks)
    {
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
    }

    private static Result<int, string> Parse(string text) =>
        int.TryParse(text, out var value) ? Result<int, string>.Ok(value) : Result<int, string>.Error(text);
}
