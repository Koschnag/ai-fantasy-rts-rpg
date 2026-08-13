# ADR 002: Plattform- und Renderunterbau

- **Status:** akzeptiert
- **Datum:** 2026-08-13
- **Entscheidungsverantwortung:** Projektleitung
- **Bezug:** Z-002, Z-003; `docs/PERFORMANCE_BUDGET.md`

## Kontext

Benötigt wird ein schlanker eigener 3D-Unterbau für Windows, Linux und macOS. Die Minimalleistung entspricht ungefähr i7-3770/GTX-660/8 GB beziehungsweise M1/8 GB. Full HD bei 30 FPS ist Pflicht, 60 FPS bevorzugt; RX-580-Klasse definiert 1080p High/60 und den obersten geplanten Effektumfang. Raytracing und Echtzeit-GI sind ausgeschlossen.

## Entscheidungskriterien

- D3D11, konservatives OpenGL und Metal
- kleine permissiv lizenzierte Bibliotheken statt Engine
- robuste Fenster-/Eingabe-/Audiointegration
- Offline-Shaderpipeline, Instancing und Texturkompression
- C-ABI und Native-AOT-freundliche Bindings
- langfristig selbst reproduzierbar und austauschbar

## Betrachtete Optionen

- **SDL3 + bgfx:** breite Backendabstraktion, BSD-2/zlib, vorhandene Shader-/Renderwerkzeuge; eigenes dünnes Binding nötig.
- **SDL3 + sokol:** sehr klein, aber Device-/Swapchain-Glue je Plattform und kein offizielles C#-Binding erhöhen Integrationsrisiko.
- **Silk.NET + eigene Low-Level-Renderer:** direkte Kontrolle, aber drei Renderer-/Synchronisationspfade sind für den Umfang zu groß.
- **Veldrid:** passende API, aber öffentlich seit 2023 nicht mehr aktuell genug als Langzeitfundament.
- **SDL3 GPU:** moderne, kohärente API, aber ohne D3D11-/OpenGL-Fallback ungeeignet für die konservative Zielmatrix.

## Entscheidung

Wir verwenden **SDL3** für Fenster, Eingabe und Plattformintegration sowie **bgfx** als dünne „bring your own engine“-Renderbibliothek.

Backendpolicy:

- Windows: D3D11 explizit
- Linux: OpenGL 3.3 als konservativer Pflichtpfad; Vulkan höchstens optional nach Messung
- macOS: Metal

Audio wird in einem Spike zwischen SDL3-Core-Audio und SDL3_mixer entschieden. Der Shipping-Build aktiviert nur tatsächlich benötigte Decoder.

Native Versionen werden mit vollständigen Commit-Hashes gepinnt. Ein kleiner eigener C#-Interop-Layer verwendet `LibraryImport`, stabile Wrapper und ABI-Smoke-Tests. Keine Funktion darf Compute Shader, SSBO, Bindless, indirekte Draws, Raytracing oder Echtzeit-GI voraussetzen.

## Folgen

- bgfx-Backenddefaults dürfen nie ungeprüft verwendet werden.
- Shader werden offline für die Zielbackends kompiliert und plattformspezifisch getestet.
- Der Renderer bleibt projektspezifisch; es entsteht keine allgemeine Editor-/Engineplattform.
- LOD, Culling, Instancing, BC-Kompression, baked lighting und Probes sind Kernfunktionen, keine späten Optimierungen.
- Ein alternativer Backend-Unterbau bleibt hinter der eigenen Render-API prinzipiell austauschbar.
- Erneute Prüfung nach leerem Dreiplattform-Window, Shaderdreieck und Benchmarkszene.

## Quellen

- [bgfx Repository, Backends und BSD-2-Lizenz](https://github.com/bkaradzic/bgfx)
- [SDL Repository und zlib-Lizenz](https://github.com/libsdl-org/SDL)
- [bgfx Shaderwerkzeuge](https://bkaradzic.github.io/bgfx/tools.html)
