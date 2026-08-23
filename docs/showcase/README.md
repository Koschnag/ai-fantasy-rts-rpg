# Project Riftward GitHub Pages

Diese Seite ist der rein digitale Retail-Era-Showcase und öffentliche
Statuspunkt des FOSS-Forschungsprojekts.

## Lokal bauen

```bash
./scripts/build-pages.sh /tmp/riftward-pages
```

Der Build verwendet nur Bash, Git und POSIX-Textwerkzeuge. Zum lokalen Ansehen
genügt ein beliebiger statischer HTTP-Server auf `/tmp/riftward-pages`.

## Wahrheitsgrenze

- `status.json` wird beim Build aus Git und `BACKLOG.md` erzeugt.
- Die öffentliche Grafik ist ein deterministisches Projekt-SVG.
- Lokale Dateien aus `assets/quarantine/` werden nie in das Pages-Artefakt
  kopiert.
- Konzeptmaterial bleibt sichtbar `CONCEPT · NOT GAMEPLAY`.
- Der Showcase verspricht keine physische Ausgabe.
