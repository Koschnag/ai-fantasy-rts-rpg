# Akzeptierte Revalidierung T-006

- Implementierungslauf: `01KZYH04MTHS6HK6QH2RSJTZ85`
- Produktionslauf: `01KZYQCK0BAGMF88A9N6W7M6GQ`
- Aufgabe: `T-006`
- Status: akzeptiert
- Ausgangs-Commit: `659257d`
- finaler Eventhash: `a47720faab3dfbf69020d2c75c8352cb2bc885c5fba92924a0d82e6f56ef3597`
- Summaryhash: `cf3247aeda0f68f0c84d893a53fb81e88dab7d970b11ceb87c82fc3dc25fd94a`

## Ergebnis

Der BCL-only-F#/.NET-Generator erzeugt ohne Python, DCC, Netzwerk,
Unterprozess oder GPU deterministische GLB-, CPU-PNG- und Reportbytes. Eine
hashverkettete Transaktion veröffentlicht sie mit T-003-Receipt und Manifest
in Quarantäne; Recovery und Manipulationsgrenzen sind fail-closed.

Das erste reale Asset `CAL-STONEWOOD-V1-39FAAE34C4CD` besteht den unabhängigen
T-005-Inspector und `assets-check --require-local`. Seine Rohbytes bleiben
gitignored und `--require-approved` scheitert absichtlich.

## Evidenz

- Release-Build: 0 Warnungen, 0 Fehler
- Tests: 115/115
- Format, Lint, Security, Locked NuGet Audit, Git-LFS-FSCK, RAG, Verify und
  Fresh Checkout: PASS
- unabhängiger Max-Reasoning-Review: ACCEPT
- globaler Assetscan: vier valide Quarantäneassets, null Freigaben,
  `shippingReady=false`

Der ausführliche Nachweis steht unter
`docs/abnahme/T-006-dotnet-asset-generator.md`.
