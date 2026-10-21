#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
. "$root/scripts/release-record.sh"
usage='usage: scripts/publish-local.sh [--package ID] <0.y.z> <package-directory> [feed-directory]
       scripts/publish-local.sh --legacy-record [--package ID] <0.y.z> [feed-directory]
ID: Rezultat (default), Rezultat.Testing.Xunit or Rezultat.Analyzers'
id=Rezultat
legacy=false
while [[ $# -gt 0 ]]; do
    case "$1" in
        --package) id="${2:?$usage}"; shift 2 ;;
        --legacy-record) legacy=true; shift ;;
        *) break ;;
    esac
done
version="${1:?$usage}"
default_feed="${REZULTAT_LOCAL_FEED:-$HOME/.local/share/nuget/rezultat}"
if $legacy; then
    feed="${2:-$default_feed}"
else
    packages="${2:?$usage}"
    feed="${3:-$default_feed}"
fi

release_known_id "$id" || { echo "Unknown package id: $id. $usage" >&2; exit 1; }
[[ "$version" =~ ^0\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]] || { echo 'Expected a private 0.y.z version.' >&2; exit 1; }
nupkg="$id.$version.nupkg"
snupkg="$id.$version.snupkg"
record="$id.$version.record"

staged=""
generated="$(mktemp)"
trap 'rm -f ${staged:+"$staged"} "$generated"' EXIT

sources=()
destinations=()
if $legacy; then
    issued=("$feed/$nupkg")
    release_has_symbols "$id" && issued+=("$feed/symbols/$snupkg")
    for source in "${issued[@]}"; do
        [[ -f "$source" ]] || { echo "Issued artifact is missing: $source." >&2; exit 1; }
    done
    release_record "$id" "$version" unknown "${issued[@]}" >"$generated" || {
        echo "No legacy source record written for $id $version." >&2
        exit 1
    }
    sources=("$generated")
    destinations=("$feed/records/$record")
else
    [[ -f "$packages/$record" ]] || {
        echo "Build-time source record is missing: $packages/$record. Issue only artifacts built by scripts/qualify.sh release <commit> <package-id>." >&2
        exit 1
    }
    artifacts=("$packages/$nupkg")
    release_has_symbols "$id" && artifacts+=("$packages/$snupkg")
    for source in "${artifacts[@]}"; do
        [[ -f "$source" ]] || { echo "Qualified package artifact is missing: $source." >&2; exit 1; }
    done
    revision="$(sed -n 's/^source-revision=//p' "$packages/$record")"
    release_revision_valid "$revision" || { echo "Source record does not name a full commit: $packages/$record." >&2; exit 1; }
    release_record "$id" "$version" "$revision" "${artifacts[@]}" >"$generated" || {
        echo "Cannot check the source record against the package artifacts: $packages/$record." >&2
        exit 1
    }
    cmp -s "$generated" "$packages/$record" || {
        echo "Source record does not match the package artifacts: $packages/$record." >&2
        exit 1
    }
    if [[ -e "$feed/$nupkg" && ! -e "$feed/records/$record" ]]; then
        echo "Issued without a source record: $feed/$nupkg. Record it with --legacy-record instead." >&2
        exit 1
    fi
    # Symbols and the record go before the package: the package makes the version restorable, so a failure
    # must not leave an issued version without its record.
    if release_has_symbols "$id"; then
        sources+=("$packages/$snupkg")
        destinations+=("$feed/symbols/$snupkg")
    fi
    sources+=("$packages/$record" "$packages/$nupkg")
    destinations+=("$feed/records/$record" "$feed/$nupkg")
fi

for i in "${!sources[@]}"; do
    if [[ -e "${destinations[$i]}" ]] && ! cmp -s "${sources[$i]}" "${destinations[$i]}"; then
        echo "Refusing to replace issued file: ${destinations[$i]}. Use a new version." >&2
        exit 1
    fi
done

mkdir -p "$feed/symbols" "$feed/records"
# Linking a staged copy gives the final name atomic no-replace semantics; dotnet nuget push to a directory can
# overwrite an already issued version.
for i in "${!sources[@]}"; do
    source="${sources[$i]}"
    destination="${destinations[$i]}"
    if [[ -e "$destination" ]]; then
        cmp -s "$source" "$destination" || { echo "Publication conflict: $destination" >&2; exit 1; }
        echo "Unchanged: $destination"
        continue
    fi
    staged="$(mktemp "$feed/.rezultat-publish.XXXXXX")"
    cp "$source" "$staged"
    chmod 444 "$staged"
    if ! ln "$staged" "$destination"; then
        cmp -s "$source" "$destination" || { echo "Publication conflict: $destination" >&2; exit 1; }
    fi
    rm -f "$staged"
    staged=""
    echo "Published: $destination"
done
