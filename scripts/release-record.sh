release_known_id() {
    [[ "$1" == Rezultat || "$1" == Rezultat.Testing.Xunit || "$1" == Rezultat.Analyzers ]]
}

release_has_symbols() {
    [[ "$1" != Rezultat.Analyzers ]]
}

release_revision_valid() {
    [[ "$1" =~ ^([0-9a-f]{40}|[0-9a-f]{64})$ ]]
}

release_nuspec_value() {
    local archive="$1" field="$2" nuspec
    nuspec="$(unzip -Z1 "$archive" | grep -E '^[^/]+\.nuspec$')" || { echo "No root nuspec in $archive." >&2; return 1; }
    [[ "$nuspec" != *$'\n'* ]] || { echo "Several root nuspec files in $archive." >&2; return 1; }
    unzip -p "$archive" "$nuspec" |
        xmllint --xpath "string(/*[local-name()='package']/*[local-name()='metadata']/*[local-name()='$field'])" -
}

release_repository_url() {
    local url
    url="$(xmllint --xpath 'string(/Project/PropertyGroup/RepositoryUrl)' "$1")" || return 1
    release_repository_url_valid "$url" || { echo "RepositoryUrl is not a credential-free https://github.com/<owner>/<repo> URL." >&2; return 1; }
    echo "$url"
}

release_repository_url_valid() {
    [[ "$1" =~ ^https://github\.com/[A-Za-z0-9_.-]+/[A-Za-z0-9_.-]+$ ]]
}

release_nuspec_repository() {
    local archive="$1" attribute="$2" nuspec
    nuspec="$(unzip -Z1 "$archive" | grep -E '^[^/]+\.nuspec$')" || { echo "No root nuspec in $archive." >&2; return 1; }
    unzip -p "$archive" "$nuspec" |
        xmllint --xpath "string(/*[local-name()='package']/*[local-name()='metadata']/*[local-name()='repository']/@$attribute)" -
}

release_bound() {
    local archive="$1" revision="$2" url="$3" actual_url actual_commit
    actual_url="$(release_nuspec_repository "$archive" url)" || return 1
    actual_commit="$(release_nuspec_repository "$archive" commit)" || return 1
    [[ "$actual_url" == "$url" ]] || { echo "$archive: nuspec repository url is not $url." >&2; return 1; }
    [[ "$actual_commit" == "$revision" ]] || { echo "$archive: nuspec repository commit is not $revision." >&2; return 1; }
}

release_sha256() {
    local sum
    sum="$(sha256sum "$1")" || { echo "Cannot hash $1." >&2; return 1; }
    [[ "$sum" =~ ^[0-9a-f]{64}\  ]] || { echo "Unexpected sha256sum output for $1: $sum" >&2; return 1; }
    echo "${sum%% *}"
}

release_record() {
    local id="$1" version="$2" revision="$3" archive nupkg_sha snupkg_sha=""
    shift 3
    for archive in "$@"; do
        [[ "$(release_nuspec_value "$archive" id)" == "$id" && "$(release_nuspec_value "$archive" version)" == "$version" ]] \
            || { echo "$archive does not declare $id $version." >&2; return 1; }
    done
    nupkg_sha="$(release_sha256 "$1")" || return 1
    if [[ $# -ge 2 ]]; then
        snupkg_sha="$(release_sha256 "$2")" || return 1
    fi
    printf 'package-id=%s\nversion=%s\nsource-revision=%s\nnupkg-sha256=%s\n' "$id" "$version" "$revision" "$nupkg_sha"
    [[ -z "$snupkg_sha" ]] || printf 'snupkg-sha256=%s\n' "$snupkg_sha"
}
