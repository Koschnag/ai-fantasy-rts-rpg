namespace RiftHarness

open System
open System.Globalization
open System.IO
open System.Text.Encodings.Web
open System.Text.Json
open System.Threading

module Cli =
    let takeFlag (name: string) (arguments: string list) =
        let occurrences = arguments |> List.filter ((=) name) |> List.length

        if occurrences > 1 then
            Internal.fail $"Flag '{name}' wurde mehrfach angegeben."

        occurrences = 1, arguments |> List.filter ((<>) name)

    let takeOption (name: string) (arguments: string list) =
        let rec loop (found: string option) (collected: string list) (remaining: string list) =
            match remaining with
            | [] -> found, List.rev collected
            | option :: _ when option = name && found.IsSome ->
                Internal.fail $"Option '{name}' wurde mehrfach angegeben."
            | option :: value :: tail when option = name -> loop (Some value) collected tail
            | [ option ] when option = name -> Internal.fail $"Option '{name}' benoetigt einen Wert."
            | head :: tail -> loop found (head :: collected) tail

        loop None [] arguments

    let requireOption name arguments =
        let value, remaining = takeOption name arguments

        match value with
        | Some result -> result, remaining
        | None -> Internal.fail $"Erforderliche Option fehlt: {name}"

    let collectOptions (name: string) (arguments: string list) : string list * string list =
        let rec loop collected remaining =
            match remaining with
            | [] -> List.rev collected, []
            | option :: value :: tail when option = name -> loop (value :: collected) tail
            | [ option ] when option = name -> Internal.fail $"Option '{name}' benoetigt einen Wert."
            | head :: tail ->
                let values, rest = loop collected tail
                values, head :: rest

        loop [] arguments

    let noArguments command remaining =
        if not (List.isEmpty remaining) then
            let joinedArguments = String.concat " " remaining
            Internal.fail $"Unerwartete Argumente fuer '{command}': {joinedArguments}"

    let jsonResult write =
        Internal.jsonBytes true (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", Constants.SchemaVersion)
            write writer
            writer.WriteEndObject())
        |> Constants.Utf8NoBom.GetString

    let private writeCalibrationEnvelope (command: string) writeBody =
        use stream = new MemoryStream()

        use writer =
            new Utf8JsonWriter(
                stream,
                JsonWriterOptions(Indented = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping)
            )

        writer.WriteStartObject()
        writer.WriteString("command", command)
        writeBody writer
        writer.WriteNumber("schemaVersion", Constants.SchemaVersion)
        writer.WriteEndObject()
        writer.Flush()

        stream.ToArray()
        |> Constants.Utf8NoBom.GetString
        |> fun value ->
            Console.Out.Write(value)
            Console.Out.Write('\n')

    let private calibrationError (command: string) (code: string) (message: string) exitCode =
        writeCalibrationEnvelope command (fun writer ->
            writer.WritePropertyName("error")
            writer.WriteStartObject()
            writer.WriteString("code", code)
            writer.WriteString("message", message)
            writer.WriteEndObject()
            writer.WriteBoolean("ok", false))

        exitCode

    let private workspaceRootIsSafe root =
        try
            let mutable current = DirectoryInfo(Path.GetFullPath(root))
            let mutable safe = current.Exists

            while safe && not (isNull current) do
                safe <-
                    isNull current.LinkTarget
                    && not (current.Attributes.HasFlag(FileAttributes.ReparsePoint))

                current <- current.Parent

            safe
        with
        | :? IOException
        | :? UnauthorizedAccessException
        | :? ArgumentException
        | :? NotSupportedException
        | :? System.Security.SecurityException -> false

    [<Literal>]
    let private AssetGeneratorActor = "riftward-dotnet-asset-generator"

    let private executeAssetCalibration namespaceName allowMutation arguments =
        let rec inferCommand remaining =
            match remaining with
            | "--workspace" :: _ :: tail -> inferCommand tail
            | "validate-spec" :: _ -> "validate-spec"
            | "inspect" :: _ -> "inspect"
            | "generate" :: _ -> "generate"
            | "recover" :: _ -> "recover"
            | _ -> namespaceName

        let command = inferCommand arguments

        try
            let optionNames =
                [ "--workspace"; "--spec"; "--glb"; "--preview"; "--report"; "--job-id" ]

            for optionName in optionNames do
                if arguments |> List.filter ((=) optionName) |> List.length > 1 then
                    Internal.fail "Option wurde mehrfach angegeben."

            arguments
            |> List.pairwise
            |> List.iter (fun (optionName, value) ->
                if
                    List.contains optionName optionNames
                    && value.StartsWith("--", StringComparison.Ordinal)
                then
                    Internal.fail "Option benoetigt einen Wert.")

            let workspace, remaining = takeOption "--workspace" arguments
            let root = workspace |> Option.defaultValue Environment.CurrentDirectory

            if not (workspaceRootIsSafe root) then
                raise (CalibrationSpecError "UNSAFE_PATH")

            match remaining with
            | "validate-spec" :: options ->
                let specPath, rest = requireOption "--spec" options
                noArguments (namespaceName + " validate-spec") rest
                let validated = BlenderCalibration.validateSpecFile root specPath

                writeCalibrationEnvelope command (fun writer ->
                    writer.WriteBoolean("ok", true)
                    writer.WritePropertyName("result")
                    writer.WriteStartObject()
                    writer.WriteNumber("familyDecodedGeometryBytes", validated.FamilyDecodedGeometryBytes)
                    writer.WriteString("familyId", validated.Spec.FamilyId)
                    writer.WriteNumber("moduleCount", validated.Modules.Length)
                    writer.WriteString("profile", validated.Spec.Profile)
                    writer.WriteNumber("renderPrimitiveCount", validated.RenderPrimitiveCount)
                    writer.WriteString("specPath", specPath)
                    writer.WriteString("specSha256", validated.SpecSha256)
                    writer.WriteEndObject())

                0
            | "inspect" :: options ->
                let specPath, rest = requireOption "--spec" options
                let glbPath, rest = requireOption "--glb" rest
                let previewPath, rest = requireOption "--preview" rest
                let reportPath, rest = requireOption "--report" rest
                noArguments (namespaceName + " inspect") rest
                let validated = BlenderCalibration.validateSpecFile root specPath

                let inspected =
                    Asset3dInspector.inspect root validated glbPath previewPath reportPath

                writeCalibrationEnvelope command (fun writer ->
                    writer.WriteBoolean("ok", true)
                    writer.WritePropertyName("result")
                    writer.WriteStartObject()
                    writer.WriteNumber("familyDecodedGeometryBytes", inspected.DecodedGeometryBytes)
                    writer.WriteString("familyId", inspected.FamilyId)
                    writer.WriteNumber("glbBytes", inspected.GlbBytes)
                    writer.WriteString("glbPath", inspected.GlbPath)
                    writer.WriteString("glbSha256", inspected.GlbSha256)
                    writer.WriteNumber("materialCount", inspected.MaterialCount)
                    writer.WriteNumber("moduleCount", validated.Modules.Length)
                    writer.WriteNumber("previewBytes", inspected.PreviewBytes)
                    writer.WriteString("previewPath", inspected.PreviewPath)
                    writer.WriteString("previewSha256", inspected.PreviewSha256)
                    writer.WriteNumber("renderPrimitiveCount", inspected.RenderPrimitiveCount)
                    writer.WriteNumber("reportBytes", inspected.ReportBytes)
                    writer.WriteString("reportPath", inspected.ReportPath)
                    writer.WriteString("reportSha256", inspected.ReportSha256)
                    writer.WriteString("specPath", specPath)
                    writer.WriteString("specSha256", inspected.SpecSha256)
                    writer.WriteEndObject())

                0
            | "generate" :: options when allowMutation ->
                let specPath, rest = requireOption "--spec" options
                let jobId, rest = requireOption "--job-id" rest
                noArguments (namespaceName + " generate") rest

                using (new CancellationTokenSource(TimeSpan.FromSeconds(300.0))) (fun cancellation ->
                    let generated =
                        DotnetAssetPipeline.generateWithCancellation
                            root
                            specPath
                            jobId
                            AssetGeneratorActor
                            cancellation.Token

                    writeCalibrationEnvelope command (fun writer ->
                        writer.WriteBoolean("ok", true)
                        writer.WritePropertyName("result")
                        writer.WriteStartObject()
                        writer.WriteString("assetId", generated.AssetId)
                        writer.WriteString("glbSha256", generated.GlbSha256)
                        writer.WriteString("jobId", generated.JobId)
                        writer.WriteString("manifestPath", generated.ManifestPath)
                        writer.WriteString("manifestSha256", generated.ManifestSha256)
                        writer.WriteString("previewSha256", generated.PreviewSha256)
                        writer.WriteString("receiptPath", generated.ReceiptPath)
                        writer.WriteString("receiptSha256", generated.ReceiptSha256)
                        writer.WriteString("reportSha256", generated.ReportSha256)
                        writer.WriteString("specPath", generated.SpecPath)
                        writer.WriteString("specSha256", generated.SpecSha256)
                        writer.WriteEndObject()))

                0
            | "recover" :: options when allowMutation ->
                let jobId, rest = requireOption "--job-id" options
                noArguments (namespaceName + " recover") rest
                let recovered = DotnetAssetPipeline.recover root jobId

                writeCalibrationEnvelope command (fun writer ->
                    writer.WriteBoolean("ok", true)
                    writer.WritePropertyName("result")
                    writer.WriteStartObject()
                    writer.WriteString("jobId", recovered.JobId)
                    writer.WriteString("state", recovered.State)
                    writer.WriteEndObject())

                0
            | _ -> calibrationError command "INVALID_ARGUMENT" "invalid arguments" 2
        with
        | DotnetAssetPipelineError(code, message, exitCode) -> calibrationError command code message exitCode
        | CalibrationSpecError code when code = "UNSAFE_PATH" -> calibrationError command "UNSAFE_PATH" "unsafe path" 2
        | CalibrationSpecError _ -> calibrationError command "INVALID_SPEC" "validation failed" 2
        | AssetInspectionPathError _ -> calibrationError command "UNSAFE_PATH" "unsafe path" 2
        | AssetInspectionError code when code = "BUDGET_EXCEEDED" ->
            calibrationError command "BUDGET_EXCEEDED" "budget exceeded" 5
        | AssetInspectionError _ -> calibrationError command "INVALID_ARTIFACT" "artifact validation failed" 5
        | HarnessException _ -> calibrationError command "INVALID_ARGUMENT" "invalid arguments" 2
        | _ -> calibrationError command "INTERNAL_ERROR" "internal error" 8

    let usage =
        """RiftHarness - lokales Agent-Gedaechtnis, BM25-RAG, Provenienz und Retention

Aufruf:
  riftharness init [--workspace PATH]
  riftharness start-run [--actor ACTOR] [--task T-###] [--model ID] [--prompt-file FILE] [--toolchain-file FILE] [--workspace PATH]
  riftharness append-event RUN_ID --type TYPE --payload-file FILE [--workspace PATH]
  riftharness append-evidence RUN_ID --criterion AC-ID --kind KIND --result-file FILE [--trace-id ID] [--span-id ID] [--command CMD] [--exit-code N] [--duration-ms N] [--artifact PATH]... [--workspace PATH]
  riftharness finish-run RUN_ID [--status succeeded|failed|cancelled] [--summary-file FILE] [--workspace PATH]
  riftharness memory propose|validate|accept|supersede|set-status|status ...
  riftharness build-rag [--workspace PATH]
  riftharness query-rag --query TEXT [--top N] [--run RUN_ID] [--criterion AC-ID] [--trace-id ID] [--span-id ID] [--workspace PATH]
  riftharness assets-check [--manifest FILE] [--require-local] [--require-approved] [--workspace PATH]
  riftharness export-generation-receipt RUN_ID --manifest FILE --output FILE [--workspace PATH]
  riftharness asset-calibration validate-spec|inspect|generate|recover ...
  riftharness asset-ci-evidence --output FILE --test-report-sha256 SHA256 [--workspace PATH]
  riftharness blender-calibration validate-spec|inspect ...  (historischer Read-only-Alias)
  riftharness retention-plan [--now UTC] [--workspace PATH]
  riftharness retention-execute --plan-file FILE --confirm-plan-sha256 SHA256 [--now UTC] [--workspace PATH]
  riftharness verify [--run RUN_ID] [--workspace PATH]
"""

    let private executeStandard arguments =
        let workspace, withoutWorkspace = takeOption "--workspace" arguments
        let root = workspace |> Option.defaultValue Environment.CurrentDirectory

        let parseNowUtc (nowText: string option) =
            match nowText with
            | None -> DateTimeOffset.UtcNow
            | Some text ->
                match DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) with
                | true, parsed when parsed.Offset = TimeSpan.Zero -> parsed.ToUniversalTime()
                | _ -> Internal.fail "--now muss einen UTC-Zeitstempel enthalten."

        let writeRunTempPayload runId prefix (bytes: byte array) =
            let locations = Workspace.requireInitialized root
            let workDirectory = Path.Combine(locations.Runs, runId, "work")
            Directory.CreateDirectory(workDirectory) |> ignore

            let path = Path.Combine(workDirectory, $"{prefix}-{Guid.NewGuid():N}.json")

            File.WriteAllBytes(path, bytes)
            path

        let deleteQuietly (path: string) =
            try
                File.Delete(path)
            with _ ->
                ()


        match withoutWorkspace with
        | []
        | [ "--help" ]
        | [ "-h" ] ->
            Console.Out.Write(usage)
            0
        | command :: rest when command = "init" ->
            noArguments command rest
            let locations = Workspace.initialize root
            HarnessConfig.load locations |> ignore

            jsonResult (fun writer -> writer.WriteBoolean("initialized", true))
            |> Console.Out.WriteLine

            0
        | command :: rest when command = "start-run" ->
            let actorId, rest = takeOption "--actor" rest
            let taskId, rest = takeOption "--task" rest
            let modelId, rest = takeOption "--model" rest
            let promptFile, rest = takeOption "--prompt-file" rest
            let toolchainFile, rest = takeOption "--toolchain-file" rest
            noArguments command rest

            let inputs: Provenance.StartInputs =
                { ActorId = actorId |> Option.defaultValue "unspecified-agent"
                  TaskId = taskId
                  ModelId = modelId
                  PromptFile = promptFile
                  ToolchainFile = toolchainFile }

            RunStore.startProvenanced root inputs.ActorId inputs |> Console.Out.WriteLine

            0
        | command :: runId :: rest when command = "append-event" ->
            let eventType, rest = requireOption "--type" rest
            let payloadFile, rest = requireOption "--payload-file" rest
            noArguments command rest
            let receipt = RunStore.append root runId eventType payloadFile

            jsonResult (fun writer ->
                writer.WriteString("runId", receipt.RunId)
                writer.WriteNumber("sequence", receipt.Sequence)
                writer.WriteString("eventHash", receipt.EventHash))
            |> Console.Out.WriteLine

            0
        | command :: runId :: rest when command = "append-evidence" ->
            let traceIdText, rest = takeOption "--trace-id" rest
            let spanIdText, rest = takeOption "--span-id" rest
            let criterionId, rest = requireOption "--criterion" rest
            let kind, rest = requireOption "--kind" rest
            let resultFile, rest = requireOption "--result-file" rest
            let evidenceCommand, rest = takeOption "--command" rest
            let exitCodeText, rest = takeOption "--exit-code" rest
            let durationMsText, rest = takeOption "--duration-ms" rest
            let artifactPaths, leftoverArguments = collectOptions "--artifact" rest
            noArguments command leftoverArguments

            let locations = Workspace.requireInitialized root
            let config = HarnessConfig.load locations

            let traceId, spanId =
                match traceIdText, spanIdText with
                | Some value1, Some value2 -> value1, value2
                | None, None -> Provenance.newTraceId (), Provenance.newSpanId ()
                | _ -> Internal.fail "--trace-id und --span-id muessen gemeinsam gesetzt oder gemeinsam fehlen."

            // Ergebnis einlesen, redigieren, kanonisieren und per Hash binden.
            let resultBytes =
                Internal.safeReadAllText resultFile config.MaxEventPayloadBytes
                |> Internal.canonicalJsonWithRedaction config.Redaction

            // Artefakte pfadsicher, existenz- und hashgebunden aufnehmen.
            let hashedArtifacts =
                artifactPaths
                |> List.map (fun artifactPath ->
                    let absolute =
                        if Path.IsPathRooted(artifactPath) then
                            artifactPath
                        else
                            Path.Combine(root, artifactPath)

                    let safePath = Workspace.requireSafePath locations "Evidenz-Artefakt" false absolute

                    let relative = Workspace.relativePath locations safePath

                    Provenance.validateArtifactPath locations relative |> ignore

                    if not (File.Exists(safePath)) then
                        Internal.fail $"Artefakt fehlt: {relative}"

                    relative, Internal.sha256File safePath)

            let payloadBytes =
                Internal.jsonBytes false (fun writer ->
                    writer.WriteStartObject()
                    writer.WriteString("traceId", traceId)
                    writer.WriteString("spanId", spanId)
                    writer.WriteString("criterionId", criterionId)
                    writer.WriteString("kind", kind)

                    match evidenceCommand with
                    | Some value -> writer.WriteString("command", value)
                    | None -> ()

                    match exitCodeText with
                    | Some text ->
                        match Int64.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture) with
                        | true, code when code >= 0L && code <= 4096L -> writer.WriteNumber("exitCode", code)
                        | _ -> Internal.fail "--exit-code muss eine Ganzzahl zwischen 0 und 4096 sein."
                    | None -> ()

                    match durationMsText with
                    | Some text ->
                        match Int64.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture) with
                        | true, duration when duration >= 0L -> writer.WriteNumber("durationMs", duration)
                        | _ -> Internal.fail "--duration-ms darf keine negative Ganzzahl sein."
                    | None -> ()

                    writer.WriteStartArray("artifacts")

                    for relative, artifactHash in hashedArtifacts do
                        writer.WriteStartObject()
                        writer.WriteString("path", relative)
                        writer.WriteString("sha256", artifactHash)
                        writer.WriteEndObject()

                    writer.WriteEndArray()
                    Internal.rawJson writer "result" resultBytes

                    writer.WriteString("resultSha256", Internal.sha256Hex resultBytes)

                    writer.WriteEndObject())

            if int64 payloadBytes.LongLength > config.MaxEventPayloadBytes then
                Internal.fail
                    $"Evidenz-Payload ueberschreitet logging.maxEventPayloadBytes ({config.MaxEventPayloadBytes} Bytes)."

            let tempPayloadPath = writeRunTempPayload runId "evidence" payloadBytes

            try
                let receipt = RunStore.append root runId "evidence.recorded" tempPayloadPath

                jsonResult (fun writer ->
                    writer.WriteString("criterionId", criterionId)
                    writer.WriteString("eventHash", receipt.EventHash)
                    writer.WriteNumber("sequence", receipt.Sequence)
                    writer.WriteString("runId", receipt.RunId)
                    writer.WriteString("spanId", spanId)
                    writer.WriteString("traceId", traceId))
                |> Console.Out.WriteLine

                0
            finally
                deleteQuietly tempPayloadPath
        | command :: runId :: rest when command = "finish-run" ->
            let status, rest = takeOption "--status" rest
            let summaryFile, rest = takeOption "--summary-file" rest
            noArguments command rest

            let receipt =
                RunStore.finish root runId (status |> Option.defaultValue "succeeded") summaryFile

            jsonResult (fun writer ->
                writer.WriteString("runId", receipt.RunId)
                writer.WriteString("status", receipt.Status)
                writer.WriteNumber("eventCount", receipt.EventCount)
                writer.WriteString("finalEventHash", receipt.FinalEventHash)
                writer.WriteString("summaryHash", receipt.SummaryHash))
            |> Console.Out.WriteLine

            0
        | [ command; subcommand ] when command = "memory" && subcommand = "validate" ->
            let receipt = MemoryStore.validate root

            jsonResult (fun writer ->
                writer.WriteNumber("recordCount", receipt.RecordCount)
                writer.WriteNumber("chainedRecordCount", receipt.ChainedRecordCount)

                match receipt.LastRecordHash with
                | Some hash -> writer.WriteString("lastRecordHash", hash)
                | None -> writer.WriteNull("lastRecordHash"))
            |> Console.Out.WriteLine

            0
        | [ command; subcommand ] when command = "memory" && subcommand = "status" ->
            MemoryStore.status root |> MemoryStore.statusJson |> Console.Out.WriteLine
            0
        | command :: subcommand :: rest when command = "memory" && subcommand = "propose" ->
            let recordFile, rest = requireOption "--record-file" rest
            noArguments "memory propose" rest
            let receipt = MemoryStore.propose root recordFile

            jsonResult (fun writer ->
                writer.WriteString("id", receipt.Id)
                writer.WriteString("status", receipt.Status)
                writer.WriteString("recordHash", receipt.RecordHash))
            |> Console.Out.WriteLine

            0
        | command :: subcommand :: recordId :: rest when command = "memory" && subcommand = "accept" ->
            let newId, rest = requireOption "--new-id" rest
            let actor, rest = requireOption "--actor" rest
            noArguments "memory accept" rest
            let receipt = MemoryStore.accept root recordId newId actor

            jsonResult (fun writer ->
                writer.WriteString("id", receipt.Id)
                writer.WriteString("status", receipt.Status)
                writer.WriteString("previousId", recordId)
                writer.WriteString("recordHash", receipt.RecordHash))
            |> Console.Out.WriteLine

            0
        | command :: subcommand :: recordId :: rest when command = "memory" && subcommand = "supersede" ->
            let proposalId, rest = requireOption "--with" rest
            let newId, rest = requireOption "--new-id" rest
            let actor, rest = requireOption "--actor" rest
            noArguments "memory supersede" rest
            let receipt = MemoryStore.supersede root recordId proposalId newId actor

            jsonResult (fun writer ->
                writer.WriteString("id", receipt.Id)
                writer.WriteString("status", receipt.Status)
                writer.WriteString("previousId", recordId)
                writer.WriteString("proposalId", proposalId)
                writer.WriteString("recordHash", receipt.RecordHash))
            |> Console.Out.WriteLine

            0
        | command :: subcommand :: recordId :: rest when command = "memory" && subcommand = "set-status" ->
            let status, rest = requireOption "--status" rest
            let newId, rest = requireOption "--new-id" rest
            let actor, rest = requireOption "--actor" rest
            noArguments "memory set-status" rest
            let receipt = MemoryStore.setStatus root recordId newId status actor

            jsonResult (fun writer ->
                writer.WriteString("id", receipt.Id)
                writer.WriteString("status", receipt.Status)
                writer.WriteString("previousId", recordId)
                writer.WriteString("recordHash", receipt.RecordHash))
            |> Console.Out.WriteLine

            0
        | command :: rest when command = "build-rag" ->
            noArguments command rest
            let receipt = RagIndex.build root

            jsonResult (fun writer ->
                writer.WriteNumber("sourceCount", receipt.SourceCount)
                writer.WriteNumber("chunkCount", receipt.ChunkCount)
                writer.WriteString("indexHash", receipt.IndexHash)
                writer.WriteString("indexPath", receipt.IndexPath)
                writer.WriteString("manifestPath", receipt.ManifestPath)
                writer.WriteString("manifestSha256", receipt.ManifestHash))
            |> Console.Out.WriteLine

            0
        | command :: rest when command = "query-rag" ->
            let traceRun, rest = takeOption "--run" rest
            let criterionId, rest = takeOption "--criterion" rest
            let spanTraceId, rest = takeOption "--trace-id" rest
            let spanIdText, rest = takeOption "--span-id" rest
            let topText, queryParts = takeOption "--top" rest
            let queryOption, positionalQuery = takeOption "--query" queryParts

            match criterionId, traceRun with
            | Some _, None -> Internal.fail "--criterion erfordert --run."
            | _ -> ()

            let top =
                match topText with
                | None -> RagIndex.defaultTop root
                | Some value ->
                    match Int32.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture) with
                    | true, parsed -> parsed
                    | _ -> Internal.fail "--top muss eine Ganzzahl sein."

            let query =
                match queryOption, positionalQuery with
                | Some value, [] -> value
                | Some _, _ -> Internal.fail "Query entweder mit --query oder positional angeben, nicht beides."
                | None, values -> String.concat " " values

            let response = RagIndex.query root query top

            let recorded =
                traceRun |> Option.map (fun runId -> RetrievalStore.record root runId response)

            // Mit gebundener Span-Huelle zusaetzlich ein retrieval.recorded-Ereignis verankern.
            match recorded, criterionId with
            | Some trace, Some criterion ->
                let boundTraceId, boundSpanId =
                    match spanTraceId, spanIdText with
                    | Some value1, Some value2 -> value1, value2
                    | None, None -> Provenance.newTraceId (), Provenance.newSpanId ()
                    | _ -> Internal.fail "--trace-id und --span-id muessen gemeinsam gesetzt oder gemeinsam fehlen."

                let eventPayloadBytes =
                    Internal.jsonBytes false (fun writer ->
                        writer.WriteStartObject()
                        writer.WriteString("traceId", boundTraceId)
                        writer.WriteString("spanId", boundSpanId)
                        writer.WriteString("criterionId", criterion)
                        writer.WriteString("indexSha256", response.IndexSha256)
                        writer.WriteNumber("sequence", trace.Sequence)
                        writer.WriteString("traceHash", trace.TraceHash)
                        writer.WriteString("queryId", trace.QueryId)
                        writer.WriteEndObject())

                let runIdForEvent = traceRun |> Option.get

                let tempPayloadPath =
                    writeRunTempPayload runIdForEvent "retrieval" eventPayloadBytes

                try
                    RunStore.append root runIdForEvent "retrieval.recorded" tempPayloadPath
                    |> ignore
                finally
                    deleteQuietly tempPayloadPath
            | Some _, None -> ()
            | None, _ -> ()

            { response with Trace = recorded }
            |> RagIndex.queryJson
            |> Console.Out.WriteLine

            0
        | command :: rest when command = "assets-check" ->
            let manifest, rest = takeOption "--manifest" rest
            let requireLocal, rest = takeFlag "--require-local" rest
            let requireApproved, rest = takeFlag "--require-approved" rest
            noArguments command rest

            let report =
                AssetStore.check
                    root
                    { ManifestPath = manifest
                      RequireLocal = requireLocal
                      RequireApproved = requireApproved }

            report |> AssetStore.reportJson |> Console.Out.WriteLine
            if report.Valid then 0 else 2
        | command :: rest when command = "asset-ci-evidence" ->
            let output, rest = requireOption "--output" rest
            let suiteReportSha256, rest = requireOption "--test-report-sha256" rest
            noArguments command rest

            try
                if
                    String.IsNullOrWhiteSpace(output)
                    || output.Contains('\\')
                    || Path.IsPathFullyQualified(output)
                    || output <> "artifacts/t007/dotnet-asset-calibration.json"
                then
                    raise (DotnetAssetCiEvidenceError "UNSAFE_PATH")

                let locations = Workspace.paths root

                let outputPath =
                    Workspace.requireSafePath locations "CI-Evidenzausgabe" true (Path.Combine(root, output))

                if File.Exists(outputPath) || Directory.Exists(outputPath) then
                    raise (DotnetAssetCiEvidenceError "TRANSACTION_CONFLICT")

                let evidence =
                    DotnetAssetCiEvidence.generateWithSuiteReport locations.Root suiteReportSha256

                Internal.atomicWrite outputPath evidence.CanonicalJson

                jsonResult (fun writer ->
                    writer.WriteString("evidencePath", output)
                    writer.WriteString("evidenceSha256", evidence.Sha256))
                |> Console.Out.WriteLine

                0
            with DotnetAssetCiEvidenceError code ->
                let message =
                    if code = "UNSUPPORTED_RUNTIME" then
                        "unsupported runtime"
                    else
                        "evidence failed"

                Console.Error.WriteLine($"RiftHarness: {message} ({code})")
                2
        | command :: runId :: rest when command = "export-generation-receipt" ->
            let manifest, rest = requireOption "--manifest" rest
            let output, rest = requireOption "--output" rest
            noArguments command rest
            let receipt = AssetStore.exportGenerationReceipt root runId manifest output

            jsonResult (fun writer ->
                writer.WriteString("runId", receipt.RunId)
                writer.WriteString("assetId", receipt.AssetId)
                writer.WriteString("receiptPath", receipt.ReceiptPath)
                writer.WriteString("receiptSha256", receipt.ReceiptSha256))
            |> Console.Out.WriteLine

            0
        | command :: rest when command = "toolchain-check" ->
            let explicitRoot, rest = takeOption "--workspace" rest
            noArguments command rest
            let checkRoot = explicitRoot |> Option.defaultValue root
            let report = ToolchainCheck.check checkRoot
            let reportText = ToolchainCheck.reportJson report
            Console.Out.WriteLine(reportText)
            if report.Valid then 0 else 2
        | command :: rest when command = "verify" ->
            let requestedRun, rest = takeOption "--run" rest
            noArguments command rest
            let report = Verification.verify root requestedRun
            report |> Verification.reportJson |> Console.Out.WriteLine
            if report.Valid then 0 else 2
        | command :: rest when command = "retention-plan" ->
            let nowText, rest = takeOption "--now" rest
            noArguments command rest
            let nowUtc = parseNowUtc nowText

            let planFileBytes, _ = Retention.planBytes root nowUtc

            Constants.Utf8NoBom.GetString(planFileBytes) |> Console.Out.WriteLine

            0
        | command :: rest when command = "retention-execute" ->
            let planFilePath, rest = requireOption "--plan-file" rest
            let confirmHash, rest = requireOption "--confirm-plan-sha256" rest
            let nowText, rest = takeOption "--now" rest
            noArguments command rest
            let nowUtc = parseNowUtc nowText
            let receipt = Retention.execute root planFilePath confirmHash nowUtc

            jsonResult (fun writer ->
                writer.WriteStartArray("deletedRunIds")

                for runId in receipt.DeletedRunIds do
                    writer.WriteStringValue(runId)

                writer.WriteEndArray()
                writer.WriteNumber("consideredCount", receipt.ConsideredCount)
                writer.WriteString("executedAtUtc", receipt.ExecutedAtUtc)
                writer.WriteString("planSha256", receipt.PlanSha256))
            |> Console.Out.WriteLine

            0
        | command :: _ -> Internal.fail $"Unbekannter oder unvollstaendiger Befehl: {command}"

    let execute arguments =
        match arguments with
        | "asset-calibration" :: remaining -> executeAssetCalibration "asset-calibration" true remaining
        | "blender-calibration" :: remaining -> executeAssetCalibration "blender-calibration" false remaining
        | _ -> executeStandard arguments

module Program =
    [<EntryPoint>]
    let main argv =
        try
            argv |> Array.toList |> Cli.execute
        with
        | HarnessException message ->
            Console.Error.WriteLine($"RiftHarness: {message}")
            2
        | error ->
            Console.Error.WriteLine($"RiftHarness: unerwarteter Fehler: {error.Message}")
            3
