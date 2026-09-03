# T-054 – Öffentliches Projektcockpit und Autopilot-Provenienz

## Stand

`REVIEW` – isolierte Builder-/Repair-Kandidatenkette. Ausgangsbasis ist der veröffentlichte
`main`-Commit `577e7c43c1782795f3af85d0322c4975cc8f8ddf` mit Tree
`25df0a74a3d23fb7617ab9d2712737ebc572f8b8`. Der Kandidat ist noch nicht
akzeptiert, nicht nach `main` promoviert und nicht auf GitHub Pages
veröffentlicht.

Der T-053-Quellvertrag und sein Protokollbundle bleiben unverändert. T-053 ist
weiterhin `READY`, aber wegen des noch nicht startberechtigten T-042 effektiv
`waiting`. Der Builder hat weder T-042 noch Runtime, Produktsemantik,
Autopilot-Leases oder `autopilot/live-wip` verändert.

## Implementierter Kandidatenstand

- README und Pages verwenden dieselbe dynamische, commitgebundene
  Statusprojektion. Ein Statusrefresh erzeugt keinen README-Commit.
- Der Pages-Workflow läuft auf `main`-Push, manuell und alle 15 Minuten. Nur
  der exakt über die GitHub-API aufgelöste `origin/main`-Commit wird
  ausgecheckt und ausgeführt; PR-, Fork- und WIP-Refs bleiben Daten.
- Der geschlossene Status-v3-Vertrag trennt Beobachtung, akzeptierten Stand,
  Kandidaten, WIP-Kontinuität, Aktivität, Tasks und enge Claim-Grenzen.
- Die öffentliche Beobachtung gilt 30 Minuten als `current`, danach als
  `stale` und ab sechs Stunden als `offline`. Der Browser wertet nur mit der
  gleichoriginären HTTP-Zeit ab; eine Client-Uhr kann keinen Zustand
  aufwerten.
- Fehlende, mehrdeutige, veraltete, zukunftsdatierte oder
  provenienzwidersprüchliche Daten werden fail-closed abgewertet oder
  verhindern das Deployment.
- Historische Promotionsreceipts, heutiger retrospektiver Audit und
  `DONE`-Berechtigung sind getrennte Evidenzklassen. Die sieben Records für
  T-034 bis T-039 und T-052 behalten
  `roleSeparation=not-publicly-proven`; solange der neue Audit aussteht, bleiben
  sie `REVIEW` und nicht akzeptiert.
- Neue Commits erhalten stabile Planner-/Builder-/Reviewer-/Repair-/WIP-
  Autopilotidentitäten und geschlossene Provenienztrailer. Die Identität
  `Koschnag` bleibt der Projektleitung/Promotion vorbehalten; bestehende
  Historie wird nicht umgeschrieben.
- Status-Sidecars sind nicht selbstreferenziell: Der jeweilige Commit bindet
  den Git-Blob über `Public-Status-Blob`; Beobachtung verlangt dieselbe OID aus
  Commit-Trailer, GitHub-Content-API und lokaler Blob-Berechnung. Fehlende,
  doppelte, falsche oder nur weitergetragene Bindungen ergeben kein Signal.
- Die Seite wurde responsiv und status-first neu aufgebaut. Tastaturtabs,
  sichtbarer Fokus, `aria-live`, Reduced Motion, CSP, lokale Medien,
  Checksum-/Größenbudgets sowie `CONCEPT · NOT GAMEPLAY` bleiben verbindlich.

## Kandidatenevidenz

Folgende Prüfungen waren auf dem isolierten macOS-Builderstand erfolgreich:

- `node --check docs/showcase/showcase.js`
- `python3 scripts/test-pages.py --source docs/showcase`
  → `PAGES_CONTRACT_PASS`
- `python3 scripts/test-reconciliation.py`
  → `RECONCILIATION_HERMETIC_PASS`
- `python3 scripts/test-commit-role.py`
  → `COMMIT_ROLE_POLICY_PASS`
- Eine frühere Live-GitHub-Prüfung des v1-Receiptsatzes stimmte für die sieben
  historischen Receipts mit Repository, PR, Workflow, Run, Attempt, Job,
  Check-Run, Check-Suite, GitHub-Actions-App sowie Task-/Review-/Workflowblobs
  überein. Sie ist ausdrücklich kein Live-Nachweis des aktuellen v2-Vertrags;
  dessen `validate_reconciliation.py --live-github` bleibt auf dem exakten
  GitHub-Kandidaten und nach der Promotion erneut auszuführen.
- `./scripts/rift.sh lint`
- `./scripts/rift.sh build`
  → 0 Warnungen, 0 Fehler
- `./scripts/rift.sh harness verify --run <lokaler-redigierter-run>`
  → gültige Ereigniskette; die private Run-ID wird nicht veröffentlicht
- `./scripts/rift.sh security` bis auf die offen ausgewiesene lokale
  Git-LFS-Werkzeuglücke; Secretheuristik, JSON, Locked Restore/NuGetAudit und
  native Toolchain-/Lizenz-/ISA-Prüfung bestanden

Die komplette Produktsuite erreichte auf dem Mac 353/383. Die 30 roten Fälle
sind kein grüner Abnahmebeleg: Sie betreffen explizit linux-x64-konstante
Runtime-/Report-/Paketfixtures, zwei Linux-CI-Evidenzfälle und die lokal nicht
installierte Git-LFS-Prüfung. Der verpflichtende frische Linux-PR-Lauf muss
daher die vollständige Suite und das Security-Gate übernehmen. Die lokale
Messung wird weder gelöscht noch als Erfolg umgedeutet.

## Noch offen

- vollständige Builder-/Repair-Kandidatenkette mit exakter Parent-/Tree-Bindung;
- frische Linux-GitHub-CI auf genau diesem Kandidaten;
- builder- und repair-getrenntes unabhängiges Review des exakten letzten
  Kandidatencommits;
- maschinenlesbarer retrospektiver Auditreceipt und explizite, dadurch
  autorisierte Statusübergänge T-034 bis T-039/T-052;
- erneute CI auf dem eingefrorenen Reviewer-Head;
- Squash-Merge, post-merge Live-Reconciliation und GitHub-Pages-Deployment;
- reale Browserabnahme der veröffentlichten Desktop- und Smartphoneansicht.

Der erste echte `main`-Deploy nach der Promotion wurde am 2026-09-03 im
fail-closed Reconciliation-Schritt abgebrochen (GitHub-Actions-Run
`33767113139`). Der saubere `main`-Checkout enthielt erwartungsgemäß nicht alle
durch Squash-Merges historisch gewordenen PR-Headobjekte; der CLI-Einstieg
forderte diese Objekte trotzdem zusätzlich zur bereits gebundenen Live-API-
Prüfung lokal an. Der Reparaturkandidat trennt deshalb nur die Quellenwahl:
Offline-`--root` behält die vollständige lokale Historienprüfung. Der
Live-Modus überspringt lokal ausschließlich die nach einem Squash-Merge nicht
notwendig erreichbaren PR-Heads; Result-Tree, Squash-Parent, Main-Ancestry sowie
Base-/Result-Blobs bleiben lokale Pflicht. PR-Heads und die vollständige
Auditkette werden zusätzlich fail-closed über die gebundene GitHub-API geprüft,
der aktuelle `main`-Commit, Tree und Manifestblob lokal und per API gebunden.
Ein anschließender realer Vorabtest erreichte damit das Review-Gate und zeigte
einen zweiten Lifecycle-Fall: GitHub lieferte für den geschlossenen PR im
historischen Workflow-Run ein leeres optionales `pull_requests`-Array. Der
Reparaturkandidat behandelt dieses leere Array nun wie bereits die sieben
historischen Receipts, bindet aber weiterhin den exakten PR samt Base/Head,
Workflow, Run-Attempt, Suite, Job und Check; mehrere oder widersprüchliche
Relationen bleiben abgelehnt.
Bis ein neuer exakter CI- und Pages-Lauf bestanden hat, ist dies kein
Deployment- oder Abnahmebeleg und T-054 bleibt `REVIEW`.

T-054 selbst wird in dieser Kandidatenkette nicht `DONE`: Sein eigener Merge
und post-merge Deploy können nicht aus dem vorangehenden Kandidatenbaum bewiesen
werden.

## Aussagegrenzen

Das Cockpit belegt weder ein fertiges oder repräsentatives Spiel noch
Zielhardwareleistung, physische Ausgabe, Vollzeitverfügbarkeit, 24/7-
Produktivität, menschliche Eingriffszeit, Tokenmenge oder Kosten. WIP zeigt
Kontinuität, nicht Akzeptanz. Eine pseudonyme Git-Autorzeile ist Rollenbeleg,
kein kryptografischer Nachweis verschiedener Personen oder Prozesse.

## Rückrollweg

Die Änderung bleibt in einem eigenen PR und wird ausschließlich per
Squash-Merge integriert. Vor dem Merge ist der Branch löschbar, ohne `main`
zu verändern. Nach dem Merge setzt ein normaler Revert-PR den vorherigen
Main-Baum wieder ein; Pages wird anschließend aus diesem akzeptierten Baum neu
gebaut. Weder `main` noch WIP-Historie werden umgeschrieben.
