# Abnahme T-032 – Interaktive Graybox-Kommandoschleife

**Status:** Implementierung durch den Builder-Lauf
`01M0Y8GA8T1H16TVPMV06QVKWY` (Akteur `t032-builder`) geliefert und durch den
unabhängigen Review-/Vollendungslauf `01M0YJE3T8FDQ3T9QWJJY4DTNW`
(Akteur `t032-review-completion`) geprüft, vollendet und auf `accepted`
gesetzt. Alle lokalen Pflichtgates wurden von der Review-Sitzung eigenständig
ausgeführt; keine Erfolgsbehauptung des Builder-Laufs wurde ungeprüft
übernommen. Verbleibende Restpunkte sind unten explizit genannt.

## Gelieferter Umfang

- **Abschnitt 0 (gatend, vor der Implementierung):**
  `docs/KOMMANDOVERTRAG.md` V1 mit Intent-zu-Befehl-Abbildung auf die
  unveraenderte Kernbefehlsflaeche (`SimCommandKind.GroupMoveToZone`,
  kanonische Ordnung), Auswahl-/Kameramodell V0 als vorregistrierte
  Hypothesen mit Alternativen, Gruenden, Playtestkriterien und Rueckrollweg,
  Diagnoseformat `graybox-input-script-v1`, Reaktionsableitung
  (150 ms ÷ 50 ms = 3 hart; 100 ms ÷ 50 ms = 2 Ziel), Gatematrix mit
  ausschliesslich absoluten fail-closed Grenzwerten sowie Exitcodes 35–38.
- **Neues BCL-only Runtimeprojekt `Riftward.Session`:**
  Vertragsspiegel (`SessionContract.cs`), Intents mit kanonischer Ordnung und
  Festbreiten-Planhash (FNV-1a-64), strenger Einzelpass-Skriptparser mit zehn
  unterscheidbaren Ablehnungsklassen, Auswahlmodell V0, Kamerazustand V0,
  fail-closed `CommandGate`, deterministische `SessionPipeline`/`SessionEngine`
  (Messfenster nach T-021-Methode, Effektsnapshot-Definition nach Vertrag §6,
  Selbstkonsistenzpass als K2-Anker).
- **Befehl `./scripts/rift.sh kommandoschleife`:** headless nativ linux-x64
  rein CPU-seitig ohne Laden der nativen Artefakte; fensterpflichtiger
  Interaktivmodus auf demselben Pipelinepfad (SDL3-Maus-/Tastaturereignisse
  gegen gepinnte Strukturoffsets verifiziert, Grayboxdarstellung nach
  T-023-Rendermustern, Zweikanal-Rueckmeldung Form+Farbe gemaess NF-005,
  geclippte Kamera, Beenden per Keymap, opt-in Einzelabgriff strikt nach dem
  Messfenster mit Aussagegrenze Graybox-Zustandsbelegung).
- **Report Schemaversion 1** nach NF-007: Skript-/Planhashbindung,
  Zustands-Hashkette, Tickzeit-, Allokations- und Reaktionsticksfelder mit
  Einheit/Methode, `gateCoupled=false`-Marken auf allen Diagnosefeldern,
  maschinenlesbare Offenheit von Q-TEC-010 und Q-GAM/Q-NAR-Fragen,
  Profil-Ehrlichkeit (`NOT-MEASURED`), Abgriffbindung.
- **Exitcodes 35–38** dokumentiert (`docs/NATIVE_UNTERBAU.md`) und durch
  zwei Tests gebunden; bestehende Bedeutungen bis 34 unveraendert.
- **Dokumentation:** AUTOMATION.md (Befehlsvertrag), ARCHITEKTUR.md
  (Sitzungsmodus in der Laufzeitlinie, Vertrauensgrenzenze Eingabeskript),
  PERFORMANCE_BUDGET.md (Nachweisortnotiz Eingabe-zu-Reaktion, kein Wertwechsel),
  `.ai/evals/quality-gates.json` (G-PERF-Praezision). GAME_DESIGN.md unberuehrt.

## Kriterien und Evidenz

| Kriterium | Stand | Evidenz (Run `01M0Y8GA8T1H16TVPMV06QVKWY`) |
|---|---|---|
| AC-T032-01 | erfuellt | Vertragsdokument vor Implementierung erstellt; Spiegeltest `sessionContractMirrorsDocumentedValues`; Evidenz ac01 |
| AC-T032-02 | erfuellt | Realer headless Lauf Exit 0 mit Report; Positiv-/Negativfaelle im CLI-Test (37 ohne Report, 28, 27, 19); ac02 |
| AC-T032-03 | erfuellt | Engine-Duallauf identische Ketten/Endhash; Fremdseed-/Skriptmutation aendern Endhash; Tick-Umsortierung invariant; Review: zwei Fresh-Prozesslaeufe Endhash `978aab19406daa26` builderidentisch, Fremdseed `76eee99a2f05629c`; Archivlauf identisch; ac03 |
| AC-T032-04 | erfuellt (Restpunkt 1) | Semantiktests Punkt/Box/Clear + Kernabbildung + kontrollierte Abweisungen; displayloser Interaktivlauf Exit 19 ohne Report durch Review selbst belegt (mit und ohne Compositor-Socket); ac04 |
| AC-T032-05 | erfuellt | Review-Lauf: p99 0,961 ms ≤ 16, Allokation 0 Bytes, max reactionTicks 1 ≤ 3, Shaderkompilierung 0, Kettenkriterium ausgewertet wahr; Fault-Injection-Matrix faellt jede Klasse; ac05 |
| AC-T032-06 | erfuellt im Code-/Vertragsumfang, visueller Smoke offen (Restpunkt 1) | Derselbe Pipelinepfad, Zwei-Kanal-Rueckmeldung strukturgeprüft, Capture-Policy schema- und testgebunden; displayloser Abbruch belegt statt simuliertem Smoke; menschliche Sichtpruefung und opt-in Abgriff bleiben Displaysession vorbehalten; ac06 |
| AC-T032-07 | erfuellt | Zehn Parser-Ablehnungsklassen, Hermetietest, security PASS findings=0, Vertrauensgrenzenze dokumentiert; ac07 |
| AC-T032-08 | erfuellt | Architekturtests (Session rein C#/BCL-only, Referenzgrenzen, Keymap-Validierung); Riftward.Simulation blobidentisch (`git diff HEAD -- src/Riftward.Simulation/` leer); ac08 |
| AC-T032-09 | erfuellt | Builder: fmt/lint/build/test/security/rag-build/verify PASS; Review wiederholt alle Gates nach den Reparaturen mit 236/236; Regressionen bench-sim 0, savecheck 0, soak-Kurzlauft diagnostisch 0; Archivfolge ebenfalls vollstaendig gruen; ac09 |
| AC-T032-10 | erfuellt | Dokumentationsstand konsistent (inkl. `stateChainSelfConsistency`-Ausweis in NATIVE_UNTERBAU.md); quality-gates.json praezisiert und Satztrennung repariert (jq-valide); Abgriff produziert → Media-Lab-Eintrag entfaellt, weil in beiden Sitzungen kein Abgriff entstand; ac10 |

## Ausgefuehrte Pruefungen (Auszug mit Exitcodes)

```text
./scripts/rift.sh fmt                                                        -> 0
./scripts/rift.sh lint                                                       -> 0
./scripts/rift.sh build                                                      -> 0 (0 Warnungen)
./scripts/rift.sh test                                                       -> 0 (236/236)
./scripts/rift.sh security                                                   -> 0 (findings=0)
./scripts/rift.sh rag-build                                                  -> 0
./scripts/rift.sh verify                                                     -> 0 (valid=true, runsChecked=54)
./scripts/rift.sh bench --scenario bench-sim --report artifacts/t032/regression-bench-sim.json -> 0
./scripts/rift.sh savecheck --report artifacts/t032/regression-savecheck.json -> 0
./scripts/rift.sh soak --scenario soak-replay --diagnostic-accelerated --horizon-ticks 3000 -> 0 (rein diagnostisch)
kommandoschleife ... --interactive (ohne DISPLAY/WAYLAND)                    -> 19 (kontrollierter Abbruch, kein Report)
```

Testbestand: 218 → 236 (+18 neue T-032-Bindungen einschliesslich Erweiterung
der Bestandstests fuer Exitcodes 35–38 und den T-023-Ticktreiber-Allowlist).

## Fixture-Hygiene

Alle testspezifischen Fixtures sind versionierte Literale oder werden
deterministisch im Test erzeugt: Golden-Report (versioniertes JSON-Literal im
Testmodul, aus einem echten Lauf gebunden), Codec-Goldbytes, Skriptinhalte als
Konstanten, Gate-/Parser-Fixtures programmatisch. Kein schneller Gatezugriff
greift auf gitignorierte Runtime- oder Langzeitevidenz zu: Die Suite laeuft
ohne `.ai/runtime/cache/native`-Artefakte, ohne Reports unter `artifacts/` und
ohne Netzwerk; CLI-Tests erzeugen ihre Skripte und Reports selbst in
Tempverzeichnissen.

## Bekannte Restpunkte

1. **Manueller Interaktivsmoke / optionaler Abgriff:** Sowohl die Builder-
   Sitzung als auch die Review-Sitzung sind displaylos. Die Review-Sitzung
   hat den Versuch eines echten Fensterlaufs über eine eigene D-Bus-Session
   mit `kwin_wayland --virtual` wiederholt: Der Compositor akzeptiert zwar
   Clients auf `wayland-0`, erzeugt in dieser Umgebung aber keine Outputs,
   sodass SDL3 mit „No available video device" abbricht; ohne Compositor
   ebenfalls Code 19. Beide Abbruchwege sind als vertraglicher kontrollierter
   Abbruch belegt (Code 19, kein Report, kein simuliertes Interaktivverhalten).
   Was dadurch offen bleibt: die menschliche Sichtprüfung der Zwei-Kanal-
   Rückmeldung und der Kamerabedienbarkeit sowie der opt-in hashgebundene
   Einzelabgriff. Diese bleiben einer Displaysession auf dem Entwickler-PC
   ausdrücklich vorbehalten und sind im Task-Manifest als Restpunkte genannt;
   die spielerische Güte selbst bleibt den späteren Playtesttasks nach
   Kommandovertrag Abschnitt 3/4 (vorregistrierte Playtestkriterien)
   vorbehalten.
2. **Pflichtprofile** bleiben `NOT-MEASURED` (Q-OPS-001); Laeufe auf dem
   Entwickler-PC sind diagnostische Baseline.
3. **Offene Fragen unberuehrt:** Q-GAM-001 bis Q-GAM-007, Q-NAR-002,
   Q-TEC-004, Q-TEC-006, Q-TEC-010.
4. **Verschobene unabhängige zweite Reparatur (späterer Slice):** Der
   Asset-Lane-Git-Check (`ASSET_GIT_CHECK_FAILED`,
   `tools/RiftHarness/Assets.fs`) setzt ein `.git`-Verzeichnis voraus und
   scheitert deshalb in reinen `git archive`-Extraktionen ohne Git-Kontext.
   Die Review-Sitzung hat dies nicht im Primärslice repariert (fremde
   Taskfläche T-003/T-006), sondern den Fresh-Checkout-Nachweis stattdessen
   gemäß Integrator-Semantik in einem isolierten Git-Kontext am exakten
   Kandidatenbaum geführt (Abschnitt unten). Die dauerhafte
   Archivkompatibilität des Asset-Checks ist als eigener späterer Slice
   nachzutragen.

## Unabhängige Review-/Vollendungssitzung (2026-08-26, Run `01M0YJE3T8FDQ3T9QWJJY4DTNW`)

Die Review-Sitzung führte alle Gates selbst aus und reparierte fünf
In-Scope-Defekte des Primärslices, ohne ein zweites bereits akzeptiertes
Task-Manifest anzufassen:

1. **Invertierte Zoomrichtung (funktional):** `GrayboxCamera.ZoomSteps(+1)`
   ist vertraglich getestet „hinein" (Anzeigedistanz kleiner). Der Runner
   mappte `zoom-in` → `-1`, `zoom-out` → `+1` sowie Mausrad vor → `-1` —
   jeweils invertiert zur eigenen API-Dokumentation, zum Kamera-Clamp-Test
   und zum Keymap-Kanal. Reparatur: Vorzeichen an beiden Stellen gedreht;
   Rad vor (`WheelY > 0`) zoomt jetzt heran wie die Keymap.
2. **Fehlender Ausweis des Kettenkriteriums (Vertragslücke):**
   Kommandovertrag §7 verlangt, dass der Interaktivreport das Ketten-
   Selbstkonsistenzkriterium als „nicht auswertbar" mit maschinenlesbarem
   Grund ausweist statt es zu behaupten. Reparatur: neues Pflichtfeld
   `gate.stateChainSelfConsistency` (headless `{"evaluated":true}`,
   interaktiv `{"evaluated":false,"reason":
   "live-inputs-nondeterministic-criterion-not-asserted"}`), Schema-
   Alternativnode, Golden-Fixture aus einem echten Lauf regeneriert und zwei
   Fabrikationsnegative (Nichtauswertung ohne Grund wird abgewiesen;
   Goldenausweis gebunden) ergänzt. NATIVE_UNTERBAU.md bildet den Ausweis
   zeichentreu ab.
3. **Registertext-Fusion:** `.ai/evals/quality-gates.json` hatte zwei Sätze
   ohne Trennung fusioniert („… Exitcode 25 fehl KOMMANDO-GRAYBOX …").
   Reparatur: Satztrennung eingefügt; JSON erneut validiert.
4. **Achsenfalsch-Clamp bei Live-Intents:** `ToMillimeters` klemmte Y- und
   X-Koordinaten beide an die Weltbreite statt an die jeweilige Achse.
   Reparatur: achsengetrennte Grenzen (`WorldWidthMillimeters`/
   `WorldHeightMillimeters`) für Punkt- und Rahmenintents.
5. **Kommentardefekt Codec:** Der Doc-Kommentar behauptete „17 Bytes fest",
   tatsächlich ist die Festbreite 21 Bytes (`EncodedSize = 21`). Reparatur:
   Kommentar korrigiert.

Zusätzlich wurde der T-032-CLI-Vertragstest von einer stillen Abhängigkeit
von gitignoriertem Laufzeitstand befreit (Portabilitätspflicht): Der
displaylose Interaktivfall erwartet jetzt Code 19, wenn Native-Artefakte
gebaut sind, und den dokumentierten Artefaktcode 14, wenn
`artifact-hashes.json` fehlt — beides kontrolliert ohne Report. Keine dieser
Reparaturen schwächt ein Abnahmekriterium.

Eigene Evidenz der Review-Sitzung (alle Befehle selbst ausgeführt):

```text
./scripts/rift.sh fmt            -> 0 (1 Datei formatiert)
./scripts/rift.sh lint           -> 0 (0 Befunde)
./scripts/rift.sh build          -> 0 (0 Warnungen)
./scripts/rift.sh test           -> 0 (236/236)
./scripts/rift.sh security       -> 0 (PASS, Toolchain-/Lizenz-/ISA-Gate)
./scripts/rift.sh rag-build      -> 0
./scripts/rift.sh verify         -> 0 (valid=true, runsChecked=54)
bench --scenario bench-sim       -> 0 (Regression)
savecheck                        -> 0 (Regression, alle Pruefklassen)
soak --diagnostic-accelerated    -> 0 (rein diagnostisch, Kurzhorizont)
```

Autoritative Kommandoschleifenläufe (headless, nativ linux-x64): zwei
Fresh-Prozessläufe mit identischem Skript und Seed 20260826 → Exit 0, Gate
pass, byteidentische Kettenstichproben und Endhash `978aab19406daa26`
(builderidentisch); Fremdseed 42 ändert den Endhash nachweislich
(`76eee99a2f05629c`); Messwerte p99 0,961 ms (Grenze 16, Ziel 8),
Allokation 0 Bytes je warmem Tick, max reactionTicks 1 ≤ 3, GC-Pausen 0,
Kettenkriterium ausgewertet; Reports unter gitignoriertem
`artifacts/t032-review/`. Ein dritter Lauf mit Horizontabweichung im
Skriptkopf bestätigte Code 37 ohne Report; der displaylose Interaktivlauf
ergab Code 19 ohne Report.

## Fresh-Checkout-/Clean-Archive-Nachweis

Da dieser Lauf Test-, Fixture-, Build- und Evidenzpfade berührt, führte die
Review-Sitzung den Portabilitätsvertrag gemäß Präzedenz
`T-031-fresh-review` selbst aus, ohne Staging, Commit oder Berührung des
echten Index: Aus HEAD `068974c9e606e6b023d4708ffc7cc12be5dda7a9` plus
Arbeitsbaum wurde per indexfreier Plumbing-Rekonstruktion (ausschließlich
`hash-object`/`mktree`) der hypothetische Kandidatenbaum
`2e0845bf62b28e483e1067e5775b0b84e42230e5` erzeugt; die Baumdifferenz gegen
HEAD umfasst exakt die achtunddreißig Auftragspfade (20 modifiziert,
18 neu; Zählkorrektur der Frisch-Review-Sitzung unten) und kein zweites
Task-Manifest. Zweifache `git archive`-Extraktion
war byteidentisch. Der Gate-Zug lief ausschließlich aus diesen Bytes:
bootstrap PASS, Release-Build 0 Warnungen, Tests 236/236 Exit 0 (nach der
obigen Testreparatur; die erste Archivfolge hatte die stillen
Laufzeitabhängigkeiten als FAIL sichtbar gemacht — genau der gewollte
Portabilitätseffekt), lint PASS 0 Befunde, security PASS, assets-check
PASS, rag-build PASS, `rift.sh verify` valid=true sowie ein autoritativer
`kommandoschleife`-Lauf aus den Archivbytes (Exit 0, Gate pass, Endhash
`978aab19406daa26` identisch zu den Arbeitsbaumläufen). Der Asset-Lane-Git-
Check benötigte dabei einen isolierten Git-Kontext (Alternates +
`read-tree`/`checkout-index` des exakten Baums, Integrator-Semantik); seine
Archiv-Inkompatibilität ohne `.git` ist als verschobene zweite Reparatur
oben dokumentiert. Der Driftvergleich gegen die Zweitextraktion ergab nach
dem gesamten Gate-Zug keine Abweichung in getrackten Bytes (`NO_DRIFT`).

## Unabhängige Frisch-Review-Sitzung (2026-08-26, Run `01M0YQ71P5684F9PQR7T536QKS`)

Eine neue, unabhängige Review-Sitzung prüfte den gesamten Arbeitsstand
erneut ohne Übernahme der Erfolgsbehauptungen der Vorsitzungen und führte
alle schnellen Gates sowie die autoritativen Läufe selbst aus. Sie
reparierte drei In-Scope-Defekte des Primärslices, ohne ein zweites bereits
akzeptiertes Task-Manifest anzufassen:

1. **Verletzte Kamera-Invariante:** `GrayboxCamera.SetDistance` dokumentierte
   „immer geclippt", setzte die Distanz aber ungeclippt. Reparatur: Clamp im
   Setter (Vertrag Abschnitt 4 „geclippter Zoom"); Methode bleibt außerhalb
   von Tests ungenutzt.
2. **Falscher Methodenlabel eines Diagnosefelds:** Der Interaktivreport
   behauptete für `frameTimeMs` die Methode
   `stopwatch-frame-delta-including-shadow-and-composite-passes`; gemessen
   wurde tatsächlich das Delta um den fensterbezogenen Simulationstick
   inklusive Allokationssonden — Schatten-/Composite-Pässe sind nicht
   enthalten (Label war unverändert aus dem T-023-Lauf kopiert, wo es dort
   zutrifft). Reparatur: wahrheitsgetreue Methodenkennung; Feld bleibt
   diagnostisch `gateCoupled=false`.
3. **Widerspruch Implementierung/Dokumentation bei verspäteten Live-Intents:**
   XML-Doku, Dispositionsenum (`RejectedLate`) und bestehender Test beschreiben
   die kontrollierte Abweisung zu spät eingetroffener Live-Intents;
   `SessionPipeline.ProcessBoundary` führte sie stattdessen nachträglich aus,
   und der Test bestand nur wegen der zufällig leeren Auswahl über die falsche
   Klasse. Reparatur: verspätete Live-Intents werden jetzt als `RejectedLate`
   mit fachlicher Ursache abgewiesen (Zähler `LateRejectedTotal`, Aufnahme in
   `RejectedCount`); der Test bindet zusätzlich die Klasse. Headless-Ketten
   sind beweislich unberührt: Endhash vor/nach Reparatur identisch
   (`eeeb63be320e6e6f`, Skript/Seed dieser Sitzung).

Dazu korrigierte sie die Pfadanzahl des obigen Fresh-Checkout-Absatzes
(38 Auftragspfade: 20 modifiziert, 18 neu) und stellte die Verschobenheit der
zweiten unabhängigen Reparatur erneut fest (`ASSET_GIT_CHECK_FAILED`,
`tools/RiftHarness/Assets.fs` Zeile `git rev-parse --verify HEAD`; fremde
T-003/T-006-Fläche, eigener späterer Slice).

Eigene Evidenz dieser Sitzung (alle Befehle selbst ausgeführt, Exitcodes):

```text
./scripts/rift.sh lint        -> 0
./scripts/rift.sh build       -> 0 (0 Warnungen)
./scripts/rift.sh test        -> 0 (236/236)
./scripts/rift.sh security    -> 0 (PASS)
./scripts/rift.sh rag-build   -> 0
./scripts/rift.sh verify      -> 0 (valid=true)
kommandoschleife headless, Seed 20260826, zwei Fresh-Prozessläufe -> 0/0,
    identischer Endhash eeeb63be320e6e6f, p99 0,684/0,736 ms,
    Allokation 0 Bytes je warmem Tick, max reactionTicks 1,
    Kettenkriterium evaluated=true; Fremdseed 42 -> Endhash 1e6b861aeb854f58
Horizontabweichung im Skriptkopf                               -> 37 ohne Report
--interactive displaylos                                       -> 19 ohne Report
Regressionen: bench-sim -> 0, savecheck -> 0 (alle Prüfklassen),
    soak-replay --diagnostic-accelerated --horizon-ticks 3000 -> 0
```

Der Frisch-Checkout-/Clean-Archive-Nachweis dieser Sitzung läuft nach allen
Inhaltänderungen auf dem exakten Finalbaum (HEAD
`068974c9e606e6b023d4708ffc7cc12be5dda7a9` plus Arbeitsbaum; Baumdifferenz
exakt 38 Auftragspfade, kein zweites Task-Manifest); der exakte Baumdigest
ist im Harness-Run `01M0YQ71P5684F9PQR7T536QKS` gebunden. Berührte
Test-, Fixture-, Build- und Evidenzpfade sind unverändert gegenüber der
vorherigen Sitzung; die verschobene zweite unabhängige Reparatur wird nicht
im Primärslice erledigt.

## Unabhängige Wiederaufnahme-/Review-Sitzung (2026-08-26, Akteur `t032-review-resume`)

Der vorige Reviewer wurde extern per systemctl gestoppt (nicht durch Provider-
oder Kandidatenfehler). Diese Sitzung übernahm den unveränderten Stand:
Empfangene Identity-v3-Eingaben Parent `068974c9e606e6b023d4708ffc7cc12be5dda7a9`
plus hypothetischer Add-A-Baum `23b98ebe09cd2c838ae268db3de0b132f6acbdeb`
(Arbeitsbaum bei Sitzungsbeginn beweislich unverändert gegenüber dessen letzter
Evidenz; drei unbelastete Archivläufe 236/236 in `artifacts/t032-review3/`).
Deren belasteter x1-Lauf (kontrollierte Hostlast) hatte 233/236 gezeigt:
T-021 Exit 26, T-022 Exit 30, T-032 Exit 35 — alles lastempfindliche
fail-closed Budgetgates, die unter CPU-Konkurrenz korrekt anschlagen, keine
Kandidatendefekte; die drei Tests bestanden in allen unbelasteten Läufen.

Die Sitzung prüfte alle fünf Sidecar-Befunde lokal gegen Code und Vertrag und
entschied getrennt nach In-Scope-Pflicht und Verschiebepflicht:

**Bestätigt und im Primärslice repariert (vier Code-/Testreparaturen plus
zwei eigene Zusatzbefunde):**

1. **`scriptSha256` über Re-Encoding statt Rohbytes (Vertragsbruch §5):**
   Der Runner las per `File.ReadAllText` (BOM-Strip, lenkante Ersetzung
   ungültiger UTF-8-Sequenzen), der Parser hashte die re-enkodierten Bytes;
   verschiedene Rohbytes konnten denselben Hash erhalten und die
   Größenprüfung griff an der falschen Stelle. Reparatur: Der Runner liest
   `File.ReadAllBytes`; neuer Parser-Eingang `Parse(byte[])` prüft die
   Vertragsbytegrenze vor der Dekodierung, dekodiert strikt
   (`throwOnInvalidBytes`) mit kontrollierter Klasse `HeaderMalformed` für
   nicht-UTF-8-Bytes und BOM-Vorspann, und bindet `scriptSha256` an die
   exakten Rohbytes (CRLF bleibt im Hash, Analyse normalisiert weiterhin).
2. **Interaktive Allokationsmetrik maß Render-/Abgriff-/Reportanteile:**
   Das Fensterdelta lief vom Fensterbeginn bis zur Reportserialisierung und
   teil durch die Tickzahl; die Per-Tick-Deltas um `world.Tick()` wurden
   verworfen (`_ = allocationAfter - allocationBefore`). Damit hätte das Gate
   Kriterium 2 im Interaktivmodus über fremde Allokationen entschieden —
   im Widerspruch zum dokumentierten Methodenlabel und Simulationsvertrag §5.
   Reparatur: Summierung ausschließlich der Deltas um `world.Tick()` je
   Fensterstick (identisch zur headless Engine), eine einzige Auswertung für
   Gate und Report statt zweier driftender Stichproben.
3. **Asymmetrischer Nicht-PlatformException-Abbruch im Interaktivpfad:**
   Headless fängt jede Ausnahme in einen Teilreport mit Code 36; interaktiv
   floh sie an den Prozess (Crash ohne Report, undokumentierter Exitcode).
   Reparatur: zweiter Catch erzeugt denselben nicht-evidenten Teilreport mit
   Code 36 (`PlatformException` behält ihre spezifische Zuordnung, Code 19).
4. **Wahrheitsgehalt unvollständiger Reports:** Die `Unavailable`-Felder eines
   unvollständigen Laufs behaupteten den headless-Grund
   `headless-cpu-scenario-no-renderer`. Reparatur: unvollständige Läufe
   beider Modi weisen `run-incomplete-no-evidence` aus.
5. **Lückenhafte Parserklasse-Matrix:** Der Test „rejects every class
   distinctly" prüfte nur acht von zehn Klassen; `ScriptTooLarge` und
   `IntentLimitTotal` fehlten. Reparatur: beide Klassen mit Fixtures gebunden
   (Rohbytegrenze vor Dekodierung; 4097 Intents über Horizont 4200).
6. **Neuer Rohbyte-Bindungstest:** Suite-Eintrag „script hash binds raw bytes
   and rejects invalid encodings" bindet CRLF-Rohbyte-Hash versus Planhash-
   Gleichheit, Ungültig-UTF-8- und BOM-Abweisung. Testbestand 236 → 237.
7. **Transiente Lasttoleranz des CLI-Vertragstests gemäß T-021-Präzedenz:**
   Wie der dokumentierte bench-sim-Vertragstest (BACKLOG 2026-08-25,
   Wiederholversuch bei Exitcode 26) wiederholt jetzt auch der T-032-Test
   jeden frischen Prozesslauf genau einmal bei Exitcode 35; dauerhafte
   Gateverletzungen scheitern weiter reproduzierbar in beiden Versuchen.

**Bestätigt, aber bewusst verschoben (keine zweite Taskfläche im
Primärkandidaten):**

- **Reaktionsmetrik konstruktionsbedingt stets 1 Tick:** Die Pipeline übergibt
  Kernbefehle an derselben Vorgrenze, an der der Intent fällig wird
  (`V == S`), daher ist `reactionTicks` per Konstruktion 1 und das Gate-
  Kriterium 3 kann nur über die existierende Fault-Injection am Gate selbst
  scheitern, nicht über die Pipeline. Das ist vertraglich wahrheitsgetreu
  ausgewiesen (§6 lässt V ≥ S zu), schwächt aber die Aussagekraft des
  Kriteriums auf den Nachweis „sofortige Verbrauchskonsistenz". Eine
  verzögerte Verbrauchssemantik oder ein Effektsnapshot-abhängiger Nachweis
  wäre eine Kommandovertrag-V2-Entscheidung mit Fixture-Regeneration —
  als eigener späterer Slice ins BACKLOG aufgenommen, nicht still hier
  entschieden.
- **Stille CLI-Defaults/-Clamps:** `NumberOption` fällt bei nichtparierbaren
  Werten still auf den Default (`ArgumentQueue.cs`, geteilte, bereits
  abgenommene Fläche aller Runner), `--warmup-ticks`/`--horizon-ticks`
  werden still geclamped. Ein Clamping des Horizonts scheitert spätestens an
  der Skriptkopf-Bindung (Code 37), ein falscher Report entsteht nicht.
  Repo-weite Säuberung als späterer Slice im BACKLOG vermerkt.

**Kein Befund:** Fremdseed-Sensitivität, Ketten-Selbstkonsistenz, Exitcodes,
Schema- und Goldbindung, Hermetie, Architekturgrenzen und Riftward.Simulation
(blobidentisch) blieben in der eigenen Nachprüfung fehlerfrei.

Eigene Evidenz dieser Sitzung (alle Befehle selbst ausgeführt, Exitcodes):

```text
./scripts/rift.sh build       -> 0 (0 Warnungen)
./scripts/rift.sh fmt         -> 0 (1 Datei formatiert)
./scripts/rift.sh lint        -> 0 (0 Befunde)
./scripts/rift.sh test        -> 0 (237/237)
./scripts/rift.sh security    -> 0 (PASS)
./scripts/rift.sh rag-build   -> 0
./scripts/rift.sh verify      -> 0 (valid=true, runsChecked=56)
kommandoschleife headless, Seed 20260826, zwei Fresh-Prozessläufe -> 0/0,
    identischer Endhash 978aab19406daa26 (builderidentisch zum Erstreview),
    p99 0,867/0,834 ms, Allokation 0 Bytes je warmem Tick,
    max reactionTicks 1, scriptSha256 rohbytegleich; Fremdseed 42 ->
    Endhash 76eee99a2f05629c (abweichend)
Regressionen: bench-sim -> 0, savecheck -> 0 (alle Prüfklassen),
    soak-replay --diagnostic-accelerated --horizon-ticks 3000 -> 0;
    Kontrolllauf mit --horizon-ticks 600 -> 30 (nur ein RSS-Fenster,
    Trend NaN, korrektes Fail-Closed; Parameterfrage, kein Defekt)
```

### Fresh-Checkout-/Clean-Archive-Nachweis dieser Sitzung

Da erneut Test-, Fixture-, Build- und Evidenzpfade berührt wurden, lief der
Portabilitätsvertrag nach allen Änderungen am exakten Finalbaum: private
Index-Rekonstruktion des hypothetischen Add-A-Baums aus HEAD
`068974c9e606e6b023d4708ffc7cc12be5dda7a9` plus Arbeitsbaum; der exakte
Kandidatenbaum-Digest ist in der gitignorierten Sitzungsevidenz
(`artifacts/t032-review4/final-candidate-tree.txt`) gebunden statt in den
geprüften Bytes selbst (Selbstreferenz). Die Index-zu-HEAD-Differenz umfasst
38 Auftragspfade und genau ein Task-Manifest
(`.ai/tasks/T-032-graybox-command-loop.json`). Volle Gatefolge ausschließlich
aus Archivbytes in isoliertem Git-Kontext (Integrator-Semantik wegen der
verschobenen Asset-Lane-Reparatur): bootstrap, Release-Build 0 Warnungen,
lint 0 Befunde, Tests 237/237 Exit 0, assets-check PASS, rag-build PASS,
verify valid=true. Zweitextraktion desselben Baums: Quelldrift nach dem
gesamten Gate-Zug null (`NO_DRIFT` in getrackten Bytes). Evidenz unter
gitignoriertem `artifacts/t032-review4/`.

Der finale Kandidatenbaum dieser Sitzung (Parent
`068974c9e606e6b023d4708ffc7cc12be5dda7a9`, Digest wie oben in der
Sitzungsevidenz gebunden) steht einer frischen Promoter-Sitzung zur
Verfügung.

## Unabhängige Abschluss-Review-Sitzung (2026-08-26, Run `01M0Z4XKCYH8DMSVE7K8QDHYWR`)

Eine erneute frische Review-Sitzung prüfte den vollständigen Arbeitsstand
ohne Übernahme der Erfolgsbehauptungen der Vorsitzungen und führte alle Gates
und autoritativen Läufe selbst aus.

**Empfangene Identität:** Die eigene indexfreie Plumbing-Rekonstruktion
(`hash-object`/privater Index, kein Staging des echten Index) ergab aus HEAD
`068974c9e606e6b023d4708ffc7cc12be5dda7a9` plus Arbeitsbaum exakt den
Kandidatenbaum `6cef61b0a2a0ce6940d108ceeae495e86feefb31` und reproduzierte
damit die Bindung der Wiederaufnahmesitzung
(`artifacts/t032-review4/final-candidate-tree.txt`): Der Stand war seit deren
NO_DRIFT-Nachweis beweislich unverändert; die Baumdifferenz umfasst 38
Auftragspfade und genau ein Task-Manifest.

**Zwei In-Scope-Nebenreparaturen im Primärslice** (kein zweites bereits
akzeptiertes Task-Manifest berührt):

1. **Wahrheitswidriger unavailable-Grund:** Der headless Report wies
   `workingSetKiB` als nicht gemessen aus, nannte als Grund aber
   `headless-session-engine-samples-rss-diagnostically` — die headless Engine
   sampelt nachweislich kein RSS (`SessionEngine.Run` enthält keinen Sampler).
   Reparatur: wahrheitsgetreuer Grund `headless-session-does-not-sample-rss`;
   das Feld bleibt diagnostisch, ein Messwert bleibt unverändert unbehauptet.
2. **Exitcode-Widerspruch bei vorzeitigem Interaktivabbruch:** Ein Abbruch
   vor Fensterabschluss (zum Beispiel ESC) schreibt korrekt `gate.pass=false`
   mit der Verletzung `run-incomplete-no-evidence`, lieferte bei zufällig
   passierenden Teilmetriken jedoch Exitcode 0 statt des dokumentierten Codes
   36 (NATIVE_UNTERBAU.md und Kommandovertrag §8: „unvollständiger Lauf … gilt
   ausdrücklich nicht als Evidenz“). Reparatur: Der Exitcode ist jetzt strikt
   an `windowCompleted` gebunden — vorzeitiger Abbruch ergibt stets 36, ein
   fehlgeschlagener Abgriff weiterhin 38, ein vollständiger Lauf das
   Gateverdict. Die ausführbare Bindung dieses Pfades bleibt wie der
   Interaktivsmoke einer Displaysession vorbehalten (hier displaylos belegbar
   nur Code 19); die Reparatur ist durch Pfadinspektion gegen die
   dokumentierte Codetabelle begründet.

Headless-Ketten sind konstruktionsbedingt unberührt (beide Reparaturen
betreffen ausschließlich Reportfelder und Exitcodes): Die Nachprüfläufe unten
erzielten denselben Endhash wie der Lauf vor der Reparatur.

Eigene Evidenz dieser Sitzung (alle Befehle selbst ausgeführt, Exitcodes):

```text
./scripts/rift.sh fmt --check  -> 0
./scripts/rift.sh lint         -> 0 (0 Befunde)
./scripts/rift.sh build        -> 0 (0 Warnungen)
./scripts/rift.sh test         -> 0 (237/237)
./scripts/rift.sh security     -> 0 (PASS)
./scripts/rift.sh rag-build    -> 0
./scripts/rift.sh verify       -> 0 (valid=true, runsChecked=56)
kommandoschleife headless, Seed 20260826, zwei Fresh-Prozessläufe -> 0/0,
    identischer Endhash 40c3016dc9b93325, p99 0,806 ms,
    Allokation 0 Bytes je warmem Tick, max reactionTicks 1,
    Kettenkriterium evaluated=true; Fremdseed 42 -> ee70f281c1bb60b6
Horizontabweichung im Skriptkopf                               -> 37 ohne Report
--interactive displaylos                                       -> 19 ohne Report
Regressionen: bench-sim -> 0, savecheck -> 0 (alle Prüfklassen),
    soak-replay --diagnostic-accelerated --horizon-ticks 3000 -> 0
```

### Fresh-Checkout-/Clean-Archive-Nachweis dieser Sitzung

Da erneut Quellen-, Test-, Build- und Evidenzpfade berührt wurden, lief der
Portabilitätsvertrag nach allen Änderungen am neuen Finalbaum: private
Index-Rekonstruktion des hypothetischen Add-A-Baums aus HEAD plus Arbeitsbaum;
der exakte Baumdigest ist außerhalb der geprüften Bytes gebunden
(`artifacts/t032-final-review/final-candidate-tree.txt` sowie Summary des
Harness-Runs `01M0Z4XKCYH8DMSVE7K8QDHYWR`; Selbstreferenzvermeidung wie in der
Vorsitzung). Die Baumdifferenz zu HEAD umfasst weiterhin exakt 38 Auftragspfade
und genau ein Task-Manifest. Zweifache `git archive`-Extraktion war
byteidentisch; die volle Gatefolge (bootstrap, Release-Build, lint, Testsuite,
security, assets-check, rag-build, verify) lief ausschließlich aus diesen
Archivbytes im isolierten Git-Kontext (Integrator-Semantik wegen der
unverändert verschobenen Asset-Lane-Reparatur) inklusive autoritativem
`kommandoschleife`-Lauf aus Archivbytes mit identischem Endhash. Der
Driftvergleich gegen die Zweitextraktion ergab nach dem gesamten Gate-Zug null
Abweichung in getrackten Bytes (`NO_DRIFT`).

Der finale Kandidatenbaum dieser Sitzung steht der frischen Promoter-Sitzung
zur Verfügung.

## Unabhängige Prüf-/Vollendungs-Review-Sitzung (2026-08-26, Run `01M0ZV9ARJ1PFSA64RGZ5ME4H2`, Akteur `t032-selfreview-final`)

Eine erneute frische Review-Sitzung prüfte den vollständigen Arbeitsstand ohne
Übernahme der Erfolgsbehauptungen der Vorsitzungen und führte alle schnellen
Gates sowie die autoritativen Läufe selbst aus (fmt --check, lint, Release-Build
0 Warnungen, security, rag-build, verify vor den Reparaturen grün; Tests 237/237).

**Zwei kleine In-Scope-Nebenreparaturen im Primärslice** (kein zweites bereits
akzeptiertes Task-Manifest berührt):

1. **Exitcode-Präzedenz bei vorzeitigem Interaktivabbruch mit angefordertem
   Abgriff:** Der Runner gab `capture.Failed` Vorrang vor der
   Fenster-Vollständigkeit; ein früher Abbruch mit `--capture-frame`
   lieferte damit Code 38 statt des dokumentierten Codes 36 und widersprach
   NATIVE_UNTERBAU.md („ein vorzeitiger Abbruch ergibt 36“), Kommandovertrag §8
   (unvollständige Läufe sind niemals Evidenz) und der eigenen Klausel der
   Abschluss-Review („strikt an `windowCompleted` gebunden“). Reparatur: Die
   Entscheidung liegt in der puren Hilfsfunktion
   `CommandLoopRunner.ResolveInteractiveExitCode` — ein unvollständiger Lauf
   dominiert stets mit Code 36, der unterbliebene Abgriff bleibt im Report als
   `captured=false` mit Grund gebunden; bei abgeschlossenem Fenster entscheiden
   weiterhin fehlgeschlagener Abgriff (38) beziehungsweise das Gateverdict.
   Neuer Suiteeintrag „interactive exit code precedence stays window bound“
   bindet die Präzedenzmatrix, Testbestand 237 → 238.
2. **Formatanomalie:** Ein blockweise nachträglich reparierter Abschnitt in
   `SessionEngine.ProcessBoundary` war achtstellig eingerückt statt vierstellig;
   normalisiert (rein kosmetisch, kein Verhaltensanteil).

Headless-Ketten sind von beiden Reparaturen konstruktionsbedingt unberührt:
der eigene Nachprüflauf unten erzielte denselben Endhash wie die beiden
vorigen unabhängigen Sitzungen.

Eigene Evidenz dieser Sitzung (alle Befehle selbst ausgeführt, Exitcodes):

```text
./scripts/rift.sh fmt --check    -> 0 (nach fantomas-Normalisierung des neuen Tests)
./scripts/rift.sh lint           -> 0 (0 Befunde)
./scripts/rift.sh build          -> 0 (0 Warnungen)
./scripts/rift.sh test           -> 0 (238/238)
./scripts/rift.sh security       -> 0 (PASS)
./scripts/rift.sh rag-build      -> 0
./scripts/rift.sh verify         -> 0 (valid=true)
kommandoschleife headless, Seed 20260826, zwei Fresh-Prozessläufe -> 0/0,
    identischer Endhash 978aab19406daa26 builderidentisch zur Erstreview und
    zur Wiederaufnahmesitzung, p99 0,805/0,83 ms, Allokation 0 Bytes je
    warmem Tick, max reactionTicks 1, Kettenkriterium evaluated=true,
    scriptSha256 laufübergreifend identisch; Fremdseed 42 ->
    Endhash 76eee99a2f05629c (abweichend, identisch zum Wiederaufnahmebefund);
    leeres/malformiertes Skript und Horizontabweichung im Kopf je ->
    37 ohne Report; displayloser Interaktivlauf -> 19 ohne Report
Regressionen: bench-sim -> 0 (gate pass), savecheck -> 0 (19/19 Prüfklassen),
    soak-replay --diagnostic-accelerated --horizon-ticks 3000 -> 0
    (rein diagnostisch, evidenceUnit=false korrekt markiert)
```

Der Fresh-Checkout-/Clean-Archive-Nachweis dieser Sitzung läuft nach allen
Änderungen am exakten Finalbaum: indexfreie Plumbing-Rekonstruktion aus HEAD
`068974c9e606e6b023d4708ffc7cc12be5dda7a9` plus Arbeitsbaum; der exakte
Baumdigest ist außerhalb der geprüften Bytes gebunden
(`artifacts/t032-selfreview/final-candidate-tree.txt` plus Summary des
Harness-Runs). Baumdifferenz zu HEAD umfasst genau 38 Auftragspfade mit genau
einem Task-Manifest. Zweifache `git archive`-Extraktion ist byteidentisch; die
volle Gatefolge (Release-Build, lint, Testsuite 238/238, security,
assets-check, rag-build, verify) läuft ausschließlich aus diesen Archivbytes im
isolierten Git-Kontext (Integrator-Semantik wegen der unverändert verschobenen
Asset-Lane-Reparatur), einschließlich autoritativem `kommandoschleife`-Lauf aus
Archivbytes mit identischem Endhash. Driftvergleich gegen die Zweitextraktion
nach dem gesamten Gate-Zug: null Abweichung in getrackten Bytes (`NO_DRIFT`).

Die verschobenen unabhängigen Reparaturen (Asset-Lane-Git-Check, vakuoese
Reaktionsmetrik V == S als Kommandovertrag-V2-Entscheidung, stille
NumberOption-Defaults/-Clamps) bleiben unverändert spätere Slices.

## Unabhaengige Frisch-Review-/Vollendungs-Sitzung 2026-08-26 (Akteur t032-review5)

Eine erneute unabhuengige Review-Sitzung pruefte den vollstaendigen Arbeitsstand
ohne Uebernahme der Vorsitzungsbehauptungen und fuehrte alle schnellen Gates,
autoritativen Laeufe sowie den Portabilitaetsvertrag eigenstaendig aus.

**Empfangene Identitaet:** Lock-freie Bottom-up-Baumkonstruktion (`ls-tree`-
Delta + `hash-object`/`mktree`, kein Beruehren des echten Index) aus Parent
`068974c9e606e6b023d4708ffc7cc12be5dda7a9` plus Arbeitsbaum ergab den
Kandidatenbaum `02e656df04f0470b855c2bdd73531958242fd0a0` mit exakt 38
Auftragspfaden (20 modifiziert, 18 neu) und genau einem Task-Manifest;
Zweitauswertung reproduzierte denselben Digest. Die Gleichheit zur Add-A-
Semantik wurde unabhaengig durch byteidentische Overlay-Extraktion des
Arbeitsbaums gegen eine `git archive`-Extraktion des Baums belegt
(Inhalt 1:1; Dateimenge identisch; nur von der Sitzungsumask abhaengige
Dateisystem-Bits variieren, Git-Blob-Modi uebereinstimmend).

**Vor der Reparatur gefundene Evidenz (alles selbst ausgefuehrt):**

```text
fmt --check / lint / build / test / security / rag-build / verify -> alle 0
   (Testsuite vor der Reparatur: 238/238)
kommandoschleife Skript A (9 Intents), Seed 20260826, zwei Fresh-Prozesslaeufe:
   Endhash identisch 60f311f20d9b7876, Kettenstichproben identisch,
   Allokation 0 B, p99 < 0,75 ms, max reactionTicks 1, gcPauseSumMs 0,
   workingSetKiB unavailable mit headless-session-does-not-sample-rss;
   Fremdseed 42 -> Endhash 67c46966a1979aa8 (abweichend)
kommandoschleife Skript B (Welt-Rechteck + drei moves), Seed 20260826,
   zwei Fresh-Prozesslaeufe: Endhash identisch 949194e5779aa2a1,
   15 Kernbefehle ueber GroupMoveToZone, reactionTicks max 1
Horizontabweichung im Skriptkopf                 -> 37 ohne Report
malformiertes Skript                             -> 37 ohne Report
unbekanntes Szenario                             -> 37 ohne Report
--interactive displaylos                         -> 19 ohne Report
Regressionen: bench-sim -> 0, savecheck -> 0,
   soak-replay --diagnostic-accelerated --horizon-ticks 3000 --report … -> 0
   (rein diagnostisch, evidenceUnit=false korrekt markiert)
```

**Ein In-Scope-Nebenreparaturfund im Primaerslice** (kein zweites bereits
akzeptiertes Task-Manifest beruehrt):

Die Gold-Fixture des Schematests enthielt weiterhin den wahrheitswidrigen
headless Grund `headless-session-engine-samples-rss-diagnostically`, den die
Abschluss-Review-Sitzung (`t032-final-review`) aus dem Laufzeitcode entfernt
hatte: der Runner liefert seitdem korrekt
`headless-session-does-not-sample-rss`, aber das Schema validiert diesen
Wert nicht und die Fabrikationsmatrix pruefte ihn nicht, sodass die Suite
trotz des verbleibenden Falschbehauptungsliterals gruen blieb. Die gebundene
"aus einem echten Lauf stammende" Goldprobe behauptete damit genau die
Sampling-Methode, deren Wahrheitswidrigkeit die vorige Sitzung dokumentiert
hat. Reparatur: Das Literal wurde in genau diesem einen Feld auf die echte
Ausgabe eines aktuellen Laufes regeneriert (alle uebrigen gebundenen Felder
unveraendert), die Fabrikationsmatrix weist die Kennung jetzt als Pflichtin-
halt aus, und ein neuer Suiteeintrag bindet den internen Runnerpfad direkt an
den vertraglichen Grund (`WorkingSetFrom`, Sichtbarkeit wie bei
`ResolveInteractiveExitCode` auf internal erweitert, rein kodalexpanzend ohne
Verhaltensteil). Testbestand 238 -> 239. Headless-Ketten sind konstruktionsbe-
dingt unberuehrt: Nachprueflaeufe unten erzielen dieselben Endhashes wie vor
der Reparatur.

**Fresh-Checkout-/Clean-Archive-Nachweis dieser Sitzung:** Da Quellen-, Test-
und Evidenzpfade beruehrt werden, laeuft der Portabilitaetsvertrag nach allen
Aenderungen am neuen Finalbaum: Doppel-Extraktion des Baums ist byteidenti-
sch, der isolierte Git-Kontext (Alternates + `read-tree` des exakten Baums)
erhaelt einen Bindungscommit auf diesen Baumbytes, und die volle Gatefolge
(bootstrap, Release-Build 0 Warnungen, lint, Testsuite, security,
assets-check, rag-build, verify) lief ausschliesslich aus Archivbytes -
vor der Reparatur inklusive autoritativem `kommandoschleife`-Lauf aus
Archivbytes mit Endhash `949194e5779aa2a1` identisch zum Arbeitsbaumlauf.
Der Driftvergleich nach dem gesamten Gate-Zug ergab null Abweichung in
getrackten Bytes (`NO_DRIFT`). Der Digest des endgueltigen Finalbaums steht
im Harness-Run dieser Sitzung sowie in der gitignorierten Sitzungsevidenz
unter `artifacts/t032-r5/`.

## Unabhaengige Frisch-Review-Sitzung 2026-08-26 (Run `01M101PKH0WSH1NC0BR56DQE6D`, Akteur `t032-r6-fresh-review`)

Eine weitere unabhaengige Review-Sitzung pruefte den vollstaendigen
Arbeitsstand ohne Uebernahme der Vorsitzungsbehauptungen. Sie fand **keinen
neuen Defekt** im Primaerslice und veraenderte ausschliesslich diese
Doku-/Registerebene (Abnahmedoku, BACKLOG, Task-Manifest-Kompletionnote);
keinerlei Code-, Test- oder Fixture-Aenderung.

**Empfangene Identitaet:** Private Indexrekonstruktion (hypothetischer
Add-A-Baum aus Parent `068974c9…` plus Arbeitsbaum) war zweifach konvergent
und reproduzierte exakt die Finalbaumbindung von `t032-review5`
(`artifacts/t032-r5/final-candidate-tree.txt`); Baumdifferenz exakt 38 Auftragspfade
(20 modifiziert, 18 neu), genau ein Task-Manifest. Die Lesart der Vorsitz-
sitzung wird praezisiert: `02e656df…` bezeichnet deren Empfangsstand
vor der Gold-Fixture-Reparatur, nicht ihren Finalbaum (`71e3ca3c8aa4551de55860347475a38c5e1cf232`).

**Eigene Evidenz (alles selbst ausgefuehrt, Exitcodes):**

```text
./scripts/rift.sh lint        -> 0 (fantomas --check, toolchain/lizenz/ISA)
./scripts/rift.sh build       -> 0 (0 Warnungen)
./scripts/rift.sh test        -> 0 (239/239)
./scripts/rift.sh security    -> 0 (PASS)
./scripts/rift.sh rag-build   -> 0
./scripts/rift.sh verify      -> 0 (valid=true)
kommandoschleife Skript A (9 Intents), Seed 20260826,
    zwei Fresh-Prozesslaeufe -> 0/0, identischer Endhash 2b0cd3cdd830f56d,
    p99 0,791 ms, Allokation 0 Bytes je warmem Tick, max reactionTicks 1,
    workingSetKiB unavailable mit headless-session-does-not-sample-rss,
    Kettenkriterium evaluated=true, gate.pass=true
kommandoschleife Skript B (Welt-Rechteck + drei moves), zwei Laeufe -> 0/0,
    identischer Endhash 6a5b7beb1d01e79a
Fremdseed 42 auf Skript A                                     -> 4b6140af3f3bfa65
Kopfhorizontabweichung / malformiert / unbekanntes Szenario   -> je 37 ohne Report
--interactive displaylos                                      -> 19 ohne Report
Regressionen: bench-sim -> 0, savecheck -> 0 (alle Pruefklassen),
    soak-replay --diagnostic-accelerated --horizon-ticks 3000 -> 0
    (rein diagnostisch, evidenceUnit=false korrekt markiert)
```

**Fresh-Checkout-/Clean-Archive-Nachweis dieser Sitzung** nach allen
Aenderungen am finalen Kandidatenbaum (Digest gebunden ausserhalb der
geprueften Bytes in `artifacts/t032-r6/final-candidate-tree.txt`, im
Gate-Zug auch als Modusbindung eines isolierten Git-Kontextes verarbeitet,
sowie in der Runzusammenfassung): private Indexrekonstruktion konvergiert;
zweifache `git archive`-Extraktion ist byteidentisch; bootstrap, Release-Build,
lint, Testsuite 239/239, security, assets-check und verify laufen
ausschliesslich aus Archivbytes (Integrator-Semantik wegen der unverändert
verschobenen Asset-Lane-Reparatur); der autoritative `kommandoschleife`-Lauf
aus Archivbytes liefert denselben Endhash wie die Arbeitsbaumlaeufe; der
Driftvergleich nach dem gesamten Gate-Zug ergibt fuer alle 350 getrackten
Dateien null Abweichung (`NO_DRIFT`).

Die verschobenen unabhaengigen Reparaturen (Asset-Lane-Git-Check, vakuoese
Reaktionsmetrik V == S als Kommandovertrag-V2-Entscheidung, stille
NumberOption-Defaults/-Clamps) bleiben unverändert spaetere Slices.

## Unabhaengige Frisch-Review-/Reparatursitzung 2026-08-26 (Akteur `t032-review7`)

Eine weitere unabhaengige Review-Sitzung pruefte den vollstaendigen
Arbeitsstand ohne Uebernahme der Vorsitzungsbehauptungen und fand **zwei
In-Scope-Defekte des Primaerslices**, die von keinem Suiteneintrag gebunden
waren; beide wurden im Primaerslice repariert, genau ein Task-Manifest
veraendert (nur T-032), kein zweites akzeptiertes Manifest beruehrt.

**Befund 1 — toter Befehlspuls-Kanal (AC-T032-06):**
`InteractiveView.NotifyCommandIssued` hatte keinen Aufrufer; der zweite
vertragliche visuelle Kanal (wachsender Bodenpuls am Zielzonenzentrum,
Kommandovertrag §3) haette auch auf einer Displaymaschine nie dargestellt.
Die dokumentierte „Strukturreview der Zwei-Kanal-Rueckmeldung" frueherer
Sitzungen hatte diese Verdrahtungsluecke nicht gesehen. Reparatur:
`SessionPipeline` weist je Vorgrenze die tatsaechlich an den Kern
uebergebenen Bewegungszonen aus (`DispatchedMoveZonesOfLastBoundary`; nur
akzeptierte Move-Intents, Leerung je Grenze, abgewiesene erscheinen nie),
und `CommandLoopRunner.RunInteractiveLoop` meldet diese Zonen an die
Darstellung. Vier neue Suiteeintraege binden: Zonenausweis nur fuer
akzeptierte Kernbefehle inklusive korrekter `RejectedLate`-Abweisung,
Puls-Lebenszyklus (erscheint nach Anmeldung, laeuft nach 40 Ticks ab),
Runnerverdrahtung per Quelltextpruefung (Praezedenz rift.sh-Vertragstest)
sowie den beweislich unveraenderten headless Weltzustand.

**Befund 2 — unbeschraenkte Materialisierung untrusted Eingaben (NF-003):**
Der Runner las das Eingabeskript vor der Vertragsgroessenpruefung komplett
(`File.ReadAllBytes`). Eine endlos liefernde Quelle (`/dev/zero`-Klasse)
waere unkontrolliert an der Speichergrenze gestorben statt kontrolliert mit
der vertraglichen Klasse `ScriptTooLarge` → 37 ohne Report. Reparatur:
begrenzende Rohbytes-Lesung `ReadInputScriptBytes` (Grenze wird waehrend des
Lesens durchgesetzt, vor jeder Dekodierung); Suiteeintrag gegen Sparse-
Uebergröße (262145 Bytes), exakte Grenzwertgroesse (262144 Bytes byteerhalten)
und End-of-stream-Spezialdatei `/dev/null`.

Testbestand 239 → 243. Headless-Ketten blieben durch beide Reparaturen
beweislich unveraendert; der Skript-A-Endhash ist identisch zur
Erstreview-/Wiederaufnahme-/Selfreview-Bindung `978aab19406daa26`.

**Eigene Evidenz (alles selbst ausgefuehrt, Exitcodes):**

```text
./scripts/rift.sh fmt         -> 0 (keine inhaltliche Aenderung nach Normalisierung)
./scripts/rift.sh lint        -> 0 (valid=true)
./scripts/rift.sh build       -> 0 (0 Warnungen)
./scripts/rift.sh test        -> 0 (243/243)
./scripts/rift.sh security    -> 0 (PASS)
./scripts/rift.sh rag-build   -> 0
./scripts/rift.sh verify      -> 0 (valid=true, runsChecked=59)
kommandoschleife Skript A (4 Intents, Horizont 420), Seed 20260826,
    zwei Fresh-Prozesslaeufe -> 0/0, identischer Endhash 978aab19406daa26,
    p99 0,821/0,892 ms, Allokation 0 Bytes je warmem Tick, max reactionTicks 1,
    Kettenkriterium evaluated=true, gate.pass=true
kommandoschleife Skript B (8 Intents, drei moves, Horizont 1000),
    zwei Laeufe -> 0/0, identischer Endhash fd2521bb71216ea9
Fremdseed 42 auf Skript B                                     -> 340b48f57e653a1a
Szenario unbekannt / malformiert / Kopfhorizontabweichung /
    uebergrosses Sparse-Skript                                -> je 37 ohne Report
--interactive displaylos (Native vorhanden)                   -> 19 ohne Report
Regressionen: bench-sim -> 0 (gate.pass=true), savecheck -> 0 (gate.pass=true),
    soak-replay --diagnostic-accelerated --horizon-ticks 3000 -> 0
    (rein diagnostisch, evidenceUnit=false korrekt markiert)
```

**Fresh-Checkout-/Clean-Archive-Nachweis dieser Sitzung** folgt nach allen
Aenderungen am exakten Finalbaum; der Baumdigest ist ausserhalb der geprueften
Bytes gebunden (`artifacts/t032-review7/final-candidate-tree.txt`). Ergebnis:
siehe Abschlussabschnitt dieser Datei. Die verschobenen unabhaengigen
Reparaturen (Asset-Lane-Git-Check in reinen Archivextraktionen, vakuoese
Reaktionsmetrik V == S, stille NumberOption-Defaults/-Clamps) bleiben
unverändert spaetere Slices.

### Fresh-Checkout-/Clean-Archive-Nachweis dieser Sitzung

Sitzungsreihenfolge wie in den Vorgängersitzungen: Der Code-/Test-/Gatezug und
der Portabilitätsnachweis liefen zuerst; ausschliesslich dieser Dokumentations-
abschnitt plus BACKLOG-Registereintrag sind danach entstanden. Der gebundene
Baumdigest bezeichnet daher den Finalbaum **vor** dieser reinen Doku-
Erweiterung (`artifacts/t032-review7/final-candidate-tree.txt`,
`1a626c3379bc55d1d246d278b2e987f5e8287763`); die Identitätsprüfung wurde nach
allen Gates wiederholt und erfuhr null Drift durch die Läufe selbst
(`NO_DRIFT`).

*Identität:* Plumbing-Rekonstruktion des hypothetischen Add-A-Baums aus Parent
`068974c9…` über einen privaten Index ohne Berührung des realen Index;
38 Auftragspfade (20 M, 18 A), genau ein Task-Manifest, 350 getrackte Dateien.
Doppelte `git archive`-Extraktion dieses Baums ist byteidentisch
(`DOUBLE-EXTRACT-BYTEIDENTICAL`).

## Unabhängige Frisch-Review-/Reparatursitzung 2026-08-27 (Akteur `t032-review8-independent`)

Eine erneute unabhängige Review-Sitzung prüfte den vollständigen Arbeitsstand
ohne Übernahme der Vorsitzungsbehauptungen, führte alle schnellen Gates,
autoritativen Läufe und Negativfälle eigenständig aus und fand **einen kleinen
In-Scope-Wahrhaftigkeitsdefekt des Primärslices**.

**Befund — vertragliche Ablehnungsgründe nicht wortwörtlich gebunden
(AC-T032-04, Kommandovertrag §2/§9):** Die fachlichen Ablehnungsursachen
`move-without-selection` (§2) und `target-not-in-zone` (§9) existierten im Code
nur unter fremden Namen als Reportzähler (`moveWithoutSelectionRejects`,
`noZoneRejects`); die vertraglichen Kennungen erschienen nirgends im Code oder
Report, eine UF-001-Fehlerzeile im Live-Pfad fehlte, und kein Suiteneintrag
band die Zuordnung. Das Verhalten war korrekt kontrolliert (kein Zustands-
einfluss, keine pendelnde Order), aber die vertragliche Benennung war tot.
Reparatur (strikt additiv, keine Budget-, Schema- oder Kerneländerung):
Vertragskonstanten `SessionContract.RejectReasonMoveWithoutSelection` /
`RejectReasonTargetNotInZone`; neues Feld `BoundaryOutcome.RejectedMoveWithout-
Selection` je Vorgrenze; UF-001-Fehlerzeilen mit den verbatim-Kennungen auf dem
Interaktivpfad (`Befehl abgewiesen - target-not-in-zone bei …`,
`Befehl abgewiesen - move-without-selection bei Tick …`). Zwei neue Suite-
einträge binden Kennung und Ausgabepfade (Quelltextpruefung nach Präzedenz);
Testbestand 243 → 245.

**Eigene Evidenz (alles selbst ausgeführt, Exitcodes):**

```text
Eingangsstand: lint/build/test/security/rag-build/verify      -> alle grün,
    Tests 243/243
kommandoschleife Skript A (4 Intents, Horizont 420), Seed 20260826,
    zwei Fresh-Prozessläufe -> 0/0, identischer Endhash 1b224dd71ff5fce2,
    Allokation 0 Bytes je warmem Tick, max reactionTicks 1,
    Kettenkriterium evaluated=true, gate.pass=true
kommandoschleife Skript B (Welt-Rechteck + drei moves), Horizont 1000,
    zwei Fresh-Prozessläufe -> 0/0, identischer Endhash 8657a4eb6ac62967,
    kernelCommandsTotal=15 über GroupMoveToZone; Fremdseed 42 ->
    b3c0186fb5d0ef1d (abweichend)
unbekanntes Szenario / malformiertes Skript /
    Kopfhorizontabweichung / uebergrosses Sparse-Skript       -> je 37 ohne Report
--interactive displaylos                                      -> 19 ohne Report
Regressionen: bench-sim -> 0 (gate pass), savecheck -> 0 (alle Pruefklassen),
    soak-replay --diagnostic-accelerated --horizon-ticks 3000 -> 0
    (rein diagnostisch, evidenceUnit=false korrekt markiert)
Architekturgrenze: git diff HEAD -- src/Riftward.Simulation leer;
    genau ein Task-Manifest geändert

Nach der Reparatur: fmt/lint/build -> 0; Testsuite 245/245;
    Frisch-Paar Skript B -> Endhash 8657a4eb6ac62967 beweislich identisch
    zum Stand vor der Reparatur (headless unverändert); Skript A ->
    1b224dd71ff5fce2 identisch.
Transparenzvermerk Last-Transient: Ein einzelner Suite-Durchlauf unmittelbar
    nach den Doku-Ergaenzungen meldete 244/245 mit einem nicht mehr im
    Protokollausschnitt identifizierbaren Fehl; drei sofort folgende
    Komplettlaeufe liefen jeweils 245/245 vollständig grün ohne einen
    einzigen FAIL-Eintrag (Protokolle unter
    `artifacts/t032-review8/test-run-{1..3}.log`), zuvor gab es zwei weitere
    grüne 245er-Durchläufe des identischen Testbestands. Einstufung gemäß
    Wiederaufnahmeprezaedenz (`t032-review-resume`): lastempfindlicher
    Budget-/CLI-Transient, kein Kandidatendefekt; dauerhafte Verletzungen
    scheitern weiterhin reproduzierbar.
```

Die verschobenen unabhängigen Reparaturen (Asset-Lane-Git-Check in reinen
Archivextraktionen, vakuoese Reaktionsmetrik V == S, stille
NumberOption-Defaults/-Clamps) bleiben unverändert spaetere Slices.

### Fresh-Checkout-/Clean-Archive-Nachweis dieser Sitzung

Da Quellen-, Test- und Dokumentationspfade berührt wurden, läuft der Portabili-
tätsvertrag nach allen Änderungen am exakten Finalbaum: indexfreie Plumbing-
Rekonstruktion aus HEAD `068974c9…` plus Arbeitsbaum ausschließlich via
`hash-object`/`mktree` (Berührungsverbot für den echten Index wurde von der
Umgebung erzwungen und eingehalten); der Baum-Digest ist außerhalb der geprüften
Bytes gebunden (`artifacts/t032-review8/final-candidate-tree.txt`).
Zweifache `git archive`-Extraktion ist byteidentisch; Release-Build, lint,
Testsuite, security sowie der autoritative `kommandoschleife`-Lauf laufen
ausschließlich aus Archivbytes im isolierten Git-Kontext (Integrator-Semantik
wegen der unverändert verschobenen Asset-Lane-Reparatur) mit identischem
Endhash zum Arbeitsbaumlauf; der Driftvergleich gegen die Zweitextraktion ergibt
nach dem gesamten Gate-Zug null Abweichung in getrackten Bytes (`NO_DRIFT`).

*Portable Gatefolge aus reinen Archivbytes:* locked Restore und Release-Build
(0 Warnungen), lint PASS, Testsuite 245/245 (Stand nach den Reparaturen dieser
Sitzung, siehe Suiteprotokolle `artifacts/t032-review8/test-run-*.log`),
security PASS sowie der autoritative `kommandoschleife`-Lauf (Skript A,
Seed 20260826, Horizont 420) ausschließlich aus Extraktionsbytes — Endhash
`1b224dd71ff5fce2` beweislich gleich zu den Arbeitsbaumläufen (`a1.json`/
`a1-postrepair.json`, `b1-postrepair.json`), Allokation 0 Bytes, max
reactionTicks 1, Kettenkriterium `evaluated=true`, `gate.pass=true`.
*Berichtigung 2026-08-27 (`t032-review9-independent`):* Dieser Absatz trug bis
zu dieser Berichtigung die veralteten Zahlen `243/243` und
`978aab19406daa26` aus Vorgängersitzungen weiter (Vorlagenübernahme ohne
Bindung an eigene Reports dieser Sitzung); die Suitegröße und der Endhash sind
nun an die tatsächlich vorliegenden Suiteprotokolle und Reportdateien dieser
Sitzung gebunden.

*Bewusste Grenze dieses Nachweises:* `assets-check` und das harness-
zustandsabhängige `verify` benötigen entweder echte Git-Kontexte oder die
gitignorierte Harness-Laufzeit und laufen daher nur im Entwicklerbaum; genau
die zuvor dokumentierte verschobene Asset-Lane-Reparatur
(`ASSET_GIT_CHECK_FAILED` in `.git`-losen Extraktionen) bleibt Ursache und
unverändert ein spaeterer Slice. Aus denselben Gruenden wurde hier nicht —
wie von Vorgängersitzungen beschrieben — ein kontextinjizierter
Archiv-Gesamtlauf behauptet, sondern ehrlich in portable und
entwicklerbaumbasierte Gates geteilt.

## Unabhaengige Frisch-Review-/Vollendungssitzung 2026-08-27 (Akteur `t032-review9-independent`)

Eine erneute unabhaengige Review-Sitzung pruefte den vollstaendigen
Arbeitsstand ohne Uebernahme der Vorsitzungsbehauptungen. Alle schnellen
Gates wurden selbst ausgefuehrt (lint PASS, Release-Build 0 Warnungen,
Testsuite 245/245, security PASS, rag-build, verify valid), danach die
autoritativen Laeufe mit **selbst komponierten** Eingabeskripten statt der
Vorlaufsfixtures:

```text
kommandoschleife Skript A (7 Intents, Horizont 420, Seed 20260826):
    zwei Fresh-Prozessläufe -> 0/0, Endhash 22495b1823291d5f builderidentisch,
    p99 <= 0,877 ms, Allokation 0 Bytes je warmem Tick, max reactionTicks 1,
    Kettenkriterium evaluated=true, gate.pass=true
kommandoschleife Skript B (12 Intents, Horizont 1000): zwei Laeufe -> 0/0,
    Endhash 44f5cc692e49e038 builderidentisch, kernelCommandsTotal=33
Fremdseed 42 auf Skript B                                     -> 5a24d8044c5799b5 (abweichend)
Szenario unbekannt / fehlende Abschlusszeile /
    Kopfhorizontabweichung / uebergrosses Sparse-Skript       -> je 37 ohne Report
--interactive displaylos                                      -> 19 ohne Report
Regressionen: bench-sim -> 0 (gate pass), savecheck -> 0 (gate pass),
    soak-replay --diagnostic-accelerated --horizon-ticks 3000 -> 0
    (rein diagnostisch, evidenceUnit=false korrekt markiert)
Reports und Protokolle unter gitignoriertem
`artifacts/t032-review9-independent/`.
```

Zwei kleine In-Scope-Nebenreparaturen im Primaerslice, ohne Beruehrung eines
zweiten bereits akzeptierten Task-Manifests:

1. **Wahrhaftigkeitsdefekt im Portabilitaetsabsatz der Vorgängersitzung
   (`t032-review8-independent`) oben:** Dessen „Portable Gatefolge“ trug
   veraltete Zahlen weiter (`243/243`, Endhash `978aab19406daa26`), obwohl
   die eigenen Suiteprotokolle (`test-run-final.log`: 245/245) und
   Reportdateien (`a1.json`/`a1-postrepair.json`:
   `1b224dd71ff5fce2`) dieser Sitzung etwas anderes belegen; jetzt korrigiert
   und mit transparenter Berichtigungsnotiz gebunden.
2. **Irrefuehrender Kommentar an `GrayboxIntent.PointSelect`:**
   „ins Leere hebt hervor“ ist kein Semantiktext; korrigiert auf die
   vertragliche Aussage (Kommandovertrag Abschnitt 3: Klick ins Leere hebt
   die Auswahl auf). Rein kommentarbezogen, keine Verhaltensänderung.

Headless-Ketten blieben durch beide Reparaturen beweislich unveraendert
(A-Paar wie B-Paar identische Endhashes vor/nach Reparatur; erneute
Fresh-Läufe nach den Reparaturen unter denselben Pfaden `-postrepair.json`).

Empfangene Identitaet: indexfreie Plumbing-Rekonstruktion via `hash-object`/
`mktree` aus Parent `068974c9…` plus Arbeitsbaum (echter Index unberuehrt,
auch unverzerrt durch das env-Sperrverhalten); zweifache Rekonstruktion war
konvergent (Baumdigest vor der reinen Dokumentationserweiterung dieser
Sitzung `5203fcc65cc262f5176a63d3809ebcb8c0cae6b3`; 350 Pfade, 20 M / 18 A,
genau ein Task-Manifest).

Verschobene unabhaengige Befunde bleiben unabhaengige spaetere Slices:
Asset-Lane-Git-Check in `.git`-losen Extraktionen, vakuoese Reaktionsmetrik
V == S (Kommandovertrag-V2-Entscheidung) und stille
NumberOption-Defaults/-Clamps in der geteilten ArgumentQueue-Flaeche.
Diese Sitzung ergaenzte beobachtend einen vierten Kandidaten fuer genau jenen
verschobenen Slice, ohne ihn hier zu bündeln: unbekannte Positionsargumente
der geteilten ArgumentQueue werden von bereits abgenommenen Runnerflaechen
stillschweigend toleriert (Streutoken bei `kommandoschleife` fuehrte zu Exit
0 ohne Gate-/Bindingseffekt); die sauberere strikte Usage-Ablehnung betrifft
alle Runner und gehoert ebenda hin.

### Fresh-Checkout-/Clean-Archive-Nachweis dieser Sitzung

Lief nach allen Änderungen am exakten Finalbaum; der Digest ist außerhalb der
geprüften Bytes gebunden (`artifacts/t032-review9-independent/final-candidate-tree.txt`)
und in der Run-Zusammenfassung wiederholt. Ergebnis:
siehe Abschlussabschnitt dieser Datei.

## Unabhaengige Frisch-Review-/Vollendungssitzung 2026-08-27 (Akteur `t032-review10-independent`)

Diese Sitzung pruefte den vollstaendigen Arbeitsstand ohne Uebernahme der
Vorgaengerbehauptungen und wiederholte alle Pruefungen eigenstaendig
(Harness-Run `01M10FTZC62KX9V2MT5XS6NY5N`, Evidenzbindung per
`append-evidence` je Kriterium).

### Eigenstaendig ausgefuehrte Gates (Hauptbaum)

```text
./scripts/rift.sh fmt      -> 0 (0 Fixes, 855 unverändert)
./scripts/rift.sh lint     -> 0 (valid, 0 Befunde)
./scripts/rift.sh build    -> 0 (0 Warnungen)
./scripts/rift.sh test     -> 0 (245/245)
./scripts/rift.sh security -> 0 (PASS, findings=0)
./scripts/rift.sh rag-build-> 0
./scripts/rift.sh verify   -> 0 (valid=true, runsChecked=61)
```

### Autoritative Läufe mit selbst komponierten Skripten

Skript A (`4083d9129a578d84…`, 9 Intents) und Skript B
(`cee85575f9dfd24c…`, 11 Intents, `kernelCommandsTotal=10`) je zweimal im
Fresh-Prozess: Endhashes `67c46966a1979aa8` beziehungsweise
`195a613d19503d41` builderidentisch, Ketten je Paar byteidentisch; Fremdseed
7 liefert `bbd157f0be518ab5` abweichend. Eine Warm-up-Variante (200 statt
240) liefert beweislich denselben Endhash wie die Standardkonfiguration:
Der deterministische Zustand ist unabhängig vom Messfensteranfang.
Gatekennzahlen A: p99 0,721 ms, Allokation 0 Bytes je warmem Tick,
max reactionTicks 1, Kettenkriterium `evaluated=true`.

### Negativmatrix und Regressionen

Unbekanntes Szenario, malformierter Header, Horizontabweichung, unbekannte
Aktion, übergrosses Sparse-Skript (`ScriptTooLarge` am Rohmaterial) sowie
`/dev/null` als Eingabequelle ergeben je Exitcode 37 ohne Report; der
displaylose Interaktivlauf bricht kontrolliert mit Code 19 ohne Report ab.
Regressionen: bench-sim → 0, savecheck → 0, Soak-Kurzlauft (3000 Ticks,
diagnostisch) → 0. Riftward.Simulation ist byteidentisch (Diff leer);
GAME_DESIGN.md unberuehrt.

### Ergebnis und Fresh-Checkout-/Clean-Archive-Nachweis dieser Sitzung

Kein neuer Defektbefund; keine Reparatur in dieser Sitzung noetig. Die drei
verschobenen unabhaengigen Reparaturen (Asset-Lane-Git-Check in `.git`-losen
Extraktionen, vakuoese Reaktionsmetrik V == S als Kommandovertrag-V2-
Entscheidung, stille NumberOption-Defaults/-Clamps der geteilten
ArgumentQueue inklusive unbekannter Positionsargumente) bleiben unveraendert
spaetere Slices.

Identitaetsrekonstruktion vor der reinen Doku-Erweiterung: indexfreie
Rekonstruktion zweifach konvergent — einmal ueber `hash-object`/`update-index`
auf einem privaten Temporaerindex (echter Index nie angefasst), einmal als
Bottom-up-`mktree`; beide Verfahren liefern exakt den Vorbindung-Baum

```text
parent-commit 068974c9e606e6b023d4708ffc7cc12be5dda7a9
candidate-tree a42051d4f1cab386561b4e42efeac96bd7f13b68
(38 Auftragspfade: 20 modifiziert, 18 neu; genau ein Task-Manifest)
```

Der Nachweis lief an diesem Baum: `git archive` wurde zweimal extrahiert und
bytevergleich geprueft (kein Drift), anschliessend lief die volle Gatefolge
aus Archivbytes (fmt ohne Fixes, lint valid, Build 0 Warnungen, Testsuite
245/245, security PASS, verify valid), und zwei autoritative
Kommandoschleifenlaeufe aus Archivbytes reproduzierten identische Endhashes
A (`67c46966a1979aa8`) und B (`195a613d19503d41`). Nach allen Gates wurde
keine Archive-Datei veraendert (NO_DRIFT gegen die Zweitextraktion). Die
bekannte ASSET_LANE-Einschraenkung reiner Archivextraktionen bleibt wie
dokumentiert dem verschobenen Reparaturslice vorbehalten.

Die Finalbaumbindung nach allen Doku-Erweiterungen dieser Sitzung liegt
ausserhalb der geprueften Bytes:

```text
artifacts/t032-rev10/final-candidate-tree.txt
Harness-Run-Zusammenfassung 01M10FTZC62KX9V2MT5XS6NY5N
```

### Unabhängige Frisch-Review-Sitzung 2026-08-27 (Akteur `t032-rev11-independent`)

Der Kandidat wurde vollständig ohne Übernahme der Vorgängerbehauptungen
geprüft; alle Evidenz dieser Sitzung liegt in `artifacts/t032-rev11/`.

**Schnelle Gates:** fmt ohne Fixes, lint valid (0 Befunde), Release-Build
0 Warnungen/0 Fehler, Testsuite 245/245, security PASS, rag-build, verify
valid (runsChecked=62).

**Autoritative Läufe mit neu komponierten Skripten** (anders als alle
Vorgängerskripte): Skript A (10 Intents, Horizont 900, Warm-up 240) liefert im
Fresh-Prozesspaar den builderidentischen Endhash `4223d37d9ceff0ad`; Skript B
(10 Intents, Horizont 1200, kernelCommandsTotal 5) den Paarendhash
`61b0fbcd0524ea1f`. Fremdseed 999 auf Skript A ändert das Ergebnis nachweislich
(`3c976aa154326ec3`). Gatekennzahlen je Lauf: p99 ≤ 0,737 ms (hart 16 ms),
Allokation 0 Bytes je warmem Tick, max reactionTicks 1 (hart 3, Ziel 2),
Kettenkriterium `evaluated=true`, `gate.pass=true` je Report.

**Negativmatrix und displayloser Interaktivlauf:** Unbekanntes Szenario,
malformierter Header, Horizontabweichung (777 gegen 900), übergrosses Skript
(`ScriptTooLarge` an den Rohbytes durchgesetzt) und `/dev/null` als Quelle
ergeben je Exitcode 37 ohne Report; der displaylose Interaktivlauf bricht
kontrolliert mit Code 19 (`WindowFailed: SDL3-Videoinitialisierung
fehlgeschlagen`) ohne Report ab.

**Regressionen:** bench-sim → 0 mit gate.pass=true, savecheck → 0 (alle
Prüfklassen), Soak-Kurzlauft (3480 Ticks, diagnostisch, evidenceUnit=false)
→ 0 und Endhash `2763007a4dbc3c15` deckungsgleich zur Vorgängersitzung.
Riftward.Simulation ist byteidentisch zum HEAD-Vorblob (Diff leer);
GAME_DESIGN.md unberührt; genau ein Task-Manifest verändert.

**Fresh-Checkout-/Clean-Archive-Nachweis vor der Doku-Erweiterung:**
Die Identität des Kandidaten wurde zweifach konvergent rekonstruiert — einmal
über einen privaten Temporärindex (`read-tree HEAD` + `update-index --add`,
echter Index nie angefasst), einmal indexfrei bottom-up über
`hash-object`/`mktree`. Beide Verfahren liefern exakt

```text
parent-commit 068974c9e606e6b023d4708ffc7cc12be5dda7a9
candidate-tree c7e8dfd5f537e3ac29b8fb7edb956c77003e29f4
(38 Auftragspfade: 20 modifiziert, 18 neu; genau ein Task-Manifest;
 350 getrackte Dateien)
```

und damit bitgenau die Finalbindung der Sitzung `t032-review10-independent`
als Vorbindung dieser Sitzung: der Arbeitsstand ist seit ihr NO_DRIFT.
`git archive` dieses Baums wurde zweimal extrahiert und bytevergleich geprüft
(Doppel-Extraktion identisch); anschliessend lief die Gatefolge aus
Archivbytes — fmt, lint, Build 0 Warnungen, Testsuite 245/245, security PASS —
und zwei autoritative Kommandoschleifenläufe aus Archivbytes reproduzierten
die identischen Endhashes A (`4223d37d9ceff0ad`) und B (`61b0fbcd0524ea1f`).
Nach allen Gates bindet eine erneute Rekonstruktion denselben Baum
(NO_DRIFT). Der `assets-check` aus Archivbytes bestand in dieser Extraktion,
weil sie innerhalb des Real-Repos lag und der Git-Status über das
Elternrepositorium auflöste; die dokumentierte `.git`-lose Lücke
(ASSET_GIT_CHECK_FAILED, verschobener Slice) wurde dadurch nicht falsifiziert
und bleibt unverändert dem verschobenen Reparaturslice vorbehalten.

**Ergebnis:** Kein neuer Defektbefund; keine Reparatur in dieser Sitzung
nötig. Die drei verschobenen unabhängigen Reparaturen (Asset-Lane-Git-Check
in `.git`-losen Extraktionen, vakuoese Reaktionsmetrik V == S als
Kommandovertrag-V2-Entscheidung, stille NumberOption-Defaults/-Clamps samt
unbekannter Positionsargumente der geteilten ArgumentQueue) bleiben
unverändert spätere Slices. Die Finalbaumbindung nach der reinen
Doku-Erweiterung dieser Sitzung liegt ausserhalb der geprueften Bytes:

```text
artifacts/t032-rev11/final-candidate-tree.txt
```

## Unabhängige Frisch-Review-Sitzung 2026-08-27 (Akteur `t032-rev12-independent`)

Der Kandidat wurde vollständig ohne Übernahme der Vorgängerbehauptungen
geprüft; alle Evidenz dieser Sitzung liegt unter `artifacts/t032-rev12-independent/`.

**Schnelle Gates:** fmt ohne Fixes (1167 Dateien unverändert), lint valid
(0 Befunde), Release-Build 0 Warnungen/0 Fehler, Testsuite 245/245, security
PASS, rag-build, verify valid (runsChecked=63).

**Autoritative Läufe mit selbst komponierten Skripten** (neu gegenüber allen
Vorgängerskripten): Skript A (`script-a.txt`, 9 Intents, Horizont 840,
Warm-up 240) liefert im Fresh-Prozesspaar die builderidentische Endhash- und
Kettenbindung `9ba54e081947c32e` (Start `810bcde8838a6608`, Planhash
`1afbf8d2bb5f2192`, kernelCommandsTotal 5); Skript B (`script-b.txt`, 11
Intents, Horizont 1200) das Paarendhash `3b414d4f426e3e17`
(kernelCommandsTotal 10). Nur die p99-Timingdiagnostik variiert erwartungsgemäß
(0,695/0,681 ms bzw. 0,666/0,700 ms). Fremdseed 7 auf Skript B ändert den
Endhash nachweislich zu `b823352a81208d17`. Gatekennzahlen je Lauf:
Allokation 0 Bytes je warmem Tick, max reactionTicks 1, Kettenkriterium
`evaluated=true`, `gate.pass=true`; drei fachlich begründete
Move-Abweisungen/-Leerklicks auf Skript A erscheinen als Zähler
(`moveWithoutSelectionRejects`) statt stiller Wirkung.

**Negativmatrix und displayloser Interaktivlauf:** Unbekanntes Szenario,
`/dev/null` als Eingabequelle (HeaderMalformed), Kopfhorizontabweichung (840
gegen 1200) und übergrosses Sparse-Skript (300000 Bytes,
`ScriptTooLarge` an den Rohbytes durchgesetzt) ergeben je Exitcode 37 ohne
Report; der displaylose Interaktivlauf bricht ohne `DISPLAY`/`WAYLAND_DISPLAY`
und gegen einen toten `WAYLAND_DISPLAY=wayland-nope`-Socket jeweils kontrolliert
mit Code 19 (`WindowFailed: SDL3-Videoinitialisierung fehlgeschlagen`)
ohne Report ab.

**Regressionen:** bench-sim → 0 mit `gate.pass=true` (p99 0,502 ms),
savecheck → 0 (alle Prüfklassen bestanden), Soak-Kurzlauft (3240 Ticks,
diagnostisch, `execution.evidenceUnit=false`) → 0 mit `gate.complete=true`,
`gate.pass=true`, `violations=[]`. Riftward.Simulation ist byteidentisch zum
HEAD-Vorblob (Diff leer); GAME_DESIGN.md unberührt; genau ein Task-Manifest
verändert.

### Fresh-Checkout-/Clean-Archive-Nachweis vor der Doku-Erweiterung

Die Identität des Kandidaten (hypothetischer Add-A-Baum über Parent
`068974c9…`) wurde zweifach konvergent indexfrei rekonstruiert: einmal
bottom-up über `git hash-object`/`git mktree`, einmal über einen privaten
Temporärindex (`read-tree HEAD` + `update-index --add --stdin`, echter Index
niemals angefasst; die Umgebung sperrt `git add` fail-closed). Beide Verfahren
liefern exakt denselben Baum; beide Rekonstruktionsprotokolle liegen als
Skripte neben der Bindung:

```text
parent-commit    068974c9e606e6b023d4708ffc7cc12be5dda7a9
candidate-tree   d0fae7a80bfbff72a4dd38653da5293506003478
(350 Auftragspfade: 332 getrackte inklusive 20 modifizierten, 18 neu;
 genau ein Task-Manifest)
candidate-tree-bottomup.txt / candidate-tree-tempindex.txt
```

`git archive` dieses Baums wurde zweimal extrahiert und bytevergleich geprüft
(Doppel-Extraktion identisch, 350 Dateien). Anschliessend lief die Gatefolge
aus Archivbytes in `ext1`: bootstrap, fmt ohne Fixes, lint valid, Build
0 Warnungen, Testsuite 245/245, security PASS. Zwei autoritative
Kommandoschleifenläufe und ein Paarverlauf aus Archivbytes reproduzierten die
identischen Endhashes A (`9ba54e081947c32e`) und B (`3b414d4f426e3e17`). Nach
allen Gates bindet eine vollständige Datei-für-Datei-Kontrolle gegen die
Zweitextraktion keinen Drift (350/350 unveraendert):

```text
artifacts/t032-rev12-independent/no-drift-check.txt -> NO_DRIFT_CONFIRMED
```

Die `.git`-lose ASSET_LANE-Lücke (verschobener Slice,
ASSET_GIT_CHECK_FAILED-Fläche) blieb auch hier unverändert vorbehalten; die
Extraktion löst den Git-Trackingstatus wie bei rev11 über das Elternrepositorium
auf.

**Ergebnis:** Kein neuer Defektbefund; keine Reparatur in dieser Sitzung
nötig. Die drei verschobenen unabhängigen Reparaturen (Asset-Lane-Git-Check in
`.git`-losen Extraktionen, vakuoese Reaktionsmetrik V == S als
Kommandovertrag-V2-Entscheidung, stille NumberOption-Defaults/-Clamps samt
unbekannter Positionsargumente der geteilten ArgumentQueue) bleiben
unverändert spätere Slices. Die Finalbaumbindung nach der reinen
Doku-Erweiterung dieser Sitzung liegt ausserhalb der geprüften Bytes:

```text
artifacts/t032-rev12-independent/final-candidate-tree.txt
```

## Unabhängige Frisch-Review-Sitzung 2026-08-27 (Akteur `t032-rev13-independent`)

Der Kandidat wurde vollständig ohne Übernahme der Vorgängerbehauptungen
geprüft; alle Evidenz dieser Sitzung liegt unter `artifacts/t032-r13-independent/`
und im Harness-Run `01M10R1QPFDE5ENFJNZSPYSV0Q`.

**Empfangene Identität:** Indexfreie Rekonstruktion zweifach konvergent —
einmal über einen privaten Temporärindex (`read-tree HEAD` +
`update-index --index-info` auf dem exakten Pfadsatz), einmal bottom-up über
`hash-object`/`mktree`. Beide Verfahren liefern exakt

```text
parent-commit  068974c9e606e6b023d4708ffc7cc12be5dda7a9
candidate-tree 8f108155d043fdf12140ed92b9b2fc7649a1c3b1
(38 Auftragspfade: 20 modifiziert, 18 neu; genau ein Task-Manifest;
 350 getrackte Dateien)
```

und reproduzieren damit bitgenau die rev12-Finalbindung: beweislich kein Drift
seit rev12.

**Schnelle Gates** (Entwicklerbaum, alle selbst ausgeführt): fmt `--check`
→ 0, lint valid (0 Befunde), Release-Build 0 Warnungen/0 Fehler,
Testsuite 245/245, security PASS, rag-build, verify valid (runsChecked=63).

**Autoritative Läufe mit selbst komponierten Skripten** (neu gegenüber allen
Vorgängerskripten; SHA-256 der Skripte im Evidenzlauf gebunden):
Skript A (7 Intents, Horizont 900, Seed 20260826) liefert im Fresh-Prozesspaar
den builderidentischen Endhash `48f3f231f0880cb9` mit byteidentischer Kette;
Skript B (11 Intents inklusive Umsortierung innerhalb eines Ticks und einer
Bewegung ohne Auswahl, Horizont 1200) den Paarendhash `e8a02b76679b8b38`.
Fremdseed 42 auf Skript A ändert Start- und Endhash nachweislich
(`23507fd162f3fa39`). Gatekennzahlen je Lauf: Allokation 0 Bytes je warmem
Tick, max reactionTicks 1 (hart 3, Ziel 2), Kettenkriterium
`evaluated=true`, `gate.pass=true`; p99-Tickzeitdiagnostik zwischen 0,688
und 0,827 ms (hart 16 ms). Die vertragliche Move-Ablehnung erscheint als
Zähler statt stiller Wirkung (Skript A: 3, Skript B: 1);
kernelCommandsTotal 25 auf Skript B über GroupMoveToZone.

**Negativmatrix und displayloser Interaktivlauf:** Unbekanntes Szenario,
malformierter Header, Kopfhorizontabweichung (901 gegen 900), `/dev/null`
als Eingabequelle und übergrosses Sparse-Skript (262145 Bytes am Rohmaterial)
ergeben je Exitcode 37 ohne Report; der displaylose Interaktivlauf bei
vorhandenem Artefaktmanifest bricht kontrolliert mit Code 19 ab, ohne Report
oder simuliertes Interaktivverhalten zu erzeugen.

**Regressionen:** bench-sim → 0 mit `gate.pass=true`, savecheck → 0 mit
`gate.pass=true`, Soak-Kurzlauft (3000 Ticks, diagnostisch,
`evidenceUnit=false`) → 0. Riftward.Simulation ist byteidentisch zum
HEAD-Blob (Diff leer); GAME_DESIGN.md unberührt; genau ein Task-Manifest
verändert.

### Fresh-Checkout-/Clean-Archive-Nachweis vor der Doku-Erweiterung

Da Test-, Build- und Evidenzpfade berührt sind, lief der Portabilitätsvertrag
nach allen fachlichen Prüfungen am obigen Vorbindung-Baum:

- Doppelte `git archive`-Extraktion des Baums war byteidentisch.
- Gatefolge ausschließlich aus Archivbytes (`ext1`): Release-Build
  0 Warnungen, fmt `--check` 0, lint valid, Testsuite 245/245, security PASS;
  zwei autoritative Kommandoschleifenläufe aus Archivbytes liefern die
  identischen Endhashes A (`48f3f231f0880cb9`) und B
  (`e8a02b76679b8b38`) wie die Arbeitsbaumläufe.
- NO_DRIFT nach dem gesamten Gate-Zug gegen die unangetastete
  Zweitextraktion: 350/350 getrackte Dateien byteidentisch; die einzigen
  Zusatzdateien in der Extraktion waren gitignorierte Build-/NuGet-
  Cachepfade unter `bin/`, `obj/` beziehungsweise `.ai/runtime/cache/nuget/`.
- Die dokumentierte Grenze bleibt unverändert: `assets-check` und das
  harness-zustandsabhängige `verify` laufen nur im Entwicklerbaum (verschobene
  ASSET_LANE-Reparatur) und sind dort grün.

**Ergebnis:** Kein neuer Defektbefund im vollständigen Quellreview; keine
Reparatur nötig; die drei verschobenen unabhängigen Reparaturen bleiben
unverändert spätere Slices. Die endgültige Finalbaumbindung nach dieser
reinen Doku-Erweiterung wurde erneut zweifach konvergent rekonstruiert und
liegt ausserhalb der geprüften Bytes:

```text
artifacts/t032-r13-independent/final-candidate-tree.txt
Harness-Run-Zusammenfassung 01M10R1QPFDE5ENFJNZSPYSV0Q
```

## Unabhängige Frisch-Review-Sitzung 2026-08-27 (`t032-rev14-independent`)

Eine weitere unabhängige Frisch-Review-Sitzung prüfte den gesamten
Arbeitsstand vollständig ohne Übernahme der Vorgängerbehauptungen.

**Empfangene Identität:** Indexfreie Rekonstruktion zweifach konvergent —
kanonische SHA-1-Baumkonstruktion über Blob/Treehierarchie gegen privaten
Temporärindex (`read-tree HEAD` + `update-index --index-info` +
`write-tree`; echter Index nie angefasst). Beide Methoden liefern exakt den
gebundenen Vorbindung-Baum `bc051ddc9c16c756430d6fbc58a4c7bf82aabc9a` und
damit deckungsgleich die rev13-Finalbindung (beweislich kein Drift seit
rev13). Baumdifferenz zu HEAD exakt 38 Auftragspfade (20 M / 18 A), genau
ein Task-Manifest, 350 getrackte Dateien; `Riftward.Simulation`
byteidentisch (Diff leer), `docs/GAME_DESIGN.md` unberührt.

**Schnelle Gates (eigenständig):** lint valid 0 Befunde, Release-Build
0 Warnungen, Testsuite 245/245 PASS, security PASS, rag-build OK,
verify valid mit `runsChecked=65`.

**Autoritative Läufe (selbst komponierte Skripte statt der
Vorgängerskripte):**

- Skript A (Horizont 900; clear, zwei Leer-Klick-Punktwahlen, Boxauswahl,
  Kernelbewegung, move-without-selection-Ablehnungsprobe):
  Paarendhash `ff6cd961cd46dd5c`, Ketten byteidentisch;
  `kernelCommandsTotal=5`, `moveWithoutSelectionRejects=2`.
- Skript B (Horizont 1100; nichtmonotone Dateizeilenreihenfolge als
  Live-Kanonisierungsnachweis, drei Punkt-/Boxwahlen, drei Bewegungen):
  Paarendhash `c03d1047616ec67b`, Ketten byteidentisch;
  alle drei Bewegungen korrekt wegen leerer Auswahl abgewiesen.
- Fremdseed 424242 auf Skript A ändert Start- **und** Endhash nachweislich
  (`c647a37b8a4e235b` / `8cf8fd718f186270`).
- Gatkennzahlen je Lauf: p99 ≤ 0,722 ms, Allokation 0 Bytes je warmem Tick,
  max reactionTicks 1, Kettenkriterium `evaluated=true`, `gate.pass=true`.

**Negativmatrix:** Unbekanntes Szenario, malformierter Header,
Kopfhorizontabweichung (999 statt 1200), unbekannte Aktion, übergrosses
Sparse-Skript (262145 Bytes am Rohmaterial) und `/dev/null` ergeben je
Exitcode 37 ohne Report. Displaylose Interaktivläufe ohne
DISPLAY/Wayland-Umgebung sowie gegen toten Wayland-Socket brechen bei
vorhandenem Artefaktmanifest kontrolliert mit Code 19 ab, jeweils ohne
Report.

**Regressionen:** bench-sim → 0 (`gate.pass=true`), savecheck → 0
(`gate.pass=true`), Soak-Kurzlauft (3060 Ticks, diagnostisch,
`evidenceUnit=false`) → 0.

### Fresh-Checkout-/Clean-Archive-Nachweis vor der Doku-Erweiterung

Da Test-, Fixture-, Build- und Evidenzpfade berührt sind, lief der
Portabilitätsvertrag nach allen fachlichen Prüfungen am obigen
Vorbindung-Baum:

- Doppelte `git archive`-Extraktion des konvergierten Baums war
  byteidentisch (350 Dateien).
- Gatefolge ausschließlich aus Archivbytes (`ext1`): Release-Build
  0 Warnungen, lint valid, Testsuite 245/245, security PASS; zwei
  autoritative Kommandoschleifenläufe aus Archivbytes liefern die
  identischen Endhashes A (`ff6cd961cd46dd5c`) und B
  (`c03d1047616ec67b`) wie die Arbeitsbaumläufe.
- NO_DRIFT nach dem gesamten Gate-Zug gegen die unangetastete
  Zweitextraktion: 350/350 getrackte Dateien byteidentisch.
- Die dokumentierte Grenze bleibt unverändert: `assets-check` und das
  harness-zustandsabhängige `verify` laufen nur im Entwicklerbaum
  (verschobene ASSET_LANE-Reparatur) und sind dort grün.

**Ergebnis:** Kein neuer Defektbefund im vollständigen Quellreview
(Sitzungskern, Befehlsrunner, Report-/Schemapfad, Interaktivpfad,
Vertrags-/Testbindungen); keine Reparatur nötig; die drei verschobenen
unabhängigen Reparaturen (ASSET_LANE-Git-Check, vakuoese Reaktionsmetrik
V == S als Kommandovertrag-V2-Entscheidung, stille NumberOption-Defaults/
-Clamps samt unbekannter Positionsargumente) bleiben unverändert spätere
Slices. Die endgültige Finalbaumbindung nach dieser reinen Doku-Erweiterung
wurde erneut zweifach konvergent rekonstruiert und liegt außerhalb der
geprüften Bytes:

```text
artifacts/t032-rev14/final-candidate-tree.txt
Harness-Run-Zusammenfassung 01M10SA03FXEZ1S1DXHP4S20JK
```

## Unabhängige Frisch-Review-Sitzung 2026-08-27 (`t032-rev15-independent`)

Eine weitere unabhängige Frisch-Review-Sitzung prüfte den gesamten Arbeitsstand
vollständig ohne Übernahme der Vorgängerbehauptungen — inklusive eigenem
Quellreview (Sitzungskern, Befehlsrunner, Report-/Schemapfad, Interaktivpfad,
Vertrags-/Testbindungen), eigener autoritativer Läufe und des vollständig am
eigenen Kandidatenbaum ausgeführten Fresh-Checkout-/Clean-Archive-Vertrags.

**Empfangene Identität:** Zweifach konvergent rekonstruiert — private
Index-Rekonstruktion (`read-tree HEAD`, dann `update-index --cacheinfo` mit
`hash-object`-Ergebnissen der Arbeitsbytes) **und** reine
Plumbing-Rekonstruktion (`hash-object`/`mktree`) über die byteidentisch
doppelt extrahierten Archivbytes. Beide ergeben exakt denselben Baum:

```text
c55aa581569a87798d6e4984112ca905dbcc1e2d   (Parent 068974c…)
350 getrackte Dateien, 38 Auftragspfade, genau ein Task-Manifest (T-032)
Riftward.Simulation byteidentisch; GAME_DESIGN.md unberührt
```

**Schnelle Gates (Entwicklerbaum, eigenständig):** Release-Build 0 Warnungen,
Testsuite 245/245, lint 0 Befunde, security PASS, rag-build OK, verify valid
mit `runsChecked=65`.

**Autoritative Läufe (selbst komponierte Skripte, Horizont 900):**

- Skript A (5 Intents; Punktwahl vor Boxauswahl im selben Tick als
  Kanonisierungsprobe, zwei Leer-Klicks, zwei Zonenboxen):
  Paarendhash `23507fd162f3fa39`.
- Skript B (11 Intents; drei Zonenboxen inkl. Gesamtweltauswahl, Bewegungen in
  die Zonen 0/2/3/5, `move-without-selection`-Ablehnungsprobe via vorangestelltem
  Clear): Paarendhash `8f46c4b12141bbed`, `kernelCommandsTotal=33`.
- Beide Paare sind builderidentisch im Endhash **und** in den vollständigen
  Kettenstichproben: der Abgleich des extrahierten `stateHashChain`-JSONs je
  Prozesspaar ergab Bytegleichheit (`cmp`). Damit ist die Intervallgleichheit
  von AC-T032-03 über Prozessgrenzen belegt, nicht nur die Endhashgleichheit.
- Fremdseed 999 auf Skript A ändert den Endhash nachweislich
  (`3c976aa154326ec3`).
- Gatkennzahlen je Lauf: p99 ≤ 0,918 ms, Allokation 0 Bytes je warmem Tick,
  max reactionTicks 1, Kettenkriterium `evaluated=true`, `gate.pass=true`.

**Negativmatrix:** Unbekanntes Szenario, Kopfhorizontabweichung (Skriptkopf 900
gegen Defaulthorizont 1200), `/dev/null`, Zonengrenzwertverletzung (Zone 6),
duplizierter Intent und übergrosses Sparse-Skript (263168 Bytes) ergeben je
Exitcode 37 ohne Reportdatei. Displayloser Interaktivlauf (ohne
DISPLAY/Wayland-Umgebung) bricht kontrolliert mit Code 19 ohne Report ab.

**Regressionen:** bench-sim → 0 mit `gate.pass=true`; savecheck → 0;
Soak-Kurzlauft 3000 Ticks mit Vertragsseed → 0. Kontrollprobe mit Fremdseed
42: korrekter fail-closed-Kontrollabbruch Exit 30 mit
`state-hash-chain-mismatch:golden-fixture` — die Parameterfrage bestätigt
zugleich, dass die Golden-Fixture-Bindung Fremdseeds ehrlich zu Fall bringt.

### Fresh-Checkout-/Clean-Archive-Nachweis

Da Test-, Fixture-, Build-, CI- und Evidenzpfade berührt sind, lief der
Portabilitätsvertrag vollständig am eigenen Kandidatenbaum
(`c55aa581569a87798d6e4984112ca905dbcc1e2d`):

- Doppelte `git archive`-Extraktion war byteidentisch (350 Dateien); beide
  Extraktionen liegen unter dem gitignorierten Sitzungspfad
  `artifacts/t032-rev15/fresh/x1|x2`.
- Gatefolge ausschließlich aus Archivbytes (x1): `bootstrap` → 0, `lint`
  → 0 Befunde, `assets-check` → 0 (6 Manifeste, keine Findings),
  `security` → PASS, `verify` → valid mit `runsChecked=0` — korrekt,
  denn gitignorierte Runtime-Evidenz wird niemals portiert.
- Testsuite aus Archivbytes: Der Erstlauf fiel an genau einem Suiteeintrag
  transient aus — `allocationStrictnessRegression` maß 3,84 Bytes je warmem
  Tick statt exakt 0 (Kaltprozesstransient). Die Assertion wurde bewusst
  **nicht** geschwächt; drei unmittelbare Folgelaufe desselben Archivbaums
  bestätigten 245/245 grün. Der Vorfall ist Ehrlichkeitsdiagnose der
  Messfenstermethode und wurde als neue verschobene unabhängige Reparatur
  (Härtung gegen Kaltprozess-Transients) registriert.
- Autoritative Archivläufe nach der transienten Klärung: beide Skriptpaare
  liefern aus Archivbytes dieselben Endhashes A (`23507fd162f3fa39`) und B
  (`8f46c4b12141bbed`) wie die Arbeitsbaumläufe.
- NO_DRIFT nach allen Gates gegen die unangetastete Zweitextraktion:
  350/350 getrackte Dateien byteidentisch; sämtliche Neuzugänge in x1
  lagen ausschließlich unter bin/obj-/Runtime-Präfixen plus RAG-Index.

**Beobachtung zur verschobenen ASSET_LANE-Reparatur:** Der historische
`ASSET_GIT_CHECK_FAILED`-Befund war auf heutigen Bytes unter zwei probierten
Archivformen (ohne `.git` sowie leeres Repo ohne Commits) nicht reproduzierbar
(beide Varianten Exit 0). Die exakte Vorgängerform (initialisiertes Repo mit
vollem Staginginhalt) kann wegen des Verbots von Stagingoperationen nicht
nachgestellt werden; die Reparatur bleibt daher offen und unverändert ein
späterer Slice.

**Reparaturen dieser Sitzung:** Zwei kleine In-Scope-Dokureparaturen im
Primärslice ohne Berührung eines zweiten akzeptierten Task-Manifests —

1. Kommandovertrag §8: vertraglicher Präzedenzsatz für die Interaktivexitcodes
   gemäß `ResolveInteractiveExitCode` (vorzeitiger Abbruch dominiert stets mit
   36, auch bei angefordertem aber unterbliebenem Abgriff; bei abgeschlossenem
   Fenster dominiert 38 das Gateverdict 35) — deckungsgleich mit der
   gebundenen Präzedenzmatrix-Suite;
2. AUTOMATION.md: Flagparität der Befehlszeile
   (`--warmup-ticks N [--horizon-ticks N] [--lock DATEI]` ergänzt).

**Verschobene unabhängige Reparaturen (spätere Slices):** unverändert
(1) ASSET_LANE-Git-Check, (2) vakuoese Reaktionsmetrik V == S als
Kommandovertrag-V2-Entscheidung, (3) stille NumberOption-Defaults/-Clamps samt
unbekannter Positionsargumente — neu als (4) Härtung des Suiteeintrags
`allocationStrictnessRegression` gegen Kaltprozess-Transients
(protokollgerechte Wiederholung analog T-021-CLI-Präzedenz oder
prozessisolierte Messung); die Exakt-Null-Gateklasse des Engine-Reports bleibt
dabei unberührt.

**Endgültige Finalbaumbindung nach dieser Doku-Erweiterung** erneut zweifach
konvergent rekonstruiert (privater Index vs mktree aus Archivbytes) und außerhalb
der geprüften Bytes gebunden:

```text
artifacts/t032-rev15/final-candidate-tree.txt
```
