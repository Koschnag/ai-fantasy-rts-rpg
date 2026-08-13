# Project Riftward

Interner, nicht öffentlich freigegebener Codename: **Project Riftward**
Status: **Vorproduktions-Fundament aufgebaut; Spielruntime noch nicht begonnen**
Letzte Aktualisierung: **2026-08-13**

Ein eigenständiger, düster-märchenhafter Echtzeitstrategie-/Rollenspiel-Hybrid mit Heldenentwicklung, Erkundung, Basisbau und Armeeführung. Das Spiel soll den langsamen, atmosphärischen Wechsel zwischen persönlichem Abenteuer und großflächiger Strategie aufgreifen, aber eine vollständig eigene Welt, Identität und Inhaltsbibliothek besitzen.

Das Repository wird zunächst als ausführbare Projektspezifikation und reproduzierbare Produktionsumgebung aufgebaut. Der erste Implementierungsmeilenstein ist ein hardwarevalidierter Vertical Slice.

## Einstieg

1. [PROJEKT.md](PROJEKT.md) – Problem, Zielbild, Zielgruppe und Umfang
2. [docs/OFFENE_FRAGEN.md](docs/OFFENE_FRAGEN.md) – Punkte, die wir gemeinsam klären
3. [docs/ANFORDERUNGEN.md](docs/ANFORDERUNGEN.md) – funktionale und nichtfunktionale Anforderungen
4. [docs/USER_FLOWS.md](docs/USER_FLOWS.md) – Nutzerwege und Fehlerfälle
5. [docs/ARCHITEKTUR.md](docs/ARCHITEKTUR.md) – technische Leitplanken
6. [docs/DATENMODELL.md](docs/DATENMODELL.md) – Daten, Beziehungen und Lebenszyklen
7. [BACKLOG.md](BACKLOG.md) – priorisierte Umsetzungseinheiten
8. [docs/QUALITAET.md](docs/QUALITAET.md) – Abnahme, Tests und Definition of Done
9. [docs/entscheidungen/README.md](docs/entscheidungen/README.md) – nachvollziehbare Entscheidungen
10. [AGENTS.md](AGENTS.md) – verbindliche Arbeitsregeln für implementierende KI-Agenten
11. [docs/GAME_DESIGN.md](docs/GAME_DESIGN.md) – Spielsäulen, Umfang und Vertical Slice
12. [docs/ART_DIRECTION.md](docs/ART_DIRECTION.md) – eigenständige Bild- und Klangidentität
13. [docs/PERFORMANCE_BUDGET.md](docs/PERFORMANCE_BUDGET.md) – feste Budgets für Zielhardware
14. [docs/AUTOMATION.md](docs/AUTOMATION.md) – autonome KI-Produktionsschleife und Qualitätsgates
15. [docs/IP_UND_LIZENZEN.md](docs/IP_UND_LIZENZEN.md) – FOSS- und Asset-Provenienzregeln
16. [docs/CLEAN_ROOM.md](docs/CLEAN_ROOM.md) – verbindliche Trennung von Genreanalyse und Produktion
17. [docs/ATMOSPHAERE.md](docs/ATMOSPHAERE.md) – emotionaler Nordstern, Weltidentität und Review-Rubrik
18. [docs/HARNESS.md](docs/HARNESS.md) – KI-Verlauf, Gedächtnis, RAG und Evidenz
19. [docs/TOOLCHAIN.md](docs/TOOLCHAIN.md) – installierte und geplante FOSS-Werkzeuge
20. [docs/PLATTFORMMATRIX.md](docs/PLATTFORMMATRIX.md) – OS-, Backend-, Paket- und Smoke-Baselines
21. [docs/ASSET_PIPELINE.md](docs/ASSET_PIPELINE.md) – synthetische Asseterzeugung, FOSS-Werkzeuge und Modellzulassung

## Statusbegriffe

- `OFFEN`: noch ungeklärt; darf nicht als Tatsache behandelt werden
- `ANGENOMMEN`: vorläufige Annahme; muss vor Umsetzung bestätigt werden
- `ENTSCHIEDEN`: bewusst festgelegt und dokumentiert
- `READY`: ausreichend spezifiziert und umsetzbar
- `DONE`: implementiert, geprüft und abgenommen

## Nächster Meilenstein

Die Vorproduktion ist abgeschlossen, wenn:

- Problem, Zielgruppe und Nutzenversprechen konkret beschrieben sind,
- MVP und Nicht-Ziele eindeutig abgegrenzt sind,
- die wichtigsten Nutzerwege Abnahmekriterien besitzen,
- Datenschutz, Sicherheit und technische Randbedingungen geklärt sind,
- die erste Umsetzungseinheit den Status `READY` hat.

`T-002`, das Asset-Provenienz-/Quarantänegate `T-003`, der unabhängige
`calibration-v1`-Inspector `T-005` und der BCL-only-F#/.NET-Assetgenerator
`T-006` sind abgenommen. Der Generator schreibt GLB direkt und rendert PNG
deterministisch auf der CPU, ohne Unterprozess, Netzwerk oder DCC. Sein
Fresh-Checkout-/CI-Nachweis `T-007` ist als nächster Auftrag `READY`.
T-006 amendiert bewusst nur Generator-Identifier, Quelleninventar und
.NET-Toolchainbindung; T-005 bleibt historisch abgenommen und wird vollständig
als Regression erneut ausgeführt.
Vollständige allgemeine Run-Provenienz/Evidenz (`T-004`) und das native
SDL3-/bgfx-Walking-Skeleton (`T-010`) folgen, sobald der jeweilige Auftrag
`READY` ist. Nur `READY`-Aufträge dürfen ohne weitere fachliche Klärung
gestartet werden.

## Leitentscheidungen

- eigenständiger Fantasy-RTS/RPG-Hybrid; keine Rekonstruktion eines bestimmten Fremdwerks
- eigener schlanker Runtime-Unterbau statt vollständiger Game Engine
- .NET 10 LTS, C# und F#, Release-Builds perspektivisch Native AOT
- Windows, Linux und macOS; plattformspezifische Builds und Tests
- mindestens 1920×1080/30 FPS auf i7-3770-/GTX-660-Klasse oder MacBook Air M1 mit 8 GB; 60 FPS werden bevorzugt
- die höchste geplante Grafikstufe ist für RX-580-Klasse ausgelegt; keine Raytracing- oder Echtzeit-GI-Pfade
- 100 % der kreativen Shipping-Assets entstehen KI- beziehungsweise agentisch synthetisch aus eigenen Spezifikationen; DCC-Verarbeitung, Gates und Nutzerfeedback liefern technische Qualität sowie dokumentierte Eigenständigkeitsnachweise, aber keine automatische Rechtsgarantie
- FOSS-first: externe Komponenten nur mit dokumentierter Lizenz, Version und Austauschstrategie

## Aktuelle Befehle

```bash
./scripts/rift.sh bootstrap
./scripts/rift.sh build
./scripts/rift.sh fmt
./scripts/rift.sh lint
./scripts/rift.sh test
./scripts/rift.sh security
./scripts/rift.sh asset-calibration validate-spec --spec assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json
./scripts/rift.sh rag-build
./scripts/rift.sh rag-query --query "Atmosphäre und RPG RTS Übergang" --top 5
./scripts/rift.sh harness memory status
RUN_ID="$(./scripts/rift.sh harness start-run)"
./scripts/rift.sh rag-query --query "Performancebudget" --top 5 --run "$RUN_ID"
./scripts/rift.sh verify
./scripts/rift.sh fresh-checkout-test
```

Noch nicht implementierte Produktionsgates schlagen ausdrücklich fehl, bis eine passende `READY`-Aufgabe sie umsetzt.
