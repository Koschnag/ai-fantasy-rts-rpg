#!/usr/bin/env bash
set -euo pipefail

rift_trace_script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
rift_trace_root=$(dirname -- "$rift_trace_script_dir")
rift_trace_tmp=$(mktemp -d /tmp/riftward-generator-trace.XXXXXX)
rift_trace_workspace="$rift_trace_tmp/workspace"
rift_trace_log="$rift_trace_tmp/trace.log"

rift_trace_cleanup() {
  if [[ -d "$rift_trace_tmp" ]]; then
    rm -rf -- "$rift_trace_tmp"
  fi
}
trap rift_trace_cleanup EXIT HUP INT TERM

if [[ "$(uname -s)/$(uname -m)" != "Linux/x86_64" ]]; then
  printf 'Generator-Instrumentierung benötigt Linux/x86_64.\n' >&2
  exit 3
fi

if ! command -v strace >/dev/null 2>&1; then
  printf 'Generator-Instrumentierung benötigt strace.\n' >&2
  exit 3
fi

mkdir -p "$rift_trace_workspace"
for rift_trace_input in \
  toolchain.lock.json \
  assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json \
  tools/RiftHarness/AssetJobJournal.fs \
  tools/RiftHarness/BlenderCalibration.fs \
  tools/RiftHarness/DotnetAssetGenerator.fs; do
  mkdir -p "$rift_trace_workspace/$(dirname -- "$rift_trace_input")"
  cp -- "$rift_trace_root/$rift_trace_input" "$rift_trace_workspace/$rift_trace_input"
done

strace -f -qq \
  -e trace=process,network \
  -o "$rift_trace_log" \
  env DOTNET_EnableDiagnostics=0 \
  dotnet "$rift_trace_root/tests/RiftHarness.Tests/bin/Release/net10.0/RiftHarness.Tests.dll" \
  --generator-probe "$rift_trace_workspace"

if grep -Ev 'execve\([^,]+, \[[^]]*--generator-probe[^]]*\]' "$rift_trace_log" \
  | grep -E 'execve\(|fork\(|vfork\(|clone\([^)]*SIGCHLD|clone3\([^}]*SIGCHLD|socket\(|socketpair\(|connect\(|bind\(|listen\(|accept\(|accept4\(' \
  >/dev/null; then
  printf 'Generator öffnete instrumentiert einen Prozess oder Socket.\n' >&2
  exit 6
fi

printf 'Generator-Instrumentierung: PASS\n'
