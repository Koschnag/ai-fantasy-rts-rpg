# Druckvertrag (T-036, Abschnitt 0)

**Vertragsversion:** 1
**Status:** Durch den gatenden Vertragsspike des Auftrags
`.ai/tasks/T-036-graybox-pressure-restart.json` vor der Implementierung
festgelegt; die maschinenlesbaren Kennungen sind in
`src/Riftward.Session/PressureContract.cs` gespiegelt und werden von einem
Test gegen dieses Dokument gehalten. Die autorisierte additive
Zyklus-Präzisierung des Entscheidungsvertrags ist als dessen Version 2
dokumentiert (`docs/ENTSCHEIDUNGSVERTRAG.md`, Abschnitt 13) und wird hier
vorausgesetzt.

Dieser Vertrag entscheidet die Druckdetails des kleinsten spielbaren Druck-
und Neustartschritts verfahrensmäßig nach der Spike-Klausel
(`docs/QUALITAET.md`, Definition of Ready). Jede Wahl nennt Alternativen,
Gründe, ein messbares Playtestkriterium und einen Rückrollweg (ADR 007). Er
antwortet auf keine offene Produktfrage: Q-GAM-001 bis Q-GAM-007, Q-GAM-010,
Q-NAR-002 und Q-NAR-004 bleiben ausdrücklich `OFFEN`; Q-TEC-004
(Simulationsvertrag-Ratifizierung), Q-TEC-006 (produktives Replayformat) und
Q-TEC-010 bleiben `OFFEN`; Q-OPS-001 folgt der protokollierten T-020- bis
T-023-Behandlung. Der Druck ist reine Zeitpressung ohne Gegnerobjekt, ohne
Schadens-, Wirtschafts-, Dialog-, Belohnungs- oder Inhaltssemantik; seine
Verständlichkeit entsteht ausschließlich aus der sichtbaren, modusübergreifenden
Zustandsfolge (Fenster, Restzeit, Fehlschlag mit Ursache, Neustart, Erfolg).

## 1. Geltungsbereich und Produktform

Dieser Vertrag implementiert die noch fehlenden Schleifenelemente Druck,
definierter Verlust und Neustart (VS-001-Kernschleife „Druck oder Kosten,
Gewinn und Verlust, Neustart"; UF-001-Fehlerfallzeile „definierter
Fehlschlag mit Ursache; kein unklarer Softlock … letzten gültigen
Checkpoint laden oder neu starten"; Alpha-Loop-Muss des Release-Modus) über
dem abgenommenen T-032-/T-033-Kern, dem T-034-Erkundungsabschluss und dem
T-035-Entscheidungsabschluss: Sobald die sitzungslokale Entscheidung
(T-035) wirksam ist, startet an derselben Vorgrenze genau eine Instanz eines
deterministischen, sitzungsseitigen Zeitfensters. Die persönliche Ankunft
des Vertragshelden in der Folgenzone innerhalb des offenen Fensters
schließt die Folge wie in T-035 als Erfolg ab; der Ablauf des Fensters an
einer Vorgrenze ohne Ankunft erzeugt einen definierten Fehlschlag mit
Ursache und unterscheidbarem Zwei-Kanal-Feedback, und an der nächsten
Vorgrenze öffnet das Entscheidungsangebot deterministisch erneut
(sitzungslokaler Neustart der Auftragskette).

Die gesamte Druckschicht ist rein sitzungsseitige Beobachtung und Semantik
an der Vorgrenze: Sie erzeugt niemals einen Kernbefehl, verändert keinen
Befehlszustand, liest ausschließlich Entscheidungszustand, Heldenzone und
wirksamen Sitzungsmodus schreibgeschützt und ist zu keinem Zeitpunkt Teil
des Simulationszustands oder Hashes. `Riftward.Simulation` bleibt gegen den
Vorblob byteidentisch (Blobvergleich als Run-Evidenz und Testbindung). Es
entstehen keinerlei Kampf-, Wirtschafts-, Dialog-, Quest-, Belohnungs-,
Inhalts- oder Fog-of-War-Regeln; Druck-, Fehlschlags- und Zykluswahrheit
sind sitzungslokal und werden weder in Save/Load noch Replay fortgesetzt
(Abschnitt 8; ADR 008, Sequenzierungsnote).

**Rückrollweg (gesamt):** Der Druckvertrag wird als V1 versioniert; jede
Änderung einer Wahl dieses Dokuments erfordert Vertragsversion 2 mit
Fixture-Regeneration. Die gesamte Druckschicht lebt ausschließlich in
`Riftward.Session`, der Reportlinie und der darstellseitigen Verdrahtung;
ein Rückbau entfernt diese Schicht (`--pressure`-Aktivierung,
`pressureSession`-Reportblock, Druck-Segment und Neustartanzeige), ohne den
Simulationskern, den Entscheidungsvertrag im Übrigen oder einen bestehenden
Vertrag zu berühren.

## 2. Auslöseregel (`decision-coupled-window-v1`)

**Wahl:** Die erste Fensterinstanz startet genau an der Vorgrenze `T`, an
der die gültige T-035-Entscheidung wirksam wird (`decision.DecisionBoundaryTick == T`
vor dem Tick); jede weitere Instanz startet genau an der Vorgrenze der
erneut wirksamen Wahl nach Wiederauffrischung des Angebots (Abschnitt 4).
Ohne wirksame Entscheidung existiert kein Fenster: Wurde der
Entscheidungsstand innerhalb des Laufs nicht erreicht, trägt der Report den
ehrlichen, maschinenlesbaren Endstatus `not-started` mit Grund
(`decision-not-reached-within-run` ohne Angebot,
`decision-offer-open-without-choice-within-run` bei offenem, ungenutztem
Angebot) statt stiller Leere. Der Fensterzustand (offen, Restzeit,
Fehlschlag, Neustart, Erfolg) ist in beiden Modi über den Titel-HUD-Ausweis
(Abschnitt 6) sichtbar und im Report maschinenlesbar.

**Alternativen:**

| Alternative | Bewertung |
|---|---|
| **A: Entscheidungsgekoppelte Auslösung (Empfehlung)** | Das Fenster ist die Zeitwahrheit der bereits akzeptierten Aufgabe (UF-001: Erkunden führt zum Auftrag, Auftrag erhält eine Frist); der Anlass ist deterministisch und in beiden Modi sichtbar. Nachteil: ohne erreichten Entscheidungsstand kein Druck im Lauf (ehrlich ausgewiesen). |
| B: Tick-basierte Auslösung unabhängig von der Entscheidung (festes Angebots- oder Sitzungstick) | Einfach deterministisch, aber das Fenster würde unabhängig vom Auftragsfortschritt laufen und die Kopplung „Frist gehört zur Aufgabe" verlieren; ohne gewählte Folgenzone wäre unklar, was das Fenster bewacht. Abgelehnt als Empfehlung. |
| C: Fensteröffnung nur im persönlichen Modus | Spiegelt die Wahl- und Ankunftskopplung, verletzt aber die Sichtbarkeitspflicht in beiden Modi (der strategisch mobilisierende Spieler sähe die Restzeit nicht, obwohl strategische Mobilmachung die Ankunft erst ermöglicht). Abgelehnt. |

**Playtestkriterien:** Tester erkennen das offene Fenster und seine Restzeit
in beiden Modi binnen 2 Sekunden ohne Tastendruck; ≥ 90 % verstehen, dass
das Fenster mit der Wahl beginnt (kein Defekt). **Rückrollweg:**
Auslöseänderung ist eine Konstantenänderung mit Vertragsversion 2 und
Fixture-Regeneration, ohne Kernänderung.

## 3. Zeitbasis (`fixed-deterministic-tick-window-v1`, reversible Produktfrage)

**Wahl: festes deterministisches Tickfenster auf der bestehenden
20-Hz-Vorgrenzenrasterung.** Die Fensterlänge ist die fixierte
Vertragskonstante `WindowLengthTicks = 600` Vorgrenzen (30 s bei 20 Hz).
Das offene Fenster umfasst jede Auswertungsgrenze ab der Öffnungsgrenze;
die Ablaufgrenze ist die erste Vorgrenze `T_open + WindowLengthTicks`. Die
persönliche Ankunft an der Ablaufgrenze selbst ist die letzte Gelegenheit
innerhalb des Fensters (sie wird in der vertraglichen Beobachtungsordnung
vor dem Ablauf geprüft, Abschnitt 4); ohne Ankunft an der Ablaufgrenze tritt
der Fehlschlag an ihr ein. **Konkreter Wert als vorregistrierte Hypothese
mit Playtestkriterium:** 600 Vorgrenzen, weil der akzeptierte T-035-
Referenzfluss (Wahl an Vorgrenze 7300, persönliche Ankunft an 7857) 557
Vorgrenzen für Mobilmachung und Reise brauchte — 600 erzeugen damit
messbare, aber überwindbare Pressung und lassen den Erfolg einer
umsichtigen Mobilmachung zu, während ein Untätigbleiben die Frist
zuverlässig verstreichen lässt.

**Alternativen:**

| Alternative | Bewertung |
|---|---|
| **A: festes deterministisches Tickfenster (Empfehlung)** | Trivial deterministisch, seedunabhängig, auf dem bestehenden Vorgrenzenraster ohne neue Zeitquelle; Restzeit ist als Tickdifferenz exakt maschinenlesbar und in beiden Modi identisch. Nachteil: Distanz zur Folgenzone bleibt unbewertet (nahe Zonen sind leichter als ferne). |
| B: distanzskaliertes Fenster (Fensterlänge aus dem Heldenabstand zur Folgenzone an der Wahlgrenze) | Bewertet Entfernung fairer, bleibt deterministisch (reine Funktion des Weltzustands an der Wahlgrenze); kostet aber eine zweite konstantengebundene Skalierungsfamilie mit eigenen Playtests und verdoppelt die Fixturefläche. Als dokumentierte Alternative mit Playtestkriterium (Pressungsempfinden ≥ 80 % der Tester je Distanzterzil) und Rückrollweg erhalten. |
| Verworfen: zufällige oder seedabhängige Fensterlängen | Brechen Determinismus und Replay (Fremdseed dürfte das Druckprotokoll niemals ändern, Auftragsvertrag AC-T036-02); die Variante wird nicht als Hypothese geführt. |

**Playtestkriterien:** ≥ 80 % der Tester empfinden das offene Fenster als
erkennbare Zeitpressung (Restzeit lesbar); ≥ 80 % erreichen die Ankunft in
einem zweiten Versuch nach einem Fehlschlag (keine unüberwindbare Frist);
die Restzeit ist in beiden Modi ohne Tastendruck binnen 2 s ablesbar.
**Rückrollweg:** Fensterwert-Änderung ist eine Konstantenänderung mit
Vertragsversion 2 und Fixture-Regeneration; Wechsel zu Alternative B
ebenfalls über Vertragsversion 2.

## 4. Fehlschlags- und Neustartregel (`defined-failure-automatic-reopen-v1`, reversible Produktfrage)

**Wahl: definierter Fehlschlag mit Ursache und deterministische
automatische Wiederauffrischung des Angebots an der nächsten Vorgrenze**
(`defined-failure-automatic-reopen-v1`, Neustartmodell
`session-local-cycle-restart-v1`).
Läuft das offene Fenster an der Ablaufgrenze ohne persönliche Ankunft in
der Folgenzone ab, erzeugt die Schicht an dieser Vorgrenze einen
definierten Fehlschlag mit der vertraglichen Ursachenkennung
`window-expired-without-arrival`, beendet die Fensterinstanz mit
Endgrund `expired` und setzt den sitzungslokalen Auftragszyklus
kontrolliert zurück (Wahl- und Folgezustand der abgelaufenen Instanz
enden; Sitzungsabweisungszähler bleiben Sitzungsgesamtwerte). An der
nächsten Vorgrenze öffnet das Entscheidungsangebot deterministisch erneut
— unveränderte Optionsableitung aus dem Aufsuchprotokoll, eindeutige
Zykluszählung (jede wirksame Wahl beginnt Zyklus `n+1`), keine Welt- oder
Simulationszustandsänderung, kein Kernbefehl. Die dafür erforderliche
Semantikänderung der Angebots-Einmaligkeit ist die autorisierte additive
Zyklus-Präzisierung des Entscheidungsvertrags: „genau einmal je Sitzung"
wird zu „genau einmal je Auftragszyklus mit definierter Wiederauffrischung
nach definiertem Fehlschlag" (Entscheidungsvertrag V2, Abschnitt 13);
Auslösung, Optionsableitung, Modus-Scoping und die Erfolgswahrheit der
Ankunftsregel bleiben im Übrigen unverändert. Der Report weist
Fehlschlagsgrenze und -ursache sowie die Wiederauffrischungsgrenze
maschinenlesbar aus. Kein unklarer Softlock: Nach jedem Fehlschlag ist das
Angebot an der nächsten Vorgrenze erneut offen; die früheste erneute Wahl
liegt an der ersten Vorgrenze nach der Wiederauffrischungsgrenze
(spiegelbildlich zur T-035-Angebotsordnung, da Intents vor den
Beobachtungen ausgewertet werden).

**Alternativen:**

| Alternative | Bewertung |
|---|---|
| **A: definierter Fehlschlag mit Ursache und automatische Wiederauffrischung an der nächsten Vorgrenze (Empfehlung)** | Erfüllt VS-001 (Gewinn und Verlust) und UF-001 (definierter Fehlschlag mit Ursache, kein unklarer Softlock) mit dem kleinsten sitzungsseitigen Zustandsautomaten; der Neustart ist ohne Tastendruck und ohne Weltverlust erkennbar. Nachteil: die Wiederauffrischung braucht die autorisierte additive Zyklus-Präzisierung des Entscheidungsvertrags (V2) mit Fixture-Regeneration. |
| B: explizite Neustartaktion als Skriptgrammatik v4-Obermenge (Spieler bestätigt den Neustart) | Hält die Angebots-Einmaligkeit je Sitzung strikt, kostet aber eine neue Grammatikstufe mit Keymap-Aktion, Validation-, Fixture- und Dokufläche; ohne Playtestbeleg für Verwirrung über die automatische Wiederauffrischung ist sie vorzeitige Komplexität. Als dokumentierte Alternative mit Playtestkriterium (≥ 90 % verstehen die automatische Wiederauffrischung; sonst Wechsel zu B) und Rückrollweg erhalten. |
| Verworfen: straffreies Zeitfenster (Ablauf ohne Folge) | Verletzt VS-001 („Gewinn und Verlust") und das Alpha-Loop-Muss („Druck oder Kosten") direkt; die Variante wird nicht als Hypothese geführt. |
| Verworfen: Sitzungsneuaufbau der Erkundung nach Fehlschlag (Auftragskette komplett neu) | Verletzt die Auftragsvorgabe (Neustart ist ausschließlich der sitzungslokale Auftragszyklus; die Optionsableitung bleibt unverändert aus dem Aufsuchprotokoll) und würde ADR 008 widersprechen (kein Welt- oder Auftragsneustart aus der Druckschicht). |

**Playtestkriterien:** Der Fehlschlag ist binnen 2 s als Zeitablauf mit
Ursache verständlich (Zwei-Kanal-Anzeige); der Neustart wird als neue
Chance erkannt und nicht als Weltverlust missverstanden (Missverständnisrate
< 10 %); keine unklare Softlock-Situation (nach jedem Fehlschlag ist eine
Handlungsofferte sichtbar). **Rückrollweg:** Wechsel zu Alternative B über
Vertragsversion 2 mit Fixture-Regeneration; die Ursachenkennung und die
Grenzfelder bleiben als Reportwahrheit erhalten.

## 5. Erfolgsregel (`unchanged-decision-arrival-within-window-v1`)

**Wahl:** Die Erfolgswahrheit der T-035-Ankunftsregel bleibt unverändert
(`boundary-arrival-personal-mode-only-v1`): genau dann, wenn an einer
Auswertungsgrenze (i) der Vertragsheld physisch in der Folgenzone ist,
(ii) die Sitzung an dieser Vorgrenze im persönlichen Modus ist und (iii)
die Folge im aktuellen Zyklus noch nicht abgeschlossen ist, schließt die
persönliche Ankunft die Folge ab. Innerhalb des offenen Fensters ist diese
Ankunft der definierte Erfolg: die Fensterinstanz endet mit Endgrund
`success`, der Report weist Ankunftsgrenze und Ankunftsmodus aus, und der
Zyklus ist mit Einmalabschluss beendet (eine zweite Ankunft zählt nicht
erneut; ohne Folgezyklus nach Erfolg — der Auftragszyklus endet in
Erfolg). Die Druckschicht beobachtet den Abschluss ausschließlich
schreibgeschützt in der vertraglichen Beobachtungsordnung nach der
Entscheidungsbeobachtung; sie ändert weder Angebots-, Wahl- noch
Abschlussregel.

**Alternativen:** Kerngetragener Erfolg (kernändernd — verworfen wie
Entscheidungsvertrag Abschnitt 5); straffreies Fenster ohne Ankunftswahrheit
(Verlust ohne Verlustwirkung — verworfen wie Abschnitt 4).
**Playtestkriterien:** ≥ 90 % der Tester erkennen den Erfolg binnen 2 s an
Titel und Marker (keine reine Farbcodierung); eine zweite Ankunft zählt
nicht erneut. **Rückrollweg:** Semantikänderungen sind Vertragsversion 2
mit Fixture-Regeneration.

## 6. Feedback in beiden Modi (`title-hud-pressure-window-v1`, `pressure-restart-indicator-channel-v1`)

**Wahl:** Zwei additive, darstellseitige Kanäle über der bestehenden
Zweikanal-Indikator-Regel NF-005 (ANFORDERUNGEN.md), beide ohne Tastendruck
in beiden Modi ablesbar, beide niemals Teil von Simulationszustand oder
Hash:

1. **Titel-HUD-Erweiterung:** Die bestehende Titelzeile (inklusive der
   T-034-Erkundungs- und T-035-Entscheidungssegmente) erhält ausschließlich
   bei Druckaktivierung genau einen additiven, unterscheidbaren
   Druck-Segment in fester Form, je Zustand:
   - offenes Fenster: ` — Druck: Zyklus <n> Rest <r>` (mit `<n>` als
     Zyklusnummer ab 1 und `<r>` als Restzeit in Vorgrenzen bis zur
     Ablaufgrenze einschließlich)
   - Fehlschlags-/Neustartzeitraum (ab der Fehlschlagsgrenze bis zur
     nächsten wirksamen Wahl): ` — Druck: Fehlschlag: Zeit abgelaufen`
   - erfolgreicher Zyklusabschluss: ` — Druck: Erfolg`
   Ohne Druckaktivierung bleibt die Titelzeile byteidentisch zum T-035-Stand.
   Kennung `title-hud-pressure-window-v1`; Lesezeit ≤ 2 s.
2. **Neustartanzeige (`pressure-restart-indicator-channel-v1`):** Genau ein
   neuer unterscheidbarer Markerzustand am bestehenden Landmarkenanker der
   Folgenzone des fehlgeschlagenen Zyklus, aktiv ab der Fehlschlagsgrenze
   bis zur nächsten wirksamen Wahl; zwei unterscheidbare visuelle Kanale
   gemäß NF-005 (Form plus Farbe, nie reine Farbcodierung):
   **zweistufige, klein-unten/groß-oben markierte Säule** (zwei
   Diamantebenen bei 1,5/3,0 m; untere Ebene ruhend mit fester Orientierung
   π/4 in Größe 0,90, obere Ebene rotiert mit der Tickzahl in Größe 1,05 —
   umgekehrte Größenordnung und abweichende Höhen gegenüber der
   zweistufigen registrierten Landmarkensäule), warmes Rot
   (0,90/0,28/0,22). Die Formkanaltrennung (Höhen/Größenprofil gegenüber
   einstufig-unbesucht, zweistufig-registriert, dreistufig-Folgeziel) und
   der Farbkanal trennen den Marker von Auswahlglyphe, Befehlspuls,
   Held-/Modus-Badge, beiden Landmarkenzuständen und dem Folgezielmarker.
   Ohne Druckaktivierung entsteht keine Neustartanzeige; die
   Bestandsdarstellung bleibt byteidentisch. Der Marker verschiebt keinen
   Anker, ändert keine Fehlschlags-, Wiederauffrischungs- oder Erfolgsregel
   und ist nie Simulations- oder Hashzustand.

**Alternativen:** gerenderte Text-HUD mit Restzeitbalken (neue
Schrift-/Renderfläche — späterer Slice, Modevertrag-Abschnitt-8-Präzedenz);
reine Farbcodierung des Fehlschlags (NF-005-Verstoß — abgelehnt);
pulsierender Einzelmarker (kollidiert im Formkanal mit dem pulsierenden
Badge — abgelehnt); rotes Echo über dem Helden (die Folgenzone ist
strategisch über Titel und Anker lesbar; ein zweites Echo verdoppelt die
Badge-/Markerdichte vor der nahen Kamera ohne Lesenutzen — abgelehnt).
**Playtestkriterien:** Fenster, Restzeit, Fehlschlag mit Ursache, Neustart
und Erfolg sind in beiden Modi binnen 2 s ablesbar; die Neustartanzeige ist
ohne Farbvergleich von beiden Landmarkenzuständen und dem Folgezielmarker
unterscheidbar. **Rückrollweg:** Beide Feedbackformen sind
Hypothesenkonstanten der Darstellung; Austausch ohne Vertragspflicht,
solange Zweikanal-Erkennbarkeit und die Reportbindung erhalten bleiben.

## 7. Aktivierungsform (`opt-in-pressure-activation-v1`) und Reportlinie

**Aktivierung:** Opt-in über das neue Befehlsflag `--pressure` des
bestehenden öffentlichen Befehls `kommandoschleife`, gekoppelt an
`--decision` (und damit transitiv an `--exploration`): `--pressure` ohne
`--decision` ist eine Usage-Fehlanwendung (bestehender Exitcode 2, keine
neue Bedeutung). Die Skriptgrammatik bleibt unverändert; Bestandsskripte
v1/v2/v3 bleiben byteidentisch gültig, und es gibt keine neuen
Exitcodebedeutungen. Die Schemaversionen des Reports sind strikt additiv
gestaffelt: ohne Flags byteidentischer Bestandsstand (Schemaversion 2); mit
`--exploration` allein byteidentischer Schemaversion-3-Stand; mit
`--decision` byteidentischer Schemaversion-4-Stand; mit beiden und
`--pressure` rein additive **Schemaversion 5** mit dem Pflichtblock
`pressureSession` — ausschließlich neue Felder, keine Umdeutung,
Umbenennung oder Entfernung bestehender Felder; der Gatevertrag bleibt
unberührt, alle neuen Felder tragen `gateCoupled=false`.

**Alternativen:** stets aktivierte Druckschicht (verletzt die
byteidentischen Bestandsreports — abgelehnt); separates Untercommand
(widerspricht dem Auftrag: derselbe öffentliche Befehl und derselbe
Pipelinepfad — abgelehnt); `--pressure` ohne Kopplung an `--decision`
(erzeugt ein Fenster ohne seinen vertraglichen Auslöserträger — abgelehnt).
**Rückrollweg:** Flag und Reportblock entfernen; ohne Flag ist der Stand
byteidentisch zum Vorgänger.

## 8. Reportbindung (rein additive Schemaversion 5) und Nichtpersistenz

Bei Aktivierung bindet der Report unter `pressureSession` (beide
Ausführungsarten; `gateCoupled=false` für sämtliche Protokollfelder):

- Vertragsbindung (`contract`: Dokumentpfad und Version dieses Vertrags),
  Modellkennungen (`opt-in-pressure-activation-v1`,
  `decision-coupled-window-v1`, `fixed-deterministic-tick-window-v1`,
  `defined-failure-automatic-reopen-v1`,
  `unchanged-decision-arrival-within-window-v1`,
  `pressure-session-local-not-persisted-v1`)
- die fixierte Fensterlänge (`windowLengthTicks`)
- das Fensterprotokoll je Instanz (Instanzzähler, Zyklusnummer,
  Startgrenze, Endgrenze bzw. ehrlicher Offenzustand, Endgrund
  `success`/`expired`, bei Erfolg Ankunftsgrenze und Ankunftsmodus, bei
  Ablauf die Ursachenkennung)
- Zykluszählung (`cycleCount`), letzter Fehlschlag (Grenze und Ursache),
  letzte Wiederauffrischungsgrenze
- Endstatus (`not-started` mit Grund / `window-open` /
  `restart-pending` / `success`)
- versionierte Nichtpersistenzaussage (`pressure-session-local-not-persisted-v1`,
  `persisted=false`, Umfänge Save/Load und Replay)
- im Interaktivmodus der HUD-Ausweis (`title-hud-pressure-window-v1`) und
  der Neustartkanalausweis (`pressure-restart-indicator-channel-v1`);
  headless und in vorzeitig beendeten Läufen ausdrücklich nicht gemessen
  mit maschinenlesbarem Grund statt stiller Behauptung.

Der Schemator prüft diese Felder relational fail-closed: die Zyklusnummer
je Instanz ist streng aufsteigend ab 1 und die Instanzzählung stimmt mit
der Zykluszählung; jede Instanz trägt entweder den ehrlichen Offenzustand
(ohne Endgrenze und Endgrund) oder genau einen Endgrund; die Endgrenze liegt
an oder nach der Startgrenze; die Ursachenkennung erscheint ausschließlich
mit Endgrund `expired`, Ankunftsgrenze und -modus ausschließlich mit
Endgrund `success`, und die Ankunft liegt innerhalb der Instanzgrenzen; ein
Fehlschlag existiert nur nach einer abgelaufenen Instanz, und die
Wiederauffrischungsgrenze liegt genau an der nächsten Vorgrenze nach der
Fehlschlagsgrenze; ohne wirksame Entscheidung existiert keine Instanz, und
der Endstatus trägt seinen ehrlichen Grund; `gateCoupled=false` überall.

Der Headless-Druck-Flow läuft über denselben öffentlichen Befehl und
dasselbe unveränderte Skriptformat `graybox-input-script-v3`; zwei
unabhängige Fresh-Prozessläufe sind builderidentisch, ein fremder Seed
ändert Start- und Endhash nachweislich, niemals aber das Druckprotokoll
(reine Funktion aus Sitzungszustand, Modus-/Ankunftsgrenzen und
Fensterinstanzen), und die Legacyschemata (Schemaversion 2 ohne Aktivierung,
Schemaversion 3 mit `--exploration` allein, Schemaversion 4 mit
`--decision`) bleiben byteidentisch gültig.

**Maschinenlesbare Nichtpersistenz (`pressure-session-local-not-persisted-v1`):**
Fenster, Fehlschlag, Zyklus und Neustart sind sitzungslokal — ein Lauf,
kein Fortsetzungsanspruch. Sie sind weder in Save/Load (T-031, Savevertrag
V1) noch in Replay oder Soak fortgesetzt, in keinem Persistenzvertrag
enthalten und ihre Persistenz ist einer späteren Savevertrags-Erweiterung
vorbehalten (ADR 008, Sequenzierungsnote). Schreibzugriffe des Laufs
bleiben auf die vertraglich erlaubten Verzeichnisse (Reportpfad, opt-in
Abgriff) beschränkt.

## 9. Beobachtungstreue (Kernabnahmekriterium)

Die Druck-, Fehlschlags- und Neustartschicht erzeugt zu keinem Zeitpunkt
einen Kernbefehl, verändert keine Befehlszustände und ist zu keinem
Zeitpunkt Teil des Simulationszustands oder Hashes. Ein Zwilling ohne
Druckaktivierung erzeugt bei identischer Intentfolge byteidentische Ketten
und denselben Endhash. Der T-035-Vollfluss und das A/B-Wahlpaar (identische
Kernintents und identischer Horizont) bleiben ketten- und endhashidentisch
und erhalten rein additive Druckfelder mit Erfolgsausweis innerhalb des
offenen Fensters. `Riftward.Simulation` bleibt gegen den Vorblob
byteidentisch; die Legacyskripte v1/v2/v3 und die Erkundungsfixtures bleiben
mit identischen Ketten und Endhashs gültig; es gibt keine neuen
Exitcodebedeutungen. Die Beobachtungsordnung an jeder Vorgrenze ist
weiterhin fixiert und wird ausschließlich am Ende ergänzt: Intents,
Erkundungsbeobachtung (T-034), Entscheidungsbeobachtung (T-035),
Druckbeobachtung (T-036).

## 10. Opt-in Abgriff des Fehlschlags-/Neustartzustands

Höchstens ein einzelner opt-in Abgriff folgt unverändert dem bestehenden
T-023- bis T-035-Muster: Nur mit `--capture-frame PFAD`, strikt nach dem
Messfenster, über demselben gebundenen Weltzustand; es entsteht **kein**
neuer Abgriffpfad, kein zweiter Abgriff und keine neue Dateibenennung. Bei
Druckaktivierung zeigt der persönliche Abgriff den Fehlschlags-/
Neustartzustand, sofern der gebundene Weltzustand in diesem Zeitraum liegt;
die maschinenlesbare Aussagegrenze bleibt
`graybox-state-occupancy-not-gameplay-atmosphere-or-shipping` (Graybox-
Zustandsbelegung — niemals Gameplay-, Atmosphären- oder Shipping-Beleg;
öffentliche Verwendung nur über `docs/communication/MEDIA_LAB.md` plus
Projektleitungsautorisierung). Ohne Flag entsteht keine Datei; ein
fehlgeschlagener Abgriff ergibt die bestehenden Codes 38/36 mit
`captured=false` und Grund.

## 11. Vorregistriertes Playtestprotokoll

Vollständiges Protokoll einer Displaysession (Entwickler-PC, gegebenenfalls
virtuelles Wayland nach T-023-Präzedenz), vor der Implementierung
registriert:

1. **Fensterlesbarkeit:** Nach wirksamer Wahl zeigt der Titel in beiden
   Modi das offene Fenster mit Zyklusnummer und Restzeit binnen 2 s ohne
   Tastendruck (Abschnitte 2, 3 und 6).
2. **Pressung:** ≥ 80 % der Tester empfinden die Frist als erkennbare
   Zeitpressung; die Restzeit ist fortlaufend ablesbar.
3. **Fehlschlagverständnis:** Der Ablauf zeigt binnen 2 s die
   unterscheidbare Fehlschlagsanzeige mit Ursache (Zwei-Kanal, Form plus
   Farbe); Missverständnisrate < 10 %.
4. **Neustartverständnis:** Das wiederaufgefrischte Angebot wird als neue
   Chance erkannt, nicht als Weltverlust; die Zyklusnummer im Titel steigt
   erkennbar; keine unklare Softlock-Situation.
5. **Erfolgserkennung:** Die persönliche Ankunft innerhalb des offenen
   Fensters schließt den Zyklus sichtbar als Erfolg ab (Titel `Erfolg`,
   Markerzustand); eine zweite Ankunft zählt nicht erneut.
6. **Beobachtungstreue:** Strategische Phasen bleiben unverändert
   bedienbar; kein Befehlspuls und keine Weltänderung geht von Fenster,
   Fehlschlag oder Neustart aus.

Ausführung: dokumentiert im Abnahmelauf; ist kein Display verfügbar,
bleiben Interaktivsmoke und Playtestausführung ausgewiesene Restpunkte mit
kontrolliertem Code-19-Nachweis ohne Simulation (Präzedenz
T-023/T-032/T-033/T-034/T-035).

## 12. Exitcodes

Die bestehenden Bedeutungen bleiben unverändert (insbesondere 19, 27, 28,
35–38 und 2/4): Der Druckmechanismus erzeugt **keine** neuen
Exitcodebedeutungen. Der nicht aktivierte Lauf verhält sich wie der
Bestandsstand; `--pressure` ohne `--decision` nutzt die bestehende
Usage-Bedeutung (2); die aktivierte Druckschicht ist reguläre fachliche
Diagnostik (Reportfelder mit `gateCoupled=false`), kein Fehlerzustand, und
sie koppelt das Gateverdict nicht. Der stabile Exitcode-Mapping-Test wird
ohne neue Bedeutung erweitert (Schemaversion-Auswahl 2/3/4/5 ist kein
Exitcode).

## 13. Offenheiten und Grenzen

Dieser Vertrag antwortet auf keine offene Produktfrage. Ausdrücklich offen
bleiben: Q-GAM-001 bis Q-GAM-007 (Kreativentscheidungen), Q-GAM-010 (finale
Wechsel-Detailregel), Q-NAR-002 (Erzählung) und Q-NAR-004 (Questoptionen mit
identitätsbestimmenden Folgen) sowie Q-TEC-004 (Simulationsvertrag-
Ratifizierung), Q-TEC-006 (produktives Replay-, Cooked-Paket- und
Definitionsformat), Q-TEC-010 (tolerierte Benchmarkstreuung), Q-OPS-001
(Referenzhardware; Pflichtprofile bleiben `NOT-MEASURED`). Es gibt keinen
Fog of War, keine Minimap, keine Aufklärungs- oder Sichtbarkeitssemantik
(GS-007 bleibt unberührt), keine Audio- oder Shipping-Assetaussage
(Q-TEC-007, Q-AST-001/Q-AST-002), keine Persistenz in jeder Form, keinen
Out-of-Session-Neustart (Hauptmenü, Neues Spiel, Prozessneustart,
Weltneuaufbau — der Neustart dieses Vertrags ist ausschließlich der
sitzungslokale Auftragszyklus), keinen Windows-/macOS-Scope (T-011,
Q-OPS-002/Q-OPS-003) und keine Budgetänderung jeder Art. GAME_DESIGN.md und
ANFORDERUNGEN.md bleiben durch die Implementierung unberührt;
`docs/MODEVERTRAG.md`, `docs/KOMMANDOVERTRAG.md` und
`docs/ERKUNDUNGSVERTRAG.md` bleiben byteidentisch;
`docs/ENTSCHEIDUNGSVERTRAG.md` ändert sich ausschließlich durch die
autorisierte additive Zyklus-Präzisierung (V2, Abschnitt 13);
`docs/ARCHITEKTUR.md` hält die sitzungslokale Druck-, Fehlschlags- und
Neustartsemantik in den Laufzeitverträgen fest; `docs/AUTOMATION.md` bildet
die Aktivierung und die Reportfelder ab.
