#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
artifacts="$root/artifacts"
feed="$artifacts/package"
qualification="$artifacts/qualification"
packages="$qualification/packages"
linux_image="mcr.microsoft.com/dotnet/sdk:10.0.402-noble-aot"

export DOTNET_NOLOGO=1 DOTNET_CLI_TELEMETRY_OPTOUT=1 TESTINGPLATFORM_TELEMETRY_OPTOUT=1

package_version() {
    dotnet msbuild "$root/src/Rezultat" -getProperty:Version
}

usage() {
    cat <<'EOF'
usage: scripts/qualify.sh <command> [args]
  check                 format verification and warning-free Release build of the solution
  test [project] [mtp-args...]
                        run own tests of the solution, or of one tests/<project>; other arguments are forwarded
                        to the test platform (select the project when filtering: a project without matches fails)
  pack                  pack Rezultat and Rezultat.Testing.Xunit (Release) into artifacts/package
  consumers             package consumers, compiler controls, packaged README examples and package inspection
  assertions            Rezultat.Testing.Xunit consumers, compiler controls, README examples, xUnit v2/v3 runner
                        scenarios and package inspection
  alloc                 warmed Release allocation controls against the package
  publish <rid>         rooted trimmed and Native AOT publication of the package consumer, then run both
  all <rid>             check, test, pack, consumers, assertions, alloc, publish
  linux                 run "all linux-arm64" in the native Linux ARM64/glibc container
EOF
}

check() {
    dotnet format "$root/Rezultat.slnx" --verify-no-changes
    dotnet format whitespace "$root/qualification" --folder --verify-no-changes
    dotnet build "$root/Rezultat.slnx" -c Release -warnaserror --no-incremental
}

run_tests() {
    if [[ $# -gt 0 && -d "$root/tests/$1" ]]; then
        dotnet test --project "$root/tests/$1" "${@:2}"
    else
        dotnet test --solution "$root/Rezultat.slnx" "$@"
    fi
}

pack() {
    rm -rf "$feed"
    dotnet pack "$root/src/Rezultat" -c Release -o "$feed" -warnaserror
    dotnet pack "$root/src/Rezultat.Testing.Xunit" -c Release -o "$feed" -warnaserror
}

reset_package_cache() {
    rm -rf "$packages/rezultat" "$packages/rezultat.testing.xunit"
    RezultatPackageVersion="$(package_version)"
    export RezultatPackageVersion
}

build_clean() {
    local project="$1"
    local output
    output="$(dotnet build "$project" -c Release --no-incremental "${@:2}" 2>&1)" || { echo "$output"; return 1; }
    if grep -Eq 'warning [A-Z]+[0-9]+' <<<"$output"; then
        echo "$output"
        echo "FAIL $project built with warnings"
        return 1
    fi
}

compiler_controls() {
    local project="$root/qualification/$1/$1.csproj"
    local failures=0 name kind expected language output status actual
    dotnet restore "$project" >/dev/null
    while read -r name kind expected language; do
        [[ -z "$name" || "$name" == \#* ]] && continue
        status=0
        output="$(dotnet build "$project" --no-restore --no-incremental -c Release -p:Case="$name" \
            ${language:+-p:LangVersion="$language"} </dev/null 2>&1)" || status=$?
        actual="$(grep -oE "$kind CS[0-9]+" <<<"$output" | awk '{print $2}' | sort -u | paste -sd, -)"
        expected="$(tr ',' '\n' <<<"$expected" | sort -u | paste -sd, -)"
        if [[ "$actual" == "$expected" ]] && { [[ "$kind" == error && $status -ne 0 ]] || [[ "$kind" == warning && $status -eq 0 ]]; }; then
            echo "PASS $name: $kind $actual${language:+ (C# $language)}"
        else
            echo "FAIL $name: expected $kind $expected (exit $status), got '${actual}'"
            grep -E "(error|warning) CS[0-9]+" <<<"$output" | sort -u || true
            failures=$((failures + 1))
        fi
    done <"$(dirname "$project")/Cases/expected.txt"
    return "$failures"
}

extract_readme_examples() {
    local readme="$1" snippets="$2"
    rm -rf "$snippets"
    mkdir -p "$snippets"
    awk -v dir="$snippets" '
        /^```csharp$/ { file = sprintf("%s/example%02d.cs", dir, ++count); inside = 1; next }
        /^```$/ { if (inside) close(file); inside = 0; next }
        inside { print > file }
    ' "$readme"
    compgen -G "$snippets/*.cs" >/dev/null || { echo "FAIL packaged $readme has no csharp examples"; return 1; }
}

readme_examples() {
    local project="$root/qualification/ReadmeExamples"
    local snippets="$qualification/readme" snippet
    dotnet restore "$project" >/dev/null
    extract_readme_examples "$packages/rezultat/$RezultatPackageVersion/README.md" "$snippets"
    for snippet in "$snippets"/*.cs; do
        build_clean "$project" -p:Snippet="$snippet"
        dotnet run --project "$project" -c Release --no-build
        echo "PASS README example $(basename "$snippet")"
    done
}

consumers() {
    reset_package_cache
    build_clean "$root/qualification/PackageConsumer"
    dotnet run --project "$root/qualification/PackageConsumer" -c Release --no-build
    build_clean "$root/qualification/NullableDisabledConsumer"
    dotnet run --project "$root/qualification/NullableDisabledConsumer" -c Release --no-build
    compiler_controls CompilerControls
    readme_examples
    build_clean "$root/qualification/PackageInspector"
    dotnet run --project "$root/qualification/PackageInspector" -c Release --no-build -- Rezultat \
        "$root" "$RezultatPackageVersion" "$feed/Rezultat.$RezultatPackageVersion.nupkg" \
        "$feed/Rezultat.$RezultatPackageVersion.snupkg" "$packages" \
        "$qualification/obj/PackageConsumer/project.assets.json" \
        "$qualification/obj/NullableDisabledConsumer/project.assets.json"
}

# Runs one xUnit consumer pair. Expected-failure fixtures make the runner exit non-zero, so the exit
# code is only checked to be the runner's "tests failed" value; the native report carries the evidence.
xunit_v3_pair() {
    local version="$1" project="$root/qualification/XunitV3Consumer" status=0
    local report="$qualification/runner-reports/v3-$version"
    build_clean "$project" -p:XunitV3Version="$version"
    rm -rf "$report"
    dotnet test --project "$project" -c Release --no-build --results-directory "$report" \
        --report-xunit-xml --report-xunit-xml-filename results.xml >"$report.log" 2>&1 || status=$?
    [[ $status -eq 2 ]] || { cat "$report.log"; echo "FAIL xunit.v3 $version: dotnet test exit $status, expected 2 (test failures)"; return 1; }
    status=0
    "$qualification/bin/XunitV3Consumer/release/XunitV3Consumer" -reporter json -noAutoReporters -noColor \
        >"$report/native.log" 2>&1 || status=$?
    [[ $status -eq 1 ]] || { cat "$report/native.log"; echo "FAIL xunit.v3 $version: native runner exit $status, expected 1"; return 1; }
    dotnet run --project "$root/qualification/RunnerReports" -c Release --no-build -- \
        v3 "$version" "$report/results.xml" "$report/native.log"
    mkdir -p "$qualification/assets/XunitV3Consumer-$version"
    cp "$qualification/obj/XunitV3Consumer/project.assets.json" "$qualification/assets/XunitV3Consumer-$version/"
}

xunit_v2_pair() {
    local project="$root/qualification/XunitV2Consumer" status=0 sdk
    local report="$qualification/runner-reports/v2"
    # VSTest needs its own global.json: the repository one selects Microsoft.Testing.Platform mode.
    sdk="$(cd "$project" && dotnet --version)"
    [[ "$sdk" == "$(cd "$root" && dotnet --version)" ]] || { echo "FAIL XunitV2Consumer resolves SDK $sdk"; return 1; }
    build_clean "$project"
    rm -rf "$report"
    (cd "$project" && dotnet test -c Release --no-build --logger "trx;LogFileName=results.trx" \
        --results-directory "$report") >"$report.log" 2>&1 || status=$?
    [[ $status -eq 1 ]] || { cat "$report.log"; echo "FAIL xunit v2: dotnet test exit $status, expected 1 (test failures)"; return 1; }
    dotnet run --project "$root/qualification/RunnerReports" -c Release --no-build -- \
        v2 "xunit 2.9.3/VSTest" "$report/results.trx"
    mkdir -p "$qualification/assets/XunitV2Consumer"
    cp "$qualification/obj/XunitV2Consumer/project.assets.json" "$qualification/assets/XunitV2Consumer/"
}

assertion_readme_examples() {
    local project="$root/qualification/AssertionReadmeExamples" snippets="$qualification/assertion-readme" status=0
    local report="$qualification/runner-reports/readme" facts total
    extract_readme_examples "$packages/rezultat.testing.xunit/$RezultatPackageVersion/README.md" "$snippets"
    build_clean "$project" -p:SnippetDirectory="$snippets"
    rm -rf "$report"
    dotnet test --project "$project" -c Release --no-build --results-directory "$report" \
        --report-xunit-xml --report-xunit-xml-filename results.xml || status=$?
    facts="$(cat "$snippets"/*.cs | grep -c '\[Fact\]' || true)"
    total="$(grep -oE '<assembly [^>]*' "$report/results.xml" | grep -oE ' (total|passed|failed|errors)="[0-9]+"' | tr -d ' ' | paste -sd' ' -)"
    if [[ $status -eq 0 && $facts -gt 0 && "$total" == *"total=\"$facts\""* && "$total" == *"passed=\"$facts\""* ]]; then
        echo "PASS assertion README examples: $facts facts ($total)"
    else
        echo "FAIL assertion README examples: exit $status, $facts facts, report $total"
        return 1
    fi
}

assertions() {
    reset_package_cache
    rm -rf "$qualification/runner-reports" "$qualification/assets"
    mkdir -p "$qualification/runner-reports"
    build_clean "$root/qualification/AssertionConsumer"
    dotnet run --project "$root/qualification/AssertionConsumer" -c Release --no-build
    build_clean "$root/qualification/AssertionNullableDisabledConsumer"
    dotnet run --project "$root/qualification/AssertionNullableDisabledConsumer" -c Release --no-build
    compiler_controls AssertionCompilerControls
    assertion_readme_examples
    build_clean "$root/qualification/RunnerReports"
    xunit_v2_pair
    xunit_v3_pair 4.0.1
    xunit_v3_pair 4.0.2
    build_clean "$root/qualification/PackageInspector"
    dotnet run --project "$root/qualification/PackageInspector" -c Release --no-build -- Rezultat.Testing.Xunit \
        "$root" "$RezultatPackageVersion" "$feed/Rezultat.Testing.Xunit.$RezultatPackageVersion.nupkg" \
        "$feed/Rezultat.Testing.Xunit.$RezultatPackageVersion.snupkg" "$packages" \
        "$qualification/obj/AssertionConsumer/project.assets.json" \
        "$qualification/obj/AssertionNullableDisabledConsumer/project.assets.json" \
        "$qualification/assets/XunitV2Consumer/project.assets.json" \
        "$qualification/assets/XunitV3Consumer-4.0.1/project.assets.json" \
        "$qualification/assets/XunitV3Consumer-4.0.2/project.assets.json"
}

alloc() {
    reset_package_cache
    build_clean "$root/qualification/AllocationControls"
    dotnet run --project "$root/qualification/AllocationControls" -c Release --no-build
}

publish() {
    local rid="${1:?runtime identifier required}"
    local project="$root/qualification/PackageConsumer"
    local mode output binary
    reset_package_cache
    for mode in trimmed aot; do
        local properties=(-p:PublishTrimmed=true -p:TrimmerSingleWarn=false)
        [[ "$mode" == aot ]] && properties=(-p:PublishAot=true -p:TrimmerSingleWarn=false)
        output="$qualification/publish/$mode-$rid"
        rm -rf "$output"
        dotnet publish "$project" -c Release -r "$rid" --self-contained -warnaserror \
            -p:ArtifactsPivots="release_${rid}_$mode" "${properties[@]}" -o "$output"
        binary="$output/PackageConsumer"
        "$binary"
    done
}

linux() {
    local host_output="$artifacts/linux-arm64" engine
    engine="$(docker info --format '{{.OSType}}/{{.Architecture}}')"
    if [[ "$engine" != linux/aarch64 && "$engine" != linux/arm64 ]]; then
        echo "native linux/arm64 Docker engine required, found $engine" >&2
        return 1
    fi
    mkdir -p "$host_output/home"
    docker run --rm --platform linux/arm64 \
        --user "$(id -u):$(id -g)" \
        -e HOME=/work/artifacts/home -e DOTNET_CLI_HOME=/work/artifacts/home -e NUGET_PACKAGES=/work/artifacts/nuget \
        -v "$root:/work:ro" -v "$host_output:/work/artifacts" -w /work \
        "$linux_image" \
        bash -c 'uname -m; ldd --version | head -1; . /etc/os-release; echo "$PRETTY_NAME"; dotnet --version; clang --version | head -1; scripts/qualify.sh all linux-arm64'
}

command="${1:-}"
[[ $# -gt 0 ]] && shift
case "$command" in
    check) check ;;
    test) run_tests "$@" ;;
    pack) pack ;;
    consumers) consumers ;;
    assertions) assertions ;;
    alloc) alloc ;;
    publish) publish "$@" ;;
    all) check; run_tests; pack; consumers; assertions; alloc; publish "$@" ;;
    linux) linux ;;
    *) usage; exit 2 ;;
esac
