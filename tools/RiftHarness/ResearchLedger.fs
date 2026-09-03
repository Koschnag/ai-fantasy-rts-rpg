namespace RiftHarness

open System
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Text.Json
open System.Text.RegularExpressions

[<RequireQualifiedAccess>]
module ResearchLedger =
    let private eventIdPattern =
        Regex("^EV-[A-Z0-9]{26}$", RegexOptions.CultureInvariant ||| RegexOptions.NonBacktracking)

    let private observationIdPattern =
        Regex("^OBS-[A-Z0-9]{26}$", RegexOptions.CultureInvariant ||| RegexOptions.NonBacktracking)

    let private taskIdPattern =
        Regex("^T-[0-9]{3,}$", RegexOptions.CultureInvariant ||| RegexOptions.NonBacktracking)

    let private timestampPattern =
        Regex(
            "^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}\\.[0-9]{3}Z$",
            RegexOptions.CultureInvariant ||| RegexOptions.NonBacktracking
        )

    let private hashOrUnknown value =
        value = ResearchContract.Unknown || Internal.isSha256 value

    let private fail code message = Internal.fail $"{code}: {message}"

    let private exactFields (section: string) (expected: Set<string>) (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then
            fail "RESEARCH_SCHEMA_INVALID" $"{section} must be an object."

        let names = HashSet<string>(StringComparer.Ordinal)

        for property in element.EnumerateObject() do
            if not (names.Add(property.Name)) then
                fail "RESEARCH_SCHEMA_INVALID" $"{section} contains duplicate field '{property.Name}'."

        let actual = Set.ofSeq names

        if actual <> expected then
            let missing = Set.difference expected actual |> String.concat ","
            let extra = Set.difference actual expected |> String.concat ","
            fail "RESEARCH_SCHEMA_INVALID" $"{section} fields differ (missing=[{missing}], extra=[{extra}])."

    let private eventFields =
        set
            [ "schemaVersion"
              "eventId"
              "studyId"
              "observationId"
              "runId"
              "parentRunId"
              "cycleId"
              "taskId"
              "sequence"
              "previousEventHash"
              "monotonicTimeNs"
              "monotonicClockId"
              "occurredAtUtc"
              "recordedAtUtc"
              "evidenceClass"
              "eventType"
              "actorRole"
              "actorId"
              "providerId"
              "modelId"
              "modelVersion"
              "branchRef"
              "baseCommit"
              "headCommit"
              "treeId"
              "autonomyMode"
              "activityState"
              "result"
              "exitCode"
              "failureClass"
              "retryIndex"
              "repairIndex"
              "usageProvenance"
              "costProvenance"
              "requestCount"
              "inputTokens"
              "outputTokens"
              "cacheReadTokens"
              "cacheWriteTokens"
              "costAmount"
              "costCurrency"
              "changedFiles"
              "changedPaths"
              "linesAdded"
              "linesDeleted"
              "binaryFilesChanged"
              "privacyClass"
              "redactionStatus"
              "redactionPolicyVersion"
              "humanActiveDurationMs"
              "sourceRefs"
              "payload"
              "supersedesEventId"
              "eventHash" ]

    let private sourceFields =
        set
            [ "sourceKind"
              "repositoryCommit"
              "repositoryPath"
              "lineStart"
              "lineEnd"
              "artifactSha256"
              "sourceEventId"
              "resolvable" ]

    let private writeStringValue (writer: Utf8JsonWriter) (name: string) (value: ResearchValue<string>) =
        writer.WritePropertyName(name)

        match value with
        | ResearchValue.Known known -> writer.WriteStringValue(known)
        | ResearchValue.Unknown -> writer.WriteStringValue(ResearchContract.Unknown)

    let private writeIntValue (writer: Utf8JsonWriter) (name: string) (value: ResearchValue<int64>) =
        writer.WritePropertyName(name)

        match value with
        | ResearchValue.Known known -> writer.WriteNumberValue(known)
        | ResearchValue.Unknown -> writer.WriteStringValue(ResearchContract.Unknown)

    let private writeStringListValue (writer: Utf8JsonWriter) (name: string) (value: ResearchValue<string list>) =
        writer.WritePropertyName(name)

        match value with
        | ResearchValue.Known known ->
            writer.WriteStartArray()
            known |> List.iter (fun item -> writer.WriteStringValue(item: string))
            writer.WriteEndArray()
        | ResearchValue.Unknown -> writer.WriteStringValue(ResearchContract.Unknown)

    let private writeSourceRef (writer: Utf8JsonWriter) (source: ResearchSourceReference) =
        writer.WriteStartObject()
        writer.WriteString("sourceKind", source.SourceKind)
        writeStringValue writer "repositoryCommit" source.RepositoryCommit
        writeStringValue writer "repositoryPath" source.RepositoryPath
        writeIntValue writer "lineStart" source.LineStart
        writeIntValue writer "lineEnd" source.LineEnd
        writer.WriteString("artifactSha256", source.ArtifactSha256)
        writeStringValue writer "sourceEventId" source.SourceEventId
        writer.WriteBoolean("resolvable", source.Resolvable)
        writer.WriteEndObject()

    let private eventBytes (includeHash: bool) (event: ResearchEvent) =
        let raw =
            Internal.jsonBytes false (fun writer ->
                let body = event.Body
                writer.WriteStartObject()
                writer.WriteNumber("schemaVersion", body.SchemaVersion)
                writer.WriteString("eventId", body.EventId)
                writer.WriteString("studyId", body.StudyId)
                writer.WriteString("observationId", body.ObservationId)
                writeStringValue writer "runId" body.RunId
                writeStringValue writer "parentRunId" body.ParentRunId
                writeStringValue writer "cycleId" body.CycleId
                writeStringValue writer "taskId" body.TaskId
                writer.WriteNumber("sequence", event.Sequence)
                writeStringValue writer "previousEventHash" event.PreviousEventHash
                writeIntValue writer "monotonicTimeNs" body.MonotonicTimeNs
                writeStringValue writer "monotonicClockId" body.MonotonicClockId
                writeStringValue writer "occurredAtUtc" body.OccurredAtUtc
                writer.WriteString("recordedAtUtc", body.RecordedAtUtc)
                writer.WriteString("evidenceClass", body.EvidenceClass)
                writer.WriteString("eventType", body.EventType)
                writeStringValue writer "actorRole" body.ActorRole
                writeStringValue writer "actorId" body.ActorId
                writeStringValue writer "providerId" body.ProviderId
                writeStringValue writer "modelId" body.ModelId
                writeStringValue writer "modelVersion" body.ModelVersion
                writeStringValue writer "branchRef" body.BranchRef
                writeStringValue writer "baseCommit" body.BaseCommit
                writeStringValue writer "headCommit" body.HeadCommit
                writeStringValue writer "treeId" body.TreeId
                writeStringValue writer "autonomyMode" body.AutonomyMode
                writeStringValue writer "activityState" body.ActivityState
                writeStringValue writer "result" body.Result
                writeIntValue writer "exitCode" body.ExitCode
                writeStringValue writer "failureClass" body.FailureClass
                writeIntValue writer "retryIndex" body.RetryIndex
                writeIntValue writer "repairIndex" body.RepairIndex
                writeStringValue writer "usageProvenance" body.UsageProvenance
                writeStringValue writer "costProvenance" body.CostProvenance
                writeIntValue writer "requestCount" body.RequestCount
                writeIntValue writer "inputTokens" body.InputTokens
                writeIntValue writer "outputTokens" body.OutputTokens
                writeIntValue writer "cacheReadTokens" body.CacheReadTokens
                writeIntValue writer "cacheWriteTokens" body.CacheWriteTokens
                writeStringValue writer "costAmount" body.CostAmount
                writeStringValue writer "costCurrency" body.CostCurrency
                writeIntValue writer "changedFiles" body.ChangedFiles
                writeStringListValue writer "changedPaths" body.ChangedPaths
                writeIntValue writer "linesAdded" body.LinesAdded
                writeIntValue writer "linesDeleted" body.LinesDeleted
                writeIntValue writer "binaryFilesChanged" body.BinaryFilesChanged
                writeStringValue writer "privacyClass" body.PrivacyClass
                writeStringValue writer "redactionStatus" body.RedactionStatus
                writeStringValue writer "redactionPolicyVersion" body.RedactionPolicyVersion
                writeIntValue writer "humanActiveDurationMs" body.HumanActiveDurationMs
                writer.WritePropertyName("sourceRefs")
                writer.WriteStartArray()
                body.SourceRefs |> List.iter (writeSourceRef writer)
                writer.WriteEndArray()
                writer.WritePropertyName("payload")
                writer.WriteRawValue(body.Payload.GetRawText(), true)
                writeStringValue writer "supersedesEventId" body.SupersedesEventId

                if includeHash then
                    writer.WriteString("eventHash", event.EventHash)

                writer.WriteEndObject())

        use document = JsonDocument.Parse(raw)
        ResearchCanonical.canonicalizeElement document.RootElement

    let canonicalEventBytes (event: ResearchEvent) = eventBytes true event

    let private computeHash (event: ResearchEvent) =
        eventBytes false event |> Internal.sha256Hex

    let private requireProperty (name: string) (element: JsonElement) =
        match element.TryGetProperty(name) with
        | true, value -> value
        | _ -> fail "RESEARCH_SCHEMA_INVALID" $"Missing field '{name}'."

    let private requireString (name: string) (element: JsonElement) =
        let value = requireProperty name element

        if value.ValueKind <> JsonValueKind.String then
            fail "RESEARCH_SCHEMA_INVALID" $"Field '{name}' must be a string or literal 'unknown'."

        value.GetString()

    let private parseStringValue (name: string) (element: JsonElement) : ResearchValue<string> =
        match requireString name element with
        | value when value = ResearchContract.Unknown -> ResearchValue.Unknown
        | value -> ResearchValue.Known value

    let private parseIntValue (name: string) (element: JsonElement) : ResearchValue<int64> =
        let value = requireProperty name element

        if
            value.ValueKind = JsonValueKind.String
            && value.GetString() = ResearchContract.Unknown
        then
            ResearchValue.Unknown
        else
            let mutable result = 0L

            if value.ValueKind <> JsonValueKind.Number || not (value.TryGetInt64(&result)) then
                fail "RESEARCH_SCHEMA_INVALID" $"Field '{name}' must be an integer or literal 'unknown'."

            ResearchValue.Known result

    let private parseStringListValue (name: string) (element: JsonElement) : ResearchValue<string list> =
        let value = requireProperty name element

        if
            value.ValueKind = JsonValueKind.String
            && value.GetString() = ResearchContract.Unknown
        then
            ResearchValue.Unknown
        elif value.ValueKind = JsonValueKind.Array then
            ResearchValue.Known(
                value.EnumerateArray()
                |> Seq.map (fun item ->
                    if item.ValueKind <> JsonValueKind.String then
                        fail "RESEARCH_SCHEMA_INVALID" $"Every '{name}' entry must be a string."

                    item.GetString())
                |> Seq.toList
            )
        else
            fail "RESEARCH_SCHEMA_INVALID" $"Field '{name}' must be a string array or literal 'unknown'."

    let private parseSourceRef (element: JsonElement) =
        exactFields "sourceRefs[]" sourceFields element
        let resolvable = requireProperty "resolvable" element

        if
            resolvable.ValueKind <> JsonValueKind.True
            && resolvable.ValueKind <> JsonValueKind.False
        then
            fail "RESEARCH_SCHEMA_INVALID" "sourceRefs[].resolvable must be Boolean."

        { SourceKind = requireString "sourceKind" element
          RepositoryCommit = parseStringValue "repositoryCommit" element
          RepositoryPath = parseStringValue "repositoryPath" element
          LineStart = parseIntValue "lineStart" element
          LineEnd = parseIntValue "lineEnd" element
          ArtifactSha256 = requireString "artifactSha256" element
          SourceEventId = parseStringValue "sourceEventId" element
          Resolvable = resolvable.GetBoolean() }

    let private parseEvent (line: byte array) =
        try
            use document = JsonDocument.Parse(line)
            let root = document.RootElement
            exactFields "event" eventFields root
            ResearchCanonical.canonicalizeElement root |> ignore
            let sources = requireProperty "sourceRefs" root

            if sources.ValueKind <> JsonValueKind.Array then
                fail "RESEARCH_SCHEMA_INVALID" "sourceRefs must be an array."

            let sequenceElement = requireProperty "sequence" root
            let mutable sequence = 0L

            if
                sequenceElement.ValueKind <> JsonValueKind.Number
                || not (sequenceElement.TryGetInt64(&sequence))
            then
                fail "RESEARCH_SCHEMA_INVALID" "sequence must be an integer."

            let schemaVersionElement = requireProperty "schemaVersion" root
            let mutable schemaVersion = 0

            if
                schemaVersionElement.ValueKind <> JsonValueKind.Number
                || not (schemaVersionElement.TryGetInt32(&schemaVersion))
            then
                fail "RESEARCH_SCHEMA_INVALID" "schemaVersion must be an integer."

            let body =
                { SchemaVersion = schemaVersion
                  EventId = requireString "eventId" root
                  StudyId = requireString "studyId" root
                  ObservationId = requireString "observationId" root
                  RunId = parseStringValue "runId" root
                  ParentRunId = parseStringValue "parentRunId" root
                  CycleId = parseStringValue "cycleId" root
                  TaskId = parseStringValue "taskId" root
                  MonotonicTimeNs = parseIntValue "monotonicTimeNs" root
                  MonotonicClockId = parseStringValue "monotonicClockId" root
                  OccurredAtUtc = parseStringValue "occurredAtUtc" root
                  RecordedAtUtc = requireString "recordedAtUtc" root
                  EvidenceClass = requireString "evidenceClass" root
                  EventType = requireString "eventType" root
                  ActorRole = parseStringValue "actorRole" root
                  ActorId = parseStringValue "actorId" root
                  ProviderId = parseStringValue "providerId" root
                  ModelId = parseStringValue "modelId" root
                  ModelVersion = parseStringValue "modelVersion" root
                  BranchRef = parseStringValue "branchRef" root
                  BaseCommit = parseStringValue "baseCommit" root
                  HeadCommit = parseStringValue "headCommit" root
                  TreeId = parseStringValue "treeId" root
                  AutonomyMode = parseStringValue "autonomyMode" root
                  ActivityState = parseStringValue "activityState" root
                  Result = parseStringValue "result" root
                  ExitCode = parseIntValue "exitCode" root
                  FailureClass = parseStringValue "failureClass" root
                  RetryIndex = parseIntValue "retryIndex" root
                  RepairIndex = parseIntValue "repairIndex" root
                  UsageProvenance = parseStringValue "usageProvenance" root
                  CostProvenance = parseStringValue "costProvenance" root
                  RequestCount = parseIntValue "requestCount" root
                  InputTokens = parseIntValue "inputTokens" root
                  OutputTokens = parseIntValue "outputTokens" root
                  CacheReadTokens = parseIntValue "cacheReadTokens" root
                  CacheWriteTokens = parseIntValue "cacheWriteTokens" root
                  CostAmount = parseStringValue "costAmount" root
                  CostCurrency = parseStringValue "costCurrency" root
                  ChangedFiles = parseIntValue "changedFiles" root
                  ChangedPaths = parseStringListValue "changedPaths" root
                  LinesAdded = parseIntValue "linesAdded" root
                  LinesDeleted = parseIntValue "linesDeleted" root
                  BinaryFilesChanged = parseIntValue "binaryFilesChanged" root
                  PrivacyClass = parseStringValue "privacyClass" root
                  RedactionStatus = parseStringValue "redactionStatus" root
                  RedactionPolicyVersion = parseStringValue "redactionPolicyVersion" root
                  HumanActiveDurationMs = parseIntValue "humanActiveDurationMs" root
                  SourceRefs = sources.EnumerateArray() |> Seq.map parseSourceRef |> Seq.toList
                  Payload = (requireProperty "payload" root).Clone()
                  SupersedesEventId = parseStringValue "supersedesEventId" root }

            { Body = body
              Sequence = sequence
              PreviousEventHash = parseStringValue "previousEventHash" root
              EventHash = requireString "eventHash" root }
        with :? JsonException as error ->
            fail "RESEARCH_SCHEMA_INVALID" error.Message

    let private validateTimestamp (name: string) (value: string) =
        if not (timestampPattern.IsMatch(value)) then
            fail "RESEARCH_SCHEMA_INVALID" $"{name} must use yyyy-MM-ddTHH:mm:ss.fffZ."

        let mutable parsed = DateTimeOffset.MinValue

        if
            not (
                DateTimeOffset.TryParseExact(
                    value,
                    "yyyy-MM-dd'T'HH:mm:ss.fff'Z'",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal ||| DateTimeStyles.AdjustToUniversal,
                    &parsed
                )
            )
        then
            fail "RESEARCH_SCHEMA_INVALID" $"{name} is not a valid UTC timestamp."

        parsed

    let private validateNonNegative (name: string) (value: ResearchValue<int64>) =
        match value with
        | ResearchValue.Known number when number < 0L -> fail "RESEARCH_SCHEMA_INVALID" $"{name} cannot be negative."
        | _ -> ()

    let private validateEnum (name: string) (allowed: Set<string>) (value: ResearchValue<string>) =
        match value with
        | ResearchValue.Known known when not (Set.contains known allowed) ->
            fail "RESEARCH_SCHEMA_INVALID" $"Unsupported {name} '{known}'."
        | _ -> ()

    let private validateCommit (name: string) (value: ResearchValue<string>) =
        match value with
        | ResearchValue.Known known when
            not (Regex.IsMatch(known, "^[0-9a-f]{40}$|^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
            ->
            fail "RESEARCH_SCHEMA_INVALID" $"{name} must be a lowercase 40- or 64-hex object id."
        | _ -> ()

    let private validateRelativePath (name: string) (value: string) =
        if
            String.IsNullOrWhiteSpace(value)
            || Path.IsPathRooted(value)
            || value.Contains('\\')
        then
            fail "RESEARCH_SCHEMA_INVALID" $"{name} must be a non-empty repo-relative slash path."

        let segments = value.Split('/')

        if
            segments
            |> Array.exists (fun segment -> segment = "" || segment = "." || segment = "..")
        then
            fail "RESEARCH_SCHEMA_INVALID" $"{name} contains an unsafe segment."

    let private validateEventBody (body: ResearchEventBody) =
        if body.SchemaVersion <> ResearchContract.SchemaVersion then
            fail "RESEARCH_SCHEMA_INVALID" "Unsupported schemaVersion."

        if body.StudyId <> ResearchContract.StudyId then
            fail "RESEARCH_SCHEMA_INVALID" "Unsupported studyId."

        if not (eventIdPattern.IsMatch(body.EventId)) then
            fail "RESEARCH_SCHEMA_INVALID" "eventId has invalid syntax."

        if not (observationIdPattern.IsMatch(body.ObservationId)) then
            fail "RESEARCH_SCHEMA_INVALID" "observationId has invalid syntax."

        if not (Set.contains body.EvidenceClass ResearchContract.EvidenceClasses) then
            fail "EVIDENCE_CLASS_INVALID" "Unsupported evidenceClass."

        if not (Set.contains body.EventType ResearchContract.EventTypes) then
            fail "EVENT_TYPE_INVALID" $"Unsupported eventType '{body.EventType}'."

        let recorded = validateTimestamp "recordedAtUtc" body.RecordedAtUtc

        match body.OccurredAtUtc with
        | ResearchValue.Known occurred ->
            let parsed = validateTimestamp "occurredAtUtc" occurred

            if recorded < parsed then
                fail "RESEARCH_SCHEMA_INVALID" "recordedAtUtc precedes occurredAtUtc."
        | ResearchValue.Unknown -> ()

        match body.MonotonicTimeNs, body.MonotonicClockId with
        | ResearchValue.Known value, ResearchValue.Known _ when value >= 0L -> ()
        | ResearchValue.Unknown, ResearchValue.Unknown -> ()
        | _ -> fail "RESEARCH_SCHEMA_INVALID" "monotonicTimeNs and monotonicClockId must both be known or both unknown."

        match body.TaskId with
        | ResearchValue.Known taskId when not (taskIdPattern.IsMatch(taskId)) ->
            fail "RESEARCH_SCHEMA_INVALID" "taskId has invalid syntax."
        | _ -> ()

        validateEnum "actorRole" ResearchContract.ActorRoles body.ActorRole
        validateEnum "autonomyMode" ResearchContract.AutonomyModes body.AutonomyMode
        validateEnum "activityState" ResearchContract.ActivityStates body.ActivityState
        validateEnum "result" ResearchContract.Results body.Result
        validateEnum "usageProvenance" ResearchContract.UsageProvenance body.UsageProvenance
        validateEnum "costProvenance" ResearchContract.CostProvenance body.CostProvenance
        validateEnum "privacyClass" ResearchContract.PrivacyClasses body.PrivacyClass
        validateEnum "redactionStatus" ResearchContract.RedactionStatuses body.RedactionStatus

        [ "exitCode", body.ExitCode
          "retryIndex", body.RetryIndex
          "repairIndex", body.RepairIndex
          "requestCount", body.RequestCount
          "inputTokens", body.InputTokens
          "outputTokens", body.OutputTokens
          "cacheReadTokens", body.CacheReadTokens
          "cacheWriteTokens", body.CacheWriteTokens
          "changedFiles", body.ChangedFiles
          "linesAdded", body.LinesAdded
          "linesDeleted", body.LinesDeleted
          "binaryFilesChanged", body.BinaryFilesChanged
          "humanActiveDurationMs", body.HumanActiveDurationMs ]
        |> List.iter (fun (name, value) -> validateNonNegative name value)

        let usageValues =
            [ body.RequestCount
              body.InputTokens
              body.OutputTokens
              body.CacheReadTokens
              body.CacheWriteTokens ]

        if
            usageValues
            |> List.exists (function
                | ResearchValue.Known _ -> true
                | ResearchValue.Unknown -> false)
        then
            match body.UsageProvenance with
            | ResearchValue.Unknown ->
                fail "RESEARCH_SCHEMA_INVALID" "Known request/token usage requires usageProvenance."
            | ResearchValue.Known _ -> ()

        [ "baseCommit", body.BaseCommit
          "headCommit", body.HeadCommit
          "treeId", body.TreeId ]
        |> List.iter (fun (name, value) -> validateCommit name value)

        match body.ChangedPaths with
        | ResearchValue.Known paths ->
            paths |> List.iter (validateRelativePath "changedPaths[]")

            if
                paths
                <> (paths
                    |> List.distinct
                    |> List.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right)))
            then
                fail "RESEARCH_SCHEMA_INVALID" "changedPaths must be ordinal-sorted and deduplicated."

            match body.ChangedFiles with
            | ResearchValue.Known count when count <> int64 paths.Length ->
                fail "RESEARCH_SCHEMA_INVALID" "changedFiles must equal changedPaths length."
            | _ -> ()
        | ResearchValue.Unknown -> ()

        match body.CostAmount, body.CostCurrency, body.CostProvenance with
        | ResearchValue.Unknown, ResearchValue.Unknown, ResearchValue.Unknown -> ()
        | ResearchValue.Known amount, ResearchValue.Known currency, ResearchValue.Known _ ->
            let mutable cost = 0M

            if
                not (Regex.IsMatch(amount, "^(0|[1-9][0-9]*)(\\.[0-9]+)?$", RegexOptions.CultureInvariant))
                || not (Decimal.TryParse(amount, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, &cost))
                || cost < 0M
            then
                fail "RESEARCH_SCHEMA_INVALID" "costAmount must be a non-negative invariant decimal string."

            if not (Regex.IsMatch(currency, "^[A-Z]{3}$", RegexOptions.CultureInvariant)) then
                fail "RESEARCH_SCHEMA_INVALID" "costCurrency must be ISO-style uppercase code."
        | _ ->
            fail
                "RESEARCH_SCHEMA_INVALID"
                "costAmount, costCurrency, and costProvenance must all be known or all unknown."

        match body.RepairIndex with
        | ResearchValue.Unknown when body.EventType = "repair.attempted" || body.EventType = "repair.outcome" ->
            fail "RESEARCH_SCHEMA_INVALID" "Repair events require repairIndex."
        | ResearchValue.Known _ when body.EventType <> "repair.attempted" && body.EventType <> "repair.outcome" ->
            fail "RESEARCH_SCHEMA_INVALID" "repairIndex applies only to repair events."
        | _ -> ()

        if List.isEmpty body.SourceRefs then
            fail "RESEARCH_SCHEMA_INVALID" "sourceRefs cannot be empty."

        for source in body.SourceRefs do
            if not (Set.contains source.SourceKind ResearchContract.SourceKinds) then
                fail "RESEARCH_SCHEMA_INVALID" $"Unsupported sourceKind '{source.SourceKind}'."

            if not (Internal.isSha256 source.ArtifactSha256) then
                fail "RESEARCH_SCHEMA_INVALID" "sourceRefs[].artifactSha256 must be lowercase SHA-256."

            validateCommit "sourceRefs[].repositoryCommit" source.RepositoryCommit

            match source.RepositoryPath with
            | ResearchValue.Known path -> validateRelativePath "sourceRefs[].repositoryPath" path
            | _ -> ()

            validateNonNegative "sourceRefs[].lineStart" source.LineStart
            validateNonNegative "sourceRefs[].lineEnd" source.LineEnd

            match source.LineStart, source.LineEnd with
            | ResearchValue.Known first, _ when first < 1L ->
                fail "RESEARCH_SCHEMA_INVALID" "sourceRefs[].lineStart is 1-based."
            | _, ResearchValue.Known last when last < 1L ->
                fail "RESEARCH_SCHEMA_INVALID" "sourceRefs[].lineEnd is 1-based."
            | ResearchValue.Known first, ResearchValue.Known last when first > last ->
                fail "RESEARCH_SCHEMA_INVALID" "sourceRefs line range is reversed."
            | _ -> ()

            if source.Resolvable then
                let hasAddress =
                    match source.RepositoryPath, source.SourceEventId with
                    | ResearchValue.Known _, _
                    | _, ResearchValue.Known _ -> true
                    | _ -> false

                if
                    not hasAddress
                    && not (
                        source.SourceKind = "harness-event"
                        && source.RepositoryPath = ResearchValue.Unknown
                    )
                then
                    fail
                        "RESEARCH_SCHEMA_INVALID"
                        "A resolvable sourceRef requires a repositoryPath or sourceEventId address."

        if
            body.EvidenceClass <> "synthetic-test-only"
            && body.SourceRefs |> List.forall (fun source -> source.SourceKind = "fixture")
        then
            fail "EVIDENCE_CLASS_INVALID" "Non-synthetic evidence cannot rely only on fixture sourceRefs."

        if body.Payload.ValueKind <> JsonValueKind.Object then
            fail "RESEARCH_SCHEMA_INVALID" "payload must be an object."

        ResearchCanonical.canonicalizeElement body.Payload |> ignore
        let required = Map.find body.EventType ResearchContract.RequiredPayloadFields

        for field in required do
            if not (body.Payload.TryGetProperty(field) |> fst) then
                fail "RESEARCH_SCHEMA_INVALID" $"payload.{field} is required for {body.EventType}."

        let exactPayload expected =
            // Do not reduce names to a set here: JSON permits duplicate names and
            // a duplicate required field must never be silently accepted.
            exactFields $"payload for {body.EventType}" (Set.ofList expected) body.Payload

        let payloadString field =
            let value = requireProperty field body.Payload

            if value.ValueKind <> JsonValueKind.String then
                fail "RESEARCH_SCHEMA_INVALID" $"payload.{field} must be a string."

            value.GetString()

        let payloadHash field =
            let value = payloadString field

            if value <> ResearchContract.Unknown && not (Internal.isSha256 value) then
                fail "RESEARCH_SCHEMA_INVALID" $"payload.{field} must be SHA-256 or unknown."

        let payloadObjectId field =
            let value = payloadString field

            if
                value <> ResearchContract.Unknown
                && not (Regex.IsMatch(value, "^[0-9a-f]{40}$|^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
            then
                fail "RESEARCH_SCHEMA_INVALID" $"payload.{field} must be a lowercase Git object id or unknown."

        let payloadIntOrUnknown minimum field =
            let value = requireProperty field body.Payload

            if
                value.ValueKind = JsonValueKind.String
                && value.GetString() = ResearchContract.Unknown
            then
                ()
            else
                let mutable number = 0L

                if
                    value.ValueKind <> JsonValueKind.Number
                    || not (value.TryGetInt64(&number))
                    || number < minimum
                then
                    let range = if minimum = 0L then "non-negative" else "positive"
                    fail "RESEARCH_SCHEMA_INVALID" $"payload.{field} must be a {range} integer or literal 'unknown'."

        let payloadBool field =
            let value = requireProperty field body.Payload

            if value.ValueKind <> JsonValueKind.True && value.ValueKind <> JsonValueKind.False then
                fail "RESEARCH_SCHEMA_INVALID" $"payload.{field} must be Boolean."

        let payloadEnum field allowed =
            let value = payloadString field

            if value <> ResearchContract.Unknown && not (Set.contains value allowed) then
                fail "RESEARCH_SCHEMA_INVALID" $"payload.{field} has an unsupported value '{value}'."

        let payloadTimestamp field =
            let value = payloadString field

            if value <> ResearchContract.Unknown then
                validateTimestamp $"payload.{field}" value |> ignore

        let payloadStringArray field =
            let value = requireProperty field body.Payload

            if value.ValueKind <> JsonValueKind.Array then
                fail "RESEARCH_SCHEMA_INVALID" $"payload.{field} must be an array."

            value.EnumerateArray()
            |> Seq.iter (fun item ->
                if item.ValueKind <> JsonValueKind.String then
                    fail "RESEARCH_SCHEMA_INVALID" $"payload.{field} entries must be strings.")

        // The frozen dictionary is a closed schema.  Presence-only checks made
        // it possible for a newly-added event family to silently accept an
        // arbitrary payload; validate the exact field set for every family
        // before applying its field-specific rules below.
        exactPayload (Map.find body.EventType ResearchContract.RequiredPayloadFields |> Set.toList)

        let hashFields =
            set
                [ "protocolBundleSha256"
                  "nonInterferenceSnapshotSha256"
                  "activationGuardSha256"
                  "policySha256"
                  "promptSha256"
                  "toolchainSha256"
                  "taskManifestSha256"
                  "evidenceSha256"
                  "originalLedgerSha256"
                  "verifiedPrefixSha256"
                  "tornTailSha256"
                  "beforeContextSha256"
                  "summarySha256"
                  "resumeStateSha256"
                  "receiptSha256"
                  "sourceManifestSha256"
                  "fileInventorySha256"
                  "dependencyInventorySha256"
                  "analyzerInventorySha256"
                  "testInventorySha256"
                  "decisionActSha256"
                  "commandDigest"
                  "resultSha256" ]

        let objectIdFields =
            set
                [ "baselineCommit"
                  "producedTreeId"
                  "implementationTreeId"
                  "reviewedTreeId"
                  "rejectedTreeId"
                  "acceptedCommit"
                  "acceptedTreeId"
                  "snapshotCommit"
                  "snapshotTreeId"
                  "targetTreeId"
                  "beforeTreeId"
                  "afterTreeId"
                  "resultCommit"
                  "resultTreeId"
                  "commitId"
                  "commitTreeId"
                  "promotedCommit"
                  "promotedTreeId"
                  "rollbackCommit"
                  "fromTreeId"
                  "toTreeId"
                  "supersededCommit"
                  "supersedingCommit"
                  "milestoneTreeId"
                  "tagObjectId"
                  "targetCommit"
                  "affectedCommit"
                  "affectedTreeId" ]

        let nonNegativeFields =
            set
                [ "attempt"
                  "pausedDurationNs"
                  "startedMonotonicNs"
                  "completedMonotonicNs"
                  "durationMs"
                  "eventCount"
                  "changedFiles"
                  "linesAdded"
                  "linesDeleted" ]

        let booleanFields = set [ "continuityOnly"; "gateCoupled"; "counted" ]

        let timestampFields =
            set [ "freezeAtUtc"; "commitTimeUtc"; "discoveredAtUtc"; "closedAtUtc" ]

        let arrayFields = set [ "parentCommitIds"; "changedPaths" ]

        for property in body.Payload.EnumerateObject() do
            if Set.contains property.Name hashFields then
                payloadHash property.Name
            elif Set.contains property.Name objectIdFields then
                payloadObjectId property.Name
            elif Set.contains property.Name nonNegativeFields then
                payloadIntOrUnknown
                    (if property.Name = "attempt" || property.Name = "eventCount" then
                         1L
                     else
                         0L)
                    property.Name
            elif Set.contains property.Name booleanFields then
                payloadBool property.Name
            elif Set.contains property.Name timestampFields then
                payloadTimestamp property.Name
            elif not (Set.contains property.Name arrayFields) then
                payloadString property.Name |> ignore

        if body.Payload.TryGetProperty("parentCommitIds") |> fst then
            payloadStringArray "parentCommitIds"

        if body.Payload.TryGetProperty("changedPaths") |> fst then
            let paths = parseStringListValue "changedPaths" body.Payload

            match paths with
            | ResearchValue.Known known ->
                known |> List.iter (validateRelativePath "payload.changedPaths[]")

                if
                    known
                    <> (known
                        |> List.distinct
                        |> List.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right)))
                then
                    fail "RESEARCH_SCHEMA_INVALID" "payload.changedPaths must be ordinal-sorted and deduplicated."
            | ResearchValue.Unknown -> ()

        if body.Payload.TryGetProperty("parentCommitIds") |> fst then
            body.Payload.GetProperty("parentCommitIds").EnumerateArray()
            |> Seq.iter (fun item ->
                let value = item.GetString()

                if
                    value <> ResearchContract.Unknown
                    && not (Regex.IsMatch(value, "^[0-9a-f]{40}$|^[0-9a-f]{64}$", RegexOptions.CultureInvariant))
                then
                    fail "RESEARCH_SCHEMA_INVALID" "payload.parentCommitIds entries must be Git object IDs or unknown.")

        for field in
            [ "triggerEventId"
              "verificationEventId"
              "resumeFromEventId"
              "resumedEventId"
              "outcomeEventId" ] do
            if body.Payload.TryGetProperty(field) |> fst then
                let value = payloadString field

                if value <> ResearchContract.Unknown && not (eventIdPattern.IsMatch(value)) then
                    fail "RESEARCH_SCHEMA_INVALID" $"payload.{field} must be an event ID or unknown."

        for field in [ "targetTaskId"; "acceptedTaskId" ] do
            if body.Payload.TryGetProperty(field) |> fst then
                let value = payloadString field

                if value <> ResearchContract.Unknown && not (taskIdPattern.IsMatch(value)) then
                    fail "RESEARCH_SCHEMA_INVALID" $"payload.{field} must be a task ID or unknown."

        if body.Payload.TryGetProperty("verdict") |> fst then
            payloadEnum "verdict" (set [ "pass"; "needs-work"; "block"; "reject" ])

        if body.Payload.TryGetProperty("taskOutcome") |> fst then
            payloadEnum "taskOutcome" (set [ "accepted"; "rejected"; "blocked"; "cancelled" ])

        if body.Payload.TryGetProperty("hypothesisResult") |> fst then
            payloadEnum "hypothesisResult" (set [ "supports"; "contradicts"; "inconclusive" ])

        if body.Payload.TryGetProperty("outcomeClass") |> fst then
            payloadEnum "outcomeClass" (set [ "fixed"; "not-fixed"; "regressed"; "abandoned" ])

        if body.Payload.TryGetProperty("fromAutonomyMode") |> fst then
            payloadEnum "fromAutonomyMode" ResearchContract.AutonomyModes

        if body.Payload.TryGetProperty("toAutonomyMode") |> fst then
            payloadEnum "toAutonomyMode" ResearchContract.AutonomyModes

        let validateAttemptAndTargetPayload fields identityField =
            exactPayload fields
            payloadIntOrUnknown 1L "attempt"
            payloadString identityField |> ignore
            payloadObjectId "targetTreeId"

        let validateFailedVerificationPayload () =
            validateAttemptAndTargetPayload [ "attempt"; "evidenceSha256"; "stageId"; "targetTreeId" ] "stageId"
            payloadHash "evidenceSha256"

        match body.EventType with
        | "agent.run.started" ->
            exactPayload [ "agentId"; "agentRole"; "promptSha256"; "toolchainSha256" ]
            let agentRole = payloadString "agentRole"

            if
                agentRole <> "builder"
                && agentRole <> "reviewer"
                && agentRole <> ResearchContract.Unknown
            then
                fail "RESEARCH_SCHEMA_INVALID" "payload.agentRole is invalid."

            payloadHash "promptSha256"
            payloadHash "toolchainSha256"
        | "agent.run.finished" ->
            exactPayload [ "finishClass"; "producedTreeId"; "summarySha256" ]
            payloadString "finishClass" |> ignore
            payloadObjectId "producedTreeId"
            payloadHash "summarySha256"
        | "gate.started" -> validateAttemptAndTargetPayload [ "attempt"; "gateId"; "targetTreeId" ] "gateId"
        | "gate.finished" ->
            validateAttemptAndTargetPayload [ "attempt"; "evidenceSha256"; "gateId"; "targetTreeId" ] "gateId"
            payloadHash "evidenceSha256"
        | "build.failed"
        | "test.failed"
        | "lint.failed"
        | "security.failed"
        | "verify.failed" -> validateFailedVerificationPayload ()
        | "tool.finished" ->
            payloadString "toolClass" |> ignore
            payloadHash "commandDigest"
            payloadIntOrUnknown 0L "startedMonotonicNs"
            payloadIntOrUnknown 0L "completedMonotonicNs"
            payloadHash "resultSha256"
        | "activity.state.changed" ->
            exactPayload [ "fromActivityState"; "toActivityState"; "reasonCode" ]

            for field in [ "fromActivityState"; "toActivityState" ] do
                let value = payloadString field

                if
                    value <> ResearchContract.Unknown
                    && not (Set.contains value ResearchContract.ActivityStates)
                then
                    fail "RESEARCH_SCHEMA_INVALID" $"payload.{field} has an invalid activity state."
        | _ -> ()

        if body.EventType = "wip.snapshot.created" then
            let value = requireProperty "continuityOnly" body.Payload

            if value.ValueKind <> JsonValueKind.True then
                fail "RESEARCH_SCHEMA_INVALID" "wip.snapshot.created requires continuityOnly=true."

        if body.EventType = "architecture.checkpoint.created" then
            let value = requireProperty "gateCoupled" body.Payload

            if value.ValueKind <> JsonValueKind.False then
                fail "RESEARCH_SCHEMA_INVALID" "architecture checkpoints require gateCoupled=false."

        if
            body.EventType = "research.intervention.recorded"
            && requireString "durationMs" body.Payload <> ResearchContract.Unknown
        then
            fail "RESEARCH_SCHEMA_INVALID" "research.intervention.recorded durationMs must remain literal unknown."

        if body.EventType = "agent.run.started" then
            let payloadAgent = requireString "agentId" body.Payload

            match body.ActorId with
            | ResearchValue.Known actor when actor <> payloadAgent ->
                fail "RESEARCH_SCHEMA_INVALID" "agent.run.started payload.agentId must equal actorId."
            | ResearchValue.Unknown when payloadAgent <> ResearchContract.Unknown ->
                fail "RESEARCH_SCHEMA_INVALID" "Known payload.agentId requires the same known actorId."
            | _ -> ()

        let interventionCategoryField =
            if body.EventType.StartsWith("research.intervention.", StringComparison.Ordinal) then
                Some "category"
            elif body.EventType.StartsWith("human.", StringComparison.Ordinal) then
                Some "interventionCategory"
            else
                None

        match interventionCategoryField with
        | Some field when body.EventType <> "research.intervention.ended" ->
            let category = requireString field body.Payload

            if
                category <> ResearchContract.Unknown
                && not (Set.contains category ResearchContract.InterventionCategories)
            then
                fail "RESEARCH_SCHEMA_INVALID" $"Unsupported intervention category '{category}'."

            let counted = requireProperty "counted" body.Payload

            if
                counted.ValueKind <> JsonValueKind.True
                && counted.ValueKind <> JsonValueKind.False
            then
                fail "RESEARCH_SCHEMA_INVALID" "Intervention counted must be Boolean."

            if category = "I0-observation-no-intervention" && counted.GetBoolean() then
                fail "RESEARCH_SCHEMA_INVALID" "I0 observation cannot be counted as an intervention."

            if
                category <> ResearchContract.Unknown
                && category <> "I0-observation-no-intervention"
                && not (counted.GetBoolean())
            then
                fail "RESEARCH_SCHEMA_INVALID" "Only I0 observation may use counted=false."
        | _ -> ()

        match body.SupersedesEventId with
        | ResearchValue.Known superseded when not (eventIdPattern.IsMatch(superseded)) || superseded = body.EventId ->
            fail "RESEARCH_SCHEMA_INVALID" "supersedesEventId must name a different valid event."
        | _ -> ()

    let private validateEventBoundSources (body: ResearchEventBody) (earlier: ResearchEvent list) =
        for source in body.SourceRefs do
            // An in-ledger harness-event reference has no file address.  It is
            // therefore meaningful only as an exact, earlier event binding.
            if
                source.SourceKind = "harness-event"
                && source.RepositoryPath = ResearchValue.Unknown
            then
                match source.SourceEventId with
                | ResearchValue.Unknown ->
                    fail "RESEARCH_SOURCE_INVALID" "harness-event without repositoryPath requires sourceEventId."
                | ResearchValue.Known eventId when eventId = body.EventId ->
                    fail "RESEARCH_SOURCE_INVALID" "harness-event cannot self-reference."
                | ResearchValue.Known eventId ->
                    match earlier |> List.tryFind (fun event -> event.Body.EventId = eventId) with
                    | Some event when
                        event.Body.ObservationId = body.ObservationId
                        && event.EventHash = source.ArtifactSha256
                        ->
                        ()
                    | Some _ -> fail "RESEARCH_SOURCE_INVALID" "harness-event hash or observation binding is invalid."
                    | None ->
                        fail
                            "RESEARCH_SOURCE_INVALID"
                            "harness-event must reference a strictly earlier event in this observation."

    let private safeLedgerPath (root: string) (path: string) (allowMissing: bool) =
        let locations = Workspace.requireInitialized root

        let candidate =
            Workspace.requireSafePath locations "Research ledger" allowMissing path

        let relative = Workspace.relativePath locations candidate

        if
            not (relative.StartsWith(".ai/runtime/research/", StringComparison.Ordinal))
            || not (relative.EndsWith(".jsonl", StringComparison.Ordinal))
        then
            fail "RESEARCH_PATH_INVALID" "Research ledgers must be .jsonl files below .ai/runtime/research/."

        locations, candidate

    let ledgerPath (root: string) (observationId: string) =
        if not (observationIdPattern.IsMatch(observationId)) then
            fail "RESEARCH_SCHEMA_INVALID" "observationId has invalid syntax."

        Path.Combine(
            Path.GetFullPath(root),
            ".ai",
            "runtime",
            "research",
            "studies",
            ResearchContract.StudyId,
            "observations",
            observationId,
            "events.jsonl"
        )

    let lockPath (ledger: string) =
        Path.Combine(Path.GetDirectoryName(Path.GetFullPath(ledger)), ".write.lock")

    let private shaOrEmpty (bytes: byte array) = Internal.sha256Hex bytes

    /// Validates relations which cannot be decided from a single JSON record.
    /// It intentionally reads the immutable event stream and never rewrites it.
    let private validateLifecycle (events: ResearchEvent list) =
        let payloadString event field =
            let value = requireProperty field event.Body.Payload

            if value.ValueKind <> JsonValueKind.String then
                fail "RESEARCH_LIFECYCLE_INVALID" $"payload.{field} must be a string."

            value.GetString()

        let payloadInt event field =
            let value = requireProperty field event.Body.Payload
            let mutable number = 0L

            if value.ValueKind <> JsonValueKind.Number || not (value.TryGetInt64(&number)) then
                fail "RESEARCH_LIFECYCLE_INVALID" $"payload.{field} must be an integer."

            number

        let runs = HashSet<string>(StringComparer.Ordinal)
        let openRuns = HashSet<string>(StringComparer.Ordinal)
        let gates = HashSet<string>(StringComparer.Ordinal)
        let openGates = HashSet<string>(StringComparer.Ordinal)
        let blocks = HashSet<string>(StringComparer.Ordinal)
        let openBlocks = HashSet<string>(StringComparer.Ordinal)
        let repairs = HashSet<string>(StringComparer.Ordinal)
        let openRepairs = HashSet<string>(StringComparer.Ordinal)
        let mutable closed = false
        let mutable outcomeId: string option = None
        let mutable protocolCount = 0
        let mutable startCount = 0
        let mutable closeCount = 0
        let mutable activityCount = 0

        for event in events do
            if closed then
                fail "OBSERVATION_CLOSED" "No event may follow observation.closed."

            match event.Body.EventType with
            | "protocol.frozen" -> protocolCount <- protocolCount + 1
            | "observation.started" ->
                startCount <- startCount + 1

                if protocolCount <> 1 then
                    fail "RESEARCH_LIFECYCLE_INVALID" "observation.started requires exactly one prior protocol.frozen."
            | "activity.state.changed" -> activityCount <- activityCount + 1
            | "agent.run.started" ->
                match event.Body.RunId with
                | ResearchValue.Known runId when runs.Add(runId) -> openRuns.Add(runId) |> ignore
                | ResearchValue.Known _ -> fail "RESEARCH_LIFECYCLE_INVALID" "agent.run.started duplicates runId."
                | ResearchValue.Unknown -> ()
            | "agent.run.finished" ->
                match event.Body.RunId with
                | ResearchValue.Known runId when openRuns.Remove(runId) -> ()
                | ResearchValue.Known _ ->
                    fail "RESEARCH_LIFECYCLE_INVALID" "agent.run.finished has no open matching run."
                | ResearchValue.Unknown -> ()
            | "gate.started" ->
                let key =
                    payloadString event "gateId"
                    + "\u001f"
                    + (payloadInt event "attempt").ToString(CultureInfo.InvariantCulture)

                if not (gates.Add(key)) || not (openGates.Add(key)) then
                    fail "RESEARCH_LIFECYCLE_INVALID" "gate.started duplicates gateId/attempt."
            | "gate.finished" ->
                let key =
                    payloadString event "gateId"
                    + "\u001f"
                    + (payloadInt event "attempt").ToString(CultureInfo.InvariantCulture)

                if not (openGates.Remove(key)) then
                    fail "RESEARCH_LIFECYCLE_INVALID" "gate.finished has no matching gate.started."
            | "budget.blocked"
            | "rate.blocked"
            | "provider.blocked"
            | "infrastructure.blocked" ->
                let blockId = payloadString event "blockId"

                if not (blocks.Add(blockId)) || not (openBlocks.Add(blockId)) then
                    fail "RESEARCH_LIFECYCLE_INVALID" "blockId is already open or reused."
            | "block.resolved" ->
                let blockId = payloadString event "blockId"

                if not (openBlocks.Remove(blockId)) then
                    fail "RESEARCH_LIFECYCLE_INVALID" "block.resolved has no matching open block."
            | "repair.attempted" ->
                let repairId = payloadString event "repairId"

                if not (repairs.Add(repairId)) || not (openRepairs.Add(repairId)) then
                    fail "RESEARCH_LIFECYCLE_INVALID" "repairId is already open or reused."
            | "repair.outcome" ->
                let repairId = payloadString event "repairId"

                if not (openRepairs.Remove(repairId)) then
                    fail "RESEARCH_LIFECYCLE_INVALID" "repair.outcome has no matching repair.attempted."
            | "outcome.observed" ->
                if Option.isSome outcomeId then
                    fail "RESEARCH_LIFECYCLE_INVALID" "Only one outcome.observed is permitted."

                outcomeId <- Some event.Body.EventId
            | "observation.closed" ->
                closeCount <- closeCount + 1

                if
                    closeCount <> 1
                    || protocolCount <> 1
                    || startCount <> 1
                    || activityCount < 1
                    || Option.isNone outcomeId
                then
                    fail
                        "RESEARCH_LIFECYCLE_INVALID"
                        "Closed observation lacks its required protocol/start/activity/outcome chain."

                if payloadString event "outcomeEventId" <> Option.get outcomeId then
                    fail "RESEARCH_LIFECYCLE_INVALID" "observation.closed must bind the actual outcome event."

                if payloadInt event "eventCount" <> event.Sequence then
                    fail "RESEARCH_LIFECYCLE_INVALID" "observation.closed eventCount must equal the final sequence."

                if
                    openRuns.Count <> 0
                    || openGates.Count <> 0
                    || openBlocks.Count <> 0
                    || openRepairs.Count <> 0
                then
                    fail
                        "RESEARCH_LIFECYCLE_INVALID"
                        "observation.closed cannot leave a run, gate, block, or repair open."

                closed <- true
            | _ -> ()

        if protocolCount > 1 || startCount > 1 || closeCount > 1 then
            fail "RESEARCH_LIFECYCLE_INVALID" "Protocol, start, and close are each singleton events."

    /// A deterministic analytic view.  Superseded records remain in the
    /// evidence ledger; this projection merely removes records replaced by a
    /// later, schema-valid event and therefore cannot rewrite history.
    let effectiveEvents (events: ResearchEvent list) =
        let byId = events |> List.map (fun event -> event.Body.EventId, event) |> Map.ofList

        let superseded =
            events
            |> List.choose (fun event ->
                match event.Body.SupersedesEventId with
                | ResearchValue.Known target ->
                    match Map.tryFind target byId with
                    | Some prior when prior.Sequence < event.Sequence && prior.Body.EventType = event.Body.EventType ->
                        Some target
                    | _ -> None
                | ResearchValue.Unknown -> None)
            |> Set.ofList

        events
        |> List.filter (fun event -> not (Set.contains event.Body.EventId superseded))

    let private verifyBytes (bytes: byte array) =
        let events = ResizeArray<ResearchEvent>()
        let ids = HashSet<string>(StringComparer.Ordinal)
        let mutable error = None
        let mutable offset = 0
        let mutable verifiedLength = 0
        let mutable expectedSequence = 1L
        let mutable previousHash = ResearchContract.Unknown
        let mutable observationId = None
        let mutable evidenceClass = None
        let clockValues = Dictionary<string, int64>(StringComparer.Ordinal)
        let interventionIds = HashSet<string>(StringComparer.Ordinal)
        let openInterventions = Dictionary<string, string * int64>(StringComparer.Ordinal)

        while offset < bytes.Length && Option.isNone error do
            let lf = Array.IndexOf(bytes, 0x0Auy, offset)

            if lf < 0 then
                error <- Some(true, "TORN_TAIL: final record has no LF terminator.", offset)
            elif lf = offset then
                error <- Some(false, "RESEARCH_SCHEMA_INVALID: blank JSONL record.", offset)
            else
                let line = bytes[offset .. lf - 1]

                try
                    let event = parseEvent line
                    let canonical = canonicalEventBytes event

                    if canonical.Length <> line.Length || not (Array.forall2 (=) canonical line) then
                        fail "RESEARCH_CANONICAL_INVALID" "Record is not canonical JSON."

                    validateEventBody event.Body
                    validateEventBoundSources event.Body (List.ofSeq events)

                    if event.Sequence <> expectedSequence then
                        fail "RESEARCH_CHAIN_INVALID" "sequence is not gapless."

                    if
                        (event.PreviousEventHash
                         |> ResearchValue.toOption
                         |> Option.defaultValue ResearchContract.Unknown)
                        <> previousHash
                    then
                        fail "RESEARCH_CHAIN_INVALID" "previousEventHash does not match."

                    if not (Internal.isSha256 event.EventHash) || computeHash event <> event.EventHash then
                        fail "RESEARCH_HASH_INVALID" "eventHash mismatch."

                    match event.Body.SupersedesEventId with
                    | ResearchValue.Known superseded ->
                        match events |> Seq.tryFind (fun prior -> prior.Body.EventId = superseded) with
                        | Some prior when prior.Body.EventType = event.Body.EventType -> ()
                        | Some _ ->
                            fail "RESEARCH_SUPERSESSION_INVALID" "supersedesEventId must replace the same event type."
                        | None ->
                            fail
                                "RESEARCH_SUPERSESSION_INVALID"
                                "supersedesEventId must refer to an earlier event in the observation."
                    | ResearchValue.Unknown -> ()

                    if not (ids.Add(event.Body.EventId)) then
                        fail "DUPLICATE_EVENT_ID" $"Duplicate eventId {event.Body.EventId}."

                    match observationId with
                    | None -> observationId <- Some event.Body.ObservationId
                    | Some known when known <> event.Body.ObservationId ->
                        fail "RESEARCH_CHAIN_INVALID" "Observation IDs cannot mix in a ledger."
                    | _ -> ()

                    match evidenceClass with
                    | None -> evidenceClass <- Some event.Body.EvidenceClass
                    | Some known when known <> event.Body.EvidenceClass ->
                        fail "EVIDENCE_CLASS_INVALID" "Evidence classes cannot mix in a ledger."
                    | _ -> ()

                    match event.Body.MonotonicClockId, event.Body.MonotonicTimeNs with
                    | ResearchValue.Known clock, ResearchValue.Known value ->
                        match clockValues.TryGetValue(clock) with
                        | true, last when value < last ->
                            fail "RESEARCH_CLOCK_INVALID" "Monotonic time moved backwards within one clock."
                        | _ -> clockValues[clock] <- value
                    | _ -> ()

                    let interventionId () =
                        let value = requireString "interventionId" event.Body.Payload

                        if value = ResearchContract.Unknown || String.IsNullOrWhiteSpace(value) then
                            fail "INTERVENTION_INVALID" "interventionId cannot be unknown or blank."

                        value

                    match event.Body.EventType with
                    | "research.intervention.started" ->
                        let identifier = interventionId ()

                        if not (interventionIds.Add(identifier)) then
                            fail "INTERVENTION_INVALID" "interventionId is not unique."

                        match event.Body.MonotonicClockId, event.Body.MonotonicTimeNs with
                        | ResearchValue.Known clock, ResearchValue.Known value ->
                            openInterventions.Add(identifier, (clock, value))
                        | _ ->
                            fail "INTERVENTION_INVALID" "Intervention start requires a known monotonic clock and time."
                    | "research.intervention.ended" ->
                        let identifier = interventionId ()

                        match
                            openInterventions.TryGetValue(identifier),
                            event.Body.MonotonicClockId,
                            event.Body.MonotonicTimeNs
                        with
                        | (true, (startClock, startTime)), ResearchValue.Known endClock, ResearchValue.Known endTime when
                            startClock = endClock && endTime >= startTime
                            ->
                            let duration = requireProperty "durationMs" event.Body.Payload
                            let mutable durationMs = 0L

                            if
                                duration.ValueKind <> JsonValueKind.Number
                                || not (duration.TryGetInt64(&durationMs))
                                || durationMs <> (endTime - startTime) / 1_000_000L
                            then
                                fail
                                    "INTERVENTION_INVALID"
                                    "durationMs must equal the same-clock monotonic start/end interval."

                            openInterventions.Remove(identifier) |> ignore
                        | _ ->
                            fail "INTERVENTION_INVALID" "Intervention end requires exactly one open same-clock start."
                    | "research.intervention.recorded" ->
                        let identifier = interventionId ()

                        if not (interventionIds.Add(identifier)) then
                            fail "INTERVENTION_INVALID" "interventionId is not unique."
                    | _ -> ()

                    events.Add(event)
                    previousHash <- event.EventHash
                    expectedSequence <- expectedSequence + 1L
                    verifiedLength <- lf + 1
                    offset <- lf + 1
                with HarnessException message ->
                    let isFinal = lf = bytes.Length - 1

                    let recoverableTail =
                        isFinal
                        && message.StartsWith("RESEARCH_HASH_INVALID:", StringComparison.Ordinal)

                    error <- Some(recoverableTail, message, offset)

        if Option.isNone error then
            try
                validateLifecycle (List.ofSeq events)
            with HarnessException message ->
                error <- Some(false, message, verifiedLength)

        match error with
        | None -> ResearchLedgerStatus.Valid, [], List.ofSeq events, int64 verifiedLength, None
        | Some(isTail, message, badOffset) when isTail ->
            let tail = bytes.[badOffset..]

            ResearchLedgerStatus.TornTail,
            [ "TORN_TAIL"; message ],
            List.ofSeq events,
            int64 verifiedLength,
            Some(shaOrEmpty tail)
        | Some(_, message, _) ->
            ResearchLedgerStatus.Invalid, [ message ], List.ofSeq events, int64 verifiedLength, None

    let verify (root: string) (path: string) =
        let _, safePath = safeLedgerPath root path false
        let bytes = File.ReadAllBytes(safePath)
        let status, errors, events, verifiedLength, tornHash = verifyBytes bytes

        let prefix =
            if verifiedLength = 0L then
                Array.empty
            else
                bytes[0 .. int verifiedLength - 1]

        { Status = status
          Errors = errors
          Events = events
          OriginalSha256 = Some(Internal.sha256Hex bytes)
          VerifiedPrefixSha256 = Internal.sha256Hex prefix
          VerifiedPrefixLength = verifiedLength
          TornTailSha256 = tornHash }

    let readVerified (root: string) (path: string) =
        let result = verify root path

        if result.Status <> ResearchLedgerStatus.Valid then
            fail "RESEARCH_LEDGER_INVALID" (String.concat "; " result.Errors)

        result.Events

    let private lockLedger (ledger: string) =
        let path = lockPath ledger
        Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore

        try
            new FileStream(
                path,
                FileMode.OpenOrCreate,
                FileAccess.ReadWrite,
                FileShare.None,
                1,
                FileOptions.WriteThrough
            )
        with :? IOException as error ->
            fail "CONCURRENT_WRITER" error.Message

    let private writeNewFile (path: string) (bytes: byte array) =
        let parent = Path.GetDirectoryName(path)
        Directory.CreateDirectory(parent) |> ignore

        let temporary =
            Path.Combine(parent, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp")

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
            File.Move(temporary, path, false)
        finally
            if File.Exists(temporary) then
                File.Delete(temporary)

    let private redactBody (policy: RedactionPolicy) (draft: ResearchEventDraft) =
        let mutable changed = false

        let redactValue value =
            match value with
            | ResearchValue.Known text ->
                let redacted, didChange = ResearchCanonical.redactScalar policy text
                changed <- changed || didChange
                ResearchValue.Known redacted
            | ResearchValue.Unknown -> ResearchValue.Unknown

        let redactPaths value =
            match value with
            | ResearchValue.Known paths ->
                let redacted = paths |> List.map (ResearchCanonical.redactScalar policy)
                // A path which needed privacy rewriting is no longer a safely
                // normalized repo-relative path.  The dictionary requires the
                // whole field to be literal unknown, never a marker-bearing
                // pseudo-path that could leak its shape or be used as input.
                if redacted |> List.exists snd then
                    changed <- true
                    ResearchValue.Unknown
                else
                    ResearchValue.Known(redacted |> List.map fst)
            | ResearchValue.Unknown -> ResearchValue.Unknown

        let redactProviderIdentifier value =
            match value with
            | ResearchValue.Known _ ->
                // A provider label is often an account, deployment, or billing
                // handle in disguise.  The public-safe ledger keeps no raw
                // provider identifier; aggregate provider classes belong in
                // redacted evidence, not this envelope.
                changed <- true
                ResearchValue.Unknown
            | ResearchValue.Unknown -> ResearchValue.Unknown

        let sources =
            draft.SourceRefs
            |> List.map (fun source ->
                { source with
                    RepositoryPath = redactValue source.RepositoryPath
                    SourceEventId = redactValue source.SourceEventId })

        let payloadBytes, payloadChanged =
            ResearchCanonical.redactAndCanonicalizePayload policy draft.Payload

        changed <- changed || payloadChanged
        use payloadDocument = JsonDocument.Parse(payloadBytes)

        let body =
            { draft with
                RunId = redactValue draft.RunId
                ParentRunId = redactValue draft.ParentRunId
                CycleId = redactValue draft.CycleId
                MonotonicClockId = redactValue draft.MonotonicClockId
                ActorId = redactValue draft.ActorId
                ProviderId = redactProviderIdentifier draft.ProviderId
                ModelId = redactValue draft.ModelId
                ModelVersion = redactValue draft.ModelVersion
                BranchRef = redactValue draft.BranchRef
                FailureClass = redactValue draft.FailureClass
                ChangedPaths = redactPaths draft.ChangedPaths
                SourceRefs = sources
                Payload = payloadDocument.RootElement.Clone() }

        { body with
            RedactionStatus =
                if changed then
                    ResearchValue.Known "applied"
                else
                    body.RedactionStatus }

    let append (root: string) (path: string) (draft: ResearchEventDraft) =
        let locations, safePath = safeLedgerPath root path true
        let config = HarnessConfig.load locations
        let body = redactBody config.Redaction draft
        let persistedPayloadBytes = ResearchCanonical.canonicalizeElement body.Payload

        if int64 persistedPayloadBytes.Length > config.MaxEventPayloadBytes then
            fail "PAYLOAD_TOO_LARGE" "Redacted research payload exceeds maxEventPayloadBytes."

        validateEventBody body
        Directory.CreateDirectory(Path.GetDirectoryName(safePath)) |> ignore
        use _lock = lockLedger safePath

        if not (File.Exists(safePath)) then
            writeNewFile safePath Array.empty

        let current = verify root safePath

        match current.Status with
        | ResearchLedgerStatus.TornTail ->
            fail "TORN_TAIL" "Ledger has an incomplete or hash-invalid final record; explicit recovery is required."
        | ResearchLedgerStatus.Invalid -> fail "RESEARCH_LEDGER_INVALID" (String.concat "; " current.Errors)
        | ResearchLedgerStatus.Valid -> ()

        if current.Events |> List.exists (fun event -> event.Body.EventId = draft.EventId) then
            fail "DUPLICATE_EVENT_ID" $"Duplicate eventId {draft.EventId}."

        match List.tryLast current.Events with
        | Some previous when previous.Body.EventType = "observation.closed" ->
            fail "OBSERVATION_CLOSED" "A closed observation is append-only complete."
        | Some previous when previous.Body.ObservationId <> body.ObservationId ->
            fail "RESEARCH_CHAIN_INVALID" "Observation IDs cannot mix in a ledger."
        | Some previous when previous.Body.EvidenceClass <> body.EvidenceClass ->
            fail "EVIDENCE_CLASS_INVALID" "Evidence classes cannot mix in a ledger."
        | _ -> ()

        match body.MonotonicClockId, body.MonotonicTimeNs with
        | ResearchValue.Known clock, ResearchValue.Known value ->
            let lastForClock =
                current.Events
                |> List.rev
                |> List.tryPick (fun previous ->
                    match previous.Body.MonotonicClockId, previous.Body.MonotonicTimeNs with
                    | ResearchValue.Known previousClock, ResearchValue.Known previousValue when previousClock = clock ->
                        Some previousValue
                    | _ -> None)

            match lastForClock with
            | Some previous when value < previous ->
                fail "RESEARCH_CLOCK_INVALID" "Monotonic time moved backwards within one clock."
            | _ -> ()
        | _ -> ()

        validateEventBoundSources body current.Events

        let sequence = int64 current.Events.Length + 1L

        let previousHash =
            current.Events
            |> List.tryLast
            |> Option.map (fun event -> ResearchValue.Known event.EventHash)
            |> Option.defaultValue ResearchValue.Unknown

        let unhashed =
            { Body = body
              Sequence = sequence
              PreviousEventHash = previousHash
              EventHash = String.replicate 64 "0" }

        let event =
            { unhashed with
                EventHash = computeHash unhashed }

        let line = canonicalEventBytes event |> ResearchCanonical.appendLf
        let currentBytes = File.ReadAllBytes(safePath)

        if current.OriginalSha256 <> Some(Internal.sha256Hex currentBytes) then
            fail "CONCURRENT_WRITER" "Ledger changed after verification despite the writer lock."

        let candidateStatus, candidateErrors, _, _, _ =
            verifyBytes (Array.concat [ currentBytes; line ])

        if candidateStatus <> ResearchLedgerStatus.Valid then
            fail "RESEARCH_APPEND_INVALID" (String.concat "; " candidateErrors)

        let staged =
            Path.Combine(Path.GetDirectoryName(safePath), $".append.{Guid.NewGuid():N}.tmp")

        try
            use stage =
                new FileStream(
                    staged,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    line.Length,
                    FileOptions.WriteThrough
                )

            stage.Write(line, 0, line.Length)
            stage.Flush(true)
            stage.Close()

            use ledger =
                new FileStream(
                    safePath,
                    FileMode.Append,
                    FileAccess.Write,
                    FileShare.Read,
                    line.Length,
                    FileOptions.WriteThrough
                )

            ledger.Write(line, 0, line.Length)
            ledger.Flush(true)
        finally
            if File.Exists(staged) then
                File.Delete(staged)

        { ObservationId = body.ObservationId
          EventId = body.EventId
          Sequence = sequence
          EventHash = event.EventHash
          LedgerSha256 = Internal.sha256File safePath }

    let recoverTo (root: string) (originalPath: string) (recoveredPath: string) (recoveryDraft: ResearchEventDraft) =
        let _, original = safeLedgerPath root originalPath false
        let _, destination = safeLedgerPath root recoveredPath true

        if String.Equals(original, destination, StringComparison.Ordinal) then
            fail "RECOVERY_PATH_INVALID" "Recovery destination must be a new file."

        if File.Exists(destination) then
            fail "RECOVERY_PATH_INVALID" "Recovery destination already exists."

        use _lock = lockLedger original
        let originalBytes = File.ReadAllBytes(original)
        let status, _, prefixEvents, prefixLength, tornHash = verifyBytes originalBytes

        if status <> ResearchLedgerStatus.TornTail then
            fail "RECOVERY_NOT_APPLICABLE" "Only a TORN_TAIL ledger can be recovered."

        if recoveryDraft.EventType <> "ledger.recovery.recorded" then
            fail "RECOVERY_EVENT_INVALID" "Recovery requires ledger.recovery.recorded."

        let originalHash = Internal.sha256Hex originalBytes

        let prefix =
            if prefixLength = 0L then
                Array.empty
            else
                originalBytes[0 .. int prefixLength - 1]

        let prefixHash = Internal.sha256Hex prefix

        let required name expected =
            if requireString name recoveryDraft.Payload <> expected then
                fail "RECOVERY_EVENT_INVALID" $"payload.{name} does not bind the damaged ledger."

        required "originalLedgerSha256" originalHash
        required "verifiedPrefixSha256" prefixHash
        required "tornTailSha256" (Option.get tornHash)
        required "recoveredLedgerPath" (Workspace.relativePath (Workspace.paths root) destination)
        let config = HarnessConfig.load (Workspace.paths root)
        let body = redactBody config.Redaction recoveryDraft
        let persistedPayloadBytes = ResearchCanonical.canonicalizeElement body.Payload

        if int64 persistedPayloadBytes.Length > config.MaxEventPayloadBytes then
            fail "PAYLOAD_TOO_LARGE" "Redacted recovery payload exceeds maxEventPayloadBytes."

        validateEventBody body

        match List.tryLast prefixEvents with
        | Some previous when
            previous.Body.ObservationId <> body.ObservationId
            || previous.Body.EvidenceClass <> body.EvidenceClass
            ->
            fail "RECOVERY_EVENT_INVALID" "Recovery event does not match the verified prefix."
        | _ -> ()

        let previousHash =
            prefixEvents
            |> List.tryLast
            |> Option.map (fun event -> ResearchValue.Known event.EventHash)
            |> Option.defaultValue ResearchValue.Unknown

        let unhashed =
            { Body = body
              Sequence = int64 prefixEvents.Length + 1L
              PreviousEventHash = previousHash
              EventHash = String.replicate 64 "0" }

        let event =
            { unhashed with
                EventHash = computeHash unhashed }

        let recoveredBytes =
            Array.concat [ prefix; canonicalEventBytes event |> ResearchCanonical.appendLf ]

        let candidateStatus, candidateErrors, _, _, _ = verifyBytes recoveredBytes

        if candidateStatus <> ResearchLedgerStatus.Valid then
            fail "RECOVERY_FAILED" (String.concat "; " candidateErrors)

        writeNewFile destination recoveredBytes
        let result = verify root destination

        if result.Status <> ResearchLedgerStatus.Valid then
            fail "RECOVERY_FAILED" (String.concat "; " result.Errors)

        result
