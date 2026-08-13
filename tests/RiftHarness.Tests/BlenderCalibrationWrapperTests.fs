namespace RiftHarness.Tests

open System
open System.Diagnostics
open System.IO
open System.Text.Json
open RiftHarness

[<RequireQualifiedAccess>]
module BlenderCalibrationWrapperTests =
    let private repositoryRoot =
        let rec findRoot (directory: DirectoryInfo) =
            if File.Exists(Path.Combine(directory.FullName, "Riftward.slnx")) then
                directory.FullName
            elif isNull directory.Parent then
                failwith "Repository root not found."
            else
                findRoot directory.Parent

        findRoot (DirectoryInfo(Environment.CurrentDirectory))

    let private assertTrue condition message =
        if not condition then
            failwith message

    let validateSpecWrapperIsClosedAndIgnoresHostInjectionEnvironment () =
        if not (OperatingSystem.IsWindows()) then
            let startInfo = ProcessStartInfo("/bin/sh")
            startInfo.WorkingDirectory <- repositoryRoot
            startInfo.UseShellExecute <- false
            startInfo.RedirectStandardOutput <- true
            startInfo.RedirectStandardError <- true
            startInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "scripts/rift.sh"))
            startInfo.ArgumentList.Add("blender-calibration")
            startInfo.ArgumentList.Add("validate-spec")
            startInfo.ArgumentList.Add("--spec")
            startInfo.ArgumentList.Add("assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json")

            startInfo.Environment["DOTNET_STARTUP_HOOKS"] <- "/tmp/DO-NOT-ECHO-WRAPPER-HOOK.dll"

            startInfo.Environment["TMPDIR"] <- "/tmp/DO-NOT-ECHO-WRAPPER-TMP"

            use child = Process.Start(startInfo)

            if isNull child then
                failwith "Calibration wrapper process did not start."

            use stdout = new MemoryStream()
            use stderr = new MemoryStream()
            let stdoutCopy = child.StandardOutput.BaseStream.CopyToAsync(stdout)
            let stderrCopy = child.StandardError.BaseStream.CopyToAsync(stderr)

            if not (child.WaitForExit(30_000)) then
                child.Kill(true)
                child.WaitForExit(5_000) |> ignore
                failwith "Calibration wrapper process exceeded its test timeout."

            stdoutCopy.GetAwaiter().GetResult()
            stderrCopy.GetAwaiter().GetResult()
            let stdoutBytes = stdout.ToArray()
            let stderrBytes = stderr.ToArray()

            assertTrue (child.ExitCode = 0) "Calibration wrapper did not return exit 0."
            assertTrue (stderrBytes.Length = 0) "Calibration wrapper wrote stderr."
            assertTrue (stdoutBytes.Length <= 1_048_576) "Calibration wrapper exceeded the stdout limit."

            assertTrue
                (stdoutBytes.Length < 3
                 || stdoutBytes[0] <> 0xEFuy
                 || stdoutBytes[1] <> 0xBBuy
                 || stdoutBytes[2] <> 0xBFuy)
                "Calibration wrapper output has a UTF-8 BOM."

            assertTrue
                (stdoutBytes.Length > 0
                 && stdoutBytes[stdoutBytes.Length - 1] = byte '\n'
                 && (stdoutBytes |> Array.filter ((=) (byte '\n')) |> Array.length) = 1)
                "Calibration wrapper output is not exactly one LF-terminated line."

            use document = JsonDocument.Parse(ReadOnlyMemory<byte>(stdoutBytes))
            assertTrue (document.RootElement.ValueKind = JsonValueKind.Object) "Calibration wrapper output is not JSON."

            let expected =
                Constants.Utf8NoBom.GetBytes(
                    "{\"command\":\"validate-spec\",\"ok\":true,\"result\":{\"familyDecodedGeometryBytes\":255048,\"familyId\":\"CAL-STONEWOOD-V1\",\"moduleCount\":3,\"profile\":\"calibration-v1\",\"renderPrimitiveCount\":18,\"specPath\":\"assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json\",\"specSha256\":\"39faae34c4cd515cb724a8ef1e2e4bee159a232136218fbb8afd8edd52db2cf8\"},\"schemaVersion\":1}\n"
                )

            assertTrue (stdoutBytes = expected) "Calibration wrapper output is not the canonical closed envelope."
