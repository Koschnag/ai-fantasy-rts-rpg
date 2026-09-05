# T-074 – Nativer Archivcontainer und Kalibrier-Provenienz

## Stand und Aussagegrenze

`ACCEPTED` fuer den ueber PR 56 integrierten Implementierungsstand
`290752299306b09cc0f2a2daca1342ee070f3dc3`, Tree
`d2d5fcdd6cb6e623c743d500b8d0df7e5553c152`.
Dieser Beleg reconciliert tatsaechlich vorliegende Implementierungs-, Review-
und Pruefergebnisse; er ist keine neue Runtime-, Gameplay-, Zielhardware-
oder Autopilot-Betriebsabnahme.

Der heutige Verifikationslauf ist bewusst rueckblickend. Er behauptet weder
einen zeitgleichen urspruenglichen Implementierungslauf noch fruehere
Freigaben oder zurueckdatierte Ereignisse.

## Gebundene Promotion

- Exakter Implementierungskandidat: `cda84a4f817a4b4c3a9d9fe75b4ff094de8d2998`.
- Unabhaengiges automatisiertes Astra-Review: [PASS fuer Commit und Tree](https://github.com/Koschnag/ai-fantasy-rts-rpg/pull/56#issuecomment-5551850750).
- [PR 56](https://github.com/Koschnag/ai-fantasy-rts-rpg/pull/56): normaler Squash-Merge;
  einziger Main-Parent `8e3b9c7b5ce831059b7797e7e9be21608d247402`;
  gemergter Tree exakt gleich dem geprueften Kandidatentree.
- Exact-head [Verify 33966183053](https://github.com/Koschnag/ai-fantasy-rts-rpg/actions/runs/33966183053)
  und [Asset 33966183043](https://github.com/Koschnag/ai-fantasy-rts-rpg/actions/runs/33966183043): erfolgreich.
- Post-merge [Verify 33966971347](https://github.com/Koschnag/ai-fantasy-rts-rpg/actions/runs/33966971347)
  und [Asset 33966971433](https://github.com/Koschnag/ai-fantasy-rts-rpg/actions/runs/33966971433): erfolgreich am exakten Main-Commit.
  Verify und sein Fresh-Checkout-Pruefschritt bestanden jeweils 385/385 Tests.

Die Abschlussmetadaten durchlaufen ihrerseits unabhaengiges Review und die
geschuetzte PR-/Squash-Pipeline. Deren zukuenftige Ergebnisse werden hier
nicht vorweggenommen. Der eingefrorene historische Reconciliation-Katalog
bleibt byteidentisch; T-074 ist keine nachtraeglich ergaenzte Alt-Promotion.

## Heutiger Harness-Verifikationslauf

- Run `01M1RTMY67F5RRARHWMN1HT2MR`, Actor `riftward-maintenance-verifier`, Task `T-074`.
- Reale UTC-Zeiten: `2026-09-05T13:03:12.5839404+00:00` bis
  `2026-09-05T13:13:03.9549488+00:00`.
- Provenienz bindet den obigen Main-Commit, Konfiguration, Prompt und aktuellen
  Toolchain-Lock; alle sechs Vollstaendigkeitsfelder sind wahr.
- Run abgeschlossen als `succeeded`: 20 Events;
  Final-Event-Hash `1eda318a0b4c17f2e87f22cee2f929427acddbdcac81317385bf9fb20f0a526d`;
  Summary-Hash `a58fd2b60c016f39b9f1fb974d0eb5fa821a9d3c71b9e50d27a640dd40985713`.
- Native `verify --run` meldet `valid=true`, einen geprueften Run und keine
  Integritaetsfehler. Der Status bezeichnet den abgeschlossenen
  Verifikationsauftrag, nicht ein angebliches PASS jedes Zusatzprobes.
- 16 aktuelle protokollierte Befehlspruefungen: 14 Exitcode 0, zwei gesondert
  offengelegte Exitcode-2-Probes; dazu hashgebundener Archivbericht und echtes
  Review-Readback. Private vollstaendige Pruefunterlagen bleiben ausserhalb
  des oeffentlichen Repositorys erhalten.

## Abnahmekriterien

| Kriterium | Erfuellungsbeleg |
|---|---|
| AC-T074-01 | Aktueller streamender Vergleich des aufbewahrten Archivs mit der checksumgebundenen primaeren Git-Inventarliste: 5194 von 5194 Eintraegen, keine fehlenden/zusaetzlichen Eintraege oder Blob-/Modusabweichungen. Commit `35a98dd6453cf25dc75c68e233abb400836d5920`, Root-Tree `a72e6c7cb722e213ca7395f9a190b0f3744e7aec`, Archiv-SHA-256 `ec4967a243c6b6e5438da0ed04b19617d52738689ff014313cc75f2f72a20c90`. |
| AC-T074-02 | Native-getesteter Buildscript, Lock und Dokumentation sind byteidentisch zum gemergten Stand. Aufbewahrte Buildlogs, checksumgebundenes `--verify-cache`-PASS, aktueller Hash-/Groessencheck aller 14 erzeugten Native-Artefakte und Bindung an den echten frueheren Smoke-Report. Das gepruefte Script setzt den Cache relativ und loest den Toolchain-Prefix absolut auf; aktuelle Shellsyntax bestanden. |
| AC-T074-03 | Exakter 12-Dateien-Implementierungsumfang und finale unabhängige Reviewbindung; aktuelle Rollen-/Parent-/Tree-Pruefung, Rollenregressionen, hermetische Reconciliation-Pruefungen und Task-Schema bestanden. Alle unveraenderten Pflichtgates bestanden auf dem exakten GitHub-Kandidaten und nach Merge. |
| AC-T074-04 | Die obige PR-/Commit-/Parent-/Tree-/Check-Kette belegt reguläre Integration und erfolgreiches post-merge Verify vor der Statusakzeptanz. Der echte heutige, abgeschlossene und integritaetsgueltige Harness-Run verknuepft Kriterien, Quellen, Befehle, Resultate, Hashes und Grenzen. |
| AC-T074-05 | Echte neue Kalibrierung `01M1RR3QZPNSGBTG44ZCYE1FVX`: heute erneut Run-Integritaet und `--require-local` fuer die wirklich erzeugten Assets geprueft. Drei Payloadhashes, Generatorquellen, Spec und Seed unveraendert; 2348418 Payloadbytes, weiterhin `quarantine`. Historischer Manifest, Receipt und kompletter Lock stimmen bytegenau mit dem vorherigen Git-Stand ueberein. Aktuelle FSI-Tests der exakten Main-Testquelle pruefen Gesamt-Lockhash, Whitespace-Drift, geschlossene Schema-Ablehnung und Unsafe-Workspace-Faelle; aktuelle globale sechs-Manifest-Pruefung bestanden. |

## Ausgewaehlte Beleg-Hashes

| Beleg | SHA-256 |
|---|---|
| Primaerer Archiv-/Tree-Vergleich | `8c660e71f300327d36994951cdb018ca449c8155bb4de1dc34410da223e08362` |
| Native `--verify-cache`-Log | `00244f6d07849993f615dcbbb2dd2933d0c1e0a912652b35f0ad6c34eb115023` |
| Frueherer Native-Smoke-Report | `3886f54becc1f2c100f9901bf20f725b3082b8b82dd7e62c6f92373256fb231e` |
| Aktueller Toolchain-Lock | `4310f9c405f261a698235c835bc56cb3d01821a44936b6146fc009e51df933cf` |
| Historischer Toolchain-Lock | `49dd75fa91e539fcf19ab3f8b01aecd81a3cf11fbff940f14051ce80ec088b42` |

## Nicht bestandene Zusatzprobes und verbleibende Grenzen

Der zusaetzliche heutige `--require-local`-Versuch im isolierten historischen
Validierungspaket lieferte `ASSET_GENERATION_RUN_MISSING`: Der urspruengliche
historische Runtime-Run ist dort nicht vorhanden. Dieses Ergebnis bleibt im
Ledger erhalten; weder eine historische lokale Run-Abnahme noch ein
rekonstruierter alter Run wird behauptet. Die geforderte Byteerhaltung der
Historie und die reale neue Generierung sind davon getrennt nachgewiesen.

Der zusaetzliche lokale Pages-Vertragstest konnte mangels `node` nicht laufen
und bleibt als Exitcode 2 aufgezeichnet. Kein lokales PASS und keine
nachinstallierte Abhaengigkeit wird behauptet. Der unveraenderte Pages-Gate
ist im exakten erfolgreichen GitHub-Verify enthalten.

Dieser heutige Lauf fuehrte keinen Vollbuild, Native-Build oder neuen Smoke
aus. Die frueheren nativen Logs besitzen keine vollstaendige signierte
Befehls-Huelle; die Bindung beruht auf aufbewahrten Belegen, identischen
relevanten Quellbytes und aktuell verifizierten Artefakthashes. Der fruehere
Smoke verwendete Software-Rendering; er ersetzt keine Zielhardware-,
Windows-/macOS-, Performance- oder Gameplay-Abnahme.

Es erfolgten kein Modellaufruf, T-053-Start, T-042-Zielereignis,
Produktionswriter-, Dienst-, Storage- oder Credentialwechsel. Rollback bleibt
ein gepruefter Revert der einschlaegigen PR; historische Belege werden nicht
umgeschrieben.
