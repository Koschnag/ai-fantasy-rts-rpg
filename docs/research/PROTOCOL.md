# Forschungsprotokoll

**Version:** 0.1

**Stand:** 2026-08-23

**Status:** explorativ; Messplan vor dem Runtime-Vertical-Slice zu präregistrieren

## Untersuchungsgegenstand

Untersuchungseinheit ist nicht die einzelne Modellantwort, sondern eine
vollständige, versionierte Änderung von einem freigegebenen Arbeitsauftrag bis
zu einem akzeptierten oder verworfenen Ergebnis. Code, Spezifikation,
Retrieval, Tools, Tests, Assets, Reviews und Laufzeitmessungen gehören gemeinsam
zum beobachteten System.

## Forschungsfragen und Hypothesen

| ID | Frage | vorläufige Hypothese | Widerlegungssignal |
|---|---|---|---|
| RQ-01 | Wie autonom kann die Lieferkette laufen? | Härtere Orakel erhöhen die Zeit bis zum notwendigen menschlichen Eingriff. | Mehr Laufzeit erzeugt nur Rework, Gate-Umgehung oder unbemerkte Defekte. |
| RQ-02 | Was leistet CCD im Projekt? | Ein versionierter Zusammenhang aus Spezifikation, Tests, Risiken, Entscheidungen und Evidenz verbessert Konvergenz stärker als Prompt-Optimierung allein. | Vergleichbare Aufgaben werden ohne diesen Zusammenhang gleich gut oder besser abgeschlossen. |
| RQ-03 | Wie niedrig kann das Hardwareziel bleiben? | Frühe feste Budgets und ein schlanker Runtime-Unterbau erhalten Lesbarkeit und Atmosphäre auf GTX-660-/M1-Klasse. | Repräsentative Szenen verfehlen Frame-, Speicher- oder Qualitätsziele trotz Scope- und Effektkontrolle. |
| RQ-04 | Lässt sich Entwicklungs-Compute amortisieren? | Zusätzlicher einmaliger Optimierungsaufwand kann bei hinreichend vielen Spielsitzungen geringere Endgeräteanforderungen aufwiegen. | Entwicklungsaufwand, geringe Nutzung, Rebound oder kaum vermiedene Hardware machen die Bilanz negativ oder unentscheidbar. |
| RQ-05 | Bleibt die kreative Identität eigenständig? | Interne Weltregeln, Clean Room, unabhängige Reviews und Provenienz erzeugen eine kohärente eigene Bild-/Spielidentität. | Blindtests erkennen keine Projektidentität oder ordnen Material spontan einem konkreten Fremdwerk zu. |
| RQ-06 | Ist steigender Hardwarebedarf für ein besseres Spielerlebnis notwendig? | Ein kleiner eigener Runtime-Unterbau, feste Budgets und gezielte Optimierung erhalten mehr Spielsystem, Lesbarkeit und Atmosphäre auf DDR3-/GTX-660-, Linux- und M1-8-GB-Systemen, als eine frühe Festlegung auf High-End-Hardware erwarten ließe. | Die akzeptierte Erlebnisqualität ist nur durch Scopeverlust, versteckte Auflösungsreduktion oder stärkere Hardware erreichbar; oder der zusätzliche KI-/Optimierungsaufwand übersteigt den nachweisbaren Nutzen. |

Die Hypothesen dürfen nach Beginn eines Experiments nicht passend zum Ergebnis
umformuliert werden. Neue Hypothesen erhalten eine neue Version und ein Datum.

## Messgrößen der agentischen SDLC

Pro akzeptierter oder verworfener Umsetzungseinheit werden mindestens erfasst:

| Dimension | Messgröße |
|---|---|
| Autonomie | Laufzeit, längste Sequenz ohne Mensch, Zahl und Art menschlicher Eingriffe, unbeantwortete Rückfragen |
| Aufwand | Requests, Tokens soweit verfügbar, Providerkosten, Maschinenzeit, menschliche Spezifikations-/Reviewzeit |
| Konvergenz | Schleifen bis Gate-Grün, fehlgeschlagene Versuche, Recovery, Reverts, Nacharbeit nach Review |
| Qualität | Build-/Teststatus, Defekte nach Akzeptanz, Gate-Abdeckung, deterministische Reproduktion, Fresh-Checkout-Ergebnis |
| Nachvollziehbarkeit | Anteil gebundener Anforderungen, Runs, Retrieval-Traces, Evidenz, Assets und Entscheidungen |
| Sicherheit/Recht | Secret-, Dependency-, Lizenz-, Clean-Room- und Originalitätsbefunde |

„24 Stunden Prozesslaufzeit“ ist keine Autonomiemessung, wenn der Prozess
wartet, dieselbe Aktion wiederholt oder keine akzeptierte Änderung erzeugt.

## Messgrößen des Spiels

Jeder Performancevergleich bindet Commit, Betriebssystem, Treiber,
Hardwareprofil, Grafikpreset, Szene, Seed, Aufwärmphase und Messdauer.

- Framezeit p50/p95/p99, 1%-Low-FPS und sichtbare Hitches
- CPU- und GPU-Zeit je System, Draw Calls, Dreiecke und sichtbare Einheiten
- Resident RAM, Peak RAM, VRAM-Schätzung und Ladezeit
- Stabilität sowie Speicherwachstum in Replay- und Soak-Läufen
- Leistungsaufnahme an der Steckdose, falls reproduzierbar messbar
- visuelle Lesbarkeit und wahrgenommene Qualität in blindem Screenshot-/Buildtest
- Abweichung von den festen Budgets in `docs/PERFORMANCE_BUDGET.md`

Qualität wird nicht aus Polygonzahl, Effektmenge oder Marketingbildern
abgeleitet. Konzeptgrafiken sind kein Gameplay- oder Performancebeleg.

## Compute- und Nachhaltigkeitsbilanz

![Konzeptionelle Amortisierungshypothese](../media/compute-amortization-hypothesis.svg)

Mindestens getrennt bilanziert werden:

1. **Entwicklung:** lokale Laufzeit, externe Modellaufrufe, CI, Generierung und
   verworfene Kandidaten.
2. **Distribution und Nutzung:** Downloadgröße, Spielsitzungen, reale
   Endgeräte, Laufzeit und Leistungsaufnahme.
3. **Gegenfaktum:** welche stärkere Hardware oder welcher andere
   Entwicklungsweg tatsächlich vermieden worden wäre.
4. **Lebenszyklus:** Herstellung, Lebensdauerverlängerung, Wiederverwendung und
   Rebound; nur mit offen gelegter Datenquelle und Unsicherheit.

Ohne providerseitige Energiedaten werden Requests, Tokens und Kosten als
Proxys publiziert, aber nicht in erfundene kWh oder CO₂e umgerechnet.

## Vergleich und Baselines

- Aufgaben werden nach Größe, Risiko und Domäne geschichtet; unähnliche Tasks
  werden nicht als Geschwindigkeitstest gegeneinander gestellt.
- Vor einem Vergleich werden Aufgabe, Gates, Zeitfenster und Abbruchregel
  eingefroren.
- Der wichtigste zeitliche Vergleich ist das Projekt gegen seine eigene
  vorherige Harness-/Runtime-Version.
- Performanceexperimente vergleichen denselben Commit oder klar isolierte
  A/B-Varianten derselben Szene: zunächst korrekt/unoptimiert gegen optimiert,
  anschließend einzelne Maßnahmen per Ablation. Verändert werden dürfen nicht
  gleichzeitig Spielumfang, Simulation, Kamera, Seed und Qualitätsziel.
- „Mehr Qualität pro Ressource“ wird gemeinsam aus Framezeit, RAM/VRAM,
  Lade-/Paketgröße, Leistungsaufnahme soweit messbar und blind bewerteter
  Lesbarkeit/Atmosphäre beurteilt. Eine bloß höhere FPS-Zahl nach entferntem
  Gameplay stützt RQ-06 nicht.
- Externe Engines, Spiele oder Medien dienen nicht als kreative
  Produktionsquelle. Öffentliche technische Benchmarks dürfen nur mit
  dokumentierter Methodik als Kontext dienen.
- Fehlende Telemetrie wird als fehlend ausgewiesen und nicht geschätzt.

Die Kritik an einer High-End-zentrierten Veröffentlichungskultur ist eine
offene Produktposition. Das Protokoll behauptet weder, jedes moderne Spiel sei
schlecht optimiert, noch dass ein bestimmtes Betriebssystem allein steigende
Anforderungen verursache. Solche allgemeineren Aussagen benötigen separate,
vergleichbare Daten außerhalb der Riftward-Einzelfallstudie.

## Evidenzpaket je Experiment

Ein publizierbares Paket enthält:

- Experiment-ID, Datum, Forschungsfrage und präregistrierte Erwartung
- Ausgangscommit, Taskmanifest, Agent-/Modell-/Toolchainbindung
- Start-/Endzeit, Kosten-/Requestdaten und Eingriffsprotokoll
- Gate- und Testartefakte einschließlich negativer Ergebnisse
- Ergebniscommit oder begründete Verwerfung
- bekannte Störfaktoren, Datenlücken und abweichende Entscheidungen
- kurze Einordnung: unterstützt, widerspricht oder unentscheidbar

Rohdaten werden nur veröffentlicht, wenn sie keine Secrets, unnötigen
personenbezogenen Daten oder vertraulichen Providerinformationen enthalten.
