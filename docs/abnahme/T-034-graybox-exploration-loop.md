# Abnahme T-034 – Graybox-Erkundungsauftrag

**Status:** Reviewkandidat. Der direkt ausführbare Headless-Produktpfad, seine
deterministische Beobachtungstreue und die lokale Regressionssuite sind grün.
Die endgültige Annahme bleibt bis zum unabhängigen Review, dem
Fresh-Checkout-/Clean-Archive-Nachweis und einem bestandenen visuellen
Playtest offen. Der echte Interaktivlauf ist technisch vollständig grün;
die Sichtprüfung des Abgriffpaars hat die vorregistrierte Lesbarkeit jedoch
noch nicht bestätigt. Diese Datei beschreibt die aktuelle Produktwahrheit;
sie behauptet keinen noch nicht ausgeführten Gate-Erfolg.

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
| AC-T034-04 | teilweise (`needs-work`) | Echter Wayland-/RX-570-Lauf: Exit 0, 6800/6800 Ticks, `windowCompleted=true`, Gate grün, 6/6 Besuche und hashgebundenes Abgriffpaar an Tick 6800/Hash `9b1c73996becfcf8`; strategisch `3c8183b2…db48`, persönlich `790984fc…4fe4`, beide 1920×1080 und verschieden. Die Sichtprüfung bestätigt die verlangte Lesbarkeit binnen zwei Sekunden **nicht**: strategisch liegt der abgeschlossene Zustand größtenteils außerhalb des unveränderten Kamerastands, persönlich verdeckt die auf ein Ziel mobilisierte 250-Agenten-Masse Landmarke und Heldenumfeld. Das Artefakt ist deshalb im Media Lab `needs-work`, nie Gameplay-/Atmosphärenbeleg; ein bestandener Playtest bleibt Pflicht. |
| AC-T034-05 | erfüllt | Keine neue Abhängigkeit oder Netz-/Secretfläche; begrenzte bestehende Skripteingabe; Session bleibt BCL-only, Runtimepfad C#; alle neuen Diagnosefelder nicht gategekoppelt; Security-Gate grün. |
| AC-T034-06 | teilweise | Release-Build 0 Warnungen/0 Fehler, 279/279 reguläre Tests einschließlich adversarialer Schema-/Schreibschutz-, Auto-Exit-, Mesh-, Billboard- und Partikelformregression grün; Lint, Security, Kandidatenscope, Harness-Preflight, Fresh-Checkout-/Clean-Archive, vollständiges Verify und unabhängiges Abschlussreview werden am finalen Bildkandidaten erneut gebunden. |

## Lokal ausgeführte Evidenz am aktuellen Kandidaten

```text
dotnet build tests/RiftHarness.Tests/RiftHarness.Tests.fsproj -c Release --no-restore
    -> 0, 0 Warnungen, 0 Fehler
dotnet tests/RiftHarness.Tests/bin/Release/net10.0/RiftHarness.Tests.dll
    -> 0, 279/279 Tests grün
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
    -> finaler Echtbild-Repass läuft; Resultat wird vor Statusänderung gebunden
```

## Offene Annahmepunkte

1. Unabhängiger Reviewer prüft Code, Dokumente und Kriterien am unveränderten
   Kandidaten und führt die vollständigen Pflichtgates aus.
2. Isolierter Fresh-Checkout-/Clean-Archive-Lauf belegt, dass keine
   gitignorierte Runtime-Evidenz als Fixture benötigt wird.
3. Der technisch grüne echte Displaypfad wird visuell repariert und erneut
   geprüft: Abschlusszustand, Heldenumfeld und Landmarkenkanal müssen in
   beiden Modi binnen zwei Sekunden lesbar sein. Der aktuelle Abgriff bleibt
   ausdrücklich `needs-work`; ein Struktur-/Pixelgate ersetzt diesen
   Playtest nicht.

Erst nach diesen Punkten werden Taskmanifest und BACKLOG auf `accepted`
gestellt. Q-GAM-001 bis Q-GAM-007, Q-GAM-010, Q-NAR-002/Q-NAR-004,
Q-TEC-006 und Q-OPS-001 bleiben unberührt offen.
