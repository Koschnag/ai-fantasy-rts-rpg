namespace RiftHarness

open System
open System.Diagnostics
open System.Globalization
open System.IO
open System.Text.Json

[<RequireQualifiedAccess>]
module ResearchRuntime =
    [<Literal>]
    let CollectorVersion = "riftward-research-collector-v1"

    [<Literal>]
    let ExporterVersion = "riftward-research-exporter-v1"

    let nowText () =
        DateTimeOffset.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture)

    let newId prefix =
        prefix + Internal.createRunId DateTimeOffset.UtcNow

    let private monotonicClockId () =
        if OperatingSystem.IsLinux() then
            let bootIdPath = "/proc/sys/kernel/random/boot_id"

            try
                let bootId = File.ReadAllText(bootIdPath, Constants.Utf8NoBom).Trim()

                if String.IsNullOrWhiteSpace(bootId) then
                    ResearchValue.Unknown
                else
                    let digest = Internal.sha256Text (ResearchContract.StudyId + "\n" + bootId)
                    ResearchValue.Known("clock-" + digest.Substring(0, 26))
            with _ ->
                ResearchValue.Unknown
        else
            // Cross-process monotonic correlation is unavailable here. Keeping
            // both fields unknown is scientifically safer than inventing a clock.
            ResearchValue.Unknown

    let monotonicNow () =
        match monotonicClockId () with
        | ResearchValue.Unknown -> ResearchValue.Unknown, ResearchValue.Unknown
        | ResearchValue.Known clockId ->
            let nanoseconds =
                (decimal (Stopwatch.GetTimestamp()) * 1_000_000_000M
                 / decimal Stopwatch.Frequency)
                |> Decimal.Truncate
                |> int64

            ResearchValue.Known nanoseconds, ResearchValue.Known clockId

    let payload write =
        let bytes =
            Internal.jsonBytes false (fun writer ->
                writer.WriteStartObject()
                write writer
                writer.WriteEndObject())
            |> Constants.Utf8NoBom.GetString
            |> ResearchCanonical.canonicalizeJson

        use document = JsonDocument.Parse(bytes)
        document.RootElement.Clone()

    /// Resolve an event only through the strict RunStore receipt boundary.
    let authoritativeEvent root runId sequence eventType eventHash =
        RunStore.eventByReceipt root runId sequence eventType eventHash

    let gitBlobSource root commit path sha256 =
        let observed = ResearchGitImport.fileAtCommit root commit path |> Internal.sha256Hex

        if observed <> sha256 then
            Internal.fail "RESEARCH_SOURCE_INVALID: declared Git blob hash does not match the immutable object."

        { SourceKind = "git-blob"
          RepositoryCommit = ResearchValue.Known commit
          RepositoryPath = ResearchValue.Known path
          LineStart = ResearchValue.Unknown
          LineEnd = ResearchValue.Unknown
          ArtifactSha256 = sha256
          SourceEventId = ResearchValue.Unknown
          Resolvable = true }

    let harnessEventSource eventId sha256 =
        if String.IsNullOrWhiteSpace(eventId) || not (Internal.isSha256 sha256) then
            Internal.fail "RESEARCH_SOURCE_INVALID: harness event reference is malformed."

        { SourceKind = "harness-event"
          RepositoryCommit = ResearchValue.Unknown
          RepositoryPath = ResearchValue.Unknown
          LineStart = ResearchValue.Unknown
          LineEnd = ResearchValue.Unknown
          ArtifactSha256 = sha256
          SourceEventId = ResearchValue.Known eventId
          Resolvable = true }

    let harnessRunEventSource root runId sequence eventType eventHash =
        if not (Internal.isRunId runId) || not (Internal.isSha256 eventHash) then
            Internal.fail "RESEARCH_SOURCE_INVALID: run/event identity is malformed."

        // RunStore performs the complete, strict chain validation before this
        // reference is accepted. Never scan a mutable JSONL file for a hash.
        authoritativeEvent root runId sequence eventType eventHash |> ignore

        let relative = $".ai/runtime/runs/{runId}/events.jsonl"

        { SourceKind = "harness-event"
          RepositoryCommit = ResearchValue.Unknown
          RepositoryPath = ResearchValue.Known relative
          LineStart = ResearchValue.Unknown
          LineEnd = ResearchValue.Unknown
          ArtifactSha256 = eventHash
          SourceEventId = ResearchValue.Known eventHash
          Resolvable = true }

    let firstHarnessRunEventSource root runId =
        if not (Internal.isRunId runId) then
            Internal.fail "RESEARCH_SOURCE_INVALID: run identity is malformed."

        let first =
            RunStore.eventsStrict root runId
            |> List.tryHead
            |> Option.defaultWith (fun () ->
                Internal.fail "RESEARCH_SOURCE_INVALID: authoritative run-start event is absent.")
            |> fun event -> RunStore.eventByReceipt root runId event.Sequence event.EventType event.EventHash

        if first.Sequence <> 1L || first.EventType <> "run.started" then
            Internal.fail "RESEARCH_SOURCE_INVALID: authoritative run must begin with sequence 1 run.started."

        let eventHash = first.EventHash

        harnessRunEventSource root runId first.Sequence first.EventType eventHash

    let sourceFromFile root kind relativePath =
        let locations = Workspace.requireInitialized root

        let path =
            Workspace.requireSafePath locations "Research source" false (Path.Combine(root, relativePath))

        { SourceKind = kind
          RepositoryCommit = ResearchValue.Unknown
          RepositoryPath = ResearchValue.Known(Workspace.relativePath locations path)
          LineStart = ResearchValue.Unknown
          LineEnd = ResearchValue.Unknown
          ArtifactSha256 = Internal.sha256File path
          SourceEventId = ResearchValue.Unknown
          Resolvable = true }

    let createDraft
        (manifest: ResearchStudyManifest)
        (identity: ResearchGitIdentity)
        eventType
        sourceRefs
        payloadValue
        =
        let timestamp = nowText ()
        let monotonicNs, clockId = monotonicNow ()

        { ResearchEventDraft.create
              (newId "EV-")
              manifest.ObservationId
              manifest.EvidenceClass
              eventType
              timestamp
              sourceRefs
              payloadValue with
            TaskId = ResearchValue.Known manifest.TargetTaskId
            MonotonicTimeNs = monotonicNs
            MonotonicClockId = clockId
            OccurredAtUtc = ResearchValue.Known timestamp
            ActorRole = ResearchValue.Known "automation"
            ActorId = ResearchValue.Known "research-collector-v1"
            BranchRef = identity.BranchRef
            BaseCommit = ResearchValue.Known manifest.BaselineCommit
            HeadCommit = ResearchValue.Known identity.HeadCommit
            TreeId =
                if identity.WorktreeClean then
                    ResearchValue.Known identity.HeadTreeId
                else
                    ResearchValue.Unknown
            AutonomyMode = ResearchValue.Known "autonomous"
            ActivityState = ResearchValue.Known "agent-active"
            RedactionPolicyVersion = ResearchValue.Known manifest.RedactionPolicyVersion }
