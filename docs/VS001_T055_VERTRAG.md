# VS-001 / T-055 – Vertrag fuer die 20–30-Minuten-Vertikalscheibe

**Status:** DRAFT – keine Implementierungsfreigabe

Dieses Dokument ist der fachliche Vertrag fuer die naechste Slice-Kette. T-055
implementiert nichts und ersetzt weder `docs/GAME_DESIGN.md` noch die
Abnahmebedingungen der bestehenden Tasks. Es dient als Reviewgrundlage fuer
die Projektleitung und als gemeinsame, reversible Klammer fuer T-056 bis
T-061.

## Ziel und Kettenplan

Die Kette soll eine erste vollstaendige, 20–30-minuetige Mission fuer
Owner-Feedback vorbereiten: zusammenhaengende, schoene und lesbare Assets,
echtes Spielgeschehen sowie einen belastbaren internen Kandidaten. Ihr
genehmigter Integrationsgraph lautet:

`T-055 -> {T-050, T-056, T-011-Vorbereitung; T-051 parallel} -> T-057 -> T-058 -> T-059 -> T-060 -> T-061`

Der Graph ist nicht insgesamt strikt seriell. Nach dem T-055-Review duerfen
Runtime-, Asset-, Plattform- und Black-box-Testvorbereitungen in getrennten,
konfliktarmen Worktrees parallel laufen. T-050, T-056 und die Vorbereitung von
T-011 bilden die erste parallele Stufe; T-051 darf als Content-Pipeline-Lane
parallel mitlaufen. Der zentrale Integrator bleibt der einzige Mutator seines
Integrationsbaums und uebernimmt gepruefte Commits einzeln. Erst dieser
single-mutator Integrationspfad verbindet die Slices ab T-057; ein vorbereiteter
Output ist weder integriert noch akzeptiert noch eine Mergefreigabe.

| Task | Zielzeit | Vertraglicher Zweck |
|---|---:|---|
| T-055 | – | Spezifikation, offene Entscheidungen, Mess- und Abnahmerahmen |
| T-050 / T-011-Vorbereitung / T-051 | parallel | Assetfamilie, Plattformvorbereitung und Content-Pipeline ohne vorzeitige Shipping-, Plattform- oder Integrationsbehauptung |
| T-056 | 5 min | Boot, Neues Spiel, Pause und Exit; Held plus zwei Begleiter; Same-world-Wechsel RTS ↔ Third-Person; integriertes Onboarding; eine minimale echte Faehigkeit im taktischen Kampf gegen genau einen Gegner; Basis-HUD, Audio und Untertitel; erster Checkpoint; offline startbares Paket |
| T-057 | 10 min | persoenlicher Loop mit Erkundung, einer Dialogkette, A/B-Entscheidung, taktischem Heldenkampf und sichtbarer persoenlich-strategischer Folge |
| T-058 | 15–20 min | Aufbau-/Wirtschaftsloop auf derselben Karte mit zwei Ressourcen, Schutzquartier und fuenf Gebaeuden beziehungsweise Gebaeudetypen |
| T-059 | 20–25 min | Armee-Loop mit vier ausbildbaren Einheitentypen, Auswahlgruppen, Formationen und kontextuellem taktischem Angriff |
| T-060 | 20–30 min | zusammenhaengender Owner-Feedback-Kandidat mit vier normalen Gegnertypen, Elite, Boss, gemeinsamem Helden-/Armeefinale sowie gespeichertem Abschlusszustand |
| T-061 | – | eingefrorener Release Candidate nach allen getrennten Vollgates und unabhaengiger Abnahme |

T-055 darf keine Tasks T-062 bis T-070 erzeugen. T-056–T-061 sind hier nur
benannte Folgeziele; ihre eigenen READY-Manifeste entstehen erst nach
Projektleitungsentscheidungen und dem jeweiligen Review.

## Unveraenderliche Mindestbasis

Die folgenden VS-001-Minima werden weder reduziert noch stillschweigend
kreativ konkretisiert:

- genau eine finalitaetsnahe Karte mit Erkundungs-, Basis- und Kampfzone
- eine Hauptfigur und zwei Begleiter; hoechstens vier Faehigkeiten je aktiver
  Figur
- genau eine Dialogkette und eine Aufgabe mit mindestens zwei sichtbaren
  Ausgaengen
- genau zwei Ressourcen und fuenf Gebaeude beziehungsweise Gebaeudetypen
- vier ausbildbare Einheitentypen
- vier normale Gegnertypen, eine Elitevariante und ein Boss
- ein echter taktischer Heldenkampf und ein gemeinsames Helden-/Armeefinale
- bidirektional nachvollziehbare Folgen (persoenlich ↔ strategisch)
- Fog of War, Minimap, Auswahlgruppen, Formationen und kontextuelle Befehle
- Wechsel zwischen strategischer RTS-Sicht und direkter Third-Person-Steuerung
  der Heldenfigur ohne Weltneustart: dieselbe autoritative Simulation, dieselben
  Akteuridentitaeten und derselbe Weltzustand
- Speichern, Laden, Checkpoints, Einstellungen und Pause-/Retry-Verhalten
- ein Abschlusszustand, in dem die gewaehlte Questoption und ihre sichtbare
  Folge gespeichert sind und nach Laden uebereinstimmen
- deutsche und englische Textstruktur, ohne eine bestimmte Uebersetzungsmenge
  vorwegzunehmen
- automatisierter Benchmark, deterministischer Gameplay-Smoke-Test und
  Offline-Lauffaehigkeit

Die abschliessende 20–30-Minuten-Sitzung muss Prolog, persoenlichen Loop,
Aufbau, Ausbildung und Angriff als einen nachvollziehbaren Spielerpfad
verbinden. Ein Konzeptbild, ein Assetmanifest, ein Healthcheck oder ein
Headless-Report allein zaehlt nicht als Gameplay- oder Owner-Abnahme.

## Traceability der akzeptierten Verhaltensweisen

Die Identitaet von Figuren, Orten, Ressourcen, Gebaeuden, Einheiten, Gegnern,
Faehigkeiten und Dialogtext bleibt offen. Die folgenden bereits akzeptierten
Verhaltensvertraege sind dagegen bindend:

| Sichtbares Verhalten | Anforderungen | Spielsystem | User Flow | Erster / abschliessender Slice |
|---|---|---|---|---|
| Held plus zwei Begleiter bewegen sich, nutzen eine echte Faehigkeit und bestehen taktischen Kampf | F-001 | GS-002 | UF-001 | T-056 / T-060 |
| Erkundung und genau eine Dialogkette fuehren zur A/B-Aufgabe; ihre Wirkung verlaeuft persoenlich → strategisch und strategisch → persoenlich | F-002, F-004 | GS-003 | UF-001 | T-057 / T-060 |
| Schutzquartier, zwei Ressourcen und fuenf Gebaeude beziehungsweise Gebaeudetypen bilden eine funktionsfaehige Basis | F-003 | GS-004 | UF-001 | T-058 / T-060 |
| Vier Einheitentypen werden ausgebildet und per Auswahlgruppen, Formationen und Kontextbefehlen gefuehrt | F-003 | GS-001, GS-005 | UF-001 | T-059 / T-060 |
| Vier normale Gegnertypen, Elite und Boss enden in einem gemeinsamen Helden-/Armeefinale | F-001, F-003, F-004 | GS-006 | UF-001 | T-060 |
| Fog of War und Minimap unterstuetzen Erkundung, Aufklaerung und taktische Befehle | F-001, F-003 | GS-001, GS-007 | UF-001 | T-057–T-060 |
| RTS- und Third-Person-Steuerung wechseln ohne Ladebildschirm oder Weltneustart ueber denselben Weltzustand | F-010 | GS-010 | UF-007 | T-056–T-060 |
| Checkpoint, Save/Load und Missionsabschluss erhalten Wahl, Gruppe, Weltfakten und Abschlusszustand | F-005 | GS-008, GS-009 | UF-001, UF-002 | T-056 / T-060–T-061 |
| Grafik, Audio, Sprache, Untertitel und Eingaben sind konfigurierbar und zugaenglich | F-006 | GS-001 | UF-003 | T-056-Basis / T-061-Vollgate |
| Asset-, Automations-, Plattform-, Performance- und Offlineevidenz bleibt getrennt pruefbar | F-007, F-008, F-009; NF-001–NF-008 | GS-001–GS-010 | UF-004, UF-005, UF-006 | parallele Vorbereitung / T-061 |

## Drei offene Entscheidungsbuendel der Projektleitung

T-055 macht die folgenden Buendel explizit und entscheidet sie nicht. Jede
Folgeaufgabe darf erst nach einer datierten Entscheidung oder einer
ausdruecklichen, widerrufbaren Zwischenfreigabe starten. Die nachstehenden
Empfehlungen sind **nicht bindende Defaults** und keine kreative Festlegung.

### A – Level, Geschichte und Spielregeln

Zu entscheiden sind Region/Ort, Hauptfigur und Begleiter, lokaler Konflikt,
Aufgabenfolge, A/B-Entscheidung mit beiden Richtungen ihrer Folgen,
Ressourcenbedeutung, fuenf Gebaeude beziehungsweise Gebaeudetypen, vier Einheitentypen,
Gegnerrollen, Elite/Boss, Druck-/Scheitern-/Checkpoint-Regeln sowie die
Pausen- und Moduswechselpraxis. Die Entscheidung muss eine eigene
Negativliste, sichtbare Konsequenzen und einen Rueckrollpunkt benennen.

Nicht bindender Arbeitsvorschlag: bewohnte Wanderbruch-Steinbruch- und
Schleusental-Landschaft; Kartografin/Feldingenieurin mit zwei komplementaeren
Begleitrollen; Schleuse gegen Tunnel; ein lokaler Interessenkonflikt als
Rivale/Boss. Keine Namen, Lore, Balancewerte oder moralische Aufloesung sind
damit beschlossen.

### B – Assetproduktion, Lizenzen und Budgets

Zu entscheiden sind zugelassene Produktionswege, Provenienz-/Lizenzklassen,
Source- und Cooking-Grenzen, Assetumfang je Folgeaufgabe, Kosten-/Zeit-/LFS-
Budget sowie die getrennten technischen, visuellen, Performance-,
Originalitaets- und Lizenzreviews. T-050 bleibt vor shipping-faehigen Assets
erforderlich; T-051 laeuft parallel fuer Karten-/Quest-/Audio-Provenienz.

Nicht bindender Arbeitsvorschlag: stilisiert-erdiger, malerisch grundierter
Low-Poly-Look mit starken Silhouetten, gebackenem Licht und 512–1024er
Texturbudget; projekt-eigene/prozedurale FOSS- und local-first-Pipeline; zu
Beginn keine Stimmen. Jedes erzeugte Ergebnis bleibt Quarantaene, bis die
bestehenden Provenienz- und Lizenzgates es freigeben.

### C – Abnahmehardware, Tester und Budget

Zu entscheiden sind reale Rechner/OS-/Treiberstaende fuer die
Minimumprofile, Testerzahl und Rollen, Owner-Feedbackformat, Messfenster,
Fehler- und Ruecklaufbudget sowie die Freigabeverantwortung. Schnelle oder
virtuelle Hardware ersetzt keinen Zielhardwarebeleg; Performanceclaims
bleiben bis zum reproduzierbaren Lauf `NOT-MEASURED`.

Nicht bindender Arbeitsvorschlag: Linux-x64-interne Pakete zuerst; harte
Pause, Checkpoint-Reload und deterministischer Retry; anschliessend
plattformgebundene Runs auf den benannten Profilen. Dies ist keine
Entscheidung gegen Windows/macOS und keine Releasefreigabe.

## Abnahme- und Evidenzrahmen fuer die Folgeaufgaben

Jede Folgeaufgabe fuehrt nur den kleinsten zu ihr gehoerenden Teilpfad aus,
bindet den exakten Ausgangstree und dokumentiert Tests, Screenshots/Clips
(falls autorisiert), Asset-Receipts und Rueckrollweg. Abnahme benoetigt:

1. deterministische Skript-/Smoke-Evidenz fuer den neu eingefuehrten Pfad;
2. interaktive Owner-Pruefung des sichtbaren Feedbacks, der Bedienung und
   der Folgen;
3. technische, Clean-Room-, Provenienz- und Lizenzpruefung gemaess
   `docs/CLEAN_ROOM.md`, `docs/ART_DIRECTION.md` und den nachgelagerten
   T-050-/T-051-Gates;
4. Performance-/Speicher-/Ladeevidenz auf der benannten Hardwareklasse;
5. unabhaengige Reviewidentitaet und ein revertierbarer Checkpoint.

Ein fehlender Tester, ein fehlendes Asset-Receipt oder ein fehlender realer
Hardwarelauf bleibt offen beziehungsweise blockiert; er wird nicht durch
Simulation, Konzeptmaterial oder einen gruene(n) CI-Lauf ersetzt.

### Vollgates des T-061 Frozen RC

T-061 darf nur einen exakt gebundenen T-060-Candidate einfrieren. Die folgenden
Nachweise sind kumulativ; `NICHT VERFUEGBAR`, ein schnellerer Rechner oder ein
Ersatznachweis gilt nicht als PASS:

| Gateklasse | Nachweis vor T-061 |
|---|---|
| Plattform | bestehende T-011-/Plattformmatrix mit nativen Smokes fuer alle Ziel-RIDs |
| Visualitaet und Atmosphaere | T-040 beziehungsweise gleichwertig gebundene visuelle Lesbarkeit, Atmosphaerenrubrik, Nachhallphase und Originalitaetsreview |
| Provenienz und Assets | T-050/T-051 sowie jedes Shipping-Asset technisch, visuell, performant, originalitaets- und lizenzgeprueft, hashgebunden und reproduzierbar gecookt |
| Settings und Zugaenglichkeit | T-041 mit Grafik-, Audio-, Sprach-, Untertitel-, UI-/Textskalierungs- und frei belegbarer Eingabeevidenz |
| Performance und Soak | alle Pflichtbenchmarks und der vertragliche Soak auf den benannten realen Minimum-Hardwareklassen; keine Performancebehauptung aus CI oder schnellerer Hardware |
| Paket und Offline | checksumgebundene Zielpakete, Start/Neues Spiel/Save/Load/Abschluss bei gesperrtem Netzwerk sowie dokumentierter Installations-, Neustart- und Rueckrollpfad |
| Owner und reale Hardware | interaktive Owner-Abnahme der vollstaendigen 20–30 Minuten und getrennte reproduzierbare Runs auf der real benannten Zielhardware; Screenshots, Headless-Smokes und Healthchecks ersetzen beides nicht |
| Integritaet | buildergetrenntes unabhaengiges Review, alle Pflichtgates und ein unveraenderter Candidate-Tree zwischen Messung und Freigabe |

## Abhaengigkeiten und Grenzen

Nur T-031 und T-039 sind Voraussetzungen dieses dokumentarischen
T-055-Vertrags. T-011, T-040, T-041, T-050 und T-051 sind keine
T-055-Dependencies, sondern nachgelagerte Vorbereitungs- oder RC-Gates. T-050
muss vor der Promotion shipping-faehiger Assets abgeschlossen sein; T-051 darf
parallel vorbereitet werden, ersetzt aber keine Assetfreigabe. T-011 darf in
der ersten Parallelstufe vorbereitet werden, seine vollstaendige Plattform-
evidenz gate-t T-061. T-040 und T-041 gate-n ebenfalls erst den Frozen RC.

Die abgenommenen T-031 bis T-039 bilden den Graybox-/Save-/Paket-Untergrund.
T-053 bleibt byteidentisch `READY` mit seiner vorregistrierten Wartebedingung;
T-054 bleibt unveraendert. Keine Aenderung an Runtime, Simulationskern,
bestehenden Vertraegen, T-053 oder T-054 wird aus diesem DRAFT abgeleitet.

## Rueckroll- und Eskalationsregel

T-055 ist rein dokumentarisch und kann als einzelner Commit vollstaendig
revertiert werden. Nach jeder Projektleitungsentscheidung wird die betroffene
Hypothese mit Datum, Verantwortlichem, Alternativen, Messkriterium und
Rueckrollweg ergaenzt. Konflikte zwischen kreativer Identitaet, Lizenz,
Hardwarebudget und Spielbarkeit werden sichtbar als `OFFEN`/`BLOCKED`
eskaliert; kein Folgeauftrag darf sie implizit aufloesen.
