# ADR 005: Performancebeweis, Sprachrollen und geschützte Integration

- **Status:** akzeptiert
- **Datum:** 2026-08-24
- **Entscheidungsverantwortung:** Projektleitung

## Kontext

Riftward soll nicht nur niedrige Hardwareanforderungen behaupten, sondern auf
den festgelegten Referenzklassen messbar mehr aus begrenzter CPU-, GPU-, RAM-
und VRAM-Leistung herausholen. Architektur, Budgets und ein schlanker
SDL3-/bgfx-Unterbau schaffen dafür Voraussetzungen. Sie sind noch kein Beweis,
dass die Runtime optimiert ist.

Gleichzeitig soll der vorhandene .NET-Stack seine Sprachen nach ihren Stärken
einsetzen, ohne aus Forschungsinteresse unnötige Runtimegrenzen einzuführen.
Autonome Änderungen müssen schließlich fortlaufend publizierbar sein, ohne den
öffentlich vorzeigbaren `main`-Stand zu beschädigen.

## Entscheidung

### 1. Optimierung ist eine gemessene Eigenschaft

- Ein Budget ist ein Vertrag beziehungsweise eine zu prüfende Hypothese, kein
  Leistungsnachweis.
- Das Projekt bezeichnet eine Runtime, einen Hotpath oder eine Zielhardware
  erst dann als optimiert beziehungsweise bestanden, wenn ein reproduzierbarer
  Release-naher Lauf auf der zugehörigen realen Hardwareklasse die relevanten
  Grenzen nach Warm-up einhält.
- Schnellere Diagnosehardware, theoretische Komplexität, Mikrobenchmarks und
  ein grüner Build ersetzen keinen Lauf auf der Referenzklasse.
- p50, p95 und p99, Peaks, Streuung, Warm-up, Laufdauer, Hardware-, OS-,
  Treiber- und Buildidentität sowie Rohartefakte gehören zur Evidenz.
- Native AOT und selbstenthaltenes CoreCLR werden gemessen. Keiner der beiden
  Modi gilt ohne diese Messung als grundsätzlich schneller.

Nach dem Walking Skeleton hat ein absichtlich einfacher, aber repräsentativer
Belastungsframe Vorrang vor weiterer allgemeiner Produktionsinfrastruktur. Er
führt mindestens folgende Lasten gemeinsam aus:

- 350 sichtbare, instanzierte Einheiten; normale Einheiten besitzen im
  repräsentativen Animationspfad mindestens 48 Bones,
- 250 vollständig simulierte mobile Agenten mit Gruppenbewegung und
  konkurrierender budgetierter Wegfindung,
- repräsentative Landschaft, Sonne, die budgetierten lokalen Schattenlichter
  und Partikelspitzen,
- Telemetrie für Frame-/GPU-/Simulationszeit, Allokationen, GC-Pausen,
  Working Set, VRAM beziehungsweise Unified Memory, Draw-/Submit-Aufrufe,
  sichtbare Dreiecke und Ladezeit.

Der integrierte Belastungsframe ergänzt die isolierten Benchmarks und ersetzt
sie nicht. Erst sein Bestehen auf `HW-PC-MIN` und `HW-MAC-MIN` bestätigt die
zentrale Effizienzhypothese.

### 2. C# führt die ausgelieferte Runtime aus

C# bleibt Sprache für Client-Host, SDL3-/bgfx-Interop, Rendering, Simulation,
Gameplay und gemessene Runtime-Hotpaths. Heiße Pfade verwenden bevorzugt
lineare und explizite Datenlayouts, wiederverwendete Buffer, Arrays,
`Span<T>`/`Memory<T>`, Pooling nur mit belegtem Nutzen und statischen Dispatch.
Unbegrenzte Objektgraphen, LINQ-/`IEnumerable`-Pipelines, Boxing, Reflection
oder temporäre Allokationen pro Tick/Frame sind dort ohne Messbeleg unzulässig.

Rust oder eigene zusätzliche C++-Fachlogik wird nicht vorsorglich ergänzt. Eine
weitere Runtime-Sprache benötigt einen reproduzierten C#-Engpass, einen Spike
mit End-to-End-Gewinn einschließlich FFI-Kosten und eine eigene ADR. Der kleine
projektbezogene C++-Shim an der vorhandenen bgfx-Grenze bleibt davon unberührt.

### 3. F# spezifiziert, kompiliert und prüft

Der vorhandene F#-Code wird nicht nach C# umgeschrieben. F# soll seinen
besonderen Nutzen schrittweise an Offline- und Korrektheitsgrenzen entfalten:

- typisierte IDs, Hashes, Statuswerte, sichere Pfade und validierte
  Wertebereiche statt freier Strings in neuen oder berührten Domänen,
- explizite DTO-zu-Domäne-Codecs; externe JSON-Verträge bleiben neutrale,
  versionierte Formate,
- vollständige Zustandsautomaten für Agenten-, Asset-, Quest- und
  Freigabelebenszyklen,
- Quest-, Welt-, Einheiten- und Regelcompiler,
- eine verständliche, reine F#-Referenzsimulation als Correctness-Oracle für
  die optimierte C#-Simulation: gleicher Seed und gleiche geordnete Befehle
  müssen gemäß dem festgelegten Numerikvertrag denselben fachlichen Zustand
  ergeben,
- Property-/modellbasierte Tests mit gespeichertem Fehlerseed sowie
  Maßeinheiten für Zeit, Speicher, Geometrie und Budgets.

Diese Rolle ist kein F#-Exklusivitätsanspruch. Die Konzepte wären auch in C#
oder Rust umsetzbar; F# wird eingesetzt, weil es sie im vorhandenen .NET-Stack
kompakt ausdrückt. F#-Listen, `Seq`-Pipelines, Closures oder funktionale
Zwischenobjekte gehören nicht ungemessen in Frame-/Tick-Hotpaths. F#-Domänen-
typen werden nicht direkt als instabile Serialisierungs- oder ABI-Verträge an
den C#-Client gekoppelt.

### 4. Python bleibt optional und untrusted

Riftward kann ohne Python gebaut und ausgeliefert werden. Der bestehende
BCL-only-.NET-Generator bleibt ein gültiger deterministischer Produktionspfad.
Python darf später als austauschbarer Offline-Adapter für Blender-, Modell-,
Bild-, Audio- oder Batchautomation eingesetzt werden, wenn ein `READY`-Auftrag
Version, Lizenz, Eingaben, Isolation, Provenienz und Austauschstrategie
festlegt.

Python-/Blender-/KI-Ausgabe ist immer untrusted Quellmaterial:

```text
optionaler Offline-Adapter -> Quarantäne + Receipt -> Validator/Cooker -> Runtimepaket
```

Sie wird nie allein aufgrund eines erfolgreichen Generators als Shipping-Asset
freigegeben und Python wird keine Spielclient-Abhängigkeit.

### 5. Agenten integrieren über einen geschützten Vorzeigestand

- Agenten arbeiten und checkpointen ausschließlich auf einem Arbeitsbranch.
- Ein repo-gebundener Publisher darf nur dieses Repository und nur den
  vorgesehenen Autopilot-Branch veröffentlichen.
- Änderungen erreichen `main` ausschließlich über Pull Request, verpflichtende
  Repository-Gates und Squash-Merge.
- Rote Gates, Mergekonflikte, ein schmutziger Arbeitsbaum oder fehlende
  Evidenz blockieren die Integration und lösen höchstens einen begrenzten
  Reparaturlauf aus. Sie lösen keinen Direkt-Push auf `main` aus.
- Nach dem Merge werden lokaler Arbeitsbranch, lokaler `main`-Ref und
  `origin/main` nur dann neu verankert, wenn der freigegebene Baum identisch
  ist. `main` ist zu jedem Zeitpunkt der abnehmbare, demonstrierbare Stand.

## Folgen

- Die Behauptung „hardwareoptimiert“ bleibt bis zum integrierten Nachweis eine
  ausdrücklich benannte Forschungsfrage.
- T-020 und T-021 liefern isolierte Renderer-/Simulationsbaselines; T-023 führt
  beide in einem repräsentativen Belastungsframe zusammen.
- Zusätzliche Harness-, Sprach- oder Toolarbeit darf den ersten
  Performancebeweis nicht verdrängen, sofern sie ihn nicht direkt ermöglicht.
- C# kann aggressiv optimiert werden, während F#-Referenzmodelle und
  generative Tests fachliche Regressionen sichtbar machen.
- Tooladapter und Anbieter bleiben FOSS-first, abstrahiert und austauschbar;
  jede zusätzliche Grenze muss ihren messbaren Nutzen belegen.

