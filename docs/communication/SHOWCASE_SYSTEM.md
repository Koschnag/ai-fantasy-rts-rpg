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

`docs/showcase/index.html` bildet Atmosphärendossier, Filmrolle, Box,
Datenträger, Handbuch, Anzeige und Bildstrecke als digitale Interaktion ab. Er
benötigt kein Framework und zur Laufzeit kein Netzwerk. Im öffentlichen
Checkout nutzt er deterministische SVG-/CSS-Medien sowie explizit
autorisierte, hashgebundene Ableitungen geprüfter Quarantänekonzepte. Die
Originale bleiben lokal; das öffentliche Manifest legt Generatorlücken,
Transformationen und Aussagegrenze offen. Der Showcase kündigt weder
Herstellung noch Versand einer physischen Ausgabe an.

Die GitHub-Pages-Pipeline baut daraus bei jedem relevanten Checkpoint ein
minimales statisches Artefakt. `status.json` entsteht aus Commit, Backlog und
T-010-Taskstatus; lokale Prozessdaten und Quarantäneoriginale werden nicht
publiziert. Die Seite dokumentiert damit den versionierten öffentlichen Stand,
nicht die bloße Aktivität eines Terminals.

## UI-/UX-Begründung

Der Showcase verwendet keine beliebige Sammlung moderner Karten, weicher
Verläufe und Glassmorphism. Seine visuelle Grammatik stammt aus einem digitalen
Redaktionsdossier: Folios, Kontaktbögen, harte Linien, begrenzte Satzbreiten,
große Bildstrecken und klar getrennte Fakt-/Konzeptzustände. Ein kleines Set
von Design-Tokens hält Farbe, Typografie, Abstand und Bewegungsregeln konsistent.

Die konkrete Umsetzung folgt folgenden öffentlichen Leitlinien:

- [USWDS Design Principles](https://designsystem.digital.gov/design-principles/):
  echte Nutzerbedürfnisse, zugängliche und nachvollziehbare Information;
- [GOV.UK Layout](https://design-system.service.gov.uk/styles/layout/):
  mobile-first, einfache Hierarchie und ungefähr 75 Zeichen maximale
  Fließtextbreite;
- [W3C WAI Designing Tips](https://www.w3.org/WAI/tips/designing/) und
  [Audio/Video Content](https://www.w3.org/WAI/media/av/av-content/):
  Kontrast, semantische Alternativen, sichtbare Mediensteuerung und eine
  Textbeschreibung der stummen Filmrolle;
- [web.dev zu Layout Shifts](https://web.dev/articles/optimize-cls) und
  [nativem Lazy Loading](https://web.dev/articles/browser-level-image-lazy-loading):
  feste Medienmaße, eager nur für das Hero-Motiv, lazy für Offscreen-Bilder;
- [Atlassian Design Tokens](https://atlassian.design/foundations/tokens/design-tokens/)
  und [Motion](https://atlassian.design/foundations/motion): konsistente Tokens
  und Bewegung nur zur Zustandsklärung;
- [WCAG 2.2: Animation from Interactions](https://www.w3.org/WAI/WCAG22/Understanding/animation-from-interactions)
  sowie [`prefers-reduced-motion`](https://developer.mozilla.org/en-US/docs/Web/CSS/Reference/At-rules/%40media/prefers-reduced-motion):
  kein Autoplay und abschaltbare Panelbewegung.

Die Leitlinien definieren keine Marke. Sie begrenzen die gestalterische
Willkür; die eigenständige Riftward-Sprache entsteht aus Inhalt, Satz und
Materialwelt.
