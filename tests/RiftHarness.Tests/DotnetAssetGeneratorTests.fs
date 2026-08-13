namespace RiftHarness.Tests

open System
open System.Buffers.Binary
open System.IO
open System.IO.Compression
open System.Reflection
open System.Reflection.Emit
open System.Text.Json
open RiftHarness

[<RequireQualifiedAccess>]
module DotnetAssetGeneratorTests =
    [<Literal>]
    let private JobId = "01KZY44M2P2RNSA5XNGM4P9EMY"

    let private stageRelative = $".ai/runtime/asset-jobs/{JobId}/stage/quarantine"

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

    let private expectGenerationFailure code action =
        try
            action ()
            failwith $"Expected generator failure {code}."
        with DotnetAssetGenerationError actual when actual = code ->
            ()

    let private withWorkspace action =
        let root =
            Path.Combine(Path.GetTempPath(), "RiftHarness.DotnetGenerator-" + Guid.NewGuid().ToString("N"))

        Directory.CreateDirectory(root) |> ignore

        try
            action root
        finally
            if Directory.Exists(root) then
                Directory.Delete(root, true)

    let private copyFile root relative =
        let source = Path.Combine(repositoryRoot, relative)
        let target = Path.Combine(root, relative)
        Directory.CreateDirectory(Path.GetDirectoryName(target)) |> ignore
        File.Copy(source, target, false)

    let private validatedWithSeed seed =
        let reference =
            File.ReadAllBytes(Path.Combine(repositoryRoot, "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json"))
            |> BlenderCalibration.parseSpecBytes

        if seed = reference.Spec.Seed then
            reference
        else
            { reference.Spec with Seed = seed }
            |> BlenderCalibration.canonicalSpecBytes
            |> BlenderCalibration.parseSpecBytes

    let private prepare root seed =
        copyFile root "toolchain.lock.json"

        for source in DotnetAssetGenerator.generatorSourcePaths do
            copyFile root source

        Directory.CreateDirectory(Path.Combine(root, $".ai/runtime/asset-jobs/{JobId}/stage"))
        |> ignore

        validatedWithSeed seed

    let private artifactBytes root (artifact: DotnetArtifactInfo) =
        let path = Path.Combine(root, artifact.RelativePath)
        let bytes = File.ReadAllBytes(path)
        assertEqual artifact.Bytes (int64 bytes.Length) "Artifact byte count differs"
        assertEqual artifact.Sha256 (Internal.sha256Hex bytes) "Artifact hash differs"
        bytes

    let private glbJson (bytes: byte array) =
        let jsonLength = int (BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12, 4)))
        let json = bytes.AsSpan(20, jsonLength)
        let mutable length = json.Length

        while length > 0 && json[length - 1] = byte ' ' do
            length <- length - 1

        JsonDocument.Parse(ReadOnlyMemory<byte>(json.Slice(0, length).ToArray()))

    let private pngChunkNames (bytes: byte array) =
        let names = ResizeArray<string>()
        let mutable offset = 8

        while offset < bytes.Length do
            let length = int (BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)))
            names.Add(Constants.Utf8NoBom.GetString(bytes, offset + 4, 4))
            offset <- offset + 12 + length

        assertEqual bytes.Length offset "PNG chunks do not consume the complete file"
        names.ToArray()

    let private idatPayload (bytes: byte array) =
        let mutable offset = 8
        let payloads = ResizeArray<byte array>()

        while offset < bytes.Length do
            let length = int (BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)))
            let kind = Constants.Utf8NoBom.GetString(bytes, offset + 4, 4)

            if kind = "IDAT" then
                payloads.Add(bytes.AsSpan(offset + 8, length).ToArray())

            offset <- offset + 12 + length

        assertEqual 1 payloads.Count "PNG must contain exactly one IDAT chunk"
        payloads[0]

    let private adler32 (bytes: byte array) =
        let mutable a = 1u
        let mutable b = 0u

        for value in bytes do
            a <- (a + uint32 value) % 65521u
            b <- (b + a) % 65521u

        (b <<< 16) ||| a

    let private assertStoredZlib (payload: byte array) (expectedDecoded: byte array) =
        assertTrue (payload.Length > 6) "Stored zlib payload is too short."
        assertEqual 0x78uy payload[0] "zlib CMF differs"
        assertEqual 0x01uy payload[1] "zlib FLG differs"
        let deflateEnd = payload.Length - 4
        let mutable offset = 2
        let mutable decodedBytes = 0
        let mutable finalSeen = false

        while offset < deflateEnd do
            assertTrue (not finalSeen) "Stored zlib has bytes after the final block."
            let header = payload[offset]
            assertTrue (header = 0uy || header = 1uy) "Deflate block is not byte-aligned and stored."
            let final = header = 1uy

            let length =
                int (BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset + 1, 2)))

            let inverse = BinaryPrimitives.ReadUInt16LittleEndian(payload.AsSpan(offset + 3, 2))
            assertEqual (~~~(uint16 length)) inverse "Stored block NLEN differs"
            assertTrue (length > 0 && length <= 65535) "Stored block length is outside its contract."
            offset <- offset + 5
            assertTrue (offset + length <= deflateEnd) "Stored block exceeds IDAT."
            decodedBytes <- decodedBytes + length
            offset <- offset + length
            finalSeen <- final

        assertTrue finalSeen "Stored zlib has no final block."
        assertEqual deflateEnd offset "Stored blocks do not consume the Deflate stream"
        assertEqual expectedDecoded.Length decodedBytes "Stored block decoded length differs"

        assertEqual
            (adler32 expectedDecoded)
            (BinaryPrimitives.ReadUInt32BigEndian(payload.AsSpan(deflateEnd, 4)))
            "Stored zlib Adler-32 differs"

    let private decodedPngScanlines (bytes: byte array) =
        use compressed = new MemoryStream()
        let mutable offset = 8

        while offset < bytes.Length do
            let length = int (BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4)))
            let kind = Constants.Utf8NoBom.GetString(bytes, offset + 4, 4)

            if kind = "IDAT" then
                compressed.Write(bytes, offset + 8, length)

            offset <- offset + 12 + length

        compressed.Position <- 0L
        use zlib = new ZLibStream(compressed, CompressionMode.Decompress)
        use decoded = new MemoryStream()
        zlib.CopyTo(decoded)
        decoded.ToArray()

    let generatedArtifactsPassTheIndependentInspector () =
        withWorkspace (fun root ->
            let validated = prepare root 1592594996u
            let generated = DotnetAssetGenerator.generate root validated stageRelative
            let glb = artifactBytes root generated.Glb
            let png = artifactBytes root generated.Preview
            let reportBytes = artifactBytes root generated.Technique

            assertEqual
                "6dddf5efed35fc29676f22ef4b7d107637506a45dc148ff44453c0627055f178"
                generated.Glb.Sha256
                "Reference GLB golden hash differs"

            assertEqual 270344L generated.Glb.Bytes "Reference GLB byte count differs"

            assertEqual
                "69adc8133c2bb9f5f78035be22c9dca83a7ebe84d18bc35758b370c89ee6fcdd"
                generated.Preview.Sha256
                "Reference preview golden hash differs"

            assertEqual 2074363L generated.Preview.Bytes "Reference preview byte count differs"

            assertEqual
                "6a063317489ccd8a979e4fda28a26b6bd08bb717508fa083fbef96131de305e4"
                generated.Technique.Sha256
                "Reference technique-report golden hash differs"

            assertEqual 3711L generated.Technique.Bytes "Reference report byte count differs"

            assertEqual "CAL-STONEWOOD-V1-39FAAE34C4CD" generated.AssetId "Derived asset ID differs"

            let stage = Path.Combine(root, stageRelative)

            assertEqual
                [| "family.glb"; "preview.png"; "technique.json" |]
                (Directory.GetFiles(stage) |> Array.map Path.GetFileName |> Array.sort)
                "Stage inventory is not closed"

            let inspected =
                Asset3dInspector.inspect
                    root
                    validated
                    generated.Glb.RelativePath
                    generated.Preview.RelativePath
                    generated.Technique.RelativePath

            assertEqual validated.FamilyDecodedGeometryBytes inspected.DecodedGeometryBytes "Geometry proxy differs"
            assertEqual 18 inspected.RenderPrimitiveCount "Primitive count differs"
            assertEqual 2 inspected.MaterialCount "Material count differs"

            use document = glbJson glb

            let jsonLength = int (BinaryPrimitives.ReadUInt32LittleEndian(glb.AsSpan(12, 4)))
            let paddedJson = glb.AsSpan(20, jsonLength)
            let mutable rawJsonLength = paddedJson.Length

            while rawJsonLength > 0 && paddedJson[rawJsonLength - 1] = byte ' ' do
                rawJsonLength <- rawJsonLength - 1

            assertTrue
                (paddedJson
                    .Slice(0, rawJsonLength)
                    .SequenceEqual((Internal.canonicalElement document.RootElement).AsSpan()))
                "GLB JSON is not recursively canonical."

            assertEqual
                DotnetAssetGenerator.GlbGenerator
                (document.RootElement.GetProperty("asset").GetProperty("generator").GetString())
                "GLB generator identity differs"

            assertEqual [| "IHDR"; "IDAT"; "IEND" |] (pngChunkNames png) "PNG chunk inventory differs"

            let scanlines = decodedPngScanlines png
            assertEqual (540 * 3841) scanlines.Length "Decoded RGBA8 scanline size differs"
            assertStoredZlib (idatPayload png) scanlines

            let mutable renderedPixels = 0

            for y = 0 to 539 do
                assertEqual 0uy scanlines[y * 3841] "PNG scanline filter differs"

                for x = 0 to 959 do
                    let pixel = y * 3841 + 1 + x * 4

                    if
                        scanlines[pixel] <> 9uy
                        || scanlines[pixel + 1] <> 9uy
                        || scanlines[pixel + 2] <> 9uy
                    then
                        renderedPixels <- renderedPixels + 1

                    assertEqual 255uy scanlines[pixel + 3] "Preview is not opaque RGBA8"

            assertTrue (renderedPixels > 10000) "CPU rasterizer did not draw a useful module preview."

            let pixelRgba x y =
                let pixel = y * 3841 + 1 + x * 4
                scanlines[pixel .. pixel + 3]

            assertEqual [| 9uy; 9uy; 9uy; 255uy |] (pixelRgba 0 0) "Background golden pixel differs"
            assertEqual [| 96uy; 92uy; 82uy; 255uy |] (pixelRgba 480 270) "Center golden pixel differs"
            assertEqual [| 67uy; 64uy; 57uy; 255uy |] (pixelRgba 480 269) "Edge-adjacent golden pixel differs"

            let expectedTransformations =
                [| "gltf2-direct-write", "81d7fcdea55de043c85ff8494bdb0f484a90e2a1de9b651b654123ba7f9db2c8"
                   "cpu-preview-v1", "c25bac11724a0f293f56460e157bc554e1672e75c04a5919a2b847ecaa30d1ea"
                   "png-encode-v1", "a875004622ac3d9b76fb52b0b32a01a2a7f4e50911e2f2aa86a2dc89418c4a50" |]

            assertEqual
                expectedTransformations.Length
                DotnetAssetGenerator.transformationParameters.Length
                "Transformation parameter count differs"

            for index = 0 to expectedTransformations.Length - 1 do
                let expectedOperation, expectedHash = expectedTransformations[index]
                let actual = DotnetAssetGenerator.transformationParameters[index]
                assertEqual expectedOperation actual.Operation "Transformation order differs"
                assertEqual expectedHash actual.Sha256 "Transformation golden hash differs"
                assertEqual actual.Sha256 (Internal.sha256Hex actual.CanonicalBytes) "Parameter bytes are not bound"

            use report = JsonDocument.Parse(ReadOnlyMemory<byte>(reportBytes))
            let reportRoot = report.RootElement
            let sources = reportRoot.GetProperty("generatorSources")

            assertEqual
                DotnetAssetGenerator.generatorSourcePaths.Length
                (sources.GetArrayLength())
                "Report source count differs"

            for index = 0 to sources.GetArrayLength() - 1 do
                let source = sources[index]
                let relative = DotnetAssetGenerator.generatorSourcePaths[index]
                assertEqual relative (source.GetProperty("path").GetString()) "Report source order differs"

                assertEqual
                    (Internal.sha256File (Path.Combine(root, relative)))
                    (source.GetProperty("sha256").GetString())
                    "Report source hash is not locally bound"

            use sourceBinding = new MemoryStream()

            for relative in DotnetAssetGenerator.generatorSourcePaths do
                let hash = Internal.sha256File (Path.Combine(root, relative))
                let binding = Constants.Utf8NoBom.GetBytes(relative + "\n" + hash + "\n")
                sourceBinding.Write(binding, 0, binding.Length)

            assertEqual
                (sourceBinding.ToArray() |> Internal.sha256Hex)
                (reportRoot.GetProperty("generatorSourceSha256").GetString())
                "Report source aggregate differs"

            assertEqual
                DotnetAssetGenerator.ToolchainPinSha256
                (reportRoot.GetProperty("toolchainPinSha256").GetString())
                "Report SDK pin differs")

    let equalInputsAreByteIdenticalAcrossIsolatedWorkspaces () =
        withWorkspace (fun firstRoot ->
            withWorkspace (fun secondRoot ->
                let firstValidated = prepare firstRoot 1592594996u
                let secondValidated = prepare secondRoot 1592594996u
                let first = DotnetAssetGenerator.generate firstRoot firstValidated stageRelative
                let second = DotnetAssetGenerator.generate secondRoot secondValidated stageRelative

                for left, right in
                    [ first.Glb, second.Glb
                      first.Preview, second.Preview
                      first.Technique, second.Technique ] do
                    assertEqual left.Sha256 right.Sha256 "Isolated artifact hashes differ"

                    assertTrue
                        ((artifactBytes firstRoot left)
                            .AsSpan()
                            .SequenceEqual((artifactBytes secondRoot right).AsSpan()))
                        "Isolated artifact bytes differ"))

    let alternateSeedChangesGeometryAndPreviewButKeepsTheContract () =
        withWorkspace (fun referenceRoot ->
            withWorkspace (fun alternateRoot ->
                let referenceValidated = prepare referenceRoot 1592594996u
                let alternateValidated = prepare alternateRoot 1592594997u

                let reference =
                    DotnetAssetGenerator.generate referenceRoot referenceValidated stageRelative

                let alternate =
                    DotnetAssetGenerator.generate alternateRoot alternateValidated stageRelative

                assertTrue (reference.Glb.Sha256 <> alternate.Glb.Sha256) "Alternate seed did not change GLB."
                assertTrue (reference.Preview.Sha256 <> alternate.Preview.Sha256) "Alternate seed did not change PNG."

                let inspected =
                    Asset3dInspector.inspect
                        alternateRoot
                        alternateValidated
                        alternate.Glb.RelativePath
                        alternate.Preview.RelativePath
                        alternate.Technique.RelativePath

                assertEqual 2 inspected.MaterialCount "Alternate seed changed material contract"

                assertEqual
                    alternateValidated.FamilyDecodedGeometryBytes
                    inspected.DecodedGeometryBytes
                    "Budget changed"))

    let stagePathAndCollisionFailClosed () =
        withWorkspace (fun root ->
            let validated = prepare root 1592594996u

            for invalid in
                [ $".ai/runtime/asset-jobs/{JobId}/stage"
                  $".ai/runtime/asset-jobs/{JobId}/stage/quarantine/extra"
                  $".ai/runtime/asset-jobs/{JobId}/stage/../quarantine"
                  $".ai/runtime/asset-jobs/{JobId.ToLowerInvariant()}/stage/quarantine"
                  "assets/quarantine/3d/output" ] do
                expectGenerationFailure "UNSAFE_PATH" (fun () ->
                    DotnetAssetGenerator.generate root validated invalid |> ignore)

            DotnetAssetGenerator.generate root validated stageRelative |> ignore

            expectGenerationFailure "TRANSACTION_CONFLICT" (fun () ->
                DotnetAssetGenerator.generate root validated stageRelative |> ignore)

            assertTrue
                (Directory.EnumerateDirectories(Path.Combine(root, $".ai/runtime/asset-jobs/{JobId}/stage"))
                 |> Seq.forall (fun path -> Path.GetFileName(path) = "quarantine"))
                "Generator left a temporary directory after the collision.")

    let sourceAndToolchainInputsAreRequiredAndBounded () =
        withWorkspace (fun root ->
            let validated = prepare root 1592594996u
            File.Delete(Path.Combine(root, DotnetAssetGenerator.generatorSourcePaths[0]))

            expectGenerationFailure "UNSAFE_PATH" (fun () ->
                DotnetAssetGenerator.generate root validated stageRelative |> ignore)

            assertTrue (not (Directory.Exists(Path.Combine(root, stageRelative)))) "Missing source created output.")

        withWorkspace (fun root ->
            let validated = prepare root 1592594996u

            File.WriteAllText(
                Path.Combine(root, "toolchain.lock.json"),
                "{\"tools\":[{\"id\":\"dotnet-sdk\",\"version\":\"10.0.999\"}]}",
                Constants.Utf8NoBom
            )

            expectGenerationFailure "PIN_MISMATCH" (fun () ->
                DotnetAssetGenerator.generate root validated stageRelative |> ignore)

            assertTrue (not (Directory.Exists(Path.Combine(root, stageRelative)))) "Bad pin created output.")

    let productionPathHasNoProcessNetworkOrNativeEscapeHatches () =
        let sourcePath =
            Path.Combine(repositoryRoot, "tools/RiftHarness/DotnetAssetGenerator.fs")

        let source = File.ReadAllText(sourcePath, Constants.Utf8NoBom)

        for forbidden in
            [ "System.Diagnostics"
              "System.Net"
              "Socket"
              "NativeLibrary"
              "DllImport"
              "LibraryImport"
              "Assembly.Load"
              "AssemblyLoadContext"
              "ProcessStartInfo"
              "Process.Start"
              "Microsoft.FSharp.Reflection" ] do
            assertTrue
                (not (source.Contains(forbidden, StringComparison.Ordinal)))
                $"Generator source references forbidden capability marker {forbidden}."

        let generatorType =
            typeof<DotnetGeneratedArtifacts>.Assembly.GetType("RiftHarness.DotnetAssetGenerator")

        assertTrue (not (isNull generatorType)) "Compiled generator module type is missing."

        let generateMethod =
            generatorType.GetMethods(BindingFlags.Public ||| BindingFlags.Static)
            |> Array.tryFind (fun methodInfo -> methodInfo.Name = "generate")
            |> Option.defaultWith (fun () -> failwith "Compiled generate method is missing.")

        let opcodeByValue =
            [ for field in typeof<OpCodes>.GetFields(BindingFlags.Public ||| BindingFlags.Static) do
                  let opcode = field.GetValue(null) :?> OpCode
                  yield uint16 opcode.Value, opcode ]
            |> dict

        let forbiddenDeclaringType (declaringType: Type) =
            if isNull declaringType then
                false
            else
                let name = declaringType.FullName

                not (isNull name)
                && (name = "System.Diagnostics.Process"
                    || name = "System.Diagnostics.ProcessStartInfo"
                    || name.StartsWith("System.Net.", StringComparison.Ordinal)
                    || name = "System.Runtime.InteropServices.NativeLibrary"
                    || name = "System.Reflection.Assembly"
                    || name = "System.Runtime.Loader.AssemblyLoadContext")

        let generatorMethods =
            typeof<DotnetGeneratedArtifacts>.Assembly.GetTypes()
            |> Array.filter (fun generatedType ->
                generatedType.FullName = "RiftHarness.DotnetAssetGenerator"
                || generatedType.FullName.StartsWith("RiftHarness.DotnetAssetGenerator+", StringComparison.Ordinal))
            |> Array.collect (fun generatedType ->
                let flags =
                    BindingFlags.Public
                    ||| BindingFlags.NonPublic
                    ||| BindingFlags.Static
                    ||| BindingFlags.Instance
                    ||| BindingFlags.DeclaredOnly

                Array.append
                    (generatedType.GetMethods(flags) |> Array.map (fun value -> value :> MethodBase))
                    (generatedType.GetConstructors(flags)
                     |> Array.map (fun value -> value :> MethodBase)))

        let mutable bodiesInspected = 0

        for methodInfo in generatorMethods do
            let body = methodInfo.GetMethodBody()

            if not (isNull body) then
                bodiesInspected <- bodiesInspected + 1
                let il = body.GetILAsByteArray()
                let mutable offset = 0

                while offset < il.Length do
                    let first = il[offset]
                    offset <- offset + 1

                    let key =
                        if first = 0xFEuy then
                            let second = il[offset]
                            offset <- offset + 1
                            uint16 (0xFE00 ||| int second)
                        else
                            uint16 first

                    let opcode = opcodeByValue[key]
                    let mutable token: int option = None

                    let operandBytes =
                        match opcode.OperandType with
                        | OperandType.InlineNone -> 0
                        | OperandType.ShortInlineBrTarget
                        | OperandType.ShortInlineI
                        | OperandType.ShortInlineVar -> 1
                        | OperandType.InlineVar -> 2
                        | OperandType.InlineI8
                        | OperandType.InlineR -> 8
                        | OperandType.InlineSwitch ->
                            let count = BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(offset, 4))
                            4 + count * 4
                        | OperandType.InlineField
                        | OperandType.InlineI
                        | OperandType.InlineMethod
                        | OperandType.InlineSig
                        | OperandType.InlineString
                        | OperandType.InlineTok
                        | OperandType.InlineType
                        | OperandType.InlineBrTarget
                        | OperandType.ShortInlineR ->
                            if opcode.OperandType = OperandType.InlineMethod then
                                token <- Some(BinaryPrimitives.ReadInt32LittleEndian(il.AsSpan(offset, 4)))

                            4
                        | value -> failwith $"Unsupported IL operand kind {value}."

                    match token with
                    | Some metadataToken ->
                        try
                            let declaringArguments =
                                if isNull methodInfo.DeclaringType || not methodInfo.DeclaringType.IsGenericType then
                                    Array.empty
                                else
                                    methodInfo.DeclaringType.GetGenericArguments()

                            let methodArguments =
                                match methodInfo with
                                | :? MethodInfo as concrete when concrete.IsGenericMethod ->
                                    concrete.GetGenericArguments()
                                | _ -> Array.empty

                            let called =
                                methodInfo.Module.ResolveMethod(metadataToken, declaringArguments, methodArguments)

                            assertTrue
                                (not (forbiddenDeclaringType called.DeclaringType))
                                $"Generator IL calls forbidden API {called.DeclaringType.FullName}.{called.Name}."
                        with
                        | :? ArgumentException
                        | :? BadImageFormatException -> ()
                    | None -> ()

                    offset <- offset + operandBytes

        assertTrue (bodiesInspected > 60) "IL scan did not cover the compiled generator implementation."

        withWorkspace (fun firstRoot ->
            withWorkspace (fun secondRoot ->
                let first =
                    DotnetAssetGenerator.generate firstRoot (prepare firstRoot 1592594996u) stageRelative

                let second =
                    DotnetAssetGenerator.generate secondRoot (prepare secondRoot 1592594996u) stageRelative

                assertEqual first.Glb.Sha256 second.Glb.Sha256 "Temporary paths influenced GLB bytes"
                assertEqual first.Preview.Sha256 second.Preview.Sha256 "Temporary paths influenced PNG bytes"
                assertEqual first.Technique.Sha256 second.Technique.Sha256 "Temporary paths influenced report bytes"))
