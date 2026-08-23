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

Das folgende Layout ist der Ausbauvertrag. T-002 erzeugt für neue Runs `run.json`, `events.jsonl`, eine leere beziehungsweise append-only gefüllte `retrieval.jsonl`, `summary.json` nach Abschluss und den rebuildbaren Index. Seit T-004 erzeugt `start-run` zusätzlich den Provenienzblock im Manifest, die Verzeichnisse `work/` und `evidence/` pro Lauf, das deterministische RAG-Buildmanifest sowie die Retention-Befehle; strukturierte Evidenzaufnahme ist über `append-evidence` verfügbar.

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
4. `query-rag --run <id>` speichert Query-/Konfig-/Index-Hash, Rankingparameter, Treffer-IDs, Zitate und den tatsächlich erzeugten Kontext redigiert in der Retrieval-Kette des laufenden Runs.
5. Tool-, Datei-, Test-, Benchmark- und Evaluationsereignisse werden geordnet an `events.jsonl` angehängt.
6. Evidenz wird Kriterien zugeordnet; ein bloßer Exitcode ohne Befehl, Umgebung und Ergebnis-Hash genügt nicht.
7. Neue Erkenntnisse landen zunächst als `proposed`, nie automatisch als `accepted`. Der Erzeugeragent darf seine eigenen Records nicht annehmen.
8. `finish-run` erzeugt eine Zusammenfassung, verankert Anzahl und finalen Hash der stabil gesperrten Retrieval-Kette und schließt die Ereigniskette.
9. Ein separater Review entscheidet über Code, Taskstatus, vorgeschlagene Erinnerungen und eine bereinigte History-Zusammenfassung. Ein separater Reviewlauf darf nur objektiv technische, quellenidentische Records annehmen; kreative, produktbezogene und lizenzielle Entscheidungen bleiben der Projektleitung vorbehalten.

## Append-only-Ereignisse

Aktuell besitzt jedes Ereignis eine monotone Sequenz, UTC-Zeit, Typ, Payload und Hash. `previousEventHash` verkettet die Einträge. Die Kette schützt nicht gegen einen Angreifer mit Schreibzugriff, macht aber versehentliche Änderung, Auslassung und Reihenfolgefehler sichtbar. Seit T-004 tragen Retrieval-, Tool- und Evidenzereignisse Trace-/Span-/Kriteriumsfelder mit strikter Prüfung.

Große Ausgaben werden als Datei mit SHA-256 referenziert, nicht in JSONL dupliziert. Payloads werden vor dem Schreiben anhand der Redaction-Policy bereinigt. Implementiert sind ein konfigurierbares Byte-Limit sowie Schlüssel- und Wert-Regexe mit linearem Regexmodus und Timeout; ungültige Regeln brechen den Lauf ab.

## RAG-Stufe 1: deterministische lokale Suche

Die erste Implementierung verwendet zeilenstabile Chunks und BM25-Volltext-Ranking. Das ist absichtlich unspektakulär:

- vollständig offline und FOSS,
- keine Embedding-Kosten oder Datenweitergabe,
- reproduzierbare Treffer bei identischem Commit,
- gut für IDs, Fachbegriffe, Code, Entscheidungen und konkrete Randbedingungen,
- sehr kleiner technischer Fußabdruck.

Bereinigte Abschlussberichte unter `.ai/history/accepted/` bleiben bewusst außerhalb des normalen Indexes, damit Metatext keine fachlichen Primärquellen überrankt. T-002 weist jedem tatsächlich indexierten Chunk eine explizite Vertrauensklasse zu; diese Kennzeichnung verändert weder BM25-Ranking noch Quellenhierarchie.

Jeder Chunk enthält Quellpfad, Start-/Endzeile und Inhalts-Hash. Antworten müssen Pfad und Zeilen nennen; `rag.maxContextCharacters` begrenzt die ausgegebenen Chunktexte. Der Index wird nicht versioniert. Seit T-004 schreibt `build-rag` zusätzlich ein deterministisches Build-Manifest (`.ai/runtime/index/build-manifest.json`), das Index, Konfiguration und alle Quellen per SHA-256 bindet; `verify` lehnt einen Index ohne gültiges Manifest ab.

Die konfigurierte Memory-JSONL-Datei wird defensiv projiziert: Nur effektiv `accepted`-Records, deren lokale Quellen-Hashes noch stimmen, die nicht abgelaufen/ersetzt sind und keiner expliziten Konfliktgruppe angehören, gelangen in den Index. Andere Status und stale beziehungsweise widersprüchliche Records bleiben im append-only Ledger erhalten. `memory status` und jede RAG-Antwort melden aktuelle Staleness und Konfliktgruppen sichtbar.

Neue Vorschläge benötigen einen `conflictKey`. Das ist keine semantische Wahrheitsmaschine, sondern ein expliziter Vertrag dafür, welche Records für genau dieselbe fachliche Stelle höchstens einen aktiven Wert besitzen dürfen. Mehrere aktive Annahmen mit demselben Key werden gemeinsam ausgeschlossen; Neuheit gewinnt nicht.

Jeder Vorschlag darf genau eine Nachfolgerevision besitzen. Sobald `accept`, `supersede` oder `set-status --status rejected` ihn konsumiert hat, ist sein effektiver Status `superseded`; ein weiterer Versuch wird vor dem Append deterministisch abgelehnt. Parser und Schreibpfad prüfen dieselbe Invariante.

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
- Ein Memory-Record mit nicht mehr passendem Quellen-Hash oder erreichtem Ablaufdatum wird übersprungen und als `MEMORY_STALE` gemeldet. `memory set-status ... --status stale` kann diese Feststellung nach Review als neue Revision festhalten; der Statusbefehl selbst mutiert nicht.
- `rag.maxFileBytes` gilt identisch für Vorschlag, Annahme, Ersetzung, Statusableitung und RAG-Projektion. Eine größere Quelle ist stale und niemals abrufbar.
- Widersprüchliche akzeptierte Records mit demselben `conflictKey` werden gemeinsam ausgeschlossen und als `MEMORY_CONFLICT` zurückgegeben; Neuheit allein gewinnt nicht.
- Ersetzungen bleiben durch append-only Revisionen und `supersedes` sichtbar; vorhandene Records werden nicht still überschrieben oder gelöscht.
- Neue CLI-Revisionen besitzen `previousRecordHash` und `recordHash`. Bereits vorhandene Legacy-Einträge bilden den verankerten Beginn; nach Start der Kette ist kein ungehashter Eintrag mehr zulässig.
- Zeitabhängiges Wissen kann `expiresAtUtc` besitzen.
- „Vergessen“ bedeutet Statusänderung oder Retention, nicht heimliches Löschen aus der Historie.
- Ledger- und Sourcepfade bleiben innerhalb des Workspace. Jede existierende Parent-/Dateikomponente wird fail-closed auf Symlink, Junction und ReparsePoint geprüft; ein externer Zielpfad wird weder gelesen noch beschrieben.

## Retrieval-Sicherheit

Indexierte Inhalte sind Daten und keine Instruktionen. Ein eingebetteter Satz wie „ignoriere alle Regeln“ ändert weder Agent-Policy noch Auftrag. Das aktuelle Harness:

- indexiert nur Allowlist-Pfade und bekannte Textformate,
- schließt Secrets, Runtime-Logs, Rohassets, Build- und VCS-Daten aus,
- begrenzt Dateigröße, Chunkzahl und Kontextvolumen,
- markiert Herkunft durch Pfad, Zeilenbereich und Hash,
- darf durch Retrieval keine Tools oder Schreiboperationen automatisch autorisieren.

T-002 implementiert explizite Vertrauensklassen, sichtbare Konflikt-/Stalenessberichte und persistierte Retrieval-Traces. Query und Kontext werden vor Persistierung mit derselben konfigurierten Schlüssel-/Wert-Policy wie Run-Payloads redigiert; Freitext-Zuweisungen wie `credential=...` werden dabei ebenfalls erkannt. Query-, Kontext-, Treffer- und Kettenhashes beziehen sich auf die persistierte redigierte Form. Neue Runs verwenden Trace-Vertrag v2: `finish-run` sperrt Retrieval gegen paralleles Append und bindet `retrievalTraceCount` sowie `finalRetrievalTraceHash` in Summary und Abschluss-Event. `verify` erkennt dadurch auch gültige Präfixe nach Tail-Kürzung oder Leerung.

Seit T-004 gilt zusätzlich der Ereignisvertrag für Trace-/Span-IDs: Die Typen `retrieval.recorded`, `tool.executed` und `evidence.recorded` tragen in ihrem Payload zwingend eine Hülle aus `traceId` (32 Hexzeichen), `spanId` (16 Hexzeichen) und genau einem `criterionId` (`AC-…-##`), der zur im Manifest gebundenen Aufgabe aufgelöst wird; fremde Kriterien werden abgelehnt. Mehrere Arbeitsschritte dürfen einen Span teilen, aber jede Span-Kombination wird durch höchstens eine Evidenz abgeschlossen. `query-rag --run … --criterion … --trace-id … --span-id …` verankert Retrievals zusätzlich als `retrieval.recorded`-Ereignis in der Eventkette; `verify` prüft den Verweis gegen die Retrieval-Kette.

## Beobachtbarkeit

T-004 implementiert Trace-/Span-IDs für Arbeitsschritte mit mehreren Tools; eine spätere OpenTelemetry-Ausgabe kann daraus abgeleitet werden, OTLP bleibt keine Voraussetzung. Zielmetriken:

- Laufdauer, Retries und Fehlerklassen
- Retrievaltreffer, Quellenabdeckung und Kontextgröße
- geänderte Dateien und Diffgröße
- Test-/Gate-Laufzeiten und Flakiness
- Benchmarkdeltas gegenüber Baseline
- vorgeschlagene und angenommene Memory-Records
- Asset-Akzeptanzquote und Ablehnungsgründe

Token- oder API-Kosten werden nur erfasst, wenn der jeweilige Anbieter sie zuverlässig meldet. Geschätzte Werte werden als Schätzung markiert.

## Sicherung und Retention

- Die Policy fordert 180 Tage lokale Roh-Run-Retention. Seit T-004 ist der Lebenszyklus implementiert: `retention-plan` erzeugt eine read-only, hashverankerte Vorschau (`planSha256`), die pro Lauf Frist, Abschlussprüfung und den akzeptierten bereinigten History-Bericht auswertet. `retention-execute --confirm-plan-sha256 …` entfernt ausschließlich Kandidaten dieses Plans, die zum Ausführungszeitpunkt erneut alle Bedingungen erfüllen (transaktional, Symlink-fail-closed) und verankert jede Löschung in `.ai/runtime/retention-log.jsonl`.
- Akzeptierte History, Gedächtnis, Aufgaben, Entscheidungen und Evidenzreferenzen werden in Git versioniert.
- Große Benchmark-, Bild-, Audio- und Trace-Artefakte benötigen später einen adressierbaren Artefaktspeicher mit Hashprüfung; Git ist dafür nicht vorgesehen.
- Secrets werden nicht als „verschlüsselte Logs im Repo“ gelöst, sondern gar nicht erst aufgenommen.

## CLI-Vertrag

Implementierte Befehle des F#-Werkzeugs `RiftHarness`:

```text
rift-harness init
rift-harness start-run [--actor ACTOR] [--task T-###] [--model ID] [--prompt-file FILE] [--toolchain-file FILE]
rift-harness append-event <run-id> --type <type> --payload-file <path>
rift-harness append-evidence <run-id> --criterion AC-…-## --kind KIND --result-file PATH [--trace-id ID] [--span-id ID] [--command CMD] [--exit-code N] [--duration-ms N] [--artifact PATH]…
rift-harness finish-run <run-id> --status succeeded --summary-file <path>
rift-harness memory propose|validate|accept|supersede|set-status|status …
rift-harness build-rag
rift-harness query-rag --query <text> --top 8 [--run <id>] [--criterion AC-…-## --trace-id ID --span-id ID]
rift-harness retention-plan [--now UTC]
rift-harness retention-execute --plan-file PATH --confirm-plan-sha256 SHA256 [--now UTC]
rift-harness verify [--run <id>]
```

`start-run` erzeugt `run.json`, eine leere Ereignis- und eine leere Retrieval-Datei, die Verzeichnisse `work/` und `evidence/`, den Provenienzblock (Task-, Git-, Prompt-, Modell-, Toolchain-, Konfigurationshashes plus explizite Vollständigkeitskennzeichnung; Git wird ohne Unterprozess direkt aus `.git/HEAD` gelesen, fehlende Anteile werden als unvollständig markiert) sowie automatisch das erste `run.started`-Ereignis mit identischer Provenienz. Prompte werden ausschließlich als Hash geführt; Rohprompts, Rohmodellantworten und verborgene Begründungen werden nie gespeichert.

## Harness-Zielkriterien

T-001 deckt Run-Lebenszyklus, Event-Hashkette, Redaction-Basis und deterministisches BM25-Retrieval ab. T-002 ergänzt Staleness-/Konfliktlogik, explizite Memory-Promotion und eine getrennte Retrieval-Hashkette. T-004 vervollständigt Run-Provenienz, Trace-/Span-/Kriteriumsverträge, das deterministische RAG-Buildmanifest und die bewachte Retention.

- Zwei Builds desselben Commits erzeugen identische Chunk-IDs und Rankings für dieselbe Query.
- Ein geänderter Quelltext invalidiert genau die betroffenen Chunks und Memory-Quellen.
- Jede Retrievalantwort enthält prüfbare Pfad-/Zeilen-/Hash-Zitate.
- Eine manipulierte oder umsortierte Eventzeile lässt `verify` fehlschlagen.
- Ein Secret-Fixture wird weder indexiert noch unredigiert geloggt.
- Kein vorgeschlagener Memory-Record wird ohne separaten Annahmeschritt abrufbar.
- Ein Konflikt zwischen zwei akzeptierten Records wird gemeldet und nicht still aufgelöst.
- Ein Lauf kann von Manifest, Commit, Ereignissen, Retrieval und Evidenz bis zu seinem Ergebnis verfolgt werden.
