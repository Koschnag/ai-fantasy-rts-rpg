namespace RiftHarness

open System.Text.Json

[<RequireQualifiedAccess>]
type ResearchValue<'T> =
    | Known of 'T
    | Unknown

[<RequireQualifiedAccess>]
module ResearchValue =
    let map mapping value =
        match value with
        | ResearchValue.Known known -> ResearchValue.Known(mapping known)
        | ResearchValue.Unknown -> ResearchValue.Unknown

    let toOption value =
        match value with
        | ResearchValue.Known known -> Some known
        | ResearchValue.Unknown -> None

type ResearchSourceReference =
    { SourceKind: string
      RepositoryCommit: ResearchValue<string>
      RepositoryPath: ResearchValue<string>
      LineStart: ResearchValue<int64>
      LineEnd: ResearchValue<int64>
      ArtifactSha256: string
      SourceEventId: ResearchValue<string>
      Resolvable: bool }

/// All non-chain fields of the flat observability event envelope.
type ResearchEventBody =
    { SchemaVersion: int
      EventId: string
      StudyId: string
      ObservationId: string
      RunId: ResearchValue<string>
      ParentRunId: ResearchValue<string>
      CycleId: ResearchValue<string>
      TaskId: ResearchValue<string>
      MonotonicTimeNs: ResearchValue<int64>
      MonotonicClockId: ResearchValue<string>
      OccurredAtUtc: ResearchValue<string>
      RecordedAtUtc: string
      EvidenceClass: string
      EventType: string
      ActorRole: ResearchValue<string>
      ActorId: ResearchValue<string>
      ProviderId: ResearchValue<string>
      ModelId: ResearchValue<string>
      ModelVersion: ResearchValue<string>
      BranchRef: ResearchValue<string>
      BaseCommit: ResearchValue<string>
      HeadCommit: ResearchValue<string>
      TreeId: ResearchValue<string>
      AutonomyMode: ResearchValue<string>
      ActivityState: ResearchValue<string>
      Result: ResearchValue<string>
      ExitCode: ResearchValue<int64>
      FailureClass: ResearchValue<string>
      RetryIndex: ResearchValue<int64>
      RepairIndex: ResearchValue<int64>
      UsageProvenance: ResearchValue<string>
      CostProvenance: ResearchValue<string>
      RequestCount: ResearchValue<int64>
      InputTokens: ResearchValue<int64>
      OutputTokens: ResearchValue<int64>
      CacheReadTokens: ResearchValue<int64>
      CacheWriteTokens: ResearchValue<int64>
      CostAmount: ResearchValue<string>
      CostCurrency: ResearchValue<string>
      ChangedFiles: ResearchValue<int64>
      ChangedPaths: ResearchValue<string list>
      LinesAdded: ResearchValue<int64>
      LinesDeleted: ResearchValue<int64>
      BinaryFilesChanged: ResearchValue<int64>
      PrivacyClass: ResearchValue<string>
      RedactionStatus: ResearchValue<string>
      RedactionPolicyVersion: ResearchValue<string>
      HumanActiveDurationMs: ResearchValue<int64>
      SourceRefs: ResearchSourceReference list
      Payload: JsonElement
      SupersedesEventId: ResearchValue<string> }

type ResearchEventDraft = ResearchEventBody

type ResearchEvent =
    { Body: ResearchEventBody
      Sequence: int64
      PreviousEventHash: ResearchValue<string>
      EventHash: string }

type ResearchAppendReceipt =
    { ObservationId: string
      EventId: string
      Sequence: int64
      EventHash: string
      LedgerSha256: string }

[<RequireQualifiedAccess>]
type ResearchLedgerStatus =
    | Valid
    | Invalid
    | TornTail

type ResearchLedgerVerification =
    { Status: ResearchLedgerStatus
      Errors: string list
      Events: ResearchEvent list
      OriginalSha256: string option
      VerifiedPrefixSha256: string
      VerifiedPrefixLength: int64
      TornTailSha256: string option }

[<RequireQualifiedAccess>]
module ResearchContract =
    [<Literal>]
    let SchemaVersion = 1

    [<Literal>]
    let StudyId = "riftward-research-observability"

    [<Literal>]
    let Unknown = "unknown"

    let EvidenceClasses =
        set [ "retrospective-derived"; "prospective-observed"; "synthetic-test-only" ]

    let ActorRoles = set [ "agent"; "human"; "tool"; "reviewer"; "automation" ]

    let AutonomyModes = set [ "autonomous"; "human-directed" ]

    let ActivityStates =
        set [ "agent-active"; "idle"; "sleeping"; "blocked"; "offline" ]

    let Results =
        set [ "success"; "pass"; "fail"; "blocked"; "cancelled"; "rejected"; "accepted" ]

    let UsageProvenance =
        set [ "provider-receipt"; "gateway-receipt"; "local-measurement" ]

    let CostProvenance = set [ "provider-reported"; "locally-calculated"; "estimated" ]

    let PrivacyClasses = set [ "public"; "internal"; "restricted" ]

    let RedactionStatuses = set [ "not-required"; "applied"; "blocked" ]

    let InterventionCategories =
        set
            [ "I0-observation-no-intervention"
              "I1-clarification"
              "I2-scope-criteria-change"
              "I3-technical-direction"
              "I4-domain-decision"
              "I5-priority-change"
              "I6-defect-report"
              "I7-technical-unblock"
              "I8-infrastructure"
              "I9-review-promotion"
              "I10-emergency-stop"
              "I11-other" ]

    let SourceKinds =
        set
            [ "git-blob"
              "git-commit"
              "harness-event"
              "harness-evidence"
              "autopilot-event"
              "agent-event"
              "task-manifest"
              "gate-log"
              "review-receipt"
              "decision-receipt"
              "provider-receipt"
              "infrastructure-receipt"
              "fixture" ]

    let EventTypes =
        set
            [ "protocol.frozen"
              "observation.started"
              "autopilot.started"
              "autopilot.paused"
              "autopilot.resumed"
              "autopilot.stopped"
              "agent.run.started"
              "agent.run.finished"
              "task.planned"
              "task.ready"
              "task.implemented"
              "task.reviewed"
              "task.rejected"
              "task.accepted"
              "wip.snapshot.created"
              "autonomy.mode.changed"
              "activity.state.changed"
              "gate.started"
              "gate.finished"
              "build.failed"
              "test.failed"
              "lint.failed"
              "security.failed"
              "verify.failed"
              "repair.attempted"
              "repair.outcome"
              "ledger.recovery.recorded"
              "context.compacted"
              "run.resumed"
              "routing.decided"
              "model.switched"
              "budget.blocked"
              "rate.blocked"
              "provider.blocked"
              "infrastructure.blocked"
              "block.resolved"
              "revision.observed"
              "git.commit.observed"
              "git.tree.promoted"
              "git.rollback.observed"
              "git.supersession.observed"
              "architecture.checkpoint.created"
              "milestone.reached"
              "git.tag.observed"
              "defect.observed"
              "tool.finished"
              "review.observed"
              "research.intervention.started"
              "research.intervention.ended"
              "research.intervention.recorded"
              "human.instruction"
              "human.review"
              "human.correction"
              "human.approval"
              "human.emergency"
              "human.observation"
              "outcome.observed"
              "observation.closed" ]

    let RequiredPayloadFields =
        [ "protocol.frozen", [ "protocolId"; "protocolVersion"; "protocolBundleSha256"; "freezeAtUtc" ]
          "observation.started",
          [ "targetTaskId"
            "baselineCommit"
            "collectorVersion"
            "nonInterferenceSnapshotSha256"
            "activationGuardSha256" ]
          "autopilot.started", [ "autopilotInstanceId"; "triggerClass"; "policySha256" ]
          "autopilot.paused", [ "autopilotInstanceId"; "reasonCode" ]
          "autopilot.resumed", [ "autopilotInstanceId"; "pausedDurationNs" ]
          "autopilot.stopped", [ "autopilotInstanceId"; "stopClass" ]
          "agent.run.started", [ "agentId"; "agentRole"; "promptSha256"; "toolchainSha256" ]
          "agent.run.finished", [ "finishClass"; "producedTreeId"; "summarySha256" ]
          "task.planned", [ "taskManifestSha256"; "authorityClass" ]
          "task.ready", [ "taskManifestSha256"; "authorityClass" ]
          "task.implemented", [ "taskManifestSha256"; "implementationTreeId" ]
          "task.reviewed", [ "reviewId"; "verdict"; "reviewedTreeId" ]
          "task.rejected", [ "reviewId"; "reasonCode"; "rejectedTreeId" ]
          "task.accepted", [ "authorityClass"; "acceptedCommit"; "acceptedTreeId" ]
          "wip.snapshot.created", [ "snapshotId"; "snapshotCommit"; "snapshotTreeId"; "continuityOnly" ]
          "autonomy.mode.changed", [ "fromAutonomyMode"; "toAutonomyMode"; "reasonCode" ]
          "activity.state.changed", [ "fromActivityState"; "toActivityState"; "reasonCode" ]
          "gate.started", [ "gateId"; "attempt"; "targetTreeId" ]
          "gate.finished", [ "gateId"; "attempt"; "targetTreeId"; "evidenceSha256" ]
          "build.failed", [ "stageId"; "attempt"; "targetTreeId"; "evidenceSha256" ]
          "test.failed", [ "stageId"; "attempt"; "targetTreeId"; "evidenceSha256" ]
          "lint.failed", [ "stageId"; "attempt"; "targetTreeId"; "evidenceSha256" ]
          "security.failed", [ "stageId"; "attempt"; "targetTreeId"; "evidenceSha256" ]
          "verify.failed", [ "stageId"; "attempt"; "targetTreeId"; "evidenceSha256" ]
          "repair.attempted", [ "repairId"; "triggerEventId"; "targetFindingId"; "beforeTreeId" ]
          "repair.outcome", [ "repairId"; "afterTreeId"; "outcomeClass"; "verificationEventId" ]
          "ledger.recovery.recorded",
          [ "originalLedgerSha256"
            "verifiedPrefixSha256"
            "tornTailSha256"
            "recoveredLedgerPath" ]
          "context.compacted", [ "compactionId"; "beforeContextSha256"; "summarySha256" ]
          "run.resumed", [ "resumedRunId"; "resumeFromEventId"; "resumeStateSha256" ]
          "routing.decided", [ "routingDecisionId"; "fromTier"; "toTier"; "reasonCode"; "policySha256" ]
          "model.switched", [ "fromModelId"; "toModelId"; "routingDecisionId"; "reasonCode" ]
          "budget.blocked", [ "blockId"; "budgetClass"; "observedLimit"; "receiptSha256" ]
          "rate.blocked", [ "blockId"; "rateClass"; "retryAfter"; "receiptSha256" ]
          "provider.blocked", [ "blockId"; "providerClass"; "reasonCode"; "receiptSha256" ]
          "infrastructure.blocked", [ "blockId"; "resourceClass"; "reasonCode"; "evidenceSha256" ]
          "block.resolved", [ "blockId"; "resolutionClass"; "resumedEventId" ]
          "revision.observed",
          [ "baseCommit"
            "resultCommit"
            "resultTreeId"
            "changedFiles"
            "changedPaths"
            "linesAdded"
            "linesDeleted" ]
          "git.commit.observed", [ "commitId"; "parentCommitIds"; "commitTreeId"; "commitTimeUtc" ]
          "git.tree.promoted", [ "fromRef"; "toRef"; "promotedCommit"; "promotedTreeId"; "authorityClass" ]
          "git.rollback.observed", [ "rollbackCommit"; "fromTreeId"; "toTreeId"; "reasonCode" ]
          "git.supersession.observed", [ "supersededCommit"; "supersedingCommit"; "reasonCode" ]
          "architecture.checkpoint.created",
          [ "checkpointId"
            "pathMapVersion"
            "fileInventorySha256"
            "dependencyInventorySha256"
            "analyzerInventorySha256"
            "testInventorySha256"
            "acceptedTaskId"
            "acceptedTreeId"
            "gateCoupled" ]
          "milestone.reached", [ "milestoneId"; "authorityClass"; "milestoneTreeId" ]
          "git.tag.observed", [ "tagRef"; "tagObjectId"; "targetCommit"; "targetTreeId"; "tagClass" ]
          "defect.observed",
          [ "defectId"
            "discoveredAtUtc"
            "affectedCommit"
            "affectedTreeId"
            "discoveryPhase"
            "severity" ]
          "tool.finished",
          [ "toolClass"
            "commandDigest"
            "startedMonotonicNs"
            "completedMonotonicNs"
            "resultSha256" ]
          "review.observed", [ "reviewId"; "verdict"; "findings"; "targetTreeId" ]
          "research.intervention.started",
          [ "interventionId"
            "category"
            "decisionActSha256"
            "counted"
            "classificationReason" ]
          "research.intervention.ended", [ "interventionId"; "durationMs" ]
          "research.intervention.recorded",
          [ "interventionId"
            "category"
            "decisionActSha256"
            "counted"
            "classificationReason"
            "durationMs" ]
          "human.instruction", [ "humanActId"; "decisionActSha256"; "interventionCategory"; "counted" ]
          "human.review",
          [ "humanActId"
            "reviewId"
            "decisionActSha256"
            "interventionCategory"
            "counted" ]
          "human.correction",
          [ "humanActId"
            "targetFindingId"
            "decisionActSha256"
            "interventionCategory"
            "counted" ]
          "human.approval",
          [ "humanActId"
            "authorityClass"
            "decisionActSha256"
            "interventionCategory"
            "counted" ]
          "human.emergency",
          [ "humanActId"
            "emergencyClass"
            "decisionActSha256"
            "interventionCategory"
            "counted" ]
          "human.observation",
          [ "humanActId"
            "observationClass"
            "decisionActSha256"
            "interventionCategory"
            "counted" ]
          "outcome.observed",
          [ "taskOutcome"
            "hypothesisResult"
            "resultCommit"
            "resultTreeId"
            "reasonCode" ]
          "observation.closed", [ "eventCount"; "sourceManifestSha256"; "outcomeEventId"; "closedAtUtc" ] ]
        |> List.map (fun (eventType, fields) -> eventType, Set.ofList fields)
        |> Map.ofList

[<RequireQualifiedAccess>]
module ResearchEventDraft =
    let create
        (eventId: string)
        (observationId: string)
        (evidenceClass: string)
        (eventType: string)
        (recordedAtUtc: string)
        (sourceRefs: ResearchSourceReference list)
        (payload: JsonElement)
        : ResearchEventDraft =
        { SchemaVersion = ResearchContract.SchemaVersion
          EventId = eventId
          StudyId = ResearchContract.StudyId
          ObservationId = observationId
          RunId = ResearchValue.Unknown
          ParentRunId = ResearchValue.Unknown
          CycleId = ResearchValue.Unknown
          TaskId = ResearchValue.Unknown
          MonotonicTimeNs = ResearchValue.Unknown
          MonotonicClockId = ResearchValue.Unknown
          OccurredAtUtc = ResearchValue.Unknown
          RecordedAtUtc = recordedAtUtc
          EvidenceClass = evidenceClass
          EventType = eventType
          ActorRole = ResearchValue.Unknown
          ActorId = ResearchValue.Unknown
          ProviderId = ResearchValue.Unknown
          ModelId = ResearchValue.Unknown
          ModelVersion = ResearchValue.Unknown
          BranchRef = ResearchValue.Unknown
          BaseCommit = ResearchValue.Unknown
          HeadCommit = ResearchValue.Unknown
          TreeId = ResearchValue.Unknown
          AutonomyMode = ResearchValue.Unknown
          ActivityState = ResearchValue.Unknown
          Result = ResearchValue.Unknown
          ExitCode = ResearchValue.Unknown
          FailureClass = ResearchValue.Unknown
          RetryIndex = ResearchValue.Unknown
          RepairIndex = ResearchValue.Unknown
          UsageProvenance = ResearchValue.Unknown
          CostProvenance = ResearchValue.Unknown
          RequestCount = ResearchValue.Unknown
          InputTokens = ResearchValue.Unknown
          OutputTokens = ResearchValue.Unknown
          CacheReadTokens = ResearchValue.Unknown
          CacheWriteTokens = ResearchValue.Unknown
          CostAmount = ResearchValue.Unknown
          CostCurrency = ResearchValue.Unknown
          ChangedFiles = ResearchValue.Unknown
          ChangedPaths = ResearchValue.Unknown
          LinesAdded = ResearchValue.Unknown
          LinesDeleted = ResearchValue.Unknown
          BinaryFilesChanged = ResearchValue.Unknown
          PrivacyClass = ResearchValue.Known "internal"
          RedactionStatus = ResearchValue.Known "not-required"
          RedactionPolicyVersion = ResearchValue.Unknown
          HumanActiveDurationMs = ResearchValue.Unknown
          SourceRefs = sourceRefs
          Payload = payload.Clone()
          SupersedesEventId = ResearchValue.Unknown }
