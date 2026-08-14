#!/usr/bin/env bash
set -euo pipefail

rift_ci_script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
rift_ci_root=$(dirname -- "$rift_ci_script_dir")
rift_ci_tmp=$(mktemp -d /tmp/riftward-dotnet-asset-ci.XXXXXX)
rift_ci_checkout="$rift_ci_tmp/checkout"
rift_ci_output_relative=artifacts/t007/dotnet-asset-calibration.json
rift_ci_log_relative=artifacts/t007/test.log

rift_ci_has_leakage() {
  if ! git diff --quiet || [[ "$rift_ci_tree_before" != "$(git write-tree)" ]]; then
    return 0
  fi

  if [[ -n "$(git ls-files --others --exclude-standard)" ]]; then
    return 0
  fi

  if git ls-files | grep -E '^(assets/(quarantine|cooked)/|\.ai/runtime/)' \
    | grep -vFx '.ai/runtime/.gitkeep' >/dev/null; then
    return 0
  fi

  if git status --porcelain --ignored \
    | grep -Eq '^!! (assets/(quarantine|cooked)/|\.ai/runtime/(memory|index|runs|asset-jobs|.*\.json|.*\.lock))'; then
    return 0
  fi

  return 1
}

rift_ci_prove_leakage_fixture() {
  local fixture_root=$1
  local fixture_source="$fixture_root/docs/DOTNET_GENERATOR_CONTRACT.md"

  mkdir -p \
    "$fixture_root/assets/quarantine/fixture" \
    "$fixture_root/assets/cooked" \
    "$fixture_root/.ai/runtime/runs/fixture"
  printf 'fixture\n' >"$fixture_root/assets/quarantine/fixture/output.glb"
  printf 'fixture\n' >"$fixture_root/assets/cooked/output.bin"
  printf 'fixture\n' >"$fixture_root/.ai/runtime/runs/fixture/event.jsonl"
  printf '\n' >>"$fixture_source"

  if ! rift_ci_has_leakage; then
    printf 'T-007-Leakage-Fixture wurde nicht erkannt.\n' >&2
    exit 6
  fi

  git checkout-index --force -- "$fixture_source"
  rm -rf -- \
    "$fixture_root/assets/quarantine/fixture" \
    "$fixture_root/assets/cooked" \
    "$fixture_root/.ai/runtime/runs/fixture"
}

rift_ci_cleanup() {
  if [[ -d "$rift_ci_tmp" ]]; then
    rm -rf -- "$rift_ci_tmp"
  fi
}
trap rift_ci_cleanup EXIT HUP INT TERM

cd "$rift_ci_root"

if ! git diff --quiet HEAD -- || ! git diff --cached --quiet HEAD -- \
  || [[ -n "$(git ls-files --others --exclude-standard)" ]]; then
  printf 'T-007 verlangt einen vollständig eingecheckten Arbeitsbaum.\n' >&2
  exit 2
fi

mkdir -p "$rift_ci_checkout"
git archive --format=tar HEAD | tar --extract --directory="$rift_ci_checkout"

cd "$rift_ci_checkout"
git init --quiet
git add -f --all
rift_ci_tree_before=$(git write-tree)

export DOTNET_CLI_HOME="$rift_ci_tmp/dotnet-home"
export NUGET_PACKAGES="$rift_ci_tmp/nuget-packages"
export DOTNET_NOLOGO=1
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_EnableDiagnostics=0
export NUGET_XMLDOC_MODE=skip
export PATH="$HOME/.local/bin:$PATH"

if [[ "$(dotnet --version)" != "10.0.110" ]]; then
  printf 'T-007 benötigt exakt .NET SDK 10.0.110.\n' >&2
  exit 3
fi

mkdir -p "$rift_ci_tmp/logs"

dotnet tool restore >"$rift_ci_tmp/logs/restore.log" 2>&1
dotnet restore Riftward.slnx --locked-mode \
  -p:RestorePackagesPath="$NUGET_PACKAGES" >>"$rift_ci_tmp/logs/restore.log" 2>&1
dotnet build Riftward.slnx --configuration Release --no-restore \
  -p:RestorePackagesPath="$NUGET_PACKAGES" >"$rift_ci_tmp/logs/build.log" 2>&1
./scripts/security.sh >"$rift_ci_tmp/logs/security.log" 2>&1
dotnet tool run fantomas . --check >"$rift_ci_tmp/logs/lint.log" 2>&1
dotnet run --project tests/RiftHarness.Tests/RiftHarness.Tests.fsproj \
  --configuration Release --no-build >"$rift_ci_tmp/logs/test.log" 2>&1
./scripts/dotnet-generator-instrumentation-test.sh \
  >"$rift_ci_tmp/logs/instrumentation.log" 2>&1
rift_ci_test_report_sha256=$(sha256sum "$rift_ci_tmp/logs/test.log" | cut -d ' ' -f 1)

mkdir -p "$(dirname -- "$rift_ci_output_relative")"
dotnet tools/RiftHarness/bin/Release/net10.0/RiftHarness.dll \
  asset-ci-evidence --output "$rift_ci_output_relative" \
  --test-report-sha256 "$rift_ci_test_report_sha256" >"$rift_ci_tmp/logs/evidence.log" 2>&1

if rift_ci_has_leakage; then
  printf 'T-007 hinterließ versionierte, unversionierte oder Runtime-Leakage.\n' >&2
  exit 6
fi

rift_ci_prove_leakage_fixture "$rift_ci_checkout"

if rift_ci_has_leakage; then
  printf 'T-007-Leakage-Fixture wurde nicht vollständig bereinigt.\n' >&2
  exit 6
fi

for rift_ci_log in "$rift_ci_tmp"/logs/*.log; do
  if [[ "$(wc -c <"$rift_ci_log")" -gt 1048576 ]]; then
    printf 'T-007-Log überschreitet 1 MiB.\n' >&2
    exit 4
  fi
done

mkdir -p "$rift_ci_root/artifacts/t007"
sed -e "s#${rift_ci_tmp//\#/\\#}#<temporary>#g" \
  -e "s#${rift_ci_root//\#/\\#}#<workspace>#g" \
  "$rift_ci_tmp/logs/test.log" >"$rift_ci_root/$rift_ci_log_relative"
install -m 0644 "$rift_ci_checkout/$rift_ci_output_relative" "$rift_ci_root/$rift_ci_output_relative"

printf 'T-007 .NET-Asset-CI: PASS\n'
