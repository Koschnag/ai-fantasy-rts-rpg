# Abnahme T-001: RiftHarness Walking Skeleton

> Historischer Erstnachweis. Der aktuelle gehärtete Abnahmestand steht in `2026-08-13-T-001-hardening.md`.

- Run-ID: `01KZXGK8Q4W1VTDF4F44DC8W9N`
- Aufgabe: `T-001`
- Status: akzeptiert
- Ausgangs-/End-Commit: nicht vorhanden; das Projekt wurde in einem noch nicht committeten neuen Repository aufgebaut
- Finaler Event-Hash: `61c58531b01a3f42204d2e70c3ea1d72cd1c655d2fd8a397e88dd1d3e816a3dc`
- Summary-Hash: `20c3a3498c032f568e912d9f24883618a125a4a7a57331a1a34ef295b2c5b0c8`

## Ergebnis

Das dependency-freie F#/.NET-10-Harness stellt sortierbare Run-IDs, einen lokalen Run-Lebenszyklus, append-only JSONL-Ereignisse mit SHA-256-Hashkette, strukturierte Secret-Feld-Redaction, einen deterministischen BM25-RAG-Index mit Pfad-/Zeilen-/Hashzitaten sowie Integritätsprüfung bereit.

Alle Kriterien `AC-T001-01` bis `AC-T001-06` wurden durch Integrationstests beziehungsweise den echten Workspace-Lauf abgedeckt. Build: 0 Warnungen, 0 Fehler. Tests: 4/4 erfolgreich. Die Golden Query nach Mindesthardware und Bildrate rankt `docs/PERFORMANCE_BUDGET.md` auf Platz 1. Die Run- und Indexprüfung meldete `valid=true`.

## Bewusst verbleibende Grenzen

- Das aktuelle `run.json` ist kleiner als das vollständige Zielmanifest.
- Memory-Promotion, Retrieval-Traces, Evidenzaufnahme und Retention benötigen Folgetasks.
- Die aktuelle Redaction ist eine Schutzschicht, kein vollständiger Secret-Scanner.
- Ohne initialen Git-Commit kann der Lauf noch keinen unveränderlichen Commit-Bezug enthalten.

Die lokalen Rohereignisse bleiben unter `.ai/runtime/runs/01KZXGK8Q4W1VTDF4F44DC8W9N/` und werden nicht eingecheckt.
