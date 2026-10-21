using Rezultat;
using Rezultat.Collections;
using Rezultat.Linq;

internal static class ReadyCollectionQueryConsumer
{
    public static void Run(Checks checks)
    {
        checks.Text("result sequence", "Result.Ok(3)", new[] { Parse("1"), Parse("2") }.Sequence().Map(static values => values.Sum()).ToString());
        checks.Text("result sequence first error", "Result.Error(x)", new[] { Parse("1"), Parse("x"), Parse("y") }.Sequence().ToString());
        checks.Throws<InvalidOperationException>("result sequence visited default", () => new[] { default(Result<int, string>) }.Sequence());
        checks.That("result sequence empty", new Result<string?, string>[0].Sequence().TryGetValue(out var empty, out _) && empty.Length == 0);
        checks.Text("maybe sequence", "Maybe.Some(2)", new[] { Maybe<string?>.Some(null), Maybe<string?>.Some("a") }.Sequence().Map(static values => values.Length).ToString());
        checks.Text("maybe sequence none", "Maybe.None", new[] { Maybe<int>.Some(1), default }.Sequence().ToString());
        checks.Text("validation sequence", "Validation.Invalid([a, b, c])",
            new[] { Validation<int, string>.Invalid("a"), Validation<int, string>.Valid(1), Validation<int, string>.InvalidMany(["b", "c"]) }.Sequence().Map(static values => values.Length).ToString());
        checks.Text("validation sequence valid", "Validation.Valid(2)",
            new[] { Validation<int, string>.Valid(1), Validation<int, string>.Valid(2) }.Sequence().Map(static values => values.Length).ToString());

        checks.Text("validate all ready", "Validation.Invalid([a, null])",
            new[] { Validation<string, string?>.Invalid("a"), Validation<string, string?>.Valid("v"), Validation<string, string?>.Invalid(null) }.ValidateAll().ToString());
        checks.Text("validate all selector", "Validation.Valid(())", new[] { "1", "2" }.ValidateAll(Check).ToString());
        checks.Text("validate all selector issues", "Validation.Invalid([x, y])", new[] { "x", "1", "y" }.ValidateAll(Check).ToString());
        checks.Text("validate all empty", "Validation.Valid(())", Array.Empty<Validation<Guid, int>>().ValidateAll().ToString());
        checks.Throws<InvalidOperationException>("validate all visited default", () => new[] { Validation<int, string>.Invalid("a"), default }.ValidateAll());
        checks.Throws<ArgumentNullException>("validate all null selector", () => ValidationCollectionExtensions.ValidateAll<int, int, string>([], null!));

        var factoryCalls = 0;
        checks.Text("at most one none", "Result.Ok(Maybe.None)", Array.Empty<string>().AtMostOne(() => { factoryCalls++; return "many"; }).ToString());
        checks.Text("at most one some", "Result.Ok(Maybe.Some(null))", new string?[] { null }.AtMostOne(() => { factoryCalls++; return "many"; }).ToString());
        checks.Text("at most one many", "Result.Error(many)", new[] { 1, 2, 3 }.AtMostOne(() => { factoryCalls++; return "many"; }).ToString());
        checks.That("at most one factory called once for many only", factoryCalls == 1);
        checks.Text("at most one after where", "Result.Ok(Maybe.Some(2))", new[] { 1, 2, 3 }.Where(static n => n % 2 == 0).AtMostOne(static () => "many").ToString());

        var context =
            from number in Parse("2")
            let doubled = number * 2
            from other in Parse("3")
            from third in Parse("4")
            select doubled + other + third;
        checks.Text("result query", "Result.Ok(11)", context.ToString());
        checks.Text("result query error", "Result.Error(x)", (from a in Parse("1") from b in Parse("x") select a + b).ToString());
        checks.Text("result select", "Result.Ok(2)", Parse("1").Select(static n => n + 1).ToString());
        checks.Throws<InvalidOperationException>("result select many default source", () => default(Result<int, string>).SelectMany<int, int, int, string>(null!, null!));
        checks.Throws<ArgumentNullException>("result select many null next on error", () => Parse("x").SelectMany<int, int, int, string>(null!, static (a, b) => a + b));
        checks.Throws<InvalidOperationException>("result select many default next", () => Parse("1").SelectMany(static _ => default(Result<int, string>), static (a, b) => a + b));

        checks.Text("maybe query", "Maybe.Some(5)", (from a in Find(2) where a > 1 from b in Find(3) select a + b).ToString());
        checks.Text("maybe query where none", "Maybe.None", (from a in Find(1) where a > 1 select a).ToString());
        checks.Text("maybe query default next", "Maybe.None", (from a in Find(1) from b in default(Maybe<int>) select a + b).ToString());
        checks.Throws<ArgumentNullException>("maybe select many null project on none", () => Maybe<int>.None.SelectMany<int, int, int>(static n => Maybe<int>.Some(n), null!));
        checks.Text("maybe static owner", "Maybe.Some(1)", MaybeQueryExtensions.Where(Find(1), static n => n > 0).ToString());
    }

    private static Result<int, string> Parse(string text) =>
        int.TryParse(text, out var value) ? Result<int, string>.Ok(value) : Result<int, string>.Error(text);

    private static Validation<int, string> Check(string text) =>
        int.TryParse(text, out var value) ? Validation<int, string>.Valid(value) : Validation<int, string>.Invalid(text);

    private static Maybe<int> Find(int value) => Maybe<int>.Some(value);
}
