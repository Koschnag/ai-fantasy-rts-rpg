# Qualität und Abnahme

Qualität bedeutet für Project Riftward nicht nur einen erfolgreichen Build. Jede Lieferung muss fachlich zusammenhängend, reproduzierbar, auf Zielhardware messbar, plattformfähig, eigenständig und anhand eines Harness-Runs nachvollziehbar sein.

## Definition of Ready

Eine Umsetzungseinheit darf nur den Backlogstatus `READY` erhalten, wenn:

- [ ] Nutzen, beobachtbares Ergebnis, Scope und Nicht-Scope benannt sind.
- [ ] betroffene Ziel-, Anforderungs-, Daten-, User-Flow- und gegebenenfalls Asset-IDs verknüpft sind.
- [ ] Abnahmekriterien mit Ausgangslage, Handlung, erwartetem Ergebnis und Grenzwert testbar formuliert sind.
- [ ] relevante Fehler-, Abbruch-, Wiederaufnahme- und Korruptionsfälle beschrieben sind.
- [ ] die maßgeblichen Quellen und ihre Priorität widerspruchsfrei sind.
- [ ] keine Produktentscheidung aus `OFFENE_FRAGEN.md` die Implementierung blockiert. Ein Spike darf ein offenes Ergebnis haben, wenn Messaufbau und Entscheidungskriterien vollständig spezifiziert sind.
- [ ] Datenverträge, Schema-/Migrationsfolgen und Vertrauensgrenzen geklärt sind.
- [ ] Hardware-, Frame-, Speicher-, Szenen- und Assetbudgets zugeordnet sind, falls ein Hotpath oder Shipping-Asset betroffen ist.
- [ ] neue Abhängigkeiten mit Upstream, exakter Version/Commit, SPDX-Lizenz, Zweck, Transitivität, Plattform-/AOT-Folgen und Austauschstrategie vorbereitet sind.
- [ ] erforderliche Plattformen, Testebenen, Fixtures, Seeds, Benchmarks und Evidenzartefakte feststehen.
- [ ] der Task unter `.ai/tasks/` maschinenlesbar angelegt und gegen sein Schema validiert ist.

Eine große Story darf nicht allein deshalb `READY` werden, weil ihr Ziel verständlich ist. Sie wird so geschnitten, dass ein Agent sie in einem nachvollziehbaren Run implementieren und objektiv abschließen kann.

## Definition of Done

Eine Umsetzungseinheit ist erst `DONE`, wenn:

- [ ] alle Abnahmekriterien erfüllt und im Abschlussbericht einzeln mit Evidenz verknüpft sind.
- [ ] Produktionscode, Tests, Fixtures, Schemas und Dokumentation gemeinsam aktualisiert wurden.
- [ ] Locked Restore und Build erfolgreich sind; neue Warnungen, AOT-/Trimming-Warnungen und native ABI-Warnungen sind null oder durch eine bestätigte Entscheidung erklärt.
- [ ] deterministische Unit-, Integrations-, Fehler- und relevante Randfalltests erfolgreich sind; kein Test wurde abgeschwächt, gelöscht oder übersprungen, um Grün zu erreichen.
- [ ] alle betroffenen nativen Zielplattformen mindestens einen nativen Build-/Smoke-Nachweis besitzen. Ein Linux-Erfolg ersetzt keinen Windows- oder macOS-Nachweis.
- [ ] Performance- oder Speicherbudgets durch reproduzierbare Messung bestätigt sind, wenn Hotpaths, Rendering, Simulation, Streaming oder Contentmenge betroffen sind.
- [ ] neue Shipping-Assets vollständige Provenienz, technische Gates, visuelles Review, Lizenzgrundlage und Originalitätsprüfung besitzen.
- [ ] Save-/Contentänderungen passende Versions-, Roundtrip-, Korruptions- und gegebenenfalls Migrationsfixtures besitzen.
- [ ] Secrets, unbekannte Binärquellen, ungeprüfte Fremdinhalte und notwendige Runtime-Netzwerkzugriffe ausgeschlossen sind.
- [ ] ein abgeschlossener, integritätsgültiger Harness-Run Änderungen, verwendete Quellen, Befehle, Ergebnisse, Artefakthashes und bekannte Restpunkte dokumentiert.
- [ ] keine unbekannten Fehler der Schwere `BLOCKER` oder `KRITISCH` und keine unentschiedene budget-/IP-kritische Abweichung verbleiben.

`DONE` bedeutet nicht automatisch Meilensteinfreigabe. Der integrierte Vertical Slice benötigt zusätzlich alle Freigabekriterien weiter unten.

## Einheitliche Qualitätsgates

| Gate | Inhalt | Öffentlicher Befehl | Aktueller Stand | Erfolgsbedingung |
|---|---|---|---|---|
| G-SPEC Spezifikation | Auftrag, Anforderungen und Abnahmekriterien | Task-Schema plus Review | für T-001/T-002/T-004 dokumentiert; Schemaautomatisierung wächst mit T-004 | eindeutiger Scope, Links, Kriterien und Entscheidungshoheit |
| G-FORMAT Format | deterministische Quellformatierung | `./scripts/rift.sh lint` | F#-Formatprüfung mit gepinntem Fantomas implementiert | Exit 0; keine ungeprüft formatierte F#-Quelle |
| G-STATIC Statisch | Locked Restore, Compiler, Warnungen, Analyzer und Schemas | `./scripts/rift.sh build` plus aufgabenspezifische Validatoren | Compilerprüfung für bestehende Solution implementiert; Runtime-/Native-Analyzer wachsen pro Task | Exit 0, keine ungeklärten Warnungen |
| G-TEST Tests | deterministische automatisierte Tests | `./scripts/rift.sh test` | Harness-Tests implementiert; Runtime-Suite wächst pro Task | Exit 0, keine ungeklärten Skips/Flakes |
| G-HARNESS Harness | Eventhashkette, Runabschluss, RAG-/Konfigintegrität | `./scripts/rift.sh verify` | implementiert für aktuellen Harnessumfang | Exit 0; Kette, Quellen und Index gültig |
| G-DATA Daten | JSON-/Content-Schemas, Referenzen, Lokalisierung, Save-Fixtures | künftig `lint`/spezifische Compiler | JSON-Syntax und Harness-Schemas teilweise implementiert; Runtime-Content noch NICHT VERFÜGBAR | alle betroffenen Dateien validiert; keine fehlende Pflichtreferenz |
| G-PROVENANCE Assets/Modelle | Provenienz, Lizenz, Ähnlichkeit, Technik und Cook | `./scripts/rift.sh assets-check`; für Shipping zusätzlich `--require-local --require-approved` | Manifest-/Receipt-/Modell-/Clean-Room- und Lifecycle-Prüfung implementiert; Cook- und Gesamtspielreview wachsen in späteren Tasks | jedes referenzierte Shipping-Asset und Modell freigegeben; Quarantäne zählt niemals als Shipping-Freigabe |
| G-PERF Performance | Pflichtszenen, Telemetrie, Baselinevergleich und integrierter `BENCH-REPRESENTATIVE` | `bench` | NICHT VERFÜGBAR | alle relevanten Profilgrenzen auf der gebundenen realen Referenzklasse eingehalten |
| G-VISUAL Bild/Atmosphäre | Golden-Szenen, Lesbarkeit, Originalität und Atmosphärenrubrik | Reviewprotokoll; später Evaluator | Rubrik spezifiziert, ausführbarer Evaluator noch NICHT VERFÜGBAR | Rubrik und harte Originalitätsbedingungen erfüllt |
| G-SECURITY Sicherheit/Lizenzen | Secrets, Abhängigkeiten, native Lizenzen und untrusted inputs | `./scripts/rift.sh security` | lokaler Baseline-Gate implementiert; native Lizenzen, Threat Model und Releaseprüfung noch NICHT VERFÜGBAR | Baseline Exit 0 und alle aufgabenspezifisch benötigten Nachweise vorhanden; Baseline allein ist keine Releasefreigabe |
| G-PLATFORM Plattform | native Builds, ABI- und Smoke-Prüfungen je betroffenem RID | native CI-/Smoke-Aufträge | NICHT VERFÜGBAR | jeder betroffene Ziel-RID nativ grün |
| G-PACKAGE Packaging | RID-spezifischer reproduzierbarer Releasebuild | `package` | NICHT VERFÜGBAR | natives Artefakt, Manifest, Hash, Lizenztexte und Smoke grün |

Ein als `NICHT VERFÜGBAR` markiertes Gate ist keine bestandene Prüfung. Ein Task, dessen Abnahmekriterien dieses Gate benötigen, kann erst `DONE` werden, nachdem das Gate oder ein gleichwertiger, dokumentierter Nachweis implementiert ist.

## Teststrategie

| Ebene | Zweck und typische Nachweise | Ausführung | Verantwortlich |
|---|---|---|---|
| Reine Unit-Tests | Regeln, Parser, Validatoren, Hashing, Questtransitionen, Kampf-/Wirtschaftsmathematik | bei jeder Änderung, ohne Netzwerk/Uhrzufall | implementierender Agent |
| Generative-/Randfalltests | Wertebereiche, Graphen, Befehlsreihenfolgen, beschädigte Daten, kanonische Sortierung | bei Daten-/Simulationsänderung mit gespeichertem Fehlerseed | implementierender Agent |
| Determinismus / Replay | gleicher Seed + Startzustand + Befehle → spezifizierter Zustand/Hash | pro Simulationsänderung; Cross-Plattform in CI | Simulation/CI |
| Native ABI / Integration | SDL3-/bgfx-Wrapper, Handles, Shader, Audio, Dateisystem und Fehlerübersetzung | pro betroffenem RID und nativer Versionsänderung | Plattform-Agent |
| Content-/Schema-Tests | IDs, Referenzen, Graphen, Budgets, Lokalisierung, Packagehash und Cook-Reproduzierbarkeit | bei jeder Content-/Tooländerung | Content-Agent |
| Save-/Migrationstests | Snapshot-Roundtrip, atomarer Abbruch, Korruption, unbekannte Version und idempotente Migration | bei jeder save-relevanten Änderung | Runtime-Agent |
| Gameplay-Smoke / E2E | zentrale Nutzerwege mit festen Seeds und Zustandsprüfpunkten | pro Merge; vollständiger Lauf vor Meilenstein | Gameplay-Agent |
| Plattform-Smoke | Prozessstart, Fenster, Eingabe, Renderbackend, Audio, Laden, Beenden | native Windows-/Linux-/macOS-Matrix | Release-/Plattform-Agent |
| Performance | `BENCH-EMPTY/ARMY/BATTLE/BASE/PATH/LOAD` plus `BENCH-REPRESENTATIVE` mit Rohmetriken und Baseline | bei Hotpath/Contentbudget; vollständig vor Meilenstein | Performance-Agent |
| Soak / Zuverlässigkeit | 8 Stunden Replay ohne Absturz, Hänger oder fortschreitenden Speicherverlust | vor Vertical-Slice-RC und nach Kernänderung | Performance/Runtime |
| Visuell / Atmosphäre | Lesbarkeit, Licht/Farbe, Audio, Pacing, Modusübergang, Originalität | finalitätsnahe Builds, blind wo spezifiziert | Art-/Creative-Review |
| Zugänglichkeit / Lokalisierung | freie Bindings, UI-Skalierung, Untertitel, keine reine Farbcodierung, DE/EN-Schlüssel | automatisiert plus manuell vor RC | UX/Localization |
| Harness / Audit | Run-Lifecycle, Redaction, Hashmanipulation, deterministische RAG-Treffer und Zitate | bei jeder Harness-/Policyänderung | Harness-Agent + Review |

### Regeln für automatisierte Tests

- Tests benötigen einen festen Seed oder speichern einen gefundenen Fehlerseed als Fixture.
- Kein Test darf Netzwerk, lokale Zeitzone, zufällige Dateisystemreihenfolge oder eine bestimmte CPU-Kernzahl voraussetzen.
- Zeit wird in testbarer Spiel-/Tickzeit injiziert; lange Echtzeitwartezeiten sind kein Unit-Test.
- Golden-Daten enthalten Schema- und Contentversion. Eine beabsichtigte Aktualisierung nennt den fachlichen Grund im Run.
- Ein sporadisch roter Test wird als Fehler behandelt. Quarantäne ist nur zeitlich begrenzt, mit verantwortlicher Person, Issue/Task und Enddatum zulässig; er zählt bis zur Behebung nicht als Gatebeleg.
- Performancewerte stammen aus Release-nahen Builds nach definierter Aufwärmphase. Debugwerte dürfen diagnostizieren, aber keine Freigabe begründen.
- Visuelle Goldens dürfen kleine backendbedingte Pixelabweichungen nur über vorab definierte Metrik/Toleranz behandeln; ein pauschal hoher Schwellwert ist unzulässig.

## Messbare Qualitätsverträge

### Korrektheit und Zuverlässigkeit

- Der deterministische Gameplay-Smoke erreicht definierte Zustandsprüfpunkte für Erkundung, Questwahl, Standort, Basis, Armee, Boss und Abschluss.
- Ein Save-Roundtrip erhält alle sim-relevanten Zustände; ein beschädigter oder inkompatibler Save ersetzt niemals den aktiven oder letzten gültigen Stand.
- Jeder bestätigte Spielerbefehl wird ausgeführt oder mit einer fachlichen Ursache abgelehnt. Endlose Pending-Orders und missionsblockierende Softlocks sind `BLOCKER`.
- Der 8-Stunden-Soak darf nicht abstürzen, hängen oder fortschreitend Speicher verlieren. Der genaue numerische Leak-Schwellwert bleibt bis zum Baseline-Spike `OFFEN`.

### Performance

- `HW-PC-MIN` und `HW-MAC-MIN`: reguläre 1080p-/äquivalente Low-Ausgabe, p99 höchstens 33,3 ms und keine anhaltenden Einbrüche unter 30 FPS.
- `HW-PC-HIGH`: 1080p High mit stabilen 60 FPS, p99 höchstens 16,7 ms und keinen anhaltenden Einbrüchen unter 60 FPS.
- Simulation: Ziel 8 ms bei 20 Hz, harte Grenze 16 ms; Numerik und Cross-Plattform-Hashvertrag werden durch Q-TEC-004 bestätigt.
- Prozess-/VRAM-, Lade-, Input- und Szenengrenzen gelten vollständig gemäß `PERFORMANCE_BUDGET.md`; ein Durchschnittswert verdeckt keine p99- oder Peakverletzung.
- Niedrigere Grafikstufen dürfen Effekte, Schatten, LOD und Partikel reduzieren, aber keine Einheiten, Simulationsregeln oder taktisch notwendige Information.
- Ein Budget ändert sich nur durch dokumentierte Entscheidung nach reproduzierbarem Profil, nicht durch Anpassung eines Tests an die aktuelle Implementierung.
- Architektur, Datenlayout und Budgetzuordnung allein belegen keine Optimierung. Die zentrale Effizienzhypothese bleibt offen, bis der in `PERFORMANCE_BUDGET.md` definierte integrierte Repräsentativitätsnachweis auf `HW-PC-MIN` und `HW-MAC-MIN` besteht.

### Atmosphäre und Eigenständigkeit

- Die Rubrik in `ATMOSPHAERE.md` erreicht mindestens 80/100 Punkte; kein Bereich liegt unter 7/10.
- In Blindtests wählen mindestens 70 % drei der vier Zieladjektive und erinnern mindestens 70 % drei freigegebene Riftward-Weltprinzipien.
- Held, Hauptziel und größte Gefahr werden bei 1080p binnen zwei Sekunden von mindestens 90 % erkannt; Audio-/Pacing-/Modusübergangswerte folgen der Bible.
- Das Originalitäts-Gate ist binär: keine im dokumentierten Review festgestellten unautorisierten Übernahmen oder Eins-zu-eins-Zuordnungen, vollständige Asset-Provenienz und ein dokumentiertes Ähnlichkeitsreview. Ein bestandener automatischer Policy-Scan ist nur Evidenz und keine Aussage über Rechtssicherheit oder weltweite Einzigartigkeit.
- Spontane Zuordnung eines zentralen Assets zu einer konkreten fremden Figur, einem Ort, UI-Element oder Track führt zur Quarantäne und Neugestaltung; ein hoher Atmosphärenwert hebt das nicht auf.

### Zugänglichkeit und Bedienung

- Jede Kernaktion ist frei belegbar; Konflikte werden vor Übernahme sichtbar und Defaults lassen sich wiederherstellen.
- UI und Untertitel sind bei 1080p skalierbar und werden auf dem kleinsten unterstützten Viewport ohne abgeschnittene Pflichtaktion geprüft.
- Dialoge sind pausier-/überspringbar und nachlesbar. Spielrelevante Information hängt weder nur von Farbe noch nur von Ton ab.
- Eine fehlende DE-/EN-Lokalisierung oder abweichende Platzhaltermenge blockiert den Shipping-Cook.

## Performance- und Plattform-Matrix

| Matrixeintrag | Pflichtumfang vor VS-Freigabe |
|---|---|
| Windows x64 / D3D11 | nativer Releasebuild, kompletter Gameplay-Smoke, Shader-/ABI-Smoke und mindestens PC-Min oder PC-High-Hardwarelauf |
| Linux x64 / OpenGL 3.3 | nativer Releasebuild, kompletter Gameplay-Smoke, Shader-/ABI-Smoke und ausgewählte Pflichtbenchmarks |
| macOS arm64 / Metal, M1 8 GB | nativer Releasebuild, kompletter Gameplay-Smoke, Shader-/ABI-Smoke, Memory-Pressure-Prüfung und vollständiger Minimum-Profillauf |
| GTX-660-Klasse / 8 GB | vollständige Pflichtbenchmarks auf Low 1080p, einschließlich VRAM- und Ladepeak |
| RX-580-Klasse | vollständige Pflichtbenchmarks auf High 1080p/60 |

Welche konkrete Maschine, OS- und Treiberversion diese Matrix erfüllt, ist in Q-OPS-001/Q-TEC-002 noch `OFFEN`. Bis dahin dürfen Messungen als Baseline, aber nicht als endgültiger Supportnachweis gelten.

## Evidenzvertrag

Ein Gatebeleg enthält mindestens:

- Task-, Run-, Commit-/Build- und Contentkennung,
- exakten Befehl beziehungsweise klar versioniertes manuelles Prüfskript,
- Betriebssystem, RID und bei Leistungstests Hardware/Treiber/Qualitätsprofil,
- Start-/Endzeit, Seed, Warm-up und Wiederholungszahl, soweit relevant,
- Ergebnis je Kriteriums-ID sowie Exitcode,
- Pfad und SHA-256 großer Reports, Bilder, Traces oder Pakete,
- Baseline und Delta bei Regressionstests,
- kurze fachliche Begründung einer bewussten Ausnahme; keine versteckten Gedankengänge.

Ein Screenshot ohne Build-/Szenenbezug, ein Exitcode ohne Befehl/Umgebung oder die Aussage „manuell getestet“ ohne Prüfschritte genügt nicht.

## Fehlerklassen und Ausnahmen

| Klasse | Bedeutung | Freigaberegel |
|---|---|---|
| `BLOCKER` | Datenverlust, nicht startbar, reproduzierbarer Absturz/Softlock im Muss-Flow, Herkunft/IP ungeklärt, harte Performancegrenze verfehlt | keine Meilensteinfreigabe |
| `KRITISCH` | Muss-Funktion falsch, schwere Plattform-/Save-/Zugänglichkeitsregression, Secret oder nicht lizenzierbare Shipping-Abhängigkeit | keine Meilensteinfreigabe |
| `HOCH` | deutliche Beeinträchtigung mit Workaround, wiederkehrender visueller/akustischer Bruch, relevante Budgetannäherung | nur mit explizitem, befristetem Abweichungsentscheid; nicht für VS-Abnahmeziel |
| `NORMAL` | begrenzter Fehler ohne Verlust des Kernwegs | priorisiert dokumentieren; Freigabeentscheidung nennt Restmenge |
| `NIEDRIG` | kosmetisch oder kleine Komfortabweichung | Backlog mit reproduzierbarem Nachweis |

Eine Ausnahme nennt Eigentümer, Grund, betroffene Profile/Plattformen, Risiko, Ablaufdatum und Korrektur-Task. Originalitäts-, Secret-, Save-Datenverlust- und harte 30-FPS-Grenzen sind nicht durch eine informelle Ausnahme aufhebbar.

## Freigabekriterien für VS-001

- [ ] Der 20–30-minütige Weg aus UF-001 ist ohne Debugeingriff durchspielbar und enthält 1 Hauptfigur, 2 Begleiter, Questwahl, Heldengefecht, 2 Ressourcen, 5 Gebäude, 4 eigene Einheitentypen, 4 normale Gegnerarchetypen, 1 Elite und 1 Boss.
- [ ] Mindestens eine Wirkung persönlich → strategisch und eine Wirkung strategisch → persönlich ist sichtbar, gespeichert und im Replay prüfbar.
- [ ] Fog of War, Minimap, Auswahlgruppen, Formationsbewegung, kontextuelle Befehle, Speichern/Laden sowie Grafik-/Audio-/Eingabeeinstellungen erfüllen ihre User Flows.
- [ ] Alle drei Plattformartefakte bestehen native Smokes; sämtliche Pflicht-Hardwareprofile bestehen ihre vereinbarten Benchmarks.
- [ ] Soak-, Save-, Korruptions-, Content-, Lokalisierungs-, Zugänglichkeits- und Gameplay-Smokes sind grün.
- [ ] Jedes Shipping-Asset ist gecookt, budgetgerecht, hashverifiziert und hinsichtlich Provenienz, Lizenz und Ähnlichkeit freigegeben.
- [ ] Atmosphäre erreicht Rubrik und Originalitäts-Gate; eine ruhige Nachhallphase und die Zieladjektive sind in Abnahmetests nachweisbar.
- [ ] Build, Tests, Harnessintegrität und alle bis dahin benötigten Produktionsgates sind implementiert und grün; `NICHT VERFÜGBAR` zählt nicht.
- [ ] Keine `BLOCKER`/`KRITISCH`-Fehler und keine blockierende offene Entscheidung verbleiben.
- [ ] Der Meilensteinbericht nennt reproduzierbare Artefakte, bekannte normale/niedrige Fehler sowie eine ausdrückliche Entscheidung, ob und in welchem Umfang Vollproduktion beginnen darf.
