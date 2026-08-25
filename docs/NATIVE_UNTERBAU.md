# Nativer Unterbau (linux-x64, T-010)

**Status:** T-010 Abschnitt 0 umgesetzt; Windows/macOS folgen über T-011

Dieses Dokument beschreibt die reproduzierbare native Build-/Cache-Prozedur für
SDL3 und die bgfx-Familie auf linux-x64 sowie die zugehörigen Verträge
(Pins, ISA-Basis, Exitcodes). Die maschinenprüfbaren Pins stehen in
`toolchain.lock.json` (`nativeComponents`, Kohorte `2026-08-23-cohort-1`);
Lizenzen sind in `THIRD_PARTY_NOTICES.md` inventarisiert.

## Komponenten und Quellen

| Komponente | Pin | Lizenz |
|---|---|---|
| SDL3 | Tag `release-3.4.14` (Commit `147a8ee32dbf9ac02f3794964490687b6bbda1bc`) | zlib |
| bgfx | Commit `35a98dd6453cf25dc75c68e233abb400836d5920` | BSD-2-Clause |
| bx | Commit `9e3fadf6f11380031486be704d2ff46ca143664f` | BSD-2-Clause |
| bimg | Commit `371d90098b1fd017cd00205979d5ef74b8c3ed62` | BSD-2-Clause |

Auswahlkriterien (Auftrag T-010, Abschnitt 0): offizielle Upstreamquellen,
bevorzugt stabile Release-Tags mit Begründungspflicht bei Abweichung,
bgfx/bx/bimg aus einem gemeinsamen Abrufkohort vor dem Upstream-Umstieg auf
„Minimum OpenGL 4.3" (2026-08-19), weil ADR-002 Linux OpenGL 3.3 Core als
Pflichtpfad festlegt und der gepinnte bgfx-Stand `BGFX_CONFIG_RENDERER_OPENGL`
als erzwungene Renderer-Version unterstützt. Am gepinnten Stand verifizierte
Lizenzen: SDL=zlib; bgfx/bx/bimg=BSD-2-Clause.

## Build und Cache

Aufruf:

```bash
scripts/native-build-linux-x64.sh                # Cache-first bauen + Hashprüfung
scripts/native-build-linux-x64.sh --verify-cache # nur Offline-Prüfung, kein Build
scripts/native-build-linux-x64.sh --fresh        # vollständiger Neubau aus dem Cache
```

Verhalten:

1. **Pins**: Alle vier Quellarchive werden gegen `sourceSha256` in
   `toolchain.lock.json` geprüft. Fehlt ein Archiv im Cache, wird es genau dann
   einmalig von der gepinnten `sourceArchiveUrl` geladen (protokollierte
   Erstbeschaffung); danach genügt der Cache ohne Netzwerk.
2. **Build**: SDL3 via CMake/Ninja (minimale Optionen, kein Audio/Gamepad/
   Wayland), bgfx/bx/bimg via GENie/gmake (`config=release64`) mit injiziertem
   `-DBGFX_CONFIG_RENDERER_OPENGL=33` (GL-3.3-Core-Pflichtpfad) sowie
   `-msse4.2 -fPIC`. Der eigene C-Shim (`src/Riftward.Native`) wird statisch
   gegen libbgfx/libbimg/libbx gebunden und als `libriftbgfx.so` verlinkt;
   Shader werden offline mit dem gepinnten `shaderc` für GLSL 130 übersetzt —
   es findet keine Shaderkompilierung zur Laufzeit statt.
3. **Manifest**: `.ai/runtime/cache/native/artifact-hashes.json` zeichnet je
   Artefakt SHA-256 und Größe auf. Es wird nur bei Erstbau bzw. `--fresh`
   geschrieben; wiederholte Läufe prüfen vorhandene Artefakte strikt gegen das
   aufgezeichnete Manifest, eine Abweichung bricht mit Fehler ab.
4. **Reproduzierbarkeit**: Zwei aufeinanderfolgende `--fresh`-Neubauten
   erzeugen byteidentische Artefakte. `SOURCE_DATE_EPOCH` ist fixiert
   (1786623387 = Lockfile-`generatedAtUtc`), weil bx `__DATE__/__TIME__`
   einbettet; GNU-ar arbeitet im deterministischen Modus.
5. **ISA-Gate**: Nach jedem Build werden die generierten Makefiles und die
   Buildkonfiguration unter `src/` auf ISA-Anhebung geprüft
   (`-march=native`, AVX/AVX2/AVX512, FMA sind verboten). Die bestätigte
   Mindestbasis ist x86-64-v2 (SSE4.2/POPCNT) gemäß
   `docs/PLATTFORMMATRIX.md`; `-msse4.2` ist damit zulässige Basisflag.

Cache-Lage: `.ai/runtime/cache/native/` (Git-ignoriert) mit `src/`
(hashgeprüfte Quellarchive und entpackte Bäume), `dist/`
(Laufzeitartefakte: `libSDL3.so.0`, `libriftbgfx.so`, `shaders/*.bin`),
`logs/`, `toolchain/` (benutzerlokale, aus Distributionspaketen extrahierte
Entwicklungswerkzeuge/-header ohne sudo; keine Shipping-Artefakte).
Distributionspakete gelten nicht als Shipping-Version.

## Laufzeitgrenze (Riftward.Platform / Riftward.App)

- Der Host prüft vor dem Laden jedes Artefakt gegen das Hashmanifest; fehlende,
  unvollständige oder hashbeschädigte Dateien führen zu kontrollierten
  Fehlermeldungen ohne Schreibzugriff und ohne Prozessabsturz.
- Native Bibliotheken werden ausschließlich aus dem geprüften Artefaktverzeichnis
  geladen (`NativeLibrary`-Resolver); Distributionspfade werden nie verwendet.
- Besitzregeln Handles: SDL-Sitzung besitzt Fenster; bgfx-Device besitzt die
  Dreiecksressourcen; Freigabe in fester Reihenfolge Programm → Shader →
  Vertex-Buffer → Shutdown. Doppelte Freigabe ist definiertes No-op,
  Nutzung nach Freigabe und falsche Shutdown-Reihenfolge sind kontrollierte
  Fehler (`PlatformErrorCode`).

## Exitcodes des Hosts (`Riftward.App`)

| Code | Bedeutung |
|---|---|
| 0 | Erfolg (Smoke mit mindestens einem fehlerfreien Frame; Effizienzlauf innerhalb aller harten Budgets) |
| 2 | Usage-Fehler |
| 14 | Artefaktmanifest fehlt/unlesbar |
| 15 | Artefakt unvollständig (Größenabweichung) |
| 16 | Artefakt fehlt |
| 17 | Artefakthash weicht ab |
| 18 | Backend/GPU-Kontext nicht initialisierbar oder falsches aktives Backend |
| 19 | Fenster-/Videoinitialisierung fehlgeschlagen |
| 20 | Falsche Freigabereihenfolge (Shutdown vor Ressourcen/Fenstern) |
| 21 | Ungültiges oder freigegebenes Handle |
| 22 | Plattform nicht unterstützt (nur linux-x64 im T-010-Scope) |
| 23 | Smoke endete ohne gerenderten Frame |
| 24 | Effizienzbudget verletzt (Report wurde dennoch geschrieben) |
| 25 | Benchmark-Szenario unbekannt oder noch nicht implementiert (kein Report; T-020/T-021/T-023) |
| 26 | Bench-Budget verletzt (Report wurde dennoch geschrieben; T-020/T-021/T-023) |
| 27 | Zwischenmetriken oder Report widersprechen dem Schemavertrag (T-020/T-021/T-023; der Report wird zur Diagnose geschrieben, gilt aber nicht als Beleg) |
| 28 | Reportpfad nicht schreibbar (T-020/T-021/T-023) |
| 29 | Opt-in Frame-Evidenzartefakt fehlgeschlagen; der Report wurde dennoch geschrieben und bindet `captured=false` mit Grund (T-023) |
| 30 | Soak-Zuverlässigkeitsgate verletzt (Wachstum, Trend, Watchdog-Stall, Warm-tick-Allokation, Kettenabweichung); der Report wurde dennoch geschrieben und klar als nicht bestanden markiert (T-022) |
| 31 | Soaklauf unvollständig oder vorzeitig beendet; der Teilreport gilt ausdrücklich nicht als Evidenz (T-022) |
| 32 | Soak-Szenario unbekannt oder noch nicht implementiert; kein Report (T-022) |

Die Codes sind Teil des öffentlichen Befehlsvertrags; Änderungen benötigen eine
dokumentierte Entscheidung und eine Anpassung der Tests
(`exitCodeMappingIsStableAndDocumented`).

## Öffentliche Befehle

```bash
./scripts/rift.sh plattformsmoke --report artifacts/t010/smoke.json
./scripts/rift.sh effizienzbaseline --report artifacts/t010/effizienz.json
./scripts/rift.sh bench --scenario bench-empty --report artifacts/t020/bench-empty.json
./scripts/rift.sh bench --scenario bench-sim --report artifacts/t021/bench-sim.json
./scripts/rift.sh bench --scenario bench-representative --report artifacts/t023/bench-representative.json
./scripts/rift.sh soak --scenario soak-replay --report artifacts/t022/soak-replay-authoritative.json
```

Beide ersten Befehle schreiben einen einzeiligen maschinenlesbaren JSON-Report mit
OS/Kernel, CPU-Modell/Flagauszug, GPU/GL-Treiberstring, Backend, Pins,
Artefaktmanifest-Hash, Messwerten und Budgetbewertung (Effizienz).

`bench` (T-020) führt die deterministische leere Szene `BENCH-EMPTY` nativ auf
linux-x64 aus: 1920×1080, Low-Profil, GL-3.3-Core-Pflichtpfad, VSync-Policy wie
die Effizienzbaseline, festes Kameraflugskript mit Seed. Der Report bindet je
Kennzahl Einheit und Erfassungsmethode (p50/p95/p99-Framezeit, Allokationen je
warmem Frame, GC-Pausen, Working-Set-Stichproben, Draw-/Submit-Aufrufe,
sichtbare Dreiecke, GPU-Zeit und bgfx-verwalteter GPU-Speicher aus der neuen
Shim-Statistikschnittstelle oder explizit unavailable mit Grund), die
Umgebungsbinding (OS/Kernel, CPU, GPU/Treiber, Backend, Pins, Commit,
Buildmodus, Szenen-/Seed-ID, Warm-up/Messdauer) sowie das fail-closed-Budgetgate.
Unbekannte oder noch nicht implementierte Szenarien brechen mit Exitcode 25 ab,
ohne einen Report zu schreiben. Läufe auf dem Entwickler-PC sind diagnostische
Baseline gemäß dem Q-OPS-001-Klärungsprotokoll; Pflichtprofile bleiben ohne
benannte Referenzhardware `NOT-MEASURED`.

`bench --scenario bench-sim` (T-021) führt die headless Simulationsbaseline
rein CPU-seitig nativ auf linux-x64 im bestehenden Host aus — ohne Fenster,
Renderer oder Netzwerk; die native Artefakte (SDL3/bgfx/Shader) werden dafür
nicht geladen, der Befehls- und Exitcodevertrag bleibt identisch (25/26/27/28
wie oben). Der Report bindet Szenario-/Seed-ID, Welt- und Vertragskennungen
aus `docs/SIMULATIONSVERTRAG.md`, Befehlsplanhash, Startzustandshash und
Zustands-Hashketten-Stichproben (`fnv1a64-canonical-chain-v1`, Start/Intervall/
End), Tickzeit-p50/p95/p99, Allokationen je warmem Tick (je-Tick-Delta des
präzisen GC-Zählers), GC-Pausen, Working-Set-Stichproben über einen
allokationsarmen `/proc`-Sampler sowie Umgebungsbinding (OS/RID/CPU/Pins/
Commit/Buildmodus). Headless nicht anwendbare Kennzahlen (GPU-Zeit, Draw-/
Submit-Aufrufe, sichtbare Dreiecke) sind ausschließlich unavailable mit
maschinenlesbarem Grund; ein angeblich messender Wert wird vom Schema
abgewiesen. Das Budgetgate entscheidet fail-closed gegen 16 ms harte
Tickzeitgrenze mit ausgewiesenem 8-ms-Ziel sowie gegen die im Simulations-
vertrag fixierte Allokationsgrenze je warmem Tick.

Die Shim-Grenze (`riftbgfx_shim.h`) wurde für T-020 um zwei dokumentierte
Funktionen erweitert: `rift_bgfx_stats_snapshot` (flache Momentaufnahme von
`bgfx::Stats`: Draws, gerenderte Dreiecke, GPU-Timer, bgfx-verwalteter Speicher)
und `rift_view_transform` (View-/Projektionsmatrix eines Views). Die Erweiterung
folgt demselben Reproduzierbarkeitsvertrag; zwei aufeinanderfolgende
`--fresh`-Neubauten bleiben byteidentisch.

Für T-023 wurde die Shim-Grenze erneut unter demselben Reproduzierbarkeits-
vertrag erweitert (`rift_tex_*`, `rift_fb_*`, `rift_view_frame_buffer`,
`rift_blit_full`, `rift_read_texture_begin`, `rift_uniform_*`,
`rift_set_texture`, `rift_ib_create`/`-destroy`, `rift_vb_create_layout`,
`rift_draw_submit`, `rift_bgfx_caps`). Die Erweiterung bringt ausschließlich
flache bgfx-Aufrufe an die C#-Grenze: Instanzdaten werden je Submit aus einem
vom Host festgepinnten Puffer in den bgfx-Ringkopiert, Knochenpaletten laufen
als RGBA32F-Textur, Schattenpaesse nutzen eigene Renderziele mit gespeicherter
Lichtdistanz, und der opt-in Einzelabgriff folgt dem Muster Renderziel → Blit →
Readback. Besitzregeln der neuen Handles (Freigabe Framebuffer → Textur →
Uniform → Index-/Vertex-Buffer, gesamt vor dem bgfx-Shutdown) liegen bei
`Riftward.App.Bench.RepBenchRunner/SceneResources`; `BgfxDevice` prüft
Handlegültigkeit und Initialisierung. Es findet weiterhin keine
Shaderkompilierung zur Laufzeit statt; alle T-023-Shader werden offline mit
dem gepinnten shaderc für GLSL 130 übersetzt.

## bench-representative (T-023) — Befehls-, Exitcode- und Abgriffvertrag

`bench --scenario bench-representative` führt den integrierten Belastungsframe
aus: 1920×1080, Low-Anzeigeprofil, GL-3.3-Core-Pflichtpfad ohne stillen
Backend-Fallback, VSync-Policy wie die Effizienzbaseline. Die Simulation wird
unverändert gemäß `docs/SIMULATIONSVERTRAG.md` V1 wiederverwendet und über
eine feste Frame-zu-Tick-Zuordnung (alle zwei Frames ein Tick) deterministisch
getaktet; die Darstellung liest ausschließlich schreibgeschützte Zugriffe des
Simulationskerns und mutiert den Zustand nie. Der Report (Schemaversion 3)
bindet Kompositionsziele und -istzaehler, p50/p95/p99 von Frame-, GPU- und
Tickzeit, Allokationen je warmem Frame, GC-Pausen, Working Set,
bgfx-verwalteten GPU-Speicher (diskreter VRAM bleibt unavailable mit Grund),
Draw-/Submit-Aufrufe, sichtbare Dreiecke (Hauptansicht ohne
Schattenwiederholung sowie bgfx-Globalwert), gleichzeitige Partikel,
Szenenaufbauzeit mit ausdrücklicher Nichtanwendbarkeit der Kartenlade-Budget-
zeile (Eigentum von BENCH-LOAD), die Zustands-Hashkette als
K2-Regressionsanker sowie die volle Umgebungsbinding. Das Budgetgate
entscheidet fail-closed ausschließlich gegen `docs/PERFORMANCE_BUDGET.md`,
den AC-T010-07/T-020/T-021-Praezedenz und die Szenebudgettabelle.

Der opt-in Parameter `--capture-frame PFAD` schreibt nach Abschluss des
Messfensters genau einen 1920×1080-Einzelabgriff einer deterministischen
Kameraposition an festem Frameindex als unkomprimiertes 32-Bit-BMP; Report
binden Hash, Abmessungen, Format, Szenario-/Seed-/Commitbinding und die
Aussagegrenze (Graybox-Lastbelegung, niemals Gameplay-, Atmosphären- oder
Shipping-Beleg; öffentliche Verwendung nur über die Bedingungen in
`docs/communication/MEDIA_LAB.md` plus Projektleitungsautorisierung). Ohne
Flag entsteht kein Bild; das Messverhalten ist identisch. Ein fehlgeschlagener
Abgriff ergibt Exitcode 29, schreibt den Report jedoch mit `captured=false`
und maschinenlesbarem Grund.

## soak-replay (T-022) — Zuverlässigkeitsvertrag

`soak --scenario soak-replay` führt den deterministischen Replay-Soak nativ
auf linux-x64 im bestehenden Host rein CPU-seitig aus — ohne Fenster,
Renderer und Netzwerk; die nativen SDL3-/bgfx-Artefakte werden nicht geladen.
Evidenzmodell nach Soakvertrag `docs/SOAKVERTRAG.md` V2
(Projektleitungsentscheidung 2026-08-25): NF-002 wird durch mindestens drei
unabhängige Fresh-Prozess-Wiederholungsläufe über den kompletten skriptierten
Planhorizont des Simulationsvertrags V1 (576000 Messsticks plus Warm-up,
genau 250 vollständig simulierte Agenten) in Release-naher Konfiguration
nachgewiesen. Die beschleunigte Taktung (`--diagnostic-accelerated`, voller
Horizont) ist dafür zulässig, weil die Pacing-Unabhängigkeit durch Test
belegt ist; jeder bestandene Lauf ist im Report als Evidenzeinheit markiert.
Horizontverkürzte Läufe (`--horizon-ticks N`) sind ausschließlich diagnostisch
und werden als keine Evidenzeinheit gekennzeichnet. `--reference-out`
erzeugt eine Golden-Fixture der Kettenstichproben, ist aber unabhängig vom
gewählten Horizont immer eine separate diagnostische Referenzemission
(`accelerated-reference-emission-diagnostic-v1`) und niemals Evidenz; erst ein
Fresh-Prozess-Folgelauf darf gegen die versionierte Fixture vergleichen. Der
frühere autoritative Achtstunden-Realzeitlauf wurde
absichtlich per SIGTERM abgebrochen und darf nicht neu gestartet werden;
das Restrisiko des nicht nachgewiesenen zusammenhängenden
Achtstunden-Echtzeitbetriebs ist im Soakvertrag Abschnitt 6 ausgewiesen.

Der Report bindet Soakvertrag (V2), Golden-Fixture mit SHA-256 und
Vergleichsergebnis je Stichprobe, Working-Set-Fensterstichproben, strenge
Per-Tick-Allokationen gemäß Simulationsvertrag §5 (zusätzlich gatefreie
Fensterdeltas als Telemetrie), GC-Pausen, Fortschritts-Watchdog, die
gatefrei diagnostischen Tickzeitdriftfelder sowie die maschinenlesbare
Aussage, dass die tolerierte Benchmarkstreuung (Q-TEC-010) offen bleibt.
Im Reportabschnitt `execution` zählen `ticksExecuted` und `requiredTicks`
einschließlich der Warm-up-Ticks (`warmupTicks`); der gatende Messhorizont
ohne Warm-up steht in `gate.limits.requiredTicks`.
Das Gate entscheidet fail-closed ausschließlich gegen die absoluten
Grenzwerte des Soakvertrags; Verletzungen ergeben Exitcode 30 bei trotzdem
geschriebenem, als nicht bestanden markiertem Report. Ein vorzeitiger Abbruch
ergibt Exitcode 31 mit einem als keine Evidenz gekennzeichneten Teilreport;
unbekannte oder noch nicht implementierte Soakszenarien brechen mit
Exitcode 32 ohne Report ab. Läufe auf dem Entwickler-PC sind diagnostische
Baseline gemäß Q-OPS-001; Pflichtprofile bleiben `NOT-MEASURED`.
