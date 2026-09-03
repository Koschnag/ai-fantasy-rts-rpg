# Abnahme- und Reviewartefakte

Dieses Verzeichnis sammelt die eingecheckten technischen Abnahmeberichte. Ein
vorhandener Bericht bedeutet nicht automatisch `DONE`: Der verbindliche
Lifecycle steht im jeweiligen `.ai/tasks/T-…json` und in `BACKLOG.md`.
Öffentliche Promotions- und Gatebelege werden zusätzlich über das
[Projektcockpit](../showcase/README.md) reconciliert.

## Harness und Produktionssystem

- [T-002 – Memory und Traces](T-002-memory-and-traces.md)
- [T-003 – Asset-Provenienz](T-003-asset-provenance.md)
- [T-004 – Run-Provenienz und Evidenz](T-004-run-provenance-and-evidence.md)
- [T-005 – Calibration Inspector](T-005-calibration-inspector.md)
- [T-006 – .NET-Assetgenerator](T-006-dotnet-asset-generator.md)
- [T-007 – .NET-Asset-CI](T-007-dotnet-asset-ci.md)

## Runtime, Simulation und Messung

- [T-010 – Nativer Walking Skeleton](T-010-native-walking-skeleton.md)
- [T-020 – Empty-Scene-Benchmark](T-020-empty-scene-benchmark.md)
- [T-021 – Headless-Simulationsbaseline](T-021-headless-simulation-baseline.md)
- [T-022 – Deterministischer Replay-Soak](T-022-deterministic-replay-soak.md)
- [T-023 – Repräsentativer Belastungsframe](T-023-representative-load-frame.md)
- [T-031 – Atomares Save/Load](T-031-atomic-save-load.md)

## Interaktive Graybox-Kette

- [T-032 – Kommandoschleife](T-032-graybox-command-loop.md)
- [T-033 – Moduswechsel](T-033-mode-switch-prototype.md)
- [T-034 – Erkundung](T-034-graybox-exploration-loop.md)
- [T-035 – Entscheidung](T-035-graybox-decision-step.md)
- [T-036 – Druck und Neustart](T-036-graybox-pressure-restart.md)
- [T-037 – Fortsetzung nach Prozessneustart](T-037-graybox-continuation-restart.md)
- [T-038 – Single-Platform-Paket](T-038-single-platform-release-package.md)
- [T-039 – Abschluss und Wiederholung](T-039-graybox-completion-repeat.md)

## Öffentliche Kommunikation

- [T-052 – Pages-Status und Showcase-Gates](T-052-pages-status-and-showcase-gates.md)
- [T-054 – Projektcockpit und Autopilot-Provenienz](T-054-public-project-cockpit.md)

Fehlende, lokale oder private Runs werden hier nicht nachträglich erfunden.
Jeder Bericht nennt seine offenen Gates und Aussagegrenzen; die verifizierte
Quelle bleibt der exakt zitierte Commit/Tree.
