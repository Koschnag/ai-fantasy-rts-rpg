# T-023 – Integrierter repräsentativer Belastungsframe BENCH-REPRESENTATIVE

**Status:** `DONE` / `accepted`
**Umsetzung:** Implementierungslauf `01M0TWMNGRTYQJA414M5DCEEYE`
(Akteur `t023-implementation-autopilot`), 2026-08-24 — lieferte Code, Tests und
Doku, konnte den fensterpflichtigen Nachweis in der kopflosen Sitzung jedoch
nicht erbringen (kontrolliert Exit 19, Restpunkt offen gelassen).
**Unabhängige Prüfung und Vollendung:** Review-/Vollendungslauf
`01M0V8V4RVW9V77S94AXVK9EXK` (Aakteur `t023-review-completion`, Modell
`stealth/ox-alpha`), 2026-08-25 — reparierte die In-Scope-Defekte, führte die
fensterpflichtigen nativen Läufe auf dem Entwickler-PC aus und wies alle
Abnahmekriterien nach.

## In-Scope-Defekte, die der Reviewlauf reparierte

1. **Stale Shim-Artefakte:** Der native Build übersetzte `libriftbgfx.so`
   nur bei fehlender Datei; Quelländerungen (hier: `rift_set_uniform_mat4`)
   erreichten das Artefakt nicht → `EntryPointNotFoundException`. Der Build
   ist jetzt eingabehashgesteuert (`libriftbgfx.inputs.sha256` über
   Shim-Quellen und Pins); bei unveränderten Eingaben bleibt der Neubau unter
   fixiertem `SOURCE_DATE_EPOCH` byteidentisch (verifiziert).
2. **Terrain-Indexformat:** Der Indexpuffer wurde mit
   `BGFX_BUFFER_INDEX32` angelegt, enthielt aber Uint16-Paare → Müllindizes,
   Hauptansicht praktisch leer (4,7 % Szenenabdeckung). Korrekt ist
   `uint32Indices:false`; danach 77 % Abdeckung.
3. **Renderzustandsbits abweichend vom Pin:** `StateCullCw` lag mit 2^44 im
   AlphaRef-Feld statt auf 2^36, `StateWriteZ` mit 2^42 statt 2^46,
   `StateBlendAlphaBits` war stellenvertauscht. Korrigiert auf die Werte des
   gepinnten bgfx-Stands; Culling/Depth-Write wirken nun tatsächlich.
4. **Kameraflug unterhalb der Landschaft:** Die Neigungsformel erzeugte
   Augen­höhen von −25 bis −60 m; die Hauptansicht sah trotz voller
   Submissions fast keine Geometrie. Neigung now 27–62° über dem Horizont,
   Auge stets über der Wandhöhe (testseitig gebunden).
5. **Allokationen je Warmframe 234 KiB statt ≤ 1 KiB:** Der Kamerapfad wurde
   je Frame als wachsender Prefix neu generiert (O(n²)), `EmitterTint`
   allokierte je Partikel ein Array (5000×/Frame), Landschaftsplatzierung,
   Projektion und Billboard-Basen wurden je Frame neu erzeugt, und die
   Messinfrastruktur entstand innerhalb des Messfensters. Alle Hotpaths sind
   jetzt allokationsfrei bzw. vorab berechnet (praefixstabiler Kamerastrom,
   statische Toene, Scratch-Matrizen); Messwert: **1,3 Bytes je Warmframe**.
6. **Captureindex hinter dem Horizont:** `cameraSamples[captureFrameIndex]`
   griff mit 1470 in eine 1440-Einträge-Liste und wäre stets in die
   Fehlklasse gelaufen. Der Kamerahorizont deckt jetzt bei opt-in den
   Abgriffindex ab; zusätzlich wacht eine kontrollierte Grenzprüfung.
7. **Tickzeitmesspunkt:** Die Stop-Marke lag erst nach Instanz-/Palette-
   Komposition; gemessen wurde jetzt ausschließlich `world.Tick()`
   (T-021-konsistent), die präzise GC-Scan-Instrumentierung verließ den
   Tickzeitpfad. Tick-p99 fiel von ~12 ms auf ~1,0 ms.
8. **Veralteter rift.sh-Hilfetext** nennt jetzt bench-representative.
9./10. Kleinere Korrekturen: `ReadOnlySpan`-Rückgabe für Emittertöne,
   Messlisten mit Kapazität vor dem Fensterstart.

Ein Zwischenvorfall im Reviewlauf (`git checkout --` auf die uncommittete
`riftbgfx_shim.cpp`) wurde durch Wiederherstellung des vollständig im
Review-Kontext erfassten Difftextes behoben und durch Neubau plus
funktionalen Benchlauf (identische Hashkette) äquivalenzbelegt.

## Ergebnis gegen die Abnahmekriterien

| Kriterium | Ergebnis | Evidenz |
|---|---|---|
| AC-T023-01 | Öffentlicher Befehl existiert, läuft nativ linux-x64 (1920×1080, Low, GL-3.3-Core ohne stilen Fallback, VSync wie Effizienzbaseline); unbekannt/nicht implementiert → Exit 25 ohne Report; fehlender App-Build → rift.sh-Guard Exit 4; Schemaabweichung → Exit 27 | Run `01M0V8V4RVW9V77S94AXVK9EXK`, CLI-Tests, echte Läufe Exit 0 |
| AC-T023-02 | Komposition codegebunden an die Szenebudgettabelle: 350 sichtbare instanzierte geskinnte Einheiten = 250 simulierte Agenten + 100 Hintergrundakteure, 48 Bones je Einheit (576 Dreiecke), Terrain 115.200 Dreiecke, Sonne + 4 lokale Schattenlichter mit aktiven Paessen (512²), Partikelpeak exakt 5000; Istzaehler maschinenlesbar gebunden; nicht künstlich leer (77 % Pixelabdeckung im Abgriff) | `compositionTargets`/`compositionObserved` im Report, Geometrie-/Landschaftstests |
| AC-T023-03 | Report Schemaversion 3 mit Einheit+Methode je Kennzahl; p50/p95/p99 Frame/GPU/Tick, Allokationen, GC, RSS-Stichproben, bgfx-GPUspeicher, diskreter VRAM unavailable mit Grund, Draw/Dreiecke global+Hauptansicht, Partikel, Szenenaufbauzeit, Kartenladezeile explizit `applicable:false` (Eigentum BENCH-LOAD), Hashketten-Stichproben, volle Umgebungsbinding; Fail-closed-Schemaprüfung lehnt Fälschungen ab | Golden-/Negativmatrix-Test, echter Report |
| AC-T023-04 | Gate fail-closed ausschließlich gegen dokumentierte Grenzen; Ziele getrennt ausgewiesen; Verletzung → Exit 26 bei geschriebenem Report (im ersten Reparaturzyklus live beobachtet: Allokationsverletzung erkannt, Exit 26, Report gültig) | Gate-Matrix-Test, Reparaturzyklus-Report, finale Läufe |
| AC-T023-05 | Zwei Fresh-Prozessläufe: identische Hashketten (Start `03aa25ae22891408…`, Ende `56d98265914d9196…`, 16 Intervallstichproben identisch); Fremdseed 20260825 → Endhash `6b43eb62bf61420e…`; kein K3-Anspruch | Reports rep-final1/rep-final2/rep-negseed |
| AC-T023-06 | Profilbestehen nur mit deklarierter Bindung auf benannten Referenzrechnern; alle drei Pflichtprofile `NOT-MEASURED`; Entwickler-PC-Lauf als `diagnostic-developer-workstation` gekennzeichnet | `profiles`/`baseline` im Report, Bindungstests |
| AC-T023-07 | Riftward.Simulation frei von SDL/bgfx/Plattformtypen; Ansicht liest nur öffentliche Zustandsleser; kein LINQ/Boxing im Instanzpfad; null neue Abhängigkeiten; 0 Warnungen | Architekturtests, Suite 184/184 |
| AC-T023-08 | Opt-in `--capture-frame`: genau ein 1920×1080-BMP an Frame 1470 > Messende 1439, SHA-256 reportgebunden und artefaktgleich, Aussagegrenze gebunden; ohne Flag kein Bild, Hashketten identisch zum Flag-freien Lauf; Schreibfehler-/Nichtunterstützungsfälle kontrolliert (Exit 29) | rep-capture.json + frame-evidence.bmp + Tests |
| AC-T023-09 | Fehlerklassen (Reportpfad, Artefaktpfad, beschaedigte Metriken, fehlender Build, widersprüchliche Argumente) kontrolliert; Exitcodes 25/26/27/28 unverändert, neuer Code 29 dokumentiert | Fault-Injection-/Mapping-Tests |
| AC-T023-10 | lint/build/test/security/verify grün, 0 neue Warnungen; bench-empty/bench-sim-Verträge unveraendert (Suiten bestehen); fensterpflichtige Läufe in dieser Sitzung nativ ausgeführt — der Restpunkt der Implementierungssitzung ist damit gegenstandslos | Gates.md-Evidenz im Run |
| AC-T023-11 | Diagnostischer Gesamtbeweislauf nativ auf dem Entwickler-PC (i7-3770/RX 570, Mesa radeonsi via virtuellem kwin_wayland/Xwayland): Reports mit allen Pflichtfeldern, Gateergebnis pass, Hashkettenbindung, Capture-SHA; Kennzeichnung als diagnostische Baseline; Pflichtprofile NOT-MEASURED | Run-Evidenz, Reports |
| AC-T023-12 | AUTOMATION.md (Befehlsvertrag), PERFORMANCE_BUDGET.md (Nachweisort ohne Budgetänderung), G-PERF-Register, NATIVE_UNTERBAU.md (Shim-, Exitcode-, Abgriffvertrag), ARCHITEKTUR.md (Snapshot-Konsum) aktualisiert; rift.sh-Hilfetext konsistent; verify grün | Doku-Review dieses Laufs |

## Diagnostische Messwerte (Entwickler-PC i7-3770/RX 570, Mesa radeonsi, Release)

Zwei Fresh-Prozessläufe à 240 Warm-up- + 1200 Messframes (720 Simulationsticks,
36 s Simulationszeit, 1920×1080 Low, VSync): Frame p50/p95/p99 ≈ 17,2 / 17,6 /
**18,0–18,7 ms** (vsyncgebunden, hart 33,3 ms), Tick p50/p95/p99 ≈ 0,56 / 0,84 /
**1,03 ms** (Ziel 8 ms, hart 16 ms), GPU-Zeit p99 ≈ **2,11 ms** (Ziel 14 ms,
hart 30 ms), **1,3 Bytes verwaltete Allokationen je Warmem Frame** (Grenze
1024), GC-Pausen 0 ms/0, Working-Set max ≈ 155 MiB (Ziel 3500 MiB), max 11
Draw-/Submit-Aufrufe, Hauptansicht 326.800 sichtbare Dreiecke (Limit 2 Mio.,
ohne Schattenwiederholung), Partikelspitze konstant 5000.

## Bekannte Restpunkte und Einschränkungen

- Pflichtprofile bleiben `NOT-MEASURED`, bis die Projektleitung Referenzrechner
  benennt (Q-OPS-001 bleibt `OFFEN`); dieser Lauf ist diagnostische Baseline
  auf dem Entwickler-PC.
- Die Aussage des Frame-Evidenzartefakts ist ausschließlich die Graybox-
  Lastbelegung; eine öffentliche Verwendung bleibt an
  `docs/communication/MEDIA_LAB.md` plus Projektleitungsautorisierung gebunden.
  Das Artefakt selbst bleibt lokal im gitignorierten `artifacts/t023/`.
- Q-TEC-008 (AOT/CoreCLR-Vergleich), Q-TEC-009 (M1-Messmethodik) und die
  Q-TEC-010-Streuung bleiben ausdrücklich außerhalb des Auftrags; T-022 bleibt
  durch Letztere blockiert.
- Die übrigen Pflichtbenchmarks (BENCH-ARMY/BATTLE/BASE/PATH/LOAD) schlagen
  weiterhin bewusst mit Exitcode 25 fehl.
