# Rezultat

Explicit outcome types for .NET: `Result<T, TError>`, `Maybe<T>`, `Presence<T>`, `Validation<T, TIssue>` and `Unit`
in one `net10.0` NuGet package, `Rezultat`, without dependencies beyond the .NET framework. A method returns an
outcome, and the caller handles success, absence or problems with ordinary C#.

## Choose a type

| The operation tells the caller | Use |
| --- | --- |
| A value; there is no expected absence or failure | a plain `T` |
| One kind of absence, and `null` is enough to say it | `T?` |
| Whether a value is there, also when the found value itself may be `null` | `Maybe<T>` |
| Whether an input field was not sent, sent as `null` or sent with a value | `Presence<T>` |
| Success, or an expected failure whose reason the caller uses | `Result<T, TError>` |
| The result of independent checks, with every problem they found | `Validation<T, TIssue>` |

`Unit` is the payload of an outcome without a value, such as `Result<Unit, TError>`. The
[package README](src/Rezultat/README.md) explains this choice in more detail, together with states, defaults and
`null`, every operation, HTTP and JSON examples and the limits; it is the text shipped inside the package.

## Use the package

The packages are issued to a private local NuGet feed, not to nuget.org. Register the feed once (see
[Local releases](#local-releases) for other locations and projects with their own `NuGet.Config`), then reference
the package:

```sh
dotnet nuget add source "$HOME/.local/share/nuget/rezultat" --name rezultat-local
dotnet add package Rezultat
```

```csharp
using Rezultat;

Result<int, string> ParseQuantity(string text) =>
    int.TryParse(text, out var value) && value > 0
        ? Result<int, string>.Ok(value)
        : Result<int, string>.Error($"'{text}' is not a positive quantity");

foreach (var input in new[] { "3", "zero" })
{
    if (!ParseQuantity(input).TryGetValue(out var quantity, out var error))
    {
        Console.WriteLine($"rejected: {error}");
        continue;
    }

    Console.WriteLine($"ordering {quantity}");
}
```

Optional packages:

- `Rezultat.Testing.Xunit` ([README](src/Rezultat.Testing.Xunit/README.md)): `ShouldBeOk`, `ShouldBeError`,
  `ShouldBeValid` and `ShouldBeInvalid` for xUnit v2 and v3 test projects.
- `Rezultat.Analyzers` ([README](src/Rezultat.Analyzers/README.md)): the `RZT0001` warning for a discarded `Result`
  or `Validation`, referenced per project with `PrivateAssets="all"`.

Contracts, decisions and policies live in the specification repository (`../rezultat-specification`): carrier
behavior in `rfc/`, technical mapping in `realization/`, engineering rules in `constitutions/`.

## Limits

- `net10.0` only. Qualification runs in a Linux ARM64/glibc container with the SDK pinned in `global.json`,
  including trimmed and Native AOT publication of a package consumer; other platforms are not qualified here.
- Issued only to a private local feed: there is no public repository URL, Source Link or symbol server yet.
- `0.y.z` versions promise no source, binary or behavioral compatibility. The reference diamond (two separately
  packed libraries exchanging carriers in one app) is checked for libraries built against the candidate version and
  for one built against the issued 0.8.0 and run with the candidate; other version pairs are not covered.
- There is no general zero-allocation claim; `just alloc` measures selected operations in warmed
  Release.

## Agent-built, specification-first

All code is written by LLM agents under human direction. Development is specification-first: agents maintain the
specification, implement scoped tasks and review changes against its requirements, architecture, design and
engineering standards. Tests, measurements, benchmarks and independent agent feedback inform technical decisions.

Bug reports and feature requests are welcome; code PRs are not accepted. Proposed changes go through specification
and architecture review, with adoption based on the project's goals and constraints.

## Development

The consumer READMEs `src/Rezultat/README.md`, `src/Rezultat.Testing.Xunit/README.md` and
`src/Rezultat.Analyzers/README.md` are packed into the root of their `.nupkg`. Their `csharp` examples, and those of
this README, are compiled against the built package and run (or checked for the `RZT0001` lines they mark). Tagged
blocks of the core README are compiled from the installed package by their own checking consumers: `csharp shouldly`
by `qualification/ShouldlyRecipeConsumer` (with Shouldly and xUnit v3, without `Rezultat.Testing.Xunit`),
`csharp http` by `qualification/HttpReadmeRecipe` (ASP.NET Core shared framework, no host) and `csharp json` by
`qualification/JsonReadmeRecipe`. All packages share the version and packaging settings in
`src/Directory.Build.props`.

`.editorconfig` and `Directory.Build.props` make every restore and build equally strict, focused or not: compiler
and MSBuild warnings are errors, with the .NET 10 recommended analysis, the enabled code-style rules and the selected
Roslynator rules. `Directory.Packages.props` owns the versions, including those used by `qualification/` projects
outside central package management. The core and `Rezultat.Testing.Xunit` track their public API in
`PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`; an unrecorded API change fails the build, and `just fix`
records it for review. Owned qualification support code inherits the profile; consumers whose restored package graph
is inspected keep it without the private analyzers. Projects marked `RezultatConsumerInput` (diagnostic corpus,
compiler controls, compiled README text and runner fixtures) compile with ordinary consumer settings and keep their
source layout, so development diagnostics do not change what they check.

The solution's projects restore from committed `packages.lock.json` files in locked mode. After an intentional
dependency change, run an ordinary `dotnet restore Rezultat.slnx` and review the lock diff. Qualification projects
that consume the freshly packed candidates, or pin a varied xUnit version, do not use lock files: they resolve into the
isolated `artifacts/qualification/packages`, which the lanes reset, and their resolved graphs are inspected.

## Prerequisites

- .NET SDK pinned in `global.json`; tests use Microsoft.Testing.Platform mode of `dotnet test`. The xUnit v2
  consumer `qualification/XunitV2Consumer` has its own `global.json` with the same SDK and VSTest mode; keep the
  two SDK versions equal.
- [Just](https://just.systems) for the commands below, plus Git and Bash.
- Docker with a native `linux/arm64` engine for the Linux qualification contour.

## Commands

All recurring operations go through the root `justfile`; `just` lists them.

```sh
just check                  # Just/source formatting, strict Release build, every test module; changes no source
just test                   # every test module of the solution; arguments use native MTP syntax
just test-project tests/Rezultat.Tests/Rezultat.Tests.csproj --filter-class Rezultat.Tests.ResultMapTests
just fmt                    # whitespace and Just formatting only
just fix --diagnostics IDE0011   # supported style/analyzer repairs, optionally scoped; review the diff
just pack                   # artifacts/package/release: Rezultat and Rezultat.Testing.Xunit .nupkg/.snupkg, Rezultat.Analyzers .nupkg
just consumers              # package consumers, compiler controls, README examples and recipes, reference diamond, package inspection
just diamond                # only the reference diamond (needs the issued Rezultat 0.8.0, see below)
just assertions             # assertion package consumers, xUnit v2/v3 runner scenarios, package inspection
just analyzers              # RZT0001 corpus in all severity modes, downstream isolation, README, package inspection
just alloc                  # warmed Release allocation controls
just publish <rid>          # rooted and unrooted trimmed and Native AOT consumer publication and run
just qualify <rid>          # check, pack, every package lane and publish for the runtime
just linux qualify linux-arm64   # the same recipes inside the Linux ARM64/glibc SDK container
just linux pack analyzers   # any recipes inside the same container
just release <commit> Rezultat   # commit-bound package build with its source record (see Local releases)
just release-controls       # release/publish controls in a disposable Git fixture (host Git, Just and Docker)
just gc                     # delete artifacts/ (not the local feed)
```

Each test module receives `--minimum-expected-tests 1` after the SDK `--` separator, so a required module that runs
no test, because it is empty or every selected test is skipped, fails even when other modules pass. `test-project`
restores and builds only the selected project and its references. `check` and `test` never change sources; `fmt` and
`fix` are the only commands that do. Qualification consumers outside the solution get the same
profile through their lane builds; after a lane has restored one, repair it with
`dotnet format qualification/<project> --no-restore --severity warn`. Restore refuses to run while a tracked lock file is deleted or its deletion is
staged.

`consumers`, `diamond`, `assertions`, `analyzers`, `alloc` and `publish` consume the packages from
`artifacts/package/release` through `qualification/NuGet.Config` and an isolated package folder, so run `pack` first.
`just linux` runs the same recipe bodies on the host and sends each `dotnet` and lane command through
`scripts/linux.sh` into the SDK container, with the source mounted read-only; container outputs are kept separately in
`artifacts/linux-arm64`. The container compiles with `UseSharedCompilation=false` (standalone compiler, no reused
compiler server) and does not retry failed builds. Paths passed to recipes are relative to the repository root.
`assertions` runs expected-failure fixtures on purpose and checks the native reports kept in
`artifacts/qualification/runner-reports`. `analyzers` compares every diagnostic of each build with the markers in the
checked sources: `/*ID*/` expects the diagnostic at the following token (the next line's first token when the marker
stands alone), a trailing `// ID` anywhere on its line. Build logs are kept in `artifacts/qualification/analyzer-logs`.

`diamond` packs `qualification/Diamond/Inventory` and `qualification/Diamond/Orders` separately and restores them as
packages into `qualification/Diamond/App`, which passes `Result` and `Validation` values from one to the other. It
runs twice in fresh package folders: with both libraries built against the candidate, and with Inventory built
against the issued `Rezultat` 0.8.0 while Orders and the app use the candidate. The 0.8.0 archive is read from the
local feed (`REZULTAT_LOCAL_FEED`, default below; `linux` mounts it read-only), must have the SHA-256 recorded at its
issue and is copied into `artifacts/qualification/diamond`. `qualification/Diamond/Inspector` checks the restore
inputs, resolved versions, sources and bytes of every case; results stay in `artifacts/qualification/diamond/<case>`.

## Local releases

The durable local feed defaults to `$HOME/.local/share/nuget/rezultat`, outside checkout,
build outputs and NuGet caches. `REZULTAT_LOCAL_FEED` or the last command argument can
select another directory.

Packages built from the working tree are candidates for review and are never issued. An issued
package is built from a commit, and its private source record goes with it into the feed:

```sh
just release <commit> Rezultat                                    # name each package to issue
just --justfile artifacts/release/<full-commit>/justfile linux consumers   # qualify those exact archives
scripts/publish-local.sh 0.8.2 artifacts/release/<full-commit>/artifacts/linux-arm64/package/release
# Other packages and explicit destination:
scripts/publish-local.sh --package Rezultat.Analyzers <version> <package-directory> /absolute/path/to/rezultat-feed
```

`release` exports the committed tree of `<commit>` with `git archive` into `artifacts/release/<full-commit>`,
so modified and untracked working-tree files never enter the build, and runs that tree's own `just linux pack`.
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
| `tests/Rezultat.Tests`, `tests/Rezultat.Testing.Xunit.Tests`, `tests/Rezultat.Analyzers.Tests` | Own behavioral and property tests (xUnit v3 on Microsoft.Testing.Platform, Shouldly assertions, FsCheck) and in-process Roslyn analyzer tests. |
| `qualification/` | Package consumers, compiler controls, README examples and recipe consumers, xUnit v2/v3 runner consumers and report checks, analyzer corpus/isolation consumers and diagnostic report checks, allocation controls and package inspectors. The inspectors and report checks are in the solution; the consumers restore only from the built packages. |
| `qualification/Diamond` | Reference diamond fixtures: two packable libraries, the consuming app and the graph inspector, with their own package sources. |
| `qualification/ReleaseControls` | Release build and publication controls against a disposable Git fixture and temporary feeds. |
| `justfile` | The command interface. |
| `scripts/qualify.sh` | Package qualification and release lanes behind the `justfile`. |
| `scripts/linux.sh` | Transport of one command into the Linux ARM64/glibc SDK container. |
| `scripts/publish-local.sh`, `scripts/release-record.sh` | Local feed publication and the package source record. |

## License

MIT, see `LICENSE`.
