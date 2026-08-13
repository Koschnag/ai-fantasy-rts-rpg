#!/usr/bin/env bash
set -euo pipefail

rift_fresh_script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
rift_fresh_root=$(dirname -- "$rift_fresh_script_dir")
rift_fresh_tmp=$(mktemp -d "${TMPDIR:-/tmp}/riftward-fresh-checkout.XXXXXX")

rift_fresh_cleanup() {
  if [[ -d "$rift_fresh_tmp" ]]; then
    rm -rf -- "$rift_fresh_tmp"
  fi
}
trap rift_fresh_cleanup EXIT HUP INT TERM

cd "$rift_fresh_root"
git ls-files -z --cached --others --exclude-standard --deduplicate \
  | tar --null --files-from=- --create \
  | tar --extract --directory="$rift_fresh_tmp"

if [[ -d "$rift_fresh_tmp/.ai/runtime" ]]; then
  find "$rift_fresh_tmp/.ai/runtime" -mindepth 1 -not -name .gitkeep -delete
fi

cd "$rift_fresh_tmp"

# Das Archiv enthält bewusst keinen fremden .git-Zustand. Für die Checkout-
# Semantik wird ein lokaler Index aus exakt den kopierten Dateien aufgebaut;
# bereits versionierte, inzwischen ignorierte Pfade bleiben dabei sichtbar.
git init --quiet
git -C "$rift_fresh_root" ls-files -z --cached \
  | git add -f --pathspec-from-file=- --pathspec-file-nul
git add --all

./scripts/rift.sh bootstrap
./scripts/rift.sh build
./scripts/rift.sh lint
./scripts/rift.sh test
./scripts/rift.sh assets-check
./scripts/rift.sh rag-build
./scripts/rift.sh verify

printf 'Fresh-Checkout-Gate: PASS (%s)\n' "$rift_fresh_tmp"
