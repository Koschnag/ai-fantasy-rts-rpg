# 2026-08-24 – T-021: Headless Simulationsbaseline BENCH-SIM (bereinigte History)

## Ergebnis

T-021 ist `DONE` und `accepted`. Der unabhängige Review-/Vollendungslauf
`01M0T61A0NT4PBA4CQZKGJS5QC` (Akteur `t021-review-completion`) prüfte die
`READY`-Spezifikation gegen Quellenhierarchie und Clean-Room-Regeln,
implementierte den vollen Umfang und wies alle Abnahmekriterien AC-T021-01 bis
AC-T021-10 mit Evidenz nach.

## Kernlieferung

- Gatender Abschnitt 0: `docs/SIMULATIONSVERTRAG.md` V1 — Numerikmodell
  reine Ganzzahl-Festkomma Q16.16 (`q16-16-fixed-point-intonly-v1`),
  Hashvertragsklassen K1/K2 garantiert und K3 (Cross-Build/-Plattform)
  ausdrücklich nicht behauptet, Seedableitung SplitMix64→Xorshift64*,
  kanonische Ordnung, datennahe Strukturen, Allokationsgrenze
  0 Bytes je warmem Tick (Verschärfung innerhalb der 1-KiB-Obergrenze).
- Neues BCL-only-C#-Projekt `Riftward.Simulation`: fester 20-Hz-Tick, genau
  250 vollständig simulierte mobile Testagenten mit Fortbewegung,
  ganzzahligem Ausweichverhalten und Gruppenbefehlen; synthetische
  Rasterwelt (160×90) mit Blockgraph-Korridorpfadsuche; Pfadhaushalt
  768 je Agent/Abschnitt und 2048 global je Tick; kanonischer
  FNV-1a-64-Zustands-Hash über den dokumentierten Relevantzustand.
- Hostintegration: `./scripts/rift.sh bench --scenario bench-sim --report
  PFAD`; Report Schemaversion 2 (je Kennzahl Einheit+Methode,
  Zustands-Hashketten-Stichproben, Umgebungsbinding inkl. Pins/Commit/
  Planhash/Starthash), headless nicht anwendbare Kennzahlen ausschließlich
  unavailable mit Grund; fail-closed Budgetgate gegen 16 ms hart/8 ms Ziel
  und die Abschnitt-0-Allokationsgrenze; Exitcodes 25/26/27/28 unverändert
  wiederverwendet und dokumentiert.
- Profilbindungs-Ehrlichkeitsregel analog T-020; Pflichtprofile bleiben
  `NOT-MEASURED` bis zur Benennung von Referenzrechnern (Q-OPS-001).
- 14 neue Tests (Suite 172/172): Vertragsspiegel, Determinismus inkl.
  Golden-Fixtures, Negativfälle Seed/Befehlsreihenfolge, kanonische Ordnung,
  Pfadhaushalt, Schemamatrix, Gate-Matrix, Registry, CLI-/Fresh-Prozess-
  Verträge, Fault-Injection, Architekturreinheit.

## Diagnostische Messwerte

Zwei Fresh-Prozessläufe (1680 Ticks): p99 0,458/0,480 ms, 0,000 Bytes
Allokationen je warmem Tick, GC-Pausen 0, Working-Set ~48 MiB; Hashketten
identisch (Start `10e13faf142094db`, Ende `de43976087a5f6a2`, 23 Glieder).
Reportstrukturen strukturell identisch.

## Bekannte Restpunkte

- Q-OPS-001/Q-TEC-004/Q-TEC-005/Q-TEC-010 bleiben formal `OFFEN`; konkrete
  Werte aus Abschnitt 0 stehen zur Ratifizierung durch die verantwortlichen
  Leads.
- `bench-empty`-Regressionslauf in dieser Sitzung displaylos nicht
  ausführbar (SDL3 ohne Wayland); Absicherung über unveränderten Codespfad
  und vollständige T-020-Testsuite.
- T-022 (Soak, Streuung) und T-023 (repräsentativer Frame) folgen.
