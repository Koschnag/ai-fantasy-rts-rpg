namespace RiftHarness

open System
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Runtime.InteropServices
open System.Text.Json
open global.Json.Schema
open Microsoft.Win32.SafeHandles

type ResearchActivationMarker =
    { CanonicalBytes: byte array
      ObservationId: string
      EvidenceClass: string
      TargetTaskId: string
      StudyManifestPath: string
      StudyManifestSha256: string
      ProtocolBundleSha256: string
      LedgerPath: string
      ActivationGuardSha256: string
      ActivationEventHash: string
      ActivatedAtUtc: string }

type ResearchBeginReceipt =
    { ObservationId: string
      Idempotent: bool
      MarkerSha256: string
      LedgerSha256: string
      ActivationEventHash: string
      ProtocolBundleSha256: string
      HeadCommit: string
      HeadTreeId: string }

type ResearchStatus =
    { Active: bool
      State: string
      ObservationId: string
      EvidenceClass: string
      TargetTaskId: string
      LedgerStatus: string
      EventCount: int
      OpenRunCount: int
      OpenInterventionCount: int
      CollectorGapCount: int
      LastEventHash: string
      Issues: string list }

type ResearchCloseReceipt =
    { ObservationId: string
      Idempotent: bool
      EventCount: int
      FinalEventHash: string
      LedgerSha256: string
      MarkerRemoved: bool }

[<RequireQualifiedAccess>]
type ResearchCrashPoint =
    | BeforeMarkerRename
    | AfterMarkerRenameBeforeDirectorySync
    | AfterOutcomeSyncBeforeClose
    | AfterCloseSyncBeforeMarkerUnlink
    | AfterMarkerUnlinkBeforeDirectorySync

[<RequireQualifiedAccess>]
module ResearchDurability =
    [<Literal>]
    let private O_RDONLY = 0

    [<Literal>]
    let private O_DIRECTORY = 0x10000

    [<Literal>]
    let private O_NOFOLLOW = 0x20000

    [<Literal>]
    let private O_CLOEXEC = 0x80000

    [<DllImport("libc", SetLastError = true, EntryPoint = "open")>]
    extern int private openNative(string path, int flags)

    [<DllImport("libc", SetLastError = true, EntryPoint = "fsync")>]
    extern int private fsyncNative(int descriptor)

    let private requireLinux () =
        if not (OperatingSystem.IsLinux()) then
            Internal.fail "DURABILITY_UNSUPPORTED: research activation requires Linux directory-fsync and O_NOFOLLOW."

    let private nativeError operation =
        let code = Marshal.GetLastPInvokeError()
        Internal.fail $"DURABILITY_FAILED: {operation} failed with errno {code}."

    let fsyncDirectory path =
        requireLinux ()
        let descriptor = openNative(path, O_RDONLY ||| O_DIRECTORY ||| O_CLOEXEC ||| O_NOFOLLOW)

        if descriptor < 0 then
            nativeError "open directory"

        use handle = new SafeFileHandle(nativeint descriptor, true)

        if fsyncNative(descriptor) <> 0 then
            nativeError "fsync directory"

    let readNoFollow path =
        requireLinux ()
        let descriptor = openNative(path, O_RDONLY ||| O_CLOEXEC ||| O_NOFOLLOW)

        if descriptor < 0 then
            nativeError "open marker"

        use handle = new SafeFileHandle(nativeint descriptor, true)
        use stream = new FileStream(handle, FileAccess.Read)

        if stream.Length > Constants.MaxPayloadBytes then
            Internal.fail "RESEARCH_MARKER_INVALID: active marker exceeds the size limit."

        use buffer = new MemoryStream()
        stream.CopyTo(buffer)
        buffer.ToArray()

[<RequireQualifiedAccess>]
module ResearchActivation =
    let private protocolPaths =
        [ ".ai/tasks/T-053-research-observability.json"
          "docs/research/METRICS.md"
          "docs/research/OBSERVABILITY_DATA_DICTIONARY.md"
          "docs/research/PRIVACY_AND_PUBLICATION.md"
          "docs/research/PROTOCOL.md"
          "docs/research/PROTOCOL_CHANGELOG.md"
          "docs/research/REPRODUCIBILITY.md"
          "docs/research/THREATS_TO_VALIDITY.md" ]

    let private markerFields =
        set
            [ "schemaVersion"; "studyId"; "observationId"; "evidenceClass"; "targetTaskId"
              "studyManifestPath"; "studyManifestSha256"; "protocolBundleSha256"; "ledgerPath"
              "activationGuardSha256"; "activationEventHash"; "activatedAtUtc"; "state" ]

    let private markerPath root = Path.Combine(Path.GetFullPath(root), ".ai", "runtime", "research", "active.json")

    let private activationLockPath root =
        Path.Combine(Path.GetFullPath(root), ".ai", "runtime", "research", ".activation.lock")

    let private withActivationLock root action =
        let locations = Workspace.requireInitialized root
        let path = Workspace.requireSafePath locations "Research activation lock" true (activationLockPath root)
        Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore

        // Only an IOException while acquiring the exclusive lock denotes a
        // competing writer.  IO failures from the protected operation must
        // retain their original classification and must never be relabelled
        // as concurrency.
        let stream =
            try
                new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, 1, FileOptions.WriteThrough)
            with :? IOException as error ->
                Internal.fail $"CONCURRENT_WRITER: {error.Message}"

        try
            action ()
        finally
            stream.Dispose()

    let private exactFields errorCode (description: string) (expected: Set<string>) (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then
            Internal.fail $"{errorCode}: {description} must be an object."

        let names = HashSet<string>(StringComparer.Ordinal)

        for property in element.EnumerateObject() do
            if not (names.Add(property.Name)) then
                Internal.fail $"{errorCode}: {description} contains duplicate field '{property.Name}'."

        if Set.ofSeq names <> expected then
            Internal.fail $"{errorCode}: {description} fields differ."

    let private requiredString (name: string) (element: JsonElement) : string =
        match element.TryGetProperty(name) with
        | true, value when value.ValueKind = JsonValueKind.String -> value.GetString()
        | _ -> Internal.fail $"RESEARCH_MARKER_INVALID: {name} must be a string."
    let private canonicalMarkerBytes
        (manifest: ResearchStudyManifest)
        (studyManifestPath: string)
        (ledgerPath: string)
        (activationGuardSha256: string)
        (activationEventHash: string)
        (activatedAtUtc: string)
        =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteString("activatedAtUtc", activatedAtUtc)
            writer.WriteString("activationEventHash", activationEventHash)
            writer.WriteString("activationGuardSha256", activationGuardSha256)
            writer.WriteString("evidenceClass", manifest.EvidenceClass)
            writer.WriteString("ledgerPath", ledgerPath)
            writer.WriteString("observationId", manifest.ObservationId)
            writer.WriteNumber("schemaVersion", ResearchContract.SchemaVersion)
            writer.WriteString("state", "active")
            writer.WriteString("studyId", manifest.StudyId)
            writer.WriteString("studyManifestPath", studyManifestPath)
            writer.WriteString("studyManifestSha256", manifest.ManifestSha256)
            writer.WriteString("protocolBundleSha256", manifest.ProtocolBundleSha256)
            writer.WriteString("targetTaskId", manifest.TargetTaskId)
            writer.WriteEndObject())
        |> Constants.Utf8NoBom.GetString
        |> ResearchCanonical.canonicalizeJson

    let private parseMarker bytes =
        use document = JsonDocument.Parse(bytes: byte array)
        let root = document.RootElement
        exactFields "RESEARCH_MARKER_INVALID" "active marker" markerFields root
        let canonical = ResearchCanonical.canonicalizeElement root

        if canonical.Length <> bytes.Length || not (Array.forall2 (=) canonical bytes) then
            Internal.fail "RESEARCH_MARKER_INVALID: active marker is not canonical."

        let schemaVersion = root.GetProperty("schemaVersion")

        if schemaVersion.ValueKind <> JsonValueKind.Number || schemaVersion.GetInt32() <> ResearchContract.SchemaVersion then
            Internal.fail "RESEARCH_MARKER_INVALID: schemaVersion differs."

        if requiredString "studyId" root <> ResearchContract.StudyId || requiredString "state" root <> "active" then
            Internal.fail "RESEARCH_MARKER_INVALID: study or state differs."

        let hash name =
            let value = requiredString name root
            if not (Internal.isSha256 value) then Internal.fail $"RESEARCH_MARKER_INVALID: {name} is not SHA-256."
            value

        { CanonicalBytes = canonical
          ObservationId = requiredString "observationId" root
          EvidenceClass = requiredString "evidenceClass" root
          TargetTaskId = requiredString "targetTaskId" root
          StudyManifestPath = requiredString "studyManifestPath" root
          StudyManifestSha256 = hash "studyManifestSha256"
          ProtocolBundleSha256 = hash "protocolBundleSha256"
          LedgerPath = requiredString "ledgerPath" root
          ActivationGuardSha256 = hash "activationGuardSha256"
          ActivationEventHash = hash "activationEventHash"
          ActivatedAtUtc = requiredString "activatedAtUtc" root }

    let private readMarker root =
        let locations = Workspace.requireInitialized root
        let path = Workspace.requireSafePath locations "Research active marker" true (markerPath root)

        if File.Exists(path) then
            Some(parseMarker (ResearchDurability.readNoFollow path))
        else
            None

    let private protocolBundle root headCommit =
        ResearchGitImport.requirePathsClean root protocolPaths

        let rows =
            protocolPaths
            |> List.sortWith (fun (left: string) (right: string) -> StringComparer.Ordinal.Compare(left, right))
            |> List.map (fun path ->
                let bytes = ResearchGitImport.fileAtCommit root headCommit path
                path, bytes, Internal.sha256Hex bytes)

        let manifestBytes =
            rows
            |> List.map (fun (path, _, sha256) -> $"{sha256}  {path}\n")
            |> String.concat ""
            |> Constants.Utf8NoBom.GetBytes

        rows, manifestBytes, Internal.sha256Hex manifestBytes

    let private taskPath root taskId =
        let directory = Path.Combine(Path.GetFullPath(root), ".ai", "tasks")

        let candidates =
            Directory.EnumerateFiles(directory, taskId + "*.json", SearchOption.TopDirectoryOnly)
            |> Seq.filter (fun path ->
                let name = Path.GetFileNameWithoutExtension(path)
                name = taskId || name.StartsWith(taskId + "-", StringComparison.Ordinal))
            |> Seq.sortWith (fun (left: string) (right: string) -> StringComparer.Ordinal.Compare(left, right))
            |> Seq.toList

        match candidates with
        | [ path ] -> Path.GetRelativePath(root, path).Replace('\\', '/')
        | [] -> Internal.fail $"TARGET_TASK_NOT_FOUND: no manifest exists for {taskId}."
        | _ -> Internal.fail $"TARGET_TASK_AMBIGUOUS: multiple manifests exist for {taskId}."

    let private validateTaskSchema root headCommit (manifest: ResearchStudyManifest) =
        let path = taskPath root manifest.TargetTaskId
        ResearchGitImport.requirePathsClean root [ path ]
        let taskBytes = ResearchGitImport.fileAtCommit root headCommit path

        if Internal.sha256Hex taskBytes <> manifest.TaskManifestSha256 then
            Internal.fail "TARGET_TASK_DRIFT: taskManifestSha256 differs from the bound commit."

        let schemaBytes = ResearchGitImport.fileAtCommit root headCommit ".ai/schemas/task.schema.json"
        let schema = JsonSchema.FromText(Constants.Utf8NoBom.GetString(schemaBytes))
        use taskDocument = JsonDocument.Parse(taskBytes)
        let evaluated =
            schema.Evaluate(
                taskDocument.RootElement,
                EvaluationOptions(OutputFormat = OutputFormat.List, RequireFormatValidation = true)
            )

        if not evaluated.IsValid then
            Internal.fail "TARGET_TASK_SCHEMA_INVALID: task manifest does not satisfy task.schema.json."

        let taskRoot = taskDocument.RootElement

        if requiredString "id" taskRoot <> manifest.TargetTaskId then
            Internal.fail "TARGET_TASK_INVALID: task ID differs from study manifest."

        if requiredString "status" taskRoot <> "ready" then
            Internal.fail "TARGET_TASK_NOT_READY: target task is not ready."

        match taskRoot.TryGetProperty("dependencies") with
        | true, dependencies when dependencies.ValueKind = JsonValueKind.Array ->
            for dependency in dependencies.EnumerateArray() do
                if dependency.ValueKind <> JsonValueKind.String then
                    Internal.fail "TARGET_TASK_SCHEMA_INVALID: dependency must be a task ID string."

                let dependencyId = dependency.GetString()
                let dependencyPath = taskPath root dependencyId
                let dependencyBytes = ResearchGitImport.fileAtCommit root headCommit dependencyPath
                use dependencyDocument = JsonDocument.Parse(dependencyBytes)

                if requiredString "id" dependencyDocument.RootElement <> dependencyId
                   || requiredString "status" dependencyDocument.RootElement <> "accepted" then
                    Internal.fail $"TARGET_TASK_NOT_READY: dependency {dependencyId} is not accepted."
        | true, _ -> Internal.fail "TARGET_TASK_SCHEMA_INVALID: dependencies must be an array."
        | false, _ -> ()

        path, taskBytes

    let private targetRunExists root targetTaskId =
        let runs = (Workspace.paths root).Runs

        if not (Directory.Exists(runs)) then
            false
        else
            Directory.EnumerateFiles(runs, "run.json", SearchOption.AllDirectories)
            |> Seq.exists (fun path ->
                try
                    use document = JsonDocument.Parse(File.ReadAllBytes(path))

                    match document.RootElement.TryGetProperty("provenance") with
                    | true, provenance ->
                        match provenance.TryGetProperty("taskId") with
                        | true, value when value.ValueKind = JsonValueKind.String -> value.GetString() = targetTaskId
                        | _ -> false
                    | _ -> false
                with _ ->
                    // An unreadable primary run source is not evidence of absence.
                    Internal.fail "PROSPECTIVE_START_UNCERTAIN: an existing run manifest cannot be verified.")

    let private validateSourceInventory root (manifest: ResearchStudyManifest) =
        if manifest.SourceInventory.ValueKind <> JsonValueKind.Array then
            Internal.fail "RESEARCH_MANIFEST_INVALID: sourceInventory must be an array."

        let paths = HashSet<string>(StringComparer.Ordinal)

        for item in manifest.SourceInventory.EnumerateArray() do
            if item.ValueKind <> JsonValueKind.Object then
                Internal.fail "RESEARCH_MANIFEST_INVALID: sourceInventory entries must be objects."

            exactFields
                "RESEARCH_MANIFEST_INVALID"
                "sourceInventory entry"
                (set [ "bytes"; "path"; "sha256" ])
                item

            let path = requiredString "path" item
            let sha256 = requiredString "sha256" item
            let declaredBytes = item.GetProperty("bytes")

            if not (paths.Add(path)) || not (Internal.isSha256 sha256) then
                Internal.fail "RESEARCH_MANIFEST_INVALID: sourceInventory path/hash is invalid or duplicate."

            let locations = Workspace.requireInitialized root
            let absolute = Workspace.requireSafePath locations "Research source inventory member" false (Path.Combine(root, path))
            let info = FileInfo(absolute)
            let mutable byteCount = 0L

            if declaredBytes.ValueKind <> JsonValueKind.Number || not (declaredBytes.TryGetInt64(&byteCount)) || byteCount < 0L then
                Internal.fail "RESEARCH_MANIFEST_INVALID: sourceInventory bytes must be non-negative."

            if info.Length <> byteCount || Internal.sha256File absolute <> sha256 then
                Internal.fail $"NON_INTERFERENCE_DRIFT: source inventory member differs: {path}."

    let private guardBytes (manifest: ResearchStudyManifest) (identity: ResearchGitIdentity) (taskPath: string) =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteString("baselineCommit", manifest.BaselineCommit)
            writer.WriteString("baselineTreeId", manifest.BaselineTreeId)
            writer.WriteString("headCommit", identity.HeadCommit)
            writer.WriteString("headTreeId", identity.HeadTreeId)
            writer.WriteString("protocolBundleSha256", manifest.ProtocolBundleSha256)
            writer.WriteString("sourceInventorySha256", manifest.SourceManifestSha256)
            writer.WriteString("studyManifestSha256", manifest.ManifestSha256)
            writer.WriteString("targetTaskId", manifest.TargetTaskId)
            writer.WriteString("taskManifestPath", taskPath)
            writer.WriteString("taskManifestSha256", manifest.TaskManifestSha256)
            writer.WriteEndObject())
        |> Constants.Utf8NoBom.GetString
        |> ResearchCanonical.canonicalizeJson

    let private writeMarker (root: string) (bytes: byte array) crashPoint =
        let locations = Workspace.requireInitialized root
        let path = Workspace.requireSafePath locations "Research active marker" true (markerPath root)
        let parent = Path.GetDirectoryName(path)
        Directory.CreateDirectory(parent) |> ignore
        let temporary = Path.Combine(parent, ".active." + Guid.NewGuid().ToString("N") + ".tmp")
        use stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, bytes.Length, FileOptions.WriteThrough)
        stream.Write(bytes, 0, bytes.Length)
        stream.Flush(true)
        stream.Close()

        if crashPoint = Some ResearchCrashPoint.BeforeMarkerRename then
            Internal.fail "INJECTED_CRASH: before marker rename."

        File.Move(temporary, path, false)

        if crashPoint = Some ResearchCrashPoint.AfterMarkerRenameBeforeDirectorySync then
            Internal.fail "INJECTED_CRASH: after marker rename before directory fsync."

        ResearchDurability.fsyncDirectory parent
        let reopened = ResearchDurability.readNoFollow path

        if reopened.Length <> bytes.Length || not (Array.forall2 (=) reopened bytes) then
            Internal.fail "RESEARCH_MARKER_INVALID: no-follow reopen differs after durable activation."

    let private verifyStartPrefix (marker: ResearchActivationMarker) (events: ResearchEvent list) =
        if events.Length < 2
           || events[0].Body.EventType <> "protocol.frozen"
           || events[1].Body.EventType <> "observation.started"
           || events[1].EventHash <> marker.ActivationEventHash then
            Internal.fail "RESEARCH_MARKER_INVALID: marker does not bind the required start chain."

    let private verifyActiveChain (marker: ResearchActivationMarker) (events: ResearchEvent list) =
        verifyStartPrefix marker events

        if events |> List.exists (fun event -> event.Body.EventType = "observation.closed") then
            Internal.fail "STALE_ACTIVE_MARKER: observation is already closed."

    let private beginLocked (root: string) (studyManifestPath: string) crashPoint =
        let manifest = ResearchExport.loadStudyManifest root studyManifestPath

        if manifest.EvidenceClass <> "prospective-observed" then
            Internal.fail "RESEARCH_BEGIN_INVALID: begin is reserved for prospective-observed studies."

        if manifest.ProtocolVersion <> "2.0.0" then
            Internal.fail "PROTOCOL_VERSION_INVALID: prospective P-001 requires protocol 2.0.0."

        if manifest.TargetTaskId <> "T-042" then
            Internal.fail "TARGET_TASK_INVALID: the preregistered first prospective target is T-042."

        let locations = Workspace.requireInitialized root
        let studyManifestAbsolute = Workspace.requireSafePath locations "Research study manifest" false studyManifestPath
        let studyManifestRelative = Workspace.relativePath locations studyManifestAbsolute
        let ledgerAbsolute = ResearchLedger.ledgerPath root manifest.ObservationId
        let ledgerRelative = Workspace.relativePath locations ledgerAbsolute

        match readMarker root with
        | Some marker ->
            if marker.ObservationId <> manifest.ObservationId || marker.StudyManifestSha256 <> manifest.ManifestSha256 then
                Internal.fail "ACTIVE_OBSERVATION_CONFLICT: another observation is active."

            let events = ResearchLedger.readVerified root ledgerAbsolute
            verifyActiveChain marker events
            ResearchDurability.fsyncDirectory(Path.GetDirectoryName(markerPath root))

            { ObservationId = manifest.ObservationId
              Idempotent = true
              MarkerSha256 = Internal.sha256Hex marker.CanonicalBytes
              LedgerSha256 = Internal.sha256File ledgerAbsolute
              ActivationEventHash = marker.ActivationEventHash
              ProtocolBundleSha256 = marker.ProtocolBundleSha256
              HeadCommit = manifest.HeadCommit
              HeadTreeId = manifest.InputTreeId }
        | None ->
            if File.Exists(ledgerAbsolute) && FileInfo(ledgerAbsolute).Length > 0L then
                Internal.fail "INCOMPLETE_ACTIVATION: a start chain exists without a durable active marker."

            ResearchGitImport.requireWorktreeClean root
            let identity = ResearchGitImport.currentIdentity root

            if manifest.CollectorVersion <> ResearchRuntime.CollectorVersion
               || manifest.ExporterVersion <> ResearchRuntime.ExporterVersion then
                Internal.fail "RESEARCH_VERSION_INVALID: study manifest does not bind this collector/exporter."

            if not (Internal.isSha256 manifest.SourceManifestSha256)
               || not (Internal.isSha256 manifest.ToolchainSha256) then
                Internal.fail "RESEARCH_MANIFEST_INVALID: prospective source inventory and toolchain must be hash-bound."

            if identity.HeadCommit <> manifest.HeadCommit || identity.HeadTreeId <> manifest.InputTreeId then
                Internal.fail "BASELINE_DRIFT: current HEAD/tree differs from the frozen study manifest."

            if ResearchGitImport.treeAt root manifest.BaselineCommit <> manifest.BaselineTreeId then
                Internal.fail "BASELINE_DRIFT: baseline tree differs from the frozen study manifest."

            let rows, bundleManifest, bundleHash = protocolBundle root identity.HeadCommit

            if bundleHash <> manifest.ProtocolBundleSha256 then
                Internal.fail "PROTOCOL_BUNDLE_INVALID: protocolBundleSha256 differs from committed inputs."

            let targetTaskPath, taskBytes = validateTaskSchema root identity.HeadCommit manifest
            validateSourceInventory root manifest

            if targetRunExists root manifest.TargetTaskId then
                Internal.fail "PROSPECTIVE_START_TOO_LATE: a target task run already exists."

            let activationGuard = guardBytes manifest identity targetTaskPath |> Internal.sha256Hex
            let protocolSources = rows |> List.map (fun (path, _, hash) -> ResearchRuntime.gitBlobSource root identity.HeadCommit path hash)
            let frozenAt = ResearchRuntime.nowText ()
            let frozenPayload =
                ResearchRuntime.payload (fun writer ->
                    writer.WriteString("freezeAtUtc", frozenAt)
                    writer.WriteString("protocolBundleSha256", bundleHash)
                    writer.WriteString("protocolId", ResearchContract.StudyId)
                    writer.WriteString("protocolVersion", manifest.ProtocolVersion))

            let frozenDraft = ResearchRuntime.createDraft manifest identity "protocol.frozen" protocolSources frozenPayload
            ResearchLedger.append root ledgerAbsolute frozenDraft |> ignore

            let startPayload =
                ResearchRuntime.payload (fun writer ->
                    writer.WriteString("activationGuardSha256", activationGuard)
                    writer.WriteString("baselineCommit", manifest.BaselineCommit)
                    writer.WriteString("collectorVersion", manifest.CollectorVersion)
                    writer.WriteString("nonInterferenceSnapshotSha256", manifest.SourceManifestSha256)
                    writer.WriteString("targetTaskId", manifest.TargetTaskId))

            let taskSource = ResearchRuntime.gitBlobSource root identity.HeadCommit targetTaskPath (Internal.sha256Hex taskBytes)
            let startDraft = ResearchRuntime.createDraft manifest identity "observation.started" [ taskSource ] startPayload
            let startReceipt = ResearchLedger.append root ledgerAbsolute startDraft
            let markerBytes =
                canonicalMarkerBytes
                    manifest
                    studyManifestRelative
                    ledgerRelative
                    activationGuard
                    startReceipt.EventHash
                    frozenAt

            // The manifest itself is runtime input. The committed protocol bundle
            // manifest is retained beside the observation for later verification.
            let observationDirectory = Path.GetDirectoryName(ledgerAbsolute)
            let bundlePath = Path.Combine(observationDirectory, "PROTOCOL-BUNDLE.SHA256")
            use bundleStream = new FileStream(bundlePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, bundleManifest.Length, FileOptions.WriteThrough)
            bundleStream.Write(bundleManifest, 0, bundleManifest.Length)
            bundleStream.Flush(true)
            bundleStream.Close()
            ResearchDurability.fsyncDirectory observationDirectory
            writeMarker root markerBytes crashPoint

            { ObservationId = manifest.ObservationId
              Idempotent = false
              MarkerSha256 = Internal.sha256Hex markerBytes
              LedgerSha256 = Internal.sha256File ledgerAbsolute
              ActivationEventHash = startReceipt.EventHash
              ProtocolBundleSha256 = bundleHash
              HeadCommit = identity.HeadCommit
              HeadTreeId = identity.HeadTreeId }

    let beginObservation root studyManifestPath =
        withActivationLock root (fun () -> beginLocked root studyManifestPath None)

    /// Public only so hermetic tests can model durable crash boundaries. The CLI
    /// never exposes crash injection.
    let beginWithCrashPoint root studyManifestPath crashPoint =
        withActivationLock root (fun () -> beginLocked root studyManifestPath (Some crashPoint))

    let tryActive root =
        match readMarker root with
        | None -> None
        | Some marker ->
            let ledger = Path.Combine(Path.GetFullPath(root), marker.LedgerPath)
            let events = ResearchLedger.readVerified root ledger

            if events |> List.exists (fun event -> event.Body.EventType = "observation.closed") then
                None
            else
                verifyActiveChain marker events
                Some marker

    let status root observationId =
        let marker = readMarker root

        let selectedObservation =
            match observationId, marker with
            | Some value, _ -> value
            | None, Some value -> value.ObservationId
            | None, None -> ResearchContract.Unknown

        if selectedObservation = ResearchContract.Unknown then
            { Active = false
              State = "inactive"
              ObservationId = ResearchContract.Unknown
              EvidenceClass = ResearchContract.Unknown
              TargetTaskId = ResearchContract.Unknown
              LedgerStatus = ResearchContract.Unknown
              EventCount = 0
              OpenRunCount = 0
              OpenInterventionCount = 0
              CollectorGapCount = 0
              LastEventHash = ResearchContract.Unknown
              Issues = [] }
        else
            let ledger = ResearchLedger.ledgerPath root selectedObservation

            if not (File.Exists(ledger)) then
                { Active = false
                  State = "invalid"
                  ObservationId = selectedObservation
                  EvidenceClass = marker |> Option.map (fun value -> value.EvidenceClass) |> Option.defaultValue ResearchContract.Unknown
                  TargetTaskId = marker |> Option.map (fun value -> value.TargetTaskId) |> Option.defaultValue ResearchContract.Unknown
                  LedgerStatus = "missing"
                  EventCount = 0
                  OpenRunCount = 0
                  OpenInterventionCount = 0
                  CollectorGapCount = 0
                  LastEventHash = ResearchContract.Unknown
                  Issues = [ "LEDGER_MISSING" ] }
            else
                let verified = ResearchLedger.verify root ledger
                let events = verified.Events
                let closed = events |> List.exists (fun event -> event.Body.EventType = "observation.closed")
                let starts = events |> List.filter (fun event -> event.Body.EventType = "agent.run.started") |> List.choose (fun event -> ResearchValue.toOption event.Body.RunId) |> Set.ofList
                let finishes = events |> List.filter (fun event -> event.Body.EventType = "agent.run.finished") |> List.choose (fun event -> ResearchValue.toOption event.Body.RunId) |> Set.ofList
                let interventionStarts =
                    events
                    |> List.filter (fun event -> event.Body.EventType = "research.intervention.started")
                    |> List.map (fun event -> event.Body.Payload.GetProperty("interventionId").GetString())
                    |> Set.ofList
                let interventionEnds =
                    events
                    |> List.filter (fun event -> event.Body.EventType = "research.intervention.ended")
                    |> List.map (fun event -> event.Body.Payload.GetProperty("interventionId").GetString())
                    |> Set.ofList
                let markerMatches = marker |> Option.exists (fun value -> value.ObservationId = selectedObservation)
                let gapDirectory = Path.Combine(Path.GetFullPath(root), ".ai", "runtime", "research", "gaps", selectedObservation)
                let gapCount =
                    if Directory.Exists(gapDirectory) then
                        Directory.EnumerateFiles(gapDirectory, "GAP-*.json", SearchOption.TopDirectoryOnly) |> Seq.length
                    else
                        0
                let issues =
                    [ if verified.Status <> ResearchLedgerStatus.Valid then yield! verified.Errors
                      if markerMatches && closed then yield "STALE_ACTIVE_MARKER"
                      if not markerMatches && not closed && not (List.isEmpty events) then yield "INCOMPLETE_ACTIVATION"
                      if gapCount > 0 then yield "COLLECTOR_GAPS_PRESENT" ]

                { Active = markerMatches && not closed && verified.Status = ResearchLedgerStatus.Valid
                  State = if closed then "closed" elif markerMatches then "active" else "incomplete"
                  ObservationId = selectedObservation
                  EvidenceClass = events |> List.tryHead |> Option.map (fun event -> event.Body.EvidenceClass) |> Option.defaultValue ResearchContract.Unknown
                  TargetTaskId = events |> List.tryHead |> Option.bind (fun event -> ResearchValue.toOption event.Body.TaskId) |> Option.defaultValue ResearchContract.Unknown
                  LedgerStatus = string verified.Status
                  EventCount = events.Length
                  OpenRunCount = Set.difference starts finishes |> Set.count
                  OpenInterventionCount = Set.difference interventionStarts interventionEnds |> Set.count
                  CollectorGapCount = gapCount
                  LastEventHash = events |> List.tryLast |> Option.map (fun event -> event.EventHash) |> Option.defaultValue ResearchContract.Unknown
                  Issues = issues }

    let private loadOutcomeReceipt (root: string) (marker: ResearchActivationMarker) (path: string) =
        let locations = Workspace.requireInitialized root
        let absolute = Workspace.requireSafePath locations "Research outcome receipt" false path
        let bytes = File.ReadAllBytes(absolute)
        let canonical = ResearchCanonical.canonicalizeJson(Constants.Utf8NoBom.GetString(bytes))

        if canonical.Length <> bytes.Length || not (Array.forall2 (=) canonical bytes) then
            Internal.fail "OUTCOME_RECEIPT_INVALID: outcome receipt must already be canonical JSON."

        use document = JsonDocument.Parse(canonical)
        let value = document.RootElement
        exactFields
            "OUTCOME_RECEIPT_INVALID"
            "outcome receipt"
            (set
                [ "observationId"; "targetTaskId"; "taskOutcome"; "hypothesisResult"
                  "resultCommit"; "resultTreeId"; "reasonCode"; "sourceManifestSha256" ])
            value
        let observationId = requiredString "observationId" value
        let targetTaskId = requiredString "targetTaskId" value
        let taskOutcome = requiredString "taskOutcome" value
        let hypothesisResult = requiredString "hypothesisResult" value
        let resultCommit = requiredString "resultCommit" value
        let resultTreeId = requiredString "resultTreeId" value
        let reasonCode = requiredString "reasonCode" value
        let sourceManifestSha256 = requiredString "sourceManifestSha256" value

        if observationId <> marker.ObservationId || targetTaskId <> marker.TargetTaskId then
            Internal.fail "OUTCOME_RECEIPT_INVALID: outcome target differs from active observation."

        if not (Set.contains taskOutcome (set [ "accepted"; "rejected"; "blocked"; "cancelled"; "unknown" ]))
           || not (Set.contains hypothesisResult (set [ "supports"; "contradicts"; "inconclusive" ])) then
            Internal.fail "OUTCOME_RECEIPT_INVALID: outcome enums are invalid."

        let isObjectId (candidate: string) =
            (candidate.Length = 40 || candidate.Length = 64)
            && candidate |> Seq.forall (fun character -> character >= '0' && character <= '9' || character >= 'a' && character <= 'f')

        if resultCommit <> ResearchContract.Unknown && not (isObjectId resultCommit) then
            Internal.fail "OUTCOME_RECEIPT_INVALID: resultCommit is invalid."

        if resultTreeId <> ResearchContract.Unknown && not (isObjectId resultTreeId) then
            Internal.fail "OUTCOME_RECEIPT_INVALID: resultTreeId is invalid."

        if not (Internal.isSha256 sourceManifestSha256) then
            Internal.fail "OUTCOME_RECEIPT_INVALID: sourceManifestSha256 is invalid."

        if
            String.IsNullOrWhiteSpace(reasonCode)
            || reasonCode.Length > 128
            || reasonCode
               |> Seq.exists (fun character ->
                   not (Char.IsAsciiLetterOrDigit(character))
                   && character <> '-'
                   && character <> '_'
                   && character <> '.')
        then
            Internal.fail "OUTCOME_RECEIPT_INVALID: reasonCode must be a bounded machine-readable code."

        canonical, taskOutcome, hypothesisResult, resultCommit, resultTreeId, reasonCode, sourceManifestSha256

    let private freezeOutcomeSource root observationId (bytes: byte array) =
        let locations = Workspace.requireInitialized root
        let hash = Internal.sha256Hex bytes
        let relative = $".ai/runtime/research/observations/{observationId}/sources/outcome-{hash}.json"
        let path = Workspace.requireSafePath locations "Research frozen outcome source" true (Path.Combine(root, relative))
        let parent = Path.GetDirectoryName(path)
        Directory.CreateDirectory(parent) |> ignore

        if File.Exists(path) then
            if Internal.sha256File path <> hash then
                Internal.fail "OUTCOME_SOURCE_CONFLICT: existing frozen source hash differs."
        else
            use stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None, max 1 bytes.Length, FileOptions.WriteThrough)
            stream.Write(bytes, 0, bytes.Length)
            stream.Flush(true)
            stream.Close()
            ResearchDurability.fsyncDirectory parent

        relative, hash

    let private removeMarkerDurably root crashPoint =
        let path = markerPath root
        let parent = Path.GetDirectoryName(path)
        File.Delete(path)

        if crashPoint = Some ResearchCrashPoint.AfterMarkerUnlinkBeforeDirectorySync then
            Internal.fail "INJECTED_CRASH: after marker unlink before directory fsync."

        ResearchDurability.fsyncDirectory parent

        if File.Exists(path) then
            Internal.fail "DURABILITY_FAILED: active marker remains after unlink and directory fsync."

    let private validateFinalClosure expectedSourceManifest (events: ResearchEvent list) =
        let outcomes = events |> List.filter (fun event -> event.Body.EventType = "outcome.observed")
        let closes = events |> List.filter (fun event -> event.Body.EventType = "observation.closed")

        match outcomes, closes with
        | [ outcome ], [ closed ] when List.last events = closed ->
            let closePayload = closed.Body.Payload
            let mutable eventCount = 0L

            match closePayload.TryGetProperty("eventCount") with
            | true, value when value.ValueKind = JsonValueKind.Number && value.TryGetInt64(&eventCount) -> ()
            | _ -> Internal.fail "RESEARCH_CLOSE_INVALID: closure eventCount is invalid."

            if eventCount <> int64 events.Length then
                Internal.fail "RESEARCH_CLOSE_INVALID: closure eventCount differs from the verified ledger."

            let closeString (name: string) : string =
                match closePayload.TryGetProperty(name) with
                | true, value when value.ValueKind = JsonValueKind.String -> value.GetString()
                | _ -> Internal.fail $"RESEARCH_CLOSE_INVALID: closure {name} is invalid."

            if closeString "outcomeEventId" <> outcome.Body.EventId then
                Internal.fail "RESEARCH_CLOSE_INVALID: closure does not bind the unique outcome event."

            match expectedSourceManifest with
            | Some expected when closeString "sourceManifestSha256" <> expected ->
                Internal.fail "RESEARCH_CLOSE_INVALID: closure source manifest differs."
            | _ -> ()

            closed
        | _ ->
            Internal.fail "RESEARCH_CLOSE_INVALID: exactly one outcome and one final closure are required."

    let private closeLocked root observationId outcomeReceipt crashPoint =
        let markerOption = readMarker root

        match markerOption with
        | None ->
            let ledger = ResearchLedger.ledgerPath root observationId

            if File.Exists(ledger) then
                let events = ResearchLedger.readVerified root ledger

                if events |> List.exists (fun event -> event.Body.EventType = "observation.closed") then
                    let finalEvent = validateFinalClosure None events

                    { ObservationId = observationId
                      Idempotent = true
                      EventCount = events.Length
                      FinalEventHash = finalEvent.EventHash
                      LedgerSha256 = Internal.sha256File ledger
                      MarkerRemoved = true }
                else
                    Internal.fail "ACTIVE_OBSERVATION_MISSING: no matching active marker exists."
            else
                Internal.fail "ACTIVE_OBSERVATION_MISSING: no matching active marker exists."
        | Some other when other.ObservationId <> observationId ->
            Internal.fail "ACTIVE_OBSERVATION_CONFLICT: marker belongs to another observation."
        | Some marker ->
            let manifestPath = Path.Combine(Path.GetFullPath(root), marker.StudyManifestPath)
            let manifest = ResearchExport.loadStudyManifest root manifestPath

            if manifest.ManifestSha256 <> marker.StudyManifestSha256 then
                Internal.fail "RESEARCH_MARKER_INVALID: study manifest changed after activation."

            let ledger = ResearchLedger.ledgerPath root observationId
            let existing = ResearchLedger.readVerified root ledger
            verifyStartPrefix marker existing
            let closed = existing |> List.filter (fun event -> event.Body.EventType = "observation.closed")

            match closed with
            | [ _ ] ->
                let closeEvent = validateFinalClosure (Some manifest.SourceManifestSha256) existing
                removeMarkerDurably root crashPoint

                { ObservationId = observationId
                  Idempotent = true
                  EventCount = existing.Length
                  FinalEventHash = closeEvent.EventHash
                  LedgerSha256 = Internal.sha256File ledger
                  MarkerRemoved = true }
            | [] ->
                let outcomeBytes, taskOutcome, hypothesisResult, resultCommit, resultTreeId, reasonCode, sourceManifestSha =
                    loadOutcomeReceipt root marker outcomeReceipt

                if sourceManifestSha <> manifest.SourceManifestSha256 then
                    Internal.fail "OUTCOME_RECEIPT_INVALID: sourceManifestSha256 differs from the frozen study manifest."

                // Re-read every frozen non-interference input immediately before
                // closing. Equality of the start/close manifest hash is only
                // meaningful when the current bytes still match each bound row.
                validateSourceInventory root manifest

                let identity = ResearchGitImport.currentIdentity root

                if (resultCommit <> ResearchContract.Unknown || resultTreeId <> ResearchContract.Unknown)
                   && not identity.WorktreeClean then
                    Internal.fail "OUTCOME_RECEIPT_INVALID: a known result commit/tree requires a clean worktree."

                if resultCommit <> ResearchContract.Unknown && resultCommit <> identity.HeadCommit then
                    Internal.fail "OUTCOME_RECEIPT_INVALID: resultCommit differs from the current immutable HEAD."

                if resultTreeId <> ResearchContract.Unknown && resultTreeId <> identity.HeadTreeId then
                    Internal.fail "OUTCOME_RECEIPT_INVALID: resultTreeId differs from the current immutable tree."

                let frozenOutcomeRelative, frozenOutcomeHash = freezeOutcomeSource root observationId outcomeBytes
                let source = ResearchRuntime.sourceFromFile root "decision-receipt" frozenOutcomeRelative

                if source.ArtifactSha256 <> frozenOutcomeHash then
                    Internal.fail "OUTCOME_SOURCE_CONFLICT: frozen source binding differs."
                let outcomePayload =
                    ResearchRuntime.payload (fun writer ->
                        writer.WriteString("hypothesisResult", hypothesisResult)
                        writer.WriteString("reasonCode", reasonCode)
                        writer.WriteString("resultCommit", resultCommit)
                        writer.WriteString("resultTreeId", resultTreeId)
                        writer.WriteString("taskOutcome", taskOutcome))
                let expectedPayloadBytes = ResearchCanonical.canonicalizeElement outcomePayload
                let existingOutcomes = existing |> List.filter (fun event -> event.Body.EventType = "outcome.observed")
                let outcomeEventId, outcomeEventHash, countAfterOutcome =
                    match existingOutcomes with
                    | [] ->
                        let outcomeDraft = ResearchRuntime.createDraft manifest identity "outcome.observed" [ source ] outcomePayload
                        let receipt = ResearchLedger.append root ledger outcomeDraft
                        receipt.EventId, receipt.EventHash, existing.Length + 1
                    | [ observed ]
                        when observed.Body.SourceRefs = [ source ]
                             && ResearchCanonical.canonicalizeElement observed.Body.Payload = expectedPayloadBytes ->
                        observed.Body.EventId, observed.EventHash, existing.Length
                    | [ _ ] ->
                        Internal.fail "RESEARCH_CLOSE_CONFLICT: existing outcome differs from the supplied receipt."
                    | _ ->
                        Internal.fail "RESEARCH_CLOSE_INVALID: multiple outcome events exist."

                let eventCount = countAfterOutcome + 1

                if crashPoint = Some ResearchCrashPoint.AfterOutcomeSyncBeforeClose then
                    Internal.fail "INJECTED_CRASH: after outcome fsync before closure."

                let closedAt = ResearchRuntime.nowText ()
                let closePayload =
                    ResearchRuntime.payload (fun writer ->
                        writer.WriteString("closedAtUtc", closedAt)
                        writer.WriteNumber("eventCount", eventCount)
                        writer.WriteString("outcomeEventId", outcomeEventId)
                        writer.WriteString("sourceManifestSha256", sourceManifestSha))
                let closeSource = ResearchRuntime.harnessEventSource outcomeEventId outcomeEventHash
                let closeDraft = ResearchRuntime.createDraft manifest identity "observation.closed" [ closeSource ] closePayload
                ResearchLedger.append root ledger closeDraft |> ignore
                let finalEvents = ResearchLedger.readVerified root ledger

                if finalEvents.Length <> eventCount then
                    Internal.fail "RESEARCH_CLOSE_INVALID: final event count differs."

                validateFinalClosure (Some sourceManifestSha) finalEvents |> ignore

                if crashPoint = Some ResearchCrashPoint.AfterCloseSyncBeforeMarkerUnlink then
                    Internal.fail "INJECTED_CRASH: after close fsync before marker unlink."

                removeMarkerDurably root crashPoint
                let finalEvent = List.last finalEvents

                { ObservationId = observationId
                  Idempotent = false
                  EventCount = finalEvents.Length
                  FinalEventHash = finalEvent.EventHash
                  LedgerSha256 = Internal.sha256File ledger
                  MarkerRemoved = true }
            | _ ->
                Internal.fail "RESEARCH_CLOSE_INVALID: multiple closure events exist."

    let close root observationId outcomeReceipt =
        withActivationLock root (fun () -> closeLocked root observationId outcomeReceipt None)

    let closeWithCrashPoint root observationId outcomeReceipt crashPoint =
        withActivationLock root (fun () -> closeLocked root observationId outcomeReceipt (Some crashPoint))
