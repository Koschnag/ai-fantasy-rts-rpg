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
        """RiftHarness - lokales Agent-Gedaechtnis und BM25-RAG

Aufruf:
  riftharness init [--workspace PATH]
  riftharness start-run [--actor ACTOR] [--workspace PATH]
  riftharness append-event RUN_ID --type TYPE --payload-file FILE [--workspace PATH]
  riftharness finish-run RUN_ID [--status succeeded|failed|cancelled] [--summary-file FILE] [--workspace PATH]
  riftharness memory propose --record-file FILE [--workspace PATH]
  riftharness memory validate [--workspace PATH]
  riftharness memory accept RECORD_ID --new-id ID --actor ACTOR [--workspace PATH]
  riftharness memory supersede RECORD_ID --with PROPOSAL_ID --new-id ID --actor ACTOR [--workspace PATH]
  riftharness memory set-status RECORD_ID --status stale|rejected --new-id ID --actor ACTOR [--workspace PATH]
  riftharness memory status [--workspace PATH]
  riftharness build-rag [--workspace PATH]
  riftharness query-rag --query TEXT [--top N] [--run RUN_ID] [--workspace PATH]
  riftharness assets-check [--manifest FILE] [--require-local] [--require-approved] [--workspace PATH]
  riftharness export-generation-receipt RUN_ID --manifest FILE --output FILE [--workspace PATH]
  riftharness asset-calibration validate-spec --spec FILE [--workspace PATH]
  riftharness asset-calibration inspect --spec FILE --glb FILE --preview FILE --report FILE [--workspace PATH]
  riftharness asset-calibration generate --spec FILE --job-id ULID [--workspace PATH]
  riftharness asset-calibration recover --job-id ULID [--workspace PATH]
  riftharness blender-calibration validate-spec|inspect ...  (historischer Read-only-Alias)
  riftharness verify [--run RUN_ID] [--workspace PATH]
"""

    let private executeStandard arguments =
        let workspace, withoutWorkspace = takeOption "--workspace" arguments
        let root = workspace |> Option.defaultValue Environment.CurrentDirectory

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
            noArguments command rest

            match actorId with
            | Some actor -> RunStore.startForActor root actor
            | None -> RunStore.start root
            |> Console.Out.WriteLine

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
                writer.WriteString("indexPath", receipt.IndexPath))
            |> Console.Out.WriteLine

            0
        | command :: rest when command = "query-rag" ->
            let traceRun, rest = takeOption "--run" rest
            let topText, queryParts = takeOption "--top" rest
            let queryOption, positionalQuery = takeOption "--query" queryParts

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
        | command :: rest when command = "verify" ->
            let requestedRun, rest = takeOption "--run" rest
            noArguments command rest
            let report = Verification.verify root requestedRun
            report |> Verification.reportJson |> Console.Out.WriteLine
            if report.Valid then 0 else 2
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
