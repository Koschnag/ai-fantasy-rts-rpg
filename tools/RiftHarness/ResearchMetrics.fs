namespace RiftHarness

open System
open System.Collections.Generic
open System.Globalization
open System.Text.Json

type ResearchMetricRow =
    { MetricId: string
      Value: string
      Unit: string
      AvailabilityReason: string
      EvidenceClass: string }

[<RequireQualifiedAccess>]
module ResearchMetrics =
    let private metricIds =
        [ "OBS-CHAIN-COMPLETE"; "OBS-SOURCE-RESOLUTION-RATE"; "OBS-EXPORT-BYTE-IDENTICAL"
          "OBS-NON-INTERFERENCE"; "OBS-UNKNOWN-RATE"; "TIME-WALL-MS"; "TIME-TO-OUTCOME-MS"
          "TIME-TO-FIRST-GREEN-MS"; "TIME-TO-HUMAN-MS"; "AGENT-UNINTERRUPTED-MAX-MS"
          "TOOL-ACTIVE-MS"; "WAIT-MS"; "MODE-AUTONOMOUS-MS"; "MODE-HUMAN-DIRECTED-MS"
          "ACTIVITY-AGENT-ACTIVE-MS"; "ACTIVITY-IDLE-MS"; "ACTIVITY-SLEEPING-MS"
          "ACTIVITY-BLOCKED-MS"; "ACTIVITY-OFFLINE-MS"; "ACTIVITY-AGENT-ACTIVE-RATIO"
          "INT-COUNT"; "INT-I0-OBSERVATION"; "INT-I1-CLARIFICATION"; "INT-I2-SCOPE-CRITERIA-CHANGE"; "INT-I3-TECHNICAL-DIRECTION"; "INT-I4-DOMAIN-DECISION"; "INT-I5-PRIORITY-CHANGE"; "INT-I6-DEFECT-REPORT"; "INT-I7-TECHNICAL-UNBLOCK"; "INT-I8-INFRASTRUCTURE"; "INT-I9-REVIEW-PROMOTION"; "INT-I10-EMERGENCY-STOP"; "INT-I11-OTHER"; "INT-UNKNOWN"; "INT-RATE-PER-HOUR"
          "INT-QUESTION-UNANSWERED"; "INT-OPEN"; "INT-CLOSED-ACTIVE-MS"; "GATE-ATTEMPTS-TOTAL"
          "GATE-FAILED-ATTEMPTS"; "GATE-BLOCKED-ATTEMPTS"; "GATE-FIRST-PASS-ATTEMPTS"
          "GATE-COVERAGE"; "GATE-PASS-COVERAGE"; "REPAIR-CYCLES"; "REVIEW-FINDINGS"
          "REVIEW-REWORK-FILES"; "CHANGE-FILES"; "CHANGE-LINES-ADDED"; "CHANGE-LINES-DELETED"
          "CHANGE-BINARY-FILES"; "ARCH-PRODUCTION-FILES"; "ARCH-MODULES-TOUCHED"
          "ARCH-CROSS-MODULE"; "ARCH-REF-EDGES-ADDED"; "ARCH-REF-EDGES-REMOVED"
          "ARCH-BOUNDARY-VIOLATIONS"; "ARCH-PRODUCTION-LINES"; "ARCH-TEST-LINES"
          "ARCH-ANALYZER-WARNINGS"; "ARCH-TEST-COUNT"; "ARCH-TEST-GROWTH"
          "ARCH-INTEGRATION-CONCENTRATION"; "ARCH-ACCEPTED-CHECKPOINT-COVERAGE"
          "TRACE-AC-EVIDENCE-COVERAGE"; "TRACE-EVENT-SOURCE-COVERAGE"; "QUALITY-OUTCOME"
          "QUALITY-DETERMINISTIC-REPEAT"; "QUALITY-POST-ACCEPT-DEFECTS"; "USE-REQUESTS"
          "USE-INPUT-TOKENS"; "USE-OUTPUT-TOKENS"; "USE-CACHE-READ-TOKENS"
          "USE-CACHE-WRITE-TOKENS"; "USE-COST-AMOUNT"; "USE-COST-ESTIMATED-AMOUNT"
          "USE-MACHINE-CPU-MS"; "USE-ENERGY-WH"; "PERF-CPU-P50-MS"; "PERF-CPU-P95-MS"; "PERF-CPU-P99-MS"; "PERF-GPU-P50-MS"; "PERF-GPU-P95-MS"; "PERF-GPU-P99-MS"; "PERF-FRAME-P50-MS"; "PERF-FRAME-P95-MS"
          "PERF-FRAME-P99-MS"; "PERF-ONE-PCT-LOW-FPS"; "PERF-HITCH-COUNT"
          "PERF-RAM-RESIDENT-MAX-BYTES"; "PERF-RAM-PEAK-BYTES"; "PERF-VRAM-DIRECT-BYTES"
          "PERF-VRAM-ESTIMATE-BYTES"; "PERF-LOAD-COLD-MS"; "PERF-LOAD-WARM-MS"
          "PERF-DRAW-CALLS"; "PERF-TRIANGLES"; "PERF-VISIBLE-UNITS"; "PERF-POWER-W"
          "PERF-BUDGET-RESULT"; "AUTO-INSTANCES"; "AUTO-OBSERVED-SPAN-MS"; "AUTO-PAUSED-MS"
          "AUTO-BLOCKED-MS"; "AUTO-ACTIVE-MS"; "AUTO-ACTIVE-RATIO"; "RUN-STARTED"
          "RUN-FINISHED"; "RUN-FINISH-RATE"; "RUN-CHILDREN"; "RUN-MAX-DEPTH"
          "RUN-ACCEPTED-OUTCOME-RATE"; "TASK-PLANNED"; "TASK-READY"; "TASK-IMPLEMENTED"
          "TASK-REVIEWED"; "TASK-REJECTED"; "TASK-ACCEPTED"; "TASK-PLAN-TO-READY-MS"
          "TASK-READY-TO-IMPLEMENTED-MS"; "TASK-IMPLEMENTED-TO-REVIEWED-MS"
          "TASK-READY-TO-ACCEPTED-MS"; "TASK-READY-TO-ACCEPT-RATE"; "TASK-REJECTION-RATE"
          "WIP-SNAPSHOTS"; "WIP-DISTINCT-TREES"; "WIP-PROMOTED-7D-RATE"; "FAIL-BUILD"
          "FAIL-TEST"; "FAIL-LINT"; "FAIL-SECURITY"; "FAIL-VERIFY"; "FAIL-UNIQUE-CLASSES"
          "REPAIR-ATTEMPTED"; "REPAIR-FIXED"; "REPAIR-SUCCESS-RATE"
          "REPAIR-ATTEMPTS-PER-FIX"; "REPAIR-TIME-TO-FIX-MS"; "FAIL-RECURRENCE-RATE"
          "CONTEXT-COMPACTIONS"; "RUN-RESUMES"; "RESUME-CONTINUITY-RATE"; "ROUTING-DECISIONS"
          "MODEL-SWITCHES"; "MODEL-SWITCHES-PER-RUN"; "MODEL-DWELL-MS"
          "ROUTING-OUTCOME-RATE"; "BLOCK-BUDGET"; "BLOCK-RATE"; "BLOCK-PROVIDER"
          "BLOCK-INFRASTRUCTURE"; "BLOCK-OPEN"; "BLOCK-DURATION-MS"
          "BLOCK-MEDIAN-RESOLUTION-MS"; "BLOCK-RESUME-RATE"; "GIT-COMMITS"
          "GIT-DISTINCT-TREES"; "GIT-PROMOTIONS"; "GIT-ROLLBACKS"; "GIT-SUPERSESSIONS"
          "PROMOTION-RATE"; "ROLLBACK-PER-PROMOTION"; "IMPLEMENTED-TO-PROMOTED-MS"
          "HUMAN-INSTRUCTION"; "HUMAN-REVIEW"; "HUMAN-CORRECTION"; "HUMAN-APPROVAL"
          "HUMAN-EMERGENCY"; "HUMAN-OBSERVATION"; "HUMAN-COUNTED-RATE"
          "HUMAN-CORRECTIONS-PER-ACCEPTED"; "HUMAN-EMERGENCY-RATE"; "OUTCOME-MILESTONES"
          "OUTCOME-TAGS"; "WINDOW-DAYS"; "ACCEPTED-OUTCOMES-PER-DAY"; "MILESTONES-PER-DAY"
          "FILES-PER-ACCEPTED"; "LINES-PER-ACCEPTED"; "REVIEW-FIRST-PASS-RATE"
          "DEFECT-ESCAPES"; "DEFECT-ESCAPE-RATE"; "REWORK-LINES"; "REWORK-RATIO"
          "ROLLBACK-RATE"; "ACCEPTED-STREAK-NO-HUMAN"; "WIP-SNAPSHOT-PER-ACCEPTED"
          "WIP-TREE-ACCEPT-RATE"; "DISCARDED-TREES-7D"; "DISCARDED-LINES-7D"
          "GATE-RECOVERY-MS"; "PROD-TEST-CHANGE-RATIO"; "USE-TOKENS-TOTAL"
          "USE-TOKENS-PER-ACCEPTED"; "USE-COST-PER-ACCEPTED"; "USE-COST-PER-FIX"
          "HUMAN-ACTIVE-MINUTES"; "HUMAN-MINUTES-PER-ACCEPTED"; "PRODUCTIVE-AUTONOMY-MS"
          "PRODUCTIVE-AUTONOMY-MS-PER-ACCEPTED"; "ACCEPTED-PER-1M-TOKENS"
          "ACCEPTED-PER-AUTO-ACTIVE-HOUR" ]

    let private unitOf (metricId: string) =
        if metricId.EndsWith("-MS", StringComparison.Ordinal) then "ms"
        elif metricId.EndsWith("-MINUTES", StringComparison.Ordinal) then "minutes"
        elif metricId.EndsWith("-RATE", StringComparison.Ordinal)
             || metricId.EndsWith("-RATIO", StringComparison.Ordinal)
             || metricId.EndsWith("-COVERAGE", StringComparison.Ordinal) then "ratio"
        elif metricId.Contains("TOKENS", StringComparison.Ordinal) then "tokens"
        elif metricId.Contains("LINES", StringComparison.Ordinal) then "lines"
        elif metricId.Contains("COST", StringComparison.Ordinal) then "ISO-currency"
        elif metricId = "WINDOW-DAYS" then "days"
        elif metricId.StartsWith("OBS-", StringComparison.Ordinal)
             || metricId.StartsWith("QUALITY-", StringComparison.Ordinal)
             || metricId = "ARCH-CROSS-MODULE"
             || metricId = "PERF-BUDGET-RESULT" then "boolean-or-enum"
        else "count"

    let private known (evidenceClass: string) (metricId: string) (value: string) =
        { MetricId = metricId
          Value = value
          Unit = unitOf metricId
          AvailabilityReason = "observed"
          EvidenceClass = evidenceClass }

    let private unknown (evidenceClass: string) (metricId: string) (reason: string) =
        { MetricId = metricId
          Value = ResearchContract.Unknown
          Unit = unitOf metricId
          AvailabilityReason = reason
          EvidenceClass = evidenceClass }

    let private invariantInt (value: int64) = value.ToString(CultureInfo.InvariantCulture)

    let private invariantRatio (numerator: int64) (denominator: int64) =
        if denominator = 0L then
            None
        else
            Some((decimal numerator / decimal denominator).ToString("0.############################", CultureInfo.InvariantCulture))

    let private payloadString (name: string) (event: ResearchEvent) =
        match event.Body.Payload.TryGetProperty(name) with
        | true, value when value.ValueKind = JsonValueKind.String -> Some(value.GetString())
        | _ -> None

    let private payloadBool (name: string) (event: ResearchEvent) =
        match event.Body.Payload.TryGetProperty(name) with
        | true, value when value.ValueKind = JsonValueKind.True -> Some true
        | true, value when value.ValueKind = JsonValueKind.False -> Some false
        | _ -> None

    let private distinctPayload (name: string) (events: ResearchEvent list) =
        events |> List.choose (payloadString name) |> Set.ofList |> Set.count |> int64

    let private countType (eventType: string) (events: ResearchEvent list) =
        events |> List.filter (fun event -> event.Body.EventType = eventType) |> List.length |> int64

    let private countedHuman (event: ResearchEvent) =
        event.Body.EventType.StartsWith("human.", StringComparison.Ordinal)
        && payloadBool "counted" event = Some true

    let private evidenceClassOf (events: ResearchEvent list) =
        match events |> List.map (fun event -> event.Body.EvidenceClass) |> Set.ofList |> Set.toList with
        | [ value ] -> value
        | [] -> ResearchContract.Unknown
        | _ -> ResearchContract.Unknown

    let private setKnown (metricId: string) (value: string) (rows: Dictionary<string, ResearchMetricRow>) =
        let evidenceClass = rows[metricId].EvidenceClass
        rows[metricId] <- known evidenceClass metricId value

    let private setRatio (metricId: string) (numerator: int64) (denominator: int64) rows =
        match invariantRatio numerator denominator with
        | Some value -> setKnown metricId value rows
        | None -> ()

    let private monotonicDuration (startType: string) (endType: string) (events: ResearchEvent list) =
        let starts = events |> List.filter (fun event -> event.Body.EventType = startType)
        let endings = events |> List.filter (fun event -> event.Body.EventType = endType)

        match starts, endings with
        | [ started ], [ finished ] ->
            let durationValues =
                started.Body.MonotonicTimeNs,
                started.Body.MonotonicClockId,
                finished.Body.MonotonicTimeNs,
                finished.Body.MonotonicClockId

            match durationValues with
            | ResearchValue.Known startNs,
              ResearchValue.Known startClock,
              ResearchValue.Known endNs,
              ResearchValue.Known endClock
                when startClock = endClock && endNs >= startNs -> Some((endNs - startNs) / 1_000_000L)
            | _ -> None
        | _ -> None

    let private eventUtc (event: ResearchEvent) =
        match event.Body.OccurredAtUtc with
        | ResearchValue.Known text ->
            match DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) with
            | true, value -> Some value
            | _ -> None
        | ResearchValue.Unknown -> None

    let private durationMs (started: ResearchEvent) (finished: ResearchEvent) =
        match started.Body.MonotonicTimeNs, started.Body.MonotonicClockId, finished.Body.MonotonicTimeNs, finished.Body.MonotonicClockId with
        | ResearchValue.Known startNs, ResearchValue.Known startClock, ResearchValue.Known endNs, ResearchValue.Known endClock
            when started.Body.ObservationId = finished.Body.ObservationId && startClock = endClock && endNs >= startNs ->
            Some((endNs - startNs) / 1_000_000L)
        | _ ->
            match eventUtc started, eventUtc finished with
            | Some startUtc, Some endUtc when endUtc >= startUtc -> Some(int64 ((endUtc - startUtc).TotalMilliseconds))
            | _ -> None

    let private median (values: int64 list) =
        match values |> List.sort with
        | [] -> None
        | ordered ->
            let middle = ordered.Length / 2
            if ordered.Length % 2 = 1 then Some ordered[middle]
            else Some((ordered[middle - 1] + ordered[middle]) / 2L)

    let private pairedTaskMedian startType endType (events: ResearchEvent list) =
        let taskEvents (eventType: string) (sourceEvents: ResearchEvent list) =
            sourceEvents
            |> List.filter (fun event -> event.Body.EventType = eventType)
            |> List.choose (fun event -> ResearchValue.toOption event.Body.TaskId |> Option.map (fun task -> task, event))
            |> List.groupBy fst

        let starts = taskEvents startType events |> Map.ofList
        let ends = taskEvents endType events |> Map.ofList
        let values =
            starts
            |> Map.toList
            |> List.choose (fun (task, startValues) ->
                match Map.tryFind task ends, startValues with
                | Some [ _, finished ], [ _, started ] -> durationMs started finished
                | _ -> None)
        if values.Length = starts.Count && starts.Count = ends.Count && not (List.isEmpty values) then median values else None

    let private payloadInt64 name event =
        match payloadString name event with
        | Some text -> match Int64.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture) with | true, value -> Some value | _ -> None
        | None -> None

    let private allKnownSum selector (events: ResearchEvent list) =
        let selected = events |> List.choose selector
        if List.isEmpty selected || selected.Length <> events.Length then None else Some(List.sum selected)

    let private intervalDurations stateField (events: ResearchEvent list) =
        let ordered = events |> List.sortBy (fun event -> event.Sequence)
        let clocks = ordered |> List.choose (fun event -> ResearchValue.toOption event.Body.MonotonicClockId) |> Set.ofList
        if clocks.Count <> 1 || ordered |> List.exists (fun event -> event.Body.MonotonicTimeNs = ResearchValue.Unknown) then None
        else
            ordered
            |> List.pairwise
            |> List.choose (fun (left, right) ->
                match left.Body.MonotonicTimeNs, right.Body.MonotonicTimeNs, payloadString stateField left with
                | ResearchValue.Known startNs, ResearchValue.Known endNs, Some state when endNs >= startNs -> Some(state, (endNs - startNs) / 1_000_000L)
                | _ -> None)
            |> fun spans -> if spans.Length = max 0 (ordered.Length - 1) then Some spans else None

    let calculate (events: ResearchEvent list) (windowStartUtc: DateTimeOffset option) (windowEndUtc: DateTimeOffset option) =
        let evidenceClass = evidenceClassOf events
        let rows = Dictionary<string, ResearchMetricRow>(StringComparer.Ordinal)
        let missingReason = if evidenceClass = ResearchContract.Unknown && not (List.isEmpty events) then "mixed-evidence-class" else "source-missing"

        for metricId in metricIds do
            rows[metricId] <- unknown evidenceClass metricId missingReason

        let typeCount eventType = countType eventType events
        let setCount metricId eventType = setKnown metricId (typeCount eventType |> invariantInt) rows

        let complete =
            typeCount "protocol.frozen" = 1L
            && typeCount "observation.started" = 1L
            && typeCount "outcome.observed" = 1L
            && typeCount "observation.closed" = 1L
            && (events
                |> List.exists (fun event ->
                    event.Body.EventType.StartsWith("task.", StringComparison.Ordinal)
                    || event.Body.EventType.StartsWith("agent.run.", StringComparison.Ordinal)
                    || event.Body.EventType.StartsWith("autopilot.", StringComparison.Ordinal)))

        setKnown "OBS-CHAIN-COMPLETE" (if complete then "true" else "false") rows

        let sourceCount = events |> List.sumBy (fun event -> event.Body.SourceRefs.Length) |> int64
        let resolvable =
            events
            |> List.sumBy (fun event -> event.Body.SourceRefs |> List.filter (fun source -> source.Resolvable) |> List.length)
            |> int64

        setRatio "OBS-SOURCE-RESOLUTION-RATE" resolvable sourceCount rows

        let traceDenominator = events |> List.filter (fun event -> event.Body.EventType <> "observation.closed")
        let traceResolved = traceDenominator |> List.filter (fun event -> event.Body.SourceRefs |> List.exists (fun source -> source.Resolvable))
        setRatio "TRACE-EVENT-SOURCE-COVERAGE" (int64 traceResolved.Length) (int64 traceDenominator.Length) rows

        monotonicDuration "observation.started" "observation.closed" events
        |> Option.iter (fun value -> setKnown "TIME-WALL-MS" (invariantInt value) rows)

        monotonicDuration "observation.started" "outcome.observed" events
        |> Option.iter (fun value -> setKnown "TIME-TO-OUTCOME-MS" (invariantInt value) rows)

        let directCounts =
            [ "AUTO-INSTANCES", "autopilot.started"
              "FAIL-BUILD", "build.failed"
              "FAIL-TEST", "test.failed"
              "FAIL-LINT", "lint.failed"
              "FAIL-SECURITY", "security.failed"
              "FAIL-VERIFY", "verify.failed"
              "CONTEXT-COMPACTIONS", "context.compacted"
              "RUN-RESUMES", "run.resumed"
              "GIT-PROMOTIONS", "git.tree.promoted"
              "GIT-ROLLBACKS", "git.rollback.observed" ]

        for metricId, eventType in directCounts do
            setCount metricId eventType

        let setDistinctKnown metricId eventType selector =
            let selected = events |> List.filter (fun event -> event.Body.EventType = eventType)
            let values = selected |> List.choose selector
            if values.Length = selected.Length then
                setKnown metricId (values |> Set.ofList |> Set.count |> int64 |> invariantInt) rows

        [ "RUN-STARTED", "agent.run.started"; "RUN-FINISHED", "agent.run.finished" ]
        |> List.iter (fun (metricId, eventType) -> setDistinctKnown metricId eventType (fun event -> ResearchValue.toOption event.Body.RunId))

        [ "TASK-PLANNED", "task.planned"; "TASK-READY", "task.ready"; "TASK-IMPLEMENTED", "task.implemented"
          "TASK-REVIEWED", "task.reviewed"; "TASK-REJECTED", "task.rejected"; "TASK-ACCEPTED", "task.accepted" ]
        |> List.iter (fun (metricId, eventType) -> setDistinctKnown metricId eventType (fun event -> ResearchValue.toOption event.Body.TaskId))

        [ "HUMAN-INSTRUCTION", "human.instruction"; "HUMAN-REVIEW", "human.review"; "HUMAN-CORRECTION", "human.correction"
          "HUMAN-APPROVAL", "human.approval"; "HUMAN-EMERGENCY", "human.emergency"; "HUMAN-OBSERVATION", "human.observation" ]
        |> List.iter (fun (metricId, eventType) -> setDistinctKnown metricId eventType (payloadString "humanActId"))

        setKnown "WIP-SNAPSHOTS" (distinctPayload "snapshotId" events |> invariantInt) rows
        setKnown "WIP-DISTINCT-TREES" (distinctPayload "snapshotTreeId" events |> invariantInt) rows
        setKnown "REPAIR-ATTEMPTED" (distinctPayload "repairId" (events |> List.filter (fun e -> e.Body.EventType = "repair.attempted")) |> invariantInt) rows

        let fixedRepairs =
            events
            |> List.filter (fun event -> event.Body.EventType = "repair.outcome" && payloadString "outcomeClass" event = Some "fixed")
            |> distinctPayload "repairId"

        setKnown "REPAIR-FIXED" (invariantInt fixedRepairs) rows
        setRatio "REPAIR-SUCCESS-RATE" (distinctPayload "repairId" (events |> List.filter (fun e -> e.Body.EventType = "repair.outcome" && payloadString "outcomeClass" e = Some "fixed"))) (distinctPayload "repairId" (events |> List.filter (fun e -> e.Body.EventType = "repair.attempted"))) rows
        setRatio "REPAIR-ATTEMPTS-PER-FIX" (distinctPayload "repairId" (events |> List.filter (fun e -> e.Body.EventType = "repair.attempted"))) fixedRepairs rows

        setKnown "GIT-COMMITS" (distinctPayload "commitId" events |> invariantInt) rows
        setKnown "GIT-DISTINCT-TREES" (distinctPayload "commitTreeId" events |> invariantInt) rows
        setKnown "GIT-SUPERSESSIONS" (typeCount "git.supersession.observed" |> invariantInt) rows
        setKnown "ROUTING-DECISIONS" (distinctPayload "routingDecisionId" events |> invariantInt) rows
        setKnown "MODEL-SWITCHES" (typeCount "model.switched" |> invariantInt) rows
        setRatio "MODEL-SWITCHES-PER-RUN" (typeCount "model.switched") (typeCount "agent.run.started") rows
        setRatio "RUN-FINISH-RATE" (typeCount "agent.run.finished") (typeCount "agent.run.started") rows
        setRatio "TASK-READY-TO-ACCEPT-RATE" (typeCount "task.accepted") (typeCount "task.ready") rows
        setRatio "TASK-REJECTION-RATE" (distinctPayload "reviewId" (events |> List.filter (fun e -> e.Body.EventType = "task.rejected"))) (distinctPayload "reviewId" (events |> List.filter (fun e -> e.Body.EventType = "task.reviewed"))) rows
        setRatio "WIP-SNAPSHOT-PER-ACCEPTED" (typeCount "wip.snapshot.created") (typeCount "task.accepted") rows
        setRatio "ROLLBACK-PER-PROMOTION" (typeCount "git.rollback.observed") (typeCount "git.tree.promoted") rows

        let failures =
            events
            |> List.choose (fun event -> ResearchValue.toOption event.Body.FailureClass)
            |> List.filter ((<>) ResearchContract.Unknown)
            |> Set.ofList
            |> Set.count

        setKnown "FAIL-UNIQUE-CLASSES" (int64 failures |> invariantInt) rows

        let interventionStarts =
            events
            |> List.filter (fun event -> event.Body.EventType = "research.intervention.started")

        let interventionEnds =
            events
            |> List.filter (fun event -> event.Body.EventType = "research.intervention.ended")
            |> List.choose (payloadString "interventionId")
            |> Set.ofList

        let openInterventions =
            interventionStarts
            |> List.choose (payloadString "interventionId")
            |> Set.ofList
            |> Set.filter (fun interventionId -> not (Set.contains interventionId interventionEnds))
            |> Set.count

        setKnown "INT-OPEN" (int64 openInterventions |> invariantInt) rows

        let humanEvents = events |> List.filter (fun event -> event.Body.EventType.StartsWith("human.", StringComparison.Ordinal))
        let countedHumanEvents = humanEvents |> List.filter countedHuman
        setRatio "HUMAN-COUNTED-RATE" (int64 countedHumanEvents.Length) (int64 humanEvents.Length) rows

        let countedInterventionIds =
            events
            |> List.filter (fun event -> event.Body.EventType.StartsWith("research.intervention.", StringComparison.Ordinal))
            |> List.filter (fun event -> payloadBool "counted" event = Some true)
            |> List.choose (payloadString "interventionId")
            |> Set.ofList

        setKnown "INT-COUNT" (int64 countedInterventionIds.Count |> invariantInt) rows

        let i0 =
            events
            |> List.filter (fun event -> payloadString "category" event = Some "I0-observation-no-intervention")
            |> List.choose (payloadString "interventionId")
            |> Set.ofList
            |> Set.count

        setKnown "INT-I0-OBSERVATION" (int64 i0 |> invariantInt) rows
        setKnown "BLOCK-BUDGET" (typeCount "budget.blocked" |> invariantInt) rows
        setKnown "BLOCK-RATE" (typeCount "rate.blocked" |> invariantInt) rows
        setKnown "BLOCK-PROVIDER" (typeCount "provider.blocked" |> invariantInt) rows
        setKnown "BLOCK-INFRASTRUCTURE" (typeCount "infrastructure.blocked" |> invariantInt) rows
        setKnown "OUTCOME-MILESTONES" (distinctPayload "milestoneId" events |> invariantInt) rows
        setKnown "OUTCOME-TAGS" (distinctPayload "tagRef" events |> invariantInt) rows

        // Acceptance-focused metrics are populated only from complete structured fields.
        let accepted = events |> List.filter (fun event -> event.Body.EventType = "task.accepted")
        let acceptedTaskIds = accepted |> List.choose (fun event -> ResearchValue.toOption event.Body.TaskId) |> Set.ofList
        let acceptedCount = if acceptedTaskIds.Count > 0 && acceptedTaskIds.Count = accepted.Length then Some(int64 acceptedTaskIds.Count) else None
        let milestones = events |> List.filter (fun event -> event.Body.EventType = "milestone.reached")
        let reviewEvents = events |> List.filter (fun event -> event.Body.EventType = "task.reviewed")
        let implementedByTask: Map<string, (string * ResearchEvent) list> =
            events
            |> List.filter (fun event -> event.Body.EventType = "task.implemented")
            |> List.choose (fun event -> ResearchValue.toOption event.Body.TaskId |> Option.map (fun task -> task, event))
            |> List.groupBy fst
            |> Map.ofList
        let reviewsByTask =
            reviewEvents
            |> List.choose (fun event -> ResearchValue.toOption event.Body.TaskId |> Option.map (fun task -> task, event))
            |> List.groupBy fst
        let firstPass =
            reviewsByTask
            |> List.choose (fun (task, values) ->
                match Map.tryFind task implementedByTask with
                | Some [ _, implementation ] ->
                    match payloadString "implementationTreeId" implementation with
                    | Some implementationTree ->
                        values
                        |> List.sortBy (fun (_, event: ResearchEvent) -> event.Body.OccurredAtUtc, event.Body.EventId)
                        |> List.tryHead
                        |> Option.bind (fun (_, review) ->
                            match payloadString "reviewedTreeId" review, payloadString "verdict" review with
                            | Some reviewedTree, Some verdict when reviewedTree = implementationTree -> Some(verdict = "pass")
                            | _ -> None)
                    | None -> None
                | _ -> None)
        if not (List.isEmpty reviewsByTask) && firstPass.Length = reviewsByTask.Length then
            setRatio "REVIEW-FIRST-PASS-RATE" (firstPass |> List.filter id |> List.length |> int64) (int64 firstPass.Length) rows

        setKnown "REPAIR-CYCLES" (distinctPayload "repairId" (events |> List.filter (fun e -> e.Body.EventType = "repair.attempted")) |> invariantInt) rows
        let gateStarts = typeCount "gate.started"
        let gateFinishes = typeCount "gate.finished"
        let gateFailures = events |> List.filter (fun e -> e.Body.EventType = "gate.finished" && e.Body.Result = ResearchValue.Known "fail") |> List.length |> int64
        setKnown "GATE-ATTEMPTS-TOTAL" (invariantInt gateStarts) rows
        setKnown "GATE-FAILED-ATTEMPTS" (invariantInt gateFailures) rows
        setRatio "GATE-COVERAGE" gateFinishes gateStarts rows
        setRatio "GATE-PASS-COVERAGE" (gateFinishes - gateFailures) gateFinishes rows
        // These require a frozen, complete follow-up window and ancestry/task
        // attribution. Merely observing no later defect/rollback/WIP promotion is
        // not evidence of zero, so the initialized literal unknown is retained.
        let wipSnapshots = events |> List.filter (fun e -> e.Body.EventType = "wip.snapshot.created")

        let changedFiles = allKnownSum (fun event -> ResearchValue.toOption event.Body.ChangedFiles) accepted
        let linesAdded = allKnownSum (fun event -> ResearchValue.toOption event.Body.LinesAdded) accepted
        let linesDeleted = allKnownSum (fun event -> ResearchValue.toOption event.Body.LinesDeleted) accepted
        changedFiles |> Option.bind (fun value -> acceptedCount |> Option.bind (invariantRatio value)) |> Option.iter (fun value -> setKnown "FILES-PER-ACCEPTED" value rows)
        match linesAdded, linesDeleted with
        | Some added, Some deleted -> acceptedCount |> Option.bind (invariantRatio (added + deleted)) |> Option.iter (fun value -> setKnown "LINES-PER-ACCEPTED" value rows)
        | _ -> ()

        pairedTaskMedian "task.ready" "task.accepted" events
        |> Option.iter (fun value -> setKnown "TASK-READY-TO-ACCEPTED-MS" (invariantInt value) rows)

        let usageEvents = events |> List.filter (fun event -> event.Body.RequestCount <> ResearchValue.Unknown || event.Body.InputTokens <> ResearchValue.Unknown || event.Body.OutputTokens <> ResearchValue.Unknown || event.Body.CacheReadTokens <> ResearchValue.Unknown || event.Body.CacheWriteTokens <> ResearchValue.Unknown)
        let usage selector = allKnownSum selector usageEvents
        let usageTotals = [ usage (fun e -> ResearchValue.toOption e.Body.InputTokens); usage (fun e -> ResearchValue.toOption e.Body.OutputTokens); usage (fun e -> ResearchValue.toOption e.Body.CacheReadTokens); usage (fun e -> ResearchValue.toOption e.Body.CacheWriteTokens) ]
        let usageSemantics = usageEvents |> List.choose (fun event -> ResearchValue.toOption event.Body.UsageProvenance) |> Set.ofList
        let usageProviders = usageEvents |> List.choose (fun event -> ResearchValue.toOption event.Body.ProviderId) |> Set.ofList
        if not (List.isEmpty usageEvents) && usageTotals |> List.forall Option.isSome && usageSemantics.Count = 1 && usageProviders.Count = 1 && usageEvents |> List.forall (fun event -> event.Body.UsageProvenance <> ResearchValue.Unknown && event.Body.ProviderId <> ResearchValue.Unknown) then
            let total = usageTotals |> List.choose id |> List.sum
            setKnown "USE-TOKENS-TOTAL" (invariantInt total) rows
            acceptedCount |> Option.bind (invariantRatio total) |> Option.iter (fun value -> setKnown "USE-TOKENS-PER-ACCEPTED" value rows)
            match acceptedCount with
            | Some count when total > 0L ->
                let perMillion = decimal count * 1_000_000M / decimal total
                setKnown "ACCEPTED-PER-1M-TOKENS" (perMillion.ToString("0.############################", CultureInfo.InvariantCulture)) rows
            | _ -> ()

        let costEvents =
            events
            |> List.filter (fun event ->
                event.Body.CostAmount <> ResearchValue.Unknown
                || event.Body.CostCurrency <> ResearchValue.Unknown
                || event.Body.CostProvenance <> ResearchValue.Unknown
                || event.Body.RequestCount <> ResearchValue.Unknown
                || event.Body.InputTokens <> ResearchValue.Unknown
                || event.Body.OutputTokens <> ResearchValue.Unknown)
        let costs = costEvents |> List.map (fun event -> ResearchValue.toOption event.Body.CostAmount, ResearchValue.toOption event.Body.CostCurrency, ResearchValue.toOption event.Body.CostProvenance)
        if not (List.isEmpty costs) && costs |> List.forall (fun (amount, currency, provenance) -> amount.IsSome && currency.IsSome && provenance.IsSome) then
            let currencies = costs |> List.choose (fun (_, currency, _) -> currency) |> Set.ofList
            let provenances = costs |> List.choose (fun (_, _, provenance) -> provenance) |> Set.ofList
            let parsed = costs |> List.choose (fun (amount, _, _) -> amount) |> List.map (fun value -> Decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture))
            let exact = provenances |> Set.forall (fun value -> value = "provider-reported" || value = "locally-calculated")
            if currencies.Count = 1 && exact && parsed |> List.forall fst then
                let amount = parsed |> List.sumBy snd
                setKnown "USE-COST-AMOUNT" (amount.ToString("0.############################", CultureInfo.InvariantCulture)) rows
                match acceptedCount with
                | Some count ->
                    let perAccepted = amount / decimal count
                    setKnown "USE-COST-PER-ACCEPTED" (perAccepted.ToString("0.############################", CultureInfo.InvariantCulture)) rows
                | None -> ()
                if fixedRepairs > 0L then
                    let perFix = amount / decimal fixedRepairs
                    setKnown "USE-COST-PER-FIX" (perFix.ToString("0.############################", CultureInfo.InvariantCulture)) rows
            elif currencies.Count = 1 && provenances = Set.singleton "estimated" && parsed |> List.forall fst then
                let amount = parsed |> List.sumBy snd
                setKnown "USE-COST-ESTIMATED-AMOUNT" (amount.ToString("0.############################", CultureInfo.InvariantCulture)) rows

        let humanDecisionIds =
            events
            |> List.filter countedHuman
            |> List.choose (payloadString "decisionActSha256")
            |> Set.ofList
        if not (Set.isEmpty humanDecisionIds) then
            let humanDurations =
                events
                |> List.filter (fun event -> countedHuman event && Set.contains (payloadString "decisionActSha256" event |> Option.defaultValue "") humanDecisionIds)
                |> List.groupBy (fun event -> payloadString "decisionActSha256" event)
                |> List.choose (fun (_, values) -> values |> List.tryHead |> Option.bind (fun e -> ResearchValue.toOption e.Body.HumanActiveDurationMs))
            if humanDurations.Length = humanDecisionIds.Count then
                let minutes = decimal (List.sum humanDurations) / 60000M
                let minutesText = minutes.ToString("0.############################", CultureInfo.InvariantCulture)
                setKnown "HUMAN-ACTIVE-MINUTES" minutesText rows
                match acceptedCount with
                | Some count ->
                    let perAccepted = minutes / decimal count
                    setKnown "HUMAN-MINUTES-PER-ACCEPTED" (perAccepted.ToString("0.############################", CultureInfo.InvariantCulture)) rows
                | None -> ()

        let activitySpans = intervalDurations "toActivityState" (events |> List.filter (fun e -> e.Body.EventType = "activity.state.changed"))
        let modeSpans = intervalDurations "toAutonomyMode" (events |> List.filter (fun e -> e.Body.EventType = "autonomy.mode.changed"))
        match activitySpans with
        | Some spans ->
            for state, duration in spans do
                let id = "ACTIVITY-" + state.ToUpperInvariant() + "-MS"
                if rows.ContainsKey id then setKnown id (invariantInt duration) rows
        | None -> ()
        match modeSpans with
        | Some spans ->
            for mode, duration in spans do
                let id = "MODE-" + mode.ToUpperInvariant() + "-MS"
                if rows.ContainsKey id then setKnown id (invariantInt duration) rows
        | None -> ()

        match windowStartUtc, windowEndUtc with
        | Some startUtc, Some endUtc when endUtc > startUtc ->
            let days: double = (endUtc - startUtc).TotalDays
            let daysText = days.ToString("0.############################", CultureInfo.InvariantCulture)
            setKnown "WINDOW-DAYS" daysText rows

            if days > 0.0 then
                let acceptedPerDay = decimal (typeCount "task.accepted") / decimal days
                let milestonesPerDay = decimal (distinctPayload "milestoneId" events) / decimal days
                setKnown "ACCEPTED-OUTCOMES-PER-DAY" (acceptedPerDay.ToString("0.############################", CultureInfo.InvariantCulture)) rows
                setKnown "MILESTONES-PER-DAY" (milestonesPerDay.ToString("0.############################", CultureInfo.InvariantCulture)) rows
        | _ -> ()

        rows["OBS-UNKNOWN-RATE"] <- known evidenceClass "OBS-UNKNOWN-RATE" "0"
        let ordered = metricIds |> List.map (fun metricId -> rows[metricId])
        let unknownCount = ordered |> List.filter (fun row -> row.Value = ResearchContract.Unknown) |> List.length |> int64
        let unknownRate = invariantRatio unknownCount (int64 ordered.Length) |> Option.defaultValue ResearchContract.Unknown
        rows["OBS-UNKNOWN-RATE"] <- known evidenceClass "OBS-UNKNOWN-RATE" unknownRate
        metricIds |> List.map (fun metricId -> rows[metricId])

    /// Called only by an exporter after verifying the event-bound artifacts.
    let calculateWithArchitecture (events: ResearchEvent list) windowStartUtc windowEndUtc (checkpoints: BoundArchitectureCheckpoint list) =
        let baseRows = calculate events windowStartUtc windowEndUtc
        let evidence = evidenceClassOf events
        let update id value reason =
            baseRows
            |> List.map (fun row -> if row.MetricId = id then { row with Value = value; AvailabilityReason = reason; EvidenceClass = evidence } else row)
        let mutable rows = baseRows
        let set id value = rows <- update id value "observed"
        let unknown id reason = rows <- update id ResearchContract.Unknown reason
        let integer (text: string) : int64 option = match Int64.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture) with | true, value -> Some value | _ -> None
        let architectureIds = [ "ARCH-PRODUCTION-FILES"; "ARCH-MODULES-TOUCHED"; "ARCH-CROSS-MODULE"; "ARCH-REF-EDGES-ADDED"; "ARCH-REF-EDGES-REMOVED"; "ARCH-BOUNDARY-VIOLATIONS"; "ARCH-PRODUCTION-LINES"; "ARCH-TEST-LINES"; "ARCH-ANALYZER-WARNINGS"; "ARCH-TEST-COUNT"; "ARCH-TEST-GROWTH"; "ARCH-INTEGRATION-CONCENTRATION" ]

        if evidence = ResearchContract.Unknown then architectureIds |> List.iter (fun id -> unknown id "mixed-evidence-class")
        elif List.isEmpty checkpoints then architectureIds |> List.iter (fun id -> unknown id "source-missing")
        else
            let production = checkpoints |> List.collect (fun checkpoint -> checkpoint.FileRows |> List.filter (fun row -> row.FileClass = "production"))
            let tests = checkpoints |> List.collect (fun checkpoint -> checkpoint.FileRows |> List.filter (fun row -> row.FileClass = "test"))
            let sum (selector: ArchitectureFileRow -> string) (values: ArchitectureFileRow list) : int64 option =
                let parsed = values |> List.map (selector >> integer)
                if List.isEmpty parsed || parsed |> List.exists Option.isNone then None else Some(parsed |> List.choose id |> List.sum)
            set "ARCH-PRODUCTION-FILES" (production.Length |> int64 |> invariantInt)
            let components = production |> List.map (fun row -> row.Component) |> Set.ofList
            set "ARCH-MODULES-TOUCHED" (components.Count |> int64 |> invariantInt)
            set "ARCH-CROSS-MODULE" (if components.Count >= 2 then "true" else "false")
            set "ARCH-REF-EDGES-ADDED" (checkpoints |> List.sumBy (fun checkpoint -> checkpoint.DependencyRows |> List.filter (fun row -> row.Change = "added") |> List.length) |> int64 |> invariantInt)
            set "ARCH-REF-EDGES-REMOVED" (checkpoints |> List.sumBy (fun checkpoint -> checkpoint.DependencyRows |> List.filter (fun row -> row.Change = "removed") |> List.length) |> int64 |> invariantInt)
            set "ARCH-BOUNDARY-VIOLATIONS" (checkpoints |> List.collect (fun checkpoint -> checkpoint.ConfirmedFindingIds) |> Set.ofList |> Set.count |> int64 |> invariantInt)
            match sum (fun row -> row.Lines) production with | Some value -> set "ARCH-PRODUCTION-LINES" (invariantInt value) | None -> unknown "ARCH-PRODUCTION-LINES" "source-missing"
            match sum (fun row -> row.Lines) tests with | Some value -> set "ARCH-TEST-LINES" (invariantInt value) | None -> unknown "ARCH-TEST-LINES" "source-missing"
            match sum (fun row -> row.AnalyzerWarnings) (production @ tests) with | Some value -> set "ARCH-ANALYZER-WARNINGS" (invariantInt value) | None -> unknown "ARCH-ANALYZER-WARNINGS" "source-missing"
            unknown "ARCH-TEST-COUNT" "source-missing"
            unknown "ARCH-TEST-GROWTH" "source-missing"
            let deltas = production |> List.map (fun row -> integer row.LineDelta)
            let integrationComponents: Set<string> =
                Microsoft.FSharp.Collections.Set.ofList [ "CommandLoopRunner"; "CommandReportSchema"; "SessionEngine" ]
            let special = production |> List.filter (fun (row: ArchitectureFileRow) -> Set.contains row.Component integrationComponents) |> List.map (fun row -> integer row.LineDelta)
            if (deltas |> List.exists Option.isNone) || (special |> List.exists Option.isNone) then unknown "ARCH-INTEGRATION-CONCENTRATION" "source-missing"
            else
                let denominator = deltas |> List.choose id |> List.sumBy abs
                if denominator = 0L then unknown "ARCH-INTEGRATION-CONCENTRATION" "not-applicable"
                else set "ARCH-INTEGRATION-CONCENTRATION" ((decimal (special |> List.choose id |> List.sumBy abs) / decimal denominator).ToString("0.############################", CultureInfo.InvariantCulture))

        let accepted = events |> List.choose (fun event -> if event.Body.EventType = "task.accepted" then match ResearchValue.toOption event.Body.TaskId, payloadString "acceptedTreeId" event with | Some task, Some tree -> Some(task, tree) | _ -> None else None) |> Set.ofList |> Set.toList
        if List.isEmpty accepted then unknown "ARCH-ACCEPTED-CHECKPOINT-COVERAGE" "not-applicable"
        elif accepted |> List.exists (fun (task, tree) -> checkpoints |> List.filter (fun checkpoint -> checkpoint.AcceptedTaskId = task && checkpoint.AcceptedTreeId = tree && not checkpoint.GateCoupled) |> List.length <> 1) then unknown "ARCH-ACCEPTED-CHECKPOINT-COVERAGE" "source-missing"
        else set "ARCH-ACCEPTED-CHECKPOINT-COVERAGE" "1"

        let dynamicArchitectureRows =
            let production = checkpoints |> List.collect (fun checkpoint -> checkpoint.FileRows |> List.filter (fun row -> row.FileClass = "production"))
            let components = production |> List.map (fun row -> row.Component) |> Set.ofList |> Set.toList |> List.sort
            let totalLines = production |> List.map (fun row -> integer row.Lines)
            let total = if totalLines |> List.exists Option.isNone then None else Some(totalLines |> List.choose id |> List.sum)
            components
            |> List.collect (fun componentId ->
                let lines = production |> List.filter (fun row -> row.Component = componentId) |> List.map (fun row -> integer row.Lines)
                let share =
                    if lines |> List.exists Option.isNone then ResearchContract.Unknown, "source-missing"
                    else match total with | Some denominator when denominator > 0L -> (decimal (lines |> List.choose id |> List.sum) / decimal denominator).ToString("0.############################", CultureInfo.InvariantCulture), "observed" | _ -> ResearchContract.Unknown, "not-applicable"
                [ { MetricId = "ARCH-COMPONENT-SHARE:" + componentId; Value = fst share; Unit = "ratio"; AvailabilityReason = snd share; EvidenceClass = evidence }
                  { MetricId = "ARCH-COMPLEXITY:" + componentId; Value = ResearchContract.Unknown; Unit = "method"; AvailabilityReason = "source-missing"; EvidenceClass = evidence } ])
        rows @ dynamicArchitectureRows
