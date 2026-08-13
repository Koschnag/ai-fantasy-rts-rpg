namespace RiftHarness

open System
open System.Buffers.Binary
open System.Collections.Generic
open System.IO
open System.Text
open System.Text.Json
open System.Text.Json.Nodes

/// A stable failure code for the in-process .NET asset generator.
exception DotnetAssetGenerationError of string

type DotnetArtifactInfo =
    { RelativePath: string
      Bytes: int64
      Sha256: string }

type DotnetGeneratedArtifacts =
    { AssetId: string
      Glb: DotnetArtifactInfo
      Preview: DotnetArtifactInfo
      Technique: DotnetArtifactInfo }

type DotnetTransformationParameter =
    { Operation: string
      CanonicalBytes: byte array
      Sha256: string }

[<RequireQualifiedAccess>]
module DotnetAssetGenerator =
    [<Literal>]
    let GlbGenerator = "Riftward .NET Asset Generator v1"

    [<Literal>]
    let ToolchainPinSha256 =
        "840ca3968e7f20d9e525a2d3a0337e8ba81fad50800942ef299496ae18677d4b"

    [<Literal>]
    let private PreviewWidth = 960

    [<Literal>]
    let private PreviewHeight = 540

    [<Literal>]
    let private RasterSubpixels = 256L

    let generatorSourcePaths =
        [| "tools/RiftHarness/AssetJobJournal.fs"
           "tools/RiftHarness/BlenderCalibration.fs"
           "tools/RiftHarness/DotnetAssetGenerator.fs" |]

    let private gltfParameterText =
        """{"accessorOrder":["POSITION","NORMAL","TEXCOORD_0","indices"],"assetGenerator":"Riftward .NET Asset Generator v1","binPaddingByte":0,"boxFaceOrder":["+X","-X","+Y","-Y","+Z","-Z"],"jsonCanonicalization":"ordinal-minimal-utf8-v1","jsonPaddingByte":32,"profile":"gltf2-direct-write-v1","schemaVersion":1}
"""

    let private cpuPreviewParameterText =
        """{"backgroundRgba8":[9,9,9,255],"cameraMicrometres":[10000000,-14000000,9000000],"depth":"int128-round-even-smaller-wins-first-on-tie","faceLightPermille":[620,360,460,700,1000,240],"forward":[-90,150,-76],"height":540,"instancesMicrometres":[[-5000000,0,0],[0,0,0],[5000000,0,0]],"pixelCenterQ8":128,"profile":"cpu-preview-v1","right":[5,3,0],"screenDenominatorX":150000,"screenDenominatorY":10000000,"schemaVersion":1,"shade":"clamp((base*intensity+500)/1000,0,255)","topLeft":"dy-positive-or-horizontal-dx-negative","up":[-114,190,510],"width":960}
"""

    let private pngParameterText =
        """{"adler32":"rfc1950-big-endian","colorType":6,"crc32Polynomial":"edb88320","deflate":"stored-blocks-max-65535","filter":0,"height":540,"idatCount":1,"interlace":0,"profile":"png-encode-v1","schemaVersion":1,"width":960,"zlibHeaderHex":"7801"}
"""

    /// Fixed canonical parameter bytes for the non-spec transformations.
    let transformationParameters =
        [| { Operation = "gltf2-direct-write"
             CanonicalBytes = Constants.Utf8NoBom.GetBytes(gltfParameterText)
             Sha256 = "81d7fcdea55de043c85ff8494bdb0f484a90e2a1de9b651b654123ba7f9db2c8" }
           { Operation = "cpu-preview-v1"
             CanonicalBytes = Constants.Utf8NoBom.GetBytes(cpuPreviewParameterText)
             Sha256 = "c25bac11724a0f293f56460e157bc554e1672e75c04a5919a2b847ecaa30d1ea" }
           { Operation = "png-encode-v1"
             CanonicalBytes = Constants.Utf8NoBom.GetBytes(pngParameterText)
             Sha256 = "a875004622ac3d9b76fb52b0b32a01a2a7f4e50911e2f2aa86a2dc89418c4a50" } |]

    do
        for item in transformationParameters do
            if Internal.sha256Hex item.CanonicalBytes <> item.Sha256 then
                invalidOp $"Transformation parameter literal mismatch: {item.Operation}."

    let private fail code = raise (DotnetAssetGenerationError code)

    let private resourceLimit () = fail "RESOURCE_LIMIT"

    let private ensureDeadline (deadlineTimestamp: int64) =
        if Environment.TickCount64 > deadlineTimestamp then
            resourceLimit ()

    let private mapArtifactFailure action =
        try
            action ()
        with
        | DotnetAssetGenerationError _ -> reraise ()
        | CalibrationSpecError _
        | AssetInspectionError _ -> fail "INVALID_ARTIFACT"
        | AssetInspectionPathError _
        | HarnessException _ -> fail "UNSAFE_PATH"
        | :? JsonException
        | :? InvalidDataException
        | :? OverflowException
        | :? ArgumentException
        | :? IndexOutOfRangeException -> fail "INVALID_ARTIFACT"

    let private node (value: 'value) = JsonValue.Create(value) :> JsonNode

    let private jsonArray (values: seq<JsonNode>) =
        let result = JsonArray()
        values |> Seq.iter (fun value -> result.Add(value))
        result

    let private jsonObject (values: seq<string * JsonNode>) =
        let result = JsonObject()

        for name, value in values do
            result[name] <- value

        result

    let private intArray values = values |> Seq.map node |> jsonArray
    let private int64Array values = values |> Seq.map node |> jsonArray
    let private singleArray values = values |> Seq.map node |> jsonArray

    let private canonicalNodeBytes (value: JsonNode) =
        use document =
            JsonDocument.Parse(value.ToJsonString(JsonSerializerOptions(WriteIndented = false)))

        Internal.canonicalElement document.RootElement

    let private isUlid (value: string) =
        let alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"

        not (isNull value) && value.Length = 26 && value |> Seq.forall alphabet.Contains

    let private validateStageRelative (relative: string) =
        try
            if
                isNull relative
                || relative <> relative.Normalize(NormalizationForm.FormC)
                || relative.StartsWith("/", StringComparison.Ordinal)
                || relative.Contains('\\')
                || relative.Contains(':')
                || Constants.Utf8NoBom.GetByteCount(relative) > 240
            then
                fail "UNSAFE_PATH"

            let actualSegments = relative.Split('/')

            if
                actualSegments.Length <> 6
                || actualSegments[0] <> ".ai"
                || actualSegments[1] <> "runtime"
                || actualSegments[2] <> "asset-jobs"
                || not (isUlid actualSegments[3])
                || actualSegments[4] <> "stage"
                || actualSegments[5] <> "quarantine"
            then
                fail "UNSAFE_PATH"

            if
                actualSegments
                |> Array.exists (fun segment ->
                    String.IsNullOrEmpty(segment)
                    || segment = "."
                    || segment = ".."
                    || Constants.Utf8NoBom.GetByteCount(segment) > 80
                    || segment |> Seq.exists Char.IsControl)
            then
                fail "UNSAFE_PATH"

            relative
        with
        | DotnetAssetGenerationError _ -> reraise ()
        | :? ArgumentException
        | :? EncoderFallbackException -> fail "UNSAFE_PATH"

    let private requireWorkspaceRoot root =
        try
            if isNull root || not (Path.IsPathFullyQualified(root)) then
                fail "UNSAFE_PATH"

            let full = Path.GetFullPath(root)

            if not (Directory.Exists(full)) then
                fail "UNSAFE_PATH"

            let info = DirectoryInfo(full)
            let attributes = File.GetAttributes(full)

            if not (isNull info.LinkTarget) || attributes.HasFlag(FileAttributes.ReparsePoint) then
                fail "UNSAFE_PATH"

            full
        with
        | DotnetAssetGenerationError _ -> reraise ()
        | :? IOException
        | :? UnauthorizedAccessException
        | :? NotSupportedException
        | :? System.Security.SecurityException
        | :? ArgumentException -> fail "UNSAFE_PATH"

    let private readRegularFile root relative maximumBytes =
        try
            let locations = Workspace.paths root
            let candidate = Path.Combine(locations.Root, relative)
            let path = Workspace.requireSafePath locations "Generatorquelle" false candidate
            let attributes = File.GetAttributes(path)

            if
                attributes.HasFlag(FileAttributes.Directory)
                || attributes.HasFlag(FileAttributes.ReparsePoint)
            then
                fail "UNSAFE_PATH"

            use stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)

            if stream.Length <= 0L || stream.Length > int64 maximumBytes then
                fail "UNSAFE_PATH"

            let bytes = Array.zeroCreate<byte> (int stream.Length)
            let mutable offset = 0

            while offset < bytes.Length do
                let count = stream.Read(bytes, offset, bytes.Length - offset)

                if count = 0 then
                    fail "UNSAFE_PATH"

                offset <- offset + count

            if stream.ReadByte() <> -1 then
                fail "UNSAFE_PATH"

            let finalPath =
                Workspace.requireSafePath locations "Generatorquelle" false candidate

            let finalAttributes = File.GetAttributes(finalPath)

            if
                not (String.Equals(path, finalPath, StringComparison.Ordinal))
                || stream.Length <> int64 bytes.Length
                || finalAttributes.HasFlag(FileAttributes.Directory)
                || finalAttributes.HasFlag(FileAttributes.ReparsePoint)
            then
                fail "UNSAFE_PATH"

            bytes
        with
        | DotnetAssetGenerationError _ -> reraise ()
        | HarnessException _
        | :? IOException
        | :? UnauthorizedAccessException
        | :? NotSupportedException
        | :? System.Security.SecurityException -> fail "UNSAFE_PATH"

    let private sourceInventory root =
        use aggregate = new MemoryStream()

        let sources =
            generatorSourcePaths
            |> Array.map (fun path ->
                let hash = readRegularFile root path (16 * 1024 * 1024) |> Internal.sha256Hex
                let binding = Constants.Utf8NoBom.GetBytes(path + "\n" + hash + "\n")
                aggregate.Write(binding)
                path, hash)

        sources, Internal.sha256Hex (aggregate.ToArray())

    let private exactFields (expected: string array) (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then
            fail "INVALID_ARTIFACT"

        let expectedSet = HashSet<string>(expected, StringComparer.Ordinal)
        let seen = HashSet<string>(StringComparer.Ordinal)

        for property in element.EnumerateObject() do
            if not (expectedSet.Contains(property.Name)) || not (seen.Add(property.Name)) then
                fail "INVALID_ARTIFACT"

        if seen.Count <> expectedSet.Count then
            fail "INVALID_ARTIFACT"

    let private stringProperty (name: string) (element: JsonElement) =
        match element.TryGetProperty(name) with
        | true, value when value.ValueKind = JsonValueKind.String && not (isNull (value.GetString())) ->
            value.GetString()
        | _ -> fail "INVALID_ARTIFACT"

    let private pinMismatch () = fail "PIN_MISMATCH"

    let private toolchainPin root =
        let bytes = readRegularFile root "toolchain.lock.json" 65536

        try
            use document =
                JsonDocument.Parse(ReadOnlyMemory<byte>(bytes), JsonDocumentOptions(MaxDepth = 8))

            let rootElement = document.RootElement

            let tools =
                match rootElement.TryGetProperty("tools") with
                | true, value when value.ValueKind = JsonValueKind.Array -> value
                | _ -> pinMismatch ()

            let matches =
                tools.EnumerateArray()
                |> Seq.filter (fun item ->
                    item.ValueKind = JsonValueKind.Object
                    && (match item.TryGetProperty("id") with
                        | true, id when id.ValueKind = JsonValueKind.String -> id.GetString() = "dotnet-sdk"
                        | _ -> false))
                |> Seq.toArray

            if matches.Length <> 1 then
                pinMismatch ()

            let dotnet = matches[0]

            exactFields [| "id"; "install"; "integrity"; "license"; "version" |] dotnet

            if
                stringProperty "id" dotnet <> "dotnet-sdk"
                || stringProperty "install" dotnet <> "scripts/bootstrap-dotnet.sh"
                || stringProperty "integrity" dotnet
                   <> "platform-specific SHA-512 values embedded in bootstrap script"
                || stringProperty "license" dotnet <> "MIT"
                || stringProperty "version" dotnet <> "10.0.110"
            then
                pinMismatch ()

            let canonical = Array.append (Internal.canonicalElement dotnet) [| byte '\n' |]
            let hash = Internal.sha256Hex canonical

            if canonical.Length <> 173 || hash <> ToolchainPinSha256 then
                pinMismatch ()

            hash
        with
        | DotnetAssetGenerationError code when code = "INVALID_ARTIFACT" -> pinMismatch ()
        | DotnetAssetGenerationError _ -> reraise ()
        | :? JsonException
        | :? InvalidOperationException -> pinMismatch ()

    let private cleanSingle (value: float32) =
        if BitConverter.SingleToInt32Bits(value) = Int32.MinValue then
            0.0f
        else
            value

    let private gltfPoint (point: MicrometrePoint) =
        cleanSingle (float32 point.X / 1000000.0f),
        cleanSingle (float32 point.Z / 1000000.0f),
        cleanSingle (float32 -point.Y / 1000000.0f)

    let private gltfVector (x: int, y: int, z: int) =
        cleanSingle (float32 x), cleanSingle (float32 z), cleanSingle (float32 -y)

    let private boxFaces (item: CalibrationBox) =
        let minimum = item.Min
        let maximum = item.Max
        let point x y z = { X = x; Y = y; Z = z }

        [| (1, 0, 0),
           [| point maximum.X minimum.Y minimum.Z
              point maximum.X maximum.Y minimum.Z
              point maximum.X maximum.Y maximum.Z
              point maximum.X minimum.Y maximum.Z |]
           (-1, 0, 0),
           [| point minimum.X maximum.Y minimum.Z
              point minimum.X minimum.Y minimum.Z
              point minimum.X minimum.Y maximum.Z
              point minimum.X maximum.Y maximum.Z |]
           (0, 1, 0),
           [| point minimum.X maximum.Y minimum.Z
              point minimum.X maximum.Y maximum.Z
              point maximum.X maximum.Y maximum.Z
              point maximum.X maximum.Y minimum.Z |]
           (0, -1, 0),
           [| point minimum.X minimum.Y maximum.Z
              point minimum.X minimum.Y minimum.Z
              point maximum.X minimum.Y minimum.Z
              point maximum.X minimum.Y maximum.Z |]
           (0, 0, 1),
           [| point minimum.X minimum.Y maximum.Z
              point maximum.X minimum.Y maximum.Z
              point maximum.X maximum.Y maximum.Z
              point minimum.X maximum.Y maximum.Z |]
           (0, 0, -1),
           [| point minimum.X maximum.Y minimum.Z
              point maximum.X maximum.Y minimum.Z
              point maximum.X minimum.Y minimum.Z
              point minimum.X minimum.Y minimum.Z |] |]

    let private floatBytes (values: float32 array) =
        let bytes = Array.zeroCreate<byte> (values.Length * 4)

        for index = 0 to values.Length - 1 do
            BinaryPrimitives.WriteInt32LittleEndian(
                bytes.AsSpan(index * 4, 4),
                BitConverter.SingleToInt32Bits(values[index])
            )

        bytes

    let private indexBytes (values: int array) =
        let bytes = Array.zeroCreate<byte> (values.Length * 2)

        for index = 0 to values.Length - 1 do
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(index * 2, 2), uint16 values[index])

        bytes

    type private PrimitiveBytes =
        { Positions: byte array
          Normals: byte array
          Uvs: byte array option
          Indices: byte array
          VertexCount: int
          IndexCount: int
          Minimum: float32 array
          Maximum: float32 array }

    let private primitiveBytes checkpoint (boxes: CalibrationBox array) render =
        let positions = ResizeArray<float32>()
        let normals = ResizeArray<float32>()
        let uvs = ResizeArray<float32>()
        let indices = ResizeArray<int>()

        for item in boxes do
            checkpoint ()

            for normal, vertices in boxFaces item do
                checkpoint ()
                let baseIndex = positions.Count / 3
                let nx, ny, nz = gltfVector normal

                for vertexIndex = 0 to 3 do
                    let x, y, z = gltfPoint vertices[vertexIndex]
                    positions.Add(x)
                    positions.Add(y)
                    positions.Add(z)
                    normals.Add(nx)
                    normals.Add(ny)
                    normals.Add(nz)

                    if render then
                        let u, v = [| 0.0f, 0.0f; 1.0f, 0.0f; 1.0f, 1.0f; 0.0f, 1.0f |][vertexIndex]

                        uvs.Add(u)
                        uvs.Add(v)

                indices.Add(baseIndex)
                indices.Add(baseIndex + 1)
                indices.Add(baseIndex + 2)
                indices.Add(baseIndex)
                indices.Add(baseIndex + 2)
                indices.Add(baseIndex + 3)

        if positions.Count = 0 || positions.Count / 3 > int UInt16.MaxValue then
            fail "INVALID_ARTIFACT"

        let positionValues = positions.ToArray()

        let xs =
            [| for index in 0..3 .. positionValues.Length - 3 -> positionValues[index] |]

        let ys =
            [| for index in 1..3 .. positionValues.Length - 2 -> positionValues[index] |]

        let zs =
            [| for index in 2..3 .. positionValues.Length - 1 -> positionValues[index] |]

        { Positions = floatBytes positionValues
          Normals = normals.ToArray() |> floatBytes
          Uvs = if render then Some(uvs.ToArray() |> floatBytes) else None
          Indices = indices.ToArray() |> indexBytes
          VertexCount = positionValues.Length / 3
          IndexCount = indices.Count
          Minimum = [| Array.min xs; Array.min ys; Array.min zs |]
          Maximum = [| Array.max xs; Array.max ys; Array.max zs |] }

    type private BufferView =
        { Offset: int
          Length: int
          Target: int }

    type private Accessor =
        { View: int
          ComponentType: int
          Count: int
          Kind: string
          Minimum: float32 array option
          Maximum: float32 array option }

    let private buildGlb checkpoint (validated: ValidatedCalibrationSpec) =
        let geometries = BlenderCalibration.deriveReferenceGeometry validated.Spec
        let views = ResizeArray<BufferView>()
        let accessors = ResizeArray<Accessor>()
        let meshes = JsonArray()
        use binary = new MemoryStream()

        let addAccessor
            (bytes: byte array)
            (target: int)
            (componentType: int)
            (count: int)
            (kind: string)
            (minimum: float32 array option)
            (maximum: float32 array option)
            =
            let alignment = if componentType = 5126 then 4L else 2L

            while binary.Position % alignment <> 0L do
                binary.WriteByte(0uy)

            let offset = int binary.Position
            binary.Write(bytes, 0, bytes.Length)
            let viewIndex = views.Count

            views.Add(
                { Offset = offset
                  Length = bytes.Length
                  Target = target }
            )

            let accessorIndex = accessors.Count

            accessors.Add(
                { View = viewIndex
                  ComponentType = componentType
                  Count = count
                  Kind = kind
                  Minimum = minimum
                  Maximum = maximum }
            )

            accessorIndex

        let addPrimitive (boxes: CalibrationBox array) (render: bool) (material: int option) =
            checkpoint ()
            let bytes = primitiveBytes checkpoint boxes render

            let position =
                addAccessor
                    bytes.Positions
                    34962
                    5126
                    bytes.VertexCount
                    "VEC3"
                    (Some bytes.Minimum)
                    (Some bytes.Maximum)

            let normal = addAccessor bytes.Normals 34962 5126 bytes.VertexCount "VEC3" None None

            let uv =
                bytes.Uvs
                |> Option.map (fun value -> addAccessor value 34962 5126 bytes.VertexCount "VEC2" None None)

            let indices =
                addAccessor bytes.Indices 34963 5123 bytes.IndexCount "SCALAR" None None

            let attributes = JsonObject()
            attributes["NORMAL"] <- node normal
            attributes["POSITION"] <- node position
            uv |> Option.iter (fun value -> attributes["TEXCOORD_0"] <- node value)

            let primitive = JsonObject()
            primitive["attributes"] <- attributes
            primitive["indices"] <- node indices
            material |> Option.iter (fun value -> primitive["material"] <- node value)
            primitive :> JsonNode

        for geometry in geometries do
            checkpoint ()
            let token = geometry.Id.Replace('-', '_')

            for name, stoneBoxes in
                [| $"MESH_{token}_LOD0", geometry.Lod0StoneBoxes
                   $"MESH_{token}_LOD1", geometry.Lod1StoneBoxes
                   $"MESH_{token}_LOD2", geometry.Lod2StoneBoxes |] do
                let primitives =
                    jsonArray
                        [ addPrimitive stoneBoxes true (Some 0)
                          addPrimitive geometry.WoodBoxes true (Some 1) ]

                meshes.Add(jsonObject [ "name", node name; "primitives", primitives :> JsonNode ])

            let collision = jsonArray [ addPrimitive geometry.CollisionBoxes false None ]

            meshes.Add(jsonObject [ "name", node $"COL_{token}"; "primitives", collision :> JsonNode ])

        let nodes = JsonArray()
        let rootIndices = ResizeArray<int>()
        let snaps = BlenderCalibration.snapPoints validated.Spec |> dict

        for moduleIndex = 0 to BlenderCalibration.moduleOrder.Length - 1 do
            let moduleId = BlenderCalibration.moduleOrder[moduleIndex]
            let token = moduleId.Replace('-', '_')
            let rootIndex = nodes.Count
            rootIndices.Add(rootIndex)
            nodes.Add(null)
            let children = ResizeArray<int>()

            for childIndex = 0 to 3 do
                let name =
                    if childIndex < 3 then
                        $"MESH_{token}_LOD{childIndex}"
                    else
                        $"COL_{token}"

                let index = nodes.Count
                children.Add(index)

                nodes.Add(jsonObject [ "mesh", node (moduleIndex * 4 + childIndex); "name", node name ])

            for snap in snaps[moduleId] do
                let index = nodes.Count
                children.Add(index)
                let x, y, z = snap.TranslationMm

                let gx, gy, gz =
                    BlenderCalibration.blenderToGltfMicrometres (x * 1000L, y * 1000L, z * 1000L)

                let snapNode = JsonObject()
                snapNode["name"] <- node snap.Id

                if snap.RotationQuarterTurns <> 0 then
                    let qx, qy, qz, qw =
                        BlenderCalibration.quarterTurnQuaternion snap.RotationQuarterTurns

                    snapNode["rotation"] <- singleArray [ qx; qy; qz; qw ]

                snapNode["translation"] <-
                    singleArray
                        [ cleanSingle (float32 gx / 1000000.0f)
                          cleanSingle (float32 gy / 1000000.0f)
                          cleanSingle (float32 gz / 1000000.0f) ]

                nodes.Add(snapNode)

            nodes[rootIndex] <-
                jsonObject
                    [ "children", (children |> Seq.map node |> jsonArray :> JsonNode)
                      "name", node $"MOD_{token}" ]

        let material name (color: int array) metallic roughness =
            let linear =
                color |> Array.map (BlenderCalibration.srgb8ToLinear >> float32 >> cleanSingle)

            let pbr =
                jsonObject
                    [ "baseColorFactor", singleArray [ linear[0]; linear[1]; linear[2]; 1.0f ] :> JsonNode
                      "metallicFactor", node (cleanSingle (float32 metallic / 1000.0f))
                      "roughnessFactor", node (cleanSingle (float32 roughness / 1000.0f)) ]

            jsonObject [ "name", node name; "pbrMetallicRoughness", pbr :> JsonNode ] :> JsonNode

        let materials =
            jsonArray
                [ material
                      "MAT_CAL_STONE"
                      validated.Spec.Materials.StoneBaseColorSrgb8
                      validated.Spec.Materials.StoneMetallicPermille
                      validated.Spec.Materials.StoneRoughnessPermille
                  material
                      "MAT_CAL_WOOD"
                      validated.Spec.Materials.WoodBaseColorSrgb8
                      validated.Spec.Materials.WoodMetallicPermille
                      validated.Spec.Materials.WoodRoughnessPermille ]

        let accessorNodes =
            accessors
            |> Seq.map (fun accessor ->
                let value = JsonObject()
                value["bufferView"] <- node accessor.View
                value["componentType"] <- node accessor.ComponentType
                value["count"] <- node accessor.Count
                accessor.Maximum |> Option.iter (fun item -> value["max"] <- singleArray item)
                accessor.Minimum |> Option.iter (fun item -> value["min"] <- singleArray item)
                value["type"] <- node accessor.Kind
                value :> JsonNode)
            |> jsonArray

        let viewNodes =
            views
            |> Seq.map (fun view ->
                jsonObject
                    [ "buffer", node 0
                      "byteLength", node view.Length
                      "byteOffset", node view.Offset
                      "target", node view.Target ]
                :> JsonNode)
            |> jsonArray

        let scene =
            jsonObject
                [ "name", node "SCENE_CAL_STONEWOOD_V1"
                  "nodes", (rootIndices |> Seq.map node |> jsonArray :> JsonNode) ]

        let rawBinary = binary.ToArray()

        let root =
            jsonObject
                [ "accessors", accessorNodes :> JsonNode
                  "asset", (jsonObject [ "generator", node GlbGenerator; "version", node "2.0" ] :> JsonNode)
                  "bufferViews", viewNodes :> JsonNode
                  "buffers", (jsonArray [ jsonObject [ "byteLength", node rawBinary.Length ] :> JsonNode ] :> JsonNode)
                  "materials", materials :> JsonNode
                  "meshes", meshes :> JsonNode
                  "nodes", nodes :> JsonNode
                  "scene", node 0
                  "scenes", jsonArray [ scene :> JsonNode ] :> JsonNode ]

        let json = canonicalNodeBytes root
        let paddedJsonLength = (json.Length + 3) &&& ~~~3
        let paddedBinLength = (rawBinary.Length + 3) &&& ~~~3
        let total = 12 + 8 + paddedJsonLength + 8 + paddedBinLength

        if total > BlenderCalibration.MaxGlbBytes then
            fail "INVALID_ARTIFACT"

        let output = Array.zeroCreate<byte> total
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(0, 4), 0x46546C67u)
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(4, 4), 2u)
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(8, 4), uint32 total)
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(12, 4), uint32 paddedJsonLength)
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(16, 4), 0x4E4F534Au)
        json.CopyTo(output, 20)

        for index = 20 + json.Length to 20 + paddedJsonLength - 1 do
            output[index] <- byte ' '

        let binHeader = 20 + paddedJsonLength
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(binHeader, 4), uint32 paddedBinLength)
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(binHeader + 4, 4), 0x004E4942u)
        Array.Copy(rawBinary, 0, output, binHeader + 8, rawBinary.Length)
        output

    type private RasterVertex = { X: int64; Y: int64; Depth: int64 }

    let private roundEvenRatio64 (numerator: int64) (denominator: int64) =
        if denominator <= 0L then
            invalidArg (nameof denominator) "The denominator must be positive."

        let negative = numerator < 0L

        let absolute =
            if negative then
                -Int128.CreateChecked(numerator)
            else
                Int128.CreateChecked(numerator)

        let divisor = Int128.CreateChecked(denominator)
        let quotient = absolute / divisor
        let remainder = absolute % divisor
        let twice = remainder * Int128.CreateChecked(2L)

        let rounded =
            if
                twice > divisor
                || (twice = divisor && quotient % Int128.CreateChecked(2L) <> Int128.Zero)
            then
                quotient + Int128.One
            else
                quotient

        let signed = if negative then -rounded else rounded
        Int64.CreateChecked(signed)

    let private roundEvenRatio128 (numerator: Int128) (denominator: int64) =
        if denominator <= 0L then
            invalidArg (nameof denominator) "The denominator must be positive."

        let negative = numerator < Int128.Zero
        let absolute = if negative then -numerator else numerator
        let divisor = Int128.CreateChecked(denominator)
        let quotient = absolute / divisor
        let remainder = absolute % divisor
        let twice = remainder * Int128.CreateChecked(2L)

        let rounded =
            if
                twice > divisor
                || (twice = divisor && quotient % Int128.CreateChecked(2L) <> Int128.Zero)
            then
                quotient + Int128.One
            else
                quotient

        Int64.CreateChecked(if negative then -rounded else rounded)

    let private rasterPreview checkpoint (validated: ValidatedCalibrationSpec) =
        let pixels = Array.zeroCreate<byte> (PreviewWidth * PreviewHeight * 4)
        let depth = Array.create (PreviewWidth * PreviewHeight) Int64.MaxValue

        for pixel = 0 to PreviewWidth * PreviewHeight - 1 do
            if pixel % PreviewWidth = 0 then
                checkpoint ()

            let offset = pixel * 4
            pixels[offset] <- 9uy
            pixels[offset + 1] <- 9uy
            pixels[offset + 2] <- 9uy
            pixels[offset + 3] <- 255uy

        let project (point: MicrometrePoint) (moduleOffset: int64) : RasterVertex option =
            let dx = point.X + moduleOffset - 10000000L
            let dy = point.Y + 14000000L
            let dz = point.Z - 9000000L
            let screenNumeratorX = dx * 5L + dy * 3L
            let screenNumeratorY = dx * -114L + dy * 190L + dz * 510L
            let projectedDepth = dx * -90L + dy * 150L + dz * -76L

            if projectedDepth <= 0L then
                None
            else
                Some
                    { X = 480L * RasterSubpixels + roundEvenRatio64 (screenNumeratorX * 256L) 150000L
                      Y = 270L * RasterSubpixels - roundEvenRatio64 (screenNumeratorY * 256L) 10000000L
                      Depth = projectedDepth }

        let edge (a: RasterVertex) (b: RasterVertex) (x: int64) (y: int64) =
            (x - a.X) * (b.Y - a.Y) - (y - a.Y) * (b.X - a.X)

        let topLeft (a: RasterVertex) (b: RasterVertex) =
            let dx = b.X - a.X
            let dy = b.Y - a.Y
            dy > 0L || (dy = 0L && dx < 0L)

        let shadeColor (baseColor: int array) (normal: int * int * int) (item: CalibrationBox) stone =
            let faceShade =
                match normal with
                | 1, 0, 0 -> 620
                | -1, 0, 0 -> 360
                | 0, 1, 0 -> 460
                | 0, -1, 0 -> 700
                | 0, 0, 1 -> 1000
                | 0, 0, -1 -> 240
                | _ -> fail "INVALID_ARTIFACT"

            baseColor
            |> Array.map (fun channel -> byte (max 0 (min 255 ((channel * faceShade + 500) / 1000))))

        let drawTriangle (color: byte array) (first: RasterVertex) (second: RasterVertex) (third: RasterVertex) =
            let mutable a = first
            let mutable b = second
            let mutable c = third
            let mutable area = edge a b c.X c.Y

            if area < 0L then
                let temporary = b
                b <- c
                c <- temporary
                area <- -area

            if area > 0L then
                let floorPixel value =
                    int (Math.Floor(float value / float RasterSubpixels))

                let ceilingPixel value =
                    int (Math.Ceiling(float value / float RasterSubpixels))

                let minX = max 0 (min (floorPixel a.X) (min (floorPixel b.X) (floorPixel c.X)))

                let maxX =
                    min (PreviewWidth - 1) (max (ceilingPixel a.X) (max (ceilingPixel b.X) (ceilingPixel c.X)))

                let minY = max 0 (min (floorPixel a.Y) (min (floorPixel b.Y) (floorPixel c.Y)))

                let maxY =
                    min (PreviewHeight - 1) (max (ceilingPixel a.Y) (max (ceilingPixel b.Y) (ceilingPixel c.Y)))

                for y = minY to maxY do
                    checkpoint ()
                    let sampleY = int64 y * RasterSubpixels + RasterSubpixels / 2L

                    for x = minX to maxX do
                        let sampleX = int64 x * RasterSubpixels + RasterSubpixels / 2L
                        let w0 = edge b c sampleX sampleY
                        let w1 = edge c a sampleX sampleY
                        let w2 = edge a b sampleX sampleY

                        if
                            (w0 > 0L || (w0 = 0L && topLeft b c))
                            && (w1 > 0L || (w1 = 0L && topLeft c a))
                            && (w2 > 0L || (w2 = 0L && topLeft a b))
                        then
                            let rasterDepth =
                                roundEvenRatio128
                                    (Int128.CreateChecked(w0) * Int128.CreateChecked(a.Depth)
                                     + Int128.CreateChecked(w1) * Int128.CreateChecked(b.Depth)
                                     + Int128.CreateChecked(w2) * Int128.CreateChecked(c.Depth))
                                    area

                            let pixel = y * PreviewWidth + x

                            if rasterDepth < depth[pixel] then
                                depth[pixel] <- rasterDepth
                                let offset = pixel * 4
                                pixels[offset] <- color[0]
                                pixels[offset + 1] <- color[1]
                                pixels[offset + 2] <- color[2]
                                pixels[offset + 3] <- 255uy

        let drawBox (moduleOffset: int64) stone (baseColor: int array) (item: CalibrationBox) =
            for normal, vertices in boxFaces item do
                let projected = vertices |> Array.map (fun point -> project point moduleOffset)

                if projected |> Array.forall Option.isSome then
                    let values = projected |> Array.map Option.get
                    let color = shadeColor baseColor normal item stone
                    drawTriangle color values[0] values[1] values[2]
                    drawTriangle color values[0] values[2] values[3]

        let geometries = BlenderCalibration.deriveReferenceGeometry validated.Spec
        let offsets = [| -5000000L; 0L; 5000000L |]

        for index = 0 to geometries.Length - 1 do
            checkpoint ()

            for item in geometries[index].Lod0StoneBoxes do
                checkpoint ()
                drawBox offsets[index] true validated.Spec.Materials.StoneBaseColorSrgb8 item

            for item in geometries[index].WoodBoxes do
                checkpoint ()
                drawBox offsets[index] false validated.Spec.Materials.WoodBaseColorSrgb8 item

        pixels

    let private crc32 (kind: ReadOnlySpan<byte>) (data: ReadOnlySpan<byte>) =
        let mutable crc = 0xFFFFFFFFu

        let update (value: byte) =
            crc <- crc ^^^ uint32 value

            for _ = 0 to 7 do
                let mask = 0u - (crc &&& 1u)
                crc <- (crc >>> 1) ^^^ (0xEDB88320u &&& mask)

        for value in kind do
            update value

        for value in data do
            update value

        ~~~crc

    let private pngChunk (kind: string) (data: byte array) =
        let kindBytes = Constants.Utf8NoBom.GetBytes(kind)
        let output = Array.zeroCreate<byte> (12 + data.Length)
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(0, 4), uint32 data.Length)
        Array.Copy(kindBytes, 0, output, 4, kindBytes.Length)
        Array.Copy(data, 0, output, 8, data.Length)

        BinaryPrimitives.WriteUInt32BigEndian(
            output.AsSpan(8 + data.Length, 4),
            crc32 (ReadOnlySpan<byte>(kindBytes)) (ReadOnlySpan<byte>(data))
        )

        output

    let private adler32 (bytes: byte array) =
        let modulus = 65521u
        let mutable a = 1u
        let mutable b = 0u

        for value in bytes do
            a <- (a + uint32 value) % modulus
            b <- (b + a) % modulus

        (b <<< 16) ||| a

    let private storedZlib (bytes: byte array) =
        use output = new MemoryStream(bytes.Length + bytes.Length / 65535 * 5 + 16)
        output.WriteByte(0x78uy)
        output.WriteByte(0x01uy)
        let mutable offset = 0

        while offset < bytes.Length do
            let count = min 65535 (bytes.Length - offset)
            let final = offset + count = bytes.Length
            output.WriteByte(if final then 1uy else 0uy)
            let lengths = Array.zeroCreate<byte> 4
            BinaryPrimitives.WriteUInt16LittleEndian(lengths.AsSpan(0, 2), uint16 count)
            BinaryPrimitives.WriteUInt16LittleEndian(lengths.AsSpan(2, 2), ~~~(uint16 count))
            output.Write(lengths, 0, lengths.Length)
            output.Write(bytes, offset, count)
            offset <- offset + count

        let checksum = Array.zeroCreate<byte> 4
        BinaryPrimitives.WriteUInt32BigEndian(checksum.AsSpan(), adler32 bytes)
        output.Write(checksum, 0, checksum.Length)
        output.ToArray()

    let private encodePng (rgba: byte array) =
        if rgba.Length <> PreviewWidth * PreviewHeight * 4 then
            fail "INVALID_ARTIFACT"

        let scanlines = Array.zeroCreate<byte> (PreviewHeight * (PreviewWidth * 4 + 1))

        for y = 0 to PreviewHeight - 1 do
            let target = y * (PreviewWidth * 4 + 1)
            scanlines[target] <- 0uy
            Array.Copy(rgba, y * PreviewWidth * 4, scanlines, target + 1, PreviewWidth * 4)

        let header = Array.zeroCreate<byte> 13
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), uint32 PreviewWidth)
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), uint32 PreviewHeight)
        header[8] <- 8uy
        header[9] <- 6uy
        header[10] <- 0uy
        header[11] <- 0uy
        header[12] <- 0uy

        Array.concat
            [| [| 137uy; 80uy; 78uy; 71uy; 13uy; 10uy; 26uy; 10uy |]
               pngChunk "IHDR" header
               pngChunk "IDAT" (storedZlib scanlines)
               pngChunk "IEND" Array.empty |]

    let private metricNode (metric: PrimitiveMetrics) =
        jsonObject
            [ "decodedGeometryBytes", node metric.DecodedGeometryBytes
              "indices", node metric.Indices
              "primitives", node metric.Primitives
              "triangles", node metric.Triangles
              "vertices", node metric.Vertices ]

    let private boundsNode (bounds: CalibrationBox) =
        jsonObject
            [ "max", int64Array [ bounds.Max.X; bounds.Max.Y; bounds.Max.Z ] :> JsonNode
              "min", int64Array [ bounds.Min.X; bounds.Min.Y; bounds.Min.Z ] :> JsonNode ]

    let private techniqueReport
        (validated: ValidatedCalibrationSpec)
        (glb: GlbInspection)
        (png: PngInspection)
        sources
        sourceHash
        toolchainHash
        =
        let sourceNodes =
            sources
            |> Seq.map (fun (path, hash) -> jsonObject [ "path", node path; "sha256", node hash ] :> JsonNode)
            |> jsonArray

        let snapReferences = BlenderCalibration.snapPoints validated.Spec |> dict

        let modules =
            glb.Modules
            |> Seq.map (fun inspected ->
                let snaps =
                    snapReferences[inspected.Id]
                    |> Seq.map (fun snap ->
                        let x, y, z = snap.TranslationMm

                        jsonObject
                            [ "id", node snap.Id
                              "rotationQuarterTurns", node snap.RotationQuarterTurns
                              "translationMm", int64Array [ x; y; z ] :> JsonNode ]
                        :> JsonNode)
                    |> jsonArray

                jsonObject
                    [ "boundsMicrometres", boundsNode inspected.Bounds :> JsonNode
                      "collision", metricNode inspected.Collision :> JsonNode
                      "id", node inspected.Id
                      "lod0", metricNode inspected.Lod0 :> JsonNode
                      "lod1", metricNode inspected.Lod1 :> JsonNode
                      "lod2", metricNode inspected.Lod2 :> JsonNode
                      "snapPoints", snaps :> JsonNode ]
                :> JsonNode)
            |> jsonArray

        let reportMaterial name color metallic roughness =
            jsonObject
                [ "baseColorSrgb8", intArray color :> JsonNode
                  "metallicPermille", node metallic
                  "name", node name
                  "roughnessPermille", node roughness ]
            :> JsonNode

        let materials =
            jsonArray
                [ reportMaterial
                      "MAT_CAL_STONE"
                      validated.Spec.Materials.StoneBaseColorSrgb8
                      validated.Spec.Materials.StoneMetallicPermille
                      validated.Spec.Materials.StoneRoughnessPermille
                  reportMaterial
                      "MAT_CAL_WOOD"
                      validated.Spec.Materials.WoodBaseColorSrgb8
                      validated.Spec.Materials.WoodMetallicPermille
                      validated.Spec.Materials.WoodRoughnessPermille ]

        let assetId =
            validated.Spec.FamilyId
            + "-"
            + validated.SpecSha256.Substring(0, 12).ToUpperInvariant()

        let artifact path hash bytes =
            jsonObject [ "bytes", node bytes; "path", node path; "sha256", node hash ]

        let artifacts =
            jsonObject
                [ "glb", artifact $"assets/quarantine/3d/{assetId}/family.glb" glb.Sha256 glb.Bytes :> JsonNode
                  "preview", artifact $"assets/quarantine/3d/{assetId}/preview.png" png.Sha256 png.Bytes :> JsonNode ]

        let familyMetrics =
            jsonObject
                [ "decodedGeometryBytes", node glb.DecodedGeometryBytes
                  "glbBytes", node glb.Bytes
                  "materialCount", node glb.MaterialCount
                  "renderPrimitiveCount", node glb.RenderPrimitiveCount ]

        let limits =
            jsonObject
                [ "collisionTriangles", node 48
                  "decodedGeometryBytes", node 2097152
                  "glbBytes", node 2097152
                  "lod0Triangles", node 4096
                  "lod0Vertices", node 3072
                  "lod1Triangles", node 1024
                  "lod1Vertices", node 1024
                  "lod2Triangles", node 192
                  "lod2Vertices", node 256
                  "materials", node 2
                  "renderPrimitivesPerLod", node 2 ]

        jsonObject
            [ "artifacts", artifacts :> JsonNode
              "familyId", node validated.Spec.FamilyId
              "familyMetrics", familyMetrics :> JsonNode
              "generatorSourceSha256", node sourceHash
              "generatorSources", sourceNodes :> JsonNode
              "limits", limits :> JsonNode
              "materials", materials :> JsonNode
              "modules", modules :> JsonNode
              "profile", node validated.Spec.Profile
              "schemaVersion", node 1
              "seed", node validated.Spec.Seed
              "specSha256", node validated.SpecSha256
              "toolchainPinSha256", node toolchainHash ]
        |> canonicalNodeBytes
        |> fun bytes -> Array.append bytes [| byte '\n' |]

    let private writeDurable (path: string) (bytes: byte array) =
        use stream =
            new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)

        stream.Write(bytes, 0, bytes.Length)
        stream.Flush(true)

    let private cleanOwnedTemporaryDirectory path (owned: (string * string) array) =
        try
            for name, hash in owned do
                let file = Path.Combine(path, name)

                if File.Exists(file) then
                    let attributes = File.GetAttributes(file)

                    if
                        not (attributes.HasFlag(FileAttributes.Directory))
                        && not (attributes.HasFlag(FileAttributes.ReparsePoint))
                        && Internal.sha256File file = hash
                    then
                        File.Delete(file)

            if Directory.Exists(path) then
                let attributes = File.GetAttributes(path)

                if
                    not (attributes.HasFlag(FileAttributes.ReparsePoint))
                    && Directory.EnumerateFileSystemEntries(path) |> Seq.isEmpty
                then
                    Directory.Delete(path)
        with _ ->
            ()

    /// Generates and atomically publishes exactly family.glb, preview.png and technique.json
    /// into a caller-owned `.ai/runtime/asset-jobs/<ULID>/stage/quarantine` directory.
    let generateWithCancellation
        root
        (validated: ValidatedCalibrationSpec)
        stageRelative
        (cancellationToken: Threading.CancellationToken)
        =
        mapArtifactFailure (fun () ->
            let deadline = Environment.TickCount64 + 300_000L

            let checkpoint () =
                if cancellationToken.IsCancellationRequested then
                    fail "CANCELLED"

                ensureDeadline deadline

            let safeRoot = requireWorkspaceRoot root
            let canonicalStage = validateStageRelative stageRelative
            let locations = Workspace.paths safeRoot
            let stagePath = Path.Combine(locations.Root, canonicalStage)
            let safeStage = Workspace.requireSafePath locations "Asset-Stage" true stagePath
            let jobRoot = Path.GetDirectoryName(safeStage)

            if isNull jobRoot || not (Directory.Exists(jobRoot)) then
                fail "UNSAFE_PATH"

            Workspace.requireSafePath locations "Asset-Job" false jobRoot |> ignore

            if File.Exists(safeStage) || Directory.Exists(safeStage) then
                fail "TRANSACTION_CONFLICT"

            let geometries = BlenderCalibration.deriveReferenceGeometry validated.Spec
            checkpoint ()

            if geometries.Length <> 3 then
                fail "INVALID_ARTIFACT"

            let glbBytes = buildGlb checkpoint validated
            checkpoint ()
            let glb = Asset3dInspector.inspectGlbBytes validated glbBytes
            let previewBytes = rasterPreview checkpoint validated |> encodePng
            checkpoint ()
            let png = Asset3dInspector.inspectPngBytes previewBytes
            let sources, sourceHash = sourceInventory safeRoot
            let pinHash = toolchainPin safeRoot
            let reportBytes = techniqueReport validated glb png sources sourceHash pinHash

            if reportBytes.Length > Asset3dInspector.MaxReportBytes then
                fail "INVALID_ARTIFACT"

            let temporaryName = ".stage-" + Guid.NewGuid().ToString("N") + ".tmp"
            let temporaryPath = Path.Combine(jobRoot, temporaryName)
            let temporaryRelative = Workspace.relativePath locations temporaryPath
            let mutable owned = Array.empty<string * string>

            try
                if File.Exists(temporaryPath) || Directory.Exists(temporaryPath) then
                    fail "TRANSACTION_CONFLICT"

                Directory.CreateDirectory(temporaryPath) |> ignore

                Workspace.requireSafePath locations "Temporaerer Asset-Stage" false temporaryPath
                |> ignore

                let artifacts =
                    [| "family.glb", glbBytes
                       "preview.png", previewBytes
                       "technique.json", reportBytes |]

                owned <- artifacts |> Array.map (fun (name, bytes) -> name, Internal.sha256Hex bytes)

                for name, bytes in artifacts do
                    checkpoint ()
                    writeDurable (Path.Combine(temporaryPath, name)) bytes

                let inspection =
                    Asset3dInspector.inspect
                        safeRoot
                        validated
                        (temporaryRelative + "/family.glb")
                        (temporaryRelative + "/preview.png")
                        (temporaryRelative + "/technique.json")

                checkpoint ()

                if
                    inspection.GlbSha256 <> glb.Sha256
                    || inspection.PreviewSha256 <> png.Sha256
                    || inspection.ReportSha256 <> Internal.sha256Hex reportBytes
                then
                    fail "INVALID_ARTIFACT"

                Workspace.requireSafePath locations "Asset-Job" false jobRoot |> ignore

                if File.Exists(safeStage) || Directory.Exists(safeStage) then
                    fail "TRANSACTION_CONFLICT"

                try
                    Directory.Move(temporaryPath, safeStage)
                with
                | :? IOException -> fail "TRANSACTION_CONFLICT"
                | :? UnauthorizedAccessException -> fail "TRANSACTION_CONFLICT"

                let assetId =
                    validated.Spec.FamilyId
                    + "-"
                    + validated.SpecSha256.Substring(0, 12).ToUpperInvariant()

                let info (name: string) (bytes: byte array) =
                    { RelativePath = canonicalStage + "/" + name
                      Bytes = int64 bytes.Length
                      Sha256 = Internal.sha256Hex bytes }

                { AssetId = assetId
                  Glb = info "family.glb" glbBytes
                  Preview = info "preview.png" previewBytes
                  Technique = info "technique.json" reportBytes }
            finally
                cleanOwnedTemporaryDirectory temporaryPath owned)

    let generate root validated stageRelative =
        generateWithCancellation root validated stageRelative Threading.CancellationToken.None
