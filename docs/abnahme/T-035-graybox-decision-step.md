# Abnahme T-035 – Graybox-Entscheidungsschritt

**Status:** Fertiggestellter Builder-Kandidat zur unabhängigen Reviewphase.
Der gatende Abschnitt 0 (versionierter Entscheidungsvertrag V1), der
headless prüfbare Entscheidungs-Flow, die A/B-Beobachtungstreue, die
Testmatrix (295/295) und alle lokalen Gates sind grün. Interaktivsmoke und
Playtestausführung bleiben wegen der displaylosen Umgebung ausgewiesene
Restpunkte mit kontrolliertem Code-19-Nachweis (Präzedenz
T-023/T-032/T-033/T-034). Diese Datei beschreibt die aktuelle
Produktwahrheit; sie behauptet keinen noch nicht ausgeführten Gate-Erfolg.

## Ausgangslage und Fortsetzung

Der vorherige Builder-Lauf (Receipt-Sequenz 148, Terminalstatus
`PROCESS_ERROR` nach Providerfehlern) hatte den gatenden Abschnitt 0
(`docs/ENTSCHEIDUNGSVERTRAG.md` V1), die sitzungsseitige Entscheidungsschicht
(`DecisionContract`, `DecisionSession`), die Skriptgrammatik v3, die
Pipeline-/Report-Integration und den Großteil des
Kommandoschleifen-Reports geliefert und brach unmittelbar nach dem
`decisionSession`-Reportblock ab. Dieser Lauf hat den unveränderten
Teilstand fortgesetzt und die fehlenden Anteile vollendet:

- `InteractiveView.BindDecision` samt vertraglichem Folgezielmarker-Kanal
  (`follow-up-marker-channel-v1`): dreistufige Markiersäule (Diamantebenen
  1,2/2,4/3,6 m; untere Ebene ruhend π/4, mittlere und obere Ebene rotieren
  mit der Tickzahl; Größen 1,30/1,15/1,00) in warmem Violett
  (0,86/0,45/0,98) am bestehenden Landmarkenanker der gewählten Zone,
  MarkerCapacity + 3, aktiv ab der Entscheidung bis zum Sitzungsende.
- `CommandReportSchema`: rein additive Schemaversion 4 mit
  Dispatch-/Body-Bindung (`decisionSession`-Pflichtblock in Version 4,
  abgewiesen in 2 und 3), relationaler fail-closed Bindung (Optionszonen
  verschieden, gewählte Zone ist Angebotszone und wahlgemäß, Folgenzone ist
  Wahl, Ankunft an oder nach der Entscheidungsgrenze, Abschluss und
  Ankunftsgrenze konsistent, ohne Angebot keine Entscheidung/Folge,
  Abweisungszähler nichtnegativ), Messflag-Verdrahtung der fensterpflichtigen
  Ausweise und Skriptformatbindung v3.
- Optionsableitung als reine, testbare Funktion (`DeriveOptions`) mit
  vertraglichem Fail-closed-Vertragsfehler im Degenerationsfall.
- `null` statt leerer Zeichenketten für nicht gefallene Wahl-/Modusangaben im
  unentschiedenen Reportblock (Vertrag: Sentinel −1, nicht gefallene Angaben
  null).
- Vertragskonstanten der Markerfarben, Kommandoschleifen-Verdrahtung der
  Entscheidungsabweisungen mit vertraglichen Kennungen, Titel-HUD-Zustände.

## Vertrag (Abschnitt 0)

`docs/ENTSCHEIDUNGSVERTRAG.md` V1 wurde vor der Implementierung festgelegt
und bindet je Wahl Alternativen, Gründe, messbare Playtestkriterien und
Rückrollweg: Auslöseregel `completion-gated-decision-offer-v1`, Options-
ableitung `visit-protocol-zone-options-v1`, Entscheidungseingabe
`graybox-input-script-v3` mit `decision-choose-personal-mode-only-v1` und
Auswertungsordnung `decision-choice-evaluation-order-v1`, Folgeregel
`chosen-zone-follow-up-objective-v1` mit
`boundary-arrival-personal-mode-only-v1`, Feedback
`title-hud-decision-objective-v1`/`follow-up-marker-channel-v1`, Aktivierung
`opt-in-decision-activation-v1`, Nichtpersistenz
`decision-session-local-not-persisted-v1`, Exitcode-Erhaltung. Das Dokument
antwortet auf keine offene Produktfrage (Q-GAM-001 bis Q-GAM-007, Q-GAM-010,
Q-NAR-002, Q-NAR-004, Q-TEC-004, Q-TEC-006, Q-TEC-010, Q-OPS-001 bleiben
OFFEN).

## Beobachtungstreue-Evidenz (Fresh-Prozess, seed 20260826, Horizon 8000)

- A/B-Wahlpaar (`t035-decision-choose-a`/`t035-decision-choose-b`): identische
  Kernintents und identischer Tickhorizont erzeugen byteidentische
  Kettenstichproben und denselben Endhash `cfdafa670fccdeea` bei
  unterscheidbaren Entscheidungsreports (A: Wahl `a`, Folgenzone 0, offen;
  B: Wahl `b`, Folgenzone 4, abgeschlossen an der Entscheidungsgrenze 7300,
  da der Vertragshelde dort steht). Doppelprozess A/A2 builderidentisch.
- Angebotsöffnung an der Erkundungsabschlussgrenze 7210 (Protokollfolge
  0, 2, 1, 5, 3, 4; Optionsableitung A=Z0, B=Z4), genau einmal je Sitzung.
- Zwilling ohne Entscheidungsschicht und Fremdseed nachweislich gebunden
  (Suite); ohne Abschluss innerhalb des Laufs trägt der Report den ehrlichen
  Grund `exploration-not-completed-within-run` statt stiller Leere.
- Vollständiger Flow (Angebot → Wahl `a` → strategische Mobilmachung →
  persönliche Ankunft → Abschluss, Horizont 12000): Endhash
  `a5acf5150e460cff`, Ankunftsgrenze 7857 nach der Entscheidungsgrenze 7300.
- Kernwahrheit unberührt: `git diff` gegen den Vorblob zeigt für
  `src/Riftward.Simulation/` und die Vertragsdokumente GAME_DESIGN,
  ANFORDERUNGEN, ERKUNDUNGSVERTRAG, KOMMANDOVERTRAG, MODEVERTRAG exakt
  keine Änderung.

## Gates und Regressionen (dieser Lauf)

- `rift.sh fmt` (Fantomas), `lint` (0 Befunde, Toolchain-/Lizenz-/ISA PASS),
  `build` (Release, 0 Warnungen), `test` (295/295, davon 15 neue
  T-035-Einheiten), `security` PASS, `rag-build`, `verify` valid
  (runsChecked=67).
- Regressionen der Bestandsbefehle: `bench-sim` gate.pass=true,
  `savecheck` gate.pass=true, Soak-Kurzlauft `--diagnostic-accelerated
  --horizon-ticks 3000` (diagnostisch, Exit 0), `kommandoschleife` Legacy
  über die Bestandsfixtures in der Suite.
- Displaylos: interaktiver Lauf bricht kontrolliert mit Code 19
  (`SDL3-Videoinitialisierung fehlgeschlagen`) ohne Report ab; Interaktiv-
  smoke, Playtestausführung und der opt-in Abgriff bleiben Restpunkte einer
  Displaysession. Es wurde kein Abgriff produziert; daher ist kein
  Media-Lab-Eintrag entstanden.

## Testmatrix (Auszug der neuen Bindungen)

Vertragsspiegel (Kennungen, Keymap 1/2, Markerabmessungen, Schemaversionen),
reine seedunabhängige Optionsableitung mit Fail-closed-Degenerationsfällen,
Angebots-/Wahl-/Folgen-/Ankunftskopplung am echten Lauf, persönliche
Ankunftskopplung inklusive strategischer Nichtabschlussprobe,
Twin-/Fremdseed-/Stufe-1-Bindung ohne Kernwirkung, Titel-HUD-Zustandsformen
ohne Bestandsänderung, CLI-Schemaversion-4-Flow mit Doppelprozessbindung,
A/B-CLI-Paar, Usage-Kopplung (`--decision` ohne `--exploration` → 2), Legacy-
Ablehnung von `choose` unter v1-/v2-Köpfen (37), Schema-Dispatch- und
Fabrikationsmatrix, Builder-Ehrlichkeit der Darstellungsausweise.

## Begleitkorrekturen im Primärslice

- Der T-034-Caller-Bindungstest las das HUD-Verdrahtungsfragment mit der
  alten `UpdateTitleHud`-Signatur; das Fragment wurde auf die additive
  `decision`-Übergabe fortgeschrieben (Bindungsabsicht unverändert).
- Der T-032-Schemamatrix-Test mutierte auf Schemaversion 4, die durch diesen
  Slice gültig wurde; die Mutation prüft jetzt Version 5 (außerhalb der
  erlaubten Versionen). Keine bestehende Exitcode- oder Gatebedeutung wurde
  geändert; kein bereits akzeptiertes zweites Task-Manifest wurde berührt.
- Review-Schärfung (unabhängiger Reviewlauf, 2026-08-28): Die relationale
  Schemabindung von `decisionSession` bindet jetzt entscheidungsstand-
  unabhängig fail-closed — Abschluss-/Ankunftsaussage, „ohne Angebot keine
  Folge" und die Sentinel-Wahrheit vor der Wahl (ohne Entscheidung gibt es
  keine gewählte Zone und keine Folge) gelten auch für `decided=false`;
  Fabrikationsfälle ohne Entscheidung werden abgewiesen. Keine Vertrags-
  änderung: Abschnitt 8 des Entscheidungsvertrags dokumentierte diese
  Bindung bereits; die Fabrikationsmatrix prüft die beiden neuen
  Ablehnungen (Testbestand unverändert 295/295).

## Restpunkte

- Interaktivsmoke, Playtestausführung (vertragsvorregistrierte Kriterien
  des Abschnitts 10) und der höchstens eine opt-in Abgriff erfordern eine
  Displaysession (Entwickler-PC oder virtuelles Wayland nach T-023-Präzedenz)
  und bleiben ausgewiesene Restpunkte.
- Pflichtprofile bleiben `NOT-MEASURED` (Q-OPS-001); die Entscheidungsschicht
  erzeugt keinen neuen budgettragenden Pfad.
- Persistenzwahrheit von Modus, Erkundung und Entscheidung bleibt gemäß
  ADR 008 Sequenzierungsnote der späteren Savevertrags-Erweiterung
  vorbehalten.
