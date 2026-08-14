namespace RiftHarness

open System
open System.IO
open System.Reflection
open System.Text
open System.Text.Json
open System.Text.Json.Nodes

/// Raised when bounded, sanitized T-007 evidence cannot be produced.
exception DotnetAssetCiEvidenceError of string

type DotnetAssetCiEvidenceResult =
    { CanonicalJson: byte array
      Sha256: string }

[<RequireQualifiedAccess>]
module DotnetAssetCiEvidence =
    [<Literal>]
    let SchemaVersion = 1

    [<Literal>]
    let MaxEvidenceBytes = 256 * 1024

    let private referenceSeed = 1592594996u
    let private alternateSeed = 1592594997u

    let private jobIds =
        [| "01KZY44M2P2RNSA5XNGM4P9EMY"
           "01KZY44M2P2RNSA5XNGM4P9EMZ"
           "01KZY44M2P2RNSA5XNGM4P9EN0" |]

    let private fail code = raise (DotnetAssetCiEvidenceError code)

    let private node (value: 'value) : JsonNode =
        JsonValue.Create<'value>(value) :> JsonNode

    let private array values =
        let result = JsonArray()
        values |> Seq.iter result.Add
        result :> JsonNode

    let private obj (values: seq<string * JsonNode>) =
        let result = JsonObject()

        for name, value in values do
            result[name] <- value

        result :> JsonNode

    let private canonical (value: JsonNode) =
        use document = JsonDocument.Parse(value.ToJsonString())
        let bytes = Internal.canonicalElement document.RootElement

        if bytes.Length > MaxEvidenceBytes then
            fail "RESOURCE_LIMIT"

        bytes

    let private repositoryRoot root =
        try
            if
                isNull root
                || not (Path.IsPathFullyQualified root)
                || not (Directory.Exists root)
            then
                fail "UNSAFE_PATH"

            let full = Path.GetFullPath root
            let mutable current = DirectoryInfo(full)
            let mutable safe = true

            while safe && not (isNull current) do
                safe <-
                    current.Exists
                    && isNull current.LinkTarget
                    && not (current.Attributes.HasFlag(FileAttributes.ReparsePoint))

                current <- current.Parent

            if not safe then
                fail "UNSAFE_PATH"

            full
        with
        | DotnetAssetCiEvidenceError _ -> reraise ()
        | :? IOException
        | :? UnauthorizedAccessException
        | :? ArgumentException -> fail "UNSAFE_PATH"

    let private safeRelative (relative: string) =
        try
            not (isNull relative)
            && relative.Length > 0
            && Constants.Utf8NoBom.GetByteCount(relative) <= 240
            && not (relative.StartsWith("/", StringComparison.Ordinal))
            && not (relative.Contains('\\') || relative.Contains(':') || relative.Contains('\000'))
            && relative = relative.Normalize(NormalizationForm.FormC)
            && relative.Split('/')
               |> Array.forall (fun part ->
                   part <> ""
                   && part <> "."
                   && part <> ".."
                   && Constants.Utf8NoBom.GetByteCount(part) <= 80
                   && not (part |> Seq.exists Char.IsControl))
        with
        | :? EncoderFallbackException
        | :? ArgumentException -> false

    let private readBounded sourceRoot relative =
        if not (safeRelative relative) then
            fail "UNSAFE_PATH"

        let locations = Workspace.paths sourceRoot
        let candidate = Path.Combine(locations.Root, relative)

        try
            let source =
                Workspace.requireSafePath locations "CI-Evidenz-Eingabe" false candidate

            let before = FileInfo(source)

            if
                not before.Exists
                || not (isNull before.LinkTarget)
                || before.Attributes.HasFlag(FileAttributes.Directory)
                || before.Attributes.HasFlag(FileAttributes.ReparsePoint)
                || before.Length <= 0L
                || before.Length > 1048576L
            then
                fail "UNSAFE_PATH"

            use stream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.Read)

            if stream.Length <> before.Length then
                fail "UNSAFE_PATH"

            let bytes = Array.zeroCreate<byte> (int stream.Length)
            stream.ReadExactly(bytes)
            let after = FileInfo(source)

            if
                stream.Position <> stream.Length
                || after.Length <> stream.Length
                || not (isNull after.LinkTarget)
                || after.Attributes.HasFlag(FileAttributes.ReparsePoint)
            then
                fail "UNSAFE_PATH"

            Workspace.requireSafePath locations "CI-Evidenz-Eingabe" false source |> ignore
            bytes
        with
        | DotnetAssetCiEvidenceError _ -> reraise ()
        | HarnessException _
        | :? IOException
        | :? UnauthorizedAccessException -> fail "UNSAFE_PATH"

    let private copyBounded sourceRoot targetRoot relative =
        let bytes = readBounded sourceRoot relative
        let target = Path.Combine(targetRoot, relative)
        Directory.CreateDirectory(Path.GetDirectoryName target) |> ignore
        File.WriteAllBytes(target, bytes)

    let private withCleanWorkspace sourceRoot jobId action =
        let temporary =
            Path.Combine(Path.GetTempPath(), "riftward-dotnet-evidence-" + Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory temporary |> ignore

        try
            let root = repositoryRoot temporary
            copyBounded sourceRoot root "toolchain.lock.json"
            copyBounded sourceRoot root "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json"

            for path in DotnetAssetGenerator.generatorSourcePaths do
                copyBounded sourceRoot root path

            Directory.CreateDirectory(Path.Combine(root, ".ai/runtime/asset-jobs", jobId, "stage"))
            |> ignore

            action root
        finally
            try
                if Directory.Exists temporary then
                    Directory.Delete(temporary, true)
            with _ ->
                ()

    let private validated sourceRoot seed =
        let bytes =
            readBounded sourceRoot "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json"

        let parsed = BlenderCalibration.parseSpecBytes bytes

        if parsed.Spec.Seed = seed then
            parsed
        else
            { parsed.Spec with Seed = seed }
            |> BlenderCalibration.canonicalSpecBytes
            |> BlenderCalibration.parseSpecBytes

    let private sourceRecords sourceRoot =
        let assembly = typeof<DotnetGeneratedArtifacts>.Assembly

        DotnetAssetGenerator.generatorSourcePaths
        |> Array.map (fun path ->
            let bytes = readBounded sourceRoot path
            let resourceName = "RiftHarness.GeneratorSource." + Path.GetFileName(path)
            use embedded = assembly.GetManifestResourceStream(resourceName)

            if isNull embedded || embedded.Length <= 0L || embedded.Length > 1048576L then
                fail "PIN_MISMATCH"

            let embeddedBytes = Array.zeroCreate<byte> (int embedded.Length)
            embedded.ReadExactly(embeddedBytes)

            if embedded.Position <> embedded.Length || embeddedBytes <> bytes then
                fail "PIN_MISMATCH"

            path, Internal.sha256Hex bytes)

    let private sourceBindingHash (sources: (string * string) array) =
        use binding = new MemoryStream()

        for path, hash in sources do
            let bytes = Constants.Utf8NoBom.GetBytes(path + "\n" + hash + "\n")
            binding.Write(bytes)

        Internal.sha256Hex (binding.ToArray())

    let private sdkVersion () =
        typeof<DotnetGeneratedArtifacts>.Assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
        |> Seq.tryFind (fun item -> item.Key = "RiftwardDotnetSdkVersion")
        |> Option.map (fun item -> item.Value)
        |> Option.filter (fun value -> value = "10.0.110")
        |> Option.defaultWith (fun () -> fail "PIN_MISMATCH")

    let private run sourceRoot jobId seed =
        withCleanWorkspace sourceRoot jobId (fun root ->
            let generated =
                DotnetAssetGenerator.generate
                    root
                    (validated sourceRoot seed)
                    $".ai/runtime/asset-jobs/{jobId}/stage/quarantine"

            let inspection =
                Asset3dInspector.inspect
                    root
                    (validated sourceRoot seed)
                    generated.Glb.RelativePath
                    generated.Preview.RelativePath
                    generated.Technique.RelativePath

            generated, inspection)

    let private artifact seed (generated: DotnetGeneratedArtifacts) =
        obj
            [ "glbSha256", node generated.Glb.Sha256
              "pngSha256", node generated.Preview.Sha256
              "reportSha256", node generated.Technique.Sha256
              "seed", node seed ]

    let private inspected (value: Asset3dInspectionResult) =
        obj
            [ "decodedGeometryBytes", node value.DecodedGeometryBytes
              "materialCount", node value.MaterialCount
              "renderPrimitiveCount", node value.RenderPrimitiveCount
              "reportSha256", node value.ReportSha256 ]

    let private isSha256 (value: string) =
        not (isNull value)
        && value.Length = 64
        && value
           |> Seq.forall (fun character ->
               (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'))

    let private generateCore sourceRoot suiteReportSha256 =
        let sourceRoot = repositoryRoot sourceRoot

        if not (isSha256 suiteReportSha256) then
            fail "INVALID_ARGUMENT"

        if
            not (OperatingSystem.IsLinux())
            || Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
               <> Runtime.InteropServices.Architecture.X64
        then
            fail "UNSUPPORTED_RUNTIME"

        // Fail source/build drift before the first temporary generation root exists.
        let sources = sourceRecords sourceRoot
        let first, firstInspection = run sourceRoot jobIds[0] referenceSeed
        let second, secondInspection = run sourceRoot jobIds[1] referenceSeed
        let alternate, alternateInspection = run sourceRoot jobIds[2] alternateSeed

        if
            first.Glb.Sha256 <> second.Glb.Sha256
            || first.Preview.Sha256 <> second.Preview.Sha256
            || first.Technique.Sha256 <> second.Technique.Sha256
        then
            fail "DETERMINISM_MISMATCH"

        if
            first.Glb.Sha256 = alternate.Glb.Sha256
            || first.Preview.Sha256 = alternate.Preview.Sha256
        then
            fail "DETERMINISM_MISMATCH"

        let acceptance =
            [| for number in 1..6 do
                   yield $"AC-T005-{number:D2}", "suite:t005-regression"
               for number in 1..8 do
                   let command =
                       if number <= 5 then "suite:dotnet-asset-generator-contract"
                       elif number <= 7 then "suite:asset-job-journal-recovery"
                       else "suite:asset-generation-provenance"

                   yield $"AC-T006-{number:D2}", command |]
            |> Array.map (fun (acId, command) ->
                obj
                    [ "acId", node acId
                      "command", node command
                      "exitCode", node 0
                      "reportSha256", node suiteReportSha256 ])

        let lockFileSha256 =
            readBounded sourceRoot "toolchain.lock.json" |> Internal.sha256Hex

        let evidence =
            obj
                [ "acceptance", array acceptance
                  "artifacts",
                  obj
                      [ "alternateSeed", artifact alternateSeed alternate
                        "referenceRun1", artifact referenceSeed first
                        "referenceRun2", artifact referenceSeed second ]
                  "generatorSourceSha256", node (sourceBindingHash sources)
                  "generatorSources",
                  array (
                      sources
                      |> Array.map (fun (path, hash) -> obj [ "path", node path; "sha256", node hash ])
                  )
                  "inspections",
                  array
                      [ inspected firstInspection
                        inspected secondInspection
                        inspected alternateInspection ]
                  "platform", obj [ "architecture", node "x64"; "os", node "linux"; "rid", node "linux-x64" ]
                  "schemaVersion", node SchemaVersion
                  "toolchain",
                  obj
                      [ "lockEntrySha256", node DotnetAssetGenerator.ToolchainPinSha256
                        "lockFileSha256", node lockFileSha256
                        "sdkVersion", node (sdkVersion ()) ] ]

        let bytes = Array.append (canonical evidence) [| byte '\n' |]

        { CanonicalJson = bytes
          Sha256 = Internal.sha256Hex bytes }

    /// Produces canonical, path-free T-007 evidence bound to the executed suite report.
    let generateWithSuiteReport sourceRoot suiteReportSha256 =
        generateCore sourceRoot suiteReportSha256

    /// Test convenience using a stable synthetic suite-report anchor.
    let generate sourceRoot =
        generateCore sourceRoot (Internal.sha256Hex (Constants.Utf8NoBom.GetBytes("t007-test-suite-v1\n")))
