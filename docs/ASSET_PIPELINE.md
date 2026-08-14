# Synthetische Asset-Pipeline

## Ziel

Alle kreativen Shipping-Assets entstehen gemäß ADR 004 synthetisch aus internen Spezifikationen. Die bevorzugte Produktionsform ist nicht „ein Prompt erzeugt ungeprüft eine fertige Datei“, sondern:

```text
AssetSpec
  -> Agent erzeugt Generatorprogramm und Parameter
  -> headless Synthese
  -> deterministische technische Verarbeitung
  -> Quarantäne + Manifest + Receipt
  -> Budget-, Provenienz-, Originalitäts- und Lizenzgates
  -> Source-Promotion
  -> reproduzierbarer Cook
```

Damit kann KI 100 % der kreativen Ausgangsdaten erzeugen, während deterministische Werkzeuge UVs, LODs, Rigging, Baking, Kompression und Export reproduzierbar erledigen.

## Kleiner FOSS-first Produktionskern

| Bereich | Kandidat | Lizenz | Rolle | Status |
|---|---|---|---|---|
| Orchestrierung und Validierung | F#/.NET-Worker im Repository | Projektcode | Jobs, Hashes, Manifeste, Budgets, Reports | im Aufbau |
| erste 3D-Kalibrierung | F#/.NET-10-Generator im Repository | Projektcode; BCL-only | direkter GLB-Writer und deterministischer CPU-Rasterizer/PNG | T-006 in Arbeit; Produktionspfad |
| optionale 3D-Sichtkontrolle | [Blender](https://www.blender.org/about/license/) | GPL-3.0-or-later; erzeugte Werke werden nicht automatisch GPL | manuelles Öffnen lokaler GLB-Dateien | installiert; nicht gatendes Kontrollwerkzeug, keine Produktionsdependency |
| prozedurale PBR-Materialien | [Material Maker](https://github.com/RodZill4/material-maker) | MIT | Graphen und CLI-Export | zu pinnen/evaluieren |
| UV-Atlas | [xatlas](https://github.com/jpcy/xatlas) | MIT | headless UV-Unwrapping | zu pinnen/evaluieren |
| Mesh-/glTF-Optimierung | [meshoptimizer](https://github.com/zeux/meshoptimizer) / `gltfpack` | MIT | LOD, Quantisierung, Animationsoptimierung | zu pinnen/evaluieren |
| Texturkompression | [Basis Universal](https://github.com/BinomialLLC/basis_universal) | Apache-2.0 | KTX2/ETC1S/UASTC | zu pinnen/evaluieren |
| glTF-Prüfung | [Khronos glTF Validator](https://github.com/KhronosGroup/glTF-Validator) | Apache-2.0 | strukturierter CLI-Report | zu pinnen/evaluieren |
| synthetisches Audio/Musik | [Csound](https://github.com/csound/csound) | LGPL-2.1-or-later | samplefreie, skriptbare Synthese | zu pinnen/evaluieren |

Der erste 3D-Produktionspfad benötigt außer .NET keine dieser externen Komponenten. Weitere Werkzeuge werden nicht pauschal aufgenommen, sondern jeweils mit exaktem Commit/Artefakthash und einem messbaren T-050-Job. Blender kann bei Bedarf rein manuell kontrollieren; seine Installation, Version und Ausgabe sind für T-006/T-007 nicht gatend. Die Spielruntime enthält keine KI- oder DCC-Runtime.

## Generative Modelle

`models.lock.json` ist die einzige Modellallowlist und bleibt fail-closed. Ein Modellname in einem Manifest ist keine Zulassung.

Vor Aufnahme werden getrennt geprüft:

- Inferenzcode und dessen transitive Lizenzen
- Gewichts- und gegebenenfalls Datensatzlizenz
- Outputbedingungen und kommerzielle Nutzung
- Modell-/Artefaktversion und SHA-256
- Speicherbedarf der Produktionshardware
- Akzeptanzrate auf einem festen originalen Eval-Set
- Near-Duplicate-/Ähnlichkeitsrisiken

Bildmodelle und musikalische Modelle können für Quarantänespikes evaluiert werden, aber kein aktueller Kandidat ist für Shipping freigegeben. Modelle oder Abhängigkeiten mit Nichtkommerziell-, Gebiets-, Registrierungs- oder unklaren Inputbedingungen bleiben gesperrt. Community-LoRAs, fremde Stimmen, Referenzbilder und Audiosamples sind standardmäßig verboten.

## Produktionshardware

Die Hardwarebudgets des Spiels gelten nicht für Offline-Generierungsworker. Arbeitsannahmen:

- prozedurale .NET-Pipeline: CPU-only; innerhalb der jobbezogenen T-006-Ressourcengrenzen, keine GPU oder DCC erforderlich
- Bild-/Musik-Eval: separate Linux-/NVIDIA-Maschine oder austauschbarer Remote-Worker mit ungefähr 16 GB VRAM und 32–64 GB RAM
- experimentelle generative 3D-Modelle: nur Quarantäne auf gesonderter Hardware; sie sind kein Produktionskern

Das Zielspiel lädt ausschließlich optimierte, gecookte Assets und bleibt auf den in `PERFORMANCE_BUDGET.md` festgelegten Rechnern lauffähig.

## Aktueller Kalibrierungssatz

Die drei Keyframe-Spezifikationen unter `assets/specs/` prüfen unabhängig:

1. bewohnter Schutzraum und weite Strategiekamera,
2. leise RPG-Erkundung und Umweltgeschichte,
3. Formationslesbarkeit und Cross-Mode-Geländehebel.

Ihre Rohbilder bleiben gitignored in `assets/quarantine/concepts/`. Die versionierten Manifeste sind ausdrücklich `quarantine`: Der verwendete eingebaute Bildgenerator legt Modellrevision und Outputbedingungen nicht vollständig offen, daher können diese Bilder die Art Bible kalibrieren, aber derzeit nicht als Shipping-Asset freigegeben werden.

## Erster reproduzierbarer 3D-Spike

Der vollständige maschinenprüfbare Vertrag steht in `DOTNET_GENERATOR_CONTRACT.md`. Er friert `calibration-v1`, CLI/Exitcodes, Safe Paths, PCG32, Geometrie- und LOD-Formeln, Achsen/Pivots/Snap-Namen, den direkten GLB-Writer, den deterministischen CPU-Rasterizer/PNG-Encoder, Proxybudgets, den .NET-Pin, BCL-only-/In-process-Grenzen, T-003-Crosschecks, Jobjournal, Crash-Recovery und Fresh-Checkout-CI ein.

| Task | kleinster überprüfbarer Liefergegenstand | Abhängigkeiten | Status |
|---|---|---|---|
| T-005 | strikter Spec-Parser, Referenzmathematik und unabhängiger .NET-Inspector mit rein synthetischen Fixtures; startet keinen Unterprozess | T-003 | DONE |
| T-006 | BCL-only-F#/.NET-Generator, direkter GLB-Writer, CPU-Rasterizer und transaktionaler T-003-Quarantäne-Lifecycle mit Recovery | T-003, T-005 | DONE |
| T-007 | pfadgefilterter Fresh-Checkout-CI-Nachweis für .NET-Pin, Null-Unterprozess/-Netz, Byteidentität, T-005-Regression sowie Fehler- und Crashpfade | T-003, T-005, T-006 | DONE |

Die Familie enthält ausschließlich drei kultur- und regionsneutrale technische Formen: gerade Wand, 90-Grad-Ecke und Wandöffnung mit Holzsturz. Kreative Eingabe ist nur das strikt numerische Spec; Prompts, Negativprompts, Fremdmodelle, Referenzbilder, Texturen, Add-ons und generative 3D-Modelle sind ausgeschlossen. T-005 ist als DCC-unabhängiger Inspector abgenommen.

T-006 läuft vollständig in F#/.NET 10 und BCL-only. Er schreibt GLB 2.0 direkt und rastert die 960×540-Preview deterministisch auf der CPU; es gibt keinen Unterprozess, Netzwerkzugriff, DCC-, GPU-, Skriptruntime- oder zusätzlichen nativen Pfad. Je Modul entstehen drei algorithmische Render-LODs, einfache Kollision, feste 4-Meter-Snap-Punkte, zwei geteilte numerische PBR-Materialien und ein kanonischer Technikreport. Zeit, Host, Job- und Run-ID sowie absolute Pfade gehören ausschließlich in Journal-/Run-/Evidenzdaten.

T-006 ist ein bewusstes Amendment gegenüber der ursprünglichen T-005-Planungsgrundlage: T-005 bleibt historisch abgenommen und unverändert, aber T-006 setzt `Riftward .NET Asset Generator v1`, genau drei F#-Generatorquellen und `dotnet-sdk:10.0.110` als Produktionsidentität. Nach diesen eng begrenzten Inspector-Konstantenänderungen muss die vollständige T-005-Suite erneut bestehen. Ein vorhandenes DCC darf nur separat und manuell GLB-Dateien ansehen; ein solcher Kontrollblick ersetzt kein Gate.

Die `calibration-v1`-Grenzen bleiben kleine Strukturproxies: LOD0 höchstens 4.096 Dreiecke/3.072 Vertices, LOD1 1.024/1.024, LOD2 192/256, Kollision 48 Dreiecke, höchstens zwei Render-Primitives je LOD, genau zwei gemeinsam genutzte Materialien sowie je höchstens 2 MiB Familien-GLB und geschätzte dekodierte Geometrie. Die exakte Accessorformel steht im Vertrag. Diese Werte entscheiden Q-AST-004 nicht und sind kein FPS-, Drawcall-, RAM- oder VRAM-Nachweis für die Zielhardware.

T-006-Ergebnisse bleiben unter `assets/quarantine/3d/`. Ein gültiger T-003-Receipt und ein Manifest mit Status `quarantine` belegen nur den protokollierten technischen Ursprung. `assets-check --require-local` muss bestehen, `--require-approved` muss scheitern. Es gibt keine Source-Promotion, keinen LFS-/Backupanspruch, keinen Shipping-Cook und keine visuelle, lizenzielle oder Originalitätsfreigabe durch den Erzeugeragenten.

`T-050` setzt T-005, T-006 und T-007 voraus. Erst T-050 wählt nach getrenntem Review eine eigene Art-Bible-Familie, entscheidet produktionsnahe Assetklassenbudgets, ergänzt nur nach Messbedarf weitere gepinnte UV-/Textur-/Cook-Werkzeuge, testet LFS/Backup/Wiederherstellung und misst integrierte Assets auf Runtime und Zielhardware.

## Nächste ausführbare Schritte

1. T-003 ist separat abgenommen; alle vorhandenen Keyframes bleiben dennoch Quarantäne.
2. T-005 ist unabhängig abgenommen; seine Proxybudgets sind kein Hardware- oder Shipping-Nachweis.
3. T-006 ist unabhängig abgenommen; das erste rein .NET-generierte 3D-Asset bleibt technisch validiert in Quarantäne.
4. T-007 ist unabhängig abgenommen und beweist den vollständigen .NET-Pfad in einem frischen Linux-x64-Checkout.
5. T-050 misst akzeptierte Varianten, Dreiecke, LOD, UV, Texturspeicher, Exportzeit und visuelle Konsistenz und erprobt getrennte Freigabe, Source-Promotion sowie Cooking.
6. Erst danach wird der Assetumfang vervielfacht.
