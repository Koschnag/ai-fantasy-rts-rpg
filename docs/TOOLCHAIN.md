# Entwicklungs- und Produktionswerkzeuge

## Prinzipien

- FOSS-first und kleine, austauschbare Komponenten
- exakte Versionen, Checksummen und reproduzierbare Builds
- ein einziger öffentlicher Aufgabenvertrag für Menschen, KI und CI
- keine große Game Engine und kein Cloud-Zwang
- globale Werkzeuge nur, wenn sie mehrere Projektaufgaben tatsächlich verbessern

Die aktuell bekannten exakten Pins und Hashes stehen in `toolchain.lock.json`. Agent-Plugins und Skills gelten als Prompt-Supply-Chain und werden dort ebenfalls gehasht; Updates sind keine beiläufige Aktualisierung.

`NuGet.config` löscht geerbte Paketquellen, erlaubt ausschließlich die offizielle NuGet-v3-Quelle und verwendet Source Mapping; Projekt- und Tool-Restore bleiben lockfilegebunden und mit NuGet-Audit aktiviert.

## Installiert

| Werkzeug | Version | Ort | Lizenz / Quelle | Zweck |
|---|---:|---|---|---|
| Codex CLI | 0.147.0 | `~/.local/bin/codex` | vorhandene Installation | KI-Arbeit |
| .NET SDK | 10.0.110 | `~/.local/share/dotnet` | MIT, offizieller .NET-Build | C#, F#, Harness und spätere Runtime |
| Blender | 5.2.0 | `~/.local/opt/blender-5.2.0` | GPL, offizieller Blender-Build | reproduzierbare 3D-/Rig-/LOD-/Bake-Pipeline |
| Git LFS | 3.7.1 | `~/.local/bin/git-lfs` | MIT mit BSD-/Third-Party-Hinweisen, offizieller Git-LFS-Build | große binäre Produktionsassets außerhalb normaler Git-Blobs |
| GitHub CLI | 2.94.0 | `~/.local/bin/gh` | MIT, offizieller checksummengeprüfter GitHub-Build | privates Remote anlegen, Sichtbarkeit prüfen und pushen |
| Fantomas | 7.0.5 | repo-lokales .NET-Tool | Apache-2.0, NuGet/Upstream | deterministische F#-Formatierung für `fmt` und `lint` |
| Superpowers Plugin | Marketplace-Revision `11c74d6b` | Codex Plugin-Cache | MIT | Planung, TDD, Debugging und Reviews |
| `security-best-practices` Skill | aktueller Installationsstand 2026-08-13 | Codex Skills | Apache-2.0 | sichere Implementierungsregeln |
| `security-threat-model` Skill | aktueller Installationsstand 2026-08-13 | Codex Skills | Apache-2.0 | Bedrohungsmodellierung |
| `playwright` Skill | aktueller Installationsstand 2026-08-13 | Codex Skills | Apache-2.0 | spätere UI-/Tooltests, falls passend |

Neue Skills werden erst in einem neuen Agent-Turn erkannt. Anbieter-, Cloud-, Figma-, Datenbank- und Deploymentplugins wurden bewusst nicht installiert.

## Systempakete für Ubuntu

Die folgende Baseline stammt aus den Ubuntu-26.04-Repositories und benötigt einmalig eine interaktive `sudo`-Freigabe:

```bash
sudo apt update
sudo apt install --no-install-recommends \
  build-essential clang lld cmake ninja-build pkg-config zlib1g-dev \
  libsdl3-dev git-lfs ripgrep fd-find jq tree fzf just shellcheck shfmt \
  sqlite3 blender imagemagick glslang-tools spirv-tools
```

`dotnet-sdk-10.0` kann alternativ systemweit aus Ubuntu installiert werden. Ubuntu- und Microsoft-Paketquellen für .NET sollen nicht gemischt werden. Die aktuelle Maschine nutzt vorerst die benutzerlokale SDK-Installation, weil `sudo` in der laufenden Sitzung eine interaktive Passwortabfrage verlangt.

Blender ist aus demselben Grund als offizieller, SHA-256-geprüfter portabler Linux-Build installiert. Der reproduzierbare Bootstrap steht in `scripts/bootstrap-blender-linux.sh`. Blender ist ein Produktionswerkzeug; seine GPL-Lizenz wird dadurch nicht zur Lizenz des erzeugten Spiels oder der eigenen Assets.

Git LFS ist ebenfalls checksummengeprüft benutzerlokal installiert und als globaler Git-Filter registriert. Die konkreten Binärformate für freigegebene Quellassets stehen in `.gitattributes`; Quarantäne und gecookte Laufzeitartefakte bleiben gemäß `.gitignore` außerhalb von Git. Ohne gewähltes LFS-Remote ist diese Ablage noch kein externes Backup.

Nicht pauschal installiert werden Docker/Podman, Kubernetes, Java, Go, Rust, Datenbankserver oder Webframeworks. Sie erhalten erst mit einem konkreten, gemessenen Bedarf eine Rolle.

Es ist noch kein Bild-, 3D-, Audio-, Sprach- oder Embeddingmodell für die Produktion freigegeben. `models.lock.json` beginnt deshalb bewusst leer. Vor mehreren Gigabyte Modellgewichten und GPU-Runtimes stehen ein festes Eval-Set, getrennte Prüfung von Code-/Gewichts-/Outputlizenz, Produktionshardware und Akzeptanzrate; erst das gewählte Modell wird mit Artefakthash gepinnt. FOSS-Code allein macht Modellgewichte nicht automatisch frei oder kommerziell geeignet.

## Geplanter nativer Unterbau

Die technische Vorentscheidung wird als ADR dokumentiert:

- SDL3 für Fenster, Eingabe und Plattformintegration
- bgfx für D3D11 unter Windows, OpenGL als konservativen Linux-Pfad und Metal unter macOS
- SDL3_mixer oder SDL3-Core-Audio nach einem Audio-Spike
- eigener sehr dünner C-ABI-/C#-`LibraryImport`-Layer
- keine große Engine, kein Raytracing und kein moderner API-Zwang

Native Quellen und ihre transitiven Komponenten werden mit vollständigem Commit-Hash gespiegelt oder reproduzierbar gebaut. Systempakete sind Entwicklungsabhängigkeiten, nicht automatisch die Shipping-Versionen.

## Sprachaufteilung

- C#: Host, native Interop, Speicher-/Job-/Render-Hotpaths und AOT-kritische Runtime
- F#: Agent-Harness, Asset-/Quest-/Weltdatenwerkzeuge, Validierung und geeignete reine Domänenlogik
- Shader-Sprachen: kleinster gemeinsamer Featurekern, offline je Backend kompiliert
- Shell: nur dünne Bootstrap-/CI-Hülle; mit ShellCheck und `set -eu`

F# ist in .NET 10 grundsätzlich Native-AOT-fähig, besitzt aber noch offene Trimming-/AOT-Warnflächen in `FSharp.Core`. Deshalb bleibt der ausgelieferte Native-AOT-Host zunächst C#; F#-Tools laufen auf CoreCLR. Native AOT und selbstenthaltenes CoreCLR werden später auf Startzeit, Speicher und Frame-Times verglichen, nicht dogmatisch gewählt.

## Plattform-Builds

Native AOT kann nicht beliebig zwischen Betriebssystemen cross-kompiliert werden. Releaseartefakte entstehen und laufen als Smoke-Test auf nativen Runnern:

| Host | Artefakt |
|---|---|
| Windows | `win-x64` |
| älteste unterstützte Linux-Buildbasis | `linux-x64` |
| macOS Intel, solange im Supportumfang | `osx-x64` |
| macOS Apple Silicon | `osx-arm64` |

Lokales Ubuntu 26.04 eignet sich zur Entwicklung, aber nicht als portable Linux-Native-AOT-Releasebasis. Dafür wird später eine ältere offiziell unterstützte CI-Basis verwendet.

Die vorläufigen OS-, Treiber-, Paket-, Signierungs- und Smoke-Baselines stehen in `PLATTFORMMATRIX.md`. Hardwarejahr und OS-Support sind getrennte Verträge.

## Öffentlicher Befehlsvertrag

Der öffentliche Vertrag umfasst folgende Aufgaben:

`bootstrap`, `build`, `fmt`, `lint`, `test`, `assets-check`, `bench`, `security`, `check`, `package` und `harness`.

Aktuell implementiert sind `bootstrap`, `build`, `fmt`, `lint`, `test`, `harness`, `rag-build`, `rag-query`, `security` und der technische Teilsatz `verify`. `assets-check`, `bench`, `check` und `package` melden ausdrücklich `NICHT VERFÜGBAR` und liefern Fehlercode 3, bis ihre jeweilige `READY`-Aufgabe umgesetzt wurde. `security` ist ein lokaler Baseline-Gate und ausdrücklich noch keine vollständige Release-Sicherheits- oder Lizenzfreigabe.
