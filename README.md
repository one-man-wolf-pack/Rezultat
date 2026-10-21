# Rezultat

Implementation of the Rezultat core library: the `Rezultat` NuGet package with `Result<T, TError>`,
`Maybe<T>`, `Presence<T>`, `Validation<T, TIssue>` and `Unit` for `net10.0`, including synchronous Result
and Maybe composition, Maybe/Result bridges (`ToResult`, `ToResultElse`, `ResultExtensions.ToMaybe`,
Maybe-receiver `MaybeExtensions.Traverse`), explicit nullable adaptation through `Maybe.FromNullable`, strict
Presence `Map`, and accumulating Validation composition (`Zip` for 2–4 inputs, `AndThen`, `OrElse`,
`MapIssues`) over `System.Collections.Immutable.ImmutableArray<TIssue>` issues, all in the `Rezultat` namespace. Operations over BCL collections are opt-in through
`using Rezultat.Collections;`: `ResultCollectionExtensions.Traverse`, `ValidationCollectionExtensions.Traverse`
and `MaybeCollectionExtensions` (`FirstOrNone`, `LastOrNone`, `SingleOrNone`, `ElementAtOrNone`, `GetOrNone`,
`Traverse`, lazy `Choose`).

The consumer-facing package README is `src/Rezultat/README.md`; it is packed into the `.nupkg` root and its
`csharp` examples are compiled and run against the built package.

Contracts, decisions and policies live in the specification repository
(`../rezultat-specification`): carrier behavior in `rfc/`, technical mapping in `realization/`,
engineering rules in `constitutions/`.

## Prerequisites

- .NET SDK pinned in `global.json`; tests use Microsoft.Testing.Platform mode of `dotnet test`.
- Docker with a native `linux/arm64` engine for the Linux qualification contour.

## Commands

All recurring operations go through `scripts/qualify.sh`; run it without arguments for the list.

```sh
scripts/qualify.sh check                 # format verification, warning-free Release build
scripts/qualify.sh test                  # own tests; extra arguments go to the test platform
scripts/qualify.sh pack                  # artifacts/package/Rezultat.<version>.nupkg and .snupkg
scripts/qualify.sh consumers             # package consumers, compiler controls, README examples, package inspection
scripts/qualify.sh alloc                 # warmed Release allocation controls
scripts/qualify.sh publish <host-rid>    # rooted trimmed and Native AOT consumer publication and run
scripts/qualify.sh linux                 # everything above inside the Linux ARM64/glibc SDK container
```

`consumers`, `alloc` and `publish` consume the package from `artifacts/package` through
`qualification/NuGet.Config` and an isolated package folder, so run `pack` first.
Linux container outputs are kept separately in `artifacts/linux-arm64`.

## Layout

| Path | Content |
| --- | --- |
| `src/Rezultat` | The core library and package; collection extensions in `Collections/`, consumer `README.md`. |
| `tests/Rezultat.Tests` | Own behavioral and property tests (xUnit v3, FsCheck). |
| `qualification/` | Package consumers, compiler controls, README examples, allocation controls and package inspector; outside the solution and restored only from the built package. |
| `scripts/qualify.sh` | Command adapter over native `dotnet` and Docker. |

## License

MIT, see `LICENSE`.
