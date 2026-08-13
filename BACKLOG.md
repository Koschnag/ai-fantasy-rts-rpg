# Backlog

Nur Einträge mit Status `READY` dürfen ohne weitere fachliche Klärung implementiert werden.

## Priorisierung

- `MUST`: für das MVP unverzichtbar
- `SHOULD`: hoher Nutzen, aber kein Freigabekriterium
- `COULD`: optional
- `WONT`: bewusst nicht in dieser Version

## Epics

| ID | Epic | Nutzen | Priorität | Abhängigkeiten | Status |
|---|---|---|---|---|---|
| E-001 | Autonome Produktionsplattform | KI-Arbeit ist reproduzierbar, erinnerungsfähig und prüfbar | MUST | – | IN ARBEIT |
| E-002 | Plattform-Walking-Skeleton | dasselbe leere Spiel startet auf Windows, Linux und macOS | MUST | E-001 | OFFEN |
| E-003 | Performancekern | Rendering und Simulation halten die Hardwarebudgets | MUST | E-002 | OFFEN |
| E-004 | Graybox-Hybridspiel | Held, Erkundung, Aufbau und Armee bilden eine spaßige Schleife | MUST | E-003 | OFFEN |
| E-005 | Atmosphärischer Vertical Slice | alle Kernsysteme und finalitätsnahe Inhalte bestehen zusammen | MUST | E-004 | OFFEN |
| E-006 | Contentproduktion | validierte KI-/prozedurale Pipelines skalieren den freigegebenen Umfang | MUST | E-005 | OFFEN |

## Umsetzungseinheiten

| ID | Epic | Ergebnis | Verknüpfte Anforderungen | Größe | Priorität | Status |
|---|---|---|---|---|---|---|
| T-001 | E-001 | lokales F#-Harness mit Run-Ledger, Hashkette, BM25-RAG, Zitaten und Integritätsprüfung | Z-004, NF-008 | M | MUST | DONE |
| T-002 | E-001 | Memory-Promotion, Konflikt-/Stalenessprüfung und Retrieval-Traces | Z-004, NF-003 | M | MUST | READY |
| T-003 | E-001 | Clean-Room-, Asset-Provenienz-, Quarantäne- und technische Validatorhülle | Z-004, Z-005 | M | MUST | DRAFT |
| T-004 | E-001 | vollständige Run-Provenienz, Evidenzzuordnung, Trace-/Span-Felder, RAG-Buildmanifest und sichere Retention | Z-004, NF-003, NF-008 | M | MUST | DRAFT |
| T-010 | E-002 | SDL3-Fenster, Input und bgfx-Dreieck auf allen Ziel-RIDs | Z-002, Z-003 | L | MUST | DRAFT |
| T-011 | E-002 | plattformspezifische Shader-/Native-Buildmatrix und Smoke-Artefakte | Z-003 | L | MUST | DRAFT |
| T-020 | E-003 | leere Benchmarkszene mit Telemetrie auf allen Hardwareprofilen | Z-002 | M | MUST | DRAFT |
| T-021 | E-003 | headless feste Simulation mit 250 mobilen Testagenten | Z-002 | L | MUST | DRAFT |
| T-022 | E-003 | deterministischer 8-Stunden-Replay-Soak weist Stabilität und begrenztes Speicherwachstum nach | Z-002, NF-002 | M | MUST | DRAFT |
| T-030 | E-004 | erste vollständige Graybox-Schleife von Erkundung bis Basiskampf | Z-001 | XL | MUST | DRAFT |
| T-031 | E-004 | versioniertes atomares Save/Load besteht Roundtrip-, Abbruch-, Korruptions- und Wiederherstellungsfixtures | Z-001, F-005, NF-002 | L | MUST | DRAFT |
| T-040 | E-005 | repräsentative Riftward-Mission besteht Atmosphären-, Originalitäts- und visuelles Lesbarkeitsgate | Z-001, Z-005 | XL | MUST | DRAFT |
| T-041 | E-005 | finale UI-, Eingabe-, Untertitel- und Einstellungsabnahme auf allen Zielplattformen | Z-002, Z-003 | L | MUST | DRAFT |
| T-050 | E-006 | eine validierte KI-/prozedurale Assetfamilie durchläuft Quarantäne, Review, LFS-Quelle und Cooking reproduzierbar | Z-004, Z-005 | L | MUST | DRAFT |
| T-051 | E-006 | gemessene Karten-/Quest-/Audio-Pipeline erzeugt konsistente Inhalte mit vollständiger Provenienz | Z-001, Z-004, Z-005 | XL | MUST | DRAFT |

## Vorlage für eine Umsetzungseinheit

### T-XXX – Kurzer ergebnisorientierter Titel

- **Status:** OFFEN
- **Zweck:** Warum wird das gebraucht?
- **Ergebnis:** Welcher beobachtbare Zustand soll entstehen?
- **Enthalten:**
- **Nicht enthalten:**
- **Abhängigkeiten:**
- **Betroffene Anforderungen:**
- **Abnahmekriterien:**
  - [ ] Konkretes, von außen prüfbares Ergebnis
- **Erforderliche Tests:**
- **Dokumentation:**
- **Offene Punkte:**
