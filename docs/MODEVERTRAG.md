# Modevertrag (T-033, Abschnitt 0)

**Vertragsversion:** 1
**Status:** Durch den gatenden Vertragsspike des Auftrags
`.ai/tasks/T-033-mode-switch-prototype.json` vor der Implementierung festgelegt;
die maschinenlesbaren Kennungen sind in
`src/Riftward.Session/ModeContract.cs` gespiegelt und werden von einem Test
gegen dieses Dokument gehalten.

Dieser Vertrag entscheidet die Wechseldetails des kleinsten Hybrid-Mode-Switch-
Prototyps verfahrensmäßig nach der Spike-Klausel (`docs/QUALITAET.md`,
Definition of Ready). Jede Wahl nennt Alternativen, Gründe, ein messbares
Playtestkriterium und einen Rückrollweg (ADR 007). Er antwortet auf keine
offene Produktfrage: Q-GAM-001 bis Q-GAM-007 und Q-NAR-002 bleiben
ausdrücklich `OFFEN`; die finale Wechsel-Detailregel Q-GAM-010 bleibt
`OFFEN`; Q-TEC-004/Q-TEC-006/Q-TEC-010 bleiben `OFFEN`; Q-OPS-001 folgt der
protokollierten T-020- bis T-023-Behandlung.

## 1. Geltungsbereich und Produktform

Dieser Vertrag implementiert die im Graybox-Stand prüfbaren Anteile der
akzeptierten Entscheidung „eine Welt, zwei Spielmodi" (ADR 008): einen
strategischen RTS-Modus (unveränderte T-032-Baseline) und einen persönlichen
Third-Person-Modus über demselben unveränderten Simulationskern
(`riftward-simworld-graybox-v1`, Simulationsvertrag V1). Der Modus ist
Sitzungszustand: Er ist niemals Teil des Simulationszustands oder Hashes, wird
an einer definierten Tickgrenze deterministisch aufgelöst und erzeugt aus sich
heraus keinen Kernbefehl. Beide Modi bilden ausschließlich auf die unveränderte
öffentliche Kernbefehlsfläche (`SimCommandKind.GroupMoveToZone`, kanonische
Ordnung `(Tick, ScopeGroup, Kind, ZoneIndex)`) ab. Es entstehen keinerlei
Kampf-, Wirtschafts-, Content-, Pausen-, Scheitern- oder Inhaltsregeln; die
Persistenzwahrheit des Modusflags in Save/Load und Replay bleibt einer
späteren Savevertrags-Erweiterung vorbehalten (ADR 008, Sequenzierungsnote zu
Kernaussage 4) und ist in diesem Slice nicht behauptet.

**Rückrollweg (gesamt):** Der Modevertrag wird als V1 versioniert; jede
Änderung einer Wahl dieses Dokuments erfordert Vertragsversion 2 mit
Fixture-Regeneration. Der Moduszustand lebt ausschließlich in
`Riftward.Session` und der darstellseitigen Verdrahtung; ein Rückbau entfernt
diese Schicht, ohne den Simulationskern zu berühren.

## 2. Heldenidentität (`session-hero-agent-index-0-group-0-v1`)

**Wahl:** Der *Vertragsheld* ist die stabile Sitzungsbezeichnung für den
Agentenindex 0 (`HeroAgentIndex = 0`); seine *Heldengruppe* ist die bestehende
Vertragsgruppe 0 (`HeroGroupIndex = 0`). Die Heldenauszeichnung ist eine neue,
versionierte **Sitzungsbezeichnung** dieses Vertrags — der Simulationskern
kennt keine Führungs- oder Heldensemantik. Codebeleg: Die Modulo-Zuordnung
`agent % GroupCount` in `Riftward.Simulation` platziert den Agentenindex 0 in
Gruppe 0, die 50 Agenten umfasst; die übrigen 49 Agenten der Gruppe 0 gelten
sitzungsseitig als autonom marschierende Begleiter. Der Kernbefehl des
persönlichen Modus ist immer `SimCommand(tick, 0, GroupMoveToZone, zone)`;
eine Agentengranularität existiert weiterhin nicht.

**Alternativen:**

| Alternative | Ablehnungsgrund |
|---|---|
| Heldkonstrukt im Simulationskern (eigene Heldentität, Führungsflag) | Kerneländerung ist verboten (Auftragsvertrag; ADR 008 Folge 6: die Simulation bleibt die einzige fachliche Wahrheit). Eine Führungssemantik würde den Befehlsvertrag V1 des Kerns erweitern und sämtliche Hashketten/Fixtures der T-021/T-022/T-032-Linie entwerten. |
| Sitzungsseitiger Scheinheld (eigene Heldfigur neben der Gruppe, Position nur darstellseitig) | Verletzte dieselbe ADR-008-Folge indirekt: Die dargestellte Figur wäre nicht dieselbe simulierte Entität; der Hybrid-Nachweis (dieselbe Welt, derselbe Akteur über alle Wechsel) wäre eine Darstellungsbehauptung ohne Simulationsträger. |
| Held als feste Spitze/Reihenfolge je Gruppe | Der Kern besitzt keine Ordnungssemantik innerhalb einer Gruppe (Stabile Indizes sind Adressierung, keine Rangfolge); eine Spitze wäre erfundene Kernbedeutung. |

**Playtestkriterium:** In Playtests erkennen Tester den Vertragshelden in
beiden Modi über den gebundenen Heldmarker und finden ihn nach einem
Moduswechsel binnen 2 Sekunden wieder; die gemeinsam marschierende Gruppe
wird als ein Verband begriffen („mein Held mit Begleitern"). **Rückrollweg:**
Die Sitzungsbezeichnung und die Markerbindung sind Sitzungszustand; Änderung
über Vertragsversion 2, ohne Kernelaenderung.

## 3. Steuerungsabbildung im persönlichen Modus (`hero-direction-steering-zones-v1`)

**Wahl:** Richtigungsgelenkte Zonenlenkung. Die Lenkeingaben (Skriptaktion
`steer <zoneIndex>` oder interaktiv die gebundenen Schwenktasten) erzeugen
ausschließlich Kernbefehle `SimCommand(tick, 0, GroupMoveToZone, zone)`. Die
skriptgebundene Lenkung nennt die Zielzone direkt; die interaktive Lenkung
löst die kamerarelative Himmelsrichtung (§4-Richtungskohärenz des
Kommandovertrags: `pan-up` = Norden (−Z), `pan-right` = Osten (+X), usw.)
deterministisch gegen die sechs Zonenzentren auf: Ziel ist die Zone mit dem
größten normierten Richtungstreue-Skalarprodukt `(Zentrum − Heldenposition) ·
Richtung`; ohne Richtungstreue-Kandidat (jedes Skalarprodukt ≤ 0) wird der
Impuls kontrolliert mit `steer-direction-without-zone` abgewiesen, ohne
Kernbefehl. Der Vertragskommandostrom ist dedupe-geregelt: Lenkt der Intent
auf die Zone, die die Heldengruppe am fraglichen Vorgrenzett bereits als
Kernziel trägt (`TargetZoneOfGroup(0)`), unterbleibt der Kernbefehl
(Ruhezustand ohne redundante Befehle, kein endlos pendelndes Order-Pingpong);
der Intent gilt als angewendet und erscheint als Ausweis `steerIdleDedupes`.
Jede durchgelassene Lenkung erzeugt exakt einen Kernbefehl auf Gruppe 0.

**Alternativen:**

| Alternative | Bewertung |
|---|---|
| **A: richtungsgelenkte Zonenlenkung (Empfehlung)** | Erhält die bestehende Richtungskohärenz (Kommandovertrag §4) über beide Modi, benötigt keine neue Belegkonkurrenz und bleibt deterministisch; Nachteil: Zonenauflösung ist grob (Graybox). |
| B: direkte Zonentasten (1–6 wählt Zone direkt) | Einfach deterministisch, aber sechs Zusatzbelegungen konkurrieren mit der bestehenden Keymapfamilie und brechen die kamerarelative Richtungsanmutung der Verfolgungskamera; als dokumentierte Alternative mit Playtestkriterium (Zonentaste innerhalb 1 s korrekt) erhalten. |
| C: kontinuierliche agentenpunktbasierte Steuerung (verworfen) | Erfordert neue Kernbefehlsarten und Agentengranularität im Kern — Kerneländerung, ausdrücklich out of scope; eine spätere kontinuierliche Heldensteuerung ist ein eigener Simulationsvertrag V2 mit Messbeleg. |

**Playtestkriterien:** In protokollierten Playtests lenken Tester in ≥ 80 %
der Versuche mit einer Korrektur die Gruppe zur beabsichtigten Nachbarzone;
die Lenkrichtung entspricht in ≥ 90 % der Versuche der Bildschirmrichtung;
die Steuerung ist binnen 2 Sekunden verstanden. **Rückrollweg:** Wechsel zu
Alternative B oder Änderung der Auflösungskonstanten über Vertragsversion 2
mit Fixture-Regeneration; Kameraparameter sind Hypothesenkonstanten
(Abschnitt 8).

## 4. Wechselauslöser, Übergang und kanonische Same-Tick-Regel

**Wechselauslöser (`mode-toggle-keymap-action-v1`):** Der Wechsel ist eine
frei belegbare, datengetriebene semantische Aktion `mode-switch` in der
bestehenden Keymapfamilie des Kommandovertrags Abschnitt 9; Standardbelegung
ist Tab (Scancode 43, unbesetzt im T-032-Stand). Im Skript ist der Wechsel die
Aktion `switch` (Abschnitt 6). Der Modus startet jeder Sitzung als
`strategisch` (T-032-Baseline-Kontinuität).

**Alternativen:**

| Alternative | Ablehnungsgrund |
|---|---|
| Kontextsensitive Doppelbelegung einer bestehenden Taste | Mischt Selektions- und Wechselsemantik auf einer Taste; widerspricht der Kontexttrennung (AC-T033-04) und verschlechtert die Moduserkennbarkeit. |
| Automatischer Moduswechsel bei Nähe/Aktion (verworfen) | Verbraucht die finale Wechsel-Detailregel (Q-GAM-010, OFFEN) und ist als Graybox-Hypothese nicht vorregistriert. |

**Übergangsregel (kanonische Same-Tick-Regel,
`same-tick-switch-last-effective-next-next-v1`):**

1. Ein Wechsel-Intent ist an seinen Tick `S` gebunden und wird an der
   Vorgrenze von `S` ausgewertet, **kanonisch nach allen anderen Intents
   desselben Ticks** (kanonische Intentordnung erweitert: `clear (0) < point
   (1) < box (2) < move (3) < steer (4) < switch (5)`; bei Gleichstand nach
   den Parametern).
2. Intents desselben Ticks `S` und alle Intents an der unmittelbar folgenden
   Gültigkeitsprüfung (Vorgrenze `S+1`) bleiben im **vorherigen Modus**
   gültig; die Modusänderung ist dort weder wirksam noch kontextbildend.
3. Der neue Modus wird erstmals an der **übernächsten** Gültigkeitsprüfung
   wirksam: Vorgrenze `M = S + 2`. Dort gilt der neue Modus für alle Intents
   dieser und aller folgenden Vorgrenzen.
4. Der Wechsel erzeugt aus sich heraus keinen Kernbefehl; er ist nie Teil von
   Simulationszustand oder Hash. Mehrere Wechsel an aufeinanderfolgenden
   Ticks sind wohldefiniert: Jeder Wechsel wird im für seine Auswertung
   gültigen Modus ausgewertet und kehrt dessen Wirkung deterministisch um.
   Wechsel an unmittelbar aufeinanderfolgenden Ticks `S` und `S+1` werden
   wegen Regel (2) beide im dann noch gültigen vorherigen Modus ausgewertet
   und tragen daher denselben Zielmodus; ihr Nettoeffekt ist genau ein
   Wechsel, der an `S + 2` wirksam wird. Ein Wechsel, dessen Wirksamkeits-
   grenze hinter dem Horizont läge, bleibt auswertbar und im Lauf unwirksam
   (`EffectiveInRun = false` im Protokoll); der Endmodus des Reports bildet
   die Wahrheit des Laufs ab.

**Begründung der Zweigrenzenregel:** Live-Intents, die zwischen `S` und `S+1`
anliegen, hätten ohne die feste Wartestufe eine von der Ereignisreihenfolge
abhängige Kontextzugehörigkeit; die Zweigrenzenregel macht die Kontext-
zugehörigkeit jedes Intents allein durch seinen gebundenen Tick bestimmt und
ist deshalb deterministisch prüfbar.

**Alternativen zur Same-Tick-Regel:** (a) Wechsel wirkt ab der nächsten
Gültigkeitsprüfung (`M = S+1`): minimal schneller, lässt aber die Kontext-
zugehörigkeit von Intents desselben Ticks von der internen Auswertungsreihen-
folge abhängen — abgelehnt; (b) Wechsel wirkt sofort an derselben Vorgrenze:
verletzt die vertragliche Kanonisierung (Intents desselben Ticks müssten nach
einem mittendrin wirkenden Wechsel zweigeteilt validiert werden) — abgelehnt.

**Reaktionsableitung:** Budgetzeile „Eingabe-zu-Reaktion"
(`docs/PERFORMANCE_BUDGET.md`) unverändert: Ziel 100 ms, harte Grenze
150 ms; vertragliche Tickrate 20 Hz ⇒ dt = 50 ms je Tick. Die
Wechselreaktion nutzt eine **eigene, arithmetikkompatible Zählbasis** analog
Kommandovertrag Abschnitt 6:

- Harte Tickgrenze: ⌊150 ms ÷ 50 ms/Tick⌋ = ⌊3,0⌋ = **3 Ticks**
- Zieltickgrenze: ⌊100 ms ÷ 50 ms/Tick⌋ = ⌊2,0⌋ = **2 Ticks**

**Definition:** Wechsel-Intent-Tick `S` ist der gebundene Absendettick;
`M` ist die erste Gültigkeitsprüfung im neuen Modus. `switchReactionTicks =
M − S`. Nach der kanonischen Same-Tick-Regel ist `M = S + 2`, also
`switchReactionTicks = 2` konstruktionsbedingt am Ziel; die harte Grenze
`max(switchReactionTicks) ≤ 3` entscheidet fail-closed (Kriterium 6 der
Gatematrix, Abschnitt 7). Das Kriterium ist wie die bestehende
Reaktionsmetrik (T-032-Präzedenz `V == S`) nur über seine Fault-Injection
falsifizierbar, nicht über die Pipeline; eine verzögerte Verbrauchssemantik
wäre eine Modevertrags-V2-Entscheidung mit Fixture-Regeneration. Verschärfung
bleibt zulässig; jede Lockerung eskaliert an die Projektleitung.

## 5. Modus-Scoping der Eingabesemantik und Kontexttrennung

**Wahl (`mode-scoping-v1`, autorisierte additive Präzisierung des
Kommandovertrags als dessen Abschnitt 12):** Strategischer Modus: die
Semantik der Kommandovertrags Abschnitte 2, 3 und 9 gilt unverändert
(Punktwahl, Rahmenwahl, Bewegung, Maussemantik, Kamera). Persönlicher Modus:
Auswahl- und strategische Bewegungssemantik sind **nicht gebunden**; die
Lenksemantik (Abschnitt 3) ist der einzige Befehlskanal; Zoom belegt die
Distanz der Verfolgungskamera; Zieh-Schwenken mit der mittleren Taste ist im
persönlichen Modus ohne Wirkung (die Kamera folgt dem Helden; ein
bodenverankerter Schwenk existiert dort nicht). Die Wechselaktion ist in
beiden Modi gültig. Strategische Intents (`clear`, `point`, `box`, `move`)
sind im persönlichen Modus und die persönliche Lenkung (`steer`) im
strategischen Modus vor der Kernübergabe mit unterscheidbaren, maschinen-
lesbaren Dispositionen abzuweisen: `strategy-intent-in-personal-mode` und
`steer-intent-in-strategy-mode` — ohne Kernbefehl, ohne Zustandsänderung,
ohne Prozessschaden. Im Interaktivpfad bedeutet „nicht erreichbar" messbar:
die gebundene Semantik kann im Fremdmodus weder Auswahlzustand noch
Kernbefehl noch Lenkbefehl auslösen (Strukturreview bindet die Abwesenheit
der Bindung).

**Interaktive Kontextabweisung (`context-visible-rejection-v1`):** Ein
kontextfalscher interaktiver Impuls (Mauswahl/‑befehl im persönlichen Modus)
erhält eine kontextierte, maschinenlesbare Abweisung am Live-Pfad
(UF-001-Fehlerzeile mit der Kennung) und erhöht den Reportzähler
`interactiveContextRejections`; er erzeugt niemals einen Kernübergabebefehl.

**Alternativen:**

| Alternative | Bewertung |
|---|---|
| **Sichtbare kontextierte Abweisung (Empfehlung)** | Beobachtbar statt still: Der Spieler erkennt Modusverwechslungen sofort; Nachteil: zusätzliche Fehlerzeilen bei Bedienfehlern. |
| Stummes Ignorieren kontextfalscher Impulse | Unauffällig, aber unprüfbar aus Spielersicht; Modusverwechslung bliebe unsichtbar und würde als „Kaputtsein" missdeutet. |
| Globaler Eingabespiegel (Fremdmodus-Befehle werden gepuffert und nach dem Wechsel ausgeführt) | Verletzt die Tickbindung und die Kontexttrennung; verschleppt Befehle über Wechselgrenzen — abgelehnt (Q-GAM-010 bleibt OFFEN). |

**Playtestkriterium:** ≥ 90 % der kontextfalschen Impulse werden in Playtests
als bewusste Abweisung verstanden (nicht als Defekt); mediane Irritations-
dauer ≤ 2 s. **Rückrollweg:** Wechsel zu stummem Ignorieren ist eine
Konstantenänderung mit Vertragsversion 2; die Skript- und Reportzähler
bleiben unverändert.

## 6. Eingabeskript-Diagnoseformat `graybox-input-script-v2`

**Wahl:** Neues Formatkennung `graybox-input-script-v2` als abwärtskompatible
**Obermengengrammatik** mit unveränderter Legacy-Grammatik:

```text
graybox-input-script-v2 <horizonTicks>
intent <tick> clear
intent <tick> point <xMm> <yMm>
intent <tick> box <x0Mm> <y0Mm> <x1Mm> <y1Mm>
intent <tick> move <zoneIndex>
intent <tick> steer <zoneIndex>
intent <tick> switch
end
```

- Die Kopfzeile `graybox-input-script-v1` behält **genau** die v1-Grammatik
  (Vier-Verbmenge); `switch`/`steer` unter einem v1-Kopf sind
  `UnknownAction` — keine stille Formatdrift innerhalb einer Version.
- Neue Aktionen: `steer <zoneIndex>` (Zonenbereich wie `move`) und `switch`
  (keine Parameter). Limits, Fensterregeln, Kanonisierung, Ablehnungsklassen
  und beide Hashbindungen (`scriptSha256` über die unveränderten Rohbytes,
  `intentPlanHash` über die kanonische Festbreitenkodierung, erweitert um
  die Kindbytes 4 und 5) entsprechen unverändert dem Kommandovertrag
  Abschnitt 5.
- Legacy-v1-Skripte bleiben byteidentisch gültig (Regressionsfixtures mit
  identischen Ketten und Endhash).
- Kontextkorrektheit ist **keine** Parserfrage: kontextfalsche Intents sind
  grammatisch gültig und werden pipeline-seitig mit den Dispositionen des
  Abschnitts 5 abgewiesen, weil der Moduskontext eines Ticks erst im Lauf
  festliegt.

**Alternativen:** v1-Kopf um neue Aktionen erweitern (stille Formatdrift
innerhalb einer versionierten Grammatik — abgelehnt); JSON (abgelehnt wie
Kommandovertrag Abschnitt 5). **Rückrollweg:** Neue Kennung
`graybox-input-script-v3`; v2-Skripte bleiben historische Fixtures.

## 7. Telemetrie-/Gatematrix (Erweiterung des Kommandovertrags §7)

Bestehende Kriterien 1–5 (Tickzeit, Allokation je warmem Tick, Intent-
Reaktion, Laufzeitshaderkompilierungen, Ketten-Selbstkonsistenz) gelten
unverändert mit ihren dokumentierten Grenzwerten. Neu:

| Nr. | Kennzahl | Grenzwert | Methode |
|---|---|---|---|
| 6 | max switchReactionTicks | ≤ 3 hart (≤ 2 Ziel ausgewiesen) | Abschnitt 4; Wechsel-Intent-Tick `S` bis erster Gültigkeitsprüfung `M` im neuen Modus; fail-closed als eigenes Reportfeld |

Die vertraglichen, maschinenlesbaren Modusnamen des Reports sind `strategic`
(strategischer Modus) und `personal` (persönlicher Modus); `initialMode` und
`finalMode` des Modussitzungsblocks sowie jede `previousMode`/`newMode`-Kante
des Wechselprotokolls tragen ausschließlich diese beiden Werte. Das
Wechselprotokoll ist eine Liste von Auswertungsereignissen, keine
Übergangskette: `previousMode` nennt den Modus, der an der Auswertungsgrenze
`S` nach Abschnitt 4 (2) gültig war, `newMode` den daraus abgeleiteten
Zielmodus, und `effectiveBoundaryTick` die Wirksamkeitsgrenze `M = S + 2`.

Alle neuen Diagnosefelder (Wechselprotokoll, Heldenstatus je Wechselgrenze,
Kontextabweisungszähler, Lenk-Dedupe, HUD-Bindung, Endmodus) tragen
maschinenlesbar `gateCoupled=false`. Pflichtprofile bleiben `NOT-MEASURED`
(Q-OPS-001); der Report weist die Offenheit von Q-TEC-004, Q-TEC-006,
Q-TEC-010, Q-GAM-001 bis Q-GAM-007, **Q-GAM-010** und Q-NAR-002
maschinenlesbar aus. G-PERF bleibt gemäß akzeptierter T-032-Präzedenz kein
neues Pflichtgate: Es entsteht kein neuer budgettragender Pfad (die
Verfolgungskamera ist rein darstellseitig, kein Budgetwert wechselt), die
Wechselreaktion ist als eigenes fail-closed Reportfeld der erweiterten
Kommandoschleifen-Gatematrix gebunden, und die Bestandsregressionen laufen
über AC-T033-09. Die Perspektivbudgetpflichten aus ADR 008 Kernaussage 8
(Nahsicht-Messpflicht mit echtem Nahsicht-Rendering) binden die späteren
Slices und sind hier nicht behauptet.

## 8. Interaktiver Modus: Verfolgungskamera, Indikatoren, HUD, Abgriffe

**Verfolgungskamera (`hero-chase-camera-v1`, rein darstellseitig):** Geneigte
Verfolgungsansicht hinter der Heldenfigur; Blickpunkt ist die Heldenposition
(Agentenindex 0), Kamera sitzt südlich (feste Nordausrichtung konsistent zur
§4-Konvention des Kommandovertrags), Nickwinkel 32°, Anzeigedistanz 9 m,
geclippt auf 5–16 m (Zoom-Schritte), Blickpunkt an Weltränder geclampt
(160×90-m-Raster). Kamera, Badge und HUD sind niemals Teil von
Simulationszustand oder Hash. **Alternativen:** frei drehbare Orbit-Kamera
(Gründe des Kommandovertrags §4 gelten unverändert — abgelehnt); exakt
First-Person (verlangt Blickrichtungssemantik ohne Kernbefehlsfläche —
abgelehnt). **Playtestkriterium:** Der Held bleibt bei normaler Gruppen-
geschwindigkeit im Bild; Orientierung (Norden oben) bleibt erhalten.
**Rückrollweg:** Parameter sind Hypothesenkonstanten; Austausch ohne
Vertragspflicht, sofern die Kontextverträge unverändert bleiben.

**Held- und Modusindikator (`hero-mode-badge-v1`, zwei unterscheidbare
visuelle Kanaele, keine reine Farbcodierung gemäß NF-005):** Ein
heldenverankerter Badge über Agentenindex 0 markiert den Vertragshelden und
zeigt zugleich den Modus über zwei unterscheidbare Kanäle je Modus:
strategisch — ruhender Diamant (feste Orientierung π/4), cyan (0,45/0,85/1,0),
Höhe 2,6 m; persönlich — pulsierender Diamant (Größe atmet deterministisch mit
der Tickzahl), warmes Orange (1,0/0,45/0,20), dieselbe Verankerung. Der
Formkanal (ruhend gegenüber pulsierend, Größe 0,60 gegenüber 0,42 der
Auswahlglyphe) und der Farbkanal (Cyan/Orange gegenüber warmem Amber der
Auswahlglyphe) trennen Badge, Auswahlglyphe und Befehlspuls. **Rückrollweg:**
Badge-Parameter sind Hypothesenkonstanten; Änderung ohne Vertragspflicht,
solange die Zwei-Kanal-Erkennbarkeit erhalten bleibt.

**Mindest-HUD (`title-hud-mode-herozone-v1`):** Die Fenstertitelzeile trägt
aktuellen Modus und Heldenzone in der festen Form
`Riftward Graybox — Modus: Strategisch|Persönlich — Heldenzone: <Zone|–>`.
Sie ist der kleinste ehrliche HUD-Träger dieser Linie: Er ist ohne neue
Render-/Schriftfläche maschinenlesbar prüfbar und im Report gebunden
(`hud: { kind, fields }`). **Alternativen:** gerenderte Text-HUD (bräuchte
einen neuen Schrift-/UI-Renderpfad — späterer Slice); Badge-only-Indikation
(reine Weltmarkierung ohne festen Lesplatz — abgelehnt). **Playtest-
kriterium:** Modus und Heldenzone sind ohne Tastendruck ablesbar; Lesezeit
≤ 2 s. **Rückrollweg:** Ersatz der Titel-HUD durch eine gerenderte Text-HUD
in einem Folgeslice ist eine Hypothesenkonstanten-Änderung; die Reportbindung
bleibt.

**Umschaltaktion und Beenden:** Die frei belegbare Aktion `mode-switch`
(Standard Tab, Scancode 43, unbesetzt im T-032-Stand) erzeugt an der
laufenden Vorgrenze einen Live-Wechsel-Intent; `quit` (Escape) beendet
kontrolliert wie T-032. Ohne nutzbares Display bricht der Interaktivmodus
kontrolliert mit dokumentiertem Code 19 ab statt zu simulieren.

**Opt-in Abgriffpaar (höchstens zwei Einzelabgriffe):** Nur mit
`--capture-frame PFAD`, strikt nach dem Messfenster, über **demselben
Weltzustand am selben Tick**: je ein 1920×1080-Einzelabgriff pro Modus als
unkomprimiertes 32-Bit-BMP nach T-023-/T-032-Muster. Dateinamen: vor der
Endung von `PFAD` wird `-strategisch` beziehungsweise `-persoenlich`
eingefügt (ohne Endung wird suffigiert); beide Dateien werden im Report je
hashgebunden (SHA-256, Abmessungen, Format, Modus) gemeinsam mit dem
gebundenen Weltzustand (Tick und Zustands-Hash), und tragen die
maschinenlesbare Aussagegrenze `graybox-state-occupancy-not-gameplay-
atmosphere-or-shipping` (Graybox-Zustandsbelegung — niemals Gameplay-,
Atmosphären- oder Shipping-Beleg; öffentliche Verwendung nur über
`docs/communication/MEDIA_LAB.md` plus Projektleitungsautorisierung). Ohne
Flag entsteht keine Datei; das Messverhalten ist identisch. Ein
fehlgeschlagener Abgriff ergibt Code 38 mit `captured=false` und Grund. Die
Modusumschaltung zwischen beiden Abgriffen ist rein darstellseitig und
verändert denselben Weltzustand nicht.

## 9. Vorregistriertes Playtestprotokoll

Vollständiges Protokoll einer Displaysession (Entwickler-PC, gegebenenfalls
virtuelles Wayland nach T-023-Präzedenz), vor der Implementierung
registriert:

1. **Erkennbarkeit des Wechsels:** Umschaltaktion drücken; Moduswechsel ist
   an Badge, Kamerapostur und Titel ablesbar; Wiederfinden des Helden
   binnen 2 Sekunden (Abschnitt 2).
2. **Lenkqualität:** Sechs Lenkversuche aus mindestens zwei Zonen; Richtungs-
   treue und Korrekturaufwand gegen Abschnitt 3 messen (≥ 80 %, ≤ 1 Korrektur).
3. **Kontexttrennung:** Je drei kontextfalsche Impulse pro Modus; Erwartung
   nach Abschnitt 5 (sichtbare Abweisung, kein Kernbefehl); Missverständnis-
   rate < 10 %.
4. **Kameralesbarkeit:** Zoomgrenzen der Verfolgungskamera halten Agenten
   lesbar; Weltrandbegrenzung verhindert Leerblick.
5. **HUD:** Modus und Heldenzone sind im Titel binnen 2 s ablesbar.
6. **Beobachtung:** Strategische Phasen bleiben unverändert bedienbar
   (Kommandovertrag unverändert).

Ausführung: dokumentiert im Abnahmelauf; ist kein Display verfügbar, bleiben
Interaktivsmoke und Playtestausführung ausgewiesene Restpunkte mit
kontrolliertem Code-19-Nachweis ohne Simulation (Präzedenz T-023/T-032).

## 10. Exitcodes

Die bestehenden Bedeutungen bleiben unverändert, insbesondere 35–38
(Kommandovertrag Abschnitt 8) und 19. Der Wechsel- und Kontextmechanismus
erzeugt **keine** neuen Exitcodebedeutungen: Kontextabweisungen sind reguläre
fachliche Daten (Reportzähler, UF-Zeilen), kein Fehlerzustand. Die
Exitcodes 35–38 behalten ihre Vertragsbedeutungen; der Exitcode-Mapping-Test
wird um die neuen Dispositionen erweitert, ohne eine bestehende Bedeutung zu
ändern.

## 11. Offenheiten und Grenzen

Dieser Vertrag antwortet auf keine offene Produktfrage. Ausdrücklich offen
bleiben: Q-GAM-001 bis Q-GAM-007 (Kreativentscheidungen), Q-GAM-010 (finale
Wechsel-Detailregel: Übergangsanimationen, Eingabesperren, automatische
Wechsel, Perspektiv-Lock, Wechsel in Save/Replay), Q-NAR-002 (Erzählung),
Q-TEC-004 (Simulationsvertrag-Ratifizierung), Q-TEC-006 (produktives
Replayformat), Q-TEC-010 (tolerierte Benchmarkstreuung), Q-OPS-001
(Referenzhardware). Kein Budgetwert wird geändert; Pflichtprofile bleiben
`NOT-MEASURED`. GAME_DESIGN.md und ANFORDERUNGEN.md bleiben durch die
Implementierung unberührt.