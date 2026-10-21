using System.Collections.Immutable;
using Rezultat;
using Rezultat.Testing.Xunit;

var checks = new Checks();
var reference = new object();
checks.That("ShouldBeOk returns the exact reference", ReferenceEquals(reference, Result<object, string>.Ok(reference).ShouldBeOk()));
checks.That("ShouldBeOk keeps a null payload", Result<string, string>.Ok(null).ShouldBeOk() is null);
checks.That("ShouldBeError keeps a null error", Result<int, string>.Error(null).ShouldBeError() is null);
checks.That("ShouldBeValid keeps a null payload", Validation<string, string>.Valid(null).ShouldBeValid() is null);
var issues = ImmutableArray.Create("b", null, "b");
checks.That("ShouldBeInvalid returns the stored issues", Validation<int, string>.InvalidMany(issues).ShouldBeInvalid() == issues);
try
{
    _ = Result<string, string>.Error(null).ShouldBeOk();
    checks.Fail("wrong branch: no failure");
}
catch (Exception exception)
{
    checks.Text("wrong branch failure type", "Xunit.Sdk.FailException", exception.GetType().FullName);
    checks.Text("wrong branch message", "Expected Result.Ok; actual Result.Error(null).", exception.Message);
}

return checks.Complete();
