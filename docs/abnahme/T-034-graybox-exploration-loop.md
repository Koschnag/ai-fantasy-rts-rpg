# Abnahme T-034 – Graybox-Erkundungsauftrag

**Status:** Reparierter Reviewkandidat nach abgeschlossenem unabhängigem
Abschlussreview. Der direkt ausführbare Headless-Produktpfad, seine
deterministische Beobachtungstreue, die lokale Regressionssuite einschließlich
der Finalgrenzen-HUD-Regression und der echte visuelle Hardware-Repass sind
grün. Das frühere unabhängige Sichtreview bleibt als Baseline für den
unveränderten Pixelpfad gültig; das erneute unabhängige Abschlussreview
(2026-08-28) hat den exakten reparierten Kandidaten vollständig geprüft und
drei dokumentarische Wahrheitskorrekturen in denselben Slice zurückgebunden
(Erkundungsvertrag §10 zur realen Modevertrags-§8-Aktualisierung,
NATIVE_UNTERBAU-Kommandosynopsis mit `--exploration`/`--auto-exit-at-horizon`,
VSync-Klausel in AUTOMATION.md). Der vertragsgemäße Fresh-Checkout-Nachweis
des formalen Promotionspfads verlangt per Skriptvertrag einen vollständig
eingecheckten Baum und läuft deshalb in der Promotion; die Reviewphase belegt
den äquivalenten Clean-Archive-Baum aus den exakten Kandidatenbytes. Diese
Datei beschreibt die aktuelle Produktwahrheit; sie behauptet keinen noch
nicht ausgeführten Gate-Erfolg.

## Gelieferter Umfang

- `docs/ERKUNDUNGSVERTRAG.md` V1 bindet die reversible Graybox-Hypothese:
  genau eine deterministisch aus der bestehenden Geometrie abgeleitete,
  begehbare Landmarke je Vertragszone; Registrierung nur bei physischer
  Heldenanwesenheit an einer persönlichen Vorgrenze; sitzungslokaler,
  ausdrücklich nicht persistierter Fortschritt; opt-in Aktivierung.
- `Riftward.Session` hält Landmarken, Besuchsprotokoll und Fortschritt hinter
  echten schreibgeschützten Sichten. Die Vorgrenzenbeobachtung liest nur
  Heldenzone und wirksamen Modus, erzeugt keinen Kernbefehl und berührt weder
  Simulationszustand noch Hashkette. Auch die defensive Telemetrie-
  Momentaufnahme ist weder als Array noch über `IList` indexweise mutierbar.
- `kommandoschleife --exploration` aktiviert denselben Headless- oder
  Interaktivpfad. Ohne Opt-in bleibt der Bestandsreport unverändert bei
  Schemaversion 2; mit Opt-in verlangt Schemaversion 3 den additiven Block
  `explorationSession` mit Landmarken, persönlichem Besuchsprotokoll,
  Fortschritt/Abschluss, Nichtpersistenzaussage und ehrlichen visuellen
  Kanalgrenzen.
- Die versionierte Abnahmefixture
  `tests/fixtures/command/t034-exploration-separated.graybox` mobilisiert
  strategisch die vier Nichtheldengruppen aus dem persönlichen Umfeld,
  bewegt sie für die Bildprüfung später wieder in die Startzone und führt
  den Vertragshelden über denselben bestehenden Lenkkanal persönlich durch
  die sechs Zonen in der Reihenfolge 0/2/1/5/3/4. Der Auftrag schließt ohne
  neue Eingabeaktion, Kernänderung, Budget- oder Exitcodebedeutung ab.
- Interaktiv konsumieren Titel-HUD und Landmarkenzustandskanal dieselbe
  schreibgeschützte Telemetrie. Unbesucht/besucht ist über echte, per Instanz
  gebundene Diamantform plus Farbe unterschieden; runde Befehlspulse behalten
  ihren getrennten Formkanal. Neben dem unveränderten festen Anker schließt
  ein heldennahes, für die persönliche Kamera eigenständig dimensioniertes
  Zustandsecho die Offscreen-Lücke der zonenweiten Registrierung. Headless
  und vorzeitig beendete Interaktivläufe weisen
  fensterpflichtige Messungen mit Grund als nicht gemessen aus.
- Der Titel-HUD wird erst nach dem vollständig nachgeholten Simulations-
  grenzzyklus und vor dem Rendern aus einem zustandsgebundenen Schlüssel
  aktualisiert. Damit zeigt auch ein Auto-Exit unmittelbar nach der letzten
  Grenze denselben Fortschritt wie Report und Renderzustand, ohne pro Frame
  neue Titelstrings zu erzeugen. Eine versionierte Finalgrenzenfixture und
  eine Regression binden Besuch, HUD-Messfelder und Aufrufreihenfolge.
- Der Report-Schemator prüft nicht nur Typen: kanonische begehbare Anker,
  eindeutige persönliche Besuchsfolge, fortlaufende Reihenfolge und die
  Relationen Protokoll ↔ Fortschritt ↔ Abschluss ↔ gemessene
  Darstellungszähler sind adversarial fail-closed gebunden. Ein angefordertes
  `--exploration` bleibt auch im Exception-Teilreport als ehrlicher,
  unvollständiger Schemaversion-3-Block erhalten.
- `--auto-exit-at-horizon` beendet ausschließlich explizit begrenzte echte
  Display-Gates nach dem vollständig gerenderten Messfenster kontrolliert;
  der normale Interaktivpfad bleibt bis zum echten Quit offen. Damit kann der
  autonome Harness Capture und Report ohne KWin-/Timeout-Eingriff abschließen.
  Nur dieser begrenzte Gatepfad deaktiviert Present-VSync, damit ein
  verdecktes oder gesperrtes Wayland-Surface den unverändert wanduhrgebundenen
  20-Hz-Simulationstakt nicht auf 5 Hz drosselt; der normale Spielpfad bleibt
  VSync-gebunden.

## Kriterienstand

| Kriterium | Stand | Gebundene Evidenz |
|---|---|---|
| AC-T034-01 | erfüllt | Vertrags-Spiegeltest hält Kennungen, Alternativen, Playtestkriterien, Rückrollwege und Nichtpersistenz gegen `ERKUNDUNGSVERTRAG.md`/`ExplorationContract` konsistent. |
| AC-T034-02 | erfüllt | Echter öffentlicher Schemaversion-3-Lauf über die versionierte 8000-Tick-Abnahmefixture besucht 6/6 Landmarken; Besuchsticks 262/2642/4174/4795/6154/7210 in der Reihenfolge 0/2/1/5/3/4, ausschließlich Modus `personal`; zwei getrennte App-Prozesse liefern byteidentische deterministische Blöcke für Szenario, Eingabe, Modussitzung, Erkundung und Hashkette. |
| AC-T034-03 | erfüllt | Aktivierter/nicht aktivierter Twin: identische Start-/Endhashes, Kettenstichproben, Intentdispositionen und Kernbefehlsanzahl; fremder Seed ändert Start/Endhash, nicht die Landmarkenmenge; `git diff -- src/Riftward.Simulation` ist leer; Legacy-Schema 2 bleibt gültig. |
| AC-T034-04 | erfüllt | Visuelle Baseline auf dem unveränderten Renderpfad: echter X11-Pfad über XWayland auf der RX 570, Exit 0, 8000/8000 Ticks, `windowCompleted=true`, Gate grün, 6/6 Besuche und hashgebundenes Abgriffpaar an Tick 8000/Hash `cfdafa670fccdeea`; strategisch `d7ac86d3…fc5d`, persönlich `afea9dddd5…d4`, beide 1920×1080 und verschieden. Das unabhängige xhigh-Sichtreview dieser Baseline bestätigt die vorregistrierte Zwei-Sekunden-Lesbarkeit: strategisch ist der abgeschlossene Zustand durch die sechs grünen Diamanten lesbar; persönlich bilden orangefarbener Held, vollständiger grüner Diamant und kleineres gedrehtes Zustandsecho getrennte Form- und Farbkanäle. Auf dem reparierten Kandidaten belegt zusätzlich ein nativer Display-Lauf mit der Finalgrenzenfixture Exit 0, 300/300 Ticks, Besuch von Zone 0 exakt an Grenze 299, übereinstimmende gemessene HUD-/Fortschrittsfelder 1/6, Gate PASS und ein an Tick 300/Hash `4183c06207b17e0c` gebundenes neues 1920×1080-Abgriffpaar. Der feste, gegebenenfalls angeschnittene Zonenanker bleibt absichtlich vom heldennahen Echo getrennt. Aussagegrenze bleibt `graybox-state-occupancy-not-gameplay-atmosphere-or-shipping`; niemals Gameplay-, Atmosphären- oder Shipping-Beleg. |
| AC-T034-05 | erfüllt | Keine neue Abhängigkeit oder Netz-/Secretfläche; begrenzte bestehende Skripteingabe; Session bleibt BCL-only, Runtimepfad C#; alle neuen Diagnosefelder nicht gategekoppelt; Security-Gate grün. |
| AC-T034-06 | erfüllt | Release-Build 0 Warnungen/0 Fehler und 281/281 reguläre Tests einschließlich adversarialer Schema-/Schreibschutz-, Finalgrenzen-HUD-, Auto-Exit-, Mesh-, Billboard-, Partikeltopologie- und Partikelformregression sind am reparierten Kandidaten grün; ebenso Lint, Security und `rift.sh verify`. Das erneute unabhängige Abschlussreview hat dieselben Gates am endgültigen Kandidaten wiederholt grün gebunden. Der formale Fresh-Checkout-/Clean-Archive-Nachweis des Promotionspfads läuft gemäß seinem Skriptvertrag erst auf dem eingecheckten Baum; die Reviewphase hat den äquivalenten Beweis aus den exakten Kandidatenarchivbytes erbracht (bootstrap/build/lint/test grün, 281/281, beide versionierten Fixturesbyteidentisch enthalten). |

## Lokal ausgeführte Evidenz am aktuellen Kandidaten

```text
dotnet build Riftward.slnx -c Release --no-restore
    -> 0, 0 Warnungen, 0 Fehler
dotnet tests/RiftHarness.Tests/bin/Release/net10.0/RiftHarness.Tests.dll
    -> 0, 281/281 Tests grün
./scripts/rift.sh fmt
    -> 0, anschließend keine Formatabweichung
./scripts/rift.sh lint
    -> 0, valid, 0 Befunde
./scripts/rift.sh security
    -> 0, Secret-/JSON-/NuGet-Audit-/LFS-/Toolchain-Gates PASS
riftward-candidate-diff-check /home/cong/ki-projekt
    -> 0
riftward-harness-preflight
    -> 0
git diff -- src/Riftward.Simulation
    -> leer
unabhängiger Headless-Reviewlauf des Abschlussreviews (dieselben Parameter
wie oben, Report unter artifacts/t034-review-independent.json)
    -> 0; Schema 3; Gate PASS; 6/6 Besuche bei 262/2642/4174/4795/6154/7210
       in der Reihenfolge 0/2/1/5/3/4, ausschließlich personal;
       Endhash cfdafa670fccdeea; HUD/Kanal ehrlich nicht gemessen
       (headless-run-without-window); 0 B je warmem Tick; p99 Tick 1,33 ms
Clean-Archive-Nachweis der Reviewphase (git archive des privaten
Kandidatenbaums, ausschließlich eingecheckte Bytes)
    -> bootstrap/build/lint/test grün, 281/281 Tests, beide T-034-Fixtures
       byteidentisch enthalten; keine gitignorierte Runtime-Evidenz nötig
./scripts/rift.sh kommandoschleife --scenario kommando-graybox \
  --input-script tests/fixtures/command/t034-exploration-separated.graybox \
  --seed 20260826 --warmup-ticks 240 --horizon-ticks 8000 \
  --exploration --interactive --auto-exit-at-horizon --capture-frame ...
    -> 0; Schema 3; 8000/8000 Ticks; Gate PASS; Allokation 0 B/warmen Tick;
       p99 Tick 1,143 ms; Reaktion p99/max 1 Tick; Moduswechsel p99/max 2 Ticks;
       6/6 Besuche bei 262/2642/4174/4795/6154/7210;
       Hash cfdafa670fccdeea; Abgriffpaar wie AC-T034-04
./scripts/rift.sh kommandoschleife --scenario kommando-graybox \
  --input-script tests/fixtures/command/t034-final-boundary-hud.graybox \
  --seed 20260826 --warmup-ticks 240 --horizon-ticks 300 \
  --exploration --interactive --auto-exit-at-horizon --capture-frame ...
    -> 0; Schema 3; 300/300 Ticks; Gate PASS; Besuch Zone 0 an Grenze 299;
       Fortschritt und gemessener HUD 1/6; Abgriffpaar an Tick 300 und
       Hash 4183c06207b17e0c
früheres unabhängiges xhigh-Sichtreview am unveränderten Pixelpfad
    -> PASS als visuelle Baseline; die erneute formale Kandidatenprüfung nach
       der Titel-HUD-Reparatur steht aus
```

Der unmittelbar vorausgehende 8000-Tick-Versuch wurde wegen einer einmaligen,
prozessglobalen Fremdthread-/Tiered-JIT-Messstörung von 0,13 B je warmem Tick
mit Exit 35 verworfen. Er ist keine Abnahmeevidenz. Der danach in ruhiger
Umgebung exakt wiederholte Lauf oben weist 0 B je warmem Tick und Exit 0 aus;
es wurde dafür kein Grenzwert gelockert.

## Offene Annahmepunkte

1. Das erneute unabhängige Abschlussreview ist am endgültigen Kandidaten
   abgeschlossen; Befundkette und Gates sind über den hash-gebundenen
   Review-Receipt an den exakten Kandidaten-Fingerabdruck gebunden.
2. Die Reviewphase hat den Clean-Archive-Vertrag äquivalent aus den exakten
   Kandidatenarchivbytes belegt; der formale Fresh-Checkout-Gate-Lauf
   (`scripts/fresh-checkout-test.sh`, vertragsgemäß nur auf vollständig
   eingechecktem Baum) bleibt der Promotionspfad-Autorität vorbehalten.

Erst nach dem formalen Promotionspfad werden Taskmanifest und BACKLOG auf
`accepted` gestellt. Q-GAM-001 bis Q-GAM-007, Q-GAM-010, Q-NAR-002/Q-NAR-004,
Q-TEC-006 und Q-OPS-001 bleiben unberührt offen.
