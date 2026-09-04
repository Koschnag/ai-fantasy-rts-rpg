# T-072 – Node-24-Pins der GitHub-Pages-Actions

## Stand

`ACCEPTED` – die drei freigegebenen GitHub-Pages-Actions sind auf ihre
verifizierten Node-24-kompatiblen Releases aktualisiert. Der Liefer-Tree ist
auf `main` veroeffentlicht, PR- und post-merge Verify sind erfolgreich, und
zwei reale Pages-Laeufe haben denselben Main-Stand gebaut und deployt.

Der akzeptierte Implementierungsstand ist Main-Commit
`e54a00ad1f1b8e54f53f62a7d640b68f8dea8525` mit Tree
`9d6f0970bec117a54cc6eca10b952e33ac40e14d`.

## Gelieferter Umfang

Der ausfuehrbare Workflow-Diff gegen den akzeptierten READY-Ausgangsstand
`2814ad2d84ce5cbf2bcf951343a954c64962f5e0` besteht aus exakt drei
`uses:`-Zeilen:

- `actions/configure-pages` v6.0.0,
  SHA `45bfe0192ca1faeb007ade9deae92b16b8254a0d`;
- `actions/upload-pages-artifact` v5.0.0,
  SHA `fc324d3547104276b827a68afc52ff2a11cc49c9`;
- `actions/deploy-pages` v5.0.1,
  SHA `368f82528645a54fb793d4d04e342629a3f51346`.

Trigger, Permissions, Checkout, exact-main-/Tree-Bindung, Reconciliation,
allowgelistete Beobachtung, deterministischer Doppelbuild, Vertragspruefung,
Hashmanifest, Main-Recheck und Deploymentgrenzen blieben unveraendert.

## Gebundene Lieferkette

- Planner-Checkpoint `f1962b18554f63a22093e88e6cff5176be57602f`
  und getrennter Spec-Reviewer-Checkpoint
  `0d9e8049dd9a2926b23af608e591ee36aff25013`, gemeinsamer Spec-Tree
  `0270855ac796455222bd1867522dcd4777d6f612`;
- Spec-PR #50 mit exact-head Linux-Verify `33840462579`: erfolgreich;
- Spec-Promotion `2814ad2d84ce5cbf2bcf951343a954c64962f5e0`,
  exakt derselbe Spec-Tree;
- post-merge Spec-Verify `33841240761`: erfolgreich;
- Repair-Checkpoint `718fe996c34ef4d9e0f573a600714f143d244a7a`
  und getrennter Implementierungs-Reviewer-Checkpoint
  `6040100e624747423e64d7a814c433242ce3f685`, gemeinsamer Liefer-Tree
  `9d6f0970bec117a54cc6eca10b952e33ac40e14d`;
- Implementierungs-PR #49 mit exact-head Linux-Verify `33862489715`:
  erfolgreich;
- Main-Promotion `e54a00ad1f1b8e54f53f62a7d640b68f8dea8525`,
  exakt derselbe Liefer-Tree;
- post-merge Verify `33864671703`: erfolgreich.

Das unabhaengige Sol-Quality-Gate endete nach Pruefung des exakten Repair-Trees
mit `PASS`. Es bestaetigte den Drei-Zeilen-Workflow-Scope, die unveraenderten
Deploygrenzen und die korrekte Trennung von T-072-Lifecyclepflege und
ausfuehrbarem Workflow-Diff.

## Pages- und Warnungsevidenz

Der Push-Pages-Lauf `33864671641` und der nach dem erfolgreichen post-merge
Verify bewusst ausgeloeste Refresh `33865865727` banden beide Head
`e54a00ad1f1b8e54f53f62a7d640b68f8dea8525`. Build, Reconciliation,
allowgelistete Beobachtung, Doppelbuild, oeffentliche Vertragspruefung,
Main-Recheck, Artefakt-Upload und Deployment waren in beiden Laeufen
erfolgreich.

Die vollstaendige Logsuche des Refresh-Laufs beobachtete keine erzwungene
Node-20-/Node.js-20-Kompatibilitaetswarnung. Sie beobachtete weiterhin eine
getrennte Node-Laufzeitwarnung im
Deploy-Schritt: `DEP0040`, veraltete Verwendung des Moduls `punycode`.
Dieser Restbefund wird nicht als durch T-072 behoben ausgegeben. Zusaetzlich
erschien nur der uebliche Git-Hinweis zum kuenftigen Default-Branch-Namen bei
einer temporaeren Repository-Initialisierung.

Nach dem Refresh lieferte die oeffentliche `status.json` den Statusvertrag v3
mit exakt demselben Main-Commit und Tree, `accepted.main.gates=passed` sowie
`observation.state=current`. Eine separate reale Browserabnahme war fuer
diesen reinen Action-Pin-Auftrag nicht gefordert und wird nicht behauptet.

## Reproduktion und Grenzen

Die CI- und Deploymentbelege lassen sich mit `gh run view <Run-ID>` und die
Warnungsbeobachtung mit `gh run view 33865865727 --log` reproduzieren. Der
Workflow bleibt ueber einen Revert der Main-Promotion #49 rueckrollbar; die
vorherigen SHA-Pins stehen unveraendert in der Git-Historie.

T-072 belegt weder Gameplayfortschritt noch Autonomie, 24/7-Verfuegbarkeit,
Performance, Kosten oder neue Forschungsdaten. T-053 und T-055, Runtime und
Gameplay wurden nicht veraendert.
