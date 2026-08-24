# Akzeptierte ADR-005-Leitdokument-Integration (unabhängiger Review-/Vollendungslauf)

- Reviewlauf: `01M0RH9XNWJ0VNBDJWB2T61Y3K` (Akteur `adr005-review-completion`)
- Aufgabe: kein BACKLOG-Implementierungstask; Vollendung der von der Projektleitung beschlossenen Leitdokumentänderungen zu ADR 005, die der T-010-Lauf laut Commit `d2ab442` bewusst uncommittet gelassen hatte
- Status: Änderungen vollständig geprüft und als Checkpoint committet; `T-020`–`T-023` und alle übrigen Backlogeinträge bleiben statuskorrekt (`DRAFT`)
- Ausgangscommit: `d2ab442097729767a8e943f9aa39a809821266a0`
- Ergebniscommit: der unmittelbar folgende Checkpoint-Commit „adr005 …“ enthält diesen Bericht
- finaler Eventhash: `ba246a85afb4cd058faf7123399ce2a349debb7b372690f24949d0865d1704d7`
- Summaryhash: `3eaa77452ede97822cba64f80412be0dde192d8ac38f722382def07e2fbecf2d`
- Retrievalkette: 4 Traces, Abschluss-Hash `c0e425568706b170072bbebcf3727a8202159f359fe8ba260ba4a32c0de1e4a4`

## Geprüfte und vollendete Änderungen

1. **Neu: `docs/entscheidungen/005-performancebeweis-sprachrollen-und-integration.md`** – ADR 005 (akzeptiert, 2026-08-24, Projektleitung): Optimierung ist eine gemessene Eigenschaft; C# führt die ausgelieferte Runtime aus; F# spezifiziert/kompiliert/prüft offline; Python bleibt optionaler untrusted Offline-Adapter; Integration nur über Arbeitsbranch → Pull Request → Pflichtgates → Squash-`main`.
2. **`AGENTS.md`** – drei neue Arbeitsregeln (Performancebeweis-Pflicht, Sprachrollen, Push-Schutz für `main`), deckungsgleich mit ADR 005 Abschnitt 1–5.
3. **`docs/ARCHITEKTUR.md`** – drei Entscheidungszeilen (Performancebeweis, Sprachrollen, Integration) mit Quellenverweis ADR 001/ADR 005.
4. **`docs/PERFORMANCE_BUDGET.md`** – Hypothesen-Vorbehalt unter dem Hardwarevertrag plus neuer Abschnitt „Integrierter Repräsentativitätsnachweis“ (`BENCH-REPRESENTATIVE`: ≥350 sichtbare instanzierte Einheiten, ≥48 Bones je normaler Einheit, 250 simulierte Agenten, konkurrierende Gruppenpfade, Landschaft, Sonne, budgetierte lokale Schattenlichter, Partikelspitze; p50/p95/p99-Telemetrie und Evidenzbindung; Bestehen auf `HW-PC-MIN`/`HW-MAC-MIN`).
5. **`docs/QUALITAET.md`** – G-PERF und Performance-Teststrategie um `BENCH-REPRESENTATIVE` erweitert; neue Messregel, dass Architektur/Datenlayout allein keinen Optimierungsnachweis bilden.
6. **`docs/ANFORDERUNGEN.md`** – Nachverfolgbarkeitszeile Z-002 bindet `T-023` ein („isolierte und integrierte Hardwarebenchmarks“).
7. **`docs/entscheidungen/README.md`** – Register ergänzt den bisher fehlenden ADR-004-Eintrag (Datei existiert seit Commit `0c7d2e5`) sowie ADR 005.

## Reviewbefunde

- `blocker`/`hoch`/`medium`: 0.
- `low` (repariert): Die neuen Absätze in `PERFORMANCE_BUDGET.md` und `QUALITAET.md` waren hart umgebrochen und brachen den durchgängigen Einzeilabsatz-Stil beider Dokumente; im Review auf den Dokumentstil normalisiert.
- Konsistenz geprüft gegen die Quellenhierarchie: ADR 004/005-Inhalte, `PROJEKT.md` (Z-002), `ANFORDERUNGEN.md`, committed `BACKLOG.md`/`README.md`/`AUTOMATION.md` (referenzieren ADR 005 bereits), Szenenbudgets (Zahlen deckungsgleich), ADR 001 (Sprachgrenzen kompatibel), `OFFENE_FRAGEN.md` (Q-OPS-001/Q-TEC-008 bleiben korrekt OFFEN).
- Clean-Room: keine fremden Spieltitel, Franchise-, Figuren-, Künstler- oder Soundtracknamen; keine fremden Medien; nur generische Werkzeugbezeichnungen. Keine Secrets.

## Prüfungen (alle Exit 0, Protokolle im Lauf unter `work/`)

- `./scripts/rift.sh lint` (Fantomas-Check + Toolchain-/Lizenz-/ISA-Gate)
- `./scripts/rift.sh build` (Locked Restore, Release, 0 Warnungen)
- `./scripts/rift.sh test` (146/146)
- `./scripts/rift.sh security` (Secrets, JSON, Audit, LFS, Native-Pins)
- `./scripts/rift.sh rag-build` (Index mit ADR 005 neu gebaut: 131 Quellen, 650 Chunks)
- `./scripts/rift.sh verify` (Build + 146/146 Tests + Assets-Check + Harness-Integrität über 33 Runs + Schema-jq)

## Verbleibende Restpunkte

- Das Gate `bench` bleibt ausdrücklich `NICHT VERFÜGBAR`; `BENCH-REPRESENTATIVE` ist mit diesem Commit Spezifikation, kein Messnachweis.
- Q-OPS-001 (konkrete Referenzrechner/Treiber) bleibt offen; die zentrale Effizienzhypothese gilt solange als unbestätigt – das ist die beabsichtigte Wirkung des ADR.
- Kein Task-Manifest unter `.ai/tasks/` angelegt, da es sich um die Integration einer Projektleitungsentscheidung und nicht um einen BACKLOG-Implementierungsauftrag handelt; der Harness-Lauf dokumentiert Provenienz und Evidenz.
