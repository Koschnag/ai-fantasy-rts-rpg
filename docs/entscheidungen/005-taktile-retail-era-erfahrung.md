# ADR 005: Taktile Retail-Era-Erfahrung

- **Status:** akzeptiert
- **Datum:** 2026-08-23
- **Entscheidungsverantwortung:** Projektleitung
- **Bezug:** Z-004, Z-005, T-008; `ART_DIRECTION.md`, `ATMOSPHAERE.md`, `CLEAN_ROOM.md`

## Kontext

Das FOSS-Spiel und sein Forschungsauftritt sollen nicht nur eine moderne
Produktseite bilden. Gewünscht ist die Erinnerung an die Vorfreude einer PC-
Veröffentlichung der frühen Digitalära: eine große Schachtel, ein sichtbarer
Datenträger, ein tatsächlich lesenswertes Handbuch, magazinartige Anzeigen,
beschriftete Bildstrecken und das Gefühl, eine Welt schon vor dem ersten Start
zu betreten. Diese Artefakte werden vollständig digital inszeniert; eine
physische Ausgabe, Herstellung oder Versand sind nicht Teil der Entscheidung.

Diese Erinnerung wird als abstrakte Erlebnisqualität verwendet. Fremde Logos,
konkrete Verpackungen, Anzeigen, Handbücher, Screenshots, Layouts, Figuren und
andere wiedererkennbare Ausdrucksformen bleiben nach `CLEAN_ROOM.md` aus dem
Produktionskontext ausgeschlossen.

## Entscheidung

Riftward erhält eine eigenständige **digitale Retail-Era-Grammatik**:

- simulierte Artefakte: Schuber oder große Box, Keep Case, optischer
  Datenträger, Handbuch, Kartenblatt und Magazinseite als digitale Ansichten;
- redaktionelle Ebenen: große Kapitelziffern, Randnotizen, technische
  Bildunterschriften, klare Feature-Hierarchie und ehrliche Statusstempel;
- Materialität: dunkles ungestrichenes Papier, matte Tinte, feine Rasterpunkte,
  Kantenabrieb und zurückhaltende Metallfolie nur als visuelle Simulation;
- Farbe: erdige Grundflächen, kühles Schieferblau und begrenztes warmes Amber;
- Typografie: robuste Serifenschrift für Welt und Erzählung, schmale
  Groteskschrift für Technik, Daten und Beschriftung;
- Spannung: ein ruhiges Hauptmotiv, kleine dichte Detailflächen und viel
  kontrollierter Leerraum statt einer heutigen Kachelwand.

Der öffentliche Forschungsauftritt verwendet zunächst deterministische
Projekt-SVGs und CSS. KI-Rasterbilder bleiben Quarantäne, bis Nutzungsgrundlage,
Originalität und Veröffentlichung getrennt freigegeben sind. Konzeptmaterial
trägt sichtbar `CONCEPT – NOT GAMEPLAY`.

## Nicht übernommen

- keine Markenlogos, Publisher-Intros, Packungsmaße oder Trade-Dress-Kopie;
- keine nachgebauten Anzeigen, Datenträgerlabels, Handbuchseiten oder UI-Rahmen;
- keine künstlichen Testzitate, Awards, Altersfreigaben oder
  Systemanforderungsbehauptungen;
- keine vorgetäuschten Gameplay-Screenshots;
- kein Glanzlack-, Neon- oder Mobile-Store-Look als visuelle Grundsprache.

## Folgen

- Der Showcase kann schon in der Vorproduktion Stimmung und Forschungsfrage
  transportieren, ohne den Produktstatus zu übertreiben.
- Echte Runtime-Bilder ersetzen die Konzeptfelder später nur über
  commitgebundene Evidence Packs.
- Druck-, Video- und Webexport teilen ein semantisches Manifest, bleiben aber
  technisch austauschbare Renderer.
- Das Projekt bleibt FOSS und forschungsorientiert. Die konkrete SPDX-Lizenz
  des Codes sowie geeignete offene Lizenzen für Dokumentation und Medien
  bleiben getrennt zu entscheiden; bis dahin wird keine Lizenz erfunden.
- Eine physische Ausgabe ist nicht geplant. Sollte sie später doch erwogen
  werden, wäre das eine neue Produktentscheidung.
