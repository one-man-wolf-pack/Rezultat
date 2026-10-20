using System.Globalization;
using Rezultat;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
var checks = new Checks();

checks.That("uninitialized is not initialized", !default(Result<int, string>).IsInitialized);
checks.Throws<InvalidOperationException>("uninitialized IsSuccess", () => _ = default(Result<int, string>).IsSuccess);
checks.Throws<InvalidOperationException>("uninitialized TryGetValue", () => default(Result<string, string>).TryGetValue(out _, out _));
checks.Throws<InvalidOperationException>("uninitialized Match", () => default(Result<Unit, Unit>).Match(_ => 0, _ => 1));
checks.Text("uninitialized text", "Result.Uninitialized", default(Result<Guid, Exception>).ToString());

checks.That("same-type branches differ", Result<string, string>.Ok("x") != Result<string, string>.Error("x"));
checks.That("unit branches differ", Result<Unit, Unit>.Ok(Unit.Value) != Result<Unit, Unit>.Error(Unit.Value));
checks.That("unit default equals value", default(Unit) == Unit.Value && new Unit().Equals((object)Unit.Value));
checks.Text("unit text", "()", Unit.Value.ToString());
checks.That("NaN payload equality", Result<double, int>.Ok(double.NaN).Equals(Result<double, int>.Ok(double.NaN)));
checks.That("struct payload equality", Result<Point, string>.Ok(new Point(1, 2)) == Result<Point, string>.Ok(new Point(1, 2)));
checks.That("null payload differs from uninitialized", Result<string?, int?>.Ok(null) != default);
checks.That("null payload hash stable", Result<string?, int?>.Error(null).GetHashCode() == Result<string?, int?>.Error(null).GetHashCode());

checks.Text("ok text", "Result.Ok(Point { X = 1, Y = 2 })", Result<Point, string>.Ok(new Point(1, 2)).ToString());
checks.Text("error text", "Result.Error(null)", Result<int, int?>.Error(null).ToString());
checks.Text("unit result text", "Result.Ok(())", Result<Unit, string>.Ok(Unit.Value).ToString());

checks.Text("early return success", "Result.Ok(5)", Sum("2", "3").ToString());
checks.Text("early return failure", "Result.Error(not a number: x)", Sum("2", "x").ToString());

if (Result<string, string>.Ok("value").TryGetValue(out var value, out var error))
{
    checks.Text("active value used without null check", "VALUE", value.ToUpperInvariant());
}
else
{
    checks.Fail($"unexpected error {error.Length}");
}

checks.Text("nullable active value", "null", Result<string?, int>.Ok(null).Match(v => v ?? "null", e => e.ToString(CultureInfo.InvariantCulture)));
checks.Text("method group handlers", "7", Result<int, string>.Ok(7).Match(FormatNumber, FormatError));
checks.Throws<ArgumentNullException>("null handler", () => Result<int, string>.Ok(1).Match(null!, FormatError));

var saved = await SaveAsync(succeed: true);
checks.That("awaited unit success", saved.IsSuccess && saved == Result<Unit, string>.Ok(Unit.Value));
var rejected = await SaveAsync(succeed: false);
checks.That("awaited unit failure", !rejected.TryGetValue(out _, out var reason) && reason == "rejected");

checks.Text("public-only extension", "Result.Ok(10)", Result<int, string>.Ok(5).Twice().ToString());
checks.Text("public-only extension error", "Result.Error(e)", Result<int, string>.Error("e").Twice().ToString());

var keys = new HashSet<Result<int, string>>(new IgnoreCaseErrorComparer())
{
    Result<int, string>.Error("Missing"),
    Result<int, string>.Error("MISSING"),
    default,
};
checks.That("external comparer", keys.Count == 2);

var lookup = new Dictionary<Result<Unit, Unit>, string>
{
    [Result<Unit, Unit>.Ok(Unit.Value)] = "ok",
    [Result<Unit, Unit>.Error(Unit.Value)] = "error",
};
checks.Text("dictionary key", "error", lookup[Result<Unit, Unit>.Error(default)]);

checks.Text("map", "Result.Ok(6)", ParseNumber("3").Map(static n => n * 2).ToString());
checks.Text("map method group", "Result.Ok(3)", ParseNumber("3").Map(FormatNumber).ToString());
checks.Text("map error", "Result.Error(15)", ParseNumber("x").MapError(static e => e.Length).ToString());
checks.Text("map error method group", "Result.Error(not a number: x)", ParseNumber("x").MapError(FormatError).ToString());
checks.Text("contextual map error", "Result.Error(field: not a number: x)",
    ParseNumber("x").MapError("field", static (e, field) => $"{field}: {e}").ToString());
checks.Text("contextual map error explicit target", "Result.Error(Failure { Code = 404 })",
    ParseNumber("x").MapError<Failure, int>(404, static (_, code) => new Failure(code)).ToString());
checks.Text("and then", "Result.Ok(5)", ParseNumber("2").AndThen(a => ParseNumber("3").Map(b => a + b)).ToString());
checks.Text("and then method group", "Result.Error(not a number: y)", Result<string, string>.Ok("y").AndThen(ParseNumber).ToString());
checks.Text("and then mapped error", "Result.Error(Failure { Code = 1 })",
    ParseNumber("x").MapError(static _ => new Failure(1)).AndThen(static n => Validate(n)).ToString());
checks.Text("ensure", "Result.Error(negative: -1)", ParseNumber("-1").Ensure(static n => n >= 0, static n => $"negative: {n}").ToString());
checks.Text("ensure passes", "Result.Ok(1)", ParseNumber("1").Ensure(static n => n >= 0, static n => $"negative: {n}").ToString());
checks.Text("or else", "Result.Ok(0)", ParseNumber("x").OrElse(static _ => Result<int, Failure>.Ok(0)).ToString());
checks.Text("or else method group", "Result.Error(Failure { Code = 15 })", ParseNumber("x").OrElse(Fallback).ToString());
checks.Text("zip", "Result.Ok(2x3)", ParseNumber("2").Zip(Result<string, string>.Ok("3"), static (a, b) => $"{a}x{b}").ToString());
checks.Text("zip tuple", "Result.Ok((2, 3))", ParseNumber("2").Zip(ParseNumber("3"), static (a, b) => (a, b)).ToString());
checks.Text("zip first error", "Result.Error(not a number: a)", ParseNumber("a").Zip(ParseNumber("b"), static (a, b) => a + b).ToString());
checks.Text("unit flow", "Result.Ok(())", ParseNumber("1").AndThen(static _ => Result<Unit, string>.Ok(Unit.Value)).ToString());

var parsed = new[] { "1", "2", "3" }.Traverse(ParseNumber);
checks.That("traverse value payload", parsed.TryGetValue(out var numbers, out _) && numbers.Sum() == 6);
checks.Text("traverse first error", "Result.Error(not a number: b)", new[] { "1", "b", "c" }.Traverse(ParseNumber).Map(static a => a.Length).ToString());
var words = new[] { "a", "bb" }.Traverse(static w => Result<string, int>.Ok(w.ToUpperInvariant()));
checks.That("traverse reference payload", words.TryGetValue(out var upper, out _) && string.Concat(upper) == "ABB");
checks.That("traverse ready outcomes", new[] { ParseNumber("4"), ParseNumber("5") }.Traverse(static r => r).Map(static a => a.Length) == Result<int, string>.Ok(2));
var tracked = new DisposalTracker();
checks.That("traverse early exit", !TrackedNumbers(tracked).Traverse(ParseNumber).IsSuccess && tracked.Disposed && tracked.Produced == 2);
checks.Throws<InvalidOperationException>("traverse visited default", () => new[] { 1 }.Traverse(static _ => default(Result<int, string>)));
checks.Throws<ArgumentNullException>("traverse null selector", () => new[] { 1 }.Traverse<int, int, string>(null!));

var awaited = (await SaveAsync(succeed: true)).Map(static _ => "saved");
checks.Text("await before map", "Result.Ok(saved)", awaited.ToString());
var pending = ParseNumber("7").Map(static async n => { await Task.Yield(); return n + 1; });
checks.That("map async payload stays ok", pending.TryGetValue(out var task, out _) && await task == 8);
var faulted = ParseNumber("7").Map(static n => Task.FromException<int>(new InvalidOperationException($"{n}")));
checks.That("faulted payload keeps branch", faulted.IsSuccess);
var nested = ParseNumber("1").Map(static _ => default(Result<int, string>));
checks.That("nested default payload", nested.TryGetValue(out var inner, out _) && !inner.IsInitialized);
checks.Throws<InvalidOperationException>("and then default outcome", () => ParseNumber("1").AndThen(static _ => default(Result<int, string>)));
checks.Throws<InvalidOperationException>("zip default other", () => ParseNumber("x").Zip(default(Result<int, string>), static (a, b) => a + b));
checks.Throws<ArgumentNullException>("ensure null predicate on error", () => ParseNumber("x").Ensure(null!, static _ => "e"));
checks.Throws<InvalidOperationException>("default receiver map", () => default(Result<int, string>).Map<int>(null!));

return checks.Complete();

static Result<int, string> ParseNumber(string text) =>
    int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
        ? Result<int, string>.Ok(number)
        : Result<int, string>.Error($"not a number: {text}");

static Result<int, string> Sum(string left, string right)
{
    if (!ParseNumber(left).TryGetValue(out var a, out var leftError))
    {
        return Result<int, string>.Error(leftError);
    }

    if (!ParseNumber(right).TryGetValue(out var b, out var rightError))
    {
        return Result<int, string>.Error(rightError);
    }

    return Result<int, string>.Ok(a + b);
}

static async Task<Result<Unit, string>> SaveAsync(bool succeed)
{
    await Task.Yield();
    return succeed ? Result<Unit, string>.Ok(Unit.Value) : Result<Unit, string>.Error("rejected");
}

static string FormatNumber(int number) => number.ToString(CultureInfo.InvariantCulture);

static string FormatError(string error) => error;

static Result<int, Failure> Validate(int number) =>
    number > 0 ? Result<int, Failure>.Ok(number) : Result<int, Failure>.Error(new Failure(0));

static Result<int, Failure> Fallback(string error) => Result<int, Failure>.Error(new Failure(error.Length));

static IEnumerable<string> TrackedNumbers(DisposalTracker tracker)
{
    try
    {
        foreach (var text in new[] { "1", "x", "3" })
        {
            tracker.Produced++;
            yield return text;
        }
    }
    finally
    {
        tracker.Disposed = true;
    }
}

internal readonly record struct Point(int X, int Y);

internal sealed record Failure(int Code);

internal sealed class DisposalTracker
{
    public int Produced { get; set; }

    public bool Disposed { get; set; }
}

internal static class ConsumerExtensions
{
    public static Result<int, TError> Twice<TError>(this Result<int, TError> result) =>
        result.Match(value => Result<int, TError>.Ok(value * 2), Result<int, TError>.Error);
}

internal sealed class IgnoreCaseErrorComparer : IEqualityComparer<Result<int, string>>
{
    public bool Equals(Result<int, string> x, Result<int, string> y)
    {
        if (!x.IsInitialized || !y.IsInitialized)
        {
            return x.IsInitialized == y.IsInitialized;
        }

        var xSuccess = x.TryGetValue(out var xValue, out var xError);
        var ySuccess = y.TryGetValue(out var yValue, out var yError);
        return xSuccess == ySuccess
            && (xSuccess ? xValue == yValue : StringComparer.OrdinalIgnoreCase.Equals(xError, yError));
    }

    public int GetHashCode(Result<int, string> obj)
    {
        if (!obj.IsInitialized)
        {
            return 0;
        }

        return obj.TryGetValue(out var value, out var error)
            ? value
            : StringComparer.OrdinalIgnoreCase.GetHashCode(error);
    }
}
