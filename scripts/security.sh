#!/usr/bin/env bash
set -uo pipefail

security_script_dir=$(CDPATH= cd -- "$(dirname -- "$0")" && pwd)
security_root=$(dirname -- "$security_script_dir")
security_failures=0
security_secret_findings=0
security_allowed_placeholders=0
security_text_files=0
security_binary_files=0
security_excluded_files=0
security_symlinks=0
security_lfs_files=0
security_tmp_dir=

security_cleanup() {
  if [[ -n "$security_tmp_dir" && -d "$security_tmp_dir" ]]; then
    rm -rf -- "$security_tmp_dir"
  fi
}
trap security_cleanup EXIT HUP INT TERM

security_log() {
  printf '[security] %s\n' "$*"
}

security_error() {
  printf '[security] FEHLER: %s\n' "$*" >&2
  security_failures=$((security_failures + 1))
}

security_quote_path() {
  printf '%q' "$1"
}

security_is_excluded_path() {
  local security_path=$1

  case "/$security_path/" in
    */.git/*|*/bin/*|*/obj/*|*/artifacts/*|*/dist/*|*/.ai/runtime/*|*/assets/cooked/*|*/assets/quarantine/*)
      return 0
      ;;
    *)
      return 1
      ;;
  esac
}

security_is_allowed_test_placeholder() {
  local security_path=$1
  local security_line_lower=$2

  [[ "$security_path" == tests/fixtures/security/* ]] || return 1
  [[ "$security_line_lower" == *"security-gate: allow-test-placeholder"* ]] || return 1
  [[ "$security_line_lower" == *"riftward_test_only_"* ]] || return 1
  return 0
}

security_is_known_embedded_redaction_fixture() {
  local security_path=$1
  local security_line_lower=$2

  [[ "$security_path" == tests/* ]] || return 1
  [[ "$security_line_lower" == *'begin rsa private key'* ]] || return 1
  [[ "$security_line_lower" == *'\nprivate-key-material'* ]] || return 1
  return 0
}

security_report_secret() {
  local security_path=$1
  local security_line_number=$2
  local security_rule=$3

  printf '[security] SECRET-FUND %s:%s Regel=%s (Wert wird nicht ausgegeben)\n' \
    "$(security_quote_path "$security_path")" "$security_line_number" "$security_rule" >&2
  security_secret_findings=$((security_secret_findings + 1))
}

security_scan_text_file() {
  local security_actual_path=$1
  local security_logical_path=$2
  local security_line
  local security_line_lower
  local security_line_number=0
  local security_rule
  local security_assignment_value
  local security_credential_pattern='(api[_-]?key|access[_-]?token|auth[_-]?token|client[_-]?secret|secret[_-]?key|password|passwd|pwd)[[:space:]]*[:=][[:space:]]*(.*)$'
  local security_value_pattern="^[\"']?([[:alnum:]_./+@:-]{4,})"

  while IFS= read -r security_line || [[ -n "$security_line" ]]; do
    security_line_number=$((security_line_number + 1))
    security_line_lower=${security_line,,}
    security_rule=

    if [[ "$security_line" =~ -----BEGIN[[:space:]]+((RSA|EC|DSA|OPENSSH)[[:space:]]+)?PRIVATE[[:space:]]+KEY----- ]]; then
      security_rule=private-key
    elif [[ "$security_line" =~ (^|[^[:alnum:]_])Bearer[[:space:]]+[A-Za-z0-9._~+/-]{12,}={0,2} ]]; then
      security_rule=bearer-token
    elif [[ "$security_line" =~ AKIA[0-9A-Z]{16} ]]; then
      security_rule=aws-access-key
    elif [[ "$security_line" =~ gh[pousr]_[A-Za-z0-9_]{20,} ]] || \
         [[ "$security_line" =~ github_pat_[A-Za-z0-9_]{20,} ]]; then
      security_rule=github-token
    elif [[ "$security_line" =~ (^|[^[:alnum:]_])sk-[A-Za-z0-9_-]{20,} ]]; then
      security_rule=api-token-prefix
    elif [[ "$security_line_lower" =~ $security_credential_pattern ]]; then
      security_assignment_value=${BASH_REMATCH[2]}
      if [[ "$security_assignment_value" =~ $security_value_pattern ]]; then
        security_rule=credential-assignment
      fi
    fi

    if [[ -n "$security_rule" ]]; then
      if security_is_known_embedded_redaction_fixture "$security_logical_path" "$security_line_lower"; then
        security_allowed_placeholders=$((security_allowed_placeholders + 1))
      elif [[ "$security_rule" != private-key ]] && \
           security_is_allowed_test_placeholder "$security_logical_path" "$security_line_lower"; then
        security_allowed_placeholders=$((security_allowed_placeholders + 1))
      else
        security_report_secret "$security_logical_path" "$security_line_number" "$security_rule"
      fi
    fi
  done < "$security_actual_path"
}

security_file_uses_lfs() {
  local security_path=$1
  local -a security_attrs=()

  mapfile -d '' -t security_attrs < <(git check-attr -z filter -- "$security_path")
  [[ ${#security_attrs[@]} -ge 3 && "${security_attrs[2]}" == lfs ]]
}

security_self_test() {
  local security_expected
  local security_start
  local security_allowed_start

  security_tmp_dir=$(mktemp -d "${TMPDIR:-/tmp}/riftward-security-test.XXXXXX") || {
    printf '[security] Self-Test konnte kein temporäres Verzeichnis anlegen.\n' >&2
    return 1
  }

  mkdir -p "$security_tmp_dir/tests/fixtures/security" "$security_tmp_dir/docs"
  printf '%s\n' 'pass''word = correct-horse-battery-staple' > "$security_tmp_dir/docs/credential.txt"
  printf '%s\n' 'Authorization: Bear''er abcdefghijklmnopqrstuvwxyz012345' > "$security_tmp_dir/docs/bearer.txt"
  printf '%s\n' 'token = s''k-abcdefghijklmnopqrstuvwxyz012345' > "$security_tmp_dir/docs/api-token.txt"
  printf '%s\n' \
    '-----BEGIN PRI''VATE KEY-----' \
    'definitely-not-key-material' > "$security_tmp_dir/docs/private-key.txt"
  printf '%s\n' \
    'api_''key = RIFTWARD_TEST_ONLY_API_KEY # security-gate: allow-test-placeholder' \
    > "$security_tmp_dir/tests/fixtures/security/allowed.env"

  security_start=$security_secret_findings
  security_allowed_start=$security_allowed_placeholders
  security_scan_text_file "$security_tmp_dir/docs/credential.txt" 'docs/credential.txt'
  security_scan_text_file "$security_tmp_dir/docs/bearer.txt" 'docs/bearer.txt'
  security_scan_text_file "$security_tmp_dir/docs/api-token.txt" 'docs/api-token.txt'
  security_scan_text_file "$security_tmp_dir/docs/private-key.txt" 'docs/private-key.txt'
  security_scan_text_file \
    "$security_tmp_dir/tests/fixtures/security/allowed.env" \
    'tests/fixtures/security/allowed.env'

  security_expected=$((security_secret_findings - security_start))
  if [[ "$security_expected" -ne 4 ]]; then
    printf '[security] Self-Test fehlgeschlagen: 4 Funde erwartet, %s erhalten.\n' "$security_expected" >&2
    return 1
  fi
  security_expected=$((security_allowed_placeholders - security_allowed_start))
  if [[ "$security_expected" -ne 1 ]]; then
    printf '[security] Self-Test fehlgeschlagen: 1 kontrollierter Platzhalter erwartet, %s erhalten.\n' "$security_expected" >&2
    return 1
  fi

  security_log 'Self-Test bestanden (4 absichtliche Funde, 1 eng begrenzter Testplatzhalter).'
  return 0
}

security_usage() {
  printf '%s\n' \
    'Aufruf: scripts/security.sh [--self-test]' \
    '' \
    'Ohne Option: lokaler Security-Baseline-Gate für das aktuelle Repository.' \
    '--self-test: prüft nur die Secret-Erkennung mit temporären künstlichen Daten.'
}

case "${1:-}" in
  --self-test)
    security_self_test
    exit $?
    ;;
  -h|--help)
    security_usage
    exit 0
    ;;
  '')
    ;;
  *)
    security_usage >&2
    exit 2
    ;;
esac

cd "$security_root" || exit 2
if ! git rev-parse --show-toplevel >/dev/null 2>&1; then
  printf '[security] Kein Git-Repository: %s\n' "$(security_quote_path "$security_root")" >&2
  exit 2
fi

security_log 'Enumeriere versionierte sowie unversionierte, nicht ignorierte Repository-Dateien.'
security_tmp_dir=$(mktemp -d "${TMPDIR:-/tmp}/riftward-security.XXXXXX") || {
  printf '[security] Temporäres Arbeitsverzeichnis konnte nicht angelegt werden.\n' >&2
  exit 2
}
if ! git ls-files -z --cached --others --exclude-standard --deduplicate \
    > "$security_tmp_dir/repository-files.zlist"; then
  printf '[security] Repository-Dateiliste konnte nicht sicher erstellt werden.\n' >&2
  exit 2
fi
mapfile -d '' -t security_repo_files < "$security_tmp_dir/repository-files.zlist"

for security_path in "${security_repo_files[@]}"; do
  security_actual_path="$security_root/$security_path"

  if security_is_excluded_path "$security_path"; then
    security_excluded_files=$((security_excluded_files + 1))
    continue
  fi
  if [[ -L "$security_actual_path" ]]; then
    security_symlinks=$((security_symlinks + 1))
    continue
  fi
  if [[ ! -f "$security_actual_path" ]]; then
    continue
  fi
  if security_file_uses_lfs "$security_path"; then
    security_lfs_files=$((security_lfs_files + 1))
  fi
  if [[ -s "$security_actual_path" ]] && ! LC_ALL=C grep -Iq '' "$security_actual_path"; then
    security_binary_files=$((security_binary_files + 1))
    continue
  fi

  security_text_files=$((security_text_files + 1))
  security_scan_text_file "$security_actual_path" "$security_path"
done

if [[ "$security_secret_findings" -gt 0 ]]; then
  security_error "$security_secret_findings möglicher/mögliche Secret-Fund(e); Werte wurden nicht protokolliert."
else
  security_log "Secret-Heuristik: PASS ($security_text_files Textdateien; $security_allowed_placeholders kontrollierte Testplatzhalter)."
fi
security_log "Ausgeschlossen: $security_excluded_files Pfade; binär: $security_binary_files; Symlinks nicht verfolgt: $security_symlinks."

security_log 'Prüfe JSON-Syntax für alle einbezogenen .json-Dateien.'
if ! command -v jq >/dev/null 2>&1; then
  security_error 'jq fehlt; JSON-Syntaxprüfung konnte nicht laufen.'
else
  security_json_failures=0
  security_json_files=0
  for security_path in "${security_repo_files[@]}"; do
    [[ "$security_path" == *.json ]] || continue
    security_is_excluded_path "$security_path" && continue
    security_actual_path="$security_root/$security_path"
    [[ -f "$security_actual_path" && ! -L "$security_actual_path" ]] || continue
    security_json_files=$((security_json_files + 1))
    if ! jq empty "$security_actual_path" >/dev/null; then
      printf '[security] JSON-SYNTAX %s\n' "$(security_quote_path "$security_path")" >&2
      security_json_failures=$((security_json_failures + 1))
    fi
  done
  if [[ "$security_json_failures" -gt 0 ]]; then
    security_error "$security_json_failures ungültige JSON-Datei(en)."
  else
    security_log "JSON-Syntax: PASS ($security_json_files Dateien, einschließlich Toolchain und Asset-Schema)."
  fi
fi

security_log 'Führe Locked Restore mit aktivem NuGetAudit (alle transitiven Pakete) aus.'
if ! command -v dotnet >/dev/null 2>&1; then
  security_error 'dotnet fehlt; NuGetAudit konnte nicht laufen.'
else
  export DOTNET_CLI_TELEMETRY_OPTOUT=1
  export DOTNET_NOLOGO=1
  if dotnet restore Riftward.slnx --locked-mode \
      -p:NuGetAudit=true \
      -p:NuGetAuditMode=all \
      -p:TreatWarningsAsErrors=true; then
    security_log 'NuGet Locked Restore/Audit: PASS.'
  else
    security_error 'NuGet Locked Restore/Audit fehlgeschlagen oder Auditquelle nicht verlässlich erreichbar.'
  fi
fi

security_log 'Prüfe Git-LFS-Pointerintegrität.'
if ! git lfs version >/dev/null 2>&1; then
  security_error 'git-lfs fehlt; Pointerprüfung konnte nicht laufen.'
elif git rev-parse --verify HEAD >/dev/null 2>&1; then
  if git lfs fsck --pointers; then
    security_log 'Git LFS fsck --pointers: PASS.'
  else
    security_error 'Git LFS fsck --pointers meldet eine Integritätsverletzung.'
  fi
else
  security_lfs_output=
  security_lfs_status=0
  security_lfs_output=$(git lfs fsck --pointers 2>&1) || security_lfs_status=$?
  if [[ "$security_lfs_status" -ne 0 && "$security_lfs_output" == *'Git can'\''t resolve ref: "HEAD"'* && "$security_lfs_files" -eq 0 ]]; then
    security_log 'Git LFS fsck --pointers: NICHT ANWENDBAR (noch kein HEAD und keine LFS-Datei); nach erstem Commit zwingend.'
  elif [[ "$security_lfs_status" -eq 0 ]]; then
    security_log 'Git LFS fsck --pointers: PASS.'
  else
    printf '%s\n' "$security_lfs_output" >&2
    security_error 'Git LFS fsck --pointers konnte nicht verlässlich abgeschlossen werden.'
  fi
fi

security_log 'Grenze: Baseline-Gate, kein vollständiger Secret-Scanner, SAST, Malware-, Lizenz- oder Threat-Model-Nachweis.'
if [[ "$security_failures" -gt 0 ]]; then
  security_log "ERGEBNIS: FAIL ($security_failures fehlgeschlagene Gate-Bereiche)."
  exit 1
fi

security_log 'ERGEBNIS: PASS.'
