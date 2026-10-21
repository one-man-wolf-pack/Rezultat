# Rezultat

Implementation of the Rezultat core library: the `Rezultat` NuGet package with `Result<T, TError>`,
`Maybe<T>`, `Presence<T>`, `Validation<T, TIssue>` and `Unit` for `net10.0`, including synchronous Result
composition with `Inspect`/`InspectError` observers, Maybe composition, Maybe/Result bridges (`ToResult`,
`ToResultElse`, `ResultExtensions.ToMaybe`, Maybe-receiver `MaybeExtensions.Traverse`), Result/Validation bridges
(`ResultExtensions.ToValidation`, `ValidationExtensions.ToResult`), explicit nullable adaptation through
`Maybe.FromNullable`, Presence composition (strict `Map`, `AndThen`, `FilterToMissing`, `FilterToNull` and
fallible `PresenceExtensions.Traverse`), and accumulating Validation composition (`Map`, dependent `Ensure`, `Zip`
for 2–4 inputs, `AndThen`, `OrElse`, `MapIssues`) over `System.Collections.Immutable.ImmutableArray<TIssue>`
issues, all in the `Rezultat` namespace. Operations over BCL collections are opt-in through
`using Rezultat.Collections;`: `ResultCollectionExtensions` (`Traverse`, `Sequence`), `ValidationCollectionExtensions`
(`Traverse`, `Sequence`, issue-only `ValidateAll`) and `MaybeCollectionExtensions` (`FirstOrNone`, `LastOrNone`,
`SingleOrNone`, `AtMostOne`, `ElementAtOrNone`, `GetOrNone`, `Traverse`, `Sequence`, lazy `Choose`). Query syntax for
Result and Maybe is opt-in through `using Rezultat.Linq;`: `ResultQueryExtensions` (`Select`, projected `SelectMany`)
and `MaybeQueryExtensions` (`Select`, projected `SelectMany`, `Where`).

The optional test-only package `Rezultat.Testing.Xunit` (`src/Rezultat.Testing.Xunit`) adds the extracting
assertions `ResultAssertions.ShouldBeOk`/`ShouldBeError` and `ValidationAssertions.ShouldBeValid`/`ShouldBeInvalid`
over the `Rezultat` package. It throws `Xunit.Sdk.FailException` from `xunit.v3.assert`, a runtime-only
dependency, and is qualified with xUnit v2 and v3 test projects.

The consumer-facing package READMEs are `src/Rezultat/README.md` and `src/Rezultat.Testing.Xunit/README.md`; each is
packed into its `.nupkg` root and its `csharp` examples are compiled and run against the built package. Both packages
share the version and packaging settings in `src/Directory.Build.props`.

Contracts, decisions and policies live in the specification repository
(`../rezultat-specification`): carrier behavior in `rfc/`, technical mapping in `realization/`,
engineering rules in `constitutions/`.

## Prerequisites

- .NET SDK pinned in `global.json`; tests use Microsoft.Testing.Platform mode of `dotnet test`. The xUnit v2
  consumer `qualification/XunitV2Consumer` has its own `global.json` with the same SDK and VSTest mode; keep the
  two SDK versions equal.
- Docker with a native `linux/arm64` engine for the Linux qualification contour.

## Commands

All recurring operations go through `scripts/qualify.sh`; run it without arguments for the list.

```sh
scripts/qualify.sh check                 # format verification, warning-free Release build
scripts/qualify.sh test                  # own tests of the solution; extra arguments go to the test platform
scripts/qualify.sh test Rezultat.Tests --filter-class Rezultat.Tests.ResultMapTests   # one test project, filtered
scripts/qualify.sh pack                  # artifacts/package/{Rezultat,Rezultat.Testing.Xunit}.<version>.nupkg and .snupkg
scripts/qualify.sh consumers             # package consumers, compiler controls, README examples, package inspection
scripts/qualify.sh assertions            # assertion package consumers, xUnit v2/v3 runner scenarios, package inspection
scripts/qualify.sh alloc                 # warmed Release allocation controls
scripts/qualify.sh publish <host-rid>    # rooted trimmed and Native AOT consumer publication and run
scripts/qualify.sh linux                 # everything above inside the Linux ARM64/glibc SDK container
```

`consumers`, `assertions`, `alloc` and `publish` consume the packages from `artifacts/package` through
`qualification/NuGet.Config` and an isolated package folder, so run `pack` first.
Linux container outputs are kept separately in `artifacts/linux-arm64`. `assertions` runs expected-failure
fixtures on purpose and checks the native reports kept in `artifacts/qualification/runner-reports`.

## Local releases

The durable local feed defaults to `$HOME/.local/share/nuget/rezultat`, outside checkout,
build outputs and NuGet caches. `REZULTAT_LOCAL_FEED` or the third command argument can
select another directory. Publish the existing qualified Linux artifacts of one package
(`Rezultat` by default, or `--package Rezultat.Testing.Xunit`):

```sh
scripts/publish-local.sh 0.8.0
scripts/publish-local.sh --package Rezultat.Testing.Xunit 0.8.0
# Explicit inputs/destination:
scripts/publish-local.sh 0.8.0 artifacts/linux-arm64/package /absolute/path/to/rezultat-feed
```

For the next release, change `Version` in `src/Directory.Build.props`, qualify the changed
outcome with `scripts/qualify.sh linux`, then publish that version of each package. Changed package
contents always receive a new `0.y.z` version. A repacked package of an already issued version has
different bytes and is refused; keep the issued one and publish only the packages that are new. Publishing identical artifacts again is a no-op;
different bytes under an issued version fail. The command copies without repacking and
publishes read-only files with atomic no-replace names. Do not use `dotnet nuget push`
directly to this directory: its local-folder path can overwrite a version. Keep this feed
when cleaning build outputs. The `symbols/` directory retains matching `.snupkg` files;
it is an archive, not a symbol server or Source Link support claim.

Register the feed for ordinary local CLI/IDE use:

```sh
dotnet nuget add source "$HOME/.local/share/nuget/rezultat" --name rezultat-local
```

A project whose own `NuGet.Config` clears sources must add the feed there. When source
mapping is enabled, map `Rezultat` and `Rezultat.*` to `rezultat-local`.
For example, merge these entries with the project's existing sources/mappings:

```xml
<configuration>
  <packageSources>
    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
    <add key="rezultat-local" value="/absolute/path/to/rezultat-feed" />
  </packageSources>
  <packageSourceMapping>
    <packageSource key="rezultat-local">
      <package pattern="Rezultat" />
      <package pattern="Rezultat.*" />
    </packageSource>
    <packageSource key="nuget.org">
      <package pattern="*" />
    </packageSource>
  </packageSourceMapping>
</configuration>
```

Consumers reference the package, not its source project. An exact first-release reference
is `<PackageReference Include="Rezultat" Version="[0.8.0]" />`; projects with central package
management put the version on their `PackageVersion` entry. Consumer repositories own the
switch and qualification of their API, adapters and deployment profiles.

## Layout

| Path | Content |
| --- | --- |
| `src/Rezultat` | The core library and package; collection extensions in `Collections/`, query extensions in `Linq/`, consumer `README.md`. |
| `src/Rezultat.Testing.Xunit` | The test-only assertions package and its consumer `README.md`. |
| `tests/Rezultat.Tests`, `tests/Rezultat.Testing.Xunit.Tests` | Own behavioral and property tests (xUnit v3, FsCheck). |
| `qualification/` | Package consumers, compiler controls, README examples, xUnit v2/v3 runner consumers and report checks, allocation controls and package inspector; outside the solution and restored only from the built packages. |
| `scripts/qualify.sh` | Command adapter over native `dotnet` and Docker. |

## License

MIT, see `LICENSE`.
