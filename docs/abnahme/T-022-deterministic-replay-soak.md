# T-022 – Deterministischer 8-Stunden-Replay-Soak

**Status:** `ACCEPTED` (Abnahme durch unabhängige Review-Sitzung nach
vollständigem Wiederholungsbündel von drei Evidenzeinheiten, 2026-08-25)
**Umsetzung:** Autonomer Implementierungslauf `01M0VF1GWE15KBDR5Z28N1C225`
(Akteur `t022-implementer`, Modell `stealth/ox-alpha`), 2026-08-25

## Review-Verlauf (unabhängige Review-Sitzungen)

- **2026-08-25, Review-Sitzung nach Langzeitjob `t022-evidence-rep1`:**
  Job `SUCCEEDED` bei Exitcode 0 mit identischen Start-/End-/Aktuell-
  Repository-Fingerprints (`d92403a5f5627937f80fbab16a92f53d1b86e6a9b7bfd65afb67096e65ff219d`)
  und eigenständig verifizierter Logbindung (stdout-SHA-256 stimmt,
  stderr leer). Der Report
  `artifacts/t022/bundle/repetition-1.json` wurde als Evidenzeinheit des
  Soakvertrags V2 geprüft: vollständig über 576480/576480 Ticks
  (einschließlich Warm-up), Gate pass ohne Verletzung, absolutes
  Working-Set-Wachstum 2460 KiB bei 16384 KiB Grenze, Trend im Limit,
  0 Bytes je strengem Prüftick, Watchdog (120 s) ohne Stall
  (größte Lücke 0,304 s), alle 18 Kettenstichproben byteidentisch zur
  versionierten Golden-Fixture (SHA-256 `5e1a6a20cb7e46adf2a1f37679a15ab66766decedb3a555480783a3767a4dd76`),
  Profile `NOT-MEASURED`, Q-TEC-010 offen. Schnelle Gates wurden durch die
  Review-Sitzung selbst erneut ausgeführt und sind grün (lint PASS,
  Release-Build 0 Warnungen, Tests 203/203, security PASS,
  `rift.sh verify` valid). **Offen:** Wiederholungen 2 und 3 laufen als
  eingefrorene Fingerprint-Langzeitjobs; die Abnahme des Auftrags bleibt
  an das vollständige Bündel von mindestens drei Evidenzeinheiten
  gebunden und erfolgt in einer frischen Review-Sitzung.

- **2026-08-25, Review-Sitzung nach Langzeitjob `t022-evidence-rep2`:**
  Job `SUCCEEDED` bei Exitcode 0 mit eigenständig nachgerechnetem,
  identischem Start-/End-/Aktuell-Repository-Fingerprint
  (`7f4914e3fe28cf843a5d35fe4184cec351adf5f3f10eab2cee7b142137d59cb2`)
  und verifizierter Logbindung (stdout-SHA-256
  `90f817a8792ca7f57488a1163bb4fcabad519cf5ac5b7f11af04914ea701e06f`
  reproduziert, stderr leer). Der Report
  `artifacts/t022/bundle/repetition-2.json` wurde als Evidenzeinheit des
  Soakvertrags V2 geprüft: vollständig über 576480/576480 Ticks
  (einschließlich Warm-up), `execution.evidenceUnit=true`, Gate pass ohne
  Verletzung, absolutes Working-Set-Wachstum 2412 KiB bei 16384 KiB Grenze,
  Trend im Limit, 0 Bytes je strengem Prüftick (9600 Prüfticks),
  Watchdog (120 s) ohne Stall (größte Lücke 0,294 s), alle 18 Ketten-
  stichproben unabhängig byteidentisch zur versionierten Golden-Fixture
  (SHA-256 `5e1a6a20cb7e46adf2a1f37679a15ab66766decedb3a555480783a3767a4dd76`),
  Reportzeitraum deckungsgleich mit dem gebundenen Jobfenster
  (13:49:55–13:53:05 UTC), Commit-/Release-Bindung im Report, Profile
  `NOT-MEASURED`, Q-TEC-010 offen. Die schnellen Gates wurden durch diese
  Review-Sitzung selbst erneut ausgeführt und sind grün (lint PASS mit
  0 Befunden, Release-Build 0 Warnungen, Tests 203/203, security PASS,
  `rift.sh verify` valid). **Offen:** Wiederholung 3 läuft als
  eingefrorener Fingerprint-Langzeitjob; die Abnahme des Auftrags bleibt
  an das vollständige Bündel von mindestens drei Evidenzeinheiten
  gebunden und erfolgt in einer frischen Review-Sitzung.

- **2026-08-25, Review-Sitzung nach Langzeitjob `t022-evidence-rep3`
  (Abschlussabnahme, Run `01M0WNMTJEW4MWYBVESPW11B3W`,
  Akteur `t022-review-acceptance`):**
  Job `SUCCEEDED` bei Exitcode 0 mit identischem Start-/End-/Aktuell-
  Repository-Fingerprint (`e1f413cfe5c5dab6165f868ea0f36a0e49fa43054e6c2315a68b8cb66ffef4ff`);
  die Logbindung wurde eigenständig verifiziert (stdout-SHA-256
  `8107486b03f13d087114ebecb766f77134a40ebc78b75b87977d63ef64e7f0cc`
  durch Byterekonstruktion reproduziert, stderr leer und hashgleich mit
  dem Leerstring). Der Report
  `artifacts/t022/bundle/repetition-3.json` wurde als dritte Evidenzeinheit
  geprüft: vollständig über 576480/576480 Ticks (einschließlich Warm-up),
  `execution.evidenceUnit=true`, Gate pass ohne Verletzung, absolutes
  Working-Set-Wachstum 2420 KiB bei 16384 KiB Grenze, Trend im Limit,
  0 Bytes je strengem Prüftick (9600 Prüfticks), Watchdog (120 s) ohne
  Stall (größte Lücke 0,298 s), alle 18 Kettenstichproben byteidentisch
  zur versionierten Golden-Fixture (SHA-256
  `5e1a6a20cb7e46adf2a1f37679a15ab66766decedb3a555480783a3767a4dd76`),
  Reportzeitraum deckungsgleich mit dem gebundenen Jobfenster
  (14:13:29–14:16:39 UTC), Commit-/Release-Bindung im Report, Profile
  `NOT-MEASURED`, Q-TEC-010 offen. Die Kettenstichproben aller drei
  Wiederholungen wurden paarweise als byteidentisch verifiziert; Plan-
  und Szenarioblock sind inhaltsidentisch. Die schnellen Gates wurden
  durch diese Sitzung selbst erneut ausgeführt und sind grün (lint PASS
  mit 0 Befunden, Release-Build 0 Warnungen, Tests 203/203, security
  PASS, `rift.sh verify` valid); zusätzlich wurden der CLI-Negativfall
  (unbekanntes Szenario → Exitcode 32 ohne Report) und ein beschleunigter
  Kurzlauf (→ Exitcode 30, `evidenceUnit=false`, Grund
  `horizon-shortened-diagnostic`, fail-closed NaN-Trendverletzung)
  frisch ausgeführt. **Ergebnis: Das Bündel erreicht 3 von mindestens 3
  Evidenzeinheiten auf derselben Maschine, demselben Commit
  (`83d742bad5914798ae8d1dfd8fe4504b159090c8`) und derselben Fixture;
  NF-002 gilt nach Soakvertrag V2 Abschnitt 4 als nachgewiesen,
  das Restrisiko nach Abschnitt 6 bleibt ausgewiesen. Der Auftrag wird
  angenommen.**

- **2026-08-25, Review-Sitzung nach gescheiterter Integrationspruefung
  (PR 11):** Die verpflichtende GitHub-Integration schlug fehl (Verify-Run
  `32878616983` und Asset-Kalibrierlauf `32878616891`, je Exitcode 1 auf dem
  Kandidaten `a31093b8`; Merge-Baum `refs/pull/11/merge` ist byteidentisch
  zum Kandidatenbaum). Eigenstaendige Diagnose und Reproduktion: die
  Schema-Fabrikationsmatrix las den **gitignorierten** Laufzeitreport
  `artifacts/t022/bundle/repetition-1.json` und scheiterte im frischen
  Checkout mit FileNotFound — lokal reproduziert als 202/203 mit
  Exitcode 1 nach kontrolliertem Wegparken der Datei (Bundle zuvor
  gesichert und byteidentisch restauriert). In-Scope-Reparatur ohne
  Beschwaechung: (1) die Evidenzform liegt jetzt als versionierte,
  sanierte Fixture `tests/fixtures/soak/t022-evidence-report-fixture.json`
  vor — abgeleitet aus dem echten akzeptierten Report, mit neutralisierten
  Hostpfad-, CPU- und Kernelangaben und repo-relativ gebundenem
  Golden-Fixture-Pfad, alle strukturellen Mess-, Ketten- und Planfelder
  unveraendert; der Test traegt zusaetzliche Echtheitswaechter
  (Evidenzeinheit, Vollhorizont, bestandener Lauf) und die unveraenderte
  Fabrikationsmatrix arbeitet gegen diese Fixture; (2) der bereits
  versionierte Inline-Goldenreport enthielt denselben lokalen Hostpfad —
  entfernt und durch den repo-relativen Pfad ersetzt; (3) neuer Test
  „tracked evidence fixture stays portable without host paths“ verhindert
  Host-/Sitzungspfad-Leakage in getrackten Soakvertragstexten dauerhaft.
  Die Laufzeitevidenz selbst bleibt gitignoriert; ein Force-Add erfolgte
  nicht. Hermetiebeweis im frischen-Checkout-Zustand (geparktes Bundle):
  Tests 204/204, Exitcode 0. Die schnellen Gates wurden danach vollstaendig
  wiederholt und sind gruen (lint PASS mit 0 Befunden, Release-Build
  0 Warnungen, Tests 204/204, security PASS, rag-build PASS,
  `rift.sh verify` valid inklusive Assets-Check); die abschliessenden reinen
  Textergaenzungen dieses Abschlusses sind per JSON-/Sanitizationspruefung
  abgedeckt, die vollstaendige Gate-Wiederholung auf den committed Bytes
  erzeugt das fingerprint-gebundene Pre-Push-Gate der Integration
  (fresh-checkout-test + dotnet-asset-calibration-ci.sh).

- **2026-08-25, unabhängige Review-Sitzung zur Reparaturverifikation
  (Run `01M0X3D16397NVSKFE9PX172GA`, Akteur
  `t022-integration-repair-review`):** Eigenständige Verifikation der
  Integrationsreparatur ohne Übernahme der Vorbehauptungen: (1)
  Strukturvergleich der Evidence-Fixture gegen den echten akzeptierten
  Runtime-Report `artifacts/t022/bundle/repetition-1.json` zeigt exakt
  drei Differenzen (`environment.cpu.model` → `fixture-cpu`,
  `environment.os.kernelRelease` → `fixture-kernel`,
  `metrics.goldenFixture.path` → repo-relativ); alle Mess-, Ketten-,
  Plan-, Gate- und Profilfelder sind unverändert. (2) Hostpfad-Scan über
  sämtliche getrackten Repositorydateien (`/home/…`, lokaler
  Arbeitsverzeichnisname) ohne Befund; der einzige Treffer in der
  Testdatei ist das Detektionsliteral des neuen Portabilitätstests.
  (3) Hermetiebeweis reproduziert: das gitignorierte Bundle
  `artifacts/t022/bundle/` wurde mit SHA-256-Protokoll wegparkiert, die
  Suite läuft im Frisch-Checkout-Zustand 204/204 mit Exitcode 0, danach
  wurde das Bundle byteidentisch restauriert (`sha256sum -c` je Datei
  OK). Ein dabei gefundener rein kosmetischer Markdown-Einzugsfehler in
  diesem Dokument wurde korrigiert. Alle schnellen Gates wurden von
  dieser Sitzung vollständig selbst ausgeführt und sind grün: lint PASS
  (0 Befunde), Release-Build 0 Warnungen, Tests 204/204, security PASS,
  rag-build PASS, `rift.sh verify` valid. Keine Schwellwert-, Kriterien-
  oder Vertragsänderung; die Laufzeitevidenz bleibt gitignoriert. Der
  unveränderte Kandidat ist für die frische Promoter-Sitzung markiert;
  Staging, Commit und Push bleiben getrennten Autoritäten vorbehalten.

- **2026-08-25, unabhängige frische Review-Sitzung mit explizitem
  Fresh-Checkout-/Clean-Archive-Nachweis (Run
  `01M0X6NMXX35RGHSTJZBR6DVZ3`, Akteur `t022-fresh-review`):**
  Eigenständige Nachprüfung der Integrationsreparatur ohne Übernahme der
  Vorbehauptungen, mit zusätzlicher lokaler Ausführung des für Änderungen
  an Tests/Fixtures vorgeschriebenen Portabilitätsvertrags: (1)
  Strukturvergleich der Evidence-Fixture gegen den echten Runtime-Report
  `artifacts/t022/bundle/repetition-1.json` reproduziert exakt drei
  Differenzen (`environment.cpu.model`, `environment.os.kernelRelease`,
  repo-relativer `metrics.goldenFixture.path`); alle Mess-, Ketten-,
  Plan-, Gate- und Profilfelder unverändert. (2) Hostpfad-Scan über
  ausschließlich getrackte Dateien erneut ohne Befund (nur
  Detektionsliterale des Portabilitätstests). (3) Verschärfter
  Hermetiebeweis: das **gesamte** gitignorierte `artifacts/`-Verzeichnis
  (5438 Dateien) wurde mit SHA-256-Vollprotokoll wegparkiert, die Suite
  lief im Frisch-Checkout-Zustand 204/204 mit Exitcode 0, danach wurde
  der Bestand anhand des Vollprotokolls byteidentisch restauriert. (4)
  Clean-Archive-Vertrag lokal am unverarbeiteten Kandidaten ausgeführt,
  ohne Staging oder Commit: aus HEAD
  `a31093b8ab30b113bd0539af929026594a716ec9` plus Arbeitsbaum wurde per
  indexfreier Git-Plumbing-Rekonstruktion der hypothetische
  Kandidatenbaum `dc85033fcaa8f26df16bf94d1921910194d26f3e` erzeugt
  (Baumdiff gegen HEAD exakt die fünf Auftragspfade), per
  `git archive` in ein isoliertes Verzeichnis ohne Laufzeitevidenz
  extrahiert und dort der volle Gate-Zug ausschließlich aus diesen Bytes
  gefahren: Restore/Build/Harness-Init, Release-Build 0 Warnungen, lint
  PASS 0 Befunde, Tests 204/204 Exitcode 0 (einschließlich
  Evidenz-Fixture- und Portabilitätstest), assets-check PASS, rag-build
  PASS, verify valid. Der echte Index blieb währenddessen unverändert.
  Keine Schwellwert-, Kriterien-, Vertrags- oder Produktcodeänderung;
  die Laufzeitevidenz bleibt gitignoriert. Der unveränderte Kandidat ist
  für die frische Promoter-Sitzung markiert; Staging, Commit und Push
  bleiben getrennten Autoritäten vorbehalten.

## Ergebnis gegen die Abnahmekriterien

| Kriterium | Ergebnis | Evidenz |
|---|---|---|
| AC-T022-01 | Abschnitt 0 vor der Soakimplementierung abgeschlossen: `docs/SOAKVERTRAG.md` V1 mit doppelter Schwellwertform (absolut 16 MiB innerhalb der Kapsel 4–64 MiB; Trend 1024 KiB/h; Konsistenz 8 MiB ≤ 16 MiB), Watchdogfenster 120 s (Band 30–300 s), Ausführungsvertrag autoritativ/beschleunigt; abgeleitet aus zwei Kalibrierläufen à 1800 s Wanduhr als dokumentiertes Vielfaches des Messrauschens; Budgetlinien nur obere Grenzen; keine Q-TEC-010-Streuungsentscheidung | Vertragsdokument + Run-Ereignisfolge (Event seq 4 vor Gateimplementierung) + Spiegeltest |
| AC-T022-02 | `./scripts/rift.sh soak --scenario soak-replay --report PFAD` nativ linux-x64 im bestehenden Host, rein CPU-seitig, lädt SDL3-/bgfx-Artefakte nicht; unbekanntes Szenario → Exitcode 32 ohne Report; fehlender Build → rift.sh-Guard Exitcode 4; Exitcodes 25–29 unverändert, neue Codes 30/31/32 dokumentiert und getestet | CLI-Frischprozess-Tests in Suite (203/203, Reviewlauf), `docs/NATIVE_UNTERBAU.md` |
| AC-T022-03 | **Nach Projektleitungsentscheidung 2026-08-25 ersetzt:** reproduzierbare deterministische Wiederholungsevidenz statt Einzelprozess-Achtstundenmodus — mindestens drei unabhängige Fresh-Prozess-Läufe über den kompletten Planhorizont (576000 Messsticks + Warm-up, Vertragssseed 20260824), Release-Build, beschleunigte Taktung zulässig (Pacing-Unabhängigkeit durch Test belegt); je Lauf vollständig, watchdog-, speicher-, allokations- und kettengeprüft fail-closed; als Evidenzeinheit (`execution.evidenceUnit`, Soakvertrag V2) markiert; verkürzte/Debug-/Stall-/Kettenabweichungs-Läufe werden mit Grund abgewiesen; `--reference-out` ist unabhängig vom Horizont diagnostisch und darf sich nie selbst als Evidenz bestätigen; Restrisiko vertraglich ausgewiesen | Evidence-Matrix-, Schemaquerbeziehungs-, echter CLI-Emissions- und Vertrags-Spiegeltest; drei Referenz-Vollhorizontläufe `artifacts/t022/reference-full{,-2,-3}.json` (je complete+pass, Endhash `4e2470332254c5ad`); **vollständiges Bundle: 3 von 3 Evidenzeinheiten** unter `artifacts/t022/bundle/repetition-{1,2,3}.json` als asynchrone Fingerprint-Langzeitjobs auf dem eingefrorenen Kandidaten, jede durch eine unabhängige Review-Sitzung verifiziert (`t022-evidence-rep1`/`rep2`/`rep3`, je SUCCEEDED, Gate pass, 18/18 Kettenstichproben fixtureidentisch, paarweise byteidentische Ketten über alle Wiederholungen) |
| AC-T022-04 | **Nach Projektleitungsentscheidung 2026-08-25 ersetzt:** Absturzfreiheit auf der ausgeführten Horizontbreite — jede unerwartete Beendigung ergibt Exitcode ungleich 0 plus Teilreport (`partial=true`, `isEvidence=false`, `incompleteReason`); kosmetisch grün aus unvollständiger Tickanzahl ausgeschlossen. Der absichtlich per SIGTERM abgebrochene Achtstundenlauf (6 h 35 m, Exitcode 143, kein Report) ist ausschließlich partielle Diagnosebeobachtung ohne PASS und wird im Abschnitt „Autoritativer Lauf“ ehrlich dokumentiert | Fault-Injection-Tests (Exitcode-Mapping, Unvollständigkeitsklassen); Vollständigkeitsmarkierung je Bundle-Report; Abbruchdokumentation unten und `docs/SOAKVERTRAG.md` V2 Abschnitt 6 |
| AC-T022-05 | Kein Hängen: Fortschritts-Watchdog außerhalb des Heisspfads, Fenster exakt 120 s gemäß Soakvertrag; Stall → Exitcode 30 bei geschriebenem, nicht bestandenem Report | Watchdog-Unit-Tests (injizierte Zeit, eingefrorener Fortschritt), Parameterabgleich gegen Vertrag |
| AC-T022-06 | Speicher-/Allokationsgate fail-closed: absolute Wachstumsschwelle, Trendkriterium, strenge Per-Tick-Allokation nach Simulationsvertrag V1 §5 (Methode `gc-total-allocated-bytes-precise-delta-per-tick-sum`); Working-Set min/max/end/first plus Fensterstatistik, gatefreie Fensterdeltas als Telemetrie, GC-Pausen, Einheit+Methode je Kennzahl, unavailable mit Grund | Gate-Matrix-Tests; Referenzlauf mit 0,000000 B je strengem Prüftick |
| AC-T022-07 | Determinismus/Replay: Golden-Fixture `src/Riftward.App/Soak/soak-replay-chain-v1.json` (SHA-256 `5e1a6a20cb7e46adf2a1f37679a15ab66766decedb3a555480783a3767a4dd76`) aus unabhängigen Referenzläufen; Loader erzwingt Vertragssseed, nachgerechneten Planhash und den vollständigen kanonischen Stichprobenplan; Evidenz verlangt 18/18 Treffer, null Abweichungen und null übersprungene Stichproben; drei Fresh-Prozess-Komplettläufe mit identischem Endhash `4e2470332254c5ad` und inhaltsidentischen Kettenstichproben; Läufe 2–3 schrieben byteidentische Fixturedokumente, Lauf 1 wich nur in der Textformatierung ab (alle 18 Stichproben tick- und hashidentisch mit der versionierten Datei); Pacing-Unabhängigkeit durch identische Präfixketten getaktet/beschleunigt; Fremdseed ändert Endhash nachweislich (Exitcode 30); Fixture-SHA-256 im Report gebunden | Referenzreports `artifacts/t022/reference-full*.json`; sparse-Fixture-, Reportmanipulations- und Planhash-Negativtests |
| AC-T022-08 | Driftfelder ausschließlich diagnostisch (`gateCoupled=false`), keine Gatekopplung (Evaluator kennt die Werte nicht — Negativtest); Q-TEC-010 maschinenlesbar als offen, nicht definiert, nicht verbraucht (Reportfeld + Vertragsankertext) | Schema-Fabrikationsmatrix, Entkopplungstest |
| AC-T022-09 | Profilbestehen nur mit deklarierter Bindung auf benannten Referenzrechnern; Entwickler-PC-Läufe diagnostische Baseline; Pflichtprofile `NOT-MEASURED` | Bindungs-/Ablehnungslogik-Tests; echter Report zeigt alle drei Profile `NOT-MEASURED` |
| AC-T022-10 | Kontrollierte Fehler an allen neuen Grenzen (nicht schreibbarer Pfad → 28, Schemawiderspruch → 27 mit Diagnose-Report, Usage → 2, fehlender Build → 4, vorzeitiger Abbruch → 31 mit Teilreport); kein Absturz/Hängen/Netzwerk/Schreibzugriff außerhalb erlaubter Verzeichnisse; bestehende Exitcodes unverändert | Mapping-/Fault-Injection-Tests |
| AC-T022-11 | Gates grün (Reviewlauf 2026-08-25): fmt/lint PASS, build 0 Warnungen, tests 203/203, security PASS, rag-build, verify valid; BCL-only (keine neue Abhängigkeit); Architekturtest hält Soak frei von SDL3-/bgfx-/Plattformtypen, F#-frei, Uhr-/Schlafprimitive nur in `PacingClock.cs`, keine Kernzahl-/Zeitzonen-/Dateisystemreihenfolgeabhängigkeit des Zustands | Gate-Ausführungen im Reviewlauf; Wiederholung auf dem eingefrorenen Kandidaten vor dem Bundle-Lauf |
| AC-T022-12 | Doku konsistent: AUTOMATION.md (soak-Befehlsvertrag), NATIVE_UNTERBAU.md (Befehl+Exitcodes 30/31/32), PERFORMANCE_BUDGET.md (Nachweisort NF-002 ohne Budgetänderung), G-PERF in `.ai/evals/quality-gates.json` (Soak + NOT-MEASURED), ARCHITEKTUR.md/DATENMODELL.md (Vertragsverweise ohne Formatfestlegung), QUALITAET.md (Spike-Klausel aufgelöst) | Doku-Review dieses Dokuments |

## Abschnitt-0-Kalibrierbasis (Entwickler-PC i7-3770/RX-570-Klasse, Release)

Zwei unabhängige Realzeit-Kalibrierläufe à 1800 s Wanduhr
(`artifacts/t022/calibration-run-a.json`, `-b.json`): nahezu identischer
Verlauf — Einschwingen der Laufzeitumgebung in den ersten Minuten
(Spitze-zu-Spitze ≈ 2,83 MiB), danach exakt fließendes Plateau
(Drittelsteigung 0 KiB/h, Medianjitter 0 KiB, größte Einzelfensterauslenkung
1424 KiB), größte Fortschrittslücke 0,057 s, 0 GC-Pausen. Ableitung:
16 MiB absolut (≈ 5,6× maximale Gesamtschwankung), 1024 KiB/h Trend
(unterhalb des Konsistenzmaximums 2048), 120 s Watchdog (≈ 2100× beobachtete
Lücke). Alle Werte mit Alternativen, Gründen und Rückrollweg in
`docs/SOAKVERTRAG.md`.

## Autoritativer Lauf (Ergebnis, Projektleitungsentscheidung 2026-08-25)

Der am 2026-08-25 gestartete autoritative Achtstunden-Realzeitlauf wurde
nach **6 h 35 m Wanduhr absichtlich per SIGTERM abgebrochen**
(`artifacts/t022/soak-authoritative-status.log`: `authoritative exit=143`;
kein Report geschrieben, `artifacts/t022/soak-authoritative.log` leer).
Verbindliche Entscheidung der Projektleitung: Der Lauf darf **nicht neu
gestartet** werden; die Beobachtung gilt ausschließlich als partielle
Diagnoseevidenz dafür, dass der Prozess bis zum Abbruch nicht von selbst
endete — **niemals als PASS, niemals als NF-002-Nachweis**.

Daraus folgt die Ersatzevidenz nach Soakvertrag V2: reproduzierbares,
risikobasiertes Bündel aus mindestens drei unabhängigen Fresh-Prozess-
Wiederholungsläufen über den kompletten Planhorizont (beschleunigte
Taktung, Pacing-Unabhängigkeit durch Test belegt), je Lauf fail-closed
gegen die unverändert geltenden Abschnitt-0-Grenzwerte. Bereits vorliegende
Referenzläufe: `artifacts/t022/reference-full.json`, `-2.json`, `-3.json`
(je 576480 Ticks, complete+pass, identischer Endhash
`4e2470332254c5ad`, maximales absolutes Wachstum ≈ 2,32 MiB bei 16 MiB
Grenze, 0 Bytes je strengem Prüftick). Das Wiederholungsbündel wurde als
asynchrone Fingerprint-Langzeitjobs auf dem jeweils eingefrorenen
Kandidaten ausgeführt und schrieb je Wiederholung einen Report unter
`artifacts/t022/bundle/repetition-N.json` (N = 1, 2, 3) nebst den
gebundenen Job-Logs; alle drei Wiederholungen sind verifizierte
Evidenzeinheiten (siehe Review-Verlauf). Das verbleibende
Restrisiko (kein Nachweis zusammenhängender 8-h-Echtzeit) ist in
`docs/SOAKVERTRAG.md` V2 Abschnitt 6 ausgewiesen.

## Bekannte Restpunkte und Einschränkungen

- Pflichtprofile bleiben `NOT-MEASURED`, bis die Projektleitung Referenzrechner
  benennt (Q-OPS-001 bleibt `OFFEN`); alle Läufe sind diagnostische Baseline.
- Der Nachweis ist ein Stabilitätstest auf linux-x64; Windows-/macOS-Nachweise
  bleiben an T-011 überwiesen (Q-OPS-002/Q-OPS-003).
- Die tolerierte Benchmarkstreuung (Q-TEC-010) bleibt offen; die
  fensterweisen Driftfelder begründen keine Toleranz.
- Die Ratifizierung von Simulationsvertrag V1 bleibt über Q-TEC-004 offen;
  der Soak wiederverwendet ihn unverändert.
- Extern verursachte Prozessfreezes (z. B. SIGSTOP oder Systemstandby) zählen
  als Watchdog-Stall und machen den Lauf nicht als Evidenzeinheit verwertbar;
  ein Neustart des abgebrochenen Achtstunden-Realzeitlaufs ist durch
  Projektleitungsentscheidung ausdrücklich untersagt. Das Restrisiko des
  nicht nachgewiesenen zusammenhängenden Achtstunden-Echtzeitbetriebs ist in
  `docs/SOAKVERTRAG.md` V2 Abschnitt 6 ausgewiesen.
- Keine visuellen Medienartefakte in diesem Auftrag (Media-Lab-Prüfung:
  headless Zuverlässigkeitslauf ohne sichtbaren Szenengehalt; Kurven-
  visualisierung bleibt MEDIA-05 vorbehalten).
