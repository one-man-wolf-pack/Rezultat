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
over the `Rezultat` package, each also with a required `includePayload` flag that keeps payloads and issues out of
the failure message when `false`. It throws `Xunit.Sdk.FailException` from `xunit.v3.assert`, a runtime-only
dependency, and is qualified with xUnit v2 and v3 test projects.

The optional compile-time package `Rezultat.Analyzers` (`src/Rezultat.Analyzers`) is a `netstandard2.0` Roslyn
analyzer built against the Roslyn 4.14 API with one diagnostic, `RZT0001`, for a directly discarded `Result` or
`Validation` (including `Task`/`ValueTask` of one, awaited outcomes and implicit void/`Action` lambda bodies). It is
packed as an analyzer-only `analyzers/dotnet/cs` asset without dependencies or symbols package, connected with
`PrivateAssets="all" IncludeAssets="analyzers"` and qualified with the .NET SDK 10.0.402 command line.

The consumer-facing package READMEs are `src/Rezultat/README.md`, `src/Rezultat.Testing.Xunit/README.md` and
`src/Rezultat.Analyzers/README.md`; each is packed into its `.nupkg` root and its `csharp` examples are compiled against
the built package (and run, or checked for the `RZT0001` lines they mark). The core README also carries the
migration guide and a Shouldly recipe in `csharp shouldly` blocks, which `qualification/ShouldlyRecipeConsumer`
compiles and tests from the installed package with Shouldly and xUnit v3, without `Rezultat.Testing.Xunit`. All packages share the version and
packaging settings in `src/Directory.Build.props`.

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
scripts/qualify.sh pack                  # artifacts/package/{Rezultat,Rezultat.Testing.Xunit}.<version>.nupkg/.snupkg, Rezultat.Analyzers.<version>.nupkg
scripts/qualify.sh consumers             # package consumers, compiler controls, README examples, Shouldly recipe, package inspection
scripts/qualify.sh assertions            # assertion package consumers, xUnit v2/v3 runner scenarios, package inspection
scripts/qualify.sh analyzers             # RZT0001 corpus in all severity modes, downstream isolation, README, package inspection
scripts/qualify.sh alloc                 # warmed Release allocation controls
scripts/qualify.sh publish <host-rid>    # rooted trimmed and Native AOT consumer publication and run
scripts/qualify.sh linux                 # everything above inside the Linux ARM64/glibc SDK container
scripts/qualify.sh linux analyzers       # one command inside the same container
scripts/qualify.sh release <commit> Rezultat   # commit-bound package build with its source record (see Local releases)
scripts/qualify.sh release-controls      # release/publish controls in a disposable Git fixture (host Git and Docker)
```

`consumers`, `assertions`, `analyzers`, `alloc` and `publish` consume the packages from `artifacts/package` through
`qualification/NuGet.Config` and an isolated package folder, so run `pack` first.
Linux container outputs are kept separately in `artifacts/linux-arm64`. `assertions` runs expected-failure
fixtures on purpose and checks the native reports kept in `artifacts/qualification/runner-reports`. `analyzers`
compares every diagnostic of each build with the markers in the checked sources: `/*ID*/` expects the diagnostic at
the following token (the next line's first token when the marker stands alone), a trailing `// ID` anywhere on its
line. Build logs are kept in `artifacts/qualification/analyzer-logs`.

## Local releases

The durable local feed defaults to `$HOME/.local/share/nuget/rezultat`, outside checkout,
build outputs and NuGet caches. `REZULTAT_LOCAL_FEED` or the last command argument can
select another directory.

Packages built from the working tree are candidates for review and are never issued. An issued
package is built from a commit, and its private source record goes with it into the feed:

```sh
scripts/qualify.sh release <commit> Rezultat                      # name each package to issue
artifacts/release/<full-commit>/scripts/qualify.sh linux consumers   # qualify those exact archives
scripts/publish-local.sh 0.8.2 artifacts/release/<full-commit>/artifacts/linux-arm64/package
# Other packages and explicit destination:
scripts/publish-local.sh --package Rezultat.Analyzers <version> <package-directory> /absolute/path/to/rezultat-feed
```

`release` exports the committed tree of `<commit>` with `git archive` into `artifacts/release/<full-commit>`,
so modified and untracked working-tree files never enter the build, and runs that tree's own `linux pack`.
For each named package it writes `<id>.<version>.record` next to the archives: package ID, version, full
commit and SHA-256 of the `.nupkg` and, when shipped, `.snupkg`. Qualify a package with its own command
in the exported tree (`consumers`, `assertions` or `analyzers`). Running `release` again rebuilds the snapshot
with new archive bytes.

`publish-local.sh` publishes one package (`Rezultat` by default) only with a matching record: it reads the
package ID and version from the archives' nuspec, compares the hashes and refuses a missing record, an
`unknown` revision or any mismatch. It copies without repacking, publishes the symbols archive, then the record
into `records/`, then the package, as read-only files with atomic no-replace names. Publishing identical files
again is a no-op; different bytes under an issued version fail. Changed package contents always receive a new
`0.y.z` version: change `Version` in `src/Directory.Build.props`. The packages of one `release` share that
version; publish only the changed ones and keep the issued versions of the others. Do not use
`dotnet nuget push` directly to this directory: its local-folder path can overwrite a version. Keep this feed
when cleaning build outputs. `Rezultat.Analyzers` has no `.snupkg`. The `symbols/` directory retains matching
`.snupkg` files; it is an archive, not a symbol server or Source Link support claim. A record is not a
reproducible-build proof or signature and gives no source retrieval.

A package issued before records existed keeps its bytes and gets a record with `source-revision=unknown` and the
hashes of the issued files:

```sh
scripts/publish-local.sh --legacy-record 0.8.0
scripts/publish-local.sh --legacy-record --package Rezultat.Testing.Xunit 0.8.0
scripts/publish-local.sh --legacy-record --package Rezultat.Analyzers 0.8.0
```

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

Consumers reference the package, not its source project. An exact reference
is `<PackageReference Include="Rezultat" Version="[0.8.2]" />`; projects with central package
management put the version on their `PackageVersion` entry. The analyzer is referenced per checked project as
`<PackageReference Include="Rezultat.Analyzers" Version="0.8.0" PrivateAssets="all" IncludeAssets="analyzers" />`;
it does not bring `Rezultat` and is not brought by it. Consumer repositories own the
switch and qualification of their API, adapters and deployment profiles.

## Layout

| Path | Content |
| --- | --- |
| `src/Rezultat` | The core library and package; collection extensions in `Collections/`, query extensions in `Linq/`, consumer `README.md`. |
| `src/Rezultat.Testing.Xunit` | The test-only assertions package and its consumer `README.md`. |
| `src/Rezultat.Analyzers` | The `RZT0001` analyzer package and its consumer `README.md`. |
| `tests/Rezultat.Tests`, `tests/Rezultat.Testing.Xunit.Tests`, `tests/Rezultat.Analyzers.Tests` | Own behavioral and property tests (xUnit v3, FsCheck) and in-process Roslyn analyzer tests. |
| `qualification/` | Package consumers, compiler controls, README examples, xUnit v2/v3 runner consumers and report checks, analyzer corpus/isolation consumers and diagnostic report checks, allocation controls and package inspectors; outside the solution and restored only from the built packages. |
| `qualification/ReleaseControls` | Release build and publication controls against a disposable Git fixture and temporary feeds. |
| `scripts/qualify.sh` | Command adapter over native `dotnet`, Git and Docker. |
| `scripts/publish-local.sh`, `scripts/release-record.sh` | Local feed publication and the package source record. |

## License

MIT, see `LICENSE`.
