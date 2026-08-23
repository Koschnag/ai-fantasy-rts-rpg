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
| `docs/media/*.svg` | eigene deterministische SVG-Grafiken | GitHub- und Forschungsvisualisierung | versionierbar; keine Runtime-/Gameplayaussage |

Die Raster- und 3D-Outputs liegen absichtlich im ignorierten
`assets/quarantine/`. Git enthält Specs, Manifeste und Receipts, aber kein
unfreigegebenes Binärmaterial. Das verhindert, dass eine anschauliche
Kommunikationsidee versehentlich in die Shipping-Pipeline rutscht.

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

Bis dahin bleiben die SVG-Forschungsgrafiken das öffentliche visuelle
Grundgerüst.
