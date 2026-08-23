namespace RiftHarness

open System
open System.Collections.Generic
open System.IO
open System.Text.Json

type private RetrievalResult =
    { Rank: int
      Score: float
      ChunkId: string
      Path: string
      StartLine: int
      EndLine: int
      SourceSha256: string
      ContentSha256: string
      TextSha256: string
      TrustClass: string }

type private RetrievalTrace =
    { RunId: string
      Sequence: int64
      QueryId: string
      TimestampUtc: string
      Query: string
      QuerySha256: string
      IndexSha256: string
      ConfigSha256: string
      Ranking: RagRanking
      Results: RetrievalResult list
      Context: string
      ContextSha256: string
      PreviousTraceHash: string option
      TraceHash: string }

type RetrievalAnchor =
    { TraceCount: int64
      FinalTraceHash: string option }

[<RequireQualifiedAccess>]
module RetrievalStore =
    let private trustClasses =
        set
            [ "accepted-decision"
              "specification"
              "accepted-memory"
              "ready-task"
              "documentation"
              "code"
              "untrusted" ]

    let private tracePath locations runId =
        if not (Internal.isRunId runId) then
            Internal.fail "Run-ID muss eine 26-stellige Crockford-Base32-ID sein."

        Path.Combine(locations.Runs, runId, "retrieval.jsonl")

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

            if not (allowed.Contains(property.Name)) then
                Internal.fail $"{description} enthaelt das unerlaubte Feld '{property.Name}'."

        for field in required do
            if not (seen.Contains(field)) then
                Internal.fail $"{description}: JSON-Feld '{field}' fehlt."

    let private writeRanking (writer: Utf8JsonWriter) (ranking: RagRanking) =
        writer.WriteStartObject("ranking")
        writer.WriteString("algorithm", ranking.Algorithm)
        writer.WriteNumber("k1", ranking.K1)
        writer.WriteNumber("b", ranking.B)
        writer.WriteNumber("top", ranking.Top)
        writer.WriteNumber("maxContextCharacters", ranking.MaxContextCharacters)
        writer.WriteEndObject()

    let private writeResults (writer: Utf8JsonWriter) (results: RetrievalResult list) =
        writer.WriteStartArray("results")

        for result in results do
            writer.WriteStartObject()
            writer.WriteNumber("rank", result.Rank)
            writer.WriteNumber("score", result.Score)
            writer.WriteString("chunkId", result.ChunkId)
            writer.WriteString("path", result.Path)
            writer.WriteNumber("startLine", result.StartLine)
            writer.WriteNumber("endLine", result.EndLine)
            writer.WriteString("sourceSha256", result.SourceSha256)
            writer.WriteString("contentSha256", result.ContentSha256)
            writer.WriteString("textSha256", result.TextSha256)
            writer.WriteString("trustClass", result.TrustClass)
            writer.WriteEndObject()

        writer.WriteEndArray()

    let private coreBytes (trace: RetrievalTrace) =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", Constants.SchemaVersion)
            writer.WriteString("runId", trace.RunId)
            writer.WriteNumber("sequence", trace.Sequence)
            writer.WriteString("queryId", trace.QueryId)
            writer.WriteString("timestampUtc", trace.TimestampUtc)
            writer.WriteString("query", trace.Query)
            writer.WriteString("querySha256", trace.QuerySha256)
            writer.WriteString("indexSha256", trace.IndexSha256)
            writer.WriteString("configSha256", trace.ConfigSha256)
            writeRanking writer trace.Ranking
            writeResults writer trace.Results
            writer.WriteString("context", trace.Context)
            writer.WriteString("contextSha256", trace.ContextSha256)

            match trace.PreviousTraceHash with
            | Some hash -> writer.WriteString("previousTraceHash", hash)
            | None -> writer.WriteNull("previousTraceHash")

            writer.WriteEndObject())

    let private lineBytes (core: byte array) (traceHash: string) =
        use document = JsonDocument.Parse(core)

        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()

            for property in document.RootElement.EnumerateObject() do
                property.WriteTo(writer)

            writer.WriteString("traceHash", traceHash)
            writer.WriteEndObject())

    let private parseRanking (element: JsonElement) =
        validateFields
            "Retrieval-Ranking"
            (set [ "algorithm"; "k1"; "b"; "top"; "maxContextCharacters" ])
            (set [ "algorithm"; "k1"; "b"; "top"; "maxContextCharacters" ])
            element

        let k1 = Internal.requiredProperty "k1" element |> fun value -> value.GetDouble()
        let b = Internal.requiredProperty "b" element |> fun value -> value.GetDouble()
        let top = Internal.requiredInt "top" element
        let contextLimit = Internal.requiredInt "maxContextCharacters" element

        if
            Internal.requiredString "algorithm" element <> "bm25"
            || Double.IsNaN(k1)
            || Double.IsInfinity(k1)
            || k1 <= 0.0
            || Double.IsNaN(b)
            || Double.IsInfinity(b)
            || b < 0.0
            || b > 1.0
            || top < 1
            || top > 100
            || contextLimit < 1000
        then
            Internal.fail "Retrieval-Rankingparameter sind ungueltig."

        { Algorithm = "bm25"
          K1 = k1
          B = b
          Top = top
          MaxContextCharacters = contextLimit }

    let private parseResult expectedRank (element: JsonElement) =
        let fields =
            set
                [ "rank"
                  "score"
                  "chunkId"
                  "path"
                  "startLine"
                  "endLine"
                  "sourceSha256"
                  "contentSha256"
                  "textSha256"
                  "trustClass" ]

        validateFields $"Retrieval-Treffer {expectedRank}" fields fields element
        let rank = Internal.requiredInt "rank" element

        let score =
            Internal.requiredProperty "score" element |> fun value -> value.GetDouble()

        let chunkId = Internal.requiredString "chunkId" element
        let path = Internal.requiredString "path" element
        let startLine = Internal.requiredInt "startLine" element
        let endLine = Internal.requiredInt "endLine" element
        let sourceHash = Internal.requiredString "sourceSha256" element
        let contentHash = Internal.requiredString "contentSha256" element
        let textHash = Internal.requiredString "textSha256" element
        let trust = Internal.requiredString "trustClass" element

        if
            rank <> expectedRank
            || Double.IsNaN(score)
            || Double.IsInfinity(score)
            || score < 0.0
            || not (Internal.isSha256 chunkId)
            || String.IsNullOrWhiteSpace(path)
            || Path.IsPathRooted(path)
            || path.Replace('\\', '/').Split('/') |> Array.exists ((=) "..")
            || startLine < 1
            || endLine < startLine
            || not (Internal.isSha256 sourceHash)
            || not (Internal.isSha256 contentHash)
            || not (Internal.isSha256 textHash)
            || not (trustClasses.Contains(trust))
        then
            Internal.fail $"Retrieval-Treffer {expectedRank} ist ungueltig."

        { Rank = rank
          Score = score
          ChunkId = chunkId
          Path = path.Replace('\\', '/')
          StartLine = startLine
          EndLine = endLine
          SourceSha256 = sourceHash
          ContentSha256 = contentHash
          TextSha256 = textHash
          TrustClass = trust }

    let private parseTrace (line: string) =
        try
            use document = JsonDocument.Parse(line)
            let root = document.RootElement

            let fields =
                set
                    [ "schemaVersion"
                      "runId"
                      "sequence"
                      "queryId"
                      "timestampUtc"
                      "query"
                      "querySha256"
                      "indexSha256"
                      "configSha256"
                      "ranking"
                      "results"
                      "context"
                      "contextSha256"
                      "previousTraceHash"
                      "traceHash" ]

            validateFields "Retrieval-Trace" fields fields root

            if Internal.requiredInt "schemaVersion" root <> Constants.SchemaVersion then
                Internal.fail "Retrieval-Trace hat eine nicht unterstuetzte Schema-Version."

            let previousElement = Internal.requiredProperty "previousTraceHash" root

            let previous =
                match previousElement.ValueKind with
                | JsonValueKind.Null -> None
                | JsonValueKind.String -> Some(previousElement.GetString())
                | _ -> Internal.fail "previousTraceHash muss String oder null sein."

            let resultsElement = Internal.requiredProperty "results" root

            if resultsElement.ValueKind <> JsonValueKind.Array then
                Internal.fail "Retrieval-results muss ein Array sein."

            { RunId = Internal.requiredString "runId" root
              Sequence = Internal.requiredInt64 "sequence" root
              QueryId = Internal.requiredString "queryId" root
              TimestampUtc = Internal.requiredString "timestampUtc" root
              Query = Internal.requiredString "query" root
              QuerySha256 = Internal.requiredString "querySha256" root
              IndexSha256 = Internal.requiredString "indexSha256" root
              ConfigSha256 = Internal.requiredString "configSha256" root
              Ranking = Internal.requiredProperty "ranking" root |> parseRanking
              Results =
                resultsElement.EnumerateArray()
                |> Seq.mapi (fun index result -> parseResult (index + 1) result)
                |> Seq.toList
              Context = Internal.requiredString "context" root
              ContextSha256 = Internal.requiredString "contextSha256" root
              PreviousTraceHash = previous
              TraceHash = Internal.requiredString "traceHash" root }
        with :? JsonException as error ->
            Internal.fail $"Ungueltiger Retrieval-Trace: {error.Message}"

    let private loadStrict policy maxLineBytes path expectedRunId =
        if not (File.Exists(path)) then
            Internal.fail $"Retrieval-Datei fehlt: {path}"

        let traces = ResizeArray<RetrievalTrace>()
        let mutable expectedSequence = 1L
        let mutable expectedPrevious: string option = None
        let mutable previousTimestamp: DateTimeOffset option = None

        File.ReadLines(path, Constants.Utf8NoBom)
        |> Seq.filter (String.IsNullOrWhiteSpace >> not)
        |> Seq.iter (fun line ->
            if int64 (Constants.Utf8NoBom.GetByteCount(line)) > maxLineBytes then
                Internal.fail $"Retrieval-Trace {expectedSequence} ueberschreitet das konfigurierte Payloadlimit."

            let trace = parseTrace line

            if trace.RunId <> expectedRunId then
                Internal.fail $"Retrieval-Trace {trace.Sequence}: Run-ID ist inkonsistent."

            if
                trace.Sequence <> expectedSequence
                || trace.PreviousTraceHash <> expectedPrevious
            then
                Internal.fail $"Retrieval-Trace {trace.Sequence}: Sequenz oder Hashkette ist unterbrochen."

            let timestamp =
                match Internal.tryParseUtc trace.TimestampUtc with
                | Some value -> value
                | None -> Internal.fail $"Retrieval-Trace {trace.Sequence}: Zeitstempel ist kein UTC-Wert."

            match previousTimestamp with
            | Some previous when timestamp < previous ->
                Internal.fail $"Retrieval-Trace {trace.Sequence}: Zeitstempel ist nicht monoton."
            | _ -> ()

            if
                not (Internal.isSha256 trace.QueryId)
                || not (Internal.isSha256 trace.QuerySha256)
                || not (Internal.isSha256 trace.IndexSha256)
                || not (Internal.isSha256 trace.ConfigSha256)
                || not (Internal.isSha256 trace.ContextSha256)
                || not (Internal.isSha256 trace.TraceHash)
                || (trace.PreviousTraceHash.IsSome
                    && not (Internal.isSha256 trace.PreviousTraceHash.Value))
            then
                Internal.fail $"Retrieval-Trace {trace.Sequence}: Hashfeld ist ungueltig."

            let expectedQueryId =
                Internal.sha256Text $"{trace.RunId}\n{trace.Sequence}\n{trace.QuerySha256}\n{trace.IndexSha256}"

            if
                trace.QuerySha256 <> Internal.sha256Text trace.Query
                || trace.ContextSha256 <> Internal.sha256Text trace.Context
                || trace.QueryId <> expectedQueryId
                || trace.Context.Length > trace.Ranking.MaxContextCharacters
            then
                Internal.fail $"Retrieval-Trace {trace.Sequence}: Query-/Kontextfelder sind inkonsistent."

            for value in
                seq {
                    trace.Query
                    trace.Context
                    yield! trace.Results |> Seq.map (fun result -> result.Path)
                } do
                if Internal.redactText policy value <> value then
                    Internal.fail $"Retrieval-Trace {trace.Sequence} enthaelt einen nicht redigierten Wert."

            let expectedHash = coreBytes trace |> Internal.sha256Hex

            if trace.TraceHash <> expectedHash then
                Internal.fail $"Retrieval-Trace {trace.Sequence}: traceHash ist ungueltig."

            traces.Add(trace)
            expectedSequence <- expectedSequence + 1L
            expectedPrevious <- Some trace.TraceHash
            previousTimestamp <- Some timestamp)

        traces |> Seq.toList

    let private runIsRunning runPath =
        let metadataPath = Path.Combine(runPath, "run.json")

        if not (File.Exists(metadataPath)) then
            Internal.fail $"Run-Metadaten fehlen: {metadataPath}"

        try
            use document = JsonDocument.Parse(File.ReadAllBytes(metadataPath))
            Internal.requiredString "status" document.RootElement = "running"
        with :? JsonException as error ->
            Internal.fail $"Ungueltige Run-Metadaten: {error.Message}"

    let withStableAnchor root runId required action =
        let locations = Workspace.requireInitialized root
        let config = HarnessConfig.load locations
        let runPath = Path.Combine(locations.Runs, runId)
        let path = tracePath locations runId

        if not (Directory.Exists(runPath)) then
            Internal.fail $"Run nicht gefunden: {runId}"

        let lockPath = Path.Combine(runPath, ".retrieval.lock")

        use lockHandle =
            try
                new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)
            with :? IOException as error ->
                Internal.fail $"Run ist bereits fuer einen Retrieval-Schreibvorgang gesperrt: {error.Message}"

        let traces =
            if File.Exists(path) then
                loadStrict config.Redaction config.MaxEventPayloadBytes path runId
            elif required then
                Internal.fail $"Retrieval-Datei fehlt: {path}"
            else
                []

        action
            { TraceCount = int64 traces.Length
              FinalTraceHash = traces |> List.tryLast |> Option.map (fun trace -> trace.TraceHash) }

    let record root runId (response: RagQueryResponse) =
        let locations = Workspace.requireInitialized root
        let config = HarnessConfig.load locations
        let runPath = Path.Combine(locations.Runs, runId)
        let path = tracePath locations runId

        if not (Directory.Exists(runPath)) then
            Internal.fail $"Run nicht gefunden: {runId}"

        let lockPath = Path.Combine(runPath, ".retrieval.lock")

        try
            use lockHandle =
                new FileStream(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None)

            if not (runIsRunning runPath) then
                Internal.fail $"Run {runId} ist bereits abgeschlossen."

            if not (File.Exists(path)) then
                // Kompatible Initialisierung fuer einen noch laufenden v1-Run.
                Internal.atomicWrite path Array.empty

            let existing = loadStrict config.Redaction config.MaxEventPayloadBytes path runId
            let sequence = int64 existing.Length + 1L
            let previous = existing |> List.tryLast |> Option.map (fun trace -> trace.TraceHash)
            let query = Internal.redactText config.Redaction response.Query

            let results, contextParts =
                response.Results
                |> List.mapi (fun index result ->
                    let text = Internal.redactText config.Redaction result.Text

                    { Rank = index + 1
                      Score = result.Score
                      ChunkId = result.ChunkId
                      Path = Internal.redactText config.Redaction result.Citation.Path
                      StartLine = result.Citation.StartLine
                      EndLine = result.Citation.EndLine
                      SourceSha256 = result.Citation.SourceSha256
                      ContentSha256 = result.Citation.ChunkSha256
                      TextSha256 = Internal.sha256Text text
                      TrustClass = result.TrustClass },
                    text)
                |> List.unzip

            let redactedContext = String.concat "" contextParts

            let context =
                if redactedContext.Length <= response.Ranking.MaxContextCharacters then
                    redactedContext
                else
                    let mutable length = response.Ranking.MaxContextCharacters

                    if
                        length > 0
                        && length < redactedContext.Length
                        && Char.IsHighSurrogate(redactedContext[length - 1])
                        && Char.IsLowSurrogate(redactedContext[length])
                    then
                        length <- length - 1

                    redactedContext.Substring(0, length)

            let queryHash = Internal.sha256Text query

            let traceWithoutHash =
                { RunId = runId
                  Sequence = sequence
                  QueryId = Internal.sha256Text $"{runId}\n{sequence}\n{queryHash}\n{response.IndexSha256}"
                  TimestampUtc = Internal.utcText DateTimeOffset.UtcNow
                  Query = query
                  QuerySha256 = queryHash
                  IndexSha256 = response.IndexSha256
                  ConfigSha256 = response.ConfigSha256
                  Ranking = response.Ranking
                  Results = results
                  Context = context
                  ContextSha256 = Internal.sha256Text context
                  PreviousTraceHash = previous
                  TraceHash = "" }

            let core = coreBytes traceWithoutHash
            let hash = Internal.sha256Hex core
            let line = lineBytes core hash

            if int64 line.LongLength > config.MaxEventPayloadBytes then
                Internal.fail "Retrieval-Trace ueberschreitet logging.maxEventPayloadBytes."

            use stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read)
            stream.Write(line, 0, line.Length)
            stream.WriteByte(byte '\n')
            stream.Flush(true)

            { QueryId = traceWithoutHash.QueryId
              Sequence = sequence
              TraceHash = hash }
        with :? IOException as error ->
            Internal.fail $"Run ist bereits fuer einen Retrieval-Schreibvorgang gesperrt: {error.Message}"

    /// Liste aller Trace-Hashes eines Laufs fuer Querverweise aus Ereignissen.
    let recordedTraceHashes root runId =
        let locations = Workspace.requireInitialized root
        let config = HarnessConfig.load locations
        let path = tracePath locations runId

        if File.Exists(path) then
            loadStrict config.Redaction config.MaxEventPayloadBytes path runId
            |> List.map (fun trace -> trace.TraceHash)
        else
            []

    let verifyRun root runId =
        let errors = ResizeArray<string>()

        try
            let locations = Workspace.paths root
            let config = HarnessConfig.load locations
            let path = tracePath locations runId

            // Runs aus Harness v1 duerfen noch keine Retrieval-Datei besitzen.
            if File.Exists(path) then
                loadStrict config.Redaction config.MaxEventPayloadBytes path runId |> ignore
        with
        | HarnessException message -> errors.Add(message)
        | error -> errors.Add(error.Message)

        errors |> Seq.toList
