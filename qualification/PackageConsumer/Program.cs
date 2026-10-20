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

internal readonly record struct Point(int X, int Y);

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
