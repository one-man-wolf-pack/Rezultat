#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
image="mcr.microsoft.com/dotnet/sdk:10.0.403-noble-aot"
issued_feed="${REZULTAT_LOCAL_FEED:-$HOME/.local/share/nuget/rezultat}"
host_output="$root/artifacts/linux-arm64"
scope_prefix=nuget-volume-
volume_prefix=rezultat-nuget-

# The scope file sits in the container-writable output tree; its content becomes a -v source, so only an exact volume
# name is accepted, never a host path that would be bind-mounted into the next container.
recorded_volume() {
    local volume
    volume="$(cat "$1")"
    [[ "$volume" =~ ^$volume_prefix[0-9a-f]{16}$ ]] || { echo "Not a Rezultat cache volume in $1: $volume" >&2; return 1; }
    echo "$volume"
}

if [[ "${1:-}" == --remove-caches ]]; then
    directory="${2:?usage: scripts/linux.sh --remove-caches <directory>}"
    [[ -d "$directory" ]] || exit 0
    find "$directory" -type f -name "$scope_prefix*" | while IFS= read -r scope; do
        volume="$(recorded_volume "$scope")"
        present="$(docker volume ls -q --filter "name=^$volume\$")"
        if [[ -n "$present" ]]; then
            docker volume rm "$volume" >/dev/null
            echo "Removed cache volume $volume" >&2
        fi
        rm "$scope"
    done
    exit 0
fi

engine="$(docker info --format '{{.OSType}}/{{.Architecture}}')"
if [[ "$engine" != linux/aarch64 && "$engine" != linux/arm64 ]]; then
    echo "native linux/arm64 Docker engine required, found $engine" >&2
    exit 1
fi
mkdir -p "$host_output"

# NuGet caches, scratch, restore roots and the CLI home stay in a Docker volume, off the host share: a cold release
# restore onto host-shared caches failed reading back a package it had just written (I-021, cause unknown). The volume
# belongs to this checkout and image and is mounted at the same /cache by every one-shot command, so restored assets
# stay valid across containers; a new checkout, such as each release clone, starts empty. Keep until that failure is
# explained and host-shared caches are qualified again.
scope="$host_output/$scope_prefix$(printf '%s' "$image" | tr -c 'A-Za-z0-9.-' '-')"
if [[ ! -f "$scope" ]]; then
    volume="$volume_prefix$(od -An -N8 -tx1 /dev/urandom | tr -d ' \n')"
    docker volume create --label org.rezultat.checkout="$root" --label org.rezultat.image="$image" "$volume" >/dev/null
    docker run --rm --network none --user 0:0 -v "$volume:/cache" "$image" \
        sh -c 'test -z "$(ls -A /cache)" && chown "$1" /cache' sh "$(id -u):$(id -g)" \
        || { docker volume rm "$volume" >/dev/null; echo "Cannot prepare the new cache volume $volume" >&2; exit 1; }
    if (set -o noclobber; echo "$volume" >"$scope") 2>/dev/null; then
        echo "linux: new empty cache volume $volume" >&2
    else
        docker volume rm "$volume" >/dev/null
    fi
fi
volume="$(recorded_volume "$scope")"
docker volume inspect "$volume" >/dev/null 2>&1 \
    || { echo "Cache volume $volume recorded in $scope is missing; run just gc." >&2; exit 1; }

mounts=()
[[ -d "$issued_feed" ]] && mounts=(-v "$(cd "$issued_feed" && pwd):/issued-feed:ro" -e REZULTAT_LOCAL_FEED=/issued-feed)
# --init: without an init process the command runs as PID 1, which ignores SIGINT/SIGTERM it does not handle, so an
# interrupted lane would keep running. The source stays read-only; the container's artifacts/ is the host's artifacts/linux-arm64, so its outputs never mix
# with workstation outputs. UseSharedCompilation=false: a one-shot container compiles with the standalone compiler, not
# a reused Roslyn compiler server; MSBuild reads the variable as a property in every nested build.
# RestoreDisableParallel=true restores projects one at a time, also inside source-bound release checkouts: cold parallel
# restores intermittently failed with corrupt package archives (cause unknown). Locked graphs, audit and signature checks
# stay on.
exec docker run --rm --init --platform linux/arm64 \
    --user "$(id -u):$(id -g)" ${mounts[@]+"${mounts[@]}"} -e UseSharedCompilation=false -e RestoreDisableParallel=true \
    -v "$volume:/cache" -e HOME=/cache/home -e DOTNET_CLI_HOME=/cache/home -e NUGET_PACKAGES=/cache/nuget \
    -e NUGET_HTTP_CACHE_PATH=/cache/http -e NUGET_SCRATCH=/cache/scratch -e NUGET_PLUGINS_CACHE_PATH=/cache/plugins \
    -e RezultatRestoreRoot=/cache \
    -e DOTNET_NOLOGO=1 -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e TESTINGPLATFORM_TELEMETRY_OPTOUT=1 \
    -v "$root:/work:ro" -v "$host_output:/work/artifacts" -w /work \
    "$image" \
    bash -c 'echo "linux: $(uname -m), $(ldd --version | head -1), $(. /etc/os-release && echo "$PRETTY_NAME"), SDK $(dotnet --version)" >&2; exec "$@"' \
    linux "$@"
