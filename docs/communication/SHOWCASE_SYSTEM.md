# Retail-Era-Showcase-System

## Zweck

Der rein digitale Showcase übersetzt ADR 005 in eine wiederholbare
Kommunikationsschicht.
Er ist kein separater Markenbaukasten und keine zweite Produktwahrheit. Inhalt,
Status und Claims stammen aus versionierten Riftward-Artefakten; ein Renderer
setzt sie für Web, Druck oder Film um.

```text
Task + Commit + Evidence + Media-Manifest
                 |
                 v
          Showcase-View-Model
             /    |    \
          Web    Print   Motion
```

## Artefaktfamilie

| Artefakt | Vorproduktionsinhalt | spätere Evidence-gebundene Ablösung |
|---|---|---|
| virtuelle große Box / Boxfront | deterministische Weltgrafik oder freigegebenes Key-Art | final freigegebenes Covermotiv |
| virtueller optischer Datenträger | typografische Projektgrafik ohne Releaseclaim | reproduzierbares Master-/Build-Manifest |
| digitales Handbuch | Weltprämisse, Steuerungsprinzip, Hardware- und Forschungsziel | getestete Bedienung, echte Karten und Regeln |
| Magazinanzeige | Forschungsfrage, ehrlicher Stand und Repository-Link | validierte Meilensteinbotschaft |
| Bildstrecke | klar bezeichnete Konzepte und Pipelinegrafiken | echte In-Engine-Screenshots mit Commit/Seed/Preset |
| Film | Storyboard/Animatic | commitgebundener Build, Shot- und Quellenmanifest |

## Pflichtfelder jedes Exports

- Export-ID, Erzeugungszeit, Quellcommit und Renderer-Version;
- Medien-ID, Status und sichtbare Aussagegrenze;
- `CONCEPT – NOT GAMEPLAY`, solange mindestens ein gezeigtes Motiv Konzept ist;
- bei Runtimebildern Build, Szene, Seed, Preset und Evidence-Pack;
- bei Kennzahlen Messumgebung, Metrik, Unsicherheit und Link zum Rohbeleg;
- maschinenlesbare Quellenliste ohne Secrets oder lokale Hostpfade.

## Meilenstein-Regel

Der Showcase exportiert nicht bei jeder Agentenantwort. Ein neues öffentliches
Paket entsteht nur nach einem akzeptierten Meilenstein oder einem ausdrücklich
als Failure Note veröffentlichten Nullresultat. GitHub erhält grüne,
nachvollziehbare Checkpoints; lokaler Dirty State ist kein Fortschrittsclaim.

## Aktueller Prototyp

`docs/showcase/index.html` bildet Box, Datenträger, Handbuch, Anzeige und
Bildstrecke als digitale Interaktion ab. Er benötigt keinen Build, kein
Framework und kein Netzwerk. Im öffentlichen Checkout nutzt er ausschließlich
deterministische SVG-/CSS-Medien. Ein lokales Quarantänebild darf nur in einer
getrennten Reviewansicht erscheinen und wird nie automatisch publiziert. Der
Showcase kündigt weder Herstellung noch Versand einer physischen Ausgabe an.

Die GitHub-Pages-Pipeline baut daraus bei jedem relevanten Checkpoint ein
minimales statisches Artefakt. `status.json` entsteht aus Commit, Backlog und
T-010-Taskstatus; lokale Prozessdaten und Quarantäneausgaben werden nicht
publiziert. Die Seite dokumentiert damit den versionierten öffentlichen Stand,
nicht die bloße Aktivität eines Terminals.
