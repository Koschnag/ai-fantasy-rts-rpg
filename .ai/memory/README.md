# Kuratiertes Projektgedächtnis

`records.jsonl` enthält ausschließlich kleine, überprüfbare Wissenseinheiten. Es ist kein Chatdump und keine zweite, unkontrollierte Spezifikation.

## Regeln

- Ein Datensatz benötigt eine stabile ID, Quellen mit Inhalts-Hash, Status und Gültigkeitsbereich.
- Nur `accepted`-Einträge dürfen bei der Planung als Gedächtnis verwendet werden.
- `proposed` ist eine Behauptung, keine Wahrheit.
- Bei Quellenänderung wird ein Eintrag `stale`, bis er erneut geprüft wurde.
- Widersprüche werden nebeneinander erhalten und als Konflikt ausgegeben; das Harness entscheidet sie nicht selbst.
- Korrekturen überschreiben alte Einträge nicht. Ein neuer Eintrag verwendet `supersedes`, der alte wird `superseded`.
- Neue Vorschläge besitzen einen expliziten `conflictKey`; mehrere aktive Annahmen für denselben Key werden gemeinsam aus Retrieval ausgeschlossen und sichtbar gemeldet.
- `memory propose`, `accept`, `supersede` und `set-status` hängen ausschließlich neue Revisionen an. Der erzeugende Akteur darf den eigenen Vorschlag nicht annehmen; jeder Vorschlag kann nur eine Nachfolgerevision besitzen.
- `rag.maxFileBytes` begrenzt Quellen in jedem Lifecycle- und Retrieval-Pfad identisch. Ledger- und Sourcepfade mit Symlink-, Junction- oder ReparsePoint-Komponenten werden nicht verfolgt.
- Neue CLI-Revisionen sind über `previousRecordHash`/`recordHash` verkettet. Vorhandene Legacy-Zeilen bleiben der verankerte Anfang und werden nicht umgeschrieben.
- Prompts, Toolausgaben, Vermutungen und Modellbegründungen werden nicht automatisch zu Langzeitgedächtnis.
- Secrets, personenbezogene Daten und fremde urheberrechtlich geschützte Inhalte sind ausgeschlossen.

Das Format ist in `../schemas/memory-record.schema.json` definiert.

Der aktuelle Zustand lässt sich ohne Mutation mit `./scripts/rift.sh harness memory status` anzeigen und mit `memory validate` strukturell sowie kryptografisch prüfen. Quellen-Staleness wird abgeleitet; eine persistierte Statusänderung benötigt stets `set-status` mit neuer ID und Akteur.
