#!/usr/bin/env bash
set -euo pipefail

rift_fresh_script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
rift_fresh_root=$(dirname -- "$rift_fresh_script_dir")
rift_fresh_tmp=$(mktemp -d /tmp/riftward-fresh-checkout.XXXXXX)

rift_fresh_cleanup() {
  if [[ -d "$rift_fresh_tmp" ]]; then
    rm -rf -- "$rift_fresh_tmp"
  fi
}
trap rift_fresh_cleanup EXIT HUP INT TERM

cd "$rift_fresh_root"

if ! git diff --quiet HEAD -- || ! git diff --cached --quiet HEAD -- \
  || [[ -n "$(git ls-files --others --exclude-standard)" ]]; then
  printf 'Fresh-Checkout-Gate verlangt einen vollständig eingecheckten Arbeitsbaum.\n' >&2
  exit 2
fi

git archive --format=tar HEAD | tar --extract --directory="$rift_fresh_tmp"

if [[ -d "$rift_fresh_tmp/.ai/runtime" ]]; then
  find "$rift_fresh_tmp/.ai/runtime" -mindepth 1 -not -name .gitkeep -delete
fi

cd "$rift_fresh_tmp"

# Das Archiv enthält ausschließlich die Bytes des aktuellen Commits und keinen
# fremden .git-Zustand. Ein lokaler Index bildet genau diesen Commitbaum ab.
git init --quiet
git add -f --all
rift_fresh_source_tree=$(git write-tree)

# Der Vertrag führt jede Prüfung der ursprünglichen Vereinigung genau einmal
# auf denselben Archivbytes aus: bootstrap stellt Tool-Restore, Locked-Restore,
# Release-Build, Harness-Init und den RAG-Index her; lint prüft Format- und
# Toolchain-/Lizenz-/ISA-Baseline; verify enthält Locked-Restore, Release-Build,
# die volle Testsuite, assets-check, Harness-Verify und JSON-Integrität;
# rag-build belegt den deterministischen Indexneubau, den verify validiert.
# Die frühere Schrittliste wiederholte Release-Build und komplette Testsuite
# ohne Erkenntnisgewinn (verify enthält beide) und ließ den Vertrag das
# 30-Minuten-Integrationsfenster messbar überschreiten (Exit 124 im
# verify-Suitenlauf bei 315/341 Tests, Commit dacdc28). Kein Gate ist entfernt,
# verkürzt oder abgeschwächt; die Archive- und Driftprüfung bleibt unverändert.
./scripts/rift.sh bootstrap
./scripts/rift.sh lint
./scripts/rift.sh rag-build
# Der PR-Commitbereich wurde bereits im äußeren Checkout geprüft. Das reine
# Source-Archiv besitzt absichtlich weder Commitobjekte noch Remote und darf
# deshalb auch bei geerbter GitHub-PR-Umgebung keinen impliziten Fetch starten.
# rift.sh akzeptiert die Ausnahme nur in genau dieser gebundenen Topologie.
RIFT_VERIFY_SOURCE_ARCHIVE=1 ./scripts/rift.sh verify

if ! git diff --quiet -- \
  || [[ "$rift_fresh_source_tree" != "$(git write-tree)" ]] \
  || [[ -n "$(git ls-files --others --exclude-standard)" ]]; then
  printf 'Fresh-Checkout-Gate abgelehnt: Archivbytes sind nach den Prüfungen gedriftet.\n' >&2
  exit 2
fi
printf 'Fresh-Checkout-Gate: PASS\n'
