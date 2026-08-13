# ADR 003: Lokales Agent-Harness und RAG

- **Status:** akzeptiert
- **Datum:** 2026-08-13
- **Entscheidungsverantwortung:** Projektleitung
- **Bezug:** Z-004; `docs/HARNESS.md`

## Kontext

Weitgehend autonome KI-Produktion benötigt persistenten Verlauf, nachvollziehbare Entscheidungen, Gedächtnis, Retrieval und objektive Evidenz. Ein Chatverlauf allein ist weder stabil noch prüfbar. Ein externer Vektor-/Agentendienst würde früh Kosten, Datenweitergabe und Lock-in erzeugen.

## Entscheidungskriterien

- offline, FOSS-first und ohne Dienst betreibbar
- deterministisch, diffbar und quellgebunden
- Trennung von Auditlog, kuratierter Wahrheit und rebuildbarem Index
- sichere Behandlung indexierter Fremdinhalte
- Erweiterbarkeit auf semantische Suche und OpenTelemetry ohne Pflicht dazu

## Betrachtete Optionen

- Chat-/Modellgedächtnis: bequem, aber nicht reproduzierbar oder ausreichend quellengebunden.
- Externe Vektordatenbank und Cloud-Embeddings: mächtig, aber für die aktuelle Größe unnötig, kosten-/datenschutz- und lock-in-behaftet.
- Git-kuratierte Wahrheit + lokale JSONL-Ereignisse + BM25: klein, transparent, deterministisch; semantischer Recall später erweiterbar.

## Entscheidung

Das Rift Harness verwendet vier Ebenen:

- lokales Arbeitsgedächtnis je Lauf
- append-only, hashverkettete JSONL-Ereignisse als episodischen Verlauf
- atomare, quellengehashte und separat akzeptierte Records als semantisches Gedächtnis
- versionierte Prompts, Policies, Aufgaben und Schemas als prozedurales Gedächtnis

RAG-Stufe 1 nutzt deterministische zeilenbasierte Chunks und BM25. Jeder Treffer nennt Pfad, Zeilen und Hash. Der Index ist Cache, niemals Quelle der Wahrheit. Semantische Embeddings werden erst ergänzt, wenn ein Retrieval-Eval einen messbaren Nutzen zeigt; lokal gepinnte FOSS-Modelle und Hybrid-Ranking werden bevorzugt.

## Folgen

- Laufzeitdaten liegen lokal unter `.ai/runtime`; bereinigte akzeptierte Berichte können versioniert werden.
- Secrets und Roh-/Binärassets werden weder indexiert noch ungefiltert geloggt.
- Retrievalinhalt gilt als untrusted data und kann keine Agentenregeln oder Berechtigungen verändern.
- Kein Modell darf eigene Behauptungen automatisch als akzeptiertes Gedächtnis hochstufen.
- Spätere SQLite-FTS5-/ONNX-Optimierungen müssen dasselbe Chunk-/Zitatmodell bewahren.
- Erneute Prüfung, wenn der Korpus oder gemessene Retrievalfehler eine semantische Stufe rechtfertigt.
