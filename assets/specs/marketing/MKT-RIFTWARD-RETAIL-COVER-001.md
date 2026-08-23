# MKT-RIFTWARD-RETAIL-COVER-001

## Rolle und Aussagegrenze

- **Rolle:** vertikales Forschungs-Key-Art für die rein digitale Simulation
  einer PC-Boxfront und eines Handbuchcovers
- **Statusziel:** ausschließlich `quarantine`
- **Keine Aussage:** kein Gameplay, kein In-Engine-Render, kein Shipping-Asset,
  keine Titel-/Marken- oder kommerzielle Freigabe
- **Eingaben:** nur diese interne Spezifikation sowie die versionierten
  Riftward-Welt-, Atmosphären-, Art-, Performance- und Clean-Room-Regeln
- **Bildreferenzen:** keine

## Bildauftrag

Ein vollständig originelles, vertikales Weltmotiv zeigt eine sterbliche
Kartografin und zwei Feldingenieure von hinten auf einem regennassen Damm. Vor
ihnen liegt ein bewohnter, terrassierter Steinbruch mit reparierten
Holzbrücken, Wasserkanälen, kleinen Werkstattlichtern und Menschen bei
sichtbarer Arbeit. Der beschädigte Ringkontinent öffnet sich im Hintergrund;
eine einzelne geologische Überlagerung zeigt sich nur durch eine feine
amberfarbene Mineralader und eine physikalisch falsche Schattenkante.

Das Motiv erzählt persönliche Erkundung, gemeinschaftlichen Aufbau und
strategische Verantwortung in einem Bild. Es benötigt oben und unten ruhige
Flächen für später deterministisch gesetzte Typografie, enthält selbst aber
keine Schrift.

## Form, Farbe und Technik

- Hochformat ungefähr 2:3, lesbar als kleine Boxfront;
- stilisiert-realistisch und malerisch, glaubhafte Materialien, vereinfachte
  produktionsnahe Geometrie, starke mittlere und große Silhouetten;
- ungefähr 60 % gedämpfte Erde und Schiefer, 30 % entsättigtes Wald-/Sturmblau,
  10 % warmes Amber;
- ein dominantes Dämmerlicht, matte Druckanmutung, feines Papierkorn und
  kontrollierte Tonwerttiefe;
- keine moderne Hochglanz-Werbefotografie, keine UI und keine Effektwand.

## Harte Negativbedingungen

- keine Buchstaben, Wörter, Logos, Wasserzeichen, Siegel, Ratings oder
  Benutzeroberfläche;
- keine fremden Figuren, Symbole, Architektur, Karten, Packungen oder
  wiedererkennbare Markenmerkmale;
- kein übergroßes Heldengesicht, kein Palast, kein Drache, kein Neonportal,
  keine Laser-, Raytracing- oder Partikelshow;
- keine namentliche Stilimitation und keine externe kreative Referenz;
- nicht als Gameplay oder freigegebenes Produktmotiv kennzeichnen.

## Ausgeführter Produktionsprompt

```text
Use case: research marketing concept and digital retro PC-box simulation for an original FOSS game research project.
Asset type: completely original vertical key art, approximately 2:3, with no typography.
Primary scene: seen from behind, one mortal woman cartographer and two field engineers stand on a rain-darkened practical stone dam. Below them is an inhabited terraced quarry refuge with repaired timber bridges, water channels, small amber workshop lights, garden plots, and ordinary people visibly rebuilding. Beyond it, a wounded ring-continent opens toward distant layered ridges. One restrained geological temporal anomaly appears only as a thin amber mineral seam and one physically impossible shadow edge.
Narrative: personal exploration grows into responsibility for a community and landscape; intimate people, working settlement, and broad strategic terrain coexist in one continuous scene.
Composition: strong vertical cover composition, readable at thumbnail size; three small human silhouettes in the lower-middle foreground; inhabited refuge as central anchor; distant landmark and storm sky above; preserve calm dark areas near top and bottom for later deterministic external title treatment, but render no text.
Style and medium: original stylized-realistic painterly game-world illustration, tactile early-digital-era print feeling expressed through matte ink character, subtle halftone/paper grain, deep controlled values, believable materials, deliberately simplified production-feasible geometry, strong middle and large silhouettes. This is not a reproduction of any existing package, advertisement, game, publisher, artist, or trade dress.
Lighting and mood: wistful, earthy, wondrous, determined; blue-hour after rain; one dominant dusk direction; restrained warm inhabited islands; localized mist only.
Palette: about 60 percent muted earth and slate, 30 percent desaturated forest and storm blue, 10 percent warm amber accents.
Hard constraints: no letters, words, logo, watermark, seal, rating badge, user interface, mock UI, barcode, disc, box, publisher mark, giant face, palace, dragon, neon portal, lasers, glossy mobile-game aesthetic, dense particles, photobash artifacts, recognizable borrowed character, architecture, map, symbol, faction emblem, named-style imitation, or external creative reference. The image itself must not claim gameplay, in-engine, shipping, or release status.
```

Der verwendete Dienst bietet in diesem Aufruf kein getrenntes Negativpromptfeld;
alle Ausschlüsse sind deshalb im Hauptprompt gebunden.

## Ausgeführter Realismus-Refinement-Prompt (v3)

```text
Refine this image one more step into the unmistakable look of a carefully staged early-2010s PC real-time game promotional capture, while keeping the exact original world, three rear-facing field surveyors, quarry settlement, vertical composition and subdued mood.

Reduce contemporary generative-art appearance. Render the scene as coherent authored 3D assets under an era-appropriate custom engine: restrained medium-poly rock silhouettes, visibly modular but art-directed timber and masonry pieces, consistent one-meter scale, plausible collision-friendly paths, sparse individually placed vegetation, limited material families, 1024-era texture character, baked lightmaps and ambient occlusion, one directional dusk light, modest anisotropic highlights on wet stone, restrained fog, finite shadow distance, slightly compressed dynamic range and natural color grading. Keep forms readable and accept small period-authentic rendering limitations instead of hiding everything in microdetail.

Simplify the distant terrain into larger authored shapes. Reduce the number of tiny buildings and random props further. Make the inhabited area clearly functional: one workshop cluster, two garden terraces, a water channel, a lift crane, one bridge and several readable paths. Keep only a very small warm mineral anomaly near a retaining wall. Give the three characters practical, historically neutral work clothing with believable fabric folds and equipment; no face is visible.

The result should feel grounded, material, handcrafted, explorable and like a plausible game world from around 2010–2013, not a painterly illustration, modern photoreal film still, fantasy matte painting or AI-generated panorama. This is an original clean-room design and must not reproduce any existing title, publisher, package, artist or trade dress.

Keep calm dark space above and below for external title layout. No text, letters, logo, watermark, UI, badge, barcode, box, disc, giant face, palace, dragon, neon, lasers, particles, oversharpening, repeated structures, impossible geology, glossy mobile aesthetic or named-style imitation.
```

Der Edit erhielt ausschließlich die eigene v2-Quarantänevariante als
Bildinput. Sie wird im GenerationReceipt per Pfad und SHA-256 gebunden.

## Prüfvariablen

1. Trägt die Komposition eine schmale vertikale Verpackungsfront?
2. Bleiben drei menschliche Figuren, Schutzraum und Fernlandmark erkennbar?
3. Entsteht die Zielstimmung wehmütig, erdig, wundersam und entschlossen?
4. Bleibt der Anomalieakzent unter zehn Prozent der Bildwirkung?
5. Sind Schrift, Logos, UI und fremde Identifikationsmerkmale abwesend?
6. Wirkt die Szene wie kohärent gebaute Echtzeitgeometrie der frühen 2010er
   statt wie ein gegenwärtiges generatives Matte Painting?
