namespace RiftHarness.Tests

open System
open System.Diagnostics
open System.IO
open System.Text.Json
open RiftHarness

module private Assert =
    let equal expected actual message =
        if not (Unchecked.equals expected actual) then
            failwith $"{message} Erwartet: {expected}; erhalten: {actual}"

    let isTrue condition message =
        if not condition then
            failwith message

    let harnessFailureContains (expected: string) (action: unit -> unit) (message: string) =
        let mutable failure: string option = None

        try
            action ()
        with HarnessException value ->
            failure <- Some value

        match failure with
        | Some value when value.Contains(expected, StringComparison.Ordinal) -> ()
        | Some value -> failwith $"{message} Fehler war: {value}"
        | None -> failwith $"{message} HarnessException blieb aus."

module private TestWorkspace =
    let run action =
        let root =
            Path.Combine(Path.GetTempPath(), "RiftHarness.Tests-" + Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory(root) |> ignore

        try
            action root
        finally
            if Directory.Exists(root) then
                Directory.Delete(root, true)

module private Tests =
    let private repositoryRoot =
        let rec findRoot path =
            if File.Exists(Path.Combine(path, "Riftward.slnx")) then
                path
            else
                let parent = Directory.GetParent(path)

                if isNull parent then
                    failwith "Repository-Wurzel nicht gefunden."

                findRoot parent.FullName

        findRoot Environment.CurrentDirectory

    let private copyAssetContract targetRoot =
        let copyFile relative =
            let source = Path.Combine(repositoryRoot, relative)
            let target = Path.Combine(targetRoot, relative)
            Directory.CreateDirectory(Path.GetDirectoryName(target)) |> ignore
            File.Copy(source, target, true)

        for relative in
            [ ".gitattributes"
              ".gitignore"
              ".ai/config.json"
              ".ai/policies/asset-clean-room.json"
              ".ai/schemas/asset-clean-room-policy.schema.json"
              ".ai/schemas/asset-manifest.schema.json"
              ".ai/schemas/generation-receipt.schema.json"
              ".ai/schemas/asset-review-evidence.schema.json"
              ".ai/schemas/models-lock.schema.json"
              "models.lock.json" ] do
            copyFile relative

        let sourceManifest =
            Path.Combine(repositoryRoot, "assets/manifests/ENV-FLOODED-CAUSEWAY-KEYFRAME-002.json")

        let manifestText = File.ReadAllText(sourceManifest, Constants.Utf8NoBom)
        use manifest = JsonDocument.Parse(manifestText)
        let root = manifest.RootElement

        for input in root.GetProperty("inputs").EnumerateArray() do
            if input.GetProperty("path").ValueKind = JsonValueKind.String then
                copyFile (input.GetProperty("path").GetString())

        let receipt = root.GetProperty("generationReceipt").GetString()
        copyFile receipt

        let output =
            root.GetProperty("outputs").EnumerateArray()
            |> Seq.head
            |> fun item -> item.GetProperty("path").GetString()

        let sourceOutput = Path.Combine(repositoryRoot, output)

        if File.Exists(sourceOutput) then
            copyFile output


        Directory.CreateDirectory(Path.Combine(targetRoot, "assets", "manifests"))
        |> ignore

        File.WriteAllText(
            Path.Combine(targetRoot, "assets", "manifests", "fixture.json"),
            manifestText,
            Constants.Utf8NoBom
        )

        let startInfo = ProcessStartInfo("git")
        startInfo.WorkingDirectory <- targetRoot
        startInfo.UseShellExecute <- false
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        startInfo.ArgumentList.Add("init")
        startInfo.ArgumentList.Add("--quiet")
        use child = Process.Start(startInfo)
        child.WaitForExit()

        if child.ExitCode <> 0 then
            failwith "Temporäres Assetfixture-Git konnte nicht initialisiert werden."

    let assetRepositoryQuarantineFixturesAreValid () =
        let report =
            AssetStore.check
                repositoryRoot
                { ManifestPath = None
                  RequireLocal = false
                  RequireApproved = false }

        Assert.isTrue report.Valid $"Repository-Assetfixtures ungueltig: {AssetStore.reportJson report}"
        Assert.equal 6 report.ManifestsChecked "Repository-Assetfixturezahl ist falsch."
        Assert.equal 6 report.QuarantineCount "Repository-Assets bleiben nicht geschlossen in Quarantaene."
        Assert.isTrue (not report.ShippingReady) "Quarantaene-Keyframes wurden shipping-faehig gemeldet."

    let assetSchemaIsStrictOfflineAndShortCircuitsCrossFields () =
        TestWorkspace.run (fun root ->
            Workspace.initialize root |> ignore
            copyAssetContract root
            let manifestPath = Path.Combine(root, "assets", "manifests", "fixture.json")

            let valid =
                AssetStore.check
                    root
                    { ManifestPath = Some "assets/manifests/fixture.json"
                      RequireLocal = false
                      RequireApproved = false }

            Assert.isTrue valid.Valid $"Gueltiges kopiertes Manifest wurde abgelehnt: {AssetStore.reportJson valid}"

            let text = File.ReadAllText(manifestPath, Constants.Utf8NoBom)

            let duplicate =
                text.Replace(
                    "\"schemaVersion\": 1,",
                    "\"schemaVersion\": 1,\n  \"schemaVersion\": 1,",
                    StringComparison.Ordinal
                )

            File.WriteAllText(manifestPath, duplicate, Constants.Utf8NoBom)

            let duplicateReport =
                AssetStore.check
                    root
                    { ManifestPath = Some "assets/manifests/fixture.json"
                      RequireLocal = false
                      RequireApproved = false }

            Assert.isTrue (not duplicateReport.Valid) "Doppelter JSON-Key wurde akzeptiert."

            File.WriteAllText(
                manifestPath,
                text.Replace("2026-08-13T14:01:00Z", "not-a-date", StringComparison.Ordinal),
                Constants.Utf8NoBom
            )

            let dateReport =
                AssetStore.check
                    root
                    { ManifestPath = Some "assets/manifests/fixture.json"
                      RequireLocal = false
                      RequireApproved = false }

            Assert.isTrue
                (not dateReport.Valid
                 && dateReport.Findings
                    |> List.exists (fun finding -> finding.Code = "ASSET_SCHEMA_INVALID"))
                "Ungültiges date-time-Format wurde akzeptiert oder Cross-Field-Parser crashte.")

    let assetCleanRoomFindingsAreRedactedAndRequireFlagsWork () =
        TestWorkspace.run (fun root ->
            Workspace.initialize root |> ignore
            copyAssetContract root
            let manifestPath = Path.Combine(root, "assets", "manifests", "fixture.json")
            let marker = String.Join(" ", [ "synthetic"; "forbidden"; "proper"; "noun" ])
            let text = File.ReadAllText(manifestPath, Constants.Utf8NoBom)

            File.WriteAllText(
                manifestPath,
                text.Replace("quiet exploration", marker, StringComparison.Ordinal),
                Constants.Utf8NoBom
            )

            let denied =
                AssetStore.check
                    root
                    { ManifestPath = Some "assets/manifests/fixture.json"
                      RequireLocal = false
                      RequireApproved = false }

            let deniedJson = AssetStore.reportJson denied

            Assert.isTrue
                (not denied.Valid
                 && denied.Findings
                    |> List.exists (fun finding -> finding.Code = "CLEAN_ROOM_DENIED_NAME"))
                "Künstlicher Deny-Marker wurde nicht blockiert."

            Assert.isTrue
                (not (deniedJson.Contains(marker, StringComparison.Ordinal)))
                "Deny-Inhalt wurde in Finding-JSON vervielfaeltigt."

            File.WriteAllText(manifestPath, text, Constants.Utf8NoBom)

            let local =
                AssetStore.check
                    root
                    { ManifestPath = Some "assets/manifests/fixture.json"
                      RequireLocal = true
                      RequireApproved = false }

            Assert.isTrue
                (not local.Valid
                 && local.Findings
                    |> List.exists (fun finding ->
                        finding.Code = "ASSET_OUTPUT_MISSING"
                        || finding.Code = "ASSET_GENERATION_RUN_MISSING"))
                "--require-local hatte keine Wirkung."

            let approved =
                AssetStore.check
                    root
                    { ManifestPath = Some "assets/manifests/fixture.json"
                      RequireLocal = false
                      RequireApproved = true }

            Assert.isTrue
                (not approved.Valid
                 && approved.Findings
                    |> List.exists (fun finding -> finding.Code = "ASSET_APPROVAL_REQUIRED"))
                "--require-approved hatte keine Wirkung.")

    let assetReceiptBindsAllCoreAnchors () =
        TestWorkspace.run (fun root ->
            Workspace.initialize root |> ignore
            copyAssetContract root

            let receiptPath =
                Directory.EnumerateFiles(
                    Path.Combine(root, "assets", "receipts"),
                    "*.json",
                    SearchOption.AllDirectories
                )
                |> Seq.exactlyOne

            let original = File.ReadAllText(receiptPath, Constants.Utf8NoBom)

            for before, after in
                [ "\"sequence\":1", "\"sequence\":2"
                  "\"status\": \"succeeded\"", "\"status\": \"failed\""
                  "\"summaryHash\": \"", "\"summaryHash\": \"0"
                  "assets/specs/", "assets/specs/tampered-" ] do
                File.WriteAllText(
                    receiptPath,
                    original.Replace(before, after, StringComparison.Ordinal),
                    Constants.Utf8NoBom
                )

                let report =
                    AssetStore.check
                        root
                        { ManifestPath = Some "assets/manifests/fixture.json"
                          RequireLocal = false
                          RequireApproved = false }

                Assert.isTrue
                    (not report.Valid
                     && report.Findings
                        |> List.exists (fun finding ->
                            finding.Code = "ASSET_RECEIPT_HASH_INVALID"
                            || finding.Code = "ASSET_RECEIPT_SCHEMA_INVALID"))
                    $"Receipt-Kernanker blieb ungebunden: {before}"

            File.WriteAllText(receiptPath, original, Constants.Utf8NoBom))

    let runIdsAreTimeSortable () =
        let first =
            Internal.createRunId (DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_000L))

        let second =
            Internal.createRunId (DateTimeOffset.FromUnixTimeMilliseconds(1_700_000_000_001L))

        Assert.equal 26 first.Length "Run-ID hat falsche Laenge."
        Assert.isTrue (StringComparer.Ordinal.Compare(first, second) < 0) "Run-IDs sind nicht nach Zeit sortierbar."
        Assert.isTrue (Internal.isRunId first) "Erzeugte Run-ID wird nicht akzeptiert."

    let runLifecycleIsHashedAndRedacted () =
        TestWorkspace.run (fun root ->
            let locations = Workspace.initialize root
            let originalConfig = File.ReadAllText(locations.Config)
            Workspace.initialize root |> ignore
            Assert.equal originalConfig (File.ReadAllText(locations.Config)) "init hat config.json ueberschrieben."

            let runId = RunStore.start root
            let payloadPath = Path.Combine(root, "payload.json")

            File.WriteAllText(
                payloadPath,
                """{"message":"started","nested":{"apiKey":"must-not-survive"}}""",
                Constants.Utf8NoBom
            )

            let first = RunStore.append root runId "agent.started" payloadPath
            File.WriteAllText(payloadPath, """{"message":"working"}""", Constants.Utf8NoBom)
            let second = RunStore.append root runId "agent.progress" payloadPath
            Assert.equal 1L first.Sequence "Erste Event-Sequenz ist falsch."
            Assert.equal 2L second.Sequence "Zweite Event-Sequenz ist falsch."

            let runPath = Path.Combine(locations.Runs, runId)
            let eventText = File.ReadAllText(Path.Combine(runPath, "events.jsonl"))

            Assert.isTrue
                (not (eventText.Contains("must-not-survive", StringComparison.Ordinal)))
                "Secret wurde protokolliert."

            Assert.isTrue (eventText.Contains("[REDACTED]", StringComparison.Ordinal)) "Secret-Platzhalter fehlt."

            let lines = File.ReadAllLines(Path.Combine(runPath, "events.jsonl"))
            use secondDocument = JsonDocument.Parse(lines[1])

            Assert.equal
                first.EventHash
                (secondDocument.RootElement.GetProperty("previousEventHash").GetString())
                "Event-Hashkette ist falsch."

            let summaryPath = Path.Combine(root, "finish.json")
            File.WriteAllText(summaryPath, """{"result":"ok","token":"also-secret"}""", Constants.Utf8NoBom)
            let finish = RunStore.finish root runId "succeeded" (Some summaryPath)
            Assert.equal 3L finish.EventCount "Abschluss-Event fehlt."

            let report = Verification.verify root (Some runId)
            let runErrors = String.concat "; " report.Errors
            Assert.isTrue report.Valid $"Gueltiger Run wurde abgelehnt: {runErrors}"

            let summaryText = File.ReadAllText(Path.Combine(runPath, "summary.json"))

            Assert.isTrue
                (not (summaryText.Contains("also-secret", StringComparison.Ordinal)))
                "Summary-Secret wurde protokolliert."

            let tampered =
                eventText.Replace("agent.started", "agent.changed", StringComparison.Ordinal)

            File.WriteAllText(Path.Combine(runPath, "events.jsonl"), tampered, Constants.Utf8NoBom)
            let tamperedReport = Verification.verify root (Some runId)
            Assert.isTrue (not tamperedReport.Valid) "Manipulierte Eventkette wurde akzeptiert.")

    let eventEnvelopeIsStrictAndPayloadRemainsStructured () =
        TestWorkspace.run (fun root ->
            let locations = Workspace.initialize root
            let payloadPath = Path.Combine(root, "payload.json")

            File.WriteAllText(
                payloadPath,
                """{
  "text": "value",
  "number": 42.5,
  "boolean": true,
  "nothing": null,
  "array": [1, "two", false, null, { "nested": "ok" }]
}
""",
                Constants.Utf8NoBom
            )

            let runId = RunStore.start root
            RunStore.append root runId "strict.fixture" payloadPath |> ignore
            let validReport = Verification.verify root (Some runId)
            Assert.isTrue validReport.Valid "Legitimer strukturierter Event-Payload wurde abgelehnt."

            let eventsPath = Path.Combine(locations.Runs, runId, "events.jsonl")
            let originalLine = File.ReadAllLines(eventsPath)[0]

            let withExtra =
                originalLine.Insert(originalLine.Length - 1, ",\"tamperedExtra\":true")

            File.WriteAllText(eventsPath, withExtra + "\n", Constants.Utf8NoBom)
            let extraReport = Verification.verify root (Some runId)

            Assert.isTrue
                (not extraReport.Valid
                 && (extraReport.Errors
                     |> List.exists (fun error -> error.Contains("tamperedExtra", StringComparison.Ordinal))))
                "Zusaetzliches Event-Top-Level-Feld wurde akzeptiert."

            let withDuplicate = originalLine.Insert(1, "\"schemaVersion\":1,")

            File.WriteAllText(eventsPath, withDuplicate + "\n", Constants.Utf8NoBom)
            let duplicateReport = Verification.verify root (Some runId)

            Assert.isTrue
                (not duplicateReport.Valid
                 && (duplicateReport.Errors
                     |> List.exists (fun error -> error.Contains("mehrfach", StringComparison.Ordinal))))
                "Doppeltes Event-Top-Level-Feld wurde akzeptiert."

            let scalarRunId = RunStore.start root
            File.WriteAllText(payloadPath, "42", Constants.Utf8NoBom)

            Assert.harnessFailureContains
                "JSON-Objekt"
                (fun () -> RunStore.append root scalarRunId "scalar.fixture" payloadPath |> ignore)
                "Nichtobjekt-Event-Payload wurde gespeichert.")

    let loggingConfigurationLimitsAndRedacts () =
        TestWorkspace.run (fun root ->
            let locations = Workspace.initialize root

            File.WriteAllText(
                locations.Config,
                """{
  "schemaVersion": 1,
  "rag": { "sources": ["README.md"], "chunkLines": 1, "overlapLines": 0 },
  "logging": {
    "format": "jsonl",
    "utcOnly": true,
    "hashChain": true,
    "rawRunRetentionDays": 180,
    "acceptedSummariesRetentionDays": 0,
    "maxEventPayloadBytes": 1024
  },
  "security": {
    "redactKeyPatterns": ["^customCredential$"],
    "redactValuePatterns": ["(?i)^bearer [a-z0-9._~+/=-]+$", "-----BEGIN .*PRIVATE KEY-----"]
  }
}
""",
                Constants.Utf8NoBom
            )

            let runId = RunStore.start root
            let payloadPath = Path.Combine(root, "payload.json")

            File.WriteAllText(
                payloadPath,
                """{
  "customCredential": "configured-key-secret",
  "header": "Bearer AbCd.123",
  "pem": "-----BEGIN RSA PRIVATE KEY-----\nprivate-key-material"
}
""",
                Constants.Utf8NoBom
            )

            RunStore.append root runId "security.fixture" payloadPath |> ignore

            let eventText =
                File.ReadAllText(Path.Combine(locations.Runs, runId, "events.jsonl"))

            for secret in [ "configured-key-secret"; "AbCd.123"; "private-key-material" ] do
                Assert.isTrue
                    (not (eventText.Contains(secret, StringComparison.Ordinal)))
                    $"Konfiguriertes Secret wurde gespeichert: {secret}"

            Assert.isTrue (eventText.Contains("[REDACTED]", StringComparison.Ordinal)) "Redaction fehlt."

            let oversized = JsonSerializer.Serialize({| data = String('x', 1100) |})
            File.WriteAllText(payloadPath, oversized, Constants.Utf8NoBom)

            Assert.harnessFailureContains
                "1024 Bytes"
                (fun () -> RunStore.append root runId "too.large" payloadPath |> ignore)
                "logging.maxEventPayloadBytes wurde nicht angewandt."

            let summaryRun = RunStore.start root
            let nearLimitSummary = JsonSerializer.Serialize({| result = String('s', 995) |})
            File.WriteAllText(payloadPath, nearLimitSummary, Constants.Utf8NoBom)

            Assert.harnessFailureContains
                "Event-Payload"
                (fun () -> RunStore.finish root summaryRun "succeeded" (Some payloadPath) |> ignore)
                "Wrapper des Abschluss-Events durfte das Payloadlimit ueberschreiten."

            File.WriteAllText(
                locations.Config,
                """{
  "schemaVersion": 1,
  "rag": { "sources": ["README.md"], "chunkLines": 1, "overlapLines": 0 },
  "security": { "redactKeyPatterns": ["("], "redactValuePatterns": [] }
}
""",
                Constants.Utf8NoBom
            )

            Assert.harnessFailureContains
                "Ungueltiger Regex"
                (fun () -> RunStore.start root |> ignore)
                "Ungueltige Redaction-Konfiguration wurde akzeptiert.")

    let ragIsDeterministicAndCitesSources () =
        TestWorkspace.run (fun root ->
            let locations = Workspace.initialize root
            let knowledge = Path.Combine(root, "knowledge")
            Directory.CreateDirectory(knowledge) |> ignore

            File.WriteAllText(
                Path.Combine(knowledge, "world.txt"),
                "Moor wind and old stones\nThe moonwell contains azurquartz\nWardens keep the silent road\n",
                Constants.Utf8NoBom
            )

            File.WriteAllText(
                Path.Combine(knowledge, "systems.txt"),
                "Frame budgets remain fixed\nBaked lights protect old hardware\n",
                Constants.Utf8NoBom
            )

            File.WriteAllText(
                locations.Config,
                """{
  "schemaVersion": 1,
  "rag": {
    "sources": ["knowledge/*.txt"],
    "chunkLines": 2,
    "overlapLines": 1
  }
}
""",
                Constants.Utf8NoBom
            )

            let first = RagIndex.build root
            let firstBytes = File.ReadAllBytes(locations.IndexFile)
            let second = RagIndex.build root
            let secondBytes = File.ReadAllBytes(locations.IndexFile)
            Assert.equal first.IndexHash second.IndexHash "Wiederholter Build hat anderen Indexhash."
            Assert.isTrue (firstBytes.AsSpan().SequenceEqual(secondBytes)) "RAG-Index ist nicht byte-deterministisch."

            let response = RagIndex.query root "azurquartz" 3
            Assert.isTrue (not (List.isEmpty response.Results)) "Seltenes Suchwort liefert keinen Chunk."
            let result = response.Results.Head
            Assert.equal "knowledge/world.txt" result.Citation.Path "Citation verweist auf falsche Quelle."

            Assert.isTrue
                (result.Citation.StartLine <= 2 && result.Citation.EndLine >= 2)
                "Citation deckt Fundzeile nicht ab."

            Assert.equal 64 result.Citation.SourceSha256.Length "Quellhash fehlt in Citation."
            Assert.equal 64 result.Citation.ChunkSha256.Length "Chunkhash fehlt in Citation."

            let report = Verification.verify root None
            let ragErrors = String.concat "; " report.Errors
            Assert.isTrue report.Valid $"Gueltiger RAG-Index wurde abgelehnt: {ragErrors}")

    let hardwareQuestionRanksPerformanceBudgetFirst () =
        TestWorkspace.run (fun root ->
            let locations = Workspace.initialize root

            let docs = Path.Combine(root, "docs")
            Directory.CreateDirectory(docs) |> ignore

            File.WriteAllText(
                Path.Combine(docs, "OFFENE_FRAGEN.md"),
                "Welche Funktionen gelten und welche Ergebnisse werden verwendet?\nWelche Daten und welche Systeme?\n",
                Constants.Utf8NoBom
            )

            File.WriteAllText(
                Path.Combine(docs, "PERFORMANCE_BUDGET.md"),
                "Hardwarevertrag\nDie Bild"
                + "rate der Mindest"
                + "hardware betraegt stabile 30 FPS; 60 FPS sind bevorzugt.\n",
                Constants.Utf8NoBom
            )

            File.WriteAllText(
                locations.Config,
                """{
  "schemaVersion": 1,
  "rag": {
    "sources": ["docs/*.md"],
    "chunkLines": 10,
    "overlapLines": 0
  }
}
""",
                Constants.Utf8NoBom
            )

            RagIndex.build root |> ignore

            let response =
                RagIndex.query root ("Welche Mindest" + "hardware und Bild" + "rate gelten?") 3

            Assert.isTrue (not (List.isEmpty response.Results)) "Hardwarefrage liefert keinen Treffer."

            Assert.equal
                "docs/PERFORMANCE_BUDGET.md"
                response.Results.Head.Citation.Path
                "Allgemeine Fragewoerter dominieren das Hardware-Ranking.")

    let ragHonorsContextCharacterBudget () =
        TestWorkspace.run (fun root ->
            let locations = Workspace.initialize root
            let knowledge = Path.Combine(root, "knowledge")
            Directory.CreateDirectory(knowledge) |> ignore

            let longLine suffix =
                "budgetneedle " + suffix + " " + String('x', 780)

            File.WriteAllLines(
                Path.Combine(knowledge, "large.txt"),
                [| longLine "alpha"; longLine "beta"; longLine "gamma" |],
                Constants.Utf8NoBom
            )

            File.WriteAllText(
                locations.Config,
                """{
  "schemaVersion": 1,
  "rag": {
    "sources": ["knowledge/*.txt"],
    "chunkLines": 1,
    "overlapLines": 0,
    "maxContextCharacters": 1000
  }
}
""",
                Constants.Utf8NoBom
            )

            RagIndex.build root |> ignore
            let response = RagIndex.query root "budgetneedle" 3

            let contextCharacters =
                response.Results |> List.sumBy (fun result -> result.Text.Length)

            Assert.equal 1000 contextCharacters "rag.maxContextCharacters wurde nicht ausgeschoepft/begrenzt."
            Assert.equal 2 response.Results.Length "Der letzte passende Chunk wurde nicht am Budget abgeschnitten.")

    let ragIndexesOnlyCurrentAcceptedMemory () =
        TestWorkspace.run (fun root ->
            let locations = Workspace.initialize root
            let docs = Path.Combine(root, "docs")
            let memory = Path.Combine(root, ".ai", "memory")
            Directory.CreateDirectory(docs) |> ignore
            Directory.CreateDirectory(memory) |> ignore

            let sourcePath = Path.Combine(docs, "truth.md")
            File.WriteAllText(sourcePath, "authoritative source\n", Constants.Utf8NoBom)
            let sourceHash = File.ReadAllBytes(sourcePath) |> Internal.sha256Hex

            let memoryRecord id status statement hash =
                let review = status <> "proposed"

                JsonSerializer.Serialize(
                    {| schemaVersion = 1
                       id = id
                       kind = "fact"
                       statement = statement
                       status = status
                       confidence = 1.0
                       scope = "test/retrieval"
                       sources =
                        [| {| path = "docs/truth.md"
                              sha256 = hash
                              locator = "test source"
                              runId = null |} |]
                       createdAtUtc = "2026-08-13T00:00:00.000Z"
                       createdBy = "test-producer"
                       reviewedAtUtc = if review then "2026-08-13T00:01:00.000Z" else null
                       reviewedBy = if review then "test-reviewer" else null
                       supersedes = Array.empty<string>
                       expiresAtUtc = null
                       tags = [| "test" |] |}
                )

            let recordsPath = Path.Combine(memory, "records.jsonl")

            File.WriteAllLines(
                recordsPath,
                [| memoryRecord "MEM-1001" "accepted" "firstcurrentmarker" sourceHash
                   memoryRecord "MEM-1002" "proposed" "proposedneedle" sourceHash
                   memoryRecord "MEM-1003" "accepted" "othercurrentmarker" sourceHash
                   memoryRecord "MEM-1004" "rejected" "rejectedneedle" sourceHash
                   memoryRecord "MEM-1005" "accepted" "staleneedle" (String('0', 64)) |],
                Constants.Utf8NoBom
            )

            File.WriteAllText(
                locations.Config,
                """{
  "schemaVersion": 1,
  "paths": {
    "runs": ".ai/runtime/runs",
    "index": ".ai/runtime/index",
    "cache": ".ai/runtime/cache",
    "acceptedHistory": ".ai/history/accepted",
    "memory": ".ai/memory/records.jsonl",
    "tasks": ".ai/tasks"
  },
  "rag": {
    "sources": [".ai/memory/records.jsonl"],
    "chunkLines": 72,
    "overlapLines": 12
  }
}
""",
                Constants.Utf8NoBom
            )

            let receipt = RagIndex.build root

            Assert.equal
                2
                receipt.ChunkCount
                "Memory-Records wurden kombiniert oder ausgeschlossene Records indexiert."

            let acceptedResults =
                (RagIndex.query root "firstcurrentmarker othercurrentmarker" 5).Results

            Assert.equal 2 acceptedResults.Length "Aktuelle accepted Memory-Records fehlen im Index."

            Assert.equal
                [ 1; 3 ]
                (acceptedResults
                 |> List.map (fun result -> result.Citation.StartLine)
                 |> List.sort)
                "Memory-Citations verweisen nicht auf ihre Originalzeilen."

            acceptedResults
            |> List.iter (fun result ->
                Assert.equal
                    result.Citation.StartLine
                    result.Citation.EndLine
                    "Ein Memory-Chunk umfasst mehr als einen Record."

                let containsFirst =
                    result.Text.Contains("firstcurrentmarker", StringComparison.Ordinal)

                let containsOther =
                    result.Text.Contains("othercurrentmarker", StringComparison.Ordinal)

                Assert.isTrue
                    (containsFirst <> containsOther)
                    "Zwei accepted Memory-Records wurden zu einem Treffertext kombiniert.")

            for excluded in [ "proposedneedle"; "rejectedneedle"; "staleneedle" ] do
                Assert.isTrue
                    (List.isEmpty (RagIndex.query root excluded 5).Results)
                    $"Nicht vertrauenswuerdiges Memory wurde indexiert: {excluded}"

            let validReport = Verification.verify root None
            Assert.isTrue validReport.Valid "Gefilterter Memory-Index ist nicht verifizierbar."

            File.WriteAllText(sourcePath, "changed authoritative source\n", Constants.Utf8NoBom)
            let staleReport = Verification.verify root None

            Assert.isTrue
                (not staleReport.Valid)
                "Nachtraeglich veraltetes accepted Memory wurde bei verify nicht erkannt.")

    let memoryLifecycleIsExplicitAppendOnlyAndTamperEvident () =
        TestWorkspace.run (fun root ->
            let locations = Workspace.initialize root

            File.WriteAllText(
                locations.Config,
                """{
  "schemaVersion": 1,
  "rag": { "sources": ["docs/*.md"], "chunkLines": 1, "overlapLines": 0 },
  "logging": {
    "format": "jsonl",
    "utcOnly": true,
    "hashChain": true,
    "rawRunRetentionDays": 180,
    "acceptedSummariesRetentionDays": 0,
    "maxEventPayloadBytes": 1024
  },
  "security": {
    "redactKeyPatterns": ["^customCredential$"],
    "redactValuePatterns": ["(?i)bearer [a-z0-9._~+/=-]+", "-----BEGIN .*PRIVATE KEY-----"]
  }
}
""",
                Constants.Utf8NoBom
            )

            let docs = Path.Combine(root, "docs")
            Directory.CreateDirectory(docs) |> ignore
            let sourcePath = Path.Combine(docs, "source.md")
            File.WriteAllText(sourcePath, "authoritative memory source\n", Constants.Utf8NoBom)
            let sourceHash = Internal.sha256File sourcePath
            let proposalPath = Path.Combine(root, "proposal.json")

            let writeProposal id statement createdBy =
                let json =
                    JsonSerializer.Serialize(
                        {| schemaVersion = 1
                           id = id
                           kind = "constraint"
                           statement = statement
                           status = "proposed"
                           confidence = 0.9
                           scope = "test/simulation"
                           conflictKey = "simulation.tick-rate"
                           sources =
                            [| {| path = "docs/source.md"
                                  sha256 = sourceHash
                                  locator = "whole fixture"
                                  runId = null |} |]
                           createdAtUtc = "2026-08-13T00:00:00.000Z"
                           createdBy = createdBy
                           reviewedAtUtc = null
                           reviewedBy = null
                           supersedes = Array.empty<string>
                           expiresAtUtc = null
                           tags = [| "simulation" |] |}
                    )

                File.WriteAllText(proposalPath, json, Constants.Utf8NoBom)

                Assert.equal
                    0
                    (Cli.execute [ "memory"; "propose"; "--record-file"; proposalPath; "--workspace"; root ])
                    "memory propose CLI ist fehlgeschlagen."

            writeProposal
                "MEM-2000"
                "Simulation tick rate is explicitly fixed; customCredential=memory-secret."
                "producer-agent"

            let recordsPath = Path.Combine(root, ".ai", "memory", "records.jsonl")
            let redactedMemory = File.ReadAllText(recordsPath)

            Assert.isTrue
                (not (redactedMemory.Contains("memory-secret", StringComparison.Ordinal))
                 && redactedMemory.Contains("[REDACTED]", StringComparison.Ordinal))
                "Konfiguriertes Secret wurde durch memory propose persistiert."

            File.WriteAllText(proposalPath, "{\"statement\":\"" + String('x', 1500) + "\"}", Constants.Utf8NoBom)

            Assert.harnessFailureContains
                "1024 Bytes"
                (fun () -> MemoryStore.propose root proposalPath |> ignore)
                "Oversize-Memory-Vorschlag wurde angenommen."

            Assert.harnessFailureContains
                "eigenen Memory-Vorschlag"
                (fun () -> MemoryStore.accept root "MEM-2000" "MEM-2001" "producer-agent" |> ignore)
                "Erzeuger konnte eigenen Vorschlag annehmen."

            Assert.harnessFailureContains
                "Secretmuster"
                (fun () ->
                    MemoryStore.accept root "MEM-2000" "MEM-2001" ("Bearer " + "abcdefghijklmnopqrstuvwxyz")
                    |> ignore)
                "Secret im Memory-Review-Akteur wurde gespeichert."

            Assert.equal
                0
                (Cli.execute
                    [ "memory"
                      "accept"
                      "MEM-2000"
                      "--new-id"
                      "MEM-2001"
                      "--actor"
                      "review-agent"
                      "--workspace"
                      root ])
                "memory accept CLI ist fehlgeschlagen."

            let acceptedStatus = MemoryStore.status root

            Assert.isTrue
                (acceptedStatus.Records
                 |> List.exists (fun status -> status.Id = "MEM-2001" && status.Retrievable))
                "Explizit angenommener Record ist nicht abrufbar."

            Assert.isTrue
                (acceptedStatus.Records
                 |> List.exists (fun status -> status.Id = "MEM-2000" && status.EffectiveStatus = "superseded"))
                "Konsumierter Vorschlag blieb effektiv proposed."

            Assert.harnessFailureContains
                "Memory MEM-2000 ist kein aktiver proposed Record (effektiver Status: 'superseded')."
                (fun () -> MemoryStore.accept root "MEM-2000" "MEM-2005" "second-review-agent" |> ignore)
                "Derselbe Vorschlag konnte ein zweites Mal angenommen werden."

            let statusAfterDuplicateAttempt = MemoryStore.status root

            Assert.equal
                2
                statusAfterDuplicateAttempt.RecordCount
                "Abgelehnte Doppelannahme hat das append-only Ledger veraendert."

            Assert.equal
                1
                statusAfterDuplicateAttempt.RetrievableCount
                "Abgelehnte Doppelannahme hat den einzigen gueltigen Retrieval-Record verdraengt."

            Assert.isTrue
                (statusAfterDuplicateAttempt.Findings
                 |> List.forall (fun finding -> finding.Code <> "MEMORY_CONFLICT"))
                "Abgelehnte Doppelannahme erzeugte einen kuenstlichen Memory-Konflikt."

            writeProposal "MEM-2002" "Simulation tick rate is replaced after measurement." "producer-agent"

            Assert.equal
                0
                (Cli.execute
                    [ "memory"
                      "supersede"
                      "MEM-2001"
                      "--with"
                      "MEM-2002"
                      "--new-id"
                      "MEM-2003"
                      "--actor"
                      "review-agent"
                      "--workspace"
                      root ])
                "memory supersede CLI ist fehlgeschlagen."

            let replacedStatus = MemoryStore.status root

            Assert.isTrue
                (replacedStatus.Records
                 |> List.exists (fun status -> status.Id = "MEM-2001" && status.EffectiveStatus = "superseded"))
                "Ersetzter accepted Record blieb aktiv."

            Assert.isTrue
                (replacedStatus.Records
                 |> List.exists (fun status -> status.Id = "MEM-2003" && status.Retrievable))
                "Expliziter Ersatz ist nicht aktiv."

            File.WriteAllText(sourcePath, "changed source invalidates memory\n", Constants.Utf8NoBom)
            let staleStatus = MemoryStore.status root

            Assert.isTrue
                (staleStatus.Findings
                 |> List.exists (fun finding -> finding.Code = "MEMORY_STALE" && finding.RecordIds = [ "MEM-2003" ]))
                "Geaenderte Quelle wird nicht sichtbar als stale gemeldet."

            Assert.equal
                0
                (Cli.execute
                    [ "memory"
                      "set-status"
                      "MEM-2003"
                      "--status"
                      "stale"
                      "--new-id"
                      "MEM-2004"
                      "--actor"
                      "review-agent"
                      "--workspace"
                      root ])
                "memory set-status CLI ist fehlgeschlagen."

            Assert.equal
                0
                (Cli.execute [ "memory"; "status"; "--workspace"; root ])
                "memory status CLI ist fehlgeschlagen."

            let validation = MemoryStore.validate root
            Assert.equal 5 validation.RecordCount "Memory-Lebenszyklus hat unerwartete Revisionszahl."

            Assert.equal
                5
                validation.ChainedRecordCount
                "Neue Memory-Revisionen sind nicht vollstaendig hashverkettet."

            let original = File.ReadAllText(recordsPath)

            let tampered =
                original.Replace("explicitly fixed", "silently changed", StringComparison.Ordinal)

            File.WriteAllText(recordsPath, tampered, Constants.Utf8NoBom)

            Assert.harnessFailureContains
                "recordHash"
                (fun () -> MemoryStore.validate root |> ignore)
                "Manipulierte Memory-Revision wurde akzeptiert."

            File.WriteAllText(recordsPath, original, Constants.Utf8NoBom)
            Assert.equal 5 (MemoryStore.validate root).RecordCount "Wiederhergestellte Memory-Kette ist ungueltig.")

    let memoryConflictsAndStatusesAreExcludedAndReported () =
        TestWorkspace.run (fun root ->
            let locations = Workspace.initialize root
            let docs = Path.Combine(root, "docs")
            let memory = Path.Combine(root, ".ai", "memory")
            Directory.CreateDirectory(docs) |> ignore
            Directory.CreateDirectory(memory) |> ignore
            let sourcePath = Path.Combine(docs, "truth.md")
            File.WriteAllText(sourcePath, "stable source\n", Constants.Utf8NoBom)
            let sourceHash = Internal.sha256File sourcePath

            let record id status statement conflictKey hash =
                JsonSerializer.Serialize(
                    {| schemaVersion = 1
                       id = id
                       kind = "fact"
                       statement = statement
                       status = status
                       confidence = 1.0
                       scope = "test/conflict"
                       conflictKey = conflictKey
                       sources =
                        [| {| path = "docs/truth.md"
                              sha256 = hash
                              locator = "fixture"
                              runId = null |} |]
                       createdAtUtc = "2026-08-13T00:00:00.000Z"
                       createdBy = "producer"
                       reviewedAtUtc =
                        if status = "proposed" then
                            null
                        else
                            "2026-08-13T00:01:00.000Z"
                       reviewedBy = if status = "proposed" then null else "reviewer"
                       supersedes = Array.empty<string>
                       expiresAtUtc = null
                       tags = [| "test" |] |}
                )

            File.WriteAllLines(
                Path.Combine(memory, "records.jsonl"),
                [| record "MEM-3000" "accepted" "conflictmarker says alpha is active." "world.active-rule" sourceHash
                   record "MEM-3001" "accepted" "conflictmarker says beta is active." "world.active-rule" sourceHash
                   record "MEM-3002" "proposed" "proposedmarker remains unreviewed." "world.proposal" sourceHash
                   record "MEM-3003" "rejected" "rejectedmarker remains rejected." "world.rejected" sourceHash
                   record "MEM-3004" "stale" "stalemarker remains explicitly stale." "world.stale" sourceHash |],
                Constants.Utf8NoBom
            )

            File.WriteAllText(
                locations.Config,
                """{
  "schemaVersion": 1,
  "paths": {
    "runs": ".ai/runtime/runs",
    "index": ".ai/runtime/index",
    "cache": ".ai/runtime/cache",
    "acceptedHistory": ".ai/history/accepted",
    "memory": ".ai/memory/records.jsonl",
    "tasks": ".ai/tasks"
  },
  "rag": { "sources": [".ai/memory/records.jsonl"], "chunkLines": 72, "overlapLines": 12 }
}
""",
                Constants.Utf8NoBom
            )

            let status = MemoryStore.status root

            Assert.isTrue
                (status.Findings
                 |> List.exists (fun finding ->
                     finding.Code = "MEMORY_CONFLICT"
                     && finding.RecordIds = [ "MEM-3000"; "MEM-3001" ]))
                "Widerspruechliche accepted Records werden nicht gemeldet."

            RagIndex.build root |> ignore

            for excluded in [ "conflictmarker"; "proposedmarker"; "rejectedmarker"; "stalemarker" ] do
                Assert.isTrue
                    (List.isEmpty (RagIndex.query root excluded 5).Results)
                    $"Ausgeschlossener Memory-Status gelangte ins Retrieval: {excluded}"

            let response = RagIndex.query root "conflictmarker" 5

            Assert.isTrue
                (response.MemoryFindings
                 |> List.exists (fun finding -> finding.Code = "MEMORY_CONFLICT"))
                "RAG-Antwort macht Memory-Konflikt nicht sichtbar.")

    let memoryFreshnessLimitAndPathsAreFailClosed () =
        TestWorkspace.run (fun root ->
            let locations = Workspace.initialize root
            let docs = Path.Combine(root, "docs")
            let memory = Path.Combine(root, ".ai", "memory")
            Directory.CreateDirectory(docs) |> ignore
            Directory.CreateDirectory(memory) |> ignore
            let sourcePath = Path.Combine(docs, "oversized.md")
            File.WriteAllText(sourcePath, String('x', 5020), Constants.Utf8NoBom)
            Assert.equal 5020L (FileInfo(sourcePath).Length) "Oversize-Fixture hat eine falsche Bytegroesse."
            let sourceHash = Internal.sha256File sourcePath

            File.WriteAllText(
                locations.Config,
                """{
  "schemaVersion": 1,
  "paths": {
    "runs": ".ai/runtime/runs",
    "index": ".ai/runtime/index",
    "cache": ".ai/runtime/cache",
    "acceptedHistory": ".ai/history/accepted",
    "memory": ".ai/memory/records.jsonl",
    "tasks": ".ai/tasks"
  },
  "rag": {
    "sources": [".ai/memory/records.jsonl"],
    "maxFileBytes": 4096,
    "chunkLines": 20,
    "overlapLines": 0
  }
}
""",
                Constants.Utf8NoBom
            )

            let accepted =
                JsonSerializer.Serialize(
                    {| schemaVersion = 1
                       id = "MEM-4100"
                       kind = "fact"
                       statement = "oversizemark must never become retrievable."
                       status = "accepted"
                       confidence = 1.0
                       scope = "test/source-limit"
                       conflictKey = "test.source-limit"
                       sources =
                        [| {| path = "docs/oversized.md"
                              sha256 = sourceHash
                              locator = "5020-byte fixture"
                              runId = null |} |]
                       createdAtUtc = "2026-08-13T00:00:00.000Z"
                       createdBy = "producer"
                       reviewedAtUtc = "2026-08-13T00:01:00.000Z"
                       reviewedBy = "reviewer"
                       supersedes = Array.empty<string>
                       expiresAtUtc = null
                       tags = [| "test" |] |}
                )

            File.WriteAllText(Path.Combine(memory, "records.jsonl"), accepted + "\n", Constants.Utf8NoBom)
            let status = MemoryStore.status root

            Assert.isTrue
                (status.Records
                 |> List.exists (fun item -> item.Id = "MEM-4100" && item.EffectiveStatus = "stale"))
                "Quelle oberhalb rag.maxFileBytes blieb im Memory-Status aktuell."

            Assert.isTrue
                (status.Findings
                 |> List.exists (fun finding -> finding.Code = "MEMORY_STALE" && finding.RecordIds = [ "MEM-4100" ]))
                "Quelle oberhalb rag.maxFileBytes erzeugte kein sichtbares MEMORY_STALE."

            RagIndex.build root |> ignore
            let response = RagIndex.query root "oversizemark" 5
            Assert.isTrue (List.isEmpty response.Results) "Oversize-Memory gelangte in RAG-Chunks."

            Assert.isTrue
                (response.MemoryFindings
                 |> List.exists (fun finding -> finding.Code = "MEMORY_STALE"))
                "RAG meldete die Oversize-Quelle nicht als stale."

            let proposalPath = Path.Combine(root, "oversized-proposal.json")

            File.WriteAllText(
                proposalPath,
                accepted
                    .Replace("MEM-4100", "MEM-4101", StringComparison.Ordinal)
                    .Replace("\"accepted\"", "\"proposed\"", StringComparison.Ordinal)
                    .Replace("\"2026-08-13T00:01:00.000Z\"", "null", StringComparison.Ordinal)
                    .Replace("\"reviewer\"", "null", StringComparison.Ordinal),
                Constants.Utf8NoBom
            )

            Assert.harnessFailureContains
                "fehlende, zu grosse oder hashabweichende Quelle"
                (fun () -> MemoryStore.propose root proposalPath |> ignore)
                "memory propose ignorierte rag.maxFileBytes."

            Assert.equal 1 (MemoryStore.validate root).RecordCount "Oversize-Proposal veraenderte das Ledger.")

        TestWorkspace.run (fun root ->
            let locations = Workspace.initialize root

            let outside =
                Path.Combine(Path.GetTempPath(), "RiftHarness.Outside-" + Guid.NewGuid().ToString("N"))

            Directory.CreateDirectory(outside) |> ignore

            try
                let docs = Path.Combine(root, "docs")
                Directory.CreateDirectory(docs) |> ignore
                let sourcePath = Path.Combine(docs, "source.md")
                File.WriteAllText(sourcePath, "workspace source\n", Constants.Utf8NoBom)
                let sourceHash = Internal.sha256File sourcePath
                let proposalPath = Path.Combine(root, "proposal.json")

                let writeProposal id path hash =
                    File.WriteAllText(
                        proposalPath,
                        JsonSerializer.Serialize(
                            {| schemaVersion = 1
                               id = id
                               kind = "fact"
                               statement = "Symlink boundaries must remain fail closed."
                               status = "proposed"
                               confidence = 1.0
                               scope = "test/path-safety"
                               conflictKey = "test.path-safety"
                               sources =
                                [| {| path = path
                                      sha256 = hash
                                      locator = "symlink fixture"
                                      runId = null |} |]
                               createdAtUtc = "2026-08-13T00:00:00.000Z"
                               createdBy = "producer"
                               reviewedAtUtc = null
                               reviewedBy = null
                               supersedes = Array.empty<string>
                               expiresAtUtc = null
                               tags = [| "test" |] |}
                        ),
                        Constants.Utf8NoBom
                    )

                let memoryLink = Path.Combine(root, ".ai", "memory")
                let mutable linksSupported = true

                try
                    Directory.CreateSymbolicLink(memoryLink, outside) |> ignore
                with
                | :? PlatformNotSupportedException
                | :? UnauthorizedAccessException
                | :? IOException -> linksSupported <- false

                if not linksSupported && not (OperatingSystem.IsWindows()) then
                    failwith "Linux-Test konnte den Memory-Symlink nicht erzeugen."

                if linksSupported then
                    writeProposal "MEM-4200" "docs/source.md" sourceHash

                    Assert.harnessFailureContains
                        "Symlink, Junction oder ReparsePoint"
                        (fun () -> MemoryStore.propose root proposalPath |> ignore)
                        "Memory-Ledger folgte einem externen Verzeichnis-Symlink."

                    Assert.isTrue
                        (not (File.Exists(Path.Combine(outside, "records.jsonl"))))
                        "Memory-Ledger schrieb ausserhalb des Workspace."

                    Directory.Delete(memoryLink)
                    Directory.CreateDirectory(memoryLink) |> ignore
                    let outsideSource = Path.Combine(outside, "outside.md")
                    File.WriteAllText(outsideSource, "outside source\n", Constants.Utf8NoBom)
                    let sourceLink = Path.Combine(docs, "linked.md")
                    File.CreateSymbolicLink(sourceLink, outsideSource) |> ignore
                    writeProposal "MEM-4201" "docs/linked.md" (Internal.sha256File outsideSource)

                    Assert.harnessFailureContains
                        "fehlende, zu grosse oder hashabweichende Quelle"
                        (fun () -> MemoryStore.propose root proposalPath |> ignore)
                        "Memory-Quelle folgte einem externen Datei-Symlink."

                    Assert.isTrue
                        (not (File.Exists(Path.Combine(memoryLink, "records.jsonl"))))
                        "Abgelehnte Symlink-Quelle veraenderte das Memory-Ledger."
            finally
                if Directory.Exists(outside) then
                    Directory.Delete(outside, true))

    let retrievalTracesAreDeterministicChainedRedactedAndBounded () =
        TestWorkspace.run (fun root ->
            let locations = Workspace.initialize root
            let knowledge = Path.Combine(root, "knowledge")
            Directory.CreateDirectory(knowledge) |> ignore

            File.WriteAllText(
                Path.Combine(knowledge, "fixture.txt"),
                "traceneedle public text\n-----BEGIN RSA PRIVATE KEY-----\\nprivate-key-material\n",
                Constants.Utf8NoBom
            )

            File.WriteAllText(
                locations.Config,
                """{
  "schemaVersion": 1,
  "paths": {
    "runs": ".ai/runtime/runs",
    "index": ".ai/runtime/index",
    "cache": ".ai/runtime/cache",
    "acceptedHistory": ".ai/history/accepted",
    "memory": ".ai/memory/records.jsonl",
    "tasks": ".ai/tasks"
  },
  "rag": {
    "sources": ["knowledge/*.txt"],
    "chunkLines": 2,
    "overlapLines": 0,
    "maxContextCharacters": 4000
  },
  "logging": {
    "format": "jsonl",
    "utcOnly": true,
    "hashChain": true,
    "rawRunRetentionDays": 180,
    "acceptedSummariesRetentionDays": 0,
    "maxEventPayloadBytes": 16384
  },
  "security": {
    "redactKeyPatterns": ["^customCredential$"],
    "redactValuePatterns": ["(?i)bearer [a-z0-9._~+/=-]+", "-----BEGIN .*PRIVATE KEY-----"]
  }
}
""",
                Constants.Utf8NoBom
            )

            RagIndex.build root |> ignore
            let runId = RunStore.start root
            let firstResponse = RagIndex.query root "traceneedle" 3
            let first = RetrievalStore.record root runId firstResponse
            let secondResponse = RagIndex.query root "traceneedle" 3
            let second = RetrievalStore.record root runId secondResponse

            Assert.equal firstResponse.QuerySha256 secondResponse.QuerySha256 "Golden Query-Hash ist instabil."
            Assert.equal firstResponse.IndexSha256 secondResponse.IndexSha256 "Golden Index-Hash ist instabil."
            Assert.equal firstResponse.Ranking secondResponse.Ranking "Golden Rankingparameter sind instabil."

            Assert.equal
                (firstResponse.Results |> List.map (fun result -> result.ChunkId, result.Score))
                (secondResponse.Results |> List.map (fun result -> result.ChunkId, result.Score))
                "Golden Treffer-IDs oder Scores sind instabil."

            Assert.equal 1L first.Sequence "Erster Retrieval-Trace hat falsche Sequenz."
            Assert.equal 2L second.Sequence "Zweiter Retrieval-Trace hat falsche Sequenz."

            let custom = RagIndex.query root "customCredential=custom-value traceneedle" 3
            RetrievalStore.record root runId custom |> ignore

            let bearer =
                RagIndex.query root ("Bearer " + "abcdefghijklmnopqrstuvwxyz traceneedle") 3

            RetrievalStore.record root runId bearer |> ignore

            let tracePath = Path.Combine(locations.Runs, runId, "retrieval.jsonl")
            let traceText = File.ReadAllText(tracePath)

            for secret in [ "private-key-material"; "custom-value"; "abcdefghijklmnopqrstuvwxyz" ] do
                Assert.isTrue
                    (not (traceText.Contains(secret, StringComparison.Ordinal)))
                    $"Secret gelangte in Retrieval-Trace: {secret}"

            Assert.isTrue (traceText.Contains("[REDACTED]", StringComparison.Ordinal)) "Trace-Redaction fehlt."
            let lines = File.ReadAllLines(tracePath)
            use secondDocument = JsonDocument.Parse(lines[1])

            Assert.equal
                first.TraceHash
                (secondDocument.RootElement.GetProperty("previousTraceHash").GetString())
                "Retrieval-Hashkette ist falsch."

            let valid = Verification.verify root (Some runId)
            let validErrors = String.concat "; " valid.Errors
            Assert.isTrue valid.Valid $"Gueltige Retrieval-Kette wurde abgelehnt: {validErrors}"

            let tampered =
                lines[0].Replace("traceneedle", "tracechanged", StringComparison.Ordinal)

            File.WriteAllText(tracePath, tampered + "\n" + String.Join("\n", lines[1..]) + "\n", Constants.Utf8NoBom)
            let invalid = Verification.verify root (Some runId)
            Assert.isTrue (not invalid.Valid) "Manipulierter Retrieval-Trace wurde akzeptiert."

            let missingTraceRun = RunStore.start root
            File.Delete(Path.Combine(locations.Runs, missingTraceRun, "retrieval.jsonl"))
            let missingTrace = Verification.verify root (Some missingTraceRun)
            Assert.isTrue (not missingTrace.Valid) "Pflicht-Retrievaldatei durfte spurlos geloescht werden."

            let tailRun = RunStore.start root
            RetrievalStore.record root tailRun firstResponse |> ignore
            let tailFinal = RetrievalStore.record root tailRun secondResponse
            RunStore.finish root tailRun "succeeded" None |> ignore
            let tailRunPath = Path.Combine(locations.Runs, tailRun)
            let tailTracePath = Path.Combine(tailRunPath, "retrieval.jsonl")
            let tailLines = File.ReadAllLines(tailTracePath)

            use tailSummary =
                JsonDocument.Parse(File.ReadAllBytes(Path.Combine(tailRunPath, "summary.json")))

            Assert.equal
                2L
                (tailSummary.RootElement.GetProperty("retrievalTraceCount").GetInt64())
                "Abschluss-Summary hat eine falsche Retrieval-Anzahl."

            Assert.equal
                tailFinal.TraceHash
                (tailSummary.RootElement.GetProperty("finalRetrievalTraceHash").GetString())
                "Abschluss-Summary verankert nicht den finalen Retrieval-Hash."

            let completedValid = Verification.verify root (Some tailRun)
            Assert.isTrue completedValid.Valid "Abgeschlossener Run mit Retrieval-Anker ist ungueltig."
            File.WriteAllText(tailTracePath, tailLines[0] + "\n", Constants.Utf8NoBom)
            let truncatedTail = Verification.verify root (Some tailRun)

            Assert.isTrue
                (not truncatedTail.Valid
                 && (truncatedTail.Errors
                     |> List.exists (fun error -> error.Contains("Retrieval-Tail", StringComparison.Ordinal))))
                "Entfernte letzte Retrieval-Zeile wurde nach Run-Abschluss nicht erkannt."

            let emptiedRun = RunStore.start root
            RetrievalStore.record root emptiedRun firstResponse |> ignore
            RunStore.finish root emptiedRun "succeeded" None |> ignore
            let emptiedTracePath = Path.Combine(locations.Runs, emptiedRun, "retrieval.jsonl")
            File.WriteAllText(emptiedTracePath, "", Constants.Utf8NoBom)
            let emptiedTrace = Verification.verify root (Some emptiedRun)

            Assert.isTrue
                (not emptiedTrace.Valid
                 && (emptiedTrace.Errors
                     |> List.exists (fun error -> error.Contains("Retrieval-Tail", StringComparison.Ordinal))))
                "Vollstaendig geleerter Retrieval-Trace wurde nach Run-Abschluss nicht erkannt.")

        TestWorkspace.run (fun root ->
            let locations = Workspace.initialize root
            let knowledge = Path.Combine(root, "knowledge")
            Directory.CreateDirectory(knowledge) |> ignore
            File.WriteAllText(Path.Combine(knowledge, "small.txt"), "small fixture\n", Constants.Utf8NoBom)

            File.WriteAllText(
                locations.Config,
                """{
  "schemaVersion": 1,
  "rag": { "sources": ["knowledge/*.txt"], "chunkLines": 1, "overlapLines": 0 },
  "logging": {
    "format": "jsonl",
    "utcOnly": true,
    "hashChain": true,
    "rawRunRetentionDays": 180,
    "acceptedSummariesRetentionDays": 0,
    "maxEventPayloadBytes": 1024
  }
}
""",
                Constants.Utf8NoBom
            )

            RagIndex.build root |> ignore
            let runId = RunStore.start root
            let oversized = RagIndex.query root (String('q', 1600)) 1

            Assert.harnessFailureContains
                "maxEventPayloadBytes"
                (fun () -> RetrievalStore.record root runId oversized |> ignore)
                "Oversize-Retrieval-Trace wurde gespeichert."

            Assert.equal
                0L
                (FileInfo(Path.Combine(locations.Runs, runId, "retrieval.jsonl")).Length)
                "Fehlgeschlagener Oversize-Trace hat Teildaten hinterlassen.")

    let ragExcludesConfiguredPathsSecretsAndSymlinks () =
        TestWorkspace.run (fun root ->
            let locations = Workspace.initialize root
            let knowledge = Path.Combine(root, "knowledge")
            Directory.CreateDirectory(knowledge) |> ignore

            let excludedFiles =
                [ Path.Combine(root, ".ai", "runtime", "private.txt"), "runtimeneedle"
                  Path.Combine(root, "bin", "build-output.txt"), "binneedle"
                  Path.Combine(root, "obj", "intermediate.txt"), "objneedle"
                  Path.Combine(root, "assets", "quarantine", "generator-notes.txt"), "quarantineneedle" ]

            for path, marker in excludedFiles do
                Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore
                File.WriteAllText(path, marker + " excluded material\n", Constants.Utf8NoBom)

            File.WriteAllText(
                Path.Combine(knowledge, "allowed.txt"),
                "allowneedle public knowledge\n",
                Constants.Utf8NoBom
            )

            File.WriteAllText(
                Path.Combine(knowledge, "ignored.key"),
                "keyneedle private material\n",
                Constants.Utf8NoBom
            )

            let outside = Path.Combine(root, "outside.dat")
            File.WriteAllText(outside, "symlinkneedle external material\n", Constants.Utf8NoBom)
            let symlinkPath = Path.Combine(knowledge, "linked.txt")

            try
                File.CreateSymbolicLink(symlinkPath, outside) |> ignore
            with
            | :? PlatformNotSupportedException -> ()
            | :? UnauthorizedAccessException -> ()
            | :? IOException -> ()

            File.WriteAllText(
                locations.Config,
                """{
  "schemaVersion": 1,
  "rag": {
    "sources": ["**/*.txt"],
    "excludedSegments": ["bin", "obj", "assets/quarantine"],
    "chunkLines": 1,
    "overlapLines": 0
  },
  "security": {
    "neverIndex": ["*.key"],
    "redactKeyPatterns": [],
    "redactValuePatterns": []
  }
}
""",
                Constants.Utf8NoBom
            )

            RagIndex.build root |> ignore

            Assert.isTrue
                (not (List.isEmpty (RagIndex.query root "allowneedle" 5).Results))
                "Erlaubte RAG-Quelle fehlt."

            Assert.isTrue
                (List.isEmpty (RagIndex.query root "keyneedle" 5).Results)
                "security.neverIndex wurde nicht angewandt."

            for _, marker in excludedFiles do
                Assert.isTrue
                    (List.isEmpty (RagIndex.query root marker 5).Results)
                    $"Konfigurierter Runtime-/Build-/Quarantaenepfad wurde indexiert: {marker}"

            if File.Exists(symlinkPath) then
                Assert.isTrue
                    (List.isEmpty (RagIndex.query root "symlinkneedle" 5).Results)
                    "Symlink-Datei wurde indexiert.")

    let initIsIdempotentAndValidatesConfiguration () =
        TestWorkspace.run (fun root ->
            let locations = Workspace.initialize root

            use defaultDocument = JsonDocument.Parse(File.ReadAllBytes(locations.Config))
            let defaultRoot = defaultDocument.RootElement

            for field in [ "projectId"; "policy"; "paths"; "rag"; "logging"; "security" ] do
                Assert.isTrue
                    (defaultRoot.TryGetProperty(field) |> fst)
                    $"init-Standardkonfiguration ist nicht vollstaendig: {field} fehlt."

            let defaultRag = defaultRoot.GetProperty("rag")

            Assert.isTrue
                ((defaultRag.TryGetProperty("roots") |> fst)
                 && not (defaultRag.TryGetProperty("sources") |> fst))
                "init schreibt weiterhin das Legacy-RAG-Format."

            HarnessConfig.load locations |> ignore

            File.WriteAllText(
                locations.Config,
                """{
  "schemaVersion": 1,
  "rag": { "sources": ["README.md"], "chunkLines": 1, "overlapLines": 0 },
  "security": { "redactKeyPatterns": ["("], "redactValuePatterns": [] }
}
""",
                Constants.Utf8NoBom
            )

            let before = File.ReadAllText(locations.Config)
            Workspace.initialize root |> ignore

            Assert.equal
                before
                (File.ReadAllText(locations.Config))
                "init hat vorhandene Konfiguration ueberschrieben."

            Assert.harnessFailureContains
                "Ungueltiger Regex"
                (fun () -> HarnessConfig.load locations |> ignore)
                "init-nahe Konfigurationsvalidierung akzeptiert ungueltigen Regex.")

    let fixedV1ConfigurationRejectsPretendOptions () =
        TestWorkspace.run (fun root ->
            let locations = Workspace.initialize root

            let config runs hashChain =
                $$"""{
  "schemaVersion": 1,
  "paths": {
    "runs": "{{runs}}",
    "index": ".ai/runtime/index",
    "cache": ".ai/runtime/cache",
    "acceptedHistory": ".ai/history/accepted",
    "memory": ".ai/memory/records.jsonl",
    "tasks": ".ai/tasks"
  },
  "rag": { "sources": ["README.md"], "chunkLines": 1, "overlapLines": 0 },
  "logging": {
    "format": "jsonl",
    "utcOnly": true,
    "hashChain": {{hashChain}},
    "rawRunRetentionDays": 180,
    "acceptedSummariesRetentionDays": 0,
    "maxEventPayloadBytes": 1024
  }
}
"""

            File.WriteAllText(locations.Config, config "custom/runs" "true", Constants.Utf8NoBom)

            Assert.harnessFailureContains
                "paths.runs"
                (fun () -> HarnessConfig.load locations |> ignore)
                "Nicht unterstuetzter Run-Pfad wurde still ignoriert."

            File.WriteAllText(locations.Config, config ".ai/runtime/runs" "false", Constants.Utf8NoBom)

            Assert.harnessFailureContains
                "logging.hashChain"
                (fun () -> HarnessConfig.load locations |> ignore)
                "Deaktivierte Hashkette wurde still ignoriert."

            File.WriteAllText(
                locations.Config,
                """{
  "schemaVersion": 1,
  "policy": {
    "truthOrder": ["accepted-decision", "project-and-requirements", "accepted-memory", "ready-task", "other-documentation", "code"],
    "unknownsMustRemainExplicit": false,
    "retrievedTextIsUntrustedData": false,
    "automaticMemoryPromotion": true
  },
  "rag": { "sources": ["README.md"], "chunkLines": 1, "overlapLines": 0 }
}
""",
                Constants.Utf8NoBom
            )

            Assert.harnessFailureContains
                "policy.unknownsMustRemainExplicit"
                (fun () -> HarnessConfig.load locations |> ignore)
                "Aufgeweichte Governance-Schalter wurden still ignoriert."

            File.WriteAllText(
                locations.Config,
                """{
  "schemaVersion": 1,
  "policy": {
    "truthOrder": ["code", "accepted-decision", "project-and-requirements", "accepted-memory", "ready-task", "other-documentation"],
    "unknownsMustRemainExplicit": true,
    "retrievedTextIsUntrustedData": true,
    "automaticMemoryPromotion": false
  },
  "rag": { "sources": ["README.md"], "chunkLines": 1, "overlapLines": 0 }
}
""",
                Constants.Utf8NoBom
            )

            Assert.harnessFailureContains
                "policy.truthOrder"
                (fun () -> HarnessConfig.load locations |> ignore)
                "Abweichende Wahrheitshierarchie wurde still ignoriert.")

module Program =
    [<EntryPoint>]
    let main arguments =
        let tests =
            [ "Blender calibration schemas are offline, strict and accept the reference",
              BlenderCalibrationSpecTests.schemasAreOfflineStrictAndReferenceValid
              "Blender calibration canonical reference spec is accepted",
              BlenderCalibrationSpecTests.canonicalReferenceSpecIsAccepted
              "Blender calibration malformed and noncanonical specs are rejected",
              BlenderCalibrationSpecTests.malformedNoncanonicalAndClosedShapeMatrixIsRejected
              "Blender calibration field boundaries are enforced",
              BlenderCalibrationSpecTests.fieldBoundaryMatrixIsEnforced
              "Blender calibration cross-field formulas are enforced",
              BlenderCalibrationSpecTests.crossFieldFormulaMatrixIsEnforced
              "Blender calibration PCG32 matches published vectors",
              BlenderCalibrationSpecTests.pcg32MatchesPublishedVectors
              "Blender calibration metrics, candidates and bounds match contract",
              BlenderCalibrationSpecTests.referenceMetricsCandidatesAndBoundsMatchContract
              "Blender calibration snaps, axes, quaternions and colors match contract",
              BlenderCalibrationSpecTests.snapAxisQuaternionAndColorMathMatchContract
              "Blender calibration safe file boundary is enforced",
              BlenderCalibrationSpecTests.safeSpecFileBoundaryIsEnforced
              "Blender calibration validate-spec CLI envelope is canonical",
              BlenderCalibrationCliTests.validateSpecEnvelopeIsCanonicalAndPositionIndependent
              "Blender calibration invalid CLI and paths are redacted",
              BlenderCalibrationCliTests.invalidCliAndPathMatrixIsRedacted
              "Blender calibration CLI rejects leaf and parent symlinks",
              BlenderCalibrationCliTests.validateSpecRejectsLeafAndParentSymlinks
              "Blender calibration CLI rejects a symlink workspace root",
              BlenderCalibrationCliTests.validateSpecRejectsSymlinkWorkspaceRoot
              "Blender calibration CLI rejects a symlink workspace ancestor",
              BlenderCalibrationCliTests.validateSpecRejectsSymlinkWorkspaceAncestor
              "Blender calibration nested wrong types map to invalid spec",
              BlenderCalibrationCliTests.validateSpecMapsNestedWrongTypesToInvalidSpec
              "Blender calibration path-length boundaries are enforced",
              BlenderCalibrationCliTests.validateSpecPathLengthBoundariesAreEnforced
              "Blender calibration NFC paths use minimal UTF-8 JSON",
              BlenderCalibrationCliTests.validateSpecEnvelopeUsesMinimalUtf8ForNfcPaths
              "Blender calibration inspect CLI envelope and exit mapping are canonical",
              BlenderCalibrationCliTests.inspectEnvelopeAndExitMappingAreCanonical
              "Blender calibration wrapper is closed and ignores host injection environment",
              BlenderCalibrationWrapperTests.validateSpecWrapperIsClosedAndIgnoresHostInjectionEnvironment
              "Asset job journal schema is closed and canonical",
              AssetJobJournalTests.schemaIsClosedOfflineAndCanonicalEntryIsValid
              "Asset job journal state graph and hash chain are strict",
              AssetJobJournalTests.stateGraphHashChainAndCanonicalBytesAreStrict
              "Asset job lock rejects concurrent recovery", AssetJobJournalTests.exclusiveLockRejectsConcurrentRecovery
              "Asset job publication and recovery are idempotent",
              AssetJobJournalTests.publicationAndRecoveryAreIdempotent
              "Asset job crash points remain recoverable", AssetJobJournalTests.crashPointsRemainRecoverable
              "Asset job recovery rejects changed, linked and corrupt ownership",
              AssetJobJournalTests.recoveryRefusesChangedSymlinkAndCorruptOwnership
              ".NET asset generator output passes the independent inspector",
              DotnetAssetGeneratorTests.generatedArtifactsPassTheIndependentInspector
              ".NET asset generator is byte-identical across isolated workspaces",
              DotnetAssetGeneratorTests.equalInputsAreByteIdenticalAcrossIsolatedWorkspaces
              ".NET asset generator alternate seed changes geometry and preview",
              DotnetAssetGeneratorTests.alternateSeedChangesGeometryAndPreviewButKeepsTheContract
              ".NET asset generator stage path and collisions fail closed",
              DotnetAssetGeneratorTests.stagePathAndCollisionFailClosed
              ".NET asset generator sources and toolchain are bounded",
              DotnetAssetGeneratorTests.sourceAndToolchainInputsAreRequiredAndBounded
              ".NET asset generator has no process, network or native escape hatches",
              DotnetAssetGeneratorTests.productionPathHasNoProcessNetworkOrNativeEscapeHatches
              ".NET asset CI evidence is deterministic, closed and sanitized",
              DotnetAssetCiEvidenceTests.deterministicEvidenceIsSchemaClosedAndSanitized
              ".NET asset CI evidence rejects unsafe workspaces", DotnetAssetCiEvidenceTests.unsafeWorkspaceFailsClosed
              ".NET asset CI workflow is dedicated, bounded and least-privileged",
              DotnetAssetCiPolicyTests.workflowHasOnlyTheDedicatedBoundedLinuxJob
              ".NET asset CI shell is commit-clean, bounded and sanitized",
              DotnetAssetCiPolicyTests.freshCheckoutShellIsClosedAndSanitizesEvidence
              ".NET asset CI contract uses only the active .NET identity",
              DotnetAssetCiPolicyTests.activeContractUsesOnlyDotnetIdentitySourcesAndPin
              ".NET asset pipeline publishes a committed T-003 quarantine",
              DotnetAssetPipelineTests.happyPathPublishesCommittedT003Quarantine
              ".NET asset pipeline is byte-identical for equal inputs",
              DotnetAssetPipelineTests.equalInputsProduceByteIdenticalArtifacts
              ".NET asset pipeline recovery preserves tampered foreign bytes",
              DotnetAssetPipelineTests.committedTamperFailsRecoveryWithoutDeletingForeignBytes
              ".NET asset pipeline rejects target collisions without overwrite",
              DotnetAssetPipelineTests.targetCollisionIsRejectedWithoutOverwrite
              ".NET asset pipeline receipt collision leaves no stage or publication",
              DotnetAssetPipelineTests.receiptTargetCollisionLeavesNoStageOrPublication
              ".NET asset pipeline fails the run and rolls back pre-finish artifact errors",
              DotnetAssetPipelineTests.preFinishArtifactFailureCreatesFailedRunAndRollsBack
              ".NET asset pipeline production spec, pin and job limits fail closed",
              DotnetAssetPipelineTests.productionSpecPinAndJobLimitsFailClosed
              ".NET asset pipeline cancellation rolls back before publication",
              DotnetAssetPipelineTests.cancellationFailsBeforePublicationAndRollsBack
              ".NET asset CLI exposes new read-only commands and legacy aliases",
              DotnetAssetPipelineCliTests.validateAndInspectUseNewNamespaceWithLegacyReadOnlyAliases
              ".NET asset CLI legacy alias rejects mutation",
              DotnetAssetPipelineCliTests.legacyAliasRejectsGenerateAndRecover
              ".NET asset CLI generate and recover envelopes are canonical",
              DotnetAssetPipelineCliTests.generateAndRecoverEnvelopesAreCanonicalAndUseFixedActor
              ".NET asset CLI maps pipeline failures to stable redacted exits",
              DotnetAssetPipelineCliTests.pipelineFailuresMapStableRedactedExitCodes
              ".NET asset wrapper is closed and ignores host injection",
              DotnetAssetPipelineCliTests.assetCalibrationWrapperIsClosedAndIgnoresHostInjection
              "Blender calibration GLB reference fixture is accepted",
              Asset3dInspectorTests.glbReferenceFixtureIsAccepted
              "Blender calibration GLB topology tampering is rejected",
              Asset3dInspectorTests.glbTopologyTamperingIsRejected
              "Blender calibration GLB alignment and JSON types are rejected",
              Asset3dInspectorTests.glbAlignmentAndJsonTypesAreRejected
              "Blender calibration normalized PNG is accepted", Asset3dInspectorTests.normalizedPngIsAccepted
              "Blender calibration trailing Deflate data is rejected",
              Asset3dInspectorTests.pngTrailingDeflateDataIsRejected
              "Blender calibration complete inspection round-trip is accepted",
              Asset3dInspectorTests.completeInspectionRoundTripIsAccepted
              "Blender calibration technique-report cross-field matrix is rejected",
              Asset3dInspectorTests.techniqueReportCrossFieldMatrixIsRejected
              "Blender calibration unsafe Unicode artifact paths are rejected",
              Asset3dInspectorTests.unsafeUnicodeArtifactPathsAreRejected
              "Blender calibration malformed toolchain pin is an artifact failure",
              Asset3dInspectorTests.malformedToolchainPinIsArtifactFailure
              "Asset repository quarantine fixtures are valid", Tests.assetRepositoryQuarantineFixturesAreValid
              "Asset schema is strict, offline and short-circuits cross-fields",
              Tests.assetSchemaIsStrictOfflineAndShortCircuitsCrossFields
              "Asset clean-room findings are redacted and require flags work",
              Tests.assetCleanRoomFindingsAreRedactedAndRequireFlagsWork
              "Asset receipt binds all core anchors", Tests.assetReceiptBindsAllCoreAnchors
              "Procedural asset export round-trip is valid", AssetCanonicalTests.proceduralExportRoundTripIsValid
              "Portable receipt chronology is bound", AssetCanonicalTests.portableReceiptChronologyIsBound
              "Exactly one generation event is required", AssetCanonicalTests.exactlyOneGenerationEventIsRequired
              "Run actor metadata is immutable and verified", AssetCanonicalTests.runActorMetadataIsImmutableAndVerified
              "Canonical prompt tamper cannot be rehashed offline",
              AssetCanonicalTests.canonicalPromptTamperCannotBeRehashedOffline
              "Canonical negative prompt tamper cannot be rehashed offline",
              AssetCanonicalTests.canonicalNegativePromptTamperCannotBeRehashedOffline
              "Fake approval without bound runs and evidence fails closed",
              AssetCanonicalTests.fakeApprovalWithoutBoundRunsAndEvidenceFailsClosed
              "Approved procedural asset accepts five bound review runs",
              AssetCanonicalTests.approvedProceduralAssetRequiresAndAcceptsFiveBoundReviewRuns
              "Binary content behind text extension is rejected",
              AssetCanonicalTests.binaryContentBehindTextExtensionIsRejected
              "Text source is bound to raw Git-index bytes", AssetCanonicalTests.textSourceIsBoundToRawGitIndexBytes
              "Procedural Python source is clean-room scanned",
              AssetCanonicalTests.proceduralPythonSourceIsCleanRoomScanned
              "Targeted approved scan is never shipping-ready",
              AssetCanonicalTests.targetedApprovedScanIsNeverShippingReady
              "Review evidence directory fails controlled", AssetCanonicalTests.reviewEvidenceDirectoryFailsControlled
              "Review timestamp is bound to evidence and run",
              AssetCanonicalTests.reviewTimestampIsBoundToEvidenceAndRun
              "Review cannot predate generation completion", AssetCanonicalTests.reviewCannotPredateGenerationCompletion
              "License-basis time matches active license review",
              AssetCanonicalTests.licenseBasisTimeMustMatchActiveLicenseReview
              "Approved review state matrix is enforced", AssetCanonicalTests.approvedReviewStateMatrixIsEnforced
              "Nested duplicate run payload is rejected", AssetCanonicalTests.nestedDuplicateRunPayloadIsRejected
              "Persisted nested duplicate run data is rejected",
              AssetCanonicalTests.persistedNestedDuplicateRunDataIsRejected
              "Review evidence content is clean-room scanned",
              AssetCanonicalTests.reviewEvidenceContentIsCleanRoomScanned
              "Asset traversal output path is rejected", AssetRegression.traversalOutputPathIsRejected
              "Asset trust-root symlinks fail closed", AssetRegression.trustRootSymlinksFailClosed
              "Asset input symlink is rejected", AssetRegression.assetInputSymlinkIsRejected
              "Invalid asset receipt and policy schemas fail closed",
              AssetRegression.invalidReceiptAndPolicySchemasFailClosed
              "Manifest required-field matrix is strict", AssetRegression.manifestRequiredFieldMatrixIsStrict
              "Duplicate model-lock tuple is rejected", AssetRegression.duplicateModelLockTupleIsRejected
              "Model-lock status matches approved entries", AssetRegression.modelLockStatusMustMatchApprovedEntries
              "Approved local model requires artifact hash", AssetRegression.approvedLocalModelRequiresArtifactHash
              "Tracked asset source orphan is rejected", AssetRegression.trackedSourceOrphanIsRejected
              "Duplicate manifest identity and receipt are rejected",
              AssetRegression.duplicateManifestIdentityAndReceiptAreRejected
              "Clean-room filename and property key are redacted",
              AssetRegression.cleanRoomFilenameAndPropertyKeyAreRedacted
              "Clean-room deny/allow collision is rejected", AssetRegression.cleanRoomPolicyDenyAllowCollisionIsRejected
              "Allowed-name register is recognized with deny precedence",
              AssetRegression.allowedNameRegisterIsRecognizedWithDenyPrecedence
              "Clean-room scans specification content", AssetRegression.cleanRoomScansSpecificationContent
              "Unsafe asset Unicode is rejected and redacted", AssetRegression.unsafeUnicodeIsRejectedAndRedacted
              "Hash-named metadata cannot bypass clean-room", AssetRegression.hashNamedMetadataCannotBypassCleanRoom
              "Oversized asset integers fail controlled", AssetRegression.oversizedAssetIntegersFailControlled
              "Oversized manifest file fails controlled", AssetRegression.oversizedManifestFileFailsControlled
              "Whitespace rights and actors are rejected", AssetRegression.whitespaceRightsAndActorsAreRejected
              "Asset review history must be contiguous", AssetRegression.reviewHistoryMustBeContiguous
              "Malformed Git-LFS pointer is rejected", AssetRegression.malformedLfsPointerIsRejected
              "Large fake-git output does not deadlock", AssetRegression.fakeGitLargeOutputDoesNotDeadlock
              "Fake-git timeout fails controlled", AssetRegression.fakeGitTimeoutFailsControlled
              "Run IDs are time-sortable", Tests.runIdsAreTimeSortable
              "Run lifecycle is hashed and redacted", Tests.runLifecycleIsHashedAndRedacted
              "Event envelope is strict and payload remains structured",
              Tests.eventEnvelopeIsStrictAndPayloadRemainsStructured
              "Logging configuration limits and redacts", Tests.loggingConfigurationLimitsAndRedacts
              "RAG is deterministic and cites sources", Tests.ragIsDeterministicAndCitesSources
              "Hardware question ranks performance budget first", Tests.hardwareQuestionRanksPerformanceBudgetFirst
              "RAG honors context character budget", Tests.ragHonorsContextCharacterBudget
              "RAG indexes only current accepted memory", Tests.ragIndexesOnlyCurrentAcceptedMemory
              "Memory lifecycle is explicit append-only and tamper-evident",
              Tests.memoryLifecycleIsExplicitAppendOnlyAndTamperEvident
              "Memory conflicts and statuses are excluded and reported",
              Tests.memoryConflictsAndStatusesAreExcludedAndReported
              "Memory freshness limits and paths are fail-closed", Tests.memoryFreshnessLimitAndPathsAreFailClosed
              "Retrieval traces are deterministic, chained, redacted and bounded",
              Tests.retrievalTracesAreDeterministicChainedRedactedAndBounded
              "RAG excludes configured paths, secrets and symlinks", Tests.ragExcludesConfiguredPathsSecretsAndSymlinks
              "Init is idempotent and validates configuration", Tests.initIsIdempotentAndValidatesConfiguration
              "Fixed v1 configuration rejects pretend options", Tests.fixedV1ConfigurationRejectsPretendOptions
              "T-004 provenanced runs record complete manifests with Git",
              RunProvenanceTests.provenancedRunsRecordCompleteManifests
              "T-004 provenance completeness stays explicit without Git",
              RunProvenanceTests.provenanceCompletenessStaysExplicitWithoutGit
              "T-004 span envelope contract rejects broken envelopes",
              RunProvenanceTests.spanEnvelopeContractRejectsBrokenEnvelopes
              "T-004 span envelope chain end-to-end verifies", RunProvenanceTests.spanEnvelopeChainEndToEndVerifies
              "T-004 duplicate evidence spans fail verification",
              RunProvenanceTests.duplicateEvidenceSpansFailVerification
              "T-004 retrieval events require known trace hashes",
              RunProvenanceTests.retrievalEventsRequireKnownTraceHashes
              "T-004 RAG build manifest is deterministic and binds inputs",
              RunProvenanceTests.ragBuildManifestIsDeterministicAndBindsInputs
              "T-004 retention plans are read-only and executions stay guarded",
              RunProvenanceTests.retentionPlansAreReadOnlyAndExecutionsStayGuarded
              "T-004 evidence payloads redact secrets and reject foreign fields",
              RunProvenanceTests.evidencePayloadsRedactSecretsAndRejectForeignFields
              "T-010 toolchain pins and read-only bootstrap contract pass",
              ToolchainCheckTests.repositoryToolchainPassesAllChecks
              "T-010 bootstrap rejects a colliding PATH file", ToolchainCheckTests.dotnetBootstrapRejectsCollidingPath
              "T-010 tampered source hash is rejected", ToolchainCheckTests.tamperedSourceHashIsRejected
              "T-010 intact cache passes the source crosscheck", ToolchainCheckTests.intactCachePassesCrosscheck
              "T-010 missing license entry is rejected", ToolchainCheckTests.missingLicenseEntryIsRejected
              "T-010 inconsistent bgfx cohort is rejected", ToolchainCheckTests.inconsistentBgfxCohortIsRejected
              "T-010 missing notices entry is rejected", ToolchainCheckTests.missingNoticesEntryIsRejected
              "T-010 forbidden ISA flag in build sources is rejected",
              ToolchainCheckTests.forbiddenIsaFlagInBuildSourcesIsRejected
              "T-010 documented x86-64-v2 base stays allowed", ToolchainCheckTests.cleanSourcesPassIsaScan
              "T-010 artifact validation accepts intact fixtures",
              PlatformInteropTests.artifactValidationAcceptsIntactFixtures
              "T-010 artifact fault classes fail controlled", PlatformInteropTests.artifactFaultClassesFailControlled
              "T-010 artifact validation never writes", PlatformInteropTests.artifactValidationNeverWrites
              "T-010 exit code mapping is stable and documented",
              PlatformInteropTests.exitCodeMappingIsStableAndDocumented
              "T-010 SDL session ownership rules are explicit", PlatformInteropTests.sdlSessionOwnershipRulesAreExplicit
              "T-010 bgfx initialization errors are translated",
              PlatformInteropTests.bgfxDeviceTranslatesInitializationErrors
              "T-010 bgfx handle ownership and shutdown order are enforced",
              PlatformInteropTests.bgfxHandleOwnershipAndShutdownOrderAreEnforced
              "T-010 invalid native handles are translated", PlatformInteropTests.invalidNativeHandlesAreTranslated
              "T-010 architecture keeps native imports inside the platform layer",
              PlatformInteropTests.architectureKeepsNativeImportsInsidePlatformLayer
              "T-020 percentile band matches golden fixtures", BenchEmptyTests.percentileBandMatchesGoldenFixtures
              "T-020 budget gate golden fixtures cover every class",
              BenchEmptyTests.budgetGateGoldenFixturesCoverEveryClass
              "T-020 report schema accepts golden and rejects fabrication matrix",
              BenchEmptyTests.reportSchemaAcceptsGoldenAndRejectsFabricationMatrix
              "T-020 camera path is deterministic with hash fixture",
              BenchEmptyTests.cameraPathIsDeterministicWithHashFixture
              "T-020 camera matrices stay finite and sane", BenchEmptyTests.cameraMatricesStayFiniteAndSane
              "T-020 profile binding honesty matrix is enforced", BenchEmptyTests.profileBindingHonestyMatrixIsEnforced
              "T-020 scenario registry rejects unknown and unimplemented",
              BenchEmptyTests.scenarioRegistryRejectsUnknownAndUnimplemented
              "T-020 exit codes 25-28 are stable and documented",
              BenchEmptyTests.exitCodesTwentyFiveToTwentyEightAreStableAndDocumented
              "T-020 CLI contract fails controlled without reports",
              BenchEmptyTests.cliContractFailsControlledWithoutReports
              "T-020 unwritable report path fails controlled", BenchEmptyTests.unwritableReportPathFailsControlled
              "T-020 structure equality ignores measurement drift but detects shape changes",
              BenchEmptyTests.structureEqualityIgnoresMeasurementDriftButDetectsShapeChanges
              "T-020 rift script bench contract keeps app build guard",
              BenchEmptyTests.riftScriptBenchContractKeepsAppBuildGuard
              "T-021 simulation contract mirrors documented values",
              BenchSimTests.simulationContractMirrorsDocumentedValues
              "T-021 world simulates exactly 250 fully simulated agents in spawn zones",
              BenchSimTests.worldSimulatesExactly250FullySimulatedAgentsInSpawnZones
              "T-021 determinism identical chains for fresh worlds with golden fixture",
              BenchSimTests.determinismIdenticalChainsForFreshWorldsWithGoldenFixture
              "T-021 changed seed or reordered commands change outcome",
              BenchSimTests.changedSeedOrReorderedCommandsChangeOutcome
              "T-021 canonical command order is enforced internally",
              BenchSimTests.canonicalCommandOrderIsEnforcedInternally
              "T-021 path budget is capped per tick and requests complete",
              BenchSimTests.pathBudgetIsCappedPerTickAndRequestsComplete
              "T-021 agents remain on walkable tiles and move substantially",
              BenchSimTests.agentsRemainOnWalkableTilesAndMoveSubstantially
              "T-021 report schema accepts golden and rejects fabrication matrix",
              BenchSimTests.reportSchemaAcceptsGoldenAndRejectsFabricationMatrix
              "T-021 sim budget gate covers every class fail closed",
              BenchSimTests.simBudgetGateCoversEveryClassFailClosed
              "T-021 scenario registry classifies implemented and pending scenarios",
              BenchSimTests.scenarioRegistryClassifiesImplementedAndPendingScenarios
              "T-021 CLI contract runs headless simulation with reports",
              BenchSimTests.cliContractRunsHeadlessSimulationWithReports
              "T-021 unwritable sim report path fails controlled", BenchSimTests.unwritableSimReportPathFailsControlled
              "T-021 exit code mapping stays stable including sim reuse",
              BenchSimTests.exitCodeMappingStaysStableIncludingSimReuse
              "T-021 architecture keeps simulation project pure", BenchSimTests.architectureKeepsSimulationProjectPure
              "T-023 scenario config mirrors documented budget table",
              BenchRepresentativeTests.scenarioConfigMirrorsDocumentedBudgetTable
              "T-023 composition targets produce non-degenerate geometry",
              BenchRepresentativeTests.compositionTargetsProduceNonDegenerateGeometry
              "T-023 landscape height field is deterministic and walkable aware",
              BenchRepresentativeTests.landscapeHeightFieldIsDeterministicAndWalkableAware
              "T-023 rig evaluates 48 bone palette deterministically",
              BenchRepresentativeTests.rigEvaluatesFortyEightBonePaletteDeterministically
              "T-023 camera flight is deterministic and canonical",
              BenchRepresentativeTests.cameraFlightIsDeterministicAndCanonical
              "T-023 schedule math binds ticks to frames and capture after window",
              BenchRepresentativeTests.scheduleMathBindsTicksToFramesAndCaptureAfterWindow
              "T-023 representative budget gate covers every class fail closed",
              BenchRepresentativeTests.representativeBudgetGateCoversEveryClassFailClosed
              "T-023 frame evidence encoder and policy are bound",
              BenchRepresentativeTests.frameEvidenceEncoderAndPolicyAreBound
              "T-023 report schema accepts golden and rejects fabrication matrix",
              BenchRepresentativeTests.reportSchemaAcceptsGoldenAndRejectsFabricationMatrix
              "T-023 profile binding stays honest for representative scenario",
              BenchRepresentativeTests.profileBindingStaysHonestForRepresentativeScenario
              "T-023 CLI contract keeps registry and error paths controlled",
              BenchRepresentativeTests.cliContractKeepsRegistryAndErrorPathsControlled
              "T-023 architecture keeps snapshot direction and hot path discipline",
              BenchRepresentativeTests.architectureKeepsSnapshotDirectionAndHotPathDiscipline
              "T-022 soak plan math is pure and contract bound", SoakTests.soakPlanMathIsPureAndContractBound
              "T-022 progress watchdog detects stalls with injected clock",
              SoakTests.progressWatchdogDetectsStallsWithInjectedClock
              "T-022 memory analysis separates trend from noise", SoakTests.memoryAnalysisSeparatesTrendFromNoise
              "T-022 window series matches telemetry math and enforces order",
              SoakTests.windowSeriesMatchesTelemetryMathAndEnforcesOrder
              "T-022 pacing independence identical chains between modes",
              SoakTests.pacingIndependenceIdenticalChainsBetweenModes
              "T-022 foreign seed changes engine outcome", SoakTests.foreignSeedChangesEngineOutcome
              "T-022 engine binds simulation contract", SoakTests.engineBindsSimulationContract
              "T-022 soak scenario registry classifies explicitly", SoakTests.soakScenarioRegistryClassifiesExplicitly
              "T-022 exit code mapping includes soak codes", SoakTests.exitCodeMappingIncludesSoakCodes
              "T-022 rift script soak contract keeps app build guard",
              SoakTests.riftScriptSoakContractKeepsAppBuildGuard
              "T-022 architecture keeps soak clock free and layered", SoakTests.architectureKeepsSoakClockFreeAndLayered
              "T-022 soak contract mirrors documented values", SoakTests.soakContractMirrorsDocumentedValues
              "T-022 soak gate covers every class fail closed", SoakTests.soakGateCoversEveryClassFailClosed
              "T-022 soak report schema accepts golden and rejects fabrication matrix",
              SoakTests.soakReportSchemaAcceptsGoldenAndRejectsFabricationMatrix
              "T-022 tracked evidence fixture stays portable without host paths",
              SoakTests.soakTrackedEvidenceFixtureStaysPortableWithoutHostPaths
              "T-022 soak chain fixture is bound to contract and plan",
              SoakTests.soakChainFixtureIsBoundToContractAndPlan
              "T-022 CLI contract runs diagnostic soak with reports", SoakTests.cliContractRunsDiagnosticSoakWithReports
              "T-022 evidence unit classification is fail closed", SoakTests.soakEvidenceUnitClassificationIsFailClosed
              "T-022 exit mapping is stable and documented", SoakTests.soakExitMappingIsStableAndDocumented
              "T-022 watchdog stall with frozen progress and running clock",
              SoakTests.watchdogStallWithFrozenProgressAndRunningClock
              "T-031 save contract mirrors documented values", SaveTests.saveContractMirrorsDocumentedValues
              "T-031 payload byte identity and metadata delineation",
              SaveTests.payloadByteIdentityAndMetadataDelineation
              "T-031 continuation equality against interrupted reference run",
              SaveTests.continuationEqualityAgainstReferenceRun
              "T-031 corruption matrix rejects every class distinctly",
              SaveTests.corruptionMatrixRejectsEveryClassDistinctly
              "T-031 display metadata remains readable without payload",
              SaveTests.displayMetadataRemainsReadableWithoutPayload
              "T-031 slot protocol fault injection preserves last valid state",
              SaveTests.slotProtocolFaultInjectionPreservesLastValidState
              "T-031 savecheck gate covers every class fail closed", SaveTests.savecheckGateCoversEveryClassFailClosed
              "T-031 migration rules are strictly monotonic and idempotent on copy",
              SaveTests.migrationRulesAreStrictlyMonotonicAndIdempotentOnCopy
              "T-031 report schema accepts golden and rejects fabrication matrix",
              SaveTests.reportSchemaAcceptsGoldenAndRejectsFabricationMatrix
              "T-031 exit code mapping stays stable including save codes",
              SaveTests.exitCodeMappingStaysStableIncludingSaveCodes
              "T-031 architecture keeps save project pure and binds sim state access",
              SaveTests.architectureKeepsSaveProjectPureAndBindsSimStateAccess
              "T-031 rift script savecheck contract keeps app build guard",
              SaveTests.riftScriptSavecheckContractKeepsAppBuildGuard
              "T-031 CLI contract runs savecheck with reports and controlled failures",
              SaveTests.cliContractRunsSavecheckWithReportsAndControlledFailures
              "T-032 session contract mirrors documented values",
              CommandLoopTests.sessionContractMirrorsDocumentedValues
              "T-032 parser accepts canonical script and binds hashes",
              CommandLoopTests.parserAcceptsCanonicalScriptAndBindsHashes
              "T-032 parser rejects every class distinctly", CommandLoopTests.parserRejectsEveryClassDistinctly
              "T-032 script hash binds raw bytes and rejects invalid encodings",
              CommandLoopTests.scriptHashBindsRawBytesAndRejectsInvalidEncodings
              "T-032 intent codec golden encoding is bound", CommandLoopTests.intentCodecGoldenEncoding
              "T-032 canonical ordering makes reordering irrelevant",
              CommandLoopTests.canonicalOrderingMakesReorderingIrrelevant
              "T-032 determinism identical chains and foreign seed sensitivity",
              CommandLoopTests.determinismIdenticalChainsAndForeignSeedSensitivity
              "T-032 selection semantics v0 point box clear", CommandLoopTests.selectionSemanticsV0PointBoxClear
              "T-032 move maps onto the unchanged kernel command surface",
              CommandLoopTests.moveMapsToKernelCommandSurface
              "T-032 reaction gate positive proof and fault injection matrix",
              CommandLoopTests.reactionGatePositiveProofAndFaultInjectionMatrix
              "T-032 allocation strictness regression stays at zero bytes",
              CommandLoopTests.allocationStrictnessRegression
              "T-032 camera model clamps world edges and zoom", CommandLoopTests.cameraModelClampsWorldEdgesAndZoom
              "T-032 pan directions match edge pan and north-up contract",
              CommandLoopTests.panDirectionsMatchEdgePanAndNorthUpContract
              "T-032 keymap validates semantic actions against defaults",
              CommandLoopTests.keymapValidatesSemanticActionsAgainstDefaults
              "T-032 architecture keeps the session core pure", CommandLoopTests.architectureKeepsSessionPure
              "T-032 exit code mapping stays stable including command codes",
              CommandLoopTests.exitCodeMappingStaysStableIncludingCommandCodes
              "T-032 interactive exit code precedence stays window bound",
              CommandLoopTests.interactiveExitCodePrecedenceStaysWindowBound
              "T-032 interactive auto-exit stays explicit and horizon bound",
              CommandLoopTests.interactiveAutoExitRemainsExplicitAndHorizonBound
              "T-032 CLI contract runs headless loops with controlled failures",
              CommandLoopTests.cliContractRunsHeadlessWithReportsAndControlledFailures
              "T-032 engine run is hermetic", CommandLoopTests.engineRunIsHermetic
              "T-032 headless working set reason binds contract truthfulness",
              CommandLoopTests.headlessWorkingSetReasonBindsContractTruthfulness
              "T-032 rift script contract keeps app build guard",
              CommandLoopTests.riftScriptKommandoschleifeContractKeepsAppBuildGuard
              "T-032 report schema accepts golden and rejects fabrication matrix",
              CommandLoopTests.reportSchemaAcceptsGoldenAndRejectsFabricationMatrix
              "T-032 dispatched move zones bind accepted kernel commands only",
              CommandLoopTests.dispatchedMoveZonesBindAcceptedCommandsOnly
              "T-032 interactive command pulse renders then expires",
              CommandLoopTests.interactiveCommandPulseRendersThenExpires
              "T-032 runner wires the second feedback channel to its source data",
              CommandLoopTests.commandLoopRunnerWiresTheSecondFeedbackChannelToItsSourceData
              "T-032 input script reading is bounded at the contract byte limit",
              CommandLoopTests.inputScriptReadingIsBoundedAtTheContractByteLimit
              "T-032 contract names the rejection causes verbatim",
              CommandLoopTests.contractNamesTheRejectionCausesVerbatim
              "T-032 runner surfaces the rejection causes on their live paths",
              CommandLoopTests.runnerSurfacesTheRejectionCausesOnTheirLivePaths
              "T-033 mode contract mirrors documented values", ModeSwitchTests.modeContractMirrorsDocumentedValues
              "T-033 keymap binds mode-switch without collisions",
              ModeSwitchTests.keymapBindsModeSwitchWithoutCollisions
              "T-033 v2 parser accepts superset grammar and binds hashes",
              ModeSwitchTests.parserV2AcceptsSupersetGrammarAndBindsHashes
              "T-033 parser rejects every malformed class distinctly",
              ModeSwitchTests.parserRejectsEveryMalformedClassDistinctly
              "T-033 intent codec encodes mode kinds deterministically",
              ModeSwitchTests.intentCodecEncodesModeKindsDeterministically
              "T-033 context rejection matrix binds distinct dispositions without kernel commands",
              ModeSwitchTests.contextRejectionMatrixBindsDistinctDispositionsWithoutKernelCommands
              "T-033 same-tick switch is evaluated last and effective at S+2",
              ModeSwitchTests.sameTickSwitchIsEvaluatedLastAndEffectiveAtSPlusTwo
              "T-033 consecutive tick switches follow the canonical evaluation basis",
              ModeSwitchTests.consecutiveTickSwitchesFollowCanonicalEvaluationBasis
              "T-033 switches near horizon stay bound as ineffective",
              ModeSwitchTests.switchesNearHorizonStayBoundAsIneffective
              "T-033 hero direction steering resolves exactly with explicit tie break",
              ModeSwitchTests.heroDirectionSteeringResolvesExactlyWithExplicitTieBreak
              "T-033 interactive report schema binds capture pair and hud",
              ModeSwitchTests.interactiveReportSchemaBindsCapturePairAndHud
              "T-033 interactive hybrid wiring is bound to sources",
              ModeSwitchTests.interactiveHybridWiringIsBoundToSources
              "T-033 personal mode hides strategic selection glyphs without state loss",
              ModeSwitchTests.personalModeHidesStrategicSelectionGlyphs
              "T-034 diamond markers and hero-local landmark cue stay readable",
              ModeSwitchTests.diamondMarkersAndHeroLocalLandmarkCueStayReadable
              "T-033 strategic capture focuses hero without mutating session camera",
              ModeSwitchTests.strategicCaptureCameraFocusesHeroWithoutMutatingSessionCamera
              "T-033 personal camera frustum keeps ground readable at world edges",
              ModeSwitchTests.personalCameraFrustumKeepsGroundReadableAtWorldEdges
              "T-033 billboard basis stays camera-facing and isotropic",
              ModeSwitchTests.billboardBasisStaysCameraFacingAndIsotropic
              "T-033 capture pair validation is fail closed", ModeSwitchTests.capturePairValidationIsFailClosed
              "T-033 hybrid flow twin continuity identical chains and end hash",
              ModeSwitchTests.hybridFlowTwinContinuityIdenticalChainsAndEndHash
              "T-033 steering produces the kernel command equivalent to the direct surface",
              ModeSwitchTests.steeringProducesKernelCommandEquivalentToDirectSurface
              "T-033 switch reaction gate criterion is fail closed without vacuum pass",
              ModeSwitchTests.commandGateSwitchReactionCriterionIsFailClosedWithoutVacuumPass
              "T-033 CLI contract runs v2 hybrid headless with mode report and twin",
              ModeSwitchTests.cliContractRunsV2HybridHeadlessWithModeReportAndTwin
              "T-034 exploration contract mirrors documented values",
              ExplorationTests.explorationContractMirrorsDocumentedValues
              "T-034 landmark derivation is deterministic, zonal and walkable",
              ExplorationTests.landmarkDerivationIsDeterministicZonalAndWalkable
              "T-034 landmark derivation fails closed without a walkable tile",
              ExplorationTests.landmarkDerivationFailsClosedWithoutWalkableTile
              "T-034 exploration views resist external mutation",
              ExplorationTests.explorationViewsResistExternalMutation
              "T-034 observation enforces mode coupling and single registration",
              ExplorationTests.observationEnforcesModeCouplingAndSingleRegistration
              "T-034 final-boundary HUD matches measured report before auto-exit",
              ExplorationTests.finalBoundaryHudMatchesMeasuredReportBeforeAutoExit
              "T-034 exploration is observation-only; twin stays byte identical",
              ExplorationTests.explorationObservationIsObservationOnlyTwinStaysByteIdentical
              "T-034 foreign seed changes hashes but not the landmark set",
              ExplorationTests.foreignSeedChangesHashesButNotLandmarkSet
              "T-034 headless exploration visits all landmarks on schema version 3",
              ExplorationTests.headlessExplorationRunVisitsAllLandmarksOnSchemaVersion3
              "T-034 legacy run without exploration stays byte identical on schema version 2",
              ExplorationTests.legacyRunWithoutExplorationStaysByteIdenticalSchema2
              "T-034 schema dispatch rejects cross variants fail closed",
              ExplorationTests.explorationSchemaDispatchRejectsCrossVariants
              "T-034 schema relations reject fabricated evidence fail closed",
              ExplorationTests.explorationSchemaRelationsRejectFabrication
              "T-035 decision contract mirrors documented values", DecisionTests.decisionContractMirrorsDocumentedValues
              "T-035 options derivation is pure, seed independent and fails closed",
              DecisionTests.optionsDerivationIsPureSeedIndependentAndFailsClosed
              "T-035 decision offer, choice and follow-up bind contractually",
              DecisionTests.decisionOfferChoosesAndFollowUpBindContractually
              "T-035 follow-up requires personal arrival including mode coupling",
              DecisionTests.decisionFollowUpRequiresPersonalArrival
              "T-035 decision is observation only; twin stays byte identical",
              DecisionTests.decisionIsObservationOnlyTwinStaysByteIdentical
              "T-035 not-activated rejection is distinguished without kernel effect",
              DecisionTests.decisionNotActivatedRejectionIsDistinguishedWithoutKernelEffect
              "T-035 title HUD binds decision states without changing legacy form",
              DecisionTests.titleHudBindsDecisionStatesWithoutChangingLegacyForm
              "T-035 CLI decision flow runs headless on schema version 4",
              DecisionTests.cliDecisionFlowRunsHeadlessOnSchemaVersion4
              "T-035 CLI A/B choice pair differs only in the decision report",
              DecisionTests.cliAbChoicePairDiffersOnlyInDecisionReport
              "T-035 CLI choose without activation and usage coupling stay contractual",
              DecisionTests.cliChooseWithoutActivationAndUsageCouplingStayContractual
              "T-035 CLI choose tokens under legacy headers are unknown actions",
              DecisionTests.cliChooseTokensUnderLegacyHeadersAreUnknownActions
              "T-035 schema dispatch rejects cross variants fail closed",
              DecisionTests.decisionSchemaDispatchRejectsCrossVariants
              "T-035 schema relations reject fabricated evidence fail closed",
              DecisionTests.decisionSchemaRelationsRejectFabrication
              "T-035 builder honesty binds decision presentation flags",
              DecisionTests.decisionBuilderEhrlichkeitBindetDarstellungsausweise
              "T-036 pressure contract mirrors documented values", PressureTests.pressureContractMirrorsDocumentedValues
              "T-036 window trigger is decision coupled once per cycle and honest without decision",
              PressureTests.windowTriggerIsDecisionCoupledOncePerCycleAndHonestWithoutDecision
              "T-036 time basis binds expiry exactness and arrival ordering",
              PressureTests.timeBasisExpiryExactnessAndArrivalOrdering
              "T-036 pressure is observation only; twin stays byte identical",
              PressureTests.pressureIsObservationOnlyTwinStaysByteIdentical
              "T-036 CLI pressure flow runs headless on schema version 5",
              PressureTests.cliPressureFlowRunsHeadlessOnSchemaVersion5
              "T-036 foreign seed changes hashes but pressure structure follows session",
              PressureTests.foreignSeedChangesHashesButPressureStructureFollowsSession
              "T-036 pressure without reached decision carries honest ground",
              PressureTests.pressureWithoutReachedDecisionCarriesHonestGround
              "T-036 pressure activation coupling stays usage error",
              PressureTests.pressureActivationCouplingStaysUsageError
              "T-036 incomplete report preserves pressure activation",
              PressureTests.pressureIncompleteReportPreservesActivation
              "T-036 title HUD binds pressure states without changing legacy form",
              PressureTests.titleHudBindsPressureStatesWithoutChangingLegacyForm
              "T-036 schema dispatch rejects cross variants fail closed",
              PressureTests.pressureSchemaDispatchRejectsCrossVariants
              "T-036 schema relations reject fabricated evidence fail closed",
              PressureTests.pressureSchemaRelationsRejectFabrication
              "T-037 session section codec roundtrip is byte identical per layer",
              ContinuationTests.sessionSectionCodecRoundtripIsByteIdenticalPerLayer
              "T-037 session section codec rejects corruption matrix",
              ContinuationTests.sessionSectionCodecRejectsCorruptionMatrix
              "T-037 V2 envelope roundtrip and legacy V1 honest emptiness",
              ContinuationTests.v2EnvelopeRoundtripAndLegacyV1Emptiness
              "T-037 untrusted slot activation guards reject mismatch",
              ContinuationTests.untrustedSlotActivationGuardsRejectMismatch
              "T-037 headless continuation chain is byte identical over boundary",
              ContinuationTests.headlessContinuationChainIsByteIdenticalOverBoundary
              "T-037 foreign seed changes hashes but continuity holds",
              ContinuationTests.foreignSeedChangesHashesButContinuityHolds
              "T-037 CLI continuation flow runs save and load on schema version 6",
              ContinuationTests.cliContinuationFlowRunsSaveAndLoadOnSchemaVersion6
              "T-037 CLI continuation pairs are builder identical",
              ContinuationTests.cliContinuationPairsAreBuilderIdentical
              "T-037 CLI continuation rejections stay controlled and exit codes stable",
              ContinuationTests.cliContinuationRejectionsStayControlledAndExitCodesStable
              "T-037 save contract documents bind continuation truth",
              ContinuationTests.saveContractDocumentsBindContinuationTruth ]

        if arguments.Length = 2 && arguments[0] = "--t032-allocation-probe" then
            // Frisch-Prozess-Eingang der Allokationsprobe: läuft die
            // exakte-0-Messbasis in einem eigenen Prozess, kann keine fremde
            // Suiteprozess-Allokation in das vertragliche Messfenster fallen.
            CommandLoopTests.runT032AllocationProbe arguments[1]
            0
        else if arguments.Length = 2 && arguments[0] = "--generator-probe" then
            let root = arguments[1]
            let jobId = "01KZY44M2P2RNSA5XNGM4P9EMY"
            let stage = $".ai/runtime/asset-jobs/{jobId}/stage/quarantine"

            try
                let spec =
                    File.ReadAllBytes(Path.Combine(root, "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json"))
                    |> BlenderCalibration.parseSpecBytes

                Directory.CreateDirectory(Path.Combine(root, $".ai/runtime/asset-jobs/{jobId}/stage"))
                |> ignore

                DotnetAssetGenerator.generate root spec stage |> ignore
                0
            with error ->
                Console.Error.WriteLine(error.Message)
                1
        else
            let mutable failures = 0

            for name, test in tests do
                try
                    test ()
                    Console.Out.WriteLine($"PASS {name}")
                with error ->
                    failures <- failures + 1
                    Console.Error.WriteLine($"FAIL {name}: {error.Message}")

            Console.Out.WriteLine($"{tests.Length - failures}/{tests.Length} tests passed")
            if failures = 0 then 0 else 1
