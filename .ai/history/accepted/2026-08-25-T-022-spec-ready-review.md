# Akzeptierte Spezifikationsfreigabe T-022

- Reviewlauf: `01M0VCJZ0KRSA2Y1ZSMRWSV2RW` (Akteur `t022-spec-reviewer`)
- Aufgabe: `T-022`
- Status: Spezifikation `READY` bestätigt; Implementierung nicht begonnen
- Ausgangscommit: `f40ac22` (add t023 graybox shader sources to representative load frame …)
- Ergebniscommit: der unmittelbar folgende Checkpoint-Commit „T-022 …“ enthält diesen Bericht
- finaler Eventhash: `2d210d030f4e3f49e0b42016f0efcfff3e654a97f4f1f4d7db13c2d0c7b11222`
- Summaryhash: `84de3c2ff33d921803f997ee196e5dbfbdb8fac16850543c9c211dfc4f20bd5e`

## Ergebnis

Die am 2026-08-25 vom autonomen Planungsagenten (Autorisierung der
Projektleitung vom 2026-08-23) erstellte T-022-Spezifikation wird als
umsetzbar bestätigt: Das Task-Manifest
`.ai/tasks/T-022-deterministic-replay-soak.json` ist gegen
`.ai/schemas/task.schema.json` gültig (13/13 Manifeste unter `.ai/tasks/`,
geprüft mit einem deterministischen Offline-Validator über die Teilmenge
required/additionalProperties:false/enum/pattern/minLength/minItems/
uniqueItems plus `jq empty`). Die gatenden Abschnitt-0-Kriterien sind
vollständig fixiert: doppelter Leak-Schwellwert (absolut 4–64 MiB gekapselt,
fensterbasiertes Trendkriterium, Konsistenzbedingung Trendschwelle × 8 h ≤
absolute Schwelle), Fortschritts-Watchdog mit 30–300-s-Fenstern und
Ausführungsvertrag (autoritativ: genau ein zusammenhängender Realzeitprozess
≥ 8 h Wanduhr bei 20 Hz; beschleunigt ausschließlich diagnostisch),
abgeleitet aus mindestens zwei Kalibrierläufen von je mindestens 30 Minuten
als Vielfaches des beobachteten Messrauschens.

Szenario-/Gateanker stimmen zeichengleich mit den Quellen überein: NF-002
(„genaue Schwelle im Spike“, `ANFORDERUNGEN.md`), Spike-Klausel in
`QUALITAET.md` (Leak-Schwellwert bleibt bis zum Baseline-Spike `OFFEN`),
`PERFORMANCE_BUDGET.md` (Budgetlinien dienen ausschließlich als obere
Grenzen; kein Budgetwert wird geändert oder berührt), `SIMULATIONSVERTRAG.md`
V1 (genau 250 vollständig simulierte mobile Agenten, fester 20-Hz-Tick,
`fnv1a64-canonical-chain-v1`, Befehlsplan `xorshift64star-group-script-v1`,
Allokationsgrenze 0 Bytes je warmem Tick laut Abschnitt 5) sowie dem
Exitcodevertrag in `NATIVE_UNTERBAU.md` (25/26/27/28 unverändert; neue
Soak-Codes werden dokumentiert und getestet).

Die Auswahlkette über alle DRAFT-Einträge und die Blockerbehandlung sind
konsistent zu `BACKLOG.md` und `docs/OFFENE_FRAGEN.md`: `T-011` bleibt an
Q-OPS-002/Q-OPS-003 plus fehlender Zielhardware blockiert; `T-030`/`T-031`
an echten Produktentscheidungen (Q-GAM-001 bis Q-GAM-007, Q-NAR-002,
Q-TEC-006); `T-040`/`T-041` liegen hinter E-004/E-005; `T-050`/`T-051`
hinter Q-AST-001/Q-AST-002; `T-008` ist `REVIEW`. Die tolerierte
Benchmarkstreuung (Rest von Q-TEC-010) wird weder definiert noch verbraucht:
sämtliche Soak-Gates binden sich an absolute Grenzwerte, die fensterweise
Tickzeitdrift bleibt gatefrei diagnostisch (Klärungsprotokoll 2026-08-25,
rückrollbar); Q-TEC-004 gilt verfahrensmäßig behandelt (Vertrag V1 unverändert
wiederverwendet, Ratifizierung bleibt `OFFEN`). Ältere, datierte Stellen, die
T-022 für blockiert erklären (Freigabe-/Abnahmevermerke von 2026-08-24),
sind historische Aufzeichnungen und werden gemäß Append-only-Konvent nicht
umschrieben; die lebenden Quellen widersprechen sich nicht.

Clean-Room-Scan ohne Befund: keine fremden Spieltitel, Stilvorgaben,
Drittmedien, Quarantäne-Binärdaten oder Secrets im Diff; Media-Lab-Prüfung
plausibel (headless Zuverlässigkeitslauf ohne sichtbaren Szenengehalt,
Telemetrie als Evidenz, Kurvenvisualisierung bleibt MEDIA-05).

## In-Scope-Reparaturen des Reviewlaufs

- `.ai/tasks/T-022-deterministic-replay-soak.json`: Freigabevermerk-Tippfehler
  korrigiert („gemuess“ → „gemaess“) und kasusfehlerhafte MEDIA-05-Formulierung
  („bleibt der MEDIA-05-Prozess“ → „bleibt dem MEDIA-05-Prozess vorbehalten“)
  repariert; datierter Reviewvermerk ergänzt.
- `BACKLOG.md`: Reviewabsatz mit Prüfevidenz, Reparatur und unverändertem
  Taskstatus `ready` ergänzt.

## Prüfungen

- Task-Schema: 13/13 Manifeste unter `.ai/tasks/` gültig; JSON-Syntax via `jq empty`
- Retrievaltrace: vier kriteriumsgebundene RAG-Abfragen zum Lauf gebunden
  (AC-T022-01/06/07/11) gegen den frisch gebauten Index, Treffer mit Pfad,
  Zeilen und Quellhash in der Retrievalkette
- `lint` Exit 0 (fantomas --check PASS; toolchain-check findings = 0)
- `build` Exit 0 mit 0 Warnungen / 0 Fehlern (Release)
- Tests: 184/184 PASS
- `security`: PASS (Baseline-Gate, findings = 0)
- `rag-build`: Exit 0, 194 Quellen, 899 Chunks,
  Indexhash `97fabc3dd77a0df156758c0f3fb6b345962e9eff411524fcbcc400193359d1d5`
- `verify`: Exit 0 (`"valid": true`) nach Runabschluss über alle Runs

## Umgebung

Das .NET SDK 10.0.110 wurde gemäß gepinntem Bootstrap
(`scripts/bootstrap-dotnet.sh`, SHA-512 `05e5a22c…` geprüft) beschafft; das
Home-Verzeichnis der Sitzung ist schreibgeschützt, daher installierte der
Lauf nach `artifacts/dotnet-sdk` (gitignoriert). Die GNU-tar-Extraktion des
SDK-Tarballs scheiterte in dieser Sandbox reproduzierbar/nichtdeterministisch
mit ENOSYS an wechselnden Einträgen; sie wurde durch Python-tarfile ersetzt
(5803/5803 Einträge, 0 Fehler, `dotnet --version` = 10.0.110). Dies betrifft
nur die lokale SDK-Beschaffung, keine Projektdateien.

## Verbleibende Risiken

- Die Implementierung von T-022 steht vollständig aus; Status bleibt `ready`.
  Insbesondere sind Soakvertrag (`docs/SOAKVERTRAG.md`), soak-Befehl,
  Gate-Evaluator, Golden-Fixture und der autoritative 8-h-Lauf Gegenstand des
  Implementierungsauftrags.
- Pflichtprofile `HW-PC-MIN`/`HW-MAC-MIN`/`HW-PC-HIGH` bleiben
  `NOT-MEASURED`, bis die Projektleitung Referenzrechner benennt
  (Q-OPS-001 bleibt `OFFEN`).
- Die Ratifizierung des Simulationsvertrags V1 bleibt über Q-TEC-004 `OFFEN`.
- Die tolerierte Benchmarkstreuung (Q-TEC-010) bleibt unentschieden und darf
  durch diesen Auftrag nicht präjudiziert werden.
