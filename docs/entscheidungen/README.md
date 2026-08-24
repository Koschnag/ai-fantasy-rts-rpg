# Entscheidungsprotokoll

Wichtige, schwer rückgängig zu machende oder querschnittliche Entscheidungen werden als Architecture Decision Record (ADR) dokumentiert. Dazu zählen auch fachliche Entscheidungen mit technischen Folgen.

## Ablauf

1. Vorlage `000-vorlage.md` kopieren.
2. Fortlaufende Nummer und kurzen Titel vergeben, zum Beispiel `001-datenbankwahl.md`.
3. Alternativen und Folgen konkret beschreiben.
4. Status zunächst `vorgeschlagen`, nach Bestätigung `akzeptiert` setzen.
5. Ersetzte Entscheidungen nicht löschen, sondern als `ersetzt` markieren und verlinken.

## Register

| ADR | Entscheidung | Status | Datum |
|---|---|---|---|
| [001](001-dotnet-sprach-und-aot-strategie.md) | .NET 10, C#/F#-Grenzen und gemessenes Native AOT | akzeptiert | 2026-08-13 |
| [002](002-plattform-und-renderunterbau.md) | SDL3 + bgfx mit konservativen Backends | akzeptiert | 2026-08-13 |
| [003](003-agent-harness-und-rag.md) | lokales event-sourced Harness und BM25-RAG | akzeptiert | 2026-08-13 |
| [004](004-autonome-synthetische-assetproduktion.md) | vollständig agentische synthetische Assetproduktion mit getrennten Gates | akzeptiert | 2026-08-13 |
| [005](005-performancebeweis-sprachrollen-und-integration.md) | gemessene Optimierung, C#/F#/Python-Rollen und geschützter `main` | akzeptiert | 2026-08-24 |
