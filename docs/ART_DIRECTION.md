# Art Direction und Audioidentität

## Zielbild

**Status:** ANGENOMMEN

Die Darstellung ist stilisiert-realistisch: glaubhafte Materialien und Beleuchtung, aber bewusst geformte Silhouetten, kontrollierte Details und eine malerische Farbdramaturgie. Das Qualitätsziel wird ausschließlich über kohärente Art Direction, sofort lesbare Silhouetten und Aktionen, saubere Animation, stimmige Beleuchtung und eine klare Effekthierarchie gemessen. Es verlangt weder einen AAA-Kampagnen-, Cinematic-, Multiplayer- oder Assetumfang noch möglichst teure Effekte.

## Visuelle Grammatik

- große, aus der Spielkamera lesbare Formen statt Mikrodetaillierung
- leicht überzeichnete Proportionen bei Einheiten und Gebäudeteilen
- PBR-nahe Materialwerte mit begrenzter Texturvielfalt und starkem Art Direction Pass
- gebackene Umgebungsbeleuchtung, Light Probes, gezielte dynamische Hauptlichter
- kein Raytracing und keine Echtzeit-Globalbeleuchtung; die höchste Qualitätsstufe muss auf RX-580-Klasse sinnvoll laufen
- kontrollierter atmosphärischer Nebel und Partikel mit festen Überzeichnungsbudgets
- Umgebung in entsättigten Natur- und Steinfarben; interaktive Elemente erhalten begrenzte Akzentfarben
- UI mit eigener Formsprache; keine nachgezeichneten Rahmen, Symbole oder Layouts bestehender Spiele

## Eigenständigkeitsregeln

Nicht als Referenzeingabe oder Produktionsquelle verwenden:

- extrahierte Screenshots, Modelle, Texturen, Sounds, Musik, Texte, Logos oder Karten fremder Spiele
- konkrete Figuren-, Kreaturen-, Gebäude- oder UI-Designs, die nur oberflächlich verändert werden
- Namen, Terminologie, Wappen, Melodien oder ikonische Farb-/Formkombinationen bestehender Marken

Erlaubt sind abstrakte Begriffe wie „melancholische High Fantasy“, „verwachsene megalithische Ruinen“, „langsamer Übergang von intimer Erkundung zu Strategie“ oder „klare RTS-Silhouetten“.

## Produktions-Bible je Kultur oder Biom

Vor Assetproduktion müssen jeweils feststehen:

- 5–8 Formbegriffe und eine Negativliste
- Palette mit funktionalen Rollen
- Materialien und Alterung
- Maßstab und Silhouettenhierarchie
- Architekturmodule und Verbindungspunkte
- Einheitensprache, Ausrüstung und VFX-Farbe
- 3 freigegebene Keyframes aus unabhängigen Prompts

## Audio

- dynamische Musikschichten statt dauerhaft maximaler Orchestrierung
- eigene Themen und Harmoniefolgen; keine Stilprompts mit lebenden Künstlern oder konkreten Soundtracks
- Umgebungsräume tragen einen großen Teil der Atmosphäre
- Effekte bleiben im Kampf spektral unterscheidbar und besitzen Prioritäten
- Stimmen sind im Vertical Slice optional; Text und Untertitel müssen immer vollständig funktionieren

## Technische Assetregeln

Die konkreten Budgets stehen in `PERFORMANCE_BUDGET.md`. Jedes Shipping-Asset benötigt:

- eindeutige ID, Quelle und Hash
- Prompt, Seed, Modell/Tool und Versionsangabe bei KI-Erzeugung
- Nutzungs- und Lizenznachweis
- automatische technische Validierung
- freigegebene LODs, Kollision, Pivot, Skalierung und Materialslots
- dokumentierte manuelle oder KI-basierte Nachbearbeitung
