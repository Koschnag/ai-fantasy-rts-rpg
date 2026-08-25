# Backlog

Nur Einträge mit Status `READY` dürfen ohne weitere fachliche Klärung implementiert werden.

## Priorisierung

- `MUST`: für das MVP unverzichtbar
- `SHOULD`: hoher Nutzen, aber kein Freigabekriterium
- `COULD`: optional
- `WONT`: bewusst nicht in dieser Version

## Epics

| ID | Epic | Nutzen | Priorität | Abhängigkeiten | Status |
|---|---|---|---|---|---|
| E-001 | Autonome Produktionsplattform | KI-Arbeit ist reproduzierbar, erinnerungsfähig und prüfbar | MUST | – | IN ARBEIT |
| E-002 | Plattform-Walking-Skeleton | dasselbe leere Spiel startet auf Windows, Linux und macOS | MUST | E-001 | OFFEN |
| E-003 | Performancekern | Rendering und Simulation halten die Hardwarebudgets | MUST | E-002 | OFFEN |
| E-004 | Graybox-Hybridspiel | Held, Erkundung, Aufbau und Armee bilden eine spaßige Schleife | MUST | E-003 | OFFEN |
| E-005 | Atmosphärischer Vertical Slice | alle Kernsysteme und finalitätsnahe Inhalte bestehen zusammen | MUST | E-004 | OFFEN |
| E-006 | Contentproduktion | validierte KI-/prozedurale Pipelines skalieren den freigegebenen Umfang | MUST | E-005 | OFFEN |

## Umsetzungseinheiten

| ID | Epic | Ergebnis | Verknüpfte Anforderungen | Größe | Priorität | Status |
|---|---|---|---|---|---|---|
| T-001 | E-001 | lokales F#-Harness mit Run-Ledger, Hashkette, BM25-RAG, Zitaten und Integritätsprüfung | Z-004, NF-008 | M | MUST | DONE |
| T-002 | E-001 | Memory-Promotion, Konflikt-/Stalenessprüfung und Retrieval-Traces | Z-004, NF-003 | M | MUST | DONE |
| T-003 | E-001 | Clean-Room-, Asset-Provenienz-, Quarantäne- und technische Validatorhülle | Z-004, Z-005 | M | MUST | DONE |
| T-004 | E-001 | vollständige Run-Provenienz, Evidenzzuordnung, Trace-/Span-Felder, RAG-Buildmanifest und sichere Retention | Z-004, NF-003, NF-008 | M | MUST | DONE |
| T-005 | E-001 | striktes calibration-v1-Spec und unabhängiger .NET-Inspector prüfen GLB, PNG, Report und Proxybudgets ohne Blender | Z-002, Z-004, Z-005, F-008, F-009 | M | MUST | DONE |
| T-006 | E-001 | BCL-only-F#/.NET-Generator schreibt GLB und CPU-Preview deterministisch in-process und publiziert transaktional über T-003 in Quarantäne | Z-002, Z-004, Z-005, F-007, F-008, F-009 | M | MUST | DONE |
| T-007 | E-001 | Fresh-Checkout-CI beweist .NET-Pin, Null-Unterprozess/-Netz, Determinismus, T-005-Regression, Recovery und T-003-Crosschecks | Z-002, Z-004, Z-005, F-007, F-008, F-009 | M | MUST | DONE |
| T-008 | E-001 | überprüfbarer Retail-Era-Forschungs-Showcase mit Press-Kit-Prototyp, Quarantäne-Key-Art und wahrheitsgebundenen Exportregeln | Z-004, Z-005 | M | SHOULD | REVIEW |
| T-010 | E-002 | SDL3-Fenster, Input und bgfx-Dreieck zuerst nativ auf linux-x64 auf Referenzhardware; Windows-/macOS-Nachweise folgen über T-011 | Z-002, Z-003 | L | MUST | DONE |
| T-011 | E-002 | plattformspezifische Shader-/Native-Buildmatrix und Smoke-Artefakte | Z-003 | L | MUST | DRAFT |
| T-020 | E-003 | leere Benchmarkszene mit Telemetrie auf allen Hardwareprofilen | Z-002 | M | MUST | DONE |
| T-021 | E-003 | headless feste Simulation mit 250 mobilen Testagenten | Z-002 | L | MUST | DONE |
| T-022 | E-003 | deterministischer 8-Stunden-Replay-Soak weist Stabilität und begrenztes Speicherwachstum nach | Z-002, NF-002 | M | MUST | DONE |
| T-023 | E-003 | integrierter repräsentativer Belastungsframe verbindet 350 sichtbare/250 simulierte Einheiten, Animation, Landschaft, Schatten, Partikel und vollständige Ressourcenmetriken auf den Minimum-Profilen | Z-002 | L | MUST | DONE |
| T-030 | E-004 | erste vollständige Graybox-Schleife von Erkundung bis Basiskampf | Z-001 | XL | MUST | DRAFT |
| T-031 | E-004 | versioniertes atomares Save/Load besteht Roundtrip-, Abbruch-, Korruptions- und Wiederherstellungsfixtures | Z-001, F-005, NF-002 | L | MUST | DRAFT |
| T-040 | E-005 | repräsentative Riftward-Mission besteht Atmosphären-, Originalitäts- und visuelles Lesbarkeitsgate | Z-001, Z-005 | XL | MUST | DRAFT |
| T-041 | E-005 | finale UI-, Eingabe-, Untertitel- und Einstellungsabnahme auf allen Zielplattformen | Z-002, Z-003 | L | MUST | DRAFT |
| T-050 | E-006 | eine validierte KI-/prozedurale Assetfamilie durchläuft Quarantäne, Review, LFS-Quelle und Cooking reproduzierbar | Z-004, Z-005 | L | MUST | DRAFT |
| T-051 | E-006 | gemessene Karten-/Quest-/Audio-Pipeline erzeugt konsistente Inhalte mit vollständiger Provenienz | Z-001, Z-004, Z-005 | XL | MUST | DRAFT |

`T-003`, `T-005`, `T-006` und `T-007` sind unabhängig abgenommen. `T-006` hat den
BCL-only-.NET-in-process-Generator samt transaktionalem Quarantäne-Lifecycle
und dem ersten lokalen 3D-Quarantäneasset geliefert. `T-007` beweist diesen
Pfad aus einem sauberen Linux-x64-Checkout. Alle drei Assettasks hängen direkt von `T-003` ab; zusätzlich hängt
`T-006` von `T-005` und `T-007` von `T-005`/`T-006` ab. Der geschlossene Vertrag steht in
`docs/DOTNET_GENERATOR_CONTRACT.md`. Das ist ein bewusstes T-006-Amendment:
T-005 bleibt historisch abgenommen; seine komplette Inspector-Suite muss nach
der eng begrenzten Identifier-/Quellen-/Pin-Anpassung erneut bestehen. `T-050`
bleibt `DRAFT` und setzt `T-003`, `T-005`, `T-006` und `T-007`
voraus; erst T-050 verantwortet getrennte visuelle/rechtliche Reviews,
Source-Promotion, LFS, Backup, Cooking und produktionsnahe Messung.

`T-004` wurde am 2026-08-22 durch die Projektleitung freigegeben (`READY`), in
Lauf `01M0NCFAVJ308TVZ3XY7J8SY58` implementiert und durch den unabhängigen
Reviewlauf `01M0QMA3NAPRXX1KBVH8XFRA6J` akzeptiert; Abnahmedokument:
`docs/abnahme/T-004-run-provenance-and-evidence.md`.

`T-010` wurde am 2026-08-23 vom autonomen Planungsagenten (Autorisierung der
Projektleitung vom 2026-08-23) auf `READY` gesetzt. Die Epikabhängigkeit E-001
ist im für T-010 erforderlichen Umfang erfüllt: T-001 bis T-007 sind
abgenommen; das Epic selbst bleibt wegen möglicher Folgeeinheiten in Arbeit.
Die zuvor blockierenden Fragen Q-TEC-001/Q-TEC-003 sind verfahrensmäßig
entschieden (Klärungsprotokoll in `docs/OFFENE_FRAGEN.md`): Die konkreten
nativen Pins und Build-/Cachedetails entstehen als gatender erster Abschnitt
des Auftrags `.ai/tasks/T-010-native-walking-skeleton.json` nach vollständig
spezifizierten Kriterien gemäß der Spike-Klausel in `docs/QUALITAET.md`;
Rückrollbar durch Pin-Austausch und Neubau. Der Auftrag liefert die nativen
linux-x64-Nachweise von AC-T010-02/03 auf dem Entwickler-PC (i7-3770/RX 570);
Windows- und macOS-Builds, Smokes und Paketnachweise sind bewusst an T-011
überwiesen. Fehlt die linux-x64-Referenzhardware bei Umsetzung, bleiben die
Kriterien offen und werden eskaliert statt durch Cross-Compile oder Simulation
ersetzt; das Projektziel Z-003/NF-006 (alle drei Pflicht-RIDs) bleibt
unverändert. Audio (Q-TEC-007) bleibt ausdrücklich ausgeschlossen.

Die Spezifikation wurde am 2026-08-23 durch den unabhängigen Reviewlauf
`01M0QQYJDX9CS56144Z7VGN8J4` geprüft: Task-Manifest schema-validiert,
Dokumente konsistent, alle lokalen Gates grün. Zu diesem Prüfzeitpunkt hatte die
Implementierung noch nicht begonnen.

T-010 wurde am 2026-08-24 durch den unabhängigen Review-/Vollendungslauf
`01M0QYAA11MC89GVMP6BWR7016` (Akteur `t010-review-completion`) abgenommen und
ist `DONE`: Zwei abgebrochene Implementierungsläufe wurden geprüft, deren
In-Scope-Defekte repariert (bgfx-Ausgabepfade, x86-64-v2-/PIC-Buildflags,
Shim-Link gegen bimg, SDL3-X11-Laufzeitbindung, Shader-Semantikdefinition,
`SOURCE_DATE_EPOCH` für byteidentische Neubauten, Manifest-Neuschreiben im
Verify-Modus) und die fehlenden Anteile vollendet: nativer Build samt
Reproduzierbarkeitsnachweis, C#-Interop mit LibraryImport/Besitzregeln/
Fehlerobjekten, Host mit `plattformsmoke`/`effizienzbaseline`,
Toolchain-/Lizenz-/ISA-Gate in `lint`+`security`, Fault-Injection- und
Architekturtests, Doku (`NATIVE_UNTERBAU.md`, Mindestbasis in
`PLATTFORMMATRIX.md`, Befehlsvertrag in `AUTOMATION.md`). Alle
Abnahmekriterien AC-T010-01 bis AC-T010-08 sind mit Evidenz im Lauf
nachgewiesen; Smoke und Effizienzbaseline liefen nativ auf dem
Entwickler-PC (i7-3770/RX 570, Mesa 26.0.3). Die durch den Pin-Nachtrag
invalidierten T-006/T-007-Bindungen (Manifest-Input-Hash, Receipt-Kette,
CI-Evidenzschema) wurden über die dokumentierte Regeneration neu verankert;
die generierten Assets blieben byteidentisch. Windows-/macOS-Builds, Smokes
und Paketnachweise bleiben gemäß Auftrag an T-011 überwiesen; Abnahmedokument:
`docs/abnahme/T-010-native-walking-skeleton.md`.

Am 2026-08-25 prüfte und vollendete ein unabhängiger Review-Lauf
(Harness-Run `01M0XD6NWC5V01CJ8HVQNJPQXF`, Akteur
`t010-bootstrap-review-completion`) den vorgefundenen Arbeitsstand zum
Bootstrap-Werkzeugvertrag und reparierte zwei In-Scope-Defekte:
(1) `scripts/bootstrap-dotnet.sh` akzeptiert eine bereits korrekte
PATH-Verknüpfung jetzt idempotent ohne Schreibzugriff (read-only eingehängte
Werkzeugbäume in CI-/Agent-Sandboxen) und bricht bei einer kollidierenden,
nicht-symlinkschen PATH-Datei kontrolliert mit Exitcode 1 ab, statt sie zuvor
nur zu vermelden und trotzdem Erfolg zu melden; ein falscher Pass wird damit
ausgeschlossen. Zwei hermetische Tests binden den Vertrag (idempotente
Annahme mit adversarialem `ln`-Stellvertreter und schreibgeschütztem
Zielverzeichnis; Kollisionsabbruch mit Unverändertheitsnachweis).
(2) Der T-021-CLI-Vertragstest für `bench-sim` wiederholt den Fresh-Prozesslauf
genau einmal bei Exitcode 26 des dokumentierten Budgetgates. Der Exitcode ist
klauselunspezifisch: Neben der lastempfindlichen Tickzeit kann unter starker
Host-Konkurrenz auch der prozessweite Allokationszähler transient falsch
anschlagen (Folgereview 2026-08-26, Harness-Run `01M0XH1YTNDSG8E5HXRCGBYEF5`:
einstellige Bytes je warmem Tick bei Last
13–18; Kettenende in allen Messläufen identisch, Produktallokation exakt 0).
Anhaltende Verletzungen jeder Klausel – Tickzeitregression oder
Produktallokation – scheitern weiterhin reproduzierbar in beiden Versuchen;
alle übrigen Klauseln sind unverändert. Suite 204 → 205;
alle lokalen Gates grün; Details in beiden Abnahmedokumenten.

Nach T-010 werden die isolierten Baselines T-020/T-021 und anschließend der
integrierte Repräsentativitätsnachweis T-023 gegenüber weiterer allgemeiner
Produktionsinfrastruktur priorisiert, soweit deren Abhängigkeiten `READY` sind.
Der bewusst einfache Belastungsframe ist der erste Beleg für oder gegen die
Effizienzhypothese; Architektur und Budgets allein gelten nicht als Optimierung
(ADR 006).

T-020 wurde am 2026-08-24 vom autonomen Planungsagenten (Autorisierung der
Projektleitung vom 2026-08-23) auf `READY` gesetzt. Die Epikabhängigkeit E-002
ist im für eine isolierte Renderer-Baseline erforderlichen Umfang erfüllt:
T-010 liefert den nativen linux-x64-Unterbau mit Smoke- und Effizienzvertrag;
Windows-/macOS-Nachweise bleiben unverändert bei T-011 (Z-003/NF-006). T-011
bleibt bewusst zurückgestellt: Seine Umsetzung setzt native Windows-/macOS-
Runner samt Build-, Signier- und Notarisierungsentscheidung voraus
(Q-OPS-002), die hier nicht vorhanden sind und nicht stillschweigend
angenommen werden dürfen. Der offenen Frage Q-OPS-001 wird verfahrensmäßig
begegnet (Klärungsprotokoll in `docs/OFFENE_FRAGEN.md`): Messungen auf dem
Entwickler-PC (i7-3770/RX 570) gelten als diagnostische Baseline,
Profilbestehen entsteht nur durch deklarierte Referenzklassenbindung, und
fehlende Referenzhardware bleibt `NOT-MEASURED` mit Eskalation statt Ersatz;
rückrollbar durch Benennung der Referenzrechner und Wiederholung desselben
bench-Befehls. Q-TEC-004/Q-TEC-005 betreffen die Simulation und bleiben
Blocker von T-021. Der Auftrag liegt als
`.ai/tasks/T-020-empty-scene-benchmark.json` vor und implementiert den ersten
ausführbaren Anteil des G-PERF-Gates (`BENCH-EMPTY`) ohne Budgetänderung.

T-020 wurde am 2026-08-24 durch den unabhängigen Review-/Vollendungslauf
`01M0T2GGVHV79RFDSKNSJ1QV8B` (Akteur `t020-review-completion`) umgesetzt,
geprüft und auf `DONE` gesetzt: Shim-Erweiterung für bgfx-Statistik (GPU-Zeit,
verwalteter GPU-Speicher, gerenderte Dreiecke) und Viewtransformation mit
zweifach byteidentischem `--fresh`-Neubau; öffentlicher Befehl
`rift.sh bench --scenario bench-empty --report PFAD`; BenchRunner mit
deterministischem Kameraflugskript, Telemetrie je Kennzahl mit Einheit und
Methodenkennung, fail-closed Budgetgate ausschließlich gegen dokumentierte
Grenzwerte, Szenarioregistry (unbekannte/nicht implementierte Szenarien →
Exitcode 25 ohne Report) und Profilbindungs-Ehrlichkeitsregel; 12 neue Tests
(Suite 158/158); Doku in `NATIVE_UNTERBAU.md`, `AUTOMATION.md`,
`PERFORMANCE_BUDGET.md` und Gate-Register aktualisiert. Diagnostischer Lauf:
p99 2,979 ms, 565 B Allokationen pro warmem Frame, GC-Pausen 0, Working-Set
max ~195 MiB, 1 Draw, 1 Dreieck, gemessene GPU-Zeit (p99 0,078 ms); alle
Grenzwerte eingehalten, Reportstruktur zweier Läufe identisch. Im
Kopflos-Aufbau dieser Sitzung rendert Mesa über llvmpipe statt radeonsi; der
Renderer-String ist im Report gebunden. Alle Pflichtprofile bleiben
`NOT-MEASURED`, bis die Projektleitung Referenzrechner benennt (Q-OPS-001
bleibt `OFFEN`); Abnahmedokument:
`docs/abnahme/T-020-empty-scene-benchmark.md`.

T-021 wurde am 2026-08-24 vom autonomen Planungsagenten (Autorisierung der
Projektleitung vom 2026-08-23) auf `READY` gesetzt. Auswahlbegründung: Der
tabellarisch frühere DRAFT `T-011` bleibt blockiert, weil seine native
Windows-/macOS-Build-/Smoke-/Paketmatrix vorhandene Runner,
Signier-/Notarisierungsentscheidungen (Q-OPS-002) und physische Zielhardware
voraussetzt, die hier nicht vorhanden sind und nicht stillschweigend angenommen
werden dürfen; `T-022` und `T-023` setzen die erst durch `T-021` zu liefernde
Simulation voraus; `T-030`/`T-031` hängen an echten Produktentscheidungen.
Damit ist `T-021` der höchstpriorisierte DRAFT-Auftrag mit erfüllbaren
Abhängigkeiten und folgt der ausdrücklichen Priorisierung der isolierten
Baselines T-020/T-021 vor weiterer Produktionsinfrastruktur (ADR 006). Die
Epikabhängigkeit E-002 ist im für eine headless-CPU-Baseline erforderlichen
Umfang erfüllt: T-010 liefert den nativen linux-x64-Host samt Befehls-/Exitcode-
vertrag, T-020 den bench-/Telemetrie-/Budgetgate-Vertrag; Z-003/NF-006 bleiben
unverändert bei T-011. Die Blocker Q-TEC-004/Q-TEC-005 sind verfahrensmäßig in
den gatenden Vertragsspike (Abschnitt 0 des Auftrags
`.ai/tasks/T-021-headless-simulation-baseline.json`) überführt (Spike-Klausel
`docs/QUALITAET.md`, Klärungsprotokoll in `docs/OFFENE_FRAGEN.md`): Numerik-
modell, Hashvertragsklassen und Daten-/Navigations-/Schedulingstrukturen
entstehen dort ausschließlich nach fixierten Kriterien mit Alternativen,
Gründen und Rückrollweg; eine exakte plattformübergreifende Hashgarantie bleibt
bis zu einer echten Cross-Plattform-Messung untersagt, tolerante Abweichungen
werden nicht erfunden. Q-OPS-001 gilt entsprechend der T-020-Behandlung:
Entwickler-PC-Messungen sind diagnostische Baseline, Pflichtprofile bleiben
`NOT-MEASURED` mit Eskalation statt Ersatz. Von Q-TEC-010 wird nur die
Allokationsgrenze je warmem Tick verfahrensmäßig abgeleitet (Arbeitsannahme
„nahe null“; Gatewert höchstens 1 KiB je warmem Tick, Verschärfung erlaubt);
die tolerierte Benchmarkstreuung verbleibt vollständig in Q-TEC-010 und
blockiert weiterhin T-022. Media-Lab-Prüfung gemäß
`docs/communication/MEDIA_LAB.md`: kein visuelles Artefakt in diesem Auftrag,
weil eine headless-Simulationsbaseline keinen sichtbaren Szenengehalt besitzt
und die maschinenlesbare Telemetrie die prüfbare Evidenz ist; die
Benchmarkvisualisierung bleibt MEDIA-05.

T-021 wurde am 2026-08-24 durch den unabhängigen Review-/Vollendungslauf
`01M0T61A0NT4PBA4CQZKGJS5QC` (Akteur `t021-review-completion`) geprüft,
umgesetzt und auf `DONE` gesetzt: Der gatende Abschnitt 0 legte in
`docs/SIMULATIONSVERTRAG.md` V1 das Numerikmodell (reine Ganzzahl-Festkomma
Q16.16), die Hashvertragsklassen (K1/K2 garantiert, Cross-Build/-Plattform
ausdrücklich nicht behauptet), Seedableitung und kanonische Ordnung, datennahe
Strukturen mit hierarchisch budgetierter Pfadsuche sowie die auf 0 Bytes
versärfte Allokationsgrenze je warmem Tick fest. Der neue BCL-only-Kern
`Riftward.Simulation` simuliert genau 250 mobile Testagenten mit
Fortbewegung, Ausweichen und Gruppenbefehlen bei festem 20-Hz-Tick; der
öffentliche Befehl `rift.sh bench --scenario bench-sim --report PFAD` liefert
den Report (Schemaversion 2) mit fail-closed Budgetgate. Zwei Fresh-Prozess-
läufe bestanden mit p99 0,458/0,480 ms, 0,000 B Allokationen je warmem Tick
und identischen Hashketten (23 Glieder); fremder Seed und umgeordnete
Befehlsfolge ändern den Endhash nachweislich. 14 neue Tests (Suite 172/172);
alle Gates grün; Pflichtprofile bleiben `NOT-MEASURED` (Q-OPS-001). Ein
`bench-empty`-Regressionslauf war in dieser kopflosen Sitzung displaylos nicht
ausführbar (SDL3 ohne Wayland); Absicherung über unveränderten Codespfad und
die vollständige T-020-Suite. Abnahmedokument:
`docs/abnahme/T-021-headless-simulation-baseline.md`.

T-023 wurde am 2026-08-24 vom autonomen Planungsagenten (Autorisierung der
Projektleitung vom 2026-08-23) auf `READY` gesetzt. Auswahlkette über alle
DRAFT-Einträge: Der tabellarisch frühere DRAFT `T-011` bleibt blockiert, weil
seine native Windows-/macOS-Build-/Smoke-/Paketmatrix vorhandene Runner,
Signier-/Notarisierungsentscheidungen (Q-OPS-002) und physische Zielhardware
voraussetzt, die hier nicht vorhanden sind und nicht stillschweigend angenommen
werden dürfen; `T-022` bleibt durch die tolerierte Benchmarkstreuung aus
Q-TEC-010 blockiert (Klärungsprotokoll 2026-08-24 nennt T-022 ausdrücklich
weiterhin blockiert); `T-030`/`T-031` hängen an echten Produktentscheidungen
(Q-GAM-001 bis Q-GAM-007, Q-NAR-002, Q-TEC-006); `T-040`/`T-041`/`T-050`/
`T-051` liegen hinter E-004/E-005 beziehungsweise hinter getrennten
Review-, LFS- und Backup-Freigaben. Damit ist `T-023` der höchstpriorisierte
DRAFT-Auftrag mit erfüllten Abhängigkeiten (`T-010`, `T-020`, `T-021` sind
`DONE`) und folgt der ausdrücklichen Priorisierung des integrierten
Repräsentativitätsnachweises nach den isolierten Baselines sowie ADR 006:
der Belastungsframe hat Vorrang vor weiterer allgemeiner
Produktionsinfrastruktur, und zusätzliche Toolarbeit darf den ersten
Performancebeweis nicht verdrängen. Blockerbehandlung ohne stille
Produktannahme: Q-OPS-001 gilt entsprechend der protokollierten
T-020-/T-021-Behandlung (Klärungsprotokoll in `docs/OFFENE_FRAGEN.md`);
Q-TEC-008/Q-TEC-009 und die Q-TEC-010-Streuung bleiben ausdrücklich außerhalb
des Auftrags. Reversible Entscheidungen der Freigabe mit Rückrollweg:
die simulierte Komponente wiederverwendet `Riftward.Simulation` unverändert
gemäß `docs/SIMULATIONSVERTRAG.md` V1; der Szeneninhalt entsteht
deterministisch zur Laufzeit als Graybox ohne Shipping-Asset (T-050 bleibt
unberührt); ein einzelner opt-in Frameabgriff dient als begrenztes visuelles
Evidenzartefakt nach Media-Lab-Prüfung (`docs/communication/MEDIA_LAB.md`),
ist lokal, hashgebunden, auf Graybox-Lastbelegung begrenzt und niemals
Gameplay-, Atmosphären- oder Shipping-Beleg; sämtliche Gatewerte stammen
unverändert aus `docs/PERFORMANCE_BUDGET.md`, dem AC-T010-07/T-020/T-021-
Präzedenz und dem Simulationsvertrag. Der Auftrag liegt als
`.ai/tasks/T-023-representative-load-frame.json` vor und implementiert den
integrierten Anteil des G-PERF-Gates (`BENCH-REPRESENTATIVE`) ohne
Budgetänderung; Pflichtprofile bleiben bis zur Benennung von Referenzrechnern
`NOT-MEASURED`.

Die Spezifikation wurde am 2026-08-24 durch den unabhängigen Reviewlauf
`01M0TQQ9QVH8WBBMYGBA36RE4K` (Akteur `t023-spec-reviewer`) geprüft: Task-
Manifest gegen `.ai/schemas/task.schema.json` gültig, Szenario-/Gatewerte
stimmen zeichenweise mit `docs/PERFORMANCE_BUDGET.md`, dem AC-T010-07/T-020/
T-021-Präzedenz und `docs/SIMULATIONSVERTRAG.md` V1 überein; Auswahlkette,
Blockerbehandlung und Klärungsprotokoll sind konsistent; Clean-Room-Scan ohne
Befund (keine Fremdtitel, keine Drittmedien, keine Secrets). Reparaturen im
Scope: `.ai/schemas/task.schema.json` um das optionale Feld `completionNote`
ergänzt (das abgenommene Manifest T-010 war durch seinen Abschlussvermerk
gegen `additionalProperties: false` ungueltig; Präzedenz: releaseNote/
reviewNote-Feldnachtrag des T-010-Spec-Reviews) und Feldkonvention in
`.ai/tasks/README.md` dokumentiert; die T-023-Zeile in `OFFENE_FRAGEN.md`
nennt die Simulationsvertrag-Ratifizierung (Q-TEC-004) jetzt ausdrücklich
weiterhin `OFFEN`, statt sie still als „abgenommen" zu behaupten. Alle lokalen
Gates grün (fmt/lint/build mit 0 Warnungen, Tests 172/172, security PASS,
rag-build, verify über alle Runs). Zu diesem Prüfzeitpunkt hatte die
Implementierung noch nicht begonnen; der Taskstatus bleibt `ready`.

T-023 wurde am 2026-08-25 durch den unabhängigen Review-/Vollendungslauf
`01M0V8V4RVW9V77S94AXVK9EXK` (Akteur `t023-review-completion`) geprüft,
vollendet und auf `DONE` gesetzt: Der Implementierungslauf
`01M0TWMNGRTYQJA414M5DCEEYE` hatte Code, Tests und Doku geliefert, den
fensterpflichtigen Nachweis aber kontrolliert offen gelassen (kopflose Sitzung,
Exit 19). Die Reparatur umfasst zehn In-Scope-Defekte, darunter die fachlich
gewichtigen: Terrain-Indexpuffer mit INDEX32-Flag über Uint16-Daten
(Hauptansicht praktisch leer), Renderzustandsbits außerhalb der Pin-Bedeutung
(Cull/WriteZ/Blend), Kameraflug mit Augen­höhen bis −60 m unterhalb der
Landschaft, Captureindex hinter dem vorrechneten Kamerahorizont,
Tickzeitmesspunkt nach statt vor der Komposition sowie 234 KiB Hotpath-
Allokationen je Warmframe (Kameraprefix-Regeneration O(n²), Partikeltint- und
Platzierungsarrayallokationen) gegenüber dem Grenzwert 1 KiB. Der native Build
stellt Shim-Artefakte jetzt eingabehashgesteuert neu (keine stale `.so` mehr).
Evidenz auf dem Entwickler-PC (i7-3770/RX 570, Mesa radeonsi via virtuellem
kwin_wayland/Xwayland): zwei Fresh-Prozessläufe mit Gate pass und identischen
Hashketten (Ende `56d98265914d9196…`), Fremdseed ändert den Endhash
nachweislich; Frame-p99 ≈ 18 ms (vsyncgebunden), Tick-p99 ≈ 1,03 ms,
GPU-p99 ≈ 2,11 ms, Allokationen 1,3 B je Warmframe, GC-Pausen 0. Opt-in
Einzelabgriff als 1920×1080-BMP an Frame 1470 strikt nach dem Messfenster,
artefakthashgebunden, Aussagegrenze Graybox-Lastbelegung; Pflichtprofile
bleiben `NOT-MEASURED` (Q-OPS-001). Abnahmedokument:
`docs/abnahme/T-023-representative-load-frame.md`.

Nach T-010–T-023 stehen die isolierten und integrierten Performancebaselines
des G-PERF-Kerns.

T-022 wurde am 2026-08-25 vom autonomen Planungsagenten (Autorisierung der
Projektleitung vom 2026-08-23) auf `READY` gesetzt. Auswahlkette über alle
DRAFT-Einträge: Der tabellarisch frühere DRAFT `T-011` bleibt blockiert, weil
seine native Windows-/macOS-Build-/Smoke-/Paketmatrix vorhandene Runner,
Signier-/Notarisierungsentscheidungen (Q-OPS-002/Q-OPS-003) und physische
Zielhardware voraussetzt, die hier nicht vorhanden sind und nicht
stillschweigend angenommen werden dürfen; `T-030`/`T-031` hängen an echten
Produktentscheidungen (Q-GAM-001 bis Q-GAM-007, Q-NAR-002, Q-TEC-006), die
einem Agenten nicht delegierbar sind; `T-040`/`T-041` liegen hinter
E-004/E-005; `T-050`/`T-051` liegen hinter den getrennten Generator-,
Storage-/Backup- und LFS-Freigaben (Q-AST-001/Q-AST-002) sowie hinter
E-005/E-006; `T-008` steht auf `REVIEW` und ist kein DRAFT. Damit ist `T-022`
der höchstpriorisierte DRAFT-Auftrag mit erfüllten Abhängigkeiten (`T-010`,
`T-020`, `T-021` sind `DONE`; Simulationskern, bench-Befehlsvertrag,
Telemetrie- und Budgetgate-Muster existieren). Blockerbehandlung ohne stille
Produktannahme: Q-TEC-004 gilt als verfahrensmäßig behandelt — der
Simulationsvertrag V1 wird unverändert wiederverwendet, seine Ratifizierung
bleibt über Q-TEC-004 ausdrücklich `OFFEN` (Präzedenz T-023); Q-OPS-001 folgt
der protokollierten T-020-/T-021-/T-023-Behandlung (Entwickler-PC-Läufe sind
diagnostische Baseline, Pflichtprofile bleiben `NOT-MEASURED` mit Eskalation).
Die tolerierte Benchmarkstreuung (Rest von Q-TEC-010) wird in diesem Auftrag
weder definiert noch verbraucht: sämtliche Soak-Gates entscheiden
ausschließlich gegen absolute Grenzwerte (kein Absturz, Fortschritts-Watchdog,
Speicherwachstum, Allokationsgrenze je warmem Tick laut Simulationsvertrag,
Hashkettenintegrität gegen eine Golden-Fixture), und die fensterweise
Tickzeitdrift wird rein diagnostisch ohne Gatekopplung ausgewiesen; sie
blockiert den Auftrag daher nicht mehr (Klärungsprotokoll 2026-08-25,
rückrollbar). Der numerische Leak-Schwellwert ist gemäß NF-002 („genaue
Schwelle im Spike") und `QUALITAET.md` („bis zum Baseline-Spike OFFEN")
ausdrücklich spike-designiert und entsteht im gatenden Abschnitt 0 als
versionierter Soakvertrag `docs/SOAKVERTRAG.md` nach dort vollständig fixierten
Kriterien (Kalibrierbasis, doppelte Schwellwertform, Kapselung, Verschärfung
erlaubt, jede Lockerung eskaliert). Media-Lab-Prüfung gemäß
`docs/communication/MEDIA_LAB.md`: kein visuelles Artefakt, weil ein headless
Zuverlässigkeitslauf keinen sichtbaren Szenengehalt besitzt und die
maschinenlesbare Telemetrie die prüfbare Evidenz ist; eine Kurvenvisualisierung
bleibt MEDIA-05 vorbehalten. Der Auftrag liegt als
`.ai/tasks/T-022-deterministic-replay-soak.json` vor, ist gegen
`.ai/schemas/task.schema.json` gültig und implementiert den
Zuverlässigkeitsanteil von Z-002/NF-002 ohne Budgetänderung; dieser
Freigabelauf hat keinen Produktcode implementiert. Da das .NET-SDK in der
Planungssitzung nicht verfügbar war, bleibt die Ausführung der lokalen Gates
(fmt/lint/build/test/security/verify) ausdrücklicher Pflichtteil des
Implementierungs- und Reviewlaufs.

Die Spezifikation wurde am 2026-08-25 durch den unabhängigen Reviewlauf
`01M0VCJZ0KRSA2Y1ZSMRWSV2RW` (Akteur `t022-spec-reviewer`) geprüft: Task-
Manifest gegen `.ai/schemas/task.schema.json` gültig (13/13 Manifeste unter
`.ai/tasks/`); Soak-Gateanker zeichengleich gegen NF-002 in
`ANFORDERUNGEN.md`, die Spike-Klausel in `QUALITAET.md` (Leak-Schwellwert
bleibt bis zum Baseline-Spike `OFFEN`), `PERFORMANCE_BUDGET.md` (Budgetlinien
dienen ausschließlich als obere Grenzen; kein Budgetwert berührt),
`docs/SIMULATIONSVERTRAG.md` V1 (genau 250 vollständige Agenten, 20-Hz-Tick,
`fnv1a64-canonical-chain-v1`, `xorshift64star-group-script-v1`,
Allokationsgrenze 0 Bytes je warmem Tick) und den Exitcodevertrag in
`NATIVE_UNTERBAU.md`; Auswahlkette über alle DRAFT-Einträge und die
Blockerbehandlung sind konsistent zu `OFFENE_FRAGEN.md`; die tolerierte
Benchmarkstreuung (Q-TEC-010) bleibt ausdrücklich offen und wird weder
definiert noch verbraucht, und die fensterweise Tickzeitdrift bleibt ohne
Gatekopplung diagnostisch. Clean-Room-Scan ohne Befund (keine Fremdtitel,
keine Stilvorgaben, keine Drittmedien, keine Secrets). Reparatur im Scope:
Freigabevermerk-Tippfehler im Manifest korrigiert (`gemuess` → `gemaess`,
MEDIA-05-Kasus). Alle lokalen Gates grün (fmt/lint PASS, build mit 0
Warnungen, Tests 184/184, security PASS, rag-build, verify über alle Runs;
das .NET SDK 10.0.110 wurde hierfür gemäß gepinntem Bootstrap SHA-512-geprüft
beschafft). Zu diesem Prüfzeitpunkt hatte die Implementierung nicht begonnen;
der Taskstatus bleibt `ready`.

Die verbleibenden DRAFT-Einheiten hängen an Referenzhardware- und
Runner-Benennung (Q-OPS-001/Q-OPS-002 für `T-011`), an getrennten
Generator- und Storage-/Backup-Freigaben (`T-050`/`T-051`) beziehungsweise an
echten Produktentscheidungen (`T-030`/`T-031`, dahinter `T-040`/`T-041`).

## Vorlage für eine Umsetzungseinheit

### T-XXX – Kurzer ergebnisorientierter Titel

- **Status:** OFFEN
- **Zweck:** Warum wird das gebraucht?
- **Ergebnis:** Welcher beobachtbare Zustand soll entstehen?
- **Enthalten:**
- **Nicht enthalten:**
- **Abhängigkeiten:**
- **Betroffene Anforderungen:**
- **Abnahmekriterien:**
  - [ ] Konkretes, von außen prüfbares Ergebnis
- **Erforderliche Tests:**
- **Dokumentation:**
- **Offene Punkte:**
