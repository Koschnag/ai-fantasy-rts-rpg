namespace RiftHarness

open System
open System.Globalization

module private Cli =
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
  riftharness start-run [--workspace PATH]
  riftharness append-event RUN_ID --type TYPE --payload-file FILE [--workspace PATH]
  riftharness finish-run RUN_ID [--status succeeded|failed|cancelled] [--summary-file FILE] [--workspace PATH]
  riftharness build-rag [--workspace PATH]
  riftharness query-rag --query TEXT [--top N] [--workspace PATH]
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
            noArguments command rest
            RunStore.start root |> Console.Out.WriteLine
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

            RagIndex.query root query top |> RagIndex.queryJson |> Console.Out.WriteLine
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
