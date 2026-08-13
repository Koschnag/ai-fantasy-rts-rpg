namespace RiftHarness

open System
open System.Collections.Generic
open System.IO
open System.Text.Json

type StoredEvent =
    { SchemaVersion: int
      RunId: string
      Sequence: int64
      TimestampUtc: string
      EventType: string
      PreviousEventHash: string option
      Payload: byte array
      EventHash: string }

type EventReceipt =
    { RunId: string
      Sequence: int64
      EventHash: string }

type RunFinishReceipt =
    { RunId: string
      Status: string
      EventCount: int64
      FinalEventHash: string
      SummaryHash: string }

type private RunMetadata =
    { RunId: string
      StartedAtUtc: string
      Status: string
      FinishedAtUtc: string option }

[<RequireQualifiedAccess>]
module RunStore =
    let private terminalStatuses = set [ "succeeded"; "failed"; "cancelled" ]

    let private validateObjectFields
        (description: string)
        (allowed: Set<string>)
        (required: Set<string>)
        (element: JsonElement)
        =
        if element.ValueKind <> JsonValueKind.Object then
            Internal.fail $"{description} muss ein JSON-Objekt sein."

        let seen = HashSet<string>(StringComparer.Ordinal)

        for property in element.EnumerateObject() do
            if not (seen.Add(property.Name)) then
                Internal.fail $"{description} enthaelt das Feld '{property.Name}' mehrfach."

            if not (allowed.Contains(property.Name)) then
                Internal.fail $"{description} enthaelt das unerlaubte Feld '{property.Name}'."

        for field in required do
            if not (seen.Contains(field)) then
                Internal.fail $"{description}: JSON-Feld '{field}' fehlt."

    let private validateEventType (eventType: string) =
        if String.IsNullOrWhiteSpace(eventType) || eventType.Length > 100 then
            Internal.fail "Event-Typ muss 1 bis 100 Zeichen lang sein."

        if eventType |> Seq.exists Char.IsControl then
            Internal.fail "Event-Typ darf keine Steuerzeichen enthalten."

    let private ensurePayloadLimit limit (payload: byte array) =
        if int64 payload.LongLength > limit then
            Internal.fail $"Event-Payload ist groesser als das erlaubte Limit von {limit} Bytes."

        payload

    let private ensureEventPayloadObject (payload: byte array) =
        try
            use document = JsonDocument.Parse(payload)

            if document.RootElement.ValueKind <> JsonValueKind.Object then
                Internal.fail "Event-Payload muss ein JSON-Objekt sein."

            payload
        with :? JsonException as error ->
            Internal.fail $"Event-Payload ist ungueltiges JSON: {error.Message}"

    let private runDirectory (locations: WorkspacePaths) runId =
        if not (Internal.isRunId runId) then
            Internal.fail "Run-ID muss eine 26-stellige Crockford-Base32-ID sein."

        Path.Combine(locations.Runs, runId)

    let private metadataBytes (metadata: RunMetadata) =
        Internal.jsonBytes true (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", Constants.SchemaVersion)
            writer.WriteString("runId", metadata.RunId)
            writer.WriteString("startedAtUtc", metadata.StartedAtUtc)
            writer.WriteString("status", metadata.Status)

            match metadata.FinishedAtUtc with
            | Some timestamp -> writer.WriteString("finishedAtUtc", timestamp)
            | None -> ()

            writer.WriteEndObject())

    let private loadMetadata runPath =
        let path = Path.Combine(runPath, "run.json")

        if not (File.Exists(path)) then
            Internal.fail $"Run-Metadaten fehlen: {path}"

        try
            use document = JsonDocument.Parse(File.ReadAllBytes(path))
            let root = document.RootElement

            validateObjectFields
                "run.json"
                (set
                    [ "$schema"
                      "schemaVersion"
                      "runId"
                      "startedAtUtc"
                      "status"
                      "finishedAtUtc" ])
                (set [ "schemaVersion"; "runId"; "startedAtUtc"; "status" ])
                root

            let schemaVersion = Internal.requiredInt "schemaVersion" root

            if schemaVersion <> Constants.SchemaVersion then
                Internal.fail $"Nicht unterstuetzte Run-Schema-Version: {schemaVersion}."

            let finished =
                match root.TryGetProperty("finishedAtUtc") with
                | true, value when value.ValueKind = JsonValueKind.String -> Some(value.GetString())
                | true, _ -> Internal.fail "Run-Feld 'finishedAtUtc' muss eine Zeichenfolge sein."
                | _ -> None

            { RunId = Internal.requiredString "runId" root
              StartedAtUtc = Internal.requiredString "startedAtUtc" root
              Status = Internal.requiredString "status" root
              FinishedAtUtc = finished }
        with :? JsonException as error ->
            Internal.fail $"Ungueltige Run-Metadaten: {error.Message}"

    let private eventCoreBytes
        (schemaVersion: int)
        (runId: string)
        (sequence: int64)
        (timestampUtc: string)
        (eventType: string)
        (previousEventHash: string option)
        (payload: byte array)
        =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", schemaVersion)
            writer.WriteString("runId", runId)
            writer.WriteNumber("sequence", sequence)
            writer.WriteString("timestampUtc", timestampUtc)
            writer.WriteString("type", eventType)

            match previousEventHash with
            | Some hash -> writer.WriteString("previousEventHash", hash)
            | None -> writer.WriteNull("previousEventHash")

            Internal.rawJson writer "payload" payload
            writer.WriteEndObject())

    let private eventLineBytes (event: StoredEvent) =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", event.SchemaVersion)
            writer.WriteString("runId", event.RunId)
            writer.WriteNumber("sequence", event.Sequence)
            writer.WriteString("timestampUtc", event.TimestampUtc)
            writer.WriteString("type", event.EventType)

            match event.PreviousEventHash with
            | Some hash -> writer.WriteString("previousEventHash", hash)
            | None -> writer.WriteNull("previousEventHash")

            Internal.rawJson writer "payload" event.Payload
            writer.WriteString("eventHash", event.EventHash)
            writer.WriteEndObject())

    let private parseEvent (line: string) =
        try
            use document = JsonDocument.Parse(line)
            let root = document.RootElement

            let eventFields =
                set
                    [ "schemaVersion"
                      "runId"
                      "sequence"
                      "timestampUtc"
                      "type"
                      "previousEventHash"
                      "payload"
                      "eventHash" ]

            validateObjectFields "Event" eventFields eventFields root
            let previous = Internal.requiredProperty "previousEventHash" root

            let payload = Internal.requiredProperty "payload" root

            if payload.ValueKind <> JsonValueKind.Object then
                Internal.fail "Event-Feld 'payload' muss ein JSON-Objekt sein."

            let eventType = Internal.requiredString "type" root
            validateEventType eventType

            let previousHash =
                match previous.ValueKind with
                | JsonValueKind.Null -> None
                | JsonValueKind.String -> Some(previous.GetString())
                | _ -> Internal.fail "Event-Feld 'previousEventHash' muss String oder null sein."

            { SchemaVersion = Internal.requiredInt "schemaVersion" root
              RunId = Internal.requiredString "runId" root
              Sequence = Internal.requiredInt64 "sequence" root
              TimestampUtc = Internal.requiredString "timestampUtc" root
              EventType = eventType
              PreviousEventHash = previousHash
              Payload = payload |> Internal.canonicalElement
              EventHash = Internal.requiredString "eventHash" root }
        with :? JsonException as error ->
            Internal.fail $"Ungueltige Event-Zeile: {error.Message}"

    let rec private unsafeSecretPaths (policy: RedactionPolicy) prefix (element: JsonElement) =
        match element.ValueKind with
        | JsonValueKind.Object ->
            element.EnumerateObject()
            |> Seq.collect (fun property ->
                let path =
                    if String.IsNullOrEmpty(prefix) then
                        property.Name
                    else
                        prefix + "." + property.Name

                if Internal.isSensitivePropertyWithPolicy policy property.Name then
                    if
                        property.Value.ValueKind = JsonValueKind.String
                        && property.Value.GetString() = "[REDACTED]"
                    then
                        Seq.empty
                    else
                        Seq.singleton path
                else
                    unsafeSecretPaths policy path property.Value)
        | JsonValueKind.Array ->
            element.EnumerateArray()
            |> Seq.mapi (fun index child -> unsafeSecretPaths policy $"{prefix}[{index}]" child)
            |> Seq.concat
        | JsonValueKind.String when Internal.isSensitiveValue policy (element.GetString()) -> Seq.singleton prefix
        | _ -> Seq.empty

    let private validatePayloadRedaction policy (payload: byte array) =
        use document = JsonDocument.Parse(payload)
        unsafeSecretPaths policy "payload" document.RootElement |> Seq.toList

    let private loadEventsStrict policy eventsPath expectedRunId =
        if not (File.Exists(eventsPath)) then
            Internal.fail $"Event-Datei fehlt: {eventsPath}"

        let mutable expectedSequence = 1L
        let mutable expectedPrevious: string option = None
        let mutable previousTimestamp: DateTimeOffset option = None
        let events = ResizeArray<StoredEvent>()

        File.ReadLines(eventsPath, Constants.Utf8NoBom)
        |> Seq.filter (String.IsNullOrWhiteSpace >> not)
        |> Seq.iter (fun line ->
            let event = parseEvent line

            if event.SchemaVersion <> Constants.SchemaVersion then
                Internal.fail $"Event {event.Sequence}: falsche Schema-Version {event.SchemaVersion}."

            if event.RunId <> expectedRunId then
                Internal.fail $"Event {event.Sequence}: Run-ID stimmt nicht mit Verzeichnis ueberein."

            if event.Sequence <> expectedSequence then
                Internal.fail $"Event-Sequenz erwartet {expectedSequence}, gefunden {event.Sequence}."

            if event.PreviousEventHash <> expectedPrevious then
                Internal.fail $"Event {event.Sequence}: previousEventHash unterbricht die Hashkette."

            let timestamp =
                match Internal.tryParseUtc event.TimestampUtc with
                | Some value -> value
                | None -> Internal.fail $"Event {event.Sequence}: timestampUtc ist kein UTC-Zeitstempel."

            match previousTimestamp with
            | Some previous when timestamp < previous ->
                Internal.fail $"Event {event.Sequence}: Zeitstempel ist nicht monoton."
            | _ -> ()

            let expectedHash =
                eventCoreBytes
                    event.SchemaVersion
                    event.RunId
                    event.Sequence
                    event.TimestampUtc
                    event.EventType
                    event.PreviousEventHash
                    event.Payload
                |> Internal.sha256Hex

            if not (String.Equals(event.EventHash, expectedHash, StringComparison.Ordinal)) then
                Internal.fail $"Event {event.Sequence}: eventHash ist ungueltig."

            match validatePayloadRedaction policy event.Payload with
            | [] -> ()
            | unsafePaths ->
                let joinedPaths = String.concat ", " unsafePaths
                Internal.fail $"Event {event.Sequence}: nicht redigierte Geheimnisfelder: {joinedPaths}."

            events.Add(event)
            expectedSequence <- expectedSequence + 1L
            expectedPrevious <- Some event.EventHash
            previousTimestamp <- Some timestamp)

        events |> Seq.toList

    let private appendLocked
        (policy: RedactionPolicy)
        (runPath: string)
        (runId: string)
        (eventType: string)
        (payload: byte array)
        =
        validateEventType eventType
        ensureEventPayloadObject payload |> ignore
        let eventsPath = Path.Combine(runPath, "events.jsonl")
        let existing = loadEventsStrict policy eventsPath runId
        let previous = existing |> List.tryLast |> Option.map (fun event -> event.EventHash)
        let sequence = int64 existing.Length + 1L
        let timestamp = DateTimeOffset.UtcNow |> Internal.utcText

        let core =
            eventCoreBytes Constants.SchemaVersion runId sequence timestamp eventType previous payload

        let event =
            { SchemaVersion = Constants.SchemaVersion
              RunId = runId
              Sequence = sequence
              TimestampUtc = timestamp
              EventType = eventType
              PreviousEventHash = previous
              Payload = payload
              EventHash = Internal.sha256Hex core }

        let line = eventLineBytes event

        use stream =
            new FileStream(eventsPath, FileMode.Append, FileAccess.Write, FileShare.Read)

        stream.Write(line, 0, line.Length)
        stream.WriteByte(byte '\n')
        stream.Flush(true)

        event

    let private withRunLock runPath action =
        let lockPath = Path.Combine(runPath, ".write.lock")

        try
            use lockHandle =
                new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)

            action ()
        with :? IOException as error ->
            Internal.fail $"Run ist bereits fuer einen Schreibvorgang gesperrt: {error.Message}"

    let start root =
        let locations = Workspace.requireInitialized root
        HarnessConfig.load locations |> ignore
        let now = DateTimeOffset.UtcNow

        let rec reserve attempts =
            if attempts = 0 then
                Internal.fail "Es konnte keine eindeutige Run-ID reserviert werden."

            let runId = Internal.createRunId now
            let runPath = Path.Combine(locations.Runs, runId)

            if Directory.Exists(runPath) then
                reserve (attempts - 1)
            else
                Directory.CreateDirectory(runPath) |> ignore
                runId, runPath

        let runId, runPath = reserve 10

        let metadata =
            { RunId = runId
              StartedAtUtc = Internal.utcText now
              Status = "running"
              FinishedAtUtc = None }

        Internal.atomicWrite (Path.Combine(runPath, "run.json")) (metadataBytes metadata)
        Internal.atomicWrite (Path.Combine(runPath, "events.jsonl")) Array.empty
        runId

    let append root runId eventType payloadFile =
        let locations = Workspace.requireInitialized root
        let config = HarnessConfig.load locations
        let runPath = runDirectory locations runId

        if not (Directory.Exists(runPath)) then
            Internal.fail $"Run nicht gefunden: {runId}"

        let payloadText = Internal.safeReadAllText payloadFile config.MaxEventPayloadBytes

        let payload =
            Internal.canonicalJsonWithRedaction config.Redaction payloadText
            |> ensurePayloadLimit config.MaxEventPayloadBytes

        withRunLock runPath (fun () ->
            let metadata = loadMetadata runPath

            if metadata.Status <> "running" then
                Internal.fail $"Run {runId} ist bereits mit Status '{metadata.Status}' abgeschlossen."

            let event = appendLocked config.Redaction runPath runId eventType payload

            { RunId = runId
              Sequence = event.Sequence
              EventHash = event.EventHash })

    let private finishPayload (status: string) (summary: byte array) =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteString("status", status)
            Internal.rawJson writer "summary" summary
            writer.WriteEndObject())
        |> Constants.Utf8NoBom.GetString
        |> Internal.canonicalJson false

    let private summaryCoreBytes
        (runId: string)
        (startedAtUtc: string)
        (finishedAtUtc: string)
        (status: string)
        (eventCount: int64)
        (finalEventHash: string)
        (summary: byte array)
        =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", Constants.SchemaVersion)
            writer.WriteString("runId", runId)
            writer.WriteString("startedAtUtc", startedAtUtc)
            writer.WriteString("finishedAtUtc", finishedAtUtc)
            writer.WriteString("status", status)
            writer.WriteNumber("eventCount", eventCount)
            writer.WriteString("finalEventHash", finalEventHash)
            Internal.rawJson writer "summary" summary
            writer.WriteEndObject())

    let private summaryBytes (core: byte array) (summaryHash: string) =
        use document = JsonDocument.Parse(core)

        Internal.jsonBytes true (fun writer ->
            writer.WriteStartObject()

            for property in document.RootElement.EnumerateObject() do
                property.WriteTo(writer)

            writer.WriteString("summaryHash", summaryHash)
            writer.WriteEndObject())

    let finish root runId status summaryFile =
        if not (terminalStatuses.Contains(status)) then
            Internal.fail "Status muss 'succeeded', 'failed' oder 'cancelled' sein."

        let locations = Workspace.requireInitialized root
        let config = HarnessConfig.load locations
        let runPath = runDirectory locations runId

        if not (Directory.Exists(runPath)) then
            Internal.fail $"Run nicht gefunden: {runId}"

        let summary =
            match summaryFile with
            | Some path ->
                Internal.safeReadAllText path config.MaxEventPayloadBytes
                |> Internal.canonicalJsonWithRedaction config.Redaction
            | None -> Constants.Utf8NoBom.GetBytes("{}")

        let finishEventPayload =
            finishPayload status summary |> ensurePayloadLimit config.MaxEventPayloadBytes

        withRunLock runPath (fun () ->
            let metadata = loadMetadata runPath

            if metadata.Status <> "running" then
                Internal.fail $"Run {runId} ist bereits mit Status '{metadata.Status}' abgeschlossen."

            if metadata.RunId <> runId then
                Internal.fail "Run-ID in run.json stimmt nicht mit dem Verzeichnis ueberein."

            let event =
                appendLocked config.Redaction runPath runId "run.finished" finishEventPayload

            let finishedAt = event.TimestampUtc

            let core =
                summaryCoreBytes runId metadata.StartedAtUtc finishedAt status event.Sequence event.EventHash summary

            let summaryHash = Internal.sha256Hex core
            Internal.atomicWrite (Path.Combine(runPath, "summary.json")) (summaryBytes core summaryHash)

            let completedMetadata =
                { metadata with
                    Status = status
                    FinishedAtUtc = Some finishedAt }

            Internal.atomicWrite (Path.Combine(runPath, "run.json")) (metadataBytes completedMetadata)

            { RunId = runId
              Status = status
              EventCount = event.Sequence
              FinalEventHash = event.EventHash
              SummaryHash = summaryHash })

    let verifyRun root runId =
        let errors = ResizeArray<string>()

        let capture description action =
            try
                action ()
            with
            | HarnessException message -> errors.Add($"{description}: {message}")
            | error -> errors.Add($"{description}: {error.Message}")

        let locations = Workspace.paths root

        let config =
            try
                Some(HarnessConfig.load locations)
            with
            | HarnessException message ->
                errors.Add($"Konfiguration: {message}")
                None
            | error ->
                errors.Add($"Konfiguration: {error.Message}")
                None

        if not (Internal.isRunId runId) then
            errors.Add($"Run-Verzeichnis '{runId}' ist keine gueltige Run-ID.")
        else if config.IsSome then
            let runPath = Path.Combine(locations.Runs, runId)

            capture "Run-Pruefung" (fun () ->
                let metadata = loadMetadata runPath

                if metadata.RunId <> runId then
                    Internal.fail "run.json enthaelt eine abweichende Run-ID."

                let started =
                    match Internal.tryParseUtc metadata.StartedAtUtc with
                    | Some value -> value
                    | None -> Internal.fail "startedAtUtc ist kein UTC-Zeitstempel."

                if metadata.Status <> "running" && not (terminalStatuses.Contains(metadata.Status)) then
                    Internal.fail $"Unbekannter Run-Status '{metadata.Status}'."

                let events =
                    loadEventsStrict config.Value.Redaction (Path.Combine(runPath, "events.jsonl")) runId

                events
                |> List.iter (fun event ->
                    match Internal.tryParseUtc event.TimestampUtc with
                    | Some timestamp when timestamp >= started -> ()
                    | _ -> Internal.fail $"Event {event.Sequence} liegt vor Run-Start oder ist ungueltig.")

                let summaryPath = Path.Combine(runPath, "summary.json")

                if metadata.Status = "running" then
                    if metadata.FinishedAtUtc.IsSome then
                        Internal.fail "Laufender Run darf kein finishedAtUtc besitzen."

                    if File.Exists(summaryPath) then
                        Internal.fail "Laufender Run darf keine summary.json besitzen."
                else
                    if not (File.Exists(summaryPath)) then
                        Internal.fail "Abgeschlossener Run hat keine summary.json."

                    let metadataFinished =
                        match metadata.FinishedAtUtc with
                        | Some value -> value
                        | None -> Internal.fail "Abgeschlossener Run hat kein finishedAtUtc."

                    use document = JsonDocument.Parse(File.ReadAllBytes(summaryPath))
                    let root = document.RootElement

                    let summaryFields =
                        set
                            [ "schemaVersion"
                              "runId"
                              "startedAtUtc"
                              "finishedAtUtc"
                              "status"
                              "eventCount"
                              "finalEventHash"
                              "summary"
                              "summaryHash" ]

                    validateObjectFields "summary.json" summaryFields summaryFields root

                    if Internal.requiredInt "schemaVersion" root <> Constants.SchemaVersion then
                        Internal.fail "summary.json hat eine falsche Schema-Version."

                    let summaryRunId = Internal.requiredString "runId" root
                    let summaryStarted = Internal.requiredString "startedAtUtc" root
                    let summaryFinished = Internal.requiredString "finishedAtUtc" root
                    let summaryStatus = Internal.requiredString "status" root
                    let eventCount = Internal.requiredInt64 "eventCount" root
                    let finalHash = Internal.requiredString "finalEventHash" root
                    let payload = Internal.requiredProperty "summary" root |> Internal.canonicalElement
                    let storedSummaryHash = Internal.requiredString "summaryHash" root

                    let expectedSummaryHash =
                        summaryCoreBytes
                            summaryRunId
                            summaryStarted
                            summaryFinished
                            summaryStatus
                            eventCount
                            finalHash
                            payload
                        |> Internal.sha256Hex

                    if expectedSummaryHash <> storedSummaryHash then
                        Internal.fail "summaryHash ist ungueltig."

                    if
                        summaryRunId <> runId
                        || summaryStarted <> metadata.StartedAtUtc
                        || summaryFinished <> metadataFinished
                        || summaryStatus <> metadata.Status
                    then
                        Internal.fail "summary.json und run.json widersprechen sich."

                    match events |> List.tryLast with
                    | None -> Internal.fail "Abgeschlossener Run hat keine Events."
                    | Some finalEvent ->
                        if
                            finalEvent.EventType <> "run.finished"
                            || finalEvent.Sequence <> eventCount
                            || finalEvent.EventHash <> finalHash
                            || finalEvent.TimestampUtc <> summaryFinished
                        then
                            Internal.fail "Abschluss-Event und summary.json widersprechen sich."

                        use payloadDocument = JsonDocument.Parse(finalEvent.Payload)

                        if Internal.requiredString "status" payloadDocument.RootElement <> summaryStatus then
                            Internal.fail "Status im Abschluss-Event ist inkonsistent."

                    match validatePayloadRedaction config.Value.Redaction payload with
                    | [] -> ()
                    | paths ->
                        let joinedPaths = String.concat ", " paths
                        Internal.fail $"summary.json enthaelt nicht redigierte Felder: {joinedPaths}.")

        errors |> Seq.toList

    let allRunIds root =
        let locations = Workspace.paths root

        if Directory.Exists(locations.Runs) then
            Directory.EnumerateDirectories(locations.Runs)
            |> Seq.map Path.GetFileName
            |> Seq.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right))
            |> Seq.toList
        else
            []
