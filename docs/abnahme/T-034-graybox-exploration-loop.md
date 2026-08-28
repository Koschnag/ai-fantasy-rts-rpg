# Abnahme T-034 – Graybox-Erkundungsauftrag

**Status:** Reviewkandidat mit bestandenem unabhängigem Abschlussreview. Der
direkt ausführbare Headless-Produktpfad, seine deterministische
Beobachtungstreue, die lokale Regressionssuite und der echte visuelle
Hardware-Repass sind grün. Die endgültige Annahme bleibt ausschließlich bis
zum Fresh-Checkout-/Clean-Archive-Nachweis des formalen Promotionspfads offen.
Diese Datei beschreibt die aktuelle Produktwahrheit; sie behauptet keinen
noch nicht ausgeführten Gate-Erfolg.

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
| AC-T034-04 | erfüllt | Echter X11-Pfad über XWayland auf der RX 570: Exit 0, 8000/8000 Ticks, `windowCompleted=true`, Gate grün, 6/6 Besuche und hashgebundenes Abgriffpaar an Tick 8000/Hash `cfdafa670fccdeea`; strategisch `d7ac86d3…fc5d`, persönlich `afea9dddd5…d4`, beide 1920×1080 und verschieden. Das unabhängige xhigh-Sichtreview bestätigt die vorregistrierte Zwei-Sekunden-Lesbarkeit: strategisch ist der abgeschlossene Zustand durch die sechs grünen Diamanten lesbar; persönlich bilden orangefarbener Held, vollständiger grüner Diamant und kleineres gedrehtes Zustandsecho getrennte Form- und Farbkanäle. Der feste, gegebenenfalls angeschnittene Zonenanker bleibt absichtlich vom heldennahen Echo getrennt. Aussagegrenze bleibt `graybox-state-occupancy-not-gameplay-atmosphere-or-shipping`; niemals Gameplay-, Atmosphären- oder Shipping-Beleg. |
| AC-T034-05 | erfüllt | Keine neue Abhängigkeit oder Netz-/Secretfläche; begrenzte bestehende Skripteingabe; Session bleibt BCL-only, Runtimepfad C#; alle neuen Diagnosefelder nicht gategekoppelt; Security-Gate grün. |
| AC-T034-06 | teilweise | Release-Build 0 Warnungen/0 Fehler und 280/280 reguläre Tests einschließlich adversarialer Schema-/Schreibschutz-, Auto-Exit-, Mesh-, Billboard-, Partikeltopologie- und Partikelformregression grün. Lint, Security, Kandidatenscope, Harness-Preflight, natives Shader-Verify und unabhängiges Abschlussreview sind am finalen Bildkandidaten grün; einzig Fresh-Checkout-/Clean-Archive wird durch den formalen Promotionspfad noch am gesicherten Kandidaten gebunden. |

## Lokal ausgeführte Evidenz am aktuellen Kandidaten

```text
dotnet build Riftward.slnx -c Release --no-restore
    -> 0, 0 Warnungen, 0 Fehler
dotnet tests/RiftHarness.Tests/bin/Release/net10.0/RiftHarness.Tests.dll
    -> 0, 280/280 Tests grün
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
./scripts/rift.sh kommandoschleife --scenario kommando-graybox \
  --input-script tests/fixtures/command/t034-exploration-separated.graybox \
  --seed 20260826 --warmup-ticks 240 --horizon-ticks 8000 \
  --exploration --interactive --auto-exit-at-horizon --capture-frame ...
    -> 0; Schema 3; 8000/8000 Ticks; Gate PASS; Allokation 0 B/warmen Tick;
       p99 Tick 1,143 ms; Reaktion p99/max 1 Tick; Moduswechsel p99/max 2 Ticks;
       6/6 Besuche bei 262/2642/4174/4795/6154/7210;
       Hash cfdafa670fccdeea; Abgriffpaar wie AC-T034-04
unabhängiges xhigh-Abschlussreview am unveränderten Abgriffpaar
    -> PASS; AC-T034-04 erfüllt, Schema-/Read-only-/Early-Quit-/Auto-Exit-
       Matrix bestätigt
```

Der unmittelbar vorausgehende 8000-Tick-Versuch wurde wegen einer einmaligen,
prozessglobalen Fremdthread-/Tiered-JIT-Messstörung von 0,13 B je warmem Tick
mit Exit 35 verworfen. Er ist keine Abnahmeevidenz. Der danach in ruhiger
Umgebung exakt wiederholte Lauf oben weist 0 B je warmem Tick und Exit 0 aus;
es wurde dafür kein Grenzwert gelockert.

## Offene Annahmepunkte

1. Isolierter Fresh-Checkout-/Clean-Archive-Lauf belegt, dass keine
   gitignorierte Runtime-Evidenz als Fixture benötigt wird.

Erst nach diesem Punkt werden Taskmanifest und BACKLOG auf `accepted`
gestellt. Q-GAM-001 bis Q-GAM-007, Q-GAM-010, Q-NAR-002/Q-NAR-004,
Q-TEC-006 und Q-OPS-001 bleiben unberührt offen.
