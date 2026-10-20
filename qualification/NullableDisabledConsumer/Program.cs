using Rezultat;

var checks = new Checks();

var okNull = Result<string, string>.Ok(null);
checks.That("null success is initialized", okNull.IsInitialized && okNull.IsSuccess);
checks.That("null success outputs", okNull.TryGetValue(out var value, out var error) && value == null && error == null);
checks.Text("null success text", "Result.Ok(null)", okNull.ToString());

var errorNull = Result<string, string>.Error(null);
checks.That("null failure branch", !errorNull.IsSuccess && errorNull != okNull);
checks.That("null failure handler receives null", errorNull.Match(_ => false, e => e == null));
checks.That("null payload equality", errorNull == Result<string, string>.Error(null));

checks.That("default is uninitialized", !default(Result<string, string>).IsInitialized);
checks.That("default differs from null success", default(Result<string, string>) != okNull);
checks.Throws<InvalidOperationException>("default IsSuccess", () => _ = default(Result<string, string>).IsSuccess);
checks.Throws<InvalidOperationException>("default TryGetValue", () => default(Result<string, string>).TryGetValue(out _, out _));
checks.Throws<InvalidOperationException>("default Match with null handlers", () => default(Result<string, string>).Match<int>(null, null));
checks.Throws<ArgumentNullException>("null success handler", () => okNull.Match(null, _ => 0));
checks.Throws<ArgumentNullException>("null error handler", () => okNull.Match(_ => 0, null));

checks.That("null payload map", okNull.Map(v => v == null ? "was null" : v) == Result<string, string>.Ok("was null"));
checks.That("null error map error", errorNull.MapError(e => e == null) == Result<string, bool>.Error(true));
checks.That("null payload zip", okNull.Zip(okNull, (a, b) => a == null && b == null) == Result<bool, string>.Ok(true));
var traversed = new string[] { null, "a" }.Traverse(v => Result<string, string>.Ok(v));
checks.That("null payload traverse", traversed.TryGetValue(out var items, out _) && items.Length == 2 && items[0] == null);
checks.That("null error from ensure factory", okNull.Ensure(v => v != null, v => null) == Result<string, string>.Error(null));
checks.That("null error into or else", errorNull.OrElse(e => Result<string, int>.Ok(e)) == Result<string, int>.Ok(null));
checks.Throws<InvalidOperationException>("default map with null selector", () => default(Result<string, string>).Map<int>(null));
checks.Throws<ArgumentNullException>("null map selector", () => okNull.Map<int>(null));
checks.Throws<ArgumentNullException>("null traverse source", () => ((string[])null).Traverse(v => Result<string, string>.Ok(v)));

var someNull = Maybe<string>.Some(null);
checks.That("maybe some null is present", someNull.IsSome && someNull != Maybe<string>.None);
checks.That("maybe some null output", someNull.TryGetValue(out var maybeValue) && maybeValue == null);
checks.Text("maybe some null text", "Maybe.Some(null)", someNull.ToString());
checks.That("maybe from null is none", !Maybe.FromNullable<string>(null).IsSome && Maybe.FromNullable((string)null) == default);
checks.That("maybe from empty nullable is none", !Maybe.FromNullable((int?)null).IsSome);
checks.That("maybe value or null fallback", Maybe<string>.None.ValueOr(null) == null);
checks.That("maybe value or keeps present null", someNull.ValueOr("fallback") == null);
checks.That("maybe value or else null result", Maybe<string>.None.ValueOrElse(() => null) == null);
checks.That("maybe match receives null", someNull.Match(v => v == null, () => false));
checks.Throws<ArgumentNullException>("maybe null on some handler for none", () => Maybe<string>.None.Match(null, () => 0));
checks.Throws<ArgumentNullException>("maybe null on none handler for some", () => someNull.Match(_ => 0, null));
checks.Throws<ArgumentNullException>("maybe null fallback factory for some", () => someNull.ValueOrElse(null));

checks.That("maybe map receives and keeps null", someNull.Map(v => v == null ? null : v) == Maybe<string>.Some(null));
checks.That("maybe and then receives null", someNull.AndThen(v => Maybe<bool>.Some(v == null)) == Maybe<bool>.Some(true));
checks.That("maybe or else keeps some null", someNull.OrElse(() => Maybe<string>.Some("x")) == someNull);
checks.That("maybe filter keeps some null", someNull.Filter(v => v == null) == someNull);
checks.That("maybe zip null payloads", someNull.Zip(someNull, (a, b) => a == b) == Maybe<bool>.Some(true));
checks.Throws<ArgumentNullException>("maybe null filter predicate on none", () => Maybe<string>.None.Filter(null));
checks.Throws<ArgumentNullException>("maybe null zip selector", () => someNull.Zip<string, string>(someNull, null));
checks.That("first or none null element", new string[] { null }.FirstOrNone() == someNull);
checks.That("last or none null element", new string[] { "a", null }.LastOrNone() == someNull);
checks.That("single or none null element", new string[] { null }.SingleOrNone(v => v == null) == someNull);
checks.That("element at or none null element", new string[] { null }.ElementAtOrNone(0) == someNull);
checks.That("get or none null value", new Dictionary<string, string> { ["k"] = null }.GetOrNone("k") == someNull);
checks.Throws<ArgumentNullException>("first or none null source", () => ((string[])null).FirstOrNone());
checks.Throws<ArgumentNullException>("get or none null source", () => ((Dictionary<string, string>)null).GetOrNone("k"));
var nullItems = new string[] { null, "a" }.Traverse(v => Maybe<string>.Some(v));
checks.That("maybe traverse null payload", nullItems.TryGetValue(out var collectedItems) && collectedItems.Length == 2 && collectedItems[0] == null);
checks.That("choose null payload", new string[] { null }.Choose(v => Maybe<string>.Some(v)).Single() == null);
checks.Throws<ArgumentNullException>("choose null selector", () => new string[0].Choose<string, string>(null));
checks.That("to result null payload and error", someNull.ToResult<string>(null) == Result<string, string>.Ok(null)
    && Maybe<string>.None.ToResult<string>(null) == Result<string, string>.Error(null));
checks.That("to result else null error", Maybe<string>.None.ToResultElse<string>(() => null) == Result<string, string>.Error(null));
checks.Throws<ArgumentNullException>("to result else null factory", () => someNull.ToResultElse<string>(null));
checks.That("to maybe null payload", okNull.ToMaybe() == someNull && errorNull.ToMaybe() == Maybe<string>.None);
checks.Throws<InvalidOperationException>("to maybe default", () => default(Result<string, string>).ToMaybe());
checks.That("maybe traverse null ok", someNull.Traverse(v => Result<string, string>.Ok(v)) == Result<Maybe<string>, string>.Ok(someNull));
checks.That("maybe traverse null error", someNull.Traverse(v => Result<string, string>.Error(null)) == Result<Maybe<string>, string>.Error(null));
checks.Throws<ArgumentNullException>("maybe traverse null selector", () => Maybe<string>.None.Traverse<string, string, string>(null));

return checks.Complete();
