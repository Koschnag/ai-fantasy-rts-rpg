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

type CompletedRunSnapshot =
    { RunId: string
      ActorId: string option
      StartedAtUtc: string
      FinishedAtUtc: string
      Status: string
      FinalEventHash: string
      SummaryHash: string
      Events: StoredEvent list }

type RunMetadata =
    { RunId: string
      ActorId: string option
      StartedAtUtc: string
      Status: string
      FinishedAtUtc: string option
      RetrievalTraceVersion: int option
      Provenance: RunProvenance option }

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

    let rec private ensureNoNestedDuplicateKeys description (element: JsonElement) =
        match element.ValueKind with
        | JsonValueKind.Object ->
            let names = HashSet<string>(StringComparer.Ordinal)

            for property in element.EnumerateObject() do
                if not (names.Add(property.Name)) then
                    Internal.fail $"{description} enthaelt einen JSON-Schluessel mehrfach."

                ensureNoNestedDuplicateKeys description property.Value
        | JsonValueKind.Array ->
            for item in element.EnumerateArray() do
                ensureNoNestedDuplicateKeys description item
        | _ -> ()

    let private ensureEventPayloadObject (payload: byte array) =
        try
            use document = JsonDocument.Parse(payload)

            if document.RootElement.ValueKind <> JsonValueKind.Object then
                Internal.fail "Event-Payload muss ein JSON-Objekt sein."

            ensureNoNestedDuplicateKeys "Event-Payload" document.RootElement

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

            match metadata.ActorId with
            | Some actorId -> writer.WriteString("actorId", actorId)
            | None -> ()

            writer.WriteString("startedAtUtc", metadata.StartedAtUtc)
            writer.WriteString("status", metadata.Status)

            match metadata.FinishedAtUtc with
            | Some timestamp -> writer.WriteString("finishedAtUtc", timestamp)
            | None -> ()

            match metadata.RetrievalTraceVersion with
            | Some version -> writer.WriteNumber("retrievalTraceVersion", version)
            | None -> ()

            match metadata.Provenance with
            | Some provenance -> Provenance.writeProvenance writer provenance
            | None -> ()

            writer.WriteEndObject())

    let private loadMetadata runPath =
        let path = Path.Combine(runPath, "run.json")

        if not (File.Exists(path)) then
            Internal.fail $"Run-Metadaten fehlen: {path}"

        try
            use document = JsonDocument.Parse(File.ReadAllBytes(path))
            let root = document.RootElement
            ensureNoNestedDuplicateKeys "run.json" root

            validateObjectFields
                "run.json"
                (set
                    [ "$schema"
                      "schemaVersion"
                      "runId"
                      "actorId"
                      "startedAtUtc"
                      "status"
                      "finishedAtUtc"
                      "retrievalTraceVersion"
                      "provenance" ])
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

            let actorId =
                match root.TryGetProperty("actorId") with
                | false, _ -> None
                | true, value when value.ValueKind = JsonValueKind.String ->
                    let actor = value.GetString()

                    if
                        String.IsNullOrWhiteSpace(actor)
                        || actor <> actor.Trim()
                        || actor.Length > 128
                        || actor |> Seq.exists Char.IsControl
                    then
                        Internal.fail "run.json.actorId ist ungueltig."

                    Some actor
                | _ -> Internal.fail "run.json.actorId muss eine Zeichenfolge sein."

            { RunId = Internal.requiredString "runId" root
              ActorId = actorId
              StartedAtUtc = Internal.requiredString "startedAtUtc" root
              Status = Internal.requiredString "status" root
              FinishedAtUtc = finished
              RetrievalTraceVersion =
                match root.TryGetProperty("retrievalTraceVersion") with
                | false, _ -> None
                | true, value ->
                    match value.TryGetInt32() with
                    | true, version when version = 1 || version = 2 -> Some version
                    | _ -> Internal.fail "Run-Feld 'retrievalTraceVersion' muss 1 oder 2 sein."
              Provenance =
                match root.TryGetProperty("provenance") with
                | false, _ -> None
                | true, value -> Provenance.parseProvenance "run.json.provenance" value |> Some }
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
            ensureNoNestedDuplicateKeys "Event" root

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
        (timestampUtc: DateTimeOffset)
        =
        validateEventType eventType
        ensureEventPayloadObject payload |> ignore
        let eventsPath = Path.Combine(runPath, "events.jsonl")
        let existing = loadEventsStrict policy eventsPath runId
        let previous = existing |> List.tryLast |> Option.map (fun event -> event.EventHash)
        let sequence = int64 existing.Length + 1L
        let timestamp = timestampUtc.ToUniversalTime() |> Internal.utcText

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

    let startForActor root actorId =
        if
            String.IsNullOrWhiteSpace(actorId)
            || actorId <> actorId.Trim()
            || actorId.Length > 128
            || actorId |> Seq.exists Char.IsControl
        then
            Internal.fail "Run-Akteur muss eine nichtleere normalisierte ID ohne Steuerzeichen sein."

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
              ActorId = Some actorId
              StartedAtUtc = Internal.utcText now
              Status = "running"
              FinishedAtUtc = None
              RetrievalTraceVersion = Some 2
              Provenance = None }

        Internal.atomicWrite (Path.Combine(runPath, "run.json")) (metadataBytes metadata)
        Internal.atomicWrite (Path.Combine(runPath, "events.jsonl")) Array.empty
        Internal.atomicWrite (Path.Combine(runPath, "retrieval.jsonl")) Array.empty
        runId

    let start root = startForActor root "unspecified-agent"

    /// Liest und prueft die Metadaten eines Laufs (u. a. fuer Retention).
    let metadataOf root runId =
        let locations = Workspace.paths root

        if not (Internal.isRunId runId) then
            Internal.fail $"Run-Verzeichnis '{runId}' ist keine gueltige Run-ID."

        let runPath = Path.Combine(locations.Runs, runId)

        if not (Directory.Exists(runPath)) then
            Internal.fail $"Run nicht gefunden: {runId}"

        loadMetadata runPath

    /// Loads the authoritative event ledger for a run and verifies its complete
    /// sequence, timestamp, redaction, and SHA-256 chain before returning any
    /// event to an observer. Research instrumentation must use this boundary
    /// instead of reparsing mutable command inputs or scanning for a hash-like
    /// string in events.jsonl.
    let eventsStrict root runId =
        let locations = Workspace.requireInitialized root
        let config = HarnessConfig.load locations
        let runPath = runDirectory locations runId

        if not (Directory.Exists(runPath)) then
            Internal.fail $"Run nicht gefunden: {runId}"

        loadEventsStrict config.Redaction (Path.Combine(runPath, "events.jsonl")) runId

    /// Resolves one exact receipt only after verifying the complete authoritative
    /// ledger. Sequence, type, and hash are all part of the lookup contract so a
    /// valid row of the wrong semantic kind cannot be rebound as research data.
    let eventByReceipt root runId sequence eventType eventHash =
        if sequence < 1L then
            Internal.fail "Event-Sequenz muss mindestens 1 sein."

        validateEventType eventType

        if not (Internal.isSha256 eventHash) then
            Internal.fail "Event-Hash muss ein kleingeschriebener SHA-256-Wert sein."

        eventsStrict root runId
        |> List.tryFind (fun event ->
            event.Sequence = sequence
            && event.EventType = eventType
            && String.Equals(event.EventHash, eventHash, StringComparison.Ordinal))
        |> Option.defaultWith (fun () ->
            Internal.fail
                $"Autoritatives Run-Ereignis fehlt oder stimmt nicht mit Receipt ueberein: {runId}/{sequence}/{eventType}/{eventHash}.")

    /// Startet einen Lauf mit vollstaendiger Start-Provenienz (T-004):
    /// erweitertes Manifest, work-/evidence-Verzeichnisse und erstes run.started-Ereignis.
    let startProvenancedAt root actorId (inputs: Provenance.StartInputs) (nowUtc: DateTimeOffset) =
        if
            String.IsNullOrWhiteSpace(actorId)
            || actorId <> actorId.Trim()
            || actorId.Length > 128
            || actorId |> Seq.exists Char.IsControl
        then
            Internal.fail "Run-Akteur muss eine nichtleere normalisierte ID ohne Steuerzeichen sein."

        let locations = Workspace.requireInitialized root
        let config = HarnessConfig.load locations

        let provenance =
            Provenance.buildProvenance locations config.MaxEventPayloadBytes inputs

        let now = nowUtc.ToUniversalTime()

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

        Directory.CreateDirectory(Path.Combine(runPath, "work")) |> ignore

        Directory.CreateDirectory(Path.Combine(runPath, "evidence")) |> ignore

        let metadata =
            { RunId = runId
              ActorId = Some actorId
              StartedAtUtc = Internal.utcText now
              Status = "running"
              FinishedAtUtc = None
              RetrievalTraceVersion = Some 2
              Provenance = Some provenance }

        Internal.atomicWrite (Path.Combine(runPath, "run.json")) (metadataBytes metadata)
        Internal.atomicWrite (Path.Combine(runPath, "events.jsonl")) Array.empty
        Internal.atomicWrite (Path.Combine(runPath, "retrieval.jsonl")) Array.empty

        // Das erste Ereignis traegt dieselbe Provenienz wie das Manifest.
        // Kanonisierung ist Pflicht: Reload und Hash vergleichen die sortierte Form.
        let startedPayload =
            Internal.jsonBytes false (fun writer ->
                writer.WriteStartObject()
                Provenance.writeProvenance writer provenance
                writer.WriteEndObject())
            |> Constants.Utf8NoBom.GetString
            |> Internal.canonicalJson false

        appendLocked config.Redaction runPath runId "run.started" startedPayload now
        |> ignore

        runId

    let startProvenanced root actorId inputs =
        startProvenancedAt root actorId inputs (DateTimeOffset.UtcNow)

    let appendAt root runId eventType payloadFile (timestampUtc: DateTimeOffset) =
        let locations = Workspace.requireInitialized root
        let config = HarnessConfig.load locations
        let runPath = runDirectory locations runId

        if not (Directory.Exists(runPath)) then
            Internal.fail $"Run nicht gefunden: {runId}"

        let payloadText = Internal.safeReadAllText payloadFile config.MaxEventPayloadBytes

        let payload =
            Internal.canonicalJsonWithRedaction config.Redaction payloadText
            |> ensurePayloadLimit config.MaxEventPayloadBytes

        // Strukturierte Trace-/Span- und Evidenzvertraege vor dem Sperren pruefen.
        use payloadDocument = JsonDocument.Parse(payload)

        let spanEnvelope =
            if payloadDocument.RootElement.ValueKind = JsonValueKind.Object then
                Provenance.extractSpan $"Ereignis '{eventType}'" eventType payloadDocument.RootElement
            else
                None

        if
            eventType = "evidence.recorded"
            && payloadDocument.RootElement.ValueKind = JsonValueKind.Object
        then
            match Provenance.validateEvidencePayload locations payloadDocument.RootElement with
            | [] -> ()
            | evidenceErrors -> Internal.fail (String.concat "; " evidenceErrors)

        withRunLock runPath (fun () ->
            let metadata = loadMetadata runPath

            if metadata.Status <> "running" then
                Internal.fail $"Run {runId} ist bereits mit Status '{metadata.Status}' abgeschlossen."

            match spanEnvelope with
            | Some span ->
                let boundTaskId =
                    metadata.Provenance |> Option.bind (fun provenance -> provenance.TaskId)

                Provenance.checkCriterion locations boundTaskId span.CriterionId
            | None -> ()

            let event =
                appendLocked config.Redaction runPath runId eventType payload timestampUtc

            { RunId = runId
              Sequence = event.Sequence
              EventHash = event.EventHash })

    let append root runId eventType payloadFile =
        appendAt root runId eventType payloadFile (DateTimeOffset.UtcNow)

    let private writeRetrievalAnchor (writer: Utf8JsonWriter) (anchor: RetrievalAnchor) =
        writer.WriteNumber("retrievalTraceCount", anchor.TraceCount)

        match anchor.FinalTraceHash with
        | Some hash -> writer.WriteString("finalRetrievalTraceHash", hash)
        | None -> writer.WriteNull("finalRetrievalTraceHash")

    let private optionalRetrievalAnchor description (element: JsonElement) =
        match element.TryGetProperty("retrievalTraceCount"), element.TryGetProperty("finalRetrievalTraceHash") with
        | (false, _), (false, _) -> None
        | (true, _), (false, _)
        | (false, _), (true, _) ->
            Internal.fail $"{description} muss retrievalTraceCount und finalRetrievalTraceHash gemeinsam enthalten."
        | (true, countElement), (true, hashElement) ->
            let count =
                match countElement.TryGetInt64() with
                | true, value when value >= 0L -> value
                | _ -> Internal.fail $"{description}.retrievalTraceCount ist ungueltig."

            let finalHash =
                match hashElement.ValueKind with
                | JsonValueKind.Null -> None
                | JsonValueKind.String when Internal.isSha256 (hashElement.GetString()) -> Some(hashElement.GetString())
                | _ -> Internal.fail $"{description}.finalRetrievalTraceHash ist ungueltig."

            if (count = 0L) <> finalHash.IsNone then
                Internal.fail
                    $"{description}: Leerer Trace benoetigt null; ein nichtleerer Trace benoetigt einen finalen Hash."

            Some
                { TraceCount = count
                  FinalTraceHash = finalHash }

    let private finishPayload
        (actorId: string option)
        (status: string)
        (summary: byte array)
        (anchor: RetrievalAnchor)
        =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()

            match actorId with
            | Some actor -> writer.WriteString("actorId", actor)
            | None -> ()

            writer.WriteString("status", status)
            writeRetrievalAnchor writer anchor
            Internal.rawJson writer "summary" summary
            writer.WriteEndObject())
        |> Constants.Utf8NoBom.GetString
        |> Internal.canonicalJson false

    let private summaryCoreBytes
        (runId: string)
        (actorId: string option)
        (startedAtUtc: string)
        (finishedAtUtc: string)
        (status: string)
        (eventCount: int64)
        (finalEventHash: string)
        (anchor: RetrievalAnchor option)
        (summary: byte array)
        =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", Constants.SchemaVersion)
            writer.WriteString("runId", runId)

            match actorId with
            | Some actor -> writer.WriteString("actorId", actor)
            | None -> ()

            writer.WriteString("startedAtUtc", startedAtUtc)
            writer.WriteString("finishedAtUtc", finishedAtUtc)
            writer.WriteString("status", status)
            writer.WriteNumber("eventCount", eventCount)
            writer.WriteString("finalEventHash", finalEventHash)

            anchor |> Option.iter (writeRetrievalAnchor writer)

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

    let finishAt root runId status summaryFile (timestampUtc: DateTimeOffset) =
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

        withRunLock runPath (fun () ->
            let metadata = loadMetadata runPath

            if metadata.Status <> "running" then
                Internal.fail $"Run {runId} ist bereits mit Status '{metadata.Status}' abgeschlossen."

            if metadata.RunId <> runId then
                Internal.fail "Run-ID in run.json stimmt nicht mit dem Verzeichnis ueberein."

            RetrievalStore.withStableAnchor root runId metadata.RetrievalTraceVersion.IsSome (fun anchor ->
                let finishEventPayload =
                    finishPayload metadata.ActorId status summary anchor
                    |> ensurePayloadLimit config.MaxEventPayloadBytes

                let event =
                    appendLocked config.Redaction runPath runId "run.finished" finishEventPayload timestampUtc

                let finishedAt = event.TimestampUtc

                let core =
                    summaryCoreBytes
                        runId
                        metadata.ActorId
                        metadata.StartedAtUtc
                        finishedAt
                        status
                        event.Sequence
                        event.EventHash
                        (Some anchor)
                        summary

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
                  SummaryHash = summaryHash }))

    let finish root runId status summaryFile =
        finishAt root runId status summaryFile (DateTimeOffset.UtcNow)

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

                if
                    metadata.RetrievalTraceVersion.IsSome
                    && not (File.Exists(Path.Combine(runPath, "retrieval.jsonl")))
                then
                    Internal.fail "Run mit Retrieval-Trace-Vertrag hat keine retrieval.jsonl."

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

                // Provenienzvertrag: Laeufe mit Manifest muessen mit passendem run.started beginnen.
                match metadata.Provenance with
                | Some provenance ->
                    match events with
                    | [] -> ()
                    | first :: _ ->
                        if first.EventType <> "run.started" then
                            Internal.fail
                                "Ein Lauf mit Provenienzmanifest muss mit einem 'run.started'-Ereignis beginnen."

                        use startedDocument = JsonDocument.Parse(first.Payload)

                        match startedDocument.RootElement.TryGetProperty("provenance") with
                        | true, value when value.ValueKind = JsonValueKind.Object ->
                            let stored = value |> Internal.canonicalElement

                            let expected = Provenance.bytesOfProvenance provenance

                            if stored <> expected then
                                Internal.fail "Provenienz im 'run.started'-Ereignis widerspricht run.json."
                        | true, _ -> Internal.fail "Provenienz im 'run.started'-Ereignis muss ein JSON-Objekt sein."
                        | false, _ -> Internal.fail "'run.started'-Ereignis traegt keine Provenienz."
                | None -> ()

                // Trace-/Span- und Kriteriumsvertraege ueber alle Ereignisse.
                // Mehrere Ereignisse duerfen einen Span teilen; eine Evidenz schliesst
                // genau eine Span-Kombination ab und damit genau ein Kriterium.
                let closedEvidenceSpans = HashSet<string>(StringComparer.Ordinal)

                let retrievalHashes = HashSet<string>(StringComparer.Ordinal)

                let boundTaskId =
                    metadata.Provenance |> Option.bind (fun provenance -> provenance.TaskId)

                for event in events do
                    use payloadDocument = JsonDocument.Parse(event.Payload)

                    match
                        Provenance.extractSpan
                            $"Ereignis {event.Sequence} ({event.EventType})"
                            event.EventType
                            payloadDocument.RootElement
                    with
                    | Some span ->
                        if event.EventType = "evidence.recorded" then
                            if not (closedEvidenceSpans.Add($"{span.TraceId}/{span.SpanId}")) then
                                Internal.fail
                                    $"Ereignis {event.Sequence}: Die Span-Kombination ist bereits mit einer Evidenz abgeschlossen."

                        Provenance.checkCriterion locations boundTaskId span.CriterionId

                        if event.EventType = "evidence.recorded" then
                            match Provenance.validateEvidencePayload locations payloadDocument.RootElement with
                            | [] -> ()
                            | evidenceErrors ->
                                let joinedEvidenceErrors = String.concat "; " evidenceErrors

                                Internal.fail $"Ereignis {event.Sequence}: {joinedEvidenceErrors}"

                        if event.EventType = "retrieval.recorded" then
                            if retrievalHashes.Count = 0 then
                                RetrievalStore.recordedTraceHashes locations.Root runId
                                |> List.iter (fun hash -> retrievalHashes.Add(hash) |> ignore)

                            let referencedTrace =
                                Internal.requiredString "traceHash" payloadDocument.RootElement

                            if not (retrievalHashes.Contains(referencedTrace)) then
                                Internal.fail
                                    $"Ereignis {event.Sequence}: retrieval.recorded verweist auf einen unbekannten Trace-Hash."
                    | None -> ()

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
                    ensureNoNestedDuplicateKeys "summary.json" root

                    let summaryFields =
                        set
                            [ "schemaVersion"
                              "runId"
                              "actorId"
                              "startedAtUtc"
                              "finishedAtUtc"
                              "status"
                              "eventCount"
                              "finalEventHash"
                              "retrievalTraceCount"
                              "finalRetrievalTraceHash"
                              "summary"
                              "summaryHash" ]

                    let requiredSummaryFields =
                        summaryFields
                        |> Set.remove "actorId"
                        |> Set.remove "retrievalTraceCount"
                        |> Set.remove "finalRetrievalTraceHash"

                    validateObjectFields "summary.json" summaryFields requiredSummaryFields root

                    if Internal.requiredInt "schemaVersion" root <> Constants.SchemaVersion then
                        Internal.fail "summary.json hat eine falsche Schema-Version."

                    let summaryRunId = Internal.requiredString "runId" root

                    let summaryActor =
                        match root.TryGetProperty("actorId") with
                        | false, _ -> None
                        | true, value when value.ValueKind = JsonValueKind.String -> Some(value.GetString())
                        | _ -> Internal.fail "summary.json.actorId ist ungueltig."

                    let summaryStarted = Internal.requiredString "startedAtUtc" root
                    let summaryFinished = Internal.requiredString "finishedAtUtc" root
                    let summaryStatus = Internal.requiredString "status" root
                    let eventCount = Internal.requiredInt64 "eventCount" root
                    let finalHash = Internal.requiredString "finalEventHash" root
                    let payload = Internal.requiredProperty "summary" root |> Internal.canonicalElement
                    let storedSummaryHash = Internal.requiredString "summaryHash" root
                    let retrievalAnchor = optionalRetrievalAnchor "summary.json" root

                    if metadata.RetrievalTraceVersion = Some 2 && retrievalAnchor.IsNone then
                        Internal.fail "Abgeschlossener Retrieval-Trace-v2-Run hat keinen Tail-Anker."

                    let expectedSummaryHash =
                        summaryCoreBytes
                            summaryRunId
                            summaryActor
                            summaryStarted
                            summaryFinished
                            summaryStatus
                            eventCount
                            finalHash
                            retrievalAnchor
                            payload
                        |> Internal.sha256Hex

                    if expectedSummaryHash <> storedSummaryHash then
                        Internal.fail "summaryHash ist ungueltig."

                    if
                        summaryRunId <> runId
                        || summaryActor <> metadata.ActorId
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

                        let finishActor =
                            match payloadDocument.RootElement.TryGetProperty("actorId") with
                            | false, _ -> None
                            | true, value when value.ValueKind = JsonValueKind.String -> Some(value.GetString())
                            | _ -> Internal.fail "Akteur im Abschluss-Event ist ungueltig."

                        if finishActor <> summaryActor then
                            Internal.fail "Akteur im Abschluss-Event und summary.json widerspricht sich."

                        if
                            optionalRetrievalAnchor "run.finished-Payload" payloadDocument.RootElement
                            <> retrievalAnchor
                        then
                            Internal.fail "Retrieval-Anker in Abschluss-Event und summary.json widersprechen sich."

                    match retrievalAnchor with
                    | Some expectedAnchor ->
                        let actualAnchor = RetrievalStore.withStableAnchor locations.Root runId true id

                        if actualAnchor <> expectedAnchor then
                            Internal.fail
                                $"Retrieval-Tail stimmt nicht mit dem Abschlussanker ueberein (erwartet {expectedAnchor.TraceCount}/{expectedAnchor.FinalTraceHash}; gefunden {actualAnchor.TraceCount}/{actualAnchor.FinalTraceHash})."
                    | None -> ()

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

    let completedSnapshot root runId =
        let errors = verifyRun root runId

        if not (List.isEmpty errors) then
            let joinedErrors = String.concat "; " errors
            Internal.fail $"Generierungslauf {runId} ist ungueltig: {joinedErrors}"

        let locations = Workspace.requireInitialized root
        let config = HarnessConfig.load locations
        let runPath = runDirectory locations runId
        let metadata = loadMetadata runPath

        if metadata.Status <> "succeeded" then
            Internal.fail $"Generierungslauf {runId} muss succeeded sein, ist aber '{metadata.Status}'."

        let finishedAt =
            metadata.FinishedAtUtc
            |> Option.defaultWith (fun () -> Internal.fail $"Generierungslauf {runId} hat keinen Abschlusszeitpunkt.")

        use summaryDocument =
            JsonDocument.Parse(File.ReadAllBytes(Path.Combine(runPath, "summary.json")))

        ensureNoNestedDuplicateKeys "summary.json" summaryDocument.RootElement

        { RunId = runId
          ActorId = metadata.ActorId
          StartedAtUtc = metadata.StartedAtUtc
          FinishedAtUtc = finishedAt
          Status = metadata.Status
          FinalEventHash = Internal.requiredString "finalEventHash" summaryDocument.RootElement
          SummaryHash = Internal.requiredString "summaryHash" summaryDocument.RootElement
          Events = loadEventsStrict config.Redaction (Path.Combine(runPath, "events.jsonl")) runId }
