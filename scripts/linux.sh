#!/usr/bin/env bash
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
image="mcr.microsoft.com/dotnet/sdk:10.0.403-noble-aot"
issued_feed="${REZULTAT_LOCAL_FEED:-$HOME/.local/share/nuget/rezultat}"
host_output="$root/artifacts/linux-arm64"

engine="$(docker info --format '{{.OSType}}/{{.Architecture}}')"
if [[ "$engine" != linux/aarch64 && "$engine" != linux/arm64 ]]; then
    echo "native linux/arm64 Docker engine required, found $engine" >&2
    exit 1
fi
mkdir -p "$host_output/home"
mounts=()
[[ -d "$issued_feed" ]] && mounts=(-v "$(cd "$issued_feed" && pwd):/issued-feed:ro" -e REZULTAT_LOCAL_FEED=/issued-feed)
# --init: without an init process the command runs as PID 1, which ignores SIGINT/SIGTERM it does not handle, so an
# interrupted lane would keep running. The source stays read-only; the container's artifacts/ is the host's artifacts/linux-arm64, so its outputs never mix
# with workstation outputs. UseSharedCompilation=false: a one-shot container compiles with the standalone compiler, not
# a reused Roslyn compiler server; MSBuild reads the variable as a property in every nested build.
# RestoreDisableParallel=true restores projects one at a time, also inside source-bound release checkouts: cold parallel
# restores onto the mounted cache intermittently failed with corrupt package archives (cause unknown). Locked graphs,
# audit and signature checks stay on.
exec docker run --rm --init --platform linux/arm64 \
    --user "$(id -u):$(id -g)" ${mounts[@]+"${mounts[@]}"} -e UseSharedCompilation=false -e RestoreDisableParallel=true \
    -e HOME=/work/artifacts/home -e DOTNET_CLI_HOME=/work/artifacts/home -e NUGET_PACKAGES=/work/artifacts/nuget \
    -e DOTNET_NOLOGO=1 -e DOTNET_CLI_TELEMETRY_OPTOUT=1 -e TESTINGPLATFORM_TELEMETRY_OPTOUT=1 \
    -v "$root:/work:ro" -v "$host_output:/work/artifacts" -w /work \
    "$image" \
    bash -c 'echo "linux: $(uname -m), $(ldd --version | head -1), $(. /etc/os-release && echo "$PRETTY_NAME"), SDK $(dotnet --version)" >&2; exec "$@"' \
    linux "$@"
