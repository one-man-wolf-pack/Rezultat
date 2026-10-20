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

return checks.Complete();
