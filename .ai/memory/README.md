# Kuratiertes Projektgedächtnis

`records.jsonl` enthält ausschließlich kleine, überprüfbare Wissenseinheiten. Es ist kein Chatdump und keine zweite, unkontrollierte Spezifikation.

## Regeln

- Ein Datensatz benötigt eine stabile ID, Quellen mit Inhalts-Hash, Status und Gültigkeitsbereich.
- Nur `accepted`-Einträge dürfen bei der Planung als Gedächtnis verwendet werden.
- `proposed` ist eine Behauptung, keine Wahrheit.
- Bei Quellenänderung wird ein Eintrag `stale`, bis er erneut geprüft wurde.
- Widersprüche werden nebeneinander erhalten und als Konflikt ausgegeben; das Harness entscheidet sie nicht selbst.
- Korrekturen überschreiben alte Einträge nicht. Ein neuer Eintrag verwendet `supersedes`, der alte wird `superseded`.
- Prompts, Toolausgaben, Vermutungen und Modellbegründungen werden nicht automatisch zu Langzeitgedächtnis.
- Secrets, personenbezogene Daten und fremde urheberrechtlich geschützte Inhalte sind ausgeschlossen.

Das Format ist in `../schemas/memory-record.schema.json` definiert.
