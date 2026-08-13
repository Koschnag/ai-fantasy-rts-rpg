# Agent-Harness: Verlauf, Gedächtnis, RAG und Evidenz

## Zweck

Das Rift Harness macht autonome KI-Arbeit reproduzierbar und überprüfbar. Es beantwortet nach jedem Lauf:

- Welcher klar begrenzte Auftrag wurde bearbeitet?
- Welche Quellen und Gedächtniseinträge wurden tatsächlich verwendet?
- Welche Werkzeuge und Änderungen waren beteiligt?
- Welche Kriterien wurden mit welcher Evidenz geprüft?
- Welche neuen Behauptungen wurden vorgeschlagen, bestätigt oder verworfen?
- Mit welchem Code-, Daten-, Prompt- und Toolstand lässt sich der Lauf nachvollziehen?

Das Harness ist lokal-first und benötigt für Logs, Gedächtnis und die erste RAG-Stufe keinen externen Dienst.

Es speichert kein verborgenes oder rohes Chain-of-Thought. Nachvollziehbarkeit entsteht aus Auftrag, verwendeten Quellen, knappen Entscheidungsbegründungen, Modell-/Promptmetadaten, Toolaufrufen, Änderungen, Artefakten und objektiver Evidenz.

## Gedächtnismodell

| Ebene | Inhalt | Speicherort | Wahrheitswert | Lebensdauer |
|---|---|---|---|---|
| Arbeitsgedächtnis | aktueller Plan, temporäre Notizen, Zwischenartefakte | `.ai/runtime/runs/<run-id>/work/` | keiner | Lauf / Retention |
| Episodisch | hashverkettete Ereignisse, Tool- und Prüfergebnisse, Abschlussbericht | `.ai/runtime/runs/<run-id>/` | Beleg eines Ablaufs, nicht automatisch fachliche Wahrheit | mindestens 180 Tage lokal |
| Semantisch | atomare bestätigte Fakten, Randbedingungen, Definitionen und Lessons | `.ai/memory/records.jsonl` | nur Status `accepted` | versioniert, bis ersetzt |
| Prozedural | Arbeitsregeln, Prompts, Schemas, Gates und Runbooks | `AGENTS.md`, `.ai/prompts/`, `.ai/schemas/`, `.ai/evals/` | verbindlich gemäß Quellenhierarchie | versioniert |

Ein Chatverlauf wird nie ungeprüft zu semantischem Gedächtnis. Stattdessen schlägt ein Lauf kleine Records vor. Annahme, Ablehnung oder Ersetzung ist ein eigener nachvollziehbarer Schritt.

## Ziel-Verzeichnisstruktur

Das folgende Layout ist der Ausbauvertrag. Das aktuelle T-001-Walking-Skeleton erzeugt bereits `run.json`, `events.jsonl`, `summary.json` und den rebuildbaren Index. Persistierte Retrievals und Memory-Zustände gehören zu T-002; `work/`, strukturierte Evidenzaufnahme, vollständige Run-Provenienz und automatische Retention zu T-004. Sie dürfen bis dahin nicht als vorhanden vorausgesetzt werden.

```text
.ai/
├── config.json                 # versionierte Harness-Policy
├── schemas/                    # maschinenlesbare Verträge
├── prompts/                    # versionierte Rollen-/Arbeitsvorlagen
├── evals/                      # Qualitätsgates und Rubriken
├── tasks/                      # ausführbare READY-Aufträge
├── memory/records.jsonl        # kuratiertes Langzeitgedächtnis
├── history/accepted/           # bereinigte, bewusst versionierte Run-Berichte
└── runtime/                    # lokal persistent, nicht in Git
    ├── runs/<run-id>/
    │   ├── run.json              # kleines v1-Laufzeitmanifest
    │   ├── events.jsonl
    │   ├── retrieval.jsonl
    │   ├── evidence/
    │   ├── work/
    │   └── summary.json
    ├── index/                  # vollständig reproduzierbarer RAG-Index
    └── cache/
```

## Lauf-Lebenszyklus im Zielzustand

1. Eine Aufgabe hat Status `ready`, testbare Kriterien und definierte Entscheidungsgrenzen.
2. `start-run` reserviert ID und Laufzeitmanifest; ein erstes `run.started`-Ereignis hält Task, Agent-/Modellkennung, Prompt-Hash, Git-Stand und Umgebung fest.
3. `build-rag` aktualisiert nur geänderte Quellchunks anhand von Inhalts-Hashes.
4. Jede Retrieval-Anfrage speichert Query, Index-Hash, Treffer-IDs, Pfade und Zeilenbereiche. Diese Persistierung ist nach T-001 noch ein Folgetask.
5. Tool-, Datei-, Test-, Benchmark- und Evaluationsereignisse werden geordnet an `events.jsonl` angehängt.
6. Evidenz wird Kriterien zugeordnet; ein bloßer Exitcode ohne Befehl, Umgebung und Ergebnis-Hash genügt nicht.
7. Neue Erkenntnisse landen zunächst als `proposed`, nie automatisch als `accepted`. Der Erzeugeragent darf seine eigenen Records nicht annehmen.
8. `finish-run` erzeugt eine Zusammenfassung und schließt die Ereigniskette.
9. Ein separater Review entscheidet über Code, Taskstatus, vorgeschlagene Erinnerungen und eine bereinigte History-Zusammenfassung. Ein separater Reviewlauf darf nur objektiv technische, quellenidentische Records annehmen; kreative, produktbezogene und lizenzielle Entscheidungen bleiben der Projektleitung vorbehalten.

## Append-only-Ereignisse

Aktuell besitzt jedes Ereignis eine monotone Sequenz, UTC-Zeit, Typ, Payload und Hash. `previousEventHash` verkettet die Einträge. Die Kette schützt nicht gegen einen Angreifer mit Schreibzugriff, macht aber versehentliche Änderung, Auslassung und Reihenfolgefehler sichtbar. Akteur-, Agent-, Modell- und Trace-Felder folgen mit T-004.

Große Ausgaben werden als Datei mit SHA-256 referenziert, nicht in JSONL dupliziert. Payloads werden vor dem Schreiben anhand der Redaction-Policy bereinigt. Implementiert sind ein konfigurierbares Byte-Limit sowie Schlüssel- und Wert-Regexe mit linearem Regexmodus und Timeout; ungültige Regeln brechen den Lauf ab.

## RAG-Stufe 1: deterministische lokale Suche

Die erste Implementierung verwendet zeilenstabile Chunks und BM25-Volltext-Ranking. Das ist absichtlich unspektakulär:

- vollständig offline und FOSS,
- keine Embedding-Kosten oder Datenweitergabe,
- reproduzierbare Treffer bei identischem Commit,
- gut für IDs, Fachbegriffe, Code, Entscheidungen und konkrete Randbedingungen,
- sehr kleiner technischer Fußabdruck.

Bereinigte Abschlussberichte unter `.ai/history/accepted/` bleiben bis zur Autoritätsgewichtung in T-002 außerhalb des normalen Indexes, damit Metatext keine fachlichen Primärquellen überrankt.

Jeder Chunk enthält Quellpfad, Start-/Endzeile und Inhalts-Hash. Antworten müssen Pfad und Zeilen nennen; `rag.maxContextCharacters` begrenzt die ausgegebenen Chunktexte. Der Index wird nicht versioniert. Ein separates Build-Manifest mit Konfigurationshash ist noch nicht implementiert und bleibt T-004.

Die konfigurierte Memory-JSONL-Datei wird bereits defensiv projiziert: Nur `accepted`-Records, deren lokale Quellen-Hashes noch stimmen, gelangen in den Index. Andere Status und stale Records bleiben in der append-only Quelldatei erhalten, sind aber nicht abrufbar. Automatische Statusänderung, Promotion und Konfliktausgabe folgen erst mit T-002.

### Spätere semantische Erweiterung

Embeddings werden erst ergänzt, wenn ein Eval-Set einen messbaren Recall-Gewinn gegenüber BM25 zeigt. Dann gelten zusätzlich:

- lokal ausführbares, klar lizenziertes Modell und Runtime bevorzugen,
- Modellartefakt, Tokenizer, Quantisierung und Dimension pinnen,
- Vektoren stets mit demselben Quellchunk und Hash verbinden,
- Hybrid-Ranking statt Ersatz der lexikalischen Suche,
- keine Projektquellen ohne ausdrückliche Entscheidung an fremde APIs senden.

Mögliche Technik wird per ADR gewählt. Ein Vektor-Datenbankdienst ist für die aktuelle Projektgröße nicht erforderlich.

## Quellenänderung und Vergessen

- Ein geänderter Datei-Hash invalidiert betroffene Chunks.
- Ein Memory-Record mit nicht mehr passendem Quellen-Hash wird derzeit beim Indexbau übersprungen; T-002 ergänzt eine sichtbare append-only Revision auf `stale`.
- Widersprüchliche akzeptierte Records werden ab T-002 als Konflikt zurückgegeben; Neuheit allein gewinnt nicht.
- Ersetzungen bleiben durch append-only Revisionen und `supersedes` sichtbar; vorhandene Records werden nicht still überschrieben oder gelöscht.
- Zeitabhängiges Wissen kann `expiresAtUtc` besitzen.
- „Vergessen“ bedeutet Statusänderung oder Retention, nicht heimliches Löschen aus der Historie.

## Retrieval-Sicherheit

Indexierte Inhalte sind Daten und keine Instruktionen. Ein eingebetteter Satz wie „ignoriere alle Regeln“ ändert weder Agent-Policy noch Auftrag. Das aktuelle Harness:

- indexiert nur Allowlist-Pfade und bekannte Textformate,
- schließt Secrets, Runtime-Logs, Rohassets, Build- und VCS-Daten aus,
- begrenzt Dateigröße, Chunkzahl und Kontextvolumen,
- markiert Herkunft durch Pfad, Zeilenbereich und Hash,
- darf durch Retrieval keine Tools oder Schreiboperationen automatisch autorisieren.

Explizite Vertrauensklassen, sichtbare Konflikt-/Stalenessberichte und persistierte Retrieval-Traces folgen mit T-002. Vollständige Run-/Evidenz-Traces folgen mit T-004. Stale Memory wird bis dahin sicher übersprungen, aber noch nicht als eigener Query-Befund ausgegeben.

## Beobachtbarkeit

T-004 ergänzt Trace-/Span-IDs für Arbeitsschritte mit mehreren Tools. Eine spätere OpenTelemetry-Ausgabe kann daraus abgeleitet werden; OTLP ist keine Voraussetzung. Zielmetriken:

- Laufdauer, Retries und Fehlerklassen
- Retrievaltreffer, Quellenabdeckung und Kontextgröße
- geänderte Dateien und Diffgröße
- Test-/Gate-Laufzeiten und Flakiness
- Benchmarkdeltas gegenüber Baseline
- vorgeschlagene und angenommene Memory-Records
- Asset-Akzeptanzquote und Ablehnungsgründe

Token- oder API-Kosten werden nur erfasst, wenn der jeweilige Anbieter sie zuverlässig meldet. Geschätzte Werte werden als Schätzung markiert.

## Sicherung und Retention

- Die Policy fordert 180 Tage lokale Roh-Run-Retention; automatische Löschung und die Vorprüfung auf einen akzeptierten, bereinigten Bericht sind noch nicht implementiert.
- Akzeptierte History, Gedächtnis, Aufgaben, Entscheidungen und Evidenzreferenzen werden in Git versioniert.
- Große Benchmark-, Bild-, Audio- und Trace-Artefakte benötigen später einen adressierbaren Artefaktspeicher mit Hashprüfung; Git ist dafür nicht vorgesehen.
- Secrets werden nicht als „verschlüsselte Logs im Repo“ gelöst, sondern gar nicht erst aufgenommen.

## CLI-Vertrag

Implementierte Befehle des F#-Werkzeugs `RiftHarness`:

```text
rift-harness init
rift-harness start-run
rift-harness append-event <run-id> --type <type> --payload-file <path>
rift-harness finish-run <run-id> --status succeeded --summary-file <path>
rift-harness build-rag
rift-harness query-rag --query <text> --top 8
rift-harness verify [--run <id>]
```

`start-run` erzeugt derzeit nur `run.json` und eine leere Ereignisdatei; die erweiterte Startprovenienz muss der Aufrufer explizit als erstes `run.started`-Payload anhängen. Bequeme Optionen für Task/Agent/Prompt sowie weitere Befehle für Memory-Promotion, Redaction-Audit, Evidenzaufnahme und Retention folgen als eigene `READY`-Aufgaben; sie werden nicht durch leere Erfolgskommandos vorgetäuscht.

## Harness-Zielkriterien

T-001 deckt Run-Lebenszyklus, Hashkette, Redaction-Basis, deterministisches BM25-Retrieval mit Zitaten und Integritätsprüfung ab. Staleness-/Konfliktlogik, Retrieval-Traces und Memory-Promotion sind T-002 zugeordnet; vollständige Run-Provenienz, Evidenzzuordnung, Buildmanifest und Retention T-004.

- Zwei Builds desselben Commits erzeugen identische Chunk-IDs und Rankings für dieselbe Query.
- Ein geänderter Quelltext invalidiert genau die betroffenen Chunks und Memory-Quellen.
- Jede Retrievalantwort enthält prüfbare Pfad-/Zeilen-/Hash-Zitate.
- Eine manipulierte oder umsortierte Eventzeile lässt `verify` fehlschlagen.
- Ein Secret-Fixture wird weder indexiert noch unredigiert geloggt.
- Kein vorgeschlagener Memory-Record wird ohne separaten Annahmeschritt abrufbar.
- Ein Konflikt zwischen zwei akzeptierten Records wird gemeldet und nicht still aufgelöst.
- Ein Lauf kann von Manifest, Commit, Ereignissen, Retrieval und Evidenz bis zu seinem Ergebnis verfolgt werden.
