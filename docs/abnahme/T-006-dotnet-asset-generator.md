# Prüfnachweis T-006: .NET-Assetgenerator und transaktionale Quarantäne

- Implementierungslauf: `01KZYH04MTHS6HK6QH2RSJTZ85`
- erster Produktionslauf: `01KZYQCK0BAGMF88A9N6W7M6GQ`
- Aufgabe: `T-006`
- Status: `ACCEPTED`
- Abnahme: unabhängiger Max-Reasoning-Funktionsreview nach adversarialer Härtung
- Ausgangs-Commit: `659257d`
- finaler Implementierungs-Eventhash: `a47720faab3dfbf69020d2c75c8352cb2bc885c5fba92924a0d82e6f56ef3597`
- Implementierungs-Summaryhash: `cf3247aeda0f68f0c84d893a53fb81e88dab7d970b11ceb87c82fc3dc25fd94a`
- finaler Retrieval-Tracehash: `c1d37e41eece78520f110771166ae6d16c89582f7c2075ccbd26b73efd15533f`

## Ergebnis

Der BCL-only-F#/.NET-10-Generator schreibt GLB 2.0 direkt, rastert eine
960×540-RGBA8-Preview deterministisch auf der CPU und erzeugt einen
kanonischen Technikreport. Der Produktionspfad verwendet weder Python noch
Blender, DCC, GPU, Netzwerk, Kindprozess oder neue Paketabhängigkeit.

Ein hashverkettetes Jobjournal publiziert Quarantäneverzeichnis, T-003-Receipt
und Manifest mit festen Zustandsübergängen und idempotenter Recovery. Safe
Paths, Quell-/Assembly- und SDK-Pin-Bindung werden vor dem Job, vor der
Provenienzbindung und vor dem Commit erneut geprüft. Zielkollisionen,
Symlinktausch, Manipulationen, Abbruch und Ressourcenüberschreitung schlagen
ohne fremde Datenmutation fehl.

## Erster realer Kalibrierungssatz

- Asset-ID: `CAL-STONEWOOD-V1-39FAAE34C4CD`
- Status: `quarantine`
- Manifest: `assets/manifests/CAL-STONEWOOD-V1-39FAAE34C4CD.json`
- Receipt: `assets/receipts/CAL-STONEWOOD-V1-39FAAE34C4CD/01KZYQCK0BAGMF88A9N6W7M6GQ.json`
- GLB: 270.344 Bytes, SHA-256 `6dddf5efed35fc29676f22ef4b7d107637506a45dc148ff44453c0627055f178`
- Preview: 2.074.363 Bytes, SHA-256 `69adc8133c2bb9f5f78035be22c9dca83a7ebe84d18bc35758b370c89ee6fcdd`
- Report: 3.711 Bytes, SHA-256 `6a063317489ccd8a979e4fda28a26b6bd08bb717508fa083fbef96131de305e4`

Die drei Rohartefakte bleiben gitignored unter
`assets/quarantine/3d/CAL-STONEWOOD-V1-39FAAE34C4CD/`; nur Manifest und
Receipt sind versioniert. `assets-check --require-local` besteht,
`--require-approved` scheitert erwartungsgemäß.

## Nachweis je Abnahmekriterium

| Kriterium | unabhängiges Urteil | Nachweis |
|---|---|---|
| `AC-T006-01` | ACCEPT | Kanonischer SDK-10.0.110-Pin, drei eingebettete F#-Quellen und wiederholte Quell-/Lock-Race-Checks; Manipulationen und Symlinks liefern Exit 2/3 vor Publikation. |
| `AC-T006-02` | ACCEPT | Direkter GLB-Writer besteht den unabhängigen T-005-Inspector und dessen vollständige Struktur-/Korruptionsmatrix. |
| `AC-T006-03` | ACCEPT | Zwei isolierte Wurzeln sind byteidentisch; Alternativseed ändert GLB und PNG; Pixel-, PNG-, Adler- und CRC-Goldens bestehen. |
| `AC-T006-04` | ACCEPT | IL-/Quellscan, instrumentierter Prozesspfad, Cancellation-Checkpoints und 64-Datei-/16-MiB-/24-MiB-/300-s-Grenzen. |
| `AC-T006-05` | ACCEPT | Geschlossene CLI-Hülle, Produktions-Specroot, ULID, Safe Paths, Symlink-/Kollisions- und Exitcode-Fixtures. |
| `AC-T006-06` | ACCEPT | Hashverkettete Zustände von CREATED bis COMMITTED, atomische Einzelpublikation und Crash-Recovery. |
| `AC-T006-07` | ACCEPT | Fremde, geänderte, verlinkte oder inkonsistente Pfade werden nicht gelöscht oder überschrieben. |
| `AC-T006-08` | ACCEPT | Verifizierter T-003-Run/Event/Receipt/Manifest-Crosscheck sowie reales lokales Quarantäneasset. |

## Ausgeführte Qualitätsprüfungen

- Format und Lint — PASS
- Release-Build — 0 Warnungen, 0 Fehler
- Tests — 115/115 erfolgreich
- unabhängiger Abschlussreview — `ACCEPT`
- globaler Assetscan — vier valide Quarantäneassets, null Freigaben,
  `shippingReady=false`
- Security einschließlich Locked NuGet Audit und Git-LFS-FSCK — PASS
- RAG/Run-Verifikation — PASS
- Fresh Checkout aus isoliertem Arbeitsbaum — PASS

## Bewusst verbleibende Grenzen

T-006 erteilt keine visuelle, Originalitäts-, Lizenz-, Performance- oder
Shippingfreigabe. Das Asset ist ein neutraler Strukturkalibrator ohne
Texturen, Ornamentik oder regionsspezifische Formensprache. T-007 übernimmt
den getrennten sauberen Commit-/Fresh-Checkout-CI-Nachweis; T-050 verantwortet
erst später Reviews, Source-Promotion, Backup, Cooking und Runtime-Messung.
