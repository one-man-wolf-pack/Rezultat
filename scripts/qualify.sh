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
  test [mtp-args...]    run own tests; arguments are forwarded to the test platform
  pack                  pack Rezultat (Release) into artifacts/package
  consumers             package consumers, compiler controls and package inspection
  alloc                 warmed Release allocation controls against the package
  publish <rid>         rooted trimmed and Native AOT publication of the package consumer, then run both
  all <rid>             check, test, pack, consumers, alloc, publish
  linux                 run "all linux-arm64" in the native Linux ARM64/glibc container
EOF
}

check() {
    dotnet format "$root/Rezultat.slnx" --verify-no-changes
    dotnet format whitespace "$root/qualification" --folder --verify-no-changes
    dotnet build "$root/Rezultat.slnx" -c Release -warnaserror --no-incremental
}

run_tests() {
    dotnet test --project "$root/tests/Rezultat.Tests" "$@"
}

pack() {
    rm -rf "$feed"
    dotnet pack "$root/src/Rezultat" -c Release -o "$feed" -warnaserror
}

reset_package_cache() {
    rm -rf "$packages/rezultat"
    RezultatPackageVersion="$(package_version)"
    export RezultatPackageVersion
}

build_clean() {
    local project="$1"
    local output
    output="$(dotnet build "$project" -c Release --no-incremental 2>&1)" || { echo "$output"; return 1; }
    if grep -Eq 'warning [A-Z]+[0-9]+' <<<"$output"; then
        echo "$output"
        echo "FAIL $project built with warnings"
        return 1
    fi
}

compiler_controls() {
    local project="$root/qualification/CompilerControls/CompilerControls.csproj"
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
    done <"$root/qualification/CompilerControls/Cases/expected.txt"
    return "$failures"
}

consumers() {
    reset_package_cache
    build_clean "$root/qualification/PackageConsumer"
    dotnet run --project "$root/qualification/PackageConsumer" -c Release --no-build
    build_clean "$root/qualification/NullableDisabledConsumer"
    dotnet run --project "$root/qualification/NullableDisabledConsumer" -c Release --no-build
    compiler_controls
    build_clean "$root/qualification/PackageInspector"
    dotnet run --project "$root/qualification/PackageInspector" -c Release --no-build -- \
        "$root" "$RezultatPackageVersion" "$feed/Rezultat.$RezultatPackageVersion.nupkg" \
        "$feed/Rezultat.$RezultatPackageVersion.snupkg" "$packages" \
        "$qualification/obj/PackageConsumer/project.assets.json" \
        "$qualification/obj/NullableDisabledConsumer/project.assets.json"
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
    alloc) alloc ;;
    publish) publish "$@" ;;
    all) check; run_tests; pack; consumers; alloc; publish "$@" ;;
    linux) linux ;;
    *) usage; exit 2 ;;
esac
