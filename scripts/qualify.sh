#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
artifacts="$root/artifacts"
feed="$artifacts/package"
qualification="$artifacts/qualification"
packages="$qualification/packages"
linux_image="mcr.microsoft.com/dotnet/sdk:10.0.402-noble-aot"
issued_feed="${REZULTAT_LOCAL_FEED:-$HOME/.local/share/nuget/rezultat}"
# The mixed-version diamond must consume the archive that was actually issued as 0.8.0 (review/096). A repack or a
# different file under the same version would compile the old library against other bytes and prove nothing.
issued_core_version=0.8.0
issued_core_sha256=4f2d6cdd7e84380515e559cdf42c2c14e8d9d357373b7d5e82e5c33389cc5469
. "$root/scripts/release-record.sh"

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
  pack                  pack Rezultat, Rezultat.Testing.Xunit and Rezultat.Analyzers (Release) into artifacts/package
  consumers             package consumers, compiler controls, root and packaged README examples, the packaged
                        Shouldly, HTTP and Presence JSON recipes in isolated consumers, the reference diamond and
                        package inspection
  diamond               reference diamond: two separately packed fixture libraries exchange Result/Validation in an
                        app, with both libraries on the candidate core and with one library compiled against the
                        issued Rezultat 0.8.0 from the local feed (REZULTAT_LOCAL_FEED)
  assertions            Rezultat.Testing.Xunit consumers, compiler controls, README examples, xUnit v2/v3 runner
                        scenarios and package inspection
  analyzers             Rezultat.Analyzers package: RZT0001 corpus in warning/error/none/warnings-as-errors modes,
                        same-named Task/ValueTask wrappers, downstream, packed-library and core-only isolation,
                        README examples and package inspection
  alloc                 warmed Release allocation controls against the package
  publish <rid>         rooted trimmed and Native AOT publication of the package consumer, then run both
  all <rid>             check, test, pack, consumers, assertions, analyzers, alloc, publish
  linux [command...]    run a command (default "all linux-arm64") in the native Linux ARM64/glibc container
  release <commit> <package-id>...
                        export the committed tree to artifacts/release/<full-commit>, pack it with its own
                        "linux pack" and write the source record of each named package next to its archives
  release-controls      release and publish controls in a disposable Git fixture (host Git and Docker)
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
    dotnet pack "$root/src/Rezultat.Analyzers" -c Release -o "$feed" -warnaserror
}

reset_package_cache() {
    rm -rf "$packages/rezultat" "$packages/rezultat.testing.xunit" "$packages/rezultat.analyzers"
    RezultatPackageVersion="$(package_version)"
    export RezultatPackageVersion
}

build_clean() {
    dotnet_clean build "$1" --no-incremental "${@:2}"
}

dotnet_clean() {
    local verb="$1" project="$2"
    local output
    output="$(dotnet "$verb" "$project" -c Release "${@:3}" 2>&1)" || { echo "$output"; return 1; }
    if grep -Eq 'warning [A-Z]+[0-9]+' <<<"$output"; then
        echo "$output"
        echo "FAIL $verb $project with warnings"
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
        /^```ini$/ { file = sprintf("%s/example%02d.ini", dir, ++count); inside = 1; next }
        /^```csharp shouldly$/ { file = sprintf("%s/recipe%02d.shouldly", dir, ++count); inside = 1; next }
        /^```csharp http$/ { file = sprintf("%s/recipe%02d.httprecipe", dir, ++count); inside = 1; next }
        /^```csharp json$/ { file = sprintf("%s/recipe%02d.jsonrecipe", dir, ++count); inside = 1; next }
        /^```$/ { if (inside) close(file); inside = 0; next }
        inside { print > file }
    ' "$readme"
    compgen -G "$snippets/*.cs" >/dev/null || { echo "FAIL $readme has no csharp examples"; return 1; }
}

readme_examples() {
    local project="$root/qualification/ReadmeExamples"
    local label readme snippets snippet
    dotnet restore "$project" >/dev/null
    for label in package root; do
        readme="$packages/rezultat/$RezultatPackageVersion/README.md"
        [[ "$label" == root ]] && readme="$root/README.md"
        snippets="$qualification/readme-$label"
        extract_readme_examples "$readme" "$snippets"
        for snippet in "$snippets"/*.cs; do
            build_clean "$project" -p:Snippet="$snippet"
            dotnet run --project "$project" -c Release --no-build
            echo "PASS $label README example $(basename "$snippet")"
        done
    done
}

readme_recipe() {
    local project="$root/qualification/$1" extension="$2"
    local snippets="$qualification/$1"
    extract_readme_examples "$packages/rezultat/$RezultatPackageVersion/README.md" "$snippets"
    compgen -G "$snippets/*.$extension" >/dev/null || { echo "FAIL packaged Rezultat README has no $extension block"; return 1; }
    build_clean "$project" -p:SnippetDirectory="$snippets"
    dotnet run --project "$project" -c Release --no-build
    echo "PASS $1"
}

consumers() {
    reset_package_cache
    build_clean "$root/qualification/PackageConsumer"
    dotnet run --project "$root/qualification/PackageConsumer" -c Release --no-build
    build_clean "$root/qualification/NullableDisabledConsumer"
    dotnet run --project "$root/qualification/NullableDisabledConsumer" -c Release --no-build
    compiler_controls CompilerControls
    readme_examples
    shouldly_recipe
    readme_recipe HttpReadmeRecipe httprecipe
    readme_recipe JsonReadmeRecipe jsonrecipe
    build_clean "$root/qualification/PackageInspector"
    dotnet run --project "$root/qualification/PackageInspector" -c Release --no-build -- Rezultat \
        "$root" "$RezultatPackageVersion" "$feed/Rezultat.$RezultatPackageVersion.nupkg" \
        "$feed/Rezultat.$RezultatPackageVersion.snupkg" "$packages" \
        "$qualification/obj/PackageConsumer/project.assets.json" \
        "$qualification/obj/NullableDisabledConsumer/project.assets.json" \
        "$qualification/obj/ShouldlyRecipeConsumer/project.assets.json" \
        "$qualification/obj/HttpReadmeRecipe/project.assets.json" \
        "$qualification/obj/JsonReadmeRecipe/project.assets.json"
    diamond
}

diamond_case() {
    local name="$1" candidate_sha="$2" inventory_core="$3" inventory_feed="$4" inventory_sha="$5"
    local d="$root/qualification/Diamond" out="$qualification/diamond/$name" library_feed="$qualification/diamond/feed"
    local restore
    rm -rf "$out" "$library_feed"
    mkdir -p "$out/assets" "$library_feed"
    restore=(-p:RestorePackagesPath="$out/packages")
    build_clean "$d/Inventory" "${restore[@]}" -p:RezultatPackageVersion="$inventory_core"
    dotnet_clean pack "$d/Inventory" --no-build -o "$library_feed" "${restore[@]}" -p:RezultatPackageVersion="$inventory_core"
    cp "$qualification/obj/DiamondInventory/project.assets.json" "$out/assets/DiamondInventory.json"
    build_clean "$d/Orders" "${restore[@]}"
    dotnet_clean pack "$d/Orders" --no-build -o "$library_feed" "${restore[@]}"
    cp "$qualification/obj/DiamondOrders/project.assets.json" "$out/assets/DiamondOrders.json"
    build_clean "$d/App" "${restore[@]}"
    cp "$qualification/obj/DiamondApp/project.assets.json" "$out/assets/DiamondApp.json"
    cp -R "$library_feed" "$out/feed"
    dotnet run --project "$d/App" -c Release --no-build -- "$RezultatPackageVersion.0" "$inventory_core.0"
    dotnet run --project "$d/Inspector" -c Release --no-build -- "$name" "$out/packages" "$library_feed" \
        "$qualification/bin/DiamondApp/release" "$out/assets" \
        "$RezultatPackageVersion" "$feed" "$candidate_sha" "$inventory_core" "$inventory_feed" "$inventory_sha"
}

diamond() {
    local issued="$qualification/diamond/issued" archive="$issued_feed/Rezultat.$issued_core_version.nupkg" actual candidate_sha
    reset_package_cache
    [[ "$RezultatPackageVersion" != "$issued_core_version" ]] || { echo "FAIL candidate must differ from issued $issued_core_version"; return 1; }
    [[ -f "$archive" ]] || { echo "FAIL issued Rezultat $issued_core_version not found: $archive (set REZULTAT_LOCAL_FEED)"; return 1; }
    actual="$(sha256sum "$archive" | cut -d' ' -f1)"
    [[ "$actual" == "$issued_core_sha256" ]] || { echo "FAIL $archive has sha256 $actual, issued $issued_core_sha256"; return 1; }
    rm -rf "$issued"
    mkdir -p "$issued"
    cp "$archive" "$issued/"
    build_clean "$root/qualification/Diamond/Inspector"
    candidate_sha="$(sha256sum "$feed/Rezultat.$RezultatPackageVersion.nupkg" | cut -d' ' -f1)"
    diamond_case same-version "$candidate_sha" "$RezultatPackageVersion" "$feed" "$candidate_sha"
    diamond_case mixed-version "$candidate_sha" "$issued_core_version" "$issued" "$issued_core_sha256"
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

readme_facts() {
    local label="$1" project="$2" snippets="$3" pattern="$4" status=0
    local report="$qualification/runner-reports/$label" facts total
    build_clean "$project" -p:SnippetDirectory="$snippets"
    rm -rf "$report"
    dotnet test --project "$project" -c Release --no-build --results-directory "$report" \
        --report-xunit-xml --report-xunit-xml-filename results.xml || status=$?
    facts="$({ cat "$snippets"/$pattern; find "$project" -maxdepth 1 -name '*.cs' -exec cat {} +; } | grep -c '\[Fact\]' || true)"
    total="$(grep -oE '<assembly [^>]*' "$report/results.xml" | grep -oE ' (total|passed|failed|errors)="[0-9]+"' | tr -d ' ' | paste -sd' ' -)"
    if [[ $status -eq 0 && $facts -gt 0 && "$total" == *"total=\"$facts\""* && "$total" == *"passed=\"$facts\""* ]]; then
        echo "PASS $label: $facts facts ($total)"
    else
        echo "FAIL $label: exit $status, $facts facts, report $total"
        return 1
    fi
}

assertion_readme_examples() {
    local snippets="$qualification/assertion-readme"
    extract_readme_examples "$packages/rezultat.testing.xunit/$RezultatPackageVersion/README.md" "$snippets"
    readme_facts readme "$root/qualification/AssertionReadmeExamples" "$snippets" '*.cs'
}

# Compiles the recipe from the installed package README, never from a checkout copy, so the shipped text is checked.
shouldly_recipe() {
    local snippets="$qualification/shouldly-readme"
    extract_readme_examples "$packages/rezultat/$RezultatPackageVersion/README.md" "$snippets"
    compgen -G "$snippets/*.shouldly" >/dev/null || { echo "FAIL packaged Rezultat README has no Shouldly recipe"; return 1; }
    mkdir -p "$qualification/runner-reports"
    readme_facts shouldly-recipe "$root/qualification/ShouldlyRecipeConsumer" "$snippets" '*.shouldly'
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

analyzer_build() {
    local label="$1" mode="$2" project="$3" status=0
    local log="$qualification/analyzer-logs/$label.log" properties=()
    shift 3
    while [[ $# -gt 0 && "$1" != -- ]]; do properties+=("$1"); shift; done
    shift
    # SDK 10.0.402: the console logger prints each diagnostic twice even with NoSummary, which would fail the
    # exactly-once check of AnalyzerReports. Return to the console log once it reports every diagnostic once.
    dotnet build "$project" -c Release --no-incremental -tl:off -noconlog -flp:"logfile=$log;verbosity=minimal;NoSummary" \
        ${properties[@]+"${properties[@]}"} </dev/null >/dev/null 2>&1 || status=$?
    dotnet run --project "$root/qualification/AnalyzerReports" -c Release --no-build -- "$label" "$mode" "$status" "$log" "$@" \
        || { cat "$log"; return 1; }
}

analyzers() {
    local q="$root/qualification" library_feed="$qualification/library-feed" snippets="$qualification/analyzer-readme"
    local corpus=("$q/AnalyzerConsumer" "$q/AnalyzerLibrary")
    reset_package_cache
    rm -rf "$qualification/analyzer-logs" "$qualification/analyzer-assets" "$library_feed" "$packages/rezultat.qualification.analyzerlibrary"
    mkdir -p "$qualification/analyzer-logs" "$qualification/analyzer-assets"
    build_clean "$q/AnalyzerReports"
    analyzer_build direct-warning warning "$q/AnalyzerConsumer" -- "${corpus[@]}"
    cp "$qualification/obj/AnalyzerConsumer/project.assets.json" "$qualification/analyzer-assets/AnalyzerConsumer.json"
    cp "$qualification/obj/AnalyzerLibrary/project.assets.json" "$qualification/analyzer-assets/AnalyzerLibrary.json"
    # Build only the consumer: -warnaserror and global properties would otherwise also apply to the referenced
    # library, whose own RZT0001 is not part of the consumer's severity configuration and would fail these checks.
    local consumer=("$q/AnalyzerConsumer" -p:BuildProjectReferences=false)
    analyzer_build direct-error error "${consumer[@]}" -p:RztSeverity=error -- "$q/AnalyzerConsumer"
    analyzer_build direct-none none "${consumer[@]}" -p:RztSeverity=none -- "$q/AnalyzerConsumer"
    analyzer_build direct-warnaserror wae "${consumer[@]}" -warnaserror -- "$q/AnalyzerConsumer"
    analyzer_build direct-treat-warnings-as-errors wae "${consumer[@]}" -p:TreatWarningsAsErrors=true -- "$q/AnalyzerConsumer"
    analyzer_build direct-rule-as-error error "${consumer[@]}" -p:WarningsAsErrors=RZT0001 -- "$q/AnalyzerConsumer"
    analyzer_build wrapper-homonyms warning "$q/AnalyzerWrapperHomonyms" -- "$q/AnalyzerLibrary" "$q/AnalyzerWrapperHomonyms"
    analyzer_build downstream-project warning "$q/AnalyzerDownstream" -- "$q/AnalyzerLibrary" "$q/AnalyzerDownstream"
    cp "$qualification/obj/AnalyzerDownstream/project.assets.json" "$qualification/analyzer-assets/AnalyzerDownstream.json"
    dotnet pack "$q/AnalyzerLibrary" -c Release -o "$library_feed" >"$qualification/analyzer-logs/library-pack.log" 2>&1 \
        || { cat "$qualification/analyzer-logs/library-pack.log"; return 1; }
    analyzer_build packed-library-consumer warning "$q/AnalyzerPackedConsumer" -- "$q/AnalyzerPackedConsumer"
    cp "$qualification/obj/AnalyzerPackedConsumer/project.assets.json" "$qualification/analyzer-assets/AnalyzerPackedConsumer.json"
    analyzer_build core-only warning "$q/AnalyzerCoreOnly" -- "$q/AnalyzerCoreOnly"
    cp "$qualification/obj/AnalyzerCoreOnly/project.assets.json" "$qualification/analyzer-assets/AnalyzerCoreOnly.json"
    extract_readme_examples "$packages/rezultat.analyzers/$RezultatPackageVersion/README.md" "$snippets"
    mv "$snippets"/*.ini "$qualification/analyzer-readme.ini"
    analyzer_build readme-warning warning "$q/AnalyzerReadmeExamples" -p:SnippetDirectory="$snippets" -- "$snippets"
    cp "$qualification/analyzer-readme.ini" "$snippets/.editorconfig"
    analyzer_build readme-editorconfig-error error "$q/AnalyzerReadmeExamples" -p:SnippetDirectory="$snippets" -- "$snippets"
    build_clean "$q/AnalyzerInspector"
    dotnet run --project "$q/AnalyzerInspector" -c Release --no-build -- "$root" "$RezultatPackageVersion" \
        "$feed/Rezultat.Analyzers.$RezultatPackageVersion.nupkg" "$packages" \
        "$library_feed/Rezultat.Qualification.AnalyzerLibrary.1.0.0.nupkg" \
        --direct "$qualification/analyzer-assets/AnalyzerConsumer.json" "$qualification/analyzer-assets/AnalyzerLibrary.json" \
        --absent "$qualification/analyzer-assets/AnalyzerDownstream.json" "$qualification/analyzer-assets/AnalyzerPackedConsumer.json" \
        "$qualification/analyzer-assets/AnalyzerCoreOnly.json"
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
    local host_output="$artifacts/linux-arm64" engine command=("$@")
    [[ $# -gt 0 ]] || command=(all linux-arm64)
    engine="$(docker info --format '{{.OSType}}/{{.Architecture}}')"
    if [[ "$engine" != linux/aarch64 && "$engine" != linux/arm64 ]]; then
        echo "native linux/arm64 Docker engine required, found $engine" >&2
        return 1
    fi
    mkdir -p "$host_output/home"
    local mounts=()
    [[ -d "$issued_feed" ]] && mounts=(-v "$(cd "$issued_feed" && pwd):/issued-feed:ro" -e REZULTAT_LOCAL_FEED=/issued-feed)
    docker run --rm --platform linux/arm64 \
        --user "$(id -u):$(id -g)" ${mounts[@]+"${mounts[@]}"} \
        -e HOME=/work/artifacts/home -e DOTNET_CLI_HOME=/work/artifacts/home -e NUGET_PACKAGES=/work/artifacts/nuget \
        -v "$root:/work:ro" -v "$host_output:/work/artifacts" -w /work \
        "$linux_image" \
        bash -c 'uname -m; ldd --version | head -1; . /etc/os-release; echo "$PRETTY_NAME"; dotnet --version; clang --version | head -1; scripts/qualify.sh "$@"' \
        qualify "${command[@]}"
}

release() {
    local revision snapshot packages id version found archives record
    revision="$(git -C "$root" rev-parse --verify --end-of-options "${1:?commit required}^{commit}")" || return 1
    shift
    [[ $# -gt 0 ]] || { echo 'at least one package id required' >&2; return 1; }
    for id in "$@"; do
        release_known_id "$id" || { echo "Unknown package id: $id" >&2; return 1; }
    done
    snapshot="$artifacts/release/$revision"
    packages="$snapshot/artifacts/linux-arm64/package"
    rm -rf "$snapshot"
    mkdir -p "$snapshot"
    # Build only the exported commit: the working tree may hold modified or untracked inputs, and attributing
    # them to the commit would make the source record false.
    git -C "$root" archive "$revision" | tar -x -C "$snapshot"
    "$snapshot/scripts/qualify.sh" linux pack
    for id in "$@"; do
        found=("$packages/$id".[0-9]*.nupkg)
        [[ ${#found[@]} -eq 1 && -f "${found[0]}" ]] || { echo "Expected one $id package in $packages" >&2; return 1; }
        version="$(release_nuspec_value "${found[0]}" version)" || return 1
        archives=("$packages/$id.$version.nupkg")
        release_has_symbols "$id" && archives+=("$packages/$id.$version.snupkg")
        record="$packages/$id.$version.record"
        if ! release_record "$id" "$version" "$revision" "${archives[@]}" >"$record.partial"; then
            rm -f "$record.partial"
            echo "No source record written for $id $version." >&2
            return 1
        fi
        mv "$record.partial" "$record"
        echo "Recorded $id $version from $revision: $record"
    done
}

command="${1:-}"
[[ $# -gt 0 ]] && shift
case "$command" in
    check) check ;;
    test) run_tests "$@" ;;
    pack) pack ;;
    consumers) consumers ;;
    diamond) diamond ;;
    assertions) assertions ;;
    alloc) alloc ;;
    publish) publish "$@" ;;
    analyzers) analyzers ;;
    all) check; run_tests; pack; consumers; assertions; analyzers; alloc; publish "$@" ;;
    linux) linux "$@" ;;
    release) release "$@" ;;
    release-controls) bash "$root/qualification/ReleaseControls/run.sh" "$root" ;;
    *) usage; exit 2 ;;
esac
