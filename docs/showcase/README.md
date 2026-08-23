# Project Riftward GitHub Pages

Diese Seite ist der rein digitale Retail-Era-Showcase und öffentliche
Statuspunkt des FOSS-Forschungsprojekts.

## Lokal bauen

```bash
./scripts/build-pages.sh /tmp/riftward-pages
```

Der Build verwendet Bash, Git, Coreutils und POSIX-Textwerkzeuge. Zum lokalen
Ansehen genügt ein beliebiger statischer HTTP-Server auf
`/tmp/riftward-pages`.

## Wahrheitsgrenze

- `status.json` wird beim Build aus Git und `BACKLOG.md` erzeugt.
- Lokale Originale aus `assets/quarantine/` werden nie automatisch in das
  Pages-Artefakt kopiert.
- Explizit autorisierte, technisch und intern visuell geprüfte
  Quarantäneableitungen dürfen als hashgebundene Forschungs-/Webexporte unter
  `docs/showcase/assets/` liegen. Ihr
  [`media-manifest.json`](assets/media-manifest.json) dokumentiert Quellen,
  Generatorlücken und Transformationen.
- Konzeptmaterial bleibt unmittelbar am Medium sichtbar
  `CONCEPT · NOT GAMEPLAY`; es ist kein Shipping- oder Lizenzclaim.
- Der Showcase verspricht keine physische Ausgabe.

## Gestaltungsregeln

- mobile-first und auf echten schmalen Viewports geprüft;
- Fließtext bleibt auf eine lesbare Zeilenlänge begrenzt;
- Bildbreite und -höhe werden im Markup reserviert; nur das Hero-Motiv lädt
  eager, die Bildstrecke lazy;
- keine automatisch startenden Medien, keine rein dekorative Dauerbewegung;
- die Seite nutzt ein kleines Set benannter Farb-, Typografie- und
  Abstands-Tokens statt komponentenweiser Zufallswerte;
- editorialer Satz, klare Folios und rechteckige Kontaktbögen ersetzen
  austauschbare Glassmorphism-, Gradient- und Kartenmuster.
