# Abschlussvertrag (T-039, Abschnitt 0)

**Vertragsversion:** 1
**Status:** Durch den gatenden Vertragsspike des Auftrags
`.ai/tasks/T-039-graybox-completion-repeat.json` vor der Implementierung
festgelegt; die maschinenlesbaren Kennungen sind in
`src/Riftward.Session/MissionContract.cs` gespiegelt und werden von einem
Test gegen dieses Dokument gehalten. Die autorisierten additiven
Präzisierungen der berührten Verträge sind als versionierte Zusatzabschnitte
dokumentiert: Erkundungsvertrag V3 (Abschnitt 12), Entscheidungsvertrag V4
(Abschnitt 15), Druckvertrag V3 (Abschnitt 15), Savevertrag V3 (Abschnitt 15)
und Kommandovertrag Abschnitt 13 (Keymap-Präzisierung in der bestehenden
Vertragsversion nach T-033-Abschnitt-12-Präzedenz).

Dieser Vertrag entscheidet die Abschluss- und Wiederholungsdetails des
kleinsten spielbaren Abschluss- und Wiederholungsschritts verfahrensmäßig
nach der Spike-Klausel (`docs/QUALITAET.md`, Definition of Ready). Jede Wahl
nennt Alternativen, Gründe, ein messbares Playtestkriterium und einen
Rückrollweg (ADR 007). Er antwortet auf keine offene Produktfrage:
Q-GAM-001 bis Q-GAM-007, Q-GAM-010, Q-NAR-002 und Q-NAR-004 bleiben
ausdrücklich `OFFEN`; Q-TEC-004 (Simulationsvertrag-Ratifizierung), Q-TEC-006
(produktives Replayformat) und Q-TEC-010 bleiben `OFFEN`; Q-OPS-001 folgt der
protokollierten T-020- bis T-023-Behandlung. Der Abschluss ist ein
Zustandsausweis der Sitzungsschicht, keine Erzählung: Es entstehen keinerlei
Nachhall-, Belohnungs-, Dialog- oder Contentsemantik, und die
Hauptmenü-Rückkehr von UF-001 Schritt 9 bleibt ausdrücklich
Out-of-Session und zurückgestellt.

## 1. Geltungsbereich und Produktform

Dieser Vertrag implementiert den noch fehlenden Erfolgspfad-Abschluss der
UF-001-Abschlussphase (Schritt 9: ruhiger Abschlusszustand, Wiederholen; die
Hauptmenü-Rückkehr bleibt ausgeschlossen) über dem abgenommenen
T-032-/T-033-Kern und der Kette T-034 bis T-038: Endet der aktuelle
Auftragszyklus in Erfolg (persönliche Ankunft des Vertragshelden in der
Folgenzone innerhalb des offenen Druckfensters gemäß Entscheidungsvertrag
Abschnitt 5 und Druckvertrag Abschnitt 5), tritt die Sitzung in einen
definierten, abgeleiteten Abschlusszustand (Auftrag abgeschlossen), der in
beiden Modi ohne Tastendruck über das bestehende additive Titel-HUD-Muster
und die bestehenden Markerkanaele ruhig sichtbar ist. Eine neue
ausdrückliche Wiederholen-Aktion setzt an einer Vorgrenze die gesamte
sitzungslokale Kette deterministisch zurück (Aufsuchprotokoll und
Erkundungsfortschritt, Entscheidungsangebot/Wahl/Folge, Druckfenster und
Zyklen), ohne Welt-, Simulations-, Kernbefehls- oder Hashänderung (ADR 008:
kein Welt- oder Auftragsneustart aus der Simulation; die Simulation läuft
unverändert weiter), und die Kette durchläuft erneut: die Erkundung öffnet
sich wieder (0/6, Registrierung ausschließlich nach dem definierten Reset
erneut möglich), das Angebot öffnet am neuen Erkundungsabschluss und leitet
seine beiden Optionen erneut als reine Funktion des neuen Aufsuchprotokolls
ab (abweichende Aufsuchfolge kann zu abweichenden Optionen führen —
Wiederholvarianz ohne Content).

Die gesamte Abschluss- und Wiederholungsschicht ist rein sitzungsseitige
Beobachtung und Semantik an der Vorgrenze: Sie erzeugt niemals einen
Kernbefehl, verändert keinen Befehlszustand, liest ausschließlich die
bestehenden Schichtwahrheiten und den wirksamen Sitzungsmodus
schreibgeschützt und ist zu keinem Zeitpunkt Teil des Simulationszustands
oder Hashes. `Riftward.Simulation` bleibt gegen den Vorblob byteidentisch
(Blobvergleich als Run-Evidenz und Testbindung). Die Schicht ist
ausdrücklich kein Out-of-Session-Neustart: Hauptmenü, Neues Spiel,
Wiederholen außerhalb der laufenden Sitzung, Prozessneustart-Orchestrierung,
Weltneuaufbau und Sitzungslöschung bleiben ausgeschlossen; der
Wiederholungsneustart dieses Vertrags ist ausschließlich der
sitzungslokale Kettenneustart innerhalb des laufenden Prozesses.

**Rückrollweg (gesamt):** Der Abschlussvertrag wird als V1 versioniert; jede
Änderung einer Wahl dieses Dokuments erfordert Vertragsversion 2 mit
Fixture-Regeneration. Die gesamte Schicht lebt ausschließlich in
`Riftward.Session`, der Reportlinie und der darstellseitigen Verdrahtung;
ein Rückbau entfernt diese Schicht (`--mission`-Aktivierung,
`missionSession`-Reportblock, Titel-HUD-Abschnitt, Keymap-Aktion), ohne den
Simulationskern, einen bestehenden Vertrag im Übrigen oder die additive
Sektionsfläche des Savevertrags zu berühren.

## 2. Abschlussableitung (`derived-completion-state-pure-function-v1`, reversible Produktfrage)

**Wahl: der Abschlusszustand ist eine abgeleitete, reine Funktion der
bestehenden Schichtwahrheiten ohne neue persistenzpflichtige
Abschlussbytes.** Der abgeleitete Abschlusszustand gilt an einer
Auswertungsgrenze genau dann, wenn alle drei bestehenden Wahrheiten der
aktuellen Kette gleichzeitig gelten: (i) der Druckendstatus des aktuellen
Zyklus ist `success` (Druckvertrag Abschnitt 8 — die letzte geschlossene
Fensterinstanz endete mit Endgrund `success`), (ii) die Entscheidungsschicht
trägt den abgeschlossenen Folgezustand (`followUpCompleted`,
Entscheidungsvertrag Abschnitt 5) und (iii) die Erkundungsschicht trägt den
abgeschlossenen Auftrag (`completed`, Erkundungsvertrag Abschnitt 4). Die
Ableitung ist total über den drei Schichtwahrheiten; sie ist rein
sitzungsseitig, erzeugt niemals einen Kernbefehl und ist nie Teil des
Simulationszustands oder Hashes.

Die **Abschlussgrenze** ist die erste Auswertungsgrenze der aktuellen Kette,
an der die abgeleitete Funktion gilt
(`derived-completion-first-boundary-observation-v1`); sie wird als
beobachtete Grenze maschinenlesbar ausgewiesen, ist eine Laufoptik der
aktuellen Kette und trägt kein persistenzpflichtiges Byte. Ohne Erfolg
innerhalb des Laufs trägt der Report den ehrlichen, maschinenlesbaren
Zustand `open` mit Grund `no-cycle-success-within-run` statt stiller Leere.

**Alternativen:**

| Alternative | Bewertung |
|---|---|
| **A: abgeleitete reine Funktion der Schichtwahrheiten (Empfehlung)** | Kein redundanter Zustand: Die drei Schichtwahrheiten existieren bereits, sind in Save/Load fortsetzbar (Savevertrag V2 Abschnitte 13.1/13.6) und können nach dem Laden nicht von der Ableitung abweichen; ein zweiter Abschlusszustand könnte gegen die Schichten driften und bräuchte eigene Konsistenz-/Migrationsfläche. Nachteil: der Abschlusszustand ist ohne die Schichten nicht aussprechbar (die Kopplung an `--exploration/--decision/--pressure` ist ohnehin vertraglich). |
| B: explizites neues Abschlussflag als Sitzungswahrheit | Eigene Persistenzbytes, eigene Verletzungsklassen, eigene Driftprävention gegen die drei Schichten; der Informationsgewinn gegenüber der totalen Ableitung ist null, weil die Ableitung dieselbe Aussage aus den fortgesetzten Schichten erzeugt. Als dokumentierte Alternative mit Playtestkriterium (Abschlusszustand bleibt nach Laden und nach Fehlerinjektion in jeder Schicht konsistent) und Rückrollweg erhalten. |
| Verworfen: druckgetragener Abschluss ohne Eigenständigkeit | Der Abschluss ist eine Kettenebene über drei Schichten; eine druckgetragene Lesart würde die Erkundungs- und Entscheidungswahrheiten still vorraussetzen, ohne sie zu nennen, und die Vertragsgrenze der Druckschicht (T-036: „ohne Folgezyklus nach Erfolg") unzulässig umdeuten. |

**Playtestkriterien:** Der Abschlusszustand ist in beiden Modi ohne
Tastendruck binnen 2 Sekunden ablesbar; nach dem Laden eines in der
Abschlusslage gespeicherten Slots zeigt der Titel denselben Abschlusszustand
ohne erneuten Erfolgsnachweis. **Rückrollweg:** Wechsel zu Alternative B ist
eine Vertragsversion 2 mit Fixture-Regeneration; die abgeleitete Form
entsteht ohne beständige Felder und kann ohne Datenträgermigrationsrest
entfernt werden.

## 3. Wiederholen-Aktivierungsform (`script-v4-plus-keymap-repeat-action-v1`, reversible Produktfrage)

**Wahl: parameterlose, sitzungsseitige Aktion `repeat` als neue
Skriptgrammatik `graybox-input-script-v4` als strikte Obermenge von v3 mit
erweiterter kanonischer Intentordnung plus genau einer frei belegbaren
Keymap-Aktion `repeat-mission` in der bestehenden Familie gemäß
Kommandovertrag Abschnitt 9 und dessen autorisierter Abschnitt-13-
Präzisierung.**

- Die v4-Grammatik kennt ausschließlich die neue Aktion `repeat` (ohne
  Parameter) zusätzlich zur v3-Verbmenge; die Bestandsskripte v1/v2/v3
  bleiben byteidentisch gültig, und `repeat` unter einem v1-/v2-/v3-Kopf ist
  `UnknownAction` mit bestehender Bedeutung (keine stille Formatdrift
  innerhalb einer Version). Der Kopf trägt die Kennung
  `graybox-input-script-v4`.
- Die kanonische Intentordnung erweitert die bestehende Festbreitenordnung
  um die Intentart `repeat` als kanonisch letzte Art ihres Ticks (Ordnungswert
  8 nach `choose-b` = 7); die Festbreitenkodierung des Intents bleibt
  unverändert 21 Bytes. Kontextfrei ist `repeat` grammatisch gültig; der
  Abschlusszustand entscheidet erst die Pipeline an der Vorgrenze.
- Die Keymap-Aktion `repeat-mission` erhält die Standardbelegung F7
  (Scancode 64, im Bestandsstand unbesetzt); die Validierungsregeln des
  Kommandovertrags Abschnitt 9 (mindestens eine Bindung je Aktion, keine
  Doppelbindungen, keine unbekannten Namen) gelten unverändert; die
  Maussemantik bleibt unverändert umbelegbar-nie. Der interaktive Impuls
  erzeugt an der laufenden Vorgrenze einen Live-`repeat`-Intent; die
  Abweisung ist dort sichtbar mit ihrer vertraglichen Kennung
  (UF-001-Fehlerzeile) und verändert sichtbar nichts.

**Wirksamkeit (`mission-repeat-completion-only-v1`):** Die Wiederholen-Aktion
ist ausschließlich im abgeleiteten Abschlusszustand wirksam. Weil die
Abschlussableitung während der Intentauswertung einer Vorgrenze die
Schichtwahrheiten der vorherigen Auswertung liest (Intents können die drei
Schichtwahrheiten derselben Vorgrenze nicht neu setzen — Folgeabschluss und
Fenstererfolg entstehen erst in der Beobachtungsordnung nach den Intents),
liegt die früheste wirksame Wiederholung an der ersten Vorgrenze **nach** der
Erfolgsgrenze (spiegelbildlich zur Wiederauffrischungsordnung des
Druckvertrags Abschnitt 4). Der Kettenneustart tritt an dieser Vorgrenze in
Kraft; die Beobachtungen derselben Vorgrenze laufen gegen die neue Kette
(die Erkundung ist wieder geöffnet; eine sofortige Registrierung an
derselben Vorgrenze ist zulässig und deterministisch). Eine Wiederholen-Aktion
vor dem Abschluss wird mit der unterscheidbaren, maschinenlesbaren Klasse
`mission-repeat-before-completion` abgewiesen und verändert nachweislich
nichts (kein Schichtzustand, kein Zählerstand außer dem Abweisungszähler,
keine Weltänderung); Wiederholen im Zustand vor jeder Schichtaktivierung
erhält die bestehende Klasse der fehlenden Aktivierung (`mission-not-activated`,
Auswertungsordnung Stufe 1).

**Alternativen:**

| Alternative | Bewertung |
|---|---|
| **A: Skriptstufe v4 plus Keymap-Aktion (Empfehlung)** | Der headless Flow ist vertraglich skriptgetrieben; ohne Skriptstufe wäre der Nachweispfad auf Interaktivläufe beschränkt und die Zwei-Ketten-Evidenz nicht deterministisch reproduzierbar. Nachteil: eine neue Grammatikstufe mit Fixturefläche (bewusst auf genau ein Verb begrenzt). |
| B: Keymap-Aktion ohne Skriptstufe | Verwirft die skriptgetriebene Evidenzlinie; der Abschluss-/Wiederholungspfad wäre headless unerreichbar. Als dokumentierte Alternative mit Playtestkriterium (Wiederholen binnen 2 s gefunden) und Rückrollweg erhalten. |
| Verworfen: automatische Wiederauffrischung des Angebots nach Erfolg | Löst den definierten Abschlusszustand auf (der Erfolg würde still zum Fehlschlagspfad) und erzeugt degenerierte, optionsidentische Zyklen ohne Spielerentscheidung; der Auftrag verlangt ausdrücklich die neue ausdrückliche Wiederholen-Aktion. |

**Playtestkriterien:** ≥ 90 % der Tester verstehen Wiederholen als
Auftragswiederholung (neue Kette ab Erkundung), nicht als Weltverlust; die
Aktion ist binnen 2 s auffindbar; eine vor dem Abschluss gedrückte Aktion
erzeugt eine sichtbare, unterscheidbare Abweisung ohne sichtbare Folge.
**Rückrollweg:** Belegung und Skriptstufe sind Hypothesenkonstanten; Wechsel
zu B über Vertragsversion 2 mit Fixture-Regeneration, ohne Kernänderung.

## 4. Reset-Umfang (`full-chain-restart-including-visit-protocol-v1`, reversible Produktfrage)

**Wahl: vollständiger Kettenneustart einschließlich Aufsuchprotokoll.** Die
wirksame Wiederholen-Aktion setzt an ihrer Vorgrenze die gesamte
sitzungslokale Kettewahrheit kontrolliert zurück: (i) Erkundung —
Registrierungszustand aller Landmarken, Aufsuchprotokoll und Fortschritt
(0/6); (ii) Entscheidung — Angebot, Wahl, Folge und Ankunft; (iii) Druck —
Fensterinstanzen, Zykluszählung, letzter Fehlschlag und
Wiederauffrischungspendenz. Die Kettenlaufzählung erhöht sich um genau eins
(Abschnitt 5). Der Sitzungsmodus bleibt unverändert (Sitzungszustand, nicht
Kettewahrheit; der Vertragsheld behält Position und Kernpfad), und die
Sitzungsabweisungszähler der Schichten bleiben unverändert
Sitzungsgesamtwerte (Präzedenz Druckvertrag Abschnitt 4). Die Simulation
läuft unverändert weiter; es gibt keinen Welt-, Kern- oder Hashzustand und
keinen Kernbefehl durch den Reset (ADR 008).

**Alternativen:**

| Alternative | Bewertung |
|---|---|
| **A: vollständiger Kettenneustart inklusive Aufsuchprotokoll (Empfehlung)** | Wiederholvarianz ohne Content: Die Optionsableitung bleibt die reine Funktion des Aufsuchprotokolls; eine abweichende Aufsuchfolge der neuen Kette kann zu abweichenden Optionen führen, und die Erkundungsschleife bleibt der spielbare Anfang der Kette (UF-001-Kernschleife). Nachteil: die Wiederholung beginnt beim längsten Kettenglied. |
| B: nur Entscheidungs-/Druckreset bei erhaltenem Aufsuchprotokoll | Die Optionsableitung würde aus dem unveränderten Protokoll exakt dieselben zwei Optionen erzeugen — jede Wiederholung wäre optionsidentisch, und die Erkundung als Spielanfang würde umgangen; der Wiederholbegriff von UF-001 Schritt 9 („Auftrag wiederholen") verlangt die volle Kette. Als dokumentierte Alternative mit Playtestkriterium (Wahrnehmung der Optionenvarianz) und Rückrollweg erhalten. |
| Verworfen: jeder Welt- oder Prozessneustart aus der Schicht | Verletzt ADR 008 (kein Weltneustart aus der Simulation) und die Auftragsgrenze (Out-of-Session-Neustartsemantik ausgeschlossen); die Variante wird nicht als Hypothese geführt. |

**Playtestkriterien:** Nach dem Wiederholen zeigt der Titel die neue Kette
(Erkundung 0/6, kein Angebot, kein Fenster) in beiden Modi ohne Tastendruck;
die neue Kette ist mit abweichender Aufsuchfolge durchspielbar und kann zu
abweichenden Optionen führen. **Rückrollweg:** Wechsel zu Alternative B über
Vertragsversion 2 mit Fixture-Regeneration; die Resetgrenzen sind
Schichtkonstanten ohne Kernelberührung.

## 5. Persistenzwahrheit des Kettenlaufs (`mission-chain-run-counter-persisted-v1`, reversible Produktfrage)

**Wahl: additive, versionierte Kettenlauf-Anzahl als Feld der bestehenden
additiven Sitzungssektion gemäß Savevertrag V3 (Abschnitt 15) mit
Fixture-Regeneration.** Die Sitzungssektion erhält die zwei additiven Felder
`MissionActive` (Aktivierungskennung der Schicht) und
`MissionChainRunCount` (Kettenlauf-Anzahl, beginnt bei 1, erhöht sich je
wirksamer Wiederholung um genau eins) als **Sektionsversion 2**; die
Bestandsslots der Sektionsversion 1 laden unverändert mit ehrlicher,
maschinenlesbarer Missionsleere
(`legacy-section-v1-mission-emptiness-v3`) ohne Migrationserfindung. Die
abgeleitete Abschlusswahrheit selbst trägt kein persistenzpflichtiges Byte
(Abschnitt 2); sie ist nach dem Laden aus den fortgesetzten
Schichtwahrheiten erneut ableitbar. Die ausdrückliche Replay-Ausnahme bleibt
bestehen: Replay und Soak setzen die Kettenlaufwahrheit nicht fort
(`replay=not-continued`).

**Alternativen:**

| Alternative | Bewertung |
|---|---|
| **A: additive Kettenlaufzählung in der bestehenden Sektion (Empfehlung)** | Die Wiederholungszählung ist die ehrliche Persistenzwahrheit der Kette: Ohne sie könnte eine fortgesetzte Sitzung Kettenlauf 1 nicht von Kettenlauf n unterscheiden; die Sektion ist die bestehende, vollständig geprüfte Persistenzfläche (T-031-Prüfklassen), und die Erweiterung kostet genau zwei Felder mit Sektionsversion 2. Nachteil: eine Sektionsversionserhöhung mit Legacy-Kompatibilitätsfläche (bewusst nach dem V1-Slot-Präzedenzmuster gehalten). |
| B: abgeleiteter Abschluss ohne Kettenlaufzähler mit ehrlicher, maschinenlesbarer Nichtpersistenz | Keine Formatfläche, aber die Kettenlaufzählung wäre nach dem Prozessende ehrlich verloren; die Fortsetzungsidentität der Kette („Wiederholung überlebt den Prozessneustart") wäre nicht bindbar. Als dokumentierte Alternative mit Playtestkriterium (Zählerverlust ist nachweisbar unsichtbar) und Rückrollweg erhalten. |

**Playtestkriterien:** Nach dem Speichern in Kettenlauf 2 und dem Laden in
einem frischen Prozess zeigt der Report dieselbe Kettenlauf-Anzahl; ein Slot
der Sektionsversion 1 lädt unverändert mit ehrlicher Missionsleere.
**Rückrollweg:** Umkehr auf ehrliche Nichtpersistenz (Alternative B) durch
Vertragsversion 2 mit Fixture-Regeneration; die Sektion trägt dann wieder
Sektionsversion 2 ohne die Felder bzw. eine ehrliche Missionsleere.

## 6. Feedback in beiden Modi (`title-hud-mission-completion-v1`)

**Wahl:** Ein additiver, darstellseitiger Titel-HUD-Abschnitt über der
bestehenden Zweikanal-Indikator-Regel NF-005 (ANFORDERUNGEN.md), ohne
Tastendruck in beiden Modi ablesbar, niemals Teil von Simulationszustand
oder Hash: Die bestehende Titelzeile (inklusive der T-034- bis
T-036-Segmente) erhält ausschließlich bei Missionsaktivierung genau einen
additiven, unterscheidbaren Abschluss-Segment in fester Form:

- abgeleiteter Abschlusszustand: ` — Auftrag: abgeschlossen`
- sonst (offene Kette): kein Missions-Segment (die bestehenden Segmente
  tragen die offene Kette wahrheitsgetreu; nach dem Kettenneustart weisen
  sie die neue Kette mit Erkundung 0/6, keinem Angebot und keinem Fenster
  aus)

Ohne Missionsaktivierung bleibt die Titelzeile byteidentisch zum
T-036-Stand. Kennung `title-hud-mission-completion-v1`; Lesezeit ≤ 2 s.
Die Kontextabweisung der Wiederholen-Aktion folgt der fixierten Hypothese
`context-visible-rejection-v1` des Kommandovertrags Abschnitt 12: ein
kontextfalscher Impuls erhält die kontextierte, maschinenlesbare
UF-001-Fehlerzeile mit der Kennung `mission-repeat-before-completion` am
Live-Pfad und erhöht den Reportzähler, ohne Welt-, Ketten- oder
Kernänderung.

**Alternativen:** gerenderte Abschlussfläche mit Belohnungsdarstellung
(Nachhall-/Contentsemantik — ausdrücklich ausgeschlossen); reine
Farbcodierung des Abschlusses (NF-005-Verstoß — abgelehnt); eigenes
Abschluss-Overlay als neue Renderfläche (späterer Slice nach
Modevertrag-Abschnitt-8-Präzedenz). **Playtestkriterien:** Der Abschluss ist
in beiden Modi binnen 2 s ablesbar; die Abweisung ist von einer wirklosen
Taste unterscheidbar. **Rückrollweg:** Segment und Kennung sind
Hypothesenkonstanten der Darstellung; Austausch ohne Vertragspflicht,
solange die Zweikanal-Erkennbarkeit und die Reportbindung erhalten bleiben.

## 7. Aktivierungsform (`opt-in-mission-activation-v1`) und Reportlinie

**Aktivierung:** Opt-in über das neue Befehlsflag `--mission` des bestehenden
öffentlichen Befehls `kommandoschleife`, gekoppelt an `--pressure` (und damit
transitiv an `--decision` und `--exploration`): `--mission` ohne `--pressure`
ist eine Usage-Fehlanwendung (bestehender Exitcode 2, keine neue Bedeutung).
Die Skriptgrammatik wird um die v4-Stufe erweitert (Abschnitt 3); die
Schemaversionen des Reports sind strikt additiv gestaffelt: ohne Flags
byteidentischer Bestandsstand (Schemaversion 2); mit `--exploration` allein
Schemaversion 3; mit `--decision` Schemaversion 4; mit `--pressure`
Schemaversion 5; Save-/Ladeläufe Schemaversion 6; mit `--mission` rein
additive **Schemaversion 7** mit dem Pflichtblock `missionSession` —
ausschließlich neue Felder, keine Umdeutung, Umbenennung oder Entfernung
bestehender Felder; der Gatevertrag bleibt unberührt, alle neuen Felder
tragen `gateCoupled=false`. Save-/Ladeläufe mit Missionsaktivierung tragen
Schemaversion 7 mit dem Fortsetzungsblock; die Schichtaktivierungsgrenze
`layer-activation-mismatch` gilt unverändert für die Missionsaktivierung.

**Alternativen:** stets aktivierte Abschlussschicht (verletzt die
byteidentischen Bestandsreports — abgelehnt); separates Untercommand
(widerspricht dem Auftrag: derselbe öffentliche Befehl und derselbe
Pipelinepfad — abgelehnt); `--mission` ohne Kopplung an `--pressure`
(erzeugt einen Abschlussausweis ohne seinen vertraglichen Auslöserträger —
abgelehnt). **Rückrollweg:** Flag und Reportblock entfernen; ohne Flag ist
der Stand byteidentisch zum Vorgänger.

## 8. Reportbindung (rein additive Schemaversion 7)

Bei Aktivierung bindet der Report unter `missionSession` (beide
Ausführungsarten; `gateCoupled=false` für sämtliche Mess- und Protokollfelder):

- Vertragsbindung (`contract`: Dokumentpfad und Version dieses Vertrags),
  Aktivierungs- und Modellkennungen (`opt-in-mission-activation-v1`,
  `derived-completion-state-pure-function-v1`,
  `derived-completion-first-boundary-observation-v1`,
  `script-v4-plus-keymap-repeat-action-v1`,
  `full-chain-restart-including-visit-protocol-v1`)
- Abschlussausweis (Zustand `completed`/`open` mit ehrlichem Grund
  `no-cycle-success-within-run` im Offenzustand, Abschlussgrenze der
  aktuellen Kette bzw. Sentinel)
- Kettenlaufzählung (`chainRunCount`, mindestens 1) samt Laufrundenzählung
  (`chainRunCountAtRunStart` — frisch 1, im Fortsetzungslauf der
  restaurierte Sektionswert; Grundlage der relationalen Protokollbindung)
- Wiederholungsprotokoll je Eintrag (Vorgrenze, Disposition `applied`/
  `rejected-before-completion`, Kettenlaufstand nach dem Eintrag)
- Abweisungszähler der vor dem Abschluss abgewiesenen Wiederholen-Aktionen
- versionierte Persistenzaussage
  (`mission-chain-run-counter-persisted-v1`, `persisted=true`,
  `saveLoad=continued`, `replay=not-continued`, ehrlicher Ausweis
  `completionStatePersisted=false` für die abgeleitete Abschlusswahrheit)
- im Interaktivmodus der HUD-Ausweis (`title-hud-mission-completion-v1`)
  und der Keymap-Ausweis (`repeat-mission`); headless und in vorzeitig
  beendeten Läufen ausdrücklich nicht gemessen mit maschinenlesbarem Grund
  statt stiller Behauptung.

Der Schemator prüft diese Felder relational fail-closed: der Abschluss
existiert nur nach einem Zykluserfolg der aktuellen Kette (die drei
Schichtwahrheiten des Abschlussblocks tragen dieselbe Aussage wie die
bestehenden Blöcke); im Offenzustand trägt der Abschluss seine Grenze nicht;
jede wirksame Wiederholung erhöht die Kettenlaufzählung um genau eins, und
die Kettenlaufstände des Protokolls sind relational zur Laufrundenzählung
konsistent (Kettenlaufzählung = Laufrundenzählung plus Anzahl wirksamer
Wiederholungen des Laufs); abgewiesene Wiederholungen verändern die Zählung
nicht; ohne Missionsaktivierung existiert kein Block; `gateCoupled=false`
überall.

Der Headless-Abschluss- und Wiederholungsflow läuft über denselben
öffentlichen Befehl und dasselbe v4-Skriptformat; zwei unabhängige
Fresh-Prozesspaare sind builderidentisch, ein fremder Seed ändert Start- und
Endhash nachweislich, niemals aber die Sitzungs- oder Abschlusswahrheit
(reine Funktion aus Sitzungszustand, Schichtwahrheiten und Modusgrenzen),
und die Legacyschemata (Schemaversionen 2 bis 6) bleiben byteidentisch
gültig.

## 9. Beobachtungstreue (Kernabnahmekriterium)

Die Abschluss- und Wiederholungsschicht erzeugt zu keinem Zeitpunkt einen
Kernbefehl, verändert keine Befehlszustände und ist zu keinem Zeitpunkt Teil
des Simulationszustands oder Hashes. Ein Zwilling ohne
Abschluss-/Wiederholungsaktivierung erzeugt bei identischer Intentfolge
byteidentische Ketten und denselben Endhash. Die Registrierungsänderung der
Erkundung ist ausschließlich durch den definierten Reset wirksam (jede
Landmarke registriert genau einmal je Kette, niemals doppelt innerhalb einer
Kette, keine stille Mehrfachzählung — Erkundungsvertrag V3, Abschnitt 12).
Die Legacy-Erkundungs-, Entscheidungs-, Druck- und Fortsetzungsfixtures
bleiben mit identischen Ketten und Endhashs gültig; die T-031-/T-037-Garantien
gelten uneingeschränkt für die additiven Sektionsfelder mit Fault-Injection
und unterscheidbaren Verletzungsklassen; Slotdateien gelten als untrusted und
werden vollständig validiert; Schreibzugriffe erfolgen ausschließlich in
vertraglich erlaubte Verzeichnisse; es gibt keinen Netzwerkzugriff, keine
Secrets und keine personenbezogenen Daten; der Sitzungskern referenziert
keine SDL3-, bgfx- oder Betriebssystemtypen; sämtliche neuen Reportfelder
tragen `gateCoupled=false`, und kein Budgetwert wird geändert. Die
Beobachtungsordnung an jeder Vorgrenze ist weiterhin fixiert und wird
ausschließlich am Ende ergänzt: Intents, Erkundungsbeobachtung (T-034),
Entscheidungsbeobachtung (T-035), Druckbeobachtung (T-036),
Abschlussbeobachtung (T-039).

## 10. Opt-in Abgriff

Höchstens ein einzelner opt-in Abgriff folgt unverändert dem bestehenden
T-023- bis T-038-Muster: Nur mit `--capture-frame PFAD`, strikt nach dem
Messfenster, über demselben gebundenen Weltzustand; es entsteht **kein**
neuer Abgriffpfad, kein zweiter Abgriff und keine neue Dateibenennung. Bei
Missionsaktivierung zeigt der persönliche Abgriff den abgeleiteten
Abschlusszustand, sofern der gebundene Weltzustand in diesem Zeitraum liegt;
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

1. **Abschlusslesbarkeit:** Nach dem Zykluserfolg zeigt der Titel in beiden
   Modi ohne Tastendruck binnen 2 s den Abschlussausweis
   ` — Auftrag: abgeschlossen` (Abschnitte 2 und 6).
2. **Abschlussverständnis:** ≥ 90 % der Tester lesen den Zustand als
   „Auftrag erledigt, nichts drückt mehr"; keine reine Farbcodierung.
3. **Wiederholen-Auffindbarkeit:** Die Wiederholen-Aktion (F7 bzw. belegte
   Taste) ist binnen 2 s auffindbar; ≥ 90 % verstehen sie als
   Auftragswiederholung (neue Kette ab Erkundung), nicht als Weltverlust
   (Missverständnisrate < 10 %).
4. **Kontextabweisung:** Eine vor dem Abschluss gedrückte Wiederholen-Aktion
   erzeugt die sichtbare, maschinenlesbare Abweisung
   `mission-repeat-before-completion` ohne sichtbare Welt- oder Kettefolge.
5. **Neue Kette:** Nach dem Wiederholen zeigt der Titel die neue Kette
   (Erkundung 0/6, kein Angebot, kein Fenster) in beiden Modi ohne
   Tastendruck; die neue Kette ist mit abweichender Aufsuchfolge
   durchspielbar und kann zu abweichenden Optionen führen.
6. **Fortsetzung:** Abschluss- und Kettenlaufwahrheit sind über die
   bestehenden Save-/Ladeaktionen fortsetzbar; nach dem Laden stimmen sie
   mit dem Zustand vor dem Prozessende überein; eine abgewiesene Ladung
   verändert sichtbar nichts.
7. **Beobachtungstreue:** Strategische und persönliche Bedienung bleiben
   unverändert; kein Befehlspuls und keine Weltänderung geht von Abschluss,
   Wiederholen oder Reset aus.

Ausführung: dokumentiert im Abnahmelauf; ist kein Display verfügbar, bleiben
Interaktivsmoke und Playtestausführung ausgewiesene Restpunkte mit
kontrolliertem Code-19-Nachweis ohne Simulation (Präzedenz
T-023/T-032/T-033/T-034/T-035/T-036/T-037).

## 12. Exitcodes

Die bestehenden Bedeutungen bleiben unverändert (insbesondere 19, 27, 28,
33–38 und 2/4): Der Abschluss- und Wiederholungsmechanismus erzeugt **keine**
neuen Exitcodebedeutungen. Der nicht aktivierte Lauf verhält sich wie der
Bestandsstand; `--mission` ohne `--pressure` nutzt die bestehende
Usage-Bedeutung (2); die aktivierte Schicht ist reguläre fachliche
Diagnostik (Reportfelder mit `gateCoupled=false`), kein Fehlerzustand, und
sie koppelt das Gateverdict nicht. Der stabile Exitcode-Mapping-Test wird
ohne neue Bedeutung erweitert (Schemaversion-Auswahl 2/3/4/5/6/7 ist kein
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
(Q-TEC-007, Q-AST-001/Q-AST-002), keine Out-of-Session-Neustartsemantik
(Hauptmenü, Neues Spiel, Prozessneustart, Weltneuaufbau — der Neustart
dieses Vertrags ist ausschließlich der sitzungslokale Kettenneustart), keine
Nachhall-, Erzähl- oder Belohnungssemantik, keinen Windows-/macOS-Scope
(T-011, Q-OPS-002/Q-OPS-003), keine Änderung am Paketvertrag oder
Paketverhalten (T-038) und keine Budgetänderung jeder Art.
GAME_DESIGN.md und ANFORDERUNGEN.md bleiben durch die Implementierung
unberührt; `docs/MODEVERTRAG.md` bleibt byteidentisch;
`docs/KOMMANDOVERTRAG.md` ändert sich ausschließlich durch die autorisierte
additive Keymap-Präzisierung (Abschnitt 13); `docs/ARCHITEKTUR.md` hält die
Abschluss-/Wiederholungsgrenze der Sitzungsschicht fest (niemals
Simulationszustand oder Hash, Persistenz nur über den Savevertrag, Replay
ausgenommen); `docs/AUTOMATION.md` bildet die Skriptstufe v4, die
Keymap-Aktion, die Aktivierung und die Reportfelder ab.
