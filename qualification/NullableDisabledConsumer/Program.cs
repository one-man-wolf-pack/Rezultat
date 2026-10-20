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

return checks.Complete();
