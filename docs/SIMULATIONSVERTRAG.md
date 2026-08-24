# Simulationsvertrag (T-021, Abschnitt 0)

**Vertragsversion:** 1
**Status:** Durch den gatenden Vertragsspike des Auftrags
`.ai/tasks/T-021-headless-simulation-baseline.json` festgelegt; die
maschinenlesbaren Kennungen sind in `src/Riftward.Simulation/SimulationContract.cs`
gespiegelt und werden von einem Test gegen dieses Dokument gehalten.

Dieser Vertrag entscheidet die Blocker Q-TEC-004/Q-TEC-005 verfahrensmäßig im
Rahmen der Spike-Klausel (`docs/QUALITAET.md`, Definition of Ready): Jede Wahl
nennt Alternativen, Gründe und Rückrollweg. Die Endwerte bleiben gemäß
Klärungsprotokoll Simulation-Lead-Entscheidung im Spike; dieser Lauf ist der
Spike. Eine exakte plattformübergreifende Hashgarantie wird an keiner Stelle
behauptet; tolerante Abweichungen werden nicht erfunden.

## 1. Numerikmodell

**Wahl:** Reine Ganzzahl-Festkommaarithmetik Q16.16
(`q16-16-fixed-point-intonly-v1`). Alle simrelevanten Zustandsübergänge
verwenden ausschließlich 32-/64-Bit-Ganzzahloperationen (Addition,
Subtraktion, Multiplikation mit nachgelagerter Shift-Skalierung, Division,
ganzzahlige Newton-Wurzel). Fließkommatypen kommen im Simulationskern nicht
vor.

**Erfüllte fixierte Kriterien:** bitidentischer Zustands-Hash bei identischen
Eingaben im selben Binary (Ganzzahlsemantik ist in .NET vollständig
spezifiziert und ohne Optimierungsfreiheiten); keine Abhängigkeit von
undefinierter Fließkommasemantik oder Compilermustern; keine ISA-Anforderung
oberhalb x86-64-v2 (kein SIMD-Pflichtpfad); AOT-/Trimming-freundlich ohne
Reflection; keine libm-Bindung.

**Alternativen:**

| Alternative | Ablehnungsgrund |
|---|---|
| Kontrolliertes Fließkomma | Benötigt disziplinierte Ausdrucksordnung, Verbot von FMA-/Vektorisierungsmustern und Buildprüfung je Compilerstand; Restrisiko bitabweichender Codegenerierung zwischen Builds bleibt vertraglich schwer zu begrenzen. Für die Bewegungsbaseline entsteht kein Genauigkeitsvorteil. |
| Hybrid (Festkomma-Zustand, Fließkomma nur außerhalb des Hashs) | Spaltet den Zustand in einen gehashten und einen wirksamen Anteil; jede Grenzverschiebung wird zur stillen Vertragsänderung. Für diese Baseline ohne Darstellungspfad nicht nötig. |

**Rückrollweg:** Austausch des Numerikmodells in
`Riftward.Simulation` samt Neubau und Regeneration aller Golden-Fixtures;
der Vertragstext erhält eine neue Vertragsversion, alte Fixtures bleiben als
historische Evidenz erhalten.

## 2. Hashvertragsklassen

Algorithmus: FNV-1a 64 Bit über den kanonischen Relevantzustand in fester
Feldordnung (`fnv1a64-canonical-chain-v1`); Kettenregel
`chain_n = FNV(chain_{n-1}, stateHash(tick_n))`.

| Klasse | Bedingung | Garantie |
|---|---|---|
| K1 | gleicher Prozess, gleicher Startzustand, gleiche geordnete Befehlsfolge | identische Hashketten |
| K2 | zusätzlicher Fresh-Prozess auf demselben gepinnten Binary (gleicher Commit, Release-nahe Konfiguration) | identische Hashketten |
| K3 | Cross-Build- oder Cross-Plattform-Gleichheit | **nicht behauptet**; erst nach echter Cross-Plattform-Messung Gegenstand einer eigenen Entscheidung |

Der Report bindet Start-, Intervall- und Endhash der Kette; die
Determinismusevidenz besteht aus zwei unabhängigen Fresh-Prozesläufen
(K2). Ein K3-Nachweis ist in diesem Auftrag ausdrücklich out of scope.

## 3. Seedableitung und kanonische Ordnung

- **Seedableitung:** Der Szenario-Seed (32 Bit) wird per SplitMix64 gestreut
  und treibt einen Xorshift64\*-Generator (`SimRandom`). Es existiert kein
  weiterer Zufallsbeitrag; Uhr, Threadzahl, Dateisystemreihenfolge und
  Umgebungsdaten fließen nie in den Zustand.
- **Befehlsordnung:** Befehle eines Ticks werden vor der Anwendung kanonisch
  sortiert `(Tick, ScopeGroup, Kind, ZoneIndex)`; die Eingabereihenfolge
  bestimmt niemals das Ergebnis (Negativfixtures prüfen dies).
- **Iterationsordnung:** Agenten aufsteigend nach Index; Kachelnachbarn in
  fester Reihenfolge N,O,S,W; Blockgraph-Nachbarn in derselben Reihenfolge;
  Nachbarschaftsbuckets in Zeilen-, dann Spaltenreihenfolge, innerhalb eines
  Buckets aufsteigende Agentenindizes (Zählsortierung). Keine
  Hashtabelleniteration im Heisspfad.
- **Befehlsplan:** Deterministisches Gruppenskript
  (`xorshift64star-group-script-v1`): alle
  <code>IntervalTicks = 300</code> Ticks ab Tick 240 erhält jede der fünf
  Gruppen deterministisch ein neues Zielgebiet ungleich dem aktuellen; der
  Planhash bindet jede Little-Endian-Kodierung kanonisch.

**Alternative:** Sortierung durch Vergleichsläufe beider Ordnungen wurde
abgelehnt, weil sie keine vertragliche Garantie, nur eine Beobachtung liefert.
**Rückrollweg:** Änderung des Ordnungsschemas erfordert neue
Vertragsversion und Fixture-Regeneration wie Abschnitt 1.

## 4. Welt-, Agenten-, Navigations- und Schedulingstruktur

- **Welt** (`riftward-simworld-graybox-v1`): 160×90-Kachelraster (1 m/Kachel),
  Randmauern, zwei Wandreihen mit je drei Toren, vier Rechteckblockaden; sechs
  rechteckige Zielgebiete (Zonen). Geometrie seedunabhängig fixiert und beim
  Prozessaufbau auf Zonenbegehbarkeit validiert. Kein Spielinhalt, keine
  kreativen Assets, keine Fremdbezüge (Clean-Room).
- **Agenten:** genau 250 gleichzeitig vollständig simulierte mobile
  Testagenten (Structure-of-Arrays: Position, Geschwindigkeit, Zielkachel,
  Pfadstatus, geplante Zone, Wegpunktpuffer je Agent). Fortbewegung über
  Wegpunktfolge, ganzzahliger Abstandsdruck im 2-m-Bucket-Umfeld als
  Ausweichverhalten, achsenweise Kollisionsauflösung. Zwei Geschwindigkeits-
  klassen (gerade/ungerade Agentenindices).
- **Hierarchische Pfadsuche:** obere Ebene Blockgraph (10×10-Kachelbloecke,
  Kanten an beidseitig begehbaren Grenzpaaren, Breitensuche); untere Ebene
  korridorbeschränkte Breitensuche auf Kachelebene entlang der groben Route.
- **Budgetierung über mehrere Ticks:** je Agent und Dienstabschnitt maximal
  768 Knotenerweiterungen, global je Tick maximal 2048; Anfragen werden in
  aufsteigender Agentenreihenfolge bedient. Erschöpft der Haushalt eine
  Feinsuche, wird der beste erreichte Teilpfad gefahren („best effort“) und
  die Route ab neuer Position erneut angefragt; unerreichbare Ziele erkennt
  die Grobsuche vollständig innerhalb eines Abschnitts
  (≤ 144 Blöcke ≤ 768). Transiente Sucharbeitsplätze tragen keine Serial
  über Tickgrenzen und gehören daher nicht zum Relevantzustand.
- **Taktung:** fester 20-Hz-Tick (dt = 50 ms), vom späteren Rendering
  entkoppelt; lesbarer Snapshot als Kopie außerhalb des Heisspfads.

**Alternativen:** A\*/Dijkstra mit Prioritätswarteschlange (teure, heapbasierte
Ordnung ohne Vorteil für diese Baseline), fertige Frameworks (neue
Abhängigkeit, vertraglich ausgeschlossen), vollständiges Flowfield je Zone
(Speicheraufwand ohne Baselinemehrwert). **Rückrollweg:** Strukturaustausch
innerhalb derselben Grenzen und Verträge; Abnahmetests bleiben maßgeblich.

## 5. Allokationsgrenze je warmem Tick

**Wahl:** 0 verwaltete Bytes je warmem Tick
(`AllocationLimitBytesPerWarmTick = 0`). Damit wird die Arbeitsannahme
„nahe null“ aus `docs/PERFORMANCE_BUDGET.md` verschärft und die Auftragsober-
grenze von 1 KiB deutlich unterschritten (Verschärfung laut Auftrag erlaubt,
bis hinunter zu null).

**Messbasis (Abschnitt-0-Spike, Entwickler-PC i7-3770-Klasse, Release):**
`GC.GetTotalAllocatedBytes(precise: true)` je Tick einzeln erfasst und
summiert; 1200 Messsticks nach 480 Warm-up-Ticks ergaben exakt 0 Bytes
Gesamtallokation, 0 GC-Pausen, p99-Tickzeit 0,449 ms. Die Erfassungsmethode
ist im Report als `gc-total-allocated-bytes-precise-delta-per-tick-sum`
angegeben; Telemetriezugriffe (RSS-Stichproben, Hashketten-Stichproben)
liegen bewusst außerhalb der je-Tick-Messfenster und sind methodisch vom
Simulationskern getrennt.

**Alternative:** 1 KiB-Obergrenze des Auftrags als Gatewert (abgelehnt als
unnötig weich, da messbar null erreichbar ist). **Rückrollweg:** Anhebung
ausschließlich als dokumentierte Entscheidung mit neuem Messprofil; jede
Lockerung gegenüber „nahe null“ eskaliert an die Projektleitung
(Q-TEC-010-Präzedenz).

## 6. Tickzeitbudget

Unverändert aus `docs/PERFORMANCE_BUDGET.md`: Ziel 8 ms, harte Grenze 16 ms
bei 20 Hz. Das Budgetgate entscheidet fail-closed ausschließlich gegen diese
Werte plus Abschnitt 5; das 8-ms-Ziel wird im Report ausgewiesen, seine
Verfehlung allein faltet das Gate jedoch nicht (harte Grenze maßgeblich),
wie in AC-T010-07/T-020 präzedenziert.

## 7. Geltungsbereich

Dieser Vertrag beschreibt ausschließlich die isolierte headless
Simulationsbaseline (`bench-sim`) mit abstrakten Graybox-Bewegungsagenten.
Er begründet keine fachlichen Spielregeln, kein Save-/Replayformat und keine
Cross-Plattform-Aussagen (DATENMODELL-OFFEN-Stellen und T-011/T-031 bleiben
unberührt).
