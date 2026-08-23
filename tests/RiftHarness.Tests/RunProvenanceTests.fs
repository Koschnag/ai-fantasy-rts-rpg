namespace RiftHarness.Tests

open System
open System.IO
open System.Text.Json
open RiftHarness

module private T004 =
    let assertTrue condition message =
        if not condition then
            failwith message

    let assertEqual expected actual message =
        if not (Unchecked.equals expected actual) then
            failwith $"{message} Erwartet: {expected}; erhalten: {actual}"

    let joinErrors values = String.concat "; " values

    let expectHarnessFailure (needle: string) (action: unit -> unit) message =
        let mutable failure = None

        try
            action ()
        with HarnessException value ->
            failure <- Some value

        match failure with
        | Some value when value.Contains(needle, StringComparison.Ordinal) -> ()
        | Some value -> failwith $"{message} Fehler war: {value}"
        | None -> failwith $"{message} HarnessException blieb aus."

    let workspace (action: string -> unit) =
        let root =
            Path.Combine(Path.GetTempPath(), "RiftHarness.T004-" + Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory(root) |> ignore

        try
            action root
        finally
            if Directory.Exists(root) then
                Directory.Delete(root, true)

    let writeText (path: string) (text: string) =
        Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore

        File.WriteAllText(path, text, Constants.Utf8NoBom)

    let makeInputs actor task model prompt toolchain : Provenance.StartInputs =
        { ActorId = actor
          TaskId = task
          ModelId = model
          PromptFile = prompt
          ToolchainFile = toolchain }

    let taskFixtureJson =
        """{
  "$schema": "../schemas/task.schema.json",
  "schemaVersion": 1,
  "id": "T-901",
  "title": "Provenienz-Fixture fuer Harness-Tests",
  "status": "ready",
  "objective": "Fixture-Aufgabe fuer die Span- und Kriteriumsvertraege der Tests.",
  "inScope": ["Trace-/Span-Huellen"],
  "outOfScope": [],
  "requirements": ["Z-004"],
  "acceptanceCriteria": [
    { "id": "AC-T901-01", "statement": "Werkzeugkette ist gebunden.", "verification": "Fixture prueft die Referenzkette." },
    { "id": "AC-T901-02", "statement": "Evidenz ist kriteriumsgebunden.", "verification": "Fixture lehnt fremde IDs ab." }
  ],
  "requiredGates": ["G-SPEC"],
  "decisionPolicy": { "mayDecide": [], "mustEscalate": [] }
}"""

    let installTaskFixture root =
        writeText (Path.Combine(root, ".ai", "tasks", "T-901-provenance-fixture.json")) taskFixtureJson

    let spanPayloadJson traceId spanId criterion =
        sprintf "{\"traceId\":\"%s\",\"spanId\":\"%s\",\"criterionId\":\"%s\"}" traceId spanId criterion

module RunProvenanceTests =

    open T004

    // ------------------------------------------------------------------
    // AC-T004-01
    // ------------------------------------------------------------------

    let provenancedRunsRecordCompleteManifests () =
        workspace (fun root ->
            let gitDirectory = Path.Combine(root, ".git")

            Directory.CreateDirectory(Path.Combine(gitDirectory, "refs", "heads")) |> ignore

            File.WriteAllText(Path.Combine(gitDirectory, "HEAD"), "ref: refs/heads/main\n")

            File.WriteAllText(
                Path.Combine(gitDirectory, "refs", "heads", "main"),
                "0123456789abcdef0123456789abcdef01234567\n"
            )

            let promptPath = Path.Combine(root, "prompt.md")

            File.WriteAllText(promptPath, "system prompt mit geheimer Markierung wert123", Constants.Utf8NoBom)

            let locations = Workspace.initialize root

            let inputs =
                makeInputs "t004-agent" (Some "T-004") (Some "local-model-v9") (Some promptPath) None

            let runId = RunStore.startProvenanced root "t004-agent" inputs

            assertTrue (Directory.Exists(Path.Combine(locations.Runs, runId, "work"))) "work-Verzeichnis fehlt."

            assertTrue
                (Directory.Exists(Path.Combine(locations.Runs, runId, "evidence")))
                "evidence-Verzeichnis fehlt."

            let metadata = RunStore.metadataOf root runId

            let provenance =
                match metadata.Provenance with
                | Some value -> value
                | None -> failwith "Provenienz fehlt im Manifest."

            assertEqual (Some "T-004") provenance.TaskId "Aufgaben-ID wurde nicht festgehalten."

            assertEqual (Some "local-model-v9") provenance.ModelId "Modellkennung wurde nicht festgehalten."

            assertEqual
                (Some "0123456789abcdef0123456789abcdef01234567")
                provenance.GitCommit
                "Git-Stand wurde nicht ohne Unterprozess gelesen."

            assertTrue provenance.Complete.Git "Vollstaendigkeit git falsch."
            assertTrue provenance.Complete.Prompt "Vollstaendigkeit prompt falsch."
            assertTrue provenance.Complete.Task "Vollstaendigkeit task falsch."
            assertTrue provenance.Complete.Model "Vollstaendigkeit model falsch."
            assertTrue provenance.Complete.Config "Vollstaendigkeit config falsch."
            assertTrue (not provenance.Complete.Toolchain) "Toolchain-Vollstaendigkeit falsch."

            assertTrue
                (Internal.isSha256 (provenance.PromptSha256 |> Option.defaultValue ""))
                "Prompt wird nicht als Hash gefuehrt."

            assertEqual
                (Internal.sha256File locations.Config)
                provenance.ConfigSha256
                "Konfigurationshash stimmt nicht."

            let runDirectoryPath = Path.Combine(locations.Runs, runId)
            let eventLines = File.ReadAllLines(Path.Combine(runDirectoryPath, "events.jsonl"))

            assertEqual 1 eventLines.Length "run.started-Ereignis fehlt."

            use startedDocument = JsonDocument.Parse(eventLines[0])

            assertEqual
                "run.started"
                (startedDocument.RootElement.GetProperty("type").GetString())
                "Erstes Ereignis ist kein run.started."

            // Roher Prompt-Inhalt darf nirgends im Lauf persistiert sein.
            for relative in [ "run.json"; "events.jsonl" ] do
                let persisted = File.ReadAllText(Path.Combine(runDirectoryPath, relative))

                assertTrue
                    (not (persisted.Contains("wert123", StringComparison.Ordinal)))
                    $"Roher Prompt-Inhalt wurde in {relative} gespeichert."

            RunStore.finish root runId "succeeded" None |> ignore

            let report = Verification.verify root (Some runId)

            assertTrue report.Valid $"Gueltiger Provenienzlauf abgelehnt: {joinErrors report.Errors}"

            // Nachtraegliche Manipulation der Provenienz wird erkannt.
            let runJsonPath = Path.Combine(runDirectoryPath, "run.json")
            let originalJson = File.ReadAllText(runJsonPath)
            let replacementConfigHash = String('a', 64)

            let tamperedJson =
                originalJson.Replace(
                    "\"configSha256\": \"" + provenance.ConfigSha256 + "\"",
                    "\"configSha256\": \"" + replacementConfigHash + "\""
                )

            assertTrue (tamperedJson <> originalJson) "Manipulationsfixture hat das Manifest nicht veraendert."

            File.WriteAllText(runJsonPath, tamperedJson, Constants.Utf8NoBom)

            let tamperedReport = Verification.verify root (Some runId)

            assertTrue (not tamperedReport.Valid) "Manipulierte Provenienz blieb unentdeckt.")

    let provenanceCompletenessStaysExplicitWithoutGit () =
        workspace (fun bareRoot ->
            let locations = Workspace.initialize bareRoot

            let bareInputs = makeInputs "bare-agent" None None None None

            let bareRunId = RunStore.startProvenanced bareRoot "bare-agent" bareInputs

            let metadata = RunStore.metadataOf bareRoot bareRunId

            let provenance =
                match metadata.Provenance with
                | Some value -> value
                | None -> failwith "Provenienz fehlt im Manifest."

            assertTrue provenance.GitCommit.IsNone "Ohne Git darf kein Commit gemeldet werden."

            assertTrue (not provenance.Complete.Git) "Fehlender Git-Stand muss explizit markiert sein."

            assertTrue provenance.Complete.Config "Konfigurationsvollstaendigkeit fehlt."

            RunStore.finish bareRoot bareRunId "succeeded" None |> ignore

            let bareReport = Verification.verify bareRoot (Some bareRunId)

            assertTrue bareReport.Valid $"Lauf ohne Git-Stand abgelehnt: {joinErrors bareReport.Errors}")

    // ------------------------------------------------------------------
    // AC-T004-02
    // ------------------------------------------------------------------

    let spanEnvelopeContractRejectsBrokenEnvelopes () =
        workspace (fun root ->
            Workspace.initialize root |> ignore
            installTaskFixture root

            let traceA = String('a', 32)
            let span1 = String('1', 16)

            let inputs = makeInputs "agent" (Some "T-901") None None None

            let runId = RunStore.startProvenanced root "agent" inputs

            let payloadPath = Path.Combine(root, "payload.json")

            File.WriteAllText(payloadPath, "{}", Constants.Utf8NoBom)

            expectHarnessFailure
                "benoetigt die Huelle"
                (fun () -> RunStore.append root runId "tool.executed" payloadPath |> ignore)
                "Huellenpflicht fehlt."

            writeText payloadPath "{\"criterionId\":\"AC-T901-01\"}"

            expectHarnessFailure
                "nur bei Retrieval-, Tool- und Evidenzereignissen"
                (fun () -> RunStore.append root runId "agent.progress" payloadPath |> ignore)
                "Kriteriumsfeld am falschen Typ wurde akzeptiert."

            writeText payloadPath (spanPayloadJson traceA span1 "AC-T999-99")

            expectHarnessFailure
                "gehoert nicht zur Aufgabe"
                (fun () -> RunStore.append root runId "tool.executed" payloadPath |> ignore)
                "Fremdes Kriterium wurde akzeptiert."

            writeText payloadPath (spanPayloadJson traceA "kurz" "AC-T901-01")

            expectHarnessFailure
                "spanId ist ungueltig"
                (fun () -> RunStore.append root runId "tool.executed" payloadPath |> ignore)
                "Ungueltige Span-ID wurde akzeptiert.")

    let spanEnvelopeChainEndToEndVerifies () =
        workspace (fun root ->
            Workspace.initialize root |> ignore
            installTaskFixture root

            Directory.CreateDirectory(Path.Combine(root, "knowledge")) |> ignore

            File.WriteAllText(
                Path.Combine(root, "knowledge", "world.txt"),
                "Moor wind and old stones azurquartz\n",
                Constants.Utf8NoBom
            )

            writeText
                (Path.Combine(root, ".ai", "config.json"))
                """{"schemaVersion":1,"rag":{"sources":["knowledge/*.txt"],"chunkLines":2,"overlapLines":1}}"""

            let traceA = String('a', 32)
            let span1 = String('1', 16)
            let span2 = String('2', 16)

            let inputs = makeInputs "agent" (Some "T-901") None None None

            let runId = RunStore.startProvenanced root "agent" inputs

            let payloadPath = Path.Combine(root, "payload.json")

            writeText payloadPath (spanPayloadJson traceA span1 "AC-T901-01")
            RunStore.append root runId "tool.executed" payloadPath |> ignore

            let artifactRelative = "knowledge/world.txt"

            let artifactHash = Internal.sha256File (Path.Combine(root, artifactRelative))

            let resultHash = Internal.sha256Text "result-anchor"

            let evidenceJson =
                sprintf
                    "{\"traceId\":\"%s\",\"spanId\":\"%s\",\"criterionId\":\"AC-T901-02\",\"kind\":\"unit-test\",\"command\":\"./scripts/rift.sh test\",\"exitCode\":0,\"durationMs\":7,\"artifacts\":[{\"path\":\"%s\",\"sha256\":\"%s\"}],\"result\":{\"ok\":true},\"resultSha256\":\"%s\"}"
                    traceA
                    span2
                    artifactRelative
                    artifactHash
                    resultHash

            writeText payloadPath evidenceJson
            RunStore.append root runId "evidence.recorded" payloadPath |> ignore

            RagIndex.build root |> ignore

            let response = RagIndex.query root "azurquartz" 3

            let traceReference = RetrievalStore.record root runId response

            let retrievalJson =
                sprintf
                    "{\"traceId\":\"%s\",\"spanId\":\"%s\",\"criterionId\":\"AC-T901-02\",\"indexSha256\":\"%s\",\"sequence\":%d,\"traceHash\":\"%s\",\"queryId\":\"%s\"}"
                    traceA
                    span1
                    response.IndexSha256
                    traceReference.Sequence
                    traceReference.TraceHash
                    traceReference.QueryId

            writeText payloadPath retrievalJson
            RunStore.append root runId "retrieval.recorded" payloadPath |> ignore

            RunStore.finish root runId "succeeded" None |> ignore

            let report = Verification.verify root (Some runId)

            assertTrue report.Valid $"Gueltige Trace-/Evidenzkette abgelehnt: {joinErrors report.Errors}")

    let duplicateEvidenceSpansFailVerification () =
        workspace (fun root ->
            Workspace.initialize root |> ignore
            installTaskFixture root

            let traceA = String('a', 32)
            let sharedSpan = String('9', 16)
            let resultHash = Internal.sha256Text "dup"

            let inputs = makeInputs "agent" (Some "T-901") None None None

            let duplicateRun = RunStore.startProvenanced root "agent" inputs

            let payloadPath = Path.Combine(root, "payload.json")

            writeText payloadPath (spanPayloadJson traceA sharedSpan "AC-T901-01")

            // Mehrere Werkzeugereignisse duerfen denselben Span teilen.
            RunStore.append root duplicateRun "tool.executed" payloadPath |> ignore
            RunStore.append root duplicateRun "tool.executed" payloadPath |> ignore

            let evidenceJson =
                sprintf
                    "{\"traceId\":\"%s\",\"spanId\":\"%s\",\"criterionId\":\"AC-T901-02\",\"kind\":\"unit-test\",\"artifacts\":[],\"result\":{\"ok\":true},\"resultSha256\":\"%s\"}"
                    traceA
                    sharedSpan
                    resultHash

            writeText payloadPath evidenceJson
            RunStore.append root duplicateRun "evidence.recorded" payloadPath |> ignore

            let secondEvidence =
                evidenceJson.Replace("\"result\":{\"ok\":true}", "\"result\":{\"ok\":false}")

            writeText payloadPath secondEvidence
            RunStore.append root duplicateRun "evidence.recorded" payloadPath |> ignore

            let duplicateReport = Verification.verify root (Some duplicateRun)

            assertTrue
                (not duplicateReport.Valid
                 && duplicateReport.Errors
                    |> List.exists (fun error ->
                        error.Contains("bereits mit einer Evidenz abgeschlossen", StringComparison.Ordinal)))
                "Zweite Evidenz auf demselben Span blieb unentdeckt.")

    let retrievalEventsRequireKnownTraceHashes () =
        workspace (fun root ->
            Workspace.initialize root |> ignore
            installTaskFixture root

            let unknownTrace = String('d', 32)
            let unknownSpan = String('7', 16)
            let unknownHash = String('e', 64)

            let inputs = makeInputs "agent" (Some "T-901") None None None

            let retrievalRun = RunStore.startProvenanced root "agent" inputs

            let payloadPath = Path.Combine(root, "payload.json")

            writeText
                payloadPath
                (sprintf
                    "{\"traceId\":\"%s\",\"spanId\":\"%s\",\"criterionId\":\"AC-T901-01\",\"traceHash\":\"%s\",\"sequence\":1,\"queryId\":\"%s\",\"indexSha256\":\"%s\"}"
                    unknownTrace
                    unknownSpan
                    unknownHash
                    (String('f', 64))
                    (String('0', 64)))

            RunStore.append root retrievalRun "retrieval.recorded" payloadPath |> ignore

            let retrievalReport = Verification.verify root (Some retrievalRun)

            assertTrue
                (not retrievalReport.Valid
                 && retrievalReport.Errors
                    |> List.exists (fun error -> error.Contains("unbekannten Trace-Hash", StringComparison.Ordinal)))
                "Retrieval-Ereignis mit unbekanntem Trace-Hash wurde akzeptiert.")

    // ------------------------------------------------------------------
    // AC-T004-03
    // ------------------------------------------------------------------

    let ragBuildManifestIsDeterministicAndBindsInputs () =
        workspace (fun root ->
            Workspace.initialize root |> ignore

            Directory.CreateDirectory(Path.Combine(root, "knowledge")) |> ignore

            File.WriteAllText(
                Path.Combine(root, "knowledge", "world.txt"),
                "Moor wind and old stones azurquartz\nWardens keep the silent road\n",
                Constants.Utf8NoBom
            )

            let configPath = Path.Combine(root, ".ai", "config.json")

            let baseConfig =
                """{"schemaVersion":1,"rag":{"sources":["knowledge/*.txt"],"chunkLines":2,"overlapLines":1}}"""

            writeText configPath baseConfig

            let firstBuild = RagIndex.build root
            let firstBytes = File.ReadAllBytes(firstBuild.ManifestPath)

            let secondBuild = RagIndex.build root

            let secondBytes = File.ReadAllBytes(secondBuild.ManifestPath)

            assertTrue
                (firstBytes.AsSpan().SequenceEqual(secondBytes))
                "Build-Manifest ist nicht byte-deterministisch."

            assertEqual
                firstBuild.ManifestHash
                secondBuild.ManifestHash
                "Manifesthash variiert zwischen identischen Builds."

            let validErrors = RagIndex.verify root

            assertTrue (List.isEmpty validErrors) $"Gueltiges Build-Manifest abgelehnt: {joinErrors validErrors}"

            // Konfigurationsaenderung invalidiert das Manifest.
            writeText
                configPath
                """{"schemaVersion":1,"rag":{"sources":["knowledge/*.txt"],"chunkLines":2,"overlapLines":1,"ranking":{"algorithm":"bm25","k1":1.25,"b":0.75}}}"""

            let configErrors = RagIndex.verify root

            assertTrue
                (configErrors
                 |> List.exists (fun error ->
                     error.Contains("passt nicht zu aktuellem Index", StringComparison.Ordinal)))
                "Konfigurationsaenderung invalidierte das Manifest nicht."

            // Quellenänderung invalidiert das Manifest ebenfalls.
            writeText configPath baseConfig

            File.AppendAllText(
                Path.Combine(root, "knowledge", "world.txt"),
                "New sentence breaks the hash.\n",
                Constants.Utf8NoBom
            )

            let sourceErrors = RagIndex.verify root

            assertTrue
                (not (List.isEmpty sourceErrors)
                 && sourceErrors
                    |> List.exists (fun error -> error.Contains("Quellhash ist veraltet", StringComparison.Ordinal)))
                "Quellenänderung blieb unentdeckt."

            File.WriteAllText(
                Path.Combine(root, "knowledge", "world.txt"),
                "Moor wind and old stones azurquartz\nWardens keep the silent road\n",
                Constants.Utf8NoBom
            )

            // Fehlendes Manifest schlaegt bewusst fehl.
            File.Delete(firstBuild.ManifestPath)

            let missingErrors = RagIndex.verify root

            assertTrue
                (missingErrors
                 |> List.exists (fun error -> error.Contains("Build-Manifest fehlt", StringComparison.Ordinal)))
                "Fehlendes Build-Manifest wurde akzeptiert.")

    // ------------------------------------------------------------------
    // AC-T004-04
    // ------------------------------------------------------------------

    let retentionPlansAreReadOnlyAndExecutionsStayGuarded () =
        workspace (fun root ->
            Workspace.initialize root |> ignore

            let baseTime = DateTimeOffset.UtcNow.AddDays(-200.0)
            let oldStart = baseTime
            let oldFinish = baseTime.AddMinutes(2.0)
            let recentStart = DateTimeOffset.UtcNow.AddHours(-26.0)
            let recentFinish = DateTimeOffset.UtcNow.AddHours(-25.0)

            let agedInputs = makeInputs "aged-agent" None None None None

            // A: abgeschlossen, Beweis vorhanden, Frist laeuft noch.
            let recentRunId =
                RunStore.startProvenancedAt root "aged-agent" agedInputs recentStart

            RunStore.finishAt root recentRunId "succeeded" None recentFinish |> ignore

            // B: abgeschlossen, Beweis vorhanden, Frist abgelaufen.
            let deletableRunId =
                RunStore.startProvenancedAt root "aged-agent" agedInputs oldStart

            RunStore.finishAt root deletableRunId "succeeded" None oldFinish |> ignore

            // C: abgeschlossen, aber ohne akzeptierten bereinigten Bericht.
            let unprovedRunId =
                RunStore.startProvenancedAt root "aged-agent" agedInputs oldStart

            RunStore.finishAt root unprovedRunId "succeeded" None oldFinish |> ignore

            // D: nie gueltig abgeschlossen.
            let runningRunId = RunStore.startProvenancedAt root "aged-agent" agedInputs oldStart

            T004.writeText
                (Path.Combine(root, ".ai", "history", "accepted", "2026-01-01-fixture.md"))
                $"# Bereinigte Zusammenfassung\n\n- Lauf: {recentRunId}\n- Lauf: {deletableRunId}\n"

            let runsRoot = (Workspace.paths root).Runs

            let runExists runId =
                Directory.Exists(Path.Combine(runsRoot, runId))

            for runId in [ recentRunId; deletableRunId; unprovedRunId; runningRunId ] do
                assertTrue (runExists runId) "Fixture-Lauf fehlt vor der Retention."

            let nowUtc = DateTimeOffset.UtcNow
            let planBytes, planHash = Retention.planBytes root nowUtc

            let planPath = Path.Combine(root, "retention-plan.json")
            File.WriteAllBytes(planPath, planBytes)

            use planDocument = JsonDocument.Parse(File.ReadAllText(planPath))

            let candidateDeletable runId =
                planDocument.RootElement.GetProperty("plan").GetProperty("candidates").EnumerateArray()
                |> Seq.filter (fun item -> item.GetProperty("runId").GetString() = runId)
                |> Seq.map (fun item -> item.GetProperty("deletable").GetBoolean())
                |> Seq.toList
                |> List.tryHead
                |> Option.defaultWith (fun () -> failwith "Kandidat fehlt im Plan.")

            assertTrue (candidateDeletable deletableRunId) "Abgelaufener Lauf mit Beweis ist nicht loeschbar markiert."

            assertTrue (not (candidateDeletable recentRunId)) "Lauf innerhalb der Frist wurde als loeschbar markiert."

            assertTrue
                (not (candidateDeletable unprovedRunId))
                "Lauf ohne History-Beweis wurde als loeschbar markiert."

            assertTrue (not (candidateDeletable runningRunId)) "Laufender Lauf wurde als loeschbar markiert."

            // Dry-Run veraendert nichts.
            for runId in [ recentRunId; deletableRunId; unprovedRunId; runningRunId ] do
                assertTrue (runExists runId) "Read-only Plan hat Laeufe veraendert."

            // Ausfuehrung mit falschem Bestaetigungshash bleibt wirkungslos.
            let wrongHash = String('b', 64)

            expectHarnessFailure
                "Bestaetigungshash"
                (fun () -> Retention.execute root planPath wrongHash nowUtc |> ignore)
                "Falscher Bestaetigungshash wurde akzeptiert."

            assertTrue (runExists deletableRunId) "Abgebrochene Ausfuehrung hat geloescht."

            // Ausfuehrung mit korrektem Hash entfernt ausschliesslich den loeschbaren Lauf.
            let receipt = Retention.execute root planPath planHash nowUtc

            assertEqual [ deletableRunId ] receipt.DeletedRunIds "Ausfuehrung hat die falschen Laeufe entfernt."

            assertTrue (not (runExists deletableRunId)) "Loeschbarer Lauf wurde nicht entfernt."

            for runId in [ recentRunId; unprovedRunId; runningRunId ] do
                assertTrue (runExists runId) "Geschuetzter Lauf wurde entfernt."

            let logText =
                File.ReadAllText(Path.Combine((Workspace.paths root).Runtime, "retention-log.jsonl"))

            assertTrue (logText.Contains(deletableRunId, StringComparison.Ordinal)) "Bereinigungsnachweis fehlt."

            // Erneute Ausfuehrung des alten Plans scheitert an der Frischpruefung.
            expectHarnessFailure
                "nicht mehr loeschbar"
                (fun () -> Retention.execute root planPath planHash nowUtc |> ignore)
                "Veralteter Plan konnte erneut ausgefuehrt werden.")

    // ------------------------------------------------------------------
    // AC-T004-05
    // ------------------------------------------------------------------

    let evidencePayloadsRedactSecretsAndRejectForeignFields () =
        workspace (fun root ->
            Workspace.initialize root |> ignore
            installTaskFixture root

            let traceA = String('a', 32)
            let span1 = String('1', 16)
            let resultAnchor = Internal.sha256Text "anchor"

            let inputs = makeInputs "agent" (Some "T-901") None None None

            let runId = RunStore.startProvenanced root "agent" inputs

            let payloadPath = Path.Combine(root, "payload.json")

            writeText payloadPath (spanPayloadJson traceA span1 "AC-T901-01")
            RunStore.append root runId "tool.executed" payloadPath |> ignore

            let secretEvidence =
                sprintf
                    "{\"traceId\":\"%s\",\"spanId\":\"%s\",\"criterionId\":\"AC-T901-02\",\"kind\":\"unit-test\",\"artifacts\":[],\"result\":{\"apiKey\":\"super-secret-wert\"},\"resultSha256\":\"%s\"}"
                    traceA
                    span1
                    resultAnchor

            writeText payloadPath secretEvidence
            RunStore.append root runId "evidence.recorded" payloadPath |> ignore

            let runsRoot = (Workspace.paths root).Runs

            let events = File.ReadAllLines(Path.Combine(runsRoot, runId, "events.jsonl"))
            let evidenceLine = events[events.Length - 1]

            assertTrue
                (evidenceLine.Contains("[REDACTED]", StringComparison.Ordinal))
                "Evidenz-Secret wurde nicht redigiert."

            assertTrue
                (not (evidenceLine.Contains("super-secret-wert", StringComparison.Ordinal)))
                "Evidenz-Secret blieb unredigiert gespeichert."

            // Fremdes Feld (z. B. verborgene Begruendung) wird abgelehnt.
            let foreignFieldEvidence =
                sprintf
                    "{\"traceId\":\"%s\",\"spanId\":\"%s\",\"criterionId\":\"AC-T901-02\",\"kind\":\"unit-test\",\"hiddenReasoning\":\"internal steps\",\"artifacts\":[],\"result\":{\"ok\":true},\"resultSha256\":\"%s\"}"
                    traceA
                    span1
                    resultAnchor

            writeText payloadPath foreignFieldEvidence

            expectHarnessFailure
                "unerlaubte Feld"
                (fun () -> RunStore.append root runId "evidence.recorded" payloadPath |> ignore)
                "Fremdes Evidenzfeld wurde akzeptiert."

            // Pfadtraversierung in Artefakten wird abgelehnt.
            let traversalEvidence =
                sprintf
                    "{\"traceId\":\"%s\",\"spanId\":\"%s\",\"criterionId\":\"AC-T901-02\",\"kind\":\"unit-test\",\"artifacts\":[{\"path\":\"../outside.bin\",\"sha256\":\"%s\"}],\"result\":{\"ok\":true},\"resultSha256\":\"%s\"}"
                    traceA
                    span1
                    resultAnchor
                    resultAnchor

            writeText payloadPath traversalEvidence

            expectHarnessFailure
                "aufsteigende Segmente"
                (fun () -> RunStore.append root runId "evidence.recorded" payloadPath |> ignore)
                "Artefaktpfad-Traversierung wurde akzeptiert.")
