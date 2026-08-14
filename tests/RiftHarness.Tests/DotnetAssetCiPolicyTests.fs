namespace RiftHarness.Tests

open System
open System.IO
open System.Text.RegularExpressions

/// Static, dependency-free policy checks for the isolated T-007 CI boundary.
[<RequireQualifiedAccess>]
module DotnetAssetCiPolicyTests =
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

    let private read relative =
        File.ReadAllText(Path.Combine(repositoryRoot, relative)).Replace("\r\n", "\n")

    let private assertTrue condition message =
        if not condition then
            failwith message

    let private hasLine pattern (text: string) =
        Regex.IsMatch(text, pattern, RegexOptions.Multiline ||| RegexOptions.CultureInvariant)

    let private immutableActions (text: string) =
        Regex.Matches(text, "(?m)^\\s*uses:\\s*[^@\\s]+@([0-9a-f]{40})(?:\\s|#|$)")
        |> Seq.cast<Match>
        |> Seq.length

    let private workflowIsSafe (text: string) =
        let requiredPaths =
            [ ".github/workflows/dotnet-asset-calibration.yml"
              ".ai/tasks/T-003-asset-provenance.json"
              ".ai/tasks/T-005-blender-stonewood-calibration.json"
              ".ai/tasks/T-006-isolated-blender-generation.json"
              ".ai/tasks/T-007-blender-fresh-checkout-ci.json"
              ".ai/policies/asset-clean-room.json"
              ".ai/schemas/asset-*.json"
              ".ai/schemas/generation-receipt.schema.json"
              ".ai/schemas/event.schema.json"
              ".ai/schemas/run-manifest.schema.json"
              ".ai/config.json"
              "assets/specs/3d/**"
              "docs/DOTNET_GENERATOR_CONTRACT.md"
              "docs/ASSET_PIPELINE.md"
              "tools/RiftHarness/**"
              "tests/RiftHarness.Tests/**"
              "scripts/dotnet-asset-calibration-ci.sh"
              "scripts/fresh-checkout-test.sh"
              ".gitignore"
              ".gitattributes"
              "models.lock.json"
              "toolchain.lock.json"
              "tools/RiftHarness/packages.lock.json"
              "tests/RiftHarness.Tests/packages.lock.json" ]

        let forbidden =
            [ "secrets\\."
              "secrets\\["
              "actions/cache"
              "cache:"
              "uses:\\s*docker/"
              "run:.*\\b(?:python|blender|dcc)\\b"
              "bootstrap-blender"
              "rag-"
              "scripts/rift.sh bootstrap" ]

        let hasAllRequiredPaths =
            requiredPaths
            |> List.forall (fun path -> text.Contains(path, StringComparison.Ordinal))

        let hasNoForbiddenCapability =
            forbidden
            |> List.forall (fun token ->
                not (Regex.IsMatch(text, token, RegexOptions.IgnoreCase ||| RegexOptions.CultureInvariant)))

        let jobBlock =
            let marker = "\njobs:\n"
            let start = text.IndexOf(marker, StringComparison.Ordinal)

            if start < 0 then
                ""
            else
                text.Substring(start + marker.Length)

        let jobCount =
            Regex.Matches(jobBlock, "(?m)^  [A-Za-z0-9_-]+:\s*$")
            |> Seq.cast<Match>
            |> Seq.length

        text.Contains("dotnet-asset-calibration-linux-x64:", StringComparison.Ordinal)
        && jobCount = 1
        && text.Contains("runs-on: ubuntu-24.04", StringComparison.Ordinal)
        && hasLine "(?m)^permissions:\\n\\s+contents:\\s+read\\s*$" text
        && text.Contains("cancel-in-progress: true", StringComparison.Ordinal)
        && hasLine "(?m)^\\s*timeout-minutes:\\s*([1-9]|[12][0-9]|30)\\s*$" text
        && hasLine "(?m)^\\s+persist-credentials:\\s*false\\s*$" text
        && immutableActions text = 2
        && text.Contains("actions/upload-artifact@ea165f8d65b6e75b540449e92b4886f43607fa02", StringComparison.Ordinal)
        && text.Contains("retention-days: 7", StringComparison.Ordinal)
        && text.Contains("artifacts/t007/dotnet-asset-calibration.json", StringComparison.Ordinal)
        && text.Contains("artifacts/t007/test.log", StringComparison.Ordinal)
        && hasAllRequiredPaths
        && hasNoForbiddenCapability
        && not (text.Contains("README.md", StringComparison.Ordinal))
        && not (text.Contains("assets/source/", StringComparison.Ordinal))
        && not (text.Contains("docs/BLENDER_GENERATOR_CONTRACT.md", StringComparison.Ordinal))

    let private ciScriptIsSafe (text: string) =
        let required =
            [ "git diff --quiet HEAD --"
              "git diff --cached --quiet HEAD --"
              "git ls-files --others --exclude-standard"
              "git archive --format=tar HEAD"
              "dotnet --version)\" != \"10.0.110"
              "dotnet restore Riftward.slnx --locked-mode"
              "git write-tree"
              "git ls-files --others --exclude-standard"
              "git ls-files | grep -E"
              "git status --porcelain --ignored"
              "<temporary>"
              "<workspace>"
              "artifacts/t007/dotnet-asset-calibration.json"
              "artifacts/t007/test.log" ]

        let forbidden =
            [ "rag-"; "rag_"; " bootstrap"; "blender"; "python"; "curl "; "wget " ]

        let hasAllRequired =
            required
            |> List.forall (fun fragment -> text.Contains(fragment, StringComparison.Ordinal))

        let hasNoForbiddenCapability =
            forbidden
            |> List.forall (fun fragment -> not (text.Contains(fragment, StringComparison.OrdinalIgnoreCase)))

        hasAllRequired
        && hasLine "(?m)-gt\\s+1048576\\b" text
        && hasNoForbiddenCapability

    let private contractIsSafe (text: string) =
        let sources =
            [ "tools/RiftHarness/AssetJobJournal.fs"
              "tools/RiftHarness/BlenderCalibration.fs"
              "tools/RiftHarness/DotnetAssetGenerator.fs" ]

        let sourceBlock =
            "tools/RiftHarness/AssetJobJournal.fs\n"
            + "tools/RiftHarness/BlenderCalibration.fs\n"
            + "tools/RiftHarness/DotnetAssetGenerator.fs"

        let hasAllSources =
            sources
            |> List.forall (fun source -> text.Contains(source, StringComparison.Ordinal))

        text.Contains("Riftward .NET Asset Generator v1", StringComparison.Ordinal)
        && text.Contains("riftward-dotnet-asset-generator", StringComparison.Ordinal)
        && text.Contains("dotnet-sdk:10.0.110", StringComparison.Ordinal)
        && text.Contains("840ca3968e7f20d9e525a2d3a0337e8ba81fad50800942ef299496ae18677d4b", StringComparison.Ordinal)
        && text.Contains(sourceBlock, StringComparison.Ordinal)
        && hasAllSources
        && not (text.Contains("blender-sdk:", StringComparison.OrdinalIgnoreCase))
        && not (text.Contains("blender bootstrap", StringComparison.OrdinalIgnoreCase))

    let private historicalBlenderTextIsContained () =
        let activeFiles =
            [ ".github/workflows/dotnet-asset-calibration.yml"
              "scripts/dotnet-asset-calibration-ci.sh"
              "scripts/fresh-checkout-test.sh" ]

        activeFiles
        |> List.forall (fun relative ->
            let text = read relative
            not (Regex.IsMatch(text, "(?im)^\\s*(?:run:|.*\\|)\\s*.*\\bblender\\b")))

    /// AC-T007-06: the workflow is narrow, least-privileged and immutable.
    let workflowHasOnlyTheDedicatedBoundedLinuxJob () =
        let workflow = read ".github/workflows/dotnet-asset-calibration.yml"
        assertTrue (workflowIsSafe workflow) "T-007 workflow policy is incomplete or unsafe."

        let mutations =
            [ workflow.Replace("ubuntu-24.04", "ubuntu-latest", StringComparison.Ordinal)
              workflow.Replace("timeout-minutes: 30", "timeout-minutes: 31", StringComparison.Ordinal)
              workflow.Replace("persist-credentials: false", "persist-credentials: true", StringComparison.Ordinal)
              workflow.Replace("cancel-in-progress: true", "cancel-in-progress: false", StringComparison.Ordinal)
              workflow.Replace("contents: read", "contents: write", StringComparison.Ordinal)
              workflow.Replace("@3d3c42e5aac5ba805825da76410c181273ba90b1", "@v7", StringComparison.Ordinal)
              workflow.Replace("retention-days: 7", "retention-days: 90", StringComparison.Ordinal)
              workflow + "\n      - run: python tools/escape.py\n"
              workflow
              + "\n      - uses: actions/cache@0123456789012345678901234567890123456789\n"
              workflow + "\n      - run: blender --version\n"
              workflow + "\n      - run: echo ${{ secrets.TOKEN }}\n"
              workflow.Replace("toolchain.lock.json", "README.md", StringComparison.Ordinal) ]

        mutations
        |> List.iter (fun mutation ->
            assertTrue (not (workflowIsSafe mutation)) "A workflow gate mutation escaped detection.")

    /// AC-T007-01/04: fresh checkout, locked restore, bounded logs and leakage checks are mandatory.
    let freshCheckoutShellIsClosedAndSanitizesEvidence () =
        let script = read "scripts/dotnet-asset-calibration-ci.sh"
        assertTrue (ciScriptIsSafe script) "T-007 shell policy is incomplete or unsafe."

        if not (OperatingSystem.IsWindows()) then
            let mode =
                File.GetUnixFileMode(Path.Combine(repositoryRoot, "scripts/dotnet-asset-calibration-ci.sh"))

            assertTrue (mode.HasFlag(UnixFileMode.UserExecute)) "T-007 shell entry point is not executable."

        let mutations =
            [ script.Replace("git archive --format=tar HEAD", "cp -a .", StringComparison.Ordinal)
              script.Replace("--locked-mode", "", StringComparison.Ordinal)
              script.Replace("10.0.110", "10.0.111", StringComparison.Ordinal)
              script.Replace("1048576", "10485760", StringComparison.Ordinal)
              script.Replace("<workspace>", "${rift_ci_root}", StringComparison.Ordinal)
              script.Replace("git write-tree", "git status --short", StringComparison.Ordinal)
              script + "\n./scripts/rift.sh rag-build\n"
              script + "\n./scripts/rift.sh bootstrap\n" ]

        mutations
        |> List.iteri (fun index mutation ->
            assertTrue (not (ciScriptIsSafe mutation)) $"Fresh-checkout shell gate mutation {index} escaped detection.")

    /// AC-T007-05: the active amendment binds exactly the .NET identity, three local sources and pin.
    let activeContractUsesOnlyDotnetIdentitySourcesAndPin () =
        let contract = read "docs/DOTNET_GENERATOR_CONTRACT.md"
        assertTrue (contractIsSafe contract) "Active .NET generator contract is not closed."
        assertTrue (historicalBlenderTextIsContained ()) "Historical Blender wording leaked into an active file."

        let mutations =
            [ contract.Replace("Riftward .NET Asset Generator v1", "Blender Asset Generator", StringComparison.Ordinal)
              contract.Replace("dotnet-sdk:10.0.110", "dotnet-sdk:10.0.111", StringComparison.Ordinal)
              contract.Replace(
                  "tools/RiftHarness/DotnetAssetGenerator.fs",
                  "tools/RiftHarness/ExternalGenerator.py",
                  StringComparison.Ordinal
              )
              contract + "\nblender-sdk:5.2.0\n"
              contract + "\nBlender bootstrap is required.\n" ]

        mutations
        |> List.iter (fun mutation ->
            assertTrue (not (contractIsSafe mutation)) "An active-contract gate mutation escaped detection.")
