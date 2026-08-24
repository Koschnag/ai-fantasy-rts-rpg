module ToolchainCheckTests

open System
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

let repositoryToolchainPassesAllChecks () =
    let report = ToolchainCheck.check repositoryRoot

    if not report.Valid then
        failwith $"Repository-Lockfile ist ungueltig: {ToolchainCheck.reportJson report}"

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
