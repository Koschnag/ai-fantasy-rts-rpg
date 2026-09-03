# Forschungsprotokoll

**Protokoll-ID:** `riftward-research-observability`

**Version:** 2.0.1

**Stand:** 2026-09-03

**Status:** praeregistriert fuer T-053; noch kein prospektives Ereignis erhoben

Version 2.0.1 ist das vor P-001 beschlossene Integrity-/Privacy-Amendment zu
2.0.0; 2.0.0 ersetzte den explorativen Messplan 0.1 vom 2026-08-23. Die
inhaltliche Aenderungshistorie steht im
[Protokoll-Changelog](PROTOCOL_CHANGELOG.md). Der Hash der eingefrorenen
Protokollfassung wird vor dem ersten prospektiven Ereignis nach dem Verfahren
im [Reproduzierbarkeitsleitfaden](REPRODUCIBILITY.md) gebildet und im
Beobachtungsmanifest gebunden.

## Untersuchungsgegenstand und Aussagegrenze

Untersuchungseinheit ist eine vollstaendige, versionierte Umsetzungseinheit
von einem freigegebenen Arbeitsauftrag bis zu einem akzeptierten, verworfenen,
blockierten oder abgebrochenen Ergebnis. Modellantworten, Agentenlaeufe,
Commits oder Prozesslaufzeit allein sind keine Outcomes. Spezifikation,
Retrieval, Werkzeuge, Aenderungen, Tests, Reviews, Gates und Ergebnis werden
nur dann verbunden, wenn ihre Quellbelege aufloesbar sind.

T-053 ist ein unabhaengiger Beobachtungspfad. Er veraendert weder den
beobachteten Produktauftrag noch dessen Gates, Freigaben, Modellrouting,
Writer-Leases oder Promotion. Insbesondere bleiben die Garantien von T-037
unangetastet. T-042 ist der vorregistrierte erste prospektive Zielauftrag,
aber nur, wenn sein eigenes Taskmanifest am Beobachtungs-Baseline-Commit
vorhanden und gemaess seinem normalen Lebenszyklus startberechtigt ist. T-053
setzt T-042 nicht auf Erfolg und darf dessen Status nicht veraendern.

## Evidenzklassen

Jeder Datensatz traegt genau eine der folgenden Klassen:

| Klasse | Zulaessige Quelle | Zulaessige Aussage |
|---|---|---|
| `retrospective-derived` | vor Protokoll-Freeze entstandene, unveraenderliche Git-Objekte und vorhandene hashgebundene Evidenz | deskriptive Rekonstruktion; keine Behauptung damaliger Vollstaendigkeit und keine Kausalitaet |
| `prospective-observed` | nach Protokoll-Freeze und vor Zielstart aktivierter Collector mit hashgebundenen Primaerereignissen | vorregistrierte deskriptive Metriken und, nur bei passendem Vergleich, die vorregistrierte Auswertung |
| `synthetic-test-only` | explizit kuenstliche Fixtures und absichtlich veraenderte Kopien | Validator-, Export- und Ablationstests; niemals reale Projektleistung |

Die Klassen werden nie zusammengelegt, um eine scheinbar groessere
Stichprobe zu erzeugen. `unknown` ist der einzige Wert fuer nicht beobachtete
oder nicht sicher ableitbare Angaben. `null`, ein Schaetzwert als Ersatz,
stilles Imputieren und die Umdeutung eines fehlenden Signals zu Erfolg sind
verboten. Separat als `estimated` provenienzierte Kosten bleiben eine eigene,
nicht exakte Metrik und ersetzen niemals `unknown`.
Die genaue Feldkonvention steht im
[Datenwoerterbuch](OBSERVABILITY_DATA_DICTIONARY.md).

## Forschungsfragen und Hypothesen

| ID | Frage | praeregistrierte Hypothese | Widerlegungs- oder Unentscheidbarkeitssignal |
|---|---|---|---|
| RQ-01 | Wie autonom kann die Lieferkette laufen? | Laengere belegte Agentensegmente ohne zaehlbare menschliche Intervention koennen bei unveraenderten Gates zu einem gueltigen Outcome konvergieren. | Mehr Laufzeit erzeugt nur Rework, Wiederholung, Gate-Umgehung oder keinen gueltigen Outcome; fehlende Ereignisse machen die Frage `unknown`. |
| RQ-02 | Was leistet der versionierte CDD-Zusammenhang? | Aufloesbare Bindungen zwischen Auftrag, Aenderung, Gate und Evidenz reduzieren nicht aufloesbare Entscheidungen und Review-Nacharbeit. | Gleichartige Einheiten ohne diese Bindungen sind gleich gut oder besser; nicht vergleichbare Einheiten erlauben keine Kausalaussage. |
| RQ-03 | Wie niedrig kann das Hardwareziel bleiben? | Feste Budgets und isolierte Optimierungen erhalten Lesbarkeit und Atmosphaere auf den deklarierten Mindestprofilen. | Reale gebundene Profile verfehlen Performance oder Qualitaet; Entwicklerhardware ersetzt keinen Profilbeleg. |
| RQ-04 | Laesst sich Entwicklungs-Compute amortisieren? | Zusaetzlicher einmaliger Optimierungsaufwand kann bei hinreichender Nutzung geringere Endgeraeteanforderungen aufwiegen. | Fehlende Provider-, Nutzungs-, Leistungs- oder Lebenszyklusdaten machen die Bilanz unentscheidbar; Proxys werden nicht in Energie oder CO2e umgerechnet. |
| RQ-05 | Bleibt die kreative Identitaet eigenstaendig? | Clean Room, interne Regeln, Provenienz und unabhaengige Reviews koennen eine erkennbare eigene Identitaet erzeugen. | Blindtests zeigen keine Identitaet oder eine konkrete Fremdzuordnung; Konzeptmaterial ersetzt keinen Test. |
| RQ-06 | Ist Hardware-Eskalation fuer bessere Spielerlebnisse notwendig? | Ein schlanker Runtime-Unterbau und isolierte Optimierungen koennen mehr akzeptierte Qualitaet pro Ressource liefern als die jeweilige eingefrorene Baseline. | Der Effekt beruht auf Scopeverlust, geaenderter Szene/Qualitaet oder staerkerer Hardware; fehlende kontrollierte Baseline macht ihn unentscheidbar. |
| RQ-07 | Kann T-053 den Lieferprozess wissenschaftlich beobachten, ohne ihn zu veraendern? | Der prospektive T-042-Lauf laesst sich mit vollstaendiger Pflicht-Ereigniskette, aufloesbaren Quellen und byteidentisch reproduzierbaren Exporten erfassen, waehrend T-042-Gates und -Artefakte unveraendert bleiben. | Ein Pflicht-Ereignis oder Quellbeleg fehlt, der Export ist nicht reproduzierbar, T-053 beeinflusst den Zielpfad, oder der Zielauftrag ist nicht startberechtigt. |

Hypothesen werden nach Protokoll-Freeze nicht passend zum Ergebnis
umformuliert. Jede Aenderung erfordert eine neue Protokollversion, einen
Changelog-Eintrag und einen neuen Bundle-Hash; bereits erhobene Ereignisse
bleiben an ihre urspruengliche Version gebunden.

## Praeregistriertes Ereignismodell

Die kanonische Ordnung ist eine append-only Kette pro `observationId`. Das
Instrument beobachtet allgemeine SDLC-Ketten ueber viele Tasks, Runs und
Autopilotzyklen; P-001 ist nur der erste praeregistrierte Einsatz. Jedes
Ereignis besitzt die gemeinsame Huelle aus Study-/Observation-/Run-/Cycle-/
Task- und Parent-IDs, Sequenz und monotoner Zeit, UTC-Zeit, Evidenzklasse,
Ereignistyp, Rolle, Branch/Base/Head/Tree, getrenntem Autonomiemodus und
Aktivitaetszustand, Provider-/Modell-/Modellversionsbindung, Resultat/Exit/
Fehlerklasse, stabiler pseudonymer Akteur-/Agentenidentitaet, Retry- und
Reparaturindex, Token- und Kostenprovenienz, Aenderungszusammenfassung samt
repo-relativen Pfaden, Privacy-/Redactionstatus, Quellreferenzen, Nutzdaten
und Kettenhashes. Nicht
anwendbare oder unbeobachtete Huellenfelder sind literal `unknown` und nie
`null` oder geschaetzt. Feldtypen und Anwendbarkeit stehen im
Datenwoerterbuch.

Das vollstaendige Ereignisregister umfasst mindestens:

| Familie | Ereignistypen |
|---|---|
| Protokoll/Beobachtung | `protocol.frozen`, `observation.started`, `outcome.observed`, `observation.closed` |
| Autopilot | `autopilot.started`, `autopilot.paused`, `autopilot.resumed`, `autopilot.stopped` |
| Agentenlaeufe | `agent.run.started`, `agent.run.finished`; Eltern-/Kindbeziehung ueber `runId`/`parentRunId` |
| Tasklebenszyklus | `task.planned`, `task.ready`, `task.implemented`, `task.reviewed`, `task.rejected`, `task.accepted` |
| Kontinuitaet | `wip.snapshot.created`, `context.compacted`, `run.resumed` |
| Modus/Aktivitaet | `autonomy.mode.changed` fuer `autonomous`/`human-directed`; `activity.state.changed` fuer `agent-active`, `idle`, `sleeping`, `blocked`, `offline`; beide Achsen bleiben unabhaengig |
| Gates | `gate.started`, `gate.finished` sowie explizite `build.failed`, `test.failed`, `lint.failed`, `security.failed`, `verify.failed` |
| Reparatur | `repair.attempted`, `repair.outcome` |
| Routing/Modelle | `routing.decided`, `model.switched` |
| Blocker | `budget.blocked`, `rate.blocked`, `provider.blocked`, `infrastructure.blocked`, jeweils mit spaeterem `block.resolved` oder offenem Outcome |
| Git/Evolution | `revision.observed`, `git.commit.observed`, `git.tree.promoted`, `git.rollback.observed`, `git.supersession.observed` |
| Architektur | `architecture.checkpoint.created` mit Datei-, Komponenten-, Abhaengigkeits-, Analyzer-/Warnungs-, Test- und optionaler Complexity-Sicht |
| Outcomes | `milestone.reached`, `git.tag.observed`, `defect.observed` sowie Task- und Forschungsoutcome |
| Tools/Review | `tool.finished`, `review.observed` |
| Interventionen/Menschen | `research.intervention.started`, `research.intervention.ended`, `research.intervention.recorded`; `human.instruction`, `human.review`, `human.correction`, `human.approval`, `human.emergency`, `human.observation`; Entscheidungswirkung wird genau einer Kategorie zugeordnet oder als reine Beobachtung nicht gezaehlt |

Fuer jede geschlossene Einzelbeobachtung sind `protocol.frozen`,
`observation.started`, mindestens ein Task-, Run- oder Autopilotereignis,
genau ein `outcome.observed` und genau ein `observation.closed` Pflicht.
Longitudinale Fenster verweisen auf geschlossene Einzelbeobachtungen und
erfinden keine fehlenden Zwischenereignisse.

Ein fehlender Start, Outcome oder Abschluss wird nicht synthetisch ergaenzt.
Die Beobachtung bleibt unvollstaendig und davon abhaengige Metriken werden
`unknown`. Ereignisse mit gleicher Quellzeit werden nach `sequence`, nicht
nach vermuteter Kausalitaet geordnet. Korrekturen erzeugen ein neues Ereignis
mit `supersedesEventId`; bestehende Ereignisse werden nicht umgeschrieben.

## Menschliche Interventionen

Die initiale Auftragserteilung und bereits im eingefrorenen Taskmanifest
enthaltene Regeln sind Kontext, keine Intervention. Jeder spaetere Akt erhaelt
genau eine Kategorie:

| Code | Kategorie | Einschlussregel |
|---|---|---|
| `I0-observation-no-intervention` | reine Beobachtung | liefert nur einen Befund ohne Prioritaet, Auftrag, Freigabe oder Arbeitsweg zu aendern; immer `counted=false` |
| `I1-clarification` | Klaerung | beantwortet eine Rueckfrage, ohne Scope oder Kriterien zu aendern |
| `I2-scope-criteria-change` | Scope-/Kriteriumsaenderung | fuegt Scope oder Abnahmekriterien hinzu, entfernt oder aendert sie |
| `I3-technical-direction` | technische Direktive | gibt einen konkreten Implementierungs- oder Diagnoseweg vor |
| `I4-domain-decision` | Domaenenentscheidung | entscheidet Produkt-, Fach- oder Forschungssemantik innerhalb vorhandener Autoritaet |
| `I5-priority-change` | Prioritaetsaenderung | aendert Reihenfolge, Dringlichkeit oder naechsten Task |
| `I6-defect-report` | Defektbericht | meldet einen konkreten Defekt, ohne schon den Reparaturweg vorzugeben |
| `I7-technical-unblock` | technische Entsperrung | liefert eine konkrete technische Voraussetzung oder loest einen technischen Block |
| `I8-infrastructure` | Infrastruktur | aendert Verfuegbarkeit von Hardware, Dienst, Runner, Netz oder Credentialzugriff |
| `I9-review-promotion` | Review/Promotion | fordert Reviewkorrektur oder autorisiert Review, Statusuebergang, Merge oder Veroeffentlichung |
| `I10-emergency-stop` | Notstopp | unterbricht oder beendet die Einheit aus Sicherheits-/Schadensgrund |
| `I11-other` | sonstige Einwirkung | belegte Einwirkung, die keiner obigen Klasse entspricht; Freitextgrund Pflicht |

Kann eine Einwirkung nicht eindeutig klassifiziert werden, ist ihre Kategorie
`unknown`; sie zaehlt weiterhin als Intervention. Mehrere Kategorien werden
nicht aus einer Nachricht erzeugt, ausser getrennte, jeweils belegbare
Entscheidungsakte liegen vor. `research intervention start` oeffnet ein
explizit gemessenes menschliches Aktivintervall; `end` schliesst genau die
genannte offene ID auf derselben monotonen Uhr; `record` erfasst einen
punktuellen Entscheidungsakt ohne behauptete Dauer. Ein offenes Intervall und
ein `record`-Akt haben Dauer literal `unknown`; Nachrichtenzeit oder
Antwortlatenz werden nie als Menschenminuten interpretiert. Das Zaehlen folgt
den Regeln in [METRICS.md](METRICS.md).

## Harnessgrenze, CLI und Ledger-Sicherheit

Primaer collectionfaehig sind die strukturierten Grenzen des vorhandenen
Harness: Run-/Task-Lifecycle, Append-Evidence, Gateaufrufe, Git-/Treebindung,
Routing-, Blocker- und Outcome-Receipts. Freie Prozesslogs duerfen nur
supplementale Quellreferenzen oder Fehlerdiagnose liefern. Logparser duerfen
niemals Taskstatus, Gatepass, Intervention, Token/Kosten, Modellversion oder
Akzeptanz als autoritative Tatsache erzeugen.

Konkret wird die spaetere Erweiterung an den bestehenden Rueckgabegrenzen von
`start-run`/`RunStore.startProvenanced`, `append-event`/`RunStore.append`,
`append-evidence`, `finish-run`/`RunStore.finish` und
`verify`/`Verification.verify` angeschlossen. Ihre strukturierten Run-,
Sequenz-, Eventhash-, Status-, Evidenz-, Exit-, Dauer-, Artefakt- und
Summaryreceipts sind die primaeren Quellen. Gate-, Task-, Routing-, Blocker-
oder Usagefakten werden nur ueber typsichere Payloads/Receipts an diesen
Grenzen oder gleichwertige spaetere Harness-APIs aufgenommen; Konsolenausgabe
nachtraeglich zu parsen ist kein Ersatz.

Der zukuenftige CLI-Vertrag lautet:

```text
riftharness research begin --study-manifest PATH
riftharness research status [--study ID] [--observation ID]
riftharness research verify --study-manifest PATH [--recover-to NEW_PATH]
riftharness research export --study-manifest PATH --output DIR
riftharness research summarize --export-manifest PATH --output report.md
riftharness research intervention start --observation ID --category CODE --source-ref REF
riftharness research intervention end --observation ID --intervention ID --source-ref REF
riftharness research intervention record --observation ID --category CODE --source-ref REF
riftharness research close --observation ID --outcome-receipt REF
riftharness research import-git-history --task T-### --base COMMIT --head COMMIT --output PATH
```

`begin` ist der einzige Aktivierungspfad fuer eine prospektive Beobachtung. Er
validiert unter exklusivem Lock das eingefrorene Study-Manifest, Protokoll-,
Baseline-, HEAD-, Tree-, Task- und Nichtinterferenz-Bindungen, die regulaere
Startberechtigung des Zieltasks sowie die Abwesenheit eines bereits gestarteten
Ziellaufs und einer zweiten aktiven Beobachtung. Erst dann schreibt er
`protocol.frozen` und direkt danach `observation.started`, fsynct beide
Ereignisse. Den hashgebundenen Active-Marker schreibt er mit exklusiver
Neuanlage in eine gleichdateisystemige Temporaerdatei, fsynct die Datei,
benennt sie atomar um, fsynct das Parent-Verzeichnis und oeffnet den Marker
danach ohne Symlinkfolge erneut, um kanonische Bytes, Bindungen und letzten
Ledgerhash zu pruefen. Erst danach darf `begin` Erfolg und einen gebundenen
Aktivierungsreceipt zurueckgeben; nur dieser Erfolg autorisiert den Zielstart.
Ein Retry derselben Beobachtung darf eine vorhandene, vollstaendig gueltige
Startkette samt Marker idempotent erneut pruefen und bestaetigen, aber keine
Ereignisse duplizieren. Eine Startkette ohne dauerhaften Marker ist
`INCOMPLETE_ACTIVATION`, darf den Marker nicht nachtraeglich rekonstruieren und
ist nicht als `prospective-observed` verwendbar. Schlaegt eine Vorbedingung
fehl oder existiert bereits ein Zielereignis, entsteht keine aktive
Beobachtung; insbesondere scheitert ein verspaeteter Start mit
`PROSPECTIVE_START_TOO_LATE` statt rueckwirkend prospektive Daten zu erzeugen.

`status` liest nur und meldet offene Runs, Interventionen, Kettenzustand und
`unknown`-Felder. `verify` prueft Schema, IDs, monotone Zeit, Kette, Quellen,
Locks, Tail und Manifeste. `export` liest ausschliesslich ein eingefrorenes
Study-Manifest. `summarize` erzeugt deterministisches `report.md` aus dem
verifizierten Export. `close` verlangt einen aufloesbaren strukturierten
Outcome-Receipt des Zielpfads, schreibt daraus genau ein `outcome.observed` und
danach `observation.closed` und fsynct die Kette. Erst nach erneuter
Validierung der finalen Kette und des passenden Markers entfernt es den
Marker, fsynct dessen Parent-Verzeichnis und prueft seine Abwesenheit; erst
dann kehrt `close` erfolgreich zurueck. Ein Retry bei gueltig geschlossener
Kette plus passendem Marker behandelt diesen als `STALE_ACTIVE_MARKER`, fuegt
kein zweites Abschlussereignis an und wiederholt nur validiert und idempotent
Unlink, Directory-fsync und Abwesenheitspruefung. Hooks behandeln eine
geschlossene Kette auch mit Stale-Marker immer als inaktiv. `close` veraendert
weder Zieltask noch Outcome.
`import-git-history` erzeugt ausschliesslich `retrospective-derived` aus den
beiden vollstaendigen Commits; malformed, bewegliche oder nicht aufloesbare
Grenzen scheitern. Hooks ohne einen gueltigen Active-Marker, bei
`INCOMPLETE_ACTIVATION` oder nach `observation.closed` sind No-ops. `status`
und `verify` melden Marker-/Ketteninkonsistenzen fail-closed; die read-only
Operation `status` repariert sie nie. Ein Collectorfehler aendert nie das
Resultat des Zielpfads, macht die Forschungsbeobachtung aber sichtbar
unvollstaendig.

Pro Beobachtung ist genau ein Writer durch einen exklusiven OS-Dateilock
zulaessig. Vor Append prueft er unter Lock Schema, letzte Sequenz und letzten
Hash. Die kanonische vollstaendige Zeile wird in einer gleichdateisystemigen
Temporaerdatei geschrieben und fsynct, dann unter Lock als ein Append in das
Ledger uebernommen und das Ledger fsynct. Lockkonkurrenz scheitert kontrolliert
mit `CONCURRENT_WRITER`; es gibt keinen zweiten Writer und keinen last-write-
wins-Pfad.

Ein Crash kann einen nicht mit LF abgeschlossenen oder hashungueltigen Tail
hinterlassen. `verify` meldet `TORN_TAIL`, bewahrt Originalbytes unveraendert
und bricht fail-closed ab. `--recover-to` schreibt nach expliziter Wahl eine
neue Datei aus dem laengsten vollstaendig verifizierten Praefix plus einem
`ledger.recovery.recorded`-Ereignis, das Originalhash, Praefixhash und
Torn-Tail-Hash bindet. Das Original wird nie still gekuerzt, ueberschrieben
oder als gueltig markiert. Numerische Exitcodes werden erst nach
Kollisionsinventar additiv festgelegt; bestehende Bedeutungen bleiben
unveraendert.

`autopilot.paused`/`resumed` ist ein Lifecyclezustand: der Autopilot plant
keine neuen Zyklen. Er ist weder `autonomyMode` noch automatisch ein
`activityState`; der Prozess kann waehrend einer Pause etwa `idle`, `sleeping`
oder `offline` sein. `autonomyMode` beschreibt Entscheidungshoheit,
`activityState` beobachtete Aktivitaet. Alle drei Achsen werden getrennt
exportiert.

## Architekturbeobachtung

Architekturtrends sind deskriptive Git- und Validatorbefunde, keine
Qualitaetswertung. Vor jeder Auswertung wird eine versionierte Pfadkarte
eingefroren: Produktionsmodule `src/<projekt>/`, Tests `tests/`, Harness
`tools/RiftHarness` und Spezifikation/Dokumentation. Erfasst werden geaenderte
Produktionsdateien, beruehrte Produktionsmodule, deklarierte
Projekt-Referenzkanten, bestaetigte Grenzverletzungen und Bruttozeilenchurn.

Dateibewegungen werden als Rename behandelt, wenn Git sie im festgelegten
Diffmodus erkennt; Binaerdateizeilen sind `unknown`. Eine steigende Zahl
beruehrter Module ist weder Kopplungsbeweis noch Verschlechterung. Ein
Grenzverstoss zaehlt nur bei einem benannten Architekturvalidator oder einem
bestaetigten Reviewbefund; Textsuche allein erzeugt keinen Befund.

Ein `architecture.checkpoint.created` exportiert Zeilen je Produktions- und
Testdatei, Top-10-Dateigroesse und -Wachstum, Komponentenanteile,
Dependencyrichtungen/-verletzungen, Analyzerwarnungen, Testanzahl/-wachstum,
Integrationspunktkonzentration und optional methodengebundene Complexity.
`CommandLoopRunner`, `CommandReportSchema` und `SessionEngine` sind
vorregistrierte Integrationspunkte. Diese Werte sind in Version 1 rein
diagnostisch (`gateCoupled=false`) und veraendern kein bestehendes Gate.
Jedes `task.accepted` verlangt bis zum Beobachtungsabschluss genau einen
diagnostischen Checkpoint fuer dieselbe Task- und Tree-ID. Fehlt er, ist die
Checkpointabdeckung unvollstaendig und davon abhaengige Akzeptanzaggregation
`unknown`; der Checkpoint bleibt dennoch immer `gateCoupled=false`.

## WIP-Provenienz ohne Promotionsautoritaet

Ein kuenftiges `wip.snapshot.created` bindet eine separate kanonische
Provenienz-Sidecar mit den Feldern `Task`, `Phase`, `Agent-Role`, `Run`,
`Parent`, `LastGate`, `FailureClass`, `AutonomyState` und `ResearchSchema`.
Unbelegte Werte sind literal `unknown`. Der Sidecar darf weder bestehende
Git-Historie umschreiben noch direkte Autoritaet fuer `main`, Akzeptanz oder
Promotion verleihen; er dokumentiert ausschliesslich Kontinuitaet.

## Spiel-, Hardware- und Compute-Messungen

Die Observability-Erweiterung ersetzt die bestehenden Spiel- und
Effizienzfragen nicht. Jeder Performancevergleich bindet Commit,
Betriebssystem, Treiber, Hardwareprofil, Grafikpreset, Szene, Seed,
Aufwaermphase und Messdauer. Erfasst werden, soweit direkt gemessen:

- Framezeit p50/p95/p99, 1%-Low-FPS und sichtbare Hitches,
- CPU- und GPU-Zeit je System, Draw Calls, Dreiecke und sichtbare Einheiten,
- Resident/Peak RAM, VRAM-Schaetzung mit expliziter Methode und Ladezeit,
- Stabilitaet und Speicherwachstum in Replay-/Soaklaeufen,
- Leistungsaufnahme an der Steckdose nur bei reproduzierbarer Messung,
- visuelle Lesbarkeit und wahrgenommene Qualitaet in blindem Screenshot- oder
  Buildtest,
- Abweichung von `docs/PERFORMANCE_BUDGET.md` ohne Grenzwertaenderung.

Qualitaet wird nicht aus Polygonzahl, Effektmenge, Commitanzahl oder
Marketingmaterial abgeleitet. Konzeptgrafiken sind kein Gameplay- oder
Performancebeleg. Pflichtprofile bleiben `NOT-MEASURED`, bis eine Messung
ihre vertragliche Referenzklassenbindung belegt.

Fuer die Compute-/Nachhaltigkeitsbilanz bleiben Entwicklung, Distribution/
Nutzung, tatsaechlich vermiedenes Gegenfaktum und Lebenszyklus getrennt.
Providerrequests, Tokens und Kosten sind hoechstens exakt quittierte Proxys.
Ohne direkte methodengebundene Energiedaten werden sie nie in kWh oder CO2e
umgerechnet. Herstellungsaufwand, Lebensdauer, Wiederverwendung, reale
Nutzungszahl und Rebound bleiben explizite Datenanforderungen.

Vergleiche werden vor Start nach Taskgroesse, Risiko und Domaene geschichtet.
Aufgabe, Gates, Zeitfenster und Abbruchregel werden eingefroren. Ein
Performance-A/B veraendert genau die praeregistrierte Massnahme; Szene,
Simulation, Kamera, Seed, Spielumfang und Qualitaetsziel bleiben gleich.
Anschliessende Ablationen entfernen genau eine Optimierung. Eine hoehere FPS
durch entferntes Gameplay stuetzt RQ-06 nicht. Fehlende Telemetrie bleibt
`unknown`.

## Erster echter Lauf und Baselines

### Retrospektive Kalibrierung R-001

- Ziel: T-037 auf dem im Export genannten Git-Commit.
- Klasse: ausschliesslich `retrospective-derived`.
- Zweck: Parser-, Quellenauflosungs- und `unknown`-Verhalten kalibrieren.
- Verbot: fehlende Tokens, Kosten, Interventionen oder Laufzeiten aus
  Release Notes, Commitabstaenden oder Plausibilitaet schaetzen.
- Aussage: deskriptive Rekonstruktion; kein Vergleichserfolg und kein Beweis
  fuer die Vollstaendigkeit historischer Agentenereignisse.

### Prospektive Beobachtung P-001

- Ziel: der erste regulaere T-042-Implementierungslauf nach Freeze dieses
  Protokollbundles.
- Startbedingung: T-042-Manifest ist im gebundenen Baseline-Commit vorhanden,
  schema-gueltig und durch seinen eigenen Prozess startberechtigt; T-053 hat
  vor `observation.started` den Protokollhash und die Baseline gebunden.
- Primaerhypothese: RQ-07.
- Primaeroutcomes: Pflicht-Ereignisketten-Vollstaendigkeit,
  Quellenaufloesungsquote, deterministische Exportidentitaet und nachgewiesene
  Nichtinterferenz.
- Sekundaer: die agentischen und architektonischen Metriken, soweit direkt
  beobachtet. Zielerfolg, Tokens und Kosten sind nicht automatisch vorhanden.
- Abbruch: Baseline oder Protokollhash aendert sich vor Start, Collector
  schreibt in T-042-Artefakte, Pflichtquelle ist nicht redigierbar, oder der
  Zielauftrag ist nicht startberechtigt. Abbruch wird als Outcome erfasst,
  niemals als erfolgreicher Lauf.

T-037 und T-042 werden wegen unterschiedlicher Evidenzklassen, Zeitpunkte und
Auftragsinhalte nicht als kausales A/B-Paar behandelt.

T-053 kann erst abgeschlossen werden, nachdem P-001 tatsaechlich prospektiv
durchgefuehrt, exportiert und auf dem exakten Ergebnisbaum durch eine vom
Builder getrennte unabhaengige Reviewinstanz mit `PASS` geprueft wurde. Ist
T-042 nicht startberechtigt oder wird P-001 abgebrochen, bleibt T-053
unfertig; ein dokumentierter Restpunkt ersetzt den Echtlauf nicht.

## Isolierte Ablationen

Ablationen laufen nur auf einer unveraenderlichen Kopie desselben
Eingabebundles. Die veraenderte Kopie und alle Ergebnisse tragen
`synthetic-test-only`; sie duerfen keine reale Laufmetrik ersetzen.

| ID | einziger veraenderter Faktor | Kontrollausgabe | Ablationsausgabe | vorab erwarteter Effekt |
|---|---|---|---|---|
| ABL-01 | Quellreferenzen | originale, aufloesbare `sourceRefs` | alle `sourceRefs` durch gueltige, aber nicht aufloesbare Fixture-IDs ersetzt | Quellenaufloesungsquote sinkt; quellenabhaengige Metriken werden `unknown` |
| ABL-02 | Interventionskategorie | originale Kategorie | jede belegte gezaehlte Kategorie durch `I11-other` ersetzt, `I0` und Ereigniszahl unveraendert | Gesamtinterventionen bleiben gleich, Kategorienmetriken verlieren Trennschaerfe |
| ABL-03 | Architektur-Pfadkarte | eingefrorene Pfadkarte aktiv | Pfadkarte leer, alle Ereignisse unveraendert | Architekturmetriken werden `unknown`; Outcome und Gates bleiben identisch |

Je Ablation werden Eingabehash, genau ein Faktor, Tool-/Versionsbindung,
beide Exporthashes und das beobachtete Delta festgehalten. Mehrere Faktoren
in einem Lauf sind verboten.

## Konzeptuelle spaetere isolierte Experimente

Nach P-001 duerfen, erst nach separater Praeregistrierung und ohne
automatische `main`-Promotion, folgende Varianten untersucht werden:

- `EXP-FULL`: vollstaendiges System als Kontrollbedingung,
- `EXP-NO-PERSISTENT-RAG-MEMORY`: keine persistente RAG-/Memory-Nutzung,
- `EXP-SINGLE-SESSION`: genau eine Agentensitzung ohne Kindlauf,
- `EXP-REVIEW-ON` gegen `EXP-REVIEW-OFF`, wobei `REVIEW-OFF` weder
  `task.accepted` noch Promotion autorisieren darf,
- `EXP-MODEL-ROUTING`: praeregistrierte Routingpolitik gegen die eingefrorene
  Kontrollpolitik.

Je Vergleich bleiben Taskbaseline, Eingabeartefakte, Budget, Zeitfenster,
Toolchain, Hardwareklasse, Gates und Abbruchregel identisch; genau ein Faktor
wird veraendert. Ergebnisse liegen auf isolierten Branches, tragen eine eigene
Experiment-/Stratum-ID und werden nie automatisch nach `main` uebernommen.

## Exporte und Ergebnisregel

Der private kanonische Export umfasst `events.jsonl`, `observations.csv`,
`autopilot-cycles.csv`, `agent-runs.csv`, `task-lifecycle.csv`,
`continuity.csv`, `activity-intervals.csv`, `routing.csv`,
`human-events.csv`, `interventions.csv`, `gate-attempts.csv`,
`failures-and-repairs.csv`, `blocks.csv`, `git-evolution.csv`, `outcomes.csv`, `usage.csv`,
`architecture-trends.csv`, `architecture-files.csv`,
`architecture-dependencies.csv`, `metrics.csv`, `study-manifest.json`,
`evidence-manifest.json`, `summary.json`, das deterministische `report.md` und
das nichtrekursive aeussere `EXPORT.SHA256`. Sortierung, Serialisierung,
Tree-Bindung und die kreisfreie Hashschichtung sind im
Reproduzierbarkeitsleitfaden festgelegt. Der oeffentliche Export ist eine
getrennte redigierte Ableitung und niemals die Quelle fuer Reproduktion.

Ein Forschungsergebnis lautet genau `supports`, `contradicts` oder
`inconclusive`. Das ist eine Bewertung der praeregistrierten Hypothese, nicht
der Taskstatus. Unvollstaendige Primaerfelder, gemischte Evidenzklassen oder
fehlende Nichtinterferenz machen RQ-07 `inconclusive`; sie werden nie in
`supports` umgedeutet.

## Begleitvertraege

- [Datenwoerterbuch](OBSERVABILITY_DATA_DICTIONARY.md)
- [exakte Metrikdefinitionen](METRICS.md)
- [Reproduzierbarkeitsleitfaden](REPRODUCIBILITY.md)
- [Bedrohungen der Validitaet](THREATS_TO_VALIDITY.md)
- [Datenschutz-, Redaktions- und Publikationsplan](PRIVACY_AND_PUBLICATION.md)
- [Protokoll-Changelog](PROTOCOL_CHANGELOG.md)

Diese Dateien bilden gemeinsam den T-053-Protokollbundle. Widersprueche
werden fail-closed als Protokolldefekt behandelt; `PROTOCOL.md` bestimmt
Forschungsfragen und Design, das Datenwoerterbuch Feldsemantik und
`METRICS.md` die Berechnung.
