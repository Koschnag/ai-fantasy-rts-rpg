# T-053 Metrikvertrag

**Vertrag:** `riftward-observability-metrics-v1`

**Protokoll:** `riftward-research-observability` 2.0.1

## Berechnungsregeln

Eine Metrik wird nur berechnet, wenn alle in ihrer Definition genannten
Ereignisse und Quellreferenzen aufloesbar sind und derselben Evidenzklasse
angehoeren. Andernfalls ist ihr Wert literal `unknown` mit dem passenden
`availabilityReason`. Null ist nur ein beobachteter Zahlenwert, nie der Ersatz
fuer fehlende Daten. Division durch null ergibt `unknown`.

Zeitintervalle sind halb-offen `[start,end)`. Ueberlappende Intervalle werden
vor Summenbildung vereinigt. Prozentwerte werden als Anteil zwischen 0 und 1
mit sechs Dezimalstellen, half-away-from-zero gerundet. Millisekunden und
Zaehler bleiben ganze Zahlen. Geld wird nicht umgerechnet; Betrag und
ISO-4217-Waehrung bleiben getrennte Felder.

## Primaermetriken fuer P-001

| ID | Einheit | exakte Definition |
|---|---|---|
| `OBS-CHAIN-COMPLETE` | boolean | `true` genau dann, wenn ein gueltiges `protocol.frozen`, genau ein `observation.started`, mindestens ein Task-, Run- oder Autopilotereignis, genau ein `outcome.observed`, genau ein `observation.closed`, eine lueckenlose Sequenz und eine gueltige Hashkette vorliegen; bei pruefbar unvollstaendiger gueltiger Eingabe `false`, bei unlesbarer Eingabe `unknown` |
| `OBS-SOURCE-RESOLUTION-RATE` | ratio | Zahl der Quellreferenzen mit `resolvable=true` geteilt durch alle Quellreferenzen; Duplikate werden je Ereignisreferenz gezaehlt |
| `OBS-EXPORT-BYTE-IDENTICAL` | boolean | `true`, wenn zwei frische Exporte desselben Eingabemanifests mit identischer Toolchain fuer jede kanonische Exportdatei denselben SHA-256 besitzen; `false` bei mindestens einem Unterschied; ohne Doppelrun `unknown` |
| `OBS-NON-INTERFERENCE` | boolean | `true`, wenn Hashmanifest von T-042-Task-/Produkt-/Test-/Gateartefakten und alle Zielstatus unmittelbar vor und nach Collectoraktivierung identisch sind und T-053 nur seine erlaubten Beobachtungspfade schreibt; `false` bei belegter Abweichung; ohne beide Snapshots `unknown` |
| `OBS-UNKNOWN-RATE` | ratio | Zahl der Metrikzeilen mit `value=unknown` geteilt durch alle Metrikzeilen der Beobachtung; die Rate wird immer mit ausgegeben und niemals als Fehler versteckt |

RQ-07 wird nur dann `supports`, wenn die ersten vier Primaermetriken `true`
bzw. `OBS-SOURCE-RESOLUTION-RATE=1.000000` sind. Ein belegtes `false` bei
Kettenvollstaendigkeit, Exportidentitaet oder Nichtinterferenz ergibt
`contradicts`. Jede `unknown`-Primaermetrik oder eine Quellenquote unter 1
ergibt `inconclusive`. Der Erfolg oder Misserfolg von T-042 aendert diese
Regel nicht.

## Zeit und Autonomie

| ID | Einheit | exakte Definition |
|---|---|---|
| `TIME-WALL-MS` | ms | monotone Zeit von `observation.started` bis `observation.closed` auf derselben `monotonicClockId`, abwaerts auf Millisekunden gerundet; umfasst Warten und ist keine aktive Arbeitszeit |
| `TIME-TO-OUTCOME-MS` | ms | monotone Zeit von `observation.started` bis `outcome.observed` auf derselben Uhr |
| `TIME-TO-FIRST-GREEN-MS` | ms | fuer das Pflichtgate-Buendel: spaeteste monotone Abschlusszeit des jeweils ersten `pass` je erforderlichem Gate minus Beobachtungsstart; falls Uhr oder Gatepass fehlt, `unknown` |
| `TIME-TO-HUMAN-MS` | ms | monotone Zeit vom Beobachtungsstart bis zur ersten gezaehlten menschlichen Intervention; ohne Intervention bei geschlossener, vollstaendiger Beobachtung gleich `TIME-WALL-MS`, bei unvollstaendiger Beobachtung `unknown` |
| `AGENT-UNINTERRUPTED-MAX-MS` | ms | groesste monotone Zeitspanne zwischen Beobachtungsstart, gezaehlten Interventionen und Outcome innerhalb derselben Uhr; sonst `unknown` |
| `TOOL-ACTIVE-MS` | ms | Laenge der Vereinigung aller vollstaendig beobachteten `tool.finished`-Intervalle derselben monotonen Uhr; ein fehlendes Intervall macht die Metrik `unknown` |
| `WAIT-MS` | ms | `TIME-TO-OUTCOME-MS - TOOL-ACTIVE-MS`; nur deskriptiver Rest, nicht automatisch Agentenwartezeit; negative Werte sind invalid und ergeben `unknown` |
| `MODE-AUTONOMOUS-MS` | ms | Summe vollstaendig begrenzter `autonomyMode=autonomous`-Intervalle derselben monotonen Uhr |
| `MODE-HUMAN-DIRECTED-MS` | ms | entsprechende Summe fuer `human-directed` |
| `ACTIVITY-AGENT-ACTIVE-MS` | ms | Summe vollstaendig begrenzter `activityState=agent-active`-Intervalle |
| `ACTIVITY-IDLE-MS` | ms | entsprechende Summe fuer `idle`; fehlende Toolaktivitaet allein erzeugt kein Idleintervall |
| `ACTIVITY-SLEEPING-MS` | ms | entsprechende Summe fuer absichtlich zeitgesteuertes `sleeping` |
| `ACTIVITY-BLOCKED-MS` | ms | entsprechende Summe fuer `blocked` |
| `ACTIVITY-OFFLINE-MS` | ms | entsprechende Summe fuer `offline` innerhalb eines explizit beobachteten Fensters |
| `ACTIVITY-AGENT-ACTIVE-RATIO` | ratio | `ACTIVITY-AGENT-ACTIVE-MS / Summe(alle fuenf vollstaendigen Aktivitaetszustaende)` |

## Interventionen

| ID | Einheit | exakte Definition |
|---|---|---|
| `INT-COUNT` | count | Zahl unterschiedlicher `interventionId` mit `counted=true`, dedupliziert nach `decisionActSha256` |
| `INT-I0-OBSERVATION` | count | verschiedene `I0-observation-no-intervention`-Akte mit `counted=false`; nie Bestandteil von `INT-COUNT` |
| `INT-I1` bis `INT-I11` | count | Teilmenge von `INT-COUNT` mit der jeweiligen Primaerkategorie |
| `INT-UNKNOWN` | count | gezaehlte Interventionen mit `category=unknown` |
| `INT-RATE-PER-HOUR` | count/hour | `INT-COUNT / (TIME-TO-OUTCOME-MS / 3_600_000)`; bei null Dauer `unknown` |
| `INT-QUESTION-UNANSWERED` | count | belegte Agentenfragen ohne spaetere Intervention mit passender `responseToQuestionId` bis Outcome; erfordert ein vollstaendiges Fragenregister, sonst `unknown` |
| `INT-OPEN` | count | `research.intervention.started`-IDs ohne passendes `ended`; offene Akte zaehlen, ihre Dauer bleibt `unknown` |
| `INT-CLOSED-ACTIVE-MS` | ms | Summe der Vereinigungen gueltiger Start-/Endintervalle derselben monotonen Uhr; ein offenes Intervall wird nicht als 0 eingerechnet und seine Einzelmetrik bleibt `unknown` |

Initialauftrag, automatische Systemnachricht, reine Empfangsbestaetigung und
unveraenderte vorab dokumentierte Regeln erzeugen kein Interventionsereignis.
Eine reine Beobachtung wird als I0 mit `counted=false` erfasst. Eine
Reviewkorrektur zaehlt auch dann, wenn sie von einer formal menschlichen
Freigaberolle uebermittelt wird; die Wirkung, nicht der Kanal, bestimmt die
Kategorie. Eine Dauer wird ausschliesslich aus `research intervention start`
und passendem `end` auf derselben monotonen Uhr berechnet; `record`, offene
Intervalle, Nachrichtentimestamps und Antwortlatenzen liefern keine Minuten.

## Konvergenz und Gates

| ID | Einheit | exakte Definition |
|---|---|---|
| `GATE-ATTEMPTS-TOTAL` | count | Zahl aller gepaarten `gate.started`/`gate.finished`-Versuche, unabhaengig vom Resultat |
| `GATE-FAILED-ATTEMPTS` | count | Zahl der Gateversuche mit `result=fail` |
| `GATE-BLOCKED-ATTEMPTS` | count | Zahl der Gateversuche mit `result=blocked` |
| `GATE-FIRST-PASS-ATTEMPTS` | count | Summe der Versuchszahl des ersten Passes je Pflichtgate; falls ein Pflichtgate keinen Pass hat, `unknown` |
| `GATE-COVERAGE` | ratio | verschiedene beobachtete Pflichtgates mit mindestens einem abgeschlossenen Versuch geteilt durch Zahl der im Taskmanifest genannten Pflichtgates |
| `GATE-PASS-COVERAGE` | ratio | verschiedene Pflichtgates mit mindestens einem belegten Pass auf dem Outcome-Zielbaum geteilt durch Zahl der Pflichtgates |
| `REPAIR-CYCLES` | count | Zahl nicht ueberlappender Sequenzen `gate fail oder Reviewbefund -> revision auf neuem Baum -> Wiederholung desselben Gates oder Reviews`; ohne neuen Baum kein Reparaturzyklus |
| `REVIEW-FINDINGS` | count | Summe verschiedener finding IDs aus gueltigen Reviewereignissen; Freitext ohne stabile ID ergibt `unknown` fuer dieses Review |
| `REVIEW-REWORK-FILES` | count | verschiedene Dateien, die nach dem ersten Reviewbefund und vor Outcome geaendert wurden; benoetigt aufloesbare Revisionsgrenzen |

Ein `pass` zaehlt nur auf dem im Outcome gebundenen Zielbaum. Ein Pass auf
einem frueheren Baum bleibt historischer Gateversuch, aber kein Bestandteil
von `GATE-PASS-COVERAGE`.

## Aenderungs- und Architekturmetriken

| ID | Einheit | exakte Definition |
|---|---|---|
| `CHANGE-FILES` | count | verschiedene Pfade zwischen gebundenem Baseline- und Ergebnisbaum nach Rename-Erkennung `git diff --find-renames=50%` |
| `CHANGE-LINES-ADDED` | lines | Summe Text-Additionen aus `git diff --numstat --find-renames=50%`; wenn irgendein Binaerpfad enthalten ist, Textsumme bleibt gueltig und `CHANGE-BINARY-FILES` weist ihn getrennt aus |
| `CHANGE-LINES-DELETED` | lines | entsprechende Text-Loeschungen |
| `CHANGE-BINARY-FILES` | count | Numstat-Zeilen mit `-`/`-` |
| `ARCH-PRODUCTION-FILES` | count | verschiedene geaenderte Pfade, die die eingefrorene Pfadkarte als Produktion klassifiziert |
| `ARCH-MODULES-TOUCHED` | count | verschiedene erste zwei Segmente `src/<projekt>` unter den Produktionspfaden |
| `ARCH-CROSS-MODULE` | boolean | `true`, wenn `ARCH-MODULES-TOUCHED >= 2`, sonst `false`; bei unbekannter Modulzahl `unknown` |
| `ARCH-REF-EDGES-ADDED` | count | Menge deklarierter Projekt-Referenzkanten im Ergebnis minus Baseline |
| `ARCH-REF-EDGES-REMOVED` | count | Menge deklarierter Projekt-Referenzkanten in Baseline minus Ergebnis |
| `ARCH-BOUNDARY-VIOLATIONS` | count | verschiedene bestaetigte Finding-IDs eines benannten Architekturvalidators oder akzeptierten Reviews |
| `ARCH-PRODUCTION-LINES` | lines | Summe bekannter `lines` aller `file_class=production` im Architekturcheckpoint; ein unbekannter Produktionspfad macht die Summe `unknown` |
| `ARCH-TEST-LINES` | lines | entsprechende Summe fuer `file_class=test` |
| `ARCH-COMPONENT-SHARE:<component>` | ratio | Produktionszeilen der Komponente geteilt durch `ARCH-PRODUCTION-LINES` |
| `ARCH-ANALYZER-WARNINGS` | count | Summe pfadgebundener eindeutiger Analyzer-Warnungs-IDs; ohne gebundenen Analyzerlauf `unknown` |
| `ARCH-TEST-COUNT` | count | Zahl eindeutiger Test-IDs im gebundenen Testinventar; Quelltextheuristik unzulaessig |
| `ARCH-TEST-GROWTH` | count | `ARCH-TEST-COUNT(head)-ARCH-TEST-COUNT(baseline)` bei gleicher Inventarmethode |
| `ARCH-INTEGRATION-CONCENTRATION` | ratio | Summe `abs(line_delta)` fuer `CommandLoopRunner`, `CommandReportSchema`, `SessionEngine` geteilt durch Summe `abs(line_delta)` aller Produktionsdateien; Nullnenner `unknown` |
| `ARCH-COMPLEXITY:<component>` | method unit | optionaler Wert der eingefrorenen `complexity_method`; ohne Methode `unknown`, nie methodenuebergreifend verglichen |
| `ARCH-ACCEPTED-CHECKPOINT-COVERAGE` | ratio | akzeptierte `taskId,acceptedTreeId`-Paare mit genau einem diagnostischen Architekturcheckpoint desselben Paars und `gateCoupled=false`, geteilt durch alle akzeptierten Paare; Nullnenner `unknown` |

`ARCH-LARGEST-FILES-TOP10` und `ARCH-LARGEST-GROWTH-TOP10` sind geordnete
Exportlisten, keine zu einem Score verdichteten Metriken. Ordnung und
Zeilenform stehen im Datenwoerterbuch. Die drei Integrationspunkte und alle
Architekturwerte sind in v1 diagnostisch mit `gateCoupled=false`; ein Anstieg
ist kein automatischer Defekt.

Trendexports enthalten pro Beobachtung Rohwerte. Eine Richtung wird erst ab
drei prospektiven, vergleichbaren Beobachtungen derselben Stratum-ID
berechnet. Fuer jede numerische Rohmetrik `M` ist
`ARCH-TREND-M-SLOPE` der Median aller paarweisen Steigungen
`(M[j]-M[i])/(j-i)` fuer `j>i`, wobei `i`/`j` die lueckenlose Ordnung nach
`observation.started.occurredAtUtc`, dann `observationId` ist; Ergebnis auf
sechs Dezimalstellen nach der globalen Regel. Weniger als drei Beobachtungen,
ein unknown-Rohwert, gemischte Klassen oder unterschiedliche Strata ergeben
`unknown`. Der Slope ist deskriptiv und keine Kausalwirkung.

## Nachvollziehbarkeit und Qualitaet

| ID | Einheit | exakte Definition |
|---|---|---|
| `TRACE-AC-EVIDENCE-COVERAGE` | ratio | Acceptance Criteria mit mindestens einem aufloesbaren, zum Outcome-Zielbaum passenden Evidenzbeleg geteilt durch alle Kriterien im gebundenen Taskmanifest |
| `TRACE-EVENT-SOURCE-COVERAGE` | ratio | Ereignisse mit mindestens einer aufloesbaren Quellreferenz geteilt durch alle Ereignisse ausser `observation.closed` |
| `QUALITY-OUTCOME` | enum | beobachtetes `taskOutcome`; kein numerischer Score |
| `QUALITY-DETERMINISTIC-REPEAT` | boolean | zwei durch Vertrag verlangte Wiederholungen haben dieselben explizit bezeichneten Artefakthashes; ohne Doppelbeleg `unknown` |
| `QUALITY-POST-ACCEPT-DEFECTS` | count | bestaetigte Defekt-IDs, deren Entdeckungszeit nach belegter Akzeptanz liegt und die auf den akzeptierten Ergebnisbaum zurueckgefuehrt sind; vor Ende des Beobachtungsfensters null nur bei vollstaendigem Defektregister, sonst `unknown` |

## Aufwand, Tokens und Kosten

| ID | Einheit | exakte Definition |
|---|---|---|
| `USE-REQUESTS` | count | providerseitig oder gatewayseitig quittierte eindeutige Request-IDs; lokale Schaetzung verboten |
| `USE-INPUT-TOKENS` | tokens | Summe quittierter Inputtokens ohne Cache-read-Tokens, entsprechend Providerfeldern |
| `USE-OUTPUT-TOKENS` | tokens | Summe quittierter Outputtokens |
| `USE-CACHE-READ-TOKENS` | tokens | Summe separat quittierter Cache-read-Tokens |
| `USE-CACHE-WRITE-TOKENS` | tokens | Summe separat quittierter Cache-write-Tokens |
| `USE-COST-AMOUNT` | ISO-currency | exakte Dezimalsumme mit `costProvenance=provider-reported` oder exakt reproduzierbarem `locally-calculated` aus quittierter Nutzung und eingefrorenem Preisstand; bei `estimated`, `unknown` oder gemischten Waehrungen `unknown`, keine Umrechnung oder Rundung |
| `USE-COST-ESTIMATED-AMOUNT` | ISO-currency | separate Summe ausschliesslich fuer `costProvenance=estimated` derselben Waehrung; nie Ersatz oder Bestandteil von `USE-COST-AMOUNT` |
| `USE-MACHINE-CPU-MS` | ms | Summe gemessener Prozess-CPU-Zeit der gebundenen Prozesse; Walltime ist kein Ersatz |
| `USE-ENERGY-WH` | Wh | nur direkt gemessene und methodengebundene Energie; nie aus Tokens, Kosten oder TDP geschaetzt |

Fehlt ein Providerreceipt oder differenziert es Tokenklassen nicht, sind die
betroffenen Werte `unknown`. Ein kostenloser oder pauschal abgerechneter
Dienst hat nur dann exakten Kostenwert 0, wenn ein Receipt fuer die
betrachteten Requests exakt 0 in der genannten Waehrung ausweist. Provider,
Modellfamilie und konkrete Modellversion werden je Usagezeile mitgefuehrt;
fehlende Modellversion bleibt `unknown`, nie still die Familienkennung.

## Spiel- und Performancewerte

T-053 aendert keine bestehende Benchmarkformel und bewertet kein neues
Hardwareprofil. Es importiert einen Wert nur aus einem schema-gueltigen,
commit-/hardware-/szenen-/seed-/warmup-/dauergebundenen Report und exportiert
dessen `methodId`. Werte unterschiedlicher `methodId`, Profile, Presets,
Szenen oder Seeds werden nicht aggregiert.

| ID | Einheit | exakte Definition |
|---|---|---|
| `PERF-FRAME-P50-MS`, `PERF-FRAME-P95-MS`, `PERF-FRAME-P99-MS` | ms | exakt der durch den bestehenden Benchmarkvertrag berichtete Perzentilwert samt `methodId`; fehlt Methode oder Bindung, `unknown` |
| `PERF-ONE-PCT-LOW-FPS` | fps | nur bei vorhandener Rohreihe: `1000 / arithmetic_mean(slowest ceil(0.01*N) frameTimeMs)`; bei N=0 oder nur Reportwert ohne belegte gleiche Methode `unknown` |
| `PERF-HITCH-COUNT` | count | Frames oberhalb eines vor Messstart im Eingabemanifest fixierten Schwellenwerts; ohne Schwelle `unknown` |
| `PERF-CPU-P50/P95/P99-MS`, `PERF-GPU-P50/P95/P99-MS` | ms | schema-gueltige Reportwerte der gebundenen Methode; nicht gemessene GPU-Zeit bleibt `unknown` |
| `PERF-RAM-RESIDENT-MAX-BYTES`, `PERF-RAM-PEAK-BYTES` | bytes | hoechster gemessener Wert innerhalb des gebundenen Messfensters |
| `PERF-VRAM-DIRECT-BYTES` | bytes | direkt gemessener Wert plus Methodenkennung; liegt nur eine Schaetzung vor, ist dieser Wert literal `unknown` |
| `PERF-VRAM-ESTIMATE-BYTES` | bytes | separat berichtete bestehende Schaetzung nur mit expliziter Methodenkennung; nie Ersatz fuer `PERF-VRAM-DIRECT-BYTES` und nie Gatebeleg |
| `PERF-LOAD-COLD-MS`, `PERF-LOAD-WARM-MS` | ms | Dauer der im Report gebundenen kalten bzw. warmen Ladedurchlaeufe; Cachezustand Pflicht |
| `PERF-DRAW-CALLS`, `PERF-TRIANGLES`, `PERF-VISIBLE-UNITS` | count | Reportwerte am festgelegten Stichprobenpunkt oder als eindeutig benanntes Maximum; Aggregationsart Pflicht |
| `PERF-POWER-W` | W | direktes Messgeraet-Ergebnis samt Geraet, Abtastrate und Fenster; TDP-/Softwaremodell `unknown` fuer diese Metrik |
| `PERF-BUDGET-RESULT` | enum | `pass` oder `fail` ausschliesslich aus dem bestehenden Budgetgate auf deklarierter Referenzklasse; Entwicklerhardware oder fehlendes Profil ergibt `unknown` |

Eine als Schaetzung gekennzeichnete VRAM-Methodik ist eine eigene beobachtete
Reportklasse, keine von T-053 erfundene Imputation. Sie wird nie mit direkter
Messung aggregiert und kann nur deskriptiv erscheinen. Fehlt ein direkter Wert,
bleibt der direkte Wert `unknown`; die Schaetzung ersetzt ihn nicht.

## Longitudinale SDLC-Chain-Metriken

Ein longitudinales Fenster besitzt `windowId`, eingefrorenes
`windowStartUtc`, exklusives `windowEndUtc`, genau eine Evidenzklasse und
optional genau eine Stratum-ID. Es enthaelt nur geschlossene Beobachtungen,
deren `observation.started.occurredAtUtc` im halb-offenen Fenster liegt.
Unbekannte Startzeit, gemischte Evidenzklasse oder nicht aufloesbare
Doppelzaehlung macht die betroffene Aggregation `unknown`. Innerhalb eines
Laufs werden Dauern aus monotoner Zeit berechnet; ueber Lauf-/Clockgrenzen
werden nur verifizierte UTC-Grenzen verwendet, sonst `unknown`.

### Autopilot und Agentenlaeufe

| ID | Einheit | exakte Definition |
|---|---|---|
| `AUTO-INSTANCES` | count | verschiedene `autopilotInstanceId` mit `autopilot.started` im Fenster |
| `AUTO-OBSERVED-SPAN-MS` | ms | Summe der gepaarten Start-bis-Stop-Intervalle; jede offene Instanz macht den Wert `unknown` |
| `AUTO-PAUSED-MS` | ms | Summe nicht ueberlappender `paused`-bis-`resumed`-Intervalle je Instanz; offene Pause `unknown` |
| `AUTO-BLOCKED-MS` | ms | Vereinigung aller Budget-/Rate-/Provider-/Infrastrukturblockintervalle innerhalb gepaarter Autopilotinstanzen |
| `AUTO-ACTIVE-MS` | ms | Summe der Schnittmenge gepaarter Autopilotspannen mit vollstaendig begrenzten `activityState=agent-active`-Intervallen; Idle und Sleeping werden nicht als aktiv gezaehlt |
| `AUTO-ACTIVE-RATIO` | ratio | `AUTO-ACTIVE-MS / AUTO-OBSERVED-SPAN-MS`; Nullnenner `unknown` |
| `RUN-STARTED` | count | verschiedene `runId` mit `agent.run.started` |
| `RUN-FINISHED` | count | gestartete Run-IDs mit genau einem passenden `agent.run.finished` |
| `RUN-FINISH-RATE` | ratio | `RUN-FINISHED / RUN-STARTED`; keine Aussage ueber Taskqualitaet |
| `RUN-CHILDREN` | count | Runs mit bekanntem `parentRunId` |
| `RUN-MAX-DEPTH` | count | groesste zyklenfreie Eltern-/Kindtiefe, Roottiefe 0; fehlende Elternreferenz oder Zyklus ergibt `unknown` |
| `RUN-ACCEPTED-OUTCOME-RATE` | ratio | Runs, die ueber aufloesbare Task-/Outcome-Beziehung zu `task.accepted` fuehren, geteilt durch gestartete Runs; ein Run darf im Nenner einmal vorkommen |

### Taskfluss und WIP

| ID | Einheit | exakte Definition |
|---|---|---|
| `TASK-PLANNED`, `TASK-READY`, `TASK-IMPLEMENTED`, `TASK-REVIEWED`, `TASK-REJECTED`, `TASK-ACCEPTED` | count | verschiedene Task-IDs mit dem jeweiligen belegten Lifecycle-Ereignis im Fenster |
| `TASK-PLAN-TO-READY-MS` | ms | Median der verifizierten UTC-Dauer von erstem `task.planned` bis erstem spaeteren `task.ready` je Task; fehlende Paare ausgeschlossen und als separate Unknownzahl berichtet |
| `TASK-READY-TO-IMPLEMENTED-MS` | ms | entsprechender Median `ready` bis `implemented` |
| `TASK-IMPLEMENTED-TO-REVIEWED-MS` | ms | entsprechender Median `implemented` bis `reviewed` |
| `TASK-READY-TO-ACCEPTED-MS` | ms | entsprechender Median `ready` bis `accepted` |
| `TASK-READY-TO-ACCEPT-RATE` | ratio | Tasks mit `ready` im Fenster und belegtem `accepted` bis Fensterende geteilt durch `TASK-READY`; keine spaetere Akzeptanz wird vorweggenommen |
| `TASK-REJECTION-RATE` | ratio | Tasks mit mindestens einem `task.rejected` geteilt durch Tasks mit mindestens einem `task.reviewed` |
| `WIP-SNAPSHOTS` | count | verschiedene `snapshotId`; jeder Datensatz muss `continuityOnly=true` tragen |
| `WIP-DISTINCT-TREES` | count | verschiedene aufloesbare `snapshotTreeId`; Commitanzahl oder Snapshothaeufigkeit ist kein Fortschritt |
| `WIP-PROMOTED-7D-RATE` | ratio | Snapshotbaeume, die binnen 7*24 Stunden nach Snapshotzeit durch `git.tree.promoted` exakt desselben Tree-IDs belegt sind, geteilt durch Snapshots mit vollstaendig beobachtbarem 7-Tage-Follow-up; unvollstaendiger Follow-up wird nicht als Misserfolg gezaehlt und separat unknown ausgewiesen |

Median ist bei ungerader Anzahl der mittlere sortierte Wert, bei gerader
Anzahl das arithmetische Mittel der beiden mittleren Werte, abwaerts auf ganze
Millisekunden gerundet. Die Zahl ausgeschlossener unbekannter Paare wird fuer
jede Lead-Time als `<ID>-UNKNOWN-PAIRS` exportiert.

### Pipelinefehler und Reparatur

| ID | Einheit | exakte Definition |
|---|---|---|
| `FAIL-BUILD`, `FAIL-TEST`, `FAIL-LINT`, `FAIL-SECURITY`, `FAIL-VERIFY` | count | Zahl der jeweiligen expliziten `*.failed`-Ereignisse, dedupliziert nach `stageId,attempt,targetTreeId` |
| `FAIL-UNIQUE-CLASSES` | count | verschiedene bekannte `failureClass`; unbekannte Klassen werden separat gezaehlt |
| `REPAIR-ATTEMPTED` | count | verschiedene `repairId` mit `repair.attempted` |
| `REPAIR-FIXED` | count | Reparaturen mit genau einem `repair.outcome.outcomeClass=fixed` und passender Verifikation auf `afterTreeId` |
| `REPAIR-SUCCESS-RATE` | ratio | `REPAIR-FIXED / REPAIR-ATTEMPTED`; offener Versuch ist nicht fixed |
| `REPAIR-ATTEMPTS-PER-FIX` | ratio | `REPAIR-ATTEMPTED / REPAIR-FIXED`; Nullnenner `unknown` |
| `REPAIR-TIME-TO-FIX-MS` | ms | Median der monotonen Dauer Attempt bis fixed Outcome derselben Clock-ID; sonst verifizierte UTC-Dauer, andernfalls `unknown` |
| `FAIL-RECURRENCE-RATE` | ratio | bekannte Fehlerklassen, die nach einem belegten fixed Outcome auf einem Nachfolgerbaum erneut auftreten, geteilt durch gefixte bekannte Fehlerklassen |

### Kontext, Routing und Modelle

| ID | Einheit | exakte Definition |
|---|---|---|
| `CONTEXT-COMPACTIONS` | count | verschiedene `compactionId` |
| `RUN-RESUMES` | count | verschiedene `run.resumed`-Ereignisse |
| `RESUME-CONTINUITY-RATE` | ratio | Resumes, deren `resumeFromEventId`, Task-ID, Run-/Parentbezug und `resumeStateSha256` aufloesbar konsistent sind, geteilt durch alle Resumes |
| `ROUTING-DECISIONS` | count | verschiedene `routingDecisionId` |
| `MODEL-SWITCHES` | count | verschiedene `model.switched`-Ereignisse, dedupliziert nach `runId,routingDecisionId,toModelId` |
| `MODEL-SWITCHES-PER-RUN` | ratio | `MODEL-SWITCHES / RUN-STARTED` |
| `MODEL-DWELL-MS` | ms | je Modell Summe gepaarter Switch-/Run-Grenzintervalle innerhalb derselben monotonen Uhr; fehlende Grenze ergibt fuer das Modell `unknown` |
| `ROUTING-OUTCOME-RATE` | ratio | Routingentscheidungen mit spaeterem belegtem Taskoutcome geteilt durch Routingentscheidungen; rein deskriptiv, keine Modellwirkung |

### Budget-, Rate-, Provider- und Infrastrukturblocker

| ID | Einheit | exakte Definition |
|---|---|---|
| `BLOCK-BUDGET`, `BLOCK-RATE`, `BLOCK-PROVIDER`, `BLOCK-INFRASTRUCTURE` | count | verschiedene `blockId` der jeweiligen Startart |
| `BLOCK-OPEN` | count | Block-IDs ohne passendes `block.resolved` bis Beobachtungs-/Fensterende |
| `BLOCK-DURATION-MS` | ms | Summe der Vereinigung aller vollstaendig gepaarten Blockintervalle; irgendein offener Block macht die Gesamtdauer `unknown` |
| `BLOCK-MEDIAN-RESOLUTION-MS` | ms | Median der verifizierten Dauer Start bis Resolution je gepaartem Block |
| `BLOCK-RESUME-RATE` | ratio | aufgeloeste Blocks mit aufloesbarem `resumedEventId` geteilt durch aufgeloeste Blocks |

Ein Budget- oder Rate-Limitwert wird nur aus dem gebundenen Receipt berichtet.
Der Block darf auch bei `observedLimit=unknown` als Ereignis zaehlen, wenn die
Blockwirkung selbst belegt ist.

### Git-Evolution und Promotion

| ID | Einheit | exakte Definition |
|---|---|---|
| `GIT-COMMITS` | count | verschiedene belegte Commit-IDs; keine Produktivitaetsmetrik |
| `GIT-DISTINCT-TREES` | count | verschiedene Commit-Tree-IDs |
| `GIT-PROMOTIONS` | count | verschiedene `git.tree.promoted`-Ereignisse |
| `GIT-ROLLBACKS` | count | verschiedene `git.rollback.observed`-Ereignisse |
| `GIT-SUPERSESSIONS` | count | verschiedene Paare `supersededCommit,supersedingCommit` |
| `PROMOTION-RATE` | ratio | akzeptierte Task-Tree-IDs mit passender `git.tree.promoted`-Evidenz geteilt durch akzeptierte Task-Tree-IDs |
| `ROLLBACK-PER-PROMOTION` | ratio | `GIT-ROLLBACKS / GIT-PROMOTIONS`; Nullnenner `unknown` |
| `IMPLEMENTED-TO-PROMOTED-MS` | ms | Median der verifizierten UTC-Dauer von `task.implemented` bis Promotion exakt desselben Tree-IDs |

Supersession ist kein Rollback, sofern kein explizites Rollbackereignis
vorliegt. Eine Refbewegung ohne belegte Autoritaet bleibt `unknown` als
Promotion.

### Menschliche Ereignisse

| ID | Einheit | exakte Definition |
|---|---|---|
| `HUMAN-INSTRUCTION`, `HUMAN-REVIEW`, `HUMAN-CORRECTION`, `HUMAN-APPROVAL`, `HUMAN-EMERGENCY`, `HUMAN-OBSERVATION` | count | verschiedene `humanActId` je Ereignistyp |
| `HUMAN-COUNTED-RATE` | ratio | menschliche Ereignisse mit `counted=true` geteilt durch alle menschlichen Ereignisse |
| `HUMAN-CORRECTIONS-PER-ACCEPTED` | ratio | `HUMAN-CORRECTION / TASK-ACCEPTED`; Nullnenner `unknown` |
| `HUMAN-EMERGENCY-RATE` | ratio | `HUMAN-EMERGENCY / AUTO-INSTANCES`; Nullnenner `unknown` |

Diese Typzaehler ersetzen nicht `INT-I0` bis `INT-I11`: Ereignistyp beschreibt
den menschlichen Akt, Intervention die Wirkung. `human.observation` kann
`counted=false` sein; eine darin enthaltene neue Direktive muss stattdessen
als eigenes gezaehltes Ereignis erfasst werden.

### Outcomes, Reviews und verworfene Arbeit

| ID | Einheit | exakte Definition |
|---|---|---|
| `OUTCOME-MILESTONES` | count | verschiedene autorisierte `milestoneId` aus `milestone.reached` |
| `OUTCOME-TAGS` | count | verschiedene aufloesbare `tagRef,targetCommit`; ein Tag ohne Milestoneklasse ist kein Milestone |
| `WINDOW-DAYS` | days | `(windowEndUtc-windowStartUtc)/86_400_000` aus den eingefrorenen UTC-Grenzen; nicht positive oder ungueltige/revidierte Grenze ergibt `unknown` |
| `ACCEPTED-OUTCOMES-PER-DAY` | tasks/day | `TASK-ACCEPTED / WINDOW-DAYS`; Zaehler und Nenner werden separat exportiert, Null-/unbekannter Nenner oder fehlender Pflicht-Architekturcheckpoint ergibt `unknown` |
| `MILESTONES-PER-DAY` | milestones/day | `OUTCOME-MILESTONES / WINDOW-DAYS`; Zaehler und Nenner werden separat exportiert, Null-/unbekannter Nenner ergibt `unknown` |
| `FILES-PER-ACCEPTED` | files/task | Summe der je akzeptierter Task zwischen gebundener Baseline und `acceptedTreeId` verschiedenen repo-relativen Pfade geteilt durch `TASK-ACCEPTED`; ueberlappende oder nicht eindeutig einer Task zurechenbare Deltas ergeben `unknown` |
| `LINES-PER-ACCEPTED` | lines/task | Summe des je akzeptierter Task eindeutig zugeordneten Bruttochurns `linesAdded+linesDeleted` geteilt durch `TASK-ACCEPTED`; Binaer- oder geteilte Deltas werden nicht geschaetzt und machen den Wert `unknown` |
| `REVIEW-FIRST-PASS-RATE` | ratio | Tasks, deren chronologisch erstes `task.reviewed` auf dem implementierten Tree den Verdict `pass` traegt, geteilt durch Tasks mit mindestens einem aufloesbaren Review |
| `DEFECT-ESCAPES` | count | verschiedene `defectId`, deren betroffener Baum formal akzeptiert war und deren gebundene Entdeckungszeit spaeter liegt |
| `DEFECT-ESCAPE-RATE` | ratio | akzeptierte Tasks mit mindestens einem Defect-Escape geteilt durch `TASK-ACCEPTED`; fehlendes vollstaendiges Defektregister ergibt `unknown`, nicht 0 |
| `REWORK-LINES` | lines | Summe `linesAdded+linesDeleted` sequenzieller Revisionsdeltas nach dem ersten `needs-work`/`block`/`reject`-Review bis Taskoutcome; ueberlappende Baseline-Diffs statt sequenzieller Deltas sind ungueltig |
| `REWORK-RATIO` | ratio | `REWORK-LINES / Summe(linesAdded+linesDeleted aller Taskrevisionen)` |
| `ROLLBACK-RATE` | ratio | akzeptierte Tasks mit spaeterem, auf ihren Tree gebundenem `git.rollback.observed` geteilt durch akzeptierte Tasks mit vollstaendigem Follow-up-Fenster |
| `ACCEPTED-STREAK-NO-HUMAN` | tasks | laengste Folge nach `task.accepted.occurredAtUtc,taskId`, deren jeweilige geschlossene Taskbeobachtung `INT-COUNT=0` hat; ein Task mit unbekannter Interventionvollstaendigkeit unterbricht die Folge |
| `WIP-SNAPSHOT-PER-ACCEPTED` | ratio | `WIP-SNAPSHOTS / TASK-ACCEPTED`; Kontinuitaetsaktivitaet, kein Fortschrittsscore |
| `WIP-TREE-ACCEPT-RATE` | ratio | verschiedene WIP-Tree-IDs, die innerhalb des vollstaendigen 7-Tage-Follow-up exakt als akzeptierter Tree belegt sind, geteilt durch WIP-Tree-IDs mit vollstaendigem Follow-up |
| `DISCARDED-TREES-7D` | count | Revisions-/WIP-Tree-IDs, die im vollstaendig beobachteten 7-Tage-Follow-up weder akzeptiert/promotet noch Vorfahr eines akzeptierten/promoteten Trees sind; unvollstaendiger Follow-up ist `unknown` |
| `DISCARDED-LINES-7D` | lines | sequenzieller Bruttochurn der eindeutig nur `DISCARDED-TREES-7D` zugeordneten Revisionen; gemeinsam genutzte Deltas machen die Metrik `unknown` |
| `GATE-RECOVERY-MS` | ms | Median von einem `gate.finished result=fail` bis zum ersten spaeteren `pass` desselben `gateId` auf demselben oder einem nachweislichen Nachfolgerbaum; monotone Uhr bevorzugt, sonst verifizierte UTC, ohne Recovery `unknown` |
| `GATE-RECOVERY-COST-EXACT:<failureEventId>` | ISO-currency | Summe aller dem Task/Run eindeutig zurechenbaren Kosten vom fehlgeschlagenen `gate.finished` inklusive bis zum ersten Recovery-Pass inklusive, nur `provider-reported` oder reproduzierbar `locally-calculated`, genau eine Waehrung und vollstaendige Receipts; jedes `estimated`/`unknown` oder eine offene Recovery ergibt `unknown` |
| `GATE-RECOVERY-COST-ESTIMATED:<failureEventId>` | ISO-currency | separate Summe ausschliesslich `costProvenance=estimated` im selben Recoveryintervall und derselben Waehrung; bei vollstaendig klassifiziertem Intervall ohne Estimated-Receipt exakt 0, bei fehlender Provenienz/Betrag oder offener Recovery `unknown`, nie Ersatz fuer die exakte Metrik |
| `PROD-TEST-CHANGE-RATIO` | ratio | Bruttochurn `linesAdded+linesDeleted` fuer Produktionspfade geteilt durch entsprechenden Churn fuer Testpfade gemaess eingefrorener Pfadkarte; Nullnenner `unknown` |

First-pass Review wertet nur den ersten aufloesbaren Review desselben
implementierten Trees. Ein spaeterer Pass schreibt den ersten Verdict nicht
um. Defect-Escape und Rollback benoetigen ein vorab eingefrorenes
Follow-up-Fenster; noch laufendes Follow-up wird nicht als Nullbefund gewertet.

### Usage- und Outcome-Effizienz

| ID | Einheit | exakte Definition |
|---|---|---|
| `USE-TOKENS-TOTAL` | tokens | Summe bekannter, nicht ueberlappender Input-, Output-, Cache-read- und Cache-write-Tokens; sobald ein einbezogenes Receipt Klassen ueberlappt oder unbekannt ist, `unknown` |
| `USE-TOKENS-PER-ACCEPTED` | tokens/task | `USE-TOKENS-TOTAL / TASK-ACCEPTED`; nur innerhalb einer einheitlichen Providerfeldsemantik, sonst `unknown` |
| `USE-COST-PER-ACCEPTED` | currency/task | `USE-COST-AMOUNT / TASK-ACCEPTED` innerhalb genau einer Waehrung |
| `USE-COST-PER-FIX` | currency/fix | `USE-COST-AMOUNT / REPAIR-FIXED` innerhalb genau einer Waehrung |
| `HUMAN-ACTIVE-MINUTES` | minutes | Summe direkt gemessener `humanActiveDurationMs / 60_000`; fehlt fuer irgendeinen gezaehlten menschlichen Akt die Dauer, ist die Summe `unknown`; Nachrichtenzahl, Antwortlatenz oder Walltime sind kein Ersatz |
| `HUMAN-MINUTES-PER-ACCEPTED` | minutes/task | `HUMAN-ACTIVE-MINUTES / TASK-ACCEPTED`; fehlende menschliche Dauern ergeben `unknown` |
| `PRODUCTIVE-AUTONOMY-MS` | ms | Vereinigung der Intervalle, in denen `autonomyMode=autonomous` und zugleich `activityState=agent-active` gilt, nur soweit jedes Intervall genau einer Task/Run-Kette zugeordnet ist, die spaeter innerhalb des gebundenen Follow-up formal `task.accepted` erreicht; geteilte, offene oder mehrdeutige Intervalle ergeben `unknown` und nie produktive Zeit |
| `PRODUCTIVE-AUTONOMY-MS-PER-ACCEPTED` | ms/task | `PRODUCTIVE-AUTONOMY-MS / TASK-ACCEPTED`; Nullnenner oder unbekannter Zaehler ergibt `unknown` |
| `ACCEPTED-PER-1M-TOKENS` | task/1M tokens | `TASK-ACCEPTED * 1_000_000 / USE-TOKENS-TOTAL`; kein Qualitaetsbeweis |
| `ACCEPTED-PER-AUTO-ACTIVE-HOUR` | task/hour | `TASK-ACCEPTED / (AUTO-ACTIVE-MS / 3_600_000)`; nur bei vollstaendiger Autopilotzeit |

Alle longitudinalen Raten und Pro-Outcome-Werte exportieren zusaetzlich den
exakt verwendeten Zaehler und Nenner als eigene Metrikzeilen, insbesondere
`WINDOW-DAYS=windowDurationMs/86_400_000`. Null- oder unbekannte Nenner ergeben
literal `unknown`; unbekannte Einheiten werden weder aus dem Nenner entfernt
noch als Nullerfolg behandelt. Kein Kosten-/Tokenwert wird zwischen Modellen, Providern
oder Waehrungen normalisiert, wenn die Receipts keine identische Semantik
belegen.

## Ablationsmetriken

Jede Ablation berichtet fuer jede Primaermetrik `controlValue`,
`ablationValue` und `delta`. Fuer boolesche Werte ist `delta` einer von
`same`, `true-to-false`, `false-to-true` oder `unknown`. Fuer Zahlen ist
`delta=ablation-control`; sobald ein Wert `unknown` ist, ist auch `delta`
literal `unknown`. Ergebnisse bleiben `synthetic-test-only`.
