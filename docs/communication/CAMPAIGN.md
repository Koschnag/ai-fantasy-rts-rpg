# Kommunikationskampagne: Build in public, measure in public

## Positionierung

Project Riftward ist kein „KI baut über Nacht ein fertiges Spiel“-Versprechen.
Es ist ein öffentlich prüfbarer Versuch, zwei Fragen praktisch zu verbinden:

1. Wie weit kommt eine full-agentic SDLC, wenn Spezifikation, Orakel, Evidenz
   und Recovery als System gebaut werden?
2. Wie viel modernes Spielerlebnis lässt sich durch harte Optimierung auf
   alter beziehungsweise kleiner Hardware erhalten?
3. Was ändert sich, wenn Effizienz und lange Hardware-Nutzbarkeit als
   Produktqualität und nicht als nachträglicher Low-Spec-Modus behandelt werden?

Der vorläufige Leitsatz lautet:

> Build the system. Measure the outcome. Publish the failures.

Der Projektname ist ein Forschungs-Codename und vor breiter Veröffentlichung
markenrechtlich zu prüfen.

## Zielgruppen

- KI-/Agent- und Software-Engineering-Praktiker
- Engine-, Rendering- und Performance-Entwickler
- Indie- und FOSS-orientierte Game-Developer
- Forschende zu Human–AI Collaboration und agentischen Systemen
- Spieler mit älterer, integrierter oder energiebegrenzter Hardware

## Fünf Inhaltssäulen

| Säule | Erzählung | belastbarer Beleg |
|---|---|---|
| Agentic SDLC | Nicht der Prompt, sondern das rückgekoppelte System ist das Experiment. | Task, Run, Commit, Gates, Evidence |
| Low-spec by design | Atmosphäre und Lesbarkeit erhalten ein Hardwarebudget, bevor Content skaliert. | feste Profile, Frame-/Speicherbudgets, später Traces |
| Efficiency is a feature | Hardware-Eskalation ist kein Naturgesetz; jede teure Laufzeitentscheidung muss ihren sichtbaren oder spielerischen Nutzen verdienen. | A/B-Messungen derselben Szene, Optimierungs-Ablationen, DDR3-/Linux-/M1-Evidence |
| Evidence over hype | Fehlschläge und Datenlücken werden nicht aus der Geschichte geschnitten. | Fallstudienlog, negative Gates, verworfene Kandidaten |
| Synthetic worldbuilding | Eigene Weltregeln, Clean Room und Provenienz statt Stilkopie. | Specs, Manifeste, Quarantäne und Reviews |

## Veröffentlichungsrhythmus für die ersten vier Wochen

| Woche | Hauptstück | kurze Begleitstücke | Call to action |
|---|---|---|---|
| 1 | Forschungsstart und Baseline | Harness-Grafik, „was noch nicht existiert“ | Methodik kritisieren |
| 2 | erster Runtime-/Gate-Bericht | Fehlversuch, Recovery, Kostenkarte | Reproduktionslauf melden |
| 3 | Low-spec-Budget und erste Messszene | ein Budget, ein Trade-off, ein Screenshot | Hardwaretester vormerken |
| 4 | Monats-Evidenzpaket | Autonomiedauer, Eingriffe, Kosten, Nullresultate | nächste Hypothese vorschlagen |

Kein Kanal erhält mehr als zwei reine Fortschrittsposts pro Woche. Ein Post
ohne neue Evidenz wird als Welt-/Methodenstück gekennzeichnet.

## Startpost – Deutsch

> Wie weit kann KI heute wirklich ein komplexes Softwareprodukt bauen – nicht
> in einer Demo, sondern mit Spezifikation, Tests, Recovery, Provenienz und
> Hardwarebudgets?
>
> Project Riftward ist mein offenes Forschungsprojekt: ein eigenständiger
> Fantasy-RTS/RPG-Hybrid, dessen Produktionskette so agentisch wie vertretbar
> arbeitet. Gleichzeitig testen wir, wie viel Atmosphäre, Lesbarkeit und
> Systemtiefe auf GTX-660-/M1-8-GB-Klasse möglich ist, wenn Performance kein
> später Patch, sondern eine frühe Produktanforderung ist.
>
> Das ist keine „fertig über Nacht“-Behauptung. Der aktuelle Stand ist ein
> reproduzierbares Produktionsfundament; die Runtime beginnt gerade. Ich werde
> Commits, Gates, Kosten, Fehlversuche, Eingriffe und später echte
> Hardwaremessungen veröffentlichen. Auch dann, wenn eine These nicht hält.
>
> Forschungsprotokoll, Code und laufende Evidenz: [Repository-Link]

## Startpost – Kurzfassung

> Can a full-agentic SDLC build a modern-feeling RTS/RPG for genuinely old
> hardware? Project Riftward treats that as an experiment, not a slogan:
> versioned intent, agents, hard gates, evidence, recovery and public failures.
> Runtime work is starting; no fake gameplay claims. Follow the commits and
> measurement protocol: [Repository-Link]

## Wiederverwendbare Formate

### Evidence card

- Ausgangscommit → Ergebniscommit
- Auftrag und überprüfbares Ziel
- autonome Laufzeit / menschliche Eingriffe
- Requests, Kosten und Maschinenzeit soweit verfügbar
- grüne und rote Gates
- Ergebnis und verbleibende Unsicherheit

### Failure note

1. Was sollte passieren?
2. Was ist beobachtbar schiefgegangen?
3. Welches Orakel hat es entdeckt oder übersehen?
4. Wurde repariert, verworfen oder eskaliert?
5. Welche Systemänderung folgt daraus?

### Low-spec note

Ein Bild, ein festes Hardwareprofil, ein Commit, eine Szene, p50/p95/p99,
RAM/VRAM und genau ein sichtbarer Trade-off. Keine FPS-Zahl ohne Messvertrag.

## Repository-Metadaten

Empfohlene Beschreibung:

> Open research project: a full-agentic, evidence-driven SDLC building an original low-spec fantasy RTS/RPG from clean-room specifications.

Empfohlene Topics:

`agentic-ai`, `game-development`, `rts`, `rpg`, `low-spec`,
`performance-engineering`, `evidence-driven-development`, `fsharp`, `dotnet`,
`sdl3`, `bgfx`, `research`

## Kommunikationsgates

Vor Veröffentlichung muss jeder konkrete Claim mindestens eine der folgenden
Quellen besitzen: festes Dokument, Commit, ausführbares Gate, Messartefakt oder
klar als Hypothese markierte Forschungsfrage.

Verboten sind insbesondere:

- Konzeptbilder als Gameplay oder fertiges Produkt auszugeben
- „vollautonom“, wenn der Lauf auf Eingabe wartet oder wiederholt festhängt
- Modell-/Providerkosten mit Gesamtenergie gleichzusetzen
- Ziel-FPS ohne reale Zielhardware, Szene und Trace als erreicht zu melden
- Quarantäneassets ohne Status-, Prompt- und Herkunftshinweis zu veröffentlichen
- FOSS-, Copyright-, Exklusivitäts- oder Markenclaims ohne abgeschlossene Prüfung
- pauschale Behauptungen, alle modernen Großproduktionen oder ein bestimmtes
  Betriebssystem verschwendeten Ressourcen, ohne vergleichbare Messdaten

## Produktpolitische Position

> Wir akzeptieren steigende Mindestanforderungen nicht als automatischen Preis
> für bessere Spiele. Riftward versucht zuerst, vorhandene Hardware durch
> bessere Datenstrukturen, kleine Laufzeitschichten, Offline-Cooking, feste
> Budgets und harte Messung besser zu nutzen. KI soll den einmaligen
> Entwicklungs- und Optimierungsaufwand skalierbar machen; ob diese Investition
> über reale Spielsitzungen Ressourcen spart, bleibt eine offene Bilanz.

Der Gegner dieser Position ist eine Gewohnheit, kein einzelnes Produkt: Effekte,
Abstraktionen und Komfortschichten dürfen Ressourcen kosten, müssen ihren
Nutzen aber sichtbar belegen. Das Projekt nennt keine pauschalen Sieger, bevor
die eigene Runtime, Zielhardware und Erlebnisqualität gemessen sind.

## Wirkungsmessung

Neben Reichweite werden qualifizierte Beiträge gemessen: reproduzierte Runs,
Hardwaretest-Zusagen, Issues mit Methodenkritik, externe Messdaten, gefundene
Fehler und übernommene Forschungsartefakte. Likes allein sind kein
Forschungsergebnis.
