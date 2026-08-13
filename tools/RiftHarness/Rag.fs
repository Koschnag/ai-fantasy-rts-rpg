namespace RiftHarness

open System
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Text
open System.Text.Json
open System.Text.RegularExpressions

type RagBuildReceipt =
    { SourceCount: int
      ChunkCount: int
      IndexHash: string
      IndexPath: string }

type RagCitation =
    { Path: string
      StartLine: int
      EndLine: int
      SourceSha256: string
      ChunkSha256: string }

type RagSearchResult =
    { Score: float
      Citation: RagCitation
      Text: string }

type RagQueryResponse =
    { Query: string
      Results: RagSearchResult list }

type private RagSourceSelection =
    | Patterns of string list
    | Roots of string list

type private RagConfig =
    { Selection: RagSourceSelection
      Extensions: string list
      ExcludedSegments: string list
      NeverIndex: string list
      MaxFileBytes: int64
      ChunkLines: int
      OverlapLines: int
      K1: float
      B: float
      DefaultTopK: int
      MaxContextCharacters: int
      MemoryPath: string }

type private IndexedSource =
    { Path: string
      Sha256: string
      LineCount: int }

type private IndexedChunk =
    { Id: string
      Path: string
      StartLine: int
      EndLine: int
      SourceSha256: string
      ChunkSha256: string
      Text: string
      TokenCount: int
      TermFrequencies: Map<string, int> }

type private IndexData =
    { ChunkLines: int
      OverlapLines: int
      K1: float
      B: float
      Sources: IndexedSource list
      Chunks: IndexedChunk list
      DocumentFrequency: Map<string, int>
      AverageDocumentLength: float }

[<RequireQualifiedAccess>]
module RagIndex =
    let private stopWords =
        set
            [ "a"
              "an"
              "and"
              "are"
              "as"
              "at"
              "auf"
              "aus"
              "be"
              "bei"
              "bis"
              "by"
              "das"
              "dass"
              "dem"
              "den"
              "der"
              "des"
              "die"
              "dies"
              "diese"
              "dieser"
              "do"
              "does"
              "ein"
              "eine"
              "einem"
              "einen"
              "einer"
              "er"
              "es"
              "for"
              "from"
              "für"
              "gelten"
              "gilt"
              "hat"
              "haben"
              "how"
              "i"
              "ich"
              "im"
              "in"
              "is"
              "ist"
              "it"
              "mit"
              "of"
              "on"
              "oder"
              "sind"
              "so"
              "the"
              "to"
              "und"
              "von"
              "was"
              "welche"
              "welchem"
              "welchen"
              "welcher"
              "welches"
              "what"
              "when"
              "where"
              "which"
              "wie"
              "wird"
              "with"
              "wo"
              "zu" ]

    let private loadConfig (locations: WorkspacePaths) =
        try
            use document = JsonDocument.Parse(File.ReadAllBytes(locations.Config))
            let root = document.RootElement

            if Internal.requiredInt "schemaVersion" root <> Constants.SchemaVersion then
                Internal.fail "config.json hat eine nicht unterstuetzte Schema-Version."

            let rag = Internal.requiredProperty "rag" root

            let readStringArray (field: string) (parent: JsonElement) =
                let value = Internal.requiredProperty field parent

                if value.ValueKind <> JsonValueKind.Array then
                    Internal.fail $"{field} muss ein JSON-Array sein."

                value.EnumerateArray()
                |> Seq.map (fun item ->
                    if
                        item.ValueKind <> JsonValueKind.String
                        || String.IsNullOrWhiteSpace(item.GetString())
                    then
                        Internal.fail $"Jeder Eintrag in {field} muss eine nicht leere Zeichenfolge sein."

                    item.GetString().Replace('\\', '/'))
                |> Seq.toList

            let optionalStringArray (field: string) (parent: JsonElement) (fallback: string list) =
                match parent.TryGetProperty(field) with
                | true, _ -> readStringArray field parent
                | _ -> fallback

            let selection, fullConfiguration =
                match rag.TryGetProperty("sources") with
                | true, _ ->
                    let sources = readStringArray "sources" rag

                    if List.isEmpty sources then
                        Internal.fail "rag.sources darf nicht leer sein."

                    Patterns sources, false
                | _ ->
                    let roots = readStringArray "roots" rag

                    if List.isEmpty roots then
                        Internal.fail "rag.roots darf nicht leer sein."

                    Roots roots, true

            let chunkLines = Internal.requiredInt "chunkLines" rag
            let overlapLines = Internal.requiredInt "overlapLines" rag

            let minimumChunkLines = if fullConfiguration then 8 else 1

            if chunkLines < minimumChunkLines || chunkLines > 500 then
                Internal.fail "rag.chunkLines muss zwischen 1 und 500 liegen."

            if overlapLines < 0 || overlapLines >= chunkLines then
                Internal.fail "rag.overlapLines muss zwischen 0 und chunkLines - 1 liegen."

            let extensions = optionalStringArray "extensions" rag []
            let excluded = optionalStringArray "excludedSegments" rag []

            let maxFileBytes =
                match rag.TryGetProperty("maxFileBytes") with
                | true, value ->
                    match value.TryGetInt64() with
                    | true, parsed when parsed >= 1024L -> parsed
                    | _ -> Internal.fail "rag.maxFileBytes muss mindestens 1024 sein."
                | _ -> 16L * 1024L * 1024L

            let k1, b =
                match rag.TryGetProperty("ranking") with
                | true, ranking ->
                    if Internal.requiredString "algorithm" ranking <> "bm25" then
                        Internal.fail "Nur der Ranking-Algorithmus 'bm25' wird unterstuetzt."

                    let parsedK1 =
                        Internal.requiredProperty "k1" ranking |> fun value -> value.GetDouble()

                    let parsedB =
                        Internal.requiredProperty "b" ranking |> fun value -> value.GetDouble()

                    if parsedK1 <= 0.0 || parsedB < 0.0 || parsedB > 1.0 then
                        Internal.fail "Ungueltige BM25-Parameter in config.json."

                    parsedK1, parsedB
                | _ -> 1.2, 0.75

            let optionalInt (field: string) (fallback: int) (minimum: int) (maximum: int) =
                match rag.TryGetProperty(field) with
                | true, value ->
                    match value.TryGetInt32() with
                    | true, parsed when parsed >= minimum && parsed <= maximum -> parsed
                    | _ -> Internal.fail $"rag.{field} liegt ausserhalb des erlaubten Bereichs."
                | _ -> fallback

            let neverIndex =
                match root.TryGetProperty("security") with
                | true, security -> optionalStringArray "neverIndex" security []
                | _ -> []

            let memoryPath =
                match root.TryGetProperty("paths") with
                | true, paths ->
                    match paths.TryGetProperty("memory") with
                    | true, value when
                        value.ValueKind = JsonValueKind.String
                        && not (String.IsNullOrWhiteSpace(value.GetString()))
                        ->
                        value.GetString().Replace('\\', '/')
                    | true, _ -> Internal.fail "paths.memory muss eine nicht leere Zeichenfolge sein."
                    | _ -> ".ai/memory/records.jsonl"
                | _ -> ".ai/memory/records.jsonl"

            if
                Path.IsPathRooted(memoryPath)
                || memoryPath.Split('/') |> Array.exists ((=) "..")
                || memoryPath.Contains('*')
                || memoryPath.Contains('?')
            then
                Internal.fail "paths.memory muss ein relativer Dateipfad ohne Globs oder '..' sein."

            { Selection = selection
              Extensions = extensions
              ExcludedSegments = excluded
              NeverIndex = neverIndex
              MaxFileBytes = maxFileBytes
              ChunkLines = chunkLines
              OverlapLines = overlapLines
              K1 = k1
              B = b
              DefaultTopK = optionalInt "defaultTopK" 5 1 50
              MaxContextCharacters = optionalInt "maxContextCharacters" 24000 1000 Int32.MaxValue
              MemoryPath = memoryPath.TrimStart('/') }
        with :? JsonException as error ->
            Internal.fail $"Ungueltige config.json: {error.Message}"

    let private validatePattern (pattern: string) =
        if Path.IsPathRooted(pattern) then
            Internal.fail $"RAG-Quellmuster muss relativ sein: {pattern}"

        if pattern.Split('/') |> Array.exists ((=) "..") then
            Internal.fail $"RAG-Quellmuster darf kein '..' enthalten: {pattern}"

        if pattern.StartsWith(".ai/runtime", StringComparison.Ordinal) then
            Internal.fail "RAG-Quellen duerfen nicht aus .ai/runtime stammen."

    let private globRegex pattern =
        validatePattern pattern
        let builder = StringBuilder("^")
        let mutable index = 0

        while index < pattern.Length do
            match pattern[index] with
            | '*' when index + 1 < pattern.Length && pattern[index + 1] = '*' ->
                if index + 2 < pattern.Length && pattern[index + 2] = '/' then
                    builder.Append("(?:.*/)?") |> ignore
                    index <- index + 3
                else
                    builder.Append(".*") |> ignore
                    index <- index + 2
            | '*' ->
                builder.Append("[^/]*") |> ignore
                index <- index + 1
            | '?' ->
                builder.Append("[^/]") |> ignore
                index <- index + 1
            | character ->
                builder.Append(Regex.Escape(string character)) |> ignore
                index <- index + 1

        builder.Append('$') |> ignore

        let options =
            if OperatingSystem.IsWindows() then
                RegexOptions.CultureInvariant ||| RegexOptions.IgnoreCase
            else
                RegexOptions.CultureInvariant

        Regex(builder.ToString(), options, TimeSpan.FromSeconds(2.0))

    let private pathMatchesRule (relative: string) (rule: string) =
        let normalized = rule.Replace('\\', '/').Trim('/')
        let segments = relative.Split('/', StringSplitOptions.RemoveEmptyEntries)

        if String.IsNullOrWhiteSpace(normalized) then
            false
        elif normalized.Contains('*') || normalized.Contains('?') then
            let matcher = globRegex normalized

            if normalized.Contains('/') then
                matcher.IsMatch(relative)
            else
                matcher.IsMatch(Path.GetFileName(relative))
        elif normalized.Contains('/') then
            relative = normalized
            || relative.StartsWith(normalized + "/", StringComparison.Ordinal)
        else
            segments |> Array.exists (fun segment -> segment = normalized)

    let private isExcluded (config: RagConfig) relative =
        relative = ".ai/runtime"
        || relative.StartsWith(".ai/runtime/", StringComparison.Ordinal)
        || config.ExcludedSegments |> List.exists (pathMatchesRule relative)
        || config.NeverIndex |> List.exists (pathMatchesRule relative)

    let private enumerateWorkspaceFiles (locations: WorkspacePaths) (config: RagConfig) =
        let rec walk directory =
            seq {
                for file in
                    Directory.EnumerateFiles(directory)
                    |> Seq.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right)) do
                    let attributes = File.GetAttributes(file)
                    let relative = Workspace.relativePath locations file

                    if
                        not (attributes.HasFlag(FileAttributes.ReparsePoint))
                        && not (isExcluded config relative)
                    then
                        yield file

                for child in
                    Directory.EnumerateDirectories(directory)
                    |> Seq.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right)) do
                    let attributes = File.GetAttributes(child)
                    let relative = Workspace.relativePath locations child

                    if
                        not (attributes.HasFlag(FileAttributes.ReparsePoint))
                        && not (isExcluded config relative)
                    then
                        yield! walk child
            }

        walk locations.Root

    let private resolveSources (locations: WorkspacePaths) (config: RagConfig) =
        let selected (relative: string) =
            match config.Selection with
            | Patterns patterns ->
                patterns
                |> List.map globRegex
                |> List.exists (fun matcher -> matcher.IsMatch(relative))
            | Roots roots ->
                roots
                |> List.exists (fun root ->
                    validatePattern root
                    let normalized = root.TrimEnd('/')

                    relative = normalized
                    || relative.StartsWith(normalized + "/", StringComparison.Ordinal))

        let allowedExtension (relative: string) =
            List.isEmpty config.Extensions
            || config.Extensions
               |> List.exists (fun extension ->
                   String.Equals(Path.GetExtension(relative), extension, StringComparison.OrdinalIgnoreCase))

        enumerateWorkspaceFiles locations config
        |> Seq.choose (fun path ->
            let relative = Workspace.relativePath locations path

            if
                selected relative
                && allowedExtension relative
                && FileInfo(path).Length <= config.MaxFileBytes
            then
                Some(relative, path)
            else
                None)
        |> Seq.distinctBy fst
        |> Seq.sortWith (fun (left, _) (right, _) -> StringComparer.Ordinal.Compare(left, right))
        |> Seq.toList

    let private normalizedLines (text: string) =
        let normalized = text.Replace("\r\n", "\n").Replace('\r', '\n')

        if normalized.Length = 0 then
            Array.empty
        else
            let lines = normalized.Split('\n')

            if normalized.EndsWith("\n", StringComparison.Ordinal) then
                lines[.. lines.Length - 2]
            else
                lines

    let private memoryPathEquals configured relative =
        let comparison =
            if OperatingSystem.IsWindows() then
                StringComparison.OrdinalIgnoreCase
            else
                StringComparison.Ordinal

        String.Equals(configured, relative, comparison)

    let private currentMemorySource (locations: WorkspacePaths) maxSourceBytes lineNumber (source: JsonElement) =
        let path =
            Internal.requiredString "path" source |> fun value -> value.Replace('\\', '/')

        let expectedHash = Internal.requiredString "sha256" source

        if
            Path.IsPathRooted(path)
            || path.Split('/') |> Array.exists ((=) "..")
            || path.Contains('*')
            || path.Contains('?')
        then
            Internal.fail $"Memory-Zeile {lineNumber}: Quellenpfad muss relativ und konkret sein: {path}"

        if
            expectedHash.Length <> 64
            || expectedHash
               |> Seq.exists (fun character -> not (Char.IsAsciiHexDigit(character)) || Char.IsUpper(character))
        then
            Internal.fail $"Memory-Zeile {lineNumber}: sha256 muss aus 64 kleinen Hex-Zeichen bestehen."

        let absolute =
            Path.Combine(locations.Root, path.Replace('/', Path.DirectorySeparatorChar))

        Workspace.isInside locations absolute
        && File.Exists(absolute)
        && FileInfo(absolute).Length <= maxSourceBytes
        && Internal.sha256File absolute = expectedHash

    let private filterMemoryLines (locations: WorkspacePaths) maxSourceBytes (lines: string array) =
        lines
        |> Array.mapi (fun index line ->
            let lineNumber = index + 1

            if String.IsNullOrWhiteSpace(line) then
                ""
            else
                try
                    use document = JsonDocument.Parse(line)
                    let record = document.RootElement

                    if Internal.requiredInt "schemaVersion" record <> Constants.SchemaVersion then
                        Internal.fail $"Memory-Zeile {lineNumber}: nicht unterstuetzte Schema-Version."

                    let status = Internal.requiredString "status" record

                    if status <> "accepted" then
                        ""
                    else
                        let sources = Internal.requiredProperty "sources" record

                        if sources.ValueKind <> JsonValueKind.Array || sources.GetArrayLength() = 0 then
                            Internal.fail $"Memory-Zeile {lineNumber}: accepted Record benoetigt Quellen."

                        let isCurrent =
                            sources.EnumerateArray()
                            |> Seq.forall (currentMemorySource locations maxSourceBytes lineNumber)

                        if isCurrent then line else ""
                with :? JsonException as error ->
                    Internal.fail $"Memory-Zeile {lineNumber} ist ungueltiges JSON: {error.Message}")

    let private sourceLines (locations: WorkspacePaths) (config: RagConfig) relative (bytes: byte array) =
        let text =
            try
                Constants.Utf8NoBom.GetString(bytes)
            with :? DecoderFallbackException as error ->
                Internal.fail $"RAG-Quelle ist kein gueltiges UTF-8 ({relative}): {error.Message}"

        let lines = normalizedLines text

        if memoryPathEquals config.MemoryPath relative then
            filterMemoryLines locations config.MaxFileBytes lines
        else
            lines

    let tokenize (text: string) =
        let tokens = ResizeArray<string>()
        let current = StringBuilder()

        let flush () =
            if current.Length > 0 then
                tokens.Add(current.ToString())
                current.Clear() |> ignore

        for character in text do
            if Char.IsLetterOrDigit(character) then
                current.Append(Char.ToLowerInvariant(character)) |> ignore
            else
                flush ()

        flush ()

        tokens
        |> Seq.filter (fun token -> not (stopWords.Contains(token)))
        |> Seq.toList

    let private termFrequencies text =
        tokenize text
        |> Seq.countBy id
        |> Seq.sortWith (fun (left, _) (right, _) -> StringComparer.Ordinal.Compare(left, right))
        |> Map.ofSeq

    let private createChunks chunkLines overlapLines (source: IndexedSource) (lines: string array) =
        let chunks = ResizeArray<IndexedChunk>()
        let step = chunkLines - overlapLines
        let mutable first = 0
        let mutable finished = lines.Length = 0

        while not finished do
            let exclusiveEnd = min lines.Length (first + chunkLines)
            let content = String.Join("\n", lines[first .. exclusiveEnd - 1])
            let frequencies = termFrequencies content
            let chunkHash = Internal.sha256Text content
            let startLine = first + 1
            let endLine = exclusiveEnd
            let idMaterial = $"{source.Path}\n{startLine}\n{endLine}\n{chunkHash}"

            if not (String.IsNullOrWhiteSpace(content)) then
                chunks.Add(
                    { Id = Internal.sha256Text idMaterial
                      Path = source.Path
                      StartLine = startLine
                      EndLine = endLine
                      SourceSha256 = source.Sha256
                      ChunkSha256 = chunkHash
                      Text = content
                      TokenCount = frequencies |> Seq.sumBy (fun pair -> pair.Value)
                      TermFrequencies = frequencies }
                )

            if exclusiveEnd = lines.Length then
                finished <- true
            else
                first <- first + step

        chunks |> Seq.toList

    let private createMemoryRecordChunks (source: IndexedSource) (lines: string array) =
        lines
        |> Array.indexed
        |> Array.choose (fun (index, content) ->
            if String.IsNullOrWhiteSpace(content) then
                None
            else
                let frequencies = termFrequencies content
                let chunkHash = Internal.sha256Text content
                let lineNumber = index + 1
                let idMaterial = $"{source.Path}\n{lineNumber}\n{lineNumber}\n{chunkHash}"

                Some
                    { Id = Internal.sha256Text idMaterial
                      Path = source.Path
                      StartLine = lineNumber
                      EndLine = lineNumber
                      SourceSha256 = source.Sha256
                      ChunkSha256 = chunkHash
                      Text = content
                      TokenCount = frequencies |> Seq.sumBy (fun pair -> pair.Value)
                      TermFrequencies = frequencies })
        |> Array.toList

    let private bodyBytes (index: IndexData) =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteString("algorithm", "bm25")
            writer.WriteString("tokenizer", "unicode-alphanumeric-lower-invariant-stopwords-de-en-v1")
            writer.WriteStartObject("parameters")
            writer.WriteNumber("chunkLines", index.ChunkLines)
            writer.WriteNumber("overlapLines", index.OverlapLines)
            writer.WriteNumber("k1", index.K1)
            writer.WriteNumber("b", index.B)
            writer.WriteEndObject()
            writer.WriteStartArray("sources")

            for source in index.Sources do
                writer.WriteStartObject()
                writer.WriteString("path", source.Path)
                writer.WriteString("sha256", source.Sha256)
                writer.WriteNumber("lineCount", source.LineCount)
                writer.WriteEndObject()

            writer.WriteEndArray()
            writer.WriteStartArray("chunks")

            for chunk in index.Chunks do
                writer.WriteStartObject()
                writer.WriteString("id", chunk.Id)
                writer.WriteString("path", chunk.Path)
                writer.WriteNumber("startLine", chunk.StartLine)
                writer.WriteNumber("endLine", chunk.EndLine)
                writer.WriteString("sourceSha256", chunk.SourceSha256)
                writer.WriteString("chunkSha256", chunk.ChunkSha256)
                writer.WriteString("text", chunk.Text)
                writer.WriteNumber("tokenCount", chunk.TokenCount)
                writer.WriteStartObject("termFrequencies")

                for KeyValue(term, count) in chunk.TermFrequencies do
                    writer.WriteNumber(term, count)

                writer.WriteEndObject()
                writer.WriteEndObject()

            writer.WriteEndArray()
            writer.WriteStartObject("documentFrequency")

            for KeyValue(term, count) in index.DocumentFrequency do
                writer.WriteNumber(term, count)

            writer.WriteEndObject()
            writer.WriteNumber("averageDocumentLength", index.AverageDocumentLength)
            writer.WriteEndObject())

    let private indexFileBytes (body: byte array) (hash: string) =
        Internal.jsonBytes true (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", Constants.SchemaVersion)
            writer.WriteString("indexHash", hash)
            Internal.rawJson writer "index" body
            writer.WriteEndObject())

    let private mapFromObject (element: JsonElement) : Map<string, int> =
        if element.ValueKind <> JsonValueKind.Object then
            Internal.fail "Erwartetes JSON-Objekt im RAG-Index."

        element.EnumerateObject()
        |> Seq.map (fun property ->
            match property.Value.TryGetInt32() with
            | true, count when count >= 0 -> property.Name, count
            | _ -> Internal.fail $"Indexwert fuer '{property.Name}' muss eine nichtnegative Ganzzahl sein.")
        |> Map.ofSeq

    let private parseIndex locations =
        if not (File.Exists(locations.IndexFile)) then
            Internal.fail $"RAG-Index fehlt: {locations.IndexFile}"

        try
            use document = JsonDocument.Parse(File.ReadAllBytes(locations.IndexFile))
            let root = document.RootElement

            if Internal.requiredInt "schemaVersion" root <> Constants.SchemaVersion then
                Internal.fail "RAG-Index hat eine nicht unterstuetzte Schema-Version."

            let storedHash = Internal.requiredString "indexHash" root
            let bodyElement = Internal.requiredProperty "index" root
            let rawBody = Constants.Utf8NoBom.GetBytes(bodyElement.GetRawText())
            let actualHash = Internal.sha256Hex rawBody

            if storedHash <> actualHash then
                Internal.fail "indexHash ist ungueltig."

            if Internal.requiredString "algorithm" bodyElement <> "bm25" then
                Internal.fail "Nicht unterstuetzter RAG-Algorithmus."

            if
                Internal.requiredString "tokenizer" bodyElement
                <> "unicode-alphanumeric-lower-invariant-stopwords-de-en-v1"
            then
                Internal.fail "Nicht unterstuetzte RAG-Tokenizer-Version."

            let parameters = Internal.requiredProperty "parameters" bodyElement
            let k1 = Internal.requiredProperty "k1" parameters |> fun value -> value.GetDouble()
            let b = Internal.requiredProperty "b" parameters |> fun value -> value.GetDouble()
            let sourceArray = Internal.requiredProperty "sources" bodyElement
            let chunkArray = Internal.requiredProperty "chunks" bodyElement

            if
                sourceArray.ValueKind <> JsonValueKind.Array
                || chunkArray.ValueKind <> JsonValueKind.Array
            then
                Internal.fail "RAG-Indexquellen und -chunks muessen Arrays sein."

            let sources =
                sourceArray.EnumerateArray()
                |> Seq.map (fun source ->
                    { Path = Internal.requiredString "path" source
                      Sha256 = Internal.requiredString "sha256" source
                      LineCount = Internal.requiredInt "lineCount" source })
                |> Seq.toList

            let chunks =
                chunkArray.EnumerateArray()
                |> Seq.map (fun chunk ->
                    { Id = Internal.requiredString "id" chunk
                      Path = Internal.requiredString "path" chunk
                      StartLine = Internal.requiredInt "startLine" chunk
                      EndLine = Internal.requiredInt "endLine" chunk
                      SourceSha256 = Internal.requiredString "sourceSha256" chunk
                      ChunkSha256 = Internal.requiredString "chunkSha256" chunk
                      Text = Internal.requiredString "text" chunk
                      TokenCount = Internal.requiredInt "tokenCount" chunk
                      TermFrequencies = Internal.requiredProperty "termFrequencies" chunk |> mapFromObject })
                |> Seq.toList

            let average =
                Internal.requiredProperty "averageDocumentLength" bodyElement
                |> fun value -> value.GetDouble()

            { ChunkLines = Internal.requiredInt "chunkLines" parameters
              OverlapLines = Internal.requiredInt "overlapLines" parameters
              K1 = k1
              B = b
              Sources = sources
              Chunks = chunks
              DocumentFrequency = Internal.requiredProperty "documentFrequency" bodyElement |> mapFromObject
              AverageDocumentLength = average },
            storedHash
        with :? JsonException as error ->
            Internal.fail $"Ungueltiger RAG-Index: {error.Message}"

    let build root =
        let locations = Workspace.requireInitialized root
        HarnessConfig.load locations |> ignore
        let config = loadConfig locations
        let resolved = resolveSources locations config

        if List.isEmpty resolved then
            Internal.fail "Die konfigurierten RAG-Quellmuster finden keine Dateien."

        let sourcesAndLines =
            resolved
            |> List.map (fun (relative, absolute) ->
                let info = FileInfo(absolute)

                if info.Length > config.MaxFileBytes then
                    Internal.fail $"RAG-Quelle ist groesser als {config.MaxFileBytes} Bytes: {relative}"

                let bytes = File.ReadAllBytes(absolute)

                let lines = sourceLines locations config relative bytes

                { Path = relative
                  Sha256 = Internal.sha256Hex bytes
                  LineCount = lines.Length },
                lines)

        let sources = sourcesAndLines |> List.map fst

        let chunks =
            sourcesAndLines
            |> List.collect (fun (source, lines) ->
                if memoryPathEquals config.MemoryPath source.Path then
                    createMemoryRecordChunks source lines
                else
                    createChunks config.ChunkLines config.OverlapLines source lines)

        let documentFrequency =
            chunks
            |> Seq.collect (fun chunk -> chunk.TermFrequencies |> Seq.map (fun pair -> pair.Key))
            |> Seq.countBy id
            |> Seq.sortWith (fun (left, _) (right, _) -> StringComparer.Ordinal.Compare(left, right))
            |> Map.ofSeq

        let average =
            if List.isEmpty chunks then
                0.0
            else
                chunks |> List.averageBy (fun chunk -> float chunk.TokenCount)

        let index =
            { ChunkLines = config.ChunkLines
              OverlapLines = config.OverlapLines
              K1 = config.K1
              B = config.B
              Sources = sources
              Chunks = chunks
              DocumentFrequency = documentFrequency
              AverageDocumentLength = average }

        let body = bodyBytes index
        let hash = Internal.sha256Hex body
        Internal.atomicWrite locations.IndexFile (indexFileBytes body hash)

        { SourceCount = sources.Length
          ChunkCount = chunks.Length
          IndexHash = hash
          IndexPath = locations.IndexFile }

    let query root queryText top =
        if String.IsNullOrWhiteSpace(queryText) then
            Internal.fail "RAG-Abfrage darf nicht leer sein."

        if top < 1 || top > 100 then
            Internal.fail "--top muss zwischen 1 und 100 liegen."

        let locations = Workspace.requireInitialized root
        HarnessConfig.load locations |> ignore
        let config = loadConfig locations
        let index, _ = parseIndex locations
        let queryTerms = tokenize queryText |> Seq.countBy id |> Map.ofSeq
        let documentCount = float index.Chunks.Length
        let averageLength = index.AverageDocumentLength

        let scoreChunk chunk =
            queryTerms
            |> Seq.sumBy (fun pair ->
                let term = pair.Key
                let queryFrequency = float pair.Value

                match chunk.TermFrequencies.TryFind(term), index.DocumentFrequency.TryFind(term) with
                | Some frequency, Some documentFrequency when averageLength > 0.0 ->
                    let tf = float frequency
                    let df = float documentFrequency
                    let idf = log (1.0 + ((documentCount - df + 0.5) / (df + 0.5)))

                    let normalization =
                        1.0 - index.B + index.B * (float chunk.TokenCount / averageLength)

                    idf
                    * ((tf * (index.K1 + 1.0)) / (tf + index.K1 * normalization))
                    * queryFrequency
                | _ -> 0.0)

        let rankedResults =
            index.Chunks
            |> List.map (fun chunk -> scoreChunk chunk, chunk)
            |> List.filter (fun (score, _) -> score > 0.0)
            |> List.sortWith (fun (leftScore, left) (rightScore, right) ->
                let scoreOrder = compare rightScore leftScore

                if scoreOrder <> 0 then
                    scoreOrder
                else
                    let pathOrder = StringComparer.Ordinal.Compare(left.Path, right.Path)

                    if pathOrder <> 0 then
                        pathOrder
                    else
                        compare left.StartLine right.StartLine)
            |> List.truncate top
            |> List.map (fun (score, chunk) ->
                { Score = score
                  Citation =
                    { Path = chunk.Path
                      StartLine = chunk.StartLine
                      EndLine = chunk.EndLine
                      SourceSha256 = chunk.SourceSha256
                      ChunkSha256 = chunk.ChunkSha256 }
                  Text = chunk.Text })

        let truncateWithoutSplittingSurrogate limit (text: string) =
            let mutable length = min limit text.Length

            if
                length > 0
                && length < text.Length
                && Char.IsHighSurrogate(text[length - 1])
                && Char.IsLowSurrogate(text[length])
            then
                length <- length - 1

            text.Substring(0, length)

        let rec applyContextBudget remaining (accumulated: RagSearchResult list) (pending: RagSearchResult list) =
            match pending with
            | [] -> List.rev accumulated
            | _ when remaining <= 0 -> List.rev accumulated
            | result :: tail ->
                let text = truncateWithoutSplittingSurrogate remaining result.Text

                if text.Length = 0 then
                    List.rev accumulated
                else
                    applyContextBudget (remaining - text.Length) ({ result with Text = text } :: accumulated) tail

        let results = applyContextBudget config.MaxContextCharacters [] rankedResults

        { Query = queryText; Results = results }

    let defaultTop root =
        let locations = Workspace.requireInitialized root
        HarnessConfig.load locations |> ignore
        (loadConfig locations).DefaultTopK

    let queryJson response =
        Internal.jsonBytes true (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", Constants.SchemaVersion)
            writer.WriteString("query", response.Query)
            writer.WriteStartArray("results")

            for result in response.Results do
                writer.WriteStartObject()
                writer.WriteNumber("score", result.Score)
                writer.WriteStartObject("citation")
                writer.WriteString("path", result.Citation.Path)
                writer.WriteNumber("startLine", result.Citation.StartLine)
                writer.WriteNumber("endLine", result.Citation.EndLine)
                writer.WriteString("sourceSha256", result.Citation.SourceSha256)
                writer.WriteString("chunkSha256", result.Citation.ChunkSha256)
                writer.WriteEndObject()
                writer.WriteString("text", result.Text)
                writer.WriteEndObject()

            writer.WriteEndArray()
            writer.WriteEndObject())
        |> Constants.Utf8NoBom.GetString

    let verify root =
        let errors = ResizeArray<string>()

        try
            let locations = Workspace.requireInitialized root
            HarnessConfig.load locations |> ignore
            let config = loadConfig locations
            let resolved = resolveSources locations config
            let index, _ = parseIndex locations

            if
                index.ChunkLines <> config.ChunkLines
                || index.OverlapLines <> config.OverlapLines
            then
                errors.Add("RAG-Indexparameter stimmen nicht mit config.json ueberein.")

            if index.K1 <> config.K1 || index.B <> config.B then
                errors.Add("RAG-Indexparameter stimmen nicht mit dem konfigurierten BM25-Ranking ueberein.")

            let resolvedPaths = resolved |> List.map fst
            let indexedPaths = index.Sources |> List.map (fun source -> source.Path)

            if resolvedPaths <> indexedPaths then
                errors.Add("RAG-Indexquellen stimmen nicht mit den konfigurierten Dateien ueberein.")

            let sourceLookup =
                index.Sources |> Seq.map (fun source -> source.Path, source) |> Map.ofSeq

            let sourceLineLookup = Dictionary<string, string array>(StringComparer.Ordinal)

            for source in index.Sources do
                let absolute =
                    Path.Combine(locations.Root, source.Path.Replace('/', Path.DirectorySeparatorChar))

                if not (Workspace.isInside locations absolute) || not (File.Exists(absolute)) then
                    errors.Add($"Indexquelle fehlt oder liegt ausserhalb des Workspace: {source.Path}")
                else
                    let bytes = File.ReadAllBytes(absolute)
                    let lines = sourceLines locations config source.Path bytes
                    sourceLineLookup[source.Path] <- lines

                    if Internal.sha256Hex bytes <> source.Sha256 then
                        errors.Add($"Quellhash ist veraltet: {source.Path}")

                    if lines.Length <> source.LineCount then
                        errors.Add($"Zeilenzahl ist inkonsistent: {source.Path}")

            let ids = HashSet<string>(StringComparer.Ordinal)

            for chunk in index.Chunks do
                if not (ids.Add(chunk.Id)) then
                    errors.Add($"Doppelte Chunk-ID: {chunk.Id}")

                match sourceLookup.TryFind(chunk.Path) with
                | None -> errors.Add($"Chunk verweist auf unbekannte Quelle: {chunk.Path}")
                | Some source ->
                    if chunk.SourceSha256 <> source.Sha256 then
                        errors.Add($"Chunk-Quellhash ist inkonsistent: {chunk.Id}")

                    match sourceLineLookup.TryGetValue(chunk.Path) with
                    | true, lines when
                        chunk.StartLine >= 1
                        && chunk.EndLine >= chunk.StartLine
                        && chunk.EndLine <= lines.Length
                        ->
                        let expectedText =
                            String.Join("\n", lines[chunk.StartLine - 1 .. chunk.EndLine - 1])

                        if expectedText <> chunk.Text then
                            errors.Add($"Chunktext stimmt nicht mit Quelle ueberein: {chunk.Id}")
                    | _ -> errors.Add($"Ungueltiger Zeilenbereich fuer Chunk: {chunk.Id}")

                let expectedFrequencies = termFrequencies chunk.Text
                let expectedChunkHash = Internal.sha256Text chunk.Text

                let expectedId =
                    Internal.sha256Text $"{chunk.Path}\n{chunk.StartLine}\n{chunk.EndLine}\n{expectedChunkHash}"

                if
                    chunk.TermFrequencies <> expectedFrequencies
                    || chunk.TokenCount <> (expectedFrequencies |> Seq.sumBy (fun pair -> pair.Value))
                then
                    errors.Add($"Termfrequenzen sind inkonsistent: {chunk.Id}")

                if chunk.ChunkSha256 <> expectedChunkHash || chunk.Id <> expectedId then
                    errors.Add($"Chunkhash oder -ID ist inkonsistent: {chunk.Id}")

            let expectedDocumentFrequency =
                index.Chunks
                |> Seq.collect (fun chunk -> chunk.TermFrequencies |> Seq.map (fun pair -> pair.Key))
                |> Seq.countBy id
                |> Map.ofSeq

            if expectedDocumentFrequency <> index.DocumentFrequency then
                errors.Add("Dokumentfrequenzen im RAG-Index sind inkonsistent.")

            let expectedAverage =
                if List.isEmpty index.Chunks then
                    0.0
                else
                    index.Chunks |> List.averageBy (fun chunk -> float chunk.TokenCount)

            if abs (expectedAverage - index.AverageDocumentLength) > 1e-12 then
                errors.Add("Mittlere Dokumentlaenge im RAG-Index ist inkonsistent.")
        with
        | HarnessException message -> errors.Add(message)
        | error -> errors.Add(error.Message)

        errors |> Seq.toList
