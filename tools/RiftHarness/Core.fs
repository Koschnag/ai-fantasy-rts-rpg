namespace RiftHarness

open System
open System.Buffers
open System.Globalization
open System.IO
open System.Security.Cryptography
open System.Text
open System.Text.Json
open System.Text.RegularExpressions

exception HarnessException of string

[<RequireQualifiedAccess>]
module Constants =
    [<Literal>]
    let SchemaVersion = 1

    [<Literal>]
    let MaxPayloadBytes = 4L * 1024L * 1024L

    [<Literal>]
    let MaxConfigurablePayloadBytes = 16L * 1024L * 1024L

    let Utf8NoBom = UTF8Encoding(false, true)

type WorkspacePaths =
    { Root: string
      State: string
      Runtime: string
      Runs: string
      Index: string
      Config: string
      IndexFile: string }

type RedactionPolicy =
    { KeyPatterns: Regex array
      ValuePatterns: Regex array }

type HarnessRuntimeConfig =
    { MaxEventPayloadBytes: int64
      MaxSourceFileBytes: int64
      Redaction: RedactionPolicy
      MemoryPath: string }

[<RequireQualifiedAccess>]
module Internal =
    let fail message = raise (HarnessException message)

    let sha256Hex (bytes: byte array) =
        SHA256.HashData(bytes)
        |> Convert.ToHexString
        |> fun value -> value.ToLowerInvariant()

    let sha256Text (value: string) =
        value |> Constants.Utf8NoBom.GetBytes |> sha256Hex

    let sha256File (path: string) =
        use stream = File.OpenRead(path)

        SHA256.HashData(stream)
        |> Convert.ToHexString
        |> fun value -> value.ToLowerInvariant()

    let utcText (value: DateTimeOffset) =
        value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture)

    let tryParseUtc (value: string) =
        match DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind) with
        | true, parsed when parsed.Offset = TimeSpan.Zero -> Some parsed
        | _ -> None

    let jsonBytes indented (write: Utf8JsonWriter -> unit) =
        use stream = new MemoryStream()
        let options = JsonWriterOptions(Indented = indented)
        use writer = new Utf8JsonWriter(stream, options)
        write writer
        writer.Flush()
        stream.ToArray()

    let atomicWrite (path: string) (bytes: byte array) =
        let parent = Path.GetDirectoryName(path)

        if not (String.IsNullOrEmpty(parent)) then
            Directory.CreateDirectory(parent) |> ignore

        let temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp"

        try
            File.WriteAllBytes(temporary, bytes)
            File.Move(temporary, path, true)
        finally
            if File.Exists(temporary) then
                File.Delete(temporary)

    let private secretNames =
        set
            [ "accesstoken"
              "apikey"
              "authorization"
              "clientsecret"
              "connectionstring"
              "cookie"
              "credential"
              "credentials"
              "idtoken"
              "password"
              "passwd"
              "privatekey"
              "refreshtoken"
              "secret"
              "token" ]

    let private normalizePropertyName (value: string) =
        value
        |> Seq.filter Char.IsLetterOrDigit
        |> Seq.map Char.ToLowerInvariant
        |> Seq.toArray
        |> String

    let isSensitiveProperty name =
        secretNames.Contains(normalizePropertyName name)

    let private matchesRegex kind (patterns: Regex array) (value: string) =
        patterns
        |> Array.exists (fun pattern ->
            try
                pattern.IsMatch(value)
            with :? RegexMatchTimeoutException ->
                fail $"Regex-Timeout bei security.{kind}: {pattern}. Konfiguration abgelehnt.")

    let isSensitivePropertyWithPolicy (policy: RedactionPolicy) name =
        isSensitiveProperty name
        || matchesRegex "redactKeyPatterns" policy.KeyPatterns name
        || matchesRegex "redactKeyPatterns" policy.KeyPatterns (normalizePropertyName name)

    let isSensitiveValue (policy: RedactionPolicy) value =
        not (isNull value)
        && matchesRegex "redactValuePatterns" policy.ValuePatterns value

    let private assignmentPattern =
        Regex(
            "(?<key>[A-Za-z0-9_.-]{1,128})(?<separator>\\s*[:=]\\s*)(?<value>\\\"[^\\\"]*\\\"|'[^']*'|[^\\s,;]+)",
            RegexOptions.CultureInvariant ||| RegexOptions.NonBacktracking,
            TimeSpan.FromMilliseconds(100.0)
        )

    let redactText (policy: RedactionPolicy) (value: string) =
        if isNull value then
            value
        elif isSensitiveValue policy value then
            "[REDACTED]"
        else
            try
                assignmentPattern.Replace(
                    value,
                    MatchEvaluator(fun assignment ->
                        let key = assignment.Groups["key"].Value

                        if isSensitivePropertyWithPolicy policy key then
                            key + assignment.Groups["separator"].Value + "[REDACTED]"
                        else
                            assignment.Value)
                )
            with :? RegexMatchTimeoutException ->
                fail "Regex-Timeout bei der Freitext-Redaction. Eingabe abgelehnt."

    let isSha256 (value: string) =
        not (isNull value)
        && value.Length = 64
        && value
           |> Seq.forall (fun character -> Char.IsAsciiHexDigit(character) && not (Char.IsUpper(character)))

    let rec private writeCanonicalElementWithPolicy
        (redaction: RedactionPolicy option)
        (writer: Utf8JsonWriter)
        (element: JsonElement)
        =
        match element.ValueKind with
        | JsonValueKind.Object ->
            writer.WriteStartObject()

            element.EnumerateObject()
            |> Seq.sortWith (fun left right -> StringComparer.Ordinal.Compare(left.Name, right.Name))
            |> Seq.iter (fun property ->
                writer.WritePropertyName(property.Name)

                if
                    redaction
                    |> Option.exists (fun policy -> isSensitivePropertyWithPolicy policy property.Name)
                then
                    writer.WriteStringValue("[REDACTED]")
                else
                    writeCanonicalElementWithPolicy redaction writer property.Value)

            writer.WriteEndObject()
        | JsonValueKind.Array ->
            writer.WriteStartArray()

            element.EnumerateArray()
            |> Seq.iter (writeCanonicalElementWithPolicy redaction writer)

            writer.WriteEndArray()
        | JsonValueKind.String ->
            let value = element.GetString()

            match redaction with
            | Some policy -> writer.WriteStringValue(redactText policy value)
            | None -> writer.WriteStringValue(value)
        | JsonValueKind.Number -> writer.WriteRawValue(element.GetRawText(), true)
        | JsonValueKind.True -> writer.WriteBooleanValue(true)
        | JsonValueKind.False -> writer.WriteBooleanValue(false)
        | JsonValueKind.Null -> writer.WriteNullValue()
        | kind -> fail $"Nicht unterstuetzte JSON-Art: {kind}."

    let private canonicalJsonUsing redaction (text: string) =
        try
            use document = JsonDocument.Parse(text)
            jsonBytes false (fun writer -> writeCanonicalElementWithPolicy redaction writer document.RootElement)
        with :? JsonException as error ->
            fail $"Ungueltiges JSON: {error.Message}"

    let canonicalJsonWithRedaction policy text = canonicalJsonUsing (Some policy) text

    let canonicalJson (redact: bool) (text: string) =
        let redaction =
            if redact then
                Some
                    { KeyPatterns = Array.empty
                      ValuePatterns = Array.empty }
            else
                None

        canonicalJsonUsing redaction text

    let canonicalElement (element: JsonElement) =
        jsonBytes false (fun writer -> writeCanonicalElementWithPolicy None writer element)

    let canonicalElementText (element: JsonElement) =
        element |> canonicalElement |> Constants.Utf8NoBom.GetString

    let rawJson (writer: Utf8JsonWriter) (propertyName: string) (canonicalJsonBytes: byte array) =
        writer.WritePropertyName(propertyName)
        writer.WriteRawValue(Constants.Utf8NoBom.GetString(canonicalJsonBytes), true)

    let requiredProperty (name: string) (element: JsonElement) =
        match element.TryGetProperty(name) with
        | true, value -> value
        | _ -> fail $"JSON-Feld '{name}' fehlt."

    let requiredString (name: string) (element: JsonElement) =
        let value = requiredProperty name element

        if value.ValueKind <> JsonValueKind.String then
            fail $"JSON-Feld '{name}' muss eine Zeichenfolge sein."

        value.GetString()

    let requiredInt (name: string) (element: JsonElement) =
        let value = requiredProperty name element

        match value.TryGetInt32() with
        | true, result -> result
        | _ -> fail $"JSON-Feld '{name}' muss eine Ganzzahl sein."

    let requiredInt64 (name: string) (element: JsonElement) =
        let value = requiredProperty name element

        match value.TryGetInt64() with
        | true, result -> result
        | _ -> fail $"JSON-Feld '{name}' muss eine Ganzzahl sein."

    let safeReadAllText (path: string) maxBytes =
        let info = FileInfo(path)

        if not info.Exists then
            fail $"Datei nicht gefunden: {path}"

        if info.Length > maxBytes then
            fail $"Datei ist groesser als das erlaubte Limit von {maxBytes} Bytes: {path}"

        File.ReadAllText(path, Constants.Utf8NoBom)

    let runIdAlphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"

    let isRunId (value: string) =
        not (isNull value)
        && value.Length = 26
        && value |> Seq.forall (fun character -> runIdAlphabet.Contains(character))

    let createRunId (now: DateTimeOffset) =
        let timestamp = now.ToUnixTimeMilliseconds()

        if timestamp < 0L || timestamp > 0xFFFFFFFFFFFFL then
            fail "Zeitstempel liegt ausserhalb des ULID-Bereichs."

        let result = Array.zeroCreate<char> 26
        let mutable remainingTimestamp = timestamp

        for index = 9 downto 0 do
            result[index] <- runIdAlphabet[int (remainingTimestamp &&& 31L)]
            remainingTimestamp <- remainingTimestamp >>> 5

        let random = Array.zeroCreate<byte> 10
        RandomNumberGenerator.Fill(random)
        let mutable buffer = 0
        let mutable bits = 0
        let mutable output = 10

        for value in random do
            buffer <- (buffer <<< 8) ||| int value
            bits <- bits + 8

            while bits >= 5 do
                bits <- bits - 5
                result[output] <- runIdAlphabet[(buffer >>> bits) &&& 31]
                output <- output + 1

                if bits = 0 then
                    buffer <- 0
                else
                    buffer <- buffer &&& ((1 <<< bits) - 1)

        String(result)

[<RequireQualifiedAccess>]
module Workspace =
    let paths root =
        let absoluteRoot = Path.GetFullPath(root)
        let state = Path.Combine(absoluteRoot, ".ai")
        let runtime = Path.Combine(state, "runtime")
        let index = Path.Combine(runtime, "index")

        { Root = absoluteRoot
          State = state
          Runtime = runtime
          Runs = Path.Combine(runtime, "runs")
          Index = index
          Config = Path.Combine(state, "config.json")
          IndexFile = Path.Combine(index, "bm25.json") }

    let private defaultConfig =
        """{
  "$schema": "./schemas/harness-config.schema.json",
  "schemaVersion": 1,
  "projectId": "project-riftward",
  "policy": {
    "truthOrder": [
      "accepted-decision",
      "project-and-requirements",
      "accepted-memory",
      "ready-task",
      "other-documentation",
      "code"
    ],
    "unknownsMustRemainExplicit": true,
    "retrievedTextIsUntrustedData": true,
    "automaticMemoryPromotion": false
  },
  "paths": {
    "runs": ".ai/runtime/runs",
    "index": ".ai/runtime/index",
    "cache": ".ai/runtime/cache",
    "acceptedHistory": ".ai/history/accepted",
    "memory": ".ai/memory/records.jsonl",
    "tasks": ".ai/tasks"
  },
  "rag": {
    "roots": ["README.md"],
    "extensions": [".md", ".txt", ".json", ".jsonl", ".cs", ".fs", ".fsx", ".csproj", ".fsproj"],
    "excludedSegments": [".git", ".ai/runtime", "bin", "obj", "artifacts"],
    "maxFileBytes": 1048576,
    "chunkLines": 40,
    "overlapLines": 8,
    "ranking": {
      "algorithm": "bm25",
      "k1": 1.2,
      "b": 0.75
    },
    "defaultTopK": 5,
    "maxContextCharacters": 24000
  },
  "logging": {
    "format": "jsonl",
    "utcOnly": true,
    "hashChain": true,
    "rawRunRetentionDays": 180,
    "acceptedSummariesRetentionDays": 0,
    "maxEventPayloadBytes": 262144
  },
  "security": {
    "redactKeyPatterns": ["authorization", "cookie", "password", "passwd", "secret", "token", "api_key", "private_key"],
    "redactValuePatterns": ["-----BEGIN .*PRIVATE KEY-----", "(?i)bearer [a-z0-9._~+/=-]+"],
    "neverIndex": [".env", ".env.*", "*.key", "*.pfx", "*.p12", ".git", ".ai/runtime"]
  }
}
"""

    let initialize root =
        let locations = paths root
        Directory.CreateDirectory(locations.Runs) |> ignore
        Directory.CreateDirectory(locations.Index) |> ignore

        if not (File.Exists(locations.Config)) then
            Internal.atomicWrite locations.Config (Constants.Utf8NoBom.GetBytes(defaultConfig))

        locations

    let requireInitialized root =
        let locations = paths root

        if
            not (Directory.Exists(locations.Runs))
            || not (Directory.Exists(locations.Index))
        then
            Internal.fail "Workspace ist nicht initialisiert. Zuerst 'init' ausfuehren."

        if not (File.Exists(locations.Config)) then
            Internal.fail $"Konfiguration fehlt: {locations.Config}"

        locations

    let relativePath (locations: WorkspacePaths) (path: string) =
        Path.GetRelativePath(locations.Root, Path.GetFullPath(path)).Replace('\\', '/')

    let isInside (locations: WorkspacePaths) (path: string) =
        let candidate = Path.GetFullPath(path)

        let rootWithSeparator =
            locations.Root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + string Path.DirectorySeparatorChar

        let comparison =
            if OperatingSystem.IsWindows() then
                StringComparison.OrdinalIgnoreCase
            else
                StringComparison.Ordinal

        candidate.Equals(locations.Root, comparison)
        || candidate.StartsWith(rootWithSeparator, comparison)

    let requireSafePath locations description allowMissingSuffix path =
        let candidate = Path.GetFullPath(path)

        if not (isInside locations candidate) then
            Internal.fail $"{description} liegt ausserhalb des Workspace."

        let relative = Path.GetRelativePath(locations.Root, candidate)

        if relative = "." then
            Internal.fail $"{description} muss eine Datei innerhalb des Workspace bezeichnen."

        let segments =
            relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)

        let mutable current = locations.Root
        let mutable missingSuffix = false

        for index = 0 to segments.Length - 1 do
            current <- Path.Combine(current, segments[index])
            let fileLink = FileInfo(current).LinkTarget
            let directoryLink = DirectoryInfo(current).LinkTarget

            if not (isNull fileLink) || not (isNull directoryLink) then
                Internal.fail
                    $"{description} darf keinen Symlink, Junction oder ReparsePoint enthalten: {relative.Replace('\\', '/')}"

            if not missingSuffix then
                let exists = File.Exists(current) || Directory.Exists(current)

                if not exists then
                    if not allowMissingSuffix then
                        Internal.fail
                            $"{description} existiert nicht innerhalb des Workspace: {relative.Replace('\\', '/')}"

                    missingSuffix <- true
                else
                    let attributes = File.GetAttributes(current)

                    if attributes.HasFlag(FileAttributes.ReparsePoint) then
                        Internal.fail
                            $"{description} darf keinen Symlink, Junction oder ReparsePoint enthalten: {relative.Replace('\\', '/')}"

        candidate

[<RequireQualifiedAccess>]
module HarnessConfig =
    let private regexTimeout = TimeSpan.FromMilliseconds(100.0)

    let private truthOrder =
        [ "accepted-decision"
          "project-and-requirements"
          "accepted-memory"
          "ready-task"
          "other-documentation"
          "code" ]

    let private validateSectionFields sectionName (expected: Set<string>) (section: JsonElement) =
        if section.ValueKind <> JsonValueKind.Object then
            Internal.fail $"{sectionName} muss ein JSON-Objekt sein."

        let actual =
            section.EnumerateObject()
            |> Seq.map (fun property -> property.Name)
            |> Set.ofSeq

        if actual <> expected then
            let missing = Set.difference expected actual |> String.concat ", "
            let extra = Set.difference actual expected |> String.concat ", "
            Internal.fail $"{sectionName} hat falsche Felder (fehlend: [{missing}]; unerlaubt: [{extra}])."

    let private requireFixedString sectionName field expected (section: JsonElement) =
        let actual = Internal.requiredString field section

        if not (String.Equals(actual, expected, StringComparison.Ordinal)) then
            Internal.fail $"{sectionName}.{field} wird in Harness v1 nur als '{expected}' unterstuetzt."

    let private requireFixedBoolean sectionName field expected (section: JsonElement) =
        let value = Internal.requiredProperty field section

        if value.ValueKind <> JsonValueKind.True && value.ValueKind <> JsonValueKind.False then
            Internal.fail $"{sectionName}.{field} muss ein Boolean sein."

        if value.GetBoolean() <> expected then
            Internal.fail
                $"{sectionName}.{field} wird in Harness v1 nur als '{expected.ToString().ToLowerInvariant()}' unterstuetzt."

    let private requireFixedInt sectionName field expected (section: JsonElement) =
        let actual = Internal.requiredInt field section

        if actual <> expected then
            Internal.fail $"{sectionName}.{field} wird in Harness v1 nur als {expected} unterstuetzt."

    let private validatePolicy (root: JsonElement) =
        match root.TryGetProperty("policy") with
        | false, _ -> ()
        | true, policy ->
            validateSectionFields
                "policy"
                (set
                    [ "truthOrder"
                      "unknownsMustRemainExplicit"
                      "retrievedTextIsUntrustedData"
                      "automaticMemoryPromotion" ])
                policy

            requireFixedBoolean "policy" "unknownsMustRemainExplicit" true policy
            requireFixedBoolean "policy" "retrievedTextIsUntrustedData" true policy
            requireFixedBoolean "policy" "automaticMemoryPromotion" false policy

            let configuredOrder = Internal.requiredProperty "truthOrder" policy

            if configuredOrder.ValueKind <> JsonValueKind.Array then
                Internal.fail "policy.truthOrder muss ein JSON-Array sein."

            let actualOrder =
                configuredOrder.EnumerateArray()
                |> Seq.map (fun item ->
                    if item.ValueKind <> JsonValueKind.String then
                        Internal.fail "Jeder Eintrag in policy.truthOrder muss eine Zeichenfolge sein."

                    item.GetString())
                |> Seq.toList

            if actualOrder <> truthOrder then
                let supportedOrder = String.concat ", " truthOrder

                Internal.fail $"policy.truthOrder wird in Harness v1 nur als [{supportedOrder}] unterstuetzt."

    let private validatePaths (root: JsonElement) =
        match root.TryGetProperty("paths") with
        | false, _ -> ()
        | true, paths ->
            validateSectionFields "paths" (set [ "runs"; "index"; "cache"; "acceptedHistory"; "memory"; "tasks" ]) paths

            requireFixedString "paths" "runs" ".ai/runtime/runs" paths
            requireFixedString "paths" "index" ".ai/runtime/index" paths
            requireFixedString "paths" "cache" ".ai/runtime/cache" paths
            requireFixedString "paths" "acceptedHistory" ".ai/history/accepted" paths
            requireFixedString "paths" "tasks" ".ai/tasks" paths

            let memory = Internal.requiredString "memory" paths

            if String.IsNullOrWhiteSpace(memory) then
                Internal.fail "paths.memory muss eine nicht leere Zeichenfolge sein."

    let private readPatternArray (field: string) (security: JsonElement) =
        match security.TryGetProperty(field) with
        | false, _ -> Array.empty
        | true, value when value.ValueKind <> JsonValueKind.Array ->
            Internal.fail $"security.{field} muss ein JSON-Array sein."
        | true, value ->
            let patterns = value.EnumerateArray() |> Seq.toArray

            if patterns.Length > 64 then
                Internal.fail $"security.{field} darf hoechstens 64 Muster enthalten."

            patterns
            |> Array.mapi (fun index item ->
                if item.ValueKind <> JsonValueKind.String then
                    Internal.fail $"security.{field}[{index}] muss eine Zeichenfolge sein."

                let pattern = item.GetString()

                if String.IsNullOrWhiteSpace(pattern) || pattern.Length > 512 then
                    Internal.fail $"security.{field}[{index}] muss 1 bis 512 Zeichen lang sein."

                try
                    Regex(
                        pattern,
                        RegexOptions.CultureInvariant
                        ||| RegexOptions.IgnoreCase
                        ||| RegexOptions.NonBacktracking,
                        regexTimeout
                    )
                with
                | :? ArgumentException as error ->
                    Internal.fail $"Ungueltiger Regex in security.{field}[{index}]: {error.Message}"
                | :? NotSupportedException as error ->
                    Internal.fail
                        $"Unsicherer/nicht unterstuetzter Regex in security.{field}[{index}]: {error.Message}")

    let load (locations: WorkspacePaths) =
        try
            use document = JsonDocument.Parse(File.ReadAllBytes(locations.Config))
            let root = document.RootElement

            if Internal.requiredInt "schemaVersion" root <> Constants.SchemaVersion then
                Internal.fail "config.json hat eine nicht unterstuetzte Schema-Version."

            validatePolicy root
            validatePaths root

            let maxEventPayloadBytes =
                match root.TryGetProperty("logging") with
                | false, _ -> Constants.MaxPayloadBytes
                | true, logging ->
                    validateSectionFields
                        "logging"
                        (set
                            [ "format"
                              "utcOnly"
                              "hashChain"
                              "rawRunRetentionDays"
                              "acceptedSummariesRetentionDays"
                              "maxEventPayloadBytes" ])
                        logging

                    requireFixedString "logging" "format" "jsonl" logging
                    requireFixedBoolean "logging" "utcOnly" true logging
                    requireFixedBoolean "logging" "hashChain" true logging
                    requireFixedInt "logging" "rawRunRetentionDays" 180 logging
                    requireFixedInt "logging" "acceptedSummariesRetentionDays" 0 logging

                    let value = Internal.requiredProperty "maxEventPayloadBytes" logging

                    match value.TryGetInt64() with
                    | true, parsed when parsed >= 1024L && parsed <= Constants.MaxConfigurablePayloadBytes -> parsed
                    | _ ->
                        Internal.fail
                            $"logging.maxEventPayloadBytes muss zwischen 1024 und {Constants.MaxConfigurablePayloadBytes} liegen."

            let maxSourceFileBytes =
                match root.TryGetProperty("rag") with
                | false, _ -> Constants.MaxConfigurablePayloadBytes
                | true, rag ->
                    match rag.TryGetProperty("maxFileBytes") with
                    | false, _ -> Constants.MaxConfigurablePayloadBytes
                    | true, value ->
                        match value.TryGetInt64() with
                        | true, parsed when parsed >= 1024L -> parsed
                        | _ -> Internal.fail "rag.maxFileBytes muss mindestens 1024 sein."

            let redaction =
                match root.TryGetProperty("security") with
                | false, _ ->
                    { KeyPatterns = Array.empty
                      ValuePatterns = Array.empty }
                | true, security ->
                    { KeyPatterns = readPatternArray "redactKeyPatterns" security
                      ValuePatterns = readPatternArray "redactValuePatterns" security }

            let memoryPath =
                match root.TryGetProperty("paths") with
                | false, _ -> ".ai/memory/records.jsonl"
                | true, paths -> Internal.requiredString "memory" paths |> fun value -> value.Replace('\\', '/')

            if
                Path.IsPathRooted(memoryPath)
                || memoryPath.Split('/') |> Array.exists ((=) "..")
                || memoryPath.Contains('*')
                || memoryPath.Contains('?')
            then
                Internal.fail "paths.memory muss ein relativer Dateipfad ohne Globs oder '..' sein."

            { MaxEventPayloadBytes = maxEventPayloadBytes
              MaxSourceFileBytes = maxSourceFileBytes
              Redaction = redaction
              MemoryPath = memoryPath.TrimStart('/') }
        with
        | :? JsonException as error -> Internal.fail $"Ungueltige config.json: {error.Message}"
        | :? IOException as error -> Internal.fail $"config.json konnte nicht gelesen werden: {error.Message}"
