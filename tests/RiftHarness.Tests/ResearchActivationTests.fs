namespace RiftHarness.Tests

open System
open System.Diagnostics
open System.IO
open RiftHarness

module ResearchActivationTests =
    let private expectFailure (code: string) (action: unit -> unit) =
        try action (); failwith $"Expected failure {code}."
        with | HarnessException message when message.Contains(code, StringComparison.Ordinal) -> ()

    let private git root args =
        let info = ProcessStartInfo("git")
        info.WorkingDirectory <- root
        info.UseShellExecute <- false
        info.RedirectStandardOutput <- true
        info.RedirectStandardError <- true
        args |> List.iter info.ArgumentList.Add
        use p = Process.Start(info)
        let output = p.StandardOutput.ReadToEnd().Trim()
        let error = p.StandardError.ReadToEnd().Trim()
        p.WaitForExit()
        if p.ExitCode <> 0 then failwith error
        output

    let private write (root: string) (relative: string) (text: string) =
        let path = Path.Combine(root, relative)
        Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore
        File.WriteAllText(path, text, Constants.Utf8NoBom)

    let private sha text = Internal.sha256Text text
    let private observation = "OBS-00000000000000000000000001"

    let private fixture action =
        let root = Path.Combine(Path.GetTempPath(), "RiftHarness.Activation-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(root) |> ignore
        try
            Workspace.initialize root |> ignore
            let schema = File.ReadAllText(Path.Combine(Environment.CurrentDirectory, ".ai/schemas/task.schema.json"))
            write root ".ai/schemas/task.schema.json" schema
            let task = "{\"schemaVersion\":1,\"id\":\"T-042\",\"title\":\"Fixture target\",\"status\":\"ready\",\"objective\":\"A valid prospective fixture target\",\"inScope\":[\"fixture\"],\"outOfScope\":[],\"requirements\":[\"fixture\"],\"acceptanceCriteria\":[{\"id\":\"A\",\"statement\":\"fixture\",\"verification\":\"test\"}],\"dependencies\":[\"T-041\"],\"requiredGates\":[\"G\"],\"decisionPolicy\":{\"mayDecide\":[],\"mustEscalate\":[]}}"
            let dependency = "{\"schemaVersion\":1,\"id\":\"T-041\",\"title\":\"Fixture dependency\",\"status\":\"accepted\",\"objective\":\"An accepted fixture dependency\",\"inScope\":[\"fixture\"],\"outOfScope\":[],\"requirements\":[\"fixture\"],\"acceptanceCriteria\":[{\"id\":\"A\",\"statement\":\"fixture\",\"verification\":\"test\"}],\"requiredGates\":[\"G\"],\"decisionPolicy\":{\"mayDecide\":[],\"mustEscalate\":[]}}"
            let protocolPaths =
                [ ".ai/tasks/T-053-research-observability.json"
                  "docs/research/METRICS.md"
                  "docs/research/OBSERVABILITY_DATA_DICTIONARY.md"
                  "docs/research/PRIVACY_AND_PUBLICATION.md"
                  "docs/research/PROTOCOL.md"
                  "docs/research/PROTOCOL_CHANGELOG.md"
                  "docs/research/REPRODUCIBILITY.md"
                  "docs/research/THREATS_TO_VALIDITY.md" ]
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
            let taskHash = Internal.sha256File(Path.Combine(root, ".ai/tasks/T-042-fixture.json"))
            let sourceHash = sha "[]"
            let toolchainHash = sha "toolchain"
            let manifest = $"{{\"actorIdentityRule\":\"fixture\",\"baselineCommit\":\"{commit}\",\"baselineTreeId\":\"{tree}\",\"collectorVersion\":\"{ResearchRuntime.CollectorVersion}\",\"evidenceClass\":\"prospective-observed\",\"exporterVersion\":\"{ResearchRuntime.ExporterVersion}\",\"generatedAtUtc\":\"2026-09-02T10:00:00.000Z\",\"headCommit\":\"{commit}\",\"inputTreeId\":\"{tree}\",\"locale\":\"C\",\"observationId\":\"{observation}\",\"pathMapVersion\":\"fixture\",\"protocolBundleSha256\":\"{sha bundle}\",\"protocolVersion\":\"2.0.0\",\"redactionPolicyVersion\":\"fixture\",\"resultTreeId\":\"unknown\",\"sourceInventory\":[],\"sourceInventorySha256\":\"{sourceHash}\",\"studyId\":\"riftward-research-observability\",\"targetTaskId\":\"T-042\",\"taskManifestSha256\":\"{taskHash}\",\"timezone\":\"UTC\",\"toolchainSha256\":\"{toolchainHash}\"}}"
            let manifestPath = Path.Combine(root, ".ai", "runtime", "research", "fixture-study.json")
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)) |> ignore
            File.WriteAllText(manifestPath, manifest, Constants.Utf8NoBom)
            action root manifestPath
        finally
            if Directory.Exists(root) then Directory.Delete(root, true)

    let private outcome root =
        let path = Path.Combine(root, "outcome.json")
        let sourceHash = sha "[]"
        File.WriteAllText(path, $"{{\"hypothesisResult\":\"inconclusive\",\"observationId\":\"{observation}\",\"reasonCode\":\"fixture\",\"resultCommit\":\"unknown\",\"resultTreeId\":\"unknown\",\"sourceManifestSha256\":\"{sourceHash}\",\"targetTaskId\":\"T-042\",\"taskOutcome\":\"unknown\"}}", Constants.Utf8NoBom)
        path

    let private linux action = if OperatingSystem.IsLinux() then action () else ()

    let crashBeforeMarkerRename () = linux (fun () -> fixture (fun root manifest ->
        expectFailure "INJECTED_CRASH" (fun () -> ResearchActivation.beginWithCrashPoint root manifest ResearchCrashPoint.BeforeMarkerRename |> ignore)
        expectFailure "INCOMPLETE_ACTIVATION" (fun () -> ResearchActivation.beginObservation root manifest |> ignore)))

    let crashAfterMarkerRename () = linux (fun () -> fixture (fun root manifest ->
        expectFailure "INJECTED_CRASH" (fun () -> ResearchActivation.beginWithCrashPoint root manifest ResearchCrashPoint.AfterMarkerRenameBeforeDirectorySync |> ignore)
        if ResearchActivation.tryActive root |> Option.isNone then failwith "Durable marker was not readable."))

    let closeCrashBoundariesAreIdempotent () = linux (fun () -> fixture (fun root manifest ->
        ResearchActivation.beginObservation root manifest |> ignore
        let receipt = outcome root
        expectFailure "INJECTED_CRASH" (fun () -> ResearchActivation.closeWithCrashPoint root observation receipt ResearchCrashPoint.AfterCloseSyncBeforeMarkerUnlink |> ignore)
        if not ((ResearchActivation.status root (Some observation)).Issues |> List.contains "STALE_ACTIVE_MARKER") then failwith "Stale marker was not reported."
        ResearchActivation.close root observation receipt |> ignore
        ResearchActivation.close root observation receipt |> ignore))

    let closeAfterOutcomeCrashDoesNotDuplicateOutcome () = linux (fun () -> fixture (fun root manifest ->
        ResearchActivation.beginObservation root manifest |> ignore
        let receipt = outcome root
        expectFailure "INJECTED_CRASH" (fun () -> ResearchActivation.closeWithCrashPoint root observation receipt ResearchCrashPoint.AfterOutcomeSyncBeforeClose |> ignore)
        ResearchActivation.close root observation receipt |> ignore
        let events = ResearchLedger.readVerified root (ResearchLedger.ledgerPath root observation)
        if (events |> List.filter (fun event -> event.Body.EventType = "outcome.observed") |> List.length) <> 1 then failwith "Outcome was duplicated after close retry."
        if (events |> List.filter (fun event -> event.Body.EventType = "observation.closed") |> List.length) <> 1 then failwith "Closure was not unique after retry."))

    let closeAfterMarkerUnlinkIsIdempotent () = linux (fun () -> fixture (fun root manifest ->
        ResearchActivation.beginObservation root manifest |> ignore
        let receipt = outcome root
        expectFailure "INJECTED_CRASH" (fun () -> ResearchActivation.closeWithCrashPoint root observation receipt ResearchCrashPoint.AfterMarkerUnlinkBeforeDirectorySync |> ignore)
        ResearchActivation.close root observation receipt |> ignore))

    let invalidLowercaseObservationAndSourceMismatchFailClosed () = linux (fun () -> fixture (fun root manifest ->
        let original = File.ReadAllText(manifest, Constants.Utf8NoBom)
        File.WriteAllText(manifest, original.Replace(observation, observation.ToLowerInvariant(), StringComparison.Ordinal), Constants.Utf8NoBom)
        expectFailure "RESEARCH_MANIFEST_INVALID" (fun () -> ResearchActivation.beginObservation root manifest |> ignore)
        File.WriteAllText(manifest, original, Constants.Utf8NoBom)
        ResearchActivation.beginObservation root manifest |> ignore
        let receipt = outcome root
        let bad = File.ReadAllText(receipt, Constants.Utf8NoBom).Replace(sha "[]", sha "[1]", StringComparison.Ordinal)
        File.WriteAllText(receipt, bad, Constants.Utf8NoBom)
        expectFailure "OUTCOME_RECEIPT_INVALID" (fun () -> ResearchActivation.close root observation receipt |> ignore)))

    let doubleBeginIsIdempotent () = linux (fun () -> fixture (fun root manifest ->
        let first = ResearchActivation.beginObservation root manifest
        let second = ResearchActivation.beginObservation root manifest
        if not second.Idempotent || first.ActivationEventHash <> second.ActivationEventHash then failwith "Duplicate begin was not idempotent."))

    let all =
        [ "T-053 activation crash before marker rename", crashBeforeMarkerRename
          "T-053 activation crash after marker rename", crashAfterMarkerRename
          "T-053 close crash boundaries are idempotent", closeCrashBoundariesAreIdempotent
          "T-053 close retry after outcome fsync does not duplicate outcome", closeAfterOutcomeCrashDoesNotDuplicateOutcome
          "T-053 close after marker unlink is idempotent", closeAfterMarkerUnlinkIsIdempotent
          "T-053 invalid IDs and source mismatch fail closed", invalidLowercaseObservationAndSourceMismatchFailClosed
          "T-053 duplicate begin is idempotent", doubleBeginIsIdempotent ]
