namespace RiftHarness

open System
open System.Collections.Generic
open System.IO
open System.Reflection
open System.Text.Json

/// A sanitized, stable failure returned by the transactional .NET asset pipeline.
exception DotnetAssetPipelineError of code: string * message: string * exitCode: int

type DotnetAssetPipelineResult =
    { JobId: string
      RunId: string
      AssetId: string
      SpecPath: string
      SpecSha256: string
      GlbSha256: string
      PreviewSha256: string
      ReportSha256: string
      ReceiptPath: string
      ReceiptSha256: string
      ManifestPath: string
      ManifestSha256: string }

type DotnetAssetPipelineRecoveryResult = { JobId: string; State: string }

[<RequireQualifiedAccess>]
module DotnetAssetPipeline =
    [<Literal>]
    let private GeneratorTool = "riftward-dotnet-asset-generator"

    [<Literal>]
    let private GeneratorVersion = "1"

    [<Literal>]
    let private ToolchainPin = "dotnet-sdk:10.0.110"

    [<Literal>]
    let private ZeroSha256 =
        "0000000000000000000000000000000000000000000000000000000000000000"

    type private InputDescriptor =
        { Id: string
          Path: string
          Sha256: string
          Origin: string
          OriginClass: string
          CreativeInfluence: bool
          License: string
          RightsEvidence: string
          AllowedUse: string }

    type private OutputDescriptor =
        { Path: string
          Sha256: string
          MediaType: string
          Bytes: int64
          Role: string
          Width: int option
          Height: int option }

    type private TransactionDescriptor =
        { StageQuarantine: string
          TargetQuarantine: string
          StageReceipt: string
          TargetReceipt: string
          ReceiptTemporary: string
          StageManifest: string
          TargetManifest: string
          ManifestTemporary: string }

    let private fail code message exitCode =
        raise (DotnetAssetPipelineError(code, message, exitCode))

    let private invalidArgument () =
        fail "INVALID_ARGUMENT" "invalid argument" 2

    let private unsafePath () = fail "UNSAFE_PATH" "unsafe path" 2

    let private invalidSpec () =
        fail "INVALID_SPEC" "spec validation failed" 2

    let private invalidArtifact () =
        fail "INVALID_ARTIFACT" "artifact validation failed" 5

    let private pinMismatch () =
        fail "PIN_MISMATCH" "toolchain pin mismatch" 3

    let private resourceLimit () =
        fail "RESOURCE_LIMIT" "resource limit exceeded" 4

    let private provenanceFailed () =
        fail "PROVENANCE_FAILED" "provenance validation failed" 6

    let private transactionConflict () =
        fail "TRANSACTION_CONFLICT" "transaction conflict" 7

    let private internalError () =
        fail "INTERNAL_ERROR" "internal pipeline error" 8

    let private canonicalBytes (bytes: byte array) =
        use document = JsonDocument.Parse(bytes)
        Internal.canonicalElement document.RootElement

    let private canonicalJson (write: Utf8JsonWriter -> unit) =
        Internal.jsonBytes false write |> canonicalBytes

    let private appendLf (bytes: byte array) = Array.append bytes [| byte '\n' |]

    let private ensureArguments jobId actorId =
        if not (Internal.isRunId jobId) then
            invalidArgument ()

        if
            String.IsNullOrWhiteSpace(actorId)
            || actorId <> actorId.Trim()
            || actorId.Length > 128
            || actorId |> Seq.exists Char.IsControl
        then
            invalidArgument ()

    let private requireProductionSpecPath (relativePath: string) =
        if
            String.IsNullOrWhiteSpace(relativePath)
            || not (relativePath.StartsWith("assets/specs/3d/", StringComparison.Ordinal))
            || relativePath.Contains('\\')
            || relativePath.Split('/')
               |> Array.exists (fun segment -> String.IsNullOrEmpty(segment) || segment = "." || segment = "..")
        then
            unsafePath ()

    let private enforceRuntimeAndBuildBinding locations =
        let runtime = Environment.Version

        if runtime.Major <> 10 || runtime.Minor <> 0 then
            fail "UNSUPPORTED_RUNTIME" "unsupported runtime" 3

        let assembly = Assembly.GetExecutingAssembly()

        let sdkVersion =
            assembly.GetCustomAttributes<AssemblyMetadataAttribute>()
            |> Seq.tryFind (fun attribute -> attribute.Key = "RiftwardDotnetSdkVersion")
            |> Option.map _.Value

        if sdkVersion <> Some "10.0.110" then
            pinMismatch ()

        for relativePath in DotnetAssetGenerator.generatorSourcePaths do
            let sourcePath =
                Workspace.requireSafePath locations "Generatorquelle" false (Path.Combine(locations.Root, relativePath))

            let resourceName = "RiftHarness.GeneratorSource." + Path.GetFileName(relativePath)

            use embedded = assembly.GetManifestResourceStream(resourceName)

            if
                isNull embedded
                || embedded.Length <= 0L
                || embedded.Length > 16L * 1024L * 1024L
            then
                pinMismatch ()

            use buffer = new MemoryStream()
            embedded.CopyTo(buffer)

            if Internal.sha256Hex (buffer.ToArray()) <> Internal.sha256File sourcePath then
                pinMismatch ()

    let private enforceToolchainPin locations =
        let expectedHash =
            try
                let lockPath =
                    Workspace.requireSafePath
                        locations
                        "Toolchain-Lock"
                        false
                        (Path.Combine(locations.Root, "toolchain.lock.json"))

                let attributes = File.GetAttributes(lockPath)
                let info = FileInfo(lockPath)

                if
                    attributes.HasFlag(FileAttributes.Directory)
                    || attributes.HasFlag(FileAttributes.ReparsePoint)
                    || not (isNull info.LinkTarget)
                    || info.Length <= 0L
                    || info.Length > 65_536L
                then
                    unsafePath ()

                use stream =
                    new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.Read)

                let bytes = Array.zeroCreate<byte> (int stream.Length)
                stream.ReadExactly(bytes)

                if
                    stream.Length <> int64 bytes.Length
                    || FileInfo(lockPath).Length <> int64 bytes.Length
                then
                    unsafePath ()

                use document = JsonDocument.Parse(bytes)
                let tools = document.RootElement.GetProperty("tools")

                let dotnet =
                    tools.EnumerateArray()
                    |> Seq.filter (fun item -> item.GetProperty("id").GetString() = "dotnet-sdk")
                    |> Seq.toArray

                if dotnet.Length <> 1 || dotnet[0].GetProperty("version").GetString() <> "10.0.110" then
                    pinMismatch ()

                let canonical = Array.append (Internal.canonicalElement dotnet[0]) [| byte '\n' |]

                if Internal.sha256Hex canonical <> DotnetAssetGenerator.ToolchainPinSha256 then
                    pinMismatch ()

                Internal.sha256Hex bytes
            with
            | DotnetAssetPipelineError _ -> reraise ()
            | HarnessException _ -> unsafePath ()
            | :? IOException
            | :? UnauthorizedAccessException -> unsafePath ()
            | _ -> pinMismatch ()

        expectedHash

    let private enforceJobResourceLimits locations jobId initial =
        let relativeRoot = $".ai/runtime/asset-jobs/{jobId}"

        let absoluteRoot =
            Workspace.requireSafePath locations "Asset-Jobroot" false (Path.Combine(locations.Root, relativeRoot))

        let mutable fileCount = 0
        let mutable totalBytes = 0L
        let mutable unexpectedInitialEntry = false

        let walk directory =
            let pending = Stack<string>()
            pending.Push(directory)

            while pending.Count > 0 do
                let current = pending.Pop()

                for entry in Directory.EnumerateFileSystemEntries(current) do
                    let attributes = File.GetAttributes(entry)
                    let isDirectory = attributes.HasFlag(FileAttributes.Directory)

                    let linkTarget =
                        if isDirectory then
                            DirectoryInfo(entry).LinkTarget
                        else
                            FileInfo(entry).LinkTarget

                    if attributes.HasFlag(FileAttributes.ReparsePoint) || not (isNull linkTarget) then
                        transactionConflict ()

                    if isDirectory then
                        if initial then
                            unexpectedInitialEntry <- true

                        pending.Push(entry)
                    else
                        fileCount <- fileCount + 1
                        let length = FileInfo(entry).Length

                        if length > 16L * 1024L * 1024L then
                            resourceLimit ()

                        totalBytes <- totalBytes + length

                        if initial && Path.GetFileName(entry) <> ".job.lock" then
                            unexpectedInitialEntry <- true

                        if fileCount > 64 || totalBytes > 24L * 1024L * 1024L then
                            resourceLimit ()

        try
            walk absoluteRoot
        with
        | DotnetAssetPipelineError _ -> reraise ()
        | :? IOException
        | :? UnauthorizedAccessException -> transactionConflict ()

        if initial && unexpectedInitialEntry then
            transactionConflict ()

    let private safeRoot root =
        try
            if String.IsNullOrWhiteSpace(root) then
                unsafePath ()

            let absolute = Path.GetFullPath(root)
            let locations = Workspace.requireInitialized absolute
            let attributes = File.GetAttributes(locations.Root)
            let info = DirectoryInfo(locations.Root)

            if attributes.HasFlag(FileAttributes.ReparsePoint) || not (isNull info.LinkTarget) then
                unsafePath ()

            locations
        with
        | DotnetAssetPipelineError _ -> reraise ()
        | HarnessException _
        | :? IOException
        | :? UnauthorizedAccessException
        | :? NotSupportedException
        | :? ArgumentException -> unsafePath ()

    let private absolutePath (locations: WorkspacePaths) (relative: string) =
        relative.Split('/')
        |> Array.fold (fun current segment -> Path.Combine(current, segment)) locations.Root

    let private ensureDirectory locations relative =
        let path = absolutePath locations relative

        Workspace.requireSafePath locations "Asset-Pipeline-Verzeichnis" true path
        |> ignore

        Directory.CreateDirectory(path) |> ignore

        Workspace.requireSafePath locations "Asset-Pipeline-Verzeichnis" false path
        |> ignore

        path

    let private requireAbsent locations relative =
        let path = absolutePath locations relative
        Workspace.requireSafePath locations "Asset-Publikationsziel" true path |> ignore

        if File.Exists(path) || Directory.Exists(path) then
            transactionConflict ()

    let private durableCreate locations relative bytes =
        let path = absolutePath locations relative
        Workspace.requireSafePath locations "Asset-Stage-Datei" true path |> ignore
        let parent = Path.GetDirectoryName(path)

        if String.IsNullOrEmpty(parent) || not (Directory.Exists(parent)) then
            transactionConflict ()

        try
            use stream =
                new FileStream(
                    path,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    65_536,
                    FileOptions.WriteThrough
                )

            stream.Write(bytes, 0, bytes.Length)
            stream.Flush(true)
        with
        | :? IOException
        | :? UnauthorizedAccessException -> transactionConflict ()

        Workspace.requireSafePath locations "Asset-Stage-Datei" false path |> ignore
        path

    let private tryDeleteOwnedFile locations relative expectedHash =
        try
            let path = absolutePath locations relative

            if File.Exists(path) && not (Directory.Exists(path)) then
                let attributes = File.GetAttributes(path)

                if
                    not (attributes.HasFlag(FileAttributes.ReparsePoint))
                    && isNull (FileInfo(path).LinkTarget)
                    && Internal.sha256File path = expectedHash
                then
                    File.Delete(path)
        with _ ->
            ()

    let private regularFileHash locations relative maximumBytes =
        try
            let path = absolutePath locations relative
            let safe = Workspace.requireSafePath locations "Asset-Pipeline-Eingabe" false path
            let attributes = File.GetAttributes(safe)

            if
                attributes.HasFlag(FileAttributes.Directory)
                || attributes.HasFlag(FileAttributes.ReparsePoint)
                || not (isNull (FileInfo(safe).LinkTarget))
            then
                unsafePath ()

            use stream = new FileStream(safe, FileMode.Open, FileAccess.Read, FileShare.Read)

            if stream.Length <= 0L || stream.Length > maximumBytes then
                unsafePath ()

            let beforeLength = stream.Length

            let hash =
                System.Security.Cryptography.SHA256.HashData(stream) |> Convert.ToHexString

            let finalAttributes = File.GetAttributes(safe)

            if
                stream.Length <> beforeLength
                || finalAttributes.HasFlag(FileAttributes.Directory)
                || finalAttributes.HasFlag(FileAttributes.ReparsePoint)
            then
                unsafePath ()

            hash.ToLowerInvariant()
        with
        | DotnetAssetPipelineError _ -> reraise ()
        | HarnessException _
        | :? IOException
        | :? UnauthorizedAccessException
        | :? NotSupportedException -> unsafePath ()

    let private sourceInventory locations =
        let sources =
            DotnetAssetGenerator.generatorSourcePaths
            |> Array.map (fun path -> path, regularFileHash locations path (16L * 1024L * 1024L))

        use binding = new MemoryStream()

        for path, hash in sources do
            let bytes = Constants.Utf8NoBom.GetBytes(path + "\n" + hash + "\n")
            binding.Write(bytes, 0, bytes.Length)

        sources, Internal.sha256Hex (binding.ToArray())

    let private writeInput (writer: Utf8JsonWriter) input =
        writer.WriteStartObject()
        writer.WriteString("allowedUse", input.AllowedUse)
        writer.WriteBoolean("creativeInfluence", input.CreativeInfluence)
        writer.WriteString("id", input.Id)
        writer.WriteString("license", input.License)
        writer.WriteString("origin", input.Origin)
        writer.WriteString("originClass", input.OriginClass)
        writer.WriteString("path", input.Path)
        writer.WriteBoolean("referenceUseReviewed", true)
        writer.WriteString("rightsEvidence", input.RightsEvidence)
        writer.WriteString("sha256", input.Sha256)
        writer.WriteEndObject()

    let private inputsBytes specPath specHash sources toolchainHash =
        let inputs = ResizeArray<InputDescriptor>()

        inputs.Add(
            { Id = "SPEC-CALIBRATION-V1"
              Path = specPath
              Sha256 = specHash
              Origin = "Project-authored numeric calibration specification."
              OriginClass = "internal-specification"
              CreativeInfluence = true
              License = "Project-owned internal specification."
              RightsEvidence = "Created inside the project without external creative media."
              AllowedUse = "internal-specification" }
        )

        sources
        |> Array.iteri (fun index (path, hash) ->
            inputs.Add(
                { Id = $"GENERATOR-SOURCE-{index + 1}"
                  Path = path
                  Sha256 = hash
                  Origin = "Project-authored F# generator source."
                  OriginClass = "agentic-synthetic"
                  CreativeInfluence = false
                  License = "Repository project license."
                  RightsEvidence = "Created inside the project without external creative media."
                  AllowedUse = "generation-input" }
            ))

        inputs.Add(
            { Id = "TOOLCHAIN-DOTNET-SDK"
              Path = "toolchain.lock.json"
              Sha256 = toolchainHash
              Origin = "Project toolchain lock metadata."
              OriginClass = "technical-nonexpressive"
              CreativeInfluence = false
              License = "Repository technical metadata."
              RightsEvidence = "Versioned project toolchain record."
              AllowedUse = "technical-calibration" }
        )

        canonicalJson (fun writer ->
            writer.WriteStartArray()
            inputs |> Seq.iter (writeInput writer)
            writer.WriteEndArray())

    let private generatorBytes (seed: uint32) (sourceHash: string) =
        canonicalJson (fun writer ->
            writer.WriteStartObject()
            writer.WriteString("executionMode", "local")
            writer.WriteString("generatorSourceSha256", sourceHash)
            writer.WriteString("kind", "procedural")
            writer.WriteNull("model")
            writer.WriteNull("modelArtifactSha256")
            writer.WriteNull("modelVersion")
            writer.WriteNumber("seed", int64 seed)
            writer.WriteString("tool", GeneratorTool)
            writer.WriteString("toolchainPin", ToolchainPin)
            writer.WriteString("version", GeneratorVersion)
            writer.WriteEndObject())

    let private writeOutput (writer: Utf8JsonWriter) (output: OutputDescriptor) =
        writer.WriteStartObject()
        writer.WriteNumber("bytes", output.Bytes)
        writer.WriteString("mediaType", output.MediaType)
        writer.WriteString("path", output.Path)
        writer.WriteString("sha256", output.Sha256)
        writer.WriteStartObject("technicalMetrics")
        writer.WriteString("artifactRole", output.Role)
        output.Height |> Option.iter (fun value -> writer.WriteNumber("height", value))
        output.Width |> Option.iter (fun value -> writer.WriteNumber("width", value))
        writer.WriteEndObject()
        writer.WriteEndObject()

    let private outputsBytes (assetId: string) (generated: DotnetGeneratedArtifacts) =
        let prefix = $"assets/quarantine/3d/{assetId}"

        let outputs =
            [| { Path = prefix + "/family.glb"
                 Sha256 = generated.Glb.Sha256
                 MediaType = "model/gltf-binary"
                 Bytes = generated.Glb.Bytes
                 Role = "geometry-family"
                 Width = None
                 Height = None }
               { Path = prefix + "/preview.png"
                 Sha256 = generated.Preview.Sha256
                 MediaType = "image/png"
                 Bytes = generated.Preview.Bytes
                 Role = "inspection-preview"
                 Width = Some 960
                 Height = Some 540 }
               { Path = prefix + "/technique.json"
                 Sha256 = generated.Technique.Sha256
                 MediaType = "application/json"
                 Bytes = generated.Technique.Bytes
                 Role = "technique-report"
                 Width = None
                 Height = None } |]

        canonicalJson (fun writer ->
            writer.WriteStartArray()
            outputs |> Array.iter (writeOutput writer)
            writer.WriteEndArray())

    let private transformationsBytes (specHash: string) =
        let entries =
            Array.append
                [| "calibration-v1-geometry", specHash |]
                (DotnetAssetGenerator.transformationParameters
                 |> Array.map (fun item -> item.Operation, item.Sha256))

        canonicalJson (fun writer ->
            writer.WriteStartArray()

            for operation, parameterHash in entries do
                writer.WriteStartObject()
                writer.WriteString("operation", operation)
                writer.WriteString("parametersSha256", parameterHash)
                writer.WriteString("tool", GeneratorTool)
                writer.WriteString("version", GeneratorVersion)
                writer.WriteEndObject()

            writer.WriteEndArray())

    let private manifestBytes
        (assetId: string)
        (actorId: string)
        (createdAt: string)
        (runId: string)
        (specHash: string)
        (receiptPath: string)
        (receiptHash: string)
        (generator: byte array)
        (inputs: byte array)
        (outputs: byte array)
        (transformations: byte array)
        =
        canonicalJson (fun writer ->
            writer.WriteStartObject()
            writer.WriteString("$schema", "../../.ai/schemas/asset-manifest.schema.json")
            writer.WriteString("assetId", assetId)
            writer.WriteString("createdAtUtc", createdAt)
            writer.WriteString("createdBy", actorId)
            writer.WriteString("generationBindingMode", "canonical-event-v1")
            writer.WriteString("generationReceipt", receiptPath)
            writer.WriteString("generationReceiptSha256", receiptHash)
            writer.WriteString("generationRunId", runId)
            Internal.rawJson writer "generator" generator
            Internal.rawJson writer "inputs" inputs
            writer.WriteStartObject("licenseBasis")
            writer.WriteBoolean("commercialUseReviewed", false)
            writer.WriteString("inputRights", "Project-authored numeric specification and generator sources only.")
            writer.WriteString("modelTerms", "No model is used by deterministic procedural generation.")
            writer.WriteString("outputPolicy", "Quarantine only; independent reviews are still required.")
            writer.WriteNull("reviewedAtUtc")
            writer.WriteNull("termsEvidenceArtifact")
            writer.WriteEndObject()
            Internal.rawJson writer "outputs" outputs
            writer.WriteNull("prompts")

            writer.WriteString(
                "purpose",
                "Neutral procedural three-module geometry calibration family for technical validation."
            )

            writer.WriteStartArray("reviews")
            writer.WriteEndArray()
            writer.WriteNumber("schemaVersion", 1)
            writer.WriteString("specSha256", specHash)
            writer.WriteString("status", "quarantine")
            writer.WriteStartArray("supersedes")
            writer.WriteEndArray()
            Internal.rawJson writer "transformations" transformations
            writer.WriteEndObject())
        |> appendLf

    let private generationEventBytes
        (actorId: string)
        (assetId: string)
        (specPath: string)
        (specHash: string)
        (generator: byte array)
        (inputs: byte array)
        (outputs: byte array)
        (transformations: byte array)
        =
        canonicalJson (fun writer ->
            writer.WriteStartObject()
            writer.WriteString("actorId", actorId)
            writer.WriteString("assetId", assetId)
            writer.WriteString("generationBindingMode", "canonical-event-v1")
            Internal.rawJson writer "generator" generator
            Internal.rawJson writer "inputs" inputs
            Internal.rawJson writer "outputs" outputs
            writer.WriteString("specPath", specPath)
            writer.WriteString("specSha256", specHash)
            Internal.rawJson writer "transformations" transformations
            writer.WriteEndObject())

    let private verifyReportSources
        (locations: WorkspacePaths)
        (reportRelative: string)
        (expectedSources: (string * string) array)
        (expectedAggregate: string)
        =
        try
            let path = absolutePath locations reportRelative
            let safe = Workspace.requireSafePath locations "Technikreport" false path
            use document = JsonDocument.Parse(File.ReadAllBytes(safe))
            let root = document.RootElement

            let sourceElements =
                root.GetProperty("generatorSources").EnumerateArray() |> Seq.toArray

            if
                sourceElements.Length <> expectedSources.Length
                || root.GetProperty("generatorSourceSha256").GetString() <> expectedAggregate
            then
                invalidArtifact ()

            for index = 0 to expectedSources.Length - 1 do
                let expectedPath, expectedHash = expectedSources[index]
                let actual = sourceElements[index]

                if
                    actual.GetProperty("path").GetString() <> expectedPath
                    || actual.GetProperty("sha256").GetString() <> expectedHash
                then
                    invalidArtifact ()
        with
        | DotnetAssetPipelineError _ -> reraise ()
        | :? JsonException
        | :? KeyNotFoundException
        | :? InvalidOperationException -> invalidArtifact ()

    let private transactionDescriptor jobId assetId runId =
        let targetReceipt = $"assets/receipts/{assetId}/{runId}.json"
        let targetManifest = $"assets/manifests/{assetId}.json"

        { StageQuarantine = $".ai/runtime/asset-jobs/{jobId}/stage/quarantine"
          TargetQuarantine = $"assets/quarantine/3d/{assetId}"
          StageReceipt = $".ai/runtime/asset-jobs/{jobId}/stage/receipt.json"
          TargetReceipt = targetReceipt
          ReceiptTemporary = targetReceipt + "." + jobId + ".tmp"
          StageManifest = $".ai/runtime/asset-jobs/{jobId}/stage/manifest.json"
          TargetManifest = targetManifest
          ManifestTemporary = targetManifest + "." + jobId + ".tmp" }

    let private requirePublicationParents locations assetId =
        for relative in [ "assets/quarantine/3d"; "assets/receipts"; "assets/manifests" ] do
            let path = absolutePath locations relative

            if File.Exists(path) then
                transactionConflict ()

            Workspace.requireSafePath locations "Asset-Publikationswurzel" true path
            |> ignore

            Directory.CreateDirectory(path) |> ignore

            Workspace.requireSafePath locations "Asset-Publikationswurzel" false path
            |> ignore

            if not (Directory.Exists(path)) || File.Exists(path) then
                transactionConflict ()

        let assetReceiptDirectory = $"assets/receipts/{assetId}"
        let path = absolutePath locations assetReceiptDirectory

        Workspace.requireSafePath locations "Asset-Receipt-Verzeichnis" true path
        |> ignore

        if File.Exists(path) then
            transactionConflict ()

        assetReceiptDirectory, Directory.Exists(path)

    let private ensureReceiptDirectory locations relative =
        let path = absolutePath locations relative
        Directory.CreateDirectory(path) |> ignore

        Workspace.requireSafePath locations "Asset-Receipt-Verzeichnis" false path
        |> ignore

    let private tryDeleteOwnedEmptyDirectory locations relative =
        try
            let path = absolutePath locations relative

            if
                Directory.Exists(path)
                && not (File.Exists(path))
                && isNull (DirectoryInfo(path).LinkTarget)
                && not (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                && Directory.EnumerateFileSystemEntries(path) |> Seq.isEmpty
            then
                Directory.Delete(path, false)
        with _ ->
            ()

    let private preflightTargets locations transaction =
        [ transaction.TargetQuarantine
          transaction.TargetReceipt
          transaction.ReceiptTemporary
          transaction.TargetManifest
          transaction.ManifestTemporary ]
        |> List.iter (requireAbsent locations)

    let private finalClaims jobLock quarantineHash receiptFileHash manifestFileHash transaction =
        [ AssetJobJournal.claimOwnedPath
              jobLock
              transaction.TargetQuarantine
              AssetJobOwnedPathKind.OwnedDirectory
              quarantineHash
          AssetJobJournal.claimOwnedPath
              jobLock
              transaction.TargetReceipt
              AssetJobOwnedPathKind.OwnedFile
              receiptFileHash
          AssetJobJournal.claimOwnedPath
              jobLock
              transaction.TargetManifest
              AssetJobOwnedPathKind.OwnedFile
              manifestFileHash ]

    let private classification error =
        match error with
        | DotnetAssetPipelineError _ -> raise error
        | CalibrationSpecError code when code = "UNSAFE_PATH" -> unsafePath ()
        | CalibrationSpecError _ -> invalidSpec ()
        | DotnetAssetGenerationError code when code = "UNSAFE_PATH" -> unsafePath ()
        | DotnetAssetGenerationError code when code = "PIN_MISMATCH" -> pinMismatch ()
        | DotnetAssetGenerationError code when code = "RESOURCE_LIMIT" -> resourceLimit ()
        | DotnetAssetGenerationError code when code = "CANCELLED" -> fail "CANCELLED" "operation cancelled" 4
        | DotnetAssetGenerationError code when code = "TRANSACTION_CONFLICT" -> transactionConflict ()
        | DotnetAssetGenerationError code when code = "INVALID_ARTIFACT" -> invalidArtifact ()
        | AssetInspectionPathError _ -> unsafePath ()
        | AssetInspectionError _ -> invalidArtifact ()
        | AssetJobJournalConflict _ -> transactionConflict ()
        | HarnessException _ -> provenanceFailed ()
        | :? JsonException -> provenanceFailed ()
        | :? IOException
        | :? UnauthorizedAccessException -> transactionConflict ()
        | _ -> internalError ()

    let private appendPayload locations relative bytes action =
        let hash = Internal.sha256Hex bytes
        let path = durableCreate locations relative bytes

        try
            action path
        finally
            tryDeleteOwnedFile locations relative hash

    let private verifyPublishedAsset root manifestPath =
        let local =
            AssetStore.check
                root
                { ManifestPath = Some manifestPath
                  RequireLocal = true
                  RequireApproved = false }

        if not local.Valid then
            provenanceFailed ()

        if local.QuarantineCount <> 1 || local.ApprovedCount <> 0 then
            provenanceFailed ()

    /// Generates, independently inspects and transactionally publishes one quarantine calibration asset.
    let generateWithCancellation root specRelative jobId actorId (cancellationToken: Threading.CancellationToken) =
        ensureArguments jobId actorId
        requireProductionSpecPath specRelative
        let locations = safeRoot root
        enforceRuntimeAndBuildBinding locations
        let initialToolchainHash = enforceToolchainPin locations

        let validated =
            try
                BlenderCalibration.validateSpecFile locations.Root specRelative
            with error ->
                classification error

        let assetId =
            validated.Spec.FamilyId
            + "-"
            + validated.SpecSha256.Substring(0, 12).ToUpperInvariant()

        let mutable runId: string option = None
        let mutable runSucceeded = false
        let mutable journalStarted = false
        let mutable transientFiles: (string * string) list = []
        let mutable createdReceiptDirectory: string option = None

        let trackTransient relative bytes =
            transientFiles <- (relative, Internal.sha256Hex bytes) :: transientFiles

        let untrackTransient relative =
            transientFiles <- transientFiles |> List.filter (fun (path, _) -> path <> relative)

        let mutable lastJournalTime = DateTimeOffset.UtcNow
        let deadline = Environment.TickCount64 + 300_000L

        let checkpoint () =
            if cancellationToken.IsCancellationRequested then
                fail "CANCELLED" "operation cancelled" 4

            if Environment.TickCount64 > deadline then
                resourceLimit ()

        let journalNow () =
            let observed = DateTimeOffset.UtcNow

            let result =
                if observed < lastJournalTime then
                    lastJournalTime
                else
                    observed

            lastJournalTime <- result
            result

        try
            AssetJobJournal.withExclusiveJobLock locations.Root jobId (fun jobLock ->
                checkpoint ()
                enforceJobResourceLimits locations jobId true

                if not (AssetJobJournal.load jobLock |> List.isEmpty) then
                    transactionConflict ()

                let stageRoot = $".ai/runtime/asset-jobs/{jobId}/stage"
                requireAbsent locations stageRoot

                let provisionalTransaction = transactionDescriptor jobId assetId jobId
                requireAbsent locations provisionalTransaction.TargetQuarantine
                requireAbsent locations provisionalTransaction.TargetManifest

                let receiptDirectory, receiptDirectoryExisted =
                    requirePublicationParents locations assetId

                let startedRun = RunStore.startForActor locations.Root actorId
                runId <- Some startedRun
                let transaction = transactionDescriptor jobId assetId startedRun
                preflightTargets locations transaction

                if not receiptDirectoryExisted then
                    ensureReceiptDirectory locations receiptDirectory
                    createdReceiptDirectory <- Some receiptDirectory

                ensureDirectory locations stageRoot |> ignore

                AssetJobJournal.appendTransition
                    jobLock
                    AssetJobState.Created
                    []
                    (journalNow ())
                    AssetJobJournal.noCrash
                |> ignore

                journalStarted <- true

                let generated =
                    DotnetAssetGenerator.generateWithCancellation
                        locations.Root
                        validated
                        transaction.StageQuarantine
                        cancellationToken

                checkpoint ()
                enforceJobResourceLimits locations jobId false

                if generated.AssetId <> assetId then
                    invalidArtifact ()

                let quarantineClaim =
                    AssetJobJournal.hashOwnedPath
                        jobLock
                        transaction.StageQuarantine
                        AssetJobOwnedPathKind.OwnedDirectory

                AssetJobJournal.appendTransition
                    jobLock
                    AssetJobState.Generated
                    [ quarantineClaim ]
                    (journalNow ())
                    AssetJobJournal.noCrash
                |> ignore

                checkpoint ()

                let inspection =
                    Asset3dInspector.inspect
                        locations.Root
                        validated
                        generated.Glb.RelativePath
                        generated.Preview.RelativePath
                        generated.Technique.RelativePath

                if
                    inspection.GlbSha256 <> generated.Glb.Sha256
                    || inspection.PreviewSha256 <> generated.Preview.Sha256
                    || inspection.ReportSha256 <> generated.Technique.Sha256
                then
                    invalidArtifact ()

                AssetJobJournal.appendTransition
                    jobLock
                    AssetJobState.Inspected
                    [ quarantineClaim ]
                    (journalNow ())
                    AssetJobJournal.noCrash
                |> ignore

                let sources, sourceHash = sourceInventory locations
                enforceRuntimeAndBuildBinding locations
                verifyReportSources locations generated.Technique.RelativePath sources sourceHash
                let toolchainHash = regularFileHash locations "toolchain.lock.json" 65_536L

                if
                    toolchainHash <> initialToolchainHash
                    || enforceToolchainPin locations <> initialToolchainHash
                then
                    pinMismatch ()

                let inputs = inputsBytes specRelative validated.SpecSha256 sources toolchainHash
                let generator = generatorBytes validated.Spec.Seed sourceHash
                let outputs = outputsBytes assetId generated
                let transformations = transformationsBytes validated.SpecSha256
                let createdAt = Internal.utcText lastJournalTime

                let eventPayload =
                    generationEventBytes
                        actorId
                        assetId
                        specRelative
                        validated.SpecSha256
                        generator
                        inputs
                        outputs
                        transformations

                let eventRelative = $".ai/runtime/asset-jobs/{jobId}/stage/generation-event.json"

                appendPayload locations eventRelative eventPayload (fun payloadPath ->
                    RunStore.append locations.Root startedRun "asset.generation.completed" payloadPath
                    |> ignore)

                RunStore.finish locations.Root startedRun "succeeded" None |> ignore
                runSucceeded <- true

                let placeholderManifest =
                    manifestBytes
                        assetId
                        actorId
                        createdAt
                        startedRun
                        validated.SpecSha256
                        transaction.TargetReceipt
                        ZeroSha256
                        generator
                        inputs
                        outputs
                        transformations

                let preparationRelative =
                    $".ai/runtime/asset-jobs/{jobId}/stage/manifest.prepare.json"

                trackTransient preparationRelative placeholderManifest
                durableCreate locations preparationRelative placeholderManifest |> ignore

                let prepared =
                    try
                        AssetStore.prepareGenerationReceipt locations.Root startedRun preparationRelative
                    finally
                        tryDeleteOwnedFile locations preparationRelative (Internal.sha256Hex placeholderManifest)
                        untrackTransient preparationRelative

                if
                    prepared.RunId <> startedRun
                    || prepared.AssetId <> assetId
                    || prepared.ReceiptPath <> transaction.TargetReceipt
                then
                    provenanceFailed ()

                let finalManifest =
                    manifestBytes
                        assetId
                        actorId
                        createdAt
                        startedRun
                        validated.SpecSha256
                        transaction.TargetReceipt
                        prepared.ReceiptSha256
                        generator
                        inputs
                        outputs
                        transformations

                trackTransient transaction.StageReceipt prepared.Bytes
                durableCreate locations transaction.StageReceipt prepared.Bytes |> ignore
                trackTransient transaction.StageManifest finalManifest
                durableCreate locations transaction.StageManifest finalManifest |> ignore

                enforceJobResourceLimits locations jobId false

                let stageReceiptClaim =
                    AssetJobJournal.hashOwnedPath jobLock transaction.StageReceipt AssetJobOwnedPathKind.OwnedFile

                let stageManifestClaim =
                    AssetJobJournal.hashOwnedPath jobLock transaction.StageManifest AssetJobOwnedPathKind.OwnedFile

                let targetQuarantineClaim =
                    AssetJobJournal.claimOwnedPath
                        jobLock
                        transaction.TargetQuarantine
                        AssetJobOwnedPathKind.OwnedDirectory
                        quarantineClaim.Sha256

                let targetReceiptClaim =
                    AssetJobJournal.claimOwnedPath
                        jobLock
                        transaction.TargetReceipt
                        AssetJobOwnedPathKind.OwnedFile
                        stageReceiptClaim.Sha256

                let receiptTemporaryClaim =
                    AssetJobJournal.claimOwnedPath
                        jobLock
                        transaction.ReceiptTemporary
                        AssetJobOwnedPathKind.OwnedFile
                        stageReceiptClaim.Sha256

                let targetManifestClaim =
                    AssetJobJournal.claimOwnedPath
                        jobLock
                        transaction.TargetManifest
                        AssetJobOwnedPathKind.OwnedFile
                        stageManifestClaim.Sha256

                let manifestTemporaryClaim =
                    AssetJobJournal.claimOwnedPath
                        jobLock
                        transaction.ManifestTemporary
                        AssetJobOwnedPathKind.OwnedFile
                        stageManifestClaim.Sha256

                let preparedClaims =
                    [ quarantineClaim
                      targetQuarantineClaim
                      stageReceiptClaim
                      targetReceiptClaim
                      receiptTemporaryClaim
                      stageManifestClaim
                      targetManifestClaim
                      manifestTemporaryClaim ]

                AssetJobJournal.appendTransition
                    jobLock
                    AssetJobState.ProvenancePrepared
                    preparedClaims
                    (journalNow ())
                    AssetJobJournal.noCrash
                |> ignore

                untrackTransient transaction.StageReceipt
                untrackTransient transaction.StageManifest

                AssetJobJournal.publishDirectoryByRename
                    jobLock
                    transaction.StageQuarantine
                    transaction.TargetQuarantine
                    quarantineClaim.Sha256
                    AssetJobJournal.noCrash

                checkpoint ()

                let quarantinePublishedClaims =
                    [ targetQuarantineClaim
                      stageReceiptClaim
                      targetReceiptClaim
                      receiptTemporaryClaim
                      stageManifestClaim
                      targetManifestClaim
                      manifestTemporaryClaim ]

                AssetJobJournal.appendTransition
                    jobLock
                    AssetJobState.QuarantinePublished
                    quarantinePublishedClaims
                    (journalNow ())
                    AssetJobJournal.noCrash
                |> ignore

                AssetJobJournal.publishFileAtomically
                    jobLock
                    transaction.StageReceipt
                    transaction.TargetReceipt
                    stageReceiptClaim.Sha256
                    AssetJobJournal.noCrash

                checkpoint ()

                AssetJobJournal.publishFileAtomically
                    jobLock
                    transaction.StageManifest
                    transaction.TargetManifest
                    stageManifestClaim.Sha256
                    AssetJobJournal.noCrash

                checkpoint ()

                let publishedClaims =
                    finalClaims
                        jobLock
                        quarantineClaim.Sha256
                        stageReceiptClaim.Sha256
                        stageManifestClaim.Sha256
                        transaction

                AssetJobJournal.appendTransition
                    jobLock
                    AssetJobState.MetadataPublished
                    publishedClaims
                    (journalNow ())
                    AssetJobJournal.noCrash
                |> ignore

                verifyPublishedAsset locations.Root transaction.TargetManifest
                enforceRuntimeAndBuildBinding locations

                if enforceToolchainPin locations <> initialToolchainHash then
                    pinMismatch ()

                checkpoint ()

                AssetJobJournal.appendTransition
                    jobLock
                    AssetJobState.Verified
                    publishedClaims
                    (journalNow ())
                    AssetJobJournal.noCrash
                |> ignore

                AssetJobJournal.appendTransition
                    jobLock
                    AssetJobState.Committed
                    publishedClaims
                    (journalNow ())
                    AssetJobJournal.noCrash
                |> ignore

                { JobId = jobId
                  RunId = startedRun
                  AssetId = assetId
                  SpecPath = specRelative
                  SpecSha256 = validated.SpecSha256
                  GlbSha256 = generated.Glb.Sha256
                  PreviewSha256 = generated.Preview.Sha256
                  ReportSha256 = generated.Technique.Sha256
                  ReceiptPath = prepared.ReceiptPath
                  ReceiptSha256 = prepared.ReceiptSha256
                  ManifestPath = transaction.TargetManifest
                  ManifestSha256 = stageManifestClaim.Sha256 })
        with error ->
            if not runSucceeded then
                match runId with
                | Some value ->
                    try
                        RunStore.finish locations.Root value "failed" None |> ignore
                    with _ ->
                        ()
                | None -> ()

            let mutable recoveryFailed = false

            if journalStarted then
                try
                    AssetJobJournal.recover locations.Root jobId DateTimeOffset.UtcNow AssetJobJournal.noCrash
                    |> ignore
                with _ ->
                    recoveryFailed <- true

            for relative, hash in transientFiles do
                tryDeleteOwnedFile locations relative hash

            createdReceiptDirectory |> Option.iter (tryDeleteOwnedEmptyDirectory locations)

            if recoveryFailed then
                transactionConflict ()

            classification error

    let generate root specRelative jobId actorId =
        generateWithCancellation root specRelative jobId actorId Threading.CancellationToken.None

    /// Recovers a known job idempotently. Committed jobs are rehashed but never deleted.
    let recover root jobId =
        if not (Internal.isRunId jobId) then
            invalidArgument ()

        let locations = safeRoot root

        try
            let outcome =
                AssetJobJournal.recover locations.Root jobId DateTimeOffset.UtcNow AssetJobJournal.noCrash

            let state =
                match outcome with
                | AssetJobRecoveryOutcome.AlreadyCommitted _ -> "COMMITTED"
                | AssetJobRecoveryOutcome.AlreadyRolledBack _
                | AssetJobRecoveryOutcome.RolledBack _ -> "ROLLED_BACK"

            { JobId = jobId; State = state }
        with error ->
            classification error
