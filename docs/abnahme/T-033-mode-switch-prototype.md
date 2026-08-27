# Abnahme T-033 – Hybrid-Mode-Switch-Prototyp

**Status:** Implementierung durch den Builder-Kandidaten (Gebundene
Critic-Evidence SHA-256 `474f84be…`, Riftward-HEAD `0a1d6f4d…`) vom
unabhängigen Review-/Vollendungslauf 2026-08-28 geprüft, in den Pflichtanteilen
vollendet und mit allen lokalen Pflichtgates sowie dem
Fresh-Checkout-/Clean-Archive-Vertrag am exakten Kandidatenbaum belegt. Keine
Erfolgsbehauptung des Builder-Kandidaten wurde ungeprüft übernommen. Diese
Datei ist die aktuelle Wahrheit des Slices; ein Sitzungschronik wird bewusst
nicht angehängt.

## Gelieferter und vervollständigter Umfang

- **Modevertrag** `docs/MODEVERTRAG.md` V1 (Abschnitt 0, vor der
  Implementierung): Heldenidentität (`session-hero-agent-index-0-group-0-v1`),
  Steuerungsabbildung (`hero-direction-steering-zones-v1`), Wechselauslöser
  (`mode-toggle-keymap-action-v1`, Standard Tab/Scancode 43), kanonische
  Same-Tick-Regel (`same-tick-switch-last-effective-next-next-v1`, M = S + 2),
  Skriptobermenge `graybox-input-script-v2`, Wechselreaktionsableitung
  (100/150 ms ÷ 50 ms = 2/3 Ticks), Gatematrix-Kriterium 6, Playtestprotokoll,
  Exitcode-Erhaltung; autorisierte additive Kommandovertrags-Präzisierung
  (Abschnitt 12, `mode-scoping-v1`, `context-visible-rejection-v1`).
- **Sitzungskern** (`Riftward.Session`, BCL-only): Modussitzung
  (`SessionMode`, `ModeSwitchEvent`, `ModeTelemetry`), Kontexttrennung mit
  unterscheidbaren Dispositionen, Dedupe-Regel der persönlichen Lenkung,
  Heldenansicht (`HeroTracker`), exakte Ganzzahl-Lenkauflösung
  (`HeroDirectionSteering`, Int128-Kreuzmultiplikation, Tie-Break niedrigste
  Zonennummer), Verfolgungskamerazustand (`HeroChaseCamera`, 32°/9 m,
  Clamps 5–16 m, Weltrand), Versionierung `ModeContract`. Riftward.Simulation
  ist byteidentisch (Diff leer).
- **Befehl `kommandoschleife`** (Report Schemaversion 2): Headless v1/v2 über
  denselben Pipelinepfad; interaktiv vollständig verdrahtet — `mode-switch`
  (Tab) erzeugt Live-Wechsel-Intents, Pan-Tasten werden im persönlichen Modus
  zu richtungsgelenkter Lenkung, strategische Maussemantik erhält dort die
  sichtbare Abweisung `strategy-intent-in-personal-mode` am Live-Pfad plus
  echten Reportzähler `interactiveContextRejections` (kein hartkodierter Wert
  mehr), Verfolgungskamera, Held-/Modus-Badge (`hero-mode-badge-v1`, Diamant
  ruhend/Cyan gegenüber pulsierend/Orange — zwei Kanaele, NF-005), Titel-HUD
  (`title-hud-mode-herozone-v1`, per `SDL_SetWindowTitle`), Abgriffpaar
  (`-strategisch`/`-persoenlich`, je einer pro Modus über demselben
  gebundenen Weltzustand, beide hashgebunden im Report), kontrollierter
  Code-19-Abbruch ohne Display. Ohne Display kein simuliertes
  Interaktivverhalten.
- **Dokumentation/Register:** AUTOMATION.md (v2-Befehlsnutzung, Schemaversion
  2, Interaktiv-Hybridverhalten), PERFORMANCE_BUDGET.md (Nachweisortnotiz
  Wechselreaktion innerhalb der unveränderten Budgetzeile), NATIVE_UNTERBAU.md
  (Exitcodes 35/38, Reportumfang, Interaktivverhalten),
  `.ai/evals/quality-gates.json` (G-PERF-Kommando und Passbedingung um
  Kriterium 6 präzisiert, kein Wertwechsel), KOMMANDOVERTRAG.md Abschnitt 12,
  ARCHITEKTUR.md (bereits im Freigabelauf). GAME_DESIGN.md und
  ANFORDERUNGEN.md sind durch die Implementierung unberührt.

## Kriterien und Evidenz (eigene Läufe dieser Sitzung)

| Kriterium | Stand | Evidenz |
|---|---|---|
| AC-T033-01 | erfüllt | Modevertrag vor Implementierung; Spiegeltest `modeContractMirrorsDocumentedValues` gegen MODEVERTRAG/KOMMANDOVERTRAG §12/Keymap |
| AC-T033-02 | erfüllt | v2-Hybrid (3 Wechsel) über den öffentlichen Befehl: zwei Fresh-Prozessläufe Exit 0/0, Endhash `420f85c9acf32a1d` builderidentisch; Twin ohne Wechsel-Intents derselbe Endhash und byteidentische Ketten; Fremdseed 42 → `edcf1170e4f2d41e`; Wechsel erzeugen null Kernbefehle (`kernelCommandsTotal=10` ausschließlich box/move) |
| AC-T033-03 | erfüllt | Lenk-Äquivalenztest gegen frischen Kontrollkern (tickgenau hashidentisch); Report weist Heldenposition/Zone/Pfadstatus je Wechselgrenze aus (z. B. S=260: `(9379,46093)` mm, Zone 0, Pfad 2) |
| AC-T033-04 | erfüllt | Kontext-Negativmatrix mit unterscheidbaren Dispositionen ohne Kernbefehle; Interaktivpfad strukturgebunden (`interactiveHybridWiringIsBoundToSources`); sichtbare Abweisung am Live-Pfad plus Zähler; RTS-Maussemantik im persönlichen Modus nicht gebunden |
| AC-T033-05 | erfüllt | Kriterium 6 fail-closed: Hybridlauf `gate.switchReaction = {evaluated:true, max:2, targetMet:true}`; Fault-Injection 4 Ticks → Verletzung `switch-reaction-ticks-above-hard-limit`; Vakuumpass mit Grund statt stiller Behauptung; übrige Grenzwerte unverändert, Diagnosefelder `gateCoupled=false`, Profile `NOT-MEASURED` |
| AC-T033-06 | erfüllt im Code-/Vertragsumfang (Restpunkt 1) | Interaktiv-Hybrid vollständig verdrahtet (Kamera, Badge, Titel-HUD, Umschaltaktion, Lenkung, Kontextabweisung, Abgriffpaar, Code-19-Pfad); displayloser Lauf dieser Sitzung → Code 19 („No available video device") ohne Report und ohne Simulation; Capture-Verhalten schema-/testgebunden; manueller Smoke und Playtestausführung bleiben der Displaysession vorbehalten (vertraglich ausgewiesener Restpunkt) |
| AC-T033-07 | erfüllt | v2-Obermenge, Legacy-v1 byteidentisch gültig (v1+steer → UnknownAction, Code 37 ohne Report); Ablehnungsklassen je unterscheidbar; `scriptSha256`/`intentPlanHash` auf neuen Aktionen (Goldbytes + FNV-Nachrechnung) |
| AC-T033-08 | erfüllt | Vertrauensgrenzen unverändert (begrenzende Rohbytes-Lesung, kein Netz, Hermetietest); Riftward.Simulation blobidentisch; Architekturgrenzen: Session BCL-only, Runtime-Hotpaths C#, F#/Python fern |
| AC-T033-09 | erfüllt | fmt 0 Fixes, lint valid 0 Befunde, Release-Build 0 Warnungen, Testsuite 262/262, security PASS; Regressionen: bench-sim 0, savecheck 0 (alle Prüfklassen), Soak-Kurzlauf 3000 Ticks diagnostisch 0; Exitcodes 35–38 unverändert; Schemaerhöhung rein additiv gebunden |
| AC-T033-10 | erfüllt | Dokumentation/Register konsistent (oben); Abnahmedoku verknüpft je Kriterium mit Evidenz; kein Abgriff produziert (displaylos) → Media-Lab-Eintrag entfällt; Playtestprotokoll vorregistriert, Ausführung als Restpunkt 1 ausgewiesen |

Q-GAM-001 bis Q-GAM-007, Q-GAM-010, Q-NAR-002, Q-TEC-004/Q-TEC-006/Q-TEC-010
und Q-OPS-001 bleiben unberührt offen; der Report weist sie maschinenlesbar
aus.

## Reparaturen des Review-/Vollendungslaufs (alle im Primärslice, genau ein Task-Manifest)

1. **AC-T033-06 unvollständig:** Kein Consumer für `mode-switch`, keine
   Verfolgungskamera, kein Badge, kein Titel-HUD, keine interaktive Lenkung,
   keine sichtbare Kontextabweisung, Einzelabgriff statt Abgriffpaar.
   Vervollständigt wie oben; Schema bindet das Paar (exakt zwei Einträge,
   Modusliteral, Hash, gemeinsamer gebundener Weltzustand) und die
   HUD-Bindung.
2. **`interactiveContextRejections` hartkodiert 0:** jetzt echter
   Live-Pfadzähler (`context-visible-rejection-v1`) mit UF-001-Zeile.
3. **Nicht geförderte Wechsel verschwanden still aus dem Protokoll:**,
   `FlushPendingSwitches` bindet ausgewertete Wechsel hinter dem Laufhorizont
   ausdrücklich mit `EffectiveInRun=false`; Horizontfixture und echter Lauf
   (S=417 wirksam M=419; S=418/S=419 unwirksam gebunden; Endmodus `personal`)
   belegen die Endmoduswahrheit.
4. **Lenkauflösung in Fließkomma ohne Tie-Break:** exakte
   Ganzzahlarithmetik (Q16, Int128-Kreuzmultiplikation) mit expliziter
   Gleichstandsregel (niedrigste Zonennummer); der Zentrumsfall ist nach §3
   ausdrücklich kein Kandidat (Skalarprodukt 0).
5. **Testdatei des Builder-Kandidaten war nie kompiliert/gelaufen:** vier von
   zwölf T-033-Suitefällen scheiterten (falsche Kanonisierungserwartungen,
   falsche Parametererwartungen, `newPipeline`-Tupelaufrufe); repariert, ohne
   eine Assertion zu schwächen.
6. **Latente Schemadiskrepanz der Display-Bindung (T-032-Fläche, für
   AC-T033-06 necessary):** das Schema verlangte unit/method für
   `environment.display`, der Builder schreibt renderer/vendorId/deviceId/
   glVersion — jeder echte Interaktivlauf wäre am Schemator mit Code 27
   gescheitert. Schemaform an die Builderwahrheit angeglichen (Nebenreparatur,
   hier dokumentiert statt still).
7. **Folgetick-Wechselsemantik:** gegen den ursprünglichen Vertragswortlaut
   validiert — Abschnitt 4 (2) („weder wirksam noch kontextbildend") plus
   (4) (Auswertung im für die Auswertung gültigen Modus) ergeben zwingend,
   dass Wechsel an Ticks S und S+1 beide denselben Zielmodus tragen (Netto-
   effekt genau ein Wechsel an S+2); der Kandidat implementiert genau das.
   Der Vertragstext wurde nur präzisiert (Folgeticksatz, Protokollfeld-
   semantik, Horizontsatz) — kein stiller Contract-Rewrite, keine
   Semantikänderung; Fixtures binden das Verhalten.

## Eigene Evidenz (alle Befehle selbst ausgeführt, Exitcodes)

```text
./scripts/rift.sh fmt        -> 0 (0 Fixes nach Normalisierung)
./scripts/rift.sh lint       -> 0 (valid, 0 Befunde)
./scripts/rift.sh build      -> 0 (0 Warnungen)
./scripts/rift.sh test       -> 0 (262/262)
./scripts/rift.sh security   -> 0 (PASS)
kommandoschleife hybrid v2 (3 Wechsel), Seed 20260826:
    zwei Fresh-Prozessläufe -> 0/0, Endhash 420f85c9acf32a1d builderidentisch,
    p99 0,793 ms (hart 16), Allokation 0 Bytes je warmem Tick,
    max reactionTicks 1, Kettenkriterium evaluated=true, gate.pass=true,
    gate.switchReaction evaluated=true max=2 targetMet=true
Twin (ohne Wechsel-Intents)  -> Endhash 420f85c9acf32a1d identisch,
    Kettenstichproben byteidentisch; Fremdseed 42 -> edcf1170e4f2d41e
Horizontlauf (Wechsel 417/418/419) -> Protokoll eff/false/false,
    Endmodus personal, Wechselreaktion nur über den wirksamen Wechsel
v1-Kopf mit steer / Kopfhorizontabweichung / /dev/null -> je 37 ohne Report
--interactive displaylos     -> 19 ohne Report (kein simuliertes Verhalten)
Regressionen: bench-sim -> 0 (gate.pass=true), savecheck -> 0 (alle
    Prüfklassen), soak-replay --diagnostic-accelerated 3000 Ticks -> 0
    (rein diagnostisch)
Riftward.Simulation byteidentisch (Diff leer); GAME_DESIGN.md unberührt;
genau ein Task-Manifest (T-033) im Kandidaten.
```

Reports und Protokolle liegen unter gitignoriertem `artifacts/t033-review/`;
gitignorierte Runtime-Evidenz ist zu keinem Zeitpunkt Test-Fixture oder
Gateeingabe.

## Bekannte Restpunkte

1. **Manueller Interaktivsmoke und Playtestausführung:** Diese Sitzung ist
   displaylos; der fensterpflichtige Modus bricht hier kontrolliert mit
   Code 19 ab (belegt, ohne Report, ohne Simulation). Die menschliche
   Sichtprüfung (Badge-/Kamera-/HUD-Lesbarkeit, Lenkqualität, kontextierte
   Abweisung) sowie der opt-in hashgebundene Abgriffpaarlauf bleiben einer
   Displaysession auf dem Entwickler-PC (gegebenenfalls virtuelles Wayland
   nach T-023-Präzedenz) vorbehalten — vertraglich ausgewiesener Restpunkt
   des Auftrags, nicht ein fehlender Code-/Vertragsumfang.
2. **Pflichtprofile** bleiben `NOT-MEASURED` (Q-OPS-001); Läufe auf dem
   Entwickler-PC sind diagnostische Baseline. G-PERF bleibt gemäß
   akzeptierter T-032-Präzedenz kein neues Pflichtgate (kein neuer
   budgettragender Pfad; Kriterium 6 ist fail-closed reportgebunden).
3. **Offene Fragen unberührt:** Q-GAM-001 bis Q-GAM-007, Q-GAM-010,
   Q-NAR-002, Q-TEC-004, Q-TEC-006, Q-TEC-010.
4. **Verschobene unabhängige Reparaturen (spätere Slices):** unverändert die
   aus der T-032-Linie registrierten Posten (ASSET_LANE-Git-Check in
   `.git`-losen Archivextraktionen, vakuoese Reaktionsmetrik V == S als
   Kommandovertrag-V2-Entscheidung, stille NumberOption-Defaults/-Clamps samt
   unbekannter Positionsargumente, Härtung von `allocationStrictnessRegression`
   gegen Kaltprozess-Transients).

## Fresh-Checkout-/Clean-Archive-Vertrag

Da dieser Lauf Test-, Fixture-, Build-, Register- und Evidenzpfade berührt,
lief der Portabilitätsvertrag nach allen Änderungen am exakten Finalbaum:
indexfreie Kandidatenbaum-Rekonstruktion aus HEAD `0a1d6f4d…` plus Arbeitsbaum
über privaten Index (`read-tree` + `update-index --index-info` aus
`hash-object -w`-Blobbindungen; der echte Index und Stagingoperationen wurden
niemals angetastet), zweifach konvergent; Ergebnis siehe
`artifacts/t033-review/fresh/final-candidate-tree.txt`. Doppelte
`git archive`-Extraktion dieses Baums ist byteidentisch; die Gatefolge
(Release-Build 0 Warnungen, lint valid, Testsuite, security) sowie ein
autoritativer `kommandoschleife`-Lauf liefen ausschließlich aus den
Archivbytes mit identischem Endhash zum Arbeitsbaumlauf; der
Driftvergleich gegen die unangetastete Zweitextraktion ergab null Abweichung
in getrackten Bytes (`NO_DRIFT`). `assets-check` und das
harness-zustandsabhängige `verify` laufen im Entwicklerbaum (dokumentierte
Grenze der verschobenen ASSET_LANE-Reparatur) und sind dort grün.
