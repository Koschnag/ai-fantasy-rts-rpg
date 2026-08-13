namespace RiftHarness.Tests

open System
open System.Diagnostics
open System.Globalization
open System.IO
open System.Text.Json
open System.Text.Json.Nodes
open System.Threading
open RiftHarness
open global.Json.Schema

/// Isolated end-to-end regressions for the asset clean-room and provenance boundary.
/// The executable test runner registers these functions explicitly; this module has no side effects.
module AssetRegression =
    let private expect condition message =
        if not condition then
            failwith message

    let private expectFinding code (report: AssetCheckReport) =
        expect
            (report.Findings |> List.exists (fun finding -> finding.Code = code))
            $"Erwartetes Asset-Finding fehlt: {code}. Bericht: {AssetStore.reportJson report}"

    let private repositoryRoot =
        let rec findRoot (path: DirectoryInfo) =
            if File.Exists(Path.Combine(path.FullName, "Riftward.slnx")) then
                path.FullName
            elif isNull path.Parent then
                failwith "Repository-Wurzel fuer Assetregressionen nicht gefunden."
            else
                findRoot path.Parent

        findRoot (DirectoryInfo(Environment.CurrentDirectory))

    let private copyFile targetRoot relative =
        let source = Path.Combine(repositoryRoot, relative)
        let target = Path.Combine(targetRoot, relative)
        Directory.CreateDirectory(Path.GetDirectoryName(target)) |> ignore
        File.Copy(source, target, true)

    let private runProcess root executable arguments =
        let startInfo = ProcessStartInfo(executable)
        startInfo.WorkingDirectory <- root
        startInfo.UseShellExecute <- false
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true

        for argument in arguments do
            startInfo.ArgumentList.Add(argument)

        use child = Process.Start(startInfo)

        if isNull child then
            failwith $"Testprozess konnte nicht gestartet werden: {executable}"

        let stdout = child.StandardOutput.ReadToEndAsync()
        let stderr = child.StandardError.ReadToEndAsync()

        if not (child.WaitForExit(10_000)) then
            child.Kill(true)
            failwith $"Testprozess ueberschritt das Zeitlimit: {executable}"

        stdout.Wait()
        stderr.Wait()

        if child.ExitCode <> 0 then
            failwith $"Testprozess fehlgeschlagen: {executable}; {stderr.Result}"

        stdout.Result.Trim()

    let private runGit root arguments = runProcess root "git" arguments

    let private initializeGit root =
        runGit root [ "init"; "--quiet" ] |> ignore

    let private readObject path =
        let node = JsonNode.Parse(File.ReadAllText(path, Constants.Utf8NoBom))

        if isNull node then
            failwith $"JSON-Testfixture ist leer: {path}"

        node.AsObject()

    let private writeObject path (root: JsonObject) =
        let options = JsonSerializerOptions(WriteIndented = true)
        File.WriteAllText(path, root.ToJsonString(options) + "\n", Constants.Utf8NoBom)

    let private fixtureManifestPath root =
        Path.Combine(root, "assets", "manifests", "fixture.json")

    let private standardOptions =
        { ManifestPath = None
          RequireLocal = false
          RequireApproved = false }

    let private copyAssetContract targetRoot =
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
            copyFile targetRoot relative

        let sourceManifestRelative =
            "assets/manifests/ENV-FLOODED-CAUSEWAY-KEYFRAME-002.json"

        let sourceManifestPath = Path.Combine(repositoryRoot, sourceManifestRelative)
        use manifest = JsonDocument.Parse(File.ReadAllBytes(sourceManifestPath))
        let manifestRoot = manifest.RootElement

        for input in manifestRoot.GetProperty("inputs").EnumerateArray() do
            let path = input.GetProperty("path")

            if path.ValueKind = JsonValueKind.String then
                copyFile targetRoot (path.GetString())

        copyFile targetRoot (manifestRoot.GetProperty("generationReceipt").GetString())

        for output in manifestRoot.GetProperty("outputs").EnumerateArray() do
            let relative = output.GetProperty("path").GetString()

            if File.Exists(Path.Combine(repositoryRoot, relative)) then
                copyFile targetRoot relative

        Directory.CreateDirectory(Path.Combine(targetRoot, "assets", "manifests"))
        |> ignore

        File.Copy(sourceManifestPath, fixtureManifestPath targetRoot, true)

    let private withFixture action =
        let root =
            Path.Combine(Path.GetTempPath(), "RiftHarness.AssetRegression-" + Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory(root) |> ignore

        try
            Workspace.initialize root |> ignore
            copyAssetContract root
            initializeGit root
            action root
        finally
            if Directory.Exists(root) then
                Directory.Delete(root, true)

    let private assertControlledInvalid expectedCode root =
        let report = AssetStore.check root standardOptions
        expect (not report.Valid) "Manipuliertes Assetfixture wurde gueltig gemeldet."
        expectFinding expectedCode report

        let json = AssetStore.reportJson report
        use parsed = JsonDocument.Parse(json)
        expect (parsed.RootElement.GetProperty("valid").GetBoolean() = false) "Finding-JSON meldet valid=true."

    let traversalOutputPathIsRejected () =
        withFixture (fun root ->
            let path = fixtureManifestPath root
            let manifest = readObject path
            let output = (manifest["outputs"].AsArray()[0]).AsObject()
            output["path"] <- JsonValue.Create("assets/quarantine/../source/traversal.png")
            writeObject path manifest

            assertControlledInvalid "ASSET_OUTPUT_UNSAFE" root)

    let trustRootSymlinksFailClosed () =
        if not (OperatingSystem.IsWindows()) then
            for relative in
                [ ".ai/schemas/asset-manifest.schema.json"
                  ".ai/policies/asset-clean-room.json"
                  "models.lock.json" ] do
                withFixture (fun root ->
                    let trustedPath = Path.Combine(root, relative)

                    let targetPath =
                        Path.Combine(root, ".symlink-target-" + Guid.NewGuid().ToString("N") + ".json")

                    File.Move(trustedPath, targetPath)
                    File.CreateSymbolicLink(trustedPath, targetPath) |> ignore

                    assertControlledInvalid "ASSET_TRUST_ROOT_INVALID" root)

            withFixture (fun root ->
                let manifestDirectory = Path.Combine(root, "assets", "manifests")
                let targetDirectory = Path.Combine(root, ".manifest-directory-target")
                Directory.Move(manifestDirectory, targetDirectory)
                Directory.CreateSymbolicLink(manifestDirectory, targetDirectory) |> ignore
                assertControlledInvalid "ASSET_TRUST_ROOT_INVALID" root)

            withFixture (fun root ->
                let original = fixtureManifestPath root
                let target = Path.Combine(root, ".manifest-file-target.json")
                File.Move(original, target)
                File.CreateSymbolicLink(original, target) |> ignore

                let report =
                    AssetStore.check
                        root
                        { standardOptions with
                            ManifestPath = Some "assets/manifests/fixture.json" }

                expect (not report.Valid) "Angefordertes Manifest-Symlink wurde akzeptiert."
                expectFinding "ASSET_TRUST_ROOT_INVALID" report)

    let assetInputSymlinkIsRejected () =
        if not (OperatingSystem.IsWindows()) then
            withFixture (fun root ->
                let manifest = readObject (fixtureManifestPath root)

                let specRelative =
                    ((manifest["inputs"].AsArray()[0]).AsObject()["path"]).GetValue<string>()

                let specPath = Path.Combine(root, specRelative)
                let target = Path.Combine(root, ".spec-target.md")
                File.Move(specPath, target)
                File.CreateSymbolicLink(specPath, target) |> ignore
                let report = AssetStore.check root standardOptions
                expect (not report.Valid) "Assetinput-Symlink wurde akzeptiert."

                expect
                    (report.Findings
                     |> List.exists (fun finding ->
                         finding.Code = "ASSET_INPUT_UNSAFE"
                         || finding.Code = "CLEAN_ROOM_INPUT_UNREADABLE"
                         || finding.Code = "ASSET_RECEIPT_SPEC_INVALID"))
                    "Assetinput-Symlink erzeugte kein kontrolliertes Finding.")

    let cleanRoomPolicyDenyAllowCollisionIsRejected () =
        withFixture (fun root ->
            let policyPath = Path.Combine(root, ".ai", "policies", "asset-clean-room.json")
            let policy = readObject policyPath
            let entries = policy["entries"].AsArray()
            let deniedHash = (entries[0].AsObject()["valueSha256"]).GetValue<string>()
            entries[1].AsObject()["valueSha256"] <- JsonValue.Create(deniedHash)
            writeObject policyPath policy
            assertControlledInvalid "ASSET_POLICY_CROSS_KIND_COLLISION" root)

    let allowedNameRegisterIsRecognizedWithDenyPrecedence () =
        let marker = "synthetic internal entity"

        let hash kind =
            Internal.sha256Text ($"clean-room-v1\u0000{kind}\u0000{marker}")

        withFixture (fun root ->
            let policyPath = Path.Combine(root, ".ai", "policies", "asset-clean-room.json")
            let policy = readObject policyPath

            policy["entries"]
                .AsArray()
                .Add(
                    JsonNode.Parse(
                        $$"""{"policyEntryId":"CR-ALLOWED-SYNTHETIC-ENTITY","kind":"allowed-name","valueSha256":"{{hash "allowed-name"}}"}"""
                    )
                )

            writeObject policyPath policy
            let manifest = readObject (fixtureManifestPath root)
            manifest["purpose"] <- JsonValue.Create(marker)
            writeObject (fixtureManifestPath root) manifest
            let report = AssetStore.check root standardOptions
            expect report.Valid "Registrierter interner Name wurde abgelehnt."
            expectFinding "CLEAN_ROOM_ALLOWED_NAME" report)

        withFixture (fun root ->
            let policyPath = Path.Combine(root, ".ai", "policies", "asset-clean-room.json")
            let policy = readObject policyPath

            for id, kind in
                [ "CR-ALLOWED-SYNTHETIC-ENTITY", "allowed-name"
                  "CR-DENIED-SYNTHETIC-ENTITY", "denied-name" ] do
                policy["entries"]
                    .AsArray()
                    .Add(
                        JsonNode.Parse(
                            $$"""{"policyEntryId":"{{id}}","kind":"{{kind}}","valueSha256":"{{hash kind}}"}"""
                        )
                    )

            writeObject policyPath policy
            let manifest = readObject (fixtureManifestPath root)
            manifest["purpose"] <- JsonValue.Create(marker)
            writeObject (fixtureManifestPath root) manifest
            assertControlledInvalid "CLEAN_ROOM_DENIED_NAME" root)

    let cleanRoomScansSpecificationContent () =
        withFixture (fun root ->
            let marker = String.Join(" ", [ "synthetic"; "forbidden"; "proper"; "noun" ])
            let manifest = readObject (fixtureManifestPath root)

            let specRelative =
                ((manifest["inputs"].AsArray()[0]).AsObject()["path"]).GetValue<string>()

            File.AppendAllText(Path.Combine(root, specRelative), "\n" + marker + "\n", Constants.Utf8NoBom)
            let report = AssetStore.check root standardOptions
            let json = AssetStore.reportJson report
            expect (not report.Valid) "Clean-Room-Deny-Marker in Spezifikation wurde akzeptiert."
            expectFinding "CLEAN_ROOM_DENIED_NAME" report
            expect (not (json.Contains(marker, StringComparison.Ordinal))) "Spezifikationsmarker wurde ausgegeben.")

    let invalidReceiptAndPolicySchemasFailClosed () =
        withFixture (fun root ->
            let manifest = readObject (fixtureManifestPath root)

            let receiptPath =
                Path.Combine(root, manifest["generationReceipt"].GetValue<string>())

            let receipt = readObject receiptPath
            receipt.Remove("status") |> ignore
            writeObject receiptPath receipt

            assertControlledInvalid "ASSET_RECEIPT_SCHEMA_INVALID" root)

        withFixture (fun root ->
            let policyPath = Path.Combine(root, ".ai", "policies", "asset-clean-room.json")
            let policy = readObject policyPath
            policy.Remove("maxNGramWords") |> ignore
            writeObject policyPath policy

            assertControlledInvalid "ASSET_POLICY_SCHEMA_INVALID" root)

        withFixture (fun root ->
            let lockPath = Path.Combine(root, "models.lock.json")
            let modelLock = readObject lockPath
            modelLock.Remove("models") |> ignore
            writeObject lockPath modelLock

            assertControlledInvalid "MODEL_LOCK_SCHEMA_INVALID" root)

    let manifestRequiredFieldMatrixIsStrict () =
        withFixture (fun root ->
            let schemaPath = Path.Combine(root, ".ai", "schemas", "asset-manifest.schema.json")
            let schemaText = File.ReadAllText(schemaPath, Constants.Utf8NoBom)
            let schema = JsonSchema.FromText(schemaText)
            use schemaDocument = JsonDocument.Parse(schemaText)
            let schemaRoot = schemaDocument.RootElement
            let original = readObject (fixtureManifestPath root)
            let options = EvaluationOptions(RequireFormatValidation = true)

            let isValid (node: JsonObject) =
                use instance = JsonDocument.Parse(node.ToJsonString())
                schema.Evaluate(instance.RootElement, options).IsValid

            expect (isValid original) "Ausgangsmanifest der Pflichtfeldmatrix ist schemaugueltig."

            let requiredNames (contract: JsonElement) =
                contract.GetProperty("required").EnumerateArray()
                |> Seq.map _.GetString()
                |> Seq.toList

            for field in requiredNames schemaRoot do
                let mutated = original.DeepClone().AsObject()
                mutated.Remove(field) |> ignore
                expect (not (isValid mutated)) $"Fehlendes Manifest-Pflichtfeld wurde akzeptiert: {field}."

            let definitions = schemaRoot.GetProperty("$defs")

            let nestedContracts: (string * (JsonObject -> JsonObject) * JsonElement) list =
                [ "generator", (fun manifest -> manifest["generator"].AsObject()), definitions.GetProperty("generator")
                  "input",
                  (fun manifest -> (manifest["inputs"].AsArray()[0]).AsObject()),
                  definitions.GetProperty("inputs").GetProperty("items")
                  "output",
                  (fun manifest -> (manifest["outputs"].AsArray()[0]).AsObject()),
                  definitions.GetProperty("outputs").GetProperty("items")
                  "licenseBasis",
                  (fun manifest -> manifest["licenseBasis"].AsObject()),
                  schemaRoot.GetProperty("properties").GetProperty("licenseBasis")
                  "review",
                  (fun manifest -> (manifest["reviews"].AsArray()[0]).AsObject()),
                  schemaRoot.GetProperty("properties").GetProperty("reviews").GetProperty("items") ]

            for label, select, contract in nestedContracts do
                for field in requiredNames contract do
                    let mutated = original.DeepClone().AsObject()
                    select mutated |> fun target -> target.Remove(field) |> ignore

                    expect (not (isValid mutated)) $"Fehlendes {label}-Pflichtfeld wurde akzeptiert: {field}.")

    let duplicateModelLockTupleIsRejected () =
        withFixture (fun root ->
            let lockPath = Path.Combine(root, "models.lock.json")
            let modelLock = readObject lockPath

            for id in [ "synthetic-model-a"; "synthetic-model-b" ] do
                modelLock["models"]
                    .AsArray()
                    .Add(
                        JsonNode.Parse(
                            $$"""{"id":"{{id}}","model":"synthetic-generator","modelVersion":"1.0.0","executionMode":"remote","modelArtifactSha256":null,"codeLicense":"Permissive synthetic fixture","weightsLicense":"Synthetic fixture terms","outputTerms":"Synthetic commercial-use fixture terms","trainingDataDisclosure":"Synthetic fixture disclosure","commercialUseReviewed":false,"status":"blocked","reviewedAtUtc":null}"""
                        )
                    )

            writeObject lockPath modelLock
            assertControlledInvalid "MODEL_LOCK_DUPLICATE_TUPLE" root)

    let modelLockStatusMustMatchApprovedEntries () =
        withFixture (fun root ->
            let lockPath = Path.Combine(root, "models.lock.json")
            let modelLock = readObject lockPath
            modelLock["status"] <- JsonValue.Create("production-models-approved")
            writeObject lockPath modelLock
            assertControlledInvalid "MODEL_LOCK_STATUS_MISMATCH" root)

    let approvedLocalModelRequiresArtifactHash () =
        withFixture (fun root ->
            let lockPath = Path.Combine(root, "models.lock.json")
            let modelLock = readObject lockPath
            modelLock["status"] <- JsonValue.Create("production-models-approved")

            modelLock["models"]
                .AsArray()
                .Add(
                    JsonNode.Parse(
                        """{"id":"synthetic-local-model","model":"synthetic-generator","modelVersion":"1.0.0","executionMode":"local","modelArtifactSha256":null,"codeLicense":"Permissive synthetic fixture","weightsLicense":"Synthetic fixture terms","outputTerms":"Synthetic commercial-use fixture terms","trainingDataDisclosure":"Synthetic fixture disclosure","commercialUseReviewed":true,"status":"approved","reviewedAtUtc":"2026-08-13T14:05:00Z"}"""
                    )
                )

            writeObject lockPath modelLock
            assertControlledInvalid "MODEL_LOCK_LOCAL_ARTIFACT_MISSING" root)

    let trackedSourceOrphanIsRejected () =
        withFixture (fun root ->
            let relative = "assets/source/orphan.gltf"
            let absolute = Path.Combine(root, relative)
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)) |> ignore
            File.WriteAllText(absolute, "{\"asset\":{\"version\":\"2.0\"}}\n", Constants.Utf8NoBom)
            runGit root [ "add"; "-f"; "--"; relative ] |> ignore

            assertControlledInvalid "ASSET_SOURCE_ORPHAN" root)

    let duplicateManifestIdentityAndReceiptAreRejected () =
        withFixture (fun root ->
            File.Copy(fixtureManifestPath root, Path.Combine(root, "assets", "manifests", "duplicate.json"))

            let report = AssetStore.check root standardOptions
            expect (not report.Valid) "Doppelte Manifestidentitaet wurde akzeptiert."
            expectFinding "ASSET_ID_DUPLICATE" report
            expectFinding "ASSET_RECEIPT_DUPLICATE" report)

    let cleanRoomFilenameAndPropertyKeyAreRedacted () =
        withFixture (fun root ->
            let markerWords = String.Join(" ", [ "synthetic"; "forbidden"; "proper"; "noun" ])
            let markerFile = markerWords.Replace(' ', '-')
            let originalPath = fixtureManifestPath root
            let manifest = readObject originalPath

            let metrics =
                ((manifest["outputs"].AsArray()[0]).AsObject()["technicalMetrics"]).AsObject()

            metrics[markerWords] <- JsonValue.Create("synthetic calibration value")
            let hostilePath = Path.Combine(root, "assets", "manifests", markerFile + ".json")
            writeObject hostilePath manifest
            File.Delete(originalPath)

            let previousOut = Console.Out
            use output = new StringWriter(CultureInfo.InvariantCulture)

            let exitCode =
                try
                    Console.SetOut(output)
                    Cli.execute [ "assets-check"; "--workspace"; root ]
                finally
                    Console.SetOut(previousOut)

            let json = output.ToString()
            expect (exitCode = 2) "Clean-Room-CLI lieferte fuer Deny-Treffer nicht Exitcode 2."
            use parsed = JsonDocument.Parse(json)
            expect (not (parsed.RootElement.GetProperty("valid").GetBoolean())) "Clean-Room-CLI meldete valid=true."
            expect (json.Contains("CLEAN_ROOM_DENIED_NAME", StringComparison.Ordinal)) "Deny-Finding fehlt."

            for secret in [ markerWords; markerFile ] do
                expect
                    (not (json.Contains(secret, StringComparison.OrdinalIgnoreCase)))
                    "Clean-Room-CLI hat Dateiname oder Property-Key im JSON offengelegt.")

    let unsafeUnicodeIsRejectedAndRedacted () =
        withFixture (fun root ->
            let path = fixtureManifestPath root
            let manifest = readObject path
            let unsafeText = "synthetic" + string (char 0x202E) + "calibration"
            manifest["purpose"] <- JsonValue.Create(unsafeText)
            writeObject path manifest

            let report = AssetStore.check root standardOptions
            let json = AssetStore.reportJson report
            expect (not report.Valid) "Unsicheres Unicode-Formatzeichen wurde akzeptiert."
            expectFinding "CLEAN_ROOM_UNSAFE_UNICODE" report

            expect
                (not (json.Contains(string (char 0x202E), StringComparison.Ordinal)))
                "Unsicherer Unicode-Inhalt wurde im Finding-JSON offengelegt.")

    let hashNamedMetadataCannotBypassCleanRoom () =
        withFixture (fun root ->
            let marker = String.Join(" ", [ "synthetic"; "forbidden"; "proper"; "noun" ])
            let manifest = readObject (fixtureManifestPath root)
            let output = (manifest["outputs"].AsArray()[0]).AsObject()
            let metrics = output["technicalMetrics"].AsObject()
            metrics["noteSha256"] <- JsonValue.Create(marker)
            writeObject (fixtureManifestPath root) manifest
            let report = AssetStore.check root standardOptions
            expect (not report.Valid) "Hashartig benannter Freitext umging Clean-Room-Pruefung."
            expectFinding "CLEAN_ROOM_DENIED_NAME" report)

    let oversizedAssetIntegersFailControlled () =
        withFixture (fun root ->
            let manifest = readObject (fixtureManifestPath root)
            let output = (manifest["outputs"].AsArray()[0]).AsObject()
            output["bytes"] <- JsonNode.Parse("999999999999999999999999999999999999999")
            writeObject (fixtureManifestPath root) manifest
            assertControlledInvalid "ASSET_SCHEMA_INVALID" root)

        withFixture (fun root ->
            let manifest = readObject (fixtureManifestPath root)
            let review = (manifest["reviews"].AsArray()[0]).AsObject()
            review["revision"] <- JsonNode.Parse("999999999999999999999999999999999999999")
            writeObject (fixtureManifestPath root) manifest
            assertControlledInvalid "ASSET_SCHEMA_INVALID" root)

    let oversizedManifestFileFailsControlled () =
        withFixture (fun root ->
            let manifestPath = fixtureManifestPath root

            use stream =
                new FileStream(manifestPath, FileMode.Open, FileAccess.Write, FileShare.None)

            stream.SetLength(Constants.MaxPayloadBytes + 1L)
            stream.Flush(true)

            let report = AssetStore.check root standardOptions
            expect (not report.Valid) "Uebergrosses Manifest wurde akzeptiert."
            expectFinding "ASSET_INPUT_INVALID" report
            use parsed = JsonDocument.Parse(AssetStore.reportJson report)

            expect
                (not (parsed.RootElement.GetProperty("valid").GetBoolean()))
                "Uebergrosses Manifest lieferte kein kontrolliertes JSON.")

    let whitespaceRightsAndActorsAreRejected () =
        withFixture (fun root ->
            let manifest = readObject (fixtureManifestPath root)
            manifest["createdBy"] <- JsonValue.Create("   ")
            manifest["licenseBasis"].AsObject()["inputRights"] <- JsonValue.Create(" \t ")
            let input = (manifest["inputs"].AsArray()[0]).AsObject()
            input["rightsEvidence"] <- JsonValue.Create("   ")
            writeObject (fixtureManifestPath root) manifest
            assertControlledInvalid "ASSET_SCHEMA_INVALID" root)

    let reviewHistoryMustBeContiguous () =
        withFixture (fun root ->
            let manifest = readObject (fixtureManifestPath root)
            let reviews = manifest["reviews"].AsArray()
            let first = (reviews[0]).DeepClone().AsObject()
            let firstId = (first["reviewId"]).GetValue<string>()
            first["reviewId"] <- JsonValue.Create("REV-SYNTHETIC-TECH-003")
            first["revision"] <- JsonValue.Create(3)
            first["active"] <- JsonValue.Create(false)
            first["supersedesReviewId"] <- JsonValue.Create(firstId)
            reviews.Add(first)
            writeObject (fixtureManifestPath root) manifest
            assertControlledInvalid "ASSET_REVIEW_CHAIN_INVALID" root)

    let malformedLfsPointerIsRejected () =
        withFixture (fun root ->
            let path = fixtureManifestPath root
            let manifest = readObject path
            manifest["status"] <- JsonValue.Create("approved")
            manifest["generationBindingMode"] <- JsonValue.Create("canonical-event-v1")
            manifest["prompts"].AsObject()["bindingMode"] <- JsonValue.Create("canonical-envelope-v1")

            let license = manifest["licenseBasis"].AsObject()
            license["commercialUseReviewed"] <- JsonValue.Create(true)
            license["reviewedAtUtc"] <- JsonValue.Create("2026-08-13T14:05:00Z")

            let reviews = manifest["reviews"].AsArray()
            reviews[1].AsObject()["result"] <- JsonValue.Create("pass")

            for reviewJson in
                [ """{"reviewId":"REV-SYNTHETIC-PERF-001","revision":1,"active":true,"kind":"performance","reviewerId":"synthetic-performance-reviewer","atUtc":"2026-08-13T14:05:00Z","result":"pass","evidence":"Synthetic fixture.","evidenceArtifact":null,"supersedesReviewId":null}"""
                  """{"reviewId":"REV-SYNTHETIC-ORIG-001","revision":1,"active":true,"kind":"originality","reviewerId":"synthetic-originality-reviewer","atUtc":"2026-08-13T14:05:00Z","result":"pass","evidence":"Synthetic fixture.","evidenceArtifact":null,"supersedesReviewId":null}"""
                  """{"reviewId":"REV-SYNTHETIC-LICENSE-001","revision":1,"active":true,"kind":"license","reviewerId":"synthetic-license-reviewer","atUtc":"2026-08-13T14:05:00Z","result":"pass","evidence":"Synthetic fixture.","evidenceArtifact":null,"supersedesReviewId":null}""" ] do
                reviews.Add(JsonNode.Parse(reviewJson))

            let relative = "assets/source/malformed-pointer.png"
            let absolute = Path.Combine(root, relative)
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)) |> ignore
            File.WriteAllBytes(absolute, [| 0uy; 1uy; 2uy; 3uy |])

            let output = (manifest["outputs"].AsArray()[0]).AsObject()
            output["path"] <- JsonValue.Create(relative)
            output["sha256"] <- JsonValue.Create(Internal.sha256File absolute)
            output["bytes"] <- JsonValue.Create(FileInfo(absolute).Length)
            writeObject path manifest

            runGit root [ "add"; "-f"; "--"; ".gitattributes" ] |> ignore
            let pointerPath = Path.Combine(root, ".ai", "runtime", "malformed-lfs-pointer.txt")

            File.WriteAllText(
                pointerPath,
                "version https://git-lfs.github.com/spec/v1\noid sha256:"
                + String('0', 64)
                + "\nsize 4\nunexpected extra line\n",
                Constants.Utf8NoBom
            )

            let blob = runGit root [ "hash-object"; "-w"; "--no-filters"; pointerPath ]

            runGit root [ "update-index"; "--add"; "--cacheinfo"; "100644"; blob; relative ]
            |> ignore

            assertControlledInvalid "ASSET_LFS_POINTER_INVALID" root

            File.WriteAllText(
                pointerPath,
                "version https://git-lfs.github.com/spec/v1\noid sha256:"
                + String('0', 64)
                + "\nsize 999999999999999999999999999999999999999999\n",
                Constants.Utf8NoBom
            )

            let oversizedBlob = runGit root [ "hash-object"; "-w"; "--no-filters"; pointerPath ]

            runGit root [ "update-index"; "--add"; "--cacheinfo"; "100644"; oversizedBlob; relative ]
            |> ignore

            assertControlledInvalid "ASSET_LFS_POINTER_INVALID" root)

    let private withFakeGit script action =
        if not (OperatingSystem.IsWindows()) then
            let directory =
                Path.Combine(Path.GetTempPath(), "RiftHarness.FakeGit-" + Guid.NewGuid().ToString("N"))

            Directory.CreateDirectory(directory) |> ignore
            let executable = Path.Combine(directory, "git")
            File.WriteAllText(executable, "#!/bin/sh\n" + script + "\n", Constants.Utf8NoBom)

            File.SetUnixFileMode(
                executable,
                UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute
            )

            let previousPath = Environment.GetEnvironmentVariable("PATH")

            try
                Environment.SetEnvironmentVariable("PATH", directory + string Path.PathSeparator + previousPath)
                action ()
            finally
                Environment.SetEnvironmentVariable("PATH", previousPath)

                if Directory.Exists(directory) then
                    Directory.Delete(directory, true)

    let fakeGitLargeOutputDoesNotDeadlock () =
        withFixture (fun root ->
            withFakeGit "head -c 1048577 /dev/zero 1>&2\nprintf 'synthetic-output'\nexit 0" (fun () ->
                let timer = Stopwatch.StartNew()
                let report = AssetStore.check root standardOptions
                timer.Stop()
                expect (timer.Elapsed < TimeSpan.FromSeconds(5.0)) "Grosse Git-Ausgabe verursachte einen Deadlock."
                expect (not report.Valid) "Grosse Git-Ausgabe wurde als sicher akzeptiert."
                expectFinding "ASSET_GIT_CHECK_FAILED" report))

    let fakeGitTimeoutFailsControlled () =
        withFixture (fun root ->
            withFakeGit "sleep 30" (fun () ->
                let timer = Stopwatch.StartNew()
                let report = AssetStore.check root standardOptions
                timer.Stop()
                expect (timer.Elapsed < TimeSpan.FromSeconds(15.0)) "Git-Timeout blieb unbeschraenkt haengen."
                expect (not report.Valid) "Git-Timeout wurde als sicher akzeptiert."
                expectFinding "ASSET_GIT_CHECK_FAILED" report))
