# Akzeptierte Spezifikationsfreigabe T-032

- Reviewlauf: `01M0Y4W40T5ZPESPWN2H0XH1N2` (Akteur `t032-spec-reviewer`)
- Aufgabe: `T-032`
- Status: Spezifikation `READY` bestätigt; Implementierung nicht begonnen
- Ausgangscommit: `5dc790b` (accept t-031 verified checkpoint after independent promotion …)
- Ergebniscommit: der unmittelbar folgende Checkpoint-Commit „accept t-032 ready specification …“ enthält diesen Bericht
- finaler Eventhash: `04c4941abe6478ae9bee7bd5a64c46bc9db3c7ee0517050226f0dcb6c37f2ff1`
- Summaryhash: `e0ce0a69fba0a77b6d9526f1abba5f562c2365e750776f65544f0c439e387171`

## Ergebnis

Die am 2026-08-26 vom autonomen Planungsagenten (Autorisierung der
Projektleitung vom 2026-08-23) erstellte T-032-Spezifikation wird als
umsetzbar bestätigt: Das Task-Manifest
`.ai/tasks/T-032-graybox-command-loop.json` ist gegen
`.ai/schemas/task.schema.json` mit dem gepinnten JsonSchema.Net 8.0.5 gültig
(15/15 Manifeste unter `.ai/tasks/`, im Reviewlauf per `dotnet fsi` gegen die
Release-DLLs von `RiftHarness` nachgeprüft). Alle fachlichen Anker wurden
eigenständig gegen Code und Verträge belegt:

- `SimCommandKind.GroupMoveToZone` existiert (`src/Riftward.Simulation/
  SimCommand.cs`, Zeile 7) samt kanonischer Ordnung (Tick, ScopeGroup, Kind,
  ZoneIndex) im Strukturvertrag; keine neue Befehlsart ist erforderlich.
- Die Vertragswelt `riftward-simworld-graybox-v1` ist gebunden
  (`SimulationContract.WorldId`, Zeile 20); `NavWorld.ZoneCount = 6`
  (Zeile 30) entspricht der Sechs-Zonen-Aussage des Manifests.
- Die Budgetzeile Eingabe-zu-Reaktion steht zeichengleich in
  `docs/PERFORMANCE_BUDGET.md` (100 ms Ziel / 150 ms hart); die
  Tickableitung bei vertraglichen 20 Hz bleibt Abschnitt-0-Gegenstand mit
  Verschärfungsoption und Eskalationspflicht bei Lockerung.
- Die Exitcode-Registry in `docs/NATIVE_UNTERBAU.md` endet bei 34; die
  geplanten Codes 35–38 sind frei und die wiederverwendeten Codes 19/27/28
  behalten ihre dokumentierten Bedeutungen.
- Die Abhängigkeiten T-010/T-020/T-021/T-023 sind laut Backlog durchweg
  `DONE`; `requiredGates` folgen dem Präzedenz der Spezifikationsfreigaben
  T-020 bis T-031.

Die Auswahlkette über alle DRAFT-Einträge (T-011 blockiert an Q-OPS-002/
Q-OPS-003 und physischer Zielhardware; T-008 ist `REVIEW`; T-040/T-041 hinter
E-004/E-005; T-050/T-051 hinter Q-AST-001/Q-AST-002 und E-005/E-006), die
dokumentiert rückrollbare T-030-Zerlegung (Klärungsprotokoll 2026-08-26 in
`docs/OFFENE_FRAGEN.md`) und die Offenhaltung von Q-GAM-001 bis Q-GAM-007
sowie Q-NAR-002 sind konsistent zu `BACKLOG.md`, `OFFENE_FRAGEN.md`, ADR 007,
der DoR-Schnittregel („Eine große Story wird so geschnitten …“) und der
Spike-Klausel in `QUALITAET.md`. GS-007 ist als GAME_DESIGN-Pfeiler existent;
die minimale Verbmenge bleibt vorregistrierte Testhypothese im gatenden
Abschnitt 0. Media-Lab-Bindung: Der opt-in Einzelabgriff folgt dem
T-023-Muster mit Aussagegrenze Graybox-Zustandsbelegung und bleibt lokal,
hashgebunden und nie Gameplay-, Atmosphären- oder Shipping-Beleg.
Clean-Room- und Secret-Scan ohne Befund (keine Fremdtitel, keine Drittmedien,
keine lokalen Pfade; drei „Secrets“-Treffer sind Verbotsformulierungen).

## In-Scope-Reparaturen des Reviewlaufs

Beide Reparaturen liegen ausschließlich im neuen Material dieses Primärslice
(neues Manifest plus sein eigener BACKLOG-Freigabeabsatz); kein zweites
bereits akzeptiertes Task-Manifest wurde angetastet:

- Die Behauptung, ein begrenztes visuelles Evidenzartefakt sei „hier erstmals
  angezeigt“, war historisch falsch — T-023 lieferte bereits einen opt-in
  Einzelabgriff mit Aussagegrenze Graybox-Lastbelegung. Formuliert als
  ausdrückliche T-023-Präzedenzverweise in `releaseNote` und BACKLOG-Absatz.
- Doppeltippfehler „ist als als Textreport“ → „ist als Textreport“ im
  `releaseNote`.

Eine davon unabhängige zweite Reparatur wurde nicht vorgenommen und ist auch
nicht erforderlich. Test-, Fixture-, Build-, CI- oder Evidenzpfade wurden
nicht berührt; der Fresh-Checkout-/Clean-Archive-Vertrag ist durch diesen
Kandidaten nicht ausgelöst (gitignorierte Runtime-Evidenz ist ohnehin nie
Test-Fixture).

## Prüfungen

- Task-Schema: 15/15 Manifeste unter `.ai/tasks/` gültig (nach Reparatur)
- Retrievaltrace: vier kriteriumsgebundene RAG-Abfragen zum Lauf gebunden
  (AC-T032-01/02/05/08), Treffer mit Pfad, Zeilen und Quellhash in der
  Retrievalkette des Laufs
- `fmt`/`lint`: Exit 0 (fantomas --check PASS, toolchain-check findings = 0)
- `build`: Exit 0 mit 0 Warnungen / 0 Fehlern (Release)
- Tests: 218/218 PASS
- `security`: PASS (Baseline-Gate)
- `rag-build`: Exit 0 (chunkCount 1095)
- `verify`: abschließende Runde nach der letzten inhaltlichen Änderung über
  alle Runs einschließlich dieses Laufs

## Verbleibende Risiken

- Die Implementierung von T-032 steht vollständig aus; Status bleibt `ready`.
- AC-T032-06 verlangt einen fensterpflichtigen Interaktivsmoke; in dieser
  kopflosen Sitzung nicht ausführbar und bewusst Teil des Umsetzungsauftrags
  mit kontrolliertem Abbruch statt Simulation (Code 19).
- Pflichtprofile bleiben `NOT-MEASURED`, bis die Projektleitung Referenzrechner
  benennt (Q-OPS-001 bleibt `OFFEN`).
- Alle Gates wurden nur unter Linux-x64 ausgeführt; Windows-/macOS-Nachweise
  bleiben unverändert an T-011 verwiesen.
