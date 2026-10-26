using Rezultat;
using Rezultat.Collections;
using Rezultat.Linq;
using Rezultat.Qualification;

var checks = new Checks();

var okNull = Result<string, string>.Ok(null);
checks.That("null success is initialized", okNull.IsInitialized && okNull.IsSuccess);
checks.That("null success outputs", okNull.TryGetValue(out string value, out string error) && value == null && error == null);
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

checks.That("null payload map", okNull.Map(v => v ?? "was null") == Result<string, string>.Ok("was null"));
checks.That("null error map error", errorNull.MapError(e => e == null) == Result<string, bool>.Error(true));
checks.That("null payload zip", okNull.Zip(okNull, (a, b) => a == null && b == null) == Result<bool, string>.Ok(true));
var traversed = new string[] { null, "a" }.Traverse(v => Result<string, string>.Ok(v));
checks.That("null payload traverse", traversed.TryGetValue(out string[] items, out _) && items.Length == 2 && items[0] == null);
checks.That("null error from ensure factory", okNull.Ensure(v => v != null, v => null) == Result<string, string>.Error(null));
checks.That("null error into or else", errorNull.OrElse(e => Result<string, int>.Ok(e)) == Result<string, int>.Ok(null));
checks.Throws<InvalidOperationException>("default map with null selector", () => default(Result<string, string>).Map<int>(null));
checks.Throws<ArgumentNullException>("null map selector", () => okNull.Map<int>(null));
checks.Throws<ArgumentNullException>("null traverse source", () => ((string[])null).Traverse(v => Result<string, string>.Ok(v)));

var someNull = Maybe<string>.Some(null);
checks.That("maybe some null is present", someNull.IsSome && someNull != Maybe<string>.None);
checks.That("maybe some null output", someNull.TryGetValue(out string maybeValue) && maybeValue == null);
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

checks.That("maybe map receives and keeps null", someNull.Map(v => v ?? null) == Maybe<string>.Some(null));
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
checks.That("maybe traverse null payload", nullItems.TryGetValue(out string[] collectedItems) && collectedItems.Length == 2 && collectedItems[0] == null);
checks.That("choose null payload", new string[] { null }.Choose(v => Maybe<string>.Some(v)).Single() == null);
checks.Throws<ArgumentNullException>("choose null selector", () => Array.Empty<string>().Choose<string, string>(null));
checks.That("to result null payload and error", someNull.ToResult<string>(null) == Result<string, string>.Ok(null)
    && Maybe<string>.None.ToResult<string>(null) == Result<string, string>.Error(null));
checks.That("to result else null error", Maybe<string>.None.ToResultElse<string>(() => null) == Result<string, string>.Error(null));
checks.Throws<ArgumentNullException>("to result else null factory", () => someNull.ToResultElse<string>(null));
checks.That("to maybe null payload", okNull.ToMaybe() == someNull && errorNull.ToMaybe() == Maybe<string>.None);
checks.Throws<InvalidOperationException>("to maybe default", () => default(Result<string, string>).ToMaybe());
checks.That("maybe traverse null ok", someNull.Traverse(v => Result<string, string>.Ok(v)) == Result<Maybe<string>, string>.Ok(someNull));
checks.That("maybe traverse null error", someNull.Traverse(v => Result<string, string>.Error(null)) == Result<Maybe<string>, string>.Error(null));
checks.Throws<ArgumentNullException>("maybe traverse null selector", () => Maybe<string>.None.Traverse<string, string, string>(null));

checks.Throws<ArgumentNullException>("presence value null", () => Presence<string>.Value(null));
checks.Throws<ArgumentNullException>("presence value empty nullable", () => Presence<int?>.Value(null));
checks.That("presence value non-empty nullable keeps type", Presence<int?>.Value(1).TryGetValue(out int? nullableNumber) && nullableNumber.HasValue);
checks.Throws<InvalidOperationException>("presence map null result", () => Presence<int>.Value(1).Map(_ => (string)null));
checks.Throws<InvalidOperationException>("presence map empty nullable result", () => Presence<int>.Value(1).Map(_ => (int?)null));
checks.That("presence map skips selector on null", Presence<string>.Null.Map<string>(_ => null).IsNull);
checks.Throws<ArgumentNullException>("presence map null selector", () => Presence<string>.Missing.Map<string>(null));
checks.Throws<ArgumentNullException>("presence match null handler", () => Presence<string>.Missing.Match(null, () => 0, () => 1));
checks.That("presence inactive output", !Presence<string>.Null.TryGetValue(out string inactivePresence) && inactivePresence == null);
checks.Text("presence null text", "Presence.Null", Presence<string>.Null.ToString());

var validNull = Validation<string, string>.Valid(null);
checks.That("validation valid null", validNull.IsValid && validNull.TryGetValue(out string validNullValue, out var validNullIssues) && validNullValue == null && validNullIssues.IsEmpty);
var invalidNull = Validation<string, string>.Invalid(null);
checks.That("validation invalid null", !invalidNull.TryGetValue(out _, out var invalidNullIssues) && invalidNullIssues.Length == 1 && invalidNullIssues[0] == null);
checks.Text("validation null texts", "Validation.Valid(null) Validation.Invalid([null])", validNull + " " + invalidNull);
checks.Throws<ArgumentException>("validation invalid many default", () => Validation<string, string>.InvalidMany(default));
checks.Throws<InvalidOperationException>("validation default match with null handlers", () => default(Validation<string, string>).Match<int>(null, null));
checks.Throws<ArgumentNullException>("validation null zip selector", () => validNull.Zip<string, string>(validNull, null));
checks.Throws<ArgumentNullException>("validation null map issues selector on valid", () => validNull.MapIssues<string>(null));
checks.Throws<ArgumentNullException>("validation null or else fallback on valid", () => validNull.OrElse<string>(null));
checks.Throws<InvalidOperationException>("validation and then default outcome", () => validNull.AndThen(_ => default(Validation<string, string>)));
checks.That("validation map issues keeps null", invalidNull.MapIssues(issue => issue) == invalidNull);
var nullTraverse = new string[] { null, "a" }.Traverse(v => v == null ? Validation<string, string>.Invalid(null) : Validation<string, string>.Valid(v));
checks.That("validation traverse null issue", nullTraverse == Validation<string[], string>.Invalid(null));
checks.Throws<ArgumentNullException>("validation traverse null source", () => ((string[])null).Traverse(v => Validation<string, string>.Valid(v)));

checks.Throws<InvalidOperationException>("presence traverse oblivious ok null", () => Presence<string>.Value("a").Traverse(_ => Result<string, string>.Ok(null)));
checks.That("presence traverse error null", Presence<string>.Value("a").Traverse(_ => Result<string, string>.Error(null)) == Result<Presence<string>, string>.Error(null));
checks.Throws<ArgumentNullException>("presence traverse null selector on null", () => Presence<string>.Null.Traverse<string, string, string>(null));
checks.Throws<ArgumentNullException>("presence and then oblivious value null", () => Presence<string>.Value("a").AndThen(_ => Presence<string>.Value(null)));
checks.Throws<ArgumentNullException>("presence filter null predicate on missing", () => Presence<string>.Missing.FilterToNull(null));
checks.That("inspect null payload", okNull.Inspect(v => checks.That("inspect receives null", v == null)) == okNull);
checks.Throws<ArgumentNullException>("inspect error null action on success", () => okNull.InspectError(null));
checks.That("to validation null error", errorNull.ToValidation() == Validation<string, string>.Invalid(null));
checks.That("to result null issue", invalidNull.ToResult().TryGetValue(out _, out var bridgedNullIssues) == false && bridgedNullIssues[0] == null);
checks.That("validation map null payload", validNull.Map(v => v == null) == Validation<bool, string>.Valid(true));
checks.That("validation ensure null issue", validNull.Ensure(v => v != null, _ => null) == Validation<string, string>.Invalid(null));

var sequencedNulls = new[] { Result<string, string>.Ok(null), Result<string, string>.Ok("a") }.Sequence();
checks.That("null payload sequence", sequencedNulls.TryGetValue(out string[] sequenced, out _) && sequenced.Length == 2 && sequenced[0] == null);
checks.That("null error sequence", new[] { Result<string, string>.Error(null) }.Sequence() == Result<string[], string>.Error(null));
checks.That("null payload maybe sequence", new[] { Maybe<string>.Some(null) }.Sequence().Map(v => v[0] == null) == Maybe<bool>.Some(true));
checks.That("null issue validate all", new[] { Validation<string, string>.Valid(null), Validation<string, string>.Invalid(null) }.ValidateAll() == Validation<Unit, string>.Invalid(null));
checks.That("null payload validate all selector", new string[] { null }.ValidateAll(v => Validation<string, string>.Valid(v)) == Validation<Unit, string>.Valid(Unit.Value));
checks.Throws<ArgumentNullException>("null validate all source", () => ((Validation<string, string>[])null).ValidateAll());
checks.That("null element at most one", new string[] { null }.AtMostOne(() => "many") == Result<Maybe<string>, string>.Ok(Maybe<string>.Some(null)));
checks.That("null error at most one", new string[] { null, null }.AtMostOne(() => (string)null) == Result<Maybe<string>, string>.Error(null));
checks.Throws<ArgumentNullException>("null at most one factory", () => Array.Empty<string>().AtMostOne<string, string>(null));
checks.That("null payload query", (from a in okNull from b in okNull select a == null && b == null) == Result<bool, string>.Ok(true));
checks.That("null project result", okNull.SelectMany(v => okNull, (a, b) => (string)null) == Result<string, string>.Ok(null));
checks.Throws<ArgumentNullException>("null select many next", () => errorNull.SelectMany<string, string, string, string>(null, null));
checks.That("null maybe query where", (from v in Maybe<string>.Some(null) where v == null select "kept") == Maybe<string>.Some("kept"));
checks.Throws<ArgumentNullException>("null maybe where predicate", () => Maybe<string>.None.Where(null));

return checks.Complete();
