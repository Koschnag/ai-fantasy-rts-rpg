# IP-, FOSS- und Lizenzpolitik

## Grundsatz

Project Riftward ist ein eigenständiges Werk. Abstrakte Genreideen und gewünschte Empfindungen dürfen als Inspiration dienen; konkrete Ausdrucksformen fremder Spiele werden nicht übernommen oder rekonstruiert.

Ein privates Repository, ein eigener Dateiname oder ein KI-Generator schafft keine Nutzungsrechte an fremden Inhalten. Die folgenden Regeln reduzieren technische und kreative Übernahmerisiken, sind aber keine Rechtsgarantie. Vor öffentlicher Ankündigung, Titelwahl oder kommerzieller Veröffentlichung ist eine qualifizierte IP-/Markenprüfung vorgesehen.

## Clean-Room-artige Produktionsgrenze

Die verbindliche Ausführung steht in `CLEAN_ROOM.md`. Kurzfassung:

- Recherche und Produktion sind getrennte Rollen und Kontexte.
- Eine Recherche darf höchstens bereinigte, abstrakte Mechanik-, Qualitäts- und Emotionsanforderungen liefern.
- Produktionsagenten erhalten keine fremden Handbücher, Screenshots, Videos, Audio-, Karten-, Modell-, UI-, Quellcode-, Objektcode- oder Spieldateien.
- Fremdspiel-, Franchise-, Figuren-, Fraktions-, Künstler- und Soundtracknamen sind in Produktionsprompts, Negativprompts, Branches, Issues und Commitnachrichten unzulässig.
- Keine Decompilation, Extraktion, Nachmodellierung, Ablauf-für-Ablauf-Rekonstruktion oder Eins-zu-eins-Zuordnung mit umbenannten Elementen.
- Auch die individuelle Auswahl, Reihenfolge und Kombination aus Handlung, Kartenaufbau, UI, Figuren, Fraktionen, Musik und visuellen Motiven wird unabhängig gestaltet.
- Zweifelhafter Output bleibt in Quarantäne und wird verworfen oder substanziell neu entworfen.

## FOSS-first für Software

Eine neue Abhängigkeit benötigt vor Aufnahme:

| Feld | Pflicht |
|---|---|
| Name, Upstream und exakte Version / Commit | ja |
| SPDX-Lizenz und Lizenztext | ja |
| Zweck und verwendete Oberfläche | ja |
| transitive native Abhängigkeiten | ja |
| unterstützte Zielplattformen | ja |
| Native-AOT-/Trimming-Folgen | bei .NET-Code |
| Alternative und Austauschkosten | ja |
| bekannte Sicherheits- oder Wartungsrisiken | ja |

Bevorzugt werden kleine Komponenten unter MIT, BSD, Apache-2.0 oder zlib. Copyleft ist nicht pauschal ausgeschlossen, muss aber vor Verbindung oder Auslieferung bewusst auf die gewünschte Projektlizenz abgestimmt werden.

## Projektlizenz

**Status:** OFFEN

„FOSS-first“ beschreibt derzeit die Auswahl der Werkzeuge und Komponenten. Ob der eigene Spielcode, die Werkzeuge und/oder die Assets öffentlich lizenziert werden, ist eine separate Produktentscheidung. Bis dahin darf keine Datei mit einer erfundenen Lizenzbehauptung veröffentlicht werden.

## Asset-Provenienz

Zulässige Quellen:

- selbst erzeugte KI-/prozedurale Ergebnisse mit dokumentierter, für das Projekt geeigneter Nutzungserlaubnis
- eigene Bearbeitung und eigene Aufnahmen
- ausdrücklich angenommene FOSS-/CC0-Produktionshilfen mit Provenienz

Nicht zulässig im Shipping-Pfad:

- extrahierte oder nachgebaute Assets bestehender Spiele
- unbekannte Downloadquellen
- Modelle oder Dienste ohne geklärte kommerzielle Nutzung und Outputbedingungen
- Referenzen, deren Lizenz die beabsichtigte Nutzung nicht erlaubt
- Prompts, die gezielt konkrete geschützte Figuren, Logos, Stimmen oder Werke reproduzieren sollen
- LoRAs, Adapter oder Referenzsammlungen, die auf ein bestimmtes Franchise, Werk oder einen lebenden Künstler zugeschnitten sind

## Provenienzregister

Das spätere maschinenlesbare Register enthält mindestens:

| Feld | Beispiel |
|---|---|
| Asset-ID | `ENV-RUIN-ARCH-001` |
| Erzeuger | Modell / Blender-Pipeline / eigene Aufnahme |
| Version und Seed | reproduzierbare Angaben |
| Eingaben | IDs, Hashes, Lizenzen |
| Bearbeitung | Schritte und Werkzeuge |
| Lizenz / Nutzungsgrundlage | SPDX oder dokumentierte Bedingungen |
| Reviewstatus | Quarantäne / technisch geprüft / visuell geprüft / freigegeben |

Ein Manifest mit Status `approved` benötigt nach Schema eine bestätigte kommerzielle Nutzungsprüfung sowie bestandene technische, visuelle, Performance-, Originalitäts- und Lizenzreviews. Bei einem KI-Generator sind Modell, konkrete Modellversion und Hash des Modellartefakts Pflicht, soweit das Modellartefakt selbst zugänglich ist; nicht vollständig identifizierbare Remote-Modelle dürfen nicht als reproduzierbar bezeichnet werden und benötigen eine eigene Freigabeentscheidung.

Ein Lizenzbezeichner allein genügt bei Eingabematerial nicht. Das Manifest benötigt einen prüfbaren Rechtebeleg, die erlaubte Nutzungsrolle, eine bewusste Referenzfreigabe und den Hash der bereinigten internen Asset-Spezifikation. Menschliche Auswahl, Überarbeitung und kreative Entscheidungen werden als Produktionsschritte dokumentiert, ohne unnötige personenbezogene Daten zu speichern.

## Namens- und Ähnlichkeitsprüfung

Vor öffentlicher Benennung oder Veröffentlichung werden Titel, Logos, Figuren, Fraktionen und zentrale Designs gesondert geprüft. Der aktuelle Name `Project Riftward` ist nur ein interner Arbeitstitel.
