# Offene Fragen und Entscheidungsbedarf

Dieses Register enthält nur echte, noch nicht bestätigte Produkt-, Content- oder Technikentscheidungen. Ein KI-Agent darf daraus keine Antwort ableiten. Die Arbeitsannahme erlaubt höchstens einen begrenzten Spike; sie wird dadurch nicht zur Entscheidung.

## Bereits geklärte Leitplanken

Diese Punkte nicht erneut als offen behandeln:

| Thema | Festlegung | Quelle |
|---|---|---|
| Werkidentität | eigenständiger Fantasy-RTS/RPG-Hybrid; keine Rekonstruktion oder Übernahme fremder Namen, Lore, Assets, Musik, Karten, UI oder Figuren | `PROJEKT.md`, `CLEAN_ROOM.md`, `IP_UND_LIZENZEN.md` |
| erster Umfang | 20–30-minütiger finalitätsnaher Vertical Slice vor Entscheidung über das Vollprodukt | `GAME_DESIGN.md` |
| Runtime | .NET 10 LTS; C# für Host/Interop/Hotpaths, F# für Harness und Offline-Werkzeuge | ADR 001 |
| Plattform/Rendering | SDL3 + bgfx; Windows D3D11, Linux OpenGL 3.3, macOS Metal | ADR 002 |
| Hardwareziel | 1080p/30 auf GTX-660-/M1-8-GB-Klasse; 1080p/60 High auf RX-580-Klasse; kein RT/Echtzeit-GI | `PERFORMANCE_BUDGET.md` |
| Runtime-Betrieb | lokales Einzelspiel ohne Konto, Bezahlung oder Pflicht-Cloud | `PROJEKT.md`, NF-004 |
| KI-Nachvollziehbarkeit | lokales Harness, hashverkettete Runs, kuratiertes Memory, BM25-RAG und Evidenz | ADR 003, `HARNESS.md` |
| Memory-Freigabe | der erzeugende Agent darf nicht selbst annehmen; ein separater Reviewlauf darf technisch objektive, quellenidentische Records annehmen, kreative/produktbezogene/lizenzielle Entscheidungen nur die Projektleitung | `HARNESS.md`, T-002 |
| Memory-Lebenszyklus | append-only Revisionen; Quelle/Hash, Status, Konflikt und Staleness bleiben sichtbar; keine automatische Annahme oder stille Löschung | `HARNESS.md`, ADR 003 |
| Assetursprung und Arbeitsmodus | 100 % der kreativen Shipping-Assets entstehen KI- beziehungsweise agentisch synthetisch aus eigenen Spezifikationen; die Projektleitung testet integrierte Builds und gibt Feedback statt Assets manuell zu produzieren | ADR 004 |
| OS-Baseline | gewartete .NET-10-Betriebssysteme gemäß `PLATTFORMMATRIX.md`; Hardwarejahr und OS-Support sind getrennt | `PLATTFORMMATRIX.md` |
| M1-Speichergrenze | Ziel ≤ 3,5 GB; hart ≤ 4,0 GB im Spiel und ≤ 4,5 GB kurzer Ladepeak | `PERFORMANCE_BUDGET.md` |
| Tool-/Komponentenwahl | FOSS-first; neue Abhängigkeiten nur mit Version, Lizenz, Zweck und Austauschstrategie | `IP_UND_LIZENZEN.md` |

## Blocker nach nächstem Arbeitspaket

| Nächster Schritt | Vor `READY` mindestens zu klären |
|---|---|
| T-002 Memory-Promotion | keine offene Produktentscheidung; implementiert die oben festgelegte getrennte Freigabe und append-only Lebenszyklusregeln |
| T-003 Asset-Provenienz | keine offene Produktentscheidung: unbekannte Modelle bleiben Quarantäne; Backup/Restore ist erst vor erster Source-Promotion Pflicht; Q-AST-003 ist durch ADR 004 entschieden |
| T-005/T-006/T-007 .NET-Assetkalibrierung | keine offene Produktentscheidung: `DOTNET_GENERATOR_CONTRACT.md` fixiert Spec, akzeptierten Inspector, direkten GLB-Writer, deterministischen CPU-Rasterizer/PNG, .NET-Pin, BCL-only-/In-process-Grenzen, Proxybudgets, Journal und Fresh-Checkout-Nachweis. T-005 bleibt historisch abgenommen; T-006 amendiert Identifier/Quellen/Pin und verlangt seine komplette Regression. T-003 und die jeweilige Vorgängerstufe bleiben technische Abhängigkeiten. Q-AST-001/002/004 blockieren weder den rein prozeduralen Quarantänespike noch seine Strukturproxies |
| T-004 Run-Provenienz/Evidenz | keine offene Produktentscheidung; nach T-002 gegen den festgelegten lokalen Evidenz-, Redaction- und 180-Tage-Retention-Vertrag schneiden |
| T-010 Plattform-Walking-Skeleton | seit 2026-08-23 bereit: Q-TEC-001/Q-TEC-003 sind verfahrensmäßig entschieden (Klärungsprotokoll); die konkreten Pins und nativen Build-/Cachedetails entstehen als gatender erster Abschnitt des Auftrags `.ai/tasks/T-010-native-walking-skeleton.json` gemäß der Spike-Klausel in `QUALITAET.md`; Audio bleibt getrennter Spike; konkrete Treiberminima entstehen aus dem Smoke-Test |
| T-020 Leere Benchmarkszene | seit 2026-08-24 bereit: keine offene Produktentscheidung blockiert die isolierte Renderer-Baseline; Q-OPS-001 ist für diesen Auftrag verfahrensmäßig behandelt (Entwickler-PC-Messungen sind diagnostische Baseline, Profilbestehen nur mit deklarierter Referenzklassenbindung, fehlende Profile bleiben `NOT-MEASURED` mit Eskalation; Klärungsprotokoll) |
| T-021 Simulation | Q-TEC-004, Q-TEC-005, Q-OPS-001 |
| T-022 Zuverlässigkeits-Soak | Q-TEC-004, Q-TEC-010, Q-OPS-001 |
| T-030 Graybox-Hybridspiel | Q-GAM-001 bis Q-GAM-007 und Q-NAR-002 |
| T-031 Save/Recovery | Q-TEC-006, Q-GAM-007 |
| finalitätsnaher Vertical Slice | Q-NAR-001 bis Q-NAR-007, Q-GAM-008, Q-AST-004 |
| öffentliche Veröffentlichung | Q-PRD-001 bis Q-PRD-005, Q-OPS-002 bis Q-OPS-004 |

## Produkt, Umfang und Veröffentlichung

| ID | Frage / notwendige Entscheidung | Arbeitsannahme bis zur Entscheidung | Benötigt vor | Verantwortlich | Status |
|---|---|---|---|---|---|
| Q-PRD-001 | Unter welcher Lizenz erscheinen eigener Spielcode, Offline-Werkzeuge und Assets jeweils? | keine Lizenzbehauptung; Repository nicht als freigegebenes FOSS behandeln | externe Beiträge / Veröffentlichung | Projektleitung, bei Bedarf Rechtsberatung | OFFEN |
| Q-PRD-002 | Wie lautet der öffentliche Titel, und welche Welt-, Kultur- und Figurennamen bestehen eine Namens-/Markenprüfung? | `Project Riftward`, Wanderbruch, Tiefenchor und Kulturnamen bleiben interne Arbeitstitel | öffentliche Ankündigung | Creative Lead / Projektleitung | OFFEN |
| Q-PRD-003 | Wie groß wird das Vollprodukt: Spielstunden, Regionen, Missionen, Kulturen, Helden und Einheitensets? | keine Vollproduktion; Umfang erst aus gemessener Content-Pipeline und Slice-Abnahme ableiten | Produktionsfreigabe nach VS-001 | Projektleitung | OFFEN |
| Q-PRD-004 | Welches Zeit-, Geld- und Rechenbudget gilt für Vertical Slice und spätere Assetproduktion? | FOSS-first und lokale Verarbeitung bevorzugen; keine kostenpflichtige Verpflichtung | Kapazitäts-/Lieferplan | Projektleitung | OFFEN |
| Q-PRD-005 | Welche Vertriebswege, Preise und Zielregionen sind vorgesehen? | lokal installierbares Einzelspielerpaket; kein Store exklusiv voraussetzen | Packaging, Altersfreigabe, Storeintegration | Projektleitung | OFFEN |

## Welt, Erzählung und Atmosphäre

| ID | Frage / notwendige Entscheidung | Arbeitsannahme bis zur Entscheidung | Benötigt vor | Verantwortlich | Status |
|---|---|---|---|---|---|
| Q-NAR-001 | Welche Region, Gemeinschaft und konkrete Bruchanomalie trägt VS-001? | eigenständige Ruinenlandschaft mit Schutzquartier; noch keine Kultur fest auswählen | finaler Karten-/Assetbrief | Creative Lead | OFFEN |
| Q-NAR-002 | Wer sind Hauptfigur und zwei Begleiter konkret: Namen, Alltag, Motive, Konflikte, Rollen und visuelle Negativlisten? | sterbliche Kartografin/Feldingenieurin des Saumwerks als Konzept; keine finalen Designs | Dialog-, Fähigkeit- und Graybox-Content | Narrative/Creative Lead | OFFEN |
| Q-NAR-003 | Welche lokale Gemeinschaft bittet um Hilfe, und welche nachvollziehbaren Interessen haben ihre internen Gruppen? | keine biologisch festgelegte Gut-/Böse-Fraktion | Quest- und Charakterproduktion | Narrative Lead | OFFEN |
| Q-NAR-004 | Welche zwei Questoptionen bietet VS-001, und welche Folgen wirken jeweils persönlich → strategisch sowie strategisch → persönlich? | beide Optionen müssen verständliche Vorteile, Kosten und sichtbaren Nachhall besitzen | VS-Questgraph | Game/Narrative Design | OFFEN |
| Q-NAR-005 | Was ist die befestigte Bedrohung und der Boss, ohne eine bekannte Figur/Fraktion nachzubilden? | Konflikt aus lokalen Interessen plus regelhafter Wanderbruchfolge, nicht „reines Böse“ | Gegner-/Boss-Spezifikation | Creative/Game Design | OFFEN |
| Q-NAR-006 | Welche drei Riftward-spezifischen Weltprinzipien soll ein Tester nach der Mission erinnern? | Wanderbruch, Tiefenchor und Heimat-durch-Arbeit sind Kandidaten | Atmosphären-Testskript | Creative Lead | OFFEN |
| Q-NAR-007 | Welche drei unabhängigen Keyframes, Musikmotive und Klangregeln bilden die freigegebene VS-Bible? | Zieladjektive wehmütig, erdig, wundersam, entschlossen; keine konkreten Fremdwerke als Referenz | skalierte Assetproduktion | Art/Audio Direction | OFFEN |

## Gameplay und Bedienung

| ID | Frage / notwendige Entscheidung | Arbeitsannahme bis zur Entscheidung | Benötigt vor | Verantwortlich | Status |
|---|---|---|---|---|---|
| Q-GAM-001 | Was bedeuten die zwei Ressourcen fachlich, wie werden sie gewonnen/transportiert und welche Entscheidungen erzeugen sie? | exakt zwei Ressourcen; keine finalen Namen oder Wirtschaftsketten | T-030 | Game Design | OFFEN |
| Q-GAM-002 | Welche fünf Gebäude und vier ausbildbaren Einheitentypen bilden eine vollständige, lesbare VS-Wirtschaft? | mindestens Hauptquartier-/Versorgungs-, Produktions- und Schutzfunktionen; keine konkrete Liste | T-030 | Game Design | OFFEN |
| Q-GAM-003 | Welche Werte, Schadens-/Rüstungsregeln, Zielprioritäten, Reichweiten und Statusregeln gelten für Helden und Armee? | wenige klar lesbare Interaktionen; keine Balancewerte erfinden | Kampfsimulations-Task | Game Design | OFFEN |
| Q-GAM-004 | Welche Rolle haben Arbeiter, Bauplatzierung, Produktionswarteschlangen und Nachschub konkret? | sichtbare Transporte und Bewohner; keine vollautomatische oder klassische Worker-Logik voraussetzen | Wirtschafts-/Basis-Task | Game Design | OFFEN |
| Q-GAM-005 | Wie funktionieren Auswahl, Kontrollgruppen, Formationen, kontextuelle Befehle, Kamera und Minimap im Detail? | Maus/Tastatur und klassische semantische RTS-Aktionen; frei belegbar | Input-/Command-Spezifikation | UX/Game Design | OFFEN |
| Q-GAM-006 | Welche Pausen-, Zeitsteuerungs- und Dialogregeln gelten während Gefahr und Basisangriff? | Menüs dürfen sicher pausieren; taktische aktive Pause ist nicht vorausgesetzt | Simulations-/UI-Vertrag | Game Design | OFFEN |
| Q-GAM-007 | Wann ist ein Held kampfunfähig, wann scheitert die Mission, und wie funktionieren Checkpoints/Retry? | kein unklarer Softlock; letzter gültiger Checkpoint bleibt nutzbar | Save-/Mission-State-Spezifikation | Game Design | OFFEN |
| Q-GAM-008 | Welche Schwierigkeitsgrade und Hilfen werden im Vertical Slice angeboten, ohne Simulation oder Sichtbarkeit zwischen Hardwareprofilen zu verändern? | eine abgestimmte Standardschwierigkeit; Zugänglichkeit nicht an Schwierigkeit koppeln | finalitätsnahe Abnahme | Game/UX Design | OFFEN |
| Q-GAM-009 | Soll Gamepadsteuerung Teil des Vertical Slice sein? | Maus/Tastatur ist Pflicht; Gamepad nicht im Muss-Umfang | Eingabeumfang freigeben | Projektleitung / UX | OFFEN |

## Runtime, Daten und Performance

| ID | Frage / notwendige Entscheidung | Arbeitsannahme bis zur Entscheidung | Benötigt vor | Verantwortlich | Status |
|---|---|---|---|---|---|
| Q-TEC-001 | Welche exakten SDL3-/bgfx-/bx-/bimg-Commits und Buildoptionen werden gepinnt? | ADR-Stack beibehalten; aktuelle Versionen erst im reproduzierbaren Spike wählen | erster gatender Abschnitt von T-010 (Verfahren entschieden, konkrete Werte offen; Klärungsprotokoll 2026-08-23) | Technical Lead | OFFEN |
| Q-TEC-002 | Welche konkreten GPU-Treiberversionen bestehen die Referenztests, und rechtfertigt der Test-/Supportaufwand zusätzlich macOS x64? | OS-Baselines und drei Pflicht-RIDs stehen in `PLATTFORMMATRIX.md`; macOS x64 bleibt außerhalb des Mussumfangs | Release-CI- und Supportmatrix | Projektleitung / Technical Lead | OFFEN |
| Q-TEC-003 | Wie werden native Abhängigkeiten pro RID gebaut, gecacht, geprüft und lizenziert ausgeliefert? | native Builds je Ziel-OS, vollständige Commit-Hashes und ABI-Smokes | T-010-Abschnitt 0 bzw. T-011 (Verfahren entschieden; Klärungsprotokoll 2026-08-23) | Technical Lead | OFFEN |
| Q-TEC-004 | Welche Numerik und Determinismus-Toleranz gelten für die auf 20 Hz budgetierte Simulation plattformübergreifend? | 20 Hz und entkoppeltes Rendering; keine exakte Cross-Plattform-Hashgarantie vor Spike | T-021 | Simulation Lead | OFFEN |
| Q-TEC-005 | Welche Job-, Speicher-, Navigations- und Zustandsstruktur besteht den 250-Agenten-Stresstest? | datennahe, allokationsarme Strukturen und hierarchisch budgetierte Pfadsuche; kein Framework vorentscheiden | T-021 Architekturfreigabe | Technical Lead | OFFEN |
| Q-TEC-006 | Welches Cooked-Paket-, Definition-, Save- und Replayformat erfüllt Lade-, Migrations- und AOT-Anforderungen? | logische Verträge aus `DATENMODELL.md`; keine Runtime-Datenbank | Content-/Save-Task | Technical Lead | OFFEN |
| Q-TEC-007 | SDL3-Core-Audio oder SDL3_mixer, welche Formate/Decoder und welches Streamingmodell? | kleinster Pfad, der Atmosphärenlayer und Budgets erfüllt | Audio-Spike | Audio/Technical Lead | OFFEN |
| Q-TEC-008 | Liefert pro RID Native AOT oder selbstenthaltenes CoreCLR die besseren Start-, Speicher-, Lade- und Framewerte? | beide warnungsfrei publizieren und messen; keine Dogmen | nach T-010 und Belastungstest | Technical Lead | OFFEN |
| Q-TEC-009 | Welches Messverfahren weist die entschiedene M1/8-GB-Grenze reproduzierbar über eine komplette Mission und Ladepeaks nach? | Grenze aus `PERFORMANCE_BUDGET.md`; OS-Speicherdruck plus Prozess-/GPU-Metriken protokollieren | M1-Hardwarebenchmark | Performance Lead | OFFEN |
| Q-TEC-010 | Welche Allokationsgrenze je warmem Simulationstick und welche tolerierte Benchmarkstreuung gelten? | nahe null; Grenzwert aus reproduzierbarem Spike | Performancebaseline | Performance Lead | OFFEN |

## KI-, Asset- und Harness-Produktion

| ID | Frage / notwendige Entscheidung | Arbeitsannahme bis zur Entscheidung | Benötigt vor | Verantwortlich | Status |
|---|---|---|---|---|---|
| Q-AST-001 | Welche lokalen oder externen Generatoren sind je Assettyp zugelassen, mit welcher Lizenz-/Outputgrundlage, Version und Reproduzierbarkeit? | `models.lock.json` bleibt fail-closed: unbekannter Output darf kalibrieren, aber nicht `approved` werden | erste Assetfreigabe; nicht T-003-Validator | Projektleitung / Lizenzreview | OFFEN |
| Q-AST-002 | Wo liegen große Rohassets, Varianten, Benchmarks und Traces; wie funktionieren Hashadressierung, Backup und Retention? | Quarantäne und Cooked Output bleiben lokal/regenerierbar; kleine Manifeste, Receipts und Hashes werden versioniert | erste Promotion wichtiger Binärquellen / Produktionsskalierung | Technical/Production Lead | OFFEN |
| Q-AST-003 | Wer darf Assets aus Quarantäne in `visuell geprüft`, `lizenzgeprüft` und `freigegeben` überführen? | Erzeuger und Originalitäts-/Lizenzreviewer sind getrennte Agentidentitäten; unklare Rechte, zweifelhafte Ähnlichkeit und Meilensteinfreigaben eskalieren an die Projektleitung | T-003 | Projektleitung | ENTSCHIEDEN durch ADR 004 |
| Q-AST-004 | Welche messbaren Polygon-, LOD-, UV-, Kollision-, Animations-, Audio- und VFX-Gates gelten pro Assetklasse zusätzlich zu den Szenenbudgets? | Ausgangswerte aus `PERFORMANCE_BUDGET.md`; Details je Asset-Spezifikation | finalitätsnahe Assetproduktion | Art/Technical Lead | OFFEN |
| Q-HAR-003 | Welches Retrieval-Eval und welcher Mindestgewinn rechtfertigen lokale Embeddings zusätzlich zu BM25? | keine Embeddings, bis ein gepinntes Eval messbaren Recall-Nutzen zeigt | semantische RAG-Erweiterung | Harness Lead | OFFEN |
| Q-HAR-004 | Welche Modell-/Agentanbieter sind für autonome Produktion zugelassen, welche Daten dürfen sie erhalten und wie werden Kostenlimits durchgesetzt? | lokale/offline Werkzeuge bevorzugen; keine Secrets/fremden Daten; kein Anbieterzwang | externer KI-Einsatz | Projektleitung | OFFEN |

## Betrieb, Test und Freigabe

| ID | Frage / notwendige Entscheidung | Arbeitsannahme bis zur Entscheidung | Benötigt vor | Verantwortlich | Status |
|---|---|---|---|---|---|
| Q-OPS-001 | Welche realen Referenzrechner, OS-/Treiberstände und Verantwortlichen liefern GTX-660-, M1-8-GB- und RX-580-Abnahmeläufe? | schnellere Hardware ist nur Diagnose, kein Ersatznachweis | T-020 | Projektleitung / Performance Lead | OFFEN |
| Q-OPS-002 | Welche CI-Runner und Signierumgebungen bauen Windows, Linux und macOS nativ? | kein Cross-OS-Build als alleiniger Releasebeleg | T-011 / Releasepipeline | Technical Lead | OFFEN |
| Q-OPS-003 | Welche Installer-/Archivformate, Code-Signing-, Notarisierungs- und Updateverfahren gelten je Plattform? | manuell installierbares, offline nutzbares Paket; kein Auto-Updater vorausgesetzt | Release Candidate | Projektleitung / Release Lead | OFFEN |
| Q-OPS-004 | Wird optionale Telemetrie überhaupt angeboten; wenn ja, welche Daten, Einwilligung, Aufbewahrung und Abschaltung gelten? | keine Runtime-Telemetrie; nur lokale Entwicklungsmetriken | jede Telemetrieimplementierung | Projektleitung / Datenschutzreview | OFFEN |
| Q-OPS-005 | Wie viele externe Atmosphäre-, UX- und Zugänglichkeitstester stehen für die messbaren Rubriken zur Verfügung? | Rubriken bleiben Gates; Stichprobengröße vor Auswertung festlegen | finalitätsnahe VS-Abnahme | Projektleitung | OFFEN |

## Klärungsprotokoll

Eine Entscheidung wird aus der obigen Tabelle entfernt oder auf `ENTSCHIEDEN` gesetzt, sobald sie mit Datum, Verantwortlichem, Folgen und gegebenenfalls ADR dokumentiert ist.

| Datum | ID | Entscheidung / Ergebnis | Quelle | Verantwortlich |
|---|---|---|---|---|
| 2026-08-13 | Q-AST-003 | Routineproduktion nutzt getrennte Erzeuger- und Originalitäts-/Lizenzreviewer; unklare Fälle eskalieren. | ADR 004 | Projektleitung |
| 2026-08-13 | T-003-READINESS | Q-AST-001 und Q-AST-002 blockieren Freigabe/Promotion, nicht den fail-closed Validator. | T-003 Readiness Notes | Harness Lead |
| 2026-08-23 | Q-TEC-001 (Verfahren) | Der ADR-002-Stack bleibt unverändert; die konkreten SDL3-/bgfx-/bx-/bimg-Pins werden als erster gatender Abschnitt von T-010 ausschließlich nach dort fixierten Kriterien gewählt und in `toolchain.lock.json` fixiert (Upstream-URL, Commit/Tag, Abrufdatum, Quell-SHA-256, SPDX-Lizenz). Rückrollbar durch Pin-Austausch mit anschließendem Neubau; die Endwerte bleiben Technical-Lead-Entscheidung im Spike. | `.ai/tasks/T-010-native-walking-skeleton.json` (Abschnitt 0), `QUALITAET.md` Definition of Ready (Spike-Klausel) | Autonomer Planungsagent gemäß Autorisierung der Projektleitung vom 2026-08-23 |
| 2026-08-23 | Q-TEC-003 (Verfahren) | Native Abhängigkeiten werden je Zielbetriebssystem aus commitgepinnten Quellen gebaut; Quellbeschaffung ist protokolliert und hashverifiziert, Artefakte liegen hashgeprüft in einem lokalen Cache außerhalb von Git, je RID gibt es einen ABI-Smoke, Lizenzen wandern in `THIRD_PARTY_NOTICES.md`. Konkrete Buildoptionen und Cachelayout sind implementierseitig reversible Details. Rückrollbar durch Neubau bzw. Layoutänderung ohne Auswirkung auf Verträge. | dito | Autonomer Planungsagent gemäß Autorisierung der Projektleitung vom 2026-08-23 |
| 2026-08-24 | Q-OPS-001 (Verfahren, T-020) | Bis die Projektleitung konkrete Referenzrechner benennt, gelten T-020-Messungen auf dem Entwickler-PC (i7-3770/RX 570) ausschließlich als diagnostische Baseline gemäß bestehender Arbeitsannahme (`QUALITAET.md` Performance-Matrix). Ein Profilbestehen entsteht nur durch deklarierte Bindung an die zugehörige Referenzklasse; fehlende Profile bleiben im Report `NOT-MEASURED` und eskalieren, statt durch Diagnose-, Cross-Compile- oder Simulationsergebnisse ersetzt zu werden. Rückrollbar durch Benennung der Referenzmaschinen und Wiederholung desselben bench-Befehls; die Frage selbst bleibt `OFFEN`. | `.ai/tasks/T-020-empty-scene-benchmark.json`, `docs/QUALITAET.md`, `BACKLOG.md` | Autonomer Planungsagent gemäß Autorisierung der Projektleitung vom 2026-08-23 |
