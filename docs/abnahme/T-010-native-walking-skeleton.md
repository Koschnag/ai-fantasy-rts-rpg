# T-010 – SDL3-Fenster, Eingabe und bgfx-Dreieck (linux-x64)

- **Status:** `DONE` (2026-08-24)
- **Umsetzung:** zwei abgebrochene Implementierungsläufe
  (`01M0QTN99Y9JXJK07Z4G28B3ZJ`, `01M0QV6VT20FG2C8PBQC9C56XS`) begannen den
  nativen Build; der unabhängige Review-/Vollendungslauf
  **`01M0QYAA11MC89GVMP6BWR7016`** (Akteur `t010-review-completion`) prüfte,
  reparierte und vollendete die Arbeit.
- **Ausgangscommit:** `9637ec8627bfacbf598bdecf8b77965bdf556655`
  („accept t010 ready specification after independent review")
- finaler Eventhash des Laufs: `1cbf07b87565d109687154e9ad6ac7eac177736ba6cc4f8d0a9596c7db7ea4d2`
- Summaryhash: `7c020883437b5c66d432088ec82b97597ced4811c39e1271a28dc04d2058b7ed`

## Gelieferte Anteile

| Bereich | Ergebnis |
|---|---|
| Pins (Abschnitt 0) | `toolchain.lock.json` `nativeComponents`: SDL3 Tag `release-3.4.14`, bgfx/bx/bimg Commit-Pins aus Kohorte `2026-08-23-cohort-1` (vor Upstream-Minimum-GL-4.3), Quell-SHA-256, SPDX-Lizenzen; Inventar in `THIRD_PARTY_NOTICES.md`; gatende Prüfung `toolchain-check` in `lint` und `security` mit Positiv-/Negativfixtures |
| Nativer Build | `scripts/native-build-linux-x64.sh`: hashverifizierte Erstbeschaffung, Offline-Cache `.ai/runtime/cache/native/`, SDL3 (CMake/Ninja, minimale Optionen), bgfx-Familie (GENie `release64`, `-DBGFX_CONFIG_RENDERER_OPENGL=33`, `-msse4.2 -fPIC`), eigener C-Shim `libriftbgfx.so`, offline kompilierte GLSL-130-Shader; Artefakthash-Manifest mit strikter Wiederholprüfung |
| Reproduzierbarkeit | Zwei aufeinanderfolgende `--fresh`-Neubauten sind byteidentisch (`SOURCE_DATE_EPOCH=1786623387` fixiert bx-`__DATE__/__TIME__`); `--verify-cache` prüft ohne Netzwerk und schreibt das Manifest nicht neu |
| Interop | `src/Riftward.Platform`: zentrale `[LibraryImport]`-Deklarationen für SDL3 und den Shim, verwaltete Fassaden mit expliziten Besitz-/Freigaberegeln (Programm → Shader → Vertex-Buffer → Shutdown), kontrollierte Fehlerobjekte und stabile Exitcodes 14–24 |
| Host | `src/Riftward.App`: Fensterereignis-/Renderloop bis Quit oder fester Zeitgrenze; Modi `plattformsmoke` und `effizienzbaseline` mit maschinenlesbaren Reports (OS/Kernel, CPU, GPU/GL-Treiber, Backend, Pins, Manifesthash) |
| Tests | 17 neue F#-Tests: Pin-/Lizenz-/Kohorten-/ISA-Fixtures (Positiv/Negativ inkl. Cache-Kreuzprüfung), Artefakt-Fault-Injection je Fehlerklasse, No-write-Nachweis, Exitcode-Stabilität, SDL-/bgfx-Besitzregeln über Fakes, Architekturtest gegen Native-Typen außerhalb des Plattform-Layers; Suite 146/146 |
| Doku | Neu: `docs/NATIVE_UNTERBAU.md`. Aktualisiert: `docs/PLATTFORMMATRIX.md` (bestätigte Mindestbasis linux-x64), `docs/AUTOMATION.md` + `README.md` (öffentliche Befehle), `BACKLOG.md`, Task-Manifest |

## In-Scope-Reparaturen am vorgefundenen Stand

- bgfx-Ausgabepfade/-Namen für `config=release64` korrigiert
  (`.build/linux64_gcc/bin/lib*Release.a`).
- x86-64-v2-Mindestbasis (`-msse4.2`) und `-fPIC` für die bgfx-Familie gesetzt;
  Shim-Link um `libbimgRelease.a` und direkte GL-Laufzeitbindung ergänzt.
- SDL3-X11-Laufzeitbindung repariert (dev-Symlinks im Cache auf System-Sonames,
  `dynamic libX11 -> libX11.so.6`); vorher „No available video device".
- Shader-Semantikdefinition vervollständigt (`varying.def.sc`: `a_position`,
  `a_color0`); vorher undeklarierte Attribute.
- `SOURCE_DATE_EPOCH` fixiert; vorher nicht reproduzierbare `libbx.a`.
- Verify-Modus des Native-Builds schreibt das Hashmanifest nicht mehr neu
  (beschaedigte Artefakte failen zuverlässig statt still durchzulaufen).
- `NativeArtifacts.Validate` löst Manifestpfade workspace-relativ auf und
  lehnt Pfadflucht ab (von den neuen Fault-Injection-Tests aufgedeckt).

## Abnahmekriterien (Evidenz je ID im Lauf `…BWR7016/evidence/`)

| AC | Nachweis | Ergebnis |
|---|---|---|
| AC-T010-01 | `t010-toolchain-check.json`, `gate-lint.log`; Negativfixtures: geänderter Hash (Cache-Kreuzprüfung), fehlende Lizenz, inkonsistente Kohorte, fehlender Notices-Eintrag | PASS |
| AC-T010-02 | `t010-native-rebuild.json` (byteidentischer `--fresh`-Neubau, leerer Manifest-Diff), `t010-native-verify-cache.log` (Offline-Prüfung exit 0; Manipulation → exit 1) | PASS |
| AC-T010-03 | `t010-plattformsmoke.json`: nativ auf i7-3770/RX 570 (Mesa 26.0.3-1ubuntu1, radeonsi/polaris10/ACO), Backend OpenGL (id 8, 3.3-Core-Pflichtpfad, kein Fallback), 242 fehlerfreie Frames, Fenster-/Quit-Ereignisse behandelt, exit 0 innerhalb 4000 ms, Start→erster Frame 333 ms | PASS |
| AC-T010-04 | `t010-interop-tests.json`, `gate-test.log`: LibraryImport nur im Plattform-Layer (Architekturtest), Besitzregeln/Doppel-Freigabe/falsche Shutdown-Reihenfolge/ungültige Handles über Fakes abgedeckt | PASS |
| AC-T010-05 | `t010-fault-injection.json`: fehlendes/unvollständiges/hashbeschädigtes Artefakt und Backend-Initialisierungsfehler → kontrollierte Meldung + Exitcodes 16/15/17/18, kein Schreiben (No-write-Test) | PASS |
| AC-T010-06 | `t010-gates-summary.json` + Gate-Logs: lint/build/test/security/rag-build/verify je exit 0, 0 neue Compiler-/Analyzerwarnungen; einzige neue Abhängigkeiten = die vier gepinnten nativen Komponenten mit Zweck/Lizenz/Austauschstrategie (`NATIVE_UNTERBAU.md`) | PASS |
| AC-T010-07 | `t010-effizienzbaseline.json` (10-Minuten-Idle-Fenster): Start 323 ms ≤ 5000; Idle-RSS 142 MiB ≤ 300 Ziel/450 hart; p99 18,17 ms ≤ 33,3 bei VSync; 0,9 B verwaltete Allokationen pro warmem Frame ≤ 1 KiB; RSS-Drift 6 MiB ≤ 16; 1 Draw ≤ 8; keine Shaderkompilierung zur Laufzeit | PASS |
| AC-T010-08 | ISA-Gate in `toolchain-check` (Negativfixture `-march=native` failt; `-msse4.2` als dokumentierte x86-64-v2-Basis zulässig); Mindestbasis mit CPU-Klasse i7-3770/FX-8350, GL-3.3-Pflicht ohne optionale Erweiterungen und lokalem Mesa-Stand in `PLATTFORMMATRIX.md` | PASS |

## Folgewirkungen auf T-005/T-006/T-007

Der Lockfile-Pin-Nachtrag änderte `toolchain.lock.json`-Bytes und damit die
gebundenen Hashes der Kalibrierungskette. Gemäß dokumentierter Regeneration:

- Manifest-Input-Hash von `CAL-STONEWOOD-V1` aktualisiert; Receipt-Kette per
  Pipeline-Regeneration (Job `01M0R8T010PJNREFRESHQ4V72W`) neu verankert.
  `family.glb`, `preview.png` und `technique.json` blieben **byteidentisch**
  (Determinismusnachweis über die Pin-Änderung hinweg). `assets-check` valid.
- CI-Evidenzschema/-test auf den neuen Lockfile-Gesamthash gesetzt;
  `artifacts/t007/dotnet-asset-calibration.json` wird nach diesem Checkpoint
  aus dem eingecheckten Baum per `scripts/dotnet-asset-calibration-ci.sh`
  regeneriert (Vertrag: Beweis ausschließlich aus archiviertem Commit).

## Prüfungen

- `lint` (Fantomas + `toolchain-check`): exit 0
- `build`: exit 0, 0 Warnungen / 0 Fehler
- `test`: 146/146 PASS (129 bestehende + 17 neue)
- `security`: PASS inklusive neuer Toolchain-/Lizenz-/ISA-Sektion
- `rag-build`: exit 0; `verify`: `"valid": true`, exit 0
- `plattformsmoke`/`effizienzbaseline`: nativ auf Referenzhardware, exit 0

## Bekannte Restpunkte

- Windows-/macOS-Builds, Smokes und Paketnachweise verbleiben bewusst bei
  T-011; Z-003/NF-006 bleibt unverändert.
- Konkrete Linux-Treiberminima entstehen erst aus T-011-Smokes
  (Q-TEC-002 bleibt `OFFEN`).
- bgfx meldet im OpenGL-Backend `vendorId/deviceId = 0`; der GPU-Nachweis im
  Report erfolgt über den `GL_RENDERER`-String.
- Mesa stellt einen 4.6-Core-Kontext bereit; der bgfx-Pfad ist am Pin auf die
  3.3-Core-Funktionsmenge fixiert (keine optionalen Erweiterungen nötig).
