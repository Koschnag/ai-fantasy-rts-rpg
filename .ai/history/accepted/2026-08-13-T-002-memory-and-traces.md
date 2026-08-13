# Akzeptierte Revalidierung T-002

- Implementierungslauf: `01KZXP5JJ5HS85W7RTZ8Z7R16K`
- Review-Fix-Lauf: `01KZXR7WZB2GGMED2NRPGSJG5J`
- Aufgabe: `T-002`
- Status: akzeptiert
- Ausgangs-Commit: `f390b20`
- End-Commit: wird beim gemeinsamen Abschlusscommit nachgetragen

## Ergebnis

Ein unabhängiger Review hat vier reale Randfälle aufgedeckt und nach ihrer Behebung erneut geprüft: doppelte Annahme desselben Vorschlags, abweichende Quellgrößenregeln zwischen Memory und RAG, Workspace-Austritt über Ledger- oder Source-Symlinks sowie unerkannte Kürzung des Retrieval-Tails. Der akzeptierte Stand schließt alle vier Fälle fail-closed.

Nur effektiv angenommene, quellenfrische, nicht abgelaufene und konfliktfreie Records gelangen ins Retrieval. Jeder konsumierte Vorschlag wird effektiv `superseded`; alle Memory-Operationen verwenden dasselbe `rag.maxFileBytes`. Ledger- und Quellpfade dürfen keine Symlink-, Junction- oder ReparsePoint-Komponente enthalten. Abgeschlossene v2-Runs verankern Retrieval-Anzahl und letzten Trace-Hash im finalen Event und in der gehashten Summary.

## Evidenz je Pflichtgate

| Gate | Nachweis |
|---|---|
| `G-SPEC` | `AC-T002-01` bis `AC-T002-05` wurden im unabhängigen Review jeweils akzeptiert. |
| `G-FORMAT` | gepinnter Fantomas-Check grün |
| `G-STATIC` | Locked Restore und Release-Build: 0 Warnungen, 0 Fehler |
| `G-TEST` | 15/15 Tests; Regressionen für Doppelannahme, 5020-Byte-Quelle bei 4096-Byte-Limit, Ledger-/Source-Symlinks sowie Tail-Kürzung und vollständige Trace-Leerung |
| `G-HARNESS` | alter v1-Run bleibt gültig; neue v2-Runs prüfen Trace-Datei und Abschlussanker |
| `G-SECURITY` | Secret-/JSON-/Locked-NuGet-Audit-/LFS-Baseline erfolgreich |
| `G-FRESH` | isolierter Fresh-Checkout-Bootstrap mit Build, Tests, RAG-Build und Verify erfolgreich |

## Bewusst verbleibende Grenzen

- Der unveränderliche Retrieval-Tail-Anker entsteht beim Abschluss; laufende Runs besitzen noch keinen finalen Anker.
- `MEM-0003` ist wegen geändertem Quellenhash sichtbar stale und wird nicht abgerufen. Eine fachliche Ersetzung benötigt einen neuen Vorschlag und getrennten Review.
- Vollständige Prompt-/Modellprovenienz, kriterienspezifische Evidenz und Retention bleiben T-004.
- Der Security-Lauf ist ein lokales Baseline-Gate und keine vollständige Release-, SAST-, Malware- oder Rechtsprüfung.

Der ausführliche Prüfnachweis liegt unter `docs/abnahme/T-002-memory-and-traces.md`; lokale Rohläufe bleiben gitignored unter `.ai/runtime/runs/`.
