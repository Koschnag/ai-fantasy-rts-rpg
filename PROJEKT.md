# Projektsteckbrief

> Dieses Dokument ist die fachliche Quelle der Wahrheit. Ungeklärte Angaben bleiben ausdrücklich `OFFEN`.

## 1. Kurzbeschreibung

**Status:** ANGENOMMEN

Project Riftward ist ein eigenständiges Einzelspieler-Spiel, das Rollenspielhelden, Erkundung und Quests mit Basisbau, Wirtschaft und Echtzeit-Armeeführung verbindet. Es soll eine melancholische, geheimnisvolle High-Fantasy-Atmosphäre und eine moderne, gut lesbare 3D-Darstellung bieten, ohne bestehende Figuren, Namen, Texte, Karten, Musik, Grafiken oder andere geschützte Inhalte zu übernehmen.

## 2. Ausgangsproblem

**Status:** ANGENOMMEN

- Moderne Spiele trennen häufig stark zwischen Rollenspiel und Strategie oder benötigen deutlich neuere Hardware.
- Gesucht ist die besondere Spannung zwischen einer kleinen, persönlich entwickelten Heldengruppe und dem Aufbau einer größeren Streitmacht.
- Die technische Produktion soll weitgehend autonom durch KI erfolgen und dennoch reproduzierbar, testbar, performant und rechtlich nachvollziehbar bleiben.
- Ein eigener schlanker Unterbau erlaubt feste Leistungsbudgets und vermeidet den Ballast einer großen Engine.

## 3. Zielgruppe

**Status:** ANGENOMMEN

| Persona / Rolle | Bedarf | Kontext | Technische Erfahrung |
|---|---|---|---|
| Kernspieler | Mag erzählerische Fantasy, Charakterentwicklung und klassische Echtzeitstrategie | Einzelspieler am Desktop, Sessions von 30–120 Minuten | mittel |
| Strategie-Spieler | Erwartet lesbare Schlachten, klare Wirtschaft und direkte Befehle | Maus/Tastatur, 1080p | hoch |
| RPG-Spieler | Erwartet Erkundung, Dialoge, Ausrüstung, Fähigkeiten und Konsequenzen | kampagnenorientiert | mittel |

## 4. Nutzenversprechen

**Status:** ANGENOMMEN

> Für Spieler klassischer Fantasy-RTS/RPG-Hybride bietet Project Riftward eine zusammenhängende Kampagne, in der persönliche Heldenreisen und der Aufbau von Siedlungen zwei Maßstäbe derselben Welt sind. Es verbindet bewusstes, gut lesbares Gameplay mit einer eigenständigen Atmosphäre und läuft auch auf älterer Desktop-Hardware.

## 5. Ziele und messbarer Erfolg

| ID | Ziel | Messgröße | Zielwert / Zeitpunkt | Status |
|---|---|---|---|---|
| Z-001 | Der RTS/RPG-Kern funktioniert als zusammenhängender Spielablauf | Vertical-Slice-Abnahme | alle Muss-Flows in einer 20–30-minütigen Sitzung | ANGENOMMEN |
| Z-002 | Zielhardware liefert mindestens eine stabile Full-HD-Darstellung | automatisierte Benchmarks und Frame-Telemetrie | 30 FPS Minimum, 60 FPS bevorzugt; siehe `docs/PERFORMANCE_BUDGET.md` | ENTSCHIEDEN |
| Z-003 | Windows, Linux und macOS bleiben lieferbar | Build- und Smoke-Test-Matrix | grüne Artefakte für alle Ziel-RIDs | ANGENOMMEN |
| Z-004 | KI-Produktion bleibt kontrollierbar | Nachverfolgbarkeit und Qualitätsgates | jeder Build verweist auf Anforderungen, Tests und Asset-Provenienz | ANGENOMMEN |
| Z-005 | Eigenständige kreative Identität | Review gegen Art Bible und Provenienzregeln | keine im dokumentierten Review festgestellte unautorisierte Übernahme; vollständige Provenienz | ANGENOMMEN |

## 6. MVP-Umfang

### Enthalten

- zunächst ein vollständiger Vertical Slice gemäß `docs/GAME_DESIGN.md`
- Einzelspieler mit eigenständiger Welt und Geschichte
- Heldensteuerung, Fähigkeiten, Ausrüstung, Dialoge und Quests
- Basisbau, Ressourcenwirtschaft, Einheitenproduktion und Echtzeitkampf
- Speichern/Laden, Einstellungen und barrierearme Eingabeoptionen
- eigene 3D-Runtime, Content-Pipeline und automatisierte Tests
- Originalassets aus kontrollierten KI-/prozeduralen Pipelines

### Ausdrücklich nicht enthalten

- Übernahme oder Rekonstruktion fremder Namen, Figuren, Texte, Karten, Modelle, Texturen, Musik, Stimmen, Benutzeroberflächen oder Markenkennzeichen
- Multiplayer im Vertical Slice
- Modding-SDK im Vertical Slice
- Raytracing, Echtzeit-Globalbeleuchtung oder andere Effekte, die der Zielhardware widersprechen
- eine allgemeine, für beliebige Spiele gedachte Engine
- Abhängigkeit von einem Cloud-Dienst zur Laufzeit

### Mögliche spätere Ausbaustufen

- vollständige Kampagne mit mehreren Regionen und spielbaren Kulturen
- Koop oder kompetitiver Mehrspielermodus nach stabiler deterministischer Simulation
- Modding-Werkzeuge und Kampagneneditor
- höhere Ausgabeauflösung und Framerate-Reserve auf schnellerer Hardware, ohne separaten Effektpfad oberhalb der RX-580-High-Stufe

## 7. Randbedingungen

| Bereich | Vorgabe oder Einschränkung | Status |
|---|---|---|
| Plattform | Windows x64, Linux x64, macOS arm64; macOS x64 wird im Toolchain-Spike geprüft | ANGENOMMEN |
| Zeitrahmen | Vertical Slice zuerst; Vollproduktion erst nach dessen Abnahme | ENTSCHIEDEN |
| Budget / Betriebskosten | FOSS-first; kostenpflichtige Dienste nur nach expliziter Entscheidung | ENTSCHIEDEN |
| Projektöffnung | Spielcode, Produktionswerkzeuge und Forschungsartefakte werden als FOSS-Projekt entwickelt; konkrete SPDX-Lizenz je Artefaktklasse wird vor öffentlicher Lizenzfreigabe entschieden | ENTSCHIEDEN / Lizenzwahl OFFEN |
| Datenschutz / Datenstandort | keine Onlinepflicht im Spiel; Produktionsdienste führen keine geheimen oder fremden Daten | ANGENOMMEN |
| Bestehende Systeme / Schnittstellen | keine Runtime-Cloudabhängigkeit | ANGENOMMEN |
| Technologie | .NET 10 LTS, C# + F#, Native AOT für Release evaluieren, kleine native FOSS-Bibliotheken | ENTSCHIEDEN |
| Barrierefreiheit | frei belegbare Eingaben, skalierbare UI, Untertitel, keine reine Farbcodierung | ANGENOMMEN |
| Sprachen / Regionen | Architektur für Lokalisierung; Vertical Slice zunächst Deutsch und Englisch | ANGENOMMEN |

## 8. Risiken und Annahmen

| ID | Typ | Beschreibung | Auswirkung | Gegenmaßnahme / Validierung | Status |
|---|---|---|---|---|---|
| R-001 | Risiko | Umfang eines vollständigen RTS/RPG und eigener Technikunterbau | sehr hoch | Vertical Slice, harte Budgets, kleine vertikale Lieferungen | AKTIV |
| R-002 | Risiko | KI-generierte 3D-Assets sind stilistisch oder technisch inkonsistent | hoch | Art Bible, Provenienz, automatische Geometrie-/Textur-Gates, kuratierte Seeds | AKTIV |
| R-003 | Risiko | Plattform- oder Native-AOT-Probleme werden zu spät entdeckt | hoch | leerer plattformweiter Smoke-Build als erster Technikmeilenstein | AKTIV |
| R-004 | Risiko | Ähnlichkeit mit einer Vorlage überschreitet Inspiration | kritisch | eigenständige Welt, Negativliste, regelmäßiges IP-Review | AKTIV |
| R-005 | Risiko | modernes Grafikziel und Low-End-/Unified-Memory-Budget kollidieren | hoch | stilisierte Grafik, baked lighting, LOD/Instancing, Benchmarks auf GTX-660- und M1-Klasse ab Beginn | AKTIV |
| A-001 | Annahme | Einzelspieler hat Vorrang vor Multiplayer | starke Architekturvereinfachung | vor Produktionsphase bestätigen | ANGENOMMEN |
| A-002 | Annahme | GTX 660 mit 2 GB und M1 mit 8 GB bilden unterschiedliche Speichermodelle ab | bestimmt Assetbudgets | auf beiden Klassen messen | ANGENOMMEN |

## 9. Begriffe

| Begriff | Verbindliche Bedeutung |
|---|---|
| Vertical Slice | Kurzer, finalitätsnaher Ausschnitt, der alle zentralen Systeme integriert und auf Zielhardware läuft |
| Runtime | Der ausgelieferte schlanke Spielkern, nicht eine allgemeine Game Engine |
| Cooken | Rohassets validieren, optimieren und in ein laufzeiteffizientes Format überführen |
| Eigenständiger Genre-Hybrid | Neues Werk aus abstrakten RTS-/RPG-Mechaniken und eigenen kreativen Entscheidungen, ohne ein bestimmtes Fremdwerk zu rekonstruieren |
