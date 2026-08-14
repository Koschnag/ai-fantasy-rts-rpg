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

./scripts/rift.sh bootstrap
./scripts/rift.sh build
./scripts/rift.sh lint
./scripts/rift.sh test
./scripts/rift.sh assets-check
./scripts/rift.sh rag-build
./scripts/rift.sh verify

git diff --quiet
printf 'Fresh-Checkout-Gate: PASS\n'
