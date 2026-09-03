# T-054 – Öffentliches Projektcockpit und Autopilot-Provenienz

## Stand

`ACCEPTED` – getrennte Projektleitungs-Promotion nach Builder-, Repair- und
Reviewer-Kette. Der abgenommene öffentliche Ausgangsstand ist der
`main`-Commit `8895d379ffe2bcc9f3c24121ddb9e7ee74a99475` mit Tree
`37958bde34c808bff536b1ed75813ba9e5394a01`.

Diese Akzeptanz verändert weder den T-053-Protokollbundle noch Runtime,
Produktsemantik, Autopilot-Leases oder `autopilot/live-wip`. T-053 bleibt
`READY`, ist wegen seiner vorregistrierten T-042-Bedingung aber weiterhin
effektiv `waiting`.

## Abgenommener Lieferumfang

- README und GitHub Pages verwenden dieselbe dynamische, commitgebundene
  Statusprojektion. Ein Statusrefresh erzeugt keinen README-Commit.
- Der Pages-Workflow läuft auf jedem `main`-Push, manuell und alle 15 Minuten.
  Ausgeführt wird ausschließlich der exakt gebundene `origin/main`-Code;
  PR-, Fork- und WIP-Refs bleiben streng allowgelistete, nicht ausführbare
  Daten.
- Der geschlossene Status-v3-Vertrag trennt Beobachtung, akzeptierten Stand,
  Kandidaten, WIP-Kontinuität, Aktivität, Aufgaben und Claim-Grenzen.
- Beobachtungen sind höchstens 30 Minuten `current`, danach `stale` und ab
  sechs Stunden `offline`. Fehlende oder widersprüchliche Daten werden
  sichtbar abgewertet oder blockieren das Deployment.
- Das responsive, status-first Cockpit unterstützt Tastaturtabs, sichtbaren
  Fokus, Screenreaderstruktur, `aria-live`, Reduced Motion und rein lokale,
  provenienzgebundene Medien.
- Künftige Planner-, Builder-, Reviewer-, Repair- und WIP-Commits besitzen
  geschlossene pseudonyme Rollen und Trailer. Bestehende Git-Historie wird
  nicht umgeschrieben; `Koschnag` bleibt Projektleitungs-/Promotionsidentität.
- Historische Promotionsreceipts für T-034 bis T-039 und T-052 sind
  reproduzierbar gebunden. Nicht öffentlich bewiesene historische
  Rollentrennung bleibt ausdrücklich `not-publicly-proven`.

## Exakte Promotionsevidenz

Die finale Repair-/Review-Kette bestand folgende unabhängige Prüfungen:

- der hermetische `PAGES_CONTRACT_PASS` prüfte unter anderem
  Tastatur-/DOM-Struktur, Reduced Motion, Links, Medien und responsive
  Vertragsgrenzen; dies ist kein Ersatz für einen nicht ausgeführten realen
  Tastatur-Playtest;
- Repair-Commit `eadf2062e9e3631293c97c1e7ab41e40be19b64a`, Tree
  `846de215a478dd550c8a23eaad399eea14b703dd`;
- getrennte Reviewer-Promotion
  `4ab4f1a61867f9b9118cf1f9931471dfea356b55`, Tree
  `37958bde34c808bff536b1ed75813ba9e5394a01`;
- exakter Reviewer-Verify-Lauf `33774141897`: vollständig erfolgreich;
- unabhängiges Sol-Quality-Gate: `PASS`;
- Squash-Promotion nach `main` als
  `8895d379ffe2bcc9f3c24121ddb9e7ee74a99475`, exakt derselbe Reviewer-Tree;
- post-merge Verify-Lauf `33775995196`: Build, Produktsuite, Harness,
  Asset-Provenienz, Security und Fresh-Checkout vollständig erfolgreich;
- gebundener Pages-Lauf `33775995228`: Reconciliation, allowgelistete
  GitHub-Beobachtung, zwei deterministische Builds, öffentliche
  Vertragsprüfung, erneute Main-Ref-Prüfung und Deployment erfolgreich.

## Reale Veröffentlichungskontrolle

Nach dem erfolgreichen Pages-Lauf wurden die öffentliche Seite,
`status.json`, `status.svg` und `task.svg` mit HTTP 200 abgerufen.
`status.json` nannte exakt den abgenommenen Commit und Tree, klassifizierte
T-053 als `READY`/`waiting` und gab fehlende Aktivitäts-, Kandidaten- und
Kontinuitätssignale sichtbar als `unknown`, `not-observed` oder `unavailable`
aus.

Eine reale Browserabnahme bestätigte:

- Desktop bei 1440 × 900 und Smartphone bei 390 × 844 ohne horizontalen
  Überlauf;
- alle drei lokalen Konzeptmedien vollständig geladen;
- funktionierende Methodentabs einschließlich sichtbarer
  Selektionszustände;
- lesbare Commit-, Tree-, Task-, Gate- und Claim-Grenzen;
- keine beim kontrollierten Reload beobachteten Page- oder Request-Fehler.

## Reparaturchronik

Der erste `main`-Deploy `33767113139` entdeckte fail-closed, dass ein sauberer
Squash-Merge-Checkout historische PR-Headobjekte nicht zwingend enthält. Die
erste Reparatur trennte deshalb Offline-Historienprüfung und live
GitHub-gebundene Reconciliation, ohne Result-Tree, Squash-Parent,
Main-Ancestry oder API-Bindungen abzuschwächen.

Der nächste Deploy `33772005266` bestand diese Reconciliation, entdeckte aber
einen zweiten Fehler: Der Statusgenerator rief denselben Validator erneut mit
dem strengeren Offline-Default auf. Die finale Reparatur band diesen Aufruf an
das unmittelbar zuvor im vertrauenswürdigen Workflow erzeugte Live-Verdict.
Adversariale Regressionstests für falschen Tree, falschen Parent und
divergierte Main-Ancestry blieben Pflicht. Der darauf folgende Lauf
`33775995228` bestand vollständig.

Diese Fehler- und Reparaturfolge bleibt als Evidenz erhalten; fehlgeschlagene
Läufe werden nicht gelöscht oder nachträglich als Erfolg umgedeutet.

## Aussagegrenzen und Restpunkte

Das Cockpit belegt weder ein fertiges oder repräsentatives Spiel noch
Zielhardwareleistung, physische Ausgabe, Vollzeitverfügbarkeit,
24/7-Produktivität, menschliche Eingriffszeit, Tokenmenge oder Kosten. WIP
belegt Kontinuität, nicht Akzeptanz. Die Konzeptbilder sind kein Gameplay.
Eine pseudonyme Git-Autorzeile ist Rollenprovenienz, kein kryptografischer
Nachweis verschiedener Personen oder Prozesse.

Der automatisch durch den Merge dieses Lifecycle-Checkpoints ausgelöste neue
Verify-/Pages-Lauf wird als eigener nachgelagerter `main`-Beleg beobachtet. Er
ändert die hier gebundene Abnahme des Liefer-Trees nicht. Die von GitHub
gemeldete Node-20-Kompatibilitätswarnung der gepinnten Pages-Actions ist ein
nicht blockierender Wartungspunkt und kein fehlgeschlagener Projekttest.

## Rückrollweg

Die Akzeptanz wird als eigener, kleiner Pull Request integriert. Ein normaler
Revert-PR setzt ausschließlich diesen Lifecycle-/Dokumentationscheckpoint
zurück. Die Implementierung selbst bleibt über die zuvor dokumentierten
T-054-Revert-Commits rückrollbar; danach erzeugt Pages den öffentlichen Stand
erneut aus dem akzeptierten `main`. Weder `main` noch WIP-Historie werden
umgeschrieben.
