# Abnahme T-037 – Graybox-Fortsetzungsschritt (Save/Load über die Prozessgrenze)

**Status:** Implementierter Kandidat zur frischen Review. Der gatende
Abschnitt 0 (versionierte Savevertrags-Erweiterung V2 samt autorisierter
additiver Persistenz-Präzisierung der vier Sitzungsverträge), der headless
prüfbare Fortsetzungspfad über die Prozessgrenze, die interaktive
Slot-Fähigkeit, die Beobachtungstreue, die Testmatrix (317/317) und alle
lokalen Gates sind grün. Interaktivsmoke und Playtestausführung bleiben wegen
der displaylosen Umgebung ausgewiesene Restpunkte mit kontrolliertem
Code-19-Nachweis (Präzedenz T-023/T-032/T-033/T-034/T-035/T-036). Diese Datei
beschreibt die aktuelle Produktwahrheit; sie behauptet keinen noch nicht
ausgeführten Gate-Erfolg. Die unabhängige Review-Sitzung (2026-08-29) hat den
Sektions-Decoder um zwei Rahmengrenzen-Prüfungen vor den ungezielten
Skalarlesungen ergänzt (Abschnitt 13.5: jede Verletzung erhält eine
unterscheidbare Klasse; Repro: zwei erreichbare Byteformen warfen zuvor eine
unkontrollierte Ausnahme), die Fenster-/Instanzkonsistenz um die Regel
verschaerft, dass eine offene Instanz stets die letzte im Protokoll ist
(validierungsstaerke nach Abschnitt 13.1; Vertragswerte unverändert), und die
Korruptionsmatrix um genau diese drei Fälle erweitert; Encoding, Ketten- und
Endhashs sind durch die Reparatur unverändert (Evidenzläufe unten auf dem
reparierten Kandidaten).

## Ausgangslage und Abschnitt 0

Der gatende Vertragsspike wurde vor der Implementierung abgeschlossen:

- `docs/SAVEVERTRAG.md` V2 (Abschnitt 13) legt mit Alternativen, Gründen,
  Playtestkriterien und Rückrollweg fest: Sektionsaufbau
  `session-section-full-state-v1` (vollständiger Sitzungszustand an einer
  Vorgrenze als eine versionierte, eigen-hashgebundene additive Sektion im
  bestehenden atomaren Umschlag; dokumentierte Alternativen Minimal-/Teil-
  zustand und Ereignisprotokoll-Wiederaufbau; jede Umdeutung bestehender
  Felder verworfen), headless Aktivierungsform `opt-in-continuation-flags-v2`
  (Befehlsflags am bestehenden `kommandoschleife`-Befehl; Skriptgrammatik-v4
  als dokumentierte Alternative, separates Untercommand verworfen),
  interaktive Aktivierungsform `opt-in-interactive-slot-actions-v2` (genau
  zwei frei belegbare Keymap-Aktionen; kontextgebundener Einzelbefehl als
  dokumentierte Alternative, Text-/Menüfläche verworfen), Modulgrenze
  `session-section-codec-boundary-v2` (additive Codecfläche neben der
  bestehenden Saveverträglichkeit, gespeist aus der kanonischen
  Sitzungsserialisierung; opake Bytesektion verworfen, weil die T-031-
  Prüfklassen uneingeschränkt für die Sektion gelten müssen),
  Umschlag V2 mit V1-Kompatibilität `legacy-v1-session-emptiness-v2`
  (strikte Monotonie {1, 2}, keine Migrationserfindung), Aktivierungsgrenzen
  `untrusted-slot-activation-guards-v2`, unterscheidbare Sektionsklassen
  `SESSION_SECTION_INTEGRITY_VIOLATION`/`SESSION_SECTION_INVALID`,
  Persistenz-Präzisierungen der vier Sitzungsverträge mit ausdrücklicher
  Replay-Ausnahme, vorregistriertes Playtestprotokoll (Abschnitt 13.7) und
  Exitcode-Erhaltung (Abschnitt 13.8).
- Die vier autorisierten additiven Persistenz-Präzisierungen sind als
  versionierte Zusatzabschnitte dokumentiert: Modevertrag V2 (Abschnitt 12),
  Erkundungsvertrag V2 (Abschnitt 10), Entscheidungsvertrag V3 (Abschnitt 14)
  und Druckvertrag V2 (Abschnitt 14). Die Versionsbindungen wurden im selben
  Kandidaten fortgeschrieben (Spiegeltests, Fixture-Regeneration der
  gebundenen Nichtpersistenzaussagen); Skript-, Ketten- und Endhashbindungen
  der T-032- bis T-036-Flüsse bleiben unverändert gültig.

## Lieferumfang des Slices

- `src/Riftward.Save/SessionSectionState.cs` und
  `src/Riftward.Save/SessionSectionCodec.cs`: kanonische Sitzungssektion
  (`riftward-session-section-canonical-binary-v1`, Sektionsversion 1) mit
  striktem Einzelpass-Decoder (Grenzen vor Zuweisung, exakter Byteverbrauch,
  Re-Encoding-Gleichheit, Relations- und Referenzwahrheiten) und
  unterscheidbaren Verletzungsklassen.
- `src/Riftward.Save/CanonicalSaveCodec.cs`: Umschlag V2
  (`WriteDocumentV2`: erweiterter Kopf um `sessionSectionLength` +
  `sessionSectionHash`, Framing `Vorspann|Kopf|metaHash|Payload|Sektion`) und
  byteidentische Legacy-V1-Erzeugung (`WriteDocumentV1`, savecheck-Verhalten
  unverändert).
- `src/Riftward.Save/SaveDocumentValidator.cs`: Versionsdispatch {1, 2},
  Sektionsprüfung (Anker, Kanonform, Grenzen, Referenzen) in der vertraglichen
  Prüfreihenfolge, ehrliche V1-Sitzungsleere mit Ursprungsmarker, neue
  Verletzungsklassen.
- `src/Riftward.Save/SaveMigrator.cs`: unterstützte Versionsmenge {2, Legacy 1}
  als identische No-op-Erreichbarkeit; Zukünftiges bleibt ohne Erfindung
  abgewiesen.
- `src/Riftward.Save/SaveContract.cs`: V2-Kennungen, Aktivierungs- und
  Modellkennungen, Report-Schemaversion 6, Keymap-Aktionsnamen (F5/F9),
  maschinenlesbare Aussagen.
- `src/Riftward.Session/SessionStateCapture.cs` und Restore-Fabriken
  (`ExplorationSession.Restore`, `DecisionSession.Restore`,
  `PressureSession.Restore`): Erfassung und Wiederherstellung der
  Kettenwahrheit je Schicht; `SessionPipeline`-Wiederherstellung (Startmodus,
  schwebende Wechsel) mit unverändertem Bestandskonstruktor.
- `src/Riftward.Session/SessionEngine.cs`: `RunWithSaveBoundary` (Speicherlauf
  bis zur Vorgrenze mit vertragsgleichem Messfenster und
  Selbstkonsistenzpass bis zur Speichergrenze) und `RunFromSessionSave`
  (Fortsetzungslauf mit vollständig restauriertem Zustand und
  Kettenfortsetzungsvergleich gegen die unterbrochene In-Prozess-Referenz:
  sämtliche Stichproben nach der Ladegrenze, aligned Anker, Kettenende —
  T-031-Fortsetzungsketten-Präzedenz).
- `src/Riftward.App/Command/ContinuationRunner.cs`: Slot-Nahtstelle (atomares
  Schreiben des V2-Dokuments; Laden mit vollständiger Validierung und
  Aktivierungsgrenzen `foreign-world-id`/`foreign-seed`/
  `unsupported-schema-version`/Sektionsklassen).
- `src/Riftward.App/Command/CommandLoopRunner.cs`: `--slot-dir`/`--slot`/
  `--save-at-tick`/`--load-slot` mit Usage-Kopplungen, headless
  Speicher-/Fortsetzungslauf, ehrliche Messfeld-Overrides, Teilreportpfad mit
  maschinenlesbarer Ablehnung (Code 36), interaktive Slot-Aktionen mit
  kontrolliertem Kontextwechsel (Welt, Sitzungsschicht, Pipeline, View-
  Bindung, ehrlicher Kettenneustart im Messausweis) und sichtbarer
  Ablehnung ohne Welt-/Kettenänderung, Report-Schemaversion 6 mit dem
  Pflichtblock `continuation`.
- `src/Riftward.App/Command/Keymap.cs`: `save-slot` (F5, 58) und
  `load-slot` (F9, 62) in der bestehenden Familie mit unveränderten
  Validierungsregeln.
- `src/Riftward.App/Command/CommandReportSchema.cs`: Version-6-Dispatch,
  `ContinuationBody` mit strikten runKind-Alternativformen,
  `ValidateContinuationRelations`, optionale Sitzungsblöcke der Schichten,
  Persistenzwahrheit V2/V3 in den drei Schichtblöcken.
- Tests: `tests/RiftHarness.Tests/ContinuationTests.fs` (10 Einheiten:
  Sektions-Codec-Roundtrip und Ablehnungsmatrix inklusive der beiden
  Rahmenniveau-Truncationsfälle der Review-Reparatur, V2-Umschlag mit
  V1-Leere und Versionsmonotonie, Aktivierungsgrenzen, Kettenfortsetzung
  über die Vorgrenze, Fremdseed, CLI-Fresh-Prozesspaare builderidentisch,
  CLI-Ablehnungsmatrix mit stabilen Exitcodes, Vertragsankerbindung),
  Fortschreibung der Spiegel- und Persistenzbindungen in Save-/Mode-/-
  Exploration-/Decision-/Pressure-Tests und der Bestandsschemamatrix
  (Golden-Mutation 6→7 gemäß T-035/T-036-Präzedenz).

## Vertragskern: Fortsetzung über die Prozessgrenze

Gebunden an der Fixture `t036-pressure-restart.graybox` (Seed 20260826,
Horizont 11000): Der Speicherlauf endet an der Vorgrenze 8100 — nach der
wirksamen Wahl `a` an 8000, mit offenem Druckfenster (Zyklus 1) und
persönlichem Modus. Der Fortsetzungslauf ist ein frischer Prozess: Er lädt den
Slot, validiert ihn vollständig vor Aktivierung, stellt Welt, Modus und alle
drei Schichten wieder her und führt dieselbe Skriptausführung fort — der
gebundene Fehlschlag an 8600 mit Ursache
`window-expired-without-arrival`, die Wiederauffrischung an 8601, die Wahl
`b` an 9200 und der Erfolg an der persönlichen Ankunft liegen vollständig in
der Fortsetzung (`cycleCount` = 2, Endstatus `success`). Die Fortsetzungskette
ist ab der Ladegrenze byteidentisch zur unterbrochenen Referenz (48
verglichene Stichproben, Referenz- und Fortsetzungsendhash
`8b4767bf5a75abb8` — identisch zum dokumentierten T-036-Endhash derselben
Fixture).

## Evidenz

- Testbestand 317/317 (307 Bestand + 10 neue Fortsetzungseinträge),
  Release-Build mit 0 Warnungen, fmt/lint 0 Befunde, security PASS,
  rag-build, `verify` valid (runsChecked=68).
- Regressionsläufe der Bestandsbefehle auf dem reparierten Kandidaten:
  `bench-sim` gate.pass=true (p99 0,491 ms, 0 Bytes je warmem Tick; der
  erste Lauf desselben unveränderten Kandidaten verfehlte die 0-Byte-Grenze
  mit 0,84 B/Tick als Sub-Kilobyte-Runtime-Ausreißer außerhalb des
  Tick-Pfads und wurde transparent wiederholt), `savecheck` gate.pass=true
  mit 19/19 Prüfklassen (Verhalten unverändert, Vertragsversionsbindung
  fortgeschrieben auf V2), Soak-Kurzlauf 3000 Ticks diagnostisch
  (`--diagnostic-accelerated --horizon-ticks 3000`, evidenceUnit=false)
  gate.pass=true.
- Autoritative CLI-Läufe (artifacts/t037/continuation-evidenz/): Speicherlauf
  Schemaversion 6, gate.pass=true, Speichervorgrenze 8100, Slot geschrieben,
  Endhash `e660daf4a5eb10c4`; Fortsetzungslauf Schemaversion 6, gate.pass=true,
  Kettenfortsetzung verifiziert (48 Stichproben), restaurierte Kettenwahrheit
  (Modus `personal`, Entscheidung gewählt, Zyklus 1) und vollständiger
  Fehlschlag-Neustart-Erfolgspfad in der Fortsetzung; zwei unabhängige
  Fresh-Prozesspaare sind builderidentisch (Ketten- und
  Fortsetzungsidentität im Test gebunden).
- Fremdseed 424242 am CLI: kontrollierte Ablehnung `foreign-seed` mit Code 36
  ohne Aktivierung; fehlender Slot: kontrollierte Ablehnung `slot-unreadable`
  mit Code 36; kein Teilreport gilt als Evidenz.
- In-process-Nachweise (Testmatrix): Kettenfortsetzung byteidentisch,
  Fremdseed ändert Start-/Endhash nachweislich bei gültiger Fortsetzung,
  V1-Dokument lädt mit ehrlicher Sitzungsleere und unveraenderter Kette,
  Versionen 0/3 ohne Migrationserfindung abgewiesen, Fault-Injection-Matrix
  der Sektion (Version, Abschneidung, fremde Zone, Doppelregistrierung,
  strategische Registrierung, beide Rahmenniveau-Truncationsfälle,
  offene Fensterinstanz vor geschlossenen), Aktivierungsgrenzen am Slot.
- Blobvergleich: alle Quelldateien von `Riftward.Simulation` sind gegen den
  Vorblob (HEAD 66051d7) byteidentisch (`git hash-object` je Datei gegen
  `git rev-parse HEAD:<Pfad>`, 9/9 Dateien, keine Abweichung).
- Portabilitätsprobe (Fresh-Bytes, contractäquivalent vor dem autorisierten
  Commit): der Kandidat wurde in ein isoliertes Verzeichnis materialisiert
  (HEAD-Archiv plus bytegeprüftes Kandidaten-Overlay) und durchlief dort
  bootstrap/build/lint/test (317/317)/rag-build/verify grün; keine
  gitignorierte Runtime-Evidenz ist Test-Fixture, der Baum blieb über die
  Gates bytestabil. Der formale Fresh-Checkout-Gate-Lauf bleibt dem
  autorisierten Commit der Integration vorbehalten.
- Displayloser Interaktivlauf: kontrollierter Code-19-Abbruch
  (`WindowFailed`, „No available video device") ohne Simulation; kein
  simulierter Beweis.

## AC-Abdeckung

- **AC-T037-01** (Abschnitt 0 vor Implementierung): SAVEVERTRAG.md V2
  Abschnitt 13 mit allen geforderten Optionen, Empfehlungen,
  Playtestkriterien und Rückrollwegen; die vier autorisierten
  Persistenz-Präzisierungen (Modevertrag V2, Erkundungsvertrag V2,
  Entscheidungsvertrag V3, Druckvertrag V2) mit Vertragsversion und
  Fixture-Regeneration; Spiegeltests halten Code und Dokumente konsistent;
  Q-GAM-001 bis Q-GAM-007, Q-GAM-010, Q-NAR-002, Q-NAR-004 und Q-TEC-006
  bleiben ausgewiesen `OFFEN`.
- **AC-T037-02** (Headless Fortsetzungspfad): Speicher- und Ladegrenze,
  wiederhergestellte Kettenwahrheit und Fortsetzungsidentität maschinenlesbar
  rein additiv (Schemaversion 6, `gateCoupled=false`), Kettenfortsetzung
  byteidentisch zur unterbrochenen Referenz (Bestandskriterium 5 fail-closed),
  zwei builderidentische Fresh-Prozesspaare, Fremdseed ändert Hashes nie die
  restaurierte Wahrheit, V1-Slots mit ehrlicher Leere, Skriptgrammatik
  unveraendert, keine neuen Exitcodebedeutungen.
- **AC-T037-03** (Vertrauensgrenzen und Beobachtungstreue): Blobvergleich
  Riftward.Simulation byteidentisch; ohne Aktivierung byteidentischer
  Bestandsstand (Legacyskripte v1/v2/v3 und Schichtfixtures mit identischen
  Ketten und Endhashs in der Bestandsmatrix); T-031-Garantien gelten
  uneingeschränkt für die Sektion (Fault-Injection-Matrix, unterscheidbare
  Klassen); untrusted Slots werden vollständig validiert (fremde Welt,
  fremder Seed, spätere Version, abgeschnittene/manipulierte Sektion);
  Schreibzugriffe ausschließlich vertragliche Verzeichnisse; kein Netzwerk,
  keine Secrets; Sitzungskern frei von SDL3-/bgfx-/Betriebssystemtypen,
  Runtime-Hotpaths in C#, F#/Python fern vom Laufzeitpfad; kein Budgetwert
  geändert.
- **AC-T037-04** (Interaktiv spielbarer Fortsetzungspfad): Keymap-Aktionen
  `save-slot`/`load-slot` in der bestehenden Familie, kontrollierter
  Kontextwechsel nach vollständig validierter Ladung, Titel-HUD-Ausweis der
  restaurierten Wahrheit ohne Tastendruck, unterscheidbare Ablehnung ohne
  Welt-/Kettenänderung, ohne Display kontrollierter Code-19-Abbruch; das
  vorregistrierte Playtestprotokoll bleibt als displayloser Restpunkt
  ausgewiesen (Präzedenz T-023 bis T-036).
- **AC-T037-05** (Gates und Regressionen): bootstrap/build/fmt/lint/test/
  security/verify grün, 0 neue Compiler-/Analyzer-Warnungen, keine neue
  Abhängigkeit; Regressionsläufe (bench-sim, savecheck inklusive
  Bestandsfixtures, Soak-Kurzlauf, kommandoschleife mit Legacy- und
  Schichtskripten) grün; alle bestehenden Exitcodebedeutungen unverändert;
  SAVEVERTRAG V2, die vier Präzisierungen, AUTOMATION.md und ARCHITEKTUR.md
  bilden den implementierten Stand zeichentreu ab; dieses Abnahmedokument
  verknüpft jedes Kriterium mit Evidenz.

## Restpunkte

- Interaktivsmoke, Playtestausführung und der opt-in Abgriff bleiben einer
  Displaysession vorbehalten (kontrollierter Code-19-Abbruch ohne Simulation);
  ohne produzierten Abgriff entsteht kein Media-Lab-Eintrag.
- Pflichtprofile bleiben `NOT-MEASURED` (Q-OPS-001); der Slice erzeugt keinen
  neuen budgettragenden Pfad.
- Die Auswahl bleibt vertraglich darstellseitig und ist nicht Teil der
  Sektion; die Fortsetzungsskripte reichen ihre Auswahl nach der Ladegrenze
  selbst wieder auf (Savevertrag V2 Abschnitt 13.1, dokumentierte
  Entscheidung).
- Q-GAM-001 bis Q-GAM-007, Q-GAM-010, Q-NAR-002, Q-NAR-004, Q-TEC-004,
  Q-TEC-006 und Q-TEC-010 bleiben ausdrücklich `OFFEN`.
