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

The first public stable release is `1.0.0` of the three packages. A version is available only once it is listed on
the [GitHub releases](https://github.com/one-man-wolf-pack/Rezultat/releases) page and on
[nuget.org](https://www.nuget.org/packages/Rezultat); before `1.0.0` is listed there, the command below does not
resolve. Source in this repository does not make a release by itself: release-please sets `version.txt` in its release
pull request, and [.github/RELEASING.md](.github/RELEASING.md) describes the release steps. Packages built from a
working tree are candidates and are never issued. With `1.0.0` published:

```sh
dotnet add package Rezultat --version 1.0.0
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
  `ShouldBeValid`, `ShouldBeInvalid`, `ShouldBeSome` and `ShouldBeNone` for xUnit v2 and v3 test projects.
- `Rezultat.Analyzers` ([README](src/Rezultat.Analyzers/README.md)): the `RZT0001` warning for a discarded `Result`
  or `Validation`, referenced per project with `PrivateAssets="all"`.

The package READMEs are the consumer contract; nothing else needs to be checked out to use the packages. Source:
https://github.com/one-man-wolf-pack/Rezultat. Questions, bug reports and feature requests:
https://github.com/one-man-wolf-pack/Rezultat/issues.

## Versions and compatibility

From `1.0.0` the packages follow [Semantic Versioning](https://semver.org/spec/v2.0.0.html) as one version line:
compatible fixes are patch releases, compatible additions minor releases, and an incompatible change of the public
contract a new major version. The contract covers the public source and binary API with its nullable annotations,
the behavior the READMEs document (states, defaults and `null`, guards, evaluation, failures) and their dependency
and support limits; for `Rezultat.Analyzers` it covers `RZT0001`, its configuration and severity, and the documented
reports. It does not cover internal layout, hash codes across processes, performance or unqualified platforms,
SDKs and IDEs. Correcting behavior that deviates from the documented contract is a fix, not a break.

The assembly version stays `1.0.0.0` for every `1.x` release, so a newer `1.x` binds where an older one was
referenced; the package and file versions name the release. The promise starts with the first published `1.0.0`;
the earlier private `0.y.z` versions carry none, and their assembly version is the full number, such as `0.8.3.0`.

## Limits

- `net10.0` only. CI qualifies on Linux x64/glibc (Ubuntu 24.04) with the SDK pinned in `global.json`, including
  trimmed and Native AOT publication of a package consumer; `just linux` runs the same checks in a local Linux
  ARM64/glibc container. Other platforms and architectures are not qualified here.
- Source Link and the commit in package metadata are produced only by a release build of an exact commit; ordinary
  candidate packages name the repository but no commit. Download of the packages, symbols and sources from
  nuget.org, its symbol server and GitHub is not verified before the release.
- The reference diamond (two separately packed libraries exchanging carriers in one app) is checked for libraries
  built against the candidate version; a separate local check runs one built against the private 0.8.0 with the
  candidate. That check is evidence, not a compatibility promise for `0.y.z`; other version pairs are not covered.
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
`csharp http` by `qualification/HttpReadmeRecipe` (ASP.NET Core shared framework, no host), `csharp json` by
`qualification/JsonReadmeRecipe`, `csharp async` by `qualification/AsyncReadmeRecipe` (pending, cancelled and
faulted steps) and `csharp span` by `qualification/SpanReadmeRecipe`. All packages share the version and packaging
settings in `src/Directory.Build.props`.

`.editorconfig` and `Directory.Build.props` make every restore and build equally strict, focused or not: compiler
and MSBuild warnings are errors, with the .NET 10 recommended analysis, the enabled code-style rules and the selected
Roslynator rules. `Directory.Packages.props` owns the versions, including those used by `qualification/` projects
outside central package management. The core and `Rezultat.Testing.Xunit` track their public API in
`PublicAPI.Shipped.txt` and `PublicAPI.Unshipped.txt`; an unrecorded API change fails the build, and `just fix`
records it for review. `Shipped` holds the accepted surface of the first stable release; `Unshipped` stays empty
until a later change adds to it. Owned qualification support code inherits the profile; consumers whose restored
package graph is inspected keep it without the private analyzers. Projects marked `RezultatConsumerInput`
(diagnostic corpus, compiler controls, compiled README text and runner fixtures) compile with ordinary consumer
settings and keep their source layout, so development diagnostics do not change what they check.

The solution's projects restore from committed `packages.lock.json` files in locked mode. After an intentional
dependency change, run an ordinary `dotnet restore Rezultat.slnx` and review the lock diff. Qualification projects
that consume the freshly packed candidates, or pin a varied xUnit version, do not use lock files: they resolve into the
isolated `artifacts/qualification/packages` (under `just linux`, `/cache/qualification/packages` in the container's
cache volume), which the lanes reset, and their resolved graphs are inspected.

## Prerequisites

- .NET SDK pinned in `global.json`; tests use Microsoft.Testing.Platform mode of `dotnet test`. The xUnit v2
  consumer `qualification/XunitV2Consumer` has its own `global.json` with the same SDK and VSTest mode; keep the
  two SDK versions equal.
- [Just](https://just.systems) for the commands below, plus Git and Bash. The release lane, `publish-local.sh` and
  the release controls also run `unzip`, `xmllint` and `sha256sum` on the host; the controls also need `zip`.
- For `just publish linux-x64` on Linux, the Native AOT prerequisites (`clang` and `zlib1g-dev` on Ubuntu).
- Docker for `just lint-workflows`, and with a native `linux/arm64` engine for the optional `just linux` contour.

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
just diamond                # only the same-version reference diamond
just diamond-historical     # the mixed-version diamond with the private Rezultat 0.8.0 (see below); not in qualify
just assertions             # assertion package consumers, xUnit v2/v3 runner scenarios, package inspection
just analyzers              # RZT0001 corpus in all severity modes, downstream isolation, README, package inspection
just alloc                  # warmed Release allocation controls
just publish <rid>          # rooted and unrooted trimmed and Native AOT consumer publication and run
just qualify <rid>          # check, pack, every package lane and publish for the runtime
just linux qualify linux-arm64   # the same recipes inside the Linux ARM64/glibc SDK container
just linux pack analyzers   # any recipes inside the same container
just release <commit> Rezultat   # source-bound package build with its source record on this host (see Releases)
just linux release <commit> Rezultat   # the same build in the Linux ARM64/glibc container
just release-controls       # release/publish controls in a disposable Git fixture (host tools above); `just linux release-controls` packs in the container
just lint-workflows         # actionlint over .github/workflows in its pinned Docker image
just gc                     # delete artifacts/ and its Linux cache volumes (not the local feed)
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
`artifacts/linux-arm64`. NuGet's package, HTTP and scratch caches, the CLI home and the qualification and diamond
package folders are not on the host share: they live under `/cache` in a Docker volume of that checkout and SDK image,
created empty on first use, owned by the calling user and named in `artifacts/linux-arm64/nuget-volume-*`. Every
`just linux` command of the checkout reuses it; `just gc` removes the recorded volumes with the outputs, and a
release clone always starts with a new one. The container compiles with `UseSharedCompilation=false` (standalone
compiler, no reused compiler server) and does not retry failed builds. Paths passed to recipes are relative to the
repository root.
`assertions` runs expected-failure fixtures on purpose and checks the native reports kept in
`artifacts/qualification/runner-reports`. `analyzers` compares every diagnostic of each build with the markers in the
checked sources: `/*ID*/` expects the diagnostic at the following token (the next line's first token when the marker
stands alone), a trailing `// ID` anywhere on its line. Build logs are kept in `artifacts/qualification/analyzer-logs`.

`diamond` packs `qualification/Diamond/Inventory` and `qualification/Diamond/Orders` separately and restores them as
packages into `qualification/Diamond/App`, which passes `Result` and `Validation` values from one to the other, in a
fresh package folder with both libraries built against the candidate; `consumers` runs it. `diamond-historical` runs
the same graph with Inventory built against the issued `Rezultat` 0.8.0 while Orders and the app use the candidate.
The 0.8.0 archive is read from the local feed (`REZULTAT_LOCAL_FEED`, default below; `linux` mounts it read-only),
must have the SHA-256 recorded at its issue and is copied into `artifacts/qualification/diamond`; without it the
command fails. `qualification/Diamond/Inspector` checks the restore inputs, resolved versions, sources and bytes of
every case; results stay in `artifacts/qualification/diamond/<case>`, the restored packages of each case in its own
`qualification/diamond/<case>/packages` under the restore root.

## Releases

Packages have two kinds of origin, and the package metadata says which one:

- A candidate from `just pack` is built from the working tree, which may hold modified or untracked files. Its
  nuspec names the project and repository URL but no commit, its informational version is the bare package version,
  and its PDB has no Source Link. Candidates are for review and qualification; they are never issued.
- A release from `just release <commit> <package-id>...` is built from that commit only. The lane clones this
  repository into `artifacts/release/<full-commit>`, sets the clone's `origin` to the `RepositoryUrl` of that commit
  (a credential-free GitHub URL, otherwise it refuses) and checks out the commit detached. The clone's own
  `just pack-bound` refuses unless the checkout is exactly that commit, clean and with that origin, then packs
  with the SDK's Git and GitHub Source Link support. It restores into empty NuGet caches under the clone; with
  `just linux release` it packs in the container instead, with a new cache volume, and its outputs are under
  `artifacts/linux-arm64` of the clone. The nuspec names the repository and commit, the informational
  version is `<version>+<commit>`, and the PDB maps `/_/*` to `https://raw.githubusercontent.com/<owner>/<repo>/<commit>/*`.
  For each named package the lane checks that commit and URL in the nuspec and writes `<id>.<version>.record`:
  package ID, version, full commit and SHA-256 of the `.nupkg` and, when shipped, `.snupkg`.

Qualify a release with the clone's own lanes; they inspect the packages against the recorded commit:

```sh
just release <commit> Rezultat Rezultat.Testing.Xunit Rezultat.Analyzers
just --justfile artifacts/release/<full-commit>/justfile consumers assertions analyzers alloc publish linux-x64
# or, in the Linux ARM64/glibc container:
just linux release <commit> Rezultat Rezultat.Testing.Xunit Rezultat.Analyzers
just --justfile artifacts/release/<full-commit>/justfile linux consumers assertions analyzers alloc publish linux-arm64
```

Generated sources (assembly attributes, global usings) are in the PDB with their checksums but are not in the
repository, so Source Link cannot serve them. Running `release` again removes the clone with its cache volume, then
rebuilds it and its archives from an empty cache.

### Versions and CI

`version.txt` holds the package version of all three packages; MSBuild reads it into `VersionPrefix`. Pull requests
and pushes to `main` run `just lint-workflows`, `just qualify linux-x64` and `just release-controls` directly on a
Linux x64 GitHub Actions runner. Public releases are prepared by release-please and built, qualified and published by
`.github/workflows/release.yml` from the tagged commit; [.github/RELEASING.md](.github/RELEASING.md) lists the
repository settings and the steps of the first public release.

### Local feed

The durable local feed defaults to `$HOME/.local/share/nuget/rezultat`, outside checkout, build outputs and NuGet
caches. `REZULTAT_LOCAL_FEED` or the last command argument can select another directory. It holds the private
`0.y.z` packages and keeps released archives with their records.

```sh
scripts/publish-local.sh <version> artifacts/release/<full-commit>/artifacts/linux-arm64/package/release   # after `just linux release`
# Other packages and explicit destination:
scripts/publish-local.sh --package Rezultat.Analyzers <version> <package-directory> /absolute/path/to/rezultat-feed
```

`publish-local.sh` publishes one package (`Rezultat` by default) only with a matching record: it reads the
package ID and version from the archives' nuspec, compares the hashes and refuses a missing record, an
`unknown` revision or any mismatch. A stable version (`1.0.0` and later) must also be the release build of its
recorded commit: a nuspec without that commit or with another repository URL is refused. It copies without
repacking, publishes the symbols archive, then the record into `records/`, then the package, as read-only files
with atomic no-replace names. Publishing identical files again is a no-op; different bytes under an issued version
fail. Changed package contents always receive a new private version: change `version.txt` following the rules
above. The packages of one `release` share that version; publish only the changed ones and keep
the issued versions of the others. Do not use `dotnet nuget push` directly to this directory: its local-folder path
can overwrite a version. Keep this feed when cleaning build outputs. `Rezultat.Analyzers` has no `.snupkg`. The
`symbols/` directory retains matching `.snupkg` files; it is an archive, not a symbol server. A record is not a
reproducible-build proof or signature and gives no source retrieval.

A private `0.y.z` package issued before records existed keeps its bytes and gets a record with
`source-revision=unknown` and the hashes of the issued files. A stable version never gets such a record:

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
is `<PackageReference Include="Rezultat" Version="[0.8.3]" />`; projects with central package
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
| `scripts/publish-local.sh`, `scripts/release-record.sh` | Local feed publication, the package source record and its source-binding check. |

## License

MIT, see `LICENSE`.
