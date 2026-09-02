# T-053 Datenwoerterbuch

**Vertrag:** `riftward-observability-data-v1`

**Protokoll:** `riftward-research-observability` 2.0.0

## Globale Konventionen

- Textkodierung ist UTF-8 ohne BOM, Zeilenende LF.
- Zeitpunkte sind UTC nach RFC 3339 als `YYYY-MM-DDTHH:MM:SS.fffZ`.
- Dauern sind nichtnegative ganze Millisekunden.
- Inhalts-, Artefakt-, Ereignis- und Manifesthashes sind kleingeschriebene
  SHA-256-Hexwerte mit genau 64 Zeichen. Git-Objekt-IDs sind 40 oder 64
  kleingeschriebene Hexzeichen entsprechend dem gebundenen Repositoryformat;
  sie werden nie als SHA-256 ausgegeben, wenn das Repository SHA-1 verwendet.
- Identifikatoren sind innerhalb ihres Bundles eindeutig und
  case-sensitive. Persistierte IDs werden nie wiederverwendet.
- Dezimalwerte werden mit Punkt und ohne Tausendertrennzeichen geschrieben.
- Boolesche Werte sind nur `true` oder `false`.
- Ein nicht beobachteter oder nicht sicher ableitbarer Wert ist die
  Zeichenkette `unknown`. JSON `null`, leere Ersatzstrings, `NaN`, ein
  Schaetzwert als Ersatz und aus Abwesenheit abgeleitetes `true`/`success`
  sind ungueltig. Die gesonderte Kostenprovenienz `estimated` bleibt eine
  nicht exakte eigene Messklasse.
- Fuer einen fachlich nicht anwendbaren Wert wird ebenfalls `unknown`
  gespeichert und `availabilityReason` auf `not-applicable` gesetzt. Dadurch
  bleibt Nichtanwendbarkeit von Messwerten getrennt, ohne einen zweiten
  magischen Wert einzufuehren.
- Ein beobachteter Zahlenwert und `unknown` teilen niemals dasselbe Feld. Die
  spaetere maschinenlesbare Schemafassung verwendet fuer messbare Werte die
  Vereinigung `integer | number | const "unknown"`.

## Evidenzklasse

`evidenceClass` ist exakt einer dieser Werte:

- `retrospective-derived`
- `prospective-observed`
- `synthetic-test-only`

Eine Beobachtung besitzt genau eine Klasse. Ein Export mit Daten mehrerer
Klassen enthaelt getrennte Beobachtungszeilen und aggregiert sie nicht.

## Ereignishuelse

| Feld | Typ | Pflicht | Definition |
|---|---|---:|---|
| `schemaVersion` | integer | ja | fuer T-053 initial exakt `1` |
| `eventId` | string | ja | `EV-` plus 26 Grossbuchstaben/Ziffern; innerhalb der Beobachtung eindeutig |
| `studyId` | string | ja | exakt `riftward-research-observability` fuer dieses Protokoll |
| `observationId` | string | ja | `OBS-` plus 26 Grossbuchstaben/Ziffern |
| `runId` | string oder `unknown` | ja | beobachteter Agenten-/Harnesslauf; fuer laufunabhaengige Ereignisse `unknown` |
| `parentRunId` | string oder `unknown` | ja | direkter Elternlauf eines Kindlaufs, sonst `unknown` |
| `cycleId` | string oder `unknown` | ja | Autopilot-/Lieferzyklus, sofern direkt beobachtet |
| `taskId` | Task-ID oder `unknown` | ja | `T-[0-9]{3,}` oder `unknown` |
| `sequence` | integer | ja | beginnt je Beobachtung bei 1 und steigt lueckenlos um 1 |
| `monotonicTimeNs` | integer oder `unknown` | ja | monotone Zeit seit Start der durch `monotonicClockId` benannten Uhr; nur innerhalb derselben Uhr vergleichbar |
| `monotonicClockId` | string oder `unknown` | ja | stabile ID der Prozess-/Hostuhr; kein Hostname und keine Identitaet |
| `occurredAtUtc` | timestamp oder `unknown` | ja | Quellzeit des beobachteten Akts; nie aus Git-Commitzeit als Laufzeitersatz abgeleitet |
| `recordedAtUtc` | timestamp | ja | Zeitpunkt der Collector-Aufnahme |
| `evidenceClass` | enum | ja | eine der drei Evidenzklassen |
| `eventType` | enum | ja | Typ aus dem Ereignisregister |
| `actorRole` | enum oder `unknown` | ja | `agent`, `human`, `tool`, `reviewer`, `automation` oder `unknown`; keine Klarnamen |
| `actorId` | string oder `unknown` | ja | stabile pseudonyme Akteur-ID innerhalb derselben Study; fuer Agentenlaeufe zugleich die stabile Agentenidentitaet, ohne Account-, Session-, Host- oder Klarnamen |
| `providerId` | string oder `unknown` | ja | Providerkennung des Agentenlaufs ohne Account-/Billing-ID |
| `modelId` | string oder `unknown` | ja | konkrete Modellfamilie laut beobachteter Laufquelle |
| `modelVersion` | string oder `unknown` | ja | konkrete Provider-/Deploymentversion; fehlende Offenlegung bleibt `unknown` |
| `branchRef` | string oder `unknown` | ja | normalisierte Git-Ref ohne Credential/URL; detached oder unbeobachtet ist `unknown` |
| `baseCommit` | Git-Objekt-ID oder `unknown` | ja | gebundene Ausgangsgrenze |
| `headCommit` | Git-Objekt-ID oder `unknown` | ja | beobachteter HEAD; kein beweglicher Refersatz |
| `treeId` | Git-Objekt-ID oder `unknown` | ja | exakter Git-Baum oder `unknown` bei nicht eingechecktem Snapshot |
| `autonomyMode` | enum oder `unknown` | ja | `autonomous`, `human-directed` oder `unknown`; sagt nichts ueber momentane Aktivitaet |
| `activityState` | enum oder `unknown` | ja | `agent-active`, `idle`, `sleeping`, `blocked`, `offline` oder `unknown` |
| `result` | enum oder `unknown` | ja | `success`, `pass`, `fail`, `blocked`, `cancelled`, `rejected`, `accepted` oder `unknown`; Abwesenheit ist nie `success` |
| `exitCode` | integer oder `unknown` | ja | beobachteter Prozess-Exitcode; nicht aus Resultat abgeleitet |
| `failureClass` | string oder `unknown` | ja | stabile Fehlerklasse; Freitext allein genuegt nicht |
| `retryIndex` | integer oder `unknown` | ja | nullbasierter Wiederholungsindex derselben semantischen Operation; erster belegter Versuch `0`, sonst bei Nichtanwendbarkeit oder unsicherer Zuordnung `unknown` |
| `repairIndex` | integer oder `unknown` | ja | nullbasierter Reparaturindex fuer denselben `triggerEventId`; bei Reparaturereignissen als Zahl Pflicht, sonst `unknown` |
| `usageProvenance` | enum oder `unknown` | ja | `provider-receipt`, `gateway-receipt`, `local-measurement` oder `unknown` |
| `costProvenance` | enum oder `unknown` | ja | exakt `provider-reported`, `locally-calculated`, `estimated` oder `unknown` |
| `requestCount` | integer oder `unknown` | ja | exakt quittierte Requests fuer dieses Ereignisintervall |
| `inputTokens` | integer oder `unknown` | ja | exakt quittiert, Cache-read nicht eingerechnet |
| `outputTokens` | integer oder `unknown` | ja | exakt quittiert |
| `cacheReadTokens` | integer oder `unknown` | ja | separat quittiert |
| `cacheWriteTokens` | integer oder `unknown` | ja | separat quittiert |
| `costAmount` | decimal string oder `unknown` | ja | exakt quittierter Betrag ohne Waerungsumrechnung |
| `costCurrency` | ISO-4217 oder `unknown` | ja | Waerung des Betrags; ohne Betrag `unknown` |
| `changedFiles` | integer oder `unknown` | ja | belegte Zahl verschiedener Pfade an dieser Aenderungsgrenze |
| `changedPaths` | array oder `unknown` | ja | sortierte, deduplizierte, redigierte repo-relative Pfade; kann ein Pfad nicht sicher normalisiert werden, ist das ganze Feld `unknown` |
| `linesAdded` | integer oder `unknown` | ja | Text-Numstat; Binaeranteile nie geschaetzt |
| `linesDeleted` | integer oder `unknown` | ja | Text-Numstat |
| `binaryFilesChanged` | integer oder `unknown` | ja | separat beobachtete Binaerpfade |
| `privacyClass` | enum oder `unknown` | ja | `public`, `internal`, `restricted` oder `unknown` |
| `redactionStatus` | enum oder `unknown` | ja | `not-required`, `applied`, `blocked` oder `unknown` |
| `redactionPolicyVersion` | string oder `unknown` | ja | verwendete Policyversion |
| `humanActiveDurationMs` | integer oder `unknown` | ja | direkt gemessene aktive menschliche Bearbeitungszeit fuer diesen Akt; Nachrichtendauer oder Antwortlatenz ist kein Ersatz |
| `sourceRefs` | array | ja | mindestens eine Quellreferenz; nur bei `synthetic-test-only` darf eine Fixture-Referenz genuegen |
| `payload` | object | ja | typspezifische, vor Persistierung redigierte Nutzdaten |
| `supersedesEventId` | string oder `unknown` | ja | ID des korrigierten Ereignisses oder `unknown` |
| `previousEventHash` | hash oder `unknown` | ja | bei Sequenz 1 exakt `unknown`, sonst Hash des Vorgaengers |
| `eventHash` | hash | ja | SHA-256 der kanonischen Ereignisbytes ohne dieses Feld |

`recordedAtUtc` darf vor `occurredAtUtc` nicht liegen, sofern beide bekannt
sind. `monotonicTimeNs` muss innerhalb derselben `monotonicClockId` und
Beobachtung mit der Sequenz monoton nicht fallend sein. Monotone Werte
verschiedener Clock-IDs werden nie subtrahiert. Bei retrospektiven Quellen
duerfen Quellzeit und monotone Zeit `unknown` sein; `recordedAtUtc` ist
weiterhin der echte Ableitungszeitpunkt.

`costAmount` entspricht `^(0|[1-9][0-9]*)(\.[0-9]+)?$`; negative Kosten,
Exponentialschreibweise und Rundung sind unzulaessig. Summen behalten die
maximale im Receipt vorhandene Dezimalstellenzahl. Token-, Request- und
Changezaehler sind nichtnegative Integer oder `unknown`.

`actorId` ist pro Study stabil: derselbe beobachtete Akteur erhaelt dieselbe,
verschiedene Akteure erhalten verschiedene pseudonyme IDs. Die private
Zuordnung liegt ausserhalb des Exports; aus Rolle, Provider oder Modell wird
keine Identitaet geraten. Ist die Zuordnung nicht belegbar, steht literal
`unknown`. `retryIndex` und `repairIndex` stammen nur aus strukturierten
Harnessgrenzen oder quittierten Receipts, nie aus wiederholten Logzeilen.

`provider-reported` bindet den Betrag an ein Providerreceipt.
`locally-calculated` bindet exakte quittierte Nutzung an einen eingefrorenen
Preisstand und exportiert beide Quellhashes. `estimated` kennzeichnet einen
nicht exakten Kostenwert, der ausschliesslich in einer separaten
Estimated-Metrik erscheinen darf. Fehlt diese Herkunft oder ist sie
mehrdeutig, sind `costAmount`, `costCurrency` und `costProvenance` literal
`unknown`; `estimated` ersetzt niemals `unknown` in einer exakten Metrik.

Fuer `eventHash` wird `eventHash` aus dem Objekt entfernt; alle
Objektschluessel werden rekursiv nach Unicode-Codepoint sortiert, Arrays
behalten ihre Reihenfolge, Strings werden als JSON UTF-8 escaped, Integer ohne
fuehrende Nullen geschrieben und unbedeutender Whitespace sowie Abschluss-LF
weggelassen. SHA-256 ueber diese Bytes ist `eventHash`. Die JSONL-Zeile ist
das vollstaendige Objekt inklusive `eventHash` plus genau einem LF.

## Quellreferenz

| Feld | Typ | Pflicht | Definition |
|---|---|---:|---|
| `sourceKind` | enum | ja | `git-blob`, `git-commit`, `harness-event`, `harness-evidence`, `autopilot-event`, `agent-event`, `task-manifest`, `gate-log`, `review-receipt`, `decision-receipt`, `provider-receipt`, `infrastructure-receipt`, `fixture` |
| `repositoryCommit` | Git-Objekt-ID oder `unknown` | ja | vollstaendiger Commit; `unknown`, wenn die Quelle nicht in Git liegt |
| `repositoryPath` | string oder `unknown` | ja | normalisierter repo-relativer Pfad; nie absolut |
| `lineStart` | integer oder `unknown` | ja | erste belegende Zeile, 1-basiert |
| `lineEnd` | integer oder `unknown` | ja | letzte belegende Zeile inklusive; nicht kleiner als `lineStart` |
| `artifactSha256` | hash | ja | Hash der tatsaechlich gelesenen, redigierten Quellbytes |
| `sourceEventId` | string oder `unknown` | ja | vorhandene Harness-/Collector-Ereignis-ID |
| `resolvable` | boolean | ja | `true` nur, wenn Hash und Adresse gegen die gebundene Quelle geprueft wurden |

Eine unaufloesbare Referenz bleibt erhalten, erhoeht aber nicht die
Quellenaufloesungsquote. Git-Commitmetadaten belegen ausschliesslich Git-Fakten
wie Baum, Autorzeit oder Diff; sie belegen keine Tokens, Kosten, menschlichen
Eingriffe oder aktive Arbeitszeit.

## Ereignisregister und Payload

| `eventType` | erforderliche Payloadfelder | Bedeutung |
|---|---|---|
| `protocol.frozen` | `protocolId`, `protocolVersion`, `protocolBundleSha256`, `freezeAtUtc` | bindet die vor Beobachtungsstart eingefrorene Fassung |
| `observation.started` | `targetTaskId`, `baselineCommit`, `collectorVersion`, `nonInterferenceSnapshotSha256`, `activationGuardSha256` | beginnt genau eine Beobachtung erst nach einem belegten Guard ohne bereits gestarteten Zielpfad |
| `autopilot.started` | `autopilotInstanceId`, `triggerClass`, `policySha256` | Beginn einer beobachteten Autopilotinstanz |
| `autopilot.paused` | `autopilotInstanceId`, `reasonCode` | explizite Lifecyclepause; weder Autonomiemodus noch Aktivitaetszustand |
| `autopilot.resumed` | `autopilotInstanceId`, `pausedDurationNs` | Fortsetzung derselben Instanz |
| `autopilot.stopped` | `autopilotInstanceId`, `stopClass` | normales, abgebrochenes oder blockiertes Ende |
| `agent.run.started` | `agentId`, `agentRole`, `promptSha256`, `toolchainSha256` | Agentenlauf; `agentId` entspricht der stabilen pseudonymen `actorId`; `runId`, `providerId`, `modelId`, `modelVersion` Pflicht oder literal `unknown`, `parentRunId` fuer Kindlauf |
| `agent.run.finished` | `finishClass`, `producedTreeId`, `summarySha256` | Ende desselben Agentenlaufs; Erfolg wird nicht aus Existenz abgeleitet |
| `task.planned` | `taskManifestSha256`, `authorityClass` | Task wurde als Plan beobachtet |
| `task.ready` | `taskManifestSha256`, `authorityClass` | belegter Uebergang auf ready |
| `task.implemented` | `taskManifestSha256`, `implementationTreeId` | Builder meldet Implementierungsgrenze; keine Akzeptanz |
| `task.reviewed` | `reviewId`, `verdict`, `reviewedTreeId` | Taskreview auf gebundenem Baum |
| `task.rejected` | `reviewId`, `reasonCode`, `rejectedTreeId` | belegte Ablehnung |
| `task.accepted` | `authorityClass`, `acceptedCommit`, `acceptedTreeId` | formale Akzeptanz; WIP/Committext allein genuegt nicht |
| `wip.snapshot.created` | `snapshotId`, `snapshotCommit`, `snapshotTreeId`, `continuityOnly` | WIP-Kontinuitaet; `continuityOnly` muss `true` sein und ist kein Outcome |
| `autonomy.mode.changed` | `fromAutonomyMode`, `toAutonomyMode`, `reasonCode` | Wechsel zwischen autonomous/human-directed; Aktivitaetszustand bleibt getrennt |
| `activity.state.changed` | `fromActivityState`, `toActivityState`, `reasonCode` | Aktivitaetswechsel; Grenzen nutzen dieselbe monotone Uhr, Autonomiemodus bleibt getrennt |
| `gate.started` | `gateId`, `attempt`, `targetTreeId` | Start genau eines Gateversuchs |
| `gate.finished` | `gateId`, `attempt`, `targetTreeId`, `evidenceSha256` | Ende desselben Gateversuchs; Resultat/Exit/Failure stehen in der Huelle |
| `build.failed` | `stageId`, `attempt`, `targetTreeId`, `evidenceSha256` | expliziter Buildfehler, zusaetzlich zum zugehoerigen Gateende |
| `test.failed` | `stageId`, `attempt`, `targetTreeId`, `evidenceSha256` | expliziter Testfehler |
| `lint.failed` | `stageId`, `attempt`, `targetTreeId`, `evidenceSha256` | expliziter Lint-/Formatfehler |
| `security.failed` | `stageId`, `attempt`, `targetTreeId`, `evidenceSha256` | expliziter Security-/Lizenzfehler |
| `verify.failed` | `stageId`, `attempt`, `targetTreeId`, `evidenceSha256` | expliziter Harness-/Verifyfehler |
| `repair.attempted` | `repairId`, `triggerEventId`, `targetFindingId`, `beforeTreeId` | Beginn genau eines Reparaturversuchs |
| `repair.outcome` | `repairId`, `afterTreeId`, `outcomeClass`, `verificationEventId` | Ergebnis: `fixed`, `not-fixed`, `regressed`, `abandoned` oder `unknown` |
| `ledger.recovery.recorded` | `originalLedgerSha256`, `verifiedPrefixSha256`, `tornTailSha256`, `recoveredLedgerPath` | nur in einer neuen Recovery-Datei; dokumentiert fail-closed Recovery, nie stilles Kuerzen des Originals |
| `context.compacted` | `compactionId`, `beforeContextSha256`, `summarySha256` | Kontextverdichtung; kein Rohprompt/-chat |
| `run.resumed` | `resumedRunId`, `resumeFromEventId`, `resumeStateSha256` | Fortsetzung nach Pause/Compaction/Block; Kontinuitaet wird belegt, nicht vermutet |
| `routing.decided` | `routingDecisionId`, `fromTier`, `toTier`, `reasonCode`, `policySha256` | Routingentscheidung, auch ohne Modellwechsel |
| `model.switched` | `fromModelId`, `toModelId`, `routingDecisionId`, `reasonCode` | tatsaechlicher Modellwechsel im gebundenen Lauf |
| `budget.blocked` | `blockId`, `budgetClass`, `observedLimit`, `receiptSha256` | belegter Budgetblock; fehlender Receiptwert bleibt `unknown` |
| `rate.blocked` | `blockId`, `rateClass`, `retryAfter`, `receiptSha256` | Rate-Limit-/Quotenblock |
| `provider.blocked` | `blockId`, `providerClass`, `reasonCode`, `receiptSha256` | Providerverfuegbarkeit oder -policy blockiert |
| `infrastructure.blocked` | `blockId`, `resourceClass`, `reasonCode`, `evidenceSha256` | lokale/CI/Hardware/Netz-/Dienstinfrastruktur blockiert |
| `block.resolved` | `blockId`, `resolutionClass`, `resumedEventId` | Ende genau eines Blocks; fehlende Aufloesung bleibt offen |
| `revision.observed` | `baseCommit`, `resultCommit`, `resultTreeId`, `changedFiles`, `changedPaths`, `linesAdded`, `linesDeleted` | belegte Aenderungsgrenze; Working-Tree-Aenderungen benoetigen ein eigenes Snapshotmanifest |
| `git.commit.observed` | `commitId`, `parentCommitIds`, `commitTreeId`, `commitTimeUtc` | historische Git-Tatsache; Commitzeit ist keine Arbeitszeit |
| `git.tree.promoted` | `fromRef`, `toRef`, `promotedCommit`, `promotedTreeId`, `authorityClass` | belegte Promotion; Zielref und Autoritaet Pflicht |
| `git.rollback.observed` | `rollbackCommit`, `fromTreeId`, `toTreeId`, `reasonCode` | expliziter Rollback; kein Rueckschluss nur aus geloeschten Zeilen |
| `git.supersession.observed` | `supersededCommit`, `supersedingCommit`, `reasonCode` | sichtbare Ersetzung ohne Historienumdeutung |
| `architecture.checkpoint.created` | `checkpointId`, `pathMapVersion`, `fileInventorySha256`, `dependencyInventorySha256`, `analyzerInventorySha256`, `testInventorySha256`, `acceptedTaskId`, `acceptedTreeId`, `gateCoupled` | reproduzierbarer Architekturstand; fuer jeden akzeptierten Task/Baum ist genau ein diagnostischer Checkpoint Pflicht; `gateCoupled` muss `false` sein |
| `milestone.reached` | `milestoneId`, `authorityClass`, `milestoneTreeId` | belegter Meilenstein; keine Ableitung nur aus Datum oder Commitzahl |
| `git.tag.observed` | `tagRef`, `tagObjectId`, `targetCommit`, `targetTreeId`, `tagClass` | beobachteter Git-Tag; Release-/Milestonebedeutung nur mit expliziter Klasse/Autoritaet |
| `defect.observed` | `defectId`, `discoveredAtUtc`, `affectedCommit`, `affectedTreeId`, `discoveryPhase`, `severity` | bestaetigter Defekt; Escape nur, wenn Entdeckung nach formaler Akzeptanz des betroffenen Baums liegt |
| `tool.finished` | `toolClass`, `commandDigest`, `startedMonotonicNs`, `completedMonotonicNs`, `resultSha256` | abgeschlossener Toolakt; Rohargumente werden nicht benoetigt |
| `review.observed` | `reviewId`, `verdict`, `findings`, `targetTreeId` | unabhaengiger oder nicht unabhaengiger Review mit gebundenem Zielbaum |
| `research.intervention.started` | `interventionId`, `category`, `decisionActSha256`, `counted`, `classificationReason` | oeffnet ein menschliches Aktivintervall an der Huellen-Monotonzeit; nur `I0-observation-no-intervention` hat `counted=false` |
| `research.intervention.ended` | `interventionId`, `durationMs` | schliesst genau dieselbe offene ID auf derselben monotonen Uhr; sonst `durationMs=unknown` und Validierungsfehler |
| `research.intervention.recorded` | `interventionId`, `category`, `decisionActSha256`, `counted`, `classificationReason`, `durationMs` | punktueller Akt; `durationMs` ist immer literal `unknown` |
| `human.instruction` | `humanActId`, `decisionActSha256`, `interventionCategory`, `counted` | Anweisung; Scope-/Technikwirkung ueber Interventionskategorie |
| `human.review` | `humanActId`, `reviewId`, `decisionActSha256`, `interventionCategory`, `counted` | menschlicher Reviewakt; reines Lesen darf `counted=false` sein |
| `human.correction` | `humanActId`, `targetFindingId`, `decisionActSha256`, `interventionCategory`, `counted` | Korrekturwirkung; Kategorie nach Wirkung, etwa `I6-defect-report`, `I3-technical-direction` oder `I9-review-promotion` |
| `human.approval` | `humanActId`, `authorityClass`, `decisionActSha256`, `interventionCategory`, `counted` | Freigabe-/Promotionwirkung; regulaer `I9-review-promotion` |
| `human.emergency` | `humanActId`, `emergencyClass`, `decisionActSha256`, `interventionCategory`, `counted` | Notstopp/-eingriff; regulaer `I10-emergency-stop` |
| `human.observation` | `humanActId`, `observationClass`, `decisionActSha256`, `interventionCategory`, `counted` | reine Beobachtung ohne Wirkung ist `I0-observation-no-intervention` und hat `counted=false` |
| `outcome.observed` | `taskOutcome`, `hypothesisResult`, `resultCommit`, `resultTreeId`, `reasonCode` | trennt Taskoutcome von Forschungsergebnis und bindet den beobachteten Ergebnisbaum explizit |
| `observation.closed` | `eventCount`, `sourceManifestSha256`, `outcomeEventId`, `closedAtUtc` | schliesst die Primaerereigniskette genau einmal nach einem aufloesbaren `outcome.observed`; sein Huelle-Feld `eventHash` ist der finale Kettenhash, Exporthashes entstehen erst danach |

Fuer Ereignisse mit inhaltlich nicht anwendbaren Huellenfeldern bleibt das
Feld literal `unknown`. Ein `task.planned` ohne Prozessende hat beispielsweise
`exitCode=unknown`; ein `gate.finished` ohne Tokenreceipt hat alle Token- und
Kostenfelder `unknown`; ein `human.observation` ohne Git-Aenderung hat
`baseCommit`, `headCommit`, `treeId` und Changefelder `unknown`. Das ist eine
explizite Nichtanwendbarkeit, kein unvollstaendiger Erfolg.

## Aktivitaetsintervalle

`autonomyMode` und `activityState` sind orthogonal. Ein autonomer Lauf kann
`agent-active`, `idle`, `sleeping`, `blocked` oder `offline` sein; ein
menschlich gesteuerter Lauf ebenfalls. `activity.state.changed` schliesst das
vorherige Intervall am eigenen `monotonicTimeNs` und oeffnet das neue Intervall
an derselben Grenze. Das erste Run-/Autopilotereignis muss den Ausgangszustand
tragen; Run-/Autopilotende schliesst das letzte Intervall. Ueber verschiedene
`monotonicClockId` werden keine Aktivitaetsdauern berechnet. Fuer
`autonomy.mode.changed` gilt dieselbe Intervallregel auf der unabhaengigen
Modusachse. Fehlende Anfangs-, Wechsel- oder Endgrenze macht die betroffene
Zustands- bzw. Modusdauer literal `unknown`.

`agent-active` bedeutet belegte Agenten-/Toolarbeit, `idle` laufbereit ohne
belegte Arbeit, `sleeping` absichtlich zeitgesteuertes Warten, `blocked`
Warten auf eine benannte innere oder aeussere Bedingung und `offline` keinen
laufenden Agenten-/Autopilotprozess. Ein bloss fehlendes Toolereignis beweist
nicht `idle`, `sleeping` oder `offline`.

`taskOutcome` ist `accepted`, `rejected`, `blocked`, `cancelled` oder
`unknown`. `hypothesisResult` ist `supports`, `contradicts`, `inconclusive`
oder `unknown`. Die Werte sind unabhaengig: ein akzeptierter Task kann ein
Forschungsergebnis widerlegen, ein gescheiterter Task kann eine valide
Beobachtung liefern.

Review-`verdict` ist `pass`, `needs-work`, `block`, `reject` oder `unknown`.
`discoveryPhase` eines Defekts ist `pre-review`, `review`, `post-review`,
`post-acceptance` oder `unknown`; fuer Defect-Escape muss zusaetzlich die
Zeit-/Baumbindung eine Entdeckung nach `task.accepted` belegen. Ein blosses
Label `post-acceptance` ohne diese Bindung zaehlt nicht.

## Intervention

| Feld | Typ | Definition |
|---|---|---|
| `interventionId` | string | innerhalb der Beobachtung eindeutiger Entscheidungsakt |
| `category` | enum oder `unknown` | `I0-observation-no-intervention`, `I1-clarification`, `I2-scope-criteria-change`, `I3-technical-direction`, `I4-domain-decision`, `I5-priority-change`, `I6-defect-report`, `I7-technical-unblock`, `I8-infrastructure`, `I9-review-promotion`, `I10-emergency-stop`, `I11-other` oder `unknown` |
| `decisionActSha256` | hash | Hash der redigierten semantischen Kurzfassung, nicht der Rohunterhaltung |
| `responseToQuestionId` | string oder `unknown` | bindet eine Klaerung an eine belegte Rueckfrage |
| `counted` | boolean | `false` fuer `I0-observation-no-intervention`; Initialauftrag und vorab eingefrorene Regeln werden nicht als Interventionsereignis erfasst |
| `classificationReason` | string | knappe beobachtbare Begruendung; keine Gedankenketten |
| `durationMs` | integer oder `unknown` | nur bei gueltigem Start-/Endpaar aus derselben monotonen Uhr; offener Start und punktuelles `record` bleiben literal `unknown` |

Eine Nachricht mit mehreren unabhaengigen Entscheidungen darf mehrere
Interventionen erzeugen, wenn jede Entscheidung einen eigenen Hash und eine
eigene Wirkung besitzt. Wiederholte Zustellung derselben Entscheidung mit
gleichem Hash zaehlt einmal.

Die CLI-Operationen `research intervention start`, `end` und `record` werden
jeweils auf die gleichnamigen Ereignistypen abgebildet. Ein offener Start darf
im Status erscheinen, wird aber weder am Beobachtungsende geschlossen noch
aus UTC-, Nachrichten- oder Antwortzeiten mit einer Dauer versehen.

`research begin` ist die einzige Operation, die einen prospektiven
Active-Marker erzeugt. Dessen kanonische Bytes binden Study-/Observation-ID,
Zieltask, Baseline-/HEAD-/Tree-ID, Protokollbundle, Study-Manifest,
`activationGuardSha256` und den letzten Ledgerhash. Der Marker wird erst nach
fsynctem `protocol.frozen` und `observation.started` ueber eine exklusiv
angelegte gleichdateisystemige Temporaerdatei publiziert: Datei-fsync,
atomarer Rename, Parent-Directory-fsync und anschliessendes no-follow Reopen
mit Byte-/Bindungspruefung sind Pflicht. Erst danach existiert ein
erfolgreicher Aktivierungsreceipt. Ein idempotenter Retry darf einen
vollstaendig gueltigen Marker bestaetigen, niemals aber eine Startkette ohne
dauerhaften Marker rekonstruieren; diese bleibt `INCOMPLETE_ACTIVATION` und
nicht prospektiv verwendbar. Ein zweites Begin, ein beweglicher oder
abweichender Gitstand und jeder bereits belegte Start des Zielpfads scheitern
fail-closed. `research close` akzeptiert nur einen aufloesbaren strukturierten
Outcome-Receipt, bindet dessen Hash an `outcome.observed`/
`observation.closed`, fsynct die finale Kette, validiert Kette und Marker,
entfernt den Marker und fsynct dessen Parent-Verzeichnis. Eine geschlossene
Kette mit verbliebenem Marker ist `STALE_ACTIVE_MARKER`: Hooks sind inaktiv,
und ein Close-Retry entfernt ihn ohne doppeltes Ereignis idempotent. Weder
Begin noch Close veraendern den Zieltask.

## WIP-Provenienz-Sidecar

Jeder kuenftige WIP-Snapshot erhaelt neben dem unveraenderten Git-Objekt eine
separate, kanonische Sidecar-Datei. Sie enthaelt exakt die Schluessel `Task`,
`Phase`, `Agent-Role`, `Run`, `Parent`, `LastGate`, `FailureClass`,
`AutonomyState` und `ResearchSchema`. Jeder Wert ist ein redigierter String
oder literal `unknown`: `Task` ist die Task-ID, `Phase` der belegte
Lifecyclezustand, `Agent-Role` die Rolle, `Run`/`Parent` die Run-IDs,
`LastGate` die letzte gebundene Gate-ID samt Resultat, `FailureClass` die
letzte bekannte Fehlerklasse, `AutonomyState` der `autonomyMode` und
`ResearchSchema` die Datenvertragsversion. Sidecar und Snapshot werden ueber
Snapshot-ID, Tree-ID und Sidecar-SHA-256 gebunden. Der Sidecar schreibt keine
Git-Historie um, erteilt keine direkte `main`-Autoritaet und macht WIP weder
akzeptiert noch promotionsfaehig.

## Gateversuch

| Feld | Typ | Definition |
|---|---|---|
| `gateId` | string | unveraenderte Projekt-Gate-ID, z. B. `G-SPEC` |
| `attempt` | integer | je Gate und Beobachtung bei 1 beginnend, lueckenlos |
| `result` | enum oder `unknown` | `pass`, `fail`, `blocked`, `cancelled` oder `unknown` |
| `startedMonotonicNs` / `finishedMonotonicNs` | integer oder `unknown` | echte Gategrenzen derselben `monotonicClockId`; Prozess-/Commitzeiten sind kein Ersatz |
| `durationMs` | integer oder `unknown` | `(finished-started)/1_000_000`, ganzzahlig abwaerts, nur bei gleicher bekannter monotoner Uhr |
| `evidenceSha256` | hash oder `unknown` | Hash des vollstaendigen redigierten Belegs |
| `targetTreeId` | Git-Objekt-ID oder `unknown` | getesteter Git-Baum; ein Working-Tree-Snapshot wird als SHA-256-Manifest separat referenziert |

Ein Exitcode 0 ohne aufloesbaren Gatevertrag und Zielbaum wird nicht zu
`pass`; `result` bleibt `unknown`. Ein fehlender Gateversuch ist kein Pass.
Bei vollstaendiger Gatehistorie gilt fuer `gate.started`/`gate.finished`
`retryIndex=attempt-1`; fehlen fruehere Versuche oder ist die semantische
Operation nicht identisch, bleibt `retryIndex=unknown`. Fuer
`repair.attempted` und das passende `repair.outcome` ist `repairIndex` je
`triggerEventId` bei 0 beginnend und auf beiden Ereignissen identisch.

## Architekturzeile

| Feld | Typ | Definition |
|---|---|---|
| `observationId` | string | Beobachtung |
| `checkpointId` | string | gebundener Architekturcheckpoint |
| `baselineCommit` / `resultCommit` | Git-Objekt-ID oder `unknown` | Vergleichsgrenzen |
| `pathMapVersion` | string | eingefrorene Pfadklassifikation |
| `productionFilesChanged` | integer oder `unknown` | verschiedene geaenderte Dateien unter Produktionspfaden |
| `productionModulesTouched` | integer oder `unknown` | verschiedene `src/<projekt>`-Module |
| `projectReferenceEdgesAdded` | integer oder `unknown` | neue deklarierte Projekt-Referenzkanten |
| `projectReferenceEdgesRemoved` | integer oder `unknown` | entfernte deklarierte Projekt-Referenzkanten |
| `confirmedBoundaryViolations` | integer oder `unknown` | nur bestaetigte Validator-/Reviewbefunde |
| `grossLinesAdded` / `grossLinesDeleted` | integer oder `unknown` | Git-Numstat ueber Textdateien; Binaeranteile separat unknown |
| `binaryFilesChanged` | integer oder `unknown` | geaenderte Binaerdateien |

## Architekturcheckpoint

`architecture.checkpoint.created` bindet zwei kanonische CSV-Unterexporte:
`architecture-files.csv` und `architecture-dependencies.csv`. Der Checkpoint
ist in Version 1 rein beobachtend und traegt zwingend `gateCoupled=false`.
Jedes `task.accepted` muss bis `observation.closed` genau einen Checkpoint mit
demselben `acceptedTaskId` und `acceptedTreeId` besitzen. Fehlt er, bleiben
checkpointabhaengige und akzeptanzbezogene Aggregationen `unknown`; die
Akzeptanz wird nicht rueckwirkend erfunden oder aufgehoben.

`architecture-files.csv` hat exakt die Spalten:

`checkpoint_id,tree_id,repo_relative_path,file_class,component_id,lines,baseline_lines,line_delta,analyzer_warning_count,test_case_count,complexity_method,complexity_value,source_sha256`

- `file_class` ist `production`, `test`, `harness`, `specification`,
  `documentation`, `generated` oder `unknown`.
- `lines` ist die Zahl der LF-separierten logischen Zeilen; eine nichtleere
  letzte Zeile ohne LF zaehlt. Binaerdateien sind `unknown`.
- `line_delta=lines-baseline_lines`; fehlt eine Grenze, `unknown`.
- `component_id` folgt der eingefrorenen Pfadkarte. Fuer die drei
  Integrationspunkte werden zusaetzlich stabile Komponenten-IDs
  `CommandLoopRunner`, `CommandReportSchema` und `SessionEngine` verwendet,
  sobald der gebundene Baum die jeweiligen Symbole/Pfade enthaelt; Abwesenheit
  ist ein beobachteter Nullbestand, nicht ein Fehler.
- `analyzer_warning_count` zaehlt Warnungs-IDs, die exakt diesem Pfad im
  gebundenen Analyzerexport zugeordnet sind. Ohne Analyzerlauf `unknown`.
- `test_case_count` ist nur fuer Testdateien die Zahl vom gebundenen
  Testinventar eindeutig zugeordneter Test-IDs; Quelltextheuristik allein ist
  unzulaessig.
- Complexity ist optional. Ohne eingefrorene `complexity_method` sind Methode
  und Wert literal `unknown`; verschiedene Methoden werden nie verglichen.

`architecture-dependencies.csv` hat exakt die Spalten:

`checkpoint_id,tree_id,from_component,to_component,dependency_kind,direction_class,evidence_sha256`

`dependency_kind` ist `project-reference`, `namespace-reference`,
`native-link`, `runtime-load` oder `unknown`. `direction_class` ist `allowed`,
`forbidden` oder `unknown` gemaess einem versionierten Architekturvertrag; nur
`forbidden` mit aufloesbarer Evidenz zaehlt als Grenzverletzung.

Der Summaryexport berechnet aus diesen Zeilen Produktions-/Testzeilen,
Komponentenanteile, Analyzerwarnungen, Testanzahl und -wachstum. „Groesste
Dateien“ und „groesstes Wachstum“ sind je die ersten zehn Zeilen nach
`lines DESC,repo_relative_path ASC` bzw.
`line_delta DESC,repo_relative_path ASC`; negative Deltas duerfen in der
Wachstumsliste erscheinen, wenn weniger als zehn positive existieren. Die
Integrationspunktkonzentration ist der Anteil geaenderter Produktionszeilen in
`CommandLoopRunner`, `CommandReportSchema` und `SessionEngine`. In Version 1
sind alle diese Trends diagnostisch und nicht gate-koppelnd.

## Metrikzeile

`metrics.csv` ist Long-Format mit genau diesen Spalten:

`observation_id,evidence_class,metric_id,value,unit,availability_reason,source_manifest_sha256,protocol_version`

`availability_reason` ist `observed`, `source-missing`, `source-unresolvable`,
`not-applicable`, `redacted`, `mixed-evidence-class` oder `invalid-input`.
Wenn `value` nicht `unknown` ist, muss `availability_reason=observed` gelten.
Wenn `value=unknown` ist, darf `availability_reason` nicht `observed` sein.

## Exporttabellen

| Datei | Primaerschluessel | Ordnung |
|---|---|---|
| `events.jsonl` | `observationId,sequence` | Beobachtung, numerische Sequenz |
| `observations.csv` | `observation_id` | lexikografisch |
| `autopilot-cycles.csv` | `observation_id,cycle_id` | Beobachtung, Cycle-ID |
| `agent-runs.csv` | `observation_id,run_id` | Beobachtung, Startzeit, Run-ID |
| `task-lifecycle.csv` | `observation_id,task_id,event_id` | Beobachtung, Task-ID, Sequenz |
| `continuity.csv` | `observation_id,event_id` | Beobachtung, Sequenz |
| `activity-intervals.csv` | `observation_id,run_id,event_id` | Beobachtung, Run-ID, Sequenz |
| `routing.csv` | `observation_id,event_id` | Beobachtung, Sequenz |
| `human-events.csv` | `observation_id,human_act_id` | Beobachtung, Human-Act-ID |
| `interventions.csv` | `observation_id,intervention_id` | Beobachtung, `occurred_at_utc`, ID |
| `gate-attempts.csv` | `observation_id,gate_id,attempt` | Beobachtung, Gate-ID, Versuch |
| `failures-and-repairs.csv` | `observation_id,event_id` | Beobachtung, Sequenz |
| `blocks.csv` | `observation_id,block_id,event_id` | Beobachtung, Block-ID, Sequenz |
| `git-evolution.csv` | `observation_id,event_id` | Beobachtung, Sequenz |
| `outcomes.csv` | `observation_id,event_id` | Beobachtung, Sequenz |
| `usage.csv` | `observation_id,event_id` | Beobachtung, Sequenz |
| `architecture-trends.csv` | `observation_id,checkpoint_id` | Beobachtung, Checkpoint |
| `architecture-files.csv` | `checkpoint_id,repo_relative_path` | Checkpoint, Pfad |
| `architecture-dependencies.csv` | `checkpoint_id,from_component,to_component,dependency_kind` | Checkpoint, Kantenfelder |
| `metrics.csv` | `observation_id,metric_id` | Beobachtung, Metrik-ID |
| `study-manifest.json` | `studyId,protocolVersion,inputTreeId` | JSON-Schluessel kanonisch sortiert |
| `evidence-manifest.json` | Pfad | Pfad in Unicode-Codepoint-Reihenfolge |
| `summary.json` | `observationId` | JSON-Schluessel kanonisch sortiert |
| `report.md` | festes Template | deterministische Abschnitte und Metrikreihenfolge, LF |
| `EXPORT.SHA256` | Pfad | jede Exportdatei ausser sich selbst, Unicode-Codepoint-Reihenfolge |

Jede ereignisbasierte CSV-Fakttabelle enthaelt als gemeinsame Spalten exakt
`study_id,observation_id,run_id,parent_run_id,cycle_id,task_id,event_id,sequence,monotonic_time_ns,monotonic_clock_id,occurred_at_utc,evidence_class,actor_role,actor_id,provider_id,model_id,model_version,branch_ref,base_commit,head_commit,tree_id,autonomy_mode,activity_state,result,exit_code,failure_class,retry_index,repair_index,usage_provenance,cost_provenance,request_count,input_tokens,output_tokens,cache_read_tokens,cache_write_tokens,cost_amount,cost_currency,changed_files,changed_paths,lines_added,lines_deleted,binary_files_changed,human_active_duration_ms,privacy_class,redaction_status,redaction_policy_version,event_hash`.
Nicht anwendbare Spalten enthalten literal `unknown`. Danach folgen die im
Ereignisregister genannten Payloadfelder in lexikografischer Reihenfolge.
`observations.csv` aggregiert ausschliesslich Identitaet, Start/Ende,
Evidenzklasse, Protokoll-/Quellmanifesthash und Task-/Forschungsergebnis;
`architecture-trends.csv` und `metrics.csv` folgen ihren eigenen oben
definierten Spaltenvertraegen.

CSV verwendet RFC 4180-Quoting, Komma als Trennzeichen, LF und immer eine
Headerzeile. JSON wird als UTF-8, ohne unbedeutenden Whitespace und mit
lexikografisch sortierten Objektschluesseln serialisiert. Array-/Objektwerte
wie `changed_paths` werden innerhalb einer CSV-Zelle als kanonisches JSON
serialisiert und anschliessend nach RFC 4180 gequotet; `unknown` bleibt die
unquotierte semantische Zeichenkette, wobei das CSV-Quoting selbst weiterhin
nach RFC 4180 erfolgen darf.
