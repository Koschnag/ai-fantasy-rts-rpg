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
| [004](004-autonome-synthetische-assetproduktion.md) | spezifikationsgetriebene synthetische Assetproduktion mit Quarantäne und getrennten Reviews | akzeptiert | 2026-08-13 |
| [005](005-taktile-retail-era-erfahrung.md) | eigenständige taktile Retail-Era-Grammatik für Forschung und spätere Produkterfahrung | akzeptiert | 2026-08-23 |
| [006](006-performancebeweis-sprachrollen-und-integration.md) | gemessene Optimierung, C#/F#/Python-Rollen und geschützter `main` | akzeptiert | 2026-08-24 |

**Provenienz der Nummer:** Historische `ADR 005`-Verweise in unveränderlicher,
akzeptierter Evidenz der lokalen Linie bis einschließlich Elternstand
`5ef1fca68a2f28076c16226e4e8d92e4bf0b802e` – ausgenommen Evidenz aus dem
Origin-Elternstand `fee6a9bebf7fcf612c441e27530355d5cbdcad6e` – meinen die
heutige ADR 006 unter
`docs/entscheidungen/006-performancebeweis-sprachrollen-und-integration.md`.
`ADR 005`-Verweise der Origin-Linie bei
`fee6a9bebf7fcf612c441e27530355d5cbdcad6e` sowie aktuelle Verweise meinen
die beibehaltene Retail-Era-ADR unter
`docs/entscheidungen/005-taktile-retail-era-erfahrung.md`. Dieser Merge hat nur
die Dokumentnummer geändert und akzeptierte Evidenz nicht umgeschrieben.
