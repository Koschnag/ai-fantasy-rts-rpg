namespace RiftHarness.Tests

open System
open System.Collections.Generic
open System.Diagnostics
open System.IO
open System.Text.Json
open System.Text.Json.Nodes
open RiftHarness

[<RequireQualifiedAccess>]
module AssetCanonicalTests =
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

    let private assertTrue condition message =
        if not condition then
            failwith message

    let private runInWorkspace action =
        let root =
            Path.Combine(Path.GetTempPath(), "RiftHarness.AssetCanonical-" + Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory(root) |> ignore

        try
            action root
        finally
            if Directory.Exists(root) then
                Directory.Delete(root, true)

    let private copyFile root relative =
        let target = Path.Combine(root, relative)
        Directory.CreateDirectory(Path.GetDirectoryName(target)) |> ignore
        File.Copy(Path.Combine(repositoryRoot, relative), target, true)

    let private runProcessOutput root executable arguments =
        let info = ProcessStartInfo(executable)
        info.WorkingDirectory <- root
        info.UseShellExecute <- false
        info.RedirectStandardOutput <- true
        info.RedirectStandardError <- true

        for argument in arguments do
            info.ArgumentList.Add(argument)

        use child = Process.Start(info)
        child.WaitForExit()

        if child.ExitCode <> 0 then
            failwith $"Fixture-Prozess fehlgeschlagen: {executable}."

        child.StandardOutput.ReadToEnd().Trim()

    let private runProcess root executable arguments =
        runProcessOutput root executable arguments |> ignore

    let private initialize root =
        Workspace.initialize root |> ignore

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
            copyFile root relative

        runProcess root "git" [ "init"; "--quiet" ]

    let private writeText (root: string) (relative: string) (text: string) =
        let path = Path.Combine(root, relative)
        Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore
        File.WriteAllText(path, text, Constants.Utf8NoBom)
        path

    let private writeBytes (root: string) (relative: string) (bytes: byte array) =
        let path = Path.Combine(root, relative)
        Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore
        File.WriteAllBytes(path, bytes)
        path

    let private saveNode (path: string) (node: JsonNode) =
        File.WriteAllText(path, node.ToJsonString(JsonSerializerOptions(WriteIndented = true)), Constants.Utf8NoBom)

    let private stringValue (value: string) = JsonValue.Create(value) :> JsonNode
    let private boolValue (value: bool) = JsonValue.Create(value) :> JsonNode
    let private intValue (value: int) = JsonValue.Create(value) :> JsonNode

    let private nullableString (value: string option) =
        match value with
        | Some text -> stringValue text
        | None -> null

    let private baseInput (id: string) (path: string option) (hash: string) (allowedUse: string) =
        JsonObject(
            [ KeyValuePair("id", stringValue id)
              KeyValuePair("path", nullableString path)
              KeyValuePair("sha256", stringValue hash)
              KeyValuePair("origin", stringValue "Internal synthetic fixture")
              KeyValuePair("originClass", stringValue "internal-specification")
              KeyValuePair("creativeInfluence", boolValue true)
              KeyValuePair("license", stringValue "Private fixture")
              KeyValuePair("rightsEvidence", stringValue "Generated only for an isolated harness regression.")
              KeyValuePair("allowedUse", stringValue allowedUse)
              KeyValuePair("referenceUseReviewed", boolValue true) ]
        )

    let private outputDescriptor (path: string) (hash: string) (mediaType: string) (bytes: int64) =
        JsonObject(
            [ KeyValuePair("path", stringValue path)
              KeyValuePair("sha256", stringValue hash)
              KeyValuePair("mediaType", stringValue mediaType)
              KeyValuePair("bytes", JsonValue.Create(bytes) :> JsonNode)
              KeyValuePair("technicalMetrics", JsonObject()) ]
        )

    let private manifestNode assetId runId actorId specHash receiptPath generator inputs prompts outputs =
        JsonObject(
            [ KeyValuePair("$schema", stringValue "../../.ai/schemas/asset-manifest.schema.json")
              KeyValuePair("schemaVersion", intValue 1)
              KeyValuePair("assetId", stringValue assetId)
              KeyValuePair("purpose", stringValue "Isolated synthetic provenance regression")
              KeyValuePair("specSha256", stringValue specHash)
              KeyValuePair("generationRunId", stringValue runId)
              KeyValuePair("generationBindingMode", stringValue "canonical-event-v1")
              KeyValuePair("generationReceipt", stringValue receiptPath)
              KeyValuePair("generationReceiptSha256", stringValue (String('0', 64)))
              KeyValuePair("createdBy", stringValue actorId)
              KeyValuePair("status", stringValue "quarantine")
              KeyValuePair("generator", generator)
              KeyValuePair("inputs", inputs)
              KeyValuePair("prompts", prompts)
              KeyValuePair("outputs", outputs)
              KeyValuePair("transformations", JsonArray())
              KeyValuePair(
                  "licenseBasis",
                  JsonObject(
                      [ KeyValuePair("modelTerms", stringValue "Fixture-only")
                        KeyValuePair("inputRights", stringValue "Internal fixture inputs only")
                        KeyValuePair("outputPolicy", stringValue "Quarantine only")
                        KeyValuePair("commercialUseReviewed", boolValue false)
                        KeyValuePair("reviewedAtUtc", null)
                        KeyValuePair("termsEvidenceArtifact", null) ]
                  )
              )
              KeyValuePair("reviews", JsonArray())
              KeyValuePair("supersedes", JsonArray())
              KeyValuePair("createdAtUtc", stringValue "2026-08-13T00:00:00Z") ]
        )

    let private appendGenerationEvent (root: string) (runId: string) (payload: JsonNode) =
        let payloadPath = writeText root "event-payload.json" (payload.ToJsonString())
        RunStore.append root runId "asset.generation.completed" payloadPath |> ignore

    let private finish root runId assetId =
        let summary =
            JsonObject(
                [ KeyValuePair("assetId", stringValue assetId)
                  KeyValuePair("outcome", stringValue "Fixture generation completed") ]
            )

        let summaryPath = writeText root "summary.json" (summary.ToJsonString())
        RunStore.finish root runId "succeeded" (Some summaryPath) |> ignore

    type private Fixture =
        { Root: string
          AssetId: string
          RunId: string
          ManifestPath: string
          ReceiptPath: string }

    type private FixtureContent =
        { OutputExtension: string
          OutputMediaType: string
          OutputBytes: byte array
          SourceExtension: string
          SourceBytes: byte array }

    let private defaultFixtureContent =
        { OutputExtension = ".gltf"
          OutputMediaType = "model/gltf+json"
          OutputBytes = Constants.Utf8NoBom.GetBytes("{\"asset\":{\"version\":\"2.0\"}}\n")
          SourceExtension = ".fsx"
          SourceBytes = Constants.Utf8NoBom.GetBytes("printfn \"synthetic fixture\"\n") }

    let private createCanonicalFixtureAtWithContent root kind extraGenerationEvent outputRoot content =
        initialize root

        let assetId =
            if kind = "procedural" then
                "PROC-FIXTURE-001"
            else
                "AI-FIXTURE-001"

        let actorId = "fixture-generator"
        let runId = RunStore.startForActor root actorId
        let specRelative = $"assets/specs/{assetId}.md"

        let specPath =
            writeText root specRelative "Synthetic numeric fixture specification.\n"

        let specHash = Internal.sha256File specPath
        let outputRelative = $"{outputRoot}/{assetId}{content.OutputExtension}"
        let outputPath = writeBytes root outputRelative content.OutputBytes
        let outputHash = Internal.sha256File outputPath
        let outputBytes = FileInfo(outputPath).Length
        let receiptRelative = $"assets/receipts/{assetId}/{runId}.json"
        let manifestRelative = $"assets/manifests/{assetId}.json"

        let specInput =
            baseInput $"SPEC-{assetId}" (Some specRelative) specHash "internal-specification"

        let inputs = JsonArray(specInput)

        let generator =
            if kind = "procedural" then
                let sourceRelative = $"tools/fixtures/{assetId}{content.SourceExtension}"
                let sourcePath = writeBytes root sourceRelative content.SourceBytes
                let sourceHash = Internal.sha256File sourcePath

                inputs.Add(baseInput $"SOURCE-{assetId}" (Some sourceRelative) sourceHash "generation-input")

                JsonObject(
                    [ KeyValuePair("kind", stringValue "procedural")
                      KeyValuePair("tool", stringValue "fixture-generator")
                      KeyValuePair("version", stringValue "1.0.0")
                      KeyValuePair("model", null)
                      KeyValuePair("modelVersion", null)
                      KeyValuePair("modelArtifactSha256", null)
                      KeyValuePair("executionMode", stringValue "local")
                      KeyValuePair("seed", intValue 41)
                      KeyValuePair("generatorSourceSha256", stringValue sourceHash)
                      KeyValuePair("toolchainPin", stringValue "dotnet-fixture-v1") ]
                )
            else
                JsonObject(
                    [ KeyValuePair("kind", stringValue "ai")
                      KeyValuePair("tool", stringValue "synthetic-fixture-service")
                      KeyValuePair("version", stringValue "1.0.0")
                      KeyValuePair("model", stringValue "fixture-model")
                      KeyValuePair("modelVersion", stringValue "fixture-v1")
                      KeyValuePair("modelArtifactSha256", null)
                      KeyValuePair("executionMode", stringValue "remote")
                      KeyValuePair("seed", null)
                      KeyValuePair("generatorSourceSha256", null)
                      KeyValuePair("toolchainPin", null) ]
                )

        let prompts =
            if kind = "procedural" then
                null
            else
                let prompt = "A synthetic geometric calibration object."
                let negativePrompt = "No text or logos."

                let envelopeHash =
                    Internal.jsonBytes false (fun writer ->
                        writer.WriteStartObject()
                        writer.WriteNumber("schemaVersion", 1)
                        writer.WriteString("prompt", prompt)
                        writer.WriteString("negativePrompt", negativePrompt)
                        writer.WriteEndObject())
                    |> Internal.sha256Hex

                JsonObject(
                    [ KeyValuePair("prompt", stringValue prompt)
                      KeyValuePair("negativePrompt", stringValue negativePrompt)
                      KeyValuePair("promptSha256", stringValue (Internal.sha256Text prompt))
                      KeyValuePair("promptEnvelopeSha256", stringValue envelopeHash)
                      KeyValuePair("bindingMode", stringValue "canonical-envelope-v1") ]
                )
                :> JsonNode

        let outputs =
            JsonArray(outputDescriptor outputRelative outputHash content.OutputMediaType outputBytes)

        let manifest =
            manifestNode assetId runId actorId specHash receiptRelative generator inputs prompts outputs

        let manifestPath = Path.Combine(root, manifestRelative)
        Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)) |> ignore
        saveNode manifestPath manifest

        let payload =
            JsonObject(
                [ KeyValuePair("actorId", stringValue actorId)
                  KeyValuePair("assetId", stringValue assetId)
                  KeyValuePair("generationBindingMode", stringValue "canonical-event-v1")
                  KeyValuePair("specPath", stringValue specRelative)
                  KeyValuePair("specSha256", stringValue specHash)
                  KeyValuePair("generator", manifest["generator"].DeepClone())
                  KeyValuePair("inputs", manifest["inputs"].DeepClone())
                  KeyValuePair("transformations", manifest["transformations"].DeepClone())
                  KeyValuePair("outputs", manifest["outputs"].DeepClone()) ]
            )

        if kind = "ai" then
            let promptNode = manifest["prompts"].AsObject()
            payload["promptSha256"] <- promptNode["promptSha256"].DeepClone()
            payload["promptBindingMode"] <- promptNode["bindingMode"].DeepClone()
            payload["promptEnvelopeSha256"] <- promptNode["promptEnvelopeSha256"].DeepClone()

        appendGenerationEvent root runId payload

        if extraGenerationEvent then
            appendGenerationEvent root runId payload

        finish root runId assetId

        { Root = root
          AssetId = assetId
          RunId = runId
          ManifestPath = manifestPath
          ReceiptPath = Path.Combine(root, receiptRelative) }

    let private createCanonicalFixtureAt root kind extraGenerationEvent outputRoot =
        createCanonicalFixtureAtWithContent root kind extraGenerationEvent outputRoot defaultFixtureContent

    let private createCanonicalFixture root kind extraGenerationEvent =
        createCanonicalFixtureAt root kind extraGenerationEvent "assets/quarantine"

    let private exportAndBind fixture =
        let relativeManifest =
            Path.GetRelativePath(fixture.Root, fixture.ManifestPath).Replace('\\', '/')

        let relativeReceipt =
            Path.GetRelativePath(fixture.Root, fixture.ReceiptPath).Replace('\\', '/')

        let export =
            AssetStore.exportGenerationReceipt fixture.Root fixture.RunId relativeManifest relativeReceipt

        let manifest = JsonNode.Parse(File.ReadAllText(fixture.ManifestPath)).AsObject()
        manifest["generationReceiptSha256"] <- stringValue export.ReceiptSha256
        saveNode fixture.ManifestPath manifest
        export

    let private receiptHash (receipt: JsonObject) =
        let properties =
            receipt
            |> Seq.filter (fun property -> property.Key <> "$schema" && property.Key <> "receiptSha256")
            |> Seq.sortWith (fun left right -> StringComparer.Ordinal.Compare(left.Key, right.Key))

        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()

            for property in properties do
                writer.WritePropertyName(property.Key)

                if isNull property.Value then
                    writer.WriteNullValue()
                else
                    use document = JsonDocument.Parse(property.Value.ToJsonString())
                    writer.WriteRawValue(Internal.canonicalElementText document.RootElement, true)

            writer.WriteEndObject())
        |> Internal.sha256Hex

    let private receiptSummaryHash (receipt: JsonObject) =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", 1)
            writer.WriteString("runId", receipt["runId"].GetValue<string>())
            writer.WriteString("actorId", receipt["actorId"].GetValue<string>())
            writer.WriteString("startedAtUtc", receipt["startedAtUtc"].GetValue<string>())
            writer.WriteString("finishedAtUtc", receipt["finishedAtUtc"].GetValue<string>())
            writer.WriteString("status", receipt["status"].GetValue<string>())
            writer.WriteNumber("eventCount", receipt["eventCount"].GetValue<int64>())
            writer.WriteString("finalEventHash", receipt["finalEventHash"].GetValue<string>())

            if not (isNull receipt["retrievalTraceCount"]) then
                writer.WriteNumber("retrievalTraceCount", receipt["retrievalTraceCount"].GetValue<int64>())

                if isNull receipt["finalRetrievalTraceHash"] then
                    writer.WriteNull("finalRetrievalTraceHash")
                else
                    writer.WriteString(
                        "finalRetrievalTraceHash",
                        receipt["finalRetrievalTraceHash"].GetValue<string>()
                    )

            writer.WritePropertyName("summary")
            use summary = JsonDocument.Parse(receipt["summary"].ToJsonString())
            writer.WriteRawValue(Internal.canonicalElementText summary.RootElement, true)
            writer.WriteEndObject())
        |> Internal.sha256Hex

    let private envelopeHash (prompt: string) (negativePrompt: string option) =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()
            writer.WriteNumber("schemaVersion", 1)
            writer.WriteString("prompt", prompt)

            match negativePrompt with
            | Some value -> writer.WriteString("negativePrompt", value)
            | None -> writer.WriteNull("negativePrompt")

            writer.WriteEndObject())
        |> Internal.sha256Hex

    let private reviewEvidenceCoreHash (evidence: JsonObject) =
        Internal.jsonBytes false (fun writer ->
            writer.WriteStartObject()

            for name in
                [ "schemaVersion"
                  "assetId"
                  "specSha256"
                  "generationReceiptSha256"
                  "licenseTermsSha256"
                  "reviewId"
                  "kind"
                  "revision"
                  "result"
                  "reviewerId"
                  "runId"
                  "reviewedAtUtc"
                  "criteriaVersion"
                  "checkedScopes"
                  "limitations" ] do
                writer.WritePropertyName(name)

                if isNull evidence[name] then
                    writer.WriteNullValue()
                else
                    use document = JsonDocument.Parse(evidence[name].ToJsonString())
                    writer.WriteRawValue(Internal.canonicalElementText document.RootElement, true)

            writer.WriteEndObject())
        |> Internal.sha256Hex

    let proceduralExportRoundTripIsValid () =
        runInWorkspace (fun root ->
            let fixture = createCanonicalFixture root "procedural" false
            exportAndBind fixture |> ignore

            let report =
                AssetStore.check
                    root
                    { ManifestPath = Some(Path.GetRelativePath(root, fixture.ManifestPath).Replace('\\', '/'))
                      RequireLocal = true
                      RequireApproved = false }

            assertTrue report.Valid $"Prozeduraler Export-Roundtrip ungueltig: {AssetStore.reportJson report}")

    let portableReceiptChronologyIsBound () =
        runInWorkspace (fun root ->
            let fixture = createCanonicalFixture root "procedural" false
            exportAndBind fixture |> ignore
            let receipt = JsonNode.Parse(File.ReadAllText(fixture.ReceiptPath)).AsObject()
            let manifest = JsonNode.Parse(File.ReadAllText(fixture.ManifestPath)).AsObject()
            receipt["startedAtUtc"] <- receipt["finishedAtUtc"].DeepClone()
            receipt["summaryHash"] <- stringValue (receiptSummaryHash receipt)
            let rebound = receiptHash receipt
            receipt["receiptSha256"] <- stringValue rebound
            manifest["generationReceiptSha256"] <- stringValue rebound
            saveNode fixture.ReceiptPath receipt
            saveNode fixture.ManifestPath manifest
            Directory.Delete(Path.Combine(root, ".ai", "runtime", "runs", fixture.RunId), true)

            let report =
                AssetStore.check
                    root
                    { ManifestPath = Some(Path.GetRelativePath(root, fixture.ManifestPath).Replace('\\', '/'))
                      RequireLocal = false
                      RequireApproved = false }

            assertTrue
                (not report.Valid
                 && report.Findings
                    |> List.exists (fun finding -> finding.Code = "ASSET_RECEIPT_CHAIN_INVALID"))
                "Portable Receipt akzeptierte Events ausserhalb seines deklarierten Zeitraums.")

    let exactlyOneGenerationEventIsRequired () =
        runInWorkspace (fun root ->
            let fixture = createCanonicalFixture root "procedural" true
            let mutable rejected = false

            try
                exportAndBind fixture |> ignore
            with HarnessException _ ->
                rejected <- true

            assertTrue rejected "Run mit zwei Generierungs-Abschlussereignissen wurde exportiert.")

    let runActorMetadataIsImmutableAndVerified () =
        runInWorkspace (fun root ->
            let fixture = createCanonicalFixture root "procedural" false
            exportAndBind fixture |> ignore
            let runPath = Path.Combine(root, ".ai", "runtime", "runs", fixture.RunId)
            let runJsonPath = Path.Combine(runPath, "run.json")
            let runJson = JsonNode.Parse(File.ReadAllText(runJsonPath)).AsObject()
            runJson["actorId"] <- stringValue "different-fixture-actor"
            saveNode runJsonPath runJson

            let report =
                AssetStore.check
                    root
                    { ManifestPath = Some(Path.GetRelativePath(root, fixture.ManifestPath).Replace('\\', '/'))
                      RequireLocal = true
                      RequireApproved = false }

            assertTrue
                (not report.Valid
                 && report.Findings
                    |> List.exists (fun finding -> finding.Code = "ASSET_RECEIPT_RUN_INVALID"))
                "Nachtraeglich geaenderte Run-Akteuridentitaet blieb verifizierbar.")

    let canonicalPromptTamperCannotBeRehashedOffline () =
        runInWorkspace (fun root ->
            let fixture = createCanonicalFixture root "ai" false

            try
                exportAndBind fixture |> ignore
            with error ->
                failwith $"AI-Receipt-Export: {error}"

            let manifest = JsonNode.Parse(File.ReadAllText(fixture.ManifestPath)).AsObject()
            let receipt = JsonNode.Parse(File.ReadAllText(fixture.ReceiptPath)).AsObject()
            let prompt = "A changed synthetic geometric object."
            let manifestPromptsNode = manifest["prompts"]
            let receiptPromptsNode = receipt["prompts"]
            assertTrue (not (isNull manifestPromptsNode)) "Manifest-Promptblock fehlt nach AI-Export."
            assertTrue (not (isNull receiptPromptsNode)) "Receipt-Promptblock fehlt nach AI-Export."
            let manifestPrompts = manifestPromptsNode.AsObject()
            let receiptPrompts = receiptPromptsNode.AsObject()
            let negativePrompt = manifestPrompts["negativePrompt"].GetValue<string>()
            let promptHash = Internal.sha256Text prompt
            let envelope = envelopeHash prompt (Some negativePrompt)

            for promptNode in [ manifestPrompts; receiptPrompts ] do
                promptNode["prompt"] <- stringValue prompt
                promptNode["promptSha256"] <- stringValue promptHash
                promptNode["promptEnvelopeSha256"] <- stringValue envelope

            let reboundReceiptHash = receiptHash receipt
            receipt["receiptSha256"] <- stringValue reboundReceiptHash
            manifest["generationReceiptSha256"] <- stringValue reboundReceiptHash
            saveNode fixture.ReceiptPath receipt
            saveNode fixture.ManifestPath manifest

            let report =
                AssetStore.check
                    root
                    { ManifestPath = Some(Path.GetRelativePath(root, fixture.ManifestPath).Replace('\\', '/'))
                      RequireLocal = false
                      RequireApproved = false }

            assertTrue
                (not report.Valid
                 && report.Findings
                    |> List.exists (fun finding -> finding.Code = "ASSET_RECEIPT_CHAIN_INVALID"))
                "Neu gehashter Prompt-Tamper blieb trotz unveraenderter Eventkette gueltig.")

    let canonicalNegativePromptTamperCannotBeRehashedOffline () =
        runInWorkspace (fun root ->
            let fixture = createCanonicalFixture root "ai" false

            try
                exportAndBind fixture |> ignore
            with error ->
                failwith $"AI-Receipt-Export: {error}"

            let originalManifest = File.ReadAllText(fixture.ManifestPath)
            let originalReceipt = File.ReadAllText(fixture.ReceiptPath)

            for negativePrompt in [ Some ""; None; Some "alpha|beta"; Some "ruhig λ" ] do
                let manifest = JsonNode.Parse(originalManifest).AsObject()
                let receipt = JsonNode.Parse(originalReceipt).AsObject()
                let manifestPromptsNode = manifest["prompts"]
                let receiptPromptsNode = receipt["prompts"]
                assertTrue (not (isNull manifestPromptsNode)) "Manifest-Promptblock fehlt nach AI-Export."
                assertTrue (not (isNull receiptPromptsNode)) "Receipt-Promptblock fehlt nach AI-Export."
                let manifestPrompts = manifestPromptsNode.AsObject()
                let receiptPrompts = receiptPromptsNode.AsObject()
                let prompt = manifestPrompts["prompt"].GetValue<string>()
                let envelope = envelopeHash prompt negativePrompt

                for promptNode in [ manifestPrompts; receiptPrompts ] do
                    promptNode["negativePrompt"] <- nullableString negativePrompt
                    promptNode["promptEnvelopeSha256"] <- stringValue envelope

                let reboundReceiptHash = receiptHash receipt
                receipt["receiptSha256"] <- stringValue reboundReceiptHash
                manifest["generationReceiptSha256"] <- stringValue reboundReceiptHash
                saveNode fixture.ReceiptPath receipt
                saveNode fixture.ManifestPath manifest

                let report =
                    AssetStore.check
                        root
                        { ManifestPath = Some(Path.GetRelativePath(root, fixture.ManifestPath).Replace('\\', '/'))
                          RequireLocal = false
                          RequireApproved = false }

                assertTrue
                    (not report.Valid
                     && report.Findings
                        |> List.exists (fun finding -> finding.Code = "ASSET_RECEIPT_CHAIN_INVALID"))
                    "Neu gehashter Negativprompt-Tamper blieb trotz unveraenderter Eventkette gueltig.")

    let fakeApprovalWithoutBoundRunsAndEvidenceFailsClosed () =
        runInWorkspace (fun root ->
            let fixture = createCanonicalFixture root "ai" false
            exportAndBind fixture |> ignore
            Directory.Delete(Path.Combine(root, ".ai", "runtime", "runs", fixture.RunId), true)
            let evidencePath = writeText root "assets/reviews/shared.json" "{}\n"
            let evidenceHash = Internal.sha256File evidencePath
            let manifest = JsonNode.Parse(File.ReadAllText(fixture.ManifestPath)).AsObject()
            manifest["status"] <- stringValue "approved"
            manifest["licenseBasis"]["commercialUseReviewed"] <- boolValue true
            manifest["licenseBasis"]["reviewedAtUtc"] <- stringValue "2026-08-13T00:01:00Z"

            let runIds =
                [ "01ARZ3NDEKTSV4RRFFQ69G5FAV"
                  "01ARZ3NDEKTSV4RRFFQ69G5FAW"
                  "01ARZ3NDEKTSV4RRFFQ69G5FAX"
                  "01ARZ3NDEKTSV4RRFFQ69G5FAY"
                  "01ARZ3NDEKTSV4RRFFQ69G5FAZ" ]

            let reviews = JsonArray()

            for index, kind in
                [ "technical"; "visual"; "performance"; "originality"; "license" ]
                |> List.indexed do
                let reviewer =
                    if kind = "originality" || kind = "license" then
                        "FIXTURE-GENERATOR"
                    else
                        $"reviewer-{kind}"

                reviews.Add(
                    JsonObject(
                        [ KeyValuePair("reviewId", stringValue $"REV-{fixture.AssetId}-{index + 1}")
                          KeyValuePair("revision", intValue 1)
                          KeyValuePair("active", boolValue true)
                          KeyValuePair("kind", stringValue kind)
                          KeyValuePair("reviewerId", stringValue reviewer)
                          KeyValuePair("atUtc", stringValue "2026-08-13T00:02:00Z")
                          KeyValuePair("result", stringValue "pass")
                          KeyValuePair("evidence", stringValue "Opaque structured evidence required")
                          KeyValuePair(
                              "evidenceArtifact",
                              JsonObject(
                                  [ KeyValuePair("path", stringValue "assets/reviews/shared.json")
                                    KeyValuePair("sha256", stringValue evidenceHash)
                                    KeyValuePair("runId", stringValue runIds[index])
                                    KeyValuePair("reviewerId", stringValue reviewer) ]
                              )
                          )
                          KeyValuePair("supersedesReviewId", null) ]
                    )
                )

            manifest["reviews"] <- reviews
            saveNode fixture.ManifestPath manifest

            let report =
                AssetStore.check
                    root
                    { ManifestPath = Some(Path.GetRelativePath(root, fixture.ManifestPath).Replace('\\', '/'))
                      RequireLocal = true
                      RequireApproved = true }

            let codes = report.Findings |> List.map (fun finding -> finding.Code) |> Set.ofList

            assertTrue
                (not report.Valid
                 && codes.Contains("ASSET_GENERATION_RUN_MISSING")
                 && codes.Contains("ASSET_REVIEW_EVIDENCE_INVALID")
                 && codes.Contains("ASSET_REVIEW_SELF_APPROVAL"))
                $"Gefälschte Freigabe wurde nicht an Run, Evidenz und Rollen getrennt gebunden: {AssetStore.reportJson report}")

    let private withApprovedProceduralFixtureFromAt createFixture reviewedAtFor action =
        runInWorkspace (fun root ->
            let fixture = createFixture root

            let export = exportAndBind fixture
            let manifest = JsonNode.Parse(File.ReadAllText(fixture.ManifestPath)).AsObject()
            manifest["status"] <- stringValue "approved"
            manifest["licenseBasis"]["commercialUseReviewed"] <- boolValue true
            manifest["licenseBasis"]["reviewedAtUtc"] <- stringValue "2026-08-13T00:01:00Z"
            let termsRelative = "assets/reviews/license-terms-snapshot.txt"

            let termsPath =
                writeText root termsRelative "Synthetic fixture terms snapshot v1.\n"

            let termsHash = Internal.sha256File termsPath

            manifest["licenseBasis"]["termsEvidenceArtifact"] <-
                JsonObject(
                    [ KeyValuePair("path", stringValue termsRelative)
                      KeyValuePair("sha256", stringValue termsHash) ]
                )

            let reviews = JsonArray()

            for kind in [ "technical"; "visual"; "performance"; "originality"; "license" ] do
                let reviewerId = $"fixture-{kind}-reviewer"
                let reviewId = $"REV-{fixture.AssetId}-{kind.ToUpperInvariant()}-001"
                let reviewRunId = RunStore.startForActor root reviewerId
                let reviewedAtUtc = reviewedAtFor kind

                if kind = "license" then
                    manifest["licenseBasis"]["reviewedAtUtc"] <- stringValue reviewedAtUtc

                let evidenceRelative = $"assets/reviews/{reviewId}.json"
                let evidencePath = Path.Combine(root, evidenceRelative)

                let evidence =
                    JsonObject(
                        [ KeyValuePair("$schema", stringValue "../../.ai/schemas/asset-review-evidence.schema.json")
                          KeyValuePair("schemaVersion", intValue 1)
                          KeyValuePair("assetId", stringValue fixture.AssetId)
                          KeyValuePair("specSha256", manifest["specSha256"].DeepClone())
                          KeyValuePair("generationReceiptSha256", stringValue export.ReceiptSha256)
                          KeyValuePair("licenseTermsSha256", if kind = "license" then stringValue termsHash else null)
                          KeyValuePair("reviewId", stringValue reviewId)
                          KeyValuePair("kind", stringValue kind)
                          KeyValuePair("revision", intValue 1)
                          KeyValuePair("result", stringValue "pass")
                          KeyValuePair("reviewerId", stringValue reviewerId)
                          KeyValuePair("runId", stringValue reviewRunId)
                          KeyValuePair("reviewedAtUtc", stringValue reviewedAtUtc)
                          KeyValuePair("criteriaVersion", stringValue "asset-review-fixture-v1")
                          KeyValuePair(
                              "checkedScopes",
                              if kind = "license" then
                                  JsonArray(stringValue kind, stringValue "license-terms-snapshot-v1")
                              else
                                  JsonArray(stringValue kind)
                          )
                          KeyValuePair("limitations", JsonArray()) ]
                    )

                evidence["reportSha256"] <- stringValue (reviewEvidenceCoreHash evidence)
                Directory.CreateDirectory(Path.GetDirectoryName(evidencePath)) |> ignore
                saveNode evidencePath evidence
                let evidenceHash = Internal.sha256File evidencePath

                let reviewPayload =
                    JsonObject(
                        [ KeyValuePair("actorId", stringValue reviewerId)
                          KeyValuePair("assetId", stringValue fixture.AssetId)
                          KeyValuePair("specSha256", manifest["specSha256"].DeepClone())
                          KeyValuePair("generationReceiptSha256", stringValue export.ReceiptSha256)
                          KeyValuePair("licenseTermsSha256", if kind = "license" then stringValue termsHash else null)
                          KeyValuePair("reviewId", stringValue reviewId)
                          KeyValuePair("kind", stringValue kind)
                          KeyValuePair("revision", intValue 1)
                          KeyValuePair("result", stringValue "pass")
                          KeyValuePair("reviewedAtUtc", stringValue reviewedAtUtc)
                          KeyValuePair("evidencePath", stringValue evidenceRelative)
                          KeyValuePair("evidenceSha256", stringValue evidenceHash) ]
                    )

                let payloadPath =
                    writeText root $"review-{kind}.json" (reviewPayload.ToJsonString())

                RunStore.append root reviewRunId "asset.review.completed" payloadPath |> ignore
                finish root reviewRunId fixture.AssetId

                reviews.Add(
                    JsonObject(
                        [ KeyValuePair("reviewId", stringValue reviewId)
                          KeyValuePair("revision", intValue 1)
                          KeyValuePair("active", boolValue true)
                          KeyValuePair("kind", stringValue kind)
                          KeyValuePair("reviewerId", stringValue reviewerId)
                          KeyValuePair("atUtc", stringValue reviewedAtUtc)
                          KeyValuePair("result", stringValue "pass")
                          KeyValuePair("evidence", stringValue "Structured synthetic fixture evidence")
                          KeyValuePair(
                              "evidenceArtifact",
                              JsonObject(
                                  [ KeyValuePair("path", stringValue evidenceRelative)
                                    KeyValuePair("sha256", stringValue evidenceHash)
                                    KeyValuePair("runId", stringValue reviewRunId)
                                    KeyValuePair("reviewerId", stringValue reviewerId) ]
                              )
                          )
                          KeyValuePair("supersedesReviewId", null) ]
                    )
                )

            manifest["reviews"] <- reviews
            saveNode fixture.ManifestPath manifest

            let outputRelative =
                ((manifest["outputs"].AsArray()[0]).AsObject()["path"]).GetValue<string>()

            runProcess root "git" [ "add"; "-f"; "--"; outputRelative ]

            action root fixture manifest)

    let private withApprovedProceduralFixtureFrom createFixture action =
        withApprovedProceduralFixtureFromAt createFixture (fun _ -> Internal.utcText DateTimeOffset.UtcNow) action

    let private withApprovedProceduralFixture action =
        withApprovedProceduralFixtureFrom
            (fun root -> createCanonicalFixtureAt root "procedural" false "assets/source")
            action

    let approvedProceduralAssetRequiresAndAcceptsFiveBoundReviewRuns () =
        withApprovedProceduralFixture (fun root fixture _ ->

            let report =
                AssetStore.check
                    root
                    { ManifestPath = Some(Path.GetRelativePath(root, fixture.ManifestPath).Replace('\\', '/'))
                      RequireLocal = true
                      RequireApproved = true }

            assertTrue
                (report.Valid
                 && report.Scope = "targeted"
                 && not report.ShippingReady
                 && report.ApprovedCount = 1)
                $"Gezielte prozedurale Freigabediagnose wurde abgelehnt: {AssetStore.reportJson report}"

            let globalReport =
                AssetStore.check
                    root
                    { ManifestPath = None
                      RequireLocal = true
                      RequireApproved = true }

            assertTrue
                (globalReport.Valid
                 && globalReport.Scope = "global"
                 && globalReport.ShippingReady)
                $"Globale prozedurale Freigabe wurde abgelehnt: {AssetStore.reportJson globalReport}")

    let binaryContentBehindTextExtensionIsRejected () =
        let content =
            { defaultFixtureContent with
                OutputExtension = ".json"
                OutputMediaType = "application/json"
                OutputBytes = [| 0uy; 255uy; 0uy; 254uy |] }

        withApprovedProceduralFixtureFrom
            (fun root -> createCanonicalFixtureAtWithContent root "procedural" false "assets/source" content)
            (fun root fixture _ ->
                let report =
                    AssetStore.check
                        root
                        { ManifestPath = Some(Path.GetRelativePath(root, fixture.ManifestPath).Replace('\\', '/'))
                          RequireLocal = true
                          RequireApproved = true }

                assertTrue
                    (not report.Valid
                     && not report.ShippingReady
                     && report.Findings
                        |> List.exists (fun finding -> finding.Code = "ASSET_TEXT_SOURCE_INVALID"))
                    "Binaerinhalt hinter einer Textendung wurde als freigegebene Textquelle akzeptiert.")

    let textSourceIsBoundToRawGitIndexBytes () =
        for staleBytes in
            [ Array.append
                  [| 0xEFuy; 0xBBuy; 0xBFuy |]
                  (Constants.Utf8NoBom.GetBytes("{\"asset\":{\"version\":\"2.0\"}}\n"))
              [| 0uy; 255uy; 0uy; 254uy |] ] do
            withApprovedProceduralFixture (fun root fixture manifest ->
                let outputRelative =
                    (((manifest["outputs"].AsArray()[0]).AsObject())["path"]).GetValue<string>()

                let stalePath = writeBytes root ".ai/runtime/stale-index-blob.bin" staleBytes

                let blob =
                    runProcessOutput root "git" [ "hash-object"; "-w"; "--no-filters"; stalePath ]

                runProcess root "git" [ "update-index"; "--add"; "--cacheinfo"; "100644"; blob; outputRelative ]

                let report =
                    AssetStore.check
                        root
                        { ManifestPath = Some(Path.GetRelativePath(root, fixture.ManifestPath).Replace('\\', '/'))
                          RequireLocal = true
                          RequireApproved = true }

                assertTrue
                    (not report.Valid
                     && report.Findings
                        |> List.exists (fun finding -> finding.Code = "ASSET_TEXT_SOURCE_INDEX_MISMATCH"))
                    "Freigegebene Textquelle wurde nicht bytegenau an den Git-Index gebunden.")

    let proceduralPythonSourceIsCleanRoomScanned () =
        let marker = String.Join(" ", [ "synthetic"; "forbidden"; "proper"; "noun" ])

        let content =
            { defaultFixtureContent with
                SourceExtension = ".py"
                SourceBytes = Constants.Utf8NoBom.GetBytes($"print('{marker}')\n") }

        withApprovedProceduralFixtureFrom
            (fun root -> createCanonicalFixtureAtWithContent root "procedural" false "assets/source" content)
            (fun root fixture _ ->
                let report =
                    AssetStore.check
                        root
                        { ManifestPath = Some(Path.GetRelativePath(root, fixture.ManifestPath).Replace('\\', '/'))
                          RequireLocal = true
                          RequireApproved = true }

                let json = AssetStore.reportJson report

                assertTrue
                    (not report.Valid
                     && report.Findings
                        |> List.exists (fun finding -> finding.Code = "CLEAN_ROOM_DENIED_NAME"))
                    "Prozedurale Pythonquelle umging die Clean-Room-Pruefung."

                assertTrue
                    (not (json.Contains(marker, StringComparison.OrdinalIgnoreCase)))
                    "Clean-Room-Finding gab Python-Quellinhalt aus.")

    let targetedApprovedScanIsNeverShippingReady () =
        withApprovedProceduralFixture (fun root fixture _ ->
            let orphanRelative = "assets/source/unowned-orphan.txt"
            writeText root orphanRelative "Synthetic orphan fixture.\n" |> ignore
            runProcess root "git" [ "add"; "-f"; "--"; orphanRelative ]

            let targeted =
                AssetStore.check
                    root
                    { ManifestPath = Some(Path.GetRelativePath(root, fixture.ManifestPath).Replace('\\', '/'))
                      RequireLocal = true
                      RequireApproved = true }

            assertTrue
                (targeted.Valid && targeted.Scope = "targeted" && not targeted.ShippingReady)
                "Gezielter Manifestscan behauptete trotz unvollstaendiger Repositorysicht Shippingbereitschaft."

            let globalReport =
                AssetStore.check
                    root
                    { ManifestPath = None
                      RequireLocal = true
                      RequireApproved = true }

            assertTrue
                (not globalReport.Valid
                 && globalReport.Findings
                    |> List.exists (fun finding -> finding.Code = "ASSET_SOURCE_ORPHAN"))
                "Globaler Scan erkannte die im Teilscan verborgene verwaiste Quelle nicht.")

    let reviewEvidenceDirectoryFailsControlled () =
        withApprovedProceduralFixture (fun root fixture manifest ->
            let firstReview = (manifest["reviews"].AsArray()[0]).AsObject()
            let evidenceReference = (firstReview["evidenceArtifact"]).AsObject()

            let evidencePath =
                Path.Combine(root, (evidenceReference["path"]).GetValue<string>())

            File.Delete(evidencePath)
            Directory.CreateDirectory(evidencePath) |> ignore

            let report =
                AssetStore.check
                    root
                    { ManifestPath = Some(Path.GetRelativePath(root, fixture.ManifestPath).Replace('\\', '/'))
                      RequireLocal = true
                      RequireApproved = true }

            assertTrue
                (not report.Valid
                 && report.Findings
                    |> List.exists (fun finding -> finding.Code = "ASSET_REVIEW_EVIDENCE_INVALID"))
                "Als JSON benanntes Evidenzverzeichnis verursachte kein kontrolliertes Finding.")

    let reviewTimestampIsBoundToEvidenceAndRun () =
        withApprovedProceduralFixture (fun root fixture manifest ->
            let firstReview = (manifest["reviews"].AsArray()[0]).AsObject()
            firstReview["atUtc"] <- stringValue "2026-08-13T00:02:00Z"

            saveNode fixture.ManifestPath manifest

            let report =
                AssetStore.check
                    root
                    { ManifestPath = Some(Path.GetRelativePath(root, fixture.ManifestPath).Replace('\\', '/'))
                      RequireLocal = true
                      RequireApproved = true }

            assertTrue
                (not report.Valid
                 && report.Findings
                    |> List.exists (fun finding -> finding.Code = "ASSET_REVIEW_EVIDENCE_INVALID"))
                "Manipulierter Review-Zeitstempel blieb gueltig.")

    let reviewCannotPredateGenerationCompletion () =
        withApprovedProceduralFixtureFromAt
            (fun root -> createCanonicalFixtureAt root "procedural" false "assets/source")
            (fun _ -> "2000-01-01T00:00:00Z")
            (fun root fixture _ ->
                let report =
                    AssetStore.check
                        root
                        { ManifestPath = Some(Path.GetRelativePath(root, fixture.ManifestPath).Replace('\\', '/'))
                          RequireLocal = true
                          RequireApproved = true }

                assertTrue
                    (not report.Valid
                     && report.Findings
                        |> List.exists (fun finding -> finding.Code = "ASSET_REVIEW_EVIDENCE_INVALID"))
                    "Reviewzeit vor Generierungsabschluss wurde akzeptiert.")

    let licenseBasisTimeMustMatchActiveLicenseReview () =
        withApprovedProceduralFixture (fun root fixture manifest ->
            manifest["licenseBasis"]["reviewedAtUtc"] <- stringValue "2000-01-01T00:00:00Z"
            saveNode fixture.ManifestPath manifest

            let report =
                AssetStore.check
                    root
                    { ManifestPath = Some(Path.GetRelativePath(root, fixture.ManifestPath).Replace('\\', '/'))
                      RequireLocal = true
                      RequireApproved = true }

            assertTrue
                (not report.Valid
                 && report.Findings
                    |> List.exists (fun finding -> finding.Code = "ASSET_LICENSE_REVIEW_TIME_MISMATCH"))
                "Lizenzbasiszeit war nicht an das aktive Lizenzreview gebunden.")

    let approvedReviewStateMatrixIsEnforced () =
        let expectInvalid mutate description =
            withApprovedProceduralFixture (fun root fixture manifest ->
                mutate manifest
                saveNode fixture.ManifestPath manifest

                let report =
                    AssetStore.check
                        root
                        { ManifestPath = Some(Path.GetRelativePath(root, fixture.ManifestPath).Replace('\\', '/'))
                          RequireLocal = true
                          RequireApproved = true }

                assertTrue (not report.Valid) description)

        expectInvalid
            (fun manifest ->
                let retained =
                    manifest["reviews"].AsArray()
                    |> Seq.filter (fun review -> review["kind"].GetValue<string>() <> "technical")
                    |> Seq.map _.DeepClone()
                    |> Seq.toArray

                manifest["reviews"] <- JsonArray(retained))
            "Freigabe ohne technischen Review wurde akzeptiert."

        expectInvalid
            (fun manifest ->
                let duplicate = (manifest["reviews"].AsArray()[0]).DeepClone().AsObject()
                duplicate["reviewId"] <- stringValue "REV-PROC-FIXTURE-001-TECHNICAL-DUPLICATE"
                manifest["reviews"].AsArray().Add(duplicate))
            "Freigabe mit doppeltem aktivem Review wurde akzeptiert."

        for result in [ "fail"; "needs-work" ] do
            expectInvalid
                (fun manifest -> (manifest["reviews"].AsArray()[0])["result"] <- stringValue result)
                $"Freigabe mit aktivem Reviewstatus {result} wurde akzeptiert."

        runInWorkspace (fun root ->
            let fixture = createCanonicalFixture root "procedural" false
            exportAndBind fixture |> ignore
            let manifest = JsonNode.Parse(File.ReadAllText(fixture.ManifestPath)).AsObject()
            let oldId = "REV-PROC-FIXTURE-001-TECH-001"

            let review revision active id supersedes =
                JsonObject(
                    [ KeyValuePair("reviewId", stringValue id)
                      KeyValuePair("revision", intValue revision)
                      KeyValuePair("active", boolValue active)
                      KeyValuePair("kind", stringValue "technical")
                      KeyValuePair("reviewerId", stringValue "fixture-technical-reviewer")
                      KeyValuePair("atUtc", stringValue "2026-08-13T00:02:00Z")
                      KeyValuePair("result", stringValue "pass")
                      KeyValuePair("evidence", stringValue "Synthetic historical review fixture")
                      KeyValuePair("evidenceArtifact", null)
                      KeyValuePair("supersedesReviewId", nullableString supersedes) ]
                )

            manifest["reviews"] <-
                JsonArray(review 1 false oldId None, review 2 true "REV-PROC-FIXTURE-001-TECH-002" (Some oldId))

            saveNode fixture.ManifestPath manifest

            let report =
                AssetStore.check
                    root
                    { ManifestPath = Some(Path.GetRelativePath(root, fixture.ManifestPath).Replace('\\', '/'))
                      RequireLocal = true
                      RequireApproved = false }

            assertTrue report.Valid "Zulaessige inaktive Reviewhistorie wurde abgelehnt.")

    let nestedDuplicateRunPayloadIsRejected () =
        runInWorkspace (fun root ->
            initialize root
            let runId = RunStore.startForActor root "fixture-actor"

            let payload =
                writeText root "duplicate-payload.json" "{\"nested\":{\"value\":1,\"value\":2}}\n"

            let mutable rejected = false

            try
                RunStore.append root runId "asset.fixture" payload |> ignore
            with HarnessException _ ->
                rejected <- true

            assertTrue rejected "Verschachtelter doppelter Run-Payloadschluessel wurde akzeptiert.")

    let persistedNestedDuplicateRunDataIsRejected () =
        runInWorkspace (fun root ->
            initialize root

            let completedRun () =
                let runId = RunStore.startForActor root "fixture-actor"

                let payload =
                    writeText root ($"payload-{runId}.json") "{\"nested\":{\"value\":1}}\n"

                RunStore.append root runId "asset.fixture" payload |> ignore
                finish root runId "PERSISTED-DUPLICATE-FIXTURE"
                runId

            let eventRunId = completedRun ()

            let eventsPath =
                Path.Combine(root, ".ai", "runtime", "runs", eventRunId, "events.jsonl")

            let eventsText = File.ReadAllText(eventsPath, Constants.Utf8NoBom)

            let tamperedEvents =
                eventsText.Replace(
                    "\"summary\":{\"assetId\":",
                    "\"summary\":{\"duplicate\":1,\"duplicate\":2,\"assetId\":",
                    StringComparison.Ordinal
                )

            assertTrue (tamperedEvents <> eventsText) "Persistiertes Eventfixture konnte nicht mutiert werden."
            File.WriteAllText(eventsPath, tamperedEvents, Constants.Utf8NoBom)

            let eventErrors = RunStore.verifyRun root eventRunId

            assertTrue
                (eventErrors
                 |> List.exists (fun error -> error.Contains("mehrfach", StringComparison.Ordinal)))
                "Persistierter verschachtelter Event-Duplikatschluessel wurde nicht explizit verworfen."

            let summaryRunId = completedRun ()

            let summaryPath =
                Path.Combine(root, ".ai", "runtime", "runs", summaryRunId, "summary.json")

            let summaryText = File.ReadAllText(summaryPath, Constants.Utf8NoBom)

            let tamperedSummary =
                summaryText.Replace(
                    "\"summary\": {\n    \"assetId\":",
                    "\"summary\": {\n    \"duplicate\": 1,\n    \"duplicate\": 2,\n    \"assetId\":",
                    StringComparison.Ordinal
                )

            assertTrue (tamperedSummary <> summaryText) "Persistiertes Summaryfixture konnte nicht mutiert werden."
            File.WriteAllText(summaryPath, tamperedSummary, Constants.Utf8NoBom)

            let summaryErrors = RunStore.verifyRun root summaryRunId

            assertTrue
                (summaryErrors
                 |> List.exists (fun error -> error.Contains("mehrfach", StringComparison.Ordinal)))
                "Persistierter verschachtelter Summary-Duplikatschluessel wurde nicht explizit verworfen.")

    let reviewEvidenceContentIsCleanRoomScanned () =
        withApprovedProceduralFixture (fun root fixture manifest ->
            let marker = String.Join(" ", [ "synthetic"; "forbidden"; "proper"; "noun" ])
            let firstReview = (manifest["reviews"].AsArray()[0]).AsObject()
            let evidenceReference = firstReview["evidenceArtifact"].AsObject()
            let evidencePath = Path.Combine(root, evidenceReference["path"].GetValue<string>())
            let evidence = JsonNode.Parse(File.ReadAllText(evidencePath)).AsObject()
            evidence["limitations"].AsArray().Add(stringValue marker)
            evidence["reportSha256"] <- stringValue (reviewEvidenceCoreHash evidence)
            saveNode evidencePath evidence
            evidenceReference["sha256"] <- stringValue (Internal.sha256File evidencePath)
            saveNode fixture.ManifestPath manifest

            let report =
                AssetStore.check
                    root
                    { ManifestPath = Some(Path.GetRelativePath(root, fixture.ManifestPath).Replace('\\', '/'))
                      RequireLocal = true
                      RequireApproved = true }

            let json = AssetStore.reportJson report

            assertTrue
                (not report.Valid
                 && report.Findings
                    |> List.exists (fun finding -> finding.Code = "CLEAN_ROOM_DENIED_NAME")
                 && not (json.Contains(marker, StringComparison.Ordinal)))
                "Clean-Room-Treffer in Review-Evidenz wurde nicht redigiert blockiert.")
