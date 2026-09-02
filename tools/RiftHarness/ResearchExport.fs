namespace RiftHarness

open System
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Text
open System.Text.Json

type ResearchStudyManifest =
    { CanonicalBytes: byte array
      ManifestSha256: string
      StudyId: string
      ObservationId: string
      EvidenceClass: string
      TargetTaskId: string
      ActorIdentityRule: string
      ProtocolVersion: string
      ProtocolBundleSha256: string
      BaselineCommit: string
      HeadCommit: string
      BaselineTreeId: string
      ResultTreeId: string
      InputTreeId: string
      TaskManifestSha256: string
      CollectorVersion: string
      ExporterVersion: string
      ToolchainSha256: string
      PathMapVersion: string
      RedactionPolicyVersion: string
      SourceInventory: JsonElement
      SourceManifestSha256: string
      GeneratedAtUtc: string
      WindowStartUtc: DateTimeOffset option
      WindowEndUtc: DateTimeOffset option }

type ResearchExportReceipt =
    { ObservationId: string
      OutputDirectory: string
      StudyManifestSha256: string
      EvidenceManifestSha256: string
      SummarySha256: string
      OuterManifestSha256: string
      FileCount: int }

[<RequireQualifiedAccess>]
module ResearchExport =
    let private requiredStudyFields =
        [ "studyId"; "observationId"; "evidenceClass"; "targetTaskId"; "actorIdentityRule"
          "protocolVersion"; "protocolBundleSha256"; "baselineCommit"; "headCommit"
          "baselineTreeId"; "resultTreeId"; "inputTreeId"; "taskManifestSha256"
          "collectorVersion"; "exporterVersion"; "toolchainSha256"; "timezone"; "locale"
          "pathMapVersion"; "sourceInventory"; "sourceInventorySha256"; "redactionPolicyVersion"
          "generatedAtUtc" ]

    let private workspacePath (root: string) (path: string) =
        if Path.IsPathFullyQualified(path) then path
        else Path.Combine(Path.GetFullPath(root), path)

    let private requiredString (name: string) (root: JsonElement) =
        match root.TryGetProperty(name) with
        | true, value when value.ValueKind = JsonValueKind.String -> value.GetString()
        | true, _ -> Internal.fail $"RESEARCH_MANIFEST_INVALID: {name} must be a string."
        | _ -> Internal.fail $"RESEARCH_MANIFEST_INVALID: required field {name} is missing."

    let private requireHash (name: string) (value: string) =
        if value <> ResearchContract.Unknown && not (Internal.isSha256 value) then
            Internal.fail $"RESEARCH_MANIFEST_INVALID: {name} must be SHA-256 or literal unknown."

    let private requireCommit (name: string) (value: string) =
        let valid =
            value = ResearchContract.Unknown
            || ((value.Length = 40 || value.Length = 64)
                && value
                   |> Seq.forall (fun character ->
                       (character >= '0' && character <= '9')
                       || (character >= 'a' && character <= 'f')))

        if not valid then
            Internal.fail $"RESEARCH_MANIFEST_INVALID: {name} must be an exact Git object ID or literal unknown."

    let private optionalUtc (name: string) (root: JsonElement) =
        match root.TryGetProperty(name) with
        | false, _ -> None
        | true, value when value.ValueKind = JsonValueKind.String && value.GetString() = ResearchContract.Unknown -> None
        | true, value when value.ValueKind = JsonValueKind.String ->
            match Internal.tryParseUtc (value.GetString()) with
            | Some timestamp -> Some timestamp
            | None -> Internal.fail $"RESEARCH_MANIFEST_INVALID: {name} must be UTC or literal unknown."
        | _ -> Internal.fail $"RESEARCH_MANIFEST_INVALID: {name} must be a string."

    let loadStudyManifest (root: string) (path: string) =
        let locations = Workspace.requireInitialized root
        let safePath = Workspace.requireSafePath locations "Research study manifest" false (workspacePath root path)
        let config = HarnessConfig.load locations
        let text = Internal.safeReadAllText safePath config.MaxSourceFileBytes
        let canonical = ResearchCanonical.canonicalizeJson text

        use document = JsonDocument.Parse(canonical)
        let element = document.RootElement

        if element.ValueKind <> JsonValueKind.Object then
            Internal.fail "RESEARCH_MANIFEST_INVALID: study manifest must be an object."

        for field in requiredStudyFields do
            if not (element.TryGetProperty(field) |> fst) then
                Internal.fail $"RESEARCH_MANIFEST_INVALID: required field {field} is missing."

        let studyId = requiredString "studyId" element
        let observationId = requiredString "observationId" element
        let evidenceClass = requiredString "evidenceClass" element
        let targetTaskId = requiredString "targetTaskId" element
        let actorIdentityRule = requiredString "actorIdentityRule" element
        let protocolVersion = requiredString "protocolVersion" element
        let protocolBundleSha256 = requiredString "protocolBundleSha256" element
        let baselineCommit = requiredString "baselineCommit" element
        let headCommit = requiredString "headCommit" element
        let baselineTree = requiredString "baselineTreeId" element
        let resultTree = requiredString "resultTreeId" element
        let inputTree = requiredString "inputTreeId" element
        let taskManifestSha256 = requiredString "taskManifestSha256" element
        let collectorVersion = requiredString "collectorVersion" element
        let exporterVersion = requiredString "exporterVersion" element
        let toolchainSha256 = requiredString "toolchainSha256" element
        let pathMapVersion = requiredString "pathMapVersion" element
        let redactionPolicyVersion = requiredString "redactionPolicyVersion" element
        let sourceManifest = requiredString "sourceInventorySha256" element
        let generatedAt = requiredString "generatedAtUtc" element
        let sourceInventory = element.GetProperty("sourceInventory")
        let sourceInventoryBytes = ResearchCanonical.canonicalizeElement sourceInventory

        if studyId <> ResearchContract.StudyId then
            Internal.fail "RESEARCH_MANIFEST_INVALID: studyId is not the frozen Riftward study."

        if not (ResearchContract.EvidenceClasses.Contains(evidenceClass)) then
            Internal.fail "RESEARCH_MANIFEST_INVALID: evidenceClass is invalid."

        if not (observationId.StartsWith("OBS-", StringComparison.Ordinal) && observationId.Length = 30) then
            Internal.fail "RESEARCH_MANIFEST_INVALID: observationId has invalid syntax."

        if targetTaskId <> ResearchContract.Unknown && not (Text.RegularExpressions.Regex.IsMatch(targetTaskId, "^T-[0-9]{3,}$")) then
            Internal.fail "RESEARCH_MANIFEST_INVALID: targetTaskId is invalid."

        for name, value in
            [ "protocolBundleSha256", protocolBundleSha256
              "sourceInventorySha256", sourceManifest
              "taskManifestSha256", taskManifestSha256
              "toolchainSha256", toolchainSha256 ] do
            requireHash name value

        if sourceManifest <> ResearchContract.Unknown && Internal.sha256Hex sourceInventoryBytes <> sourceManifest then
            Internal.fail "RESEARCH_MANIFEST_INVALID: sourceInventorySha256 does not bind sourceInventory."

        for name, value in
            [ "baselineCommit", baselineCommit; "headCommit", headCommit; "baselineTreeId", baselineTree
              "resultTreeId", resultTree; "inputTreeId", inputTree ] do
            requireCommit name value

        if requiredString "timezone" element <> "UTC" || requiredString "locale" element <> "C" then
            Internal.fail "RESEARCH_MANIFEST_INVALID: timezone and locale must be UTC and C."

        match Internal.tryParseUtc generatedAt with
        | None -> Internal.fail "RESEARCH_MANIFEST_INVALID: generatedAtUtc must be a UTC timestamp."
        | Some _ -> ()

        let windowStart = optionalUtc "windowStartUtc" element
        let windowEnd = optionalUtc "windowEndUtc" element

        match windowStart, windowEnd with
        | Some first, Some last when last <= first ->
            Internal.fail "RESEARCH_MANIFEST_INVALID: the longitudinal window must be positive."
        | _ -> ()

        { CanonicalBytes = canonical
          ManifestSha256 = Internal.sha256Hex canonical
          StudyId = studyId
          ObservationId = observationId
          EvidenceClass = evidenceClass
          TargetTaskId = targetTaskId
          ActorIdentityRule = actorIdentityRule
          ProtocolVersion = protocolVersion
          ProtocolBundleSha256 = protocolBundleSha256
          BaselineCommit = baselineCommit
          HeadCommit = headCommit
          BaselineTreeId = baselineTree
          ResultTreeId = resultTree
          InputTreeId = inputTree
          TaskManifestSha256 = taskManifestSha256
          CollectorVersion = collectorVersion
          ExporterVersion = exporterVersion
          ToolchainSha256 = toolchainSha256
          PathMapVersion = pathMapVersion
          RedactionPolicyVersion = redactionPolicyVersion
          SourceInventory = sourceInventory.Clone()
          SourceManifestSha256 = sourceManifest
          GeneratedAtUtc = generatedAt
          WindowStartUtc = windowStart
          WindowEndUtc = windowEnd }

    let private researchString (value: ResearchValue<string>) =
        match value with
        | ResearchValue.Known known -> known
        | ResearchValue.Unknown -> ResearchContract.Unknown

    let private researchInt (value: ResearchValue<int64>) =
        match value with
        | ResearchValue.Known known -> known.ToString(CultureInfo.InvariantCulture)
        | ResearchValue.Unknown -> ResearchContract.Unknown

    let private csvCell (value: string) =
        let text = if isNull value then ResearchContract.Unknown else value

        if text.Contains(',') || text.Contains('"') || text.Contains('\r') || text.Contains('\n') then
            "\"" + text.Replace("\"", "\"\"") + "\""
        else
            text

    let private csvLine (values: string list) =
        values |> List.map csvCell |> String.concat "," |> fun line -> line + "\n"

    let private commonHeaders =
        [ "study_id"; "observation_id"; "run_id"; "parent_run_id"; "cycle_id"; "task_id"
          "event_id"; "sequence"; "monotonic_time_ns"; "monotonic_clock_id"; "occurred_at_utc"
          "evidence_class"; "actor_role"; "actor_id"; "provider_id"; "model_id"; "model_version"
          "branch_ref"; "base_commit"; "head_commit"; "tree_id"; "autonomy_mode"; "activity_state"
          "result"; "exit_code"; "failure_class"; "retry_index"; "repair_index"; "usage_provenance"
          "cost_provenance"; "request_count"; "input_tokens"; "output_tokens"; "cache_read_tokens"
          "cache_write_tokens"; "cost_amount"; "cost_currency"; "changed_files"; "changed_paths"
          "lines_added"; "lines_deleted"; "binary_files_changed"; "human_active_duration_ms"
          "privacy_class"; "redaction_status"; "redaction_policy_version"; "event_hash" ]

    let private changedPaths (value: ResearchValue<string list>) =
        match value with
        | ResearchValue.Unknown -> ResearchContract.Unknown
        | ResearchValue.Known paths ->
            Internal.jsonBytes false (fun writer ->
                writer.WriteStartArray()
                paths |> List.iter (fun path -> writer.WriteStringValue(path: string))
                writer.WriteEndArray())
            |> Constants.Utf8NoBom.GetString

    let private invariantInt (value: int64) = value.ToString(CultureInfo.InvariantCulture)

    let private commonValues (event: ResearchEvent) =
        let body = event.Body

        [ body.StudyId; body.ObservationId; researchString body.RunId; researchString body.ParentRunId
          researchString body.CycleId; researchString body.TaskId; body.EventId; invariantInt event.Sequence
          researchInt body.MonotonicTimeNs; researchString body.MonotonicClockId; researchString body.OccurredAtUtc
          body.EvidenceClass; researchString body.ActorRole; researchString body.ActorId
          researchString body.ProviderId; researchString body.ModelId; researchString body.ModelVersion
          researchString body.BranchRef; researchString body.BaseCommit; researchString body.HeadCommit
          researchString body.TreeId; researchString body.AutonomyMode; researchString body.ActivityState
          researchString body.Result; researchInt body.ExitCode; researchString body.FailureClass
          researchInt body.RetryIndex; researchInt body.RepairIndex; researchString body.UsageProvenance
          researchString body.CostProvenance; researchInt body.RequestCount; researchInt body.InputTokens
          researchInt body.OutputTokens; researchInt body.CacheReadTokens; researchInt body.CacheWriteTokens
          researchString body.CostAmount; researchString body.CostCurrency; researchInt body.ChangedFiles
          changedPaths body.ChangedPaths; researchInt body.LinesAdded; researchInt body.LinesDeleted
          researchInt body.BinaryFilesChanged; researchInt body.HumanActiveDurationMs
          researchString body.PrivacyClass; researchString body.RedactionStatus
          researchString body.RedactionPolicyVersion; event.EventHash ]

    let private payloadValue (name: string) (event: ResearchEvent) =
        match event.Body.Payload.TryGetProperty(name) with
        | false, _ -> ResearchContract.Unknown
        | true, value ->
            match value.ValueKind with
            | JsonValueKind.String -> value.GetString()
            | _ -> ResearchCanonical.canonicalizeElement value |> Constants.Utf8NoBom.GetString

    let private eventCsv (selectedTypes: Set<string>) (events: ResearchEvent list) =
        let selected = events |> List.filter (fun event -> Set.contains event.Body.EventType selectedTypes)

        let payloadHeaders =
            selectedTypes
            |> Seq.collect (fun eventType ->
                ResearchContract.RequiredPayloadFields
                |> Map.tryFind eventType
                |> Option.defaultValue Set.empty)
            |> Set.ofSeq
            |> Set.toList
            |> List.sortWith (fun (left: string) (right: string) -> StringComparer.Ordinal.Compare(left, right))

        let buffer = StringBuilder()
        buffer.Append(csvLine (commonHeaders @ payloadHeaders)) |> ignore

        for event in selected do
            let payloadValues = payloadHeaders |> List.map (fun header -> payloadValue header event)
            buffer.Append(csvLine (commonValues event @ payloadValues)) |> ignore

        Constants.Utf8NoBom.GetBytes(buffer.ToString())

    let private typeSet (prefix: string) =
        ResearchContract.EventTypes |> Set.filter (fun eventType -> eventType.StartsWith(prefix, StringComparison.Ordinal))

    let private writeFile (outputDirectory: string) (path: string) (bytes: byte array) =
        let target = Path.Combine(outputDirectory, path)
        let parent = Path.GetDirectoryName(target)

        if not (String.IsNullOrEmpty(parent)) then
            Directory.CreateDirectory(parent) |> ignore

        use stream = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough)
        stream.Write(bytes, 0, bytes.Length)
        stream.Flush(true)

    let private observationsCsv (manifest: ResearchStudyManifest) (events: ResearchEvent list) =
        let startEvent = events |> List.tryFind (fun event -> event.Body.EventType = "observation.started")
        let closeEvent = events |> List.tryFind (fun event -> event.Body.EventType = "observation.closed")
        let outcomeEvent = events |> List.tryFind (fun event -> event.Body.EventType = "outcome.observed")

        let sourceTime (event: ResearchEvent option) =
            event |> Option.map (fun value -> researchString value.Body.OccurredAtUtc) |> Option.defaultValue ResearchContract.Unknown
        let taskOutcome = outcomeEvent |> Option.map (payloadValue "taskOutcome") |> Option.defaultValue ResearchContract.Unknown
        let hypothesis = outcomeEvent |> Option.map (payloadValue "hypothesisResult") |> Option.defaultValue ResearchContract.Unknown
        let headers = [ "observation_id"; "started_at_utc"; "closed_at_utc"; "evidence_class"; "protocol_bundle_sha256"; "source_manifest_sha256"; "task_outcome"; "hypothesis_result" ]
        let values = [ manifest.ObservationId; sourceTime startEvent; sourceTime closeEvent; manifest.EvidenceClass; manifest.ProtocolBundleSha256; manifest.SourceManifestSha256; taskOutcome; hypothesis ]
        Constants.Utf8NoBom.GetBytes(csvLine headers + csvLine values)

    let private metricsCsv (manifest: ResearchStudyManifest) (events: ResearchEvent list) checkpoints =
        let metrics = ResearchMetrics.calculateWithArchitecture events manifest.WindowStartUtc manifest.WindowEndUtc checkpoints
        let header = csvLine [ "observation_id"; "evidence_class"; "metric_id"; "value"; "unit"; "availability_reason"; "source_manifest_sha256"; "protocol_version" ]
        let body =
            metrics
            |> List.map (fun (row: ResearchMetricRow) ->
                csvLine
                    [ manifest.ObservationId; row.EvidenceClass; row.MetricId; row.Value; row.Unit
                      row.AvailabilityReason; manifest.SourceManifestSha256; manifest.ProtocolVersion ])
            |> String.concat ""

        Constants.Utf8NoBom.GetBytes(header + body)

    let private harnessRunIdFromPath (path: string) =
        let parts = path.Split('/', StringSplitOptions.None)

        match parts with
        | [| ".ai"; "runtime"; "runs"; runId; "events.jsonl" |]
            when Internal.isRunId runId
                 && String.Equals(path, $".ai/runtime/runs/{runId}/events.jsonl", StringComparison.Ordinal) ->
            runId
        | _ ->
            Internal.fail "EXPORT_SOURCE_INVALID: harness-event path must be the exact .ai/runtime/runs/<runId>/events.jsonl receipt path."

    let private resolveHarnessRunEvent root expectedRunId (source: ResearchSourceReference) (path: string) (receiptHash: string) =
        if source.RepositoryCommit <> ResearchValue.Unknown
           || source.LineStart <> ResearchValue.Unknown
           || source.LineEnd <> ResearchValue.Unknown
           || receiptHash <> source.ArtifactSha256 then
            Internal.fail "EXPORT_SOURCE_INVALID: harness-event receipt metadata is not exact."

        let runId = harnessRunIdFromPath path

        match expectedRunId with
        | ResearchValue.Known declaredRunId when not (String.Equals(declaredRunId, runId, StringComparison.Ordinal)) ->
            Internal.fail "EXPORT_SOURCE_INVALID: harness-event receipt path crosses the declared research run."
        | _ -> ()

        let matchingEvents =
            RunStore.eventsStrict root runId
            |> List.filter (fun event -> String.Equals(event.EventHash, receiptHash, StringComparison.Ordinal))

        let authoritative =
            match matchingEvents with
            | [ event ] -> RunStore.eventByReceipt root runId event.Sequence event.EventType receiptHash
            | [] -> Internal.fail "EXPORT_SOURCE_INVALID: harness-event receipt is absent from its authoritative run."
            | _ -> Internal.fail "EXPORT_SOURCE_INVALID: harness-event receipt hash is duplicated in its authoritative run."

        if not (String.Equals(authoritative.EventHash, source.ArtifactSha256, StringComparison.Ordinal)) then
            Internal.fail "EXPORT_SOURCE_INVALID: harness-event receipt artifact hash mismatch."

    let private sourceBytes root priorEvents expectedRunId (source: ResearchSourceReference) =
        match source.SourceKind, source.Resolvable, source.RepositoryCommit, source.RepositoryPath, source.SourceEventId with
        | "fixture", false, _, _, _ -> None
        | "harness-event", true, _, ResearchValue.Known path, ResearchValue.Known eventHash ->
            resolveHarnessRunEvent root expectedRunId source path eventHash
            None
        | "harness-event", true, _, _, ResearchValue.Known eventId ->
            match priorEvents |> List.tryFind (fun event -> event.Body.EventId = eventId) with
            | Some event when event.EventHash = source.ArtifactSha256 -> None
            | _ -> Internal.fail "EXPORT_SOURCE_INVALID: research-event binding is absent or hash-mismatched."
        | _, true, ResearchValue.Known commit, ResearchValue.Known path, _ ->
            let bytes = ResearchGitImport.fileAtCommit root commit path
            if Internal.sha256Hex bytes <> source.ArtifactSha256 then Internal.fail "EXPORT_SOURCE_INVALID: git blob hash mismatch."
            Some bytes
        | _, true, ResearchValue.Unknown, ResearchValue.Known path, _ ->
            let locations = Workspace.requireInitialized root
            let target = Workspace.requireSafePath locations "Research export source" false (workspacePath root path)
            if not (File.Exists(target)) || Internal.sha256File target <> source.ArtifactSha256 then Internal.fail "EXPORT_SOURCE_INVALID: current artifact hash mismatch."
            Some(File.ReadAllBytes(target))
        | _ -> Internal.fail "EXPORT_SOURCE_INVALID: sourceRef is not exactly resolvable."

    let verifyLedgerSources root (events: ResearchEvent list) =
        events
        |> List.mapi (fun index event -> index, event)
        |> List.iter (fun (index, event) ->
            let prior = events |> List.take index
            event.Body.SourceRefs
            |> List.iter (fun source ->
                if source.SourceKind <> "fixture" && not source.Resolvable then
                    Internal.fail "EXPORT_SOURCE_INVALID: non-fixture sourceRef must be resolvable."
                sourceBytes root prior event.Body.RunId source |> ignore))

    let private requireClosedChain (manifest: ResearchStudyManifest) (events: ResearchEvent list) =
        let exactlyOne eventType = events |> List.filter (fun event -> event.Body.EventType = eventType)
        let protocol = exactlyOne "protocol.frozen"
        let started = exactlyOne "observation.started"
        let outcomes = exactlyOne "outcome.observed"
        let closes = exactlyOne "observation.closed"
        if protocol.Length <> 1 || started.Length <> 1 || outcomes.Length <> 1 || closes.Length <> 1 || (events |> List.exists (fun event -> event.Body.EventType = "activity.state.changed") |> not) then
            Internal.fail "EXPORT_CHAIN_INCOMPLETE: protocol/start/activity/outcome/close chain is required."
        if events |> List.last <> closes.Head then Internal.fail "EXPORT_CHAIN_INCOMPLETE: observation.closed must be final."
        let close = closes.Head
        let eventCount = payloadValue "eventCount" close
        if eventCount <> events.Length.ToString(CultureInfo.InvariantCulture) || payloadValue "sourceManifestSha256" close <> manifest.SourceManifestSha256 then
            Internal.fail "EXPORT_CHAIN_INCOMPLETE: closure does not bind eventCount/source manifest."

    let private boundArchitecture root events =
        events
        |> List.mapi (fun index event -> index, event)
        |> List.choose (fun (index, event) ->
            if event.Body.EventType <> "architecture.checkpoint.created" then None
            else
                let prior = events |> List.take index
                let artifacts =
                    event.Body.SourceRefs
                    |> List.choose (fun source -> sourceBytes root prior event.Body.RunId source |> Option.map (fun bytes -> source.ArtifactSha256, bytes))
                    |> Map.ofList
                let find name =
                    let hash = payloadValue name event
                    artifacts |> Map.tryFind hash |> Option.defaultWith (fun () -> Internal.fail $"ARCHITECTURE_BINDING_INVALID: missing {name} source artifact.")
                Some(ResearchArchitecture.bind event.Body.Payload (find "fileInventorySha256") (find "dependencyInventorySha256") (find "analyzerInventorySha256") (find "testInventorySha256")))

    let private architectureFilesCsv (checkpoints: BoundArchitectureCheckpoint list) =
        let header = [ "checkpoint_id"; "tree_id"; "repo_relative_path"; "file_class"; "component_id"; "lines"; "baseline_lines"; "line_delta"; "analyzer_warning_count"; "test_case_count"; "complexity_method"; "complexity_value"; "source_sha256" ]
        let rows = checkpoints |> List.collect (fun checkpoint -> checkpoint.FileRows |> List.map (fun row -> [ checkpoint.CheckpointId; checkpoint.AcceptedTreeId; row.RepoRelativePath; row.FileClass; row.Component; row.Lines; row.BaselineLines; row.LineDelta; row.AnalyzerWarnings; row.TestCount; row.ComplexityMethod; row.Complexity; row.SourceSha256 ])) |> List.sortBy (fun row -> row[0], row[2])
        Constants.Utf8NoBom.GetBytes(csvLine header + (rows |> List.map csvLine |> String.concat ""))

    let private architectureDependenciesCsv (checkpoints: BoundArchitectureCheckpoint list) =
        let header = [ "checkpoint_id"; "tree_id"; "from_component"; "to_component"; "dependency_kind"; "direction_class"; "evidence_sha256" ]
        let rows = checkpoints |> List.collect (fun checkpoint -> checkpoint.DependencyRows |> List.map (fun row -> [ checkpoint.CheckpointId; checkpoint.AcceptedTreeId; row.FromComponent; row.ToComponent; "project-reference"; row.Direction; ResearchContract.Unknown ])) |> List.sortBy (fun row -> row[0], row[2], row[3])
        Constants.Utf8NoBom.GetBytes(csvLine header + (rows |> List.map csvLine |> String.concat ""))

    let private architectureTrendsCsv (manifest: ResearchStudyManifest) (checkpoints: BoundArchitectureCheckpoint list) =
        let header = [ "observation_id"; "checkpoint_id"; "baseline_commit"; "result_commit"; "path_map_version"; "production_files_changed"; "production_modules_touched"; "project_reference_edges_added"; "project_reference_edges_removed"; "confirmed_boundary_violations"; "gross_lines_added"; "gross_lines_deleted"; "binary_files_changed" ]
        let rows = checkpoints |> List.map (fun checkpoint ->
            let files = checkpoint.FileRows
            let production = files |> List.filter (fun row -> row.FileClass = "production" && row.LineDelta <> "0")
            let lines (selector: ArchitectureFileRow -> string) = files |> List.map selector |> List.map (fun value -> match Int64.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) with | true, number -> Some number | _ -> None) |> fun values -> if values |> List.exists Option.isNone then ResearchContract.Unknown else values |> List.choose id |> List.sum |> string
            [ manifest.ObservationId; checkpoint.CheckpointId; checkpoint.BaselineCommit; checkpoint.ResultCommit; checkpoint.PathMapVersion; string production.Length; string (production |> List.map (fun row -> row.Component) |> Set.ofList |> Set.count); string (checkpoint.DependencyRows |> List.filter (fun row -> row.Change = "added") |> List.length); string (checkpoint.DependencyRows |> List.filter (fun row -> row.Change = "removed") |> List.length); string checkpoint.ConfirmedFindingIds.Length; lines (fun row -> if row.LineDelta = "unknown" then "unknown" else string (max 0L (Int64.Parse(row.LineDelta, CultureInfo.InvariantCulture)))); lines (fun row -> if row.LineDelta = "unknown" then "unknown" else string (max 0L (-Int64.Parse(row.LineDelta, CultureInfo.InvariantCulture)))); string (files |> List.filter (fun row -> row.Lines = "unknown") |> List.length) ]) |> List.sortBy (fun row -> row[1])
        Constants.Utf8NoBom.GetBytes(csvLine header + (rows |> List.map csvLine |> String.concat ""))

    let private emptyCsv (headers: string list) = Constants.Utf8NoBom.GetBytes(csvLine headers)

    let private evidenceManifestBytes (files: (string * byte array) list) =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteStartArray("files")

            for path, bytes in files |> List.sortWith (fun (left, _) (right, _) -> StringComparer.Ordinal.Compare(left, right)) do
                writer.WriteStartObject()
                writer.WriteNumber("bytes", bytes.LongLength)
                writer.WriteString("path", path)
                writer.WriteString("sha256", Internal.sha256Hex bytes)
                writer.WriteEndObject()

            writer.WriteEndArray()
            writer.WriteNumber("schemaVersion", 1)
            writer.WriteEndObject())
        |> Constants.Utf8NoBom.GetString
        |> ResearchCanonical.canonicalizeJson

    let private summaryBytes (manifest: ResearchStudyManifest) (eventCount: int) (evidenceHash: string) =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteString("baselineCommit", manifest.BaselineCommit)
            writer.WriteString("evidenceManifestSha256", evidenceHash)
            writer.WriteString("headCommit", manifest.HeadCommit)
            writer.WriteString("inputTreeId", manifest.InputTreeId)
            writer.WriteString("observationId", manifest.ObservationId)
            writer.WriteNumber("observedEventCount", eventCount)
            writer.WriteString("protocolVersion", manifest.ProtocolVersion)
            writer.WriteString("resultTreeId", manifest.ResultTreeId)
            writer.WriteString("studyId", manifest.StudyId)
            writer.WriteEndObject())
        |> Constants.Utf8NoBom.GetString
        |> ResearchCanonical.canonicalizeJson

    let private reportBytes (manifest: ResearchStudyManifest) (eventCount: int) (evidenceHash: string) (summaryHash: string) =
        $"""# Riftward research export

- Study: `{manifest.StudyId}`
- Observation: `{manifest.ObservationId}`
- Evidence class: `{manifest.EvidenceClass}`
- Protocol: `{manifest.ProtocolVersion}`
- Baseline commit: `{manifest.BaselineCommit}`
- Head commit: `{manifest.HeadCommit}`
- Result tree: `{manifest.ResultTreeId}`
- Input tree: `{manifest.InputTreeId}`
- Events: `{eventCount}`
- Evidence manifest SHA-256: `{evidenceHash}`
- Summary SHA-256: `{summaryHash}`

Missing values remain literal `unknown`. This private canonical export is not
an authorization to publish data and does not establish a task outcome.
"""
        |> fun text -> text.Replace("\r\n", "\n")
        |> Constants.Utf8NoBom.GetBytes

    let private outerManifestBytes (files: (string * byte array) list) =
        files
        |> List.sortWith (fun (left, _) (right, _) -> StringComparer.Ordinal.Compare(left, right))
        |> List.map (fun (path, bytes) -> $"{Internal.sha256Hex bytes}  {path}\n")
        |> String.concat ""
        |> Constants.Utf8NoBom.GetBytes

    let private publicationBlockBytes =
        Constants.Utf8NoBom.GetBytes("RAW RESEARCH EXPORT - PUBLICATION BLOCKED\nThis private export contains non-public research evidence and cannot be published.\n")

    let export (root: string) (studyManifestPath: string) (outputDirectory: string) =
        let locations = Workspace.requireInitialized root
        let manifest = loadStudyManifest root studyManifestPath
        let ledger = ResearchLedger.ledgerPath root manifest.ObservationId
        let events = ResearchLedger.readVerified root ledger

        if events |> List.exists (fun event -> event.Body.EvidenceClass <> manifest.EvidenceClass) then
            Internal.fail "EVIDENCE_CLASS_INVALID: study manifest and ledger differ."

        requireClosedChain manifest events

        verifyLedgerSources root events

        let checkpoints = boundArchitecture root events

        let output = Workspace.requireSafePath locations "Research export directory" true (workspacePath root outputDirectory)
        let relative = Workspace.relativePath locations output

        if not (relative.StartsWith(".ai/runtime/research/exports/", StringComparison.Ordinal)) then
            Internal.fail "RESEARCH_PATH_INVALID: export must be below .ai/runtime/research/exports/."

        if Directory.Exists(output) || File.Exists(output) then
            Internal.fail "EXPORT_PATH_EXISTS: output must be a fresh absent directory."

        Directory.CreateDirectory(output) |> ignore

        let eventGroups =
            [ "autopilot-cycles.csv", typeSet "autopilot."
              "agent-runs.csv", typeSet "agent.run."
              "task-lifecycle.csv", typeSet "task."
              "continuity.csv", set [ "wip.snapshot.created"; "context.compacted"; "run.resumed" ]
              "activity-intervals.csv", Set.union (typeSet "activity.") (typeSet "autonomy.")
              "routing.csv", Set.union (typeSet "routing.") (typeSet "model.")
              "human-events.csv", typeSet "human."
              "interventions.csv", typeSet "research.intervention."
              "gate-attempts.csv", typeSet "gate."
              "failures-and-repairs.csv", Set.union (typeSet "repair.") (set [ "build.failed"; "test.failed"; "lint.failed"; "security.failed"; "verify.failed" ])
              "blocks.csv", Set.union (typeSet "block.") (set [ "budget.blocked"; "rate.blocked"; "provider.blocked"; "infrastructure.blocked" ])
              "git-evolution.csv", Set.union (typeSet "git.") (set [ "revision.observed" ])
              "outcomes.csv", set [ "outcome.observed"; "observation.closed"; "milestone.reached"; "git.tag.observed"; "defect.observed" ]
              "usage.csv", set [ "agent.run.started"; "agent.run.finished"; "tool.finished"; "model.switched" ] ]

        let ledgerBytes = File.ReadAllBytes(ledger)
        let effectiveBytes =
            ResearchLedger.effectiveEvents events
            |> List.collect (fun event -> ResearchLedger.canonicalEventBytes event |> ResearchCanonical.appendLf |> Array.toList)
            |> List.toArray

        let dataFiles: (string * byte array) list =
            [ ("study-manifest.json", manifest.CanonicalBytes)
              ("events.jsonl", ledgerBytes)
              ("effective-events.jsonl", effectiveBytes)
              ("observations.csv", observationsCsv manifest events)
              ("architecture-trends.csv", architectureTrendsCsv manifest checkpoints)
              ("architecture-files.csv", architectureFilesCsv checkpoints)
              ("architecture-dependencies.csv", architectureDependenciesCsv checkpoints)
              ("metrics.csv", metricsCsv manifest events checkpoints) ]
            @ (eventGroups |> List.map (fun (path, types) -> path, eventCsv types events))
            |> List.sortWith (fun (left, _) (right, _) -> StringComparer.Ordinal.Compare(left, right))

        let evidenceInputs = dataFiles
        let evidenceManifest = evidenceManifestBytes evidenceInputs
        let evidenceHash = Internal.sha256Hex evidenceManifest
        let summary = summaryBytes manifest events.Length evidenceHash
        let summaryHash = Internal.sha256Hex summary
        let report = reportBytes manifest events.Length evidenceHash summaryHash
        let completeFiles = dataFiles @ [ "evidence-manifest.json", evidenceManifest; "summary.json", summary; "report.md", report; "PUBLICATION.BLOCKED", publicationBlockBytes ]
        let outer = outerManifestBytes completeFiles
        let finalFiles = completeFiles @ [ "EXPORT.SHA256", outer ]

        try
            for path, bytes in finalFiles do
                writeFile output path bytes
        with error ->
            Internal.fail $"EXPORT_WRITE_FAILED: {error.GetType().Name}. Partial output remains quarantined at {relative}."

        { ObservationId = manifest.ObservationId
          OutputDirectory = relative
          StudyManifestSha256 = manifest.ManifestSha256
          EvidenceManifestSha256 = evidenceHash
          SummarySha256 = summaryHash
          OuterManifestSha256 = Internal.sha256Hex outer
          FileCount = finalFiles.Length }

    let private exactObject label fields (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object
           || (element.EnumerateObject() |> Seq.map (fun property -> property.Name) |> Set.ofSeq) <> Set.ofList fields then
            Internal.fail $"EXPORT_INVALID: {label} field set is invalid."

    let private canonicalJsonMember path label =
        let bytes = File.ReadAllBytes(path)
        let canonical = ResearchCanonical.canonicalizeJson (Constants.Utf8NoBom.GetString(bytes))
        if canonical <> bytes then Internal.fail $"EXPORT_INVALID: {label} is not canonical JSON."
        use document = JsonDocument.Parse(bytes)
        document.RootElement.Clone()

    let verifyExportWithExpectedReceipt (root: string) (outputDirectory: string) (expectedOuterSha256: string option) =
        let locations = Workspace.requireInitialized root
        let output = Workspace.requireSafePath locations "Research export directory" false (workspacePath root outputDirectory)
        let outerPath = Path.Combine(output, "EXPORT.SHA256")

        if not (File.Exists(outerPath)) then
            Internal.fail "EXPORT_INVALID: EXPORT.SHA256 is missing."

        let lines = File.ReadAllLines(outerPath, Constants.Utf8NoBom)
        let seen = HashSet<string>(StringComparer.Ordinal)

        for line in lines do
            if line.Length < 67 || line.Substring(64, 2) <> "  " then
                Internal.fail "EXPORT_INVALID: malformed outer manifest line."

            let expected = line.Substring(0, 64)
            let path = line.Substring(66)

            if not (Internal.isSha256 expected) || path = "EXPORT.SHA256" || not (seen.Add(path)) then
                Internal.fail "EXPORT_INVALID: unsafe or duplicate outer manifest entry."

            let target = Workspace.requireSafePath locations "Research export member" false (Path.Combine(output, path))

            if not (File.Exists(target)) || Internal.sha256File target <> expected then
                Internal.fail $"EXPORT_HASH_INVALID: {path}."

        let listedPaths = lines |> Array.map (fun line -> line.Substring(66)) |> Array.toList

        if listedPaths <> (listedPaths |> List.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right))) then
            Internal.fail "EXPORT_INVALID: EXPORT.SHA256 is not ordinal-sorted."

        let actualFiles =
            Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories)
            |> Seq.map (fun path -> Path.GetRelativePath(output, path).Replace('\\', '/'))
            |> Set.ofSeq

        let expectedFiles = Set.add "EXPORT.SHA256" (Set.ofSeq seen)

        if actualFiles <> expectedFiles then
            Internal.fail "EXPORT_INVALID: exported file set differs from EXPORT.SHA256."

        let requiredMembers = set [ "study-manifest.json"; "events.jsonl"; "effective-events.jsonl"; "evidence-manifest.json"; "summary.json"; "report.md"; "PUBLICATION.BLOCKED" ]
        if not (Set.isSubset requiredMembers (Set.ofSeq seen)) then Internal.fail "EXPORT_INVALID: required layered export members are missing."
        if File.ReadAllBytes(Path.Combine(output, "PUBLICATION.BLOCKED")) <> publicationBlockBytes then
            Internal.fail "EXPORT_INVALID: raw export publication block is missing or altered."

        let manifest = loadStudyManifest root (Path.Combine(output, "study-manifest.json"))
        let evidencePath = Path.Combine(output, "evidence-manifest.json")
        let evidence = canonicalJsonMember evidencePath "evidence manifest"
        exactObject "evidence manifest" [ "files"; "schemaVersion" ] evidence
        if evidence.GetProperty("schemaVersion").GetInt32() <> 1 then Internal.fail "EXPORT_INVALID: evidence manifest schemaVersion is invalid."
        let entries =
            evidence.GetProperty("files").EnumerateArray()
            |> Seq.map (fun entry ->
                exactObject "evidence manifest entry" [ "bytes"; "path"; "sha256" ] entry
                let path, hash, count = entry.GetProperty("path").GetString(), entry.GetProperty("sha256").GetString(), entry.GetProperty("bytes").GetInt64()
                if not (Internal.isSha256 hash) || count < 0L then Internal.fail "EXPORT_INVALID: evidence manifest entry is malformed."
                path, hash, count)
            |> Seq.toList
        if entries <> (entries |> List.sortBy (fun (path, _, _) -> path)) then Internal.fail "EXPORT_INVALID: evidence manifest is not ordered."
        let evidencePaths = entries |> List.map (fun (path, _, _) -> path) |> Set.ofList
        let expectedEvidencePaths =
            seen
            |> Set.ofSeq
            |> Set.remove "evidence-manifest.json"
            |> Set.remove "summary.json"
            |> Set.remove "report.md"
            |> Set.remove "PUBLICATION.BLOCKED"
        if evidencePaths.Count <> entries.Length || evidencePaths <> expectedEvidencePaths then
            Internal.fail "EXPORT_INVALID: evidence manifest member set is incomplete or duplicated."
        for path, hash, count in entries do
            let memberPath = Workspace.requireSafePath locations "Evidence manifest member" false (Path.Combine(output, path))
            if not (File.Exists(memberPath)) || FileInfo(memberPath).Length <> count || Internal.sha256File memberPath <> hash then
                Internal.fail $"EXPORT_HASH_INVALID: evidence member {path}."
        let evidenceHash = Internal.sha256File evidencePath

        let summaryPath = Path.Combine(output, "summary.json")
        let summary = canonicalJsonMember summaryPath "summary"
        exactObject "summary" [ "baselineCommit"; "evidenceManifestSha256"; "headCommit"; "inputTreeId"; "observationId"; "observedEventCount"; "protocolVersion"; "resultTreeId"; "studyId" ] summary
        if summary.GetProperty("evidenceManifestSha256").GetString() <> evidenceHash
           || summary.GetProperty("studyId").GetString() <> manifest.StudyId
           || summary.GetProperty("observationId").GetString() <> manifest.ObservationId then
            Internal.fail "EXPORT_INVALID: summary binding is invalid."

        let ledger = ResearchLedger.ledgerPath root manifest.ObservationId
        let eventPath = Path.Combine(output, "events.jsonl")
        if not (File.Exists(ledger)) || File.ReadAllBytes(eventPath) <> File.ReadAllBytes(ledger) then
            Internal.fail "EXPORT_INVALID: events.jsonl is not bound to the authoritative ledger."
        let events = ResearchLedger.readVerified root ledger
        requireClosedChain manifest events
        verifyLedgerSources root events
        if summary.GetProperty("observedEventCount").GetInt32() <> events.Length then Internal.fail "EXPORT_INVALID: summary event count is invalid."
        let effective =
            ResearchLedger.effectiveEvents events
            |> List.collect (fun event -> ResearchLedger.canonicalEventBytes event |> ResearchCanonical.appendLf |> Array.toList)
            |> List.toArray
        if File.ReadAllBytes(Path.Combine(output, "effective-events.jsonl")) <> effective then Internal.fail "EXPORT_INVALID: effective event view is not bound to the ledger."
        let report = Constants.Utf8NoBom.GetString(File.ReadAllBytes(Path.Combine(output, "report.md")))
        let summaryHash = Internal.sha256File summaryPath
        if not (report.Contains($"Study: `{manifest.StudyId}`", StringComparison.Ordinal)
                && report.Contains($"Observation: `{manifest.ObservationId}`", StringComparison.Ordinal)
                && report.Contains($"Evidence manifest SHA-256: `{evidenceHash}`", StringComparison.Ordinal)
                && report.Contains($"Summary SHA-256: `{summaryHash}`", StringComparison.Ordinal)) then
            Internal.fail "EXPORT_INVALID: report bindings are invalid."

        let outerHash = Internal.sha256File outerPath
        match expectedOuterSha256 with
        | Some expected when not (Internal.isSha256 expected) || expected <> outerHash -> Internal.fail "EXPORT_RECEIPT_MISMATCH: independently supplied export receipt does not match."
        | _ -> ()
        outerHash

    let verifyExport (root: string) (outputDirectory: string) =
        verifyExportWithExpectedReceipt root outputDirectory None
