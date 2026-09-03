# Forschungsprotokoll-Changelog

Alle Protokollaenderungen werden vor dem naechsten prospektiven Ereignis
versioniert. Bereits erfasste Beobachtungen behalten ihre urspruengliche
Version und ihren Bundle-Hash.

## 2.0.1 - 2026-09-03

**Status:** praeregistriertes Amendment vor P-001; weiterhin kein
prospektives Ereignis erhoben.

- den kanonischen `studyManifestSha256` als Pflichtpayload sowohl in
  `observation.started` als auch in `observation.closed` aufgenommen,
- Ledgerpruefung, Export und Exportpruefung auf identische Start-/Closure-/
  Eingabemanifestbindung fail-closed verschaerft, damit eine nach dem Close
  geaenderte, weiterhin feldgueltige Manifestkopie keinen Export erzeugt,
- das Study-Manifest auf seine dokumentierten Pflichtfelder sowie die beiden
  optionalen longitudinalen Felder `windowStartUtc` und `windowEndUtc`
  geschlossen; unbekannte Felder werden vor Persistierung oder Export
  abgelehnt,
- adversariale Secret-/PII-/Absolutpfad-Felder und Manifestdrift nach Closure
  als Negativtests aufgenommen.

Das Amendment aendert keine Forschungsfrage, Hypothese, Primaermetrik,
Evidenzklasse, T-042-Startbedingung oder Ergebnisregel. Da P-001 noch nicht
begonnen hat, existiert keine prospektive 2.0.0-Beobachtung, die umgedeutet
werden koennte. 2.0.0 bleibt als historischer Section-0-Freeze erhalten; der
erste zulaessige P-001-Start muss 2.0.1 und dessen neuen Bundle-Hash binden.

## 2.0.0 - 2026-09-02

**Status:** praeregistriert; noch kein prospektives Ereignis erhoben.

- den explorativen Plan 0.1 in einen pruefbaren T-053-Protokollbundle
  ueberfuehrt,
- die Evidenzklassen `retrospective-derived`, `prospective-observed` und
  `synthetic-test-only` eingefuehrt,
- literal `unknown` als einzigen fehlenden Wert festgelegt und `null`,
  Schaetzung als Ersatz sowie fehlendes Signal als Erfolg ausgeschlossen;
  Kostenprovenienz `estimated` bleibt getrennt von exakten Kosten,
- append-only Ereignismodell, Quellenreferenzen, Hashkette, stabile
  pseudonyme Actor-/Agenten-ID sowie Retry-/Repairindizes praeregistriert,
- strukturierte Harnessgrenzen und die CLI-Vertraege `research status`,
  `begin`, `verify`, `export`, `summarize`, `intervention`, `close` und
  `import-git-history` festgelegt; `begin` schreibt Protokoll-/Startkette und
  einen per Datei-/Directory-fsync und Reopen bestaetigten Active-Marker vor
  dem ersten Zielereignis, `close` bindet einen strukturierten Outcome-Receipt
  und entfernt den Marker idempotent mit Directory-fsync; unvollstaendige
  Aktivierung und Stale-Marker werden fail-closed rekonsiliert, freie Logs
  bleiben ausschliesslich supplemental,
- exklusiven Writerlock, atomaren fsync-Append und fail-closed Torn-Tail-
  Recovery in eine neue Datei ohne stille Trunkierung festgelegt,
- Crash-Injection-Grenzen vor/nach Marker-Rename sowie vor/nach Marker-Unlink
  mit Unveraendertheits- und No-retroactive-claim-Nachweisen festgelegt,
- das Ereignisregister auf allgemeine longitudinale SDLC-Ketten erweitert:
  Autopilot, Eltern-/Kindlaeufe, Taskphasen, WIP, Gates und explizite
  Pipelinefehler, Reparatur, Compaction/Resume, Routing/Modellwechsel,
  Budget-/Rate-/Provider-/Infrastrukturblocker, Git-Evolution und menschliche
  Instruction/Review/Correction/Approval/Emergency/Observation,
- Study-/Run-/Cycle-/Task-IDs, monotone Zeit, Provider/Modell/Modellversion,
  Branch/Base/Head/Tree, getrennte Autonomie-/Aktivitaetszustaende,
  Exit-/Fehlerdaten, Usage-/Kostenprovenienz, repo-relative Changed Paths,
  menschliche Aktivzeit sowie Privacy-/Redactionstatus als Huellenfelder
  festgelegt,
- exklusive Interventionskategorien I0 bis I11 einschliesslich Domaenen- und
  Prioritaetsentscheidung, Defektbericht, technischem Unblock,
  Infrastruktur, Review/Promotion, Notstopp und reiner Beobachtung sowie
  explizite `start`/`end`/`record`-Semantik definiert; offene Dauer bleibt
  literal `unknown`,
- exakte agentische, Gate-, Aufwand-, Nachvollziehbarkeits-, Architektur- und
  longitudinale Lifecycle-/Blocker-/Routing-/Repair-/Promotionmetriken
  festgelegt,
- diagnostische Architekturcheckpoints mit Datei-/Testzeilen, Top-10-Groesse
  und Wachstum, Komponentenanteilen, Dependencyrichtungen,
  Grenzverletzungen, Analyzerwarnungen, Testwachstum,
  Integrationspunktkonzentration fuer CommandLoopRunner,
  CommandReportSchema/SessionEngine und optionaler Complexity festgelegt,
- Milestone-/Tag-, First-pass-Review-, Defect-Escape-, Rework-/Rollback-,
  WIP/Accept-, Discarded-Work-, Gate-Recovery-, Prod/Test-Verhaeltnis- und
  Aufwand-je-Accepted-Metriken praeregistriert,
- Accepted-/Milestone-pro-Tag, Files/Lines-pro-Accepted, nur spaeter
  akzeptierter Arbeit zurechenbare produktive Autonomie sowie getrennte
  exakte/estimated Recoverykosten praeregistriert,
- fuer jeden akzeptierten Baum einen diagnostischen Architekturcheckpoint mit
  `gateCoupled=false` verlangt,
- deterministische private und oeffentliche Exporte mit
  `study-manifest.json`, `report.md` und kreisfreiem aeusserem
  `EXPORT.SHA256` spezifiziert und an Ergebnis-/Input-Tree gebunden,
- den kuenftigen WIP-Provenienz-Sidecar ohne Historienumschreibung oder
  direkte `main`-Autoritaet festgelegt,
- T-037 als rein retrospektive Kalibrierung und T-042 als bedingten ersten
  prospektiven Echtlauf festgelegt, ohne beide als A/B-Paar zu behandeln,
- drei einzeln isolierte `synthetic-test-only`-Ablationen praeregistriert,
- konzeptuelle spaetere Full-, No-Persistent-RAG/Memory-, Single-Session-,
  Review-on/off- und Model-Routing-Experimente unter identischer
  Baseline/Budgetbindung ohne Auto-Main vorregistriert,
- eine Pflicht-Negativmatrix fuer Roundtrip, Ketten-/Exporttampering,
  IDs, Usage/Model-Unknowns, Resume/Torn Tail/Writer/Uhr, offene
  Intervention, Redaction, Git-Import, Evidenzklassen und Determinismus
  festgelegt,
- Reproduzierbarkeit, Validitaetsbedrohungen sowie Datenschutz-, Redaktions-
  und Publikationsgates als eigene Begleitvertraege aufgenommen,
- Nichtinterferenz zu bestehenden Task-, Gate-, T-037- und T-042-Vertraegen
  als Primaerkriterium festgelegt,
- T-053-Abschluss an einen tatsaechlichen P-001-Echtlauf und buildergetrennten
  unabhaengigen `PASS` auf dem exakten Ergebnisbaum gebunden; ein
  nicht-startberechtigtes T-042 oder Restpunkt laesst T-053 unfertig.

## 0.1 - 2026-08-23

**Status:** explorativer Ausgangsplan.

- Forschungsfragen RQ-01 bis RQ-06, grobe SDLC-/Spielmetriken,
  Compute-Amortisierungshypothese, Vergleichsgrundsaetze und ein allgemeines
  Evidenzpaket dokumentiert.
- Noch keine verbindliche Evidenzklassifikation, Feldsemantik,
  Interventionsontologie, Exportform, Unknown-Regel oder prospektive
  Beobachtung praeregistriert.
