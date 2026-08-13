# ADR 001: .NET-, Sprach- und Native-AOT-Strategie

- **Status:** akzeptiert
- **Datum:** 2026-08-13
- **Entscheidungsverantwortung:** Projektleitung
- **Bezug:** Z-002, Z-003, Z-004; `docs/TOOLCHAIN.md`

## Kontext

Das Spiel soll mit der .NET-Palette, einschließlich F#, ohne große Engine entstehen, auf drei Desktopbetriebssystemen laufen und sehr geringe Frame-Time-/Speicherbudgets einhalten. Native AOT ist erwünscht, besitzt aber für F# und dynamische .NET-Muster Einschränkungen.

## Entscheidungskriterien

- LTS-Support und aktuelle Optimierungen
- Native-AOT-/Trimming-Kompatibilität
- kontrollierbare native Interop-Grenze
- F# sinnvoll und substanziell einsetzen
- profilierbare, allokationsarme Hotpaths
- native Builds je Zielbetriebssystem

## Betrachtete Optionen

### Option A: gesamter Stack in F# und immer Native AOT

- Vorteile: einheitliche funktionale Sprache, knappe Domänenmodelle
- Nachteile: offene `FSharp.Core`-Trimming-/AOT-Warnflächen, C#-spezifischer `LibraryImport`-Generator, unnötiges Risiko in Interop und Hotpaths
- Risiko: Warnungen werden unterdrückt oder Runtime-Design wird zu spät unvereinbar

### Option B: C# für ausgelieferten Host/Hotpaths, F# für Harness und Offline-Tools

- Vorteile: klare C-Interop- und AOT-Grenze; F# eignet sich stark für Compiler, Validatoren und Datentransformation
- Nachteile: zwei Sprachen und sorgfältig definierte Datenverträge
- Risiko: F# wird zu wenig in produktnahen Systemen genutzt, falls Grenzen nicht regelmäßig überprüft werden

### Option C: nur C#

- Vorteile: kleinste AOT-/Interop-Reibung
- Nachteile: ignoriert den gewünschten und für Toolpipelines passenden F#-Einsatz

## Entscheidung

Wir verwenden **.NET 10 LTS** und pinnen SDK `10.0.110` in `global.json`.

- C#: ausführbarer Client-Host, native Interop, Speicher/Jobs, Rendering und gemessene Runtime-Hotpaths
- F#: Agent-Harness, Asset-/Quest-/Weltdatenwerkzeuge, Compiler, Validatoren und passende reine Domänenlogik
- Gemeinsame Grenzen: kleine explizite C#-Verträge und blittable Daten an nativen Übergängen

Releaseprojekte werden von Anfang an AOT-/Trim-kompatibel entworfen. **Native AOT ist aber kein Dogma:** Native-AOT- und selbstenthaltene CoreCLR-Builds werden auf Startzeit, Working Set, Ladezeit und Frame-Time verglichen. Die bessere gemessene Variante wird ausgeliefert.

Verboten im Runtimekern sind dynamisches Assembly-Laden, `Reflection.Emit`, Laufzeit-Codegenerierung und unkontrollierte Reflection. Registries, Serialisierung und Komponentenlisten sind statisch oder source-generiert. Tools dürfen CoreCLR/JIT verwenden.

## Folgen

- AOT-/Trim-Warnungen werden nicht pauschal unterdrückt.
- Native AOT wird auf Windows, Linux und macOS jeweils nativ gebaut; Release-CI benötigt entsprechende Runner.
- F#-Runtimecode wird nur aufgenommen, wenn Publish- und Performancegates auf allen betroffenen RIDs bestehen.
- Native AOT und C# source-generiertes `LibraryImport` beeinflussen die Architektur früh, ohne die spätere Wahl des Runtime-Modus vorwegzunehmen.
- Erneute Prüfung nach dem plattformweiten Walking Skeleton und dem ersten 250-Agenten-Stresstest.

## Quellen

- [.NET Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [.NET Runtime Identifier](https://learn.microsoft.com/en-us/dotnet/core/rid-catalog)
- [F# 10](https://devblogs.microsoft.com/dotnet/introducing-fsharp-10/)
- [offener F# AOT-/Trimming-Tracker](https://github.com/dotnet/fsharp/issues/13398)
