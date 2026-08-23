#!/usr/bin/env bash
set -euo pipefail

pages_script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
pages_root=$(dirname -- "$pages_script_dir")
pages_output=${1:-}

if [[ -z "$pages_output" ]]; then
  printf 'Aufruf: %s OUTPUT_DIRECTORY\n' "$0" >&2
  exit 2
fi

pages_output=$(realpath -m -- "$pages_output")
case "$pages_output" in
  /|"$pages_root"|"$pages_root/docs"|"$pages_root/docs/showcase")
    printf 'Unsicheres Pages-Ausgabeverzeichnis: %s\n' "$pages_output" >&2
    exit 2
    ;;
esac

if grep -Eiq '<video[^>]*[[:space:]]autoplay([[:space:]>]|=)' "$pages_root/docs/showcase/index.html"; then
  printf 'Pages-Policy verletzt: Konzeptvideo darf nicht automatisch starten.\n' >&2
  exit 2
fi

if grep -q 'ENV-FLOODED-CAUSEWAY-KEYFRAME-002' "$pages_root/docs/showcase/index.html"; then
  printf 'Pages-Policy verletzt: needs-work-Konzept darf nicht publiziert werden.\n' >&2
  exit 2
fi

if ! grep -q 'CONCEPT · NOT GAMEPLAY' "$pages_root/docs/showcase/index.html"; then
  printf 'Pages-Policy verletzt: sichtbare Konzeptkennzeichnung fehlt.\n' >&2
  exit 2
fi

(cd "$pages_root/docs/showcase" && sha256sum -c assets/media-checksums.sha256)

if [[ -e "$pages_output" ]] && find "$pages_output" -mindepth 1 -print -quit | grep -q .; then
  printf 'Pages-Ausgabeverzeichnis ist nicht leer: %s\n' "$pages_output" >&2
  exit 2
fi

mkdir -p -- "$pages_output"
cp -R -- "$pages_root/docs/showcase/." "$pages_output/"
rm -f -- "$pages_output/README.md"
: > "$pages_output/.nojekyll"

pages_accepted=$(awk -F'|' '/^\| T-[0-9]+ / { value=$8; gsub(/^[[:space:]]+|[[:space:]]+$/, "", value); if (value == "DONE") count++ } END { print count+0 }' "$pages_root/BACKLOG.md")
pages_ready=$(awk -F'|' '/^\| T-[0-9]+ / { value=$8; gsub(/^[[:space:]]+|[[:space:]]+$/, "", value); if (value == "READY") count++ } END { print count+0 }' "$pages_root/BACKLOG.md")
pages_runtime_status=$(awk -F'"' '$2 == "status" { print $4; exit }' "$pages_root/.ai/tasks/T-010-native-walking-skeleton.json")
pages_commit=$(git -C "$pages_root" rev-parse HEAD)
pages_generated_at=$(git -C "$pages_root" show -s --format=%cI HEAD)

if [[ ! "$pages_commit" =~ ^[0-9a-f]{40}$ ]] ||
   [[ ! "$pages_runtime_status" =~ ^(draft|ready|running|review|accepted|blocked|cancelled)$ ]]; then
  printf 'Projektstatus konnte nicht sicher aus dem Repository gelesen werden.\n' >&2
  exit 2
fi

printf '%s\n' \
  '{' \
  '  "schemaVersion": 1,' \
  "  \"generatedAt\": \"$pages_generated_at\"," \
  "  \"commit\": \"$pages_commit\"," \
  "  \"workItems\": { \"accepted\": $pages_accepted, \"ready\": $pages_ready }," \
  "  \"activeTask\": { \"id\": \"T-010\", \"status\": \"$pages_runtime_status\" }," \
  '  "claims": { "gameplay": false, "targetHardwareValidated": false, "physicalEdition": false }' \
  '}' > "$pages_output/status.json"

printf 'Pages-Artefakt: %s (Commit %s)\n' "$pages_output" "${pages_commit:0:12}"
