# Abnahme T-036 – Graybox-Druck- und Neustartschritt

**Status:** Fertiggestellter Builder-Kandidat zur unabhängigen Reviewphase.
Der gatende Abschnitt 0 (versionierter Druckvertrag V1 samt autorisierter
additiver Zyklus-Präzisierung des Entscheidungsvertrags V2), der headless
prüfbare Fehlschlags-Neustart-Erfolgspfad, die Beobachtungstreue, die
Testmatrix (307/307) und alle lokalen Gates sind grün. Interaktivsmoke und
Playtestausführung bleiben wegen der displaylosen Umgebung ausgewiesene
Restpunkte mit kontrolliertem Code-19-Nachweis (Präzedenz
T-023/T-032/T-033/T-034/T-035). Diese Datei beschreibt die aktuelle
Produktwahrheit; sie behauptet keinen noch nicht ausgeführten Gate-Erfolg.

## Ausgangslage und Abschnitt 0

Der gatende Vertragsspike wurde vor der Implementierung abgeschlossen:

- `docs/DRUCKVERTRAG.md` V1 legt mit Alternativen, Gründen, Playtestkriterien
  und Rückrollweg fest: Auslöseregel `decision-coupled-window-v1` (erste
  Fensterinstanz genau an der Entscheidungsgrenze, weitere an der erneut
  wirksamen Wahl nach Wiederauffrischung; ehrliche not-started-Gründe),
  Zeitbasis `fixed-deterministic-tick-window-v1` mit der vorregistrierten
  Hypothese 600 Vorgrenzen (30 s bei 20 Hz; T-035-Referenzfluss 557
  Vorgrenzen; Alternative B distanzskaliertes Fenster dokumentiert; zufällige
  Längen verworfen), Fehlschlags- und Neustartregel
  `defined-failure-automatic-reopen-v1` mit `session-local-cycle-restart-v1`
  (drei ernsthafte Optionen; explizite Neustartaktion als v4-Obermenge als
  dokumentierte Alternative; straffreies Fenster und Sitzungsneuaufbau
  verworfen), Erfolgsregel `unchanged-decision-arrival-within-window-v1`
  (unveränderte T-035-Ankunftsregel, Einmalabschluss je Zyklus), Zwei-Kanal-
  Sichtbarkeit (`title-hud-pressure-window-v1`,
  `pressure-restart-indicator-channel-v1`), Aktivierung
  `opt-in-pressure-activation-v1` (`--pressure` gekoppelt an `--decision`,
  Usage-Bedeutung 2 ohne Kopplung), Reportbindung (additive Schemaversion 5,
  Pflichtblock `pressureSession`, relational fail-closed,
  `gateCoupled=false`), Nichtpersistenz
  `pressure-session-local-not-persisted-v1`, vorregistriertes
  Playtestprotokoll und Exitcode-Erhaltung.
- Die autorisierte additive Zyklus-Präzisierung des Entscheidungsvertrags
  ist als dessen Version 2 dokumentiert (`docs/ENTSCHEIDUNGSVERTRAG.md`,
  Abschnitt 13): Angebots-Einmaligkeit je Auftragszyklus mit definierter
  Wiederauffrischung nach definiertem Fehlschlag; Auslösung,
  Optionsableitung, Modus-Scoping und Ankunftsregel bleiben im Übrigen
  unverändert. Die Versionsbindung wurde im selben Kandidaten fortgeschrieben
  (`DecisionContract.ContractVersion` „2", Spiegeltest, Fixture-Regeneration
  der vertraglichen Versionsbindung); Skript-, Ketten- und Endhashbindungen
  der T-035-Flüsse bleiben unverändert gültig.

## Lieferumfang des Slices

- `src/Riftward.Session/PressureContract.cs` und
  `src/Riftward.Session/PressureSession.cs`: sitzungslokale Druckschicht —
  vertragliche Kennungen, Fensterprotokoll
  (`PressureWindowEvent`), Telemetrie, Beobachtung an der Vorgrenze in der
  festen Ordnung nach der Entscheidungsbeobachtung, Ablauf exakt an
  Start + 600, Erfolg vor Ablauf an derselben Grenze geprüft (Ankunft an der
  Ablaufgrenze ist die letzte Gelegenheit), kontrolliertes
  Zykluszurücksetzen über `DecisionSession.RestartCycle`, ehrliche
  Endstatuswerte.
- `src/Riftward.Session/DecisionSession.cs`: interne Testbindung
  `OpenOfferForContractTest` (Präzedenz `DeriveOptions`) und die vom
  Druckvertrag geforderte `RestartCycle`-Semantik; Sitzungsabweisungszähler
  bleiben Sitzungsgesamtwerte.
- `src/Riftward.Session/SessionEngine.cs`: `PressureEnabled` am Request mit
  fail-closed Kopplungsvalidierung (Druck ohne Entscheidung ist ein
  Vertragswiderspruch), Pipelineintegration der Druckbeobachtung nach der
  Entscheidungsbeobachtung, `Pressure`-Ausweis am Laufergebnis.
- `src/Riftward.App/Command/CommandLoopRunner.cs`: `--pressure`-Flag mit
  Usage-Kopplung (bestehende Bedeutung 2), Report-Schemaversion 5,
  `BuildPressureSession` (Fensterprotokoll, Zykluszählung, letzter
  Fehlschlag mit Ursache, Wiederauffrischungsgrenze, Endstatus,
  Nichtpersistenz, ehrliche Darstellungsausweise), Teilreport-Erhaltung
  (`ResolveIncompletePressure`), additive Titel-HUD-Segmente
  (` — Druck: Zyklus <n> Rest <r>` / ` — Druck: Fehlschlag: Zeit
  abgelaufen` / ` — Druck: Erfolg`).
- `src/Riftward.App/Command/CommandReportSchema.cs`: Version-5-Dispatch,
  `PressureSessionBody` mit Offen-Sentinel- und Nullable-Knoten,
  relationale fail-closed Bindungen, Messflag-Verdrahtung der neuen Kanäle.
- `src/Riftward.App/Command/InteractiveView.cs`: Neustartanzeige am
  Landmarkenanker der Folgenzone des fehlgeschlagenen Zyklus — zweistufige,
  klein-unten/groß-oben markierte Säule (1,5/3,0 m; 0,90/1,05) in warmem Rot
  (0,90/0,28/0,22), aktiv ab der Fehlschlagsgrenze bis zur nächsten wirksamen
  Wahl; MarkerCapacity +2. Die Anzeigezeitraum-Bindung liegt auf dem ehrlichen
  Neustarendzustand (`PressureSession.RestartPending`), sodass ein Erfolg
  eines Folgazyklus niemals eine rote Neustartanzeige erzeugt (Reparatur der
  unabhängigen Review vom 2026-08-29; Regression im Zeitbasistest gebunden).
- Tests: `tests/RiftHarness.Tests/PressureTests.fs` (12 Einheiten), Fixture
  `tests/fixtures/command/t036-pressure-restart.graybox` (deterministisch,
  versioniert), Fortschreibung der Bestandsschemamatrix (Golden-Mutation
  2→6 gemäß T-035-Präzedenz) und der Entscheidungsvertragsversionsbindung.

## Vertragskern: Fehlschlags-Neustart-Erfolgspfad

Gebunden an der Fixture `t036-pressure-restart.graybox` (Seed 20260826,
Horizont 11000): Angebot an 7210, Wahl `a` an 8000 (Folgenzone A = Zone der
zuerst registrierten Landmarke), Fenster 1 öffnet an 8000, der Held bleibt
in der zuletzt registrierten Zone, Ablauf exakt an 8600 mit Ursache
`window-expired-without-arrival`, Angebots-Wiederauffrischung exakt an 8601,
Wahl `b` an 9200 (Folgenzone B = Heldenzone), Fenster 2 öffnet an 9200 und
schließt mit persönlicher Ankunft an derselben Grenze als Erfolg,
Endstatus `success`, `cycleCount` = 2, kein Kernbefehl der Druckschicht.

## Evidenz

- Testbestand 307/307 (295 Bestand + 12 neue Druckeinträge), Release-Build
  mit 0 Warnungen, fmt/lint 0 Befunde, security PASS, rag-build, `verify`
  valid (runsChecked=67).
- Regressionsläufe der Bestandsbefehle: `bench-sim` gate.pass=true (p99
  0,53 ms, 0 Bytes je warmem Tick), `savecheck` gate.pass=true mit 19/19
  Prüfklassen, Soak-Kurzlauft 3000 Ticks diagnostisch (evidenceUnit=false)
  gate.pass=true.
- Autoritative CLI-Läufe: Fresh-Prozesspaar builderidentisch (Endhash
  `8b4767bf5a75abb8`, identisches Druckprotokoll), Fremdseed 7 ändert Start-
  und Endhash nachweislich (`42adf741dc76cca0`) bei strukturgleichen
  Protokollinvarianten, Schemaversion 5, Exitcode 0.
- In-process-Zwilling: Druckschicht ohne Aktivierung erzeugt byteidentische
  Ketten und denselben Endhash; A/B-Wahlpaar bleibt ketten- und
  endhashidentisch bei vertraglich unterschiedlichen Druckwahrheiten
  (B: Erfolg an der Entscheidungsgrenze; A: definierter Fehlschlag mit
  Wiederauffrischung); T-035-Vollfluss bleibt endhashidentisch und schließt
  innerhalb des offenen Fensters als Erfolg ab.
- Blobvergleich: alle Quelldateien von `Riftward.Simulation` sind gegen den
  Vorblob (HEAD) byteidentisch (`git hash-object` je Datei gegen
  `git rev-parse HEAD:<Pfad>`, keine Abweichung).
- Displayloser Interaktivlauf: kontrollierter Code-19-Abbruch
  (`WindowFailed`, „No available video device") ohne Report; kein simulierter
  Beweis.

## AC-Abdeckung

- **AC-T036-01** (Abschnitt 0 vor Implementierung): DRUCKVERTRAG.md V1 und
  Entscheidungsvertrag V2 Abschnitt 13 mit allen geforderten Optionen,
  Empfehlungen, Playtestkriterien und Rückrollwegen; Spiegeltest hält Code
  und Dokument konsistent.
- **AC-T036-02** (Headless Druck-Flow): Fehlschlags-Neustart-Erfolgspfad
  über denselben Befehl und dasselbe Skriptformat, maschinenlesbares
  Druckprotokoll mit `gateCoupled=false`, Dual-Prozess- und
  Fremdseed-Nachweise, Legacy-Skripte byteidentisch gültig.
- **AC-T036-03** (Beobachtungstreue): Zwilling byteidentisch, A/B-Paar und
  Vollfluss ketten- und endhashidentisch mit rein additiven Druckfeldern,
  Blobvergleich Riftward.Simulation, keine neuen Exitcodebedeutungen.
- **AC-T036-04** (Interaktiv): additive Titel-HUD-Segmente und
  Neustartanzeige mit zwei visuellen Kanälen (Form plus Farbe) an den
  bestehenden Mustern; Headless-Ausweise mit Grund; Interaktivsmoke und
  Playtestausführung sind ausgewiesene Restpunkte (displaylose Umgebung,
  Code-19-Nachweis ohne Simulation).
- **AC-T036-05** (Vertrauensgrenzen): untrusted Skriptpfad unverändert,
  Druckschicht nur an vertraglichen Vorgrenzen wirksam, ohne Kernbefehl;
  Schreibzugriffe auf Reportpfad; kein Netzwerk, keine Secrets; Architektur-
  grenzen durch die Bestandsarchitekturtests und die Blobbindung gehalten.
- **AC-T036-06** (Gates): bootstrap/build/fmt/lint/test/security/verify
  grün, 0 neue Warnungen, keine neue Abhängigkeit; Regressionsläufe grün;
  Doku (DRUCKVERTRAG, ENTSCHEIDUNGSVERTRAG V2, AUTOMATION, ARCHITEKTUR)
  bildet den implementierten Stand ab; Playtestprotokoll vorregistriert,
  Ausführung als displayloser Restpunkt ausgewiesen.

## Restpunkte

- Interaktivsmoke, Playtestausführung und der opt-in Einzelabgriff des
  Fehlschlags-/Neustartzustands bleiben einer Displaysession vorbehalten
  (kontrollierter Code-19-Abbruch ohne Report statt Simulation); ohne
  produzierten Abgriff entsteht kein Media-Lab-Eintrag.
- Pflichtprofile bleiben `NOT-MEASURED` (Q-OPS-001); der Slice erzeugt keinen
  neuen budgettragenden Pfad.
- Q-GAM-001 bis Q-GAM-007, Q-GAM-010, Q-NAR-002, Q-NAR-004, Q-TEC-004,
  Q-TEC-006 und Q-TEC-010 bleiben ausdrücklich `OFFEN`.
