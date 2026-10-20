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

Maybe<string> foundName = Maybe.FromNullable(FindName(1));
checks.That("from nullable reference strips nullable", foundName.TryGetValue(out var name) && name.Length == 3);
checks.That("from null reference is none", !Maybe.FromNullable(FindName(2)).IsSome);
Maybe<int> foundAge = Maybe.FromNullable(FindAge(1));
checks.That("from nullable value strips nullable", foundAge.TryGetValue(out var age) && age + 1 == 1);
checks.That("from empty nullable value is none", Maybe.FromNullable(FindAge(2)) == Maybe<int>.None);
checks.That("explicit generic from null", Maybe.FromNullable<string>(null) == default && Maybe.FromNullable<int>(null) == default);

var presentNull = Maybe<string?>.Some(null);
checks.That("some null is present", presentNull.IsSome && presentNull != Maybe<string?>.None);
checks.That("some null observed", presentNull.TryGetValue(out var maybeNull) && maybeNull is null);
checks.That("some empty nullable", Maybe<int?>.Some(null).TryGetValue(out var emptyNullable) && !emptyNullable.HasValue);
checks.That("default is none", !default(Maybe<int>).IsSome && default(Maybe<int>) == Maybe<int>.None && new Maybe<int>() == default);
checks.That("none observation outputs default", !Maybe<int>.None.TryGetValue(out var inactive) && inactive == 0);
checks.That("upcast some", Maybe<IEnumerable<char>>.Some("ab").ValueOr([]).Count() == 2);

if (Maybe<string>.Some("value").TryGetValue(out var someValue))
{
    checks.Text("active maybe value used without null check", "VALUE", someValue.ToUpperInvariant());
}
else
{
    checks.Fail("unexpected none");
}

checks.Text("maybe match", "3", Maybe<int>.Some(3).Match(FormatNumber, static () => "none"));
checks.Text("maybe match none", "none", Maybe<int>.None.Match(FormatNumber, static () => "none"));
checks.That("maybe match nullable result", Maybe<int>.None.Match<string?>(static _ => "x", static () => null) is null);
checks.Text("maybe match nullable input", "null", presentNull.Match(static v => v ?? "null", static () => "none"));
checks.Throws<ArgumentNullException>("maybe null handler on other branch", () => Maybe<int>.Some(1).Match(FormatNumber, null!));
checks.Text("value or", "fallback", Maybe<string>.None.ValueOr("fallback"));
checks.Text("value or else method group", "anonymous", Maybe<string>.None.ValueOrElse(Anonymous));
checks.That("value or keeps present null", Maybe<string?>.Some(null).ValueOr("fallback") is null);
checks.That("value or nullable fallback", Maybe<string?>.None.ValueOr(null) is null);
checks.Throws<ArgumentNullException>("value or else null factory on some", () => Maybe<int>.Some(1).ValueOrElse(null!));
checks.Text("conditional fallback", "Ada", GreetingName(Maybe<string>.Some("Ada"), "guest"));

checks.Text("maybe none text", "Maybe.None", default(Maybe<Point>).ToString());
checks.Text("maybe some text", "Maybe.Some(Point { X = 1, Y = 2 })", Maybe<Point>.Some(new Point(1, 2)).ToString());
checks.Text("maybe some null text", "Maybe.Some(null)", presentNull.ToString());
checks.Text("nested maybe text", "Maybe.Some(Maybe.None)", Maybe<Maybe<int>>.Some(Maybe<int>.None).ToString());
checks.Text("maybe of result text", "Maybe.Some(Result.Ok(()))", Maybe<Result<Unit, string>>.Some(Result<Unit, string>.Ok(Unit.Value)).ToString());
checks.That("nested none differs", Maybe<Maybe<int>>.Some(Maybe<int>.None) != Maybe<Maybe<int>>.None);
checks.That("maybe NaN equality", Maybe<double>.Some(double.NaN) == Maybe<double>.Some(double.NaN));
checks.That("maybe array reference equality", Maybe<int[]>.Some([1]) != Maybe<int[]>.Some([1]));
checks.That("boxed none is not null", !((object)Maybe<int>.None).Equals(null) && ((object)Maybe<int>.None).Equals(Maybe<int>.None));

var maybeKeys = new HashSet<Maybe<string>>(new IgnoreCaseMaybeComparer())
{
    Maybe<string>.Some("Ada"),
    Maybe<string>.Some("ADA"),
    Maybe<string>.None,
    default,
};
checks.That("external maybe comparer", maybeKeys.Count == 2);
var maybeLookup = new Dictionary<Maybe<string?>, string>
{
    [Maybe<string?>.None] = "none",
    [Maybe<string?>.Some(null)] = "null",
};
checks.Text("maybe dictionary key", "null", maybeLookup[Maybe<string?>.Some(null)]);
checks.Text("public-only maybe extension", "Maybe.Some(4)", Maybe<int>.Some(2).Squared().ToString());
checks.Text("public-only maybe extension none", "Maybe.None", Maybe<int>.None.Squared().ToString());
checks.Throws<InvalidOperationException>("public-only throwing unwrap", () => Maybe<int>.None.OrThrow());

var loaded = await LoadAsync(found: true);
checks.That("awaited maybe some", loaded.TryGetValue(out var loadedName) && loadedName == "loaded");
checks.That("awaited maybe none", !(await LoadAsync(found: false)).IsSome);
checks.That("value task maybe", (await new ValueTask<Maybe<int>>(Maybe<int>.Some(1))).ValueOr(0) == 1);

checks.Text("maybe map", "Maybe.Some(6)", Maybe.FromNullable(FindAge(1)).Map(static n => (n + 3) * 2).ToString());
checks.Text("maybe map method group", "Maybe.Some(3)", Maybe<int>.Some(3).Map(FormatNumber).ToString());
checks.Text("maybe map keeps null result", "Maybe.Some(null)", Maybe<int>.Some(1).Map(static _ => (string?)null).ToString());
checks.Text("maybe and then", "Maybe.Some(3)", Maybe.FromNullable(FindName(1)).AndThen(static n => Maybe<int>.Some(n.Length)).ToString());
checks.Text("maybe and then method group", "Maybe.None", Maybe<int>.Some(2).AndThen(FindUser).ToString());
checks.Text("maybe and then default", "Maybe.None", Maybe<int>.Some(1).AndThen(static _ => default(Maybe<string>)).ToString());
checks.Text("maybe or else", "Maybe.Some(fallback)", Maybe<string>.None.OrElse(static () => Maybe<string>.Some("fallback")).ToString());
checks.Text("maybe or else keeps some null", "Maybe.Some(null)", presentNull.OrElse(static () => Maybe<string?>.Some("x")).ToString());
checks.Text("maybe filter", "Maybe.None", Maybe<int>.Some(-1).Filter(static n => n >= 0).ToString());
checks.Text("maybe filter keeps nullable type", "Maybe.Some(null)", presentNull.Filter(static v => v is null).ToString());
var reassigned = Maybe<int>.Some(1);
checks.Text("maybe filter keeps observed receiver", "Maybe.Some(1)", reassigned.Filter(_ => { reassigned = Maybe<int>.None; return true; }).ToString());
checks.Text("maybe zip", "Maybe.Some(Ada:36)", Maybe<string>.Some("Ada").Zip(Maybe<int>.Some(36), static (n, a) => $"{n}:{a}").ToString());
checks.Text("maybe zip tuple", "Maybe.Some((1, x))", Maybe<int>.Some(1).Zip(Maybe<string>.Some("x"), static (a, b) => (a, b)).ToString());
checks.Text("maybe zip none", "Maybe.None", Maybe<int>.None.Zip(Maybe<int>.Some(1), static (a, b) => a + b).ToString());
checks.Throws<ArgumentNullException>("maybe zip null selector on none", () => Maybe<int>.None.Zip<int, int>(Maybe<int>.None, null!));
checks.Throws<ArgumentNullException>("maybe map null selector on none", () => Maybe<int>.None.Map<int>(null!));
var pendingMaybe = Maybe<int>.Some(1).Map(static async n => { await Task.Yield(); return n + 1; });
checks.That("maybe map async payload", pendingMaybe.TryGetValue(out var pendingTask) && await pendingTask == 2);

string?[] names = [null, "Ada", "Bob", "Ada"];
checks.Text("first or none present null", "Maybe.Some(null)", names.FirstOrNone().ToString());
checks.Text("first or none predicate", "Maybe.Some(Bob)", names.FirstOrNone(static n => n is { Length: 3 } && n[0] == 'B').ToString());
checks.Text("first or none empty", "Maybe.None", Array.Empty<int>().FirstOrNone().ToString());
checks.Text("last or none list", "Maybe.Some(Ada)", names.LastOrNone().ToString());
checks.Text("last or none predicate", "Maybe.Some(Bob)", names.LastOrNone(static n => n == "Bob").ToString());
checks.Text("last or none sequence", "Maybe.Some(3)", Enumerable.Range(1, 3).Select(static n => n).LastOrNone().ToString());
checks.Text("single or none", "Maybe.Some(Bob)", names.SingleOrNone(static n => n == "Bob").ToString());
checks.Text("single or none empty", "Maybe.None", names.SingleOrNone(static n => n == "Eve").ToString());
checks.Throws<InvalidOperationException>("single or none multiple", () => names.SingleOrNone(static n => n == "Ada"));
checks.Text("element at or none", "Maybe.Some(Ada)", names.ElementAtOrNone(1).ToString());
checks.Text("element at or none negative", "Maybe.None", names.ElementAtOrNone(-1).ToString());
checks.Text("element at or none past end", "Maybe.None", Enumerable.Range(0, 2).ElementAtOrNone(5).ToString());

var ages = new Dictionary<string, int?> { ["Ada"] = 36, ["Unknown"] = null };
checks.Text("get or none dictionary", "Maybe.Some(36)", ages.GetOrNone("Ada").ToString());
checks.Text("get or none present null", "Maybe.Some(null)", ages.GetOrNone("Unknown").ToString());
checks.Text("get or none miss", "Maybe.None", ages.GetOrNone("Eve").ToString());
IDictionary<string, int?> mutableAges = ages;
IReadOnlyDictionary<string, int?> readOnlyAges = ages;
checks.Text("get or none static IDictionary", "Maybe.Some(36)", mutableAges.GetOrNone("Ada").ToString());
checks.Text("get or none static IReadOnlyDictionary", "Maybe.Some(36)", readOnlyAges.GetOrNone("Ada").ToString());
checks.Text("get or none read-only priority", "Maybe.Some(read-only)", new DivergentDictionary().GetOrNone("key").ToString());
checks.Text("get or none method group", "Maybe.Some(36)", Maybe<string>.Some("Ada").AndThen(ages.GetOrNone).ToString());
if (new Dictionary<string, string> { ["k"] = "value" }.GetOrNone("k").TryGetValue(out var found))
{
    checks.Text("get or none non-null payload", "VALUE", found.ToUpperInvariant());
}
else
{
    checks.Fail("get or none non-null payload: unexpected none");
}

var collected = new[] { "1", "2" }.Traverse(ParseMaybe);
checks.That("maybe collection traverse", collected.TryGetValue(out var collectedValues) && collectedValues.Sum() == 3);
checks.Text("maybe collection traverse none", "Maybe.None", new[] { "1", "x" }.Traverse(ParseMaybe).ToString());
checks.That("maybe collection traverse ready", new[] { Maybe<int>.Some(1), Maybe<int>.Some(2) }.Traverse(static m => m).Map(static a => a.Length) == Maybe<int>.Some(2));
checks.Text("result collection traverse still binds", "Result.Ok(2)", new[] { "1", "2" }.Traverse(ParseNumber).Map(static a => a.Length).ToString());
checks.Text("choose", "1,3", string.Join(",", new[] { "1", "x", "3" }.Choose(ParseMaybe)));
checks.Text("choose ready", "2", string.Join(",", new[] { Maybe<int>.None, Maybe<int>.Some(2) }.Choose(static m => m)));
var chooseCalls = 0;
var lazyChoice = new[] { "1" }.Choose(text => { chooseCalls++; return ParseMaybe(text); });
checks.That("choose is lazy", chooseCalls == 0 && lazyChoice.Count() == 1 && chooseCalls == 1);
checks.Throws<ArgumentNullException>("choose null selector", () => new[] { 1 }.Choose<int, int>(null!));

checks.Text("to result", "Result.Ok(Ada)", Maybe.FromNullable(FindName(1)).ToResult("missing").ToString());
checks.Text("to result none", "Result.Error(missing)", Maybe.FromNullable(FindName(2)).ToResult("missing").ToString());
checks.Text("to result else", "Result.Error(Failure { Code = 404 })", Maybe<int>.None.ToResultElse(static () => new Failure(404)).ToString());
var delegateError = Maybe<int>.None.ToResult(static () => "missing");
checks.That("to result keeps delegate error uninvoked", !delegateError.TryGetValue(out _, out var errorDelegate) && errorDelegate() == "missing");
checks.Throws<ArgumentNullException>("to result else null factory on some", () => Maybe<int>.Some(1).ToResultElse<string>(null!));
checks.Text("to maybe", "Maybe.Some(3)", ParseNumber("3").ToMaybe().ToString());
checks.Text("to maybe loses error", "Maybe.None", ParseNumber("x").ToMaybe().ToString());
checks.Throws<InvalidOperationException>("to maybe uninitialized", () => default(Result<int, string>).ToMaybe());
checks.Text("maybe traverse none", "Result.Ok(Maybe.None)", Maybe<string>.None.Traverse(ParseNumber).ToString());
checks.Text("maybe traverse some", "Result.Ok(Maybe.Some(4))", Maybe<string>.Some("4").Traverse(ParseNumber).ToString());
checks.Text("maybe traverse error", "Result.Error(not a number: x)", Maybe<string>.Some("x").Traverse(ParseNumber).ToString());
checks.Text("maybe traverse ready", "Result.Ok(Maybe.Some(5))", Maybe<Result<int, string>>.Some(ParseNumber("5")).Traverse(static r => r).ToString());
checks.Throws<InvalidOperationException>("maybe traverse default selector result", () => Maybe<int>.Some(1).Traverse(static _ => default(Result<int, string>)));
checks.Text("static owner maybe extensions", "Maybe.Some(1)", MaybeExtensions.FirstOrNone(new[] { 1 }).ToString());
checks.Text("static owner result extensions", "Maybe.Some(1)", ResultExtensions.ToMaybe(ParseNumber("1")).ToString());

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

static string? FindName(int id) => id == 1 ? "Ada" : null;

static Maybe<string> FindUser(int id) => id == 1 ? Maybe<string>.Some("user") : Maybe<string>.None;

static Maybe<int> ParseMaybe(string text) =>
    int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? Maybe<int>.Some(number) : Maybe<int>.None;

static int? FindAge(int id) => id == 1 ? 0 : null;

static string Anonymous() => "anonymous";

static string GreetingName(Maybe<string> name, string guest) => name.TryGetValue(out var value) ? value : guest;

static async Task<Maybe<string>> LoadAsync(bool found)
{
    await Task.Yield();
    return found ? Maybe<string>.Some("loaded") : Maybe<string>.None;
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

internal static class MaybeConsumerExtensions
{
    public static Maybe<int> Squared(this Maybe<int> maybe) =>
        maybe.TryGetValue(out var value) ? Maybe<int>.Some(value * value) : Maybe<int>.None;

    public static T OrThrow<T>(this Maybe<T> maybe) =>
        maybe.TryGetValue(out var value) ? value : throw new InvalidOperationException("none");
}

internal sealed class DivergentDictionary : Dictionary<string, string>, IReadOnlyDictionary<string, string>
{
    public DivergentDictionary() => this["key"] = "dictionary";

    bool IReadOnlyDictionary<string, string>.TryGetValue(string key, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out string value)
    {
        value = "read-only";
        return true;
    }
}

internal sealed class IgnoreCaseMaybeComparer : IEqualityComparer<Maybe<string>>
{
    public bool Equals(Maybe<string> x, Maybe<string> y)
    {
        var xSome = x.TryGetValue(out var xValue);
        var ySome = y.TryGetValue(out var yValue);
        return xSome == ySome && (!xSome || StringComparer.OrdinalIgnoreCase.Equals(xValue, yValue));
    }

    public int GetHashCode(Maybe<string> obj) =>
        obj.TryGetValue(out var value) ? StringComparer.OrdinalIgnoreCase.GetHashCode(value) : 0;
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
