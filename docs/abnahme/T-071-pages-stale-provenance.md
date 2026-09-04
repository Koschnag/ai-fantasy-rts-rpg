# T-071 – Letzte akzeptierte Main-Provenienz bei stale/offline

## Stand

`ACCEPTED` – der Implementierungs-Tree ist auf `main` veroeffentlicht, der
exakte post-merge Verify-Lauf ist erfolgreich und Pages-/Live-HTTP- sowie
reale Browserpruefung sind erfolgt.

Der gepruefte Implementierungsstand ist Main-Commit
`42f42e4594acc95d15696def525cb6f81e317712` mit Tree
`c5ad533ff1f37ccfeac3f86c8b96a188ab6e4936`.

## Gelieferte Aussagegrenze

- Eine im Browser zu `stale` oder `offline` herabgestufte, weiterhin
  schema- und exact-main-gueltige Beobachtung zeigt nur den zuletzt
  beobachteten akzeptierten Main-Commit, Tree, oeffentlichen Commitzeitpunkt
  und die akzeptierte Taskanzahl.
- Zustand und Alter werden aus dem gleichoriginigen HTTP-`Date`-/`Age`-Beleg
  sichtbar ausgewiesen. Alte Werte werden nie als aktuell bezeichnet.
- Kandidaten, WIP, Aktivitaet, Autonomie, aktueller Task, Gate und Blocker
  bleiben bei `stale`/`offline` nicht verfuegbar. Unbekannte, ungueltige,
  zukunftsdatierte oder nicht gleichoriginig belegte Daten bleiben
  vollstaendig fail-closed.
- Die Grenzen von 1.800 und 21.600 Sekunden, Status-v3, exact-main-Bindung,
  Doppelbuild, Artefakthashes, Berechtigungen und Deploypfad wurden nicht
  veraendert.
- Der Cron versucht Beobachtungen auf Minute 7, 22, 37 und 52. Das ist ein
  nomineller 15-Minuten-Rhythmus und keine Startzeit-, Intervall-,
  Verfuegbarkeits- oder 24/7-Garantie.

## Gebundene Lieferkette

- Planner-Checkpoint `00db313de67769e64bc7ef105314a01df487ab15`
  und getrennter Reviewer-Checkpoint
  `8ef6a3fcf034710c135bd6331f030df7dce8291e`, gemeinsamer Spec-Tree
  `b956e866cdc4169882b1c960b9d58832e743dfa3`;
- Spec-PR #44 mit exact-head Linux-Verify `33823333858`: erfolgreich;
- Spec-Promotion `e03e69bf72682f01309a144c0ea891d0fc83be2d`, derselbe Tree;
- post-merge Spec-Verify `33824430503`: erfolgreich;
- Builder-Checkpoint `eaffa3e1c4714d5f807c83e0f58fbd4d4f4d616c`
  und getrennter Reviewer-Checkpoint
  `c222ee90e3e039fff749645687063e05eddc4645`, gemeinsamer Liefer-Tree
  `c5ad533ff1f37ccfeac3f86c8b96a188ab6e4936`;
- Implementierungs-PR #45 mit exact-head Linux-Verify `33824590613`:
  erfolgreich;
- Main-Promotion `42f42e4594acc95d15696def525cb6f81e317712`, exakt derselbe Liefer-Tree;
- post-merge Verify `33825619292`: vollstaendig erfolgreich.

Damit sind Planner, Spec-Reviewer, Builder und Implementierungs-Reviewer
belegt. Die vom Sol-Quality-Gate angeforderten Korrekturen wurden vor dem
Builder-Checkpoint in den Builder-Tree integriert. Ein separater
Repair-Checkpoint oder Repair-Receipt ist nicht belegt und wird nicht
behauptet.

Das unabhaengige Sol-Quality-Gate fand vor der Promotion zwei reale Luecken:
eine fehlende fail-closed-Pruefung leerer Response-URLs sowie einen kuenstlichen
Viewport-Test, der kein echter Browserbeleg war. Die Reparatur verlangt nun
eine vorhandene, nichtleere, absolut parsebare und gleichoriginige URL und
laesst die reale Browserabnahme offen. Das abschliessende Quality-Gate endete
mit `PASS`.

## Pages- und Live-HTTP-Evidenz

Der an Main-Commit und Tree gebundene Pages-Lauf `33825619125` bestand
Reconciliation, allowgelistete GitHub-Beobachtung, zwei deterministische
Builds, den oeffentlichen Vertragslauf, die erneute Main-Ref-Pruefung, Upload
und Deployment.

Die danach abgerufene oeffentliche Seite sowie `status.json`, `status.svg` und
`task.svg` antworteten erfolgreich. `status.json` band exakt Commit
`42f42e4594acc95d15696def525cb6f81e317712` und Tree
`c5ad533ff1f37ccfeac3f86c8b96a188ab6e4936`, Statusvertrag v3 sowie die
unveraenderten Schwellen 1.800/21.600. Der SHA-256 des beobachteten
`status.json` war
`1e3826fdf77cdd7ae803b94bd9226bcc080c44307db23398d6c60a684db10669`;
`status.svg` und `task.svg` hatten
`30e2b2e60cc901e02f08574c03996cecc2c779e5f26e112009ecef7415abb038`
beziehungsweise
`ca3c6eb6d518b26ef879f3ad8943fa8e5b93004116ded6472c3a6b24c4d663ac`.
Das zu diesem Zeitpunkt noch `unknown` gemeldete Main-Gate wurde nicht als
Pass umgedeutet; es wartete korrekt auf den separaten post-merge Verify-Lauf.

Nach dessen Erfolg aktualisierte der bewusst ausgeloeste, erneut exakt an
denselben Main-Commit und Tree gebundene Pages-Lauf `33826464571` die
oeffentliche Beobachtung. Der neu beobachtete SHA-256 von `status.json` war
`ed9597bb518f8b1e5e5f0f8f7e7a2227c14885406413abfe31d71bafa6b72748`;
der Status meldete `accepted.main.gates=passed`. Die SVG-Hashes blieben
unveraendert. Ein erneuter realer Browserreload zeigte denselben Commit/Tree,
`BESTANDEN`, `current` und weiterhin keinen horizontalen Ueberlauf.

## Reale Browserabnahme

Die oeffentliche Seite wurde nach dem erfolgreichen Deployment real in einem
Chromium-basierten Browser geprueft:

- 1.440 x 900: Dokumentbreite 1.440 Pixel, kein horizontaler Ueberlauf,
  exakt der erwartete Commit/Tree im Meta- und sichtbaren Status;
- 390 x 844: Dokumentbreite 390 Pixel, kein horizontaler Ueberlauf,
  sichtbare Navigation und dieselbe Commit-/Tree-Bindung;
- genau eine Hauptueberschrift, ein Main-Bereich, deutsches Dokumentlang und
  `aria-live=polite` fuer die Statusmeldung;
- Pfeiltastennavigation wechselte `Graybox` zu `CDD & Evidenz`, aktualisierte
  `aria-selected`, `tabIndex` und das sichtbare Tabpanel und zeigte einen
  3-Pixel-Fokusrahmen;
- alle drei lokalen Konzeptmedien luden mit 1.600 x 900 Pixeln; sie blieben als
  Konzept und nicht als Gameplay markiert;
- nach Reload, Tabwechsel und vollstaendigem Scroll wurden keine Browserwarnung
  und kein Browserfehler beobachtet.

Dieser reale Lauf pruefte den frischen Produktionszustand. Das spaetere
`stale`-/`offline`-Rendering ist durch hermetische positive und adversariale
Fixtures belegt, wurde aber nicht durch Warten auf einen absichtlich
veralteten Produktionsstand erzwungen. Diese Evidenzarten werden nicht
miteinander verwechselt.

## Restgrenzen und Rueckrollweg

T-071 belegt weder ununterbrochene Verfuegbarkeit noch aktive oder produktive
24/7-Autonomie. Ein GitHub-Actions-Zeitplan kann verzoegert oder ausgelassen
werden. WIP, Kandidaten und Aktivitaet bleiben von akzeptiertem Fortschritt
getrennt. Es entstehen keine Gameplay-, Asset-, Hardware-, Kosten- oder
Forschungsclaims.

Ein Revert-PR der Implementierungs-Promotion #45 stellt den vorherigen
Renderer und Cron wieder her; der bestehende Pages-Workflow baut und deployt
danach erneut ausschließlich den akzeptierten Main-Tree. Historie, WIP und
fremde Tasks werden nicht umgeschrieben.
