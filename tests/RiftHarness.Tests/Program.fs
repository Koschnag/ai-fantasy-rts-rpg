namespace RiftHarness.Tests

open System
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
                JsonSerializer.Serialize(
                    {| schemaVersion = 1
                       id = id
                       status = status
                       statement = statement
                       sources =
                        [| {| path = "docs/truth.md"
                              sha256 = hash |} |] |}
                )

            let recordsPath = Path.Combine(memory, "records.jsonl")

            File.WriteAllLines(
                recordsPath,
                [| memoryRecord "MEM-TEST-1" "accepted" "firstcurrentmarker" sourceHash
                   memoryRecord "MEM-TEST-2" "proposed" "proposedneedle" sourceHash
                   memoryRecord "MEM-TEST-3" "accepted" "othercurrentmarker" sourceHash
                   memoryRecord "MEM-TEST-4" "rejected" "rejectedneedle" sourceHash
                   memoryRecord "MEM-TEST-5" "accepted" "staleneedle" (String('0', 64)) |],
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
    let main _ =
        let tests =
            [ "Run IDs are time-sortable", Tests.runIdsAreTimeSortable
              "Run lifecycle is hashed and redacted", Tests.runLifecycleIsHashedAndRedacted
              "Event envelope is strict and payload remains structured",
              Tests.eventEnvelopeIsStrictAndPayloadRemainsStructured
              "Logging configuration limits and redacts", Tests.loggingConfigurationLimitsAndRedacts
              "RAG is deterministic and cites sources", Tests.ragIsDeterministicAndCitesSources
              "Hardware question ranks performance budget first", Tests.hardwareQuestionRanksPerformanceBudgetFirst
              "RAG honors context character budget", Tests.ragHonorsContextCharacterBudget
              "RAG indexes only current accepted memory", Tests.ragIndexesOnlyCurrentAcceptedMemory
              "RAG excludes configured paths, secrets and symlinks", Tests.ragExcludesConfiguredPathsSecretsAndSymlinks
              "Init is idempotent and validates configuration", Tests.initIsIdempotentAndValidatesConfiguration
              "Fixed v1 configuration rejects pretend options", Tests.fixedV1ConfigurationRejectsPretendOptions ]

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
