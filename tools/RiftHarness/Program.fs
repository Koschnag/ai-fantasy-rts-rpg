namespace RiftHarness

open System
open System.Globalization

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
  riftharness verify [--run RUN_ID] [--workspace PATH]
"""

    let execute arguments =
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
