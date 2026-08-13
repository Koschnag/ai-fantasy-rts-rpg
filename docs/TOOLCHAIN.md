# Entwicklungs- und Produktionswerkzeuge

## Prinzipien

- FOSS-first und kleine, austauschbare Komponenten
- exakte Versionen, Checksummen und reproduzierbare Builds
- ein einziger öffentlicher Aufgabenvertrag für Menschen, KI und CI
- keine große Game Engine und kein Cloud-Zwang
- globale Werkzeuge nur, wenn sie mehrere Projektaufgaben tatsächlich verbessern

Die aktuell bekannten Werkzeugpins und Hashes stehen in `toolchain.lock.json`.
NuGet-Bibliotheken sind zusätzlich durch die eingecheckten
`packages.lock.json`-Dateien samt Content-Hash gebunden; direkte
PackageReferences verwenden geschlossene Versionsintervalle. Agent-Plugins und
Skills gelten als Prompt-Supply-Chain und werden ebenfalls gehasht; Updates
sind keine beiläufige Aktualisierung.

`NuGet.Config` löscht geerbte Paketquellen, erlaubt ausschließlich die offizielle NuGet-v3-Quelle und verwendet Source Mapping; Projekt- und Tool-Restore bleiben lockfilegebunden und mit NuGet-Audit aktiviert. `Directory.Build.props` deaktiviert zusätzlich den vom F#-SDK injizierten lokalen Library-Pack-Feed und verwendet den ignorierten Repository-Cache `.ai/runtime/cache/nuget`. Damit stammt `FSharp.Core` auf Entwicklerrechnern und CI aus derselben Quelle und besitzt denselben Lockfile-Hash.

## Installiert

| Werkzeug | Version | Ort | Lizenz / Quelle | Zweck |
|---|---:|---|---|---|
| Codex CLI | 0.147.0 | `~/.local/bin/codex` | vorhandene Installation | KI-Arbeit |
| .NET SDK | 10.0.110 | `~/.local/share/dotnet` | MIT, offizieller .NET-Build | C#, F#, Harness und spätere Runtime |
| Blender | 5.2.0 | `~/.local/opt/blender-5.2.0` | GPL, offizieller Blender-Build | optionales, nicht gatendes Kontrollwerkzeug zum manuellen Öffnen lokaler GLB-Dateien; keine T-006/T-007-Produktionsdependency |
| Git LFS | 3.7.1 | `~/.local/bin/git-lfs` | MIT mit BSD-/Third-Party-Hinweisen, offizieller Git-LFS-Build | große binäre Produktionsassets außerhalb normaler Git-Blobs |
| GitHub CLI | 2.94.0 | `~/.local/bin/gh` | MIT, offizieller checksummengeprüfter GitHub-Build | privates Remote anlegen, Sichtbarkeit prüfen und pushen |
| Fantomas | 7.0.5 | repo-lokales .NET-Tool | Apache-2.0, NuGet/Upstream | deterministische F#-Formatierung für `fmt` und `lint` |
| JsonSchema.Net | 8.0.5, exakt | RiftHarness-NuGet-Lockfile | MIT, `json-everything` | lokale Draft-2020-12-Prüfung der Asset-, Receipt-, Modell- und Reviewverträge |
| Superpowers Plugin | Marketplace-Revision `11c74d6b` | Codex Plugin-Cache | MIT | Planung, TDD, Debugging und Reviews |
| `security-best-practices` Skill | aktueller Installationsstand 2026-08-13 | Codex Skills | Apache-2.0 | sichere Implementierungsregeln |
| `security-threat-model` Skill | aktueller Installationsstand 2026-08-13 | Codex Skills | Apache-2.0 | Bedrohungsmodellierung |
| `playwright` Skill | aktueller Installationsstand 2026-08-13 | Codex Skills | Apache-2.0 | spätere UI-/Tooltests, falls passend |

Neue Skills werden erst in einem neuen Agent-Turn erkannt. Anbieter-, Cloud-, Figma-, Datenbank- und Deploymentplugins wurden bewusst nicht installiert.

### JSON-Schema-Validator im Produktionswerkzeug

`RiftHarness` verwendet ausschließlich für das offline laufende
Asset-Provenienzgate `JsonSchema.Net` 8.0.5. Die direkte Version ist als
`[8.0.5]` festgelegt. Der gelockte rein verwaltete Abhängigkeitsgraph lautet:

| Paket | Version | SPDX | Rolle |
|---|---:|---|---|
| JsonSchema.Net | 8.0.5 | MIT | Draft-2020-12-Auswertung |
| JsonPointer.Net | 6.0.1 | MIT | transitive JSON-Pointer-Auflösung |
| Json.More.Net | 2.2.0 | MIT | transitive `System.Text.Json`-Hilfen |
| Humanizer.Core | 3.0.1 | MIT | transitive String-/Namenshilfen von JsonPointer.Net |

Es gibt keine transitive native Bibliothek. Das Gate registriert nur die
versionierten lokalen Projektschemas, löst keine Netzreferenzen auf und kapselt
die Bibliothek hinter einer kleinen Schemaauswertungsfunktion in `Assets.fs`;
fachliche Querfeld-, Pfad-, Hash-, Rollen-, LFS- und Clean-Room-Regeln bleiben
eigener Code. Dadurch ist ein Wechsel auf einen anderen vollständigen
Draft-2020-12-Validator begrenzt, aber wegen unterschiedlicher
Auswertungsdetails mit einer erneuten Negativfixture-Abnahme verbunden.

Der vollständig verwaltete Paketgraph ist für den `net10.0`-CoreCLR-Harness
unter Windows, Linux und macOS vorgesehen; in diesem Projektstand ist er nur
unter Linux tatsächlich gebaut und getestet. Diese Pakete gehören nur zum
F#-Harness auf CoreCLR und weder zum Spielclient
noch zum C#-Native-AOT-Host. Sie erweitern daher nicht die Shipping-Größe oder
Trimming-/AOT-Fläche der Runtime; für das Produktionswerkzeug wird keine
Native-AOT-Kompatibilität behauptet. Eine mögliche Alternative ist ein
source-generierter Validator oder ein enger BCL-Validator, letzterer wäre für
den vollständigen Draftvertrag jedoch erheblich wartungsintensiver.

Die Version 8.0.5 und ihre Paketlizenz wurden vor Aufnahme geprüft. Upstream
führte ab der 9er-Linie zusätzliche Bedingungen für Binärpakete ein; deshalb
sind Major-Upgrades ausdrücklich gesperrt und dürfen weder Renovate noch ein
Agent automatisch durchführen. Die Kehrseite des 8er-Pins ist ein mögliches
Wartungs-/Security-Ende. Advisories werden über den gelockten NuGet-Audit
geprüft; bei einem relevanten Fund wird entweder eine neue Lizenzentscheidung
getroffen oder die Adaptergrenze auf einen anderen Validator umgestellt.
Hinweise für diesen NuGet-Graphen stehen in `THIRD_PARTY_NOTICES.md`.

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

Blender ist als offizieller, SHA-256-geprüfter portabler Linux-Build vorhanden; der historische Bootstrap steht in `scripts/bootstrap-blender-linux.sh`. Seit dem T-006-Contract-Amendment ist Blender ausschließlich ein optionales, manuelles FOSS-Kontrollwerkzeug. Der aktive Kalibrierungsgenerator läuft BCL-only in F#/.NET 10, schreibt GLB direkt und rastert PNG auf der CPU; weder Blender-Version noch -Installation oder -Ausgabe sind Produktionspin oder CI-Gate. Die GPL-Lizenz des Kontrollwerkzeugs wird nicht zur Lizenz des erzeugten Spiels oder eigener Assets.

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

T-006 konkretisiert diese Aufteilung: Der `calibration-v1`-Produktionspfad ist
ein in-process laufendes F#/.NET-10-CoreCLR-Werkzeug und verwendet für direkten
GLB-Write, CPU-Rasterisierung und PNG-Encoding ausschließlich die BCL. Er
startet keinen Unterprozess, öffnet kein Netzwerk und lädt keine zusätzliche
native Bibliothek. Der bindende Vertrag steht in
`DOTNET_GENERATOR_CONTRACT.md`.

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

Aktuell implementiert sind `bootstrap`, `build`, `fmt`, `lint`, `test`,
`harness`, `rag-build`, `rag-query`, `assets-check`, `security` und der
technische Teilsatz `verify`. `assets-check` prüft Assetmanifeste, lokale
Generation-Receipts, Modellpins, Clean-Room-Regeln und den
Quarantäne-/Freigabestatus offline. Ein valides Quarantäneasset ist dabei noch
kein Shipping-Asset; Promotion und Packaging müssen zusätzlich den globalen
Repo-Scan ohne `--manifest` als `assets-check --require-local
--require-approved` verwenden. Eine gezielte Einzelmanifestprüfung ersetzt die
repo-weite Source-Inventur nicht. `bench`, `check` und `package`
melden ausdrücklich `NICHT VERFÜGBAR` und liefern Fehlercode 3, bis ihre
jeweilige `READY`-Aufgabe umgesetzt wurde. `security` ist ein lokaler
Baseline-Gate und ausdrücklich noch keine vollständige Release-Sicherheits-
oder Lizenzfreigabe.
