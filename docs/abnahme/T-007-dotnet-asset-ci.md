# Prüfnachweis T-007: Fresh-Checkout-CI für die .NET-Assetkalibrierung

- Implementierungslauf: `01KZYQZRGHKXAE4QJ3X25RMDJN`
- Aufgabe: `T-007`
- Status: `ACCEPTED`
- Abnahme: unabhängiger Max-Reasoning-Funktionsreview
- finaler Implementierungscommit: `b188e111935d6be761d64ba24ec71e1ba3be5d49`
- finaler Eventhash: `a313724769cf636655fa79de45537e6060d2790cc35ed44b3ee809c24f570d82`
- Summaryhash: `418ae1bb67e75df70a7b86ff016211008aae60124930004a5672f988f47f9779`
- finaler Retrieval-Tracehash: `e4b9c28e3163877bad57fd923c76f8d603132617621981082ba4f5c8aa89b58b`

## Ergebnis

Ein eigener pfadgefilterter Linux-x64-Workflow archiviert ausschließlich den
committeten Git-Stand, verwendet Locked Restore und .NET SDK 10.0.110 und
führt die vollständige T-005-/T-006-/T-003-Regressionskette aus. Der eigentliche
Generatorpfad bleibt F#/.NET-BCL-only und öffnet weder Kindprozesse noch
Netzwerksockets. Python, Blender, ein DCC, GPU-Code und neue Paketabhängigkeiten
werden nicht verwendet.

Zwei isolierte Referenzläufe erzeugen byteidentische GLB-, PNG- und
Reporthashes; der festgelegte Alternativseed ändert GLB und Preview. Alle drei
Ergebnisse bestehen den unabhängigen T-005-Inspector. Build- und Quellbytes
werden vor der ersten Generierung gegengeprüft. Manipulation einer der drei
Generatorquellen scheitert vor Runtime- oder Evidenzausgabe.

## Private CI-Evidenz

- T-007-Workflow: GitHub-Run `31758082185`, erfolgreich am exakten Commit
  `b188e111935d6be761d64ba24ec71e1ba3be5d49`
- allgemeiner Verify-Workflow: GitHub-Run `31758082100`, erfolgreich
- bereinigtes Artifact: `9203537818`, genau JSON-Evidenz und begrenztes Testlog
- Evidenz-SHA-256: `51508b53a0bcaee32606451b79ccfadf9baca8d6ba4734e914971b838cd9b551`
- Testlog-SHA-256: `93112ad18515c665e283f5023e3abdb1e0f4262288736b0b2422602c23624254`
- vollständige Suite: 120/120 erfolgreich
- SDK-Lockeintrag: `840ca3968e7f20d9e525a2d3a0337e8ba81fad50800942ef299496ae18677d4b`
- Lockdatei: `e1115c5484a8df29fd25f2a96ee77de8f5561088a869b4192b5cc8f791f4afa8`
- Quellenaggregat: `4923778143b2491b6c4ea70f3343cded86fda332badee47f0b9fc1ba739d9887`

Referenzhashes sind GLB
`6dddf5efed35fc29676f22ef4b7d107637506a45dc148ff44453c0627055f178`,
PNG `69adc8133c2bb9f5f78035be22c9dca83a7ebe84d18bc35758b370c89ee6fcdd`
und Report
`6a063317489ccd8a979e4fda28a26b6bd08bb717508fa083fbef96131de305e4`.
Der Alternativseed erzeugt andere GLB-/PNG-Hashes und den Reporthash
`111bf5ceba7aaf8e8d4f54f77b8c44a72f099c93f3fb25e0162b73dcb8c76578`.

## Nachweis je Abnahmekriterium

| Kriterium | unabhängiges Urteil | Nachweis |
|---|---|---|
| `AC-T007-01` | ACCEPT | Clean Commit-Archiv, Locked Restore, SDK-/Lock-/Quellenbindung; jede der drei Quellmutationen scheitert vor Ausgabe. |
| `AC-T007-02` | ACCEPT | Zwei byteidentische Referenzläufe, abweichender Alternativseed, drei erfolgreiche Inspectorläufe; instrumentierter Trace ohne Kindprozess- oder Netzwerk-Syscall. |
| `AC-T007-03` | ACCEPT | 120/120 Tests; alle sechs T-005- und acht T-006-Kriterien sind an Exitcode und Testloghash gebunden. |
| `AC-T007-04` | ACCEPT | Ausführbare Positiv- und Leakage-Fixtures für Gitstatus/-index, Quarantäne, Cooked, Recovery, Memory und RAG; Upload enthält nur JSON und begrenztes Log. |
| `AC-T007-05` | ACCEPT | Policy bindet ausschließlich den .NET-Identifier, drei Quellen und SDK-Pin; DCC bleibt optional und nicht gatend. |
| `AC-T007-06` | ACCEPT | Enger Pfadfilter, `contents: read`, feste Action-SHAs, 30-Minuten-Limit, Concurrency-Abbruch, keine Secrets oder Caches. |

## Qualitätsgrenzen

Der Nachweis gilt für Linux-x64 und .NET SDK 10.0.110. Er behauptet keine
Cross-Host-, Cross-RID- oder Cross-SDK-Byteidentität und erteilt keine visuelle,
rechtliche, Performance- oder Shippingfreigabe. Quarantäneartefakte bleiben
lokal und gitignored; Source-Promotion, LFS, Cooking und Nutzerreview gehören
zu T-050 oder einem später ausdrücklich freigegebenen Auftrag.
