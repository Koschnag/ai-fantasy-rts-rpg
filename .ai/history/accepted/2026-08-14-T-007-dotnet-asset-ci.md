# Akzeptierte Revalidierung T-007

- Implementierungslauf: `01KZYQZRGHKXAE4QJ3X25RMDJN`
- Aufgabe: `T-007`
- Status: akzeptiert
- Implementierungscommit: `b188e111935d6be761d64ba24ec71e1ba3be5d49`
- finaler Eventhash: `a313724769cf636655fa79de45537e6060d2790cc35ed44b3ee809c24f570d82`
- Summaryhash: `418ae1bb67e75df70a7b86ff016211008aae60124930004a5672f988f47f9779`

## Ergebnis

Der private Linux-x64-Workflow reproduziert die vollständig in-process
laufende F#/.NET-Assetkalibrierung aus dem committeten Git-Stand. Er bindet
SDK, Lockdatei und drei eingebettete Generatorquellen, beweist deterministische
Artefakte, T-005-Inspektion, T-006-Recovery und T-003-Provenienz und publiziert
nur bereinigte JSON-/Logevidenz. Der Generator verwendet weder Python noch
DCC, Netzwerk oder Kindprozesse.

## Evidenz

- unabhängiger Max-Reasoning-Review: AC-T007-01 bis AC-T007-06 ACCEPT
- Release-/Regressionstests: 120/120
- privater T-007-Run `31758082185`: PASS
- allgemeiner Verify-Run `31758082100`: PASS
- Artifact `9203537818`: nur JSON und begrenztes Testlog
- Evidenzhash: `51508b53a0bcaee32606451b79ccfadf9baca8d6ba4734e914971b838cd9b551`
- Testloghash: `93112ad18515c665e283f5023e3abdb1e0f4262288736b0b2422602c23624254`

Der ausführliche Nachweis steht unter
`docs/abnahme/T-007-dotnet-asset-ci.md`.
