# Project Riftward GitHub Pages

Das Cockpit ist ein rein statischer, öffentlich prüfbarer Status- und
Forschungs-Showcase. Es ist weder ein Release-Tracker noch eine
Autonomieanzeige. FOSS-first beschreibt Werkzeugwahl und Abhängigkeiten, nicht
die noch offene Repository-, Code- oder Medienlizenz.

## Status-V3: Wahrheitsgrenze

`status.json` ist ein streng fail-closed gelesenes View-Model. Fehlt es, passt
sein Vertrag nicht oder fehlt eine vertrauenswürdige HTTP-Zeit, zeigt die Seite
`UNVERFÜGBAR`, `UNBEKANNT` oder `NICHT BEOBACHTET`. Eine bei Erzeugung als
`current` markierte Beobachtung wird im Browser anhand des gleichoriginären
HTTP-`Date`-Headers (einschließlich eines gültigen `Age`-Werts) zu `stale` oder
`offline` herabgestuft. Die lokale Browseruhr darf eine alte Beobachtung nie
als aktuell aufwerten.

Der Workflow versucht auf den versetzten Cronminuten 7, 22, 37 und 52 nominell
alle **15 Minuten** eine neue Beobachtung. GitHub-Actions-Zeitpläne arbeiten
jedoch nur **Best Effort**: Es gibt weder eine Garantie für eine genaue
Startzeit oder ein exaktes 15-Minuten-Intervall noch für Verfügbarkeit oder
24/7-Betrieb. `current` gilt höchstens **30 Minuten**
(`freshForSeconds=1800`), danach folgt `stale`; nach **6 Stunden**
(`offlineAfterSeconds=21600`) folgt `offline`. Die Beobachtung bindet exakt den
veröffentlichten `main`-Commit und Tree. `observedAtUtc` ist ausschließlich die
öffentliche GitHub-Actions-Beobachtungszeit. Daneben werden nur öffentliche
Commitzeiten gezeigt, nie private Runtime-Zeitpunkte oder rohe Run-, Sitzungs-,
Elternlauf-, Maschinen- oder Modell-IDs.

Der Renderer muss vor dem Schreiben die folgenden exakten V3-Felder erzeugen;
`showcase.js` verwirft unbekannte Schlüssel und ungültige Enumwerte.

```text
root = {
  schemaVersion: 3,
  statusContract: "riftward-public-status-v3",
  observation, accepted, candidates, continuity, activity, tasks, claims
}

observation = {
  state: "current" | "stale" | "offline" | "unknown",
  basis: "trusted-main-and-allowlisted-inputs-v1",
  observedAtUtc: <RFC3339 public Actions observation time>,
  freshForSeconds: 1800, offlineAfterSeconds: 21600,
  sourceCommit: <40 lowercase hex>, sourceTree: <40 lowercase hex>
}

accepted = {
  main: {
    branch: "main", classification: "accepted-main",
    commit: <40 lowercase hex>, tree: <40 lowercase hex>,
    committedAt: <RFC3339 public commit time>,
    gates: "passed" | "blocked" | "unknown"
  },
  tasks: { count: <non-negative integer>, ids: ["T-000", ...] }
}

candidates = {
  state: "observed" | "not-observed" | "unavailable",
  items: [{ taskId, lifecycleStatus, gate, blocker }]
}

continuity = {
  state: "published" | "not-observed" | "stale" | "unavailable",
  classification: "continuity-not-accepted-progress",
  commit?: <40 lowercase hex>, committedAt?: <RFC3339 public commit time>
}

activity = { state: "active" | "waiting" | "blocked" | "idle" | "offline" | "unknown", ... }
tasks = { current: { taskId, lifecycleStatus, effectiveStartEligibility, waitingReason, selectorEnforcement: "pending" }, ready: ["T-000", ...] }
claims = {
  gameplay: "graybox-only",
  targetHardware: "not-validated",
  physicalEdition: "not-produced",
  twentyFourSevenAutonomy: "not-demonstrated",
  concepts: "not-gameplay"
}
```

Bei `activity.state = active|waiting|blocked|idle` sind zusätzlich nur diese geschlossenen,
öffentlichen Labels erlaubt: `taskId`, `phase` (`planning|building|reviewing|
repairing|waiting|unknown`), `role` (`planner|builder|reviewer|repair|wip|
unknown`), `lastGate` (`passed|failed|waiting|unknown`), `blocker`
(`none|awaiting-review|awaiting-preregistered-t042-start-eligibility|blocked|
unknown`), `autonomy` (`human-gated|bounded-autopilot|unknown`) und
`parentClass` (`root|child|unknown`). Bei jedem anderen Activity-Zustand sind
keine weiteren Activity-Felder zulässig. `candidates.items` nutzt dieselben
geschlossenen Lifecycle-, Gate- und Blockerlabels; keine Branch-, Run- oder
Personen-ID wird ausgegeben.

Bei `stale` oder `offline` bleiben ausschließlich der zuletzt validierte
akzeptierte `main`-Commit, Tree, öffentliche Commitzeit und die akzeptierte
Taskanzahl sichtbar. Zustand und Beobachtungsalter werden ausdrücklich aus der
gleichoriginigen HTTP-Vertrauenszeit ausgewiesen. Kandidaten, WIP-Kontinuität,
Aktivität, Autonomie, aktuelle Aufgabe, Gate und Blocker sowie alle übrigen
Werte sind dann nicht verfügbar. Eine unbekannte oder ungültige Beobachtung,
eine Beobachtungszeit in der Zukunft, ein Fetchfehler oder ein fehlender,
ungültiger beziehungsweise nicht gleichoriginiger HTTP-`Date`-/`Age`-Beleg
führt vollständig fail-closed zu `unavailable`; dabei bleibt auch keine letzte
Main-Provenienz sichtbar.

Die `observation.sourceCommit`/`sourceTree` müssen exakt mit
`accepted.main.commit`/`tree` übereinstimmen und mit den beim Pages-Build in
HTML eingesetzten Source-Metadaten. `accepted.main.committedAt` und, nur bei
publizierter WIP-Kontinuität, `continuity.committedAt` sind die einzigen
darstellbaren Commitzeitwerte. Dazu kommt allein `observedAtUtc` als öffentliche
Actions-Beobachtungszeit. `tasks.current` beschreibt Lifecycle und effektive
Startberechtigung getrennt; `READY` allein ist kein Startclaim und
`selectorEnforcement=pending` behauptet keine bereits wirksame Selektorsperre.

Für den eingefrorenen Beobachtungsauftrag T-053 sind genau zwei Zustände
zulässig: Solange kein eindeutig vorhandenes, schemaförmiges und über
akzeptierte Abhängigkeiten startberechtigtes T-042 vorliegt, gilt
`READY / waiting / awaiting-preregistered-t042-start-eligibility`. Sobald
diese Bedingung belegt ist, gilt `READY / eligible / none`. `eligible` bedeutet
nur, dass die Beobachtung vor dem ersten T-042-Zielereignis gestartet werden
darf; es ist weder ein laufender Agent noch eine T-042-Akzeptanz oder bereits
durchgesetzte Selektorsperre. Python-, JSON-Schema- und Browservalidatoren
verwerfen jede andere Kombination fail-closed.

## Provenienz und Betrieb

- Der Pages-Renderer führt ausschließlich den exakten `origin/main`-Baum aus.
  Ein PR-, Fork- oder WIP-Checkout wird nie ausgeführt.
- Kandidaten und WIP können nur über explizit allowlistete, öffentliche,
  schema-geprüfte Eingaben erscheinen. Fehlt eine solche Eingabe, lautet ihr
  Zustand `not-observed` oder `unavailable`.
- Der Workflow versucht die Beobachtung auf den Cronminuten 7, 22, 37 und 52
  nominell alle 15 Minuten anzustoßen. GitHub Actions plant nur Best Effort;
  genaue Startzeit, exaktes Intervall, Verfügbarkeit und 24/7-Betrieb sind
  ausdrücklich nicht garantiert. Eine alte oder fehlende Beobachtung ist kein
  Hinweis auf Inaktivität, sondern schlicht `stale`, `offline`, `unknown` oder
  `unavailable`.
- Git-Rollen bleiben in öffentlichen Darstellungen stabil pseudonymisiert:
  `Planner`, `Builder`, `Reviewer`, `Repair`, `WIP`. Die Promotion-Identität
  des Projektleads ist `Koschnag`; sie ersetzt keine Review- oder Gate-Evidenz.
- Kein Showcase- oder Statusschritt schreibt Git-Historie um. Rollback heißt:
  einen zuvor akzeptierten `main`-Commit erneut deployen, Status daraus neu
  erzeugen und den neuen Deploy prüfen. Redeploy ist kein neuer
  Forschungsfortschritt.

## Inhalte und Medien

- Konzeptmedien bleiben unmittelbar am Medium als `CONCEPT · NOT GAMEPLAY`
  markiert. Sie sind kein Shipping-, Gameplay- oder Lizenzclaim.
- Lokale Originale aus `assets/quarantine/` gelangen nie automatisch in Pages.
  Zugelassene Webableitungen stehen ausschließlich unter
  `docs/showcase/assets/` und sind im
  [`media-manifest.json`](assets/media-manifest.json) gebunden.
- Akzeptierte Graybox-Interaktion ist ein enger, separater Beleg. Das Cockpit
  behauptet weder ein fertiges/repräsentatives Spiel, validierte Zielhardware
  noch 24/7-Autonomie.

## Lokal bauen

```bash
./scripts/build-pages.sh /tmp/riftward-pages
```

Der Build verwendet Bash, Git, Coreutils und POSIX-Textwerkzeuge. Zum lokalen
Ansehen genügt ein statischer HTTP-Server auf `/tmp/riftward-pages`.

Die hermetischen T-071-Vertragstests prüfen Renderzustände und die statischen
CSS-/DOM-Grenzen, sind aber kein echter Layout- oder Browsernachweis.
`AC-T071-06` bleibt bis zu einem gerenderten Browser-Smoke des exakt
veröffentlichten Stands ausdrücklich offen: breite und schmale Viewports,
horizontaler Überlauf, Konsolen-/Seitenfehler sowie Tastatur- und
Screenreaderstruktur müssen dort real geprüft werden.
