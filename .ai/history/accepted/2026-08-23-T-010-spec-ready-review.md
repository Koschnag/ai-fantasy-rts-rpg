# Akzeptierte Spezifikationsfreigabe T-010

- Reviewlauf: `01M0QQYJDX9CS56144Z7VGN8J4` (Akteur `t010-spec-reviewer`)
- Aufgabe: `T-010`
- Status: Spezifikation `READY` bestätigt; Implementierung nicht begonnen
- Ausgangscommit: `7a0658ed67e75ef981edb77f7f6e8a462fcad751`
- Ergebniscommit: der unmittelbar folgende Checkpoint-Commit „T-010 …“ enthält diesen Bericht
- finaler Eventhash: `e17899f153980015e6215846603596ae1d291d04f44647ce10c63f25a55dbfbf`
- Summaryhash: `2de35c2128b4eb4a1ab59373e7601864f192cc71b3b8759a88722bb72e150ae5`

## Ergebnis

Die am 2026-08-23 vom autonomen Planungsagenten (Autorisierung der
Projektleitung vom 2026-08-23) erstellte T-010-Spezifikation wird als
umsetzbar bestätigt: Das Task-Manifest ist gegen
`.ai/schemas/task.schema.json` gültig, widerspruchsfrei zu ADR-002,
`PLATTFORMMATRIX.md`, `PERFORMANCE_BUDGET.md`, `QUALITAET.md` (Spike-Klausel)
und dem Klärungsprotokoll in `docs/OFFENE_FRAGEN.md`; Q-TEC-001/Q-TEC-003
sind verfahrensmäßig in den gatenden Pin-Bauabschnitt überführt. Keine fremden
Spieltitel, Stilvorgaben oder Drittmedien im Auftrag; nur FOSS-Bibliothekspins
(SDL3/bgfx/bx/bimg) gemäß ADR-002.

In-Scope-Reparaturen des Reviewlaufs:

- `BACKLOG.md`: Ergebniszeile von T-010 nennt linux-x64 zuerst und weist
  Windows/macOS an T-011 (bisher Widerspruch zum `outOfScope` des Auftrags);
  Restrisikosatz korrigiert (AC-T010-02/03 erfordern linux-x64-Nachweise auf
  dem Entwickler-PC, nicht win-x64/osx-arm64); E-001-Aussage an den
  Epic-Status `IN ARBEIT` angeglichen.
- `.ai/schemas/task.schema.json`: optionale Felder `releaseNote`/
  `reviewNote` aufgenommen. Ohne diese Vertragsreparatur war das abgenommene
  Manifest `T-004` durch den Notiznachtrag in Commit `7a0658e` gegen
  `additionalProperties: false` ungültig (8/8 Manifeste validieren jetzt).
- `.ai/tasks/T-010-native-walking-skeleton.json`: Freigabe-/Reviewvermerke
  ergänzt; `.ai/tasks/README.md`: Feldkonvention dokumentiert.
- `.ai/runtime/work/t010-review/validate-task.fsx`: defekter Scratch-Validator
  ersatzlos durch eine offline laufende, formatierte Variante ersetzt (er
  blockierte G-FORMAT für jeden künftigen Lauf).

## Prüfungen

- Task-Schema: 8/8 Manifeste unter `.ai/tasks/` gültig
- `fmt` Exit 0; `lint` Exit 0
- `build` Exit 0 mit 0 Warnungen / 0 Fehlern
- Tests: 129/129 PASS
- `security`: PASS (Baseline-Gate)
- `rag-build`: Exit 0, deterministisches Buildmanifest gebunden
- `verify`: Exit 0, `"valid": true` über alle 27 Runs inklusive dieses Laufs
- Retrievaltrace: RAG-Abfragen zum Lauf gebunden, Treffer mit Pfad/Zeilen/Hash

## Verbleibende Risiken

- Die Implementierung von T-010 steht vollständig aus; Status bleibt `ready`.
- AC-T010-02/03 benötigen echte linux-x64-Referenzhardware
  (i7-3770/RX 570); fehlt sie, wird eskaliert statt Cross-Compile oder
  Simulation zu setzen.
- Windows-/macOS-Builds, Smokes und Paketnachweise verbleiben bei T-011;
  die konkreten Pinwerte bleiben Inhalt von Q-TEC-001/Q-TEC-003 (`OFFEN`).
- Alle Gates wurden nur unter Linux-x64 ausgeführt.
