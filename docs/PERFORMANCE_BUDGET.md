# Performance- und Ressourcenbudgets

## Hardwarevertrag

**Status:** ENTSCHIEDEN; konkrete Budgets sind auf echter Hardware zu validieren

Die Werte dieses Dokuments sind verbindliche Zielverträge und zunächst Performancehypothesen. Ein vorhandenes Budget, passender Datenentwurf, erfolgreicher Build oder schneller Lauf auf stärkerer Hardware ist noch kein Optimierungsnachweis. „Bestanden“ setzt reproduzierbare Release-nahe Evidenz auf der jeweiligen realen Referenzklasse voraus; siehe ADR 006.

Die Hardwaremodelle sind Leistungsklassen, keine Bindung an exakt ein Bauteil. Schnellere Hardware wird unterstützt, erzeugt aber keinen eigenen Effektpfad oberhalb der geplanten höchsten Qualitätsstufe.

| Profil | CPU / SoC | GPU | Speicher | Verbindliches Ziel |
|---|---|---|---:|---|
| `HW-PC-MIN` | Intel Core i7-3770 oder vergleichbare 4C/8T-Leistung | GTX-660-Klasse, typischerweise 2 GB VRAM | 8 GB DDR3-Klasse | 1920×1080 Low, stabile 30 FPS |
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
| Eingabe-zu-Reaktion | 100 ms | 150 ms | Befehlsmarker / Simulation; Nachweisort seit T-032: der maschinenlesbare Report von `./scripts/rift.sh kommandoschleife` (Reaktionsticks von Befehlstick bis erstem Effektsnapshot bei vertraglichen 20 Hz, abgeleitet auf Ziel ≤ 2 Ticks / hart ≤ 3 Ticks gemäß `docs/KOMMANDOVERTRAG.md` V1); dieselbe Budgetzeile trägt seit T-033 die Wechselreaktion des Moduswechsels (Wechsel-Intent-Tick S bis erster Gültigkeitsprüfung M im neuen Modus, `switchReactionTicks = M − S`, Nachweisort `modeSession.switchReactionTicks` und fail-closed `gate.switchReaction` desselben Reports gemäß `docs/MODEVERTRAG.md` V1 Abschnitt 4/7) — kein Wert dieser Zeile wird geändert |

Die 30-FPS-Grenze ist der unterstützte Mindestmodus, nicht das bevorzugte Ergebnis. Grafikstufen dürfen Effekte reduzieren, nicht Gameplay, Simulation oder taktische Sichtbarkeit verändern. Dynamische Auflösung ist optional, aber 1080p muss der reguläre Low-Modus bleiben und darf nicht durch dauerhaftes Upscaling aus einer deutlich kleineren internen Auflösung nur nominell erfüllt werden.

Das DDR3-Profil ist kein Nostalgie-Benchmark ohne Produktfolge. Es prüft die
These, dass gezielte Datenlayouts, Offline-Verarbeitung und begrenzte
Laufzeitsysteme mehr nutzbare Qualität liefern können als die pauschale
Anhebung der Mindestanforderungen. Ein Ergebnis zählt nur, wenn dieselbe
Spielmechanik, taktische Lesbarkeit und akzeptierte Atmosphärenabsicht erhalten
bleiben.

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

`BENCH-EMPTY` ist seit T-020 implementiert und über
`./scripts/rift.sh bench --scenario bench-empty --report PFAD` ausführbar. Der
Nachweisort ist der maschinenlesbare Telemetriereport (Schemaversion 1) mit je
Kennzahl Einheit und Erfassungsmethode; das Budgetgate entscheidet dort
fail-closed ausschließlich gegen die oben fixierten Werte (p99 ≤ 33,3 ms auf
den Minimumprofilen, Allokationen ≤ 1 KiB je warmem Frame, ≤ 8 Draw-/Submit-
Aufrufe je Frame, keine Laufzeitshaderkompilierung, RSS ≤ 300 MB Ziel /
450 MB hart, geerbt aus AC-T010-07). Kein Budgetwert wird dadurch geändert.
Messungen ohne deklarierte Bindung an eine Referenzklasse auf benannter
Referenzhardware bleiben diagnostische Baseline (Q-OPS-001-Klärungsprotokoll).

Die Simulationsbaseline `BENCH-SIM` ist seit T-021 implementiert und über
`./scripts/rift.sh bench --scenario bench-sim --report PFAD` ausführbar; sie
misst die Zeile „CPU-Spielsimulation: 8 ms Ziel / 16 ms harte Grenze bei
20 Hz" der Laufzeitziele. Nachweisort ist der maschinenlesbare Report
(Schemaversion 2) mit Tickzeit-p50/p95/p99 (Methode: Stoppuhr-Delta je Tick),
Allokationen je warmem Tick (Methode: `GC.GetTotalAllocatedBytes(precise)`
Delta je Tick, summiert), GC-Pausen, Working-Set-Stichproben und der
Zustands-Hashkette gemäß dem in `docs/SIMULATIONSVERTRAG.md` fixierten
Vertrag. Das Gate entscheidet fail-closed ausschließlich gegen 16 ms harte
Grenze (8-ms-Ziel ausgewiesen) sowie die dort vertragliche Allokationsgrenze
je warmem Tick (0 Bytes innerhalb der Auftragsobergrenze von 1 KiB); kein
Budgetwert dieses Dokuments wird dadurch geändert. Headless nicht anwendbare
Kennzahlen sind im Report explizit unavailable mit Grund. Auch hier gilt:
Läufe ohne deklarierte Referenzklassenbindung auf benannter Referenzhardware
sind diagnostische Baseline (Q-OPS-001), Pflichtprofile bleiben
`NOT-MEASURED`.

## Integrierter Repräsentativitätsnachweis

Nach den isolierten Renderer- und Simulationsbaselines kombiniert `BENCH-REPRESENTATIVE` mindestens 350 sichtbare instanzierte Einheiten, den repräsentativen Animationspfad mit mindestens 48 Bones je normaler Einheit, 250 vollständig simulierte mobile Agenten, konkurrierende Gruppenpfade, Landschaft, Sonne, die budgetierten lokalen Schattenlichter und eine Partikelspitze. Der Aufbau darf visuell einfach sein; seine Lastverteilung darf nicht künstlich leer sein.

Der Nachweis protokolliert mindestens p50/p95/p99 von Frame-, GPU- und Simulationszeit, Allokationen und GC-Pausen, Working Set, VRAM beziehungsweise Unified Memory, Draw-/Submit-Aufrufe, sichtbare Dreiecke und Ladezeit. Außerdem bindet er Rohmessung, Warm-up, Laufdauer, Szenen-/Seed-ID, Commit, Buildmodus, Runtimeprofil, Hardware, OS und Treiber. `BENCH-REPRESENTATIVE` muss auf `HW-PC-MIN` und `HW-MAC-MIN` bestehen, bevor die zentrale Effizienzhypothese als bestätigt gilt.

Seit T-023 ist das Szenario als
`./scripts/rift.sh bench --scenario bench-representative --report PFAD`
implementiert (Report Schemaversion 3): Die Komposition ist codegebunden an
die obige Szenebudgettabelle gebunden (350 sichtbare/250 simulierte Einheiten,
48 Bones, 1+4 Lichter mit aktiven Schattenpaessen, Partikelspitze am
Budgetpeak von 5000), die simulierte Komponente wiederverwendet den
Simulationsvertrag V1 unverändert, und das Budgetgate entscheidet fail-closed
ausschließlich gegen die oben fixierten Werte (33,3 ms Frame-p99 der
Minimumprofile, GPU 14 ms Ziel/30 ms hart, Tick 8 ms Ziel/16 ms hart,
Allokationen ≤ 1 KiB je warmem Frame gemäß AC-T010-07/T-020-Praezedenz,
≤ 1200 Draw-/Submit-Aufrufe, ≤ 2 Mio. sichtbare Dreiecke Low ohne
Schattenwiederholung, ≤ 5000 Partikel, null Laufzeitshaderkompilierungen,
Arbeitssatz gegen die Prozesszeile 3,5 GB Ziel/4,5 GB hart). Der Nachweisort
der Kartenlade-Budgetzeile bleibt ausschließlich `BENCH-LOAD`; der integrierte
Report weist diese Zeile als nicht anwendbar aus. Kein Budgetwert wird dadurch
geändert. Bis die Projektleitung Referenzrechner benennt (Q-OPS-001), bleiben
alle Pflichtprofile für dieses Szenario `NOT-MEASURED`; Läufe auf dem
Entwickler-PC gelten als diagnostische Baseline.

## Zuverlässigkeitsnachweis (NF-002)

Der 8-Stunden-Soak aus `ANFORDERUNGEN.md` NF-002 wird über
`./scripts/rift.sh soak --scenario soak-replay --report PFAD` nativ auf
linux-x64 nachgewiesen. Nachweisort und Methode des genauen numerischen
Leak-Schwellwerts sind ausschließlich der versionierte Soakvertrag
`docs/SOAKVERTRAG.md` (V2, Abschnitt 0: doppelte Schwellwertform aus
absolutem Wachstum und Trendkriterium mit Konsistenzbedingung,
Hangkriterium als Fortschritts-Watchdog; Abschnitt 4:
wiederholungsbasiertes Evidenzmodell mit mindestens drei Fresh-Prozess-
Läufen über den kompletten Planhorizont laut Projektleitungsentscheidung
2026-08-25; Abschnitt 6: ausgewiesenes Restrisiko des nicht nachgewiesenen
zusammenhängenden Achtstunden-Echtzeitbetriebs). Dieses Dokument dient
dabei nur als obere Grenze: Kein Budgetwert dieser Tabelle wird durch den
Soak geändert, erweitert oder als Soak-Erlaubnis umgedeutet; die
Allokationsgrenze je warmem Tick bleibt unverändert an den
Simulationsvertrag V1 gebunden. Läufe auf dem Entwickler-PC sind
diagnostische Baseline gemäß Q-OPS-001; Pflichtprofile bleiben ohne
benannte Referenzhardware `NOT-MEASURED`.
