# Rezultat.Testing.Xunit

Test-only extracting assertions for the `Rezultat` outcome carriers. Each assertion checks the expected
branch and returns its active payload, so the test continues with ordinary framework assertions on that
payload. The package is optional: production code depends only on `Rezultat`.

```xml
<PackageReference Include="Rezultat.Testing.Xunit" Version="0.8.0" />
```

Add it to a test project. It brings the `Rezultat` package and the `xunit.v3.assert` runtime assembly;
it does not bring an xUnit runner, test framework or analyzers.

## Usage

```csharp
using Rezultat;
using Rezultat.Testing.Xunit;
using Xunit;

public sealed class OrderTests
{
    [Fact]
    public void ExtractsTheActiveBranch()
    {
        var saved = Result<int, string>.Ok(42).ShouldBeOk();
        Assert.Equal(42, saved);

        var refused = Result<int, string>.Error("server refused").ShouldBeError();
        Assert.Equal("server refused", refused);

        var name = Validation<string, string>.Valid("Ada").ShouldBeValid();
        Assert.Equal("Ada", name);

        var issues = Validation<string, string>.Invalid("bad name").ShouldBeInvalid();
        Assert.Equal(["bad name"], issues);
    }

    [Fact]
    public void KeepsDeclaredNullability()
    {
        string? text = Result<string?, string>.Ok(null).ShouldBeOk();
        Assert.Null(text);
    }
}
```

The assertions live in `ResultAssertions` (`ShouldBeOk`, `ShouldBeError`) and `ValidationAssertions`
(`ShouldBeValid`, `ShouldBeInvalid`) in the `Rezultat.Testing.Xunit` namespace; they are extension methods
and can also be called as ordinary static methods.

- A matching branch returns the exact payload without formatting or comparing anything. A nullable payload
  stays nullable: success does not prove non-null.
- `ShouldBeInvalid` returns the stored `ImmutableArray<TIssue>` unchanged: same order, repeats and nulls.
- A wrong branch or an uninitialized `default` carrier fails and never returns a substitute value. An
  uninitialized carrier is reported as `Uninitialized`, not as Error or Invalid.
- The failure message names the expected and actual branch and shows the active payload or every issue
  through its `ToString()`. If that `ToString()` throws, the message keeps the mismatch and names the
  exception type. The message is for people; do not parse it.
- `await` the operation first and then assert on the carrier; there are no async variants.

## xUnit versions

Failures throw `Xunit.Sdk.FailException` from `xunit.v3.assert`, also in xUnit v2 tests. In v2 it is not the
v2 `Xunit.Sdk.XunitException`, so `Assert.ThrowsAny<XunitException>` from v2 does not catch it; xUnit v2 still
reports the test as failed, not as a runner error. The helper call and the current lambda stop on failure;
an outer `Assert.Multiple` may still run its other checks.

Qualified with the .NET 10 SDK command line on `net10.0` test projects:

- xUnit v2: `xunit` 2.9.3 with `xunit.runner.visualstudio` 3.1.5 and `Microsoft.NET.Test.Sdk` 18.0.1 under
  `dotnet test` (VSTest);
- xUnit v3: `xunit.v3` 4.0.1 and 4.0.2 under `dotnet test` with Microsoft.Testing.Platform.

The package requires `xunit.v3.assert` 4.0.1 or later. Other versions, runners and IDE test explorers are not
qualified. Trimming and Native AOT of test projects are not supported claims of this package.
