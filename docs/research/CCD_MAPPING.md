# Abbildung auf Cong-Driven Development

Diese Datei beschreibt eine Arbeitsabbildung, keine formale
Konformitätserklärung. Referenz ist das
[CCD-Repository](https://github.com/Koschnag/cong-driven-development). Die
Begriffe werden für Riftward konkretisiert und anhand ausführbarer Evidenz
weiter geprüft.

## Vom Signal zum Outcome

| CCD-/EIDOS-Schritt | Riftward-Artefakt | aktueller Nachweis | offene Lücke |
|---|---|---|---|
| Signal | Nutzerziel, Issue, Experimentfrage | Projekt- und Forschungsdokumente | externe Signale noch nicht strukturiert importiert |
| Lagebild | RAG-Index, Memory, Risiken, offene Fragen | T-001, T-002, T-004 | keine vollständige System-Twin-Sicht auf Runtime/Provider |
| Change Intent | Ergebnisbeschreibung und Akzeptanzkriterien | `BACKLOG.md`, `READY`-Regel | Produktziele noch überwiegend manuell freigegeben |
| Mission Order | versioniertes Taskmanifest | `.ai/tasks/*.json` | noch kein generischer Multi-Agent-Missionsplaner |
| Candidate | begrenzte Änderung in Git | kleine Commits und Scope-Regeln | langlebige Paralleländerungen brauchen weitere Isolation |
| Assurance | Build, Tests, Security, Performance, Review | `scripts/rift.sh`, CI, unabhängige Abnahmen | echte Rollen-/Providerunabhängigkeit nur teilweise belegt |
| Evidence | Run-Ledger, Trace, Hashkette, Assetmanifest | T-003 und T-004 | Kosten-/Request-/Energieanker fehlen teilweise |
| Sandbox | Fresh Checkout und Quarantäne | T-003 und T-007 | Runtime-Hardwarematrix beginnt erst mit T-010/T-011 |
| Outcome | akzeptiert, verworfen oder eskaliert | Abnahmedokumente und Taskstatus | Spiel- und Nutzeroutcomes existieren vor Vertical Slice nicht |

## SPOT im Repository

Der gegenwärtige Single Point of Truth ist noch kein einzelner Graphdienst,
sondern ein überprüfbarer Zusammenhang versionierter Dateien:

- **Specs:** `PROJEKT.md`, `docs/*.md`, `.ai/tasks/*.json`
- **Tests:** `tests/`, Akzeptanzkriterien und CI-Workflows
- **Risks:** `RISKS.md`, offene Fragen, Sicherheits- und IP-Regeln
- **Decisions:** `docs/entscheidungen/`
- **Knowledge:** kuratierte Memory-Einträge und reproduzierbarer BM25-RAG-Index
- **Tools:** `toolchain.lock.json`, Skripte, Generator- und Inspectorcode
- **Evidence:** `.ai`-Runverträge, Receipts, Manifeste und Abnahmedokumente

Das Forschungsziel ist zu messen, ob diese Beziehungen die Konvergenz
verbessern. Mehr Dokumente allein gelten nicht als Erfolg.

## Autonomiegrenze

Agenten dürfen innerhalb eines `READY`-Auftrags selbst planen, implementieren,
testen, reparieren und Evidenz erzeugen. Sie dürfen offene Weltentscheidungen,
Lizenzen, Hardwarezieländerungen oder Freigaben nicht durch selbstbewusstes
Raten ersetzen. Mehr Autonomie entsteht durch stärkere Orakel und kleinere
reversible Missionsräume, nicht durch das Entfernen aller Grenzen.

## Forschungsfähige Weiterentwicklung

Die nächste CCD-Ausbaustufe soll:

1. Task, Run, Retrieval, Commit, Gate, Asset und Outcome als gemeinsam
   abfragbaren Evidenzgraph exportieren,
2. menschliche Eingriffe und Agent-Wartezeit explizit unterscheiden,
3. Assurance möglichst rollen- und laufunabhängig ausführen,
4. Kosten-/Requestanker aufnehmen, ohne Providertelemetrie zu erfinden,
5. Nullresultate und abgebrochene Missionen gleichwertig archivieren.
