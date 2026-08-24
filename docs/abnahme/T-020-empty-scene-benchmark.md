# T-020 – Leere Benchmarkszene BENCH-EMPTY mit Telemetrie und Budgetgate

- **Status:** `DONE` (2026-08-24)
- **Umsetzung:** der unabhängige Review-/Vollendungslauf
  **`01M0T2GGVHV79RFDSKNSJ1QV8B`** (Akteur `t020-review-completion`) prüfte die
  vorgefundene `READY`-Spezifikation, implementierte den gesamten Umfang und
  wies alle Kriterien nach. Der Lauf begann auf Commit
  `942352054f594da162a53d7e7cde8eae91b244aa`.
- finaler Eventhash des Laufs: `cdd93648134b87ee29670cc793d0477929a20df1703f9d7ae3eadff2918e593f`
- Summaryhash: `f67f8945c1a4d9b979b57f7078d3a30917a660733f756dd1a613e2bb15fb4867`
- Abnahmekriterien-Beweisführung: Evidenz je ID im Laufverzeichnis
  `.ai/runtime/runs/01M0T2GGVHV79RFDSKNSJ1QV8B/evidence/`

## Vorläuferlauf und Werkzeugvorfall (Transparenz)

Der erste Versuch desselben Laufs (`01M0SX2CQ8XA2A5HGDF48KAAZE`, identischer
Akteur, identischer Umfang) wurde nach einem doppelten Evidence-Span-Append
von `verify` korrekt fail-closed als invalid verworfen. Der Rohbestand blieb
vollständig erhalten und liegt dokumentiert unter
`.ai/runtime/defective-runs/` (kein Löschen, keine Kettenmanipulation). Alle
Evidenzinhalte wurden im Nachfolgerlauf unverändert neu verankert; Messwerte
und Gates sind identisch.

## Gelieferte Anteile

| Bereich | Ergebnis |
|---|---|
| Shim-Grenze | `riftbgfx_shim.h/.cpp`: neue dokumentierte Funktionen `rift_bgfx_stats_snapshot` (`bgfx::Stats`: Draws, Compute, gerenderte Dreiecke, GPU-Timer, bgfx-verwalteter Speicher) und `rift_view_transform`; C#-Bindung über `[LibraryImport]` mit blittablem Struct, Fassade `BgfxFrameStats`, Besitzregeln unverändert |
| Nativer Build | zweifach aufeinanderfolgende `--fresh`-Neubauten sind **byteidentisch** (`libriftbgfx.so` SHA-256 `c8654c2c…aff2a`); ISA-Gate PASS; neuer Offline-Shader `bench_empty.vs.sc` (Weltkoordinaten + `u_viewProj`) im Buildskript und Hashmanifest |
| Befehlsvertrag | `./scripts/rift.sh bench --scenario bench-empty --report PFAD` (App-Build-Guard Exitcode 4); unbekannte oder registriert-unimplementierte Szenarien brechen mit Exitcode 25 ab und schreiben keinen Report |
| BenchRunner | deterministische leere Szene: 1920×1080, Low, GL-3.3-Core-Pflichtpfad, VSync wie Effizienzbaseline, Warm-up 180 / Messphase 900 Frames, Seed 20260824; Kameraflugskript als quantisierte Xorshift-Orbitbahn (Algorithmus-ID `xorshift64star-fixedpoint-v1`) mit SHA-256-Bindung im Report |
| Telemetrie | je Kennzahl Einheit + Erfassungsmethode: p50/p95/p99-Framezeit, verwaltete Allokationen je warmem Frame, GC-Pausensumme/-anzahl, Working-Set min/max/end, Draw-/Submit-Aufrufe, sichtbare Dreiecke, GPU-Zeit (bgfx-Timer) und bgfx-verwalteter GPU-Speicher — jeweils gemessen oder explizit `measured:false` mit maschinenlesbarem Grund; Umgebungsbinding OS/Kernel/CPU/GPU/GL/Backend/RID/Commit/Buildmodus/Pins/Szenario/Seed |
| Budgetgate | fail-closed ausschließlich gegen dokumentierte Grenzwerte (p99 ≤ 33,3 ms; Allokationen ≤ 1 KiB je warmem Frame; ≤ 8 Draw-/Submit-Aufrufe; keine Laufzeitshaderkompilierung; RSS ≤ 300 MB Ziel / 450 MB hart); Verletzung → Exitcode 26 bei trotzdem geschriebenem Report; nicht messbare Werte zählen als Verletzung |
| Schemaprüfung | Report-Schemaversion 1 als closed shape; erfundene (ohne Methodenkennung), typenfremde oder grundlos fehlende Werte sowie nicht begründete unavailable-Kennzeichnungen schlagen fehl; Selbstprüfung vor Gültigkeit (Verstoß → Exitcode 27) |
| Ehrlichkeitsregel | Profilbestehen nur durch deklarierte Bindung an die Referenzklasse **und** benannte Referenzrechner; Entwickler-PC-Läufe stets diagnostische Baseline; Pflichtprofile ohne Referenzhardware `NOT-MEASURED` mit Eskalationsgrund |
| Tests | 12 neue F#-Tests: Percentil-/Gate-Golden-Fixtures je Bestehens- und Verletzungsklasse, Report-Schemamatrix (Goldendokument + 7 Fabrikationsklassen), Kamerapfad-Determinismus mit Hashfixture, Matrix-Sanität, Profilbindungs-Matrix über synthetische Hardwarebeschreibungen, Szenarioregistry, Exitcode-Stabilität 25–28, CLI-Fault-Injection ohne Fensteroeffnung, nicht schreibbarer Reportpfad, Strukturgleichheit, rift.sh-Vertragsquelle; Suite 158/158 |
| Doku | `NATIVE_UNTERBAU.md` (Exitcodes 25–28, bench-Vertrag, Shim-Erweiterung), `AUTOMATION.md` (bench-Zeile), `PERFORMANCE_BUDGET.md` (Nachweisort/-methode ohne Budgetänderung), `.ai/evals/quality-gates.json` (G-PERF präzisiert inkl. Baseline-Status), `README.md` |

## Abnahmekriterien (Evidenz je ID im Lauf `…QV8B/evidence/`)

| AC | Nachweis | Ergebnis |
|---|---|---|
| AC-T020-01 | `t020-bench-empty-run1.json` (Exitcode 0, vollständige Evidenzfelder inkl. Szenen-/Seed-ID, Commit `9423520…`, Buildmodus Release, Pins, Warm-up/Messdauer); `t020-cli-negative.json` + `-log` (bench-army/totally-unknown → Exitcode 25 ohne Report; fehlender App-Build → Exitcode 4); CLI-Tests in Suite | PASS |
| AC-T020-02 | Report enthält alle geforderten Kennzahlen mit Einheit/Methodenkennung; GPU-Zeit gemessen (`bgfx-stats-gpu-timer-p99`, timerFreqHz 1 GHz), VRAM gemessen (`bgfx-managed-memory-texture-rt-transient-end`); Schemamatrix lehnt Fabrikationen ab (Test `report schema accepts golden and rejects fabrication matrix`) | PASS |
| AC-T020-03 | Hashfixture-Test für identische Samplefolgen (SHA-256 `6be4bc23…98e0` für Seed/256 Samples); Strukturgleichheit zweier echter Läufe: 158 Knoten, 0 Differenzen (`t020-bench-empty-run2.json`); Konfiguration maschinenlesbar gebunden | PASS |
| AC-T020-04 | Gate-Evaluator-Fixtures je Klasse (p99/Allokation/Draws/Shaderkompilierung/RSS hart/RSS-Ziel/nicht messbar/NaN); echter Lauf hält alle Grenzwerte: p99 2,979 ms ≤ 33,3; 565,2 B ≤ 1024 B; 1 Draw ≤ 8; keine Laufzeitshaderkompilierung; RSS max ~195 MiB ≤ 450 hart (Ziel 300 eingehalten); Grenzwerte gegen `PERFORMANCE_BUDGET.md`/AC-T010-07 abgeglichen, keine Lockerung | PASS |
| AC-T020-05 | Test `profile binding honesty matrix is enforced` über synthetische Beschreibungen (RX 570 Entwickler-PC → Diagnose trotz Klassenpassung; GTX-660/M1/RX-570-Klassenbindungen nur bei benannten Rechnern gültig; Klassenfremde → abgewiesen); Live-Report markiert alle drei Pflichtprofile `NOT-MEASURED` mit Grund `reference-hardware-unnamed-qops001` und Baseline-Klassifikation `diagnostic-developer-workstation` | PASS |
| AC-T020-06 | Fault-Injection: nicht schreibbarer Reportpfad → Exitcode 28 ohne Absturz (Test + Livepfad-Prüfung); beschädigte Zwischenmetriken → Schemaverstoß Exitcode 27; fehlender App-Build → Exitcode 4 (Live-Check mit temporär entfernten DLL, Zustand wiederhergestellt); ungültige Argumente → Usage 2 bzw. 25; kein Netz, keine Schreibvorgänge außerhalb erlaubter Verzeichnisse | PASS |
| AC-T020-07 | `gate-build.log` (0 Warnungen/Fehler), `gate-lint.log`, `gate-test.log` (158/158), `gate-security.log`, `gate-rag-build.log`, `gate-verify.log` (`valid:true`); keine neue Abhängigkeit — Telemetrie nutzt BCL (`GC`, `Stopwatch`, `/proc`, `System.Text.Json`) plus gepinnte SDL3-/bgfx-Grenze; Architekturtest hält Native-Importe im Plattform-Layer; Effizienzbaseline-Regression nach Shim-Änderung: alle Budgets weiter grün (`t020-effizienz-regression.json`, Idle-Fenster verkürzt auf 60 s) | PASS |
| AC-T020-08 | Doku-Review mit Pfadangaben: `docs/NATIVE_UNTERBAU.md` (Befehle/Exitcodes/Shim), `docs/AUTOMATION.md` (bench-Zeile inkl. Exitcode-25-Verhalten), `docs/PERFORMANCE_BUDGET.md` (Nachweisort/-methode, kein Budgetwert geändert), `.ai/evals/quality-gates.json` G-PERF präzisiert mit Baseline-Status laut Q-OPS-001-Klärungsprotokoll; `rift.sh verify` grün | PASS |

## Messwerte des diagnostischen Laufs (Entwickler-PC, Kopflos-Aufbau)

Renderer dieser Sitzung ist `llvmpipe (LLVM 21.1.8, 256 bits)` unter Mesa
26.0.3 (Xwayland/kwin-virtual ohne DRI3-Durchreichung); T-010 hatte für seine
Smokes radeonsi/polaris10. Der Renderer-String ist in jedem Report gebunden;
die Klassifizierung bleibt `diagnostic-developer-workstation`. p50 1,734 ms /
p95 2,226 ms / p99 2,979 ms; Allokationen 565,2 B je warmem Frame; GC-Pausen
0 ms/0; Working-Set 197952–200040 KiB; 1 Draw, 1 sichtbares Dreieck je Frame;
GPU-Zeit p99 0,078 ms; bgfx-verwalteter GPU-Speicher 49152 B.

## Bekannte Restpunkte

- Alle Pflichtprofile (`HW-PC-MIN`, `HW-MAC-MIN`, `HW-PC-HIGH`) bleiben
  `NOT-MEASURED`; ein Profilbestehen entsteht erst durch benannte
  Referenzrechner und deklarierte Bindung (Q-OPS-001 bleibt `OFFEN`,
  Projektleitung).
- Die übrigen Pflichtbenchmarks (`BENCH-ARMY/BATTLE/BASE/PATH/LOAD`) und
  `BENCH-REPRESENTATIVE` sind nicht Bestandteil dieses Auftrags und schlagen
  definiert fehl.
- Das Kameraflugskript treibt in BENCH-EMPTY die Viewtransformation über eine
  Orbitbahn um das technische Testdreieck; Weltgeometrie jenseits davon
  entsteht erst mit T-021/T-023, dieselbe Generator-/Reportbindung wird dort
  wiederverwendet.
- GPU-Zeit/VRAM beruhen auf bgfx-eigenen Statistiken (GL-Timer-Queries bzw.
  Allocatorbuchhaltung) und schließen Treiberoverhead aus; die Methode ist im
  Report je Kennzahl genannt.
