#!/usr/bin/env bash
set -euo pipefail

root="${1:?repository root required}"
mkdir -p "$root/artifacts/qualification"
work="$(mktemp -d "$root/artifacts/qualification/release-controls.XXXXXX")"
repo="$work/repo"
echo "release controls work directory: $work"
failures=0
publish_path="$PATH"
broken_bin="$work/broken-bin"
mkdir -p "$broken_bin"
printf '#!/bin/sh\necho "sha256sum: simulated failure" >&2\nexit 1\n' >"$broken_bin/sha256sum"
chmod +x "$broken_bin/sha256sum"

pass() { echo "PASS $1"; }
fail() { echo "FAIL $1"; failures=$((failures + 1)); }
check() {
    local name="$1"
    shift
    if "$@"; then pass "$name"; else fail "$name"; fi
}
fixture_git() { git -C "$repo" -c user.name=Fixture -c user.email=fixture@invalid -c commit.gpgsign=false "$@"; }
publish() { PATH="$publish_path" "$repo/scripts/publish-local.sh" "$@" >"$work/publish.log" 2>&1; }
sha() { local sum; sum="$(sha256sum "$1")"; echo "${sum%% *}"; }
listing() { (cd "$1" && find . -type f | sort | while read -r file; do echo "$file $(sha "$file") $(ls -l "$file" | cut -c1-10)"; done); }
empty_feed() { [[ -z "$(find "$1" -type f 2>/dev/null)" ]]; }
new_feed() { mkdir -p "$work/feeds/$1"; echo "$work/feeds/$1"; }
rewrite() { sed "$2" "$1" >"$1.new" && mv "$1.new" "$1"; }
copy_packages() { rm -rf "$work/packages/$1"; mkdir -p "$work/packages"; cp -R "$packages" "$work/packages/$1"; echo "$work/packages/$1"; }

refused() {
    local name="$1" feed="$2"
    shift 2
    if publish "$@"; then
        fail "$name: published"
        cat "$work/publish.log"
    elif empty_feed "$feed"; then
        pass "$name: $(tail -1 "$work/publish.log")"
    else
        fail "$name: refused, but the feed is not empty"
    fi
}

mkdir -p "$repo"
(cd "$root" && git ls-files -z -co --exclude-standard | tar --null -T - -cf -) | tar -xf - -C "$repo"
git -C "$repo" init -q
fixture_git add -A
fixture_git commit -q -m snapshot
built="$(git -C "$repo" rev-parse HEAD)"

printf '\nDirty working tree marker.\n' >>"$repo/src/Rezultat/README.md"
printf '#error untracked working tree input reached the release build\n' >"$repo/src/Rezultat/UntrackedInput.cs"
if ! "$repo/scripts/qualify.sh" release "$built" Rezultat Rezultat.Analyzers >"$work/release.log" 2>&1; then
    cat "$work/release.log"
    echo "FAIL release build of the fixture commit; log: $work/release.log"
    exit 1
fi
fixture_git add -A
fixture_git commit -q -m later
head="$(git -C "$repo" rev-parse HEAD)"

snapshot="$repo/artifacts/release/$built"
packages="$snapshot/artifacts/linux-arm64/package"
version="$(sed -n 's/^version=//p' "$packages"/Rezultat.[0-9]*.record)"
core_record="$packages/Rezultat.$version.record"
analyzer_record="$packages/Rezultat.Analyzers.$version.record"

check "HEAD moved after the build ($head)" test "$head" != "$built"
check "record names the built commit $built" grep -qx "source-revision=$built" "$core_record"
check "modified tracked README did not reach the package" \
    cmp -s <(unzip -p "$packages/Rezultat.$version.nupkg" README.md) <(git -C "$repo" show "$built:src/Rezultat/README.md")
check "untracked input is absent from the built snapshot" test ! -e "$snapshot/src/Rezultat/UntrackedInput.cs"
check "unrequested Rezultat.Testing.Xunit has no record" test ! -e "$packages/Rezultat.Testing.Xunit.$version.record"
check "core record has both archive hashes" grep -qx "snupkg-sha256=$(sha "$packages/Rezultat.$version.snupkg")" "$core_record"
check "analyzer record has no symbols hash" bash -c "! grep -q '^snupkg-sha256=' '$analyzer_record'"

feed="$(new_feed issued)"
publish "$version" "$packages" "$feed" && publish --package Rezultat.Analyzers "$version" "$packages" "$feed" \
    && pass "core and analyzer of $version published" || { fail "publication"; cat "$work/publish.log"; }
check "issued core record equals the build-time record" cmp -s "$core_record" "$feed/records/Rezultat.$version.record"
check "issued analyzer record equals the build-time record" cmp -s "$analyzer_record" "$feed/records/Rezultat.Analyzers.$version.record"
check "issued archives are the recorded bytes" cmp -s "$packages/Rezultat.$version.nupkg" "$feed/Rezultat.$version.nupkg"
check "same version of two package IDs keeps two records" \
    test "$(sed -n 's/^package-id=//p' "$feed/records/Rezultat.$version.record")/$(sed -n 's/^package-id=//p' "$feed/records/Rezultat.Analyzers.$version.record")" \
    == "Rezultat/Rezultat.Analyzers"
before="$(listing "$feed")"
publish "$version" "$packages" "$feed" && ! grep -q Published "$work/publish.log" \
    && pass "identical retry is a no-op" || fail "identical retry"
check "identical retry keeps feed bytes and modes" test "$before" == "$(listing "$feed")"

candidate="$(copy_packages candidate)"
rm "$candidate"/*.record
feed="$(new_feed candidate)"
refused "candidate without a record" "$feed" "$version" "$candidate" "$feed"
feed="$(new_feed unrequested)"
refused "unrequested package of the build" "$feed" --package Rezultat.Testing.Xunit "$version" "$packages" "$feed"

tampered="$(copy_packages hash)"
rewrite "$tampered/Rezultat.$version.record" "s/^nupkg-sha256=.*/nupkg-sha256=$(printf '0%.0s' {1..64})/"
feed="$(new_feed hash)"
refused "record with another package hash" "$feed" "$version" "$tampered" "$feed"

foreign="$(copy_packages foreign)"
cp "$foreign/Rezultat.Analyzers.$version.record" "$foreign/Rezultat.$version.record"
feed="$(new_feed foreign)"
refused "record of another package ID" "$feed" "$version" "$foreign" "$feed"

renamed="$(copy_packages renamed)"
mv "$renamed/Rezultat.Analyzers.$version.nupkg" "$renamed/Rezultat.Analyzers.0.99.0.nupkg"
sed "s/^version=.*/version=0.99.0/" "$renamed/Rezultat.Analyzers.$version.record" >"$renamed/Rezultat.Analyzers.0.99.0.record"
feed="$(new_feed renamed)"
refused "archive renamed to another version" "$feed" --package Rezultat.Analyzers 0.99.0 "$renamed" "$feed"

unknown="$(copy_packages unknown)"
rewrite "$unknown/Rezultat.$version.record" 's/^source-revision=.*/source-revision=unknown/'
feed="$(new_feed unknown)"
refused "new issuance with unknown revision" "$feed" "$version" "$unknown" "$feed"

feed="$(new_feed conflict)"
mkdir -p "$feed/records"
sed "s/^source-revision=.*/source-revision=$head/" "$core_record" >"$feed/records/Rezultat.$version.record"
chmod 444 "$feed/records/Rezultat.$version.record"
before="$(listing "$feed")"
if publish "$version" "$packages" "$feed"; then fail "conflicting record: published"; else pass "conflicting record: $(tail -1 "$work/publish.log")"; fi
check "conflicting record keeps the feed unchanged" test "$before" == "$(listing "$feed")"

feed="$(new_feed legacy)"
mkdir -p "$feed/symbols"
cp "$packages/Rezultat.$version.nupkg" "$feed/"
cp "$packages/Rezultat.$version.snupkg" "$feed/symbols/"
cp "$packages/Rezultat.Analyzers.$version.nupkg" "$feed/"
chmod 444 "$feed/Rezultat.$version.nupkg" "$feed/symbols/Rezultat.$version.snupkg" "$feed/Rezultat.Analyzers.$version.nupkg"
archives="$(listing "$feed")"
if publish "$version" "$packages" "$feed"; then fail "commit record for a legacy archive: published"; else pass "commit record for a legacy archive: $(tail -1 "$work/publish.log")"; fi
publish --legacy-record "$version" "$feed" && publish --legacy-record --package Rezultat.Analyzers "$version" "$feed" \
    && pass "legacy records written" || { fail "legacy records"; cat "$work/publish.log"; }
check "legacy core record is unknown with the issued hashes" test "$(cat "$feed/records/Rezultat.$version.record")" == \
    "$(printf 'package-id=Rezultat\nversion=%s\nsource-revision=unknown\nnupkg-sha256=%s\nsnupkg-sha256=%s' \
        "$version" "$(sha "$feed/Rezultat.$version.nupkg")" "$(sha "$feed/symbols/Rezultat.$version.snupkg")")"
check "legacy analyzer record is unknown without symbols" test "$(cat "$feed/records/Rezultat.Analyzers.$version.record")" == \
    "$(printf 'package-id=Rezultat.Analyzers\nversion=%s\nsource-revision=unknown\nnupkg-sha256=%s' \
        "$version" "$(sha "$feed/Rezultat.Analyzers.$version.nupkg")")"
check "legacy archives keep their bytes and modes" test "$archives" == "$(listing "$feed" | grep -v '^./records/')"
before="$(listing "$feed")"
publish --legacy-record "$version" "$feed" && grep -q Unchanged "$work/publish.log" \
    && pass "legacy retry is a no-op" || fail "legacy retry"
if publish "$version" "$packages" "$feed"; then fail "commit record after legacy record: published"; else pass "commit record after legacy record: $(tail -1 "$work/publish.log")"; fi
check "legacy feed unchanged by retries and refusals" test "$before" == "$(listing "$feed")"

publish_path="$broken_bin:$PATH"
feed="$(new_feed hashing-issue)"
refused "new issuance when hashing fails" "$feed" "$version" "$packages" "$feed"
feed="$(new_feed hashing-legacy)"
mkdir -p "$feed/symbols"
cp "$packages/Rezultat.$version.nupkg" "$feed/"
cp "$packages/Rezultat.$version.snupkg" "$feed/symbols/"
before="$(listing "$feed")"
if publish --legacy-record "$version" "$feed"; then fail "legacy record when hashing fails: published"; else pass "legacy record when hashing fails: $(tail -1 "$work/publish.log")"; fi
check "failed legacy record keeps the feed unchanged" test "$before" == "$(listing "$feed")"
publish_path="$PATH"

rm "$repo/src/Rezultat/UntrackedInput.cs"
fixture_git add -A
fixture_git commit -q -m buildable
buildable="$(git -C "$repo" rev-parse HEAD)"
hashing_packages="$repo/artifacts/release/$buildable/artifacts/linux-arm64/package"
if PATH="$broken_bin:$PATH" "$repo/scripts/qualify.sh" release "$buildable" Rezultat >"$work/release-hashing.log" 2>&1; then
    fail "record generation when hashing fails: release succeeded"
else
    pass "record generation when hashing fails: $(tail -1 "$work/release-hashing.log")"
fi
check "failed generation still built the archives" test -f "$hashing_packages/Rezultat.$version.nupkg"
check "failed generation writes no record" test -z "$(find "$hashing_packages" -name '*.record*')"

echo "release controls: $failures failure(s)"
exit "$failures"
