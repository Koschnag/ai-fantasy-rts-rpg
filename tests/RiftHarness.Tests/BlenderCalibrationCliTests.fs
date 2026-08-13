namespace RiftHarness.Tests

open System
open System.Globalization
open System.IO
open System.Text
open System.Text.Json
open RiftHarness

[<RequireQualifiedAccess>]
module BlenderCalibrationCliTests =
    let private physicalTempRoot =
        let configured = Path.GetFullPath(Path.GetTempPath())

        if
            OperatingSystem.IsMacOS()
            && configured.StartsWith("/var/", StringComparison.Ordinal)
        then
            "/private" + configured
        else
            configured

    let private repositoryRoot =
        let rec findRoot path =
            if File.Exists(Path.Combine(path, "Riftward.slnx")) then
                path
            else
                let parent = Directory.GetParent(path)

                if isNull parent then
                    failwith "Repository root not found."

                findRoot parent.FullName

        findRoot Environment.CurrentDirectory

    let private assertTrue condition message =
        if not condition then
            failwith message

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

    let private withWorkspace action =
        let root =
            Path.Combine(physicalTempRoot, "riftward-calibration-cli-" + Guid.NewGuid().ToString("N"))

        try
            let specDirectory = Path.Combine(root, "assets/specs/3d")
            Directory.CreateDirectory(specDirectory) |> ignore

            File.Copy(
                Path.Combine(repositoryRoot, "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json"),
                Path.Combine(specDirectory, "CAL-STONEWOOD-V1.calibration-v1.json")
            )

            action root
        finally
            if Directory.Exists(root) then
                Directory.Delete(root, true)

    let validateSpecEnvelopeIsCanonicalAndPositionIndependent () =
        withWorkspace (fun root ->
            let spec = "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json"

            let exitCode, output, error =
                capture [ "blender-calibration"; "--workspace"; root; "validate-spec"; "--spec"; spec ]

            assertTrue (exitCode = 0) "validate-spec failed for the canonical reference."
            assertTrue (error = String.Empty) "validate-spec wrote unexpected stderr."
            assertTrue (output.EndsWith("\n", StringComparison.Ordinal)) "CLI output is not LF-terminated."
            assertTrue (not (output.EndsWith("\n\n", StringComparison.Ordinal))) "CLI output has multiple lines."

            let expected =
                "{\"command\":\"validate-spec\",\"ok\":true,\"result\":{\"familyDecodedGeometryBytes\":255048,\"familyId\":\"CAL-STONEWOOD-V1\",\"moduleCount\":3,\"profile\":\"calibration-v1\",\"renderPrimitiveCount\":18,\"specPath\":\"assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json\",\"specSha256\":\"39faae34c4cd515cb724a8ef1e2e4bee159a232136218fbb8afd8edd52db2cf8\"},\"schemaVersion\":1}\n"

            assertTrue (output = expected) "validate-spec success envelope is not canonical or closed."

            use document = JsonDocument.Parse(output)
            let names = document.RootElement.EnumerateObject() |> Seq.map _.Name |> Seq.toArray

            assertTrue
                (names = [| "command"; "ok"; "result"; "schemaVersion" |])
                "Top-level success properties are not ordinal.")

    let invalidCliAndPathMatrixIsRedacted () =
        withWorkspace (fun root ->
            let spec = "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json"
            let marker = "DO-NOT-ECHO-7F3"

            let cases =
                [ ("duplicate-workspace",
                   [ "blender-calibration"
                     "validate-spec"
                     "--workspace"
                     root
                     "--workspace"
                     root
                     "--spec"
                     spec ],
                   "INVALID_ARGUMENT",
                   "validate-spec")
                  ("missing-workspace-value",
                   [ "blender-calibration"; "validate-spec"; "--spec"; spec; "--workspace" ],
                   "INVALID_ARGUMENT",
                   "validate-spec")
                  ("workspace-value-is-verb",
                   [ "blender-calibration"; "--workspace"; "inspect" ],
                   "UNSAFE_PATH",
                   "blender-calibration")
                  ("adjacent-duplicate",
                   [ "blender-calibration"
                     "validate-spec"
                     "--spec"
                     "--spec"
                     "--workspace"
                     root ],
                   "INVALID_ARGUMENT",
                   "validate-spec")
                  ("option-as-value",
                   [ "blender-calibration"
                     "validate-spec"
                     "--spec"
                     "--unknown"
                     "--workspace"
                     root ],
                   "INVALID_ARGUMENT",
                   "validate-spec")
                  ("duplicate",
                   [ "blender-calibration"
                     "validate-spec"
                     "--spec"
                     spec
                     "--spec"
                     spec
                     "--workspace"
                     root ],
                   "INVALID_ARGUMENT",
                   "validate-spec")
                  ("unknown",
                   [ "blender-calibration"
                     "validate-spec"
                     "--spec"
                     spec
                     "--unknown"
                     marker
                     "--workspace"
                     root ],
                   "INVALID_ARGUMENT",
                   "validate-spec")
                  ("absolute",
                   [ "blender-calibration"
                     "validate-spec"
                     "--spec"
                     Path.Combine(root, marker + ".json")
                     "--workspace"
                     root ],
                   "UNSAFE_PATH",
                   "validate-spec")
                  ("traversal",
                   [ "blender-calibration"
                     "validate-spec"
                     "--spec"
                     "assets/specs/3d/../" + marker + ".json"
                     "--workspace"
                     root ],
                   "UNSAFE_PATH",
                   "validate-spec")
                  ("backslash",
                   [ "blender-calibration"
                     "validate-spec"
                     "--spec"
                     "assets\\specs\\3d\\" + marker + ".json"
                     "--workspace"
                     root ],
                   "UNSAFE_PATH",
                   "validate-spec")
                  ("control",
                   [ "blender-calibration"
                     "validate-spec"
                     "--spec"
                     "assets/specs/3d/" + marker + "\u0001.json"
                     "--workspace"
                     root ],
                   "UNSAFE_PATH",
                   "validate-spec")
                  ("invalid-unicode",
                   [ "blender-calibration"
                     "validate-spec"
                     "--spec"
                     "assets/specs/3d/" + String([| char 0xD800 |]) + ".json"
                     "--workspace"
                     root ],
                   "UNSAFE_PATH",
                   "validate-spec") ]

            for label, arguments, expectedCode, expectedCommand in cases do
                let exitCode, output, error = capture arguments
                assertTrue (exitCode = 2) $"Invalid CLI case did not return exit 2: {label}."
                assertTrue (error = String.Empty) $"Invalid CLI case wrote stderr: {label}."
                assertTrue (not (output.Contains(marker, StringComparison.Ordinal))) $"CLI leaked input: {label}."

                assertTrue
                    (output.Split('\n', StringSplitOptions.None).Length = 2)
                    $"CLI wrote multiple lines: {label}."

                use document = JsonDocument.Parse(output)
                let rootElement = document.RootElement
                assertTrue (not (rootElement.GetProperty("ok").GetBoolean())) $"Error marked successful: {label}."

                assertTrue
                    (rootElement.GetProperty("command").GetString() = expectedCommand)
                    $"Unexpected command ID: {label}."

                assertTrue
                    (rootElement.GetProperty("error").GetProperty("code").GetString() = expectedCode)
                    $"Unexpected error code: {label}.")

    let validateSpecRejectsLeafAndParentSymlinks () =
        if OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() then
            withWorkspace (fun root ->
                let outside =
                    Path.Combine(physicalTempRoot, "riftward-calibration-outside-" + Guid.NewGuid().ToString("N"))

                try
                    Directory.CreateDirectory(outside) |> ignore
                    let source = Path.Combine(outside, "outside.json")

                    File.Copy(
                        Path.Combine(repositoryRoot, "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json"),
                        source
                    )

                    let leaf = Path.Combine(root, "assets/specs/3d/LEAF.json")
                    File.CreateSymbolicLink(leaf, source) |> ignore

                    let exitCode, output, error =
                        capture
                            [ "blender-calibration"
                              "validate-spec"
                              "--spec"
                              "assets/specs/3d/LEAF.json"
                              "--workspace"
                              root ]

                    assertTrue (exitCode = 2 && error = String.Empty) "Leaf symlink was not rejected cleanly."

                    assertTrue
                        (output.Contains("\"code\":\"UNSAFE_PATH\"", StringComparison.Ordinal))
                        "Wrong leaf code."

                    Directory.Delete(Path.Combine(root, "assets/specs/3d"), true)

                    Directory.CreateSymbolicLink(Path.Combine(root, "assets/specs/3d"), outside)
                    |> ignore

                    let parentExit, parentOutput, parentError =
                        capture
                            [ "blender-calibration"
                              "validate-spec"
                              "--spec"
                              "assets/specs/3d/outside.json"
                              "--workspace"
                              root ]

                    assertTrue
                        (parentExit = 2 && parentError = String.Empty)
                        "Parent symlink was not rejected cleanly."

                    assertTrue
                        (parentOutput.Contains("\"code\":\"UNSAFE_PATH\"", StringComparison.Ordinal))
                        "Wrong parent code."
                finally
                    if Directory.Exists(outside) then
                        Directory.Delete(outside, true))

    let validateSpecRejectsSymlinkWorkspaceRoot () =
        if OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() then
            withWorkspace (fun root ->
                let link = root + "-link"

                try
                    Directory.CreateSymbolicLink(link, root) |> ignore

                    let exitCode, output, error =
                        capture
                            [ "blender-calibration"
                              "validate-spec"
                              "--spec"
                              "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json"
                              "--workspace"
                              link ]

                    assertTrue (exitCode = 2 && error = String.Empty) "Symlink workspace root was not rejected."

                    assertTrue
                        (output.Contains("\"code\":\"UNSAFE_PATH\"", StringComparison.Ordinal))
                        "Wrong root code."

                    assertTrue
                        (not (output.Contains(link, StringComparison.Ordinal)))
                        "Workspace path leaked in output."
                finally
                    if Directory.Exists(link) then
                        Directory.Delete(link))

    let validateSpecRejectsSymlinkWorkspaceAncestor () =
        if OperatingSystem.IsLinux() || OperatingSystem.IsMacOS() then
            let basePath =
                Path.Combine(physicalTempRoot, "riftward-calibration-ancestor-" + Guid.NewGuid().ToString("N"))

            try
                let targetParent = Path.Combine(basePath, "target")
                let workspace = Path.Combine(targetParent, "workspace")
                let specDirectory = Path.Combine(workspace, "assets/specs/3d")
                Directory.CreateDirectory(specDirectory) |> ignore

                File.Copy(
                    Path.Combine(repositoryRoot, "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json"),
                    Path.Combine(specDirectory, "CAL-STONEWOOD-V1.calibration-v1.json")
                )

                Directory.CreateSymbolicLink(Path.Combine(basePath, "linked-parent"), targetParent)
                |> ignore

                let linkedWorkspace = Path.Combine(basePath, "linked-parent/workspace")

                let exitCode, output, error =
                    capture
                        [ "blender-calibration"
                          "validate-spec"
                          "--spec"
                          "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json"
                          "--workspace"
                          linkedWorkspace ]

                assertTrue (exitCode = 2 && error = String.Empty) "Symlink workspace ancestor was not rejected."

                assertTrue
                    (output.Contains("\"code\":\"UNSAFE_PATH\"", StringComparison.Ordinal))
                    "Wrong ancestor code."
            finally
                if Directory.Exists(basePath) then
                    Directory.Delete(basePath, true)

    let validateSpecMapsNestedWrongTypesToInvalidSpec () =
        withWorkspace (fun root ->
            let original =
                Path.Combine(root, "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json")

            let relative = "assets/specs/3d/WRONG-COLOR-TYPE.json"
            let mutated = Path.Combine(root, relative)

            File
                .ReadAllText(original, Constants.Utf8NoBom)
                .Replace(
                    "\"stoneBaseColorSrgb8\":[96,92,82]",
                    "\"stoneBaseColorSrgb8\":[\"96\",92,82]",
                    StringComparison.Ordinal
                )
            |> fun value -> File.WriteAllText(mutated, value, Constants.Utf8NoBom)

            let exitCode, output, error =
                capture
                    [ "blender-calibration"
                      "validate-spec"
                      "--workspace"
                      root
                      "--spec"
                      relative ]

            assertTrue (exitCode = 2 && error = String.Empty) "Nested wrong type did not map to exit 2."

            assertTrue
                (output.Contains("\"code\":\"INVALID_SPEC\"", StringComparison.Ordinal)
                 && not (output.Contains("WRONG-COLOR", StringComparison.Ordinal)))
                "Nested wrong type leaked input or returned the wrong code.")

    let validateSpecPathLengthBoundariesAreEnforced () =
        withWorkspace (fun root ->
            let source =
                Path.Combine(root, "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json")

            let writeSpec relative =
                let target = Path.Combine(root, relative)
                Directory.CreateDirectory(Path.GetDirectoryName(target)) |> ignore
                File.Copy(source, target)

            let segment80 = String('a', 75) + ".json"
            let segment81 = String('b', 76) + ".json"
            let relative80 = "assets/specs/3d/" + segment80
            let relative81 = "assets/specs/3d/" + segment81

            let nestedPrefix =
                "assets/specs/3d/"
                + String('c', 60)
                + "/"
                + String('d', 60)
                + "/"
                + String('e', 60)
                + "/"

            let relative240 = nestedPrefix + String('f', 36) + ".json"
            let relative241 = nestedPrefix + String('g', 37) + ".json"

            assertTrue (Constants.Utf8NoBom.GetByteCount(segment80) = 80) "80-byte segment fixture drifted."
            assertTrue (Constants.Utf8NoBom.GetByteCount(segment81) = 81) "81-byte segment fixture drifted."
            assertTrue (Constants.Utf8NoBom.GetByteCount(relative240) = 240) "240-byte path fixture drifted."
            assertTrue (Constants.Utf8NoBom.GetByteCount(relative241) = 241) "241-byte path fixture drifted."

            for relative in [ relative80; relative81; relative240; relative241 ] do
                writeSpec relative

            let invoke relative =
                capture
                    [ "blender-calibration"
                      "validate-spec"
                      "--workspace"
                      root
                      "--spec"
                      relative ]

            for label, relative in [ "segment-max", relative80; "path-max", relative240 ] do
                let exitCode, _, error = invoke relative
                assertTrue (exitCode = 0 && error = String.Empty) $"Valid path boundary failed: {label}."

            for label, relative in [ "segment-over", relative81; "path-over", relative241 ] do
                let exitCode, output, error = invoke relative
                assertTrue (exitCode = 2 && error = String.Empty) $"Unsafe path boundary passed: {label}."

                assertTrue
                    (output.Contains("\"code\":\"UNSAFE_PATH\"", StringComparison.Ordinal))
                    $"Unsafe path boundary returned the wrong code: {label}.")

    let validateSpecEnvelopeUsesMinimalUtf8ForNfcPaths () =
        withWorkspace (fun root ->
            let source =
                Path.Combine(root, "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json")

            let relative = "assets/specs/3d/ä.json"
            File.Copy(source, Path.Combine(root, relative))

            let exitCode, output, error =
                capture
                    [ "blender-calibration"
                      "validate-spec"
                      "--workspace"
                      root
                      "--spec"
                      relative ]

            assertTrue (exitCode = 0 && error = String.Empty) "NFC Unicode path was rejected."

            assertTrue
                (output.Contains(relative, StringComparison.Ordinal))
                "NFC Unicode path was not emitted directly."

            assertTrue
                (not (output.Contains("\\u00E4", StringComparison.OrdinalIgnoreCase)))
                "NFC Unicode path was unnecessarily escaped.")

    let inspectEnvelopeAndExitMappingAreCanonical () =
        let root =
            Path.Combine(physicalTempRoot, "riftward-calibration-inspect-cli-" + Guid.NewGuid().ToString("N"))

        try
            let fixture = Asset3dInspectorTests.createInspectionFixture root

            let arguments =
                [ "blender-calibration"
                  "inspect"
                  "--report"
                  fixture.ReportRelative
                  "--workspace"
                  root
                  "--preview"
                  fixture.PreviewRelative
                  "--spec"
                  fixture.SpecRelative
                  "--glb"
                  fixture.GlbRelative ]

            let exitCode, output, error = capture arguments
            assertTrue (exitCode = 0 && error = String.Empty) "Valid inspect CLI call failed."
            assertTrue (output.EndsWith("\n", StringComparison.Ordinal)) "Inspect output is not LF-terminated."

            use document = JsonDocument.Parse(output)
            let envelope = document.RootElement
            let result = envelope.GetProperty("result")

            let names = envelope.EnumerateObject() |> Seq.map _.Name |> Seq.toArray
            let resultNames = result.EnumerateObject() |> Seq.map _.Name |> Seq.toArray

            let expectedResultNames =
                [| "familyDecodedGeometryBytes"
                   "familyId"
                   "glbBytes"
                   "glbPath"
                   "glbSha256"
                   "materialCount"
                   "moduleCount"
                   "previewBytes"
                   "previewPath"
                   "previewSha256"
                   "renderPrimitiveCount"
                   "reportBytes"
                   "reportPath"
                   "reportSha256"
                   "specPath"
                   "specSha256" |]

            assertTrue
                (names = [| "command"; "ok"; "result"; "schemaVersion" |])
                "Inspect envelope is not closed and ordinal."

            assertTrue (resultNames = expectedResultNames) "Inspect result is not the exact ordinal 16-field contract."

            assertTrue (envelope.GetProperty("command").GetString() = "inspect") "Wrong inspect command ID."
            assertTrue (result.GetProperty("familyDecodedGeometryBytes").GetInt64() = 255048L) "Wrong decoded total."
            assertTrue (result.GetProperty("materialCount").GetInt32() = 2) "Wrong material count."

            let invalidGlb = Path.Combine(root, fixture.GlbRelative)
            File.WriteAllBytes(invalidGlb, [| 0uy |])
            let invalidExit, invalidOutput, invalidError = capture arguments

            assertTrue (invalidExit = 5 && invalidError = String.Empty) "Invalid artifact did not map to clean exit 5."

            assertTrue
                (invalidOutput.Contains("\"code\":\"INVALID_ARTIFACT\"", StringComparison.Ordinal))
                "Invalid artifact returned the wrong code."

            File.WriteAllBytes(invalidGlb, Array.zeroCreate<byte> (Asset3dInspector.MaxGlbBytes + 1))
            let budgetExit, budgetOutput, budgetError = capture arguments
            assertTrue (budgetExit = 5 && budgetError = String.Empty) "GLB budget did not map to clean exit 5."

            assertTrue
                (budgetOutput.Contains("\"code\":\"BUDGET_EXCEEDED\"", StringComparison.Ordinal))
                "GLB budget returned the wrong code."

            let unsafeArguments =
                arguments
                |> List.map (fun value ->
                    if value = fixture.GlbRelative then
                        "../DO-NOT-ECHO-INSPECT.glb"
                    else
                        value)

            let unsafeExit, unsafeOutput, unsafeError = capture unsafeArguments
            assertTrue (unsafeExit = 2 && unsafeError = String.Empty) "Unsafe inspect path did not map to exit 2."

            assertTrue
                (unsafeOutput.Contains("\"code\":\"UNSAFE_PATH\"", StringComparison.Ordinal)
                 && not (unsafeOutput.Contains("DO-NOT-ECHO", StringComparison.Ordinal)))
                "Unsafe inspect path leaked or returned the wrong code."
        finally
            if Directory.Exists(root) then
                Directory.Delete(root, true)
