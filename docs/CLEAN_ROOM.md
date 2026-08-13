# Clean-Room-Produktionsregeln

## Zweck und Grenze

Dieses Projekt entwickelt einen eigenständigen Fantasy-RTS/RPG-Hybrid. Es darf abstrakte Ideen wie Heldengruppensteuerung, Erkundung, Quests, Basisbau, Ressourcenwirtschaft und Echtzeit-Armeeführung kombinieren. Es rekonstruiert kein bestimmtes Spiel und übernimmt keine konkrete fremde Ausdrucksform.

Diese Regeln reduzieren Urheberrechts-, Marken-, Herkunfts- und Ähnlichkeitsrisiken. Sie sind keine Rechtsgarantie. Ein privates Repository ändert die Rechte an fremden Inhalten nicht. Vor öffentlicher Benennung oder kommerziellem Release bleiben eine formelle Titel-/Markenrecherche und bei Bedarf anwaltliche Prüfung erforderlich.

Arbeitsgrundlage ist die Trennung zwischen abstrakter Idee/Funktion und konkreter Ausdrucksform: § 69a Abs. 2 UrhG schließt die einem Programmelement zugrunde liegenden Ideen und Grundsätze vom Programmschutz aus, während konkrete Ausdrucksformen und die einzelnen kreativen Spielelemente geschützt sein können. Maßgeblich bleiben der [amtliche § 69a UrhG](https://www.gesetze-im-internet.de/urhg/__69a.html) und der Überblick des [European IP Helpdesk zu IP in Videospielen](https://intellectual-property-helpdesk.ec.europa.eu/regional-helpdesks/european-ip-helpdesk/europe-ip-specials/europe-ip-specials-ip-videogames-industry_en). Diese technische Policy ersetzt keine Rechtsberatung.

## Getrennte Rollen

### Recherche

Eine ausdrücklich beauftragte Recherche darf öffentlich zugängliche, rechtmäßig verwendete Quellen untersuchen, um ausschließlich abstrakte Ergebnisse festzuhalten:

- Genre- und Mechanikkategorien
- beobachtbare Qualitätsmerkmale
- technische oder ergonomische Anforderungen
- allgemeine emotionale Ziele
- bekannte Fehlerklassen und Testideen

Der bereinigte Bericht enthält keine fremden Namen, Screenshots, Karten, Dialoge, Musikmotive, konkreten Zahlenfolgen, Ablaufskripte oder nachbaubaren Detailbeschreibungen. Externe Vergleichsquellen werden nicht in das Produktions-RAG aufgenommen.

### Produktion

Implementierungs-, Welt-, Quest-, UI-, Audio- und Assetagenten arbeiten nur aus `PROJEKT.md`, bestätigten ADRs, den internen Bibles, Anforderungen und freigegebenen Spezifikationen. Neue Produktionsläufe beginnen ohne alten Fremdwerk-Kontext.

## Verbotene Produktionsinputs

- fremde Handbücher, Strategieführer, Wiki-Dumps oder Dialog-/Questtexte
- Screenshots, Videos, Konzeptbilder, Karten, UI-Aufnahmen oder Logos fremder Spiele
- extrahierte Modelle, Texturen, Animationen, Audio-, Musik-, Sprach- oder Spieldateien
- fremder Quell-/Objektcode, Decompilationsergebnisse oder rekonstruierte proprietäre Datenformate
- Franchise-, Spiel-, Figuren-, Fraktions-, Künstler- oder Soundtracknamen in Prompts und Negativprompts
- „style of“, Bild-zu-Bild, Control-, Adapter-/LoRA- oder Audio-Referenzen ohne einzeln dokumentierte eigene beziehungsweise freigegebene Herkunft
- Eins-zu-eins-Tabellen, bei denen fremde Figuren, Fraktionen, Einheiten, Ressourcen, Kartenabschnitte, UI-Flows oder Handlungsschritte lediglich umbenannt werden

Die Regeln gelten auch für Dateinamen, Metadaten, Branches, Issues, Commitnachrichten, Harness-Payloads und generierte Zwischentexte.

## Unabhängige Gestaltung

Jede produktive Spezifikation begründet ihre konkrete Ausprägung aus eigenen Weltregeln, Spielerzielen und Hardwarebudgets. Mindestens folgende Bereiche erhalten unabhängige Entscheidungen und Negativlisten:

- Weltprämisse, Geschichte, Figuren und Dialogstimme
- Kulturen, Einheiten, Gebäude, Ressourcen und Fähigkeiten
- Kartenstruktur, Missionsabfolge und Questfolgen
- UI-Komposition, Icons, Terminologie und Eingabefluss
- Silhouetten, Materialien, Farb- und Lichtsystem
- Musik, Harmonik, Motive, Instrumentierung und Sounddesign

Ein bloßer Namens-, Farb- oder Oberflächentausch gilt nicht als eigenständige Gestaltung.

## Provenienz- und Reviewpflicht

1. Jeder Generatorinput besitzt Herkunft, Hash, Rechtebeleg und erlaubte Nutzungsrolle.
2. Jeder Job verweist auf eine bereinigte interne Spezifikation per SHA-256.
3. Rohoutput beginnt in `assets/quarantine/` und gelangt nie direkt ins Shipping-Paket.
4. Technisches, visuelles, Performance-, Lizenz- und Originalitätsreview werden getrennt protokolliert.
5. Der Erzeugeragent darf das abschließende Originalitäts-/Lizenzreview nicht selbst freigeben.
6. Bei spontaner Zuordnung zu einem konkreten Fremdwerk wird das Ergebnis verworfen oder substanziell neu gestaltet; ein hoher Qualitätswert hebt dies nicht auf.
7. Menschliche Auswahl, Überarbeitung und kreative Entscheidungen werden als Transformationen/Evidenz dokumentiert.

Bis `T-003` das ausführbare Provenienz-/Prompt-Gate implementiert, darf kein KI- oder prozedural erzeugtes Asset den Status `approved` beziehungsweise shipping-fähig erhalten.

## Stop-Regel

Ein Agent stoppt, wenn ein Auftrag fremde Medien, namentliche Stilabkürzungen, Extraktion, Decompilation oder detailgetreue Rekonstruktion verlangt. Er darf nur mit einer bereinigten abstrakten Anforderung weiterarbeiten. Unsicherheit führt zur Quarantäne und zu einem unabhängigen Review, nicht zu stiller Freigabe.
