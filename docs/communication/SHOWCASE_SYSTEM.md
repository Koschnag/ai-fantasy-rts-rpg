# Riftward Showcase-System

## Zweck und Wahrheitsebene

Der Showcase ist eine statische, öffentliche Leseschicht für den belegten
Projektstand. Er ist kein zweites Backlog, kein Run-Monitor und keine
Autonomiebehauptung. Er trennt fünf Aussagen sichtbar:

```text
accepted main | open candidates | WIP continuity | observed activity | claims
```

Nur der exakte, verifizierte `main`-Commit mit Tree ist akzeptierter Fortschritt.
Kandidaten sind nicht akzeptiert; WIP dokumentiert höchstens Kontinuität;
Aktivität ist ohne frisches, begrenztes Signal unbekannt. Eine `READY`-Aufgabe
ist nicht automatisch effektiv startberechtigt.

Die verwendeten Konzepte bleiben unmittelbar als `CONCEPT · NOT GAMEPLAY`
markiert. Sie sind weder Runtime-Capture noch Gameplay-, Hardware-, Release-
oder Lizenzbeleg. Die Lizenzentscheidung für Repository, Code und Medien bleibt
offen; FOSS-first beschreibt nur Werkzeuge und Abhängigkeiten.

## Statusquelle und Ausführung

- Der Pages-Job führt ausschließlich den exakten `origin/main`-Baum aus.
  Er bindet dessen Commit und Tree in HTML sowie `status.json`.
- Pull Requests, Forks und WIP werden niemals als Code ausgeführt. Sie können
  nur als explizit allowlistete, öffentliche, schema-geprüfte Dateneingabe
  erscheinen. Alles andere ist `not-observed` oder `unavailable`.
- Kandidaten- und Aktivitätsdaten stammen aus der geschlossenen Datei
  `.ai/public-status-v3.json` des beobachteten Commits. Deren Git-Blob-OID muss
  zugleich im Commit-Trailer `Public-Status-Blob` und in der GitHub-Content-
  Antwort stehen; Autor-/Committerrolle, Task, Parent, Ausgangscommit und
  Ausgangstree werden ebenfalls geprüft. Eine bloß geerbte Sidecar-Datei gilt
  deshalb nicht als neue Aktivität.
- Der Workflow versucht alle 15 Minuten eine Aktualisierung. Eine Beobachtung
  ist bis 1800 Sekunden (30 Minuten) `current`, danach `stale` und ab 21600
  Sekunden (6 Stunden) `offline`. Diese Klassen sind kein Heartbeat und kein
  Autonomienachweis.
- Der Browser ist fail-closed: Er akzeptiert nur `riftward-public-status-v3`,
  exakte Feldmengen, geschlossene Enums und einen mit HTML identischen Main-
  Commit/Tree. Die Altersberechnung verwendet den gleichoriginären HTTP-
  `Date`-Header und gegebenenfalls dessen gültigen `Age`-Wert, nie die lokale
  Browseruhr als Aufwertungsbeleg. Fehlende/ungültige HTTP-Zeit oder eine
  Beobachtung in der Zukunft führt zu `UNVERFÜGBAR`; bei `stale`/`offline`
  werden insbesondere keine Activity-Details weiter angezeigt.
- Öffentlich darstellbar sind Main- und veröffentlichte WIP-Commitzeiten sowie
  `observation.observedAtUtc` als öffentliche GitHub-Actions-Beobachtungszeit.
  Private Runtime-Zeitpunkte, Sitzungs-/Elternlauf-/Maschinen-/Modell-IDs und
  sonstige interne Kennungen gehören weder ins Statusmodell noch in das DOM.

Der geschlossene Vertrag verwendet `observation.state = current|stale|offline|
unknown`, `basis = trusted-main-and-allowlisted-inputs-v1`,
`freshForSeconds = 1800` und `offlineAfterSeconds = 21600`. Operative
`activity.state`-Werte sind `active|waiting|blocked|idle|offline|unknown`.
`tasks.current.selectorEnforcement` bleibt ehrlich `pending`. Claims sind keine
Booleans, sondern exakt `graybox-only`, `not-validated`, `not-produced`,
`not-demonstrated` und `not-gameplay` in ihren dokumentierten Feldern.

Die vollständige Feldmatrix und enumgenaue Generator-Schnittstelle steht in
[`docs/showcase/README.md`](../showcase/README.md).

## Personen- und Rollenkommunikation

Öffentliche Agentenrollen sind stabil und pseudonym: `Planner`, `Builder`,
`Reviewer`, `Repair`, `WIP`. Sie dürfen als Rolle, nie als Identitätsersatz,
für frische Aktivität erscheinen. Die Promotionsidentität des Projektleads ist
`Koschnag`; sie ersetzt weder unabhängige Prüfung noch erforderliche Gates.

Ein Modelloutput, ein offener Branch oder ein aktives Terminal wird nie als
akzeptierter Fortschritt formuliert. Die zulässige Form lautet beispielsweise:
„Kandidat beobachtet“, „WIP-Kontinuität veröffentlicht“ oder „Aktivität nicht
beobachtet“ — nicht „läuft autonom“ oder „fertig“.

## Export, Rollback und Redeploy

Der Showcase exportiert nur einen berechenbaren, commitgebundenen öffentlichen
View. Lokale Originale unter Quarantäne werden nicht kopiert; zugelassene
Webableitungen müssen im öffentlichen Medienmanifest gebunden bleiben.

Es gibt keine History-Rewrites als Betriebsmaßnahme. Ein Rollback redeployt
einen zuvor akzeptierten `main`-Commit, erzeugt daraus einen neuen Status und
prüft den Deploy erneut. Ein Redeploy ist kein neuer Taskabschluss, keine neue
Messung und kein neuer Claim. Eine nicht reproduzierbare oder nicht
provenienzgebundene Statusdatei wird verworfen, nicht redaktionell repariert.

## Redaktionelle Form

Die Oberfläche ist mobil zuerst, semantisch gegliedert, über Tabs mit Tastatur
bedienbar und aktualisiert die Statusmeldung über `aria-live`. Farbe verstärkt
Zustände, ist aber nie ihr einziges Signal. `prefers-reduced-motion` entfernt
Bewegung. CSP erlaubt kein externes Runtime-Script, kein externes Bildladen und
keine API außer der gleich-originären statischen `status.json`.
