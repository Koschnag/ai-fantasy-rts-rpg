# Erkundungsvertrag (T-034, Abschnitt 0)

**Vertragsversion:** 1
**Status:** Durch den gatenden Vertragsspike des Auftrags
`.ai/tasks/T-034-graybox-exploration-loop.json` vor der Implementierung
festgelegt; die maschinenlesbaren Kennungen sind in
`src/Riftward.Session/ExplorationContract.cs` gespiegelt und werden von einem
Test gegen dieses Dokument gehalten.

Dieser Vertrag entscheidet die Erkundungsdetails des kleinsten spielbaren
Erkundungsauftrag-Loops verfahrensmäßig nach der Spike-Klausel
(`docs/QUALITAET.md`, Definition of Ready). Jede Wahl nennt Alternativen,
Gründe, ein messbares Playtestkriterium und einen Rückrollweg (ADR 007). Er
antwortet auf keine offene Produktfrage: Q-GAM-001 bis Q-GAM-007, Q-GAM-010,
Q-NAR-002 und Q-NAR-004 bleiben ausdrücklich `OFFEN`; Q-TEC-004 (Ratifizierung
des Simulationsvertrags), Q-TEC-006 (produktives Replayformat) und Q-TEC-010
bleiben `OFFEN`; Q-OPS-001 folgt der protokollierten T-020- bis T-023-Behandlung.

## 1. Geltungsbereich und Produktform

Dieser Vertrag implementiert die erste Phase des akzeptierten Flows UF-001
(„Erkunden führt zu einem Auftrag mit Ziel, Aktion und Feedback“; Kernschleife
„Erkunden und Hinweise finden“, GS-007-Aufklärung als reine Graybox-Hypothese
ohne jegliche Sichtbarkeits-/Fog-of-War-Semantik) über dem abgenommenen
T-032-/T-033-Kern: Die Graybox-Vertragswelt `riftward-simworld-graybox-v1`
(Simulationsvertrag V1) erhält eine deterministische, assetfreie Menge von
Landmarken (Graybox-Markierungen ohne Content, Lore oder Fog of War), die die
Vertragsheldengruppe (`session-hero-agent-index-0-group-0-v1`, Modevertrag
Abschnitt 2) im Rahmen eines sitzungslokalen Erkundungsauftrags aufsucht. Der
Spieler mobilisiert strategisch (bestehende Auswahl- und Bewegungssemantik des
Kommandovertrags) und vollzieht das Aufsuchen im persönlichen Modus (bestehende
Lenk- und Wechselsemantik des Modevertrags); Fortschritt und Abschluss des
Auftrags sind in beiden Modi über die bestehenden Zwei-Kanal-Indikator- und
Titel-HUD-Muster sichtbar und maschinenlesbar im Report gebunden.

Das Aufsuchen ist eine rein sitzungsseitige Beobachtung an der Vorgrenze: Es
erzeugt niemals einen Kernbefehl, verändert keinen Befehlszustand, ist nie Teil
des Simulationszustands oder Hashes und berührt `Riftward.Simulation` nicht
(die Simulationsquelldateien bleiben gegen den Vorblob byteidentisch; der
Blobvergleich ist Run-Evidenz und Testbindung). Es entstehen keinerlei Kampf-,
Wirtschafts-, Dialog-, Quest-, Belohnungs-, Inhalts- oder Fog-of-War-Regeln;
der Fortschritt ist sitzungslokal und wird weder in Save/Load noch Replay
fortgesetzt (Abschnitt 4; ADR 008, Sequenzierungsnote).

**Rückrollweg (gesamt):** Der Erkundungsvertrag wird als V1 versioniert; jede
Änderung einer Wahl dieses Dokuments erfordert Vertragsversion 2 mit
Fixture-Regeneration. Der gesamte Erkundungsstand lebt ausschließlich in
`Riftward.Session`, der Reportlinie und der darstellseitigen Verdrahtung; ein
Rückbau entfernt diese Schicht (Sitzungszustand, `--exploration`-Aktivierung,
Landmarkenkanal, Reportblock), ohne den Simulationskern oder einen bestehenden
Vertrag zu berühren.

## 2. Landmarkenmenge (`graybox-landmark-zone-anchor-v1`)

**Wahl:** Genau eine Landmarke je Vertragszone; die Landmarkenmenge ist damit
eine feste Menge von `NavWorld.ZoneCount` (6) Einträgen mit fester
Zonenzuordnung 0–5. Der Anker je Landmarke ist die erste betretbare Kachel der
Zone in festen zeilenmajoritischen Scanreihenfolgen (aufsteigend y, dann
aufsteigend x) innerhalb der Vertragszonen-Schranken
(`NavWorld.IsInsideZone`/`NavWorld.IsWalkable`). **Totalität und
Fail-closed-Randfall:** Im gebundenen Vertragsweltstand besteht jede Zone
0–5 vollständig aus betretbaren Kacheln; `NavWorld` erzwingt das fail-closed
bereits pro Prozessstart (`ValidateZones` im statischen Konstruktor,
`src/Riftward.Simulation/NavWorld.cs`: kontrollierter Fehler bei jeder
unbetretbaren Zonenkachel). Die Ableitung selbst ist zusätzlich vertraglich
fail-closed definiert: Besäße eine Zone keine betretbare Kachel, bricht die
Ableitung kontrolliert mit dem definierten Vertragsfehler
`exploration-landmark-zone-without-walkable-tile` ab, statt einen
undefinierten Anker zu bilden; der Ableitungstest hält beide Aussagen fest
(Zonendeckung 0–5 mit betretbarem Anker je Zone sowie der kontrollierte
Ablehnungsfall); die Ableitung konsumiert
ausschließlich die fixierte Zonen-/Kachelgeometrie der Vertragswelt und
keine Asset-, Namens-, Text- oder Loreinhalte, keine Ortsemantik und keinen
 externen Beitrag. Die Ableitung ist rein geometrisch und konsumiert den
Sitzungsseed bewusst nicht (ausdrückliche, versionierte Entscheidung, keine
stille Auslassung): Die Landmarkenmenge ist damit über Seeds identisch, und
die Aufsuchfolge ist eine reine Funktion des Weltzustands und der
Modusgrenzen — gleiche Skripte liefern identische Protokolle, und ein fremder
Seed ändert nachweislich ausschließlich Start-/Endhash der Simulation, niemals
die Landmarkenmenge oder -zonen.

**Alternativen:**

| Alternative | Bewertung |
|---|---|
| **A: je Vertragszone eine geometrisch abgeleitete Landmarke (Empfehlung)** | Vollständige Zoneabdeckung des kleinsten Loops ohne Content- oder Mengenfrage; geometrische Reinheit hält die Menge seedunabhängig und die Protokolle über Seeds vergleichbar; Nachteil: sechs Aufsuche je vollem Loop (Absicht des Auftrags). |
| B: Seedabhängiger Anker je Zone (SimRandom-Strom je Zone wählt eine freie Kachel) | Verträglich und deterministisch, koppelt die Ankerposition an den Sitzungsseed; die Anker wandern je Seed, ohne dass die Aufsuchregel davon abhängt. Als dokumentierte Alternative mit Playtestkriterium (Anker je Seed binnen 2 s identifizierbar) und Rückrollweg erhalten. |
| C: feste Teilmenge (drei Landmarken, Zonen 0–2) oder einzelne Landmarke | Kleinerer Loop mit kürzerem Horizont, aber unvollständiger Querschnitt über die Vertragswelt; der Auftrag verlangt das modusgebundene Aufsuchen deterministischer Landmarken als kleinsten vollständigen Loop — abgelehnt als Empfehlung, als spätere Verschärfung (Mengenreduktion) jederzeit zulässig. |

Es gibt keine Namens-, Lore-, Text- oder Assetinhalte und keine Erfindung von
Ortsemantik: Eine Landmarke ist ausschließlich das Tripel (Zonenindex,
Ankerkachel, Aufsuchzustand). **Playtestkriterien:** Die Landmarkenmarkierung
ist in beiden Modi binnen 2 Sekunden als solche identifizierbar; Tester
erkennen Fortschritt (n von m) ohne Tastendruck. **Rückrollweg:** Mengen- und
Platzierungswahl sind Konstanten des versionierten Sitzungsvertrags; Wechsel
zu B oder C über Vertragsversion 2 mit Fixture-Regeneration, ohne
Kernelaenderung.

## 3. Aufsuch- und Moduskopplungsregel (`boundary-visit-personal-mode-only-v1`)

**Wahl:** Das Aufsuchen registriert genau dann, wenn an einer Auswertungsgrenze
(Vorgrenze `T`; `world.TickIndex == T` vor dem Tick) alle drei Bedingungen
gelten: (i) der Vertragsheld (Agentenindex 0) befindet sich physisch in der
Landmarkenzone (Zonenmitgliedschaft der Heldenposition, nicht Ankernähe);
(ii) die Sitzung befindet sich an dieser Vorgrenze im **persönlichen Modus**
(derselbe Modus, der an dieser Vorgrenze nach der kanonischen
Same-Tick-Regel `same-tick-switch-last-effective-next-next-v1` des
Modevertrags Abschnitt 4 für die Gültigkeitsprüfung maßgeblich ist); (iii) die Landmarke ist in dieser Sitzung noch nicht registriert.
Division der Arbeit: Mobilmachung (Hinbewegung zur Landmarkenzone) läuft
strategisch über die bestehende Auswahl-/Bewegungssemantik; das Aufsuchen
selbst ist persönliche Anwesenheit. Jede Auswertungsgrenze des Laufs
(einschließlich Warm-up-Grenzen) wird beobachtet; die Beobachtung liest
ausschließlich Heldenzone und Sitzungsmodus schreibgeschützt.

**Bindende Auswertungseigenschaften (alle maschinell getestet):**

- Das Aufsuchen erzeugt zu keinem Zeitpunkt einen Kernbefehl, verändert keine
  Befehlszustände und ist zu keinem Zeitpunkt Teil des Simulationszustands
  oder Hashes. Ein Twin-Kontrolllauf mit identischer Intentfolge ohne
  Aktivierung erzeugt bei gleicher Tickzahl byteidentische Kettenstichproben
  und denselben Endhash; die Kernbefehlsfolge ist identisch.
- Doppelbesuch ohne Mehrfachzählung: eine registrierte Landmarke registriert
  in derselben Sitzung nie erneut.
- Reihenfolgeunabhängigkeit: das Aufsuchen ist reihenfolgefrei; der
  Abschlussbedingung genügt jede Permutation der Landmarkenmenge.
- Moduskopplung: an einer Vorgrenze mit Heldenanwesenheit ohne persönlichen
  Modus wird nicht registriert (kein stiller Zähler, kein Puffer, keine
  Nachwirkung — die Gelegenheit ist verstrichen, ein späterer persönlicher
  Grenzbesuch derselben unregistrierten Landmarke registriert regulär).

**Alternativen:**

| Alternative | Bewertung |
|---|---|
| **A: persönlicher Modus erforderlich (Empfehlung)** | Macht den Moduswechsel zu einem spürbaren Entscheidungsschritt der Erkundungsschleife (UF-001: Ziel, Aktion, Feedback); strategische Beobachtung bleibt lesbar, registriert aber bewusst nicht. Nachteil: ohne Wechsel kein Fortschritt. |
| B: modusunabhängiges Aufsuchen (dokumentierte Alternative) | Registriert an jeder Vorgrenze mit Heldenanwesenheit in beiden Modi; weniger Reibung, aber der persönliche Modus wird für das Aufsuchen nie erforderlich und der Hybrid-Charakter des Auftrags (Mobilmachung strategisch, Interaktion persönlich) verliert seinen spielerischen Anlass. Playtestkriterium: Aufsuchverständnis ohne Modusanforderung ≥ 90 %; Rückrollweg: Wechsel zu B ist eine Konstantenänderung mit Vertragsversion 2. |

**Ausdrücklich verworfene, kernändernde Variante:** Landmarken-,
Entdeckungs- oder Aufsuchzustand im Simulationskern (eigener
Interaktions-/Aufsuchbefehl oder Besuchsflag im gehashten Zustand). Verworfen:
jede Kerneländerung ist verboten (Auftragsvertrag; ADR 008 Folge 6); sie würde
den Befehlsvertrag V1 erweitern und sämtliche Hashketten und Fixtures der
T-021-/T-022-/T-032-/T-033-Linie entwerten. Diese Variante wird nicht als
Hypothese geführt; ihre Realisierung wäre eine eigene Simulationsvertrag-V2-
Entscheidung (Q-TEC-004, `OFFEN`).

**Playtestkriterien:** In protokollierten Playtests nutzt der Spieler je
Aufsuchen bewusst den Moduswechsel (≥ 80 % der Aufsuche gehen einem
intentionalen Wechsel in den persönlichen Modus unmittelbar voraus);
≥ 90 % der Tester verstehen, dass im strategischen Modus an derselben Stelle
bewusst nicht registriert wird (kein Defekt); das Aufsuch-Feedback ist binnen
2 Sekunden lesbar. **Rückrollweg:** Wechsel zu Alternative B über
Vertragsversion 2 mit Fixture-Regeneration; keine Kernelberührung.

## 4. Fortschritt und Abschluss (`session-local-visit-counter-v1`)

**Wahl:** Sitzungslokaler Zähler über die Landmarkenmenge: Fortschritt ist
`visitedCount` von `landmarkCount` (n von m), Abschluss (`completed`) ist
`visitedCount == landmarkCount`. Je Registrierung entsteht ein
maschinenlesbarer Protokolleintrag mit Auswertungsgrenze (`evaluationBoundaryTick`,
Vorgrenze-Tick), Landmarkenzone (`zoneIndex`), vertraglichem Modusnamen an
dieser Grenze (`mode`: ausschließlich `strategic`/`personal`) und
Registrierungsreihenfolge (`visitOrder`, 1-basiert in Registrierungsreihenfolge,
reihenfolgeunabhängig im Wertebereich). Fortschritt und Abschluss sind
Reportfelder mit `gateCoupled=false`; sie koppeln an kein Gate, keinen
Budgetwert und keine Exitcodebedeutung.

**Maschinenlesbare Nichtpersistenzaussage
(`session-local-not-persisted-v1`):** Der Auftragsfortschritt ist
sitzungslokal — ein Lauf, kein Fortsetzungsanspruch. Er ist weder in Save/Load
(T-031, Savevertrag V1) noch in Replay oder Soak fortgesetzt, in keinem
Persistenzvertrag enthalten und seine Persistenz ist einer späteren
Savevertrags-Erweiterung vorbehalten (ADR 008, Sequenzierungsnote zu
Kernaussage 4). Der Report trägt diese Aussage als versionierte Kennung mit
`persisted=false`; keine Write-/Lesefähigkeit entsteht über den Sitzungszustand
hinaus, und Schreibzugriffe des Laufs bleiben auf die vertraglich erlaubten
Verzeichnisse (Reportpfad, opt-in Abgriff) beschränkt.

**Alternativen:** Kernelgetragener Fortschritt (kernändernd — verworfen wie
Abschnitt 3); Persistenz im Bestandssavevertrag (Berührung von T-031-Artefakten
— out of scope, abgelehnt). **Playtestkriterium:** Nach abschließender
Landmarkenmenge ist der Abschlussstatus in beiden Modi ohne Tastendruck
ablesbar und im Report maschinenlesbar. **Rückrollweg:** Semantikänderungen
(Reihenfolgegebundenheit, Zählerreset) sind Vertragsversion 2 mit
Fixture-Regeneration; die Nichtpersistenz bleibt bis zu einer autorisierten
Savevertrags-Erweiterung bestehen.

## 5. Feedback in beiden Modi (`title-hud-expedition-progress-v1`, `landmark-state-channel-v1`)

**Wahl:** Zwei additive, darstellseitige Kanäle über der bestehenden
Zwei-Kanal-Indikator-Regel NF-005 (ANFORDERUNGEN.md) und den bestehenden
Amber-Auswahl- und Befehlspuls-Gegenkanälen des Interaktivmodus
(Modevertrag Abschnitt 8), beide ohne Tastendruck
in beiden Modi ablesbar, beide niemals Teil von Simulationszustand oder Hash:

1. **Titel-HUD-Erweiterung:** Die bestehende Titelzeile
   `Riftward Graybox — Modus: <Modus> — Heldenzone: <Zone|–>` erhält
   ausschließlich bei Aktivierung den additiven, unterscheidbaren Segment
   ` — Erkundung: <n>/<m>` (feste Form, unveränderte Bestandssubform; ohne
   Aktivierung bleibt die Titelzeile byteidentisch zum T-033-Stand). Kennung
   `title-hud-expedition-progress-v1`; Lesezeit ≤ 2 s.
2. **Landmarkenzustandskanal:** Je Landmarke ein darstellseitiger Marker am
   Anker mit zwei unterscheidbaren visuellen Kanaelen gemäß
   NF-005 (Form plus Farbe, nie reine Farbcodierung): **unbesucht** —
   ruhender Einzel-Diamant, feste Orientierung π/4, kühles Blaugrau
   (0,55/0,75/0,95), Höhe 1,6 m und Größe 1,15; **registriert** —
   zweistufige Markiersäule (unten ruhend bei 1,4 m/Größe 1,25, oben
   rotierend mit der Tickzahl bei 3,6 m/Größe 1,05), kühles Grün
   (0,40/0,90/0,60), Gesamtform klar zweigeteilt. Die Kombination aus
   Formkanal (ruhend-einstufig gegenüber zweistufig-rotierend) und Farbkanal
   trennt den Kanal von Auswahlglyphe (warmes Amber, klein, rotierend über
   Agenten), Befehlspuls (wachsend, bodenverankert, Cyan) und Held-/Modus-
   Badge (Diamant über dem Helden, 2,6 m, Cyan/Orange). Ohne Aktivierung
   entsteht kein Landmarkenmarker; die Bestandsdarstellung bleibt
   byteidentisch. Da die Registrierung vertraglich zonenweit und nicht an
   Ankernähe gebunden ist, wiederholt derselbe bestehende Partikelkanal den
   Zustand der aktuellen Heldenzone zusätzlich als rein darstellungsseitiges
   Echo direkt über dem Helden: unbesucht als ein blauer Diamant bei 3,6 m
   und Größe 0,70, registriert als zwei getrennte grüne Diamanten bei
   3,5/4,25 m und den für die nahe persönliche Kamera begrenzten Größen
   0,65/0,55. Die Farben entsprechen dem jeweiligen Ankermarker; dessen
   Größen 1,25/1,05 bleiben unverändert. Das Echo verschiebt keinen
   Anker, ändert weder Aufsuchregel noch Fortschritt und ist nie Simulations-
   oder Hashzustand; es schließt ausschließlich die generische Offscreen-
   Lücke zwischen zonenweiter Registrierung und heldenzentrierter Kamera.
   Badge, Anker und Echo nutzen einen per Instanz gebundenen echten
   Glyphenpfad; Befehlspulse bleiben im getrennten Formkanal rund.

**Alternativen:** gerenderte Text-HUD (neue Schrift-/Renderfläche — späterer
Slice, Modevertrag-Abschnitt-8-Präzedenz); reine Farbcodierung des
Landmarkenzustands (NF-005-Verstoß — abgelehnt); Puls-Only-Formkanal
(kollidiert mit dem pulsenden Badge — abgelehnt). **Playtestkriterien:**
Fortschritt und Abschluss sind in beiden Modi binnen 2 s ablesbar;
unregistrierte und registrierte Landmarken sind ohne Farbvergleich
unterscheidbar. **Rückrollweg:** Beide Feedbackformen sind
Hypothesenkonstanten der Darstellung; Austausch ohne Vertragspflicht, solange
Zweikanal-Erkennbarkeit und die Reportbindung erhalten bleiben.

## 6. Aktivierungsform (`opt-in-exploration-activation-v1`)

**Wahl:** Opt-in Aktivierung über das Befehlsflag `--exploration` des
bestehenden öffentlichen Befehls `kommandoschleife`. Ohne Flag ist das
Verhalten und der Report byteidentisch zum Bestandsstand (Report
Schemaversion 2, kein Erkundungsblock, kein Landmarkenmarker, unverändertes
Titel-HUD): Bestandsreports bleiben byteidentisch, Bestandsregressionsläufe
unverändert gültig. Mit Flag wird die Beobachtung aktiviert und der Report
rein additiv auf **Schemaversion 3** erhöht: ausschließlich neue Felder
(`explorationSession`-Block; interaktiv zusätzlich der Erkundungs-HUD-Ausweis
innerhalb dieses Blocks), keine Umdeutung, Umbenennung oder Entfernung
bestehender Felder; der Gatevertrag (Kriterien 1–6) bleibt unberührt, alle
neuen Felder tragen `gateCoupled=false`.

**Alternativen:** stets aktivierte Beobachtung (verletzt die Empfehlung der
byteidentischen Bestandsreports und erzwingt Erkundungszustand in
Bestandsregressionsläufen — abgelehnt); separates Untercommand (widerspricht
dem Auftrag: derselbe öffentliche Befehl und derselbe Pipelinepfad —
abgelehnt). **Rückrollweg:** Flag und Reportblock entfernen; ohne Flag ist
der Stand byteidentisch zum Vorgänger.

## 7. Reportbindung (rein additive Schemaversion 3)

Bei Aktivierung bindet der Report unter `explorationSession` (beide
Ausführungsarten; `gateCoupled=false` für sämtliche Mess- und Protokollfelder):

- Vertragsbindung (`contract`: Dokumentpfad und Version dieses Vertrags),
  Aktivierungs- und Modellkennungen (`opt-in-exploration-activation-v1`,
  `graybox-landmark-zone-anchor-v1`, `boundary-visit-personal-mode-only-v1`,
  `session-local-visit-counter-v1`)
- Landmarkenliste je Zone (Zonenindex, Ankerkachelkoordinaten,
  Betretbarkeitsausweis) in fester Zonenordnung
- Aufsuchprotokoll je Registrierung (Auswertungsgrenze, Zone, Modus,
  Reihenfolge) in kanonischer Registrierungsfolge
- Fortschritt `visitedCount`/`landmarkCount`, Abschluss `completed`
- versionierte Nichtpersistenzaussage (`session-local-not-persisted-v1`,
  `persisted=false`, Umfänge Save/Load und Replay)
- im Interaktivmodus der HUD-Ausweis (`title-hud-expedition-progress-v1`)
  und der Landmarkenkanalausweis (`landmark-state-channel-v1`); headless
  ausdrücklich nicht gemessen mit maschinenlesbarem Grund statt stiller
  Behauptung. Auch ein vorzeitig beendetes Interaktivfenster weist beide
  Kanäle mit Grund als nicht gemessen aus; `measured=true` setzt einen
  tatsächlich abgeschlossenen Fensterhorizont voraus.

Der Schemator prüft diese Felder nicht nur einzeln, sondern bindet sie
relational fail-closed: Anker müssen der kanonischen betretbaren
Kernelgeometrie entsprechen; Besuche sind zoneneindeutig, strikt fortlaufend
und ausschließlich persönlich; Protokolllänge, `visitedCount`, `completed`
und gemessene HUD-/Kanalzähler müssen dieselbe Aussage tragen.

Der Headless-Erkundungsflow läuft über denselben öffentlichen Befehl und
dasselbe Skriptformat `graybox-input-script-v2` (keine neue Grammatik oder
Aktion); zwei unabhängige Fresh-Prozessläufe sind builderidentisch, ein fremder
Seed ändert Start- und Endhash nachweislich, und die Legacyschemata
(Schemaversion 2 ohne Aktivierung) bleiben unverändert gültig.

## 8. Vorregistriertes Playtestprotokoll

Vollständiges Protokoll einer Displaysession (Entwickler-PC, gegebenenfalls
virtuelles Wayland nach T-023-Präzedenz), vor der Implementierung
registriert:

1. **Auftragssichtbarkeit:** Titel-HUD zeigt Erkundungsfortschritt in beiden
   Modi ohne Tastendruck; Lesezeit ≤ 2 s (Abschnitte 2 und 5).
2. **Moduskopplung:** Mobilisierung strategisch (Auswahl/Bewegung), Wechsel in
   den persönlichen Modus an der Landmarkenzone; Registrierung ist nur dort
   beobachtbar (Abschnitt 3); Missverständnisrate der bewussten
   strategischen Nichtregistrierung < 10 %.
3. **Aufsuchfeedback:** Je Registrierung ist der Zustandswechsel des
   Landmarkenkanals (Form und Farbe) binnen 2 s erkennbar; der Fortschritt im
   Titel aktualisiert sich synchron.
4. **Abschluss:** Nach vollständiger Landmarkenmenge zeigt der Titel den
   Abschluss; der Report bindet `completed=true`.
5. **Beobachtungstreue:** Strategische Phasen bleiben unverändert bedienbar;
   kein Befehlspuls und keine Weltänderung geht vom Aufsuchen aus.

Ausführung: dokumentiert im Abnahmelauf; ist kein Display verfügbar, bleiben
Interaktivsmoke und Playtestausführung ausgewiesene Restpunkte mit
kontrolliertem Code-19-Nachweis ohne Simulation (Präzedenz T-023/T-032/T-033).

## 9. Exitcodes

Die bestehenden Bedeutungen bleiben unverändert (insbesondere 19, 27, 28,
35–38 und 2/4): Der Erkundungsmechanismus erzeugt **keine** neuen
Exitcodebedeutungen. Der nicht aktivierte Lauf verhält sich wie der
Bestandsstand; die aktivierte Beobachtung ist reguläre fachliche Diagnostik
(Reportfelder mit `gateCoupled=false`), kein Fehlerzustand, und sie koppelt
das Gateverdict nicht. Der stabile Exitcode-Mapping-Test wird ohne neue
Bedeutung erweitert (Schemaversion-Auswahl 2/3 ist kein Exitcode).

## 10. Offenheiten und Grenzen

Dieser Vertrag antwortet auf keine offene Produktfrage. Ausdrücklich offen
bleiben: Q-GAM-001 bis Q-GAM-007 (Kreativentscheidungen), Q-GAM-010 (finale
Wechsel-Detailregel), Q-NAR-002 (Erzählung) und Q-NAR-004 (Questoptionen mit
Folgen) sowie Q-TEC-004 (Simulationsvertrag-Ratifizierung), Q-TEC-006
(produktives Replay-, Cooked-Paket- und Definitionsformat), Q-TEC-010
(tolerierte Benchmarkstreuung), Q-OPS-001 (Referenzhardware; Pflichtprofile
bleiben `NOT-MEASURED`). Es gibt keinen Fog of War, keine Minimap, keine
Aufklärungs- oder Sichtbarkeitssemantik (GS-007 bleibt unberührt), keine
Audio- oder Shipping-Assetaussage (Q-TEC-007, Q-AST-001/Q-AST-002), keine
Persistenz in jeder Form, keinen Windows-/macOS-Scope (T-011,
Q-OPS-002/Q-OPS-003) und keine Budgetänderung jeder Art. GAME_DESIGN.md und
ANFORDERUNGEN.md bleiben durch die Implementierung unberührt;
`docs/KOMMANDOVERTRAG.md` und `docs/MODEVERTRAG.md` bleiben unverändert.
