# T-021 – Headless feste Simulation mit 250 mobilen Testagenten

**Status:** `DONE` / `accepted`
**Umsetzung und unabhängige Prüfung:** Review-/Vollendungslauf
`01M0T61A0NT4PBA4CQZKGJS5QC` (Akteur `t021-review-completion`, Modell
`stealth/ox-alpha`), 2026-08-24

## Ergebnis gegen die Abnahmekriterien

| Kriterium | Ergebnis | Evidenz |
|---|---|---|
| AC-T021-01 | Abschnitt 0 vor Hotpath abgeschlossen; `docs/SIMULATIONSVERTRAG.md` V1 mit Alternativen/Gründen/Rückrollweg je Wahl; keine Cross-Plattform-Hashzusage | Vertragsdokument + `SimulationContract.cs`-Spiegel + Konsistenztest |
| AC-T021-02 | `./scripts/rift.sh bench --scenario bench-sim --report PFAD` nativ linux-x64 im bestehenden Host; zwei echte Fresh-Prozessläufe Exit 0; unbekannt/nicht implementiert → 25 ohne Report; fehlender Build → Exit 4 (rift.sh-Guard) | Run-Evidenz AC-T021-02, Reports in Run-Work |
| AC-T021-03 | Genau 250 gleichzeitig vollständig simulierte mobile Testagenten (SoA), Fortbewegung/Ausweichen/Gruppenbefehle auf synthetischer Navigationswelt inkl. konkurrierender langer Wege über budgetierte hierarchische Pfadsuche; Szenario-/Seed-/Starthash-/Planhash maschinengebunden; kein Spielinhalt, keine Fremdbezüge | Schemaassertion + Welttests + Reportbindung |
| AC-T021-04 | Report Schemaversion 2 je Kennzahl Einheit+Methode; alle geforderten Kennzahlen; GPU/Draw/Dreiecke ausschließlich unavailable mit Grund; Fail-closed-Schema lehnt Erfundenes/Typenfremdes/Fehlendes ab | Golden-Fixture-Negativmatrix |
| AC-T021-05 | Gate fail-closed: p99-Tickzeit ≤ 16 ms hart (8-ms-Ziel ausgewiesen), Allokationen ≤ 0 B je warmem Tick (Abschnitt 0); Verletzung → Exit ≠ 0 bei trotzdem geschriebenem Report; echte Läufe bestanden | Run-Reports + Gate-Matrix |
| AC-T021-06 | Zwei Fresh-Prozessläufe: identische Hashketten (23 Glieder, Start `10e13faf142094db…`, Ende `de43976087a5f6a2…`); fremder Seed/umgeordnete Folge ändern den Endhash nachweislich; Gleichheitsklassen exakt nach Vertrag (K1/K2, kein K3) | Run-Reports + Determinismustests |
| AC-T021-07 | Profilbestehen nur mit deklarierter Bindung auf benannten Referenzrechnern; Entwickler-PC diagnostisch; Pflichtprofile `NOT-MEASURED` | Bindungsmatrix + echter Report |
| AC-T021-08 | Kontrollierte Fehlerklassen 2/4/25/26/27/28 stabil dokumentiert und getestet; kein Absturz/Hängen/Netzwerk/Schreibzugriff außerhalb erlaubter Verzeichnisse | Fault-Injection-Tests |
| AC-T021-09 | build/lint/test/security grün; 0 neue Warnungen; BCL-only; Architekturtest hält Simulation frei von SDL/bgfx/Plattformtypen, F# und Fließkomma im Zustandskern | Suite 172/172 |
| AC-T021-10 | AUTOMATION.md, PERFORMANCE_BUDGET.md, G-PERF-Register, NATIVE_UNTERBAU.md, ARCHITEKTUR.md, DATENMODELL.md aktualisiert; verify grün | Doku-Review dieses Runs |

## Diagnostische Messwerte (Entwickler-PC i7-3770/RX 570, Release)

Zwei Fresh-Prozessläufe à 480 Warm-up- + 1200 Messsticks (1680 Ticks ≙ 84 s
Simulationszeit): p50 0,277 ms / p95 0,378 ms / p99 0,458 ms beziehungsweise
0,480 ms (Ziel 8 ms, hart 16 ms deutlich eingehalten); **0,000 Bytes verwaltete
Allokationen je warmem Tick**; GC-Pausen 0 ms/0; Working-Set ~47,5–48,8 MiB;
Hashketten der beiden Läufe identisch. Pfadhaushalt aktiv (bis 2048
Knotenerweiterungen je Tick), kein Agent jemals fälschlich `Unreachable`.

## Bekannte Restpunkte und Einschränkungen

- Pflichtprofile bleiben `NOT-MEASURED`, bis die Projektleitung Referenzrechner
  benennt (Q-OPS-001 bleibt `OFFEN`); dieser Lauf ist diagnostische Baseline.
- Die tolerierte Benchmarkstreuung (Q-TEC-010) bleibt offen und blockiert
  weiterhin T-022; die Allokationsgrenze je warmem Tick ist vertraglich fixiert.
- Q-TEC-004/Q-TEC-005 bleiben formal `OFFEN` zur Ratifizierung durch Simulation
  Lead bzw. Technical Lead; die konkreten Werte wurden verfahrensmäßig im
  gatenden Abschnitt 0 innerhalb der delegierten Spike-Kriterien gewählt.
- Ein `bench-empty`-Regressionslauf war in dieser kopflosen Sitzung nicht
  ausführbar (SDL3 ohne Wayland gebaut, kein X-Server verfügbar; Fehlerklasse
  NIEDRIG). Absicherung: unveränderter bench-empty-Codespfad (nur
  Dispatch-Weiche ergänzt) plus vollständige T-020-Testsuite im grünen Lauf;
  letzter akzeptierter nativer Nachweis bleibt commitgebunden (Lauf
  `01M0T2GGVHV79RFDSKNSJ1QV8B`).
- Keine visuellen Medienartefakte in diesem Auftrag (Media-Lab-Prüfung gemäß
  Auftragsbegründung; Telemetrie ist die prüfbare Evidenz).
