# T-052 – Pages-Status und Showcase-Gates

## Stand

`REVIEW` – isolierter Builderkandidat, nicht nach `main` promoviert und noch
nicht über GitHub Pages veröffentlicht.

Ausgangsbasis ist der am 2026-09-02 erneut gelesene öffentliche
`main`-Commit `a7764b8db2ddeb41eae8f93b59bdffaca8fcda1a`. Die Umsetzung erfolgte
getrennt auf `task/t-052-pages-status`; der T-042-/Autopilot-Arbeitsbaum wurde
nicht verändert.

## Implementiertes Ergebnis

- `status.json` folgt `riftward-public-status-v2` und bindet Branch,
  Quellcommit, Tree und Commitzeit an dieselben Werte im gebauten HTML.
- `DONE`, `REVIEW` und `READY` werden aus dem eingecheckten `BACKLOG.md`
  gezählt. Ein aktiver Task wird nicht aus einer Dirty-Datei oder einem
  Terminalzustand erraten.
- Akzeptiertes `main`, ein ausgecheckter Kandidatenbranch und der optionale
  öffentliche `autopilot/live-wip`-Ref sind getrennte Zustände. Live-WIP trägt
  zwingend die Klassifikation
  `continuity-snapshot-not-accepted-progress`.
- Fehlende, manipulierte oder widersprüchliche Statusdaten lassen Zahlen und
  Commit auf `—` und melden sichtbar sowie über `aria-live`:
  `Status nicht verfügbar`.
- Der Showcase besitzt eine mobile Navigation, explizite Tabsemantik,
  sichtbaren Fokus, Reduced-Motion-Verhalten, Canonical-/Open-Graph-/Twitter-
  Metadaten, `robots.txt`, `sitemap.xml` und eine eng begrenzte Meta-CSP.
- Medien bleiben unverändert. Ihr vorhandenes SHA-256-Manifest wird geprüft;
  ein versioniertes Budget deckelt die sieben Medien auf zusammen höchstens
  `6.935.492` Bytes.
- Der Workflow prüft Pull Requests, baut das Artefakt bei identischen Eingaben
  zweimal, vergleicht beide Ausgaben, prüft öffentliche Links und lädt oder
  deployt nur einen Lauf von `refs/heads/main` als Pages-Artefakt.

## Builder-Evidenz vom 2026-09-02

### Korrektur-Review nach unabhängiger BLOCK-Prüfung

Der erste Builderstand wurde nach unabhängiger Prüfung gezielt nachgeschärft:
`accepted-main` ist nun nur bei `HEAD == public origin/main` zulässig; der
Workflow checkt bei Pull Requests explizit `pull_request.head.sha` aus und
bindet diesen Commit. Die Statusdatei enthält deterministisches
`generatedAt`/`freshness`, relationale Task-ID-Listen und explizite WIP-
Provenienz. Der gesamte Git-Tree muss sauber sein; Symlinks und nicht reguläre
Dateien werden im Quell- und Ausgabeumfang abgelehnt. Der Task bleibt
`REVIEW`: dieser Korrekturlauf ist lokal, ohne GitHub-CI, Promotion, Push oder
Pages-Deployment.

- Quellvertrag und eingebaute Negativmatrix: `PAGES_CONTRACT_PASS`.
- Python- und JavaScript-Parser sowie `bash -n`: erfolgreich.
- Zwei Builds des Implementierungscommits
  `d9a8313592ce7e760d37e5851ca3f67b2e977d82` mit denselben Eingaben:
  byteidentisch; beide `status.json` hatten SHA-256
  `bf966b650472b11b0c6460402175ddd378e9f26f6314f0efe3a262a2ba15f152`.
- Externe Linkprüfung gegen die freigegebenen Hosts `github.com` und
  `koschnag.github.io`: erfolgreich.
- Öffentlicher Live-WIP-Ref während des Laufs:
  `297ac92a72411b295162018f472916d0b3956732`, Commitzeit
  `2026-09-01T22:03:01Z`; ausschließlich als Kontinuitätssnapshot ausgegeben.
- Reale Browserprüfung: Desktop gerendert; bei 390 Pixeln
  `scrollWidth == innerWidth == 390`, Navigation sichtbar; Pfeiltaste wechselte
  von `Handbuch` zu `Datenträger` und genau das zugehörige Panel wurde
  eingeblendet.
- Reale Browser-Negativprobe ohne `status.json`: Zustand `unavailable`,
  sichtbare Fehlermeldung, Accepted- und Commitwerte blieben `—`.

Die Negativmatrix weist unter anderem alte Fallbackwerte, Autoplay,
Quarantäne-URLs, fehlende Provenienz, ein erfundenes `activeTask: T-042`, WIP
als akzeptierten Fortschritt, einen unbelegten 24/7-Claim, ungültige lokale
Links, Medienbudgetüberschreitung und `null` statt eines expliziten Zustands
ab.

## Noch nicht belegt

- GitHub Actions auf dem veröffentlichten Branch ist noch nicht gelaufen.
- Ein vom Builder getrenntes Review und die formale Promotion stehen aus.
- Der neue Stand ist noch nicht auf der öffentlichen Pages-URL sichtbar.
- Es wird keine 24/7-Verfügbarkeit, menschliche Eingriffszeit, Modellaktivität,
  Tokenmenge oder Kostenwirkung aus Git- oder WIP-Daten abgeleitet.
- Eine Meta-CSP ist auf GitHub Pages eine statische Seitengrenze; sie wird hier
  nicht als Ersatz für nicht konfigurierbare HTTP-Antwortheader ausgegeben.

## Rückrollweg

Der T-052-Diff ist auf Pages-, Task- und Abnahmepfade begrenzt. Nach einer
Promotion kann die Seite durch Revert des isolierten T-052-Commits und ein
erneutes Pages-Deployment auf den zuvor bekannten `main`-Stand `a7764b8…`
zurückgesetzt werden. Die Historie von `main` und `autopilot/live-wip` wird
nicht umgeschrieben.
