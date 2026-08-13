# Plattform- und Auslieferungsmatrix

**Status:** Baseline für T-010/T-011; auf realen Runnern zu validieren

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
