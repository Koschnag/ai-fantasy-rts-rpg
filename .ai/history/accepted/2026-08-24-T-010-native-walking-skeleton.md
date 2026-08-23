# Akzeptierte T-010-Lieferung: nativer linux-x64 Walking-Skeleton

- Reviewlauf: `01M0QYAA11MC89GVMP6BWR7016` (Akteur `t010-review-completion`)
- Aufgabe: `T-010`
- Status: `DONE` (Task-Manifest `accepted`); E-002 beginnt
- Ausgangscommit: `9637ec8627bfacbf598bdecf8b77965bdf556655`
- Ergebniscommit: der unmittelbar folgende Checkpoint-Commit „T-010 …" enthält diesen Bericht
- finaler Eventhash: `1cbf07b87565d109687154e9ad6ac7eac177736ba6cc4f8d0a9596c7db7ea4d2`
- Summaryhash: `7c020883437b5c66d432088ec82b97597ced4811c39e1271a28dc04d2058b7ed`

## Ergebnis

Der unabhängige Review-/Vollendungslauf hat die von zwei abgebrochenen
Implementierungsläufen begonnene T-010-Arbeit geprüft, In-Scope-Defekte
repariert und vollendet. Der Client öffnet ein SDL3-Fenster, initialisiert
den bgfx-Pflichtpfad OpenGL 3.3 Core ohne Fallback, rendert das technische
Testdreieck aus offline kompilierten Shadern und beendet sich kontrolliert;
alle nativen Komponenten sind commit-/taggepinnt, lizenziert inventarisiert,
nativ gebaut und per ABI-Smoke auf dem Entwickler-PC (i7-3770/RX 570,
Mesa 26.0.3-1ubuntu1) nachgewiesen. Effizienzbaseline innerhalb aller harten
Budgets. Details und AC-Evidenz:
`docs/abnahme/T-010-native-walking-skeleton.md`.

Wesentliche Reparaturen am vorgefundenen Stand: bgfx-release64-Ausgabepfade,
x86-64-v2/PIC-Buildflags, Shim-Link inklusive bimg/GL-Laufzeitbindung,
SDL3-X11-Laufzeitbindung, vollständige Shader-Semantikdefinition,
`SOURCE_DATE_EPOCH` für byteidentische Neubauten, kein Manifest-Neuschreiben
im Verify-Modus, workspace-relative Pfadauflösung im Artefaktvalidator.

Neu vollendet: C#-Interop (`Riftward.Platform`, LibraryImport, Besitzregeln,
kontrollierte Fehlerobjekte/Exitcodes 14–24), Host (`Riftward.App`) mit
öffentlichen Befehlen `plattformsmoke`/`effizienzbaseline`,
Toolchain-/Lizenz-/ISA-Gate (`toolchain-check`) in `lint`+`security`, 17 neue
Tests (Fault-Injection je Fehlerklasse, No-write, Exitcode-Stabilität,
Architekturtest), Doku (`NATIVE_UNTERBAU.md`, Mindestbasis in
`PLATTFORMMATRIX.md`, Befehlsvertrag).

Folgewirkung: Die Pin-Nachtrag-Bytes erforderten die dokumentierte
Regeneration der CAL-STONEWOOD-Provenienzkette (Manifest-Input-Hash,
Receipt-Kette, CI-Evidenzschema-Anker); generierte Assets blieben byteidentisch.
`artifacts/t007` wird anschließend aus dem eingecheckten Baum regeneriert.

## Prüfungen

- Task-Schema: 8/8 Manifeste gültig; T-010-Status `accepted`
- `lint` exit 0 (Fantomas + Toolchain-/Lizenz-/ISA-Prüfung)
- `build` exit 0 mit 0 Warnungen / 0 Fehlern
- Tests: 146/146 PASS (129 bestehende + 17 neue)
- `security`: PASS (Baseline + Native-Pins/Lizenzen/Kohorte/ISA)
- `rag-build` exit 0; `verify` `"valid": true`
- `native-build --fresh`: byteidentischer Wiederholbau; `--verify-cache` exit 0
- `plattformsmoke` exit 0; `effizienzbaseline` exit 0 (alle Budgets ein)

## Verbleibende Risiken

- Windows-/macOS-Builds, Smokes und Paketnachweise verbleiben bei T-011;
  die Ziel-RID-Matrix Z-003/NF-006 bleibt unverändert.
- Konkrete Treiberminima entstehen aus den T-011-Smokes (Q-TEC-002 `OFFEN`).
- bgfx liefert im GL-Backend keine PCI-IDs (vendorId/deviceId = 0);
  GPU-Evidenz über `GL_RENDERER`.
- Mesa bereitete einen 4.6-Core-Kontext; der Pflichtpfad ist am Pin auf die
  GL-3.3-Core-Funktionsmenge fixiert.
