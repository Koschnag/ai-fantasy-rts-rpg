# Soakvertrag (T-022, Abschnitt 0)

**Vertragsversion:** 2
**Status:** Durch den gatenden Vertragsspike des Auftrags
`.ai/tasks/T-022-deterministic-replay-soak.json` festgelegt, bevor die
Soakimplementierung (Gate, Reportvertrag, `soak-replay`-Lauf) erfolgte.
**V2 (2026-08-25):** Der Ausführungsvertrag wurde durch verbindliche
Projektleitungsentscheidung (`product-directive` vom 2026-08-25) ersetzt:
der gestartete autoritative Achtstunden-Realzeitlauf wurde absichtlich per
SIGTERM abgebrochen (Exitcode 143 nach 6 h 35 m Wanduhr, kein Report) und
darf nicht neu gestartet werden; NF-002-Evidenz entsteht nun aus dem
wiederholungsbasierten Evidenzmodell nach Abschnitt 4 mit ausgewiesenem
Restrisiko nach Abschnitt 6. Die Abschnitte 0 bis 3 (Kalibrierbasis,
Schwellwerte, Hangkriterium, Erfassungsmethoden) sind unverändert; V1 bleibt
in der Git-Historie als Rückrollweg konserviert.
Die maschinenlesbaren Kennungen sind in
`src/Riftward.App/Soak/SoakContract.cs` gespiegelt und werden von einem Test
gegen dieses Dokument gehalten.

Dieser Vertrag entscheidet NF-002 verfahrensmäßig im Rahmen der Spike-Klausel
(`docs/QUALITAET.md`): Jede Wahl nennt Alternativen, Gründe und Rückrollweg.
Er legt ausschließlich absolute Grenzwerte fest; die tolerierte
Benchmarkstreuung (Rest von Q-TEC-010) wird an keiner Stelle definiert,
berührt oder verbraucht, und die fensterweise Tickzeitdrift ist rein
diagnostisch ohne Gatekopplung. Budgetlinien aus `docs/PERFORMANCE_BUDGET.md`
dienen weiterhin nur als obere Grenzen und werden nicht geändert.

## 0. Messbasis (Kalibrierung)

Zwei unabhängige Realzeit-Kalibrierläufe auf dem Entwickler-PC
(i7-3770/RX 570-Klasse, Release-Build, linux-x64), je 1800 s Wanduhr bei
festem 20-Hz-Tick über die unveränderte Welt des Simulationsvertrags V1 mit
genau 250 vollständig simulierten Agenten, 60 Fenstern à 30 s:

| Kennzahl (Messmethode siehe Abschnitt 3) | Lauf A | Lauf B |
|---|---:|---:|
| Working Set erster/min/max/letzter Fensterwert (KiB) | 48720 / 48720 / 51620 / 50128 | 48716 / 48716 / 51552 / 50060 |
| Gesamtschwankung der Fensterserie (KiB) | 2900 | 2836 |
| Medianresiduum gegen lokalen Median nach Einschwingen (KiB) | 0 | 0 |
| Größte Auslenkung eines Einzelfensters gegen den lokalen Median (KiB) | 1424 | 1424 |
| Steigung erstes Drittel (KiB/h) | +7902 | +7607 |
| Steigung letztes Drittel (KiB/h) | 0 | 0 |
| Trend-Delta (letztes minus erstes Drittel, KiB/h) | −7902 | −7607 |
| größte Fortschrittslücke des Watchdogs (s) | 0,057 | 0,057 |
| GC-Pausen (Anzahl/Summe) | 0 / 0 ms | 0 / 0 ms |

Beide Läufe zeigen denselben Verlauf: einen einmaligen
Einschwingvorgang der Laufzeitumgebung in den ersten Minuten
(Spitze zu Spitze ≈ 2,8 MiB) und anschließend ein exakt fließendes Plateau;
kein fortlaufender Anstieg. Die Reports liegen als Laufartefakte
(`calibration-run-a.json`, `calibration-run-b.json`) im Abschlussrun; ihre
Kennzahlen sind hier die vertragliche Ableitungsbasis.

## 1. Speicherkennzahl und numerischer Leak-Schwellwert

**Speicherkennzahl:** Working Set des Prozesses (`VmRSS`,
`proc-self-status-vmrss-window-samples`), je Fenster ein Stichprobenwert,
erfasst allokationsfrei außerhalb der Allokationsklammern der Tickarbeit.

**Wahl, doppelte verpflichtende Form:**

1. **Absolutes Wachstumsziel:** `Endwert − Startwert` der Fensterserie
   eines jeden Evidenzlaufs darf **16 MiB (16384 KiB)** nicht überschreiten
   (`AbsoluteGrowthLimitMiB = 16`).
2. **Trendkriterium:** Die fensterbasierte Steigung (kleinste Quadrate über
   die Fensterwerte) des letzten Drittels minus die Steigung des ersten
   Drittels darf **1024 KiB/h** (`TrendLimitKiBPerHour = 1024`) nicht
   überschreiten. Negative Deltas gelten als erfüllt.
3. **Konsistenzbedingung:** `1024 KiB/h × 8 h = 8192 KiB = 8 MiB ≤ 16 MiB`
   ist eingehalten; das Gate verifiziert sie beim Start fail-closed.

**Implementierte Gate-Semantik (bewusste Verschärfung, keine Schwellwertänderung):**
Das Gate wertet nicht messbare Kennzahlen (beispielsweise ein unlesbares
`/proc/self/status`) sowie ein negatives absolutes Wachstum fail-closed als
Verletzung; nur ein Wachstum von null bis einschließlich der Schwelle besteht.
Negative Trenddeltas gelten weiterhin gemäß Absatz 2 als erfüllt. Verschärfungen
sind laut Auftrag jederzeit zulässig; die hier dokumentierte Semantik ist Teil
dieser Vertragsversion V1.

**Ableitung als Vielfaches des beobachteten Messrauschens:** Der absolute
Wert entspricht rund dem 5,6-fachen der größten beobachteten
Gesamtschwankung (2,83 MiB) und dem 11,5-fachen der größten
Einzelfensterauslenkung (1,39 MiB) über beide Läufe. Der Trendgrenzwert
liegt oberhalb jeder beobachteten Plateau-Schwankung (Slope-Rauschen im
eingeschwungenen Zustand: 0 KiB/h) und unterhalb des durch die
Konsistenzbedingung erlaubten Maximums von 2048 KiB/h; er würde jeden
fortschreitenden Verlust, der schneller als 1 MiB/h zunimmt, innerhalb der
Laufzeit gegen beide Formen fassen.

**Alternativen:**

| Alternative | Ablehnungsgrund |
|---|---|
| 4 MiB absolut (untere Kapselgrenze als Wert) | Weniger als das Doppelte der beobachteten legitimen Einschwingamplitude (2,83 MiB); Risiko systematischer Fehlalarme auf der Zielhardwareklasse ohne Mehrwert für Leakdetektion. |
| 64 MiB absolut (obere Kapselgrenze als Wert) | Erlaubte fortschreitende Verluste bis zur Größenordnung ganzer Simulationsstrukturen; verschärft weder etwas noch spiegelt es das gemessene Rauschen. |
| Trendgrenzwert 2048 KiB/h (Maximum der Konsistenzbedingung) | Kein Vielfaches des beobachteten Rauschens, sondern exhaustedes Konsistenzmaximum; verwässert das Trendkriterium gegenüber der gewählten Form ohne messbaren Vorteil. |
| Managed-Heap-Größe statt Working Set als Kennzahl | `GC.GetGCMemoryInfo` misst nur verwaltete Sichtbarkeit; Lecks über nicht freigegebene native/Pinned-Anteile blieben unsichtbar. Working Set ist die strengere prozessuale Größe. |

**Rückrollweg:** Änderung eines Schwellwerts ausschließlich als neue
Vertragsversion dieses Dokuments mit neuer Kalibrierbasis; jede Lockerung
gegenüber den hier abgeleiteten Werten eskaliert an die Projektleitung
(Auftragsentscheidungspolitik). Verschärfungen bleiben jederzeit erlaubt.

## 2. Hangkriterium (Fortschritts-Watchdog)

**Wahl:** Watchdogfenster **120 Sekunden**
(`WatchdogWindowSeconds = 120`). Beobachtet wird der Tickindex des
Simulationskerns; liegt länger als das Fenster kein Fortschritt vor, bricht
der Lauf mit definierter Gateverletzung ab (Exitcode 30, Report trotzdem,
klar als nicht bestanden markiert). Beobachtet wird außerhalb des
Heisspfads; die Striktheitsregel lautet „strikt jenseits des Fensters“.

**Ableitung:** Die größte beobachtete Fortschrittslücke lag bei 0,057 s
(einer Tickperiode entsprechend). Das gewählte Fenster ist rund das
2100-fache dieser Beobachtung, liegt mitten im Auftragband von 30 bis
300 Sekunden und toleriert selbst mehrminütige Scheduler-/Speicherdruck­
phasen des Betriebssystems, ohne einen echten Hänger zu verschweigen.

**Alternative:** 300 s (Bandoberkante) – halbiert die Empfindlichkeit ohne
messbaren Vorteil, da selbst extreme Systemlast in der Kalibrierung um
Größenordnungen unterhalb jedes Bandwerts blieb. **Rückrollweg** wie
Abschnitt 1.

## 3. Erfassungsmethoden (bindend für Report und Gate)

- Working Set: `/proc/self/status` (`VmRSS`), ein Wert je Fenster,
  allokationsfreier Sampler, gelesen nach dem Allokationszähler des
  Fensters („Erfassung außerhalb der Heisspfadfenster“).
- Verwaltete Allokationen des Soakgates: exakt die Methode des
  Simulationsvertrags V1 Abschnitt 5
  (`gc-total-allocated-bytes-precise-delta-per-tick-sum`): je Tick enge
  Klammern um `world.Tick()`, summiert über vertraglich fixierte
  Prüfintervalle (erste 1200 Messsticks sowie je Stundenbeginn 1200
  Messsticks). Die gröberen Fensterdeltas des Laufes werden zusätzlich als
  Telemetrie berichtet, gehen aber nicht in dieses Gate ein.
- Zustands-Hashketten: `fnv1a64-canonical-chain-v1` gemäß
  `docs/SIMULATIONSVERTRAG.md`; Stichproben bei Tick 0, dann alle
  36000 Ticks (≙ 30 Minuten), plus Endtick; Vergleich byteidentisch gegen
  die versionierte Golden-Fixture aus einem unabhängigen Referenzlauf.
- GC-Pausen: `GC.GetTotalPauseDuration`-Delta plus Sammlungszähler-Delta.
- Fensterweise Tickzeit-p50/p95/p99 (Anfang/Mitte/Ende): rein
  diagnostische Felder ohne Gatekopplung; maschinenlesbar als solche
  markiert (`gateCoupled=false`).
- Headless nicht anwendbare Kennzahlen (GPU-Zeit, Draw-/Submit-Aufrufe,
  sichtbare Dreiecke) sind ausschließlich unavailable mit Grund.

## 4. Ausführungsvertrag (V2: wiederholungsbasiertes Evidenzmodell)

**Evidenzmodell (Projektleitungsentscheidung 2026-08-25):** Der NF-002-
Nachweis besteht aus einem Bündel von mindestens **drei unabhängigen
Fresh-Prozess-Wiederholungsläufen**
(`MinimumEvidenceRepetitions = 3`) über den **kompletten skriptierten
Planhorizont von 576000 Messsticks plus 480 Warm-up-Ticks**, identischem
Vertragssseed, unveränderter Welt und genau 250 vollständig simulierten
Agenten in Release-naher Buildkonfiguration. Die Taktung der Wiederholungen
darf beschleunigt sein, weil die Pacing-Unabhängigkeit durch identische
Präfixketten zwischen getaktetem und beschleunigtem Lauf im regelmäßigen
Testlauf belegt ist; die Wanduhrdauer eines Einzelprozesses ist kein
Evidenzkriterium mehr.

Jeder Lauf ist genau dann eine maschinenlesbare **Evidenzeinheit**
(`evidenceUnitId = deterministic-full-plan-repetition-v2`,
Reportfeld `execution.evidenceUnit = true`), wenn er

1. den kompletten Planhorizont ohne Verkürzung abdeckt,
2. vollständig ist (volle Tickanzahl, kein Watchdog-Stall),
3. in Release-naher Konfiguration ausgeführt wurde,
4. im Modus `accelerated-repetition-evidence-v2` läuft,
5. byteidentische Kettenstichproben gegen eine **bereits bestehende**,
   versionierte Golden-Fixture zeigt, dabei den kanonischen Stichprobenplan
   lückenlos (keine übersprungenen oder abweichenden Stichproben) abdeckt und
   denselben deterministisch nachgerechneten Befehlsplanhash bindet, und
6. das fail-closed Gate gegen alle Abschnitt-0-Grenzwerte besteht.

Horizontverkürzte, Debug-, unvollständige oder kettenverletzende Läufe sowie
jede Golden-Fixture-Emission werden mit maschinenlesbarem Grund als keine
Evidenzeinheit abgewiesen. Ein Lauf mit `--reference-out` erzeugt den
Vergleichsanker erst selbst und kann sich deshalb niemals selbst bestätigen;
er trägt unabhängig vom Horizont den Modus
`accelerated-reference-emission-diagnostic-v1` und den Ablehnungsgrund
`golden-fixture-reference-emission-diagnostic`.
Das Bündel gilt nur mit mindestens drei bestandenen Evidenzeinheiten auf
derselben Maschine, demselben Commit und derselben Fixture als NF-002-
Nachweis; der aggregierende Nachweisbericht verlinkt die Einzelreports.

- **Realzeit-Modus (Standard ohne Diagnoseflag):**
  bleibt verfügbar, ist unter V2 aber ein diagnostischer Dauermodule
  (mindestens 8 Stunden Wanduhr bei festem 20-Hz-Tick); er ist keine
  Abnahmevoraussetzung mehr. Der frühere autoritative Achtstundenlauf wurde
  absichtlich per SIGTERM abgebrochen (6 h 35 m, Exitcode 143, kein Report)
  und darf nicht neu gestartet werden (Abschnitt 6).
- **Beschleunigte Wiederholung (`--diagnostic-accelerated`, voller
  Horizont):** Evidenzmodus nach diesem Vertrag
  (`executionModeId = accelerated-repetition-evidence-v2`).
- **Beschleunigter Kurzlauf (`--diagnostic-accelerated
  --horizon-ticks N`):** ausschließlich diagnostisch
  (`accelerated-diagnostic-v1`), niemals Evidenzeinheit.
- **Referenzemission (`--diagnostic-accelerated --reference-out PFAD`):**
  ausschließlich diagnostisch
  (`accelerated-reference-emission-diagnostic-v1`), auch bei vollständigem
  Planhorizont niemals Evidenzeinheit. Erst ein separater Fresh-Prozess-Lauf
  darf gegen die versionierte und geprüfte Fixture vergleichen.
- **Anker unveraenderlich:** Budgetlinien aus `PERFORMANCE_BUDGET.md`
  dienen nur als obere Grenzen, niemals als Soak-Erlaubnis; die
  Allokationsgrenze je warmem Tick bleibt unverändert an Simulationsvertrag
  V1 Abschnitt 5 (0 Bytes, Methode wie Abschnitt 3) gebunden. Verschärfung
  jederzeit erlaubt; jede Lockerung eskaliert an die Projektleitung.
- **Q-TEC-010:** die tolerierte Benchmarkstreuung wird weder definiert
  noch verbraucht; maschinenlesbare Kennung:
  `tolerated-benchmark-variance-qtec010-remains-open-not-defined-not-consumed-in-this-task`.
- **Profilbindung:** analog T-020/T-021/T-023 (Q-OPS-001-Klärungsprotokoll):
  Entwickler-PC-Läufe sind diagnostische Baseline; Pflichtprofile bleiben
  ohne benannte Referenzhardware `NOT-MEASURED`. Dieser Vertrag begründet
  keinerlei Optimierungs- oder Performancebehauptung (ADR 006).

**Alternativen zur V2-Entscheidung:**

| Alternative | Ablehnungsgrund |
|---|---|
| Fortsetzung/Neustart des Achtstunden-Realzeitlaufs | Durch Projektleitungsentscheidung 2026-08-25 ausdrücklich untersagt; ein erneuter Block des Arbeitsbaums um 8 h Wanduhr mit unverändertem Abbruchrisiko (externes SIGTERM, Systemstandby) steht in keinem Verhältnis zum Erkenntnisgewinn gegenüber dem Wiederholungsbündel. |
| Ein einziger beschleunigter Vollhorizontlauf | Kein Wiederholungsnachweis: Prozessstart-, Zeitplanungs- und GC-Zufälle blieben unbeobachtet; die Mindestanzahl drei fängt Streuung zwischen Prozessen ein. |
| Checkpoint/Resume oder shardweise Verteilung | Schafft Wiederaufnahmesemantik und damit Evidenzlücken; bleibt eskalationspflichtig. |

**Rückrollweg:** Rückkehr zu V1 (Einzelprozess-Achtstundenmodus)
ausschließlich durch neue Projektleitungsentscheidung und neue Vertrags-
version dieses Dokuments; V1 liegt in der Git-Historie vor.

## 5. Geltungsbereich

Dieser Vertrag beschreibt ausschließlich den Zuverlässigkeitsnachweis
NF-002 des Szenarios `soak-replay` über die unveränderte Baselinewelt des
Simulationsvertrags V1. Er begründet keine fachlichen Spielregeln, kein
Save-/Replayformat und keine Aussagen über Cross-Plattform-Determinismus
oder Zielhardwareleistung.

## 6. Restrisiko und Abbruchdokumentation (V2)

**Ausgewiesenes Restrisiko:** Das Wiederholungsbündel nach Abschnitt 4
weist Determinismus, Speicherverhalten, Allokationsfreiheit warmer Ticks,
Watchdog-Fortschritt und Kettenintegrität über den vollständigen
Planhorizont nach — nicht jedoch ein zusammenhängendes Überleben von
8 Stunden Echtzeit. Ausdrücklich nicht abgedeckt und als akzeptiertes
Restrisiko ausgewiesen sind: langandauernde Betriebssystem-/Scheduler-
Einflüsse (etwa minutenlange Entzug von CPU-Zeit), externe Eingriffe in den
Prozess (SIGSTOP/SIGTERM, Systemstandby), thermische oder Hardwareereignisse
sowie Memory-Fragmentierung, die erst nach vielen Echtzeitstunden sichtbar
würde. Gegen einen echten Hänger schützt weiterhin der Watchdog mit dem
Abschnitt-2-Fenster (120 Sekunden, beobachtete Kalibrierlücke 0,057 s);
gegen fortschreitenden Speicherverlust die Abschnitt-1-Schwellwerte.

**Abgebrochener Realzeitlauf (Diagnosebeobachtung):** Der am 2026-08-25
gestartete Achtstunden-Realzeitlauf wurde nach 6 h 35 m Wanduhr absichtlich
per SIGTERM abgebrochen (`artifacts/t022/soak-authoritative-status.log`:
Exitcode 143; kein Report, kein Logausstoß). Projektleitungsentscheidung:
Der Lauf darf nicht neu gestartet werden; die Beobachtung gilt ausschließlich
als partielle Diagnoseevidenz dafür, dass der Prozess bis zum Abbruch nicht
von selbst endete, niemals als PASS und niemals als NF-002-Nachweis.
