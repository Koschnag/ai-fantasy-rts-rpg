namespace RiftHarness

open System
open System.Collections.Generic
open System.IO
open System.Security.Cryptography
open System.Text.Json
open System.Text.RegularExpressions

/// Explizite Vollstaendigkeitskennzeichnung der Run-Provenienz.
type ProvenanceCompleteness =
    { Config: bool
      Git: bool
      Model: bool
      Prompt: bool
      Task: bool
      Toolchain: bool }

/// Erweiterte Lauf-Provenienz im run.json-Manifest (T-004).
type RunProvenance =
    { SchemaVersion: int
      ConfigSha256: string
      GitCommit: string option
      ModelId: string option
      PromptSha256: string option
      TaskId: string option
      ToolchainSha256: string option
      Complete: ProvenanceCompleteness }

/// Strukturierte Trace-/Span-Huelle in Event-Payloads.
type SpanEnvelope =
    { TraceId: string
      SpanId: string
      ParentSpanId: string option
      CriterionId: string }

[<RequireQualifiedAccess>]
module Provenance =

    let private regexTimeout = TimeSpan.FromMilliseconds(100.0)

    let private compile pattern =
        Regex(pattern, RegexOptions.CultureInvariant ||| RegexOptions.NonBacktracking, regexTimeout)

    let private taskIdRegex = compile @"^T-[0-9]{3,}$"
    let private commitRegex = compile @"^[0-9a-f]{40}$|^[0-9a-f]{64}$"
    let private traceIdRegex = compile @"^[0-9a-f]{32}$"
    let private spanIdRegex = compile @"^[0-9a-f]{16}$"
    let private criterionRegex = compile @"^AC-[A-Z][A-Z0-9]{1,11}-[0-9]{2,3}$"

    /// Ereignistypen, deren Payload eine vollstaendige Trace-/Span-/Kriteriums-Huelle tragen muss.
    let requiredSpanTypes =
        set [ "retrieval.recorded"; "tool.executed"; "evidence.recorded" ]

    /// Zulaessige Evidenzarten gemaess evidence.schema.json.
    let evidenceKinds =
        set
            [ "build"
              "unit-test"
              "integration-test"
              "replay"
              "benchmark"
              "visual"
              "asset-validation"
              "security"
              "license"
              "manual-review" ]

    /// Erzeugt eine neue 128-Bit-Trace-ID (32 Kleinbuchstaben-Hexzeichen).
    let newTraceId () =
        let bytes = Array.zeroCreate<byte> 16
        RandomNumberGenerator.Fill(bytes)
        Convert.ToHexString(bytes).ToLowerInvariant()

    /// Erzeugt eine neue 64-Bit-Span-ID (16 Kleinbuchstaben-Hexzeichen).
    let newSpanId () =
        let bytes = Array.zeroCreate<byte> 8
        RandomNumberGenerator.Fill(bytes)
        Convert.ToHexString(bytes).ToLowerInvariant()

    /// Prueft das Format einer Trace-ID (32 Kleinbuchstaben-Hexzeichen).
    let isTraceId (value: string) =
        not (isNull value) && traceIdRegex.IsMatch(value)

    /// Prueft das Format einer Span-ID (16 Kleinbuchstaben-Hexzeichen).
    let isSpanId (value: string) =
        not (isNull value) && spanIdRegex.IsMatch(value)

    // ------------------------------------------------------------------
    // Strikte JSON-Feldpruefung (keine Duplikate, keine Fremdfelder)
    // ------------------------------------------------------------------

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

    // ------------------------------------------------------------------
    // Git-Stand ohne Unterprozess lesen; fail-closed zu None.
    // ------------------------------------------------------------------

    let private validRefName (name: string) =
        if
            String.IsNullOrWhiteSpace(name)
            || name.Length > 200
            || name.Contains('\\')
            || not (name.StartsWith("refs/", StringComparison.Ordinal))
        then
            false
        else
            name.Split('/')
            |> Array.forall (fun segment ->
                segment.Length > 0
                && segment <> "."
                && segment <> ".."
                && segment
                   |> Seq.forall (fun character ->
                       Char.IsLetterOrDigit character
                       || character = '-'
                       || character = '_'
                       || character = '.'))

    let private tryReadSmallText (root: string) (relative: string) =
        try
            let path = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar))

            let info = FileInfo(path)

            if isNull info.LinkTarget && info.Exists && info.Length <= 65536L then
                Some(File.ReadAllText(path, Constants.Utf8NoBom).Trim())
            else
                None
        with _ ->
            None

    /// Liest den aktuellen Commit-Hash ohne Unterprozess direkt aus .git.
    /// Ist kein gueltiger Stand lesbar, wird bewusst None gemeldet.
    let readGitHead (root: string) : string option =
        try
            match tryReadSmallText root ".git/HEAD" with
            | None -> None
            | Some head ->
                if head.StartsWith("ref:", StringComparison.Ordinal) then
                    let refName = head.Substring(4).Trim()

                    if not (validRefName refName) then
                        None
                    else
                        match tryReadSmallText root $".git/{refName}" with
                        | Some loose when commitRegex.IsMatch(loose) -> Some loose
                        | _ ->
                            match tryReadSmallText root ".git/packed-refs" with
                            | None -> None
                            | Some packed ->
                                packed.Split('\n')
                                |> Seq.tryPick (fun line ->
                                    let trimmed = line.Trim()

                                    if
                                        trimmed.StartsWith("#", StringComparison.Ordinal)
                                        || trimmed.StartsWith("^", StringComparison.Ordinal)
                                    then
                                        None
                                    else
                                        match trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries) with
                                        | [| commit; reference |] when
                                            reference = refName && commitRegex.IsMatch(commit)
                                            ->
                                            Some commit
                                        | _ -> None)
                elif commitRegex.IsMatch(head) then
                    Some head
                else
                    None
        with _ ->
            None

    // ------------------------------------------------------------------
    // Run-Provenienz schreiben und strikt lesen
    // ------------------------------------------------------------------

    // Feldreihenfolge bewusst alphabetisch: Die kanonische Normalisierung des
    // Harness (Internal.canonicalElement/canonicalJson) sortiert Objektschluessel,
    // damit Hashes und Vergleiche zwischen Schreib- und Lesepfad byte-stabil sind.
    let private writeProvenanceBody (writer: Utf8JsonWriter) (provenance: RunProvenance) =
        writer.WriteStartObject()
        writer.WriteStartObject("complete")
        writer.WriteBoolean("config", provenance.Complete.Config)
        writer.WriteBoolean("git", provenance.Complete.Git)
        writer.WriteBoolean("model", provenance.Complete.Model)
        writer.WriteBoolean("prompt", provenance.Complete.Prompt)
        writer.WriteBoolean("task", provenance.Complete.Task)
        writer.WriteBoolean("toolchain", provenance.Complete.Toolchain)
        writer.WriteEndObject()
        writer.WriteString("configSha256", provenance.ConfigSha256)

        match provenance.GitCommit with
        | Some value -> writer.WriteString("gitCommit", value)
        | None -> writer.WriteNull("gitCommit")

        match provenance.ModelId with
        | Some value -> writer.WriteString("modelId", value)
        | None -> writer.WriteNull("modelId")

        match provenance.PromptSha256 with
        | Some value -> writer.WriteString("promptSha256", value)
        | None -> writer.WriteNull("promptSha256")

        writer.WriteNumber("schemaVersion", Constants.SchemaVersion)

        match provenance.TaskId with
        | Some value -> writer.WriteString("taskId", value)
        | None -> writer.WriteNull("taskId")

        match provenance.ToolchainSha256 with
        | Some value -> writer.WriteString("toolchainSha256", value)
        | None -> writer.WriteNull("toolchainSha256")

        writer.WriteEndObject()

    /// Schreibt den 'provenance'-Block in ein laufendes JSON-Objekt.
    let writeProvenance (writer: Utf8JsonWriter) (provenance: RunProvenance) =
        writer.WritePropertyName("provenance")
        writeProvenanceBody writer provenance

    /// Kanonische Bytes des Provenienzobjekts (ohne Eigenschaftsnamen).
    let bytesOfProvenance (provenance: RunProvenance) =
        Internal.jsonBytes false (fun writer -> writeProvenanceBody writer provenance)

    let private optionalStringField (maxLen: int) (field: string) description (element: JsonElement) =
        match element.TryGetProperty(field) with
        | false, _ -> Internal.fail $"{description}: Feld '{field}' fehlt."
        | true, value when value.ValueKind = JsonValueKind.Null -> None
        | true, value when value.ValueKind = JsonValueKind.String ->
            let text = value.GetString()

            if String.IsNullOrWhiteSpace(text) || text <> text.Trim() || text.Length > maxLen then
                Internal.fail $"{description}.{field} ist ungueltig."

            Some text
        | true, _ -> Internal.fail $"{description}.{field} muss eine Zeichenfolge oder null sein."

    let parseProvenance description element : RunProvenance =
        let allowed =
            set
                [ "complete"
                  "configSha256"
                  "gitCommit"
                  "modelId"
                  "promptSha256"
                  "schemaVersion"
                  "taskId"
                  "toolchainSha256" ]

        validateFields description allowed allowed element

        if Internal.requiredInt "schemaVersion" element <> Constants.SchemaVersion then
            Internal.fail $"{description} hat eine nicht unterstuetzte Schema-Version."

        let configSha256 = Internal.requiredString "configSha256" element

        if not (Internal.isSha256 configSha256) then
            Internal.fail $"{description}.configSha256 ist ungueltig."

        let gitCommit =
            match element.TryGetProperty("gitCommit") with
            | false, _ -> Internal.fail $"{description}: Feld 'gitCommit' fehlt."
            | true, value when value.ValueKind = JsonValueKind.Null -> None
            | true, value when value.ValueKind = JsonValueKind.String ->
                let text = value.GetString()

                if not (commitRegex.IsMatch(text)) then
                    Internal.fail $"{description}.gitCommit ist kein gueltiger Commit-Hash."

                Some text
            | true, _ -> Internal.fail $"{description}.gitCommit muss eine Zeichenfolge oder null sein."

        let modelId = optionalStringField 200 "modelId" description element
        let taskId = optionalStringField 64 "taskId" description element

        match taskId with
        | Some value when not (taskIdRegex.IsMatch(value)) -> Internal.fail $"{description}.taskId ist ungueltig."
        | _ -> ()

        let optionalHash (field: string) =
            match element.TryGetProperty(field) with
            | false, _ -> Internal.fail $"{description}: Feld '{field}' fehlt."
            | true, value when value.ValueKind = JsonValueKind.Null -> None
            | true, value when value.ValueKind = JsonValueKind.String ->
                let text = value.GetString()

                if not (Internal.isSha256 text) then
                    Internal.fail $"{description}.{field} ist kein SHA-256-Hash."

                Some text
            | true, _ -> Internal.fail $"{description}.{field} muss eine Zeichenfolge oder null sein."

        let promptSha256 = optionalHash "promptSha256"
        let toolchainSha256 = optionalHash "toolchainSha256"

        let completeElement =
            match element.TryGetProperty("complete") with
            | true, value -> value
            | false, _ -> Internal.fail $"{description}: Feld 'complete' fehlt."

        let completeAllowed =
            set [ "config"; "git"; "model"; "prompt"; "task"; "toolchain" ]

        validateFields $"{description}.complete" completeAllowed completeAllowed completeElement

        let flag field =
            let property = Internal.requiredProperty field completeElement

            if
                property.ValueKind <> JsonValueKind.True
                && property.ValueKind <> JsonValueKind.False
            then
                Internal.fail $"{description}.complete.{field} muss ein Boolean sein."

            property.GetBoolean()

        let complete =
            { Config = flag "config"
              Git = flag "git"
              Model = flag "model"
              Prompt = flag "prompt"
              Task = flag "task"
              Toolchain = flag "toolchain" }

        // Die Vollstaendigkeitskennzeichnung muss exakt zu den Feldern passen.
        if not complete.Config then
            Internal.fail $"{description}.complete.config muss wahr sein."

        if
            complete.Task <> taskId.IsSome
            || complete.Model <> modelId.IsSome
            || complete.Prompt <> promptSha256.IsSome
            || complete.Toolchain <> toolchainSha256.IsSome
            || complete.Git <> gitCommit.IsSome
        then
            Internal.fail $"{description}: Vollstaendigkeitskennzeichnung widerspricht den Provenienzfeldern."

        { SchemaVersion = Constants.SchemaVersion
          ConfigSha256 = configSha256
          GitCommit = gitCommit
          ModelId = modelId
          PromptSha256 = promptSha256
          TaskId = taskId
          ToolchainSha256 = toolchainSha256
          Complete = complete }

    // ------------------------------------------------------------------
    // Provenienz aus Startoptionen berechnen
    // ------------------------------------------------------------------

    type StartInputs =
        { ActorId: string
          TaskId: string option
          ModelId: string option
          PromptFile: string option
          ToolchainFile: string option }

    let buildProvenance (locations: WorkspacePaths) (maxFileBytes: int64) (inputs: StartInputs) =
        match inputs.ModelId with
        | Some value when
            String.IsNullOrWhiteSpace(value)
            || value <> value.Trim()
            || value.Length > 200
            || value |> Seq.exists Char.IsControl
            ->
            Internal.fail "Modellkennung muss eine nichtleere normalisierte Zeichenfolge ohne Steuerzeichen sein."
        | _ -> ()

        match inputs.TaskId with
        | Some value when not (taskIdRegex.IsMatch(value)) ->
            Internal.fail $"Aufgaben-ID '{value}' folgt nicht dem Muster T-###."
        | _ -> ()

        let resolveInsideWorkspace description (path: string) =
            let candidate =
                if Path.IsPathRooted(path) then
                    path
                else
                    Path.Combine(locations.Root, path)

            Workspace.requireSafePath locations description false candidate

        let hashWorkspaceFile description (path: string) =
            let safePath = resolveInsideWorkspace description path

            Internal.safeReadAllText safePath maxFileBytes |> Internal.sha256Text

        let promptSha256 =
            inputs.PromptFile |> Option.map (hashWorkspaceFile "Prompt-Datei")

        let toolchainSha256 =
            match inputs.ToolchainFile with
            | Some path -> hashWorkspaceFile "Toolchain-Lockdatei" path |> Some
            | None ->
                let defaultPath = Path.Combine(locations.Root, "toolchain.lock.json")

                if File.Exists(defaultPath) then
                    hashWorkspaceFile "Toolchain-Lockdatei" defaultPath |> Some
                else
                    None

        let gitCommit = readGitHead locations.Root

        { SchemaVersion = Constants.SchemaVersion
          ConfigSha256 = Internal.sha256File locations.Config
          GitCommit = gitCommit
          ModelId = inputs.ModelId
          PromptSha256 = promptSha256
          TaskId = inputs.TaskId
          ToolchainSha256 = toolchainSha256
          Complete =
            { Config = true
              Git = gitCommit.IsSome
              Model = inputs.ModelId.IsSome
              Prompt = promptSha256.IsSome
              Task = inputs.TaskId.IsSome
              Toolchain = toolchainSha256.IsSome } }

    // ------------------------------------------------------------------
    // Trace-/Span-/Kriteriums-Huellen in Event-Payloads
    // ------------------------------------------------------------------

    /// Prueft und liest die Span-Huelle eines Event-Payloads.
    /// Retrieval-, Tool- und Evidenzereignisse benoetigen eine vollstaendige Huelle;
    /// criterionId ist nur bei diesen drei Typen zulaessig.
    let extractSpan description eventType (payload: JsonElement) =
        let criterionAllowed = requiredSpanTypes.Contains(eventType)

        match payload.TryGetProperty("criterionId") with
        | true, _ when not criterionAllowed ->
            Internal.fail $"{description}: criterionId ist nur bei Retrieval-, Tool- und Evidenzereignissen zulaessig."
        | _ -> ()

        let hasTrace = fst (payload.TryGetProperty("traceId"))
        let hasSpan = fst (payload.TryGetProperty("spanId"))

        if hasTrace <> hasSpan then
            Internal.fail $"{description}: traceId und spanId muessen gemeinsam gesetzt sein."

        if not hasTrace then
            if criterionAllowed then
                Internal.fail
                    $"{description}: Ereignistyp '{eventType}' benoetigt die Huelle aus traceId, spanId und criterionId."

            None
        else
            let traceId = Internal.requiredString "traceId" payload

            if not (isTraceId traceId) then
                Internal.fail $"{description}.traceId ist ungueltig."

            let spanId = Internal.requiredString "spanId" payload

            if not (isSpanId spanId) then
                Internal.fail $"{description}.spanId ist ungueltig."

            let parentSpanId =
                match payload.TryGetProperty("parentSpanId") with
                | false, _ -> None
                | true, value when value.ValueKind = JsonValueKind.Null -> None
                | true, value when value.ValueKind = JsonValueKind.String ->
                    let text = value.GetString()

                    if not (isSpanId text) then
                        Internal.fail $"{description}.parentSpanId ist ungueltig."

                    Some text
                | true, _ -> Internal.fail $"{description}.parentSpanId muss eine Zeichenfolge oder null sein."

            let criterionId =
                match payload.TryGetProperty("criterionId") with
                | true, value when value.ValueKind = JsonValueKind.String -> value.GetString()
                | true, _ -> Internal.fail $"{description}.criterionId muss eine Zeichenfolge sein."
                | false, _ -> Internal.fail $"{description}: criterionId fehlt in der Span-Huelle."

            if not (criterionRegex.IsMatch(criterionId)) then
                Internal.fail $"{description}.criterionId ist ungueltig."

            Some
                { TraceId = traceId
                  SpanId = spanId
                  ParentSpanId = parentSpanId
                  CriterionId = criterionId }

    // ------------------------------------------------------------------
    // Evidenzvertrag fuer 'evidence.recorded'
    // ------------------------------------------------------------------

    let validateArtifactPath locations (path: string) =
        if String.IsNullOrWhiteSpace(path) || path.Length > 512 then
            Internal.fail $"Artefaktpfad ist ungueltig: {path}"

        if path.Contains('\\') || Path.IsPathRooted(path) then
            Internal.fail $"Artefaktpfad muss relativ mit '/'-Trennern sein: {path}"

        let segments = path.Split('/')

        if
            segments
            |> Array.exists (fun segment -> segment.Length = 0 || segment = "." || segment = "..")
        then
            Internal.fail $"Artefaktpfad enthaelt leere oder aufsteigende Segmente: {path}"

        let absolute = Path.GetFullPath(Path.Combine(locations.Root, path))

        if not (Workspace.isInside locations absolute) then
            Internal.fail $"Artefaktpfad liegt ausserhalb des Workspace: {path}"

        path

    /// Prueft den Evidenzvertrag und sammelt saemtliche Verletzungen.
    let validateEvidencePayload locations (payload: JsonElement) =
        let errors = ResizeArray<string>()

        try
            let allowed =
                set
                    [ "artifacts"
                      "command"
                      "criterionId"
                      "durationMs"
                      "exitCode"
                      "kind"
                      "parentSpanId"
                      "result"
                      "resultSha256"
                      "spanId"
                      "traceId" ]

            validateFields "Evidenz-Payload" allowed (set [ "artifacts"; "kind"; "result"; "resultSha256" ]) payload

            let kind = Internal.requiredString "kind" payload

            if not (evidenceKinds.Contains(kind)) then
                errors.Add($"Evidenzart '{kind}' ist ungueltig.")

            let resultSha256 = Internal.requiredString "resultSha256" payload

            if not (Internal.isSha256 resultSha256) then
                errors.Add("Evidenz-resultSha256 ist kein SHA-256-Hash.")

            match payload.TryGetProperty("result") with
            | true, value when value.ValueKind = JsonValueKind.Object -> ()
            | true, _ -> errors.Add("Evidenz-result muss ein JSON-Objekt sein.")
            | false, _ -> errors.Add("Evidenz-result fehlt.")

            match payload.TryGetProperty("command") with
            | false, _ -> ()
            | true, value when value.ValueKind = JsonValueKind.Null -> ()
            | true, value when value.ValueKind = JsonValueKind.String ->
                let command = value.GetString()

                if String.IsNullOrEmpty(command) || command.Length > 512 then
                    errors.Add("Evidenz-command muss 1 bis 512 Zeichen lang sein.")
            | true, _ -> errors.Add("Evidenz-command muss eine Zeichenfolge oder null sein.")

            match payload.TryGetProperty("exitCode") with
            | false, _ -> ()
            | true, value when value.ValueKind = JsonValueKind.Null -> ()
            | true, value ->
                match value.TryGetInt64() with
                | true, code when code >= 0L && code <= 4096L -> ()
                | _ -> errors.Add("Evidenz-exitCode ist ausserhalb des erlaubten Bereichs.")

            match payload.TryGetProperty("durationMs") with
            | false, _ -> ()
            | true, value when value.ValueKind = JsonValueKind.Null -> ()
            | true, value ->
                match value.TryGetInt64() with
                | true, duration when duration >= 0L -> ()
                | _ -> errors.Add("Evidenz-durationMs darf nicht negativ sein.")

            match payload.TryGetProperty("artifacts") with
            | true, value when value.ValueKind = JsonValueKind.Array ->
                let mutable index = 0

                for artifact in value.EnumerateArray() do
                    if artifact.ValueKind = JsonValueKind.Object then
                        try
                            let artifactFields = set [ "path"; "sha256" ]
                            validateFields $"Evidenz-Artefakt {index}" artifactFields artifactFields artifact

                            Internal.requiredString "path" artifact
                            |> validateArtifactPath locations
                            |> ignore

                            let artifactHash = Internal.requiredString "sha256" artifact

                            if not (Internal.isSha256 artifactHash) then
                                errors.Add($"Evidenz-Artefakt {index}: sha256 ist ungueltig.")
                        with HarnessException message ->
                            errors.Add(message)
                    else
                        errors.Add($"Evidenz-Artefakt {index} muss ein JSON-Objekt sein.")

                    index <- index + 1
            | true, _ -> errors.Add("Evidenz-artifacts muss ein Array sein.")
            | false, _ -> errors.Add("Evidenz-artifacts fehlt.")
        with HarnessException message ->
            errors.Add(message)

        errors |> Seq.toList

    // ------------------------------------------------------------------
    // Kriteriumsaufloesung gegen die gebundene Aufgabe
    // ------------------------------------------------------------------

    /// Liest die Abnahmekriterien-IDs der gebundenen Aufgabendatei.
    let loadTaskCriterionIds locations (taskId: string) =
        if String.IsNullOrWhiteSpace(taskId) || not (taskIdRegex.IsMatch(taskId)) then
            Internal.fail $"Aufgaben-ID '{taskId}' ist ungueltig."

        let directory = Path.Combine(locations.State, "tasks")

        if not (Directory.Exists(directory)) then
            Internal.fail "Aufgabenverzeichnis .ai/tasks fehlt."

        let exact = Path.Combine(directory, taskId + ".json")

        let candidates =
            if File.Exists(exact) then
                [ exact ]
            else
                Directory.EnumerateFiles(directory, taskId + "-*.json")
                |> Seq.sortWith (fun left right -> StringComparer.Ordinal.Compare(left, right))
                |> Seq.toList

        match candidates with
        | [] -> Internal.fail $"Keine Aufgabendatei fuer '{taskId}' unter .ai/tasks gefunden."
        | first :: _ ->
            let taskPath = Workspace.requireSafePath locations "Aufgabendatei" false first

            use document =
                JsonDocument.Parse(Internal.safeReadAllText taskPath Constants.MaxPayloadBytes)

            let root = document.RootElement

            let criteria =
                match root.TryGetProperty("acceptanceCriteria") with
                | true, value when value.ValueKind = JsonValueKind.Array -> value
                | true, _ -> Internal.fail $"Aufgabe '{taskId}': acceptanceCriteria muss ein Array sein."
                | false, _ -> Internal.fail $"Aufgabe '{taskId}': acceptanceCriteria fehlt."

            criteria.EnumerateArray()
            |> Seq.map (fun item ->
                if item.ValueKind <> JsonValueKind.Object then
                    Internal.fail $"Aufgabe '{taskId}': Kriterium muss ein Objekt sein."

                let id = Internal.requiredString "id" item

                if String.IsNullOrWhiteSpace(id) || id.Length > 64 then
                    Internal.fail $"Aufgabe '{taskId}': Kriterium-ID ist ungueltig."

                id)
            |> Seq.toList

    /// Prueft, dass ein Kriterium zu genau der gebundenen Aufgabe gehoert.
    let checkCriterion locations taskIdOption criterionId =
        match taskIdOption with
        | None -> Internal.fail "Ereignis mit criterionId referenziert einen Lauf ohne gebundene Aufgabe."
        | Some taskId ->
            let known = loadTaskCriterionIds locations taskId

            if not (List.contains criterionId known) then
                Internal.fail $"Kriterium '{criterionId}' gehoert nicht zur Aufgabe '{taskId}'."
