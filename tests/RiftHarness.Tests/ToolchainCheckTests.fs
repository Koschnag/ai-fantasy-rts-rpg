module ToolchainCheckTests

open System
open System.Diagnostics
open System.IO
open RiftHarness

let private repositoryRoot =
    let rec findRoot path =
        if File.Exists(Path.Combine(path, "Riftward.slnx")) then
            path
        else
            match Directory.GetParent(path) with
            | null -> failwith "Repository-Wurzel nicht gefunden."
            | parent -> findRoot parent.FullName

    findRoot Environment.CurrentDirectory

/// Legt einen minimalen Pruef-Workspace an: Kopien von Lockfile und Notices,
/// leeres src/-Verzeichnis fuer den ISA-Scan.
let private makeCheckWorkspace (action: string -> unit) =
    let root =
        Path.Combine(Path.GetTempPath(), "RiftToolchainCheck-" + Guid.NewGuid().ToString("N"))

    Directory.CreateDirectory(root) |> ignore
    Directory.CreateDirectory(Path.Combine(root, "src")) |> ignore
    File.Copy(Path.Combine(repositoryRoot, "toolchain.lock.json"), Path.Combine(root, "toolchain.lock.json"))
    File.Copy(Path.Combine(repositoryRoot, "THIRD_PARTY_NOTICES.md"), Path.Combine(root, "THIRD_PARTY_NOTICES.md"))

    try
        action root
    finally
        Directory.Delete(root, true)

let private editLock (root: string) (transform: string -> string) =
    let path = Path.Combine(root, "toolchain.lock.json")
    File.WriteAllText(path, transform (File.ReadAllText(path)))

let private bimgCommit = "371d90098b1fd017cd00205979d5ef74b8c3ed62"

let private bimgPinnedSourceHash =
    "a1464cfbbbbbb1712df9231bb5c5442e3728f78110c7072d5145892e428fd937"

/// Erstellt eine kleine hermetische Cache-Fixture. Der Toolchain-Check prueft
/// nur die Bindung Dateiname/Lock-SHA; die Gueltigkeit echter Upstream-Archive
/// bleibt Vertrag des separaten Native-Builds.
let private writeBimgCacheFixture (root: string) =
    let fixtureCache = Path.Combine(root, ".ai", "runtime", "cache", "native", "src")
    Directory.CreateDirectory(fixtureCache) |> ignore

    let fixturePath = Path.Combine(fixtureCache, $"bimg-{bimgCommit}.tar.gz")

    let fixtureBytes =
        Text.Encoding.UTF8.GetBytes("riftward-toolchain-cache-fixture-v1\n")

    File.WriteAllBytes(fixturePath, fixtureBytes)

    let fixtureHash =
        Security.Cryptography.SHA256.HashData(fixtureBytes)
        |> Convert.ToHexString
        |> fun text -> text.ToLowerInvariant()

    fixturePath, fixtureHash

let private bindBimgFixtureHash (root: string) (fixtureHash: string) =
    editLock root (fun text ->
        if not (text.Contains(bimgPinnedSourceHash, StringComparison.Ordinal)) then
            failwith "Bimg-Quellhash fehlt in der Test-Lockdatei."

        text.Replace(bimgPinnedSourceHash, fixtureHash))

let private assertFinding (codePrefix: string) (report: ToolchainCheck.Report) =
    if report.Valid then
        failwith $"Erwartete Finding '{codePrefix}', aber der Lauf war gueltig."

    let present =
        report.Findings
        |> List.exists (fun finding -> finding.Code.StartsWith(codePrefix, StringComparison.Ordinal))

    if not present then
        let codes = report.Findings |> List.map (fun finding -> finding.Code)
        failwith $"Erwartete Finding '{codePrefix}', erhalten: {codes}"

let private assertDotnetBootstrapAcceptsCorrectReadOnlyLink () =
    if not (OperatingSystem.IsWindows()) then
        let root =
            Path.Combine(Path.GetTempPath(), "RiftDotnetBootstrap-" + Guid.NewGuid().ToString("N"))

        let fakeHome = Path.Combine(root, "home")
        let homeBin = Path.Combine(fakeHome, ".local", "bin")
        let sdkRoot = Path.Combine(root, "sdk")
        let fakeDotnet = Path.Combine(sdkRoot, "dotnet")
        let dotnetLink = Path.Combine(homeBin, "dotnet")
        let fakeBin = Path.Combine(root, "fake-bin")
        let forbiddenLn = Path.Combine(fakeBin, "ln")

        let writableMode =
            UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute

        Directory.CreateDirectory(homeBin) |> ignore
        Directory.CreateDirectory(sdkRoot) |> ignore
        Directory.CreateDirectory(fakeBin) |> ignore
        File.WriteAllText(fakeDotnet, "#!/bin/sh\nprintf '10.0.110\\n'\n")
        File.WriteAllText(forbiddenLn, "#!/bin/sh\nprintf 'ln must not be invoked\\n' >&2\nexit 91\n")
        File.SetUnixFileMode(fakeDotnet, writableMode)
        File.SetUnixFileMode(forbiddenLn, writableMode)
        File.CreateSymbolicLink(dotnetLink, fakeDotnet) |> ignore
        File.SetUnixFileMode(homeBin, UnixFileMode.UserRead ||| UnixFileMode.UserExecute)

        try
            let startInfo = ProcessStartInfo("/bin/sh")
            startInfo.WorkingDirectory <- repositoryRoot
            startInfo.UseShellExecute <- false
            startInfo.RedirectStandardOutput <- true
            startInfo.RedirectStandardError <- true
            startInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "scripts", "bootstrap-dotnet.sh"))
            startInfo.Environment["HOME"] <- fakeHome
            startInfo.Environment["RIFT_DOTNET_DIR"] <- sdkRoot

            startInfo.Environment["PATH"] <-
                fakeBin + string Path.PathSeparator + Environment.GetEnvironmentVariable("PATH")

            use child =
                match Process.Start(startInfo) with
                | null -> failwith "Dotnet-Bootstrap-Testprozess konnte nicht gestartet werden."
                | startedChild -> startedChild

            let stdoutTask = child.StandardOutput.ReadToEndAsync()
            let stderrTask = child.StandardError.ReadToEndAsync()

            if not (child.WaitForExit(30_000)) then
                child.Kill(true)
                failwith "Dotnet-Bootstrap-Testprozess lief in einen Timeout."

            let stdout = stdoutTask.GetAwaiter().GetResult()
            let stderr = stderrTask.GetAwaiter().GetResult()

            if child.ExitCode <> 0 then
                failwith
                    $"Korrekte Read-only-Dotnet-Verknüpfung wurde nicht idempotent akzeptiert: exit={child.ExitCode}; stdout={stdout}; stderr={stderr}"

            if FileInfo(dotnetLink).LinkTarget <> fakeDotnet then
                failwith "Dotnet-Bootstrap veränderte die bereits korrekte Verknüpfung."
        finally
            if Directory.Exists(homeBin) then
                File.SetUnixFileMode(homeBin, writableMode)

            if Directory.Exists(root) then
                Directory.Delete(root, true)

let repositoryToolchainPassesAllChecks () =
    let report = ToolchainCheck.check repositoryRoot

    if not report.Valid then
        failwith $"Repository-Lockfile ist ungueltig: {ToolchainCheck.reportJson report}"

    assertDotnetBootstrapAcceptsCorrectReadOnlyLink ()

let dotnetBootstrapRejectsCollidingPath () =
    if not (OperatingSystem.IsWindows()) then
        let root =
            Path.Combine(Path.GetTempPath(), "RiftDotnetBootstrapCollision-" + Guid.NewGuid().ToString("N"))

        let fakeHome = Path.Combine(root, "home")
        let homeBin = Path.Combine(fakeHome, ".local", "bin")
        let sdkRoot = Path.Combine(root, "sdk")
        let fakeDotnet = Path.Combine(sdkRoot, "dotnet")
        let dotnetCollision = Path.Combine(homeBin, "dotnet")

        let executableMode =
            UnixFileMode.UserRead ||| UnixFileMode.UserWrite ||| UnixFileMode.UserExecute

        Directory.CreateDirectory(homeBin) |> ignore
        Directory.CreateDirectory(sdkRoot) |> ignore
        File.WriteAllText(fakeDotnet, "#!/bin/sh\nprintf '10.0.110\\n'\n")
        File.SetUnixFileMode(fakeDotnet, executableMode)
        File.WriteAllText(dotnetCollision, "user-owned-command\n")

        try
            let startInfo = ProcessStartInfo("/bin/sh")
            startInfo.WorkingDirectory <- repositoryRoot
            startInfo.UseShellExecute <- false
            startInfo.RedirectStandardOutput <- true
            startInfo.RedirectStandardError <- true
            startInfo.ArgumentList.Add(Path.Combine(repositoryRoot, "scripts", "bootstrap-dotnet.sh"))
            startInfo.Environment["HOME"] <- fakeHome
            startInfo.Environment["RIFT_DOTNET_DIR"] <- sdkRoot

            use child =
                match Process.Start(startInfo) with
                | null -> failwith "Dotnet-Bootstrap-Kollisionstest konnte nicht gestartet werden."
                | startedChild -> startedChild

            let stdoutTask = child.StandardOutput.ReadToEndAsync()
            let stderrTask = child.StandardError.ReadToEndAsync()

            if not (child.WaitForExit(30_000)) then
                child.Kill(true)
                failwith "Dotnet-Bootstrap-Kollisionstest lief in einen Timeout."

            let stdout = stdoutTask.GetAwaiter().GetResult()
            let stderr = stderrTask.GetAwaiter().GetResult()

            if child.ExitCode = 0 then
                failwith $"Kollidierende PATH-Datei wurde als Erfolg gemeldet: stdout={stdout}; stderr={stderr}"

            if File.ReadAllText(dotnetCollision) <> "user-owned-command\n" then
                failwith "Dotnet-Bootstrap veränderte die kollidierende PATH-Datei."
        finally
            if Directory.Exists(root) then
                Directory.Delete(root, true)

let tamperedSourceHashIsRejected () =
    // Eine nach ihrer Bindung manipulierte Cache-Fixture muss die
    // Quellhash-Kreuzpruefung failen, auch ohne Entwicklercache.
    makeCheckWorkspace (fun root ->
        let fixturePath, fixtureHash = writeBimgCacheFixture root
        bindBimgFixtureHash root fixtureHash
        File.AppendAllText(fixturePath, "tampered\n")

        let report = ToolchainCheck.check root
        assertFinding "SOURCE_CACHE_MISMATCH_BIMG" report)

let intactCachePassesCrosscheck () =
    makeCheckWorkspace (fun root ->
        let _, fixtureHash = writeBimgCacheFixture root
        bindBimgFixtureHash root fixtureHash

        let report = ToolchainCheck.check root

        if not report.Valid then
            failwith $"Unveraenderte Pins schlugen die Cache-Kreuzpruefung fehl: {ToolchainCheck.reportJson report}")

let missingLicenseEntryIsRejected () =
    makeCheckWorkspace (fun root ->
        editLock root (fun text ->
            text.Replace("\"licenseSpdx\": \"BSD-2-Clause\",", "\"licenseSpdxMissing\": \"BSD-2-Clause\","))

        let report = ToolchainCheck.check root
        assertFinding "LICENSE_SPDX_INVALID" report)

let inconsistentBgfxCohortIsRejected () =
    makeCheckWorkspace (fun root ->
        editLock root (fun text ->
            let parts = text.Split("\"id\": \"bx\"")

            if parts.Length <> 2 then
                failwith "Fixture erwartet genau einen bx-Eintrag im Lockfile."

            parts[0]
            + "\"id\": \"bx\""
            + parts[1]
                .Replace(
                    "\"compatibilityKey\": \"2026-08-23-cohort-1\"",
                    "\"compatibilityKey\": \"2026-08-23-cohort-X\""
                ))

        let report = ToolchainCheck.check root
        assertFinding "BGFX_COHORT_INCONSISTENT" report)

let missingNoticesEntryIsRejected () =
    makeCheckWorkspace (fun root ->
        let noticesPath = Path.Combine(root, "THIRD_PARTY_NOTICES.md")
        let notices = File.ReadAllText(noticesPath)
        File.WriteAllText(noticesPath, notices.Replace("35a98dd6453cf25dc75c68e233abb400836d5920", "<entfernt>"))

        let report = ToolchainCheck.check root
        assertFinding "NOTICES_COMMIT_MISSING" report)

let forbiddenIsaFlagInBuildSourcesIsRejected () =
    makeCheckWorkspace (fun root ->
        let nativeDirectory = Path.Combine(root, "src", "Riftward.Native")
        Directory.CreateDirectory(nativeDirectory) |> ignore
        File.WriteAllText(Path.Combine(nativeDirectory, "evil.cpp"), "// Negativfixture\nint f();\n// -march=native\n")

        let report = ToolchainCheck.check root
        assertFinding "ISA_FLAG_FORBIDDEN" report)

let cleanSourcesPassIsaScan () =
    makeCheckWorkspace (fun root ->
        let nativeDirectory = Path.Combine(root, "src", "Riftward.Native")
        Directory.CreateDirectory(nativeDirectory) |> ignore
        // -msse4.2 entspricht der dokumentierten x86-64-v2-Basis und bleibt zulaessig.
        File.WriteAllText(Path.Combine(nativeDirectory, "ok.cpp"), "int f() { return 1; }\n// Basis: -msse4.2\n")

        let report = ToolchainCheck.check root

        if not report.Valid then
            failwith $"ISA-Basisflag wurde zu Unrecht abgelehnt: {ToolchainCheck.reportJson report}")
