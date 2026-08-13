namespace RiftHarness.Tests

open System
open System.Diagnostics
open System.Globalization
open System.IO
open System.Text.Json
open RiftHarness

[<RequireQualifiedAccess>]
module DotnetAssetPipelineCliTests =
    [<Literal>]
    let private SpecRelative = "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json"

    [<Literal>]
    let private AssetId = "CAL-STONEWOOD-V1-39FAAE34C4CD"

    [<Literal>]
    let private GeneratorActor = "riftward-dotnet-asset-generator"

    let private repositoryRoot =
        let rec findRoot (directory: DirectoryInfo) =
            if File.Exists(Path.Combine(directory.FullName, "Riftward.slnx")) then
                directory.FullName
            elif isNull directory.Parent then
                failwith "Repository root not found."
            else
                findRoot directory.Parent

        findRoot (DirectoryInfo(Environment.CurrentDirectory))

    let private physicalTempRoot =
        let configured = Path.GetFullPath(Path.GetTempPath())

        if
            OperatingSystem.IsMacOS()
            && configured.StartsWith("/var/", StringComparison.Ordinal)
            && Directory.Exists("/private" + configured)
        then
            "/private" + configured
        else
            configured

    let private assertTrue condition message =
        if not condition then
            failwith message

    let private assertEqual expected actual message =
        if not (Unchecked.equals expected actual) then
            failwith $"{message} Expected: {expected}; actual: {actual}."

    let private capture arguments =
        let previousOut = Console.Out
        let previousError = Console.Error
        use output = new StringWriter(CultureInfo.InvariantCulture)
        use error = new StringWriter(CultureInfo.InvariantCulture)

        try
            Console.SetOut(output)
            Console.SetError(error)
            let exitCode = Cli.execute arguments
            exitCode, output.ToString(), error.ToString()
        finally
            Console.SetOut(previousOut)
            Console.SetError(previousError)

    let private copyFile root relative =
        let source = Path.Combine(repositoryRoot, relative)
        let target = Path.Combine(root, relative)
        Directory.CreateDirectory(Path.GetDirectoryName(target)) |> ignore
        File.Copy(source, target, true)

    let private initializeGit root =
        let startInfo = ProcessStartInfo("git")
        startInfo.WorkingDirectory <- root
        startInfo.UseShellExecute <- false
        startInfo.RedirectStandardOutput <- true
        startInfo.RedirectStandardError <- true
        startInfo.ArgumentList.Add("init")
        startInfo.ArgumentList.Add("--quiet")
        use child = Process.Start(startInfo)

        if isNull child then
            failwith "Temporary Git fixture could not start."

        child.WaitForExit()

        if child.ExitCode <> 0 then
            failwith "Temporary Git fixture could not be initialized."

    let private preparePipelineWorkspace root =
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
              "models.lock.json"
              "toolchain.lock.json"
              SpecRelative ] do
            copyFile root relative

        for source in DotnetAssetGenerator.generatorSourcePaths do
            copyFile root source

        initializeGit root

    let private withPipelineWorkspace action =
        let root =
            Path.Combine(physicalTempRoot, "riftward-dotnet-pipeline-cli-" + Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory(root) |> ignore

        try
            preparePipelineWorkspace root
            action root
        finally
            if Directory.Exists(root) then
                Directory.Delete(root, true)

    let private propertyNames (element: JsonElement) =
        element.EnumerateObject() |> Seq.map _.Name |> Seq.toArray

    let private assertCanonicalSingleEnvelope
        (expectedCommand: string)
        (expectedExit: int)
        (exitCode: int)
        (output: string)
        (error: string)
        =
        assertEqual expectedExit exitCode "CLI exit code differs"
        assertEqual String.Empty error "CLI wrote stderr"
        assertTrue (output.EndsWith("\n", StringComparison.Ordinal)) "CLI output lacks its final LF."
        assertTrue (not (output.EndsWith("\n\n", StringComparison.Ordinal))) "CLI output has multiple lines."
        use document = JsonDocument.Parse(output)
        assertEqual expectedCommand (document.RootElement.GetProperty("command").GetString()) "Command id differs"

        let canonical =
            Internal.canonicalElement document.RootElement
            |> Constants.Utf8NoBom.GetString
            |> fun value -> value + "\n"

        assertEqual canonical output "CLI envelope is not canonical"

    let validateAndInspectUseNewNamespaceWithLegacyReadOnlyAliases () =
        withPipelineWorkspace (fun root ->
            let options = [ "validate-spec"; "--spec"; SpecRelative; "--workspace"; root ]
            let newExit, newOutput, newError = capture ("asset-calibration" :: options)
            let oldExit, oldOutput, oldError = capture ("blender-calibration" :: options)
            assertCanonicalSingleEnvelope "validate-spec" 0 newExit newOutput newError

            assertEqual
                (newExit, newOutput, newError)
                (oldExit, oldOutput, oldError)
                "Read-only validate alias drifted")

        let fixtureRoot =
            Path.Combine(physicalTempRoot, "riftward-dotnet-inspect-cli-" + Guid.NewGuid().ToString("N"))

        try
            let fixture = Asset3dInspectorTests.createInspectionFixture fixtureRoot

            let options =
                [ "inspect"
                  "--report"
                  fixture.ReportRelative
                  "--workspace"
                  fixtureRoot
                  "--preview"
                  fixture.PreviewRelative
                  "--spec"
                  fixture.SpecRelative
                  "--glb"
                  fixture.GlbRelative ]

            let newExit, newOutput, newError = capture ("asset-calibration" :: options)
            let oldExit, oldOutput, oldError = capture ("blender-calibration" :: options)
            assertCanonicalSingleEnvelope "inspect" 0 newExit newOutput newError
            assertEqual (newExit, newOutput, newError) (oldExit, oldOutput, oldError) "Read-only inspect alias drifted"
        finally
            if Directory.Exists(fixtureRoot) then
                Directory.Delete(fixtureRoot, true)

    let legacyAliasRejectsGenerateAndRecover () =
        withPipelineWorkspace (fun root ->
            let marker = "DO-NOT-ECHO-LEGACY-MUTATION"

            for command, options in
                [ "generate", [ "--spec"; SpecRelative; "--job-id"; marker ]
                  "recover", [ "--job-id"; marker ] ] do
                let exitCode, output, error =
                    capture ([ "blender-calibration"; "--workspace"; root; command ] @ options)

                assertCanonicalSingleEnvelope command 2 exitCode output error
                assertTrue (not (output.Contains(marker, StringComparison.Ordinal))) "Legacy alias leaked an argument."

                use document = JsonDocument.Parse(output)
                let envelope = document.RootElement

                assertEqual
                    [| "command"; "error"; "ok"; "schemaVersion" |]
                    (propertyNames envelope)
                    "Error shape differs"

                assertEqual
                    "INVALID_ARGUMENT"
                    (envelope.GetProperty("error").GetProperty("code").GetString())
                    "Legacy mutation code differs"

            assertTrue
                (not (Directory.Exists(Path.Combine(root, "assets/quarantine/3d", AssetId))))
                "Legacy alias reached generation.")

    let generateAndRecoverEnvelopesAreCanonicalAndUseFixedActor () =
        withPipelineWorkspace (fun root ->
            let jobId = "01ARZ3NDEKTSV4RRFFQ69G5FB1"

            let generateExit, generateOutput, generateError =
                capture
                    [ "asset-calibration"
                      "--workspace"
                      root
                      "generate"
                      "--job-id"
                      jobId
                      "--spec"
                      SpecRelative ]

            assertCanonicalSingleEnvelope "generate" 0 generateExit generateOutput generateError
            use generatedDocument = JsonDocument.Parse(generateOutput)
            let generated = generatedDocument.RootElement

            assertEqual
                [| "command"; "ok"; "result"; "schemaVersion" |]
                (propertyNames generated)
                "Generate envelope fields differ"

            let result = generated.GetProperty("result")

            assertEqual
                [| "assetId"
                   "glbSha256"
                   "jobId"
                   "manifestPath"
                   "manifestSha256"
                   "previewSha256"
                   "receiptPath"
                   "receiptSha256"
                   "reportSha256"
                   "specPath"
                   "specSha256" |]
                (propertyNames result)
                "Generate result fields differ"

            assertEqual jobId (result.GetProperty("jobId").GetString()) "Generate job id differs"
            assertEqual AssetId (result.GetProperty("assetId").GetString()) "Generate asset id differs"
            assertEqual SpecRelative (result.GetProperty("specPath").GetString()) "Generate spec path differs"

            let runIds = RunStore.allRunIds root
            assertEqual 1 runIds.Length "CLI generation run count differs"
            let runPath = Path.Combine(root, ".ai/runtime/runs", runIds.Head, "run.json")
            use runDocument = JsonDocument.Parse(File.ReadAllBytes(runPath))

            assertEqual
                GeneratorActor
                (runDocument.RootElement.GetProperty("actorId").GetString())
                "CLI did not use the fixed generator actor"

            assertEqual
                $"assets/receipts/{AssetId}/{runIds.Head}.json"
                (result.GetProperty("receiptPath").GetString())
                "CLI returned the wrong T-003 receipt path"

            let recoverExit, recoverOutput, recoverError =
                capture [ "asset-calibration"; "recover"; "--workspace"; root; "--job-id"; jobId ]

            assertCanonicalSingleEnvelope "recover" 0 recoverExit recoverOutput recoverError

            let expectedRecover =
                $"{{\"command\":\"recover\",\"ok\":true,\"result\":{{\"jobId\":\"{jobId}\",\"state\":\"COMMITTED\"}},\"schemaVersion\":1}}\n"

            assertEqual expectedRecover recoverOutput "Recover success envelope differs")

    let pipelineFailuresMapStableRedactedExitCodes () =
        withPipelineWorkspace (fun root ->
            let invalidMarker = "DO-NOT-ECHO-INVALID-JOB"

            let invalidExit, invalidOutput, invalidError =
                capture
                    [ "asset-calibration"
                      "generate"
                      "--workspace"
                      root
                      "--spec"
                      SpecRelative
                      "--job-id"
                      invalidMarker ]

            assertCanonicalSingleEnvelope "generate" 2 invalidExit invalidOutput invalidError
            assertTrue (not (invalidOutput.Contains(invalidMarker, StringComparison.Ordinal))) "Invalid job id leaked."

            use invalidDocument = JsonDocument.Parse(invalidOutput)

            assertEqual
                "INVALID_ARGUMENT"
                (invalidDocument.RootElement.GetProperty("error").GetProperty("code").GetString())
                "Invalid job code differs"

            let manifestPath = Path.Combine(root, "assets/manifests", AssetId + ".json")
            Directory.CreateDirectory(Path.GetDirectoryName(manifestPath)) |> ignore
            let sentinel = Constants.Utf8NoBom.GetBytes("DO-NOT-ECHO-FOREIGN-MANIFEST\n")
            File.WriteAllBytes(manifestPath, sentinel)
            let jobId = "01ARZ3NDEKTSV4RRFFQ69G5FB2"

            let conflictExit, conflictOutput, conflictError =
                capture
                    [ "asset-calibration"
                      "generate"
                      "--spec"
                      SpecRelative
                      "--job-id"
                      jobId
                      "--workspace"
                      root ]

            assertCanonicalSingleEnvelope "generate" 7 conflictExit conflictOutput conflictError

            use conflictDocument = JsonDocument.Parse(conflictOutput)

            assertEqual
                "TRANSACTION_CONFLICT"
                (conflictDocument.RootElement.GetProperty("error").GetProperty("code").GetString())
                "Pipeline conflict code differs"

            assertTrue
                (File.ReadAllBytes(manifestPath).AsSpan().SequenceEqual(sentinel.AsSpan()))
                "CLI pipeline overwrote a foreign target."

            assertTrue
                (not (conflictOutput.Contains("FOREIGN", StringComparison.Ordinal)))
                "Pipeline error leaked foreign bytes.")

    let assetCalibrationWrapperIsClosedAndIgnoresHostInjection () =
        if not (OperatingSystem.IsWindows()) then
            let startInfo = ProcessStartInfo("/bin/sh")
            startInfo.WorkingDirectory <- repositoryRoot
            startInfo.UseShellExecute <- false
            startInfo.RedirectStandardOutput <- true
            startInfo.RedirectStandardError <- true
            startInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "scripts/rift.sh"))
            startInfo.ArgumentList.Add("asset-calibration")
            startInfo.ArgumentList.Add("validate-spec")
            startInfo.ArgumentList.Add("--spec")
            startInfo.ArgumentList.Add(SpecRelative)
            startInfo.Environment["DOTNET_STARTUP_HOOKS"] <- "/tmp/DO-NOT-ECHO-ASSET-HOOK.dll"
            startInfo.Environment["TMPDIR"] <- "/tmp/DO-NOT-ECHO-ASSET-TMP"
            use child = Process.Start(startInfo)

            if isNull child then
                failwith "Asset calibration wrapper process did not start."

            use stdout = new MemoryStream()
            use stderr = new MemoryStream()
            let stdoutCopy = child.StandardOutput.BaseStream.CopyToAsync(stdout)
            let stderrCopy = child.StandardError.BaseStream.CopyToAsync(stderr)

            if not (child.WaitForExit(30_000)) then
                child.Kill(true)
                child.WaitForExit(5_000) |> ignore
                failwith "Asset calibration wrapper process exceeded its test timeout."

            stdoutCopy.GetAwaiter().GetResult()
            stderrCopy.GetAwaiter().GetResult()
            let stdoutBytes = stdout.ToArray()
            let stderrBytes = stderr.ToArray()
            assertEqual 0 child.ExitCode "Asset calibration wrapper exit differs"
            assertEqual 0 stderrBytes.Length "Asset calibration wrapper wrote stderr"

            let expected =
                Constants.Utf8NoBom.GetBytes(
                    "{\"command\":\"validate-spec\",\"ok\":true,\"result\":{\"familyDecodedGeometryBytes\":255048,\"familyId\":\"CAL-STONEWOOD-V1\",\"moduleCount\":3,\"profile\":\"calibration-v1\",\"renderPrimitiveCount\":18,\"specPath\":\"assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json\",\"specSha256\":\"39faae34c4cd515cb724a8ef1e2e4bee159a232136218fbb8afd8edd52db2cf8\"},\"schemaVersion\":1}\n"
                )

            assertTrue (stdoutBytes.AsSpan().SequenceEqual(expected.AsSpan())) "Asset wrapper envelope differs."
