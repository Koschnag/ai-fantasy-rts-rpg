namespace RiftHarness

open System
open System.IO
open System.Text.Json

[<RequireQualifiedAccess>]
type ResearchCollectionResult =
    | Inactive
    | Recorded of ResearchAppendReceipt
    | GapRecorded of string

[<RequireQualifiedAccess>]
module ResearchCollector =
    let private writeImmutableDurably (path: string) (conflictCode: string) (bytes: byte array) =
        let parent = Path.GetDirectoryName(path)
        Directory.CreateDirectory(parent) |> ignore
        // A same-directory rename followed by an fsync of that directory is the
        // publication boundary.  Never expose a partially-written gap/source.
        let temporary =
            Path.Combine(parent, "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp")

        try
            use stream =
                new FileStream(
                    temporary,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    max 1 bytes.Length,
                    FileOptions.WriteThrough
                )

            stream.Write(bytes, 0, bytes.Length)
            stream.Flush(true)
            stream.Close()

            try
                File.Move(temporary, path, false)
            with :? IOException when File.Exists(path) ->
                if Internal.sha256File path <> Internal.sha256Hex bytes then
                    Internal.fail $"{conflictCode}: existing immutable receipt differs."

            ResearchDurability.fsyncDirectory parent

            if not (File.Exists(path)) || Internal.sha256File path <> Internal.sha256Hex bytes then
                Internal.fail $"{conflictCode}: immutable receipt was not durably published."
        finally
            if File.Exists(temporary) then
                File.Delete(temporary)

    let private safeCode (message: string) =
        let candidate = message.Split(':', 2)[0]
        let lowered = candidate.ToLowerInvariant()

        let secretLike =
            [ "authorization"
              "cookie"
              "password"
              "passwd"
              "secret"
              "token"
              "api_key"
              "private_key" ]
            |> List.exists lowered.Contains

        if
            not (String.IsNullOrWhiteSpace(candidate))
            && not secretLike
            && candidate.Length <= 64
            && candidate
               |> Seq.forall (fun character ->
                   Char.IsAsciiLetterOrDigit(character) || character = '_' || character = '-')
        then
            candidate
        else
            "COLLECTOR_FAILED"

    let private isGapDurabilityFailure (message: string) =
        message.Contains("RESEARCH_GAP_DURABILITY_FAILED", StringComparison.Ordinal)

    let private recordGap (root: string) (observationId: string) (eventType: string) (failureCode: string) =
        let locations = Workspace.requireInitialized root
        let gapId = ResearchRuntime.newId "GAP-"

        let directory =
            Workspace.requireSafePath
                locations
                "Research collector gap directory"
                true
                (Path.Combine(root, ".ai", "runtime", "research", "gaps", observationId))

        let bytes =
            Internal.jsonBytes false (fun (writer: Utf8JsonWriter) ->
                writer.WriteStartObject()
                writer.WriteString("attemptedEventType", eventType)
                writer.WriteString("failureClass", failureCode)
                writer.WriteString("gapId", gapId)
                writer.WriteString("observationId", observationId)
                writer.WriteString("recordedAtUtc", ResearchRuntime.nowText ())
                writer.WriteNumber("schemaVersion", ResearchContract.SchemaVersion)
                writer.WriteString("studyId", ResearchContract.StudyId)
                writer.WriteEndObject())
            |> Constants.Utf8NoBom.GetString
            |> ResearchCanonical.canonicalizeJson

        let path = Path.Combine(directory, gapId + ".json")
        writeImmutableDurably path "RESEARCH_GAP_DURABILITY_FAILED" bytes
        gapId

    /// Records a collector failure that must remain invisible to the wrapped
    /// product command. The receipt is content-addressed and contains only
    /// bounded machine codes; raw exceptions, prompts, and secret values are
    /// deliberately not retained.
    let recordHealthFailure (root: string) (hookName: string) (errorClass: string) =
        let locations = Workspace.requireInitialized root
        let safeHook = safeCode hookName
        let safeError = safeCode errorClass

        let bytes =
            Internal.jsonBytes false (fun (writer: Utf8JsonWriter) ->
                writer.WriteStartObject()
                writer.WriteString("errorClass", safeError)
                writer.WriteString("hookName", safeHook)
                writer.WriteNumber("schemaVersion", ResearchContract.SchemaVersion)
                writer.WriteString("studyId", ResearchContract.StudyId)
                writer.WriteEndObject())
            |> Constants.Utf8NoBom.GetString
            |> ResearchCanonical.canonicalizeJson

        let hash = Internal.sha256Hex bytes

        let directory =
            Workspace.requireSafePath
                locations
                "Research collector health directory"
                true
                (Path.Combine(root, ".ai", "runtime", "research", "health"))

        let path = Path.Combine(directory, "HEALTH-" + hash + ".json")
        writeImmutableDurably path "RESEARCH_HEALTH_DURABILITY_FAILED" bytes
        Workspace.relativePath locations path

    let healthIssues (root: string) =
        let locations = Workspace.requireInitialized root
        let directory = Path.Combine(locations.Root, ".ai", "runtime", "research", "health")

        if
            Directory.Exists(directory)
            && (Directory.EnumerateFiles(directory, "HEALTH-*.json", SearchOption.TopDirectoryOnly)
                |> Seq.isEmpty
                |> not)
        then
            [ "COLLECTOR_HEALTH_FAILURES_PRESENT" ]
        else
            []

    let private withActive
        (root: string)
        (eventType: string)
        (action: ResearchActivationMarker -> ResearchStudyManifest -> ResearchAppendReceipt)
        =
        try
            match ResearchActivation.tryActive root with
            | None -> ResearchCollectionResult.Inactive
            | Some marker ->
                try
                    let manifestPath = Path.Combine(Path.GetFullPath(root), marker.StudyManifestPath)
                    let manifest = ResearchExport.loadStudyManifest root manifestPath

                    if manifest.ManifestSha256 <> marker.StudyManifestSha256 then
                        Internal.fail "RESEARCH_MARKER_INVALID: study manifest hash differs."

                    action marker manifest |> ResearchCollectionResult.Recorded
                with
                | HarnessException message when isGapDurabilityFailure message ->
                    // Do not turn a failed gap publication into a second,
                    // superficially successful gap attempt.
                    reraise ()
                | HarnessException message ->
                    let gapId = recordGap root marker.ObservationId eventType (safeCode message)
                    ResearchCollectionResult.GapRecorded gapId
                | error ->
                    let gapId = recordGap root marker.ObservationId eventType (error.GetType().Name)
                    ResearchCollectionResult.GapRecorded gapId
        with
        | HarnessException message when isGapDurabilityFailure message -> reraise ()
        | HarnessException message ->
            ResearchCollectionResult.GapRecorded(recordGap root ResearchContract.Unknown eventType (safeCode message))
        | error ->
            ResearchCollectionResult.GapRecorded(
                recordGap root ResearchContract.Unknown eventType (error.GetType().Name)
            )

    let private append
        (root: string)
        (marker: ResearchActivationMarker)
        (manifest: ResearchStudyManifest)
        (eventType: string)
        (sourceRefs: ResearchSourceReference list)
        (payload: JsonElement)
        (customize: ResearchEventDraft -> ResearchEventDraft)
        =
        let identity = ResearchGitImport.currentIdentity root

        let draft =
            ResearchRuntime.createDraft manifest identity eventType sourceRefs payload
            |> customize

        ResearchLedger.append root (ResearchLedger.ledgerPath root marker.ObservationId) draft

    let recordStructured root eventType sourceRefs payload customize =
        withActive root eventType (fun marker manifest ->
            append root marker manifest eventType sourceRefs payload customize)

    let private pseudonym value =
        let digest = Internal.sha256Text (ResearchContract.StudyId + "\nactor\n" + value)
        "agent-" + digest.Substring(0, 26)

    let private persistJsonSource
        (root: string)
        (marker: ResearchActivationMarker)
        (sourceKind: string)
        (prefix: string)
        (bytes: byte array)
        =
        let locations = Workspace.requireInitialized root
        let config = HarnessConfig.load locations

        let canonical =
            bytes
            |> Constants.Utf8NoBom.GetString
            |> fun text -> Internal.canonicalJsonWithRedaction config.Redaction text
        // The decision artifact itself may be a prompt or contain free-form
        // operator text.  Bind a redacted canonical digest, never its content,
        // so the immutable research record cannot become a second prompt/secret
        // store while still detecting source mutation and replay.
        let redactedDigest = Internal.sha256Hex canonical

        let frozen =
            ResearchRuntime.payload (fun writer ->
                writer.WriteString("redactedSourceSha256", redactedDigest)
                writer.WriteNumber("schemaVersion", ResearchContract.SchemaVersion))
            |> ResearchCanonical.canonicalizeElement

        let hash = Internal.sha256Hex frozen

        let relative =
            $".ai/runtime/research/studies/{ResearchContract.StudyId}/observations/{marker.ObservationId}/sources/{prefix}-{hash}.json"

        let path =
            Workspace.requireSafePath locations "Research immutable source receipt" true (Path.Combine(root, relative))

        writeImmutableDurably path "RESEARCH_SOURCE_CONFLICT" frozen

        let source = ResearchRuntime.sourceFromFile root sourceKind relative

        if source.ArtifactSha256 <> hash then
            Internal.fail "RESEARCH_SOURCE_CONFLICT: persisted source hash differs."

        source

    let private validateReasonCode (reasonCode: string) =
        if
            String.IsNullOrWhiteSpace(reasonCode)
            || reasonCode.Length > 128
            || reasonCode
               |> Seq.exists (fun character ->
                   not (Char.IsAsciiLetterOrDigit(character))
                   && character <> '-'
                   && character <> '_'
                   && character <> '.')
        then
            Internal.fail "INTERVENTION_INVALID: reasonCode must be a bounded machine-readable code."

    let private freezeInterventionSource root marker sourceRef =
        let locations = Workspace.requireInitialized root

        let sourcePath =
            Workspace.requireSafePath locations "Research intervention source" false (Path.Combine(root, sourceRef))

        let config = HarnessConfig.load locations

        let bytes =
            Internal.safeReadAllText sourcePath config.MaxEventPayloadBytes
            |> Constants.Utf8NoBom.GetBytes

        persistJsonSource root marker "decision-receipt" "intervention" bytes

    let recordRunStarted root runId actorId =
        withActive root "agent.run.started" (fun marker manifest ->
            let started =
                RunStore.eventsStrict root runId
                |> List.head
                |> fun event -> RunStore.eventByReceipt root runId event.Sequence "run.started" event.EventHash

            if started.Sequence <> 1L || started.EventType <> "run.started" then
                Internal.fail "RESEARCH_SOURCE_INVALID: authoritative run must begin with sequence 1 run.started."

            let source =
                ResearchRuntime.harnessRunEventSource root runId started.Sequence started.EventType started.EventHash

            use runDocument = JsonDocument.Parse(started.Payload)

            let provenanceValue (name: string) : string =
                match runDocument.RootElement.TryGetProperty("provenance") with
                | true, provenance ->
                    match provenance.TryGetProperty(name) with
                    | true, value when value.ValueKind = JsonValueKind.String -> value.GetString()
                    | _ -> ResearchContract.Unknown
                | _ -> ResearchContract.Unknown

            let actor = pseudonym actorId
            let observedModel = provenanceValue "modelId"

            let payload =
                ResearchRuntime.payload (fun (writer: Utf8JsonWriter) ->
                    writer.WriteString("agentId", actor)
                    writer.WriteString("agentRole", "builder")
                    writer.WriteString("promptSha256", provenanceValue "promptSha256")
                    writer.WriteString("toolchainSha256", provenanceValue "toolchainSha256"))

            append root marker manifest "agent.run.started" [ source ] payload (fun draft ->
                { draft with
                    RunId = ResearchValue.Known runId
                    ActorRole = ResearchValue.Known "agent"
                    ActorId = ResearchValue.Known actor
                    ModelId =
                        if observedModel = ResearchContract.Unknown then
                            ResearchValue.Unknown
                        else
                            ResearchValue.Known observedModel }))

    let recordRunFinished (root: string) (runId: string) (sequence: int64) (eventHash: string) =
        withActive root "agent.run.finished" (fun marker manifest ->
            let finished = RunStore.eventByReceipt root runId sequence "run.finished" eventHash

            let source =
                ResearchRuntime.harnessRunEventSource
                    root
                    runId
                    finished.Sequence
                    finished.EventType
                    finished.EventHash

            use finishedDocument = JsonDocument.Parse(finished.Payload)

            let observedStatus =
                match finishedDocument.RootElement.TryGetProperty("status") with
                | true, value when value.ValueKind = JsonValueKind.String -> value.GetString()
                | _ -> ResearchContract.Unknown

            let payload =
                ResearchRuntime.payload (fun (writer: Utf8JsonWriter) ->
                    writer.WriteString("finishClass", observedStatus)
                    writer.WriteString("producedTreeId", ResearchContract.Unknown)

                    let summary =
                        match finishedDocument.RootElement.TryGetProperty("summary") with
                        | true, value -> value |> Internal.canonicalElement |> Internal.sha256Hex
                        | _ -> ResearchContract.Unknown

                    writer.WriteString("summarySha256", summary))

            let result =
                match observedStatus with
                | "succeeded" -> ResearchValue.Known "success"
                | "failed" -> ResearchValue.Known "fail"
                | "cancelled" -> ResearchValue.Known "cancelled"
                | _ -> ResearchValue.Unknown

            append root marker manifest "agent.run.finished" [ source ] payload (fun draft ->
                { draft with
                    RunId = ResearchValue.Known runId
                    Result = result }))

    let private passthroughEventTypes =
        Set.difference
            ResearchContract.EventTypes
            (set
                [ "protocol.frozen"
                  "observation.started"
                  "agent.run.started"
                  "agent.run.finished"
                  "gate.started"
                  "gate.finished"
                  "ledger.recovery.recorded"
                  "tool.finished"
                  "research.intervention.started"
                  "research.intervention.ended"
                  "research.intervention.recorded"
                  "outcome.observed"
                  "observation.closed" ])

    let recordHarnessEvent root runId sequence sourceEventHash eventType =
        withActive root eventType (fun marker manifest ->
            let authoritative =
                ResearchRuntime.authoritativeEvent root runId sequence eventType sourceEventHash

            if not (Set.contains authoritative.EventType passthroughEventTypes) then
                Internal.fail $"RESEARCH_EVENT_GAP: unmapped authoritative harness event '{authoritative.EventType}'."
            else
                use payloadDocument = JsonDocument.Parse(authoritative.Payload)
                let canonical = ResearchCanonical.canonicalizeElement payloadDocument.RootElement
                use canonicalDocument = JsonDocument.Parse(canonical)

                let source =
                    ResearchRuntime.harnessRunEventSource
                        root
                        runId
                        authoritative.Sequence
                        authoritative.EventType
                        authoritative.EventHash

                append
                    root
                    marker
                    manifest
                    authoritative.EventType
                    [ source ]
                    (canonicalDocument.RootElement.Clone())
                    (fun draft ->
                        { draft with
                            RunId = ResearchValue.Known runId }))

    let recordHarnessEvidence root runId sequence sourceEventHash =
        withActive root "tool.finished" (fun marker manifest ->
            let authoritative =
                ResearchRuntime.authoritativeEvent root runId sequence "evidence.recorded" sourceEventHash

            let canonical =
                ResearchCanonical.canonicalizeElement (JsonDocument.Parse(authoritative.Payload).RootElement)

            use payloadDocument = JsonDocument.Parse(canonical)

            let source =
                ResearchRuntime.harnessRunEventSource
                    root
                    runId
                    authoritative.Sequence
                    authoritative.EventType
                    authoritative.EventHash

            let requiredString (name: string) : string =
                match payloadDocument.RootElement.TryGetProperty(name) with
                | true, value when value.ValueKind = JsonValueKind.String -> value.GetString()
                | _ -> Internal.fail $"RESEARCH_SOURCE_INVALID: append-evidence payload lacks {name}."

            let criterionId = requiredString "criterionId"
            let kind = requiredString "kind"
            let resultSha256 = requiredString "resultSha256"
            let traceId = requiredString "traceId"
            let spanId = requiredString "spanId"

            let researchPayload =
                ResearchRuntime.payload (fun (writer: Utf8JsonWriter) ->
                    writer.WriteString("commandDigest", Internal.sha256Text $"append-evidence\n{criterionId}\n{kind}")
                    writer.WriteString("completedMonotonicNs", ResearchContract.Unknown)
                    writer.WriteString("criterionId", criterionId)
                    writer.WriteString("kind", kind)
                    writer.WriteString("resultSha256", resultSha256)
                    writer.WriteString("spanId", spanId)
                    writer.WriteString("startedMonotonicNs", ResearchContract.Unknown)
                    writer.WriteString("toolClass", "append-evidence")
                    writer.WriteString("traceId", traceId))

            let exitCode =
                match payloadDocument.RootElement.TryGetProperty("exitCode") with
                | true, value when value.TryGetInt64() |> fst -> ResearchValue.Known(value.GetInt64())
                | _ -> ResearchValue.Unknown

            let result =
                match exitCode with
                | ResearchValue.Known 0L -> ResearchValue.Known "success"
                | ResearchValue.Known _ -> ResearchValue.Known "fail"
                | ResearchValue.Unknown -> ResearchValue.Unknown

            append root marker manifest "tool.finished" [ source ] researchPayload (fun draft ->
                { draft with
                    RunId = ResearchValue.Known runId
                    Result = result
                    ExitCode = exitCode }))

    let private validateGateId (gateId: string) =
        if
            String.IsNullOrWhiteSpace(gateId)
            || gateId.Length > 64
            || gateId
               |> Seq.exists (fun character ->
                   not (Char.IsAsciiLetterOrDigit(character))
                   && character <> '-'
                   && character <> '_')
        then
            Internal.fail "RESEARCH_GATE_INVALID: gateId must be a bounded machine-readable identifier."

    let private payloadString (name: string) (event: ResearchEvent) =
        match event.Body.Payload.TryGetProperty(name) with
        | true, value when value.ValueKind = JsonValueKind.String -> value.GetString()
        | _ -> ResearchContract.Unknown

    let private payloadAttempt (event: ResearchEvent) =
        match event.Body.Payload.TryGetProperty("attempt") with
        | true, value when value.ValueKind = JsonValueKind.Number ->
            match value.TryGetInt64() with
            | true, attempt when attempt > 0L -> Some attempt
            | _ -> None
        | _ -> None

    let private unmatchedGateStart root observationId gateId =
        let events =
            ResearchLedger.readVerified root (ResearchLedger.ledgerPath root observationId)

        let finishes =
            events
            |> List.filter (fun event ->
                event.Body.EventType = "gate.finished" && payloadString "gateId" event = gateId)
            |> List.choose payloadAttempt
            |> Set.ofList

        events
        |> List.filter (fun event -> event.Body.EventType = "gate.started" && payloadString "gateId" event = gateId)
        |> List.choose (fun event -> payloadAttempt event |> Option.map (fun attempt -> attempt, event))
        |> List.filter (fun (attempt, _) -> not (Set.contains attempt finishes))
        |> List.tryLast
        |> Option.defaultWith (fun () ->
            Internal.fail "RESEARCH_GATE_INVALID: no unmatched authoritative gate start exists.")

    let recordVerificationStarted (root: string) (gateId: string) =
        // Keep all collector-only reads behind the active-observation guard:
        // an inactive/broken research recorder must not affect a product gate.
        withActive root "gate.started" (fun marker manifest ->
            validateGateId gateId
            let identity = ResearchGitImport.currentIdentity root

            let targetTree =
                if identity.WorktreeClean then
                    identity.HeadTreeId
                else
                    ResearchContract.Unknown

            let prior =
                ResearchLedger.readVerified root (ResearchLedger.ledgerPath root marker.ObservationId)

            let attempt =
                prior
                |> List.filter (fun event ->
                    event.Body.EventType = "gate.started" && payloadString "gateId" event = gateId)
                |> List.length
                |> int64
                |> (+) 1L

            let payload =
                ResearchRuntime.payload (fun (writer: Utf8JsonWriter) ->
                    writer.WriteNumber("attempt", attempt)
                    writer.WriteString("gateId", gateId)
                    writer.WriteString("targetTreeId", targetTree))

            let sourceBytes =
                ResearchRuntime.payload (fun writer ->
                    writer.WriteNumber("attempt", attempt)
                    writer.WriteString("gateId", gateId)
                    writer.WriteString("phase", "started")
                    writer.WriteString("targetTreeId", targetTree))
                |> ResearchCanonical.canonicalizeElement

            let source = persistJsonSource root marker "gate-log" "gate-start" sourceBytes

            append root marker manifest "gate.started" [ source ] payload (fun draft ->
                { draft with
                    RetryIndex = ResearchValue.Known(attempt - 1L) }))

    let private recordVerificationTerminal
        (root: string)
        (gateId: string)
        (valid: bool)
        (failureClass: string option)
        (reportBytes: byte array)
        =
        withActive root "gate.finished" (fun marker manifest ->
            validateGateId gateId
            let attempt, started = unmatchedGateStart root marker.ObservationId gateId
            let targetTree = payloadString "targetTreeId" started
            let source = persistJsonSource root marker "gate-log" "gate-result" reportBytes

            let payload =
                ResearchRuntime.payload (fun (writer: Utf8JsonWriter) ->
                    writer.WriteNumber("attempt", attempt)
                    writer.WriteString("evidenceSha256", source.ArtifactSha256)
                    writer.WriteString("gateId", gateId)
                    writer.WriteString("targetTreeId", targetTree))

            let result =
                if valid then
                    ResearchValue.Known "pass"
                else
                    ResearchValue.Known "fail"

            let finished =
                append root marker manifest "gate.finished" [ source ] payload (fun draft ->
                    { draft with
                        Result = result
                        RetryIndex = ResearchValue.Known(attempt - 1L) })

            match failureClass with
            | None -> finished
            | Some failure ->
                let failurePayload =
                    ResearchRuntime.payload (fun writer ->
                        writer.WriteNumber("attempt", attempt)
                        writer.WriteString("evidenceSha256", source.ArtifactSha256)
                        writer.WriteString("stageId", gateId)
                        writer.WriteString("targetTreeId", targetTree))

                append root marker manifest "verify.failed" [ source ] failurePayload (fun draft ->
                    { draft with
                        Result = ResearchValue.Known "fail"
                        FailureClass = ResearchValue.Known failure
                        RetryIndex = ResearchValue.Known(attempt - 1L) }))

    let recordVerificationFinished (root: string) (gateId: string) (valid: bool) (reportText: string) =
        let failure = if valid then None else Some "verification-invalid"
        recordVerificationTerminal root gateId valid failure (Constants.Utf8NoBom.GetBytes(reportText))

    let recordVerificationException (root: string) (gateId: string) (errorClass: string) =
        let safeError = safeCode errorClass

        let bytes =
            ResearchRuntime.payload (fun writer ->
                writer.WriteString("errorClass", safeError)
                writer.WriteString("gateId", gateId)
                writer.WriteString("result", "fail"))
            |> ResearchCanonical.canonicalizeElement

        recordVerificationTerminal root gateId false (Some safeError) bytes

    let interventionStart
        (root: string)
        (observationId: string)
        (category: string)
        (sourceRef: string)
        (reasonCode: string)
        =
        let marker =
            ResearchActivation.tryActive root
            |> Option.defaultWith (fun () -> Internal.fail "ACTIVE_OBSERVATION_MISSING: no active observation.")

        if marker.ObservationId <> observationId then
            Internal.fail "ACTIVE_OBSERVATION_CONFLICT: intervention observation differs."

        if not (Set.contains category ResearchContract.InterventionCategories) then
            Internal.fail "INTERVENTION_INVALID: unsupported category."

        validateReasonCode reasonCode

        let interventionId = ResearchRuntime.newId "INT-"
        let source = freezeInterventionSource root marker sourceRef
        let counted = category <> "I0-observation-no-intervention"

        let payload =
            ResearchRuntime.payload (fun (writer: Utf8JsonWriter) ->
                writer.WriteString("category", category)
                writer.WriteString("classificationReason", reasonCode)
                writer.WriteBoolean("counted", counted)
                writer.WriteString("decisionActSha256", source.ArtifactSha256)
                writer.WriteString("interventionId", interventionId))

        let result =
            recordStructured root "research.intervention.started" [ source ] payload (fun draft ->
                { draft with
                    ActorRole = ResearchValue.Known "human"
                    ActorId = ResearchValue.Known "human-project-lead"
                    AutonomyMode = ResearchValue.Known "human-directed"
                    ActivityState = ResearchValue.Unknown })

        interventionId, result

    let interventionEnd root observationId interventionId sourceRef =
        let marker =
            ResearchActivation.tryActive root
            |> Option.defaultWith (fun () -> Internal.fail "ACTIVE_OBSERVATION_MISSING: no active observation.")

        if marker.ObservationId <> observationId then
            Internal.fail "ACTIVE_OBSERVATION_CONFLICT: intervention observation differs."

        let ledger = ResearchLedger.ledgerPath root observationId
        let events: ResearchEvent list = ResearchLedger.readVerified root ledger

        let started =
            events
            |> List.tryFind (fun event ->
                event.Body.EventType = "research.intervention.started"
                && event.Body.Payload.GetProperty("interventionId").GetString() = interventionId)
            |> Option.defaultWith (fun () -> Internal.fail "INTERVENTION_INVALID: start event is missing.")

        if
            events
            |> List.exists (fun event ->
                event.Body.EventType = "research.intervention.ended"
                && event.Body.Payload.GetProperty("interventionId").GetString() = interventionId)
        then
            Internal.fail "INTERVENTION_INVALID: intervention is already closed."

        let nowNs, clockId = ResearchRuntime.monotonicNow ()

        let duration =
            match started.Body.MonotonicTimeNs, started.Body.MonotonicClockId, nowNs, clockId with
            | ResearchValue.Known first,
              ResearchValue.Known firstClock,
              ResearchValue.Known last,
              ResearchValue.Known lastClock when firstClock = lastClock && last >= first ->
                ResearchValue.Known((last - first) / 1_000_000L)
            | _ -> ResearchValue.Unknown

        let source = freezeInterventionSource root marker sourceRef

        let payload =
            ResearchRuntime.payload (fun (writer: Utf8JsonWriter) ->
                writer.WritePropertyName("durationMs")

                match duration with
                | ResearchValue.Known value -> writer.WriteNumberValue(value)
                | ResearchValue.Unknown -> writer.WriteStringValue(ResearchContract.Unknown)

                writer.WriteString("interventionId", interventionId))

        recordStructured root "research.intervention.ended" [ source ] payload (fun draft ->
            { draft with
                ActorRole = ResearchValue.Known "human"
                ActorId = ResearchValue.Known "human-project-lead"
                AutonomyMode = ResearchValue.Known "human-directed"
                ActivityState = ResearchValue.Unknown
                MonotonicTimeNs = nowNs
                MonotonicClockId = clockId
                HumanActiveDurationMs = duration })

    let interventionRecord
        (root: string)
        (observationId: string)
        (category: string)
        (sourceRef: string)
        (reasonCode: string)
        =
        let marker =
            ResearchActivation.tryActive root
            |> Option.defaultWith (fun () -> Internal.fail "ACTIVE_OBSERVATION_MISSING: no active observation.")

        if marker.ObservationId <> observationId then
            Internal.fail "ACTIVE_OBSERVATION_CONFLICT: intervention observation differs."

        if not (Set.contains category ResearchContract.InterventionCategories) then
            Internal.fail "INTERVENTION_INVALID: unsupported category."

        validateReasonCode reasonCode

        let interventionId = ResearchRuntime.newId "INT-"
        let source = freezeInterventionSource root marker sourceRef
        let counted = category <> "I0-observation-no-intervention"

        let payload =
            ResearchRuntime.payload (fun (writer: Utf8JsonWriter) ->
                writer.WriteString("category", category)
                writer.WriteString("classificationReason", reasonCode)
                writer.WriteBoolean("counted", counted)
                writer.WriteString("decisionActSha256", source.ArtifactSha256)
                writer.WriteString("durationMs", ResearchContract.Unknown)
                writer.WriteString("interventionId", interventionId))

        let result =
            recordStructured root "research.intervention.recorded" [ source ] payload (fun draft ->
                { draft with
                    ActorRole = ResearchValue.Known "human"
                    ActorId = ResearchValue.Known "human-project-lead"
                    AutonomyMode = ResearchValue.Known "human-directed"
                    ActivityState = ResearchValue.Unknown })

        interventionId, result
