# Kommandovertrag (T-032, Abschnitt 0)

**Vertragsversion:** 1
**Status:** Durch den gatenden Vertragsspike des Auftrags
`.ai/tasks/T-032-graybox-command-loop.json` vor der Implementierung festgelegt;
die maschinenlesbaren Kennungen sind in
`src/Riftward.Session/SessionContract.cs` gespiegelt und werden von einem Test
gegen dieses Dokument gehalten.

Dieser Vertrag entscheidet die darstellseitigen Interaktionswahlen des ersten
E-004-Slices verfahrensmäßig nach der Spike-Klausel (`docs/QUALITAET.md`,
Definition of Ready). Jede Wahl nennt Alternativen, Gründe, ein messbares
Playtestkriterium und einen Rückrollweg (ADR 007). Er antwortet auf keine
offene Produktfrage: Q-GAM-001 bis Q-GAM-007 und Q-NAR-002 bleiben
ausdrücklich `OFFEN`; Q-TEC-006 (produktives Replayformat) bleibt `OFFEN`;
Q-TEC-004 (Simulationsvertrag-Ratifizierung) bleibt `OFFEN`.

## 1. Öffentlicher Befehl

**Wahl:** `kommandoschleife` als neuer öffentlicher Befehl in
`scripts/rift.sh` gemäß bestehender Namenskonvention:

```bash
./scripts/rift.sh kommandoschleife --scenario kommando-graybox \
  --input-script PFAD --seed N --report PFAD [--interactive] \
  [--capture-frame PFAD] [--warmup-ticks N] [--horizon-ticks N] [--lock DATEI]
```

Ohne `--interactive` läuft der Befehl headless nativ auf linux-x64 rein
CPU-seitig ohne Fenster, Renderer und Netzwerk aus; native SDL3-/bgfx-Artefakte
werden nicht geladen. Mit `--interactive` bedient derselbe Pipelinepfad den
fensterpflichtigen Modus; ohne nutzbares Display bricht er kontrolliert mit dem
dokumentierten Code 19 ab statt zu simulieren.

**Alternativen:**

| Alternative | Ablehnungsgrund |
|---|---|
| Untercommand von `bench` (neues Szenario) | Der Auftrag ist Interaktionsslice, kein Performancebenchmarkszenario; ein Bench-Eintrag würde das Szenarioregister und das G-PERF-Gate mit einer Nichtmessgröße belasten. |
| Englischer Befehlsname | Bricht die durchgängige deutschsprachige rift.sh-Konvention (plattformsmoke, effizienzbaseline, savecheck). |

**Rückrollweg:** Befehlseintrag in `rift.sh` und Routingeintrag entfernen;
der Sitzungskern in `Riftward.Session` bleibt ohne Vertragsänderung
wiederverwendbar.

## 2. Intent-zu-Befehl-Abbildung

**Wahl:** Die Sitzung kennt genau vier Intentarten; sie benutzt ausschließlich
die unveränderte öffentliche Befehlsfläche des Simulationskerns
(`SimCommandKind.GroupMoveToZone`) mit dessen kanonischer Ordnung
`(Tick, ScopeGroup, Kind, ZoneIndex)`:

| Intentart | Parameter (Skripteinheit) | Wirkung |
|---|---|---|
| `clear` | keine | hebt die Auswahl auf (rein darstellseitig) |
| `point` | x, y (Millimeter) | wählt die Gruppe des nächstgelegenen Agenten im Auswahlradius; ohne Agent im Radius wird die Auswahl gehoben (Klick ins Leere) |
| `box` | x0, y0, x1, y1 (Millimeter) | wählt die Vereinigung der Gruppen aller Agenten, deren Position innerhalb des kanonisierten Rechtecks liegt |
| `move` | zoneIndex (0–5) | erzeugt je ausgewählter Gruppe genau einen Kernbefehl `SimCommand(tick, gruppe, GroupMoveToZone, zone)` |

Es gibt keine neue Befehlsart, keine Agentengranularität, kein Freilziel
jenseits der sechs Zonen und keinerlei Änderung an `Riftward.Simulation`.
Ein `move` ohne ausgewählte Gruppe ist kein stiller No-op: Er wird mit der
fachlichen Ursache `move-without-selection` abgewiesen (UF-001-Fehlerzeile);
es gibt keine endlos pendelnde Order.

Innerhalb eines Ticks werden Intents in kanonischer Reihenfolge ausgeführt:
`clear (0) < point (1) < box (2) < move (3)`, bei Gleichstand nach den
Parametern als numerisches Tupel aufsteigend. Die Eingabereihenfolge im
Skript bestimmt niemals das Ergebnis (Negativfixtures binden dies).

**Alternativen:** Agentengranulare Einzelauswahl (benötigt neue Kernbefehle,
verboten); Kontrollgruppen (explizit out of scope, Q-GAM-005); Formationsziele
(Q-GAM-005, OFFEN). **Playtestkriterium:** In späteren Spieltesttasks muss die
Gruppenwahl per Punktklick in ≥ 90 % der Versuche die beabsichtigte Gruppe
treffen und darf nicht länger als 2 Sekunden nachdenken lassen; Messung über
protokollierte Intent-/Auswahlpaare. **Rückrollweg:** Neue Intentart oder
geänderte Abbildung erfordert Vertragsversion 2 mit Neuregenerierung aller
Skriptfixtures; der Simulationskern bleibt unberührt.

## 3. Auswahlmodell V0 (vorregistrierte Hypothese)

**Wahl:** `graybox-selection-model-v0`. Punktwahl selektiert die *Gruppe* des
nächstgelegenen Agenten innerhalb eines Radius von 3000 mm (3,0 m) um den
Klickpunkt; Rahmenauswahl selektiert die Vereinigung der Gruppen aller
Agenten im Rechteck; Klick ins Leere deselectiert. Die Auswahl ist rein
darstellseitiger Zustand, gehört nie zum Simulationszustand oder Hash und ist
auf die fünf Vertragsgruppen begrenzt (Booleschbelegung je Gruppe).

**Alternativen:**

| Alternative | Ablehnungsgrund |
|---|---|
| Agentengenaue Punktauswahl | Ohne neue Kernbefehle wirkungslos (Befehlsgranularität ist Gruppe); täuschte eine Steuerungspräzision vor, die die Befehlsfläche nicht liefert. |
| Auswahlradius 1,5 m | Bei Graybox-Agentdichte in Startzonen zu fehleranfällig; 3,0 m entsprechen der doppelten Ausweichradiusstrecke und bleiben deutlich unter Kachelgruppengröße. |
| Akkumulative Mehrfachauswahl per Umschalt | Vorgriff auf Kontrollgruppen-/Modifierfragen (Q-GAM-005); für den Slice bewusst zurückgestellt. |

**Playtestkriterium:** Anteil fehlgeklickter Gruppenauswahlen < 10 % und
medianer Korreuraufwand ≤ 1 zusätzlicher Klick in protokollierten Playtests
(spaßige Schleife erst danach beurteilen). **Rückrollweg:** Radius und
Semantik sind Konstanten des Sitzungskerns; Änderung mit Vertragsversion 2 und
Fixture-Regeneration, ohne Kernelaenderung.

## 4. Kameramodell V0 (vorregistrierte Hypothese)

**Wahl:** `graybox-camera-model-v0`. Geneigte Top-Down-Ansicht (Nickwinkel
55°, feste Nordausrichtung), Schwenken per Fensterrandkontakt, Ziehen mit der
mittleren Maustaste sowie W/A/S/D und Pfeiltasten; geclippter Zoom
(Anzeigedistanz 12–60 m, Mausrad sowie E/+ und Q/−); Weltrandbegrenzung auf
das 160×90-m-Vertragsraster. Die Kamera ist ausschließlich Darstellung: Sie
ist niemals Teil von Simulationszustand oder Hash und beeinflusst nur
Pickingstrahlen und Sichtfenster.

**Richtungskohärenz (Abschluss-Review 2026-08-27 präzisiert):** Bildschirm oben
ist Norden (−Z). Tasten- und Rand-Schwenken bewegen die Sicht in dieselbe
Himmelsrichtung: `pan-up`/Kontakt am oberen Rand → Norden, `pan-down`/unterer
Rand → Süden, `pan-left`/linker Rand → Westen (−X), `pan-right`/rechter Rand →
Osten (+X). Die Bildschirm-zu-Boden-Zuordnung ist die exakte Umkehrung der
gepinnten bgfx-Clipkette (Kombination proj·view) und vom Test gebunden; die
Kamera lebt im Render-Raum der Szene (T-020/T-023-Präzedenz). Beobachtete
Eigenschaft dieser akzeptierten Renderkonvention: Osten erscheint am linken
Bildschirmrand; die horizontale Bildschirmorientierung ist damit keine
stille Produktentscheidung, sondern wird mit dem Playtestkriterium dieser
Sektion beurteilt. Ihre Änderung ist eine Vertragsänderung mit Neubindung der
Richtungstests.

**Alternativen:**

| Alternative | Ablehnungsgrund |
|---|---|
| Freie Rotation (Yaw steuerbar) | Für Auswahl-/Rechtecksemantik und Lesbarkeit des Slices ohne Nutzen; erhöht Picking- und Testmatrixfläche. |
| Isometrische Orthogonalprojektion | Weicht vom T-020/T-023-Perspektivpfad ab und bräuchte eine zweite Projektionslinie ohne Slicevorteil. |

**Playtestkriterium:** Zielanwahl an beliebiger Weltstelle in ≤ 2 s ohne
Überzoom-Verlust der Agentenlesbarkeit; Zoomgrenzen verhindern Pixelbreiten
unter 2 px je Agent bei maximaler Distanz. **Rückrollweg:** Kameraparameter
sind dokumentierte Hypothesenkonstanten; Austausch ohne Vertragspflicht,
sofern Auswahl-/Pickingverträge unverändert bleiben.

## 5. Eingabeskript-Diagnoseformat `graybox-input-script-v1`

**Wahl:** Kanonisches, tickbezogenes Zeilenformat in UTF-8 (LF-Zeilenenden):

```text
graybox-input-script-v1 <horizonTicks>
intent <tick> clear
intent <tick> point <xMm> <yMm>
intent <tick> box <x0Mm> <y0Mm> <x1Mm> <y1Mm>
intent <tick> move <zoneIndex>
end
```

- Kopfzeile bindet `<horizonTicks>`; letzte Zeile ist exakt `end`.
- Koordinaten sind Ganzzahl-Millimeter innerhalb des Weltmaßes
  (x ∈ [0, 160000], y ∈ [0, 90000]); die Konversion nach Q16.16 rundet
  deterministisch kaufmännisch (half-up).
- Gültiger Intentbereich: alle Ticks in `[warmupTicks, horizonTicks)`;
  höchstens 4 Intents je Tick, höchstens 4096 insgesamt, Dateigröße höchstens
  262144 Bytes.
- Ablehnungsklassen sind unterscheidbar und kontrolliert:
  `HeaderMalformed`, `LineMalformed`, `UnknownAction`, `RangeViolation`,
  `DuplicateIntent` (identische normalisierte Parameter je Tick),
  `IntentLimitPerTick`, `IntentLimitTotal`, `IntentOutsideWindow`,
  `ScriptTooLarge`, `TrailingContent`. Die Bytegrenze wird an den Rohbytes
  vor der Dekodierung geprüft (`ScriptTooLarge`); kein gültiges UTF-8
  (einschließlich BOM-Vorspann) ist `HeaderMalformed` und wird nie still
  umkodiert.
- Zwei Hashbindungen: `scriptSha256` über die unveränderten Rohbytes der
  Datei (Zeilenenden bleiben im Hash erhalten; die Zeilenende-Normalisierung
  wirkt ausschließlich auf die Analyse) und `intentPlanHash` als FNV-1a-64
  über die kanonische Festbreitenkodierung (Little-Endian, ungenutzte
  Parameterslots nullgefüllt).

Das Format dient ausschließlich interner Test- und Reproinfrastruktur. Es ist
**nicht** das produktive Replayformat (Q-TEC-006 bleibt `OFFEN`), begründet
keine solche Zusage und ist nicht shippingbestimmt; eine Formatdrift erfordert
eine Anhebung der Vertragsversion.

**Alternativen:** JSON-Skript (schwergewichtiger Parser, keine Vorteile bei
kanonischer Ordnung); Binärformat (Diagnoselesbarkeit verloren). 
**Rückrollweg:** Neue Formatkennung `graybox-input-script-v2`; alte Skripte
bleiben historische Fixtures.

## 6. Reaktionsgatewerte (Ableitung mit transparenter Arithmetik)

Budgetzeile „Eingabe-zu-Reaktion“ (`docs/PERFORMANCE_BUDGET.md`): Ziel
100 ms, harte Grenze 150 ms. Vertragliche Tickrate: 20 Hz ⇒ dt = 50 ms je
Tick.

- Harte Tickgrenze: ⌊150 ms ÷ 50 ms/Tick⌋ = ⌊3,0⌋ = **3 Ticks**
- Zieltickgrenze: ⌊100 ms ÷ 50 ms/Tick⌋ = ⌊2,0⌋ = **2 Ticks**

**Definition:** Befehlstick `S` ist der im Skript gebundene Absendettick.
Verbrauchstick `V ≥ S` ist der Tick, an dessen Vorgrenze die abgebildeten
Kernbefehle in `ApplyCommands` übergeben wurden. Effektsnapshot ist der
kanonische Zustands-Hash unmittelbar nach Abschluss dieses Ticks
(Zustandsindex `V + 1`). `reactionTicks = (V + 1) − S`. Die Gateentscheidung
fail-closed: `max(reactionTicks) ≤ 3` über alle angewendeten Intents; das
2-Tick-Ziel wird im Report ausgewiesen, seine Verfehlung allein faltet das
Gate nicht (AC-T010-07/T-020/T-021-Präzedenz). Abgewiesene Intents zählen
nicht in die Verteilung; sie erhalten ihre fachliche Ursache.

Verschärfung bleibt jederzeit zulässig; jede Lockerung gegenüber 3 eskaliert
an die Projektleitung.

## 7. Telemetrie-/Gatematrix

Fail-closed entscheidet ausschließlich gegen absolute, hier dokumentierte
Grenzwerte (headless Modus; alle Werte ohne Änderung aus Simulationsvertrag V1
und PERFORMANCE_BUDGET.md übernommen):

| Nr. | Kennzahl | Grenzwert | Methode |
|---|---|---|---|
| 1 | p99-Tickzeit | ≤ 16 ms hart (8 ms Ziel ausgewiesen) | Stoppuhr-Delta je Tick (T-021-Präzedenz) |
| 2 | Allokation je warmem Tick | ≤ 0 Bytes (= 0) | `GC.GetTotalAllocatedBytes(precise)`-Delta je Tick, summiert (Simulationsvertrag §5) |
| 3 | max reactionTicks | ≤ 3 hart (≤ 2 Ziel ausgewiesen) | Abschnitt 6 |
| 4 | Laufzeitshaderkompilierungen | == 0 | offline-shaderc-binaries-only |
| 5 | Zustandsketten-Selbstkonsistenz | == wahr | zweiter frischer Prozessdurchlauf im selben Prozess mit identischen Stichprobenticks und Endhash (K2-Anker; Cross-Prozess-Doppellauf bleibt Testevidenz) |

Im fensterpflichtigen Interaktivmodus entscheidet Kriterium 5 nicht (live
Geräteeingaben sind nicht deterministisch); der Report weist es als nicht
auswertbar mit maschinenlesbarem Grund aus statt es zu behaupten. Die
übrigen Kriterien gelten dort unverändert über das skriptgetriebene
Messfenster. Diagnosefelder — Framezeit, GPU-Zeit, Draw-/Submit-Aufrufe,
sichtbare Dreiecke, GC-Pausen, Working Set, aktive Agenten, Markerzahl — sind
ausschließlich informativ und tragen maschinenlesbar `gateCoupled=false`.

Pflichtprofile bleiben bis zur Benennung von Referenzrechnern `NOT-MEASURED`
(Q-OPS-001); Entwickler-PC-Läufe sind diagnostische Baseline. Der Report weist
maschinenlesbar aus, dass Q-TEC-010 sowie Q-GAM-001 bis Q-GAM-007 und
Q-NAR-002 offen bleiben.

## 8. Exitcodes

Neue Codes 35–38; alle bestehenden Bedeutungen (bis 34) bleiben unverändert:

| Code | Bedeutung |
|---|---|
| 35 | Kommandoschleifen-Gate verletzt (Tickzeit, Allokation, Reaktionsticks, Shaderkompilierung, Kettenkonsistenz); Report wurde dennoch geschrieben und klar als nicht bestanden markiert |
| 36 | Kommandoschleifenlauf unvollständig oder vorzeitig beendet; der Teilreport gilt ausdrücklich nicht als Evidenz |
| 37 | Kommandoschleifen-Szenario unbekannt oder Eingabeskript unlesbar/malformiert/außerhalb Wertebereiche; kein Report |
| 38 | Opt-in Einzelabgriff fehlgeschlagen; der Report wurde dennoch geschrieben und bindet `captured=false` mit Grund |

Präzedenz im Interaktivmodus (vertraglich gebunden durch den stabilen
Exitcode-Mapping-Test und die Präzedenzmatrix-Suite): Ein vorzeitiger Abbruch
vor Fensterabschluss ergibt stets Code 36 — auch wenn ein Abgriff angefordert,
aber wegen der Unvollständigkeit unterblieben war; sein Grund bleibt im Report
gebunden (`captured=false`). Bei abgeschlossenem Fenster dominiert ein
fehlgeschlagener opt-in Abgriff (Code 38) das Gateverdict (Code 35); sonst
entscheidet allein das Gateverdict.

Weiterhin gültig und wiederverwendet: 19 (Fensterinitialisierung fehlgeschlagen,
u. a. fehlendes Display im Interaktivmodus), 27 (Schemawiderspruch des
Reports), 28 (Reportpfad nicht schreibbar), 2 (Usage), 4 (fehlender Build via
rift.sh-Wache). Änderungen benötigen eine dokumentierte Entscheidung und die
Erweiterung des stabilen Exitcode-Mapping-Tests.

## 9. Keymap-Familie und Standardbelegung

Die Belegung ist datengetrieben und validiert semantische Aktionsnamen gegen
die Defaults (Testbindung). SDL-Scancodes des gepinnten Standes:

| Semantische Aktion | Defaults (Scancodes) |
|---|---|
| `quit` | Escape (41) |
| `pan-up` / `pan-down` / `pan-left` / `pan-right` | W(26)/S(22)/A(4)/D(7) sowie Up(82)/Down(81)/Left(80)/Right(79) |
| `zoom-in` / `zoom-out` | E(8), Equals(46) / Q(20), Minus(45) |

Maussemantik ist fixiert und nicht umbelegbar: Linke Taste Punkt-/Rahmenauswahl,
rechte Taste Befehl auf Zone unter dem Fadenkreuz (Zonenzugehörigkeit des
Bodenpunkts; außerhalb einer Zone erfolgt kontrollierte Abweisung mit
`target-not-in-zone`), mittlere Taste Zieh-Schwenken, Mausrad Zoom. Jede
Abweichung davon ist eine Vertragsänderung.

## 10. Opt-in Einzelabgriff

Unverändert nach dem T-023-/Media-Lab-Muster: Nur mit
`--capture-frame PFAD`, strikt nach dem Messfenster, genau ein
1920×1080-Einzelabgriff als unkomprimiertes 32-Bit-BMP, im Report hashgebunden
(SHA-256, Abmessungen, Format) mit der maschinenlesbaren Aussagegrenze
`graybox-state-occupancy-not-gameplay-atmosphere-or-shipping` (Graybox-
Zustandsbelegung — niemals Gameplay-, Atmosphären- oder Shipping-Beleg;
öffentliche Verwendung nur über `docs/communication/MEDIA_LAB.md` plus
Projektleitungsautorisierung). Ohne Flag entsteht keine Datei; das
Messverhalten ist identisch. Ein fehlgeschlagener Abgriff ergibt Code 38 mit
`captured=false` und Grund.

## 11. Geltungsbereich

Dieser Vertrag beschreibt ausschließlich die erste interaktive
Graybox-Kommandoschleife über der Vertragswelt
`riftward-simworld-graybox-v1`. Er begründet keine fachlichen Spielregeln
(Ressourcen, Wirtschaft, Kampf, Pause, Scheitern, Inhalte), keine
Minimap-, Fog-of-War-, Kontrollgruppen-, Formations- oder
Kontextbefehlssemantik, kein Save-/Replay-/Cooked-Format und keine
Cross-Plattform-Aussagen. GAME_DESIGN.md bleibt unberührt.
