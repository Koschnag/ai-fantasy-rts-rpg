namespace RiftHarness

open System
open System.Collections.Generic
open System.IO
open System.Text.Json
open System.Text.RegularExpressions

type MemorySource =
    { Path: string
      Sha256: string
      Locator: string
      RunId: string option }

type MemoryRecord =
    { SchemaVersion: int
      Id: string
      Kind: string
      Statement: string
      Status: string
      Confidence: float
      Scope: string
      ConflictKey: string option
      Sources: MemorySource list
      CreatedAtUtc: string
      CreatedBy: string
      ReviewedAtUtc: string option
      ReviewedBy: string option
      Supersedes: string list
      ExpiresAtUtc: string option
      Tags: string list
      PreviousRecordHash: string option
      RecordHash: string option }

type MemoryFinding =
    { Code: string
      RecordIds: string list
      Message: string }

type MemoryRecordStatus =
    { Id: string
      DeclaredStatus: string
      EffectiveStatus: string
      SourcesCurrent: bool
      Expired: bool
      Retrievable: bool
      ConflictIds: string list }

type MemoryStatusReport =
    { RecordCount: int
      RetrievableCount: int
      Records: MemoryRecordStatus list
      Findings: MemoryFinding list }

type MemoryWriteReceipt =
    { Id: string
      Status: string
      PreviousId: string option
      RecordHash: string }

type MemoryValidationReceipt =
    { RecordCount: int
      ChainedRecordCount: int
      LastRecordHash: string option }

type private MemoryEntry =
    { LineNumber: int
      CanonicalLine: byte array
      LinkHash: string
      Record: MemoryRecord }

[<RequireQualifiedAccess>]
module MemoryStore =
    let private idPattern =
        Regex("^MEM-[0-9]{4,}$", RegexOptions.CultureInvariant ||| RegexOptions.NonBacktracking)

    let private tagPattern =
        Regex("^[a-z0-9][a-z0-9-]*$", RegexOptions.CultureInvariant ||| RegexOptions.NonBacktracking)

    let private conflictKeyPattern =
        Regex("^[a-z0-9][a-z0-9./-]{2,127}$", RegexOptions.CultureInvariant ||| RegexOptions.NonBacktracking)

    let private kinds =
        set [ "fact"; "constraint"; "decision"; "definition"; "lesson"; "preference" ]

    let private statuses =
        set [ "proposed"; "accepted"; "stale"; "superseded"; "rejected" ]

    let private validateFields
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

            if not (Set.contains property.Name allowed) then
                Internal.fail $"{description} enthaelt das unerlaubte Feld '{property.Name}'."

        for field in required do
            if not (seen.Contains(field)) then
                Internal.fail $"{description}: JSON-Feld '{field}' fehlt."

    let private optionalString (name: string) (element: JsonElement) =
        match element.TryGetProperty(name) with
        | false, _ -> None
        | true, value when value.ValueKind = JsonValueKind.Null -> None
        | true, value when value.ValueKind = JsonValueKind.String -> Some(value.GetString())
        | _ -> Internal.fail $"Memory-Feld '{name}' muss String oder null sein."

    let private requiredStringArray (name: string) (element: JsonElement) =
        let value = Internal.requiredProperty name element

        if value.ValueKind <> JsonValueKind.Array then
            Internal.fail $"Memory-Feld '{name}' muss ein Array sein."

        value.EnumerateArray()
        |> Seq.map (fun item ->
            if item.ValueKind <> JsonValueKind.String then
                Internal.fail $"Jeder Eintrag in Memory-Feld '{name}' muss eine Zeichenfolge sein."

            item.GetString())
        |> Seq.toList

    let private validateText description minimum maximum (value: string) =
        if
            String.IsNullOrWhiteSpace(value)
            || value.Length < minimum
            || value.Length > maximum
            || value |> Seq.exists Char.IsControl
        then
            Internal.fail $"{description} muss {minimum} bis {maximum} druckbare Zeichen enthalten."

    let private validateMemoryId description value =
        if isNull value || not (idPattern.IsMatch(value)) then
            Internal.fail $"{description} muss dem Muster MEM-[0-9]{{4,}} entsprechen."

    let private parseSource recordId index (source: JsonElement) =
        validateFields
            $"Memory-Quelle {recordId}[{index}]"
            (set [ "path"; "sha256"; "locator"; "runId" ])
            (set [ "path"; "sha256"; "locator" ])
            source

        let path =
            Internal.requiredString "path" source |> fun value -> value.Replace('\\', '/')

        let sha256 = Internal.requiredString "sha256" source
        let locator = Internal.requiredString "locator" source
        let runId = optionalString "runId" source

        if
            String.IsNullOrWhiteSpace(path)
            || Path.IsPathRooted(path)
            || path.Split('/') |> Array.exists ((=) "..")
            || path.Contains('*')
            || path.Contains('?')
        then
            Internal.fail $"Memory-Quelle {recordId}[{index}] hat einen unsicheren Pfad."

        if not (Internal.isSha256 sha256) then
            Internal.fail $"Memory-Quelle {recordId}[{index}] hat keinen gueltigen SHA-256."

        validateText $"Memory-Quelle {recordId}[{index}].locator" 1 500 locator

        match runId with
        | Some value when not (Internal.isRunId value) ->
            Internal.fail $"Memory-Quelle {recordId}[{index}].runId ist keine gueltige Run-ID."
        | _ -> ()

        { Path = path.TrimStart('/')
          Sha256 = sha256
          Locator = locator
          RunId = runId }

    let private validateRecordSemantics (record: MemoryRecord) =
        if record.SchemaVersion <> Constants.SchemaVersion then
            Internal.fail $"Memory {record.Id}: nicht unterstuetzte Schema-Version."

        validateMemoryId "Memory-ID" record.Id

        if not (kinds.Contains(record.Kind)) then
            Internal.fail $"Memory {record.Id}: unbekannte Art '{record.Kind}'."

        if not (statuses.Contains(record.Status)) then
            Internal.fail $"Memory {record.Id}: unbekannter Status '{record.Status}'."

        validateText $"Memory {record.Id}.statement" 10 2000 record.Statement
        validateText $"Memory {record.Id}.scope" 1 200 record.Scope
        validateText $"Memory {record.Id}.createdBy" 1 200 record.CreatedBy

        if
            Double.IsNaN(record.Confidence)
            || Double.IsInfinity(record.Confidence)
            || record.Confidence < 0.0
            || record.Confidence > 1.0
        then
            Internal.fail $"Memory {record.Id}.confidence muss zwischen 0 und 1 liegen."

        match record.ConflictKey with
        | Some value when not (conflictKeyPattern.IsMatch(value)) ->
            Internal.fail $"Memory {record.Id}.conflictKey ist ungueltig."
        | _ -> ()

        if List.isEmpty record.Sources || record.Sources.Length > 32 then
            Internal.fail $"Memory {record.Id} benoetigt 1 bis 32 Quellen."

        if
            record.Sources
            |> List.map (fun source -> source.Path, source.Locator)
            |> List.distinct
            |> List.length
            <> record.Sources.Length
        then
            Internal.fail $"Memory {record.Id} enthaelt doppelte Quellen."

        if (Internal.tryParseUtc record.CreatedAtUtc).IsNone then
            Internal.fail $"Memory {record.Id}.createdAtUtc ist kein UTC-Zeitstempel."

        match record.ReviewedAtUtc, record.ReviewedBy with
        | None, None -> ()
        | Some reviewedAt, Some reviewedBy ->
            validateText $"Memory {record.Id}.reviewedBy" 1 200 reviewedBy

            match Internal.tryParseUtc reviewedAt with
            | Some reviewed when reviewed >= (Internal.tryParseUtc record.CreatedAtUtc).Value -> ()
            | _ -> Internal.fail $"Memory {record.Id}.reviewedAtUtc ist ungueltig oder liegt vor createdAtUtc."
        | _ -> Internal.fail $"Memory {record.Id} benoetigt reviewedAtUtc und reviewedBy gemeinsam."

        match record.Status with
        | "proposed" when record.ReviewedAtUtc.IsSome || not (List.isEmpty record.Supersedes) ->
            Internal.fail $"Memory {record.Id}: proposed darf weder Review noch supersedes enthalten."
        | "accepted"
        | "stale"
        | "superseded"
        | "rejected" when record.ReviewedAtUtc.IsNone ->
            Internal.fail $"Memory {record.Id}: Status '{record.Status}' benoetigt einen expliziten Review."
        | _ -> ()

        match record.ExpiresAtUtc with
        | Some value when (Internal.tryParseUtc value).IsNone ->
            Internal.fail $"Memory {record.Id}.expiresAtUtc ist kein UTC-Zeitstempel."
        | _ -> ()

        if record.Supersedes |> List.distinct |> List.length <> record.Supersedes.Length then
            Internal.fail $"Memory {record.Id}.supersedes enthaelt Duplikate."

        for previousId in record.Supersedes do
            validateMemoryId $"Memory {record.Id}.supersedes" previousId

            if previousId = record.Id then
                Internal.fail $"Memory {record.Id} darf sich nicht selbst ersetzen."

        if record.Tags |> List.distinct |> List.length <> record.Tags.Length then
            Internal.fail $"Memory {record.Id}.tags enthaelt Duplikate."

        for tag in record.Tags do
            if not (tagPattern.IsMatch(tag)) then
                Internal.fail $"Memory {record.Id} enthaelt den ungueltigen Tag '{tag}'."

        match record.PreviousRecordHash, record.RecordHash with
        | None, None -> ()
        | previous, Some hash when Internal.isSha256 hash && (previous.IsNone || Internal.isSha256 previous.Value) -> ()
        | _ -> Internal.fail $"Memory {record.Id}: previousRecordHash/recordHash sind unvollstaendig oder ungueltig."

    let private parseRecord lineNumber (line: string) =
        try
            use document = JsonDocument.Parse(line)
            let root = document.RootElement

            validateFields
                $"Memory-Zeile {lineNumber}"
                (set
                    [ "schemaVersion"
                      "id"
                      "kind"
                      "statement"
                      "status"
                      "confidence"
                      "scope"
                      "conflictKey"
                      "sources"
                      "createdAtUtc"
                      "createdBy"
                      "reviewedAtUtc"
                      "reviewedBy"
                      "supersedes"
                      "expiresAtUtc"
                      "tags"
                      "previousRecordHash"
                      "recordHash" ])
                (set
                    [ "schemaVersion"
                      "id"
                      "kind"
                      "statement"
                      "status"
                      "confidence"
                      "scope"
                      "sources"
                      "createdAtUtc"
                      "createdBy"
                      "tags" ])
                root

            let confidenceElement = Internal.requiredProperty "confidence" root

            let confidence =
                match confidenceElement.TryGetDouble() with
                | true, value -> value
                | _ -> Internal.fail $"Memory-Zeile {lineNumber}.confidence muss eine Zahl sein."

            let sourcesElement = Internal.requiredProperty "sources" root

            if sourcesElement.ValueKind <> JsonValueKind.Array then
                Internal.fail $"Memory-Zeile {lineNumber}.sources muss ein Array sein."

            let id = Internal.requiredString "id" root

            let record =
                { SchemaVersion = Internal.requiredInt "schemaVersion" root
                  Id = id
                  Kind = Internal.requiredString "kind" root
                  Statement = Internal.requiredString "statement" root
                  Status = Internal.requiredString "status" root
                  Confidence = confidence
                  Scope = Internal.requiredString "scope" root
                  ConflictKey = optionalString "conflictKey" root
                  Sources = sourcesElement.EnumerateArray() |> Seq.mapi (parseSource id) |> Seq.toList
                  CreatedAtUtc = Internal.requiredString "createdAtUtc" root
                  CreatedBy = Internal.requiredString "createdBy" root
                  ReviewedAtUtc = optionalString "reviewedAtUtc" root
                  ReviewedBy = optionalString "reviewedBy" root
                  Supersedes =
                    match root.TryGetProperty("supersedes") with
                    | true, _ -> requiredStringArray "supersedes" root
                    | _ -> []
                  ExpiresAtUtc = optionalString "expiresAtUtc" root
                  Tags = requiredStringArray "tags" root
                  PreviousRecordHash = optionalString "previousRecordHash" root
                  RecordHash = optionalString "recordHash" root }

            validateRecordSemantics record
            record, Internal.canonicalElement root
        with :? JsonException as error ->
            Internal.fail $"Memory-Zeile {lineNumber} ist ungueltiges JSON: {error.Message}"

    let private writeSource (writer: Utf8JsonWriter) (source: MemorySource) =
        writer.WriteStartObject()
        writer.WriteString("path", source.Path)
        writer.WriteString("sha256", source.Sha256)
        writer.WriteString("locator", source.Locator)

        match source.RunId with
        | Some runId -> writer.WriteString("runId", runId)
        | None -> writer.WriteNull("runId")

        writer.WriteEndObject()

    let private coreBytes (record: MemoryRecord) (previousRecordHash: string option) =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", Constants.SchemaVersion)
            writer.WriteString("id", record.Id)
            writer.WriteString("kind", record.Kind)
            writer.WriteString("statement", record.Statement)
            writer.WriteString("status", record.Status)
            writer.WriteNumber("confidence", record.Confidence)
            writer.WriteString("scope", record.Scope)

            match record.ConflictKey with
            | Some conflictKey -> writer.WriteString("conflictKey", conflictKey)
            | None -> ()

            writer.WriteStartArray("sources")
            record.Sources |> List.iter (writeSource writer)
            writer.WriteEndArray()
            writer.WriteString("createdAtUtc", record.CreatedAtUtc)
            writer.WriteString("createdBy", record.CreatedBy)

            match record.ReviewedAtUtc with
            | Some value -> writer.WriteString("reviewedAtUtc", value)
            | None -> writer.WriteNull("reviewedAtUtc")

            match record.ReviewedBy with
            | Some value -> writer.WriteString("reviewedBy", value)
            | None -> writer.WriteNull("reviewedBy")

            writer.WriteStartArray("supersedes")
            record.Supersedes |> List.iter writer.WriteStringValue
            writer.WriteEndArray()

            match record.ExpiresAtUtc with
            | Some value -> writer.WriteString("expiresAtUtc", value)
            | None -> writer.WriteNull("expiresAtUtc")

            writer.WriteStartArray("tags")
            record.Tags |> List.iter writer.WriteStringValue
            writer.WriteEndArray()

            match previousRecordHash with
            | Some hash -> writer.WriteString("previousRecordHash", hash)
            | None -> writer.WriteNull("previousRecordHash")

            writer.WriteEndObject())

    let private lineBytes (core: byte array) (recordHash: string) =
        use document = JsonDocument.Parse(core)

        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()

            for property in document.RootElement.EnumerateObject() do
                property.WriteTo(writer)

            writer.WriteString("recordHash", recordHash)
            writer.WriteEndObject())

    let private parseEntries (lines: string array) =
        let entries = ResizeArray<MemoryEntry>()
        let ids = HashSet<string>(StringComparer.Ordinal)
        let consumedBy = Dictionary<string, string>(StringComparer.Ordinal)
        let mutable chained = false
        let mutable previousLink: string option = None

        lines
        |> Array.iteri (fun index line ->
            if String.IsNullOrWhiteSpace(line) then
                Internal.fail $"Memory-Zeile {index + 1} ist leer; JSONL darf keine Luecken enthalten."

            let record, canonical = parseRecord (index + 1) line

            if not (ids.Add(record.Id)) then
                Internal.fail $"Memory-ID ist nicht eindeutig: {record.Id}."

            for previousId in record.Supersedes do
                if not (ids.Contains(previousId)) then
                    Internal.fail $"Memory {record.Id} ersetzt unbekannte oder spaetere ID {previousId}."

                match consumedBy.TryGetValue(previousId) with
                | true, consumerId ->
                    Internal.fail
                        $"Memory {previousId} ist bereits durch {consumerId} konsumiert; {record.Id} darf keine zweite Nachfolgerevision bilden."
                | false, _ -> consumedBy.Add(previousId, record.Id)

            let linkHash =
                match record.PreviousRecordHash, record.RecordHash with
                | None, None when chained ->
                    Internal.fail $"Memory {record.Id}: ungehashte Revision nach Beginn der Hashkette."
                | None, None -> Internal.sha256Hex canonical
                | previous, Some storedHash ->
                    if previous <> previousLink then
                        Internal.fail $"Memory {record.Id}: previousRecordHash unterbricht die Hashkette."

                    let expectedHash = coreBytes record previous |> Internal.sha256Hex

                    if storedHash <> expectedHash then
                        Internal.fail $"Memory {record.Id}: recordHash ist ungueltig."

                    chained <- true
                    storedHash
                | _ -> Internal.fail $"Memory {record.Id}: unvollstaendige Hashkette."

            entries.Add(
                { LineNumber = index + 1
                  CanonicalLine = canonical
                  LinkHash = linkHash
                  Record = record }
            )

            previousLink <- Some linkHash)

        entries |> Seq.toList

    let private memoryPath locations (config: HarnessRuntimeConfig) =
        let path =
            Path.Combine(locations.Root, config.MemoryPath.Replace('/', Path.DirectorySeparatorChar))

        Workspace.requireSafePath locations "Konfigurierter Memory-Pfad" true path

    let private readEntries locations config =
        let path = memoryPath locations config

        if not (File.Exists(path)) then
            []
        else
            let text = Internal.safeReadAllText path Constants.MaxConfigurablePayloadBytes
            let normalized = text.Replace("\r\n", "\n").Replace('\r', '\n')

            let lines =
                if String.IsNullOrEmpty(normalized) then
                    Array.empty
                else
                    let split = normalized.Split('\n')

                    if normalized.EndsWith("\n", StringComparison.Ordinal) then
                        split[.. split.Length - 2]
                    else
                        split

            parseEntries lines

    let private sourceIsCurrent locations maxSourceBytes (source: MemorySource) =
        let absolute =
            Path.Combine(locations.Root, source.Path.Replace('/', Path.DirectorySeparatorChar))

        try
            let safe =
                Workspace.requireSafePath locations $"Memory-Quelle {source.Path}" false absolute

            File.Exists(safe)
            && FileInfo(safe).Length <= maxSourceBytes
            && Internal.sha256File safe = source.Sha256
        with :? HarnessException ->
            false

    let private isExpired now (record: MemoryRecord) =
        record.ExpiresAtUtc
        |> Option.bind Internal.tryParseUtc
        |> Option.exists (fun expiry -> expiry <= now)

    let private analyze locations maxSourceBytes entries =
        let supersededIds =
            entries
            |> Seq.filter (fun entry -> entry.Record.Status <> "proposed")
            |> Seq.collect (fun entry -> entry.Record.Supersedes)
            |> Set.ofSeq

        let now = DateTimeOffset.UtcNow

        let freshness =
            entries
            |> Seq.map (fun entry ->
                let current =
                    entry.Record.Sources |> List.forall (sourceIsCurrent locations maxSourceBytes)

                entry.Record.Id, (current, isExpired now entry.Record))
            |> Map.ofSeq

        let activeAccepted =
            entries
            |> List.filter (fun entry ->
                let current, expired = freshness[entry.Record.Id]

                entry.Record.Status = "accepted"
                && not (supersededIds.Contains(entry.Record.Id))
                && current
                && not expired)

        let conflictGroups =
            activeAccepted
            |> Seq.choose (fun entry -> entry.Record.ConflictKey |> Option.map (fun key -> key, entry.Record.Id))
            |> Seq.groupBy fst
            |> Seq.map (fun (key, values) -> key, values |> Seq.map snd |> Seq.sort |> Seq.toList)
            |> Seq.filter (fun (_, ids) -> ids.Length > 1)
            |> Map.ofSeq

        let conflictLookup =
            conflictGroups
            |> Seq.collect (fun pair -> pair.Value |> Seq.map (fun id -> id, pair.Value))
            |> Map.ofSeq

        let recordStatuses =
            entries
            |> List.map (fun entry ->
                let record = entry.Record
                let current, expired = freshness[record.Id]
                let conflicts = conflictLookup.TryFind(record.Id) |> Option.defaultValue []

                let effective =
                    if supersededIds.Contains(record.Id) then "superseded"
                    elif record.Status <> "accepted" then record.Status
                    elif not current || expired then "stale"
                    elif not (List.isEmpty conflicts) then "conflict"
                    else "accepted"

                { Id = record.Id
                  DeclaredStatus = record.Status
                  EffectiveStatus = effective
                  SourcesCurrent = current
                  Expired = expired
                  Retrievable = effective = "accepted"
                  ConflictIds = conflicts })

        let staleFindings =
            recordStatuses
            |> List.choose (fun status ->
                if status.DeclaredStatus = "accepted" && status.EffectiveStatus = "stale" then
                    Some
                        { Code = "MEMORY_STALE"
                          RecordIds = [ status.Id ]
                          Message = $"Accepted Memory {status.Id} ist wegen Quelle oder Ablaufdatum nicht abrufbar." }
                else
                    None)

        let conflictFindings =
            conflictGroups
            |> Seq.map (fun pair ->
                { Code = "MEMORY_CONFLICT"
                  RecordIds = pair.Value
                  Message =
                    $"Conflict-Key '{pair.Key}' besitzt mehrere aktive accepted Records; alle sind ausgeschlossen." })
            |> Seq.toList

        { RecordCount = entries.Length
          RetrievableCount = recordStatuses |> List.filter (fun status -> status.Retrievable) |> List.length
          Records = recordStatuses
          Findings = staleFindings @ conflictFindings }

    let private withMemoryLock locations action =
        Directory.CreateDirectory(locations.Runtime) |> ignore
        let lockPath = Path.Combine(locations.Runtime, "memory.write.lock")

        try
            use lockHandle =
                new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)

            action ()
        with :? IOException as error ->
            Internal.fail $"Memory ist bereits fuer einen Schreibvorgang gesperrt: {error.Message}"

    let private appendRecord root (record: MemoryRecord) =
        let locations = Workspace.requireInitialized root
        let config = HarnessConfig.load locations

        withMemoryLock locations (fun () ->
            let entries = readEntries locations config

            if entries |> List.exists (fun entry -> entry.Record.Id = record.Id) then
                Internal.fail $"Memory-ID existiert bereits: {record.Id}."

            let knownIds = entries |> Seq.map (fun entry -> entry.Record.Id) |> Set.ofSeq

            let consumedBy =
                entries
                |> Seq.collect (fun entry ->
                    entry.Record.Supersedes
                    |> Seq.map (fun previousId -> previousId, entry.Record.Id))
                |> Map.ofSeq

            for previousId in record.Supersedes do
                if not (knownIds.Contains(previousId)) then
                    Internal.fail $"Memory {record.Id} ersetzt unbekannte ID {previousId}."

                match consumedBy.TryFind(previousId) with
                | Some consumerId ->
                    Internal.fail
                        $"Memory {previousId} ist bereits durch {consumerId} konsumiert; {record.Id} darf keine zweite Nachfolgerevision bilden."
                | None -> ()

            validateRecordSemantics record

            let previousHash =
                entries |> List.tryLast |> Option.map (fun entry -> entry.LinkHash)

            let core = coreBytes record previousHash
            let hash = Internal.sha256Hex core
            let line = lineBytes core hash
            let path = memoryPath locations config
            Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore

            Workspace.requireSafePath locations "Konfigurierter Memory-Pfad" true path
            |> ignore

            use stream =
                new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read)

            stream.Seek(0L, SeekOrigin.End) |> ignore

            if stream.Length > 0L then
                stream.Seek(-1L, SeekOrigin.End) |> ignore
                let last = stream.ReadByte()
                stream.Seek(0L, SeekOrigin.End) |> ignore

                if last <> int (byte '\n') then
                    stream.WriteByte(byte '\n')

            stream.Write(line, 0, line.Length)
            stream.WriteByte(byte '\n')
            stream.Flush(true)

            { Id = record.Id
              Status = record.Status
              PreviousId = record.Supersedes |> List.tryHead
              RecordHash = hash })

    let private requireActor (policy: RedactionPolicy) actor =
        validateText "Akteur" 1 200 actor

        if Internal.redactText policy actor <> actor then
            Internal.fail "Akteur enthaelt ein konfiguriertes Secretmuster und wird nicht gespeichert."

    let private requireNewId entries newId =
        validateMemoryId "Neue Memory-ID" newId

        if entries |> List.exists (fun entry -> entry.Record.Id = newId) then
            Internal.fail $"Memory-ID existiert bereits: {newId}."

    let private requireRecord entries id =
        entries
        |> List.tryFind (fun entry -> entry.Record.Id = id)
        |> Option.defaultWith (fun () -> Internal.fail $"Memory-ID nicht gefunden: {id}.")

    let propose root recordFile =
        let locations = Workspace.requireInitialized root
        let config = HarnessConfig.load locations

        let text = Internal.safeReadAllText recordFile config.MaxEventPayloadBytes

        let redacted =
            Internal.canonicalJsonWithRedaction config.Redaction text
            |> Constants.Utf8NoBom.GetString

        let record, _ = parseRecord 1 redacted

        if record.Status <> "proposed" then
            Internal.fail "memory propose akzeptiert nur Records mit status 'proposed'."

        if record.ConflictKey.IsNone then
            Internal.fail "Neue Memory-Vorschlaege benoetigen einen conflictKey fuer sichtbare Widerspruchspruefung."

        if record.CreatedBy.Contains("[REDACTED]", StringComparison.Ordinal) then
            Internal.fail "createdBy wurde redigiert; der Memory-Vorschlag benoetigt einen nicht geheimen Akteur."

        if record.PreviousRecordHash.IsSome || record.RecordHash.IsSome then
            Internal.fail "Memory-Hashfelder werden ausschliesslich vom Harness erzeugt."

        if
            record.Sources
            |> List.exists (sourceIsCurrent locations config.MaxSourceFileBytes >> not)
        then
            Internal.fail "Memory-Vorschlag besitzt eine fehlende, zu grosse oder hashabweichende Quelle."

        appendRecord root record

    let accept root id newId actor =
        let locations = Workspace.requireInitialized root
        let config = HarnessConfig.load locations
        requireActor config.Redaction actor
        let entries = readEntries locations config
        requireNewId entries newId
        let proposal = (requireRecord entries id).Record
        let report = analyze locations config.MaxSourceFileBytes entries
        let proposalStatus = report.Records |> List.find (fun status -> status.Id = id)

        if proposalStatus.EffectiveStatus <> "proposed" then
            Internal.fail
                $"Memory {id} ist kein aktiver proposed Record (effektiver Status: '{proposalStatus.EffectiveStatus}')."

        if String.Equals(actor, proposal.CreatedBy, StringComparison.OrdinalIgnoreCase) then
            Internal.fail "Der erzeugende Akteur darf seinen eigenen Memory-Vorschlag nicht annehmen."

        if
            proposal.Sources
            |> List.exists (sourceIsCurrent locations config.MaxSourceFileBytes >> not)
        then
            Internal.fail $"Memory {id} besitzt keine quellenfrische Grundlage."

        let now = Internal.utcText DateTimeOffset.UtcNow

        appendRecord
            root
            { proposal with
                Id = newId
                Status = "accepted"
                ReviewedAtUtc = Some now
                ReviewedBy = Some actor
                Supersedes = [ id ]
                PreviousRecordHash = None
                RecordHash = None }

    let supersede root id proposalId newId actor =
        let locations = Workspace.requireInitialized root
        let config = HarnessConfig.load locations
        requireActor config.Redaction actor
        let entries = readEntries locations config
        requireNewId entries newId
        let current = (requireRecord entries id).Record
        let proposal = (requireRecord entries proposalId).Record
        let report = analyze locations config.MaxSourceFileBytes entries

        let currentStatus = report.Records |> List.find (fun status -> status.Id = id)

        let proposalStatus =
            report.Records |> List.find (fun status -> status.Id = proposalId)

        if currentStatus.EffectiveStatus <> "accepted" then
            Internal.fail $"Memory {id} ist kein aktiver accepted Record."

        if proposalStatus.EffectiveStatus <> "proposed" then
            Internal.fail
                $"Memory {proposalId} ist kein aktiver proposed Ersatz (effektiver Status: '{proposalStatus.EffectiveStatus}')."

        if current.ConflictKey <> proposal.ConflictKey then
            Internal.fail "Ersatz und aktueller Record muessen denselben conflictKey besitzen."

        if String.Equals(actor, proposal.CreatedBy, StringComparison.OrdinalIgnoreCase) then
            Internal.fail "Der erzeugende Akteur darf seinen eigenen Memory-Vorschlag nicht annehmen."

        if
            proposal.Sources
            |> List.exists (sourceIsCurrent locations config.MaxSourceFileBytes >> not)
        then
            Internal.fail $"Memory {proposalId} besitzt keine quellenfrische Grundlage."

        let now = Internal.utcText DateTimeOffset.UtcNow

        appendRecord
            root
            { proposal with
                Id = newId
                Status = "accepted"
                ReviewedAtUtc = Some now
                ReviewedBy = Some actor
                Supersedes = [ id; proposalId ]
                PreviousRecordHash = None
                RecordHash = None }

    let setStatus root id newId status actor =
        if status <> "stale" && status <> "rejected" then
            Internal.fail "memory set-status unterstuetzt nur 'stale' und 'rejected'."

        let locations = Workspace.requireInitialized root
        let config = HarnessConfig.load locations
        requireActor config.Redaction actor
        let entries = readEntries locations config
        requireNewId entries newId
        let original = (requireRecord entries id).Record
        let report = analyze locations config.MaxSourceFileBytes entries
        let originalStatus = report.Records |> List.find (fun item -> item.Id = id)

        if status = "rejected" && originalStatus.EffectiveStatus <> "proposed" then
            Internal.fail
                $"Nur ein aktiver proposed Record kann rejected werden (effektiver Status: '{originalStatus.EffectiveStatus}')."

        if status = "stale" then
            let isCurrent =
                original.Sources
                |> List.forall (sourceIsCurrent locations config.MaxSourceFileBytes)

            if isCurrent && not (isExpired DateTimeOffset.UtcNow original) then
                Internal.fail "Ein quellenfrischer, nicht abgelaufener Record kann nicht als stale markiert werden."

        appendRecord
            root
            { original with
                Id = newId
                Status = status
                ReviewedAtUtc = Some(Internal.utcText DateTimeOffset.UtcNow)
                ReviewedBy = Some actor
                Supersedes = [ id ]
                PreviousRecordHash = None
                RecordHash = None }

    let status root =
        let locations = Workspace.requireInitialized root
        let config = HarnessConfig.load locations

        readEntries locations config |> analyze locations config.MaxSourceFileBytes

    let validate root =
        let locations = Workspace.requireInitialized root
        let config = HarnessConfig.load locations
        let entries = readEntries locations config

        { RecordCount = entries.Length
          ChainedRecordCount =
            entries
            |> List.filter (fun entry -> entry.Record.RecordHash.IsSome)
            |> List.length
          LastRecordHash = entries |> List.tryLast |> Option.map (fun entry -> entry.LinkHash) }

    let projectForRetrieval locations maxSourceBytes (lines: string array) =
        let entries = parseEntries lines
        let report = analyze locations maxSourceBytes entries

        let retrievable =
            report.Records
            |> Seq.filter (fun item -> item.Retrievable)
            |> Seq.map (fun item -> item.Id)
            |> Set.ofSeq

        let projected =
            entries
            |> List.map (fun entry ->
                if retrievable.Contains(entry.Record.Id) then
                    Constants.Utf8NoBom.GetString(entry.CanonicalLine)
                else
                    "")
            |> List.toArray

        projected, report.Findings

    let statusJson (report: MemoryStatusReport) =
        Internal.jsonBytes true (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", Constants.SchemaVersion)
            writer.WriteNumber("recordCount", report.RecordCount)
            writer.WriteNumber("retrievableCount", report.RetrievableCount)
            writer.WriteStartArray("records")

            for record in report.Records do
                writer.WriteStartObject()
                writer.WriteString("id", record.Id)
                writer.WriteString("declaredStatus", record.DeclaredStatus)
                writer.WriteString("effectiveStatus", record.EffectiveStatus)
                writer.WriteBoolean("sourcesCurrent", record.SourcesCurrent)
                writer.WriteBoolean("expired", record.Expired)
                writer.WriteBoolean("retrievable", record.Retrievable)
                writer.WriteStartArray("conflictIds")
                record.ConflictIds |> List.iter writer.WriteStringValue
                writer.WriteEndArray()
                writer.WriteEndObject()

            writer.WriteEndArray()
            writer.WriteStartArray("findings")

            for finding in report.Findings do
                writer.WriteStartObject()
                writer.WriteString("code", finding.Code)
                writer.WriteStartArray("recordIds")
                finding.RecordIds |> List.iter writer.WriteStringValue
                writer.WriteEndArray()
                writer.WriteString("message", finding.Message)
                writer.WriteEndObject()

            writer.WriteEndArray()
            writer.WriteEndObject())
        |> Constants.Utf8NoBom.GetString
