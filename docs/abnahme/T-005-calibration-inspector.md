# Prüfnachweis T-005: Calibration-v1 und unabhängiger 3D-Inspector

- Implementierungslauf: `01KZY44M2P2RNSA5XNGM4P9EMY`
- Aufgabe: `T-005`
- Status: `ACCEPTED`
- Abnahme: unabhängiger Read-only-Funktionsreview nach mehreren gehärteten Ständen
- Ausgangs-Commit: `0c7d2e5`
- Referenzspec-SHA-256: `39faae34c4cd515cb724a8ef1e2e4bee159a232136218fbb8afd8edd52db2cf8`
- finaler Implementierungs-Eventhash: `ed454d10578fc6dc05bdd9ab145e0739aff876d417e8c322003a1bb6aa2e283f`
- Implementierungs-Summaryhash: `1a2aa054bed40e7c49566738199e17a8aa752ab83e63688af21ac34879364775`
- finaler Retrieval-Tracehash: `928aa7d485780c1c1dc8de3717a5cfd2bceba1fecce41ba3b1ba4b2c6081b187`

## Ergebnis

Das F#/.NET-Werkzeug validiert den geschlossenen numerischen
`calibration-v1`-Vertrag und leitet PCG32, Modulanordnung, ganzzahlige
Mikrometergeometrie, LODs, Kollision, Bounds, Pivots und Snap-Transforms
deterministisch ab. Der Blender-unabhängige Inspector prüft synthetische
GLB-2.0-, normalisierte PNG- und kanonische Technikreportbytes einschließlich
vollständiger Box-/Dreiecks-/Normalen-/UV-Topologie und Cross-Hashes.

Die öffentliche CLI liefert genau eine kanonische JSON-Zeile mit stabilen
Exitcodes. Sichere relative Pfade, Workspace- und Pfadsymlinks, Unicode,
Größenlimits, JSON-Typen, GLB-Chunkpadding sowie Host-Injektionsvariablen sind
durch negative Fixtures abgedeckt. Das Werkzeug startet Blender nicht und
enthält keine fremden Medien oder neue Paketabhängigkeit.

## Nachweis je Abnahmekriterium

| Kriterium | unabhängiges Urteil | Nachweis |
|---|---|---|
| `AC-T005-01` | ACCEPT | Vollständige Pflichtfeld-/Typ-/Bereichs-/Beziehungs-/Sliver-Matrix, 16-KiB-Grenze, kanonische Bytes und Exit 2. |
| `AC-T005-02` | ACCEPT | veröffentlichte PCG32-Vektoren, Referenz- und Alternativseed, Kandidaten, Boxen, Bounds, Achsen, Quaternionen und Farben. |
| `AC-T005-03` | ACCEPT | handgebautes Positiv-GLB sowie Header-, Chunk-, Accessor-, Index-, NaN-, URI-, Hierarchie-, Material-, Transform- und Topologiekorruptionen mit Exit 5. |
| `AC-T005-04` | ACCEPT | 960×540-RGBA8, CRC/Chunk-/Deflate-Ende, variable Metadaten, kanonischer Report und einzeln mutierte Spec-/GLB-/Preview-/Quellhashbindungen. |
| `AC-T005-05` | ACCEPT | früher Accessor-Budgetpreflight für jede erreichbare Limit-plus-eins-Grenze; Referenzsumme 255.048 dekodierte Bytes und 18 Renderprimitives. |
| `AC-T005-06` | ACCEPT | CLI-, Wrapper-, Redaction-, Symlink-, Unicode-, 80/81-Segment- und 240/241-Gesamtpfadfixtures; minimaler UTF-8-JSON-Output. |

## Ausgeführte Qualitätsprüfungen

- `./scripts/rift.sh fmt` und `./scripts/rift.sh lint` — PASS
- Locked Restore und Release-Build — 0 Warnungen, 0 Fehler
- Tests — 90/90 erfolgreich
- `./scripts/rift.sh assets-check` — global gültig, drei Quarantänemanifeste, null Freigaben, `shippingReady=false`
- `./scripts/rift.sh security` — Secret-/JSON-/NuGet-Audit-/LFS-Baseline PASS
- `./scripts/rift.sh rag-build` und `./scripts/rift.sh verify` — PASS nach finaler Quellenaktualisierung
- `./scripts/rift.sh fresh-checkout-test` — PASS
- unabhängige Abschlussmatrix `AC-T005-01` bis `AC-T005-06` — ACCEPT

## Bewusst verbleibende Grenzen

- T-005 erzeugt kein Asset und startet Blender nie. Die reale Erzeugung,
  Linux-Isolation, Prozesslimits, Journal-/Recovery-Transaktion sowie T-003-
  Receipt/Manifestbindung sind ausschließlich T-006.
- Die Zähler sind Strukturproxies, kein GTX-660-/M1-/RX-580-FPS-, RAM-, VRAM-
  oder Runtime-Importnachweis.
- Es wurde kein Asset nach `assets/source/` aufgenommen und keine visuelle,
  Originalitäts-, Lizenz- oder Shippingfreigabe erteilt.

Die lokalen Rohereignisse bleiben gitignored unter `.ai/runtime/runs/`.
