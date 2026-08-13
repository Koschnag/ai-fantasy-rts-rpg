# Prüfnachweis T-002: Memory und Retrieval-Traces

- Run-ID: `01KZXP5JJ5HS85W7RTZ8Z7R16K`
- Review-Fix-Run-ID: `01KZXR7WZB2GGMED2NRPGSJG5J`
- Aufgabe: `T-002`
- Status: `ACCEPTED`
- Abnahme: unabhängiger Reviewlauf; alle fünf Abnahmekriterien akzeptiert
- Ausgangs-Commit: `f390b20`; End-Commit wird beim gemeinsamen Abschlusscommit nachgetragen

## Ergebnis

Das dependency-freie F#-Harness besitzt einen expliziten append-only Memory-Lebenszyklus und eine getrennte hashverkettete Retrieval-Historie je neuem Run. Normales Retrieval nimmt ausschließlich effektiv angenommene, quellenfrische, nicht abgelaufene und konfliktfreie Memory-Records auf. Stale Quellen und Konfliktgruppen werden sichtbar gemeldet, ohne Neuheit als Wahrheit zu behandeln.

Memory- und Retrieval-Eingaben werden vor Persistierung mit der konfigurierten Schlüssel-/Wert-Policy redigiert und durch Größen- sowie Kontextlimits begrenzt. Alle Memory-Operationen verwenden dasselbe `rag.maxFileBytes`; unsichere Symlink-, Junction- oder ReparsePoint-Komponenten in Ledger- und Quellpfaden werden fail-closed abgelehnt. Neue Runs deklarieren Retrieval-Trace-Vertrag v2 im Manifest. Beim Abschluss verankern Summary und finales Event die Trace-Anzahl sowie den letzten Trace-Hash, sodass auch eine entfernte letzte Zeile oder vollständige Leerung erkannt wird.

## Nachweis je Abnahmekriterium

| Kriterium | Nachweis |
|---|---|
| `AC-T002-01` | Integrationstest erzeugt `proposed`, `accepted`, `superseded`, `stale` und widersprüchliche Records, ändert einen Quellenhash und prüft Retrieval-Ausschluss sowie `MEMORY_STALE`-/`MEMORY_CONFLICT`-Findings. Eine 5020-Byte-Quelle bei `maxFileBytes=4096` ist in Status und RAG konsistent stale. |
| `AC-T002-02` | CLI-Integrationstest führt `propose`, getrenntes `accept`, `supersede` und `set-status` aus; Eigenreview, unsichere Akteure, manipulierte Verkettung und veraltete Quellen werden abgelehnt. Ein konsumierter Vorschlag ist effektiv `superseded` und kann sequenziell kein zweites Mal angenommen werden; ein exklusiver Schreiblock verhindert parallele Mutation. |
| `AC-T002-03` | Die Golden Query wird zweimal aufgezeichnet. Query-/Index-/Konfigurationshash, BM25-Parameter, Treffer-IDs, Vertrauensklassen, Zitate und Kontext sind reproduzierbar. Ein abgeschlossener v2-Run verankert Trace-Anzahl und finalen Hash; Tests erkennen Tail-Kürzung und vollständige Leerung. |
| `AC-T002-04` | Bearer-, Private-Key-, Custom-Key- und Oversize-Fixtures prüfen Redaction und Limits. Separate Regressionen verhindern einen Memory-Write über externen Parent-Symlink und das Einlesen einer externen Source über Datei-Symlink. |
| `AC-T002-05` | `./scripts/rift.sh fresh-checkout-test` kopiert ausschließlich Checkout-Dateien in ein temporäres Verzeichnis, entfernt Runtime-Inhalt und führt Bootstrap, Build, Lint, Tests, RAG-Build und Verify erfolgreich aus. |

## Ausgeführte Qualitätsprüfungen

- `./scripts/rift.sh fmt`
- `./scripts/rift.sh lint`
- `./scripts/rift.sh build` — 0 Warnungen, 0 Fehler
- `./scripts/rift.sh test` — 15/15 Tests erfolgreich
- `./scripts/rift.sh security` — lokale Baseline erfolgreich
- `./scripts/rift.sh fresh-checkout-test` — `Fresh-Checkout-Gate: PASS`
- `./scripts/rift.sh rag-build`
- `./scripts/rift.sh verify`
- `git diff --check`, Shell-Syntaxprüfung und JSON-Parsing

## Bewusst verbleibende Grenzen

- `MEM-0003` ist durch einen inzwischen geänderten ADR-Quellenhash stale. Der Record bleibt revisionssicher erhalten, wird aber korrekt gemeldet und aus Retrieval ausgeschlossen; eine fachliche Ersetzung benötigt einen separaten Vorschlag und Review.
- Alte Runs ohne Trace-Vertrag beziehungsweise mit dem bisherigen v1-Vertrag bleiben aus Migrationsgründen lesbar. Jeder neu gestartete v2-Run verlangt `retrieval.jsonl` und einen Abschlussanker.
- Vollständige Run-/Prompt-/Modellprovenienz, kriterienspezifische Evidenz und Retention bleiben ausdrücklich T-004.
- Der unveränderliche Tail-Anker entsteht beim Run-Abschluss. Ein noch laufender v2-Run besitzt naturgemäß noch keinen Abschlussanker; seine interne Kette wird dennoch bei jeder Prüfung validiert.
