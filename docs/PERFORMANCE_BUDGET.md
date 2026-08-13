# Performance- und Ressourcenbudgets

## Hardwarevertrag

**Status:** ENTSCHIEDEN; konkrete Budgets sind auf echter Hardware zu validieren

Die Hardwaremodelle sind Leistungsklassen, keine Bindung an exakt ein Bauteil. Schnellere Hardware wird unterstützt, erzeugt aber keinen eigenen Effektpfad oberhalb der geplanten höchsten Qualitätsstufe.

| Profil | CPU / SoC | GPU | Speicher | Verbindliches Ziel |
|---|---|---|---:|---|
| `HW-PC-MIN` | Intel Core i7-3770 oder vergleichbare 4C/8T-Leistung | GTX-660-Klasse, typischerweise 2 GB VRAM | 8 GB RAM | 1920×1080 Low, stabile 30 FPS |
| `HW-MAC-MIN` | Apple M1 | integrierte M1-GPU | 8 GB Unified Memory | 1920×1080 bzw. äquivalente skalierte Ausgabe, stabile 30 FPS |
| `HW-PC-HIGH` | passend zur RX-580-Ära oder besser | Radeon RX 580 bzw. vergleichbar | 8 GB Minimum, 16 GB empfohlen | 1920×1080 High, stabile 60 FPS |

Auf den Minimum-Profilen sind 60 FPS das bevorzugte Optimierungsziel, aber 30 FPS die harte Abnahmegrenze. Die RX-580-Klasse definiert die höchste geplante Darstellungsqualität, nicht die maximal unterstützte Hardware.

## Laufzeitziele

| Messgröße | Ziel | Harte Grenze | Messung |
|---|---:|---:|---|
| Bildrate `HW-PC-MIN` / `HW-MAC-MIN` | 60 FPS bevorzugt | p99 ≤ 33,3 ms, keine anhaltenden Einbrüche unter 30 FPS | automatisierter Kameraflug + Gefecht |
| Bildrate `HW-PC-HIGH` | 60 FPS | p99 ≤ 16,7 ms, keine anhaltenden Einbrüche unter 60 FPS | automatisierter Kameraflug + Gefecht |
| CPU-Spielsimulation | 8 ms bei 20 Hz | 16 ms | feste Benchmark-Welt |
| GPU-Zeit Minimum-Profile | 14 ms bevorzugt | p99 ≤ 30 ms | GPU-Timestamps, nach Aufwärmphase |
| Prozess-Arbeitssatz PC | 3,0–3,5 GB | 4,5 GB Ladepeak | Telemetrie über komplette Karte |
| diskreter VRAM `HW-PC-MIN` | 1,2–1,5 GB | 1,8 GB | Backend-Debugdaten / externes Profiling |
| kombinierter App-Fußabdruck `HW-MAC-MIN` | ≤ 3,5 GB | ≤ 4,0 GB im Spiel, ≤ 4,5 GB kurzer Ladepeak | OS-Speicherdruck und Telemetrie |
| Ladezeit Karte auf SATA-SSD | 20 s | 35 s | kalter und warmer Lauf |
| Eingabe-zu-Reaktion | 100 ms | 150 ms | Befehlsmarker / Simulation |

Die 30-FPS-Grenze ist der unterstützte Mindestmodus, nicht das bevorzugte Ergebnis. Grafikstufen dürfen Effekte reduzieren, nicht Gameplay, Simulation oder taktische Sichtbarkeit verändern. Dynamische Auflösung ist optional, aber 1080p muss der reguläre Low-Modus bleiben und darf nicht durch dauerhaftes Upscaling aus einer deutlich kleineren internen Auflösung nur nominell erfüllt werden.

## Szenenbudget für den Vertical Slice

| Bereich | Ausgangsbudget | Bemerkung |
|---|---:|---|
| gleichzeitig vollständig simulierte mobile Einheiten | 250 | plus vereinfachte Hintergrundakteure |
| gleichzeitig sichtbare Einheiten | 350 | Animations-LOD, Culling und Instancing verpflichtend |
| sichtbare Dreiecke Hauptansicht Low | 2 Mio. | einschließlich normaler LODs, ohne Schattenwiederholung |
| Draw-/Submit-Aufrufe | 1.200 | Materialsortierung und Instancing |
| dynamische Schattenlichter | 1 Sonne + 4 lokale | lokale Lichter nur selektiv mit Schatten |
| transparente Partikel gleichzeitig Low | 5.000 | überwiegend GPU-/batchfähig, kontrolliertes Overdraw |
| Knochen je normale Einheit | 48 | Helden maximal 96 |
| Materialien je normale Einheit | 2 | Helden maximal 4 |
| Textur einer normalen Einheit | 1K-Set | Helden/Bosse maximal 2K-Set |
| Umgebungsmodul | 1K–2K atlasfähig | Trim Sheets und Materialwiederverwendung |

Alle Zahlen sind Startbudgets. Ein Budget darf nur per dokumentierter Entscheidung verändert werden, nachdem ein reproduzierbares Profil zeigt, dass das Gesamtziel weiter eingehalten wird.

## Renderstrategie

- plattformspezifisch robuster Backend-Pfad statt eines einzigen modernen API-Zwangs
- GPU-Instancing, LOD, Frustum-/Occlusion-Culling und Materialsortierung ab dem ersten Benchmark
- gebackene indirekte Beleuchtung und Light Probes statt Echtzeit-GI
- begrenzte Shadow Maps mit stabilen Kaskaden und Qualitätsstufen
- post processing sparsam: Tonemapping, Bloom, Farbkorrektur und optional leichtes SSAO
- kein Raytracing, keine Echtzeit-GI, kein Nanite-/Virtual-Geometry-ähnliches System
- kein separater Ultra-Pfad für Hardware oberhalb der RX-580-Klasse; zusätzliche Leistung verbessert primär Stabilität und Auflösungsreserve
- Texturen offline komprimieren; keine unkomprimierten Produktionsassets im Runtime-Paket

## Simulationsstrategie

- feste Gameplay-Tickrate, Rendering davon entkoppelt
- datennahe Arrays/Strukturen in heißen Pfaden, keine unbegrenzten Objektgraphen
- Pfadsuche hierarchisch und über mehrere Ticks budgetiert
- Wahrnehmung, Entscheidungslogik und Animation mit Entfernungs-/Relevanz-LOD
- reproduzierbare Szenarien mit Zustands-Hashes für Regressionen
- Allokationsbudget pro normalem Simulations-Tick: nach Warm-up nahe null; genaue Grenze im Technik-Spike festlegen

## Pflicht-Benchmarks

- `BENCH-EMPTY`: leere Karte, Backend- und Frame-Overhead
- `BENCH-ARMY`: maximale sichtbare gemischte Armee in Bewegung
- `BENCH-BATTLE`: Fähigkeiten, Projektil- und Partikel-Spitzen
- `BENCH-BASE`: vollständige Basis, Arbeiter und Produktionswarteschlangen
- `BENCH-PATH`: mehrere Gruppen mit konkurrierenden langen Wegen
- `BENCH-LOAD`: kaltes Laden und Asset-Streaming
