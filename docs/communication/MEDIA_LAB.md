# Visual- und Media-Lab

Das Media-Lab produziert Material für Art Direction, Forschungserklärung und
spätere Kommunikation. Es ist von `assets/source` und `assets/cooked`
getrennt: Ein gutes Bild wird nicht automatisch zu einem Spielasset.

## Aktuelles Inventar

| ID | Medium | Zweck | Status |
|---|---|---|---|
| ENV-QUARRY-REFUGE-KEYFRAME-001 | KI-Konzeptbild | Strategie-Kamera, Schutzraum, Materialverteilung | lokale Quarantäne; technische/interne visuelle Prüfung bestanden, unabhängige Lizenz-/Originalitätsprüfung offen |
| ENV-FLOODED-CAUSEWAY-KEYFRAME-002 | KI-Konzeptbild | stille Erkundung und Umweltgeschichte | lokale Quarantäne; visuelles Review `needs-work` wegen vier statt drei Resonanzsteinen |
| ENV-SLUICE-DEFENSE-KEYFRAME-003 | KI-Konzeptbild | Formationen, Zivilisten und Geländehebel | lokale Quarantäne; keine Shipping-Freigabe |
| CAL-STONEWOOD-V1-39FAAE34C4CD | prozedurales GLB + Preview | deterministische 3D- und Inspector-Kalibrierung | lokale Quarantäne; technisches Testasset |
| MKT-RIFTWARD-RESEARCH-HERO-001 | KI-Hero-Konzept | Erkundung → Aufbau → Verteidigung | neu erzeugt, kanonische Provenienz, lokale Quarantäne; kein Gameplaybeleg |
| MKT-RIFTWARD-RETAIL-COVER-001 | KI-Coverstudie | geerdete Echtzeit-3D-Anmutung der frühen 2010er für die digitale Retail-Erinnerungswelt | vierte lokale Variante, kanonische Provenienz, Quarantäne; nicht auf Pages und kein Gameplaybeleg |
| MKT-RIFTWARD-ATMOSPHERE-STUDY-001 | drei KI-Konzeptbilder + deterministische Filmrolle | Morgen, Arbeit, Versorgung und Gemeinschaft als erreichbare Echtzeit-Anmutung | Quarantänequellen; kleine hashgebundene Pages-Exporte mit sichtbarer Konzeptkennzeichnung; kein Gameplaybeleg |
| `docs/media/*.svg` | eigene deterministische SVG-Grafiken | GitHub- und Forschungsvisualisierung | versionierbar; keine Runtime-/Gameplayaussage |
| EVD-T033-MODE-PAIR-001 | lokales In-Engine-Abgriffpaar (zwei 1920×1080 32bpp BMP, `kommandoschleife --interactive --capture-frame`, T-033) | unterscheidbare Graybox-Zustandsbelegung beider Spielmodi (strategische Übersicht gegenüber persönlicher Verfolgungsperspektive) über demselben gebundenen Weltzustand | lokale Evidenz, Report-/hashgebunden (boundTick 890, boundStateHash `3559ad7791a5010f`; strategisch `b83167b6…51d`, persönlich `f8dbd051…6a0`; Report unter gitignoriertem `artifacts/t033-review/interactive-real-display-fixed.json`); Aussagegrenze `graybox-state-occupancy-not-gameplay-atmosphere-or-shipping` — niemals Gameplay-, Atmosphären- oder Shipping-Beleg; öffentliche Verwendung nur nach diesem Vertrag plus Projektleitungsautorisierung |
| EVD-T034-EXPLORATION-PAIR-001 | lokales In-Engine-Abgriffpaar (zwei 1920×1080 32bpp BMP, echter Wayland-/RX-570-Lauf mit `--exploration --interactive --auto-exit-at-horizon --capture-frame`, T-034) | technische Bindung des abgeschlossenen 6/6-Erkundungszustands in strategischer und persönlicher Darstellung über demselben Weltzustand | `needs-work`: Report-/hashgebunden (boundTick 6800, boundStateHash `9b1c73996becfcf8`; strategisch `3c8183b2…db48`, persönlich `790984fc…4fe4`; Report unter gitignoriertem `artifacts/t034-review/interactive.json`), aber die Sichtprüfung bestätigt die vorregistrierte Zwei-Sekunden-Lesbarkeit nicht (strategisch Zustand außerhalb des unveränderten Kamerastands, persönlich Agentenüberdeckung). Aussagegrenze `graybox-state-occupancy-not-gameplay-atmosphere-or-shipping`; niemals Gameplay-, Atmosphären- oder Shipping-Beleg, kein AC-T034-04-Pass, keine öffentliche Verwendung |

Die Raster- und 3D-Originaloutputs liegen absichtlich im ignorierten
`assets/quarantine/`. Git enthält Specs, Manifeste und Receipts. Ein
ausdrücklich autorisierter Forschungs-/Kommunikationsexport darf zusätzlich
eine kleine, hashgebundene Ableitung unter `docs/showcase/assets/` enthalten.
Diese Ableitung bleibt außerhalb von `assets/source`, `assets/cooked` und jedem
Shipping-Paket. Das verhindert, dass eine anschauliche Kommunikationsidee
versehentlich zum Spielasset oder Gameplaybeleg wird.

## Öffentliche Quarantäne-Exporte

Ein Quarantänekonzept darf nur als transparenter Forschungsgegenstand im
öffentlichen Showcase erscheinen, wenn alle folgenden Bedingungen erfüllt
sind:

- technische und interne visuelle Prüfung sind `pass`; `needs-work` wird nie
  exportiert;
- die Projektleitung hat die konkrete öffentliche Verwendung autorisiert;
- Quelle, Prompt/Spec, Generatorlücken, Transformation und Output-Hash stehen
  in einem öffentlichen Manifest;
- unmittelbar am Medium steht `CONCEPT · NOT GAMEPLAY`;
- die Seite behauptet weder Shipping-Freigabe, Runtime-Ursprung, Lizenzklarheit
  noch eine bestandene unabhängige Originalitätsprüfung;
- ein späteres negatives Review entfernt den Export wieder.

Der fehlgeschlagene Causeway-Keyframe bleibt beispielsweise trotz technischer
Gültigkeit unveröffentlicht, weil sein visuelles Review `needs-work` lautet.

## Produktionswarteschlange

| Paket | Ergebnis | Abhängigkeit / Gate |
|---|---|---|
| MEDIA-01 · World scale | sechs unabhängige Keyframes für persönlich, gemeinschaftlich und mythisch | drei saubere Specs je Motivfamilie, Modellterms, Originalitätsreview |
| MEDIA-02 · Shape language | modulare 3D-Prop-Familien für Stein, Holz, Wasserbau und Werkzeuge | T-050, GLB-/LOD-/Material-/Budgetprüfung |
| MEDIA-03 · Motion language | 6–12 s Turntables sowie Laufen, Arbeiten, Tragen, Signalisieren | Rig-/Clipvertrag, Foot-Slide- und Silhouettengate |
| MEDIA-04 · Research teaser | 35–45 s Video nach `STORYBOARD-001.md` | nur freigegebene Medien, Audio-/Fontrechte, Claim-Review |
| MEDIA-05 · Benchmark film | Split-Screen aus Build, Framegraph und Zielhardware | T-020, identischer Commit/Seed/Preset, ungeschnittene Trace-Referenz |
| MEDIA-06 · Monthly evidence reel | automatischer Monatsrückblick aus akzeptierten Evidence Cards | strukturierter Export, Secret-Redaction, menschliches Veröffentlichungsreview |

„Animation“ bedeutet im Projekt ein versioniertes Rig-/Clip-Artefakt mit
technischer Prüfung; „Movie“ bedeutet ein reproduzierbarer Schnitt mit
Shotliste, Quellen, Audio- und Claimfreigabe. Ein bewegtes KI-Moodpiece ohne
diese Bindung bleibt ein Experiment, nicht Produktwerbung.

## Gemeinsamer Vertrag je Medium

Jedes Medium erhält:

- ID, Zweck, Zielkanal und klare Aussagegrenze
- intern begründete Clean-Room-Spezifikation
- Prompt/Seed/Modell oder Generator-/Toolchainbindung
- Eingabe- und Output-Hashes sowie Transformationen
- technische Budgets für Auflösung, Länge, Codec, Geometrie und Speicher
- visuelles, Originalitäts- und Lizenzreview durch getrennte Rollen
- Kennzeichnung als Konzept, In-Engine, Gameplay oder Shipping

## Varianten statt Masse

Neue Bilder werden nicht nur „schöner“ gemacht. Jede Variante prüft eine
konkrete Frage, zum Beispiel Kameradistanz, Einheitensilhouette, 60/30/10-
Farbverteilung, Zivilistenlesbarkeit oder Produktionsaufwand. Pro Iteration
wird möglichst eine Variable geändert und das Review dokumentiert.

## Automatisierbarer Export

Sobald Rechte und Quellen freigegeben sind, soll ein eigener Exportjob:

1. nur explizit freigegebene Manifest-IDs akzeptieren,
2. Titel/Untertitel außerhalb des KI-Bildes deterministisch setzen,
3. Formate für GitHub, LinkedIn und Video-Storyboard rendern,
4. sichtbare `CONCEPT – NOT GAMEPLAY`-Kennzeichnung bei Konzepten erzwingen,
5. eine maschinenlesbare Quellenliste neben jeden Export schreiben.

Der Showcase darf neben SVG-Forschungsgrafiken ausdrücklich gekennzeichnete
Quarantäneableitungen zeigen. Echte Runtime-Aufnahmen ersetzen diese Motive
erst, wenn Commit, Szene, Seed, Preset und Evidence Pack reproduzierbar sind.
