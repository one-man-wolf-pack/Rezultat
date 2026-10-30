# Rezultat.Analyzers

Optional C# compiler diagnostic for the `Rezultat` outcome carriers. Rule `RZT0001` warns where code drops a
`Result<T, TError>` or `Validation<T, TIssue>` value, including `Result<Unit, TError>`, without storing,
returning, passing or explicitly discarding it. It recognizes the carrier types themselves, so every producer
method is covered regardless of its name or assembly. The package contains only the analyzer: it does not
bring the `Rezultat` package, carriers or any runtime code.

```xml
<PackageReference Include="Rezultat.Analyzers" Version="1.0.0" PrivateAssets="all" IncludeAssets="analyzers" />
```

Add this reference to every project whose own source should be checked; a repository can put it into its
`Directory.Build.props` for the selected projects. `PrivateAssets="all"` keeps the analyzer a tool of that
project: projects that reference it, or consume its NuGet package, do not get the rule. Referencing
`Rezultat` alone does not enable the rule.

## What is reported

```csharp
using System;
using System.Threading.Tasks;
using Rezultat;

public sealed class Orders
{
    public Result<int, string> Save(int order) => Result<int, string>.Ok(order);

    public Task<Result<int, string>> SaveAsync(int order) => Task.FromResult(Save(order));

    public static void RunStep(Action step) => step();

    public void Lost(Orders? next)
    {
        Save(1); // RZT0001
        SaveAsync(2); // RZT0001
        RunStep(() => Save(3)); // RZT0001
        next?.Save(4); // RZT0001
    }

    public async Task LostAfterAwait()
    {
        await SaveAsync(5); // RZT0001
        await SaveAsync(6).ConfigureAwait(false); // RZT0001
    }

    public async Task<Result<int, string>> Kept()
    {
        _ = Save(1);
        _ = await SaveAsync(2);
        var saved = Save(3);
        RunStep(() => { _ = Save(4); });
        return saved.IsSuccess ? await SaveAsync(5) : saved;
    }
}
```

The rule reports a call, `new` or `await` statement whose value is the carrier, a `Task` or `ValueTask` of
it that is neither awaited nor kept, a conditional call `receiver?.Operation()` and the implicit statement of
a void method or lambda body converted to `Action` or an async delegate.

An explicit discard `_ = expression` marks intentional ignoring and is never reported. It does not handle the
error, and `_ = SaveAsync(order)` does not await the task or observe its exceptions. Returning, assigning or
passing a value as an argument is not reported either.

The rule does not track what happens to a kept value: unused locals, boxing, `dynamic`, collections and
deeper wrappers such as `Task<Task<Result<T, TError>>>` are out of scope, and reading a flag does not prove
the error was handled. `Maybe<T>` and `Presence<T>` are not reported, generated code is not analyzed, and
same-named types from other assemblies are not carriers. The analyzer never runs consumer code and does not
inspect which branch a carrier holds.

## Severity

`RZT0001` is a warning by default. Configure it in `.editorconfig` or `.globalconfig` like any other
diagnostic:

```ini
[*.cs]
dotnet_diagnostic.RZT0001.severity = error
```

Use `error`, `warning` or `none`. `TreatWarningsAsErrors` or `WarningsAsErrors=RZT0001` also make it fail
the build. The package itself adds no MSBuild properties or severity settings to the consuming project. The
diagnostic message is for people; do not parse it.

## Supported hosts

Qualified with the .NET SDK 10.0.403 command line (`dotnet build`) on macOS ARM64 and Linux ARM64. The
analyzer targets `netstandard2.0` and compiles against the Roslyn 4.14 API. Other SDK versions, IDEs and
their live analysis are not qualified by this release.

## Versions and support

The package shares the version line of `Rezultat` and follows [Semantic Versioning](https://semver.org/spec/v2.0.0.html)
from `1.0.0`: the rule ID `RZT0001`, its configuration and default severity and the reports documented above change
incompatibly only in a new major version. Reporting a case this README says is reported, or no longer reporting one
it says is not, is a fix. The diagnostic message text and the hosts not listed above are not part of the promise.
The earlier `0.y.z` versions carried no compatibility promise.

Source: https://github.com/one-man-wolf-pack/Rezultat. Questions, bug reports and feature requests:
https://github.com/one-man-wolf-pack/Rezultat/issues.

## Release notes

- 1.0.0: the first stable release of `RZT0001` as described here; the package names its project and repository.
