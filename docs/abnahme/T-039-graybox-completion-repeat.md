# Abnahmedokument T-039 – Kleinster spielbarer Abschluss- und Wiederholungsschritt

**Auftrag:** `.ai/tasks/T-039-graybox-completion-repeat.json` (Status `ready` bei
Implementierungsbeginn)
**Vertrag:** `docs/ABSCHLUSSVERTRAG.md` V1 (gatender Abschnitt 0 vor der
Implementierung)
**Abhängigkeiten:** T-010, T-020, T-021, T-023, T-031, T-032, T-033, T-034,
T-035, T-036, T-037, T-038 — sämtlich abgenommen oder im formalen
Promotionspfad (`REVIEW`).

## 1. Gelieferter Umfang

Der gatende Abschnitt 0 wurde vor der Implementierung abgeschlossen:

- `docs/ABSCHLUSSVERTRAG.md` V1 legt die vier vertraglichen Fragen mit
  Alternativen, begründeter Empfehlung, messbarem Playtestkriterium und
  Rückrollweg fest: (a) Abschlussableitung als abgeleitete reine Funktion der
  bestehenden Schichtwahrheiten ohne neue persistenzpflichtige Abschlussbytes
  (`derived-completion-state-pure-function-v1`); (b) Wiederholen-Aktivierung
  als Skriptgrammatik `graybox-input-script-v4` (strikte v3-Obermenge,
  parameterloses `repeat`, kanonische Intentordnung 8) plus genau einer frei
  belegbaren Keymap-Aktion `repeat-mission` (Standard F7/Scancode 64) in der
  bestehenden Familie; (c) Reset-Umfang als vollständiger Kettenneustart
  einschließlich Aufsuchprotokoll
  (`full-chain-restart-including-visit-protocol-v1`); (d) Persistenzwahrheit
  als additive, versionierte Kettenlaufzählung in der bestehenden Sektion
  (`mission-chain-run-counter-persisted-v1`).
- Die autorisierten additiven Präzisierungen sind versioniert ausgewiesen:
  Erkundungsvertrag V3 (Abschnitt 12, `registration-uniqueness-per-chain-v3`),
  Entscheidungsvertrag V4 (Abschnitt 15,
  `chain-scoped-offer-and-cycle-truth-v4`), Druckvertrag V3 (Abschnitt 15,
  `chain-scoped-cycle-counting-v3`), Savevertrag V3 (Abschnitt 15,
  `mission-chain-run-section-fields-v3`, `legacy-section-v1-mission-emptiness-v3`)
  und Kommandovertrag Abschnitt 13 (Wiederholen-Aktion der Keymap-Familie
  nach T-033-Abschnitt-12-Präzedenz). Der Modevertrag bleibt byteidentisch.
- Implementierung: `MissionContract.cs`, `MissionSession.cs`,
  `GrayboxIntentKind.RepeatMission` samt Codec, v4-Grammatik im
  `InputScriptParser`, Pipeline-Ordnung (Intents, Erkundung, Entscheidung,
  Druck, Abschluss), Reset-Aufrufe (`ExplorationSession.RestartChain`,
  `DecisionSession.RestartCycle`, `PressureSession.RestartChain`),
  `--mission`-Aktivierung im `CommandLoopRunner`, Titel-HUD-Segment
  ` — Auftrag: abgeschlossen`, Keymap-Aktion, Sektionsversion 2
  (`MissionActive`, `MissionChainRunCount`) mit versionsgetreuer
  Legacy-v1-Kompatibilität, Report-Schemaversion 7 mit Pflichtblock
  `missionSession` und relationalen fail-closed Bindungen, Save-/Lade- und
  Interaktivpfad, Fixture
  `tests/fixtures/command/t039-completion-repeat.graybox`.

## 2. Kriterienevidenz

### AC-T039-01 — Abschnitt 0 vor der Implementierung

- Der Abschlussvertrag V1 liegt vor und trägt sämtliche maschinenlesbaren
  Kennungen; der Spiegeltest `T-039 mission contract mirrors documented
  values` hält Code und Vertragsdokumente (Abschluss-, Erkundungs-,
  Entscheidungs-, Druck-, Save-, Kommandovertrag) zeichentreu zusammen.
- Die Vertragsversionserhöhungen sind in den C#-Spiegelklassen gebunden
  (Erkundung `3`, Entscheidung `4`, Druck `3`, Save `3`) und wurden in den
  Bestandsspiegeltests regeneriert (Fixture-Regeneration nach T-037-Präzedenz).
- Keine Antwort auf eine offene Produktfrage: Q-GAM-001 bis Q-GAM-007,
  Q-GAM-010, Q-NAR-002, Q-NAR-004, Q-TEC-004, Q-TEC-006, Q-TEC-010, Q-OPS-001
  bleiben ausgewiesen offen (openQuestions-Block unverändert, ARCHITEKTUR- und
  AUTOMATION-Vermerke nennen die Grenzen).
- Keine Budgetänderung; keine Änderung an `Riftward.Simulation` (Blobvergleich,
  siehe AC-T039-03).

### AC-T039-02 — Headless Abschluss- und Wiederholungspfad

- Die Zwei-Ketten-Fixture spielt die Kette bis zum Zykluserfolg
  (Wahl 9200, Erfolg 9200), leitet den abgeleiteten Abschluss ab, wiederholt
  an Vorgrenze 9400 und durchläuft die neue Kette (Aufsuchfolge 4,2,1,5,3,0 —
  abweichend von Kette 1 mit 0,2,1,5,3,4) bis zum erneuten Erfolg (Wahl 16500,
  Erfolg 16500, Ankunft in der Folgenzone 0). Die Optionsableitung der neuen
  Kette ist (A=4, B=0) gegenüber Kette 1 (A=0, B=4) — Wiederholvarianz ohne
  Content.
- Zwei unabhängige Fresh-Prozesspaare sind builderidentisch (Seed 20260826:
  Endhash `fca90350abb6e99b`; Seed 7: Endhash `888c7c271341c83c`); je Paar
  sind Ketten und Missionsausweis byteidentisch, Exitcode 0, Gate pass.
- Ein fremder Seed (7) ändert Start- und Endhash nachweislich; die
  Strukturinvarianten der Abschlusswahrheit (Abschlusszustand, Kettenlauf-
  zählung, Dispositionsfolge des Wiederholungsprotokolls) bleiben, die
  Vorgrenzen folgen der Sitzung (T-036-Fremdseed-Präzedenz).
- Eine Wiederholung vor dem Abschluss wird mit der unterscheidbaren Klasse
  `rejected-before-completion` abgewiesen und verändert nachweislich nichts
  (Kette, Endhash, Erkundungswahrheit, Kettenlaufzählung byteidentisch zum
  Zwilling; Tests `repeat before completion …` und `repeat without activation …`).
- `repeat` unter v1-/v2-/v3-Köpfen ist `UnknownAction` mit bestehender
  Bedeutung; die v4-Obermenge hält die Legacy-Verbmenge unter ihren Köpfen
  gültig (Test `repeat under legacy headers is unknown action`).
- Report: Schemaversion 7 mit Pflichtblock `missionSession`
  (Abschlussgrenze/-zustand, Kettenlaufzählung, Wiederholungsprotokoll je
  Kettenlauf), sämtliche neuen Felder `gateCoupled=false`, relational
  fail-closed gebunden (Abschluss nur nach dem Zykluserfolg der Schichten,
  Zählkonsistenz, Abweisungszählergleichheit; Tests `mission schema relations
  reject fabrication fail closed` und Schema-Dispatch der Bestandssuite).
- Keine neuen Exitcodebedeutungen; `--mission` ohne `--pressure` ist Usage (2).

### AC-T039-03 — Vertrauensgrenzen und Beobachtungstreue

- Abschlusszustand und Wiederholung erzeugen niemals einen Kernbefehl und sind
  niemals Teil von Simulationszustand oder Hash: Der Zwei-Ketten-Lauf mit
  Missionsaktivierung ist gegenüber dem Zwilling ohne Aktivierung
  byteidentisch in Kettenstichproben und Endhash; `KernelCommandsTotal` ist
  identisch (Test `mission layer never touches simulation or hash`).
- Registrierungseindeutigkeit je Kette: nach dem definierten Reset registriert
  jede Landmarke genau einmal je Kette (12 Besuche über beide Ketten, je Kette
  sechs verschiedene Zonen; Test `two chain flow …`).
- Legacy-Erkundungs-, Entscheidungs-, Druck- und Fortsetzungsfixtures bleiben
  mit identischen Ketten und Endhashs gültig; die bestehende Suite (T-034 bis
  T-038) läuft unverändert grün, und der T-036-Referenzfluss bleibt mit
  Missionsaktivierung kettenidentisch (Test `legacy pressure fixture stays
  chain identical with mission`).
- Sektionsversion 2 trägt die additive Missionsfläche; die Legacy-Sektions-
  version 1 lädt mit ehrlicher Missionsleere, Re-Encoding ist versionsgetreu,
  die Relationswahrheiten (Aktivierung/Zählung/Druckkopplung) weisen
  Fabrikationen fail-closed ab, und Sektionsversion 3 wird ohne
  Migrationserfindung abgewiesen (Test `session section v2 carries mission
  fields and legacy v1 stays empty`). Die T-031-Prüfklassen gelten
  uneingeschränkt (Bestandssuite).
- Slotdateien bleiben untrusted; die Aktivierungsgrenze
  `layer-activation-mismatch` umfasst die Missionsaktivierung (Save- und
  Interaktivpfad). Kein Netzwerkzugriff, keine Secrets; Schreibzugriffe
  beschränken sich auf Reportpfad, Slotverzeichnis und opt-in Abgriff. Der
  Sitzungskern bleibt frei von SDL3-/bgfx-/Betriebssystemtypen
  (Bestands-Architekturtest deckt die neuen Dateien mit ab).

### AC-T039-04 — Interaktiv spielbarer Pfad

- Derselbe Pipelinepfad bedient den fensterpflichtigen Modus; der
  Abschlusszustand ist über den additiven Titel-HUD-Abschnitt
  ` — Auftrag: abgeschlossen` in fester Form in beiden Modi ohne Tastendruck
  lesbar (NF-005-Zweikanal über Text plus bestehende Kanäle, nie reine
  Farbcodierung); nach dem Kettenneustart weist der Titel die neue Kette
  (Erkundung 0/6, kein Angebot, kein Fenster) aus, weil die bestehenden
  Segmente die zurückgesetzte Kettenwahrheit wahrheitsgetreu tragen. Die
  Wiederholen-Aktion ist über `repeat-mission` (Standard F7) erreichbar und
  kontextfalsche Impulse erhalten die sichtbare UF-001-Fehlerzeile
  `mission-repeat-before-completion` ohne Welt-, Ketten- oder Kernänderung.
- Abschluss- und Kettenlaufwahrheit sind über die bestehenden Save-/Lade-
  Aktionen fortsetzbar; der headless Save-/Lade-Rundtrip (Speichern in Kette 2
  an 9600, frischer Prozess, Laden) bestätigt Fortsetzungsidentität
  (`chainContinuity.verified=true`), restaurierte Kettenlaufzählung 2 und den
  fortgesetzten Abschlusszustand (Schemaversion 7, Sektionsversion 2; Test
  `CLI mission flow runs schema 7 with save load roundtrip`). Eine abgewiesene
  Ladung ändert sichtbar nichts (Bestandsprüfungen).
- **Ausgewiesener Restpunkt (displaylose Umgebung):** Interaktivsmoke und
  die Ausführung des vorregistrierten Playtestprotokolls bleiben der
  Displaysession vorbehalten; ohne nutzbares Display ist der kontrollierte
  Code-19-Abbruch dokumentiert (Präzedenz T-023 bis T-038), und die
  darstellseitigen Ausweise werden headless ausdrücklich nicht gemessen
  (`headless-run-without-window`) statt still behauptet. Es wurde kein Abgriff
  produziert; ein Media-Lab-Eintrag entsteht erst mit einem tatsächlichen
  opt-in Abgriff.

### AC-T039-05 — Gates und Regressionen

- `./scripts/rift.sh build/fmt/lint/test/security/verify` laufen mit dem neuen
  Code grün; null neue Compiler-/Analyzer-Warnungen (`TreatWarningsAsErrors`
  aktiv), keine neue Abhängigkeit (BCL-only, keine csproj-Referenzänderung).
- Die Regressionsläufe der Bestandsbefehle (bench-sim, savecheck, Soak-Kurzlauf
  sowie kommandoschleife mit Legacy-v1-/v2-/v3-Skripten und den Erkundungs-,
  Entscheidungs-, Druck- und Fortsetzungsskripten) bleiben grün; die
  Schemaliste der Schema-Dispatch-Tests wurde um die legitime Version 7
  erweitert (Fabrikationsprüfung auf Version 8 verschoben, T-037-Präzedenz).
- AUTOMATION.md (Skriptstufe v4, Keymap-Aktion, Aktivierung, Reportfelder) und
  ARCHITEKTUR.md (Abschluss-/Wiederholungsgrenze der Sitzungsschicht) bilden
  den implementierten Stand ab.
- Die `--mission`-Kette der T-039-Suite bindet: Vertrags- und Modellspiegel,
  Ableitungsmatrix, Abweisungsmatrix, Legacy-Grammatik, Zwei-Ketten-Flow,
  Beobachtungstreue, Fremdseedstruktur, Legacy-Kettenidentität, Sektions-
  version 2 mit Legacy-v1-Leere, CLI-Rundtrip mit Fortsetzungsidentität,
  Kopplung/Exitcodes und die relationalen Schemafabrikationen.

## 3. In-Scope-Reparatur eines Bestandsdefekts

Während der Schemaarbeit zeigte sich, dass die Schemaversion-6-Bodyform die
Sitzungsblöcke der Schichten über `OptionalFieldNode` verbaut, aber der
geschlossene `RObj`-Knoten fehlende Felder stets als Pflichtfeld meldete — ein
Save-/Ladelauf ohne jede Schichtaktivierung wäre an der Schemalinie
gescheitert, obwohl Savevertrag V2 Abschnitt 13.8 ausdrücklich festlegt:
„Schemaversion 6 erzwingt keine Schichtaktivierung und keine Schichtblockpflicht".
Reparatur: `RObj.WithOptionalFields` macht benannte Felder tatsächlich
optional; die Schemaversion-6-Form nutzt sie für die drei Schichtblöcke, die
Schemaversion-7-Form für den optionalen Fortsetzungsblock. Die Missionsfläche
selbst ist Pflicht (vertragliche Kopplung). Die Bestandssuite bindet die
Reparatur über die vorhandenen Fortsetzungs-/Drucktests; ein eigener Suiteeintrag
prüft zusätzlich, dass ein Lauf ohne Schichtaktivierung die Schemaversion 6
erfüllt (regression: `incomplete report preserves pressure activation`-Familie
und savecheck-Legacyflow).

## 4. Restpunkte und Grenzen

- Interaktivsmoke, Playtestausführung und ein eventuelles opt-in
  Abgriffpaar bleiben der Displaysession vorbehalten (kontrollierter
  Code-19-Nachweis in der displaylosen Umgebung; die darstellseitigen
  Missionsausweise werden headless ehrlich als nicht gemessen ausgewiesen).
- Pflichtprofile bleiben `NOT-MEASURED` (Q-OPS-001); dieser Slice erzeugt
  keinen neuen budgettragenden Pfad und ändert keinen Grenzwert.
- Q-TEC-006 (Replay-/Cooked-/Definitionsformat), Q-GAM-001 bis Q-GAM-007,
  Q-GAM-010, Q-NAR-002, Q-NAR-004, Q-TEC-004, Q-TEC-010 und die verschobenen
  unabhängigen Reparaturen der T-032-Linie bleiben unverändert offen.
- Die Out-of-Session-Semantik (Hauptmenü, Neues Spiel, Wiederholen außerhalb
  der laufenden Sitzung) bleibt ausdrücklich zurückgestellt (UF-001-Schritt-9-
  Hauptmenüanteil); dieser Slice liefert ausschließlich den sitzungslokalen
  Kettenneustart.
