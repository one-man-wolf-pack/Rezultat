set dotenv-load := false
set positional-arguments

solution := "Rezultat.slnx"

# Empty on the workstation; `just linux` sets it to the container transport, so the same bodies run on Linux.
runner := env("REZULTAT_RUNNER", "")
dotnet := runner + "dotnet"
qualify := runner + "bash scripts/qualify.sh"

# Consumer inputs keep their checked layout: diagnostic corpus, compiler controls and runner fixtures.
qualification_inputs := "CompilerControls AssertionCompilerControls AnalyzerConsumer AnalyzerLibrary AnalyzerDownstream AnalyzerPackedConsumer AnalyzerCoreOnly AnalyzerWrapperHomonyms XunitV2Consumer XunitV3Consumer Shared/RunnerScenarios.cs"

[private]
default:
    @just --list

[doc('Restore the committed dependency graph and local tools, if present.')]
setup: _lockfiles
    {{ dotnet }} restore {{ quote(solution) }} --locked-mode
    if test -f .config/dotnet-tools.json; then {{ dotnet }} tool restore; fi

[doc('Build with the shared strict compiler and analyzer policy.')]
build: setup
    {{ dotnet }} build {{ quote(solution) }} -c Release --no-restore

[doc('Run managed tests; further arguments use native MTP syntax.')]
test *args: build
    {{ dotnet }} test --solution {{ quote(solution) }} -c Release --no-build -- --minimum-expected-tests 1 "$@"

[doc('Run one test project and its required build graph; forward native MTP/xUnit filters.')]
test-project project *args: _lockfiles
    #!/usr/bin/env bash
    set -euo pipefail
    project="$1"
    shift
    {{ dotnet }} restore "$project" --locked-mode
    if test -f .config/dotnet-tools.json; then {{ dotnet }} tool restore; fi
    {{ dotnet }} test --project "$project" -c Release --no-restore -- --minimum-expected-tests 1 "$@"

[doc('Verify formatting, build, and every managed test module without repairing source.')]
check: _justfile-check build
    #!/usr/bin/env bash
    set -euo pipefail
    {{ dotnet }} format {{ quote(solution) }} --no-restore --verify-no-changes --severity warn
    {{ dotnet }} format whitespace qualification --folder --verify-no-changes --exclude {{ qualification_inputs }}
    projects="$({{ dotnet }} sln {{ quote(solution) }} list | awk '/\.csproj$/')"
    found=0
    while IFS= read -r project; do
        test -n "$project" || continue
        is_test="$({{ dotnet }} msbuild "$project" -getProperty:IsTestingPlatformApplication)"
        case "$is_test" in [Tt][Rr][Uu][Ee]) ;; *) continue ;; esac
        found=1
        module="$(basename "$project" .csproj)"
        {{ dotnet }} test --project "$project" -c Release --no-build \
            --results-directory "artifacts/test/$module" \
            -- --minimum-expected-tests 1
    done <<< "$projects"
    test "$found" = 1 || { echo 'check: no test projects selected' >&2; exit 1; }

[doc('Apply whitespace and Just formatting without structural source fixes.')]
fmt: setup
    {{ dotnet }} format whitespace {{ quote(solution) }} --no-restore
    {{ dotnet }} format whitespace qualification --folder --exclude {{ qualification_inputs }}
    just --fmt

[doc('Apply available warning/error style and analyzer fixes; inspect the resulting diff.')]
fix *args: setup
    {{ dotnet }} format {{ quote(solution) }} --no-restore --severity warn "$@"
    just --fmt

[doc('Delete reproducible SDK, test and qualification output and the Linux cache volumes it records; not during active work.')]
gc:
    bash scripts/linux.sh --remove-caches artifacts
    rm -rf artifacts

[doc('Pack the three packages into artifacts/package/release for the qualification lanes.')]
[group('qualification')]
pack: setup
    {{ qualify }} pack

[private]
pack-bound commit: setup
    {{ qualify }} pack-bound "$1"

[doc('Packed core: consumers, compiler controls, README examples and recipes, reference diamond, package inspection.')]
[group('qualification')]
consumers: setup
    {{ qualify }} consumers

[doc('Same-version reference diamond against the packed core only.')]
[group('qualification')]
diamond: setup
    {{ qualify }} diamond

[doc('Mixed-version reference diamond with the issued private Rezultat 0.8.0 from REZULTAT_LOCAL_FEED; not part of qualify.')]
[group('qualification')]
diamond-historical: setup
    {{ qualify }} diamond-historical

[doc('Packed Rezultat.Testing.Xunit: consumers, compiler controls, README, xUnit v2/v3 runners, package inspection.')]
[group('qualification')]
assertions: setup
    {{ qualify }} assertions

[doc('Packed Rezultat.Analyzers: RZT0001 corpus modes, isolation consumers, README and package inspection.')]
[group('qualification')]
analyzers: setup
    {{ qualify }} analyzers

[doc('Warmed Release allocation controls against the packed core.')]
[group('qualification')]
alloc: setup
    {{ qualify }} alloc

[doc('Rooted and unrooted trimmed and Native AOT publication of the package consumer, then run each.')]
[group('qualification')]
publish rid: setup
    {{ qualify }} publish "$1"

[doc('Full candidate qualification: check, pack, every package lane and publication for the runtime.')]
[group('qualification')]
qualify rid: check pack consumers assertions analyzers alloc (publish rid)

[doc('Run recipes in the native Linux ARM64/glibc container, e.g. `just linux check` or `just linux qualify linux-arm64`.')]
[group('qualification')]
linux +recipes:
    REZULTAT_RUNNER='scripts/linux.sh ' just "$@"

[doc('Clone a commit to artifacts/release/<commit>, pack it source-bound on this host (`just linux release`: in the Linux container) and record the named packages.')]
[group('release')]
release commit +packages:
    bash scripts/qualify.sh release "$@"

[doc('Release and publish controls in a disposable Git fixture, packing like `release`.')]
[group('release')]
release-controls:
    bash scripts/qualify.sh release-controls

[doc('Lint the GitHub workflows with actionlint and its runner-label configuration.')]
lint-workflows:
    docker run --rm --network none -v {{ quote(justfile_directory()) }}:/repo:ro -w /repo rhysd/actionlint:1.7.12 -color

[private]
_justfile-check:
    just --fmt --check

# Locked restore can recreate a deleted lock; reject that before invoking restore.
[private]
_lockfiles:
    test -z "$(git ls-files --deleted -- '*packages.lock.json')" || { echo 'setup: restore refused; restore deleted tracked lockfiles first' >&2; exit 1; }
    git diff --cached --quiet --diff-filter=D -- '*packages.lock.json' || { echo 'setup: restore refused; resolve staged lockfile deletions first' >&2; exit 1; }
