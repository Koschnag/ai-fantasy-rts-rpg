#!/usr/bin/env bash
set -euo pipefail

pages_script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
pages_root=$(dirname -- "$pages_script_dir")
pages_requested=${1:-}

if [[ -z "$pages_requested" ]]; then
  printf 'Aufruf: %s OUTPUT_DIRECTORY\n' "$0" >&2
  exit 2
fi

pages_output=$(python3 - "$pages_requested" <<'PY'
import os
import sys
print(os.path.realpath(sys.argv[1]))
PY
)

case "$pages_output" in
  /|"$pages_root"|"$pages_root/docs"|"$pages_root/docs/showcase"|"$pages_root/.git"|"$pages_root/.git/"*)
    printf 'Unsicheres Pages-Ausgabeverzeichnis: %s\n' "$pages_output" >&2
    exit 2
    ;;
esac

if [[ -e "$pages_output" ]] && find "$pages_output" -mindepth 1 -print -quit | grep -q .; then
  printf 'Pages-Ausgabeverzeichnis ist nicht leer: %s\n' "$pages_output" >&2
  exit 2
fi

python3 "$pages_root/scripts/test-pages.py" --source "$pages_root/docs/showcase"

pages_branch=${PAGES_SOURCE_BRANCH:-}
if [[ -z "$pages_branch" ]]; then
  pages_branch=$(git -C "$pages_root" symbolic-ref --quiet --short HEAD || true)
fi
if [[ -z "$pages_branch" ]]; then
  printf 'Pages-Quellbranch ist im detached HEAD nicht gesetzt. PAGES_SOURCE_BRANCH angeben.\n' >&2
  exit 2
fi

pages_status_temp=$(mktemp "${TMPDIR:-/tmp}/riftward-pages-status.XXXXXX")
pages_parent=$(dirname -- "$pages_output")
mkdir -p -- "$pages_parent"
pages_stage=$(mktemp -d "$pages_parent/.riftward-pages.XXXXXX")

cleanup_pages_build() {
  rm -f -- "$pages_status_temp"
  if [[ -n "${pages_stage:-}" && -d "$pages_stage" ]]; then
    rm -rf -- "$pages_stage"
  fi
}
trap cleanup_pages_build EXIT

pages_status_args=(
  --root "$pages_root"
  --output "$pages_status_temp"
  --branch "$pages_branch"
)
if [[ -n "${PAGES_PUBLIC_MAIN_COMMIT:-}" ]]; then
  pages_status_args+=(--public-main-commit "$PAGES_PUBLIC_MAIN_COMMIT")
fi
if [[ -n "${PAGES_WIP_COMMIT:-}" || -n "${PAGES_WIP_COMMITTED_AT:-}" ]]; then
  pages_status_args+=(--wip-commit "${PAGES_WIP_COMMIT:-}" --wip-committed-at "${PAGES_WIP_COMMITTED_AT:-}")
fi
python3 "$pages_root/scripts/pages_status.py" "${pages_status_args[@]}"

cp -R -- "$pages_root/docs/showcase/." "$pages_stage/"
rm -f -- "$pages_stage/README.md"
: > "$pages_stage/.nojekyll"
cp -- "$pages_status_temp" "$pages_stage/status.json"

python3 - "$pages_stage/index.html" "$pages_stage/status.json" <<'PY'
import json
from pathlib import Path
import sys

html_path = Path(sys.argv[1])
status = json.loads(Path(sys.argv[2]).read_text(encoding="utf-8"))
source = status["source"]
replacements = {
    "__RIFTWARD_SOURCE_COMMIT__": source["commit"],
    "__RIFTWARD_SOURCE_TREE__": source["tree"],
    "__RIFTWARD_SOURCE_BRANCH__": source["branch"],
    "__RIFTWARD_SOURCE_CLASSIFICATION__": source["classification"],
}
html = html_path.read_text(encoding="utf-8")
for placeholder, value in replacements.items():
    if html.count(placeholder) != 1:
        raise SystemExit(f"Pages-Platzhalter fehlt oder ist mehrdeutig: {placeholder}")
    html = html.replace(placeholder, value)
html_path.write_text(html, encoding="utf-8", newline="\n")
PY

python3 - "$pages_stage" <<'PY'
from hashlib import sha256
from pathlib import Path
import sys

root = Path(sys.argv[1])
manifest = root / "publication-hashes.sha256"
lines = []
for path in sorted(candidate for candidate in root.rglob("*") if candidate.is_file() and candidate != manifest):
    relative = path.relative_to(root).as_posix()
    lines.append(f"{sha256(path.read_bytes()).hexdigest()}  {relative}")
manifest.write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
PY

python3 "$pages_root/scripts/test-pages.py" --source "$pages_root/docs/showcase" --built "$pages_stage"

if [[ -d "$pages_output" ]]; then
  rmdir -- "$pages_output"
fi
mv -- "$pages_stage" "$pages_output"
pages_stage=
printf 'Pages-Artefakt: %s (Commit %s, Branch %s)\n' "$pages_output" "$(git -C "$pages_root" rev-parse --short=12 HEAD)" "$pages_branch"
