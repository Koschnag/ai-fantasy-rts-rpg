namespace RiftHarness.Tests

open System
open System.Diagnostics
open System.IO
open System.Text.Json
open System.Threading
open RiftHarness

[<RequireQualifiedAccess>]
module DotnetAssetPipelineTests =
    [<Literal>]
    let private AssetId = "CAL-STONEWOOD-V1-39FAAE34C4CD"

    [<Literal>]
    let private SpecRelative = "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json"

    [<Literal>]
    let private ActorId = "riftward-dotnet-asset-generator"

    let private repositoryRoot =
        let rec find path =
            if File.Exists(Path.Combine(path, "Riftward.slnx")) then
                path
            else
                let parent = Directory.GetParent(path)

                if isNull parent then
                    failwith "Repository root not found."

                find parent.FullName

        find Environment.CurrentDirectory

    let private assertTrue condition message =
        if not condition then
            failwith message

    let private assertEqual expected actual message =
        if not (Unchecked.equals expected actual) then
            failwith $"{message} Expected: {expected}; actual: {actual}."

    let private physicalTemporaryRoot () =
        let configured = Path.GetFullPath(Path.GetTempPath())

        if
            OperatingSystem.IsMacOS()
            && configured.StartsWith("/var/", StringComparison.Ordinal)
            && Directory.Exists("/private" + configured)
        then
            "/private" + configured
        else
            configured

    let private copyFile root relative =
        let source = Path.Combine(repositoryRoot, relative)
        let target = Path.Combine(root, relative)
        Directory.CreateDirectory(Path.GetDirectoryName(target)) |> ignore
        File.Copy(source, target, true)

    let private initializeGit root =
        let info = ProcessStartInfo("git")
        info.WorkingDirectory <- root
        info.UseShellExecute <- false
        info.RedirectStandardOutput <- true
        info.RedirectStandardError <- true
        info.ArgumentList.Add("init")
        info.ArgumentList.Add("--quiet")
        use child = Process.Start(info)

        if isNull child then
            failwith "Temporary Git fixture could not start."

        child.WaitForExit()

        if child.ExitCode <> 0 then
            failwith "Temporary Git fixture could not be initialized."

    let private prepareWorkspace root =
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

    let private withWorkspace action =
        let root =
            Path.Combine(physicalTemporaryRoot (), "riftward-dotnet-pipeline-tests-" + Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory(root) |> ignore

        try
            prepareWorkspace root
            action root
        finally
            if Directory.Exists(root) then
                Directory.Delete(root, true)

    let private generate root jobId =
        DotnetAssetPipeline.generate root SpecRelative jobId ActorId

    let private expectPipelineFailure expectedCode expectedExit action =
        let mutable observed: (string * int) option = None

        try
            action ()
        with DotnetAssetPipelineError(code, _, exitCode) ->
            observed <- Some(code, exitCode)

        assertEqual (Some(expectedCode, expectedExit)) observed "Pipeline failure contract differs"

    let private absolute (root: string) (relative: string) =
        relative.Split('/')
        |> Array.fold (fun current segment -> Path.Combine(current, segment)) root

    let private runStatus root runId =
        let path = Path.Combine(root, ".ai/runtime/runs", runId, "run.json")
        use document = JsonDocument.Parse(File.ReadAllBytes(path))
        document.RootElement.GetProperty("status").GetString()

    let happyPathPublishesCommittedT003Quarantine () =
        withWorkspace (fun root ->
            let jobId = "01ARZ3NDEKTSV4RRFFQ69G5FAV"
            let result = generate root jobId

            assertEqual jobId result.JobId "Job binding differs"
            assertEqual AssetId result.AssetId "Asset identity differs"
            assertEqual SpecRelative result.SpecPath "Specification path differs"
            assertTrue (Internal.isSha256 result.SpecSha256) "Specification hash is not canonical."

            for relative in
                [ $"assets/quarantine/3d/{AssetId}/family.glb"
                  $"assets/quarantine/3d/{AssetId}/preview.png"
                  $"assets/quarantine/3d/{AssetId}/technique.json"
                  result.ReceiptPath
                  result.ManifestPath ] do
                assertTrue (File.Exists(absolute root relative)) $"Published file is missing: {relative}."

            assertEqual [] (RunStore.verifyRun root result.RunId) "Generation run is not verifiable"
            assertEqual "succeeded" (runStatus root result.RunId) "Generation run did not succeed"

            let recovery = DotnetAssetPipeline.recover root jobId
            assertEqual "COMMITTED" recovery.State "Committed recovery state differs"

            let local =
                AssetStore.check
                    root
                    { ManifestPath = Some result.ManifestPath
                      RequireLocal = true
                      RequireApproved = false }

            assertTrue local.Valid $"Published T-003 quarantine is invalid: {AssetStore.reportJson local}"

            let approval =
                AssetStore.check
                    root
                    { ManifestPath = Some result.ManifestPath
                      RequireLocal = true
                      RequireApproved = true }

            assertTrue
                (not approval.Valid
                 && approval.Findings
                    |> List.exists (fun finding -> finding.Code = "ASSET_APPROVAL_REQUIRED"))
                "Quarantine unexpectedly passed the approval gate."

            use manifest =
                JsonDocument.Parse(File.ReadAllBytes(absolute root result.ManifestPath))

            assertEqual
                result.ReceiptSha256
                (manifest.RootElement.GetProperty("generationReceiptSha256").GetString())
                "Manifest receipt binding differs"

            assertTrue
                (not (Directory.Exists(Path.Combine(root, "assets/source")))
                 && not (Directory.Exists(Path.Combine(root, "assets/cooked"))))
                "Pipeline leaked output outside quarantine.")

    let equalInputsProduceByteIdenticalArtifacts () =
        let mutable first: DotnetAssetPipelineResult option = None

        withWorkspace (fun root -> first <- Some(generate root "01ARZ3NDEKTSV4RRFFQ69G5FAW"))

        withWorkspace (fun root ->
            let second = generate root "01ARZ3NDEKTSV4RRFFQ69G5FAX"
            let expected = first |> Option.get
            assertEqual expected.GlbSha256 second.GlbSha256 "GLB determinism differs"
            assertEqual expected.PreviewSha256 second.PreviewSha256 "Preview determinism differs"
            assertEqual expected.ReportSha256 second.ReportSha256 "Report determinism differs")

    let committedTamperFailsRecoveryWithoutDeletingForeignBytes () =
        withWorkspace (fun root ->
            let jobId = "01ARZ3NDEKTSV4RRFFQ69G5FAY"
            let result = generate root jobId
            let glbPath = absolute root $"assets/quarantine/3d/{AssetId}/family.glb"
            let marker = [| 0x54uy; 0x41uy; 0x4Duy; 0x50uy; 0x45uy; 0x52uy |]

            use stream =
                new FileStream(glbPath, FileMode.Append, FileAccess.Write, FileShare.None)

            stream.Write(marker, 0, marker.Length)
            stream.Flush(true)
            stream.Dispose()

            let tampered = File.ReadAllBytes(glbPath)

            expectPipelineFailure "TRANSACTION_CONFLICT" 7 (fun () -> DotnetAssetPipeline.recover root jobId |> ignore)

            assertTrue
                (File.ReadAllBytes(glbPath).AsSpan().SequenceEqual(tampered.AsSpan()))
                "Recovery changed or deleted hash-divergent bytes."

            let local =
                AssetStore.check
                    root
                    { ManifestPath = Some result.ManifestPath
                      RequireLocal = true
                      RequireApproved = false }

            assertTrue
                (not local.Valid
                 && local.Findings
                    |> List.exists (fun finding -> finding.Code = "ASSET_OUTPUT_HASH_MISMATCH"))
                "T-003 did not detect published artifact tampering.")

    let targetCollisionIsRejectedWithoutOverwrite () =
        withWorkspace (fun root ->
            let target = $"assets/manifests/{AssetId}.json"
            let targetPath = absolute root target
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)) |> ignore
            let sentinel = Constants.Utf8NoBom.GetBytes("foreign manifest\n")
            File.WriteAllBytes(targetPath, sentinel)

            expectPipelineFailure "TRANSACTION_CONFLICT" 7 (fun () ->
                generate root "01ARZ3NDEKTSV4RRFFQ69G5FAZ" |> ignore)

            assertTrue
                (File.ReadAllBytes(targetPath).AsSpan().SequenceEqual(sentinel.AsSpan()))
                "A pre-existing publication target was overwritten.")

    let receiptTargetCollisionLeavesNoStageOrPublication () =
        withWorkspace (fun root ->
            let jobId = "01ARZ3NDEKTSV4RRFFQ69G5FB1"
            let receiptDirectory = absolute root $"assets/receipts/{AssetId}"
            Directory.CreateDirectory(receiptDirectory) |> ignore

            let originalStart = RunStore.startForActor root ActorId
            RunStore.finish root originalStart "failed" None |> ignore

            let collision = Path.Combine(receiptDirectory, originalStart + ".json")
            let sentinel = Constants.Utf8NoBom.GetBytes("foreign receipt\n")
            File.WriteAllBytes(collision, sentinel)

            // A run id is generated by the harness and cannot be predicted. Bind the collision to
            // the next pipeline run by occupying the whole per-asset receipt directory as a file.
            Directory.Delete(receiptDirectory, true)
            File.WriteAllBytes(receiptDirectory, sentinel)

            expectPipelineFailure "TRANSACTION_CONFLICT" 7 (fun () -> generate root jobId |> ignore)

            assertTrue
                (File.ReadAllBytes(receiptDirectory).AsSpan().SequenceEqual(sentinel.AsSpan()))
                "Receipt target parent collision was overwritten."

            assertTrue
                (not (Directory.Exists(absolute root $".ai/runtime/asset-jobs/{jobId}/stage")))
                "Preflight collision left an unowned stage directory."

            assertTrue
                (not (Directory.Exists(absolute root $"assets/quarantine/3d/{AssetId}"))
                 && not (File.Exists(absolute root $"assets/manifests/{AssetId}.json")))
                "Preflight collision published partial metadata or quarantine bytes.")

    let preFinishArtifactFailureCreatesFailedRunAndRollsBack () =
        withWorkspace (fun root ->
            let lockPath = Path.Combine(root, "toolchain.lock.json")

            File.WriteAllText(
                lockPath,
                File
                    .ReadAllText(lockPath, Constants.Utf8NoBom)
                    .Replace("10.0.110", "10.0.111", StringComparison.Ordinal),
                Constants.Utf8NoBom
            )

            let jobId = "01ARZ3NDEKTSV4RRFFQ69G5FB0"

            expectPipelineFailure "PIN_MISMATCH" 3 (fun () -> generate root jobId |> ignore)

            let runIds = RunStore.allRunIds root
            assertEqual 0 runIds.Length "Pin preflight unexpectedly started a generation run"

            assertTrue
                (not (Directory.Exists(absolute root $".ai/runtime/asset-jobs/{jobId}")))
                "Pin preflight unexpectedly created a job journal"

            assertTrue
                (not (Directory.Exists(Path.Combine(root, $"assets/quarantine/3d/{AssetId}"))))
                "Failed generation left a quarantine publication behind.")

    let productionSpecPinAndJobLimitsFailClosed () =
        withWorkspace (fun root ->
            let fixtureRelative = "tests/Fixtures/Asset3d/generate-must-not-read.json"
            let fixturePath = absolute root fixtureRelative
            Directory.CreateDirectory(Path.GetDirectoryName(fixturePath)) |> ignore
            File.Copy(absolute root SpecRelative, fixturePath, true)

            expectPipelineFailure "UNSAFE_PATH" 2 (fun () ->
                DotnetAssetPipeline.generate root fixtureRelative "01ARZ3NDEKTSV4RRFFQ69G5FB3" ActorId
                |> ignore)

            let jobId = "01ARZ3NDEKTSV4RRFFQ69G5FB4"
            let jobRoot = absolute root $".ai/runtime/asset-jobs/{jobId}"
            Directory.CreateDirectory(jobRoot) |> ignore

            for index = 0 to 64 do
                File.WriteAllText(Path.Combine(jobRoot, $"foreign-{index:D2}.txt"), "x", Constants.Utf8NoBom)

            expectPipelineFailure "RESOURCE_LIMIT" 4 (fun () -> generate root jobId |> ignore)

            assertEqual
                65
                (Directory.EnumerateFiles(jobRoot, "foreign-*.txt") |> Seq.length)
                "Resource preflight mutated foreign job bytes")

        if not (OperatingSystem.IsWindows()) then
            let externalLock =
                Path.Combine(
                    physicalTemporaryRoot (),
                    "riftward-external-toolchain-" + Guid.NewGuid().ToString("N") + ".json"
                )

            try
                withWorkspace (fun root ->
                    let lockPath = Path.Combine(root, "toolchain.lock.json")
                    File.Copy(lockPath, externalLock, true)
                    File.Delete(lockPath)
                    File.CreateSymbolicLink(lockPath, externalLock) |> ignore

                    expectPipelineFailure "UNSAFE_PATH" 2 (fun () ->
                        generate root "01ARZ3NDEKTSV4RRFFQ69G5FB5" |> ignore))
            finally
                if File.Exists(externalLock) then
                    File.Delete(externalLock)

    let cancellationFailsBeforePublicationAndRollsBack () =
        withWorkspace (fun root ->
            use cancellation = new CancellationTokenSource()
            cancellation.Cancel()
            let jobId = "01ARZ3NDEKTSV4RRFFQ69G5FB8"

            expectPipelineFailure "CANCELLED" 4 (fun () ->
                DotnetAssetPipeline.generateWithCancellation root SpecRelative jobId ActorId cancellation.Token
                |> ignore)

            assertTrue
                (not (Directory.Exists(absolute root $"assets/quarantine/3d/{AssetId}")))
                "Cancelled generation published quarantine bytes"

            assertTrue
                (not (File.Exists(absolute root $"assets/manifests/{AssetId}.json")))
                "Cancelled generation published a manifest")
