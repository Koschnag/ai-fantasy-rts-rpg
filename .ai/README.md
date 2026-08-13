# Rift Harness

Dieses Verzeichnis enthält den versionierten Vertrag für autonome KI-Arbeit. Die ausführliche Architektur steht in [`docs/HARNESS.md`](../docs/HARNESS.md).

- `config.json`: Quellen, Pfade, Ranking, Retention und Redaction
- `schemas/`: JSON-Verträge für Runs, Events, Aufgaben, Gedächtnis, Evidenz, Retrieval und Assets
- `tasks/`: maschinenlesbare, begrenzte Arbeitspakete
- `prompts/`: versionierte Arbeitsrollen; keine Secrets
- `evals/`: objektive Gates und Rubriken
- `memory/`: ausschließlich kuratierte, quellengebundene Records
- `history/accepted/`: bewusst bereinigte Abschlussberichte
- `runtime/`: lokale Runs, Index und Cache; persistent, aber nicht in Git

Der Index ist nur eine Projektion. Bei Widersprüchen gilt die Quellenhierarchie in `AGENTS.md` und `config.json`.

Akzeptierte History bleibt auditierbar, wird im aktuellen lexikalischen RAG aber bewusst nicht indexiert: Das MVP besitzt noch keine Autoritätsgewichtung und Meta-Berichte könnten sonst fachliche Primärquellen überranken. T-002 darf sie erst mit expliziter niedriger Vertrauensklasse wieder aufnehmen.
