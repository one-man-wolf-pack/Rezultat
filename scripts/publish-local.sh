#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
usage='usage: scripts/publish-local.sh [--package Rezultat|Rezultat.Testing.Xunit|Rezultat.Analyzers] <0.y.z> [package-directory] [feed-directory]'
id=Rezultat
if [[ "${1:-}" == --package ]]; then
    id="${2:?$usage}"
    shift 2
fi
version="${1:?$usage}"
packages="${2:-$root/artifacts/linux-arm64/package}"
feed="${3:-${REZULTAT_LOCAL_FEED:-$HOME/.local/share/nuget/rezultat}}"

[[ "$id" == Rezultat || "$id" == Rezultat.Testing.Xunit || "$id" == Rezultat.Analyzers ]] || { echo "Unknown package id: $id. $usage" >&2; exit 1; }
[[ "$version" =~ ^0\.(0|[1-9][0-9]*)\.(0|[1-9][0-9]*)$ ]] || { echo 'Expected a private 0.y.z version.' >&2; exit 1; }
package="$packages/$id.$version.nupkg"
artifacts=("$package")
[[ "$id" == Rezultat.Analyzers ]] || artifacts=("$packages/$id.$version.snupkg" "$package")
for source in "${artifacts[@]}"; do
    [[ -f "$source" ]] || { echo "Qualified package artifact is missing: $source." >&2; exit 1; }
done

mkdir -p "$feed/symbols"
for source in "${artifacts[@]}"; do
    destination="$feed/$(basename "$source")"
    [[ "$source" == *.snupkg ]] && destination="$feed/symbols/$(basename "$source")"
    if [[ -e "$destination" ]] && ! cmp -s "$source" "$destination"; then
        echo "Refusing to replace issued artifact: $destination. Use a new version." >&2
        exit 1
    fi
done

staged=""
trap '[[ -z "$staged" ]] || rm -f "$staged"' EXIT

# Publish symbols first. Linking a staged copy gives the final name atomic no-replace semantics;
# dotnet nuget push to a directory can overwrite an already issued version.
for source in "${artifacts[@]}"; do
    destination="$feed/$(basename "$source")"
    [[ "$source" == *.snupkg ]] && destination="$feed/symbols/$(basename "$source")"
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
