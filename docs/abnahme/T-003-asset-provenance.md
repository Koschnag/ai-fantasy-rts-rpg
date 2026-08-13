# Prüfnachweis T-003: Asset-Provenienz und Quarantänegate

- Implementierungslauf: `01KZXS64AKZ7AMYMPQWVCTJ3F3`
- Koordinationslauf: `01KZXS5QRJQPX6EXBQ67X5KZ1Q`
- Aufgabe: `T-003`
- Status: `ACCEPTED`
- Abnahme: unabhängiger Read-only-Review nach drei gehärteten Freeze-Ständen
- Ausgangs-Commit: `f390b20`
- akzeptierter Workspace-Marker: `1e77ee7f826f396386dde1688fd8b75b1178f2fae48b6fcf253012d6e1d9e1a7`
- finaler Implementierungs-Eventhash: `6eb5cb261fbca0800544b2456547c9276223eed12b2e8dcfec7a48d14a8ae551`
- Implementierungs-Summaryhash: `a8cb5238ec095749724318b782ab73d3c620371d6657be0e9ad901d94e65b47d`

## Ergebnis

Das offline arbeitende F#-Gate validiert strikte Draft-2020-12-Verträge für
Assetmanifeste, portable Generation-Receipts, Modell-Allowlist, Clean-Room-
Policy und strukturierte Reviewevidenz. Receipt und Manifest binden den
verifizierten Generierungslauf, unveränderliche Akteuridentität, vollständige
Eventkette, Prompt-Envelope beziehungsweise prozedurale Generatorparameter,
Inputs, Transformationen und Outputs.

Eine Shippingfreigabe verlangt im globalen Scan genau eine aktive bestandene
Revision für Technik, Visual, Performance, Originalität und Lizenz,
kommerzielle Nutzungsprüfung, getrennte Erzeuger-/Revieweridentitäten,
quellengebundene Zeiten und vollständige Repositoryinventur. Textquellen werden
als begrenzte strikte UTF-8-/Formatdaten bytegenau gegen den Git-Index geprüft;
Binärquellen benötigen einen kanonischen Git-LFS-Pointer mit passendem OID und
Size. Quarantäne und Cooked-Ausgaben bleiben aus Git und RAG ausgeschlossen.

## Nachweis je Abnahmekriterium

| Kriterium | unabhängiges Urteil | Nachweis |
|---|---|---|
| `AC-T003-01` | ACCEPT | Pflichtfeldmatrix, Receipt-/Run-/Akteur-, Spec-, Prompt-/Generator-, Input- und Outputbindungen; unbekannte oder abweichende Runs scheitern fail-closed. |
| `AC-T003-02` | ACCEPT | getrennte gehashte Allow-/Deny-Domänen, Deny-Vorrang, Scan von Prompt, Negativprompt, Spec, Python-/Textquellen, Pfaden, Receipt und Reviewevidenz; Findings vervielfältigen keine problematischen Inhalte. |
| `AC-T003-03` | ACCEPT | fünf gebundene Reviewarten, Zustands-/Historienmatrix, Review nach Generierungsabschluss und Lizenzzeit gleich aktivem Lizenzreview. |
| `AC-T003-04` | ACCEPT | Run-Akteur ist Teil der gehashten Runmetadaten; Receipt übernimmt ihn aus dem Snapshot; Erzeuger-Selbstfreigabe für Originalität oder Lizenz wird blockiert. |
| `AC-T003-05` | ACCEPT | globale Source-/Manifest-/Receipt-Inventur, Rohbyte-Git-Indexprüfung, strikter LFS-Pointer, Ignore-/RAG-Regeln und zusätzlicher isolierter positiver PNG-LFS-Roundtrip. |

## Ausgeführte Qualitätsprüfungen

- `./scripts/rift.sh fmt` und `./scripts/rift.sh lint` — PASS
- Locked Restore und Release-Build — 0 Warnungen, 0 Fehler
- `./scripts/rift.sh test` — 62/62 Tests erfolgreich
- `./scripts/rift.sh assets-check` — `valid=true`, global, drei Quarantänemanifeste, null Freigaben, `shippingReady=false`
- gezielter Scan — `scope=targeted`, grundsätzlich `shippingReady=false`
- `./scripts/rift.sh security` — Secret-/JSON-/NuGet-Audit-/LFS-Baseline PASS
- `./scripts/rift.sh rag-build` und `./scripts/rift.sh verify` — PASS
- `./scripts/rift.sh fresh-checkout-test` — PASS
- isolierter echter PNG-/Git-LFS-Positivroundtrip mit korrektem OID und Size — PASS

## Bewusst verbleibende Grenzen

- Die drei vorhandenen Keyframes bleiben `quarantine`; null Assets sind für
  Shipping freigegeben. Modellrevision und Outputbedingungen des eingebauten
  Bildgenerators reichen für eine Freigabe nicht aus.
- Der Hashscan deckt versionierte bekannte Deny-Einträge ab, ist aber kein
  globaler Eigennamen-, Ähnlichkeits- oder Rechtsdetektor. Unabhängige Reviews
  und ein Gesamtspielreview vor Veröffentlichung bleiben zwingend.
- Noch ist kein Produktionsmodell in `models.lock.json` zugelassen.
- Backup/Restore großer LFS-Quellen, Source-Promotion, Cooking und
  produktionsnahe Assetfamilien folgen erst in späteren Tasks.

Die lokalen Rohereignisse bleiben gitignored unter `.ai/runtime/runs/`.
