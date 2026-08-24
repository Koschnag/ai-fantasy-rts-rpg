namespace RiftHarness

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Globalization
open System.Text
open System.Text.Json
open System.Text.RegularExpressions
open System.Threading.Tasks
open System.Xml
open System.Security.Cryptography
open global.Json.Schema

type AssetFinding =
    { Severity: string
      Code: string
      Manifest: string option
      Path: string
      Message: string
      MatchSha256: string option }

type AssetCheckOptions =
    { ManifestPath: string option
      RequireLocal: bool
      RequireApproved: bool }

type AssetCheckReport =
    { Scope: string
      Valid: bool
      ShippingReady: bool
      ManifestsChecked: int
      ApprovedCount: int
      QuarantineCount: int
      Findings: AssetFinding list }

type GenerationReceiptExport =
    { RunId: string
      AssetId: string
      ReceiptPath: string
      ReceiptSha256: string }

type GenerationReceiptPrepared =
    { RunId: string
      AssetId: string
      ReceiptPath: string
      ReceiptSha256: string
      Bytes: byte array }

type private AssetSchemas =
    { Manifest: JsonSchema
      Receipt: JsonSchema
      ReviewEvidence: JsonSchema
      ModelsLock: JsonSchema
      CleanRoomPolicy: JsonSchema }

[<RequireQualifiedAccess>]
module AssetStore =
    let private manifestSchemaRelative = ".ai/schemas/asset-manifest.schema.json"
    let private receiptSchemaRelative = ".ai/schemas/generation-receipt.schema.json"

    let private reviewEvidenceSchemaRelative =
        ".ai/schemas/asset-review-evidence.schema.json"

    let private modelsLockSchemaRelative = ".ai/schemas/models-lock.schema.json"

    let private cleanRoomPolicySchemaRelative =
        ".ai/schemas/asset-clean-room-policy.schema.json"

    let private modelsLockRelative = "models.lock.json"
    let private cleanRoomPolicyRelative = ".ai/policies/asset-clean-room.json"
    let private maxAssetSourceBytes = 512L * 1024L * 1024L

    let private approvedReviewKinds =
        set [ "technical"; "visual"; "performance"; "originality"; "license" ]

    let private approvedGeneratorKinds = set [ "ai"; "procedural" ]

    let private textSourceExtensions =
        set
            [ ".cfg"
              ".cs"
              ".csv"
              ".fs"
              ".fsx"
              ".gltf"
              ".glsl"
              ".hlsl"
              ".json"
              ".md"
              ".py"
              ".shader"
              ".svg"
              ".toml"
              ".txt"
              ".xml"
              ".yaml"
              ".yml" ]

    let private reportFinding severity code manifest path message =
        { Severity = severity
          Code = code
          Manifest = manifest
          Path = path
          Message = message
          MatchSha256 = None }

    let private safeRelativePath (locations: WorkspacePaths) description allowMissing (path: string) =
        let absolute =
            if Path.IsPathRooted(path) then
                path
            else
                Path.Combine(locations.Root, path)

        Workspace.requireSafePath locations description allowMissing absolute

    let private safeManifestPath (locations: WorkspacePaths) description allowMissing (path: string) =
        let segments = path.Split('/')

        let hasUnsafePathCharacter =
            path
            |> Seq.exists (fun character ->
                Char.IsControl(character)
                || CharUnicodeInfo.GetUnicodeCategory(character) = UnicodeCategory.Format)

        if
            Path.IsPathRooted(path)
            || path.Contains('\\')
            || hasUnsafePathCharacter
            || path <> path.Normalize(NormalizationForm.FormC)
            || segments
               |> Array.exists (fun segment -> String.IsNullOrEmpty(segment) || segment = "." || segment = "..")
        then
            Internal.fail $"{description} muss ein kanonischer relativer Workspace-Pfad sein."

        safeRelativePath locations description allowMissing path

    let private ensureNoDuplicateKeys description (bytes: byte array) =
        let options =
            JsonReaderOptions(CommentHandling = JsonCommentHandling.Disallow, AllowTrailingCommas = false)

        let mutable reader = Utf8JsonReader(ReadOnlySpan<byte>(bytes), options)
        let objectKeys = Stack<HashSet<string>>()

        while reader.Read() do
            match reader.TokenType with
            | JsonTokenType.StartObject -> objectKeys.Push(HashSet<string>(StringComparer.Ordinal))
            | JsonTokenType.EndObject -> objectKeys.Pop() |> ignore
            | JsonTokenType.PropertyName ->
                let name = reader.GetString()

                if objectKeys.Count = 0 || not (objectKeys.Peek().Add(name)) then
                    Internal.fail $"{description} enthaelt einen doppelten JSON-Schluessel."
            | _ -> ()

    let private withRegularFile (path: string) (description: string) (action: FileStream -> 'result) =
        try
            let attributes = File.GetAttributes(path)

            if
                attributes.HasFlag(FileAttributes.Directory)
                || attributes.HasFlag(FileAttributes.ReparsePoint)
                || not (File.Exists(path))
            then
                Internal.fail $"{description} ist keine regulaere lokale Datei."

            use stream =
                new FileStream(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    64 * 1024,
                    FileOptions.SequentialScan
                )

            let openedAttributes = File.GetAttributes(path)

            if
                openedAttributes.HasFlag(FileAttributes.Directory)
                || openedAttributes.HasFlag(FileAttributes.ReparsePoint)
                || stream.Length < 0L
            then
                Internal.fail $"{description} besitzt eine ungueltige Dateilaenge."

            let result = action stream
            let finalAttributes = File.GetAttributes(path)

            if
                finalAttributes.HasFlag(FileAttributes.Directory)
                || finalAttributes.HasFlag(FileAttributes.ReparsePoint)
                || not (File.Exists(path))
            then
                Internal.fail $"{description} wurde waehrend des Lesens ausgetauscht."

            result
        with
        | HarnessException _ -> reraise ()
        | :? IOException
        | :? UnauthorizedAccessException
        | :? NotSupportedException
        | :? System.Security.SecurityException ->
            Internal.fail $"{description} konnte nicht sicher als regulaere Datei gelesen werden."

    let private safeFileBytes path description =
        withRegularFile path description (fun stream ->
            let expectedLength = stream.Length

            if expectedLength > Constants.MaxPayloadBytes then
                Internal.fail $"{description} ueberschreitet das Dateilimit."

            let bytes = Array.zeroCreate<byte> (int expectedLength)
            let mutable offset = 0

            while offset < bytes.Length do
                let count = stream.Read(bytes, offset, bytes.Length - offset)

                if count = 0 then
                    Internal.fail $"{description} wurde waehrend des Lesens verkuerzt."

                offset <- offset + count

            if stream.ReadByte() <> -1 || stream.Length <> expectedLength then
                Internal.fail $"{description} wurde waehrend des Lesens veraendert."

            bytes)

    let private safeFileHashAndLength path description =
        withRegularFile path description (fun stream ->
            let expectedLength = stream.Length

            if expectedLength > maxAssetSourceBytes then
                Internal.fail $"{description} ueberschreitet das Assetquelllimit."

            use algorithm = IncrementalHash.CreateHash(HashAlgorithmName.SHA256)
            let buffer = Array.zeroCreate<byte> (64 * 1024)
            let mutable total = 0L
            let mutable remaining = expectedLength

            while remaining > 0L do
                let requested = int (min remaining (int64 buffer.Length))
                let count = stream.Read(buffer, 0, requested)

                if count = 0 then
                    Internal.fail $"{description} wurde waehrend der Hashpruefung verkuerzt."

                algorithm.AppendData(buffer, 0, count)
                total <- total + int64 count
                remaining <- remaining - int64 count

            if
                stream.ReadByte() <> -1
                || total <> expectedLength
                || stream.Length <> expectedLength
            then
                Internal.fail $"{description} wurde waehrend der Hashpruefung veraendert."

            Convert.ToHexString(algorithm.GetHashAndReset()).ToLowerInvariant(), total)

    let private safeJsonBytes path description =
        let bytes = safeFileBytes path description
        ensureNoDuplicateKeys description bytes
        bytes

    // JsonSchema.Net is isolated here so domain/cross-field rules remain independent of a schema library.
    let private schemaErrors (schema: JsonSchema) instancePath =
        use instance = JsonDocument.Parse(safeJsonBytes instancePath "JSON-Instanz")

        let options =
            EvaluationOptions(OutputFormat = OutputFormat.List, RequireFormatValidation = true)

        let result = schema.Evaluate(instance.RootElement, options)

        if result.IsValid then
            []
        else
            result.Details
            |> Seq.filter (fun detail -> not detail.IsValid)
            |> Seq.map (fun detail ->
                "schema-location-sha256:"
                + Internal.sha256Text ("schema-location-v1\u0000" + detail.InstanceLocation.ToString()))
            |> Seq.distinct
            |> Seq.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right))
            |> Seq.toList

    let private readSchemaText (locations: WorkspacePaths) (relative: string) =
        let path = safeManifestPath locations "JSON-Schema" false relative

        let bytes = safeJsonBytes path "JSON-Schema"
        Constants.Utf8NoBom.GetString(bytes)

    let private loadSchemas locations =
        let registry = SchemaRegistry()
        registry.Fetch <- Func<Uri, SchemaRegistry, IBaseDocument>(fun _ _ -> null)
        let options = BuildOptions(SchemaRegistry = registry)

        let manifest =
            JsonSchema.FromText(readSchemaText locations manifestSchemaRelative, options)

        registry.Register(Uri("asset-manifest.schema.json", UriKind.RelativeOrAbsolute), manifest)

        { Manifest = manifest
          Receipt = JsonSchema.FromText(readSchemaText locations receiptSchemaRelative, options)
          ReviewEvidence = JsonSchema.FromText(readSchemaText locations reviewEvidenceSchemaRelative, options)
          ModelsLock = JsonSchema.FromText(readSchemaText locations modelsLockSchemaRelative, options)
          CleanRoomPolicy = JsonSchema.FromText(readSchemaText locations cleanRoomPolicySchemaRelative, options) }

    let private getString (name: string) (element: JsonElement) =
        match element.TryGetProperty(name) with
        | true, value when value.ValueKind = JsonValueKind.String && not (isNull (value.GetString())) ->
            value.GetString()
        | _ -> Internal.fail $"JSON-Stringfeld '{name}' fehlt oder ist ungueltig."

    let private getOptionalString (name: string) (element: JsonElement) =
        match element.TryGetProperty(name) with
        | true, value when value.ValueKind = JsonValueKind.Null -> None
        | true, value when value.ValueKind = JsonValueKind.String && not (isNull (value.GetString())) ->
            Some(value.GetString())
        | _ -> Internal.fail $"Optionales JSON-Stringfeld '{name}' fehlt oder ist ungueltig."

    let private pathStartsWith (prefix: string) (path: string) =
        path.Replace('\\', '/').StartsWith(prefix, StringComparison.Ordinal)

    let private addError (findings: ResizeArray<AssetFinding>) code manifest path message =
        findings.Add(reportFinding "error" code manifest path message)

    let private addWarning (findings: ResizeArray<AssetFinding>) code manifest path message =
        findings.Add(reportFinding "warning" code manifest path message)

    let private addPolicyFinding (findings: ResizeArray<AssetFinding>) code manifest path entryId =
        findings.Add(
            { Severity = "error"
              Code = code
              Manifest = manifest
              Path = path
              Message = "Clean-Room-Policytreffer; Inhalt wird nicht ausgegeben."
              MatchSha256 = Some(Internal.sha256Text ("finding-v1\u0000" + entryId)) }
        )

    let private addPolicyWarning (findings: ResizeArray<AssetFinding>) code manifest path entryId =
        findings.Add(
            { Severity = "warning"
              Code = code
              Manifest = manifest
              Path = path
              Message = "Intern registrierter Clean-Room-Name erkannt; Inhalt wird nicht ausgegeben."
              MatchSha256 = Some(Internal.sha256Text ("finding-v1\u0000" + entryId)) }
        )

    let private normalizeCleanRoomText (value: string) =
        let normalized = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant()
        let builder = StringBuilder(normalized.Length)
        let mutable previousSpace = true

        for character in normalized do
            let category = CharUnicodeInfo.GetUnicodeCategory(character)

            if Char.IsLetterOrDigit(character) then
                builder.Append(character) |> ignore
                previousSpace <- false
            elif
                category = UnicodeCategory.NonSpacingMark
                || category = UnicodeCategory.SpacingCombiningMark
                || category = UnicodeCategory.EnclosingMark
            then
                ()
            elif not previousSpace then
                builder.Append(' ') |> ignore
                previousSpace <- true

        builder.ToString().Trim()

    let private isUnsafeUnicode (character: char) =
        let code = int character
        let category = CharUnicodeInfo.GetUnicodeCategory(character)

        (Char.IsControl(character)
         && character <> '\n'
         && character <> '\r'
         && character <> '\t')
        || category = UnicodeCategory.Format
        || code = 0x200B
        || code = 0x200C
        || code = 0x200D
        || code = 0x2060
        || (code >= 0x202A && code <= 0x202E)
        || (code >= 0x2066 && code <= 0x2069)

    let private hasUnsafeUnicode (value: string) = value |> Seq.exists isUnsafeUnicode

    let private strictUtf8Text description (bytes: byte array) =
        try
            let text = UTF8Encoding(false, true).GetString(bytes)

            if hasUnsafeUnicode text then
                Internal.fail $"{description} enthaelt unzulaessige Textzeichen."

            text
        with :? DecoderFallbackException ->
            Internal.fail $"{description} ist kein striktes UTF-8."

    let private validateStructuredText extension description (bytes: byte array) (text: string) =
        match extension with
        | ".json"
        | ".gltf" ->
            ensureNoDuplicateKeys description bytes
            use document = JsonDocument.Parse(bytes)

            if document.RootElement.ValueKind <> JsonValueKind.Object then
                Internal.fail $"{description} muss ein JSON-Objekt sein."

            if extension = ".gltf" then
                match document.RootElement.TryGetProperty("asset") with
                | true, asset when asset.ValueKind = JsonValueKind.Object && getString "version" asset = "2.0" -> ()
                | _ -> Internal.fail $"{description} ist kein glTF-2.0-JSON."
        | ".xml"
        | ".svg" ->
            let settings =
                XmlReaderSettings(
                    DtdProcessing = DtdProcessing.Prohibit,
                    XmlResolver = null,
                    MaxCharactersInDocument = int64 bytes.Length
                )

            use stringReader = new StringReader(text)
            use reader = XmlReader.Create(stringReader, settings)
            let mutable rootName: string option = None

            while reader.Read() do
                if reader.NodeType = XmlNodeType.Element && rootName.IsNone then
                    rootName <- Some reader.LocalName

            if rootName.IsNone || (extension = ".svg" && rootName <> Some "svg") then
                Internal.fail $"{description} besitzt kein passendes XML-Wurzelelement."
        | _ -> ()

    let private textMediaTypes extension =
        match extension with
        | ".gltf" -> set [ "model/gltf+json" ]
        | ".json" -> set [ "application/json" ]
        | ".svg" -> set [ "image/svg+xml" ]
        | ".xml" -> set [ "application/xml"; "text/xml" ]
        | ".csv" -> set [ "text/csv" ]
        | ".yaml"
        | ".yml" -> set [ "application/yaml"; "text/yaml"; "text/x-yaml" ]
        | ".toml" -> set [ "application/toml"; "text/toml" ]
        | ".py" -> set [ "text/x-python"; "text/plain" ]
        | extension when textSourceExtensions.Contains(extension) -> set [ "text/plain" ]
        | _ -> Set.empty

    let private readValidatedText path description extension =
        try
            let bytes = safeFileBytes path description
            let text = strictUtf8Text description bytes
            validateStructuredText extension description bytes text
            text
        with
        | HarnessException _ -> reraise ()
        | :? JsonException
        | :? XmlException
        | :? InvalidOperationException -> Internal.fail $"{description} verletzt den Textformatvertrag."

    let private policyHash kind value =
        Internal.sha256Text ("clean-room-v1\u0000" + kind + "\u0000" + value)

    let private cleanRoomMatches
        (maxWords: int)
        kind
        (denied: Map<string, string>)
        (allowed: Map<string, string>)
        (value: string)
        =
        let words =
            normalizeCleanRoomText value
            |> fun normalized -> normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries)

        let matches = ResizeArray<string>()
        let allowedMatches = ResizeArray<string>()

        for start = 0 to words.Length - 1 do
            for count = 1 to min maxWords (words.Length - start) do
                let phrase = String.Join(" ", words, start, count)
                let hash = policyHash kind phrase
                let allowedEntry = allowed.TryFind(policyHash "allowed-name" phrase)

                match denied.TryFind(hash) with
                | Some entryId -> matches.Add(entryId)
                | None -> allowedEntry |> Option.iter allowedMatches.Add

        matches |> Seq.distinct |> Seq.toList, allowedMatches |> Seq.distinct |> Seq.toList

    let rec private scanStrings path (element: JsonElement) =
        seq {
            match element.ValueKind with
            | JsonValueKind.String -> yield path, element.GetString()
            | JsonValueKind.Object ->
                for property in element.EnumerateObject() do
                    yield path + "/$key", property.Name
                    yield! scanStrings (path + "/" + property.Name) property.Value
            | JsonValueKind.Array ->
                for index, item in element.EnumerateArray() |> Seq.indexed do
                    yield! scanStrings (path + "/" + string index) item
            | _ -> ()
        }

    let private opaqueManifestReference (relative: string) =
        "manifest-sha256:"
        + Internal.sha256Text ("manifest-reference-v1\u0000" + relative)

    let private validatePolicySchema locations (schema: JsonSchema) findings =
        let policyPath =
            safeManifestPath locations "Clean-Room-Policy" false cleanRoomPolicyRelative

        let policySchemaErrors = schemaErrors schema policyPath

        for path in policySchemaErrors do
            addError findings "ASSET_POLICY_SCHEMA_INVALID" None path "Clean-Room-Policy verletzt ihr Schema."

        if not (List.isEmpty policySchemaErrors) then
            1, Map.empty, Map.empty, Map.empty
        else
            use document = JsonDocument.Parse(safeJsonBytes policyPath "Clean-Room-Policy")
            let root = document.RootElement

            let entries = root.GetProperty("entries").EnumerateArray() |> Seq.toList
            let ids = HashSet<string>(StringComparer.Ordinal)
            let tuples = HashSet<string>(StringComparer.Ordinal)

            for entry in entries do
                let id = getString "policyEntryId" entry
                let tuple = getString "kind" entry + "\u0000" + getString "valueSha256" entry

                if not (ids.Add(id)) || not (tuples.Add(tuple)) then
                    addError
                        findings
                        "ASSET_POLICY_DUPLICATE"
                        None
                        "/entries"
                        "Clean-Room-Policyeintrag ist nicht eindeutig."

            let map kind =
                entries
                |> Seq.filter (fun entry -> getString "kind" entry = kind)
                |> Seq.map (fun entry -> getString "valueSha256" entry, getString "policyEntryId" entry)
                |> Map.ofSeq

            let allowed = map "allowed-name"

            let allHashes = HashSet<string>(StringComparer.Ordinal)

            for entry in entries do
                if not (allHashes.Add(getString "valueSha256" entry)) then
                    addError
                        findings
                        "ASSET_POLICY_CROSS_KIND_COLLISION"
                        None
                        "/entries"
                        "Clean-Room-Policywerte muessen zwischen Kategorien disjunkt sein."

            root.GetProperty("maxNGramWords").GetInt32(), map "denied-name", allowed, map "denied-style"

    let private validateCleanRoomElement policy manifestRelative scope findings (root: JsonElement) =
        let maxWords, deniedNames, allowedNames, deniedStyle = policy

        for path, value in scanStrings "" root do
            if not (String.IsNullOrEmpty(value)) then
                if hasUnsafeUnicode value then
                    addError
                        findings
                        "CLEAN_ROOM_UNSAFE_UNICODE"
                        (Some(opaqueManifestReference manifestRelative))
                        scope
                        "Steuer-, Bidi- oder unsichtbares Formatzeichen ist im Manifest unzulaessig."

                let deniedNameMatches, allowedNameMatches =
                    cleanRoomMatches maxWords "denied-name" deniedNames allowedNames value

                for entryId in deniedNameMatches do
                    addPolicyFinding
                        findings
                        "CLEAN_ROOM_DENIED_NAME"
                        (Some(opaqueManifestReference manifestRelative))
                        scope
                        entryId

                for entryId in allowedNameMatches do
                    addPolicyWarning
                        findings
                        "CLEAN_ROOM_ALLOWED_NAME"
                        (Some(opaqueManifestReference manifestRelative))
                        scope
                        entryId

                let deniedStyleMatches, _ =
                    cleanRoomMatches maxWords "denied-style" deniedStyle Map.empty value

                for entryId in deniedStyleMatches do
                    addPolicyFinding
                        findings
                        "CLEAN_ROOM_DENIED_STYLE"
                        (Some(opaqueManifestReference manifestRelative))
                        scope
                        entryId

    let private validateCleanRoomText policy manifestRelative scope findings (value: string) =
        use document =
            JsonDocument.Parse(
                Internal.jsonBytes false (fun writer ->
                    writer.WriteStartObject()
                    writer.WriteString("text", value)
                    writer.WriteEndObject())
            )

        validateCleanRoomElement policy manifestRelative scope findings document.RootElement

    let private validateCleanRoom policy manifestRelative findings (manifestRoot: JsonElement) =
        let maxWords, deniedNames, allowedNames, deniedStyle = policy
        validateCleanRoomElement policy manifestRelative "manifest-content" findings manifestRoot

        let fileName = Path.GetFileNameWithoutExtension(manifestRelative)

        if hasUnsafeUnicode fileName then
            addError
                findings
                "CLEAN_ROOM_UNSAFE_UNICODE"
                (Some(opaqueManifestReference manifestRelative))
                "manifest-filename"
                "Unsicheres Unicodezeichen ist im Dateinamen unzulaessig."

        let deniedFileNames, allowedFileNames =
            cleanRoomMatches maxWords "denied-name" deniedNames allowedNames fileName

        for entryId in deniedFileNames do
            addPolicyFinding
                findings
                "CLEAN_ROOM_DENIED_NAME"
                (Some(opaqueManifestReference manifestRelative))
                "manifest-filename"
                entryId

        for entryId in allowedFileNames do
            addPolicyWarning
                findings
                "CLEAN_ROOM_ALLOWED_NAME"
                (Some(opaqueManifestReference manifestRelative))
                "manifest-filename"
                entryId

        let deniedFileStyles, _ =
            cleanRoomMatches maxWords "denied-style" deniedStyle Map.empty fileName

        for entryId in deniedFileStyles do
            addPolicyFinding
                findings
                "CLEAN_ROOM_DENIED_STYLE"
                (Some(opaqueManifestReference manifestRelative))
                "manifest-filename"
                entryId

    let private validateCleanRoomInputs locations policy manifestRelative findings (manifestRoot: JsonElement) =
        let generator = manifestRoot.GetProperty("generator")

        for input in manifestRoot.GetProperty("inputs").EnumerateArray() do
            match getOptionalString "path" input with
            | Some relative ->
                let extension = Path.GetExtension(relative).ToLowerInvariant()

                let mustScanAsText =
                    textSourceExtensions.Contains(extension)
                    || getString "allowedUse" input = "internal-specification"
                    || (getString "kind" generator = "procedural"
                        && getString "allowedUse" input = "generation-input")

                if mustScanAsText then
                    try
                        let absolute = safeManifestPath locations "Clean-Room-Eingabe" false relative
                        let text = readValidatedText absolute "Clean-Room-Eingabe" extension
                        validateCleanRoomText policy manifestRelative "input-content" findings text
                    with HarnessException _ ->
                        addError
                            findings
                            "CLEAN_ROOM_INPUT_UNREADABLE"
                            (Some(opaqueManifestReference manifestRelative))
                            "input-content"
                            "Textuelle Asset-Eingabe konnte nicht sicher geprueft werden."
            | None -> ()

    let private validateCleanRoomOutputs locations policy manifestRelative findings (manifestRoot: JsonElement) =
        for output in manifestRoot.GetProperty("outputs").EnumerateArray() do
            let relative = getString "path" output
            let extension = Path.GetExtension(relative).ToLowerInvariant()

            if textSourceExtensions.Contains(extension) then
                try
                    let allowMissing = getString "status" manifestRoot <> "approved"

                    let absolute = safeManifestPath locations "Clean-Room-Output" allowMissing relative

                    if File.Exists(absolute) then
                        let text = readValidatedText absolute "Clean-Room-Output" extension
                        validateCleanRoomText policy manifestRelative "output-content" findings text
                with HarnessException _ ->
                    addError
                        findings
                        "CLEAN_ROOM_OUTPUT_UNREADABLE"
                        (Some(opaqueManifestReference manifestRelative))
                        "output-content"
                        "Textueller Assetoutput konnte nicht sicher geprueft werden."

    let private processResultWithTimeout
        (timeoutMilliseconds: int)
        (root: string)
        (executable: string)
        (arguments: string list)
        =
        try
            let startInfo = ProcessStartInfo(executable)
            startInfo.WorkingDirectory <- root
            startInfo.UseShellExecute <- false
            startInfo.RedirectStandardOutput <- true
            startInfo.RedirectStandardError <- true

            for argument in arguments do
                startInfo.ArgumentList.Add(argument)

            use child = Process.Start(startInfo)

            if isNull child then
                Internal.fail "Subprozess konnte nicht gestartet werden."

            let readBounded (reader: StreamReader) =
                task {
                    let buffer = Array.zeroCreate<char> 4096
                    let builder = StringBuilder()
                    let mutable reading = true

                    while reading do
                        let! count = reader.ReadAsync(buffer, 0, buffer.Length)

                        if count = 0 then
                            reading <- false
                        elif builder.Length + count > 1_048_576 then
                            Internal.fail "Subprozess-Ausgabe ueberschreitet das Limit."
                        else
                            builder.Append(buffer, 0, count) |> ignore

                    return builder.ToString()
                }

            let stdoutTask = readBounded child.StandardOutput
            let stderrTask = readBounded child.StandardError

            if not (child.WaitForExit(timeoutMilliseconds)) then
                child.Kill(true)
                Internal.fail "Subprozess-Zeitlimit ueberschritten."

            if not (Task.WaitAll([| stdoutTask :> Task; stderrTask :> Task |], 2_000)) then
                Internal.fail "Subprozess-Ausgabe konnte nicht gelesen werden."

            child.ExitCode, stdoutTask.Result, stderrTask.Result
        with
        | HarnessException _ -> reraise ()
        | _ -> Internal.fail "Subprozess konnte nicht sicher ausgefuehrt werden."

    let private processResult root executable arguments =
        processResultWithTimeout 10_000 root executable arguments

    let private processBytesResultWithTimeout
        (timeoutMilliseconds: int)
        (maxStdoutBytes: int)
        (root: string)
        (executable: string)
        (arguments: string list)
        =
        try
            let startInfo = ProcessStartInfo(executable)
            startInfo.WorkingDirectory <- root
            startInfo.UseShellExecute <- false
            startInfo.RedirectStandardOutput <- true
            startInfo.RedirectStandardError <- true

            for argument in arguments do
                startInfo.ArgumentList.Add(argument)

            use child = Process.Start(startInfo)

            if isNull child then
                Internal.fail "Subprozess konnte nicht gestartet werden."

            let readBoundedBytes (stream: Stream) limit =
                task {
                    use output = new MemoryStream()
                    let buffer = Array.zeroCreate<byte> 8192
                    let mutable reading = true

                    while reading do
                        let! count = stream.ReadAsync(buffer, 0, buffer.Length)

                        if count = 0 then
                            reading <- false
                        elif output.Length + int64 count > int64 limit then
                            Internal.fail "Subprozess-Ausgabe ueberschreitet das Limit."
                        else
                            output.Write(buffer, 0, count)

                    return output.ToArray()
                }

            let stdoutTask = readBoundedBytes child.StandardOutput.BaseStream maxStdoutBytes

            let stderrTask = readBoundedBytes child.StandardError.BaseStream 1_048_576

            if not (child.WaitForExit(timeoutMilliseconds)) then
                child.Kill(true)
                Internal.fail "Subprozess-Zeitlimit ueberschritten."

            if not (Task.WaitAll([| stdoutTask :> Task; stderrTask :> Task |], 2_000)) then
                Internal.fail "Subprozess-Ausgabe konnte nicht gelesen werden."

            child.ExitCode, stdoutTask.Result
        with
        | HarnessException _ -> reraise ()
        | _ -> Internal.fail "Subprozess konnte nicht sicher bytegenau ausgefuehrt werden."

    let private gitTracked root path =
        let exitCode, _, _ =
            processResult root "git" [ "ls-files"; "--error-unmatch"; "--"; path ]

        exitCode = 0

    let private gitIndexBlob root path =
        let exitCode, output =
            processBytesResultWithTimeout 10_000 (int Constants.MaxPayloadBytes) root "git" [ "show"; ":" + path ]

        if exitCode = 0 then Some output else None

    let private strictLfsPointer (bytes: byte array) =
        let value =
            try
                UTF8Encoding(false, true).GetString(bytes)
            with :? DecoderFallbackException ->
                ""

        let lines = value.Split('\n')

        if
            lines.Length = 4
            && lines[3] = ""
            && not (value.Contains('\r'))
            && lines[0] = "version https://git-lfs.github.com/spec/v1"
            && Regex.IsMatch(lines[1], "^oid sha256:[0-9a-f]{64}$", RegexOptions.CultureInvariant)
            && Regex.IsMatch(lines[2], "^size (0|[1-9][0-9]*)$", RegexOptions.CultureInvariant)
        then
            match
                Int64.TryParse(lines[2].Substring("size ".Length), NumberStyles.None, CultureInfo.InvariantCulture)
            with
            | true, size -> Some(lines[1].Substring("oid sha256:".Length), size)
            | _ -> None
        else
            None

    let private lfsAttributeEnabled root path =
        let exitCode, output, _ =
            processResult root "git" [ "check-attr"; "--cached"; "filter"; "--"; path ]

        exitCode = 0 && output.TrimEnd().EndsWith(": lfs", StringComparison.Ordinal)

    let private validateRepositoryBoundary locations findings =
        let exitCode, tracked, _ =
            processResult locations.Root "git" [ "ls-files"; "--"; "assets/quarantine"; "assets/cooked" ]

        if exitCode <> 0 then
            addError findings "ASSET_GIT_CHECK_FAILED" None "git" "Git-Trackingstatus konnte nicht geprueft werden."
        elif not (String.IsNullOrWhiteSpace(tracked)) then
            addError
                findings
                "ASSET_FORBIDDEN_TRACKED_PATH"
                None
                "assets"
                "Quarantaene- oder Cooked-Datei ist in Git erfasst."

        for lifecyclePath in [ "assets/quarantine/.asset-check-probe"; "assets/cooked/.asset-check-probe" ] do
            let ignoreExit, _, _ =
                processResult locations.Root "git" [ "check-ignore"; "--no-index"; "--quiet"; lifecyclePath ]

            if ignoreExit <> 0 then
                addError
                    findings
                    "ASSET_IGNORE_RULE_MISSING"
                    None
                    "gitignore"
                    "Asset-Lebenszykluspfad ist nicht fail-closed ignoriert."

        let headExit, _, _ =
            processResult locations.Root "git" [ "rev-parse"; "--verify"; "HEAD" ]

        if headExit = 0 then
            let lfsExit, _, _ =
                processResultWithTimeout 60_000 locations.Root "git" [ "lfs"; "fsck"; "--pointers" ]

            if lfsExit <> 0 then
                addError findings "ASSET_LFS_FSCK_FAILED" None "git-lfs" "Git-LFS-Pointerpruefung ist fehlgeschlagen."

        let configPath =
            safeManifestPath locations "Harness-Konfiguration" false ".ai/config.json"

        use config = JsonDocument.Parse(safeJsonBytes configPath "Harness-Konfiguration")
        let root = config.RootElement

        let valuesAt (section: string) (name: string) =
            root.GetProperty(section).GetProperty(name).EnumerateArray()
            |> Seq.map (fun item -> item.GetString())
            |> Set.ofSeq

        let excluded = valuesAt "rag" "excludedSegments"
        let neverIndex = valuesAt "security" "neverIndex"

        for path in [ "assets/quarantine"; "assets/cooked" ] do
            if not (excluded.Contains(path)) && not (neverIndex.Contains(path)) then
                addError
                    findings
                    "ASSET_RAG_EXCLUSION_MISSING"
                    None
                    ".ai/config.json"
                    "Asset-Lebenszykluspfad fehlt in der RAG-Sperrliste."

    let private validateModelLock locations (schema: JsonSchema) findings =
        let lockPath = safeManifestPath locations "Modell-Lock" false modelsLockRelative

        let modelSchemaErrors = schemaErrors schema lockPath

        for path in modelSchemaErrors do
            addError findings "MODEL_LOCK_SCHEMA_INVALID" None path "Modell-Lock verletzt das versionierte Schema."

        if not (List.isEmpty modelSchemaErrors) then
            []
        else
            use document = JsonDocument.Parse(safeJsonBytes lockPath "Modell-Lock")
            let root = document.RootElement
            let models = root.GetProperty("models").EnumerateArray() |> Seq.toList
            let tuples = HashSet<string>(StringComparer.Ordinal)
            let ids = HashSet<string>(StringComparer.Ordinal)
            let mutable approvedCount = 0

            for model in models do
                let id = getString "id" model

                let artifact =
                    getOptionalString "modelArtifactSha256" model |> Option.defaultValue ""

                let tuple =
                    String.Join(
                        "\u001f",
                        [ getString "model" model
                          getString "modelVersion" model
                          getString "executionMode" model
                          artifact ]
                    )

                if not (ids.Add(id)) then
                    addError findings "MODEL_LOCK_DUPLICATE_ID" None "/models" "Modell-Lock enthaelt eine doppelte ID."

                if not (tuples.Add(tuple)) then
                    addError
                        findings
                        "MODEL_LOCK_DUPLICATE_TUPLE"
                        None
                        "/models"
                        "Modell-Lock enthaelt ein doppeltes Modell-Tupel."

                if getString "status" model = "approved" then
                    approvedCount <- approvedCount + 1

                    if
                        not (model.GetProperty("commercialUseReviewed").GetBoolean())
                        || getOptionalString "reviewedAtUtc" model |> Option.isNone
                    then
                        addError
                            findings
                            "MODEL_LOCK_APPROVAL_INCOMPLETE"
                            None
                            "/models"
                            "Freigegebenes Modell besitzt keine vollstaendige Nutzungspruefung."

                    if getString "executionMode" model = "local" && String.IsNullOrEmpty(artifact) then
                        addError
                            findings
                            "MODEL_LOCK_LOCAL_ARTIFACT_MISSING"
                            None
                            "/models"
                            "Lokales freigegebenes Modell besitzt keinen Artefakthash."

            let status = getString "status" root

            if (status = "no-production-model-approved") <> (approvedCount = 0) then
                addError
                    findings
                    "MODEL_LOCK_STATUS_MISMATCH"
                    None
                    "/status"
                    "Modell-Lock-Status widerspricht den freigegebenen Eintraegen."

            models

    let private validateReviewHistory manifestRelative findings (manifestRoot: JsonElement) =
        let reviews = manifestRoot.GetProperty("reviews").EnumerateArray() |> Seq.toList
        let byId = Dictionary<string, JsonElement>(StringComparer.Ordinal)

        for review in reviews do
            let id = getString "reviewId" review

            if byId.ContainsKey(id) then
                addError
                    findings
                    "ASSET_REVIEW_ID_DUPLICATE"
                    (Some manifestRelative)
                    "/reviews"
                    "Review-ID ist nicht eindeutig."
            else
                byId.Add(id, review)

        for kind in approvedReviewKinds do
            let revisions =
                reviews |> List.filter (fun review -> getString "kind" review = kind)

            let active =
                revisions
                |> List.filter (fun review -> review.GetProperty("active").GetBoolean())

            if active.Length > 1 then
                addError
                    findings
                    "ASSET_REVIEW_ACTIVE_DUPLICATE"
                    (Some manifestRelative)
                    "/reviews"
                    "Reviewart besitzt mehr als eine aktive Revision."

            let numbers = HashSet<int>()

            for review in revisions do
                let revision = review.GetProperty("revision").GetInt32()

                if not (numbers.Add(revision)) then
                    addError
                        findings
                        "ASSET_REVIEW_REVISION_DUPLICATE"
                        (Some manifestRelative)
                        "/reviews"
                        "Reviewrevision ist nicht eindeutig."

                let supersedes = getOptionalString "supersedesReviewId" review

                if revision = 1 && supersedes.IsSome then
                    addError
                        findings
                        "ASSET_REVIEW_CHAIN_INVALID"
                        (Some manifestRelative)
                        "/reviews"
                        "Erste Reviewrevision darf keinen Vorgaenger besitzen."
                elif revision > 1 then
                    match supersedes with
                    | None ->
                        addError
                            findings
                            "ASSET_REVIEW_CHAIN_INVALID"
                            (Some manifestRelative)
                            "/reviews"
                            "Spaetere Reviewrevision muss ihren Vorgaenger binden."
                    | Some id ->
                        match byId.TryGetValue(id) with
                        | true, previous when
                            getString "kind" previous = kind
                            && previous.GetProperty("revision").GetInt32() = revision - 1
                            ->
                            ()
                        | _ ->
                            addError
                                findings
                                "ASSET_REVIEW_CHAIN_INVALID"
                                (Some manifestRelative)
                                "/reviews"
                                "Review-Vorgaenger ist ungueltig."

            if
                numbers.Count > 0
                && ([ 1 .. numbers.Count ]
                    |> List.exists (fun expectedRevision -> not (numbers.Contains(expectedRevision))))
            then
                addError
                    findings
                    "ASSET_REVIEW_CHAIN_INVALID"
                    (Some manifestRelative)
                    "/reviews"
                    "Reviewrevisionen muessen eine lueckenlose Kette ab Revision 1 bilden."

            match active, revisions with
            | [ current ], _ when
                current.GetProperty("revision").GetInt32()
                <> (revisions
                    |> List.map (fun review -> review.GetProperty("revision").GetInt32())
                    |> List.max)
                ->
                addError
                    findings
                    "ASSET_REVIEW_ACTIVE_NOT_LATEST"
                    (Some manifestRelative)
                    "/reviews"
                    "Aktive Reviewrevision ist nicht die neueste."
            | _ -> ()

    let private reviewEvidenceCoreBytes (root: JsonElement) =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()

            for name in
                [ "schemaVersion"
                  "assetId"
                  "specSha256"
                  "generationReceiptSha256"
                  "licenseTermsSha256"
                  "reviewId"
                  "kind"
                  "revision"
                  "result"
                  "reviewerId"
                  "runId"
                  "reviewedAtUtc"
                  "criteriaVersion"
                  "checkedScopes"
                  "limitations" ] do
                writer.WritePropertyName(name)
                writer.WriteRawValue(Internal.canonicalElementText (root.GetProperty(name)), true)

            writer.WriteEndObject())

    let private validateReviewEvidence
        locations
        (schema: JsonSchema)
        policy
        manifestRelative
        findings
        (manifestRoot: JsonElement)
        (review: JsonElement)
        =
        let reference = review.GetProperty("evidenceArtifact")

        if reference.ValueKind = JsonValueKind.Null then
            addError
                findings
                "ASSET_REVIEW_EVIDENCE_MISSING"
                (Some manifestRelative)
                "/reviews"
                "Freigabereview benoetigt strukturierte Evidenz."
        else
            try
                let path = getString "path" reference
                let absolute = safeManifestPath locations "Review-Evidenz" false path
                let evidenceBytes = safeFileBytes absolute "Review-Evidenz"

                if Internal.sha256Hex evidenceBytes <> getString "sha256" reference then
                    Internal.fail "Review-Evidenzhash stimmt nicht."

                if not (List.isEmpty (schemaErrors schema absolute)) then
                    Internal.fail "Review-Evidenz verletzt das Schema."

                ensureNoDuplicateKeys "Review-Evidenz" evidenceBytes
                use document = JsonDocument.Parse(evidenceBytes)
                let evidence = document.RootElement
                validateCleanRoomElement policy manifestRelative "review-evidence-content" findings evidence

                if
                    Internal.sha256Hex (reviewEvidenceCoreBytes evidence)
                    <> getString "reportSha256" evidence
                then
                    Internal.fail "Review-Berichtshash stimmt nicht."

                let assertString field expected =
                    if not (String.Equals(getString field evidence, expected, StringComparison.Ordinal)) then
                        Internal.fail "Review-Evidenzbindung widerspricht Manifest oder Review."

                assertString "assetId" (getString "assetId" manifestRoot)
                assertString "specSha256" (getString "specSha256" manifestRoot)
                assertString "generationReceiptSha256" (getString "generationReceiptSha256" manifestRoot)
                assertString "reviewId" (getString "reviewId" review)
                assertString "kind" (getString "kind" review)
                assertString "result" (getString "result" review)
                assertString "runId" (getString "runId" reference)
                assertString "reviewedAtUtc" (getString "atUtc" review)

                let licenseTermsReference =
                    manifestRoot.GetProperty("licenseBasis").GetProperty("termsEvidenceArtifact")

                let expectedLicenseTermsHash =
                    if getString "kind" review = "license" then
                        if licenseTermsReference.ValueKind = JsonValueKind.Null then
                            Internal.fail "Lizenzreview besitzt keinen gebundenen Bedingungssnapshot."

                        Some(getString "sha256" licenseTermsReference)
                    else
                        None

                if getOptionalString "licenseTermsSha256" evidence <> expectedLicenseTermsHash then
                    Internal.fail "Review-Evidenz bindet den Lizenzbedingungssnapshot nicht korrekt."

                if getString "kind" review = "license" then
                    let scopes =
                        evidence.GetProperty("checkedScopes").EnumerateArray()
                        |> Seq.map (fun scope -> scope.GetString())
                        |> Set.ofSeq

                    if not (scopes.Contains("license-terms-snapshot-v1")) then
                        Internal.fail "Lizenzreview deckt den Bedingungssnapshot nicht ab."

                if
                    not (
                        String.Equals(
                            getString "reviewerId" evidence,
                            getString "reviewerId" review,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                then
                    Internal.fail "Review-Evidenzbindung widerspricht der Revieweridentitaet."

                if
                    evidence.GetProperty("revision").GetInt32()
                    <> review.GetProperty("revision").GetInt32()
                    || not (
                        String.Equals(
                            getString "reviewerId" reference,
                            getString "reviewerId" review,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                then
                    Internal.fail "Review-Evidenzreferenz widerspricht dem Review."

                let snapshot =
                    RunStore.completedSnapshot locations.Root (getString "runId" evidence)

                if
                    not (
                        snapshot.ActorId
                        |> Option.exists (fun actor ->
                            String.Equals(actor, getString "reviewerId" review, StringComparison.OrdinalIgnoreCase))
                    )
                then
                    Internal.fail "Verifizierter Review-Run bindet nicht die Revieweridentitaet."

                let reviewEvents =
                    snapshot.Events
                    |> List.filter (fun event -> event.EventType = "asset.review.completed")

                let event =
                    match reviewEvents with
                    | [ value ] -> value
                    | _ -> Internal.fail "Review-Run benoetigt genau ein Abschlussereignis."

                use payloadDocument = JsonDocument.Parse(event.Payload)
                let payload = payloadDocument.RootElement

                if
                    not (
                        String.Equals(
                            getString "actorId" payload,
                            getString "reviewerId" review,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                then
                    Internal.fail "Review-Ereignis widerspricht der Revieweridentitaet."

                for field, expected in
                    [ "assetId", getString "assetId" manifestRoot
                      "specSha256", getString "specSha256" manifestRoot
                      "generationReceiptSha256", getString "generationReceiptSha256" manifestRoot
                      "reviewId", getString "reviewId" review
                      "kind", getString "kind" review
                      "result", getString "result" review
                      "reviewedAtUtc", getString "atUtc" review
                      "evidencePath", path
                      "evidenceSha256", getString "sha256" reference ] do
                    if getString field payload <> expected then
                        Internal.fail "Review-Ereignis widerspricht Evidenz oder Manifest."

                if
                    getOptionalString "licenseTermsSha256" payload
                    <> getOptionalString "licenseTermsSha256" evidence
                then
                    Internal.fail "Review-Ereignis widerspricht dem Lizenzbedingungssnapshot."

                if
                    payload.GetProperty("revision").GetInt32()
                    <> review.GetProperty("revision").GetInt32()
                then
                    Internal.fail "Review-Ereignis widerspricht der Revision."

                let generationReceiptPath =
                    safeManifestPath
                        locations
                        "GenerationReceipt fuer Reviewzeit"
                        false
                        (getString "generationReceipt" manifestRoot)

                use generationReceipt =
                    JsonDocument.Parse(safeJsonBytes generationReceiptPath "GenerationReceipt fuer Reviewzeit")

                let generationFinishedAt =
                    Internal.tryParseUtc (getString "finishedAtUtc" generationReceipt.RootElement)
                    |> Option.defaultWith (fun () -> Internal.fail "GenerationReceipt-Abschlusszeit ist ungueltig.")

                match
                    Internal.tryParseUtc (getString "atUtc" review),
                    Internal.tryParseUtc snapshot.StartedAtUtc,
                    Internal.tryParseUtc snapshot.FinishedAtUtc,
                    Internal.tryParseUtc event.TimestampUtc
                with
                | Some reviewedAt, Some startedAt, Some finishedAt, Some eventAt when
                    reviewedAt >= generationFinishedAt
                    && reviewedAt >= startedAt
                    && reviewedAt <= eventAt
                    && eventAt <= finishedAt
                    ->
                    ()
                | _ -> Internal.fail "Review-Zeitstempel ist nicht an den verifizierten Run gebunden."
            with
            | HarnessException _
            | :? JsonException
            | :? KeyNotFoundException
            | :? InvalidOperationException
            | :? FormatException
            | :? OverflowException ->
                addError
                    findings
                    "ASSET_REVIEW_EVIDENCE_INVALID"
                    (Some manifestRelative)
                    "/reviews"
                    "Review-Evidenz oder verifizierter Review-Run ist ungueltig."

    let private validateLicenseEvidence locations policy manifestRelative findings (manifestRoot: JsonElement) =
        if getString "status" manifestRoot = "approved" then
            let reference =
                manifestRoot.GetProperty("licenseBasis").GetProperty("termsEvidenceArtifact")

            if reference.ValueKind = JsonValueKind.Null then
                addError
                    findings
                    "ASSET_LICENSE_TERMS_EVIDENCE_MISSING"
                    (Some manifestRelative)
                    "/licenseBasis/termsEvidenceArtifact"
                    "Freigabe benoetigt einen gehashten Snapshot der relevanten Nutzungsbedingungen."
            else
                try
                    let path = getString "path" reference
                    let absolute = safeManifestPath locations "Lizenzbedingungssnapshot" false path
                    let bytes = safeFileBytes absolute "Lizenzbedingungssnapshot"

                    if Internal.sha256Hex bytes <> getString "sha256" reference then
                        Internal.fail "Lizenzbedingungssnapshot-Hash stimmt nicht."

                    if textSourceExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()) then
                        let text = strictUtf8Text "Lizenzbedingungssnapshot" bytes

                        validateCleanRoomText policy manifestRelative "license-terms-content" findings text
                with
                | HarnessException _
                | :? DecoderFallbackException
                | :? IOException
                | :? UnauthorizedAccessException ->
                    addError
                        findings
                        "ASSET_LICENSE_TERMS_EVIDENCE_INVALID"
                        (Some manifestRelative)
                        "/licenseBasis/termsEvidenceArtifact"
                        "Lizenzbedingungssnapshot fehlt, ist unsicher oder hashabweichend."

    let private validateApprovedReviews
        locations
        (reviewEvidenceSchema: JsonSchema)
        policy
        manifestRelative
        findings
        (manifestRoot: JsonElement)
        =
        if getString "status" manifestRoot = "approved" then
            if not (approvedGeneratorKinds.Contains(getString "kind" (manifestRoot.GetProperty("generator")))) then
                addError
                    findings
                    "ASSET_APPROVED_ORIGIN_INVALID"
                    (Some manifestRelative)
                    "/generator/kind"
                    "Freigegebene kreative Assets muessen synthetisch erzeugt sein."

            let creator = getString "createdBy" manifestRoot

            let active =
                manifestRoot.GetProperty("reviews").EnumerateArray()
                |> Seq.filter (fun review -> review.GetProperty("active").GetBoolean())
                |> Seq.toList

            for kind in approvedReviewKinds do
                let matches =
                    active
                    |> List.filter (fun review -> getString "kind" review = kind && getString "result" review = "pass")

                if matches.Length <> 1 then
                    addError
                        findings
                        "ASSET_APPROVAL_REVIEW_MISSING"
                        (Some manifestRelative)
                        "/reviews"
                        "Freigabe benoetigt genau eine aktive bestandene Revision jeder Reviewart."

                for review in matches do
                    if
                        (kind = "originality" || kind = "license")
                        && String.Equals(getString "reviewerId" review, creator, StringComparison.OrdinalIgnoreCase)
                    then
                        addError
                            findings
                            "ASSET_REVIEW_SELF_APPROVAL"
                            (Some manifestRelative)
                            "/reviews"
                            "Erzeuger darf finale Originalitaets- oder Lizenzpruefung nicht selbst freigeben."

                    validateReviewEvidence
                        locations
                        reviewEvidenceSchema
                        policy
                        manifestRelative
                        findings
                        manifestRoot
                        review

                    if kind = "license" then
                        let declaredReviewAt =
                            getOptionalString "reviewedAtUtc" (manifestRoot.GetProperty("licenseBasis"))

                        if declaredReviewAt <> Some(getString "atUtc" review) then
                            addError
                                findings
                                "ASSET_LICENSE_REVIEW_TIME_MISMATCH"
                                (Some manifestRelative)
                                "/licenseBasis/reviewedAtUtc"
                                "Kommerzielle Nutzungspruefung ist nicht an das aktive Lizenzreview gebunden."

    let private validateApprovedTextOutput locations manifestRelative findings index (output: JsonElement) =
        let path = getString "path" output
        let extension = Path.GetExtension(path).ToLowerInvariant()
        let mediaType = getString "mediaType" output
        let allowedMediaTypes = textMediaTypes extension

        try
            if Set.isEmpty allowedMediaTypes || not (allowedMediaTypes.Contains(mediaType)) then
                Internal.fail "Textquellen-Endung und Medientyp widersprechen sich."

            let absolute = safeManifestPath locations "Textueller Assetoutput" false path
            readValidatedText absolute "Textueller Assetoutput" extension |> ignore
        with HarnessException _ ->
            addError
                findings
                "ASSET_TEXT_SOURCE_INVALID"
                (Some manifestRelative)
                ($"/outputs/{index}")
                "Als Textquelle deklarierter Assetoutput ist kein passendes begrenztes UTF-8-Format."

    let private validateInputsAndOutputs locations requireLocal manifestRelative findings (manifestRoot: JsonElement) =
        let specHash = getString "specSha256" manifestRoot
        let mutable specMatches = 0
        let generator = manifestRoot.GetProperty("generator")
        let proceduralSources = ResizeArray<string * string>()
        let inputIds = HashSet<string>(StringComparer.Ordinal)

        for index, input in manifestRoot.GetProperty("inputs").EnumerateArray() |> Seq.indexed do
            if not (inputIds.Add(getString "id" input)) then
                addError
                    findings
                    "ASSET_INPUT_ID_DUPLICATE"
                    (Some manifestRelative)
                    ($"/inputs/{index}/id")
                    "Assetinput-ID ist nicht eindeutig."

            let originClass = getString "originClass" input
            let creativeInfluence = input.GetProperty("creativeInfluence").GetBoolean()

            if
                getString "status" manifestRoot = "approved"
                && creativeInfluence
                && originClass <> "internal-specification"
                && originClass <> "agentic-synthetic"
            then
                addError
                    findings
                    "ASSET_EXTERNAL_CREATIVE_INPUT"
                    (Some manifestRelative)
                    ($"/inputs/{index}/originClass")
                    "Freigabe erlaubt keinen externen kreativen Generatorinput."

            if getString "allowedUse" input = "technical-calibration" && creativeInfluence then
                addError
                    findings
                    "ASSET_INPUT_ROLE_MISMATCH"
                    (Some manifestRelative)
                    ($"/inputs/{index}/creativeInfluence")
                    "Technische Kalibrierung darf keine kreative Gestaltung vorgeben."

            match getOptionalString "path" input with
            | None -> ()
            | Some path ->
                try
                    let absolute = safeManifestPath locations "Assetinput" true path

                    if File.Exists(absolute) then
                        let actualHash, _ = safeFileHashAndLength absolute "Assetinput"

                        if actualHash <> getString "sha256" input then
                            addError
                                findings
                                "ASSET_INPUT_HASH_MISMATCH"
                                (Some manifestRelative)
                                ($"/inputs/{index}/sha256")
                                "Assetinput-Hash stimmt nicht."
                    elif requireLocal then
                        addError
                            findings
                            "ASSET_INPUT_MISSING"
                            (Some manifestRelative)
                            ($"/inputs/{index}/path")
                            "Lokal erforderlicher Assetinput fehlt."
                with
                | HarnessException _
                | :? IOException
                | :? UnauthorizedAccessException ->
                    addError
                        findings
                        "ASSET_INPUT_UNSAFE"
                        (Some manifestRelative)
                        ($"/inputs/{index}/path")
                        "Assetinput ist nicht lokal, fehlt oder ist unsicher."

            if
                getString "allowedUse" input = "internal-specification"
                && getString "sha256" input = specHash
            then
                specMatches <- specMatches + 1

            if getString "kind" generator = "procedural" then
                match getOptionalString "path" input with
                | Some path when
                    getString "allowedUse" input = "generation-input"
                    && (originClass = "internal-specification" || originClass = "agentic-synthetic")
                    ->
                    proceduralSources.Add(path, getString "sha256" input)
                | _ -> ()

        if specMatches <> 1 then
            addError
                findings
                "ASSET_SPEC_BINDING_INVALID"
                (Some manifestRelative)
                "/specSha256"
                "Spezifikationshash muss genau einen internen Input binden."

        if getString "kind" generator = "procedural" then
            let sources =
                proceduralSources
                |> Seq.sortWith (fun (left, _) (right, _) -> StringComparer.Ordinal.Compare(left, right))
                |> Seq.toArray

            let aggregate =
                use binding = new MemoryStream()

                for path, hash in sources do
                    let bytes = Constants.Utf8NoBom.GetBytes(path + "\n" + hash + "\n")
                    binding.Write(bytes)

                binding.ToArray() |> Internal.sha256Hex

            if
                sources.Length = 0
                || (getOptionalString "generatorSourceSha256" generator <> Some aggregate
                    && not (
                        sources.Length = 1
                        && getOptionalString "generatorSourceSha256" generator = Some(snd sources[0])
                    ))
            then
                addError
                    findings
                    "ASSET_GENERATOR_SOURCE_BINDING_INVALID"
                    (Some manifestRelative)
                    "/generator/generatorSourceSha256"
                    "Prozedurale Generatorquellen muessen ihren geordneten lokalen Aggregathash binden."

        let status = getString "status" manifestRoot

        for index, output in manifestRoot.GetProperty("outputs").EnumerateArray() |> Seq.indexed do
            let path = getString "path" output
            let pathField = $"/outputs/{index}/path"

            if pathStartsWith "assets/cooked/" path then
                addError
                    findings
                    "ASSET_COOKED_OUTPUT_FORBIDDEN"
                    (Some manifestRelative)
                    pathField
                    "Cooked-Datei darf kein Provenienz-Quelloutput sein."

            if status = "approved" && not (pathStartsWith "assets/source/" path) then
                addError
                    findings
                    "ASSET_APPROVED_PATH_INVALID"
                    (Some manifestRelative)
                    pathField
                    "Freigegebener Output muss unter assets/source liegen."
            elif status <> "approved" && not (pathStartsWith "assets/quarantine/" path) then
                addError
                    findings
                    "ASSET_QUARANTINE_PATH_INVALID"
                    (Some manifestRelative)
                    pathField
                    "Nicht freigegebener Output muss in Quarantaene bleiben."

            try
                let absolute =
                    safeManifestPath locations "Assetoutput" (not requireLocal && status <> "approved") path

                if File.Exists(absolute) then
                    let actualHash, actualLength = safeFileHashAndLength absolute "Assetoutput"

                    if actualHash <> getString "sha256" output then
                        addError
                            findings
                            "ASSET_OUTPUT_HASH_MISMATCH"
                            (Some manifestRelative)
                            ($"/outputs/{index}/sha256")
                            "Assetoutput-Hash stimmt nicht."

                    if actualLength <> output.GetProperty("bytes").GetInt64() then
                        addError
                            findings
                            "ASSET_OUTPUT_SIZE_MISMATCH"
                            (Some manifestRelative)
                            ($"/outputs/{index}/bytes")
                            "Assetoutput-Groesse stimmt nicht."
                elif requireLocal || status = "approved" then
                    addError
                        findings
                        "ASSET_OUTPUT_MISSING"
                        (Some manifestRelative)
                        pathField
                        "Lokal erforderlicher Assetoutput fehlt."
            with
            | HarnessException _
            | :? IOException
            | :? UnauthorizedAccessException ->
                addError
                    findings
                    "ASSET_OUTPUT_UNSAFE"
                    (Some manifestRelative)
                    pathField
                    "Assetoutputpfad ist ungueltig oder unsicher."

            if status = "approved" then
                if not (gitTracked locations.Root path) then
                    addError
                        findings
                        "ASSET_APPROVED_NOT_TRACKED"
                        (Some manifestRelative)
                        pathField
                        "Freigegebener Output ist nicht in Git erfasst."
                elif textSourceExtensions.Contains(Path.GetExtension(path).ToLowerInvariant()) then
                    validateApprovedTextOutput locations manifestRelative findings index output

                    match gitIndexBlob locations.Root path with
                    | Some blob when
                        Internal.sha256Hex blob = getString "sha256" output
                        && int64 blob.LongLength = output.GetProperty("bytes").GetInt64()
                        ->
                        ()
                    | _ ->
                        addError
                            findings
                            "ASSET_TEXT_SOURCE_INDEX_MISMATCH"
                            (Some manifestRelative)
                            pathField
                            "Freigegebene Textquelle stimmt nicht bytegenau mit dem Git-Index ueberein."
                else
                    match gitIndexBlob locations.Root path with
                    | Some blob ->
                        match strictLfsPointer blob with
                        | Some(oid, size) when
                            oid = getString "sha256" output
                            && size = output.GetProperty("bytes").GetInt64()
                            && lfsAttributeEnabled locations.Root path
                            ->
                            ()
                        | _ ->
                            addError
                                findings
                                "ASSET_LFS_POINTER_INVALID"
                                (Some manifestRelative)
                                pathField
                                "Freigegebene Binaerquelle besitzt keinen exakt gebundenen LFS-Pointer."
                    | _ ->
                        addError
                            findings
                            "ASSET_LFS_POINTER_INVALID"
                            (Some manifestRelative)
                            pathField
                            "Freigegebene Binaerquelle besitzt keinen exakt gebundenen LFS-Pointer."

    let private sameCanonical left right =
        Internal.canonicalElement left = Internal.canonicalElement right

    let private promptEnvelopeHash (prompts: JsonElement) =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", 1)
            writer.WriteString("prompt", getString "prompt" prompts)

            match getOptionalString "negativePrompt" prompts with
            | Some value -> writer.WriteString("negativePrompt", value)
            | None -> writer.WriteNull("negativePrompt")

            writer.WriteEndObject())
        |> Internal.sha256Hex

    let private validatePromptEnvelope manifestRelative findings (manifestRoot: JsonElement) =
        let prompts = manifestRoot.GetProperty("prompts")

        if prompts.ValueKind <> JsonValueKind.Null then
            if
                Internal.sha256Text (getString "prompt" prompts)
                <> getString "promptSha256" prompts
            then
                addError
                    findings
                    "ASSET_PROMPT_HASH_MISMATCH"
                    (Some manifestRelative)
                    "/prompts/promptSha256"
                    "Prompt-Hash stimmt nicht."

            if promptEnvelopeHash prompts <> getString "promptEnvelopeSha256" prompts then
                addError
                    findings
                    "ASSET_PROMPT_ENVELOPE_HASH_MISMATCH"
                    (Some manifestRelative)
                    "/prompts/promptEnvelopeSha256"
                    "Prompt-Envelope-Hash stimmt nicht."

            if
                getString "status" manifestRoot = "approved"
                && getString "bindingMode" prompts <> "canonical-envelope-v1"
            then
                addError
                    findings
                    "ASSET_PROMPT_LEGACY_NOT_APPROVABLE"
                    (Some manifestRelative)
                    "/prompts/bindingMode"
                    "Legacy-Promptbindung ist nicht freigabefaehig."

    let private receiptCoreBytes (receiptRoot: JsonElement) =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()

            receiptRoot.EnumerateObject()
            |> Seq.filter (fun property -> property.Name <> "$schema" && property.Name <> "receiptSha256")
            |> Seq.sortWith (fun left right -> StringComparer.Ordinal.Compare(left.Name, right.Name))
            |> Seq.iter (fun property ->
                writer.WritePropertyName(property.Name)
                writer.WriteRawValue(Internal.canonicalElementText property.Value, true))

            writer.WriteEndObject())

    let private eventCoreBytes (runId: string) (eventRoot: JsonElement) =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", Constants.SchemaVersion)
            writer.WriteString("runId", runId)
            writer.WriteNumber("sequence", eventRoot.GetProperty("sequence").GetInt64())
            writer.WriteString("timestampUtc", getString "timestampUtc" eventRoot)
            writer.WriteString("type", getString "type" eventRoot)

            match eventRoot.GetProperty("previousEventHash").ValueKind with
            | JsonValueKind.Null -> writer.WriteNull("previousEventHash")
            | _ -> writer.WriteString("previousEventHash", getString "previousEventHash" eventRoot)

            Internal.rawJson writer "payload" (Internal.canonicalElement (eventRoot.GetProperty("payload")))
            writer.WriteEndObject())

    let private summaryCoreBytes (receiptRoot: JsonElement) =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", Constants.SchemaVersion)
            writer.WriteString("runId", getString "runId" receiptRoot)

            if getString "generationBindingMode" receiptRoot = "canonical-event-v1" then
                writer.WriteString("actorId", getString "actorId" receiptRoot)

            writer.WriteString("startedAtUtc", getString "startedAtUtc" receiptRoot)
            writer.WriteString("finishedAtUtc", getString "finishedAtUtc" receiptRoot)
            writer.WriteString("status", getString "status" receiptRoot)
            writer.WriteNumber("eventCount", receiptRoot.GetProperty("eventCount").GetInt64())
            writer.WriteString("finalEventHash", getString "finalEventHash" receiptRoot)

            match
                receiptRoot.TryGetProperty("retrievalTraceCount"),
                receiptRoot.TryGetProperty("finalRetrievalTraceHash")
            with
            | (true, count), (true, finalHash) ->
                writer.WriteNumber("retrievalTraceCount", count.GetInt64())

                match finalHash.ValueKind with
                | JsonValueKind.Null -> writer.WriteNull("finalRetrievalTraceHash")
                | _ -> writer.WriteString("finalRetrievalTraceHash", finalHash.GetString())
            | _ -> ()

            Internal.rawJson writer "summary" (Internal.canonicalElement (receiptRoot.GetProperty("summary")))
            writer.WriteEndObject())

    let private writeStoredEvent (writer: Utf8JsonWriter) (event: StoredEvent) =
        writer.WriteStartObject()
        writer.WriteNumber("sequence", event.Sequence)
        writer.WriteString("timestampUtc", event.TimestampUtc)
        writer.WriteString("type", event.EventType)

        match event.PreviousEventHash with
        | Some hash -> writer.WriteString("previousEventHash", hash)
        | None -> writer.WriteNull("previousEventHash")

        Internal.rawJson writer "payload" event.Payload
        writer.WriteString("eventHash", event.EventHash)
        writer.WriteEndObject()

    let private findGenerationEvent assetId (snapshot: CompletedRunSnapshot) =
        let candidates =
            snapshot.Events
            |> List.filter (fun event -> event.EventType = "asset.generation.completed")

        match candidates with
        | [ event ] ->
            use payload = JsonDocument.Parse(event.Payload)

            if getString "assetId" payload.RootElement <> assetId then
                Internal.fail "Generierungslauf enthaelt ein Abschlussereignis fuer ein anderes Asset."

            event
        | [] -> Internal.fail "Generierungslauf enthaelt kein Generierungs-Abschlussereignis."
        | _ -> Internal.fail "Generierungslauf enthaelt mehrere Generierungs-Abschlussereignisse."

    let private validateGenerationPayloadCore (manifestRoot: JsonElement) (event: StoredEvent) =
        use payloadDocument = JsonDocument.Parse(event.Payload)
        let payload = payloadDocument.RootElement
        let generator = manifestRoot.GetProperty("generator")
        let outputs = manifestRoot.GetProperty("outputs").EnumerateArray() |> Seq.toList

        let requireEqual name expected actual =
            if not (String.Equals(expected, actual, StringComparison.Ordinal)) then
                Internal.fail $"Generierungsereignis widerspricht dem Manifestfeld '{name}'."

        if
            not (
                String.Equals(
                    getString "createdBy" manifestRoot,
                    getString "actorId" payload,
                    StringComparison.OrdinalIgnoreCase
                )
            )
        then
            Internal.fail "Generierungsereignis widerspricht der Erzeugeridentitaet."

        requireEqual "assetId" (getString "assetId" manifestRoot) (getString "assetId" payload)
        requireEqual "specSha256" (getString "specSha256" manifestRoot) (getString "specSha256" payload)

        let kind = getString "kind" generator

        let canonicalEvent =
            getString "generationBindingMode" manifestRoot = "canonical-event-v1"

        if kind = "ai" then
            let prompts = manifestRoot.GetProperty("prompts")
            requireEqual "promptSha256" (getString "promptSha256" prompts) (getString "promptSha256" payload)

            if canonicalEvent then
                requireEqual "promptBindingMode" "canonical-envelope-v1" (getString "bindingMode" prompts)

                requireEqual
                    "promptBindingMode"
                    (getString "bindingMode" prompts)
                    (getString "promptBindingMode" payload)

                requireEqual
                    "promptEnvelopeSha256"
                    (getString "promptEnvelopeSha256" prompts)
                    (getString "promptEnvelopeSha256" payload)

        if not canonicalEvent then
            if outputs.Length <> 1 then
                Internal.fail "Legacy-Generierungsereignis bindet genau einen Output."

            let actualOutput = outputs.Head
            requireEqual "outputPath" (getString "path" actualOutput) (getString "outputPath" payload)
            requireEqual "outputSha256" (getString "sha256" actualOutput) (getString "outputSha256" payload)

            if
                actualOutput.GetProperty("bytes").GetInt64()
                <> payload.GetProperty("outputBytes").GetInt64()
            then
                Internal.fail "Generierungsereignis widerspricht der Outputgroesse."

        let eventGenerator = payload.GetProperty("generator")
        requireEqual "generator.tool" (getString "tool" generator) (getString "tool" eventGenerator)

        requireEqual
            "generator.executionMode"
            (getString "executionMode" generator)
            (getString "executionMode" eventGenerator)

        if canonicalEvent then
            requireEqual
                "generationBindingMode"
                (getString "generationBindingMode" manifestRoot)
                (getString "generationBindingMode" payload)

            requireEqual "generator.kind" kind (getString "kind" eventGenerator)
            requireEqual "generator.version" (getString "version" generator) (getString "version" eventGenerator)

            for name in
                [ "model"
                  "modelVersion"
                  "modelArtifactSha256"
                  "seed"
                  "generatorSourceSha256"
                  "toolchainPin" ] do
                if not (sameCanonical (generator.GetProperty(name)) (eventGenerator.GetProperty(name))) then
                    Internal.fail $"Generierungsereignis widerspricht dem Manifestfeld 'generator.{name}'."

            for name in [ "inputs"; "transformations" ] do
                if not (sameCanonical (manifestRoot.GetProperty(name)) (payload.GetProperty(name))) then
                    Internal.fail $"Generierungsereignis widerspricht dem Manifestfeld '{name}'."

            if not (sameCanonical (manifestRoot.GetProperty("outputs")) (payload.GetProperty("outputs"))) then
                Internal.fail "Generierungsereignis widerspricht dem vollstaendigen Outputdeskriptor."
        elif getString "status" manifestRoot = "approved" then
            Internal.fail "Legacy-Generierungsereignis ist nicht freigabefaehig."

        getString "specPath" payload

    let private validateGenerationPayload (manifestRoot: JsonElement) (event: StoredEvent) =
        try
            validateGenerationPayloadCore manifestRoot event
        with
        | HarnessException _ -> reraise ()
        | :? JsonException
        | :? KeyNotFoundException
        | :? InvalidOperationException
        | :? FormatException
        | :? OverflowException -> Internal.fail "Generierungsereignis verletzt den versionierten Payloadvertrag."

    let private receiptEvent runId (eventRoot: JsonElement) =
        { SchemaVersion = Constants.SchemaVersion
          RunId = runId
          Sequence = eventRoot.GetProperty("sequence").GetInt64()
          TimestampUtc = getString "timestampUtc" eventRoot
          EventType = getString "type" eventRoot
          PreviousEventHash =
            match eventRoot.GetProperty("previousEventHash").ValueKind with
            | JsonValueKind.Null -> None
            | _ -> Some(getString "previousEventHash" eventRoot)
          Payload = Internal.canonicalElement (eventRoot.GetProperty("payload"))
          EventHash = getString "eventHash" eventRoot }

    let private validatePortableReceiptCore (manifestRoot: JsonElement) (receipt: JsonElement) =
        let runId = getString "runId" receipt

        let startedAt, finishedAt =
            match
                Internal.tryParseUtc (getString "startedAtUtc" receipt),
                Internal.tryParseUtc (getString "finishedAtUtc" receipt)
            with
            | Some started, Some finished when finished >= started -> started, finished
            | _ -> Internal.fail "GenerationReceipt besitzt keinen konsistenten UTC-Zeitraum."

        let storedEvents =
            receipt.GetProperty("events").EnumerateArray()
            |> Seq.map (receiptEvent runId)
            |> Seq.toList

        if int64 storedEvents.Length <> receipt.GetProperty("eventCount").GetInt64() then
            Internal.fail "GenerationReceipt-Eventanzahl ist inkonsistent."

        let mutable previousHash: string option = None
        let mutable previousTimestamp = startedAt

        for index, eventRoot in receipt.GetProperty("events").EnumerateArray() |> Seq.indexed do
            let event = storedEvents[index]

            if event.Sequence <> int64 (index + 1) || event.PreviousEventHash <> previousHash then
                Internal.fail "GenerationReceipt-Eventkette ist nicht lueckenlos."

            if Internal.sha256Hex (eventCoreBytes runId eventRoot) <> event.EventHash then
                Internal.fail "GenerationReceipt-Eventhash ist ungueltig."

            match Internal.tryParseUtc event.TimestampUtc with
            | Some timestamp when
                timestamp >= startedAt
                && timestamp <= finishedAt
                && timestamp >= previousTimestamp
                ->
                previousTimestamp <- timestamp
            | None -> Internal.fail "GenerationReceipt-Eventzeit ist ungueltig."
            | _ -> Internal.fail "GenerationReceipt-Eventzeiten sind nicht chronologisch gebunden."

            previousHash <- Some event.EventHash

        let finalEvent = storedEvents |> List.last

        if
            finalEvent.EventHash <> getString "finalEventHash" receipt
            || finalEvent.EventType <> "run.finished"
            || finalEvent.TimestampUtc <> getString "finishedAtUtc" receipt
        then
            Internal.fail "GenerationReceipt-Abschlussanker ist inkonsistent."

        let generationEvents =
            storedEvents
            |> List.filter (fun event -> event.EventType = "asset.generation.completed")

        let generationEvent =
            match generationEvents with
            | [ event ] -> event
            | [] -> Internal.fail "GenerationReceipt enthaelt kein Generierungs-Abschlussereignis."
            | _ -> Internal.fail "GenerationReceipt enthaelt mehrere Generierungs-Abschlussereignisse."

        use generationPayload = JsonDocument.Parse(generationEvent.Payload)

        if
            getString "assetId" generationPayload.RootElement
            <> getString "assetId" manifestRoot
        then
            Internal.fail "GenerationReceipt-Ereignis gehoert zu einem anderen Asset."

        validateGenerationPayload manifestRoot generationEvent |> ignore

        let generationAnchor = receipt.GetProperty("generationEvent")

        if
            generationEvent.Sequence <> generationAnchor.GetProperty("sequence").GetInt64()
            || generationEvent.EventHash <> getString "eventHash" generationAnchor
            || Internal.sha256Hex generationEvent.Payload
               <> getString "payloadSha256" generationAnchor
        then
            Internal.fail "GenerationReceipt-Generierungsanker ist inkonsistent."

        use finishPayload = JsonDocument.Parse(finalEvent.Payload)
        let finishRoot = finishPayload.RootElement

        if
            getString "status" finishRoot <> getString "status" receipt
            || not (sameCanonical (finishRoot.GetProperty("summary")) (receipt.GetProperty("summary")))
        then
            Internal.fail "GenerationReceipt-Abschlusspayload ist inkonsistent."

        if
            getString "generationBindingMode" receipt = "canonical-event-v1"
            && getString "actorId" finishRoot <> getString "actorId" receipt
        then
            Internal.fail "GenerationReceipt-Abschlussakteur ist inkonsistent."

        match receipt.TryGetProperty("retrievalTraceCount"), receipt.TryGetProperty("finalRetrievalTraceHash") with
        | (true, count), (true, finalHash) ->
            let countValue = count.GetInt64()

            if
                countValue < 0L
                || (countValue = 0L) <> (finalHash.ValueKind = JsonValueKind.Null)
            then
                Internal.fail "GenerationReceipt-Retrievalanker besitzt keine gueltige Tail-Semantik."

            let hasCount, finishCount = finishRoot.TryGetProperty("retrievalTraceCount")
            let hasHash, finishHash = finishRoot.TryGetProperty("finalRetrievalTraceHash")

            if
                not hasCount
                || not hasHash
                || not (sameCanonical count finishCount)
                || not (sameCanonical finalHash finishHash)
            then
                Internal.fail "GenerationReceipt-Retrievalanker ist inkonsistent."
        | _ ->
            if getString "generationBindingMode" receipt = "canonical-event-v1" then
                Internal.fail "Kanonischer GenerationReceipt benoetigt einen Retrieval-Tail-Anker."

            let hasCount, _ = finishRoot.TryGetProperty("retrievalTraceCount")
            let hasHash, _ = finishRoot.TryGetProperty("finalRetrievalTraceHash")

            if hasCount || hasHash then
                Internal.fail "GenerationReceipt laesst vorhandenen Retrievalanker aus."

        if Internal.sha256Hex (summaryCoreBytes receipt) <> getString "summaryHash" receipt then
            Internal.fail "GenerationReceipt-Summaryhash ist ungueltig."

        generationEvent

    let private validatePortableReceipt (manifestRoot: JsonElement) (receipt: JsonElement) =
        try
            validatePortableReceiptCore manifestRoot receipt
        with
        | HarnessException _ -> reraise ()
        | :? JsonException
        | :? KeyNotFoundException
        | :? InvalidOperationException
        | :? FormatException
        | :? OverflowException -> Internal.fail "GenerationReceipt verletzt den portablen Eventkettenvertrag."

    let prepareGenerationReceipt root runId manifestPath =
        let locations = Workspace.requireInitialized root
        let manifestAbsolute = safeManifestPath locations "Assetmanifest" false manifestPath
        let schemas = loadSchemas locations
        let manifestSchemaErrors = schemaErrors schemas.Manifest manifestAbsolute

        if not (List.isEmpty manifestSchemaErrors) then
            Internal.fail "Assetmanifest verletzt das versionierte Schema; Receipt-Export abgebrochen."

        use manifestDocument =
            JsonDocument.Parse(safeJsonBytes manifestAbsolute "Assetmanifest")

        let manifest = manifestDocument.RootElement

        if getString "generationRunId" manifest <> runId then
            Internal.fail "Run-ID widerspricht dem Assetmanifest."

        let declaredReceipt = getString "generationReceipt" manifest
        let assetId = getString "assetId" manifest
        let expectedReceipt = $"assets/receipts/{assetId}/{runId}.json"

        if declaredReceipt <> expectedReceipt then
            Internal.fail "Ausgabepfad widerspricht generationReceipt im Assetmanifest."

        let snapshot = RunStore.completedSnapshot root runId
        let event = findGenerationEvent (getString "assetId" manifest) snapshot
        let specPath = validateGenerationPayload manifest event

        let receiptActor =
            if getString "generationBindingMode" manifest = "canonical-event-v1" then
                match snapshot.ActorId with
                | Some actor when
                    String.Equals(actor, getString "createdBy" manifest, StringComparison.OrdinalIgnoreCase)
                    ->
                    actor
                | _ -> Internal.fail "Kanonischer Generierungsrun bindet nicht den Manifest-Erzeuger."
            else
                getString "createdBy" manifest

        let inputs = manifest.GetProperty("inputs")

        let summaryPath =
            safeManifestPath locations "Run-Abschlussmanifest" false $".ai/runtime/runs/{runId}/summary.json"

        use summaryDocument =
            JsonDocument.Parse(safeJsonBytes summaryPath "Run-Abschlussmanifest")

        let summaryRoot = summaryDocument.RootElement

        let specInput =
            inputs.EnumerateArray()
            |> Seq.tryFind (fun input ->
                getOptionalString "path" input = Some specPath
                && getString "sha256" input = getString "specSha256" manifest)
            |> Option.defaultWith (fun () ->
                Internal.fail "Generierungsereignis bindet keinen Manifest-Spezifikationsinput.")

        let receiptCore =
            Internal.jsonBytes false (fun writer ->
                writer.WriteStartObject()
                writer.WriteNumber("schemaVersion", Constants.SchemaVersion)
                writer.WriteString("runId", snapshot.RunId)
                writer.WriteString("status", snapshot.Status)
                writer.WriteString("startedAtUtc", snapshot.StartedAtUtc)
                writer.WriteString("finishedAtUtc", snapshot.FinishedAtUtc)
                writer.WriteString("actorId", receiptActor)
                writer.WriteString("assetId", getString "assetId" manifest)
                writer.WriteString("generationBindingMode", getString "generationBindingMode" manifest)
                writer.WriteNumber("eventCount", snapshot.Events.Length)
                writer.WriteStartArray("events")

                for storedEvent in snapshot.Events do
                    writeStoredEvent writer storedEvent

                writer.WriteEndArray()
                writer.WriteStartObject("generationEvent")
                writer.WriteNumber("sequence", event.Sequence)
                writer.WriteString("eventHash", event.EventHash)
                writer.WriteString("payloadSha256", Internal.sha256Hex event.Payload)
                writer.WriteEndObject()
                writer.WriteString("finalEventHash", snapshot.FinalEventHash)

                match
                    summaryRoot.TryGetProperty("retrievalTraceCount"),
                    summaryRoot.TryGetProperty("finalRetrievalTraceHash")
                with
                | (true, count), (true, finalHash) ->
                    writer.WriteNumber("retrievalTraceCount", count.GetInt64())

                    match finalHash.ValueKind with
                    | JsonValueKind.Null -> writer.WriteNull("finalRetrievalTraceHash")
                    | _ -> writer.WriteString("finalRetrievalTraceHash", finalHash.GetString())
                | _ -> ()

                Internal.rawJson writer "summary" (Internal.canonicalElement (summaryRoot.GetProperty("summary")))
                writer.WriteString("summaryHash", snapshot.SummaryHash)
                writer.WriteStartObject("spec")
                writer.WriteString("path", specPath)
                writer.WriteString("sha256", getString "sha256" specInput)
                writer.WriteEndObject()
                Internal.rawJson writer "generator" (Internal.canonicalElement (manifest.GetProperty("generator")))
                Internal.rawJson writer "inputs" (Internal.canonicalElement inputs)
                Internal.rawJson writer "prompts" (Internal.canonicalElement (manifest.GetProperty("prompts")))

                Internal.rawJson
                    writer
                    "transformations"
                    (Internal.canonicalElement (manifest.GetProperty("transformations")))

                Internal.rawJson writer "outputs" (Internal.canonicalElement (manifest.GetProperty("outputs")))
                writer.WriteEndObject())

        use receiptCoreDocument = JsonDocument.Parse(receiptCore)

        let receiptHash =
            Internal.sha256Hex (receiptCoreBytes receiptCoreDocument.RootElement)

        let finalBytes =
            Internal.jsonBytes true (fun writer ->
                writer.WriteStartObject()
                writer.WriteString("$schema", "../../.ai/schemas/generation-receipt.schema.json")

                for property in receiptCoreDocument.RootElement.EnumerateObject() do
                    writer.WritePropertyName(property.Name)
                    writer.WriteRawValue(property.Value.GetRawText(), true)

                writer.WriteString("receiptSha256", receiptHash)
                writer.WriteEndObject())

        { RunId = runId
          AssetId = getString "assetId" manifest
          ReceiptPath = expectedReceipt
          ReceiptSha256 = receiptHash
          Bytes = finalBytes }

    let exportGenerationReceipt root runId manifestPath outputPath =
        let prepared = prepareGenerationReceipt root runId manifestPath
        let locations = Workspace.requireInitialized root
        let outputAbsolute = safeManifestPath locations "GenerationReceipt" true outputPath

        if Workspace.relativePath locations outputAbsolute <> prepared.ReceiptPath then
            Internal.fail "Ausgabepfad widerspricht generationReceipt im Assetmanifest."

        if File.Exists(outputAbsolute) || Directory.Exists(outputAbsolute) then
            Internal.fail "GenerationReceipt existiert bereits und wird nicht ueberschrieben."

        Directory.CreateDirectory(Path.GetDirectoryName(outputAbsolute)) |> ignore
        Internal.atomicWrite outputAbsolute prepared.Bytes

        { RunId = prepared.RunId
          AssetId = prepared.AssetId
          ReceiptPath = prepared.ReceiptPath
          ReceiptSha256 = prepared.ReceiptSha256 }

    let private validateReceipt
        locations
        (schema: JsonSchema)
        policy
        requireLocalRun
        manifestRelative
        findings
        (manifestRoot: JsonElement)
        =
        let receiptPath = getString "generationReceipt" manifestRoot
        let assetId = getString "assetId" manifestRoot
        let generationRunId = getString "generationRunId" manifestRoot
        let expectedReceiptPath = $"assets/receipts/{assetId}/{generationRunId}.json"

        if receiptPath <> expectedReceiptPath then
            addError
                findings
                "ASSET_RECEIPT_PATH_INVALID"
                (Some manifestRelative)
                "/generationReceipt"
                "GenerationReceipt muss dem kanonischen Asset-/Run-Pfad entsprechen."

        try
            let absolute = safeManifestPath locations "GenerationReceipt" false receiptPath

            let receiptSchemaErrors = schemaErrors schema absolute

            for path in receiptSchemaErrors do
                addError
                    findings
                    "ASSET_RECEIPT_SCHEMA_INVALID"
                    (Some manifestRelative)
                    path
                    "GenerationReceipt verletzt das versionierte Schema."

            if not (List.isEmpty receiptSchemaErrors) then
                Internal.fail "GenerationReceipt-Schema ist ungueltig."

            use document = JsonDocument.Parse(safeJsonBytes absolute "GenerationReceipt")
            let receipt = document.RootElement
            validateCleanRoomElement policy manifestRelative "receipt-content" findings receipt

            if
                Internal.sha256Hex (receiptCoreBytes receipt)
                <> getString "receiptSha256" receipt
            then
                addError
                    findings
                    "ASSET_RECEIPT_HASH_INVALID"
                    (Some manifestRelative)
                    "/generationReceipt"
                    "GenerationReceipt-Hash ist ungueltig."

            if
                getString "receiptSha256" receipt
                <> getString "generationReceiptSha256" manifestRoot
            then
                addError
                    findings
                    "ASSET_RECEIPT_DECLARATION_MISMATCH"
                    (Some manifestRelative)
                    "/generationReceiptSha256"
                    "Manifest bindet einen anderen GenerationReceipt-Hash."

            for receiptField, manifestField in
                [ "runId", "generationRunId"
                  "actorId", "createdBy"
                  "assetId", "assetId"
                  "generationBindingMode", "generationBindingMode" ] do
                if getString receiptField receipt <> getString manifestField manifestRoot then
                    addError
                        findings
                        "ASSET_RECEIPT_BINDING_MISMATCH"
                        (Some manifestRelative)
                        "/generationReceipt"
                        "GenerationReceipt widerspricht dem Assetmanifest."

            if getString "status" receipt <> "succeeded" then
                addError
                    findings
                    "ASSET_RECEIPT_STATUS_INVALID"
                    (Some manifestRelative)
                    "/generationReceipt"
                    "GenerationReceipt stammt nicht aus einem erfolgreichen Run."

            match
                Internal.tryParseUtc (getString "startedAtUtc" receipt),
                Internal.tryParseUtc (getString "finishedAtUtc" receipt)
            with
            | Some started, Some finished when finished >= started -> ()
            | _ ->
                addError
                    findings
                    "ASSET_RECEIPT_TIME_INVALID"
                    (Some manifestRelative)
                    "/generationReceipt"
                    "GenerationReceipt besitzt keinen konsistenten UTC-Zeitraum."

            for field in [ "generator"; "inputs"; "prompts"; "transformations"; "outputs" ] do
                if not (sameCanonical (receipt.GetProperty(field)) (manifestRoot.GetProperty(field))) then
                    addError
                        findings
                        "ASSET_RECEIPT_BINDING_MISMATCH"
                        (Some manifestRelative)
                        "/generationReceipt"
                        "GenerationReceipt widerspricht dem Assetmanifest."

            let portableEvent =
                try
                    Some(validatePortableReceipt manifestRoot receipt)
                with HarnessException _ ->
                    addError
                        findings
                        "ASSET_RECEIPT_CHAIN_INVALID"
                        (Some manifestRelative)
                        "/generationReceipt/events"
                        "GenerationReceipt besitzt keine gueltige portable Event- und Summarykette."

                    None

            let spec = receipt.GetProperty("spec")

            let specPath = getString "path" spec
            let specHash = getString "sha256" spec

            let specInputs =
                manifestRoot.GetProperty("inputs").EnumerateArray()
                |> Seq.filter (fun input ->
                    getString "allowedUse" input = "internal-specification"
                    && getOptionalString "path" input = Some specPath
                    && getString "sha256" input = specHash)
                |> Seq.length

            if specHash <> getString "specSha256" manifestRoot || specInputs <> 1 then
                addError
                    findings
                    "ASSET_RECEIPT_BINDING_MISMATCH"
                    (Some manifestRelative)
                    "/generationReceipt"
                    "GenerationReceipt widerspricht der Spezifikation."

            try
                let specAbsolute = safeManifestPath locations "Asset-Spezifikation" false specPath

                let specBytes = safeFileBytes specAbsolute "Asset-Spezifikation"

                if Internal.sha256Hex specBytes <> specHash then
                    addError
                        findings
                        "ASSET_RECEIPT_SPEC_INVALID"
                        (Some manifestRelative)
                        "/generationReceipt/spec"
                        "GenerationReceipt bindet keine gueltige lokale Spezifikation."
            with HarnessException _ ->
                addError
                    findings
                    "ASSET_RECEIPT_SPEC_INVALID"
                    (Some manifestRelative)
                    "/generationReceipt/spec"
                    "GenerationReceipt bindet keine gueltige lokale Spezifikation."

            let runDirectory = Path.Combine(locations.Runs, getString "runId" receipt)

            if Directory.Exists(runDirectory) then
                try
                    let snapshot = RunStore.completedSnapshot locations.Root (getString "runId" receipt)
                    let event = findGenerationEvent (getString "assetId" manifestRoot) snapshot
                    validateGenerationPayload manifestRoot event |> ignore

                    if
                        getString "generationBindingMode" manifestRoot = "canonical-event-v1"
                        && not (
                            snapshot.ActorId
                            |> Option.exists (fun actor ->
                                String.Equals(
                                    actor,
                                    getString "createdBy" manifestRoot,
                                    StringComparison.OrdinalIgnoreCase
                                )
                                && String.Equals(
                                    actor,
                                    getString "actorId" receipt,
                                    StringComparison.OrdinalIgnoreCase
                                ))
                        )
                    then
                        addError
                            findings
                            "ASSET_GENERATION_ACTOR_MISMATCH"
                            (Some manifestRelative)
                            "/generationRunId"
                            "Verifizierter Generierungsrun bindet nicht den Manifest-Erzeuger."

                    let receiptEvents =
                        receipt.GetProperty("events").EnumerateArray()
                        |> Seq.map (receiptEvent (getString "runId" receipt))
                        |> Seq.toList

                    let sameEvent (left: StoredEvent) (right: StoredEvent) =
                        left.Sequence = right.Sequence
                        && left.TimestampUtc = right.TimestampUtc
                        && left.EventType = right.EventType
                        && left.PreviousEventHash = right.PreviousEventHash
                        && left.EventHash = right.EventHash
                        && left.Payload = right.Payload

                    if
                        portableEvent.IsNone
                        || snapshot.Events.Length <> receiptEvents.Length
                        || not (List.forall2 sameEvent snapshot.Events receiptEvents)
                    then
                        addError
                            findings
                            "ASSET_RECEIPT_RUN_MISMATCH"
                            (Some manifestRelative)
                            "/generationReceipt/events"
                            "GenerationReceipt-Eventkette widerspricht dem lokalen verifizierten Run."

                    if
                        snapshot.FinalEventHash <> getString "finalEventHash" receipt
                        || snapshot.SummaryHash <> getString "summaryHash" receipt
                        || snapshot.StartedAtUtc <> getString "startedAtUtc" receipt
                        || snapshot.FinishedAtUtc <> getString "finishedAtUtc" receipt
                    then
                        addError
                            findings
                            "ASSET_RECEIPT_RUN_MISMATCH"
                            (Some manifestRelative)
                            "/generationReceipt"
                            "GenerationReceipt widerspricht dem lokalen verifizierten Run."

                    let generationEvent = receipt.GetProperty("generationEvent")

                    if
                        event.Sequence <> generationEvent.GetProperty("sequence").GetInt64()
                        || event.EventHash <> getString "eventHash" generationEvent
                        || Internal.sha256Hex event.Payload <> getString "payloadSha256" generationEvent
                    then
                        addError
                            findings
                            "ASSET_RECEIPT_RUN_MISMATCH"
                            (Some manifestRelative)
                            "/generationReceipt"
                            "GenerationReceipt-Ereignis widerspricht dem lokalen verifizierten Run."
                with HarnessException _ ->
                    addError
                        findings
                        "ASSET_RECEIPT_RUN_INVALID"
                        (Some manifestRelative)
                        "/generationReceipt"
                        "Lokaler Generierungsrun ist ungueltig."
            elif requireLocalRun || getString "status" manifestRoot = "approved" then
                addError
                    findings
                    "ASSET_GENERATION_RUN_MISSING"
                    (Some manifestRelative)
                    "/generationRunId"
                    "Freigabe oder lokale Pruefung benoetigt den verifizierbaren Generierungsrun."
        with
        | HarnessException _ ->
            addError
                findings
                "ASSET_RECEIPT_MISSING_OR_UNSAFE"
                (Some manifestRelative)
                "/generationReceipt"
                "GenerationReceipt fehlt oder besitzt einen unsicheren Pfad."
        | :? JsonException ->
            addError
                findings
                "ASSET_RECEIPT_JSON_INVALID"
                (Some manifestRelative)
                "/generationReceipt"
                "GenerationReceipt ist kein gueltiges JSON."

    let private validateModelAdmission manifestRelative findings models (manifestRoot: JsonElement) =
        let generator = manifestRoot.GetProperty("generator")

        if
            getString "status" manifestRoot = "approved"
            && getString "kind" generator = "ai"
        then
            let matches =
                models
                |> List.filter (fun model ->
                    getString "status" model = "approved"
                    && getString "model" model = getString "model" generator
                    && getString "modelVersion" model = getString "modelVersion" generator
                    && getString "executionMode" model = getString "executionMode" generator
                    && getOptionalString "modelArtifactSha256" model = getOptionalString
                        "modelArtifactSha256"
                        generator)

            if matches.Length <> 1 then
                addError
                    findings
                    "ASSET_MODEL_NOT_APPROVED"
                    (Some manifestRelative)
                    "/generator"
                    "KI-Generator ist nicht als eindeutiges freigegebenes Modell-Tupel zugelassen."

    let private manifestPaths locations requested =
        match requested with
        | Some path -> [ safeManifestPath locations "Assetmanifest" false path ]
        | None ->
            let directory = Path.Combine(locations.Root, "assets", "manifests")

            if not (Directory.Exists(directory)) then
                []
            else
                Directory.EnumerateFiles(directory, "*.json", SearchOption.TopDirectoryOnly)
                |> Seq.map (fun path -> Workspace.requireSafePath locations "Assetmanifest" false path)
                |> Seq.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right))
                |> Seq.toList

    let private checkCore root options =
        let locations = Workspace.paths root
        let findings = ResizeArray<AssetFinding>()
        let manifests = manifestPaths locations options.ManifestPath
        let schemas = loadSchemas locations
        let policy = validatePolicySchema locations schemas.CleanRoomPolicy findings
        let models = validateModelLock locations schemas.ModelsLock findings

        // A targeted quarantine check is deliberately process-free. Git/LFS is
        // relevant only to the global repository inventory or an approval
        // decision; local receipt/output/run validation remains fully in-process.
        let requiresRepositoryBoundary =
            options.ManifestPath.IsNone || options.RequireApproved

        let gitBoundaryValid =
            if not requiresRepositoryBoundary then
                true
            else
                try
                    validateRepositoryBoundary locations findings
                    true
                with HarnessException _ ->
                    addError
                        findings
                        "ASSET_GIT_CHECK_FAILED"
                        None
                        "git"
                        "Git-/LFS-Lebenszyklus konnte nicht sicher geprueft werden."

                    false

        let mutable approved = 0
        let mutable quarantine = 0
        let assetOwners = Dictionary<string, string>(StringComparer.Ordinal)
        let receiptOwners = Dictionary<string, string>(StringComparer.Ordinal)
        let outputOwners = Dictionary<string, string>(StringComparer.Ordinal)

        for manifestPath in manifests do
            let relative = Workspace.relativePath locations manifestPath
            let manifestReference = opaqueManifestReference relative

            try
                let manifestSchemaErrors = schemaErrors schemas.Manifest manifestPath

                for instanceLocation in manifestSchemaErrors do
                    findings.Add(
                        reportFinding
                            "error"
                            "ASSET_SCHEMA_INVALID"
                            (Some manifestReference)
                            instanceLocation
                            "Assetmanifest verletzt das versionierte Schema."
                    )

                if List.isEmpty manifestSchemaErrors then
                    use document = JsonDocument.Parse(safeJsonBytes manifestPath "Assetmanifest")
                    let manifestRoot = document.RootElement
                    let assetId = getString "assetId" manifestRoot

                    let receiptKey =
                        getString "generationReceipt" manifestRoot
                        + "\u0000"
                        + getString "generationReceiptSha256" manifestRoot

                    let registerUnique code (registry: Dictionary<string, string>) key =
                        match registry.TryGetValue(key) with
                        | true, _ ->
                            addError
                                findings
                                code
                                (Some manifestReference)
                                "manifest"
                                "Provenienzbeziehung ist nicht eindeutig."
                        | _ -> registry.Add(key, manifestReference)

                    registerUnique "ASSET_ID_DUPLICATE" assetOwners assetId
                    registerUnique "ASSET_RECEIPT_DUPLICATE" receiptOwners receiptKey

                    if getString "status" manifestRoot = "approved" then
                        for output in manifestRoot.GetProperty("outputs").EnumerateArray() do
                            registerUnique "ASSET_OUTPUT_OWNER_DUPLICATE" outputOwners (getString "path" output)

                    validateCleanRoom policy relative findings manifestRoot
                    validateCleanRoomInputs locations policy relative findings manifestRoot
                    validateCleanRoomOutputs locations policy relative findings manifestRoot
                    validateReviewHistory manifestReference findings manifestRoot
                    validateLicenseEvidence locations policy manifestReference findings manifestRoot

                    validateApprovedReviews
                        locations
                        schemas.ReviewEvidence
                        policy
                        manifestReference
                        findings
                        manifestRoot

                    validatePromptEnvelope manifestReference findings manifestRoot

                    validateInputsAndOutputs locations options.RequireLocal manifestReference findings manifestRoot

                    validateReceipt
                        locations
                        schemas.Receipt
                        policy
                        (options.RequireLocal || options.RequireApproved)
                        manifestReference
                        findings
                        manifestRoot

                    validateModelAdmission manifestReference findings models manifestRoot

                    match manifestRoot.TryGetProperty("status") with
                    | true, value when value.ValueKind = JsonValueKind.String && value.GetString() = "approved" ->
                        approved <- approved + 1
                    | true, value when value.ValueKind = JsonValueKind.String && value.GetString() = "quarantine" ->
                        quarantine <- quarantine + 1
                    | _ -> ()

                    if options.RequireApproved then
                        match manifestRoot.TryGetProperty("status") with
                        | true, value when value.ValueKind = JsonValueKind.String && value.GetString() = "approved" ->
                            ()
                        | _ ->
                            findings.Add(
                                reportFinding
                                    "error"
                                    "ASSET_APPROVAL_REQUIRED"
                                    (Some manifestReference)
                                    "/status"
                                    "Der Aufruf verlangt ein freigegebenes Asset."
                            )
            with
            | HarnessException _ ->
                findings.Add(
                    reportFinding
                        "error"
                        "ASSET_INPUT_INVALID"
                        (Some manifestReference)
                        ""
                        "Assetmanifest konnte nicht sicher validiert werden."
                )
            | :? JsonException ->
                findings.Add(
                    reportFinding
                        "error"
                        "ASSET_JSON_INVALID"
                        (Some manifestReference)
                        ""
                        "Assetmanifest ist kein gueltiges JSON."
                )

        if options.ManifestPath.IsNone && gitBoundaryValid then
            try
                let exitCode, trackedSource, _ =
                    processResult locations.Root "git" [ "ls-files"; "--"; "assets/source" ]

                if exitCode <> 0 then
                    addError
                        findings
                        "ASSET_GIT_CHECK_FAILED"
                        None
                        "git"
                        "Git-Quellinventar konnte nicht geprueft werden."
                else
                    for sourcePath in trackedSource.Split('\n', StringSplitOptions.RemoveEmptyEntries) do
                        if
                            not (String.Equals(sourcePath, "assets/source/README.md", StringComparison.Ordinal))
                            && not (outputOwners.ContainsKey(sourcePath))
                        then
                            addError
                                findings
                                "ASSET_SOURCE_ORPHAN"
                                None
                                "assets/source"
                                "Versionierte Assetquelle besitzt kein eindeutiges freigegebenes Manifest."
            with HarnessException _ ->
                addError
                    findings
                    "ASSET_GIT_CHECK_FAILED"
                    None
                    "git"
                    "Git-Quellinventar konnte nicht sicher geprueft werden."

        if List.isEmpty manifests then
            findings.Add(
                reportFinding
                    "error"
                    "ASSET_MANIFEST_MISSING"
                    None
                    "assets/manifests"
                    "Kein Assetmanifest wurde zur Pruefung gefunden."
            )

        let errors = findings |> Seq.exists (fun finding -> finding.Severity = "error")

        { Scope = if options.ManifestPath.IsSome then "targeted" else "global"
          Valid = not errors
          ShippingReady =
            options.ManifestPath.IsNone
            && not errors
            && manifests.Length > 0
            && approved = manifests.Length
          ManifestsChecked = manifests.Length
          ApprovedCount = approved
          QuarantineCount = quarantine
          Findings = findings |> Seq.toList }

    let check root options =
        try
            checkCore root options
        with
        | HarnessException _
        | :? JsonException
        | :? IOException
        | :? UnauthorizedAccessException ->
            { Scope = if options.ManifestPath.IsSome then "targeted" else "global"
              Valid = false
              ShippingReady = false
              ManifestsChecked = 0
              ApprovedCount = 0
              QuarantineCount = 0
              Findings =
                [ reportFinding
                      "error"
                      "ASSET_TRUST_ROOT_INVALID"
                      None
                      "asset-contract"
                      "Asset-Vertrauensbasis oder lokale Voraussetzung konnte nicht sicher geprueft werden." ] }

    let reportJson report =
        Internal.jsonBytes true (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", Constants.SchemaVersion)
            writer.WriteString("scope", report.Scope)
            writer.WriteBoolean("valid", report.Valid)
            writer.WriteBoolean("shippingReady", report.ShippingReady)
            writer.WriteNumber("manifestsChecked", report.ManifestsChecked)
            writer.WriteNumber("approvedCount", report.ApprovedCount)
            writer.WriteNumber("quarantineCount", report.QuarantineCount)
            writer.WriteStartArray("findings")

            for finding in report.Findings do
                writer.WriteStartObject()
                writer.WriteString("severity", finding.Severity)
                writer.WriteString("code", finding.Code)

                match finding.Manifest with
                | Some manifest -> writer.WriteString("manifest", manifest)
                | None -> writer.WriteNull("manifest")

                writer.WriteString("path", finding.Path)
                writer.WriteString("message", finding.Message)

                match finding.MatchSha256 with
                | Some hash -> writer.WriteString("matchSha256", hash)
                | None -> ()

                writer.WriteEndObject()

            writer.WriteEndArray()
            writer.WriteEndObject())
        |> Constants.Utf8NoBom.GetString
