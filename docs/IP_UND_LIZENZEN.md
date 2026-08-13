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

### Aufgenommener JSON-Schema-Validator

Für das offline laufende `assets-check` ist `JsonSchema.Net` exakt in Version
8.0.5 aufgenommen. Das NuGet-Paket und seine gelockten Transitiven
`JsonPointer.Net` 6.0.1, `Json.More.Net` 2.2.0 und `Humanizer.Core` 3.0.1 sind
als MIT ausgewiesen; sie enthalten keine native Komponente. Paketversionen und
Content-Hashes stehen in den jeweiligen `packages.lock.json`-Dateien,
Attribution und Lizenztext in `THIRD_PARTY_NOTICES.md`.

Die verwendete Oberfläche ist auf lokale Draft-2020-12-Schemaauswertung hinter
einem Adapter begrenzt. Alle sicherheits- und projektspezifischen
Querfeldprüfungen bleiben davon unabhängig. Die Bibliotheken laufen nur im
F#-Produktionsharness auf CoreCLR, werden nicht mit dem Spielclient ausgeliefert
und berühren dessen Native-AOT-/Trimmingvertrag nicht. Ein Austausch ist damit
technisch eingegrenzt; sämtliche Schema-Negativfixtures müssen danach erneut
abgenommen werden.

Die Majorlinie 9 wird nicht automatisch übernommen: Upstream hat für neuere
Binärpakete zusätzliche Nutzungsbedingungen angekündigt. Zulässig ist nur der
geprüfte 8.0.5-Paketstand, bis eine neue bewusste Lizenzentscheidung oder ein
Wechsel des Validators erfolgt. Das bewusst alte Major erhöht umgekehrt das
Wartungsrisiko; Locked Restore, NuGet Audit und ein Verbot automatischer
Major-Upgrades sind deshalb Teil des Vertrags.

## Projektlizenz

**Status:** OFFEN

„FOSS-first“ beschreibt derzeit die Auswahl der Werkzeuge und Komponenten. Ob der eigene Spielcode, die Werkzeuge und/oder die Assets öffentlich lizenziert werden, ist eine separate Produktentscheidung. Bis dahin darf keine Datei mit einer erfundenen Lizenzbehauptung veröffentlicht werden.

## Asset-Provenienz

Zulässiger kreativer Ursprung für Shipping-Assets:

- selbst erzeugte KI-Ergebnisse mit dokumentierter, für das Projekt geeigneter Nutzungserlaubnis
- agentisch erzeugte prozedurale Ergebnisse aus versioniertem Projektcode und freigegebenen internen Spezifikationen

Eigene Bearbeitung sowie technische FOSS-/CC0-Hilfen dürfen Ergebnisse transformieren oder vermessen, sind aber gemäß ADR 004 kein kreativer Ursprung eines freigegebenen Assets. Eigene Aufnahmen und externe Medien dürfen für diese Produktionslinie nicht als kreative Referenz- oder Generierungseingabe dienen. Nicht-ausdruckshafte technische Kalibrierungsdaten benötigen eine ausdrücklich begrenzte Rolle und Provenienz; sie dürfen keine Formen, Texturen, Stimmen, Melodien, Texte, Karten oder andere kreative Gestaltung vorgeben.

Nicht zulässig im Shipping-Pfad:

- extrahierte oder nachgebaute Assets bestehender Spiele
- unbekannte Downloadquellen
- Modelle oder Dienste ohne geklärte kommerzielle Nutzung und Outputbedingungen
- Referenzen, deren Lizenz die beabsichtigte Nutzung nicht erlaubt
- Prompts, die gezielt konkrete geschützte Figuren, Logos, Stimmen oder Werke reproduzieren sollen
- LoRAs, Adapter oder Referenzsammlungen, die auf ein bestimmtes Franchise, Werk oder einen lebenden Künstler zugeschnitten sind

## Provenienzregister

Das maschinenlesbare Register enthält mindestens:

| Feld | Beispiel |
|---|---|
| Asset-ID | `ENV-RUIN-ARCH-001` |
| Erzeuger | zugelassenes KI-Modell / agentisch erzeugte prozedurale Pipeline |
| Version und Seed | reproduzierbare Angaben |
| Eingaben | IDs, Hashes, Lizenzen |
| Bearbeitung | Schritte und Werkzeuge |
| Lizenz / Nutzungsgrundlage | SPDX oder dokumentierte Bedingungen |
| Reviewstatus | Quarantäne / technisch geprüft / visuell geprüft / freigegeben |

Ein Manifest mit Status `approved` benötigt nach Schema eine bestätigte kommerzielle Nutzungsprüfung sowie bestandene technische, visuelle, Performance-, Originalitäts- und Lizenzreviews. Bei einem KI-Generator sind Modell, konkrete Modellversion und Hash des Modellartefakts Pflicht, soweit das Modellartefakt selbst zugänglich ist; nicht vollständig identifizierbare Remote-Modelle dürfen nicht als reproduzierbar bezeichnet werden und benötigen eine eigene Freigabeentscheidung.

Ein Lizenzbezeichner allein genügt bei Eingabematerial nicht. Das Manifest benötigt einen prüfbaren Rechtebeleg, die erlaubte Nutzungsrolle, eine bewusste Referenzfreigabe und den Hash der bereinigten internen Asset-Spezifikation. Menschliche Auswahl, Überarbeitung und kreative Entscheidungen werden als Produktionsschritte dokumentiert, ohne unnötige personenbezogene Daten zu speichern.

## Namens- und Ähnlichkeitsprüfung

Vor öffentlicher Benennung oder Veröffentlichung werden Titel, Logos, Figuren, Fraktionen und zentrale Designs gesondert geprüft. Der aktuelle Name `Project Riftward` ist nur ein interner Arbeitstitel.

## KI-Output: drei getrennte Prüfungen

Für jeden KI-Output werden drei Fragen getrennt dokumentiert:

1. **Nutzungsbefugnis:** Erlauben die zum Generierungszeitpunkt archivierten Modell-, Gewichts-, Dienst- und Outputbedingungen die geplante Nutzung?
2. **Rechte Dritter:** Wurde im dokumentierten fachlichen Review keine unautorisierte Übernahme erkannt? Anbieterbedingungen oder ein bestandener Namensscan garantieren dies nicht.
3. **Eigene Schutzfähigkeit:** Welche nachweisbaren menschlichen freien und kreativen Entscheidungen prägen das Endergebnis, beziehungsweise wird ausdrücklich keine Exklusivität behauptet? Solche Beiträge werden nicht nachträglich erfunden.

„KI-generiert“, „kommerziell nutzbar“ und „urheberrechtlich exklusiv geschützt“ sind keine Synonyme. Der Validator darf nur die vorhandene Evidenz und festgestellte Policyverstöße melden; er erteilt keine Rechtsfreigabe.
