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
  Simulationszustand noch Hashkette.
- `kommandoschleife --exploration` aktiviert denselben Headless- oder
  Interaktivpfad. Ohne Opt-in bleibt der Bestandsreport unverändert bei
  Schemaversion 2; mit Opt-in verlangt Schemaversion 3 den additiven Block
  `explorationSession` mit Landmarken, persönlichem Besuchsprotokoll,
  Fortschritt/Abschluss, Nichtpersistenzaussage und ehrlichen visuellen
  Kanalgrenzen.
- Der vollständige deterministische Headless-Flow mobilisiert strategisch
  über die bestehende Rahmenwahl alle fünf Vertragsgruppen (einschließlich
  Heldengruppe 0), besucht die sechs Zonen persönlich in der Reihenfolge
  0/4/2/3/5/1 und schließt den Auftrag ohne neue Eingabeaktion,
  Kernänderung, Budget- oder Exitcodebedeutung ab.
- Interaktiv konsumieren Titel-HUD und Landmarkenzustandskanal dieselbe
  schreibgeschützte Telemetrie. Unbesucht/besucht ist über Form plus Farbe
  unterschieden; headless werden fensterpflichtige Messungen mit Grund als
  nicht gemessen ausgewiesen.
- `--auto-exit-at-horizon` beendet ausschließlich explizit begrenzte echte
  Display-Gates nach dem vollständig gerenderten Messfenster kontrolliert;
  der normale Interaktivpfad bleibt bis zum echten Quit offen. Damit kann der
  autonome Harness Capture und Report ohne KWin-/Timeout-Eingriff abschließen.

## Kriterienstand

| Kriterium | Stand | Gebundene Evidenz |
|---|---|---|
| AC-T034-01 | erfüllt | Vertrags-Spiegeltest hält Kennungen, Alternativen, Playtestkriterien, Rückrollwege und Nichtpersistenz gegen `ERKUNDUNGSVERTRAG.md`/`ExplorationContract` konsistent. |
| AC-T034-02 | erfüllt | Echter öffentlicher Schemaversion-3-Lauf besucht 6/6 Landmarken; Besuchsticks 262/1302/2602/4068/5302/6202, ausschließlich Modus `personal`; zwei getrennte App-Prozesse liefern byteidentische deterministische Blöcke für Szenario, Eingabe, Modussitzung, Erkundung und Hashkette. |
| AC-T034-03 | erfüllt | Aktivierter/nicht aktivierter Twin: identische Start-/Endhashes, Kettenstichproben, Intentdispositionen und Kernbefehlsanzahl; fremder Seed ändert Start/Endhash, nicht die Landmarkenmenge; `git diff -- src/Riftward.Simulation` ist leer; Legacy-Schema 2 bleibt gültig. |
| AC-T034-04 | teilweise (`needs-work`) | Echter Wayland-/RX-570-Lauf: Exit 0, 6800/6800 Ticks, `windowCompleted=true`, Gate grün, 6/6 Besuche und hashgebundenes Abgriffpaar an Tick 6800/Hash `9b1c73996becfcf8`; strategisch `3c8183b2…db48`, persönlich `790984fc…4fe4`, beide 1920×1080 und verschieden. Die Sichtprüfung bestätigt die verlangte Lesbarkeit binnen zwei Sekunden **nicht**: strategisch liegt der abgeschlossene Zustand größtenteils außerhalb des unveränderten Kamerastands, persönlich verdeckt die auf ein Ziel mobilisierte 250-Agenten-Masse Landmarke und Heldenumfeld. Das Artefakt ist deshalb im Media Lab `needs-work`, nie Gameplay-/Atmosphärenbeleg; ein bestandener Playtest bleibt Pflicht. |
| AC-T034-05 | erfüllt | Keine neue Abhängigkeit oder Netz-/Secretfläche; begrenzte bestehende Skripteingabe; Session bleibt BCL-only, Runtimepfad C#; alle neuen Diagnosefelder nicht gategekoppelt; Security-Gate grün. |
| AC-T034-06 | teilweise | Release-Build 0 Warnungen/0 Fehler, Fantomas/Lint grün, Security grün, reguläre Suite einschließlich Auto-Exit-Regression grün, Kandidatenscope und Harness-Preflight grün. Fresh-Checkout-/Clean-Archive, vollständiges Verify und unabhängiges Abschlussreview stehen noch aus. |

## Lokal ausgeführte Evidenz am aktuellen Kandidaten

```text
dotnet build tests/RiftHarness.Tests/RiftHarness.Tests.fsproj -c Release --no-restore
    -> 0, 0 Warnungen, 0 Fehler
dotnet tests/RiftHarness.Tests/bin/Release/net10.0/RiftHarness.Tests.dll
    -> 0, reguläre Suite grün; exakte Zahl im Abschlussrepass zu binden
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
env DISPLAY=:0 WAYLAND_DISPLAY=wayland-0 XDG_RUNTIME_DIR=/run/user/1000 \
  XDG_SESSION_TYPE=wayland vblank_mode=0 ./scripts/rift.sh kommandoschleife \
  ... --exploration --interactive --auto-exit-at-horizon --capture-frame ...
    -> 0, 6800/6800, gate.pass=true, 6/6, Capture-Paar gebunden;
       visuelles Review needs-work (keine Lesbarkeitsannahme)
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
