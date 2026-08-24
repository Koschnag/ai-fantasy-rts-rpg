# Plattform- und Auslieferungsmatrix

**Status:** Baseline für T-010/T-011; auf realen Runnern zu validieren

## Bestätigte Mindestbasis linux-x64 (T-010, 2026-08-23)

Auf dem Entwickler-PC (Intel Core i7-3770, Radeon RX 570, Mesa/radeonsi) wurde
der Walking-Skeleton-Smoke nativ bestanden. Daraus folgt die dokumentierte,
verbindliche Mindestbasis für den Linux-Pflichtpfad:

- **CPU-Klasse:** Intel Core i7-3770 (Ivy Bridge) beziehungsweise AMD FX-8350
  (Piledriver). ISA-Basis ist **x86-64-v2** (SSE4.2/POPCNT): bx deklariert am
  gepinnten Stand SSE4.2 als Mindestspezifikation; `-march=native` und jede
  AVX-/AVX2-/FMA-Pflicht sind verboten und werden im Native-Build sowie in
  `lint`/`security` geprüft (`toolchain-check`).
- **Renderer:** OpenGL **3.3 Core ohne optionale Erweiterungspflicht**, am
  gepinnten bgfx-Stand per `BGFX_CONFIG_RENDERER_OPENGL=33` erzwungen; kein
  stiller Backend-Fallback.
- **Erste Referenz des lokalen Treiberstands:** Mesa 26.0.3-1ubuntu1
  (radeonsi, LLVM/ACO), Kernel 7.0.0-29-generic. Dieser Stand ist erste
  Messreferenz, keine Supportgarantie; konkrete Treiberminima entstehen aus den
  Smokes von T-011 (Q-TEC-002 bleibt `OFFEN`).

Die genannten GPUs beschreiben Leistungsprofile. Sie erzwingen keine Unterstützung eines Betriebssystems aus dem Erscheinungsjahr der Hardware. Unterstützt werden gewartete Betriebssysteme und Treiber, weil .NET 10, Signierung und aktuelle Sicherheitsupdates Teil des Produkts sind.

| Plattform | Architektur | vorläufige Mindestbasis | Renderer | Paket / Pflichtnachweis |
|---|---|---|---|---|
| Windows | x64 | Windows 10 LTSC/Enterprise in einer von .NET 10 unterstützten Version oder Windows 11 | D3D11 | ZIP/MSIX-Entscheidung in T-011; sauberer Start, Save, Shader und Eingabe |
| Linux | x64 | Ubuntu 22.04 LTS als älteste geplante Build-/Testbasis; weitere Distributionen best effort bis getestet | OpenGL 3.3 Core | Tar/AppImage-Entscheidung in T-011; glibc-/Native-Abhängigkeiten protokolliert |
| macOS | arm64 | macOS 14 Sonoma oder neuer, solange von .NET 10 unterstützt | Metal | signiertes/notarisiertes `.app`; M1/8-GB-Smoke und Benchmark |
| macOS | x64 | OFFEN; nur bei vertretbarem Test- und Supportaufwand | Metal | eigener nativer Runner erforderlich |

Aktueller Referenzstand der Runtimeunterstützung: [.NET unter Windows](https://learn.microsoft.com/en-us/dotnet/core/install/windows), [.NET unter macOS](https://learn.microsoft.com/en-us/dotnet/core/install/macos), [.NET unter Ubuntu](https://learn.microsoft.com/en-us/dotnet/core/install/linux-ubuntu-decision). Die Dokumentation ist zeitabhängig; Release-CI prüft sie vor jeder Supportänderung erneut. Windows 7/8.1 sind mit .NET 10 nicht unterstützt. Eine mit Developer ID verteilte macOS-App wird notarisiert.

## Treibervertrag

- Windows: ein vom Hersteller für das gewählte OS veröffentlichter D3D11-Treiber; konkrete Mindestversion nach GTX-660-/RX-580-Hardwaretest.
- Linux: Mesa-/proprietäre Treiberversion der unterstützten Distribution; konkrete Mindestversion nach GL-3.3-Smoke.
- macOS: System-Metal-Treiber der jeweiligen macOS-Version.
- Eine alte GPU mit ungepflegtem Treiber kann Leistungsreferenz bleiben, ohne zertifizierte Supportkonfiguration zu sein.

## Freigaberegel

Eine Plattform gilt erst als unterstützt, wenn Build, Installation/Paket, Start, Eingabe, Rendering, Audio, Save/Load und ein deterministischer Smoke-Test auf nativer Hardware/Runnern grün sind. Cross-Compile allein genügt nicht. macOS-Pakete benötigen zusätzlich Signierung und Notarisierung; Geheimnisse dafür bleiben außerhalb des Repositories.
