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
| T-031 | E-004 | versioniertes atomares Save/Load besteht Roundtrip-, Abbruch-, Korruptions- und Wiederherstellungsfixtures | Z-001, F-005, NF-002 | L | MUST | DONE |
| T-032 | E-004 | interaktive Graybox-Kommandoschleife: Auswahl, Gruppenbewegung und Kamera über dem unveränderten Simulationskern | Z-001, F-001, NF-001, NF-003, NF-005, NF-007, NF-008 | L | MUST | DONE |
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

Am 2026-08-26 übernahm der autonome Planungsagent (Autorisierung der
Projektleitung vom 2026-08-23) die am 2026-08-25 bestätigte
Quality-First-Direktive als akzeptierte querschnittliche Entscheidung in
`PROJEKT.md` und das ADR-Register (`docs/entscheidungen/
007-quality-first-produktdirektive.md`), getrennt von jeder Taskarbeit:
Gameplay, Grafik, Atmosphäre, Performance und Softwarequalität bleiben
gleichwertige Pflichtdimensionen; reversible Spiel-/Gestaltungsentscheidungen
brauchen Alternativen, Hypothesen, Playtestkriterien und Rückrollweg; Lange
Gates laufen asynchron nur auf eingefrorenen Kandidaten; ein Termin erzeugt
keine Freigabe.

T-031 wurde am 2026-08-26 vom autonomen Planungsagenten (Autorisierung der
Projektleitung vom 2026-08-23) auf `READY` gesetzt. Auswahlkette über alle
DRAFT-Einträge: `T-011` bleibt blockiert, weil seine native Windows-/macOS-
Build-/Smoke-/Paketmatrix vorhandene Runner, Signier-/Notarisierungs-
entscheidungen (Q-OPS-002/Q-OPS-003) und physische Zielhardware voraussetzt,
die hier nicht vorhanden sind und nicht stillschweigend angenommen werden
dürfen; `T-030` bleibt an echten kreativen Produktentscheidungen gebunden
(Q-GAM-001 bis Q-GAM-007, Q-NAR-002) — sie bestimmen konkrete Inhaltssubstanz
und wären gemäß ADR 007 nur mit Alternativen, begründeten Hypothesen,
Playtestkriterien und Rückrollweg auf bespielbaren Artefakten zu treffen, nicht
massenhaft in einem Freigabelauf; `T-040`/`T-041` liegen hinter E-004/E-005;
`T-050`/`T-051` hinter den getrennten Generator-, Storage-/Backup- und
LFS-Freigaben (Q-AST-001/Q-AST-002); `T-008` ist `REVIEW`. Damit ist `T-031`
der höchstpriorisierte DRAFT-Auftrag mit erfüllten Abhängigkeiten (`T-010`
liefert Host-, Befehls- und Exitcodevertrag, `T-021` den unverändert
wiederverwendeten Simulationskern samt Zustands-Hashvertrag; Präzedenz
T-022/T-023). Blockerbehandlung ohne stille Produktannahme: Q-TEC-006 wird
ausschließlich im Teilaspekt Save-Umschlag/Persistenzformat verfahrensmäßig in
den gatenden Abschnitt 0 des Auftrags `.ai/tasks/T-031-atomic-save-load.json`
überführt (versionierter Savevertrag `docs/SAVEVERTRAG.md` nach fixierten
Kriterien mit Alternativen, Gründen und Rückrollweg; Spike-Klausel in
`docs/QUALITAET.md`, Klärungsprotokoll in `docs/OFFENE_FRAGEN.md`); die Anteile
Cooked-Paket-, Definitions- und Replayformat bleiben ausdrücklich OFFEN.
Q-GAM-007 blockiert nachweislich nicht: Mechanik und Garantien (atomare
Ersetzung, Validierung vor Aktivierung, Erhalt des letzten gültigen Standes,
Korruptionsabweisung) sind bereits durch die akzeptierten UF-002-Fehlerfälle,
den DATENMODELL-Lebenszyklus und ARCHITEKTUR.md verbindlich festgelegt und
hängen an keiner Inkapazitäts-/Checkpointpolitik; die Frage bleibt OFFEN und
out of scope. Media-Lab-Prüfung: kein visuelles Artefakt, weil persistente
Zustandsdaten keinen sichtbaren Szenengehalt besitzen. Der Auftrag folgt ADR
007 als Infrastruktur-Evidenz vor Contentskalierung; dieser Freigabelauf hat
keinen Produktcode implementiert und keinen Commit erstellt — die formale
Schema-Validierung mit Repository-Werkzeug bleibt Pflichtteil des
Spec-Reviewlaufs.

Die Spezifikation wurde am 2026-08-26 durch den unabhängigen Reviewlauf
`01M0XNCAJETH6R619V1TCK6QFP` (Akteur `t031-spec-reviewer`) geprüft: Das Task-
Manifest ist gegen `.ai/schemas/task.schema.json` mit dem gepinnten
JsonSchema.Net 8.0.5 gültig (14/14 Manifeste unter `.ai/tasks/`, inklusive
`T-031`); Auswahlkette über alle DRAFT-Einträge und Blockerbehandlung sind
konsistent zu `OFFENE_FRAGEN.md`; die Belege wurden gegen `ANFORDERUNGEN.md`
(F-005, NF-002, NF-003, NF-007, NF-008), `USER_FLOWS.md` UF-002-Fehlerfälle,
`DATENMODELL.md` (SaveEnvelope-Pflichtfelder, Spielstand-Lebenszyklus,
Golden-Fixturliste), `ARCHITEKTUR.md` (Persistenzzeile,
Vertrauensgrenzentabelle), `SIMULATIONSVERTRAG.md` V1
(`fnv1a64-canonical-chain-v1`), die Spike-Klausel in `QUALITAET.md`, den
Befehls-/Exitcodevertrag in `NATIVE_UNTERBAU.md` sowie
`communication/MEDIA_LAB.md` geprüft; ADR 007 wurde zeichentreu gegen die am
2026-08-25 bestätigte Quality-First-Direktive übernommen; Clean-Room- und
Secret-Scan ohne Befund. Reparatur im Scope des neuen Manifests: `AC-T031-06`
und die Testmatrix beriefen sich auf die DATENMODELL-Fixturliste, verschwiegen
deren Klasse „finalitätsnah gültig“ aber stillschweigend; beide Stellen nennen
die vor T-030-/T-051-Inhalt nicht darstellbare Klasse jetzt ausdrücklich als
dokumentierte Zurückstellung ohne Abschwächung. Eine davon unabhängige zweite
Reparatur wurde bewusst nicht vorgenommen. Test-, Fixture-, Build-, CI- oder
Evidenzpfade wurden nicht berührt, daher ist der Fresh-Checkout-/Clean-Archive-
Vertrag durch diesen Lauf nicht ausgelöst. Alle lokalen Gates grün (fmt/lint
PASS, build mit 0 Warnungen, Tests 205/205, security PASS, rag-build, Schema
14/14, verify über alle Runs). Zu diesem Prüfzeitpunkt hatte die
Implementierung nicht begonnen; der Taskstatus bleibt `ready`.

T-031 wurde am 2026-08-26 durch den unabhängigen Review-/Vollendungslauf
`01M0XYZEQJSGJSFEHKJ4GVCV2N` (Akteur `t031-review-completion`) geprüft,
vollendet und auf `DONE` gesetzt: Der Builder-Lauf
`01M0XYH3VXYBXRTGV1BP0JKEAS` hatte Savevertrag V1 (`docs/SAVEVERTRAG.md`),
das BCL-only-Runtimeprojekt `Riftward.Save` (kanonische Binärcodierung,
doppelter SHA-256-Anker, strikter Einzelpass-Validator mit neun
unterscheidbaren Verletzungsklassen, atomares Slotprotokoll, Migrationsregel
ohne Erfindung, UnsafeAccessor-Zustandsbindung am unveränderten
Simulationskern), den Befehl `rift.sh savecheck` mit NF-007-Report und 13 neue
Tests geliefert. Die Review-Sitzung führte alle Gates selbst aus (lint PASS,
Release-Build 0 Warnungen, Tests 218/218, security PASS) und reparierte drei
In-Scope-Defekte des Primärslices ohne Berührung eines zweiten bereits
akzeptierten Task-Manifests: das nur dokumentierte, nie implementierte
Phantom-Flag `--continuation-ticks` wurde aus dem Befehlsvertrag entfernt; der
in keinem Test belegte Verzeichnis-Sync-Schritt wurde aus Atomarprotokoll und
Savevertrag genommen und als explizites Restrisiko mit Rückrollweg geführt
(BCL-Primitive fehlt; der T-010-Architekturtest hält Native-Imports in der
Plattformsschicht — ein libc-PInvoke-Versuch scheiterte dort kontrolliert);
der Vertragsspiegeltest wurde von einer toten Verzweigung befreit und bindet
jetzt alle vier maschinenlesbaren Kennungen. AC-T031-05 blieb unangetastet.
Eigener autoritativer Lauf: Exitcode 0, Gate pass, 19/19 Prüfklassen,
Kalibrierbasis 30040/30040 Bytes (Faktor 4 → Grenzwert 120160),
`payloadHash` builderidentisch, Fortsetzungskette byteidentisch zum
unterbrochenen Referenzlauf; `Riftward.Simulation`, Budgetdatei und
`.ai/tasks/` byteidentisch unverändert. Der Fresh-Checkout-/Clean-Archive-
Nachweis gemäß Präzedenz T-022-fresh-review wurde am hypothetischen
Kandidatenbaum ausgeführt; Details im Abnahmedokument
`docs/abnahme/T-031-atomic-save-load.md`. F-005 bleibt anteilig offen
(Content-Payload erst mit T-030/T-051); Q-TEC-006/Q-TEC-004 bleiben OFFEN.

T-032 wurde am 2026-08-26 vom autonomen Planungsagenten (Autorisierung der
Projektleitung vom 2026-08-23) als erster Slice der T-030-Zerlegung auf `READY`
gesetzt. Auswahlkette über alle DRAFT-Einträge: `T-011` bleibt blockiert, weil
seine native Windows-/macOS-Build-/Smoke-/Paketmatrix vorhandene Runner,
Signier-/Notarisierungsentscheidungen (Q-OPS-002/Q-OPS-003) und physische
Zielhardware voraussetzt, die hier nicht vorhanden sind und nicht
stillschweigend angenommen werden dürfen; `T-008` ist `REVIEW`;
`T-040`/`T-041` liegen hinter E-004/E-005; `T-050`/`T-051` hinter den
getrennten Generator-, Storage-/Backup- und LFS-Freigaben (Q-AST-001/
Q-AST-002) sowie hinter E-005/E-006. Der XL-Gesamtauftrag `T-030` bleibt
bewusst `DRAFT`: Seine sieben verschränkten kreativen Entscheidungen
(Q-GAM-001 bis Q-GAM-007, Q-NAR-002) bestimmen konkrete Inhaltssubstanz und
wären gemäß ADR 007 nur mit Alternativen, begründeten Hypothesen,
Playtestkriterien und Rückrollweg auf bespielbaren Artefakten zu treffen, und
der ungeschnittene Umfang verstößt gegen die DoR-Schnittregel in
`QUALITAET.md`. Stattdessen wurde die dokumentiert rückrollbare Zerlegung von
`T-030` in geordnete Slices beschlossen (Klärungsprotokoll 2026-08-26 in
`docs/OFFENE_FRAGEN.md`); T-032 ist ihr erster Abschnitt und erzeugt genau das
bislang fehlende bespielbare Prerequisite für alle playtestbasierten
Kreativentscheidungen, ohne eine einzige von ihnen vorwegzunehmen: Er benutzt
ausschließlich die akzeptierten Arbeitsannahmen und Verträge — den
unveränderten Simulationskern mit seinem bestehenden öffentlichen
Befehlssurface (`SimCommandKind.GroupMoveToZone`, Gruppen-/Zonengranularität),
die Vertragswelt `riftward-simworld-graybox-v1`, den T-010-Host- und
Eingabevertrag sowie die T-020/T-023-Telemetrie-, Gate- und Abgriffmuster.
Die minimale Verbmenge (Punktauswahl, Rahmenauswahl, Deselektion,
Gruppenbewegung zur Zone) entsteht als vorregistrierte Testhypothese im
gatenden Abschnitt 0 des Auftrags `.ai/tasks/T-032-graybox-command-loop.json`
(versionierter Kommandovertrag `docs/KOMMANDOVERTRAG.md`, Spike-Klausel in
`docs/QUALITAET.md`); Q-GAM-001 bis Q-GAM-004, die Q-GAM-005-Details,
Q-GAM-006, Q-GAM-007 und Q-NAR-002 bleiben ausdrücklich `OFFEN`; Q-OPS-001
folgt der protokollierten T-020-/T-021-/T-022-/T-023-Behandlung; Q-TEC-004/
Q-TEC-006/Q-TEC-010 werden nicht berührt. Media-Lab-Prüfung gemäß
`docs/communication/MEDIA_LAB.md`: ein begrenztes visuelles Evidenzartefakt
ist — wie bereits beim T-023-Belastungsframe mit Graybox-Lastbelegung —
angezeigt und wieder als opt-in Einzelabgriff nach demselben Muster
vorgesehen, weil interaktiver Auswahl-/Markerzustand visuell prüfbarer ist;
es bleibt lokal, hashgebunden, auf Graybox-Zustandsbelegung beschränkt und
niemals Gameplay-, Atmosphären- oder Shipping-Beleg. Das Manifest ist gegen
`.ai/schemas/task.schema.json` mit dem gepinnten JsonSchema.Net 8.0.5 gültig
(15/15 Manifeste unter `.ai/tasks/`); dieser Freigabelauf hat keinen
Produktcode implementiert und keinen Commit erstellt.

Die Spezifikation wurde am 2026-08-26 durch den unabhängigen Reviewlauf
`01M0Y4W40T5ZPESPWN2H0XH1N2` (Akteur `t032-spec-reviewer`) geprüft: Das Task-
Manifest ist gegen `.ai/schemas/task.schema.json` mit dem gepinnten
JsonSchema.Net 8.0.5 gültig (15/15 Manifeste unter `.ai/tasks/`); alle fachlichen
Anker wurden eigenständig gegen Code und Verträge belegt (`SimCommandKind.
GroupMoveToZone` mit kanonischer Ordnung in `Riftward.Simulation`, Welt
`riftward-simworld-graybox-v1`, `NavWorld.ZoneCount = 6`, Budgetzeile
Eingabe-zu-Reaktion 100 ms Ziel/150 ms hart in `PERFORMANCE_BUDGET.md`,
freie Exitcodes 35 bis 38 bei dokumentiertem Bestand bis 34 inklusive
wiederverwendeter Codes 19/27/28, Abhängigkeiten T-010/T-020/T-021/T-023
durchweg `DONE`); die Auswahlkette über alle DRAFT-Einträge, die T-030-
Zerlegungsentscheidung mit Klärungsprotokoll und die Offenhaltung von Q-GAM-001
bis Q-GAM-007 sowie Q-NAR-002 sind konsistent zu `OFFENE_FRAGEN.md`, ADR 007,
der DoR-Schnittregel und Spike-Klausel in `QUALITAET.md`; GS-007 ist als
GAME_DESIGN-Pfeiler existent; Media-Lab-Bindung des opt-in Einzelabgriffs nach
T-023-Muster ist vertragsgemäß. Clean-Room- und Secret-Scan ohne Befund
(keine Fremdtitel, keine Drittmedien, keine lokalen Pfade). Reparaturen im
Scope des neuen Materials: die historisch falsche Behauptung, ein visuelles
Evidenzartefakt sei „hier erstmals angezeigt“ (T-023 lieferte bereits einen
opt-in Einzelabgriff mit Aussagegrenze Graybox-Lastbelegung), wurde in
Manifest und Freigabeabsatz auf die ausdrückliche T-023-Präzedenz umformuliert;
der Doppeltippfehler „als als“ im Manifest wurde behoben. Kein zweites
bereits akzeptiertes Task-Manifest wurde angetastet; eine unabhängige zweite
Reparatur liegt nicht vor. Test-, Fixture-, Build-, CI- oder Evidenzpfade
wurden nicht berührt, daher ist der Fresh-Checkout-/Clean-Archive-Vertrag
durch diesen Lauf nicht ausgelöst. Alle lokalen Gates grün (fmt/lint PASS,
build mit 0 Warnungen, Tests 218/218, security PASS, rag-build, Schema 15/15).
Zu diesem Prüfzeitpunkt hatte die Implementierung nicht begonnen; der
Taskstatus bleibt `ready`.

T-032 wurde am 2026-08-26 durch den unabhängigen Review-/Vollendungslauf
`01M0YJE3T8FDQ3T9QWJJY4DTNW` (Akteur `t032-review-completion`) geprüft,
vollendet und auf `DONE` gesetzt: Der Builder-Lauf `01M0Y8GA8T1H16TVPMV06QVKWY`
hatte Abschnitt 0 (`docs/KOMMANDOVERTRAG.md` V1), das BCL-only-Projekt
`Riftward.Session`, den Befehl `rift.sh kommandoschleife` (headless und
fensterpflichtig, Exitcodes 35–38), Report Schemaversion 1, 18 neue Tests und
die Dokumentation geliefert. Die Review-Sitzung führte alle Gates selbst aus
(lint PASS, Release-Build 0 Warnungen, Tests 236/236, security PASS,
rag-build, verify valid) und reparierte fünf In-Scope-Defekte des Primärslices
ohne Berührung eines zweiten bereits akzeptierten Task-Manifests: die gegen
die eigene API invertierte Zoomrichtung (Keys und Mausrad), den fehlenden
vertraglichen Ausweis des Kettenkriteriums im Interaktivreport
(`gate.stateChainSelfConsistency`, Kommandovertrag §7, mit Schema-, Golden- und
Negativtestbindung), eine Satzfusion im G-PERF-Registertext, den
achsenfalschen Clamp bei Live-Auswahlintents sowie einen Kommentardefekt am
Intent-Codec; zusätzlich befreite sie den displaylosen CLI-Vertragstest von
seiner stillen Abhängigkeit von gitignorierten Native-Artefakten (Erwartung 19
mit, Code 14 ohne Artefaktmanifest; beides kontrolliert ohne Report). Eigene
Evidenz: zwei Fresh-Prozessläufe mit builderidentischem Endhash
`978aab19406daa26`, Fremdseed ändert den Endhash nachweislich, autoritativer
Lauf Exit 0 mit p99 0,961 ms / Allokation 0 Bytes / max reactionTicks 1;
Regressionen bench-sim, savecheck und soak-Kurzlauft grün. Da Test-, Fixture-,
Build- und Evidenzpfade berührt wurden, führte die Sitzung den
Fresh-Checkout-/Clean-Archive-Nachweis gemäß Präzedenz selbst aus:
indexfreie Rekonstruktion des hypothetischen Kandidatenbaums
`2e0845bf62b28e483e1067e5775b0b84e42230e5` (exakt 36 Auftragspfade, kein
zweites Task-Manifest), volle Gatefolge aus Archivbytes inklusive Testsuite
236/236 und identischem Endhash, `NO_DRIFT` gegen die Zweitextraktion. Der
manuelle Interaktivsmoke und der opt-in Einzelabgriff bleiben wegen der
displaylosen Umgebung (kontrollierter Abbruch Code 19, kein simulierter Beweis)
ausdrückliche Restpunkte einer Displaysession; Details in
`docs/abnahme/T-032-graybox-command-loop.md`. Eine unabhängige zweite
Reparatur wurde bewusst verschoben: Der Asset-Lane-Git-Check scheitert in
reinen `git archive`-Extraktionen ohne `.git` (`ASSET_GIT_CHECK_FAILED`,
`tools/RiftHarness/Assets.fs`, T-003/T-006-Fläche); er ist als eigener
späterer Slice nachzutragen.

Am 2026-08-26 prüfte eine weitere unabhängige Frisch-Review-Sitzung
(Harness-Run `01M0YQ71P5684F9PQR7T536QKS`, Akteur
`t032-fresh-review-completion`) den vollständigen Arbeitsstand erneut, führte
alle Gates und autoritativen Läufe selbst aus (lint/build/test 236/236/
security/rag-build/verify grün; zwei Fresh-Prozessläufe mit identischem
Endhash `eeeb63be320e6e6f`, Fremdseed ändert den Endhash nachweislich;
Regressionen bench-sim, savecheck und Soak-Kurzlauf grün) und reparierte drei
In-Scope-Defekte des Primärslices ohne Berührung eines zweiten bereits
akzeptierten Task-Manifests: die ungeclippte `GrayboxCamera.SetDistance`
gegen das eigene „immer geclippt"-Versprechen, den unwahren Methodenlabel des
diagnostischen Interaktivfelds `frameTimeMs` (behielt fälschlich den
T-023-Schatten-/Composite-Passbezug) und den Widerspruch zwischen
dokumentierter kontrollierter Abweisung verspäteter Live-Intents
(`RejectedLate`) und ihrer nachträglichen Ausführung in
`SessionPipeline.ProcessBoundary`, wodurch der bestehende Vertragstest nur
über die falsche Klasse bestand; die Klasse ist jetzt codegetreu gebunden.
Headless-Ketten blieben beweislich byteidentisch. Die Pfadanzahl des
vorherigen Fresh-Checkout-Absatzes wurde korrigiert (38 Auftragspfade:
20 modifiziert, 18 neu); der Nachweis dieser Sitzung läuft am exakten
Finalbaum, dessen Digest im Harness-Run gebunden ist.

Am 2026-08-26 nahm eine unabhängige Wiederaufnahme-/Review-Sitzung (Akteur
`t032-review-resume`) den durch externen systemctl-Stopp des vorigen
Reviewers unterbrochenen, beweislich unveränderten Stand wieder auf
(Identity-v3-Eingaben Parent `068974c…`, Add-A-Baum `23b98ebe…`; dessen
belasteter x1-Lauf 233/236 mit T-021 Exit 26, T-022 Exit 30 und T-032 Exit 35
ist ehrliche Last-Diagnose fail-closed Gates, kein Kandidatendefekt; alle
unbelasteten Archivläufe 236/236). Sie prüfte alle fünf Sidecar-Befunde lokal
und reparierte vier bestätigte In-Scope-Defekte des Primärslices ohne
Berührung eines zweiten bereits akzeptierten Task-Manifests: (1)
`scriptSha256` wurde über Re-Encoding dekodierten Texts statt über die
Rohbytes gebunden — der Runner liest jetzt Rohbytes, der Parser prüft die
Vertragsbytegrenze vor Dekodierung, weist nicht-UTF-8/BOM kontrolliert als
`HeaderMalformed` ab und bindet den Hash an die exakten Rohbytes
(Kommandovertrag §5 präzisiert; neuer Suiteeintrag, Testbestand 236 → 237);
(2) die interaktive Allokationsmetrik maß Fensterbeginn bis
Reportserialisierung inklusive Render-/Abgriffanteilen statt der
vertraglichen Per-Tick-Deltas um `world.Tick()` — jetzt identische Methode
zur headless Engine mit einer einzigen Auswertung für Gate und Report;
(3) nicht-`PlatformException`-Abbrüche im Interaktivpfad crashten ohne Report
mit undokumentiertem Exitcode — jetzt Teilreport Code 36 wie headless;
(4) unvollständige Läufe wiesen headless-Gründe für `Unavailable`-Felder aus
— jetzt `run-incomplete-no-evidence`; zusätzlich schlossen zwei Fixtures die
Lücken `ScriptTooLarge`/`IntentLimitTotal` in der Zehn-Klassen-Matrix, und
der CLI-Vertragstest toleriert transiente Gate-Treffer (Exit 35) gemäß
T-021-Präzedenz mit genau einem Wiederholversuch. Eigene Evidenz: lint/
build/test 237/237/security/rag-build/verify grün; zwei Fresh-Prozessläufe
Endhash `978aab19406daa26` builderidentisch zum Erstreview, Fremdseed ändert
den Endhash nachweislich (`76eee99a2f05629c`); Regressionen bench-sim,
savecheck und Soak-Kurzlauf (3000 Ticks) grün; Kontrolllauf 600 Ticks → Exit
30 (nur ein RSS-Fenster, Trend NaN, korrektes Fail-Closed, Parameterfrage).
Der Fresh-Checkout-/Clean-Archive-Nachweis lief am exakten Finalbaum
(exakt ein Task-Manifest; der Baumdigest ist in der gitignorierten
Sitzungsevidenz `artifacts/t032-review4/final-candidate-tree.txt` gebunden),
volle Gatefolge aus Archivbytes inklusive Testsuite,
`NO_DRIFT` gegen die Zweitextraktion; Details im Abnahmedokument. Zwei
unabhängige Befunde wurden bewusst verschoben und sind als eigene spätere
Slices nachzutragen: (a) Die Reaktionsmetrik ist konstruktionsbedingt stets
1 Tick (`V == S` an derselben Vorgrenze); das Gatekriterium 3 ist nur über
seine Fault-Injection falsifizierbar, nicht über die Pipeline — eine
verzögerte Verbrauchssemantik wäre eine Kommandovertrag-V2-Entscheidung mit
Fixture-Regeneration. (b) `ArgumentQueue.NumberOption` fällt bei
nichtparierbaren Zahlen still auf den Default, und Warm-up-/Horizontangaben
werden still geclamped (geteilte, bereits abgenommene Fläche aller Runner;
die Skriptkopf-Bindung verhindert falsche Reports) — repo-weite Säuberung.
Die bereits zuvor verschobene Reparatur des Asset-Lane-Git-Checks in reinen
Archivextraktionen (`ASSET_GIT_CHECK_FAILED`, `tools/RiftHarness/Assets.fs`)
bleibt unverändert ein späterer Slice.

Am 2026-08-26 verifizierte eine unabhängige Abschluss-Review-Sitzung
(Harness-Run `01M0Z4XKCYH8DMSVE7K8QDHYWR`, Akteur `t032-final-review`) den
eingefrorenen Stand: Ihre indexfreie Plumbing-Rekonstruktion reproduzierte den
gebundenen Kandidatenbaum `6cef61b0…` exakt (kein Drift seit der
Wiederaufnahmesitzung; 38 Auftragspfade, genau ein Task-Manifest). Sie
reparierte zwei kleine In-Scope-Defekte des Primärslices ohne Berührung eines
zweiten bereits akzeptierten Task-Manifests: den wahrheitswidrigen
unavailable-Grund des headless `workingSetKiB`-Felds (behauptete RSS-Sampling,
das nicht existiert; jetzt `headless-session-does-not-sample-rss`) und den
Exitcode-Widerspruch bei vorzeitigem Interaktivabbruch (Code 0 statt 36 bei
passierenden Teilmetriken; Exitcode jetzt strikt an `windowCompleted` gebunden,
Report bleibt mit `run-incomplete-no-evidence` nicht-evident). Eigene Evidenz:
fmt/lint/build 0 Warnungen/test 237/237/security/rag-build/verify grün; zwei
Fresh-Prozessläufe Endhash `40c3016dc9b93325` builderidentisch und beweislich
identisch vor/nach Reparatur, Fremdseed ändert den Endhash nachweislich
(`ee70f281c1bb60b6`); Horizontabweichung → 37 ohne Report, displayloser
Interaktivlauf → 19 ohne Report; Regressionen bench-sim, savecheck und
Soak-Kurzlauf (3000 Ticks) grün. Der Fresh-Checkout-/Clean-Archive-Nachweis
lief nach allen Änderungen am neuen Finalbaum (Digest gebunden in
`artifacts/t032-final-review/final-candidate-tree.txt` und im Harness-Run):
byteidentische Doppel-Extraktion, volle Gatefolge aus Archivbytes inklusive
Testsuite und autoritativem Lauf, `NO_DRIFT` gegen die Zweitextraktion. Die
verschobenen unabhängigen Reparaturen (Asset-Lane-Git-Check, vakuoese
Reaktionsmetrik V == S als Kommandovertrag-V2-Entscheidung, stille
NumberOption-Defaults/-Clamps) bleiben unverändert spätere Slices.

Am 2026-08-26 verifizierte eine weitere unabhängige Prüf-/Vollendungs-Sitzung
(Harness-Run `01M0ZV9ARJ1PFSA64RGZ5ME4H2`, Akteur `t032-selfreview-final`) den
gesamten Arbeitsstand ohne Übernahme der Vorsitzungsbehauptungen. Zwei kleine
In-Scope-Nebenreparaturen im Primärslice ohne Berührung eines zweiten bereits
akzeptierten Task-Manifests: (1) Die Exitcode-Präzedenz des Interaktivmodus
wurde vertraglich korrigiert — ein vorzeitiger Abbruch vor Fensterabschluss
ergibt stets Code 36 auch dann, wenn ein `--capture-frame` angefordert war,
dessen Unterbleiben zuvor Code 38 maskierte; die Entscheidung liegt jetzt in
der puren Hilfsfunktion `ResolveInteractiveExitCode` und ist durch einen neuen
Suiteeintrag an die Präzedenzmatrix gebunden (Testbestand 237 → 238).
(2) Rein kosmetische Einrückungsnormalisierung in
`SessionEngine.ProcessBoundary`. Eigene Evidenz: alle schnellen Gates grün
(238/238), zwei Fresh-Prozessläufe Endhash `978aab19406daa26`
builderidentisch zu Erstreview und Wiederaufnahmesitzung, Fremdseed
`76eee99a2f05629c` abweichend, Horizontabweichung/leeres Skript → 37 ohne
Report, displayloser Interaktivlauf → 19 ohne Report; Regressionen bench-sim,
savecheck und Soak-Kurzlauf grün. Der Fresh-Checkout-/Clean-Archive-Nachweis
lief am exakten Finalbaum (Digest gebunden in
`artifacts/t032-selfreview/final-candidate-tree.txt`): byteidentische
Doppel-Extraktion, volle Gatefolge aus Archivbytes inklusive Testsuite und
autoritativem Lauf mit identischem Endhash, `NO_DRIFT` gegen die
Zweitextraktion. Die drei verschobenen unabhängigen Reparaturen bleiben
unverändert spätere Slices.

Am 2026-08-26 verifizierte eine weitere unabhängige Frisch-Review-/Vollendungs-
Sitzung (Akteur `t032-review5`) den gesamten Arbeitsstand ohne Übernahme der
Vorsitzungsbehauptungen: Lock-freie Rekonstruktion des Kandidatenbaums
`02e656df04f0470b855c2bdd73531958242fd0a0` (Parent `068974c9…`; exakt 38
Auftragspfade, genau ein Task-Manifest), alle schnellen Gates, autoritativen
Läufe und der Fresh-Checkout-/Clean-Archive-Vertrag wurden selbst ausgeführt
(zwei Fresh-Prozesspaare mit identischen Endhashes `60f311f20d9b7876` bzw.
`949194e5779aa2a1`, Fremdseed weicht nachweislich ab; 37/37/37/19-Negativfälle
ohne Report; Regressionen bench-sim/savecheck/Soak-Kurzlauf grün; volle
Archiv-Gatefolge inklusive autoritativem Lauf aus Archivbytes und
`NO_DRIFT`). Ein In-Scope-Nebenreparaturfund im Primärslice ohne Berührung
eines zweiten bereits akzeptierten Task-Manifests: Die Gold-Fixture behielt
den wahrheitswidrigen Grund `headless-session-engine-samples-rss-diagnostically`,
obwohl die Abschluss-Review den Laufzeitcode bereits auf
`headless-session-does-not-sample-rss` korrigiert hatte — das Schema validiert
diesen Wert nicht, sodass die Suite fälschlich grün blieb. Reparatur: das
einzelne Feld der Goldprobe wurde auf die echte Laufausgabe regeneriert, die
Fabrikationsmatrix weist die Kennung jetzt als Pflichtinhalt aus, und ein
neuer Suiteeintrag bindet den internen Runnerpfad `WorkingSetFrom` an den
vertraglichen Grund (Sichtbarkeitspraezedenz `ResolveInteractiveExitCode`);
Testbestand 238 → 239; headless-Ketten beweislich
unverändert. Die verschobenen unabhängigen Reparaturen bleiben unverändert
spätere Slices.

Am 2026-08-26 schloss eine weitere unabhängige Frisch-Review-Sitzung
(Harness-Run `01M101PKH0WSH1NC0BR56DQE6D`, Akteur `t032-r6-fresh-review`) den
Kandidaten ohne neuen Defektbefund ab: alle schnellen Gates (lint, Release-
Build 0 Warnungen, Tests 239/239, security, rag-build, verify), Regressionen
(bench-sim, savecheck, Soak-Kurzlauf) und autoritative Läufe wurden selbst
ausgeführt — zwei Skriptpaare mit builderidentischen Endhashes
(`2b0cd3cdd830f56d`, `6a5b7beb1d01e79a` inklusive Archivbytes), Fremdseed
weicht nachweislich ab, Negativfälle 37/37/37 ohne Report und displayloser
Interaktivlauf 19 ohne Report. Der Fresh-Checkout-/Clean-Archive-Nachweis
lief nach allen Änderungen am finalen Kandidatenbaum: private
Add-A-Rekonstruktion zweifach konvergent und deckungsgleich mit der
`t032-review5`-Bindung (vor der reinen Doku-Erweiterung dieser Sitzung),
byteidentische Doppel-Extraktion, volle Gatefolge aus Archivbytes,
NO_DRIFT über 350 getrackte Dateien; der Baumdigest ist außerhalb der
geprüften Bytes gebunden (`artifacts/t032-r6/final-candidate-tree.txt`,
Run-Zusammenfassung). Genau ein Task-Manifest verändert; die verschobenen
unabhängigen Reparaturen bleiben unverändert spätere Slices.

Am 2026-08-26 prüfte eine weitere unabhängige Frisch-Review-/Reparatursitzung
(Akteur `t032-review7`) den vollständigen Arbeitsstand ohne Übernahme der
Vorsitzungsbehauptungen und fand zwei In-Scope-Defekte des Primärslices, die
kein Test gebunden hatte, ohne Berührung eines zweiten bereits akzeptierten
Task-Manifests: (1) Die vertragliche Zweikanalrückmeldung war im
Interaktivpfad tot — `InteractiveView.NotifyCommandIssued` wurde nirgends
aufgerufen, der Befehlspuls-Kanal (Kommandovertrag §3, AC-T032-06) hätte also
auch auf einer Displaymaschine nie geglüht; die bisherige „Strukturreview" war
rein textlich und kein Suiteeintrag band die Verdrahtung. Reparatur: Der
Sitzungskern weist je Vorgrenze die tatsächlich an den Kern übergebenen
Bewegungszonen aus (`DispatchedMoveZonesOfLastBoundary`), der Runner meldet
diese Zonen an die Darstellung; vier neue Suiteeinträge binden Zonenausweis
(nur akzeptierte Befehle, Leerung je Grenze, verspätete Klasse korrekt
abgewiesen), Puls-Lebenszyklus (Anzeige und Ablauf nach 40 Ticks),
Runnerverdrahtung und den unveränderten headless Weltzustand (Endhash
`978aab19406daa26` beweislich identisch zu Erstreview/Wiederaufnahme/
Selfreview). (2) Das untrusted Eingabeskript wurde vor der Bytegrenze
unbeschränkt materialisiert; endlos liefernde Quellen wären unkontrolliert an
der Speichergrenze gestorben statt kontrolliert als `ScriptTooLarge` → 37 ohne
Report. Reparatur: begrenzende Rohbytes-Lesung an der Vertragsgrenze vor
Dekodierung, Suiteeintrag gegen Sparse-Uebergröße, exakte Grenzwertgröße und
End-of-stream-Spezialdatei. Testbestand 239 → 243. Alle Gates wurden selbst
ausgeführt: fmt/lint PASS, Release-Build 0 Warnungen, Tests 243/243,
security PASS, rag-build, verify valid. Eigene Evidenz: A-Paar identisch
`978aab19406daa26`, B-Paar identisch `fd2521bb71216ea9`, Fremdseed ändert den
Endhash nachweislich (`340b48f57e653a1a`), p99 ≤ 0,892 ms / Allokation
0 Bytes / max reactionTicks 1, Negativfälle unbekanntes Szenario/malformiert/
Horizontabweichung/uebergross je 37 ohne Report, displayloser Interaktivlauf
19 ohne Report, Regressionen bench-sim/savecheck/Soak-Kurzlauf grün. Der
Fresh-Checkout-/Clean-Archive-Nachweis lief nach allen Änderungen am exakten
Finalbaum (Digest gebunden in `artifacts/t032-review7/final-candidate-tree.txt`
und Run-Zusammenfassung): byteidentische Doppel-Extraktion, Gatefolge aus
Archivbytes inklusive Testsuite und autoritativem Lauf mit identischem
Endhash, `NO_DRIFT`. Die verschobenen unabhängigen Reparaturen bleiben
unverändert spätere Slices.

Am 2026-08-27 prüfte eine weitere unabhängige Frisch-Review-/Reparatursitzung
(Akteur `t032-review8-independent`) den vollständigen Arbeitsstand ohne
Übernahme der Vorsitzungsbehauptungen: alle schnellen Gates, autoritativen
Läufe (Frisch-Paare Skript A `1b224dd71ff5fce2` und Skript B
`8657a4eb6ac62967` mit 15 Kernbefehlen über GroupMoveToZone, Fremdseed
`b3c0186fb5d0ef1d` abweichend, Negativmatrix je 37 ohne Report,
displayloser Interaktivlauf 19 ohne Report) sowie die Regressionen
bench-sim/savecheck/Soak-Kurzlauf wurden eigenständig ausgeführt.
Ein kleiner In-Scope-Wahrhaftigkeitsdefekt des Primärslices wurde gefunden und
repariert, ohne Berührung eines zweiten bereits akzeptierten Task-Manifests:
Die vertraglichen Ablehnungsgründe `move-without-selection` (Kommandovertrag
§2) und `target-not-in-zone` (§9) existierten im Code nur unter fremden Namen
als Reportzähler; die Kennungen waren nirgends gebunden und im Interaktivpfad
erschien keine UF-001-Fehlerzeile. Reparatur strikt additiv: Vertrags-
konstanten in `SessionContract`, Feld `RejectedMoveWithoutSelection` je
Vorgrenze, UF-Zeilen mit den verbatim-Kennungen am Live-Pfad; zwei neue
Suiteeinträge binden Kennung und Ausgabepfade (Testbestand 243 → 245);
headless-Ketten blieben beweislich unverändert (identische Endhashes vor/nach
Reparatur). Der Fresh-Checkout-/Clean-Archive-Nachweis lief nach allen
Änderungen am exakten Finalbaum (Digest gebunden in
`artifacts/t032-review8/final-candidate-tree.txt`; indexfreie Plumbing-
Rekonstruktion via `hash-object`/`mktree`, da die Umgebung jeden Indexzugriff
fail-closed sperrte); Details im Abnahmedokument. Die verschobenen
unabhängigen Reparaturen bleiben unverändert spätere Slices.

Am 2026-08-27 prüfte eine erneute unabhängige Frisch-Review-/Vollendungs-
Sitzung (Akteur `t032-review9-independent`) den vollständigen Arbeitsstand
ohne Übernahme der Vorsitzungsbehauptungen: alle schnellen Gates eigenständig
grün (lint PASS, Release-Build 0 Warnungen, Testsuite 245/245, security PASS,
rag-build, verify valid), autoritative Läufe mit selbst komponierten
Eingabeskripten (Skript A `22495b1823291d5f`, Skript B `44f5cc692e49e038`
jeweils builderidentisch; Fremdseed `5a24d8044c5799b5` abweichend;
kernelCommandsTotal 33 auf B), Negativmatrix je 37 ohne Report, displayloser
Interaktivlauf 19 ohne Report sowie Regressionen bench-sim/savecheck/
Soak-Kurzlauf grün. Zwei kleine In-Scope-Nebenreparaturen im Primärslice,
ohne Berührung eines zweiten bereits akzeptierten Task-Manifests: der
Portabilitätsabsatz der Vorgängersitzung trug veraltete Zahlen weiter
(`243/243`, Endhash `978aab19406daa26`) gegen die eigenen Suiteprotokolle und
Reports jener Sitzung (245/245, `1b224dd71ff5fce2`) — berichtigt samt
Transparenznotiz; zusätzlich wurde ein irreführender Kommentar an
`GrayboxIntent.PointSelect` auf die vertragliche Semantik korrigiert.
Headless-Ketten blieben beweislich identisch vor/nach Reparatur. Empfangene
Identität: indexfreie Rekonstruktion zweifach konvergent (Vorbindung
`5203fcc65cc262f5176a63d3809ebcb8c0cae6b3`; Finalbaumbindung dieser Sitzung im
Abnahmedokument). Beobachtet und dem verschobenen ArgumentQueue-Slice zuge-
ordnet (nicht hier gebündelt): still tolerierte unbekannte Positionsargumente
in der geteilten Runnerfläche. Die übrigen verschobenen Reparaturen bleiben
unverändert spätere Slices.

Am 2026-08-27 prüfte eine erneute unabhängige Frisch-Review-/Vollendungs-
Sitzung (Harness-Run `01M10FTZC62KX9V2MT5XS6NY5N`, Akteur
`t032-review10-independent`) den vollständigen Arbeitsstand ohne Übernahme der
Vorsitzungsbehauptungen: alle schnellen Gates eigenständig grün (fmt ohne
Fixes, lint 0 Befunde, Release-Build 0 Warnungen, Testsuite 245/245, security
PASS, rag-build, verify valid mit runsChecked=61), autoritative Läufe mit
selbst komponierten Eingabeskripten — Skript A `67c46966a1979aa8` und Skript B
`195a613d19503d41` (kernelCommandsTotal=10 über GroupMoveToZone) jeweils
builderidentisch inklusive byteidentischer Ketten je Paar; Fremdseed
`bbd157f0be518ab5` abweichend; Warm-up-Variante 200 liefert beweislich
denselben Endhash wie 240 (Messfensterunabhängigkeit des Zustands); Gate-
kennzahlen p99 0,721 ms / Allokation 0 Bytes je warmem Tick / max
reactionTicks 1 / Kettenkriterium evaluated=true. Negativmatrix (unbekanntes
Szenario, malformierter Header, Horizontabweichung, unbekannte Aktion,
übergrosses Sparse-Skript, `/dev/null`) je 37 ohne Report; displayloser
Interaktivlauf 19 ohne Report. Regressionen bench-sim/savecheck/
Soak-Kurzlauft (3000 Ticks, diagnostisch) grün; Riftward.Simulation
byteidentisch (Diff leer), GAME_DESIGN.md unberührt. Kein neuer Defektbefund;
keine Reparatur in dieser Sitzung nötig; die drei verschobenen unabhängigen
Reparaturen bleiben unverändert spätere Slices.
Fresh-Checkout-/Clean-Archive-Nachweis vor der reinen Doku-Erweiterung:
indexfreie Rekonstruktion zweifach konvergent (`hash-object`/`update-index`
auf privatem Temporärindex gegen Bottom-up-`mktree`) am Vorbindung-Baum
`a42051d4f1cab386561b4e42efeac96bd7f13b68` (38 Auftragspfade: 20 modifiziert,
18 neu, genau ein Task-Manifest); Doppel-Extraktion byteidentisch, volle
Gatefolge aus Archivbytes (fmt/lint/build 0 Warnungen/test 245/245/security
PASS/verify valid), autoritative Archivläufe liefern identische Endhashes A/B,
NO_DRIFT nach den Gates gegen die Zweitextraktion. Die Finalbaumbindung nach
allen Doku-Erweiterungen ist im Abnahmedokument sowie in
`artifacts/t032-rev10/final-candidate-tree.txt` gebunden.

Am 2026-08-27 prüfte eine weitere unabhängige Frisch-Review-Sitzung (Akteur
`t032-rev11-independent`) den Kandidaten erneut vollständig ohne Übernahme der
Vorgängerbehauptungen: alle schnellen Gates eigenständig grün (fmt/lint ohne
Befunde, Release-Build 0 Warnungen, Testsuite 245/245, security PASS,
rag-build, verify valid mit runsChecked=62), autoritative Läufe mit neu
komponierten Skriptpaaren statt der Vorgängerskripte — Skript A (Horizont 900)
Endhash `4223d37d9ceff0ad`, Skript B Endhash `61b0fbcd0524ea1f`, je Paar
builderidentisch; Fremdseed 999 liefert `3c976aa154326ec3` abweichend;
p99 ≤ 0,737 ms / Allokation 0 Bytes je warmem Tick / max reactionTicks 1 /
Kettenkriterium evaluated=true. Negativmatrix (unbekanntes Szenario,
malformierter Header, Horizontabweichung, übergrosses Skript am Rohmaterial,
`/dev/null`) je 37 ohne Report; displayloser Interaktivlauf 19 ohne Report.
Regressionen bench-sim/savecheck/Soak-Kurzlauft (3480 Ticks, diagnostisch)
grün; der Soak-Kurzlauft reproduzierte den Vorgängerendhash
`2763007a4dbc3c15`. Riftward.Simulation byteidentisch, GAME_DESIGN.md
unberührt, genau ein Task-Manifest verändert. Kein neuer Defektbefund; keine
Reparatur nötig; die drei verschobenen unabhängigen Reparaturen bleiben
unverändert spätere Slices. Fresh-Checkout-/Clean-Archive-Nachweis am
Vorbindung-Baum `c7e8dfd5f537e3ac29b8fb7edb956c77003e29f4` — exakt die
rev10-Finalfortschreibung, indexfrei zweifach konvergent rekonstruiert und
damit drift- und revisionsbeständig fortgeführt; Doppel-Extraktion
byteidentisch (350 Dateien), Gatefolge aus Archivbytes inklusive Testsuite und
autoritativer Archivläufe mit identischen Endhashes A/B, NO_DRIFT nach allen
Gates. Die `.git`-lose ASSET_LANE-Lücke blieb auch hier unverifizierbar und
unverändert dem verschobenen Slice vorbehalten. Finalbaumbindung nach dieser
Doku-Erweiterung: `artifacts/t032-rev11/final-candidate-tree.txt`.

Am 2026-08-27 prüfte eine erneute unabhängige Frisch-Review-Sitzung (Akteur
`t032-rev12-independent`) den Kandidaten vollständig ohne Übernahme der
Vorgängerbehauptungen: alle schnellen Gates eigenständig grün (fmt ohne Fixes,
lint valid, Release-Build 0 Warnungen, Testsuite 245/245, security PASS,
rag-build, verify valid mit runsChecked=63), autoritative Läufe mit neu
komponierten Skriptpaaren — Skript A Endhash `9ba54e081947c32e` und Skript B
Endhash `3b414d4f426e3e17`, je Paar builderidentisch inklusive byteidentischer
Ketten; nur die p99-Timingdiagnostik variiert erwartungsgemäß — Fremdseed 7
liefert `b823352a81208d17` abweichend. Allokation 0 Bytes je warmem Tick,
max reactionTicks 1, Kettenkriterium `evaluated=true`. Negativmatrix
(unbekanntes Szenario, `/dev/null`, Kopfhorizontabweichung, übergrosses
Sparse-Skript) je 37 ohne Report; displaylose Interaktivläufe ohne Display und
gegen toten Wayland-Socket je 19 ohne Report. Regressionen bench-sim
(p99 0,502 ms, pass) / savecheck (alle Prüfklassen) / Soak-Kurzlauft
(3240 Ticks, diagnostisch) grün; Riftward.Simulation byteidentisch,
GAME_DESIGN.md unberührt, genau ein Task-Manifest verändert. Kein neuer
Defektbefund; keine Reparatur nötig; die drei verschobenen unabhängigen
Reparaturen bleiben unverändert spätere Slices.
Fresh-Checkout-/Clean-Archive-Nachweis vor der Doku-Erweiterung: indexfreie
Identitätsrekonstruktion zweifach konvergent (bottom-up `hash-object`/`mktree`
gegen privaten Temporärindex `read-tree HEAD` + `update-index --add --stdin`;
echter Index nie angefasst) am Vorbindung-Baum
`d0fae7a80bfbff72a4dd38653da5293506003478` (350 Auftragspfade, genau ein
Task-Manifest); Doppel-Extraktion byteidentisch (350 Dateien), volle Gatefolge
aus Archivbytes (bootstrap/fmt/lint/build 0 Warnungen/test 245/245/security
PASS), autoritative Archivläufe mit identischen Endhashes A/B, NO_DRIFT nach
allen Gates gegen die Zweitextraktion (350/350). Die `.git`-lose
ASSET_LANE-Lücke blieb unverändert dem verschobenen Slice vorbehalten.
Finalbaumbindung nach dieser Doku-Erweiterung:
`artifacts/t032-rev12-independent/final-candidate-tree.txt`.

Am 2026-08-27 prüfte eine weitere unabhängige Frisch-Review-Sitzung (Akteur
`t032-rev13-independent`) den Kandidaten vollständig ohne Übernahme der
Vorgängerbehauptungen. Empfangene Identität: indexfreie Rekonstruktion
zweifach konvergent — privater Temporärindex (`read-tree HEAD` +
`update-index --index-info` am exakten Pfadsatz) gegen Bottom-up-
`hash-object`/`mktree`; beide liefern exakt den gebundenen Vorbindung-Baum
`8f108155d043fdf12140ed92b9b2fc7649a1c3b1` und reproduzieren damit die
rev12-Finalbindung (beweislich kein Drift seit rev12). Baumdifferenz zu HEAD
exakt 38 Auftragspfade (20 M, 18 A), genau ein Task-Manifest, 350 getrackte
Dateien; Riftward.Simulation byteidentisch, GAME_DESIGN.md unberührt. Alle
schnellen Gates eigenständig grün (fmt `--check` 0, lint valid 0 Befunde,
Release-Build 0 Warnungen, Testsuite 245/245, security PASS, rag-build,
verify valid runsChecked=63). Autoritative Läufe mit neu komponierten
Skripten: Skript A (7 Intents, Horizont 900) Paarendhash
`48f3f231f0880cb9`, Skript B (11 Intents inklusive Tick-Umsortierung und
Ablehnungsprobe) Paarendhash `e8a02b76679b8b38`, Ketten je Paar byteidentisch;
Fremdseed 42 auf Skript A ändert Start- und Endhash nachweislich
(`23507fd162f3fa39`). Allokation 0 Bytes je warmem Tick, max reactionTicks 1,
Kettenkriterium `evaluated=true`, `gate.pass=true`; die vertragliche
Move-Ablehnung erscheint als Zähler statt stiller Wirkung (Skript A: 3,
Skript B: 1; kernelCommandsTotal 25 auf B über GroupMoveToZone).
Negativmatrix (unbekanntes Szenario, malformierter Header,
Kopfhorizontabweichung 901/900, `/dev/null`, übergrosses Sparse-Skript)
je 37 ohne Report; displayloser Interaktivlauf bei vorhandenem
Artefaktmanifest kontrollierter Code-19-Abbruch ohne Report.
Regressionen bench-sim → 0 (`gate.pass=true`), savecheck → 0
(`gate.pass=true`), Soak-Kurzlauft 3000 Ticks diagnostisch (`evidenceUnit=false`)
→ 0. Kein neuer Defektbefund im vollständigen Quellreview; keine Reparatur
nötig; die drei verschobenen unabhängigen Reparaturen bleiben unverändert
spätere Slices. Fresh-Checkout-/Clean-Archive-Nachweis vor der reinen
Doku-Erweiterung dieser Sitzung: Doppel-Extraktion des Baums byteidentisch;
volle Gatefolge aus Archivbytes (Release-Build 0 Warnungen, fmt `--check`
0, lint valid, Testsuite 245/245, security PASS) plus zwei autoritative
Kommandoschleifenläufe aus Archivbytes mit identischen Endhashes A/B wie die
Arbeitsbaumlaeufe; NO_DRIFT nach allen Gates gegen die Zweitextraktion
(350/350); `assets-check` und das harness-zustandsabhängige `verify`
bleiben gemäß der dokumentierten verschobenen ASSET_LANE-Reparatur
Entwicklerbaum-Gates und sind dort grün. Endgültige Finalbaumbindung nach
dieser reine-Doku-Erweiterung zweifach konvergent rekonstruiert und in
`artifacts/t032-r13-independent/final-candidate-tree.txt` sowie der
Zusammenfassung des Harness-Runs `01M10R1QPFDE5ENFJNZSPYSV0Q` gebunden;
die Baumdifferenz gegenüber dem geprüften Vorbindung-Baum umfasst
ausschließlich die drei Dokumentationspfade dieser Sitzung.

Die verbleibenden DRAFT-Einheiten hängen an Referenzhardware- und
Runner-Benennung (Q-OPS-001/Q-OPS-002 für `T-011`) sowie an echten kreativen
Produktentscheidungen (`T-030` samt seiner geplanten Folgeslices, dahinter
`T-040`/`T-041`) beziehungsweise an getrennten Generator- und Storage-/Backup-
Freigaben (`T-050`/`T-051`).

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

Am 2026-08-27 prüfte eine weitere unabhängige Frisch-Review-Sitzung (Akteur
`t032-rev14-independent`, Harness-Run `01M10SA03FXEZ1S1DXHP4S20JK`) den
Kandidaten vollständig ohne Übernahme der Vorgängerbehauptungen. Empfangene
Identität: indexfreie Rekonstruktion zweifach konvergent (kanonische
SHA-1-Baumkonstruktion vs privater Temporärindex) am Vorbindung-Baum
`bc051ddc…`, deckungsgleich mit der rev13-Finalbindung — beweislich kein
Drift; 38 Auftragspfade (20 M/18 A), genau ein Task-Manifest;
Riftward.Simulation byteidentisch, docs/GAME_DESIGN.md unberührt. Alle
schnellen Gates eigenständig grün (lint 0 Befunde, Release-Build
0 Warnungen, Testsuite 245/245, security PASS, rag-build, verify valid mit
runsChecked=65). Autoritative Läufe mit selbst komponierten Skripten:
Skript A Paarendhash `ff6cd961cd46dd5c` (kernelCommandsTotal=5,
moveWithoutSelectionRejects=2), Skript B (nichtmonotone Dateireihenfolge als
Live-Kanonisierungsnachweis) Paarendhash `c03d1047616ec67b`; Ketten je Paar
byteidentisch; Fremdseed 424242 ändert Start- und Endhash nachweislich; p99
≤ 0,722 ms, Allokation 0 Bytes je warmem Tick, max reactionTicks 1,
Kettenkriterium evaluated=true, gate.pass=true. Negativmatrix (unbekanntes
Szenario, malformierter Header, Kopfhorizontabweichung, unbekannte Aktion,
übergrosses Sparse-Skript am Rohmaterial, /dev/null) je 37 ohne Report;
displaylose Interaktivläufe ohne Display und gegen toten Wayland-Socket je
19 ohne Report bei vorhandenem Artefaktmanifest. Regressionen bench-sim/
savecheck grün mit gate.pass=true, Soak-Kurzlauft 3060 Ticks diagnostisch
(evidenceUnit=false) → 0. Fresh-Checkout-/Clean-Archive-Nachweis am
Vorbindung-Baum: Doppel-Extraktion byteidentisch (350 Dateien), Gatefolge
aus Archivbytes inklusive Testsuite und autoritativer Archivläufe mit
identischen Endhashes A/B wie die Arbeitsbaumläufe, NO_DRIFT nach allen
Gates gegen die Zweitextraktion (350/350); assets-check und das harness-
zustandsabhängige verify bleiben gemäß der dokumentierten verschobenen
ASSET_LANE-Reparatur Entwicklerbaum-Gates und sind dort grün. Kein neuer
Defektbefund im vollständigen Quellreview; keine Reparatur nötig; die drei
verschobenen unabhängigen Reparaturen bleiben unverändert spätere Slices.
Endgültige Finalbaumbindung nach dieser reinen Doku-Erweiterung zweifach
konvergent rekonstruiert in `artifacts/t032-rev14/final-candidate-tree.txt`.

Am 2026-08-27 prüfte eine weitere unabhängige Frisch-Review-Sitzung (Akteur
`t032-rev15-independent`) den gesamten Arbeitsstand vollständig ohne Übernahme
der Vorgängerbehauptungen. Kandidatenidentität zweifach konvergent: private
Index-Rekonstruktion (`read-tree HEAD` + `update-index --cacheinfo` aus
Arbeitsbytes) und reine `hash-object`/`mktree`-Plumbing-Rekonstruktion aus
Archivbytes ergeben exakt denselben Baum `c55aa581…` (Parent `068974c…`, 350
getrackte Dateien, 38 Auftragspfade, genau ein Task-Manifest). Alle schnellen
Gates eigenständig grün (Release-Build 0 Warnungen, Testsuite 245/245,
lint 0 Befunde, security PASS, rag-build OK, verify valid mit
runsChecked=65). Autoritative Läufe mit selbst komponierten Skriptpaaren
(Horizont 900): A Endhash `23507fd162f3fa39` (Punktwahl vor Boxauswahl im
selben Tick als Kanonisierungsprobe), B Endhash `8f46c4b12141bbed`
(kernelCommandsTotal=33); je Paar builderidentisch **und** die vollständigen
Kettenstichproben prozessgrenzenübergreifend byteidentisch (Abgleich des
extrahierten `stateHashChain`-JSONs) — damit ist auch die Intervallgleichheit
von AC-T032-03 über Prozessgrenzen belegt. Fremdseed 999 →
`3c976aa154326ec3` abweichend; p99 ≤ 0,918 ms, Allokation 0 Bytes,
max reactionTicks 1, Kettenkriterium evaluated=true, gate.pass=true.
Negativmatrix (unbekanntes Szenario, Kopfhorizontabweichung 900/1200,
/dev/null, Zonengrenzwert 6, duplizierter Intent, übergrosses Sparse-Skript)
je 37 ohne Report; displayloser Interaktivlauf 19 ohne Report.
Regressionen bench-sim (gate.pass=true), savecheck (0), Soak-Kurzlauft
3000 Ticks mit Vertragsseed grün; Kontrollprobe Soak mit Fremdseed 42 schlägt
korrekt fail-closed mit Exit 30 `state-hash-chain-mismatch:golden-fixture` ab.
Fresh-Checkout-/Clean-Archive-Vertrag vollständig ausgeführt: Doppel-Extraktion
byteidentisch, Gatefolge ausschließlich aus Archivbytes — bootstrap/lint/
assets-check/security/verify (valid, runsChecked=0 erwartungsgemäß ohne
portierte Runtime-Evidenz) plus Testsuite; der Erstlauf der exakten
Null-Allokations-Einzelassertion fiel transient mit 3,84 Bytes aus
(Kaltprozesstransient, bewusst keine Schwächung vorgenommen) und war danach
dreifach reproduzierbar grün 245/245; NO_DRIFT nach allen Gates gegen die
Zweitextraktion (350/350). Der historische ASSET_GIT_CHECK_FAILED-Befund war
auf heutigen Bytes unter zwei Archivformen nicht reproduzierbar; die
verschobene Reparatur bleibt offen (exakte Vorgängerform mit vollen Staging
ist wegen des Stagingverbots nicht nachstellbar). Zwei kleine In-Scope-Doku­
reparaturen im Primärslice ohne Berührung eines zweiten Task-Manifests:
Präzedenzsatz für Exitcodes 36/38/35 im Kommandovertrag §8 gemäß
`ResolveInteractiveExitCode` sowie Flagparität der AUTOMATION.md-Befehlszeile
(`--warmup-ticks/--horizon-ticks/--lock`). Neue verschobene unabhängige
Reparatur als späterer Slice: Härtung von
`allocationStrictnessRegression` gegen Kaltprozess-Transients
(protokollgerechte Wiederholung analog T-021-CLI-Präzedenz oder
prozessisolierte Messung); die Exakt-Null-Gateklasse des Engine-Reports bleibt
unberührt. Riftward.Simulation byteidentisch, GAME_DESIGN.md unberührt;
Finalbaumbindung in `artifacts/t032-rev15/final-candidate-tree.txt`.

Am 2026-08-27 prüfte eine erneute unabhängige Frisch-Review-/Vollendungs-
Sitzung (Akteur `t032-rev17-independent`) den vollständigen Arbeitsstand ohne
Übernahme der Vorgängerbehauptungen: indexfreie Doppelrekonstruktion zweifach
konvergent — privater Temporärindex gegen Bottom-up-Plumbing (`hash-object`/
`mktree`) — am gebundenen Kandidatenbaum `797b774bb19c8a734d2d21cd34b50df69e3dae73`
(Parent `068974c9…`; exakt 38 Auftragspfade: 20 M/18 A, genau ein Task-Manifest,
350 getrackte Dateien); Riftward.Simulation byteidentisch (kein Diff-Eintrag),
GAME_DESIGN.md unberührt. Alle schnellen Gates eigenständig grün (fmt ohne
Fixes, lint 0 Befunde, Release-Build 0 Warnungen, Testsuite 245/245,
security PASS, rag-build OK, verify valid mit runsChecked=65). Autoritative
Läufe mit neu komponierten Skripten (Horizont 900): Skript A Paarendhash
`d8a4650b768731b3` (kernelCommandsTotal=10), Skript B (nichtkanonisch
geschriebener Gleichtick clear+box+move als Kanonisierungsprobe) Paarendhash
`4d35e6733f1278ba` (kernelCommandsTotal=5); je Paar Endhash **und**
vollständige Kettenstichproben byteidentisch; Fremdseed 424242 ändert Start-
und Endhash nachweislich; p99 ≤ 0,852 ms, Allokation 0 Bytes je warmem Tick,
max reactionTicks 1, Kettenkriterium evaluated=true, gate.pass=true.
Negativmatrix (unbekanntes Szenario, malformierter Header,
Kopfhorizontabweichung, unbekannte Aktion, Zone 6, duplizierter Intent,
/dev/null, übergrosses Sparse-Skript am Rohmaterial) je 37 ohne Report;
displayloser Interaktivlauf 19 ohne Report. Regressionen bench-sim
(gate.pass=true)/savecheck/Soak-Kurzlauft (3000 Ticks, diagnostisch) grün.
Kein neuer Defektbefund im vollständigen Quellreview; keine Reparatur nötig;
die vier verschobenen unabhängigen Reparaturen bleiben unverändert spätere
Slices. Fresh-Checkout-/Clean-Archive-Nachweis vollständig: Doppel-Extraktion
byteidentisch (350 Dateien), Gatefolge aus Archivbytes (bootstrap/build 0
Warnungen/lint/test 245/245/security/assets-check/verify runsChecked=0) plus
zwei autoritative Archivläufe mit zeichengleichen Endhashes und vollständig
identischen Ketten gegenüber den Arbeitsbaumläufen, NO_DRIFT nach allen Gates
auf allen 350 getrackten Pfaden; die .git-lose ASSET_LANE-Lücke blieb auch hier
nicht reproduzierbar und unverändert dem verschobenen Slice vorbehalten.
Endgültige Finalbaumbindung nach dieser Doku-Erweiterung in
`artifacts/t032-r17/final-candidate-tree.txt`.
