# RiftHarness

Kleines, lokales F#/.NET-10-Harness fuer nachvollziehbare KI-Laeufe, eine
BCL-basierte BM25-Suche und ein offline arbeitendes Asset-Provenienzgate. Der
Asset-Schemadapter verwendet die exakt gelockte MIT-Bibliothek
`JsonSchema.Net` 8.0.5 mit drei verwalteten Transitiven; der restliche Harness
bleibt BCL-basiert. Es benoetigt im Betrieb kein Netzwerk.

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
      retrieval.jsonl     # leer oder redigierte, hashverkettete RAG-Abfragen
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

Die konfigurierte Memory-JSONL-Datei ist eine Sonderquelle: Nur effektiv
`accepted`, quellenfrische, nicht abgelaufene, nicht ersetzte und konfliktfreie
Records werden indexiert. Andere Records werden als Leerzeile projiziert und
durch `memory status` beziehungsweise `memoryFindings` sichtbar. Neue
Vorschlaege benennen ihre fachliche Eindeutigkeitsstelle mit `conflictKey` und
koennen genau einmal konsumiert werden. `rag.maxFileBytes` gilt auch fuer ihre
Quellen. Ledger- und Sourcepfade mit Symlink-, Junction- oder
ReparsePoint-Komponenten werden fail-closed abgelehnt.

## Befehle

```bash
dotnet run --project tools/RiftHarness -- init
RUN_ID="$(dotnet run --project tools/RiftHarness -- start-run)"
dotnet run --project tools/RiftHarness -- append-event "$RUN_ID" \
  --type agent.step --payload-file /tmp/event.json
dotnet run --project tools/RiftHarness -- finish-run "$RUN_ID" \
  --status succeeded --summary-file /tmp/summary.json
dotnet run --project tools/RiftHarness -- memory propose --record-file /tmp/memory.json
dotnet run --project tools/RiftHarness -- memory validate
dotnet run --project tools/RiftHarness -- memory accept MEM-1000 \
  --new-id MEM-1001 --actor independent-reviewer
dotnet run --project tools/RiftHarness -- memory status
dotnet run --project tools/RiftHarness -- build-rag
dotnet run --project tools/RiftHarness -- query-rag \
  --query "performance budget" --top 5 --run "$RUN_ID"
dotnet run --project tools/RiftHarness -- assets-check
dotnet run --project tools/RiftHarness -- assets-check \
  --require-local --require-approved
dotnet run --project tools/RiftHarness -- blender-calibration validate-spec \
  --spec assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json
dotnet run --project tools/RiftHarness -- blender-calibration inspect \
  --spec assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json \
  --glb tests/Fixtures/Asset3d/positive/family.glb \
  --preview tests/Fixtures/Asset3d/positive/preview.png \
  --report tests/Fixtures/Asset3d/positive/technique.json
dotnet run --project tools/RiftHarness -- verify
```

Die `blender-calibration`-Pfade im `inspect`-Beispiel sind nur die erlaubten
logischen Fixturepfade; die Tests erzeugen die synthetischen Bytes in einem
temporären Workspace. T-005 startet Blender nie und checkt keine GLB-/PNG-
Binärfixtures ein. Der echte Generator folgt separat in T-006.

`query-rag` akzeptiert aus Kompatibilitaetsgruenden auch eine positionale Query.
Eine feste, versionierte Liste allgemeiner deutscher und englischer Stopwoerter
verhindert, dass Fragewoerter das BM25-Ranking dominieren. `--workspace PATH`
ist fuer jeden Befehl verfuegbar. `verify --run ID` prueft
nur einen Lauf. `start-run` schreibt ausschliesslich die sortierbare Run-ID auf
stdout; die anderen Befehle liefern JSON.

Payload, Abschlussnotiz und Memory-Vorschlag werden absichtlich nur ueber Dateien angenommen,
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

Jeder mit `query-rag --run` persistierte Retrieval-Trace besitzt eine eigene
Sequenz und Hashkette. Er enthaelt den Hash der redigierten Query, Index- und
Konfigurationshash, BM25-Parameter, Treffer-IDs, Vertrauensklassen, Zitate und
den redigierten erzeugten Kontext. Query-/Kontextlimit und
`logging.maxEventPayloadBytes` werden vor dem atomaren Append erzwungen. Neue
Runs verwenden Retrieval-Vertrag v2: Beim Abschluss werden Trace-Anzahl und
finaler Trace-Hash in Summary und Abschluss-Event verankert, sodass `verify`
auch eine entfernte letzte Zeile oder komplette Leerung erkennt.

Memory-Aktionen sind ebenfalls append-only: `accept` und `supersede` benoetigen
eine neue ID, einen getrennten Akteur und explizite vorherige IDs. Der
Erzeuger darf den eigenen Vorschlag nicht annehmen. `set-status` persistiert
`stale` beziehungsweise `rejected` nur als neue Revision; `status` selbst
mutiert nicht. Neue Revisionen sind hashverkettet und `verify` prueft Ledger,
Quellenstaleness, Konfliktausschluss und Retrievalketten.

## Asset-Provenienz

`assets-check` wertet die versionierten Draft-2020-12-Schemas offline aus und
prueft zusaetzlich Querfeld-, Hash-, Run-/Akteur-, Modell-, Pfad-,
Clean-Room-, LFS- und Lebenszyklusinvarianten. Ein strukturell gueltiges
Quarantaenemanifest darf den Standardaufruf bestehen, ist aber niemals
shipping-faehig. Der Releasepfad muss `--require-local --require-approved`
als globalen Scan ohne `--manifest` setzen; dabei sind lokale,
integritaetsgueltige Generation-/Reviewlaeufe, freigegebene Sourceassets und
die vollstaendige repo-weite Source-Inventur Pflicht. `--manifest` dient nur
der gezielten Diagnose und ist kein Releasegate.

Die Schema-Bibliothek ist in `Assets.fs` hinter einer kleinen Adapterfunktion
gekapselt. Sie ist nur eine CoreCLR-Abhaengigkeit dieses Produktionswerkzeugs,
nicht des Native-AOT-Spielclients. Paketgraph, Lizenz-, Wartungs- und
Austauschentscheidung sind in `docs/TOOLCHAIN.md`,
`docs/IP_UND_LIZENZEN.md` und `THIRD_PARTY_NOTICES.md` dokumentiert.

## Tests

Die Tests laufen als Konsolenprogramm und verwenden ueber den Harness denselben
exakt gelockten Schema-Validator:

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

T-004 erweitert die vollstaendige Run-/Prompt-/Modellprovenienz,
kriterienspezifische Evidenz, das RAG-Buildmanifest und sichere Retention.
Memory-Annahmen bleiben trotz der T-002-CLI fachliche Reviewentscheidungen:
Objektiv technische, quellenidentische Records duerfen getrennte Reviewlaeufe
annehmen; kreative, produktbezogene und lizenzielle Entscheidungen bleiben der
Projektleitung vorbehalten.
