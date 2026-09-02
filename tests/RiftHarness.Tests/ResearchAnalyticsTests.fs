namespace RiftHarness.Tests

open System
open System.Diagnostics
open System.Globalization
open System.IO
open System.Text.Json
open RiftHarness

module ResearchAnalyticsTests =
    let private assertTrue condition message = if not condition then failwith message
    let private assertEqual expected actual message = if not (Unchecked.equals expected actual) then failwith $"{message}; expected={expected}; actual={actual}"

    let private expectFailure (code: string) (action: unit -> unit) =
        try
            action ()
            failwith $"Expected failure {code}."
        with
        | HarnessException (message: string) when message.Contains(code, StringComparison.Ordinal) -> ()

    let private withWorkspace action =
        let root = Path.Combine(Path.GetTempPath(), "RiftHarness.Analytics-" + Guid.NewGuid().ToString("N"))
        Directory.CreateDirectory(root) |> ignore

        try
            Workspace.initialize root |> ignore
            action root
        finally
            if Directory.Exists(root) then Directory.Delete(root, true)

    let private json (text: string) =
        use document = JsonDocument.Parse(text)
        document.RootElement.Clone()

    let private id (prefix: string) (number: int) = prefix + number.ToString("D26", CultureInfo.InvariantCulture)
    let private sha character = String(character, 64)
    let private commit character = String(character, 40)

    let private source =
        { SourceKind = "fixture"
          RepositoryCommit = ResearchValue.Unknown
          RepositoryPath = ResearchValue.Unknown
          LineStart = ResearchValue.Unknown
          LineEnd = ResearchValue.Unknown
          ArtifactSha256 = sha 'a'
          SourceEventId = ResearchValue.Unknown
          Resolvable = false }

    let private toolDraft number observation evidence =
        ResearchEventDraft.create
            (id "EV-" number)
            observation
            evidence
            "tool.finished"
            "2026-09-02T10:00:00.000Z"
            [ source ]
            (json $"{{\"toolClass\":\"test\",\"commandDigest\":\"{sha 'b'}\",\"startedMonotonicNs\":1,\"completedMonotonicNs\":2,\"resultSha256\":\"{sha 'c'}\"}}")

    let private metric metricId rows =
        rows |> List.find (fun row -> row.MetricId = metricId)

    let private metricEvent number eventType payload monotonic result =
        { Body =
            { toolDraft number (id "OBS-" 90) "synthetic-test-only" with
                EventType = eventType
                Payload = json payload
                MonotonicTimeNs = ResearchValue.Known monotonic
                MonotonicClockId = ResearchValue.Known "fixture-clock"
                Result = result }
          Sequence = int64 number
          PreviousEventHash = ResearchValue.Unknown
          EventHash = sha 'f' }

    let private writeManifest root observation =
        let sourceInventory = ResearchCanonical.canonicalizeJson "[]"
        let sourceHash = Internal.sha256Hex sourceInventory
        let text =
            $"""{{"actorIdentityRule":"test","baselineCommit":"{commit 'a'}","baselineTreeId":"{commit 'b'}","collectorVersion":"test","evidenceClass":"synthetic-test-only","exporterVersion":"test","generatedAtUtc":"2026-09-02T10:00:00.000Z","headCommit":"{commit 'c'}","inputTreeId":"{commit 'd'}","locale":"C","observationId":"{observation}","pathMapVersion":"test","protocolBundleSha256":"{sha 'e'}","protocolVersion":"v1","redactionPolicyVersion":"test","resultTreeId":"{commit 'f'}","sourceInventory":[],"sourceInventorySha256":"{sourceHash}","studyId":"riftward-research-observability","targetTaskId":"T-053","taskManifestSha256":"{sha 'f'}","timezone":"UTC","toolchainSha256":"{sha '0'}"}}"""
        let path = Path.Combine(root, "study.json")
        File.WriteAllText(path, text, Constants.Utf8NoBom)
        path

    let private appendTool root observation =
        let ledger = ResearchLedger.ledgerPath root observation
        ResearchLedger.append root ledger (toolDraft 1 observation "synthetic-test-only") |> ignore
        ledger

    let private appendClosedFixture root observation sourceManifestSha =
        let ledger = ResearchLedger.ledgerPath root observation
        let append number eventType payload =
            ResearchLedger.append root ledger (ResearchEventDraft.create (id "EV-" number) observation "synthetic-test-only" eventType "2026-09-02T10:00:00.000Z" [ source ] (json payload)) |> ignore
        append 1 "protocol.frozen" $"{{\"protocolId\":\"p\",\"protocolVersion\":\"v1\",\"protocolBundleSha256\":\"{sha 'e'}\",\"freezeAtUtc\":\"2026-09-02T10:00:00.000Z\"}}"
        append 2 "observation.started" $"{{\"targetTaskId\":\"T-053\",\"baselineCommit\":\"{commit 'a'}\",\"collectorVersion\":\"test\",\"nonInterferenceSnapshotSha256\":\"{sha 'b'}\",\"activationGuardSha256\":\"{sha 'c'}\"}}"
        append 3 "activity.state.changed" "{\"fromActivityState\":\"idle\",\"toActivityState\":\"agent-active\",\"reasonCode\":\"test\"}"
        ResearchLedger.append root ledger (toolDraft 4 observation "synthetic-test-only") |> ignore
        append 5 "outcome.observed" $"{{\"taskOutcome\":\"accepted\",\"hypothesisResult\":\"unknown\",\"resultCommit\":\"{commit 'd'}\",\"reasonCode\":\"test\"}}"
        let outcomeId = id "EV-" 5
        append 6 "observation.closed" $"{{\"eventCount\":6,\"sourceManifestSha256\":\"{sourceManifestSha}\",\"outcomeEventId\":\"{outcomeId}\",\"closedAtUtc\":\"2026-09-02T10:00:00.000Z\"}}"
        ledger

    let private filesUnder root =
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        |> Seq.map (fun path -> Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllBytes(path))
        |> Map.ofSeq

    let metricsRemainUnknownWithoutStructuredEvidence () =
        let rows = ResearchMetrics.calculate [] None None

        for metricId in [ "USE-INPUT-TOKENS"; "USE-OUTPUT-TOKENS"; "USE-COST-AMOUNT"; "USE-TOKENS-PER-ACCEPTED"; "MODEL-SWITCHES-PER-RUN" ] do
            assertEqual ResearchContract.Unknown (metric metricId rows).Value $"{metricId} invented a value without evidence"

        assertEqual ResearchContract.Unknown (metric "USE-COST-AMOUNT" rows).EvidenceClass "Empty evidence gained a class"
        assertEqual ResearchContract.Unknown (metric "GATE-COVERAGE" rows).Value "Null denominator did not remain unknown"

    let evidenceClassesStaySeparatedInMetrics () =
        withWorkspace (fun root ->
            let first = id "OBS-" 1
            let second = id "OBS-" 2
            let firstLedger = ResearchLedger.ledgerPath root first
            let secondLedger = ResearchLedger.ledgerPath root second
            ResearchLedger.append root firstLedger (toolDraft 1 first "synthetic-test-only") |> ignore
            let retrospective =
                { toolDraft 2 second "retrospective-derived" with
                    SourceRefs = [ { source with SourceKind = "git-commit" } ] }
            ResearchLedger.append root secondLedger retrospective |> ignore
            let rows = ResearchMetrics.calculate (ResearchLedger.readVerified root firstLedger @ ResearchLedger.readVerified root secondLedger) None None
            assertEqual ResearchContract.Unknown (metric "OBS-CHAIN-COMPLETE" rows).EvidenceClass "Mixed evidence classes were collapsed into one class")

    let metricsRequireBoundFactsAndUseExactUnions () =
        let tree = commit 'a'
        let gateStart = metricEvent 1 "gate.started" $"{{\"attempt\":1,\"gateId\":\"G-SPEC\",\"targetTreeId\":\"{tree}\"}}" 0L ResearchValue.Unknown
        let gateFinish = metricEvent 2 "gate.finished" $"{{\"attempt\":1,\"evidenceSha256\":\"{sha 'e'}\",\"gateId\":\"G-SPEC\",\"targetTreeId\":\"{tree}\"}}" 1_000_000L (ResearchValue.Known "fail")
        let toolOne = metricEvent 3 "tool.finished" $"{{\"toolClass\":\"test\",\"commandDigest\":\"{sha 'b'}\",\"startedMonotonicNs\":0,\"completedMonotonicNs\":4000000,\"resultSha256\":\"{sha 'c'}\"}}" 4_000_000L ResearchValue.Unknown
        let toolTwo = metricEvent 4 "tool.finished" $"{{\"toolClass\":\"test\",\"commandDigest\":\"{sha 'd'}\",\"startedMonotonicNs\":2000000,\"completedMonotonicNs\":6000000,\"resultSha256\":\"{sha 'e'}\"}}" 6_000_000L ResearchValue.Unknown
        let intervention = metricEvent 5 "research.intervention.recorded" $"{{\"interventionId\":\"INT-1\",\"category\":\"I1-clarification\",\"decisionActSha256\":\"{sha 'a'}\",\"counted\":true,\"classificationReason\":\"fixture\",\"durationMs\":\"unknown\"}}" 7_000_000L ResearchValue.Unknown
        let duplicateDecision = metricEvent 6 "research.intervention.recorded" $"{{\"interventionId\":\"INT-2\",\"category\":\"I1-clarification\",\"decisionActSha256\":\"{sha 'a'}\",\"counted\":true,\"classificationReason\":\"fixture\",\"durationMs\":\"unknown\"}}" 8_000_000L ResearchValue.Unknown
        [ "paired gate", [ gateStart; gateFinish ], "GATE-ATTEMPTS-TOTAL", "1"
          "orphan gate", [ gateStart ], "GATE-ATTEMPTS-TOTAL", ResearchContract.Unknown
          "interval union", [ toolOne; toolTwo ], "TOOL-ACTIVE-MS", "6"
          "decision dedup", [ intervention; duplicateDecision ], "INT-COUNT", "1"
          "missing receipt identity", [ toolOne ], "USE-INPUT-TOKENS", ResearchContract.Unknown ]
        |> List.iter (fun (name, events, metricId, expected) ->
            assertEqual expected (metric metricId (ResearchMetrics.calculate events None None)).Value $"{name} did not follow the frozen metric contract")

    let exportsAreByteIdenticalAndTamperEvident () =
        withWorkspace (fun root ->
            let observation = id "OBS-" 3
            let manifest = writeManifest root observation
            let sourceHash = Internal.sha256Hex (ResearchCanonical.canonicalizeJson "[]")
            appendClosedFixture root observation sourceHash |> ignore
            let first = ".ai/runtime/research/exports/first"
            let second = ".ai/runtime/research/exports/second"
            ResearchExport.export root manifest first |> ignore
            ResearchExport.export root manifest second |> ignore
            let firstFiles = filesUnder (Path.Combine(root, first))
            let secondFiles = filesUnder (Path.Combine(root, second))
            assertEqual firstFiles secondFiles "Repeated export bytes differ"
            let receipt = ResearchExport.verifyExport root first
            ResearchExport.verifyExportWithExpectedReceipt root first (Some receipt) |> ignore
            expectFailure "EXPORT_RECEIPT_MISMATCH" (fun () -> ResearchExport.verifyExportWithExpectedReceipt root first (Some(sha '0')) |> ignore)
            let summary = Path.Combine(root, first, "summary.json")
            File.AppendAllText(summary, "x", Constants.Utf8NoBom)
            expectFailure "EXPORT_HASH_INVALID" (fun () -> ResearchExport.verifyExport root first |> ignore))

    let private git root arguments =
        let info = ProcessStartInfo("git")
        info.WorkingDirectory <- root
        info.UseShellExecute <- false
        info.RedirectStandardOutput <- true
        info.RedirectStandardError <- true
        arguments |> List.iter info.ArgumentList.Add
        use child = Process.Start(info)
        let output = child.StandardOutput.ReadToEnd()
        let error = child.StandardError.ReadToEnd()
        child.WaitForExit()
        if child.ExitCode <> 0 then failwith $"Temporary Git fixture failed: {error}"
        output.Trim()

    let private gitFixture root =
        git root [ "init"; "--quiet" ] |> ignore
        git root [ "config"; "user.email"; "fixture@example.invalid" ] |> ignore
        git root [ "config"; "user.name"; "fixture" ] |> ignore
        File.WriteAllText(Path.Combine(root, "tracked.txt"), "one\n", Constants.Utf8NoBom)
        git root [ "add"; "tracked.txt" ] |> ignore
        git root [ "commit"; "--quiet"; "-m"; "first" ] |> ignore
        let baseline = git root [ "rev-parse"; "HEAD" ]
        File.WriteAllText(Path.Combine(root, "tracked.txt"), "two\n", Constants.Utf8NoBom)
        git root [ "commit"; "--quiet"; "-am"; "second" ] |> ignore
        baseline, git root [ "rev-parse"; "HEAD" ]

    let gitBoundariesRejectMalformedAndMovingNames () =
        withWorkspace (fun root ->
            let baseline, head = gitFixture root
            let history = ResearchGitImport.read root baseline head
            assertEqual 1 history.Commits.Length "Fixture import omitted its forward commit"

            [ "uppercase-object", String('A', 40)
              "moving-name", "HEAD" ]
            |> List.iter (fun (_, invalid) -> expectFailure "Commitgrenze" (fun () -> ResearchGitImport.read root baseline invalid |> ignore)))

    let retrospectiveImportKeepsHumanAndUsageUnknown () =
        withWorkspace (fun root ->
            let baseline, head = gitFixture root
            let output = ".ai/runtime/research/imports/history.json"
            let exitCode = ResearchCli.execute root [ "import-git-history"; "--task"; "T-053"; "--base"; baseline; "--head"; head; "--output"; output ]
            assertEqual 0 exitCode "Retrospective import failed"
            use document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, output)))
            for name in [ "inputTokens"; "outputTokens"; "costAmount"; "costCurrency"; "humanActiveDurationMs" ] do
                assertEqual ResearchContract.Unknown (document.RootElement.GetProperty(name).GetString()) $"Retrospective import fabricated {name}")

    let architectureAblationsStayDiagnostic () =
        let baseInput files =
            { CheckpointId = "checkpoint"
              BaselineCommit = commit 'a'
              ResultCommit = commit 'b'
              AcceptedTaskId = "T-053"
              AcceptedTreeId = commit 'c'
              PathMapVersion = "v1"
              PathMap = [ { Prefix = "src"; FileClass = "production"; Component = "app" } ]
              Files = files
              BaselineReferences = []
              ResultReferences = []
              Findings = []
              AnalyzerReceipt = None
              TestReceipt = None
              BaselineTestReceipt = None
              ComplexityReceipt = None }

        [ "binary", baseInput [ { Path = "src/asset.bin"; BaselinePath = None; ResultLines = Some 4; BaselineLines = Some 1; IsBinary = true; SourceSha256 = sha 'a'; Changed = true } ], "unknown", "production", false
          "empty-map", { baseInput [ { Path = "src/a.cs"; BaselinePath = None; ResultLines = Some 4; BaselineLines = Some 1; IsBinary = false; SourceSha256 = sha 'b'; Changed = true } ] with PathMap = [] }, "4", "unknown", false ]
        |> List.iter (fun (name, input, expectedLines, expectedClass, expectedGateCoupled) ->
            let snapshot = ResearchArchitecture.create input
            assertEqual expectedLines snapshot.FileRows.Head.Lines $"{name} did not preserve unknown"
            assertEqual expectedClass snapshot.FileRows.Head.FileClass $"{name} inferred an unsupported class"
            assertEqual expectedGateCoupled snapshot.GateCoupled $"{name} became gate-coupled")

    let all =
        [ "research metrics preserve unknown denominators and missing usage", metricsRemainUnknownWithoutStructuredEvidence
          "research metrics keep evidence classes separated", evidenceClassesStaySeparatedInMetrics
          "research metrics require paired facts and interval unions", metricsRequireBoundFactsAndUseExactUnions
          "research exports are deterministic and tamper-evident", exportsAreByteIdenticalAndTamperEvident
          "research git import rejects malformed and moving boundaries", gitBoundariesRejectMalformedAndMovingNames
          "research retrospective import preserves unknown usage and human duration", retrospectiveImportKeepsHumanAndUsageUnknown
          "research architecture binary and path-map ablations stay diagnostic", architectureAblationsStayDiagnostic ]
