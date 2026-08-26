# T-031 – Versioniertes atomares Save/Load

**Status:** `DONE` — Implementierung durch den Builder-Lauf geprüft,
In-Scope-Defekte repariert und durch die unabhängige Review-/Vollendungs-
sitzung abgenommen (Abschnitt „Unabhängige Review-Sitzung“). Staging,
Commit und Push bleiben getrennten Autoritäten vorbehalten.
**Umsetzung:** Autonomer Builder-Lauf (Akteur `ox-alpha-builder`,
Modell `stealth/ox-alpha`), 2026-08-26, Arbeitszweig
`autopilot/ox-alpha` auf Basis von `main` = `74ac4a0`.
**Harness-Build-Run:** `01M0XYH3VXYBXRTGV1BP0JKEAS`
(Akteur `ox-alpha-builder-t031`, Status succeeded,
Summary-Hash `e25ff9ba6220c83361eff3a6bd63eb6f9d705aeb65ca160964598b597e9e6854`).
**Harness-Review-/Vollendungs-Run:** `01M0XYZEQJSGJSFEHKJ4GVCV2N`
(Akteur `t031-review-completion`, Modell `stealth/ox-alpha`).

## Gelieferte Artefakte

- **Savevertrag (Abschnitt 0, vor der Implementierung):**
  `docs/SAVEVERTRAG.md` V1 mit Alternativen, Gründen und Rückrollweg je
  Wahl: kanonische Binärcodierung `riftward-save-canonical-binary-v1`
  (Schemaversion zuerst lesbar, feste Feld-/Byteordnung, BCL-only,
  reflectionsfreier Ladepfad), doppelter SHA-256-Anker (`payloadHash` über
  Payloadbytes, `metaHash` über Kopf einschließlich Hashfeld) mit
  dokumentierter Metadatenabgrenzung, Payloadumfang exakt nach
  Simulationsvertrag V1 inklusive bytegetreuer Übernahme des transienten
  Cursor>Anzahl-Paares, Envelopeabbild mit ausdrücklich leeren/unavailable
  Feldern, Atomarprotokoll, Größen-Sanity-Faktor 4 im Band 2×–16×,
  Fortsetzungsminimum die Hälfte des Planhorizonts, Migrationsregel ohne
  Erfindung, Zustandszugriff über Kompilierungszeit-Accessoren und
  Exitcodes 33/34.
- **Neues Runtimeprojekt:** `src/Riftward.Save` (BCL-only, C#, referenziert
  ausschließlich Riftward.Simulation): `SimulationSaveAdapter`
  (`UnsafeAccessor`-Bindung plus fail-closed Hashankerprüfung),
  `CanonicalSaveCodec`, `SaveDocumentValidator` mit neun unterscheidbaren
  Verletzungsklassen, `SlotStore` mit Fault-Injection-Nahtstelle,
  `SaveMigrator`, interne Korruptionsfixtures als gemeinsame
  Testinfrastruktur.
- **Öffentlicher Befehl:** `./scripts/rift.sh savecheck --report PFAD`
  (headless nativ linux-x64, rein CPU-seitig, keine SDL3-/bgfx-Artefakte);
  Report Schemaversion 1 nach NF-007 mit Umgebungsbinding, Kalibrierbasis,
  diagnostischen Dauerfeldern (`gateCoupled=false`) und Prüfklassenmatrix.
- **Tests:** `tests/RiftHarness.Tests/SaveTests.fs` (13 neue Tests),
  Suite 205 → **218/218**.

## Evidenz der Abnahmekriterien (Builder-Seite; von der Review-Sitzung bestätigt)

- **AC-T031-01 (Vertragsspike vor Implementierung):**
  `docs/SAVEVERTRAG.md` wurde erstellt, bevor Produktcode existierte;
  Abschnitt 11 nennt alle Verletzungsklassen, Abschnitt 6 die
  Schwellwertableitung, Abschnitt 9 die Strukturentscheidung zum
  Zustandszugriff. Keine Budgetzeile geändert
  (`git diff HEAD -- docs/PERFORMANCE_BUDGET.md` leer). Spiegeltest:
  `T-031 save contract mirrors documented values`.
- **AC-T031-02 (Befehlsvertrag):** Autoritativer Lauf
  `./scripts/rift.sh savecheck --report artifacts/t031/savecheck.json`
  → Exitcode 0, Report SHA-256
  `877465aec8f5deabc25dcc992acaafbba65dc09cedaf85ac7717b71c4ec83050`,
  19/19 Prüfklassen pass. CLI-Negativfälle (Usage ohne `--report`,
  Horizontverstoß mit Meldung ohne Report, nicht schreibbarer Pfad →
  Code 28) im Test `cliContractRunsSavecheckWithReportsAndControlledFailures`;
  Buildguard im Shellwrapper durch
  `riftScriptSavecheckContractKeepsAppBuildGuard` gebunden.
- **AC-T031-03 (Kanonischer Determinismus):** Zwei Fresh-Prozessläufe im
  CLI-Test erzeugen denselben `payloadHash`
  `6ce087b6070e820c66b2435a756681ef9cf8489dbf9cc73c3be3ce5971f4899d` bei
  zulässig variierenden UTC-Metadaten; Einheitstest
  `payloadByteIdentityAndMetadataDelineation`; Ordnungs-/Endianness-/
  Framingverletzungen werden kanonisch abgewiesen
  (`corruption-canonical-order` u. a.).
- **AC-T031-04 (Fortsetzungsgleichheit):** Autoritativer Lauf verglich
  sämtliche Stichproben nach Tick 1800 (6 Samples bis Tick 3600) und das
  Kettenende byteidentisch gegen den unterbrochenen Referenzlauf
  (`end=referenceEnd=f11cf610e76ab8b1`, `identical=true`); Fremdseed
  ändert den Endhash nachweislich (`foreign-seed-sensitivity`). Zusätzlich
  In-Prozess-Nachweis in `continuationEqualityAgainstReferenceRun`.
- **AC-T031-05 (Atomarität):** Fault-Injection in vier Phasen
  (temp-write, validation-read, validation, atomic-replace) lässt den
  letzten gültigen Stand intakt und ladbar, keine Tempdateien bleiben;
  Laden erfolgt ausschließlich in getrennten Zustand mit vollständiger
  Validierung vor Aktivierung (`slotProtocolFaultInjectionPreservesLastValidState`,
  `TryRestore`-Ankerprüfung).
- **AC-T031-06 (Korruptionsmatrix):** Alle DATENMODELL-Klassen führen zu
  unterscheidbaren Ablehnungen ohne Prozessabsturz und ohne Veränderung
  des Originals: TruncatedFile, MetaIntegrityViolation (falscher
  payloadHash), PayloadIntegrityViolation (Bitfehler), SchemaVersionUnsupported,
  MagicInvalid, CanonicalViolation, SizeLimitExceeded (übergroß),
  ReferenceInvalid (fehlende Referenz), LimitViolation; minimal gültig
  wird akzeptiert. „Finalitätsnah gültig“ bleibt vertraglich der
  Contentstufe vorbehalten (dokumentierte Zurückstellung).
- **AC-T031-07 (Migration):** Produktmigrator lehnt frühere/zukünftige
  Versionen ohne Migrationserfindung ab; No-op auf Kopie ist byteidentisch;
  synthetisches Zwei-Version-Fixturepaar (intern gekennzeichnet) belegt
  schrittweise Idempotenz und Originalerhalt auch bei Schrittfehler
  (`migrationRulesAreStrictlyMonotonicAndIdempotentOnCopy`).
- **AC-T031-08 (Diagnose statt Behauptung):** Dauern ausschließlich mit
  `gateCoupled=false` (Schema erzwingt false); allein der
  Größen-Sanity-Schwellwert entscheidet: Kalibrierbasis der Abnahmeläufe
  30040 Bytes in beiden Läufen identisch, Faktor 4 → Grenzwert 120160
  Bytes, absoluter Vorablimit-Abweisung vor Zuweisung. Q-TEC-006-Restoffen-
  heit und F-005-Anteiligkeit sind maschinenlesbar im Report ausgewiesen.
- **AC-T031-09 (Vertrauensgrenze):** Größenlimits vor Zuweisung,
  Pfadaustritts-/Symlink-Proben kontrolliert abgewiesen, Fehlermeldungen
  ohne interne Pfade jenseits des Diagnosefalls; security-Gate PASS.
- **AC-T031-10 (Gates, Byteidentität):** fmt angewendet; lint PASS
  (0 Befunde); Release-Build mit 0 Warnungen; Tests 218/218 (kein Test
  abgeschwächt oder entfernt; bestehende T-023-Allowlisten um den neuen
  Lauf-Treiber `SavecheckEngine.cs` im selben Muster wie T-022 ergänzt);
  security PASS; `rift.sh verify` valid=true. `Riftward.Simulation`
  byteidentisch unverändert: `git diff HEAD -- src/Riftward.Simulation/`
  leer (Blobvergleich gegen Basis-Commit `74ac4a0`); keine neue
  Abhängigkeit (BCL-only, architekturgetestet).
- **AC-T031-11 (Dokumentation/Register):** AUTOMATION.md (Befehlsvertrag),
  NATIVE_UNTERBAU.md (Exitcodes 33/34, Befehlsabschnitt), ARCHITEKTUR.md
  (Persistenzzeile und Content-/Persistenzbullet verweisen auf den
  Savevertrag ohne Stilllegung offener Fragen), DATENMODELL.md
  (SaveEnvelope- und Migrationsabschnitte verweisen auf Ort/Geltungsbereich,
  OFFEN-Stellen explizit gehalten), G-DATA-Eintrag in
  `.ai/evals/quality-gates.json` präzisiert, dieses Dokument angelegt.

## Fixture-Hygiene (Portabilitätsvertrag)

Alle testseitigen Fixtures sind versioniert-sanitisiert (Golden-Report als
bereinigtes Inline-Literal mit fixture-kernel/fixture-cpu/festen Zeitpunkten)
oder deterministisch im Test erzeugt (`buildValidDocument` aus festem Seed
und Tick). Kein Test liest `artifacts/` oder `.ai/runtime/`; gitignorierte
Runtime-Evidenz ist keine Voraussetzung eines schnellen Gates. Die frische
Review-Sitzung führt den Fresh-Checkout-/Clean-Archive-Nachweis gemäß
Präzedenz T-022 selbst aus, da dieser Lauf Testpfade berührt.

## Unabhängige Review-Sitzung (2026-08-26, Run `01M0XYZEQJSGJSFEHKJ4GVCV2N`)

Die frische Review-Sitzung übernahm keine Erfolgsbehauptung des Builder-
Laufs ungeprüft und führte alle Gates selbst aus. Befunde und Reparaturen,
allesamt im Scope des Primärslices T-031 und ohne Berührung eines zweiten
bereits akzeptierten Task-Manifests:

1. **Phantom-Flag im Vertragsbefehl (Doku-Defekt):** Savevertrag §10
   dokumentierte ein Flag `--continuation-ticks N`, das es in Runner,
   Usage, `rift.sh`-Hilfe, AUTOMATION.md und NATIVE_UNTERBAU.md nie gab.
   Der Fortsetzungshorizont ist vertraglich bereits als Resthorizont
   `planTicks − safeTick` (§7) festgelegt. Reparatur: Flag aus §10
   entfernt; die Abgrenzung zum implementierten CLI ist jetzt zeichen-
   gleich.
2. **Überversprochener Verzeichnis-Sync (Vertrags-/Implementierungs-
   defect):** Savevertrag §5 Schritt 4 behauptete einen Sync des
   Verzeichniseintrags nach der Ersetzung. Empirische Prüfung ergab, dass
   die BCL dafür kein Primitive besitzt (`FileStream` öffnet Verzeichnisse
   auf POSIX nicht; jeder erfolgreiche Slot-Schreibvorgang trug still eine
   Warnung). Ein Reparaturversuch über libc-P/Invoke wurde verworfen, weil
   der akzeptierte T-010-Architekturtest Native-Imports ausschließlich in
   der Plattformschicht zulässt (`architectureKeepsNativeImportsInsidePlatformLayer`,
   1 Befund gegen `src/Riftward.Save`). Reparatur: Sync-Schritt sauber aus
   Protokoll (`SlotStore`) und Vertrag entfernt und als explizites
   Restrisiko mit Rückrollweg dokumentiert (SAVEVERTRAG §5); AC-T031-05
   bleibt unangetastet — Tempdatei-Sync, Validierung vor Ersetzung und
   Umbenennungsatomarität sind die getesteten Garantien. Neuer Negativtest:
   Ein grünes Slotergebnis trägt niemals eine Warnung (kein teilweise
   ausgeführter Schritt maskiert sich als Erfolg).
3. **Spiegeltest mit toter Verzweigung (Testqualität):** Der Vertrags-
   spiegeltest übersprang den Dokumentpfad still über eine wirkungslose
   Doppelbedingung und prüfte `F005PartialStatement` gar nicht. Reparatur:
   Schleife auf die vier tatsächlich bindenden Kennungen gestellt
   (`EncodingId`, Q-TEC-006-, F-005- und Finalitäts-Aussage); der
   Dokumentpfad bleibt über die Zeichengleichheitsprüfung oben gebunden.

Eigene Evidenz dieser Sitzung: lint PASS (0 Befunde), Release-Build
0 Warnungen, Tests **218/218**, security PASS, autoritativer Lauf
`./scripts/rift.sh savecheck --report artifacts/t031-review/savecheck-review.json`
→ Exitcode 0, Gate pass, 19/19 Prüfklassen, Kalibrierbasis 30040/30040
Bytes konsistent (Faktor 4 → Grenzwert 120160 Bytes), `payloadHash`
`6ce087b6070e820c66b2435a756681ef9cf8489dbf9cc73c3be3ce5971f4899d`
(builderidentisch), Kettenende `f11cf610e76ab8b1` = Referenzende
(`identical=true`, 6 Stichproben nach Tick 1800), Report-SHA-256 siehe
Abschnitt „Autoritativer Review-Lauf“. `Riftward.Simulation`,
`docs/PERFORMANCE_BUDGET.md` und `.ai/tasks/` blieben byteidentisch
unverändert (`git diff HEAD` je leer).

## Autoritativer Review-Lauf

Nach Abschluss aller Reparaturen führte die Review-Sitzung den
autoritativen savecheck-Lauf am reparierten Kandidaten selbst aus:
`./scripts/rift.sh savecheck --report artifacts/t031-review/savecheck-review-final.json`
→ Exitcode 0, Gate pass, 19/19 Prüfklassen ohne Fehlklassen,
Kalibrierbasis 30040/30040 Bytes konsistent (Faktor 4 → Grenzwert
120160 Bytes), `payloadHash`
`6ce087b6070e820c66b2435a756681ef9cf8489dbf9cc73c3be3ce5971f4899d`
(builderidentisch), Fortsetzungskette byteidentisch zum unterbrochenen
Referenzlauf (`end=referenceEnd=f11cf610e76ab8b1`, 6 Stichproben nach
Tick 1800, `identical=true`). Report-SHA-256:
`35cb5f24376a16ece46b7a5b5dc3e8d254ed94e20b72feb62d2d95777768e28c`
(gitignorierte Laufzeitevidenz; reproduzierbar über den dokumentierten
Befehl). Danach folgte der Fresh-Checkout-/Clean-Archive-Nachweis am
hypothetischen Kandidatenbaum (Abschnitt unten).

## Fresh-Checkout-/Clean-Archive-Nachweis

Da dieser Lauf Test-, Fixture-, Build- und Evidenzpfade berührt, führte die
Review-Sitzung den Portabilitätsvertrag gemäß Präzedenz
`T-022-fresh-review` selbst aus, ohne Staging oder Commit und ohne
Berührung des echten Index: Aus HEAD `74ac4a0` plus Arbeitsbaum wurde per
indexfreier Git-Plumbing-Rekonstruktion der hypothetische Kandidatenbaum
`064fbfd061aa8f0aa50cc1d6cf4c6c5301e1e152` erzeugt (Baumdiff gegen HEAD
exakt die zweiunddreißig Auftragspfade, kein zweites Task-Manifest), per
`git archive` in ein isoliertes Verzeichnis ohne Laufzeitevidenz
extrahiert und dort der volle Gate-Zug ausschließlich aus diesen Bytes
gefahren: Harness-Init, Release-Build 0 Warnungen, lint PASS 0 Befunde,
Tests **218/218** Exitcode 0 (einschließlich aller T-031-Kontrakt-,
Korruptions-, Fault-Injection- und Fresh-Prozess-CLI-Tests),
assets-check PASS, rag-build PASS, `rift.sh verify` valid sowie ein
zusätzlicher autoritativer `savecheck`-Lauf aus den Archivbytes
(Exitcode 0, 19/19 Prüfklassen, `payloadHash` und Kettenende identisch zu
obigen Werten). Der Driftvergleich gegen eine zweite Referenzextraktion
desselben Baums zeigte nach dem gesamten Gate-Zug keine Abweichung in
getrackten Bytes (`NO_DRIFT`). Der finale Kandidat unterscheidet sich von
diesem geprüften Baum ausschließlich durch diesen Dokumentabschnitt;
die Promoter-Sitzung wiederholt den Pflichtgate-Zug am exakten Finalbaum.

## Bekannte Restpunkte

- F-005 bleibt anteilig offen: Der Payload deckt den Relevantzustand des
  Simulationskerns ab; die vollständige WorldState-/MissionState-Payload
  entsteht erst mit T-030/T-051-Inhalt.
- Q-TEC-006 bleibt OFFEN (Cooked-Paket-, Definitions-, Replayformat);
  Q-TEC-004 (Ratifizierung Simulationsvertrag) bleibt OFFEN;
  Q-GAM-001 bis Q-GAM-007/Q-NAR-002 unberührt.
- Cross-Build-/Cross-Plattform-Save-Kompatibilität ist nicht behauptet
  (entspricht Hashklasse-K3-Vorbehalt); Windows/macOS bleiben T-011.
- Durability: Ein Sync des Verzeichniseintrags nach der atomaren Ersetzung
  wird nicht durchgeführt (kein BCL-Primitive; Native-Imports bleiben auf
  die Plattformschicht beschränkt). Nach Geräteverlust im Ersetzungs-
  augenblick bleibt der alte oder neue Stand jeweils vollständig gültig;
  der Verlust des Umbenennens selbst ist Restrisiko (SAVEVERTRAG §5 mit
  Rückrollweg).
- Der autoritative savecheck-Report liegt unter gitignoriertem
  `artifacts/…` (SHA-256 im Abschnitt „Autoritativer Review-Lauf“); er ist
  reproduzierbar über den dokumentierten Befehl am eingefrorenen
  Kandidaten.
