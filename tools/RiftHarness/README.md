# RiftHarness

Kleines, lokales F#/.NET-10-Harness fuer nachvollziehbare KI-Laeufe und eine
dependency-freie BM25-Suche. Es verwendet ausschliesslich die .NET-BCL und
benoetigt im Betrieb kein Netzwerk.

## Zustandslayout

`init` legt relativ zum Workspace folgendes an (vorhandene Konfigurationen
werden nicht ueberschrieben):

```text
.ai/
  config.json
  runtime/
    runs/<26-stellige-run-id>/
      run.json
      events.jsonl
      summary.json       # nach finish-run
    index/bm25.json
```

Die Quellmuster, feste Chunkgroesse und Ueberlappung stehen in
`.ai/config.json`. Muster unterstuetzen `*`, `?` und `**`; absolute Pfade,
`..` sowie Symlink-Verzeichnisse werden nicht gelesen. Der Index enthaelt den
normalisierten Text, Termfrequenzen, Dokumentfrequenzen sowie SHA-256-Hashes
von Quelle und Chunk. Identische Eingaben erzeugen byte-identische Indizes.
`rag.maxContextCharacters` begrenzt die Summe der ausgegebenen Chunktexte;
der letzte Treffer kann dafuer am Zeichenlimit gekuerzt werden.

Die konfigurierte Memory-JSONL-Datei ist eine Sonderquelle: Nur Records mit
`status: accepted`, deren lokale Quellen alle vorhanden und noch hashgleich sind,
werden indexiert. `proposed`, `rejected` und andere Status sowie accepted
Records mit fehlender/geaenderter Quelle werden als Leerzeile projiziert. Ihre
Originaldatei bleibt unveraendert; der Index ist nie die Wahrheit.

## Befehle

```bash
dotnet run --project tools/RiftHarness -- init
RUN_ID="$(dotnet run --project tools/RiftHarness -- start-run)"
dotnet run --project tools/RiftHarness -- append-event "$RUN_ID" \
  --type agent.step --payload-file /tmp/event.json
dotnet run --project tools/RiftHarness -- finish-run "$RUN_ID" \
  --status succeeded --summary-file /tmp/summary.json
dotnet run --project tools/RiftHarness -- build-rag
dotnet run --project tools/RiftHarness -- query-rag --query "performance budget" --top 5
dotnet run --project tools/RiftHarness -- verify
```

`query-rag` akzeptiert aus Kompatibilitaetsgruenden auch eine positionale Query.
Eine feste, versionierte Liste allgemeiner deutscher und englischer Stopwoerter
verhindert, dass Fragewoerter das BM25-Ranking dominieren. `--workspace PATH`
ist fuer jeden Befehl verfuegbar. `verify --run ID` prueft
nur einen Lauf. `start-run` schreibt ausschliesslich die sortierbare Run-ID auf
stdout; die anderen Befehle liefern JSON.

Payload und Abschlussnotiz werden absichtlich nur ueber Dateien angenommen,
damit Inhalte nicht in Shell-History und Prozessliste gelangen. Vor dem
Speichern ersetzt das Harness Werte gaengiger Geheimnisfelder sowie Treffer
aus `security.redactKeyPatterns` und `security.redactValuePatterns` rekursiv
durch `[REDACTED]`. Die Regexe laufen case-insensitiv, mit linearem
`NonBacktracking`-Verhalten und 100-ms-Timeout; ungueltige oder dort nicht
unterstuetzte Muster brechen beim Laden der Konfiguration ab.
`logging.maxEventPayloadBytes` begrenzt Payload und Abschlussnotiz bereits vor
dem Parsen und ist aus Ressourcenschutz auf 16 MiB gedeckelt. Das sind
Schutzschichten, kein vollstaendiger Secret-Scanner:
Zugangsdaten gehoeren grundsaetzlich nicht in Agent-Payloads.

Harness v1 verwendet feste Zustands-/History-/Task-Pfade sowie zwingend
JSONL, UTC und Hashkette. Abweichende Werte brechen beim Konfigurationsladen
ab, statt still ignoriert zu werden. `paths.memory`, RAG- und
Redaction-Einstellungen sind wirksam konfigurierbar. Die beiden Retentionwerte
werden bis T-004 nur als deklarative Ziel-Policy validiert (`180` Tage
Roh-Runs, akzeptierte Zusammenfassungen ohne automatische Loeschfrist); eine
automatische Retention ist noch nicht implementiert.
Auch die Wahrheitshierarchie und die Governance-Schalter (Unklarheiten
explizit halten, Retrieval als nicht vertrauenswuerdige Daten behandeln, keine
automatische Memory-Annahme) sind feste v1-Invarianten; eine abweichende
Konfiguration erfordert eine neue Harness-/Schema-Version.

Jedes JSONL-Ereignis besitzt Sequenz, UTC-Zeit, Schema-Version,
`previousEventHash` und einen SHA-256-`eventHash` ueber die kanonischen
relevanten Felder. `verify` prueft diese Kette, Abschlusszusammenfassungen,
Indexhash, Quellen/Chunks und die grundlegenden Schema-Invarianten.

## Tests

Die Tests sind ebenfalls dependency-frei und laufen als Konsolenprogramm:

```bash
dotnet run --project tests/RiftHarness.Tests
```

## Frischer Checkout

Im Projektroot fuehrt `./scripts/rift.sh bootstrap` die gepinnte
.NET-Installation, Tool-/NuGet-Restore, Release-Build, idempotentes `init` und
den ersten RAG-Build aus. Danach funktionieren `rag-query` und `verify` ohne
implizite, versteckte Restore-Schritte. Direkte CLI-Aufrufe setzen weiterhin
`init`, einen Build und fuer Abfragen einen vorhandenen aktuellen Index voraus.

## Noch nicht implementiert

Memory-Promotion, Revisionen, Konfliktberichte und explizite
Staleness-Uebergaenge sind Gegenstand von T-002. Der Erzeugeragent darf einen
Record dabei nie selbst annehmen. Ein separater Reviewlauf darf spaeter nur
objektiv technische, quellenidentische Records freigeben; kreative,
produktbezogene und lizenzielle Entscheidungen bleiben der Projektleitung
vorbehalten. T-002 ergaenzt außerdem persistierte Retrieval-Ereignisse; T-004
erweitert die vollstaendige Run-/Prompt-/Modellprovenienz, Evidenzzuordnung und
Retention. Bis dahin filtert der Index vorhandene Records defensiv, promotet,
aendert oder loescht sie aber nicht.
