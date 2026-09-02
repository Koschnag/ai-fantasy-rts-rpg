namespace RiftHarness.Tests

open System
open System.Diagnostics
open System.IO
open System.Text.Json
open RiftHarness

module ResearchCollectorTests =
    let private expectFailure (code: string) (action: unit -> unit) =
        try action (); failwith $"Expected failure {code}."
        with | HarnessException message when message.Contains(code, StringComparison.Ordinal) -> ()

    let private write (root: string) (relative: string) (text: string) =
        let path = Path.Combine(root, relative)
        Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore
        File.WriteAllText(path, text, Constants.Utf8NoBom)

    let private git root args =
        let info = ProcessStartInfo("git")
        info.WorkingDirectory <- root
        info.UseShellExecute <- false
        info.RedirectStandardOutput <- true
        info.RedirectStandardError <- true
        args |> List.iter info.ArgumentList.Add
        use child = Process.Start(info)
        let output = child.StandardOutput.ReadToEnd().Trim()
        let error = child.StandardError.ReadToEnd().Trim()
        child.WaitForExit()
        if child.ExitCode <> 0 then failwith error
        output

    let private sha text = Internal.sha256Text text
    let private observation = "OBS-00000000000000000000000053"

    let private fixture action =
        let root = Path.Combine(Path.GetTempPath(), "RiftHarness.Collector-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(root) |> ignore
        try
            Workspace.initialize root |> ignore
            let schema = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, ".ai/schemas/task.schema.json"))
            let gitignore = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, ".gitignore"))
            write root ".ai/schemas/task.schema.json" schema
            write root ".gitignore" gitignore
            let task = "{\"schemaVersion\":1,\"id\":\"T-042\",\"title\":\"Fixture target\",\"status\":\"ready\",\"objective\":\"A valid prospective fixture target\",\"inScope\":[\"fixture\"],\"outOfScope\":[],\"requirements\":[\"fixture\"],\"acceptanceCriteria\":[{\"id\":\"A\",\"statement\":\"fixture\",\"verification\":\"test\"}],\"dependencies\":[\"T-041\"],\"requiredGates\":[\"G\"],\"decisionPolicy\":{\"mayDecide\":[],\"mustEscalate\":[]}}"
            let dependency = "{\"schemaVersion\":1,\"id\":\"T-041\",\"title\":\"Fixture dependency\",\"status\":\"accepted\",\"objective\":\"A fixture dependency\",\"inScope\":[\"fixture\"],\"outOfScope\":[],\"requirements\":[\"fixture\"],\"acceptanceCriteria\":[{\"id\":\"A\",\"statement\":\"fixture\",\"verification\":\"test\"}],\"requiredGates\":[\"G\"],\"decisionPolicy\":{\"mayDecide\":[],\"mustEscalate\":[]}}"
            let protocolPaths = [ ".ai/tasks/T-053-research-observability.json"; "docs/research/METRICS.md"; "docs/research/OBSERVABILITY_DATA_DICTIONARY.md"; "docs/research/PRIVACY_AND_PUBLICATION.md"; "docs/research/PROTOCOL.md"; "docs/research/PROTOCOL_CHANGELOG.md"; "docs/research/REPRODUCIBILITY.md"; "docs/research/THREATS_TO_VALIDITY.md" ]
            write root ".ai/tasks/T-042-fixture.json" task
            write root ".ai/tasks/T-041-fixture.json" dependency
            for path in protocolPaths do write root path "fixture"
            git root [ "init"; "--quiet" ] |> ignore
            git root [ "config"; "user.email"; "fixture@example.invalid" ] |> ignore
            git root [ "config"; "user.name"; "fixture" ] |> ignore
            git root [ "add"; "." ] |> ignore
            git root [ "commit"; "--quiet"; "-m"; "fixture" ] |> ignore
            let commit = git root [ "rev-parse"; "HEAD" ]
            let tree = git root [ "rev-parse"; "HEAD^{tree}" ]
            let bundle = protocolPaths |> List.sort |> List.map (fun path -> $"{Internal.sha256File(Path.Combine(root, path))}  {path}\n") |> String.concat ""
            let sourceHash = sha "[]"
            let taskHash = Internal.sha256File(Path.Combine(root, ".ai/tasks/T-042-fixture.json"))
            let toolchainHash = sha "toolchain"
            let manifest = $"{{\"actorIdentityRule\":\"fixture\",\"baselineCommit\":\"{commit}\",\"baselineTreeId\":\"{tree}\",\"collectorVersion\":\"{ResearchRuntime.CollectorVersion}\",\"evidenceClass\":\"prospective-observed\",\"exporterVersion\":\"{ResearchRuntime.ExporterVersion}\",\"generatedAtUtc\":\"2026-09-02T10:00:00.000Z\",\"headCommit\":\"{commit}\",\"inputTreeId\":\"{tree}\",\"locale\":\"C\",\"observationId\":\"{observation}\",\"pathMapVersion\":\"fixture\",\"protocolBundleSha256\":\"{sha bundle}\",\"protocolVersion\":\"2.0.0\",\"redactionPolicyVersion\":\"fixture\",\"resultTreeId\":\"unknown\",\"sourceInventory\":[],\"sourceInventorySha256\":\"{sourceHash}\",\"studyId\":\"riftward-research-observability\",\"targetTaskId\":\"T-042\",\"taskManifestSha256\":\"{taskHash}\",\"timezone\":\"UTC\",\"toolchainSha256\":\"{toolchainHash}\"}}"
            let manifestPath = Path.Combine(root, ".ai/runtime/research/fixture-study.json")
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)) |> ignore
            File.WriteAllText(manifestPath, manifest, Constants.Utf8NoBom)
            ResearchActivation.beginObservation root manifestPath |> ignore
            action root
        finally if Directory.Exists(root) then Directory.Delete(root, true)

    let inactiveHookIsNoOp () =
        let root = Path.Combine(Path.GetTempPath(), "RiftHarness.CollectorInactive-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(root) |> ignore
        try
            Workspace.initialize root |> ignore
            match ResearchCollector.recordHarnessEvent root "not-a-run" 0L "not-a-hash" "unmapped.command" with
            | ResearchCollectionResult.Inactive -> ()
            | result -> failwith $"Inactive hook interfered: {result}"
        finally if Directory.Exists(root) then Directory.Delete(root, true)

    let unmappedCommandCreatesDurableGap () =
        if OperatingSystem.IsLinux() then fixture (fun root ->
            let run = RunStore.startProvenanced root "fixture-agent" { ActorId = "fixture-agent"; TaskId = None; ModelId = None; PromptFile = None; ToolchainFile = None }
            let payload = Path.Combine(root, "payload.json")
            File.WriteAllText(payload, "{}", Constants.Utf8NoBom)
            let receipt = RunStore.append root run "unmapped.command" payload
            let result = ResearchCollector.recordHarnessEvent root run receipt.Sequence receipt.EventHash "unmapped.command"
            let gapId = match result with | ResearchCollectionResult.GapRecorded id -> id | _ -> failwith "Unmapped command did not create a gap."
            let gap = Path.Combine(root, ".ai/runtime/research/gaps", observation, gapId + ".json")
            if not (File.Exists(gap)) then failwith "Gap receipt was not published."
            use document = JsonDocument.Parse(File.ReadAllText(gap, Constants.Utf8NoBom))
            if document.RootElement.GetProperty("failureClass").GetString() <> "RESEARCH_EVENT_GAP" then failwith "Gap receipt has the wrong failure class.")

    let interventionSourcesAreFrozenRedactedAndReplaySafe () =
        if OperatingSystem.IsLinux() then fixture (fun root ->
            let sourcePath = Path.Combine(root, "decision.json")
            File.WriteAllText(sourcePath, "{\"prompt\":\"raw prompt must not persist\",\"password\":\"super-secret\",\"reason\":\"operator text\"}", Constants.Utf8NoBom)
            let first, firstResult = ResearchCollector.interventionRecord root observation "I1-clarification" "decision.json" "clarification.requested"
            match firstResult with | ResearchCollectionResult.Recorded _ -> () | _ -> failwith "Intervention was not recorded."
            File.WriteAllText(sourcePath, "{\"prompt\":\"changed source\",\"password\":\"other-secret\"}", Constants.Utf8NoBom)
            let events = ResearchLedger.readVerified root (ResearchLedger.ledgerPath root observation)
            let firstEvent = events |> List.find (fun event -> event.Body.EventType = "research.intervention.recorded" && event.Body.Payload.GetProperty("interventionId").GetString() = first)
            let frozen = firstEvent.Body.SourceRefs |> List.head
            let frozenPath = match frozen.RepositoryPath with | ResearchValue.Known path -> Path.Combine(root, path) | _ -> failwith "Frozen source has no path."
            let frozenText = File.ReadAllText(frozenPath, Constants.Utf8NoBom)
            if frozenText.Contains("raw prompt", StringComparison.Ordinal) || frozenText.Contains("super-secret", StringComparison.Ordinal) || frozenText.Contains("operator text", StringComparison.Ordinal) then failwith "Frozen source retained raw intervention content."
            if Internal.sha256File frozenPath <> frozen.ArtifactSha256 then failwith "Frozen source hash changed after original mutation."
            let _, replay = ResearchCollector.interventionRecord root observation "I1-clarification" "decision.json" "clarification.requested"
            match replay with | ResearchCollectionResult.Recorded _ -> () | _ -> failwith "A new immutable source replay was not recorded."
            expectFailure "INTERVENTION_INVALID" (fun () -> ResearchCollector.interventionRecord root observation "I1-clarification" "decision.json" "raw prompt with spaces" |> ignore))

    let healthFailureIsDurableRedactedAndIdempotent () =
        if OperatingSystem.IsLinux() then
            let root = Path.Combine(Path.GetTempPath(), "RiftHarness.CollectorHealth-" + Guid.NewGuid().ToString("N"))
            Directory.CreateDirectory(root) |> ignore
            try
                Workspace.initialize root |> ignore
                let first = ResearchCollector.recordHealthFailure root "append-event" "unsafe error: fixture-alpha"
                let second = ResearchCollector.recordHealthFailure root "append-event" "unsafe error: fixture-beta"
                if first <> second then failwith "Equivalent bounded health failure was not content-addressed."
                let text = File.ReadAllText(Path.Combine(root, first), Constants.Utf8NoBom)
                if text.Contains("fixture-alpha", StringComparison.Ordinal) then failwith "Health receipt retained raw failure text."
                if ResearchCollector.healthIssues root <> [ "COLLECTOR_HEALTH_FAILURES_PRESENT" ] then failwith "Health failure is not discoverable."
            finally if Directory.Exists(root) then Directory.Delete(root, true)

    let all =
        [ "T-053 inactive collector hook is a noninterfering no-op", inactiveHookIsNoOp
          "T-053 unmapped command creates a durable collector gap", unmappedCommandCreatesDurableGap
          "T-053 intervention sources freeze, redact, and validate replay", interventionSourcesAreFrozenRedactedAndReplaySafe
          "T-053 collector health receipt is durable and non-secret", healthFailureIsDurableRedactedAndIdempotent ]
