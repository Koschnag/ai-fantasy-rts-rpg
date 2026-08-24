# 2026-08-24 – T-020: Leere Benchmarkszene BENCH-EMPTY (bereinigte History)

## Ergebnis

T-020 ist `DONE` und `accepted`. Der unabhängige Review-/Vollendungslauf
`01M0T2GGVHV79RFDSKNSJ1QV8B` (Akteur `t020-review-completion`) prüfte die
`READY`-Spezifikation, implementierte den vollen Umfang und wies alle
Abnahmekriterien AC-T020-01 bis AC-T020-08 mit Evidenz nach.

## Kernlieferung

- Shim-Erweiterung (`rift_bgfx_stats_snapshot`, `rift_view_transform`) mit
  zweifach byteidentischem `--fresh`-Neubau; ISA-Gate PASS.
- Öffentlicher Befehl `./scripts/rift.sh bench --scenario bench-empty --report PFAD`.
- BenchRunner mit deterministischer Orbit-Kamera (Seed 20260824,
  Hashbindung), Telemetrie je Kennzahl mit Einheit/Methodenkennung
  (p50/p95/p99, Allokationen, GC-Pausen, Working-Set, Draws, Dreiecke,
  GPU-Zeit, bgfx-verwalteter GPU-Speicher), fail-closed Budgetgate
  ausschließlich gegen dokumentierte Grenzwerte.
- Szenarioregistry: unbekannte/nicht implementierte Szenarien → Exitcode 25
  ohne Report. Neue Exitcodes 25–28 dokumentiert und mapping-stabil getestet.
- Profilbindungs-Ehrlichkeitsregel: Entwickler-PC-Läufe diagnostische
  Baseline, Pflichtprofile `NOT-MEASURED`, Eskalation statt Ersatz (Q-OPS-001).
- 12 neue Tests, Suite 158/158; Gates build/lint/test/security/rag-build/
  verify grün, null neue Compiler-/Analyzerwarnungen, keine neue Abhängigkeit.

## Diagnostische Messwerte

p99 2,979 ms (≤ 33,3), 565,2 B Allokationen pro warmem Frame (≤ 1 KiB),
GC-Pausen 0, Working-Set max ~195 MiB (≤ 300 Ziel), 1 Draw (≤ 8), 1 Dreieck,
GPU-Zeit p99 0,078 ms (bgfx-Timer gemessen). Renderer im Kopflos-Aufbau:
llvmpipe (Mesa 26.0.3); Reportstruktur zweier Läufe identisch (158 Knoten).

## Bekannte Restpunkte

- Pflichtprofile bleiben `NOT-MEASURED` bis zur Benennung von Referenzrechnern
  durch die Projektleitung (Q-OPS-001 `OFFEN`).
- Übrige Pflichtbenchmarks und `BENCH-REPRESENTATIVE` folgen in T-021/T-022/T-023.
