module PackageTests

open System
open System.Diagnostics
open System.IO
open System.Security.Cryptography
open System.Text.Json

open Riftward.App
open Riftward.App.Package
open Riftward.Platform

// ---------------------------------------------------------------------------
// T-038: kleinster Single-Platform-Releasepfad (Paketvertrag
// docs/PAKETVERTRAG.md V1). Jede Pruefung bindet Codec, Composer, Archiv,
// Verifikator und Befehlsvertrag gegeneinander; keine Pruefung antwortet auf
// eine offene Produktfrage und keine veraendert Riftward.Simulation.
// ---------------------------------------------------------------------------

let private repositoryRoot =
    let rec findRoot path =
        if File.Exists(Path.Combine(path, "Riftward.slnx")) then
            path
        else
            match Directory.GetParent(path) with
            | null -> failwith "Repository-Wurzel nicht gefunden."
            | parent -> findRoot parent.FullName

    findRoot Environment.CurrentDirectory

let private tempRoot () =
    Path.Combine(Path.GetTempPath(), $"RiftHarness-Package-{Guid.NewGuid():N}")

let private sha256File (path: string) =
    use stream = File.OpenRead(path)
    Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant()

let private runAppHost (arguments: string[]) =
    let startInfo = ProcessStartInfo("dotnet")
    startInfo.UseShellExecute <- false
    startInfo.RedirectStandardOutput <- true
    startInfo.RedirectStandardError <- true

    startInfo.ArgumentList.Add(
        Path.Combine(repositoryRoot, "src", "Riftward.App", "bin", "Release", "net10.0", "Riftward.App.dll")
    )

    for argument in arguments do
        startInfo.ArgumentList.Add(argument)

    use processHandle = Process.Start(startInfo)
    let stdout = processHandle.StandardOutput.ReadToEnd()
    let stderr = processHandle.StandardError.ReadToEnd()
    processHandle.WaitForExit()
    (processHandle.ExitCode, stdout.TrimEnd(), stderr.TrimEnd())

let private violationClasses (verification: PackageDirectoryVerification) =
    verification.Violations
    |> Seq.map (fun violation -> violation.Class)
    |> Seq.toArray

// ---------------------------------------------------------------------------
// Synthetische Eingaben: Publish-Ausgabe, Native-Dist und Artefaktmanifest
// werden hermetisch im Temp-Verzeichnis erzeugt; gitignorierte Runtime-Evidenz
// ist niemals Voraussetzung eines schnellen Gates.
// ---------------------------------------------------------------------------

let private writeText (path: string) (content: string) =
    Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore
    File.WriteAllText(path, content)
    path

let private syntheticPublishDir (root: string) =
    let dir = Path.Combine(root, "publish")
    Directory.CreateDirectory(Path.Combine(dir, "runtimes", "native")) |> ignore

    writeText (Path.Combine(dir, "RiftwardAppStub.dll")) "publish-stub-v1\n"
    |> ignore

    writeText (Path.Combine(dir, "RiftwardApp.deps.json")) "{\"stub\":true}\n"
    |> ignore

    let executable = writeText (Path.Combine(dir, "RiftwardApp")) "elf-stub\n"

    File.SetUnixFileMode(
        executable,
        UnixFileMode.UserRead
        ||| UnixFileMode.UserWrite
        ||| UnixFileMode.UserExecute
        ||| UnixFileMode.GroupRead
        ||| UnixFileMode.OtherRead
    )

    dir

let private syntheticNativeDist (root: string) =
    let dist = Path.Combine(root, "native-dist")
    let lib = Path.Combine(dist, "lib")
    let shaders = Path.Combine(dist, "shaders")
    Directory.CreateDirectory(lib) |> ignore
    Directory.CreateDirectory(shaders) |> ignore

    let soFile = Path.Combine(lib, "libstub.so.0.1.2")
    File.WriteAllText(soFile, String.replicate 512 "x")

    let manifestEntryHash = sha256File soFile
    let bytes = FileInfo(soFile).Length

    File.WriteAllText(Path.Combine(lib, "libriftstub.so"), String.replicate 64 "y")

    let shaderFile = Path.Combine(shaders, "stub.vs.bin")
    File.WriteAllText(shaderFile, "shader-bytes")
    let shaderHash = sha256File shaderFile

    // Loader-Symlinks wie im echten Native-Dist.
    File.CreateSymbolicLink(Path.Combine(lib, "libstub.so.0"), "libstub.so.0.1.2")
    |> ignore

    File.CreateSymbolicLink(Path.Combine(lib, "libstub.so"), "libstub.so.0.1.2")
    |> ignore

    let manifest =
        "{\n"
        + $"  \".ai/runtime/cache/native/dist/lib/libstub.so.0.1.2\": {{\"sha256\": \"{manifestEntryHash}\", \"bytes\": {bytes}}},\n"
        + "  \".ai/runtime/cache/native/dist/lib/libriftstub.so\": {\"sha256\": \""
        + sha256File (Path.Combine(lib, "libriftstub.so"))
        + "\", \"bytes\": 64},\n"
        + "  \".ai/runtime/cache/native/dist/shaders/stub.vs.bin\": {\"sha256\": \""
        + shaderHash
        + "\", \"bytes\": 12}\n"
        + "}\n"

    let manifestPath = writeText (Path.Combine(root, "artifact-hashes.json")) manifest
    (dist, manifestPath, manifestEntryHash, bytes)

let private compositionInput (root: string) (publishDir: string) (nativeDist: string) (nativeManifest: string) =
    let binding = PackageSourceReader.Read(repositoryRoot, root)

    PackageComposer.CompositionInput(
        repositoryRoot,
        publishDir,
        nativeDist,
        nativeManifest,
        binding.CommitSha256,
        binding.TreeSha256,
        PackageDocs.ReadPinCohort(Path.Combine(repositoryRoot, "toolchain.lock.json")),
        PackageDocs.DotnetRuntimeVersion()
    )

// ---------------------------------------------------------------------------
// Codec: kanonische Form, Parse-Roundtrip, unterscheidbare Ablehnungsmatrix.
// ---------------------------------------------------------------------------

let private sampleManifest () =
    PackageManifest(
        PackageHeader(
            PackageContract.PackageId,
            "0.1.0-alpha.12345678",
            PackageContract.SupportedRid,
            PackageContract.RuntimeForm,
            PackageContract.AlphaMarker
        ),
        PackageSourceBinding(String.replicate 40 "a", String.replicate 64 "b", PackageContract.SourceDateEpoch),
        PackageArtifactManifestBinding(
            PackageContract.NativeManifestTargetPath,
            String.replicate 64 "c",
            "2026-08-23-cohort-1"
        ),
        PackageProtection(
            PackageContract.ProtectionKind,
            "native",
            PackageContract.NativeManifestTargetPath,
            PackageContract.ProtectionExitCodes
        ),
        [ PackageEntry(
              "docs/RELEASE_NOTES.md",
              PackageEntryKind.File,
              String.replicate 64 "1",
              Nullable 12L,
              null,
              PackageContract.UnixModeRegular
          )
          PackageEntry(
              "native/lib/libstub.so.0",
              PackageEntryKind.Symlink,
              null,
              Nullable(),
              "libstub.so.0.1.2",
              PackageContract.UnixModeSymlink
          )
          PackageEntry(
              "native/lib/libstub.so.0.1.2",
              PackageEntryKind.File,
              String.replicate 64 "2",
              Nullable 512L,
              null,
              PackageContract.UnixModeExecutable
          ) ]
    )

let private parseFromBytes (bytes: byte[]) =
    let path = Path.Combine(tempRoot (), $"manifest-{Guid.NewGuid():N}.json")
    Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore
    File.WriteAllBytes(path, bytes)
    PackageManifestCodec.Parse(path)

let codecRoundtripIsCanonicalAndStable () =
    let manifest = sampleManifest ()
    let bytes = PackageManifestCodec.Encode(manifest)

    // Keine Whitespace-Variation, kein BOM: identische Eingaben, identische Bytes.
    let bytesAgain = PackageManifestCodec.Encode(manifest)

    if bytes <> bytesAgain then
        failwith "Kanonische Kodierung war nicht stabil."

    let parsed = parseFromBytes bytes

    if
        parsed.Package.Version <> manifest.Package.Version
        || parsed.Source.CommitSha256 <> manifest.Source.CommitSha256
        || parsed.Source.TreeSha256 <> manifest.Source.TreeSha256
        || parsed.ArtifactManifest.Sha256 <> manifest.ArtifactManifest.Sha256
        || parsed.Entries.Count <> manifest.Entries.Count
    then
        failwith "Parse-Roundtrip veraenderte die Manifestwahrheit."

    for (original, decoded) in Seq.zip manifest.Entries parsed.Entries do
        if
            original.Path <> decoded.Path
            || original.Kind <> decoded.Kind
            || original.UnixMode <> decoded.UnixMode
            || original.LinkTarget <> decoded.LinkTarget
            || original.Bytes <> decoded.Bytes
        then
            failwith $"Eintrag {original.Path} wich im Roundtrip ab."

    if PackageManifestCodec.Encode(parsed) <> bytes then
        failwith "Re-Encoding des geparsten Manifests wich ab."

let codecRejectsCorruptionMatrixFailClosed () =
    let manifestBytes = PackageManifestCodec.Encode(sampleManifest ())
    let text = System.Text.Encoding.UTF8.GetString(manifestBytes)

    let expectClass (expected: string) (bytes: byte[]) =
        try
            parseFromBytes bytes |> ignore
            failwith $"Verletzungsklasse {expected} blieb aus."
        with error ->
            match error with
            | :? PackageVerificationException as violation ->
                if violation.ViolationClass <> expected then
                    failwith $"Erwartet {expected}, erhalten {violation.ViolationClass}."
            | _ -> failwith $"Erwartete PackageVerificationException fuer {expected}, erhalten {error.GetType().Name}."

    // Schlechtes JSON.
    expectClass "MANIFEST_MALFORMED" (System.Text.Encoding.UTF8.GetBytes("{nope"))
    // Falsche Vertragskennung.
    expectClass
        "MANIFEST_MALFORMED"
        (System.Text.Encoding.UTF8.GetBytes(text.Replace(PackageContract.ContractId, "riftward-paketvertrag-v9")))
    // Falsche RID.
    expectClass "MANIFEST_MALFORMED" (System.Text.Encoding.UTF8.GetBytes(text.Replace("\"linux-x64\"", "\"win-x64\"")))
    // Ungueltige Eintragshash-Form.
    expectClass
        "MANIFEST_HASH_INVALID"
        (System.Text.Encoding.UTF8.GetBytes(text.Replace(String.replicate 64 "1", "abcd")))
    // Unsortierte Eintraege.
    let unsorted =
        text
            .Replace("\"path\":\"docs/RELEASE_NOTES.md\"", "\"path\":\"zzz-nach-alle.md\"")
            .Replace("\"path\":\"native/lib/libstub.so.0\"", "\"path\":\"docs/RELEASE_NOTES.md\"")

    expectClass "MANIFEST_MALFORMED" (System.Text.Encoding.UTF8.GetBytes(unsorted))
    // Unsicherer Pfad.
    expectClass
        "MANIFEST_MALFORMED"
        (System.Text.Encoding.UTF8.GetBytes(
            text.Replace("\"path\":\"docs/RELEASE_NOTES.md\"", "\"path\":\"../escape.md\"")
        ))

// ---------------------------------------------------------------------------
// Composer + Verifikator: deterministisches Staging, Positivfall und
// unterscheidbare Verletzungsmatrix auf hermetischen Eingaben.
// ---------------------------------------------------------------------------

let private composedStage (root: string) =
    let publishDir = syntheticPublishDir root
    let (nativeDist, nativeManifest, _, _) = syntheticNativeDist root

    let result =
        PackageComposer.Compose(root, compositionInput root publishDir nativeDist nativeManifest)

    result

let composerProducesDeterministicStagingAndManifest () =
    let root1 = tempRoot ()
    let root2 = tempRoot ()

    try
        let result1 = composedStage root1
        let result2 = composedStage root2

        if result1.RootName <> result2.RootName then
            failwith "Zwei Compose-Läufe desselben Baums erzeugten verschiedene Wurzelnamen."

        let manifest1 =
            File.ReadAllText(Path.Combine(result1.StageRoot, PackageContract.ManifestFileName))

        let manifest2 =
            File.ReadAllText(Path.Combine(result2.StageRoot, PackageContract.ManifestFileName))

        if manifest1 <> manifest2 then
            failwith "Paketmanifest war nicht deterministisch."

        if
            result1.ManifestSha256
            <> sha256File (Path.Combine(result1.StageRoot, PackageContract.ManifestFileName))
        then
            failwith "Anker stimmte nicht mit dem Manifesthash überein."

        let anchor =
            File.ReadAllText(Path.Combine(result1.StageRoot, PackageContract.AnchorFileName))

        if not (anchor.Contains(result1.ManifestSha256)) then
            failwith "Ankerdatei bindet nicht den Manifesthash."

        // Einträge strikt aufsteigend sortiert.
        let previous = ref ""

        for entry in result1.Manifest.Entries do
            if (String.CompareOrdinal(!previous, entry.Path) >= 0) then
                failwith $"Manifesteinträge nicht sortiert an {entry.Path}."

            previous := entry.Path

        // Version folgt der Baumbindung: tree8 = erste 8 Hexzeichen des gebundenen
        // Baum-SHA-256 (SHA-256 über den 40-stelligen Git-Baum-Digest).
        if result1.Manifest.Source.TreeSha256.Length <> 64 then
            failwith "Baumbindung besitzt nicht die SHA-256-Form."

        if not (result1.Manifest.Package.Version.EndsWith(result1.Manifest.Source.TreeSha256[..7])) then
            failwith "Version bindet nicht die ersten 8 Hexzeichen des Baum-SHA-256."

        // Release Notes tragen die ehrliche Alpha-Kennzeichnung und Aussagegrenze.
        let notes =
            File.ReadAllText(Path.Combine(result1.StageRoot, PackageContract.ReleaseNotesTargetPath))

        for required in [ PackageContract.AlphaMarker; "kein"; "Q-PRD-001" ] do
            if not (notes.Contains(required)) then
                failwith $"Release Notes erwähnen '{required}' nicht."

        // Lizenzen stammen aus dem Lockfile und benennen die Komponenten.
        let licenses =
            File.ReadAllText(Path.Combine(result1.StageRoot, PackageContract.LicensesTargetPath))

        for componentName in [ "sdl3"; "bgfx"; "bx"; "bimg" ] do
            if not (licenses.Contains(componentName)) then
                failwith $"Attributionsmanifest nennt {componentName} nicht."
    finally
        Directory.Delete(root1, true)
        Directory.Delete(root2, true)

let verifierDistinguishesViolationMatrixFailClosed () =
    let root = tempRoot ()

    try
        let result = composedStage root

        // Positivfall.
        let positive = PackageVerifier.VerifyDirectory(result.StageRoot)

        if not positive.Valid then
            failwith $"Positivfall schlug fehl: {positive.Violations}"

        if
            positive.ArtifactChecks.Count <> 3
            || not (positive.ArtifactChecks |> Seq.forall (fun check -> check.Valid))
        then
            failwith "Die drei synthetischen Artefakte wurden nicht alle freigegeben."

        // Manipulationsmatrix: jede Klasse bleibt unterscheidbar.
        let expectClass (expected: string) (action: unit -> PackageDirectoryVerification) =
            let verification = action ()

            if verification.Valid then
                failwith $"Verletzungsklasse {expected} blieb aus."

            let classes = violationClasses verification

            if not (Array.contains expected classes) then
                let actual = String.Join(" | ", classes)
                failwith $"Erwartet {expected}, erhalten {actual}."

        let mutateStage (mutation: string -> unit) =
            // Frisches Staging je Fall, damit Manipulationen sich nicht überlagern.
            let caseRoot = tempRoot ()
            let caseResult = composedStage caseRoot

            try
                mutation caseResult.StageRoot
                let verification = PackageVerifier.VerifyDirectory(caseResult.StageRoot)

                let classes = violationClasses verification

                Directory.Delete(caseRoot, true)
                classes
            with error ->
                Directory.Delete(caseRoot, true)
                reraise ()

        // ENTRY_HASH_MISMATCH: Inhaltsmanipulation mit gleicher Bytezahl.
        let hashCase =
            mutateStage (fun stage ->
                let path = Path.Combine(stage, "native", "lib", "libriftstub.so")
                File.WriteAllText(path, String.replicate 64 "z"))

        if not (Array.contains "ENTRY_HASH_MISMATCH" hashCase) then
            failwith "Inhaltsmanipulation ergab nicht ENTRY_HASH_MISMATCH."

        // ENTRY_INCOMPLETE: Verkürzung.
        let incompleteCase =
            mutateStage (fun stage ->
                let path = Path.Combine(stage, "native", "lib", "libriftstub.so")
                File.WriteAllText(path, "x"))

        if not (Array.contains "ENTRY_INCOMPLETE" incompleteCase) then
            failwith "Verkürzung ergab nicht ENTRY_INCOMPLETE."

        // ENTRY_MISSING.
        let missingCase =
            mutateStage (fun stage -> File.Delete(Path.Combine(stage, "native", "lib", "libriftstub.so")))

        if not (Array.contains "ENTRY_MISSING" missingCase) then
            failwith "Löschung ergab nicht ENTRY_MISSING."

        // UNMANIFESTED_FILE.
        let extraCase =
            mutateStage (fun stage -> File.WriteAllText(Path.Combine(stage, "schmuggel.txt"), "extra"))

        if not (Array.contains "UNMANIFESTED_FILE" extraCase) then
            failwith "Zusätzliche Datei ergab nicht UNMANIFESTED_FILE."

        // ANCHOR_MISMATCH.
        let anchorCase =
            mutateStage (fun stage ->
                File.WriteAllText(
                    Path.Combine(stage, PackageContract.AnchorFileName),
                    String.replicate 64 "0" + "  package-manifest.json\n"
                ))

        if not (Array.contains "ANCHOR_MISMATCH" anchorCase) then
            failwith "Falscher Anker ergab nicht ANCHOR_MISMATCH."

        // ANCHOR_MISSING.
        let anchorMissingCase =
            mutateStage (fun stage -> File.Delete(Path.Combine(stage, PackageContract.AnchorFileName)))

        if not (Array.contains "ANCHOR_MISSING" anchorMissingCase) then
            failwith "Fehlender Anker ergab nicht ANCHOR_MISSING."

        // ENTRY_SYMLINK_MISMATCH.
        let symlinkCase =
            mutateStage (fun stage ->
                let link = Path.Combine(stage, "native", "lib", "libstub.so.0")
                File.Delete(link)
                File.CreateSymbolicLink(link, "libriftstub.so") |> ignore)

        if not (Array.contains "ENTRY_SYMLINK_MISMATCH" symlinkCase) then
            failwith "Falsches Symlinkziel ergab nicht ENTRY_SYMLINK_MISMATCH."

        // ARTIFACT_MANIFEST_REJECTED: Manipulation eines manifestierten Artefakts
        // wird durch die bestehende Host-Artefaktprüfung abgewiesen.
        let artifactCase =
            mutateStage (fun stage ->
                let path = Path.Combine(stage, "native", "lib", "libstub.so.0.1.2")
                File.WriteAllText(path, "manipuliert"))

        if not (Array.contains "ARTIFACT_MANIFEST_REJECTED" artifactCase) then
            failwith "Artefaktmanipulation ergab nicht ARTIFACT_MANIFEST_REJECTED."
    finally
        if Directory.Exists(root) then
            Directory.Delete(root, true)

// ---------------------------------------------------------------------------
// Archiv: deterministische tar.gz-Erzeugung, Sidecar- und Entpackpfad.
// ---------------------------------------------------------------------------

let archiveWriteIsDeterministicAndVerifiable () =
    let root = tempRoot ()

    try
        let result = composedStage root
        let archive1 = Path.Combine(root, "a1.tar.gz")
        let archive2 = Path.Combine(root, "a2.tar.gz")

        let sha1 = PackageArchive.Write(result.StageRoot, result.RootName, archive1)
        let sha2 = PackageArchive.Write(result.StageRoot, result.RootName, archive2)

        if sha1 <> sha2 then
            failwith "Zwei Archivierungen desselben Stagings waren nicht byteidentisch."

        if sha1 <> sha256File archive1 then
            failwith "Rückgegebener Archivhash wich von den Bytes ab."

        // Entpackt identisch: Manifesthash und Anker bleiben gleich.
        let extractDir = Path.Combine(root, "extract")
        PackageArchive.Extract(archive1, extractDir)
        let extractedRoot = Directory.GetDirectories(extractDir) |> Seq.exactlyOne

        if
            File.ReadAllText(Path.Combine(extractedRoot, PackageContract.AnchorFileName))
            <> File.ReadAllText(Path.Combine(result.StageRoot, PackageContract.AnchorFileName))
        then
            failwith "Entpackter Anker wich vom Staging ab."

        let verification = PackageVerifier.VerifyDirectory(extractedRoot)

        if not verification.Valid then
            failwith $"Entpacktes Archiv verifizierte nicht: {verification.Violations}"
    finally
        if Directory.Exists(root) then
            Directory.Delete(root, true)

// ---------------------------------------------------------------------------
// Befehlsvertrag: Usage-Ablehnung, Verifikationsnegativfälle, Frischverzeichnis.
// ---------------------------------------------------------------------------

let cliUsageRejectionsStayControlled () =
    let root = tempRoot ()

    try
        // Unbekannte Option → 2.
        let (exitCode1, _, _) = runAppHost [| "package"; "--bogus" |]

        if exitCode1 <> 2 then
            failwith $"Unbekannte Option ergab {exitCode1} statt 2."

        // Unbekannte RID → 2.
        let (exitCode2, _, _) = runAppHost [| "package"; "--rid"; "win-x64" |]

        if exitCode2 <> 2 then
            failwith $"Unbekannte RID ergab {exitCode2} statt 2."

        // --verify schließt --output-dir aus → 2.
        let (exitCode3, _, _) =
            runAppHost [| "package"; "--verify"; "x.tar.gz"; "--output-dir"; "y" |]

        if exitCode3 <> 2 then
            failwith $"Kombinierte Flags ergaben {exitCode3} statt 2."

        // Fehlendes Archiv → 40 mit maschinenlesbarem Grund.
        let (exitCode4, stdout4, _) =
            runAppHost [| "package"; "--verify"; Path.Combine(root, "fehlt.tar.gz") |]

        if exitCode4 <> 40 then
            failwith $"Fehlendes Archiv ergab {exitCode4} statt 40."

        if not (stdout4.Contains("fehlt.tar.gz")) then
            failwith "Verifikationsreport bindet den Archivpfad nicht."
    finally
        if Directory.Exists(root) then
            Directory.Delete(root, true)

let cliVerifyRejectsManipulatedArchiveWithDistinguishableClass () =
    // Der Fall benötigt ein echtes Paket; ohne Native-Dist kontrolliert 39
    // (dokumentierte Voraussetzung, kein stiller Skip, Präzedenz T-032).
    let nativeManifest =
        Path.Combine(repositoryRoot, PackageContract.NativeManifestSourcePath)

    if not (File.Exists(nativeManifest)) then
        let root = tempRoot ()

        try
            let (exitCode, _, _) = runAppHost [| "package"; "--output-dir"; root |]

            if exitCode <> 39 then
                failwith $"Ohne Native-Dist ergab der Bau {exitCode} statt 39 (kontrollierte Ablehnung)."
        finally
            if Directory.Exists(root) then
                Directory.Delete(root, true)
    else
        let root = tempRoot ()
        let outputDir = Path.Combine(root, "out")

        try
            let (buildExit, buildStdout, _) =
                runAppHost [| "package"; "--output-dir"; outputDir; "--work"; Path.Combine(root, "work") |]

            if buildExit <> 0 then
                failwith $"Paketbau schlug fehl (Exit {buildExit}): {buildStdout}"

            let report = JsonDocument.Parse(buildStdout).RootElement
            let archive = report.GetProperty("archivePath").GetString()

            // Positiv: verify akzeptiert das frisch gebaute Paket.
            let (verifyExit, _, _) = runAppHost [| "package"; "--verify"; archive |]

            if verifyExit <> 0 then
                failwith $"--verify schlug am frisch gebauten Paket fehl (Exit {verifyExit})."

            // Negativ: manipuliertes Sidecar → SIDE_CAR_MISMATCH (die Archivbytes
            // selbst sind gzip-komprimiert und werden am Archivhash gebunden).
            File.WriteAllText(archive + ".sha256", String.replicate 64 "0" + "  " + Path.GetFileName(archive) + "\n")

            let (tamperedExit, tamperedStdout, _) =
                runAppHost [| "package"; "--verify"; archive |]

            if tamperedExit <> 40 then
                failwith $"Manipuliertes Paket ergab {tamperedExit} statt 40."

            if not (tamperedStdout.Contains("SIDE_CAR_MISMATCH")) then
                failwith "Manipuliertes Archiv ergab nicht SIDE_CAR_MISMATCH."

            // Negativ: fehlendes Sidecar → SIDE_CAR_MISSING.
            File.Delete(archive + ".sha256")

            let (missingExit, missingStdout, _) =
                runAppHost [| "package"; "--verify"; archive |]

            if missingExit <> 40 || not (missingStdout.Contains("SIDE_CAR_MISSING")) then
                failwith $"Fehlendes Sidecar ergab {missingExit} ohne SIDE_CAR_MISSING."
        finally
            if Directory.Exists(root) then
                Directory.Delete(root, true)

let cliVerifyRejectsUnreadableArchiveWithDistinguishableClass () =
    // Korrupte Archivbytes mit konsistent gefaschtem Sidecar (z. B. nach einem
    // Rekompressions- oder Rehash-Unfall) bleiben ein kontrollierter Befund:
    // Exit 40 mit Pruefreport und Klasse ARCHIVE_UNREADABLE, niemals ein
    // unkontrollierter Prozessabbruch.
    let root = tempRoot ()

    try
        let archive = Path.Combine(root, "kein-gzip.tar.gz")
        File.WriteAllText(archive, "not-a-gzip-archive-at-all\n")

        let hash =
            use stream = File.OpenRead(archive)
            Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant()

        File.WriteAllText(archive + ".sha256", hash + "  kein-gzip.tar.gz\n")

        let (exitCode, stdout, _) = runAppHost [| "package"; "--verify"; archive |]

        if exitCode <> 40 then
            failwith $"Unlesbares Archiv ergab {exitCode} statt 40."

        if not (stdout.Contains("ARCHIVE_UNREADABLE")) then
            failwith "Unlesbares Archiv ergab nicht ARCHIVE_UNREADABLE."
    finally
        if Directory.Exists(root) then
            Directory.Delete(root, true)

// ---------------------------------------------------------------------------
// Exitcode- und Doku-Bindung.
// ---------------------------------------------------------------------------

let exitCodeMappingBindsPackageCodes () =
    if ExitCodes.Map(PlatformErrorCode.PackageBuildFailed) <> 39 then
        failwith "Paketbau-Exitcode ist nicht 39."

    if ExitCodes.Map(PlatformErrorCode.PackageVerificationFailed) <> 40 then
        failwith "Paketverifikations-Exitcode ist nicht 40."

    let nativeUnterbau =
        File.ReadAllText(Path.Combine(repositoryRoot, "docs", "NATIVE_UNTERBAU.md"))

    for documented in [ "| 39 |"; "| 40 |" ] do
        if not (nativeUnterbau.Contains(documented, StringComparison.Ordinal)) then
            failwith $"docs/NATIVE_UNTERBAU.md dokumentiert {documented} nicht."

    let paketvertrag =
        File.ReadAllText(Path.Combine(repositoryRoot, "docs", "PAKETVERTRAG.md"))

    for required in
        [ PackageContract.ContractId
          PackageContract.AlphaMarker
          PackageContract.RuntimeForm
          "0.1.0-alpha."
          "ENTRY_HASH_MISMATCH"
          "ARTIFACT_MANIFEST_REJECTED"
          "internal-alpha-graybox-v1" ] do
        if not (paketvertrag.Contains(required, StringComparison.Ordinal)) then
            failwith $"Paketvertrag bindet '{required}' nicht."
