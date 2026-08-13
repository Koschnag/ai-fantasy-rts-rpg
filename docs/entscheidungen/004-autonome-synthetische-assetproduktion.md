# ADR 004: Autonome synthetische Assetproduktion

- **Status:** akzeptiert
- **Datum:** 2026-08-13
- **Entscheidungsverantwortung:** Projektleitung
- **Bezug:** Z-004, Z-005, F-007, F-008; T-003, T-050, T-051; `CLEAN_ROOM.md`

## Kontext

Die Projektleitung möchte sämtliche visuellen, akustischen und dreidimensionalen Spielassets durch KI- beziehungsweise agentisch gesteuerte Synthese erzeugen lassen. Menschliche Arbeit soll sich auf das Testen des integrierten Spiels, zielgerichtetes Feedback und seltene Freigabeentscheidungen beschränken. Gleichzeitig müssen Herkunft, Eigenständigkeit, technische Budgets und spätere kommerzielle Nutzbarkeit nachprüfbar bleiben.

„KI-generiert“ bezeichnet hier den Ursprung der kreativen Assetdaten. Deterministische, von Agenten gesteuerte Verarbeitungsschritte wie Retopologie, UV-Erzeugung, Baking, LOD, Kompression, Rigging, Export und Validierung dürfen klassische FOSS-Werkzeuge verwenden. Fremde Spielassets, manuell nachgebaute Vorlagen und ungeklärte Trainingsadapter bleiben ausgeschlossen.

## Entscheidungskriterien

- maximal automatisierbare Produktion mit reproduzierbaren Jobs
- vollständig synthetischer oder eigener Ursprung der kreativen Inhalte
- unabhängige konkrete Welt-, Form-, Material-, Farb-, Audio- und UI-Entscheidungen
- getrennte technische, visuelle, Originalitäts- und Lizenzprüfung
- Hardwarebudgets werden bereits beim Erzeugen und Cooken erzwungen
- Generatoren, Modelle und DCC-Werkzeuge bleiben austauschbar

## Betrachtete Optionen

### Option A: Fremdassets als Ausgangspunkt und KI-Restilisierung

- Vorteile: schnelle frühe Ergebnisse
- Nachteile: Herkunfts-, Ähnlichkeits- und Lizenzrisiko; schlechte Reproduzierbarkeit
- Risiko: bloßer Oberflächentausch statt eigenständiger Gestaltung

### Option B: Gemischte Hand- und KI-Produktion

- Vorteile: klassische künstlerische Kontrolle
- Nachteile: entspricht nicht dem gewünschten autonomen Produktionsmodell
- Risiko: menschliche Assetarbeit wird zum dauerhaften Engpass

### Option C: Spezifikationsgetriebene synthetische Produktion mit agentischer DCC-Verarbeitung

- Vorteile: klare Provenienz, Automatisierbarkeit, Messbarkeit und stilistische Skalierung
- Nachteile: benötigt starke Validatoren, Modellzulassung und mehrere Iterationen
- Risiko: technisch valide, aber inkonsistente oder generische Ergebnisse

## Entscheidung

Wir verwenden **Option C**.

- 100 % der kreativen Shipping-Assets entstehen aus freigegebenen internen Spezifikationen durch KI- oder agentisch gesteuerte prozedurale Synthese.
- Produktionsprompts enthalten nur unsere eigenen Bibles und keine fremden Werks-, Figuren-, Fraktions-, Künstler- oder Soundtracknamen.
- Jeder Rohoutput beginnt in Quarantäne. Nur Assets mit gültigem Manifest, geklärtem Generator-/Inputrecht, technischen Budgets und getrennten Reviews dürfen in `assets/source/` beziehungsweise Shipping-Pakete wechseln.
- Erzeugeragenten dürfen technische Varianten selbst iterieren, aber keine abschließende Originalitäts- oder Lizenzfreigabe für den eigenen Output erteilen.
- Die Projektleitung testet integrierte Builds und gibt Feedback. Sie wird nur bei Produktentscheidungen, unklaren Rechten, zweifelhafter Ähnlichkeit oder Meilensteinfreigaben benötigt.
- Ein Generator ist kein dauerhafter Architekturbaustein. Jobs verwenden einen versionsunabhängigen Assetvertrag; konkrete Modelle werden erst nach Eval und Lizenzprüfung in `models.lock.json` zugelassen.

## Folgen

- Positiv: Der gewünschte Arbeitsmodus ist eindeutig; Assetjobs, Runs und Reviews lassen sich vollautomatisch vorbereiten.
- Negativ: Modell- und Outputbedingungen können nicht aus „KI-generiert“ abgeleitet werden; sie bleiben je Generator separat zu prüfen.
- Folgemaßnahmen: T-002 abschließen, T-003 als ausführbares Quarantäne-/Provenienzgate implementieren, danach einen Generator je Assetklasse evaluieren und T-050 an einer kleinen zusammengehörigen Familie messen.
- Zeitpunkt für erneute Prüfung: nach T-050 anhand von Akzeptanzrate, Iterationskosten, visueller Konsistenz und Hardwarebudget.
