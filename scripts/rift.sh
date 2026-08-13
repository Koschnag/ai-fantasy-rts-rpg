#!/usr/bin/env sh
set -eu

rift_script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
rift_root=$(dirname -- "$rift_script_dir")
cd "$rift_root"

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

rift_need_dotnet() {
  if ! command -v dotnet >/dev/null 2>&1; then
    printf 'dotnet fehlt. Führe scripts/bootstrap-dotnet.sh aus.\n' >&2
    exit 127
  fi
}

rift_restore() {
  rift_need_dotnet
  dotnet tool restore
  dotnet restore Riftward.slnx --locked-mode
}

rift_need_build_outputs() {
  if [ ! -f "$rift_root/tools/RiftHarness/bin/Release/net10.0/RiftHarness.dll" ] \
    || [ ! -f "$rift_root/tests/RiftHarness.Tests/bin/Release/net10.0/RiftHarness.Tests.dll" ]; then
    printf 'Release-Build fehlt. Führe zuerst ./scripts/rift.sh bootstrap oder ./scripts/rift.sh build aus.\n' >&2
    exit 4
  fi
}

rift_need_harness_state() {
  if [ ! -d "$rift_root/.ai/runtime/runs" ] || [ ! -d "$rift_root/.ai/runtime/index" ]; then
    printf 'Harness ist nicht initialisiert. Führe zuerst ./scripts/rift.sh bootstrap aus.\n' >&2
    exit 4
  fi
}

rift_need_rag_index() {
  rift_need_harness_state
  if [ ! -f "$rift_root/.ai/runtime/index/bm25.json" ]; then
    printf 'RAG-Index fehlt. Führe zuerst ./scripts/rift.sh rag-build aus.\n' >&2
    exit 4
  fi
}

rift_harness() {
  dotnet run --project tools/RiftHarness/RiftHarness.fsproj --configuration Release --no-build --no-restore -- "$@"
}

rift_unavailable() {
  printf '%s: NICHT VERFÜGBAR – benötigt eine eigene READY-Aufgabe und darf nicht grün vorgetäuscht werden.\n' "$1" >&2
  exit 3
}

rift_command=${1:-help}
if [ "$#" -gt 0 ]; then
  shift
fi

case "$rift_command" in
  bootstrap)
    "$rift_root/scripts/bootstrap-dotnet.sh"
    if [ -x "$HOME/.local/bin/dotnet" ]; then
      export PATH="$HOME/.local/bin:$PATH"
    fi
    rift_restore
    dotnet build Riftward.slnx --configuration Release --no-restore
    rift_harness init
    rift_harness build-rag
    printf 'Für native/Asset-Systempakete anschließend scripts/bootstrap-ubuntu.sh interaktiv ausführen.\n'
    ;;
  build)
    rift_restore
    dotnet build Riftward.slnx --configuration Release --no-restore "$@"
    ;;
  test)
    rift_need_dotnet
    rift_need_build_outputs
    dotnet run --project tests/RiftHarness.Tests/RiftHarness.Tests.fsproj --configuration Release --no-restore -- "$@"
    ;;
  fmt)
    rift_need_dotnet
    dotnet tool run fantomas . "$@"
    ;;
  lint)
    rift_need_dotnet
    dotnet tool run fantomas . --check "$@"
    ;;
  harness)
    rift_need_dotnet
    rift_need_build_outputs
    rift_harness "$@"
    ;;
  rag-build)
    rift_need_dotnet
    rift_need_build_outputs
    rift_need_harness_state
    rift_harness build-rag "$@"
    ;;
  rag-query)
    rift_need_dotnet
    rift_need_build_outputs
    rift_need_rag_index
    rift_harness query-rag "$@"
    ;;
  assets-check)
    rift_need_dotnet
    rift_need_build_outputs
    rift_harness assets-check "$@"
    ;;
  security)
    "$rift_root/scripts/security.sh" "$@"
    ;;
  fresh-checkout-test)
    "$rift_root/scripts/fresh-checkout-test.sh" "$@"
    ;;
  verify)
    rift_need_rag_index
    rift_restore
    dotnet build Riftward.slnx --configuration Release --no-restore
    dotnet run --project tests/RiftHarness.Tests/RiftHarness.Tests.fsproj --configuration Release --no-restore
    rift_harness assets-check
    rift_harness verify
    find .ai -type f -name '*.json' -not -path '.ai/runtime/*' -exec jq empty {} +
    ;;
  bench|check|package)
    rift_unavailable "$rift_command"
    ;;
  help|-h|--help)
    printf '%s\n' \
      'Project Riftward Aufgaben' \
      '' \
      '  bootstrap     .NET installieren; Restore, Build, Harness-init und RAG-Build ausführen' \
      '  build         Tool-/Test-Solution im Release-Modus bauen' \
      '  fmt           F#-Quellen mit gepinntem Fantomas formatieren' \
      '  lint          F#-Formatierung unverändernd prüfen' \
      '  test          Harness-Tests ausführen' \
      '  harness ...   RiftHarness CLI aufrufen' \
      '  rag-build     lokalen BM25-Index nach bootstrap/build neu bauen' \
      '  rag-query ... vorhandenen, aktuellen BM25-Index abfragen' \
      '  assets-check  Assetprovenienz und Clean-Room-Regeln offline prüfen' \
      '  security      lokalen Secret-/NuGet-/JSON-/LFS-Baseline-Gate ausführen' \
      '  fresh-checkout-test  isolierten Bootstrap-, Build-, Lint-, Test-, RAG- und Verify-Lauf ausführen' \
      '  verify        nach bootstrap Build, Tests, Harness- und JSON-Integrität prüfen' \
      '  check         noch nicht verfügbarer vollständiger Gate-Satz' \
      '' \
      'Noch nicht implementierte Gates schlagen ausdrücklich fehl.'
    ;;
  *)
    printf 'Unbekannte Aufgabe: %s\n' "$rift_command" >&2
    exit 2
    ;;
esac
