namespace RiftHarness.Tests

open System
open System.Diagnostics
open System.Globalization
open System.IO
open System.Text.Json
open RiftHarness

module ResearchAnalyticsTests =
    let private assertTrue condition message =
        if not condition then
            failwith message

    let private assertEqual expected actual message =
        if not (Unchecked.equals expected actual) then
            failwith $"{message}; expected={expected}; actual={actual}"

    let private expectFailure (code: string) (action: unit -> unit) =
        try
            action ()
            failwith $"Expected failure {code}."
        with HarnessException(message: string) when message.Contains(code, StringComparison.Ordinal) ->
            ()

    let private withWorkspace action =
        let root =
            Path.Combine(Path.GetTempPath(), "RiftHarness.Analytics-" + Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory(root) |> ignore

        try
            Workspace.initialize root |> ignore
            action root
        finally
            if Directory.Exists(root) then
                Directory.Delete(root, true)

    let private json (text: string) =
        use document = JsonDocument.Parse(text)
        document.RootElement.Clone()

    let private id (prefix: string) (number: int) =
        prefix + number.ToString("D26", CultureInfo.InvariantCulture)

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
            (json
                $"{{\"toolClass\":\"test\",\"commandDigest\":\"{sha 'b'}\",\"startedMonotonicNs\":1,\"completedMonotonicNs\":2,\"resultSha256\":\"{sha 'c'}\"}}")

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

        ResearchLedger.append root ledger (toolDraft 1 observation "synthetic-test-only")
        |> ignore

        ledger

    let private appendClosedFixture root observation sourceManifestSha studyManifestSha =
        let ledger = ResearchLedger.ledgerPath root observation

        let append number eventType payload =
            ResearchLedger.append
                root
                ledger
                (ResearchEventDraft.create
                    (id "EV-" number)
                    observation
                    "synthetic-test-only"
                    eventType
                    "2026-09-02T10:00:00.000Z"
                    [ source ]
                    (json payload))
            |> ignore

        append
            1
            "protocol.frozen"
            $"{{\"protocolId\":\"p\",\"protocolVersion\":\"v1\",\"protocolBundleSha256\":\"{sha 'e'}\",\"freezeAtUtc\":\"2026-09-02T10:00:00.000Z\"}}"

        append
            2
            "observation.started"
            $"{{\"targetTaskId\":\"T-053\",\"baselineCommit\":\"{commit 'a'}\",\"collectorVersion\":\"test\",\"nonInterferenceSnapshotSha256\":\"{sha 'b'}\",\"activationGuardSha256\":\"{sha 'c'}\",\"studyManifestSha256\":\"{studyManifestSha}\"}}"

        append
            3
            "activity.state.changed"
            "{\"fromActivityState\":\"idle\",\"toActivityState\":\"agent-active\",\"reasonCode\":\"test\"}"

        ResearchLedger.append root ledger (toolDraft 4 observation "synthetic-test-only")
        |> ignore

        append
            5
            "outcome.observed"
            $"{{\"taskOutcome\":\"accepted\",\"hypothesisResult\":\"unknown\",\"resultCommit\":\"{commit 'd'}\",\"resultTreeId\":\"{commit 'e'}\",\"reasonCode\":\"test\"}}"

        let outcomeId = id "EV-" 5

        append
            6
            "observation.closed"
            $"{{\"eventCount\":6,\"sourceManifestSha256\":\"{sourceManifestSha}\",\"studyManifestSha256\":\"{studyManifestSha}\",\"outcomeEventId\":\"{outcomeId}\",\"closedAtUtc\":\"2026-09-02T10:00:00.000Z\"}}"

        ledger

    let private filesUnder root =
        Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories)
        |> Seq.map (fun path -> Path.GetRelativePath(root, path).Replace('\\', '/'), File.ReadAllBytes(path))
        |> Map.ofSeq

    let metricsRemainUnknownWithoutStructuredEvidence () =
        let rows = ResearchMetrics.calculate [] None None

        for metricId in
            [ "USE-INPUT-TOKENS"
              "USE-OUTPUT-TOKENS"
              "USE-COST-AMOUNT"
              "USE-TOKENS-PER-ACCEPTED"
              "MODEL-SWITCHES-PER-RUN" ] do
            assertEqual
                ResearchContract.Unknown
                (metric metricId rows).Value
                $"{metricId} invented a value without evidence"

        assertEqual
            ResearchContract.Unknown
            (metric "USE-COST-AMOUNT" rows).EvidenceClass
            "Empty evidence gained a class"

        assertEqual
            ResearchContract.Unknown
            (metric "GATE-COVERAGE" rows).Value
            "Null denominator did not remain unknown"

    let evidenceClassesStaySeparatedInMetrics () =
        withWorkspace (fun root ->
            let first = id "OBS-" 1
            let second = id "OBS-" 2
            let firstLedger = ResearchLedger.ledgerPath root first
            let secondLedger = ResearchLedger.ledgerPath root second

            ResearchLedger.append root firstLedger (toolDraft 1 first "synthetic-test-only")
            |> ignore

            let retrospective =
                { toolDraft 2 second "retrospective-derived" with
                    SourceRefs =
                        [ { source with
                              SourceKind = "git-commit" } ] }

            ResearchLedger.append root secondLedger retrospective |> ignore

            let rows =
                ResearchMetrics.calculate
                    (ResearchLedger.readVerified root firstLedger
                     @ ResearchLedger.readVerified root secondLedger)
                    None
                    None

            assertEqual
                ResearchContract.Unknown
                (metric "OBS-CHAIN-COMPLETE" rows).EvidenceClass
                "Mixed evidence classes were collapsed into one class")

    let metricsRequireBoundFactsAndUseExactUnions () =
        let tree = commit 'a'

        let gateStart =
            metricEvent
                1
                "gate.started"
                $"{{\"attempt\":1,\"gateId\":\"G-SPEC\",\"targetTreeId\":\"{tree}\"}}"
                0L
                ResearchValue.Unknown

        let gateFinish =
            metricEvent
                2
                "gate.finished"
                $"{{\"attempt\":1,\"evidenceSha256\":\"{sha 'e'}\",\"gateId\":\"G-SPEC\",\"targetTreeId\":\"{tree}\"}}"
                1_000_000L
                (ResearchValue.Known "fail")

        let toolOne =
            metricEvent
                3
                "tool.finished"
                $"{{\"toolClass\":\"test\",\"commandDigest\":\"{sha 'b'}\",\"startedMonotonicNs\":0,\"completedMonotonicNs\":4000000,\"resultSha256\":\"{sha 'c'}\"}}"
                4_000_000L
                ResearchValue.Unknown

        let toolTwo =
            metricEvent
                4
                "tool.finished"
                $"{{\"toolClass\":\"test\",\"commandDigest\":\"{sha 'd'}\",\"startedMonotonicNs\":2000000,\"completedMonotonicNs\":6000000,\"resultSha256\":\"{sha 'e'}\"}}"
                6_000_000L
                ResearchValue.Unknown

        let intervention =
            metricEvent
                5
                "research.intervention.recorded"
                $"{{\"interventionId\":\"INT-1\",\"category\":\"I1-clarification\",\"decisionActSha256\":\"{sha 'a'}\",\"counted\":true,\"classificationReason\":\"fixture\",\"durationMs\":\"unknown\"}}"
                7_000_000L
                ResearchValue.Unknown

        let duplicateDecision =
            metricEvent
                6
                "research.intervention.recorded"
                $"{{\"interventionId\":\"INT-2\",\"category\":\"I1-clarification\",\"decisionActSha256\":\"{sha 'a'}\",\"counted\":true,\"classificationReason\":\"fixture\",\"durationMs\":\"unknown\"}}"
                8_000_000L
                ResearchValue.Unknown

        [ "paired gate", [ gateStart; gateFinish ], "GATE-ATTEMPTS-TOTAL", "1"
          "orphan gate", [ gateStart ], "GATE-ATTEMPTS-TOTAL", ResearchContract.Unknown
          "interval union", [ toolOne; toolTwo ], "TOOL-ACTIVE-MS", "6"
          "decision dedup", [ intervention; duplicateDecision ], "INT-COUNT", "1"
          "missing receipt identity", [ toolOne ], "USE-INPUT-TOKENS", ResearchContract.Unknown ]
        |> List.iter (fun (name, events, metricId, expected) ->
            assertEqual
                expected
                (metric metricId (ResearchMetrics.calculate events None None)).Value
                $"{name} did not follow the frozen metric contract")

    let exportsAreByteIdenticalAndTamperEvident () =
        withWorkspace (fun root ->
            let observation = id "OBS-" 3
            let manifest = writeManifest root observation
            let sourceHash = Internal.sha256Hex (ResearchCanonical.canonicalizeJson "[]")
            let studyHash = (ResearchExport.loadStudyManifest root manifest).ManifestSha256
            appendClosedFixture root observation sourceHash studyHash |> ignore
            let first = ".ai/runtime/research/exports/first"
            let second = ".ai/runtime/research/exports/second"
            ResearchExport.export root manifest first |> ignore
            ResearchExport.export root manifest second |> ignore
            let firstFiles = filesUnder (Path.Combine(root, first))
            let secondFiles = filesUnder (Path.Combine(root, second))
            assertEqual firstFiles secondFiles "Repeated export bytes differ"
            let receipt = ResearchExport.verifyExport root first

            ResearchExport.verifyExportWithExpectedReceipt root first (Some receipt)
            |> ignore

            expectFailure "EXPORT_RECEIPT_MISMATCH" (fun () ->
                ResearchExport.verifyExportWithExpectedReceipt root first (Some(sha '0'))
                |> ignore)

            let originalManifest = File.ReadAllText(manifest, Constants.Utf8NoBom)

            File.WriteAllText(
                manifest,
                originalManifest.Replace(
                    "\"actorIdentityRule\":\"test\"",
                    "\"actorIdentityRule\":\"changed-after-close\"",
                    StringComparison.Ordinal
                ),
                Constants.Utf8NoBom
            )

            let changedOutput = ".ai/runtime/research/exports/changed-manifest"

            expectFailure "STUDY_MANIFEST_BINDING_INVALID" (fun () ->
                ResearchExport.export root manifest changedOutput |> ignore)

            assertTrue
                (not (Directory.Exists(Path.Combine(root, changedOutput))))
                "A changed post-closure manifest created an export directory"

            expectFailure "STUDY_MANIFEST_BINDING_INVALID" (fun () ->
                ResearchCli.execute root [ "verify"; "--study-manifest"; manifest ] |> ignore)

            let summary = Path.Combine(root, first, "summary.json")
            File.AppendAllText(summary, "x", Constants.Utf8NoBom)
            expectFailure "EXPORT_HASH_INVALID" (fun () -> ResearchExport.verifyExport root first |> ignore))

    let studyManifestRejectsUndocumentedSensitiveFields () =
        withWorkspace (fun root ->
            let observation = id "OBS-" 4
            let manifest = writeManifest root observation
            let original = File.ReadAllText(manifest, Constants.Utf8NoBom)

            for field, value in
                [ "apiToken", "sk-private-value"
                  "userEmail", "alice@example.invalid"
                  "privateNotes", "/Users/alice/secret.txt" ] do
                File.WriteAllText(
                    manifest,
                    original.Substring(0, original.Length - 1) + $",\"{field}\":\"{value}\"}}",
                    Constants.Utf8NoBom
                )

                expectFailure "RESEARCH_MANIFEST_INVALID" (fun () ->
                    ResearchExport.loadStudyManifest root manifest |> ignore)

                let output = $".ai/runtime/research/exports/privacy-{field}"

                expectFailure "RESEARCH_MANIFEST_INVALID" (fun () ->
                    ResearchExport.export root manifest output |> ignore)

                assertTrue
                    (not (Directory.Exists(Path.Combine(root, output))))
                    $"Sensitive unknown field {field} reached an export directory"

            let longitudinal =
                original.Substring(0, original.Length - 1)
                + ",\"windowEndUtc\":\"2026-09-03T10:00:00.000Z\",\"windowStartUtc\":\"2026-09-02T10:00:00.000Z\"}"

            File.WriteAllText(manifest, longitudinal, Constants.Utf8NoBom)

            let accepted = ResearchExport.loadStudyManifest root manifest
            assertTrue accepted.WindowStartUtc.IsSome "Documented windowStartUtc was rejected"
            assertTrue accepted.WindowEndUtc.IsSome "Documented windowEndUtc was rejected")

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

        if child.ExitCode <> 0 then
            failwith $"Temporary Git fixture failed: {error}"

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

            [ "uppercase-object", String('A', 40); "moving-name", "HEAD" ]
            |> List.iter (fun (_, invalid) ->
                expectFailure "Commitgrenze" (fun () -> ResearchGitImport.read root baseline invalid |> ignore)))

    let retrospectiveImportKeepsHumanAndUsageUnknown () =
        withWorkspace (fun root ->
            let baseline, head = gitFixture root
            let output = ".ai/runtime/research/imports/history.json"

            let exitCode =
                ResearchCli.execute
                    root
                    [ "import-git-history"
                      "--task"
                      "T-053"
                      "--base"
                      baseline
                      "--head"
                      head
                      "--output"
                      output ]

            assertEqual 0 exitCode "Retrospective import failed"
            use document = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, output)))

            for name in
                [ "inputTokens"
                  "outputTokens"
                  "costAmount"
                  "costCurrency"
                  "humanActiveDurationMs" ] do
                assertEqual
                    ResearchContract.Unknown
                    (document.RootElement.GetProperty(name).GetString())
                    $"Retrospective import fabricated {name}"

            let mutable unexpectedCalibration = Unchecked.defaultof<JsonElement>

            assertTrue
                (not (document.RootElement.TryGetProperty("calibration", &unexpectedCalibration)))
                "Raw import unexpectedly gained a calibration field"

            let history = ResearchGitImport.read root baseline head

            let expected =
                Internal.jsonBytes false (fun writer ->
                    writer.WriteStartObject()
                    writer.WriteString("baseCommit", history.BaseCommit)
                    writer.WriteStartArray("commits")

                    for imported in history.Commits do
                        writer.WriteStartObject()
                        writer.WriteString("commitId", imported.CommitId)
                        writer.WriteString("commitObjectSha256", imported.CommitObjectSha256)
                        writer.WriteString("commitTimeUtc", imported.CommitTimeUtc)
                        writer.WriteStartArray("parentCommitIds")
                        imported.ParentCommitIds |> List.iter writer.WriteStringValue
                        writer.WriteEndArray()
                        writer.WriteString("treeId", imported.TreeId)
                        writer.WriteEndObject()

                    writer.WriteEndArray()
                    writer.WriteString("costAmount", ResearchContract.Unknown)
                    writer.WriteString("costCurrency", ResearchContract.Unknown)
                    writer.WriteString("evidenceClass", "retrospective-derived")
                    writer.WriteString("headCommit", history.HeadCommit)
                    writer.WriteString("humanActiveDurationMs", ResearchContract.Unknown)
                    writer.WriteString("inputTokens", ResearchContract.Unknown)
                    writer.WriteString("objectFormat", history.ObjectFormat)
                    writer.WriteString("outputTokens", ResearchContract.Unknown)
                    writer.WriteNumber("schemaVersion", ResearchContract.SchemaVersion)
                    writer.WriteString("studyId", ResearchContract.StudyId)
                    writer.WriteString("targetTaskId", "T-053")
                    writer.WriteEndObject())
                |> Constants.Utf8NoBom.GetString
                |> ResearchCanonical.canonicalizeJson

            assertTrue
                (expected.AsSpan().SequenceEqual(ReadOnlySpan<byte>(File.ReadAllBytes(Path.Combine(root, output)))))
                "Raw import bytes differ from the pre-calibration contract")

    type private CalibrationFixture =
        { BaseCommit: string
          BaseTree: string
          ReadyCommit: string
          ReviewCommit: string
          ReviewTree: string
          ContaminatedCommit: string
          ContaminatedTree: string
          LaterCommit: string
          LaterTree: string
          HeadManifest: ResearchGitBlob
          ReadySnapshot: ResearchGitBlob
          HeadReview: ResearchGitBlob
          AcceptedManifest: ResearchGitBlob
          Reconciliation: ResearchGitBlob
          Audit: ResearchGitBlob }

    let private writeRelative root relativePath (text: string) =
        let path = Path.Combine(root, relativePath)
        Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore
        File.WriteAllText(path, text, Constants.Utf8NoBom)

    let private commitAll root message =
        git root [ "add"; "--all" ] |> ignore
        git root [ "commit"; "--quiet"; "-m"; message ] |> ignore
        git root [ "rev-parse"; "HEAD" ]

    let private calibrationFixture root =
        git root [ "init"; "--quiet" ] |> ignore
        git root [ "config"; "user.email"; "fixture@example.invalid" ] |> ignore
        git root [ "config"; "user.name"; "fixture" ] |> ignore
        writeRelative root "seed.txt" "base\n"
        let baseCommit = commitAll root "base"
        let baseTree = ResearchGitImport.treeAt root baseCommit
        let manifestPath = ".ai/tasks/T-037-fixture.json"
        let readySnapshotPath = ".ai/tasks/T-037-ready-snapshot.json"
        let reviewPath = "docs/abnahme/T-037-fixture.md"
        writeRelative root manifestPath "{\"status\":\"ready\"}\n"
        writeRelative root readySnapshotPath "{\"status\":\"ready\"}\n"
        let readyCommit = commitAll root "T-037 ready"
        writeRelative root manifestPath "{\"status\":\"review\"}\n"
        writeRelative root reviewPath "review-state evidence\n"
        let reviewCommit = commitAll root "T-037 review"
        let reviewTree = ResearchGitImport.treeAt root reviewCommit
        let headManifest = ResearchGitImport.blobAtCommit root reviewCommit manifestPath
        let headReview = ResearchGitImport.blobAtCommit root reviewCommit reviewPath

        writeRelative root "foreign.txt" "unrelated change\n"
        let contaminatedCommit = commitAll root "foreign commit"
        let contaminatedTree = ResearchGitImport.treeAt root contaminatedCommit
        writeRelative root manifestPath "{\"status\":\"accepted\"}\n"

        writeRelative
            root
            "docs/showcase/reconciliation.json"
            $"""{{"receipts":[{{"baseSha":"{readyCommit}","mergeSha":"{reviewCommit}","outcome":"success","resultSha":"{reviewCommit}","resultTree":"{reviewTree}","reviewEvidenceBlobOid":"{headReview.BlobOid}","roleSeparation":"not-publicly-proven","taskId":"T-037","taskManifestBlobOid":"{headManifest.BlobOid}"}}]}}"""

        writeRelative
            root
            ".ai/audits/T-054-fixture.json"
            "{\"coveredTaskIds\":[\"T-037\"],\"criteria\":\"PASS\",\"historicalRoleSeparation\":\"not-publicly-proven\"}\n"

        let laterCommit = commitAll root "later acceptance reconciliation"
        let laterTree = ResearchGitImport.treeAt root laterCommit

        { BaseCommit = baseCommit
          BaseTree = baseTree
          ReadyCommit = readyCommit
          ReviewCommit = reviewCommit
          ReviewTree = reviewTree
          ContaminatedCommit = contaminatedCommit
          ContaminatedTree = contaminatedTree
          LaterCommit = laterCommit
          LaterTree = laterTree
          HeadManifest = headManifest
          ReadySnapshot = ResearchGitImport.blobAtCommit root reviewCommit readySnapshotPath
          HeadReview = headReview
          AcceptedManifest = ResearchGitImport.blobAtCommit root laterCommit manifestPath
          Reconciliation = ResearchGitImport.blobAtCommit root laterCommit "docs/showcase/reconciliation.json"
          Audit = ResearchGitImport.blobAtCommit root laterCommit ".ai/audits/T-054-fixture.json" }

    let private calibrationSpec (fixture: CalibrationFixture) =
        $"""{{
  "baseCommit": "{fixture.BaseCommit}",
  "baseTreeId": "{fixture.BaseTree}",
  "calibrationId": "R-001",
  "evidenceClass": "retrospective-derived",
  "expectedCommitIds": ["{fixture.ReadyCommit}", "{fixture.ReviewCommit}"],
  "headCommit": "{fixture.ReviewCommit}",
  "headManifest": {{"blobOid":"{fixture.HeadManifest.BlobOid}","kind":"task-manifest","path":".ai/tasks/T-037-fixture.json","sha256":"{fixture.HeadManifest.Sha256}"}},
  "headManifestStatus": "review",
  "headReviewEvidence": {{"blobOid":"{fixture.HeadReview.BlobOid}","kind":"review-receipt","path":"docs/abnahme/T-037-fixture.md","sha256":"{fixture.HeadReview.Sha256}"}},
  "headTreeId": "{fixture.ReviewTree}",
  "laterLifecycle": {{
    "acceptedManifest": {{"blobOid":"{fixture.AcceptedManifest.BlobOid}","kind":"task-manifest","path":".ai/tasks/T-037-fixture.json","sha256":"{fixture.AcceptedManifest.Sha256}"}},
    "acceptedManifestStatus": "accepted",
    "auditEvidence": {{"blobOid":"{fixture.Audit.BlobOid}","kind":"review-receipt","path":".ai/audits/T-054-fixture.json","sha256":"{fixture.Audit.Sha256}"}},
    "reconciliationEvidence": {{"blobOid":"{fixture.Reconciliation.BlobOid}","kind":"review-receipt","path":"docs/showcase/reconciliation.json","sha256":"{fixture.Reconciliation.Sha256}"}},
    "relation": "git.supersession.observed",
    "supersededCommit": "{fixture.ReviewCommit}",
    "supersedingCommit": "{fixture.LaterCommit}",
    "supersedingTreeId": "{fixture.LaterTree}"
  }},
  "schemaVersion": 1,
  "targetTaskId": "T-037"
}}"""

    let private importCalibration root baseCommit headCommit spec output =
        ResearchCli.execute
            root
            [ "import-git-history"
              "--task"
              "T-037"
              "--base"
              baseCommit
              "--head"
              headCommit
              "--calibration-spec"
              spec
              "--output"
              output ]

    let retrospectiveCalibrationIsExactUnknownAndDeterministic () =
        withWorkspace (fun root ->
            let fixture = calibrationFixture root
            let spec = "calibration.json"
            writeRelative root spec (calibrationSpec fixture)
            let first = ".ai/runtime/research/imports/R-001-first.json"
            let second = ".ai/runtime/research/imports/R-001-second.json"

            assertEqual
                0
                (importCalibration root fixture.BaseCommit fixture.ReviewCommit spec first)
                "First calibration import failed"

            assertEqual
                0
                (importCalibration root fixture.BaseCommit fixture.ReviewCommit spec second)
                "Second calibration import failed"

            let firstBytes = File.ReadAllBytes(Path.Combine(root, first))
            let secondBytes = File.ReadAllBytes(Path.Combine(root, second))
            assertTrue (firstBytes.AsSpan().SequenceEqual(secondBytes)) "Calibration imports were not byte-identical"
            use document = JsonDocument.Parse(firstBytes)
            let calibration = document.RootElement.GetProperty("calibration")

            assertEqual
                "retrospective-derived"
                (document.RootElement.GetProperty("evidenceClass").GetString())
                "Evidence class changed"

            assertEqual
                "review"
                (calibration.GetProperty("headManifestStatus").GetString())
                "Review state was retrodicted"

            assertEqual
                "accepted"
                (calibration.GetProperty("laterLifecycle").GetProperty("acceptedManifestStatus").GetString())
                "Later acceptance was omitted"

            assertEqual
                "git.supersession.observed"
                (calibration.GetProperty("laterLifecycle").GetProperty("relation").GetString())
                "Lifecycle relation changed"

            assertEqual
                "not-publicly-proven"
                (calibration.GetProperty("historicalRoleSeparation").GetString())
                "Role-separation limitation changed"

            for name in
                [ "actorId"
                  "actorRole"
                  "agentActiveDurationMs"
                  "autonomousDurationMs"
                  "cacheReadTokens"
                  "cacheWriteTokens"
                  "costProvenance"
                  "elapsedDurationMs"
                  "identityAssurance"
                  "interventionCount"
                  "interventionDurationMs"
                  "modelId"
                  "modelVersion"
                  "providerId"
                  "requestCount"
                  "taskOutcome"
                  "usageProvenance" ] do
                assertEqual
                    ResearchContract.Unknown
                    (calibration.GetProperty(name).GetString())
                    $"Calibration fabricated {name}")

    let retrospectiveCalibrationRejectsContaminationRetrodatingAndTampering () =
        withWorkspace (fun root ->
            let fixture = calibrationFixture root
            let valid = calibrationSpec fixture

            let contaminated =
                valid.Replace(
                    $"\"headCommit\": \"{fixture.ReviewCommit}\"",
                    $"\"headCommit\": \"{fixture.ContaminatedCommit}\"",
                    StringComparison.Ordinal
                )
                |> fun value ->
                    value.Replace(
                        $"\"headTreeId\": \"{fixture.ReviewTree}\"",
                        $"\"headTreeId\": \"{fixture.ContaminatedTree}\"",
                        StringComparison.Ordinal
                    )
                |> fun value ->
                    value.Replace(
                        $"\"supersededCommit\": \"{fixture.ReviewCommit}\"",
                        $"\"supersededCommit\": \"{fixture.ContaminatedCommit}\"",
                        StringComparison.Ordinal
                    )

            writeRelative root "contaminated.json" contaminated

            expectFailure "ordered commit list" (fun () ->
                importCalibration
                    root
                    fixture.BaseCommit
                    fixture.ContaminatedCommit
                    "contaminated.json"
                    ".ai/runtime/research/imports/contaminated.json"
                |> ignore)

            assertTrue
                (not (File.Exists(Path.Combine(root, ".ai/runtime/research/imports/contaminated.json"))))
                "Contaminated range wrote output"

            let retrodating =
                valid.Replace(
                    $"\"supersedingCommit\": \"{fixture.LaterCommit}\"",
                    $"\"supersedingCommit\": \"{fixture.ReviewCommit}\"",
                    StringComparison.Ordinal
                )
                |> fun value ->
                    value.Replace(
                        $"\"supersedingTreeId\": \"{fixture.LaterTree}\"",
                        $"\"supersedingTreeId\": \"{fixture.ReviewTree}\"",
                        StringComparison.Ordinal
                    )

            writeRelative root "retrodating.json" retrodating

            expectFailure "later acceptance cannot be dated" (fun () ->
                importCalibration
                    root
                    fixture.BaseCommit
                    fixture.ReviewCommit
                    "retrodating.json"
                    ".ai/runtime/research/imports/retrodating.json"
                |> ignore)

            let wrongHash = String('0', 64)

            let tampered =
                valid.Replace(fixture.HeadManifest.Sha256, wrongHash, StringComparison.Ordinal)

            writeRelative root "tampered.json" tampered

            expectFailure "SHA-256" (fun () ->
                importCalibration
                    root
                    fixture.BaseCommit
                    fixture.ReviewCommit
                    "tampered.json"
                    ".ai/runtime/research/imports/tampered.json"
                |> ignore)

            let wrongDeclaredStatus =
                valid.Replace(
                    "\"headManifestStatus\": \"review\"",
                    "\"headManifestStatus\": \"accepted\"",
                    StringComparison.Ordinal
                )

            writeRelative root "wrong-declared-status.json" wrongDeclaredStatus

            expectFailure "head manifest status contract" (fun () ->
                importCalibration
                    root
                    fixture.BaseCommit
                    fixture.ReviewCommit
                    "wrong-declared-status.json"
                    ".ai/runtime/research/imports/wrong-declared-status.json"
                |> ignore)

            let wrongActualStatus =
                valid.Replace(fixture.HeadManifest.BlobOid, fixture.ReadySnapshot.BlobOid, StringComparison.Ordinal)
                |> fun value ->
                    value.Replace(
                        ".ai/tasks/T-037-fixture.json",
                        ".ai/tasks/T-037-ready-snapshot.json",
                        StringComparison.Ordinal
                    )
                |> fun value ->
                    value.Replace(fixture.HeadManifest.Sha256, fixture.ReadySnapshot.Sha256, StringComparison.Ordinal)

            writeRelative root "wrong-actual-status.json" wrongActualStatus

            expectFailure "head manifest status" (fun () ->
                importCalibration
                    root
                    fixture.BaseCommit
                    fixture.ReviewCommit
                    "wrong-actual-status.json"
                    ".ai/runtime/research/imports/wrong-actual-status.json"
                |> ignore))

    let architectureAblationsStayDiagnostic () =
        let baseInput files =
            { CheckpointId = "checkpoint"
              BaselineCommit = commit 'a'
              ResultCommit = commit 'b'
              AcceptedTaskId = "T-053"
              AcceptedTreeId = commit 'c'
              PathMapVersion = "v1"
              PathMap =
                [ { Prefix = "src"
                    FileClass = "production"
                    Component = "app" } ]
              Files = files
              BaselineReferences = []
              ResultReferences = []
              Findings = []
              AnalyzerReceipt = None
              TestReceipt = None
              BaselineTestReceipt = None
              ComplexityReceipt = None }

        [ "binary",
          baseInput
              [ { Path = "src/asset.bin"
                  BaselinePath = None
                  ResultLines = Some 4
                  BaselineLines = Some 1
                  IsBinary = true
                  SourceSha256 = sha 'a'
                  Changed = true } ],
          "unknown",
          "production",
          false
          "empty-map",
          { baseInput
                [ { Path = "src/a.cs"
                    BaselinePath = None
                    ResultLines = Some 4
                    BaselineLines = Some 1
                    IsBinary = false
                    SourceSha256 = sha 'b'
                    Changed = true } ] with
              PathMap = [] },
          "4",
          "unknown",
          false ]
        |> List.iter (fun (name, input, expectedLines, expectedClass, expectedGateCoupled) ->
            let snapshot = ResearchArchitecture.create input
            assertEqual expectedLines snapshot.FileRows.Head.Lines $"{name} did not preserve unknown"
            assertEqual expectedClass snapshot.FileRows.Head.FileClass $"{name} inferred an unsupported class"
            assertEqual expectedGateCoupled snapshot.GateCoupled $"{name} became gate-coupled")

    let all =
        [ "research metrics preserve unknown denominators and missing usage",
          metricsRemainUnknownWithoutStructuredEvidence
          "research metrics keep evidence classes separated", evidenceClassesStaySeparatedInMetrics
          "research metrics require paired facts and interval unions", metricsRequireBoundFactsAndUseExactUnions
          "research exports are deterministic and tamper-evident", exportsAreByteIdenticalAndTamperEvident
          "research study manifest rejects undocumented sensitive fields",
          studyManifestRejectsUndocumentedSensitiveFields
          "research git import rejects malformed and moving boundaries", gitBoundariesRejectMalformedAndMovingNames
          "research retrospective import preserves unknown usage and human duration",
          retrospectiveImportKeepsHumanAndUsageUnknown
          "research retrospective calibration is exact, unknown, and deterministic",
          retrospectiveCalibrationIsExactUnknownAndDeterministic
          "research retrospective calibration rejects contamination, retrodating, and tampering",
          retrospectiveCalibrationRejectsContaminationRetrodatingAndTampering
          "research architecture binary and path-map ablations stay diagnostic", architectureAblationsStayDiagnostic ]
