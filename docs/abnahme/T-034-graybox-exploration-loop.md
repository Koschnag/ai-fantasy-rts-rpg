# Abnahme T-034 – Graybox-Erkundungsauftrag

**Status:** Reviewkandidat. Der direkt ausführbare Headless-Produktpfad, seine
deterministische Beobachtungstreue und die lokale Regressionssuite sind grün.
Die endgültige Annahme bleibt bis zum unabhängigen Review, dem
Fresh-Checkout-/Clean-Archive-Nachweis und dem ehrlichen Interaktivsmoke auf
einer nutzbaren Displaysession offen. Diese Datei beschreibt die aktuelle
Produktwahrheit; sie behauptet keinen noch nicht ausgeführten Gate-Erfolg.

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

## Kriterienstand

| Kriterium | Stand | Gebundene Evidenz |
|---|---|---|
| AC-T034-01 | erfüllt | Vertrags-Spiegeltest hält Kennungen, Alternativen, Playtestkriterien, Rückrollwege und Nichtpersistenz gegen `ERKUNDUNGSVERTRAG.md`/`ExplorationContract` konsistent. |
| AC-T034-02 | erfüllt | Echter öffentlicher Schemaversion-3-Lauf besucht 6/6 Landmarken; Besuchsticks 262/1302/2602/4068/5302/6202, ausschließlich Modus `personal`; zwei getrennte App-Prozesse liefern byteidentische deterministische Blöcke für Szenario, Eingabe, Modussitzung, Erkundung und Hashkette. |
| AC-T034-03 | erfüllt | Aktivierter/nicht aktivierter Twin: identische Start-/Endhashes, Kettenstichproben, Intentdispositionen und Kernbefehlsanzahl; fremder Seed ändert Start/Endhash, nicht die Landmarkenmenge; `git diff -- src/Riftward.Simulation` ist leer; Legacy-Schema 2 bleibt gültig. |
| AC-T034-04 | teilweise | HUD-/Kanalquellen, Schemaformen und kontrollierte Headless-Aussagegrenzen sind regulär getestet. Ehrlicher Real-Display-Smoke/Playtest und gegebenenfalls der einzelne Media-Lab-konforme Abgriff stehen vor Annahme noch aus; ohne Display darf nur Code 19 belegt werden. |
| AC-T034-05 | erfüllt | Keine neue Abhängigkeit oder Netz-/Secretfläche; begrenzte bestehende Skripteingabe; Session bleibt BCL-only, Runtimepfad C#; alle neuen Diagnosefelder nicht gategekoppelt; Security-Gate grün. |
| AC-T034-06 | teilweise | Release-Build 0 Warnungen/0 Fehler, Fantomas/Lint grün, Security grün, 273/273 Tests grün, Kandidatenscope und Harness-Preflight grün. Fresh-Checkout-/Clean-Archive, vollständiges Verify und unabhängiges Abschlussreview stehen noch aus. |

## Lokal ausgeführte Evidenz am aktuellen Kandidaten

```text
dotnet build tests/RiftHarness.Tests/RiftHarness.Tests.fsproj -c Release --no-restore
    -> 0, 0 Warnungen, 0 Fehler
dotnet tests/RiftHarness.Tests/bin/Release/net10.0/RiftHarness.Tests.dll
    -> 0, 273/273
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
```

## Offene Annahmepunkte

1. Unabhängiger Reviewer prüft Code, Dokumente und Kriterien am unveränderten
   Kandidaten und führt die vollständigen Pflichtgates aus.
2. Isolierter Fresh-Checkout-/Clean-Archive-Lauf belegt, dass keine
   gitignorierte Runtime-Evidenz als Fixture benötigt wird.
3. Eine echte Displaysession belegt den Interaktivpfad, HUD-Lesbarkeit und
   den Zwei-Kanal-Landmarkenzustand; ohne Display wird kontrolliert Code 19
   statt eines simulierten Erfolgs dokumentiert.

Erst nach diesen Punkten werden Taskmanifest und BACKLOG auf `accepted`
gestellt. Q-GAM-001 bis Q-GAM-007, Q-GAM-010, Q-NAR-002/Q-NAR-004,
Q-TEC-006 und Q-OPS-001 bleiben unberührt offen.
