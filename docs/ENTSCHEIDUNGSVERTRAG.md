# Entscheidungsvertrag (T-035, Abschnitt 0)

**Vertragsversion:** 1
**Status:** Durch den gatenden Vertragsspike des Auftrags
`.ai/tasks/T-035-graybox-decision-step.json` vor der Implementierung festgelegt;
die maschinenlesbaren Kennungen sind in
`src/Riftward.Session/DecisionContract.cs` gespiegelt und werden von einem
Test gegen dieses Dokument gehalten.

Dieser Vertrag entscheidet die Entscheidungsdetails des kleinsten spielbaren
Entscheidungsschritts verfahrensmäßig nach der Spike-Klausel
(`docs/QUALITAET.md`, Definition of Ready). Jede Wahl nennt Alternativen,
Gründe, ein messbares Playtestkriterium und einen Rückrollweg (ADR 007). Er
antwortet auf keine offene Produktfrage: Q-GAM-001 bis Q-GAM-007, Q-GAM-010,
Q-NAR-002 und Q-NAR-004 bleiben ausdrücklich `OFFEN`; Q-TEC-004
(Simulationsvertrag-Ratifizierung), Q-TEC-006 (produktives Replayformat) und
Q-TEC-010 bleiben `OFFEN`; Q-OPS-001 folgt der protokollierten T-020- bis
T-023-Behandlung. Die beiden Optionen erhalten keine Namen, keine Dialoge,
keine Lore, keine Texte und keine Belohnungssemantik; ihre Verständlichkeit
entsteht ausschließlich aus der sichtbaren, modusübergreifenden
Zonenzielfolge.

## 1. Geltungsbereich und Produktform

Dieser Vertrag implementiert den akzeptierten UF-001-Aufgabenbaustein „Aufgabe
mit zwei verständlichen Optionen" (VS-001-Mussinhalt „1 Aufgabe mit mindestens
2 sichtbaren Ausgängen"; Kernschleife „Heldenkampf / Questentscheidung", hier
bewusst nur der Entscheidungsast) über dem abgenommenen T-032-/T-033-Kern und
dem Erkundungsabschluss von T-034: Sobald der sitzungslokale Erkundungsauftrag
abgeschlossen ist, öffnet die Sitzung an einer Vorgrenze genau einmal ein
Entscheidungsangebot mit genau zwei deterministischen, assetfreien Optionen.
Der Spieler trifft die Entscheidung mit einer neuen sitzungsseitigen
Entscheidungsaktion; die gewählte Zone wird zum sichtbaren Folgeziel in beiden
Modi, und die persönliche Anwesenheit des Vertragshelden in dieser Zone an
einer Vorgrenze schließt die Folge ab.

Die gesamte Entscheidungsschicht ist rein sitzungsseitige Beobachtung und
Semantik an der Vorgrenze: Sie erzeugt niemals einen Kernbefehl, verändert
keinen Befehlszustand, ist nie Teil des Simulationszustands oder Hashes und
berührt `Riftward.Simulation` nicht (die Simulationsquelldateien bleiben gegen
den Vorblob byteidentisch; der Blobvergleich ist Run-Evidenz und Testbindung).
Es entstehen keinerlei Kampf-, Wirtschafts-, Dialog-, Quest-, Belohnungs-,
Inhalts- oder Fog-of-War-Regeln; Entscheidung, Folge und Protokoll sind
sitzungslokal und werden weder in Save/Load noch Replay fortgesetzt
(Abschnitt 7; ADR 008, Sequenzierungsnote).

**Rückrollweg (gesamt):** Der Entscheidungsvertrag wird als V1 versioniert;
jede Änderung einer Wahl dieses Dokuments erfordert Vertragsversion 2 mit
Fixture-Regeneration. Die gesamte Entscheidungsschicht lebt ausschließlich in
`Riftward.Session`, der Reportlinie und der darstellseitigen Verdrahtung; ein
Rückbau entfernt diese Schicht (Sitzungszustand, `--decision`-Aktivierung,
Folgezielkanal, Reportblock), ohne den Simulationskern, den Erkundungsvertrag
oder einen bestehenden Vertrag zu berühren.

## 2. Auslöseregel (`completion-gated-decision-offer-v1`)

**Wahl:** Das Angebot öffnet genau an der ersten Auswertungsgrenze (Vorgrenze
`T`; `world.TickIndex == T` vor dem Tick), an der der sitzungslokale
Erkundungsauftrag abgeschlossen ist (Erkundungsvertrag Abschnitt 4,
`completed == visitedCount == landmarkCount`), und genau einmal je Sitzung.
Die Beobachtungsordnung an jeder Vorgrenze ist fixiert: Erst werden die
Intents des Ticks ausgewertet (einschließlich Entscheidungsaktionen), dann die
Erkundungsbeobachtung (T-034), dann die Entscheidungsbeobachtung — das Angebot
öffnet also an der Abschlussgrenze erst nach deren Intentverarbeitung, und die
früheste wirksame Entscheidung liegt an der ersten Vorgrenze **nach** der
Angebotsgrenze. Der Angebotszustand ist in
beiden Modi über den Titel-HUD-Ausweis (Abschnitt 5) sichtbar und im Report
maschinenlesbar; ohne Abschluss innerhalb des Laufs wird kein Angebot geöffnet
und der Report trägt den ehrlichen, maschinenlesbaren Grund
`exploration-not-completed-within-run` statt stiller Leere.

**Alternativen:**

| Alternative | Bewertung |
|---|---|
| **A: Abschlussgekoppelte einmalige Auslösung (Empfehlung)** | Koppelt die Aufgabe an die akzeptierte Erkundungskette (UF-001: Erkunden führt zum Auftrag); der Anlass ist spielerisch verdient und deterministisch. Nachteil: ohne Erkundungsabschluss keine Entscheidung im Lauf. |
| B: Zeitor Tick-basierte Auslösung (festes Angebotstick unabhängig vom Abschluss) | Einfach deterministisch, aber der Auftrag öffnet unabhängig vom tatsächlichen Fortschritt; die Kopplung „Aufgabe entsteht aus erkundeter Welt" (UF-001) verlöre ihren Träger. Abgelehnt als Empfehlung, als spätere Verschärfung (feste Frist) jederzeit zulässig. |
| C: Angebotsöffnung nur im persönlichen Modus | Spiegelt die Aufsuchregel, verletzt aber die Sichtbarkeitspflicht in beiden Modi (der strategische Spieler sähe das Angebot nicht). Abgelehnt; die Moduskopplung liegt bewusst bei der Entscheidungseingabe (Abschnitt 3), nicht bei der Angebotsöffnung. |

**Playtestkriterien:** Tester erkennen das Angebot und beide Optionen in
beiden Modi binnen 2 Sekunden ohne Tastendruck; ≥ 90 % verstehen, dass das
Angebot erst nach dem vollständigen Erkundungsauftrag erscheint (kein
Defekt). **Rückrollweg:** Auslöseänderung ist eine Konstantenänderung mit
Vertragsversion 2 und Fixture-Regeneration, ohne Kernelaenderung.

## 3. Optionsableitung (`visit-protocol-zone-options-v1`)

**Wahl:** Genau zwei Optionen, deterministisch und assetfrei aus dem
sitzungslokalen Aufsuchprotokoll abgeleitet: **Option A** ist die Zone der
zuerst registrierten Landmarke (niedrigste `visitOrder`), **Option B** die
Zone der zuletzt registrierten Landmarke (höchste `visitOrder`). Beide Zonen
sind dem Spieler persönlich bekannt (er hat beide selbst aufgesucht). Die
Ableitung konsumiert ausschließlich Zonenindizes des Aufsuchprotokolls —
keinen Sitzungsseed, keine Assets, keine Namens-, Lore-, Text- oder
Ortsemantik jenseits der bestehenden Zonenidentitäten. **Fail-closed
Degenerationsfall:** Liefern weniger als zwei verschiedene Zonen (im
gebundenen Erkundungsvertrag unerreichbar: jede Landmarke registriert
höchstens einmal und der Abschluss verlangt alle sechs Zonen), bricht die
Ableitung kontrolliert mit dem definierten Vertragsfehler
`decision-offer-insufficient-distinct-zones` ab, statt ein entwertetes
Angebot mit gleichzeitigen Optionen zu öffnen.

**Alternativen:**

| Alternative | Bewertung |
|---|---|
| **A: zuerst und zuletzt registrierte Zone (Empfehlung)** | Beide Zonen sind dem Spieler persönlich bekannt, die Ableitung ist eine reine Funktion des Protokolls und über Seeds und Läufe vergleichbar; der Endpunkt (zuletzt aufgesucht, Held steht dort an der Abschlussgrenze) erzeugt einen sichtbaren Kontrast zum Startpunkt. Nachteil: die Optionswahl folgt der Aufsuchfolge, nicht einer Weltbedeutung (bewusst: keine Ortsemantik). |
| B: feste Zonen 0 und 5 | Einfach konstant und seedunabhängig, aber unabhängig vom tatsächlichen Aufsuchverhalten; Optionen könnten Zonen nennen, die der Spieler nie persönlich betreten hat — schwächt die UF-001-Kopplung „Aufgabe entsteht aus eigener Erkundung". Als dokumentierte Alternative mit Playtestkriterium (beide festen Zonen binnen 2 s identifizierbar) und Rückrollweg erhalten. |
| C: erste zwei registrierte Zonen | Ebenfalls protokollgeleitet, verliert aber den Abschlusskontext (die zuletzt aufgesuchte Zone ist der natürliche „hier stehe ich jetzt"-Anker der Entscheidung). Abgelehnt als Empfehlung. |
| D: drei Optionen | Verletzt die Auftragsvorgabe „genau zwei Optionen" (VS-001 „mindestens 2 Ausgänge" wird hier bewusst auf genau zwei präzisiert); abgelehnt. |

**Playtestkriterien:** Beide Optionszonen sind ohne Tastendruck in beiden
Modi binnen 2 Sekunden als Angebotsziele identifizierbar; Tester erkennen,
dass beide Optionen Orte sind, die sie selbst besucht haben. **Rückrollweg:**
Wechsel zu B oder C über Vertragsversion 2 mit Fixture-Regeneration, ohne
Kernelaenderung.

## 4. Entscheidungseingabe (`graybox-input-script-v3`, `decision-choose-personal-mode-only-v1`)

**Skriptgrammatik:** Neue Formatkennung `graybox-input-script-v3` als strikte
Obermenge von `graybox-input-script-v2` mit genau zwei neuen
sitzungsseitigen Entscheidungsaktionen `choose-a` und `choose-b` (keine
Parameter). Die Legacy-Grammatiken bleiben byteidentisch: Ein `choose`-Token
unter einem v1- oder v2-Kopf ist `UnknownAction` — keine stille Formatdrift
innerhalb einer Version. Die kanonische Intentordnung wird additive erweitert:
`clear (0) < point (1) < box (2) < move (3) < steer (4) < switch (5) <
choose-a (6) < choose-b (7)`; Entscheidungsaktionen werden also kanonisch nach
allen übrigen Intents desselben Ticks ausgewertet, erzeugen niemals einen
Kernbefehl und sind an den an ihrer Vorgrenze wirksamen Sitzungsmodus
gebunden. Limits, Fensterregeln, Kanonisierung, Ablehnungsklassen und beide
Hashbindungen entsprechen unverändert dem Kommandovertrag Abschnitt 5 (die
Kindbytes 6 und 7 erweitern den Planhash auf denselben Festbreitenschemata).

**Interaktive Bindung:** Die Keymapfamilie des Kommandovertrags Abschnitt 9
erhält die zwei zusätzlichen, frei belegbaren semantischen Aktionen
`choose-a` und `choose-b`; Standardbelegung ist die Zifferntaste `1`
(Scancode 30) beziehungsweise `2` (Scancode 31), im Bestandsstand unbesetzt.
Die Validierungsregeln des Abschnitts 9 (mindestens eine Bindung je Aktion,
keine Doppelbindungen, keine unbekannten Namen) gelten unverändert. Die
Maussemantik bleibt unverändert umbelegbar-nie.

**Modus-Scoping der Entscheidungseingabe (reversible Produktfrage):**

| Alternative | Bewertung |
|---|---|
| **A: Entscheidung nur im persönlichen Modus (Empfehlung, `decision-choose-personal-mode-only-v1`)** | Spiegelt die persönliche Anwesenheitsregel des Aufsuchens (Erkundungsvertrag Abschnitt 3): Mobilmachung und Überblick sind strategisch, die bewusste persönliche Handlung — hier die Wahl — erfordert die persönliche Perspektive. Der Wechsel wird damit zu einem spürbaren Schritt der Aufgabe. Nachteil: ein strategisch bleibender Spieler kann nicht wählen (bewusste Nichtregistrierung analog). |
| B: Entscheidung in beiden Modi | Weniger Reibung, aber die persönliche Entscheidungshandlung verlöre ihren Modusanlass; der Hybrid-Charakter (strategische Mobilmachung, persönliche Interaktion) würde zur optionalen Dekoration. Als dokumentierte Alternative mit Playtestkriterium (Wahlverständnis ohne Modusanforderung ≥ 90 %) und Rückrollweg erhalten. |
| Verworfen: Entscheidungs- oder Aufgabenzustand im Simulationskern | Kerneländerung ist verboten (Auftragsvertrag; ADR 008 Folge 6). Sie würde den Befehlsvertrag V1 erweitern und sämtliche Hashketten und Fixtures der T-021-/T-022-/T-032-/T-033-/T-034-Linie entwerten. Diese Variante wird nicht als Hypothese geführt; ihre Realisierung wäre eine eigene Simulationsvertrag-V2-Entscheidung (Q-TEC-004, `OFFEN`). |

**Auswertungsordnung (`decision-choice-evaluation-order-v1`):** Eine
Entscheidungsaktion wird an ihrer Vorgrenze in fester Reihenfolge geprüft und
sonst mit unterscheidbarer, maschinenlesbarer Disposition ohne Kernaenderung
abgewiesen: (1) Entscheidungsschicht nicht aktiviert →
`decision-not-activated`; (2) Angebot nicht geöffnet →
`decision-choose-before-offer`; (3) an dieser Vorgrenze strategischer Modus →
`decision-choose-in-strategic-mode`; (4) Entscheidung bereits gefallen →
`decision-choose-after-decision`; (5) sonst wirksam: die gewählte Zone wird
Folgeziel (Abschnitt 5). Die Reihenfolge ist vertraglich und testgebunden.

**Playtestkriterien:** Die Wahl ist binnen 2 Sekunden verstanden (Taste 1/2);
≥ 90 % der kontextfalschen Wahlen (falscher Modus, vor dem Angebot) werden als
bewusste Abweisung verstanden, nicht als Defekt; mediane Irritationsdauer
≤ 2 s. **Rückrollweg:** Wechsel zu Alternative B oder Änderung der
Keymap-Defaults über Vertragsversion 2 mit Fixture-Regeneration; die
Skript- und Reportzähler bleiben unverändert.

## 5. Folgeregel (`chosen-zone-follow-up-objective-v1`, `boundary-arrival-personal-mode-only-v1`)

**Wahl:** Die gewählte Zone wird sitzungslokales Folgeziel (einmalig, ohne
Alternativwechsel: nach der Entscheidung gibt es in derselben Sitzung keine
zweite Wahl). Der Abschluss der Folge wird mit dem bestehenden
Vorgrenzen-Besuchsmuster beobachtet (`boundary-arrival-personal-mode-only-v1`,
spiegelbildlich zur Aufsuchregel des Erkundungsvertrags Abschnitt 3): genau
dann, wenn an einer Auswertungsgrenze (i) der Vertragsheld (Agentenindex 0)
physisch in der Folgenzone ist (Zonenmitgliedschaft der Heldenposition),
(ii) die Sitzung an dieser Vorgrenze im persönlichen Modus ist und (iii) die
Folge in dieser Sitzung noch nicht abgeschlossen ist. Die Beobachtung
geschieht in der fixierten Vorgrenzenordnung nach der Entscheidungsbeobachtung;
eine Folge kann daher frühestens an der Entscheidungsgrenze selbst
abschließen (der Held steht dort bereits in der gewählten Zone), nicht davor.
Genau einmal je Sitzung mit Doppelabschluss-Schutz; ohne Kernbefehl und ohne
Simulationsberührung. Reihenfolgeunabhängigkeit: welche der beiden Optionen
gewählt wird, ändert ausschließlich die Folgenzone, niemals die
Abschlussregel.

**Alternativen:** Kerngetragenes Folgeziel (kernändernd — verworfen wie
Abschnitt 4); Abschluss ohne Moduskopplung (dokumentierte Alternative,
spiegelt Erkundungsvertrag Alternative B: Registrierung an jeder Vorgrenze
mit Anwesenheit; weniger Reibung, aber die persönliche Ankunft verlöre ihren
Modusanlass — Wechsel über Vertragsversion 2); strikt spätere
Abschlussgrenze (Anwesenheit erst ab der Grenze nach der Entscheidung; lehnt
die ehrliche „ich stehe schon hier"-Ankunft ab, ohne Playtestnutzen —
abgelehnt).

**Playtestkriterien:** ≥ 90 % der Tester verstehen die Kopplung
„Wahl → sichtbares Folgeziel"; die persönliche Ankunft in der Folgenzone
schließt die Folge erkennbar ab (binnen 2 s lesbar); eine zweite Ankunft
zählt nicht erneut. **Rückrollweg:** Semantikänderungen (Moduskopplung,
strikt spätere Grenze) sind Vertragsversion 2 mit Fixture-Regeneration; die
Nichtpersistenz bleibt bis zu einer autorisierten Savevertrags-Erweiterung
bestehen.

## 6. Feedback in beiden Modi (`title-hud-decision-objective-v1`, `follow-up-marker-channel-v1`)

**Wahl:** Zwei additive, darstellseitige Kanäle über der bestehenden
Zweikanal-Indikator-Regel NF-005 (ANFORDERUNGEN.md), beide ohne Tastendruck
in beiden Modi ablesbar, beide niemals Teil von Simulationszustand oder Hash:

1. **Titel-HUD-Erweiterung:** Die bestehende Titelzeile (inklusive der
   T-034-Erkundungssegmente) erhält ausschließlich bei Entscheidungs-
   aktivierung genau einen additiven, unterscheidbaren Entscheidungssegment
   in fester Form, je Zustand:
   - vor dem Angebot: ` — Entscheidung: –`
   - Angebot offen, unentschieden: ` — Entscheidung: A=Z<a> B=Z<b>`
     (mit `<a>`/`<b>` als Optionszonen; A ist die zuerst, B die zuletzt
     registrierte Zone gemäß Abschnitt 3)
   - entschieden, Folge offen: ` — Folgeziel: Z<f>`
   - entschieden, Folge abgeschlossen: ` — Folgeziel: Z<f> abgeschlossen`
   Ohne Entscheidungsaktivierung bleibt die Titelzeile byteidentisch zum
   T-034-Stand. Kennung `title-hud-decision-objective-v1`; Lesezeit ≤ 2 s.
2. **Folgezielmarker (`follow-up-marker-channel-v1`):** Genau ein neuer
   unterscheidbarer Markerzustand am bestehenden Landmarkenanker der
   gewählten Zone, aktiv ab der Entscheidung bis zum Sitzungsende; zwei
   unterscheidbare visuelle Kanaele gemäß NF-005 (Form plus Farbe, nie reine
   Farbcodierung): **dreistufige Markiersäule** (drei Diamantebenen bei
   1,2/2,4/3,6 m; untere Ebene ruhend mit fester Orientierung π/4, mittlere
   und obere Ebene rotieren mit der Tickzahl; Größen 1,30/1,15/1,00), warmes
   Violett (0,86/0,45/0,98). Die Formkanaltrennung (dreistufig gegenüber
   einstufig-unbesucht und zweistufig-registriert der Erkundungskanäle) und
   der Farbkanal trennen den Marker von Auswahlglyphe (warmes Amber, klein,
   ruhend), Befehlspuls (wachsend, bodenverankert, Cyan), Held-/Modus-Badge
   (einstufiger Diamant, Cyan/Orange) und beiden Landmarkenzuständen. Ohne
   Entscheidungsaktivierung entsteht kein Folgezielmarker; die
   Bestandsdarstellung bleibt byteidentisch. Der Marker verschiebt keinen
   Anker, ändert weder Angebots-, Wahl- noch Abschlussregel und ist nie
   Simulations- oder Hashzustand.

**Alternativen:** gerenderte Text-HUD (neue Schrift-/Renderfläche — späterer
Slice, Modevertrag-Abschnitt-8-Präzedenz); reine Farbcodierung des
Folgezielmarkers (NF-005-Verstoß — abgelehnt); pulsierender Einzelmarker
(kollidiert im Formkanal mit dem pulsierenden Badge — abgelehnt); Folgeziel-
Echo über dem Helden wie der T-034-Zonenecho (die Folgezone ist strategisch
sichtbar über den Titel und den Anker lesbar; ein zweites Echo verdoppelt die
Badge-/Echo-Dichte vor der nahen Kamera ohne Lesenutzen — abgelehnt als
Empfehlung, als Hypothesenkonstante später jederzeit zulässig).
**Playtestkriterien:** Angebotszustand, beide Optionen, Folgeziel und
Abschluss sind in beiden Modi binnen 2 s ablesbar; der Folgezielmarker ist
ohne Farbvergleich von beiden Landmarkenzuständen unterscheidbar.
**Rückrollweg:** Beide Feedbackformen sind Hypothesenkonstanten der
Darstellung; Austausch ohne Vertragspflicht, solange Zweikanal-Erkennbarkeit
und die Reportbindung erhalten bleiben.

## 7. Aktivierungsform (`opt-in-decision-activation-v1`) und Nichtpersistenz (`decision-session-local-not-persisted-v1`)

**Aktivierung:** Opt-in über das neue Befehlsflag `--decision` des bestehenden
öffentlichen Befehls `kommandoschleife`, gekoppelt an `--exploration`:
`--decision` ohne `--exploration` ist eine Usage-Fehlanwendung (bestehender
Exitcode 2, keine neue Bedeutung). Die Schemaversionen des Reports sind strikt
additiv gestaffelt: ohne Flags byteidentischer Bestandsstand (Schemaversion
2); mit `--exploration` allein byteidentischer Schemaversion-3-Stand des
T-034-Kandidaten; mit beiden Flags rein additive **Schemaversion 4** mit dem
Pflichtblock `decisionSession` — ausschließlich neue Felder, keine Umdeutung,
Umbenennung oder Entfernung bestehender Felder; der Gatevertrag bleibt
unberührt, alle neuen Felder tragen `gateCoupled=false`.

**Alternativen:** stets aktivierte Entscheidungsschicht (verletzt die
byteidentischen Bestandsreports — abgelehnt); separates Untercommand
(widerspricht dem Auftrag: derselbe öffentliche Befehl und derselbe
Pipelinepfad — abgelehnt); `--decision` ohne Kopplung an `--exploration`
(erzeugt einen Entscheidungszustand ohne seinen vertraglichen
Auslöserträger — abgelehnt). **Rückrollweg:** Flag und Reportblock entfernen;
ohne Flag ist der Stand byteidentisch zum Vorgänger.

**Maschinenlesbare Nichtpersistenz (`decision-session-local-not-persisted-v1`):**
Angebot, Entscheidung, Folge und Protokoll sind sitzungslokal — ein Lauf,
kein Fortsetzungsanspruch. Sie sind weder in Save/Load (T-031, Savevertrag
V1) noch in Replay oder Soak fortgesetzt, in keinem Persistenzvertrag
enthalten und ihre Persistenz ist einer späteren Savevertrags-Erweiterung
vorbehalten (ADR 008, Sequenzierungsnote zu Kernaussage 4). Der Report trägt
diese Aussage als versionierte Kennung mit `persisted=false`; es entsteht
keine Write-/Lesefähigkeit über den Sitzungszustand hinaus, und Schreib-
zugriffe des Laufs bleiben auf die vertraglich erlaubten Verzeichnisse
(Reportpfad, opt-in Abgriff) beschränkt.

## 8. Reportbindung (rein additive Schemaversion 4)

Bei Aktivierung bindet der Report unter `decisionSession` (beide
Ausführungsarten; `gateCoupled=false` für sämtliche Mess- und Protokollfelder):

- Vertragsbindung (`contract`: Dokumentpfad und Version dieses Vertrags),
  Aktivierungs- und Modellkennungen (`opt-in-decision-activation-v1`,
  `completion-gated-decision-offer-v1`, `visit-protocol-zone-options-v1`,
  `decision-choose-personal-mode-only-v1`,
  `chosen-zone-follow-up-objective-v1`,
  `boundary-arrival-personal-mode-only-v1`)
- Angebot (Geöffnetkennung, Angebotsgrenze bzw. ehrlicher
  Nichtöffnungsgrund), Optionszonen A/B, Entscheidung (Grenze, Wahl
  `a`/`b`, Modus der Wahl, gewählte Zone), Folge (Folgenzone,
  Abschlusskennung, Ankunftsgrenze)
- Abweisungszähler je Auswertungsordnungsklasse (Abschnitt 4)
- versionierte Nichtpersistenzaussage (`decision-session-local-not-persisted-v1`,
  `persisted=false`, Umfänge Save/Load und Replay)
- im Interaktivmodus der HUD-Ausweis (`title-hud-decision-objective-v1`) und
  der Folgezielkanalausweis (`follow-up-marker-channel-v1`); headless
  ausdrücklich nicht gemessen mit maschinenlesbarem Grund statt stiller
  Behauptung. Auch ein vorzeitig beendetes Interaktivfenster weist beide
  Kanäle mit Grund als nicht gemessen aus; `measured=true` setzt einen
  tatsächlich abgeschlossenen Fensterhorizont voraus.

Der Schemator prüft diese Felder nicht nur einzeln, sondern bindet sie
relational fail-closed: die gewählte Zone ist eine Angebotszone und der Wahl
zugeordnet; die Ankunft liegt an oder nach der Entscheidungsgrenze; die
Folgenzone ist die gewählte Zone; Abschluss und Ankunftsgrenze tragen
dieselbe Aussage; ohne Angebot gibt es keine Entscheidung und keine Folge;
die Abweisungszähler sind nichtnegativ und die Angebotszonen sind
verschieden.

Der Headless-Entscheidungsflow läuft über denselben öffentlichen Befehl und
dasselbe Skriptformat `graybox-input-script-v3`; zwei unabhängige
Fresh-Prozessläufe sind builderidentisch, ein fremder Seed ändert Start- und
Endhash nachweislich, und die Legacyschemata (Schemaversion 2 ohne
Aktivierung, Schemaversion 3 mit `--exploration` allein) bleiben unverändert
gültig. Angebotsoptionen und Folgestruktur sind reine Funktionen des
Sitzungszustands (Aufsuchprotokoll), der Modusgrenzen und der Wahl: Die
Ableitung aus einem gegebenen Protokoll ist seedunabhängig, und die
Grenzzeiten des Protokolls folgen ausschließlich den beobachteten
Sitzungsgrenzen desselben Laufs.

## 9. Opt-in Abgriff des entschieden/abgeschlossenen Zustands

Höchstens ein einzelner opt-in Abgriff folgt unverändert dem bestehenden
T-023-/T-032-/T-033-/T-034-Muster: Nur mit `--capture-frame PFAD`, strikt
nach dem Messfenster, über demselben gebundenen Weltzustand; der
bestehende Abgriffpaar-Mechanismus (je ein strategischer und persönlicher
1920×1080-BMP-Einzelabgriff, SHA-256-hashgebunden im Report) dient als
dieser eine Abgriff. Bei Entscheidungsaktivierung zeigt der persönliche
Abgriff den Folgezielmarker am gewählten Anker; es entsteht **kein** neuer
Abgriffpfad, kein zweiter Abgriff und keine neue Dateibenennung. Die
maschinenlesbare Aussagegrenze bleibt
`graybox-state-occupancy-not-gameplay-atmosphere-or-shipping` (Graybox-
Zustandsbelegung — niemals Gameplay-, Atmosphären- oder Shipping-Beleg;
öffentliche Verwendung nur über `docs/communication/MEDIA_LAB.md` plus
Projektleitungsautorisierung). Ohne Flag entsteht keine Datei; das
Messverhalten ist identisch; ein fehlgeschlagener Abgriff ergibt die
bestehenden Codes 38/36 mit `captured=false` und Grund.

## 10. Vorregistriertes Playtestprotokoll

Vollständiges Protokoll einer Displaysession (Entwickler-PC, gegebenenfalls
virtuelles Wayland nach T-023-Präzedenz), vor der Implementierung
registriert:

1. **Angebotslesbarkeit:** Nach Abschluss des Erkundungsauftrags zeigt der
   Titel in beiden Modi das Angebot mit beiden Optionszonen binnen 2 s ohne
   Tastendruck (Abschnitte 2, 3 und 6).
2. **Wahlverständnis:** Die Zifferntasten 1/2 wählen binnen 2 s verständlich;
   die gewählte Zone wird unmittelbar als Folgeziel sichtbar (Titel und
   Marker am Anker).
3. **Moduskopplung der Wahl:** Im strategischen Modus erhält eine Wahltaste
   die sichtbare, maschinenlesbare Abweisung `decision-choose-in-strategic-
   mode`; nach dem Wechsel in den persönlichen Modus wird dieselbe Taste
   wirksam; Missverständnisrate < 10 %.
4. **Folgeabschluss:** Die persönliche Ankunft in der Folgenzone schließt die
   Folge sichtbar ab (Titel `abgeschlossen`); eine erneute Ankunft zählt
   nicht erneut (Reportbindung).
5. **Beobachtungstreue:** Strategische Phasen bleiben unverändert bedienbar;
   kein Befehlspuls und keine Weltänderung geht von Angebot, Wahl oder
   Abschluss aus.

Ausführung: dokumentiert im Abnahmelauf; ist kein Display verfügbar, bleiben
Interaktivsmoke und Playtestausführung ausgewiesene Restpunkte mit
kontrolliertem Code-19-Nachweis ohne Simulation (Präzedenz
T-023/T-032/T-033/T-034).

## 11. Exitcodes

Die bestehenden Bedeutungen bleiben unverändert (insbesondere 19, 27, 28,
35–38 und 2/4): Der Entscheidungsmechanismus erzeugt **keine** neuen
Exitcodebedeutungen. Der nicht aktivierte Lauf verhält sich wie der
Bestandsstand; `--decision` ohne `--exploration` nutzt die bestehende
Usage-Bedeutung (2); die aktivierte Entscheidungsschicht ist reguläre
fachliche Diagnostik (Reportfelder mit `gateCoupled=false`), kein
Fehlerzustand, und sie koppelt das Gateverdict nicht. Der stabile
Exitcode-Mapping-Test wird ohne neue Bedeutung erweitert (Schemaversion-
Auswahl 2/3/4 ist kein Exitcode).

## 12. Offenheiten und Grenzen

Dieser Vertrag antwortet auf keine offene Produktfrage. Ausdrücklich offen
bleiben: Q-GAM-001 bis Q-GAM-007 (Kreativentscheidungen), Q-GAM-010 (finale
Wechsel-Detailregel), Q-NAR-002 (Erzählung) und Q-NAR-004 (Questoptionen mit
identitätsbestimmenden Folgen) sowie Q-TEC-004 (Simulationsvertrag-
Ratifizierung), Q-TEC-006 (produktives Replay-, Cooked-Paket- und
Definitionsformat), Q-TEC-010 (tolerierte Benchmarkstreuung), Q-OPS-001
(Referenzhardware; Pflichtprofile bleiben `NOT-MEASURED`). Es gibt keinen Fog
of War, keine Minimap, keine Aufklärungs- oder Sichtbarkeitssemantik (GS-007
bleibt unberührt), keine Audio- oder Shipping-Assetaussage (Q-TEC-007,
Q-AST-001/Q-AST-002), keine Persistenz in jeder Form, keinen
Windows-/macOS-Scope (T-011, Q-OPS-002/Q-OPS-003) und keine Budgetänderung
jeder Art. GAME_DESIGN.md und ANFORDERUNGEN.md bleiben durch die
Implementierung unberührt; `docs/KOMMANDOVERTRAG.md` und `docs/MODEVERTRAG.md`
bleiben byteidentisch; `docs/ERKUNDUNGSVERTRAG.md` bleibt byteidentisch (die
Entscheidungsschicht konsumiert ausschließlich seine öffentlichen
sitzungslokalen Ausweise). `docs/ARCHITEKTUR.md` hält die sitzungslokale
Entscheidungssemantik in den Laufzeitverträgen fest; `docs/AUTOMATION.md`
bildet die Aktivierung und die Reportfelder ab.
