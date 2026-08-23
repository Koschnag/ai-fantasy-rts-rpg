# Laufendes Fallstudienprotokoll

Das Protokoll ist append-only auf Ebene der Beobachtungen: Korrekturen
supersedieren einen Eintrag sichtbar, statt historische Ergebnisse umzudeuten.

## CS-000 – Ausgangsbaseline

- **Stichtag:** 2026-08-23
- **Commit:** `9637ec8627bfacbf598bdecf8b77965bdf556655`
- **Phase:** Produktionsfundament; Spielruntime noch nicht implementiert
- **Akzeptiert:** T-001 bis T-007
- **Nächster Auftrag:** T-010 war am Stichtag spezifiziert, unabhängig geprüft
  und `READY`, aber noch nicht implementiert
- **Beobachtbare Fähigkeiten:** Run-Ledger, Memory/RAG, Evidenz- und
  Retentionverträge, Assetquarantäne/-provenienz, unabhängiger 3D-Inspector,
  deterministischer lokaler GLB-/PNG-Generator und Fresh-Checkout-CI
- **Medienstand:** drei KI-Konzept-Keyframes sowie eine lokale prozedurale
  3D-Kalibrierungsfamilie existieren in Quarantäne; sie sind weder Shipping
  Assets noch Gameplay-Nachweise
- **Nicht belegt:** mehrtägige produktive Autonomie, fertige Runtime,
  Spielspaß, Zielhardwareleistung, ökologische Amortisierung oder vollständige
  kreative/rechtliche Freigabe

Diese Baseline verhindert, dass spätere Kommunikation Vorproduktion und
laufendes Produkt verwechselt.

## CS-001 – Kommunikations- und Forschungsrahmen

- **Datum:** 2026-08-23
- **Frage:** Lässt sich die laufende Entwicklung bereits als überprüfbare
  Fallstudie und nicht nur als Fortschrittsbericht strukturieren?
- **Änderung:** Forschungsprotokoll, CCD-Abbildung, Kampagnenpaket,
  Medieninventar, Storyboard und deterministische Repository-Grafiken angelegt
- **Neues KI-Medium:** `MKT-RIFTWARD-RESEARCH-HERO-001` lokal erzeugt, Prompt,
  Output, Run und Receipt kanonisch gebunden; Status bleibt `quarantine`
- **Grenze:** Die offiziellen OpenAI-Produktdokumente identifizieren für das
  eingebaute Werkzeug weder das exakte Modellartefakt noch dessen Seed und
  liefern in den geprüften Produktseiten keinen hinreichenden lokalen
  Lizenzsnapshot. Das Rasterbild wird daher nicht als freigegebene
  Repository-Bildquelle behandelt.
- **Outcome:** Die öffentliche Darstellung kann mit eigenen SVG-Grafiken und
  überprüfbaren Projektfakten beginnen; KI-Rastermedien benötigen weiter das
  getrennte Freigabeverfahren.

## CS-002 – Digitaler Retail-Era-Showcase und realistischere Coverstudie

- **Datum:** 2026-08-23
- **Frage:** Lässt sich das Gefühl einer PC-Retail-Veröffentlichung der Jahre
  2000 bis 2010 rein digital, eigenständig und wahrheitsgebunden darstellen,
  während ein Rasterexperiment gezielt näher an eine geerdete Echtzeit-3D-
  Darstellung der frühen 2010er geführt wird?
- **Änderung:** Abhängigkeitsfreie GitHub-Pages-Ausgabe mit virtueller Box,
  Datenträger, Handbuch, Magazinanzeige und Bildstrecke; eigene SVG-/CSS-
  Darstellung bleibt der einzige öffentliche Bildträger. Die FOSS-Richtung
  ist festgehalten, die konkreten SPDX-Lizenzen je Artefaktklasse bleiben eine
  offene Projektleitungsentscheidung.
- **Medienexperiment:** `MKT-RIFTWARD-RETAIL-COVER-001` wurde in vier lokalen
  Varianten von einer malerischen Panoramaanmutung zu gröberen, plausibleren
  Engine-Modulen, begrenzten Materialien, lesbaren Wegen und zurückhaltender
  Beleuchtung verengt. Die vierte Variante bleibt lokale Quarantäne und wird
  weder als Gameplay noch als freigegebenes Marketingmaterial verwendet.
- **Fehlversuch:** Der erste Receipt-Export band einen nicht kanonisch
  serialisierten Prompt-Envelope-Hash. `assets-check` erkannte die Abweichung;
  ein frischer Run mit dem vom Harness verwendeten Canonical-Envelope-Format
  ersetzte den fehlerhaften Export, ohne dessen Historie als Erfolg
  umzudeuten.
- **CI-Gegenbeispiel:** Der lokale Audit fand den ignorierten v3-Rasterinput,
  der Fresh Checkout dagegen nicht. Dadurch wurde sichtbar, dass
  `RequireLocal=false` nur für Quarantäne-Outputs, nicht für gleichartige
  Inputs wirkte. Der Vertrag erlaubt nun fehlende Quarantäne-Inputs im
  Repository-Audit, während `--require-local` weiterhin explizit mit
  `ASSET_INPUT_MISSING` scheitert; unsichere Pfade, Symlinks und falsche
  Hashes bleiben Fehler.
- **Outcome:** Die öffentliche Erzählung ist ohne Rasterfreigabe testbar. Das
  Provenienzgate hat dabei nicht nur Erfolg dokumentiert, sondern einen realen
  Integrationsfehler vor Veröffentlichung abgefangen.

## Vorlage für Folgeeinträge

```text
## CS-XXX – Titel

- Datum / Ausgangscommit / Ergebniscommit
- Forschungsfrage und vorab erwartetes Ergebnis
- Agent, Modell, Toolchain und Zeitfenster
- Requests, Tokens/Kosten und menschliche Eingriffe
- Gates, Tests, Hardware- und Medienartefakte
- Ergebnis: unterstützt / widerspricht / unentscheidbar
- Fehlversuche, Datenlücken und Störfaktoren
- nächste engere Frage
```
