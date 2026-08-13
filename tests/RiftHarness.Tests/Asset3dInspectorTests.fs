namespace RiftHarness.Tests

open System
open System.Buffers.Binary
open System.Collections.Generic
open System.IO
open System.IO.Compression
open System.Text.Json
open System.Text.Json.Nodes
open RiftHarness

[<RequireQualifiedAccess>]
module Asset3dInspectorTests =
    type private GlbMutation =
        | NoMutation
        | InteriorPosition
        | DuplicateTriangle
        | AdjacentSplit
        | ReverseWinding
        | WrongNormal
        | NegativeZeroFloat
        | UnalignedFloatView
        | StringSceneNode
        | NonObjectNode
        | OutOfRangeView
        | OutOfRangeIndex
        | NonFiniteFloat
        | ExternalBufferUri
        | UnknownNodeName
        | WrongRootTransform
        | MissingLodChild
        | WrongMaterial
        | WrongAccessorBounds
        | TooManyRenderPrimitives
        | TooManyMaterials
        | TooManyLod0Vertices
        | TooManyLod0Triangles
        | TooManyLod1Vertices
        | TooManyLod1Triangles
        | TooManyLod2Vertices
        | TooManyLod2Triangles
        | TooManyCollisionTriangles

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

    let private validated () =
        File.ReadAllBytes(Path.Combine(repositoryRoot, "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json"))
        |> BlenderCalibration.parseSpecBytes

    let private expectInspectionFailure expected action =
        try
            action ()
            failwith $"Expected {expected}."
        with AssetInspectionError actual when actual = expected ->
            ()

    let private expectPathFailure action =
        try
            action ()
            failwith "Expected UNSAFE_PATH."
        with AssetInspectionPathError "UNSAFE_PATH" ->
            ()

    let private node (value: 'value) = JsonValue.Create(value) :> JsonNode

    let private jsonArray (values: seq<JsonNode>) =
        let result = JsonArray()
        values |> Seq.iter (result.Add >> ignore)
        result

    let private intArray values = values |> Seq.map node |> jsonArray
    let private int64Array values = values |> Seq.map node |> jsonArray
    let private floatArray values = values |> Seq.map node |> jsonArray

    let private gltfPoint (point: MicrometrePoint) =
        float32 point.X / 1000000.0f, float32 point.Z / 1000000.0f, float32 -point.Y / 1000000.0f

    let private gltfVector (x: int, y: int, z: int) = float32 x, float32 z, float32 -y

    let private boxFaces (box: CalibrationBox) =
        let minimum = box.Min
        let maximum = box.Max
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

    type private PrimitiveBytes =
        { Positions: byte array
          Normals: byte array
          Uvs: byte array option
          Indices: byte array
          VertexCount: int
          IndexCount: int
          Minimum: float32 array
          Maximum: float32 array }

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

    let private primitiveBytes mutation primitiveOrdinal (boxes: CalibrationBox array) render =
        let positions = ResizeArray<float32>()
        let normals = ResizeArray<float32>()
        let uvs = ResizeArray<float32>()
        let indices = ResizeArray<int>()

        for box in boxes do
            for normal, vertices in boxFaces box do
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
                        let uv = [| 0.0f, 0.0f; 1.0f, 0.0f; 1.0f, 1.0f; 0.0f, 1.0f |][vertexIndex]
                        uvs.Add(fst uv)
                        uvs.Add(snd uv)

                indices.Add(baseIndex)
                indices.Add(baseIndex + 1)
                indices.Add(baseIndex + 2)
                indices.Add(baseIndex)
                indices.Add(baseIndex + 2)
                indices.Add(baseIndex + 3)

        let extendFloats components targetVertices (source: float32 array) =
            if targetVertices <= source.Length / components then
                source
            else
                Array.init (targetVertices * components) (fun index -> source[index % source.Length])

        let targetVertices =
            match mutation, primitiveOrdinal with
            | TooManyLod0Vertices, 0 -> 3025
            | TooManyLod1Vertices, 2 -> 977
            | TooManyLod2Vertices, 4 -> 209
            | _ -> positions.Count / 3

        let targetTriangles =
            match mutation, primitiveOrdinal with
            | TooManyLod0Triangles, 0 -> 4073
            | TooManyLod1Triangles, 2 -> 1001
            | TooManyLod2Triangles, 4 -> 169
            | TooManyCollisionTriangles, 6 -> 49
            | _ -> indices.Count / 3

        let positionArray = positions.ToArray() |> extendFloats 3 targetVertices
        let normalArray = normals.ToArray() |> extendFloats 3 targetVertices

        let uvArray =
            if render then
                uvs.ToArray() |> extendFloats 2 targetVertices
            else
                Array.empty

        let sourceIndices = indices.ToArray()

        let indexArray =
            if targetTriangles <= sourceIndices.Length / 3 then
                sourceIndices
            else
                Array.init (targetTriangles * 3) (fun index -> sourceIndices[index % sourceIndices.Length])

        if primitiveOrdinal = 0 then
            match mutation with
            | InteriorPosition -> positionArray[2] <- (positionArray[2] + positionArray[5]) / 2.0f
            | WrongNormal -> normalArray[0] <- -normalArray[0]
            | DuplicateTriangle ->
                indexArray[3] <- indexArray[0]
                indexArray[4] <- indexArray[1]
                indexArray[5] <- indexArray[2]
            | AdjacentSplit ->
                let originalFourth = indexArray[3]
                indexArray[3] <- indexArray[1]
                indexArray[4] <- indexArray[2]
                indexArray[5] <- originalFourth
            | ReverseWinding ->
                let temporary = indexArray[0]
                indexArray[0] <- indexArray[1]
                indexArray[1] <- temporary
            | NegativeZeroFloat -> normalArray[1] <- BitConverter.Int32BitsToSingle(Int32.MinValue)
            | NonFiniteFloat -> normalArray[1] <- Single.NaN
            | OutOfRangeIndex -> indexArray[0] <- positionArray.Length / 3
            | _ -> ()

        let xs = [| for index in 0..3 .. positionArray.Length - 3 -> positionArray[index] |]
        let ys = [| for index in 1..3 .. positionArray.Length - 2 -> positionArray[index] |]
        let zs = [| for index in 2..3 .. positionArray.Length - 1 -> positionArray[index] |]
        let minimum = [| Array.min xs; Array.min ys; Array.min zs |]
        let maximum = [| Array.max xs; Array.max ys; Array.max zs |]

        if primitiveOrdinal = 0 && mutation = WrongAccessorBounds then
            minimum[0] <- minimum[0] - 1.0f

        { Positions = floatBytes positionArray
          Normals = floatBytes normalArray
          Uvs = if render then Some(floatBytes uvArray) else Option.None
          Indices = indexBytes indexArray
          VertexCount = positionArray.Length / 3
          IndexCount = indexArray.Length
          Minimum = minimum
          Maximum = maximum }

    let private syntheticGlb mutation (validated: ValidatedCalibrationSpec) =
        let geometries = BlenderCalibration.deriveReferenceGeometry validated.Spec
        let views = JsonArray()
        let accessors = JsonArray()
        let meshes = JsonArray()
        use binary = new MemoryStream()
        let mutable primitiveOrdinal = 0

        if mutation = UnalignedFloatView then
            binary.Write([| 0uy; 0uy |])

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

            if mutation <> UnalignedFloatView then
                while binary.Position % alignment <> 0L do
                    binary.WriteByte(0uy)

            let offset = int binary.Position
            binary.Write(bytes)
            let viewIndex = views.Count
            let view = JsonObject()
            view["buffer"] <- node 0
            view["byteLength"] <- node bytes.Length
            view["byteOffset"] <- node offset
            view["target"] <- node target
            views.Add(view)
            let accessorIndex = accessors.Count
            let accessor = JsonObject()
            accessor["bufferView"] <- node viewIndex
            accessor["componentType"] <- node componentType
            accessor["count"] <- node count
            accessor["type"] <- node kind

            match minimum, maximum with
            | Some minValues, Some maxValues ->
                accessor["min"] <- floatArray minValues
                accessor["max"] <- floatArray maxValues
            | _ -> ()

            accessors.Add(accessor)
            accessorIndex

        let addPrimitive (boxes: CalibrationBox array) (render: bool) (material: int option) =
            let ordinal = primitiveOrdinal
            let bytes = primitiveBytes mutation ordinal boxes render
            primitiveOrdinal <- primitiveOrdinal + 1

            let position =
                addAccessor
                    bytes.Positions
                    34962
                    5126
                    bytes.VertexCount
                    "VEC3"
                    (Some bytes.Minimum)
                    (Some bytes.Maximum)

            let normal =
                addAccessor bytes.Normals 34962 5126 bytes.VertexCount "VEC3" Option.None Option.None

            let uv =
                bytes.Uvs
                |> Option.map (fun values ->
                    addAccessor values 34962 5126 bytes.VertexCount "VEC2" Option.None Option.None)

            let indices =
                addAccessor bytes.Indices 34963 5123 bytes.IndexCount "SCALAR" Option.None Option.None

            let attributes = JsonObject()
            attributes["POSITION"] <- node position
            attributes["NORMAL"] <- node normal
            uv |> Option.iter (fun value -> attributes["TEXCOORD_0"] <- node value)
            let primitive = JsonObject()
            primitive["attributes"] <- attributes
            primitive["indices"] <- node indices

            let emittedMaterial =
                if mutation = WrongMaterial && ordinal = 0 then
                    Some 1
                else
                    material

            emittedMaterial
            |> Option.iter (fun value -> primitive["material"] <- node value)

            primitive :> JsonNode

        for geometry in geometries do
            let token = geometry.Id.Replace('-', '_')

            for name, stoneBoxes in
                [| $"MESH_{token}_LOD0", geometry.Lod0StoneBoxes
                   $"MESH_{token}_LOD1", geometry.Lod1StoneBoxes
                   $"MESH_{token}_LOD2", geometry.Lod2StoneBoxes |] do
                let primitiveItems =
                    ResizeArray<JsonNode>(
                        [ addPrimitive stoneBoxes true (Some 0)
                          addPrimitive geometry.WoodBoxes true (Some 1) ]
                    )

                if mutation = TooManyRenderPrimitives && meshes.Count = 0 then
                    primitiveItems.Add(addPrimitive stoneBoxes true (Some 0))

                let primitives = jsonArray primitiveItems

                meshes.Add(JsonObject([ KeyValuePair("name", node name); KeyValuePair("primitives", primitives) ]))

            let collision = jsonArray [ addPrimitive geometry.CollisionBoxes false Option.None ]

            meshes.Add(
                JsonObject(
                    [ KeyValuePair("name", node $"COL_{token}")
                      KeyValuePair("primitives", collision) ]
                )
            )

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

                nodes.Add(
                    JsonObject(
                        [ KeyValuePair("mesh", node (moduleIndex * 4 + childIndex))
                          KeyValuePair("name", node name) ]
                    )
                )

            for snap in snaps[moduleId] do
                let index = nodes.Count
                children.Add(index)
                let snapNode = JsonObject()
                snapNode["name"] <- node snap.Id
                let x, y, z = snap.TranslationMm

                let gx, gy, gz =
                    BlenderCalibration.blenderToGltfMicrometres (x * 1000L, y * 1000L, z * 1000L)

                snapNode["translation"] <-
                    floatArray [ float32 gx / 1000000.0f; float32 gy / 1000000.0f; float32 gz / 1000000.0f ]

                if snap.RotationQuarterTurns <> 0 then
                    let qx, qy, qz, qw =
                        BlenderCalibration.quarterTurnQuaternion snap.RotationQuarterTurns

                    snapNode["rotation"] <- floatArray [ qx; qy; qz; qw ]

                nodes.Add(snapNode)

            let childNodes =
                children
                |> Seq.mapi (fun index value -> index, value)
                |> Seq.filter (fun (index, _) -> mutation <> MissingLodChild || moduleIndex <> 0 || index <> 2)
                |> Seq.map (snd >> node)
                |> jsonArray

            let rootNode = JsonObject()
            rootNode["children"] <- childNodes

            rootNode["name"] <-
                if mutation = UnknownNodeName && moduleIndex = 0 then
                    node "MOD_UNKNOWN"
                else
                    node $"MOD_{token}"

            if mutation = WrongRootTransform && moduleIndex = 0 then
                rootNode["translation"] <- floatArray [ 0.0f; 0.0f; 0.0f ]

            nodes[rootIndex] <-
                if mutation = NonObjectNode && moduleIndex = 0 then
                    node "bad"
                else
                    rootNode

        let material (name: string) (color: int array) (metallic: int) (roughness: int) : JsonNode =
            let linear = color |> Array.map (BlenderCalibration.srgb8ToLinear >> float32)
            let pbr = JsonObject()
            pbr["baseColorFactor"] <- floatArray [ linear[0]; linear[1]; linear[2]; 1.0f ]
            pbr["metallicFactor"] <- node (float32 metallic / 1000.0f)
            pbr["roughnessFactor"] <- node (float32 roughness / 1000.0f)

            JsonObject(
                [ KeyValuePair("name", node name)
                  KeyValuePair("pbrMetallicRoughness", pbr :> JsonNode) ]
            )

        let materialItems =
            ResizeArray<JsonNode>(
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
            )

        if mutation = TooManyMaterials then
            materialItems.Add(
                material
                    "MAT_CAL_EXTRA"
                    validated.Spec.Materials.StoneBaseColorSrgb8
                    validated.Spec.Materials.StoneMetallicPermille
                    validated.Spec.Materials.StoneRoughnessPermille
            )

        let materials = jsonArray materialItems

        if mutation = OutOfRangeView then
            let firstView = views[0].AsObject()
            firstView["byteOffset"] <- node (int binary.Length)

        let root = JsonObject()
        root["accessors"] <- accessors

        root["asset"] <-
            JsonObject(
                [ KeyValuePair("generator", node "Khronos glTF Blender I/O v5.2.39")
                  KeyValuePair("version", node "2.0") ]
            )

        root["bufferViews"] <- views

        let bufferObject =
            JsonObject([ KeyValuePair("byteLength", node (int binary.Length)) ])

        if mutation = ExternalBufferUri then
            bufferObject["uri"] <- node "external.bin"

        root["buffers"] <- jsonArray [ bufferObject ]
        root["materials"] <- materials
        root["meshes"] <- meshes
        root["nodes"] <- nodes
        root["scene"] <- node 0

        let sceneNodeItems = rootIndices |> Seq.map node |> Seq.toArray

        if mutation = StringSceneNode then
            sceneNodeItems[0] <- node "bad"

        root["scenes"] <-
            jsonArray
                [ JsonObject(
                      [ KeyValuePair("name", node "SCENE_CAL_STONEWOOD_V1")
                        KeyValuePair("nodes", jsonArray sceneNodeItems :> JsonNode) ]
                  )
                  :> JsonNode ]

        let json =
            Constants.Utf8NoBom.GetBytes(root.ToJsonString(JsonSerializerOptions(WriteIndented = false)))

        let paddedJsonLength = (json.Length + 3) &&& ~~~3
        let rawBin = binary.ToArray()
        let paddedBinLength = (rawBin.Length + 3) &&& ~~~3
        let bin = Array.zeroCreate<byte> paddedBinLength
        rawBin.CopyTo(bin, 0)
        let total = 12 + 8 + paddedJsonLength + 8 + bin.Length
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
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(binHeader, 4), uint32 bin.Length)
        BinaryPrimitives.WriteUInt32LittleEndian(output.AsSpan(binHeader + 4, 4), 0x004E4942u)
        bin.CopyTo(output, binHeader + 8)
        output

    let private crc32 (kind: byte array) (data: byte array) =
        let mutable crc = 0xFFFFFFFFu

        for value in Array.append kind data do
            crc <- crc ^^^ uint32 value

            for _ = 0 to 7 do
                let mask = 0u - (crc &&& 1u)
                crc <- (crc >>> 1) ^^^ (0xEDB88320u &&& mask)

        ~~~crc

    let private pngChunk (kind: string) (data: byte array) =
        let kindBytes = Constants.Utf8NoBom.GetBytes(kind)
        let output = Array.zeroCreate<byte> (12 + data.Length)
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(0, 4), uint32 data.Length)
        Array.Copy(kindBytes, 0, output, 4, kindBytes.Length)
        Array.Copy(data, 0, output, 8, data.Length)
        BinaryPrimitives.WriteUInt32BigEndian(output.AsSpan(8 + data.Length, 4), crc32 kindBytes data)
        output

    let private syntheticPng trailingJunk =
        let decoded = Array.zeroCreate<byte> (3841 * 540)
        use compressed = new MemoryStream()

        use zlib = new ZLibStream(compressed, CompressionLevel.SmallestSize, true)
        zlib.Write(decoded)
        zlib.Close()
        let original = compressed.ToArray()

        let payload =
            if trailingJunk then
                Array.concat
                    [| original[0 .. original.Length - 5]
                       [| 0uy |]
                       original[original.Length - 4 ..] |]
            else
                original

        let header = Array.zeroCreate<byte> 13
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(0, 4), 960u)
        BinaryPrimitives.WriteUInt32BigEndian(header.AsSpan(4, 4), 540u)
        header[8] <- 8uy
        header[9] <- 6uy
        let signature = [| 137uy; 80uy; 78uy; 71uy; 13uy; 10uy; 26uy; 10uy |]

        Array.concat
            [| signature
               pngChunk "IHDR" header
               pngChunk "IDAT" payload
               pngChunk "IEND" Array.empty |]

    type InspectionFixture =
        { Validated: ValidatedCalibrationSpec
          SpecRelative: string
          GlbRelative: string
          PreviewRelative: string
          ReportRelative: string }

    let private metricNode (metric: PrimitiveMetrics) =
        JsonObject(
            [ KeyValuePair("decodedGeometryBytes", node metric.DecodedGeometryBytes)
              KeyValuePair("indices", node metric.Indices)
              KeyValuePair("primitives", node metric.Primitives)
              KeyValuePair("triangles", node metric.Triangles)
              KeyValuePair("vertices", node metric.Vertices) ]
        )

    let private boundsNode (bounds: CalibrationBox) =
        JsonObject(
            [ KeyValuePair("max", int64Array [ bounds.Max.X; bounds.Max.Y; bounds.Max.Z ] :> JsonNode)
              KeyValuePair("min", int64Array [ bounds.Min.X; bounds.Min.Y; bounds.Min.Z ] :> JsonNode) ]
        )

    let private reportNode (validated: ValidatedCalibrationSpec) (glb: GlbInspection) (png: PngInspection) =
        let sourcePaths =
            [| "tools/BlenderCalibration/generate.py"
               "tools/RiftHarness/AssetJobJournal.fs"
               "tools/RiftHarness/BlenderCalibration.fs"
               "tools/RiftHarness/BlenderGenerator.fs"
               "tools/RiftHarness/LinuxSandbox.fs" |]

        let sourceHashes =
            sourcePaths |> Array.map (fun path -> Internal.sha256Text ("fixture:" + path))

        use sourceBinding = new MemoryStream()

        let sources =
            Array.map2
                (fun path hash ->
                    let binding = Constants.Utf8NoBom.GetBytes(path + "\n" + hash + "\n")
                    sourceBinding.Write(binding, 0, binding.Length)

                    JsonObject([ KeyValuePair("path", node path); KeyValuePair("sha256", node hash) ]) :> JsonNode)
                sourcePaths
                sourceHashes
            |> jsonArray

        let sourceAggregate = Internal.sha256Hex (sourceBinding.ToArray())
        let snapReferences = BlenderCalibration.snapPoints validated.Spec |> dict

        let modules =
            glb.Modules
            |> Array.map (fun inspected ->
                let snaps =
                    snapReferences[inspected.Id]
                    |> Array.map (fun snap ->
                        let x, y, z = snap.TranslationMm

                        JsonObject(
                            [ KeyValuePair("id", node snap.Id)
                              KeyValuePair("rotationQuarterTurns", node snap.RotationQuarterTurns)
                              KeyValuePair("translationMm", int64Array [ x; y; z ] :> JsonNode) ]
                        )
                        :> JsonNode)
                    |> jsonArray

                JsonObject(
                    [ KeyValuePair("boundsMicrometres", boundsNode inspected.Bounds :> JsonNode)
                      KeyValuePair("collision", metricNode inspected.Collision :> JsonNode)
                      KeyValuePair("id", node inspected.Id)
                      KeyValuePair("lod0", metricNode inspected.Lod0 :> JsonNode)
                      KeyValuePair("lod1", metricNode inspected.Lod1 :> JsonNode)
                      KeyValuePair("lod2", metricNode inspected.Lod2 :> JsonNode)
                      KeyValuePair("snapPoints", snaps :> JsonNode) ]
                )
                :> JsonNode)
            |> jsonArray

        let reportMaterial name color metallic roughness =
            JsonObject(
                [ KeyValuePair("baseColorSrgb8", intArray color :> JsonNode)
                  KeyValuePair("metallicPermille", node metallic)
                  KeyValuePair("name", node name)
                  KeyValuePair("roughnessPermille", node roughness) ]
            )
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
            JsonObject(
                [ KeyValuePair("bytes", node bytes)
                  KeyValuePair("path", node path)
                  KeyValuePair("sha256", node hash) ]
            )

        let artifacts =
            JsonObject(
                [ KeyValuePair(
                      "glb",
                      artifact $"assets/quarantine/3d/{assetId}/family.glb" glb.Sha256 glb.Bytes :> JsonNode
                  )
                  KeyValuePair(
                      "preview",
                      artifact $"assets/quarantine/3d/{assetId}/preview.png" png.Sha256 png.Bytes :> JsonNode
                  ) ]
            )

        let familyMetrics =
            JsonObject(
                [ KeyValuePair("decodedGeometryBytes", node glb.DecodedGeometryBytes)
                  KeyValuePair("glbBytes", node glb.Bytes)
                  KeyValuePair("materialCount", node glb.MaterialCount)
                  KeyValuePair("renderPrimitiveCount", node glb.RenderPrimitiveCount) ]
            )

        let limits =
            JsonObject(
                [ KeyValuePair("collisionTriangles", node 48)
                  KeyValuePair("decodedGeometryBytes", node 2097152)
                  KeyValuePair("glbBytes", node 2097152)
                  KeyValuePair("lod0Triangles", node 4096)
                  KeyValuePair("lod0Vertices", node 3072)
                  KeyValuePair("lod1Triangles", node 1024)
                  KeyValuePair("lod1Vertices", node 1024)
                  KeyValuePair("lod2Triangles", node 192)
                  KeyValuePair("lod2Vertices", node 256)
                  KeyValuePair("materials", node 2)
                  KeyValuePair("renderPrimitivesPerLod", node 2) ]
            )

        JsonObject(
            [ KeyValuePair("artifacts", artifacts :> JsonNode)
              KeyValuePair("familyId", node validated.Spec.FamilyId)
              KeyValuePair("familyMetrics", familyMetrics :> JsonNode)
              KeyValuePair("generatorSourceSha256", node sourceAggregate)
              KeyValuePair("generatorSources", sources :> JsonNode)
              KeyValuePair("limits", limits :> JsonNode)
              KeyValuePair("materials", materials :> JsonNode)
              KeyValuePair("modules", modules :> JsonNode)
              KeyValuePair("profile", node validated.Spec.Profile)
              KeyValuePair("schemaVersion", node 1)
              KeyValuePair("seed", node validated.Spec.Seed)
              KeyValuePair("specSha256", node validated.SpecSha256)
              KeyValuePair("toolchainPinSha256", node Asset3dInspector.ToolchainPinSha256) ]
        )

    let private canonicalNodeBytes (value: JsonNode) =
        use document =
            JsonDocument.Parse(value.ToJsonString(JsonSerializerOptions(WriteIndented = false)))

        Array.append (Internal.canonicalElement document.RootElement) [| byte '\n' |]

    let private writeBytes root relative (bytes: byte array) =
        let path = Path.Combine(root, relative)
        Directory.CreateDirectory(Path.GetDirectoryName(path)) |> ignore
        File.WriteAllBytes(path, bytes)

    /// Builds only synthetic bytes under an isolated caller-owned workspace.
    let createInspectionFixture (root: string) =
        Directory.CreateDirectory(root) |> ignore
        let specRelative = "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json"
        let glbRelative = "tests/Fixtures/Asset3d/positive/family.glb"
        let previewRelative = "tests/Fixtures/Asset3d/positive/preview.png"
        let reportRelative = "tests/Fixtures/Asset3d/positive/technique.json"
        writeBytes root specRelative (File.ReadAllBytes(Path.Combine(repositoryRoot, specRelative)))
        File.Copy(Path.Combine(repositoryRoot, "toolchain.lock.json"), Path.Combine(root, "toolchain.lock.json"), true)
        let validated = BlenderCalibration.validateSpecFile root specRelative
        let glbBytes = syntheticGlb NoMutation validated
        let pngBytes = syntheticPng false
        writeBytes root glbRelative glbBytes
        writeBytes root previewRelative pngBytes
        let glb = Asset3dInspector.inspectGlbBytes validated glbBytes
        let png = Asset3dInspector.inspectPngBytes pngBytes
        let report = reportNode validated glb png
        writeBytes root reportRelative (canonicalNodeBytes report)

        { Validated = validated
          SpecRelative = specRelative
          GlbRelative = glbRelative
          PreviewRelative = previewRelative
          ReportRelative = reportRelative }

    let private withFixture action =
        let root =
            Path.Combine(Path.GetTempPath(), "RiftHarness.Asset3d-" + Guid.NewGuid().ToString("N"))

        try
            let fixture = createInspectionFixture root
            action root fixture
        finally
            if Directory.Exists(root) then
                Directory.Delete(root, true)

    let glbReferenceFixtureIsAccepted () =
        let spec = validated ()
        Asset3dInspector.inspectGlbBytes spec (syntheticGlb NoMutation spec) |> ignore

    let glbTopologyTamperingIsRejected () =
        let spec = validated ()

        for mutation in
            [ InteriorPosition
              DuplicateTriangle
              AdjacentSplit
              ReverseWinding
              WrongNormal
              NegativeZeroFloat ] do
            expectInspectionFailure "INVALID_ARTIFACT" (fun () ->
                Asset3dInspector.inspectGlbBytes spec (syntheticGlb mutation spec) |> ignore)

    let glbAlignmentAndJsonTypesAreRejected () =
        let spec = validated ()

        for mutation in
            [ UnalignedFloatView
              StringSceneNode
              NonObjectNode
              OutOfRangeView
              OutOfRangeIndex
              NonFiniteFloat
              ExternalBufferUri
              UnknownNodeName
              WrongRootTransform
              MissingLodChild
              WrongMaterial
              WrongAccessorBounds ] do
            expectInspectionFailure "INVALID_ARTIFACT" (fun () ->
                Asset3dInspector.inspectGlbBytes spec (syntheticGlb mutation spec) |> ignore)

        let reference = syntheticGlb NoMutation spec

        let wrongJsonPadding = Array.copy reference

        let jsonLength =
            int (BinaryPrimitives.ReadUInt32LittleEndian(wrongJsonPadding.AsSpan(12, 4)))

        if wrongJsonPadding[19 + jsonLength] <> byte ' ' then
            failwith "Synthetic GLB unexpectedly has no JSON padding byte."

        wrongJsonPadding[19 + jsonLength] <- byte '\t'

        expectInspectionFailure "INVALID_ARTIFACT" (fun () ->
            Asset3dInspector.inspectGlbBytes spec wrongJsonPadding |> ignore)

        for offset in [ 0; 16 ] do
            let corrupt = Array.copy reference
            corrupt[offset] <- corrupt[offset] ^^^ 1uy

            expectInspectionFailure "INVALID_ARTIFACT" (fun () ->
                Asset3dInspector.inspectGlbBytes spec corrupt |> ignore)

        for mutation in
            [ TooManyRenderPrimitives
              TooManyMaterials
              TooManyLod0Vertices
              TooManyLod0Triangles
              TooManyLod1Vertices
              TooManyLod1Triangles
              TooManyLod2Vertices
              TooManyLod2Triangles
              TooManyCollisionTriangles ] do
            try
                Asset3dInspector.inspectGlbBytes spec (syntheticGlb mutation spec) |> ignore
                failwith $"Expected BUDGET_EXCEEDED for {mutation}."
            with
            | AssetInspectionError "BUDGET_EXCEEDED" -> ()
            | AssetInspectionError actual -> failwith $"Expected BUDGET_EXCEEDED for {mutation}, got {actual}."

        expectInspectionFailure "BUDGET_EXCEEDED" (fun () ->
            Array.zeroCreate<byte> (Asset3dInspector.MaxGlbBytes + 1)
            |> Asset3dInspector.inspectGlbBytes spec
            |> ignore)

    let normalizedPngIsAccepted () =
        Asset3dInspector.inspectPngBytes (syntheticPng false) |> ignore

    let pngTrailingDeflateDataIsRejected () =
        expectInspectionFailure "INVALID_ARTIFACT" (fun () ->
            Asset3dInspector.inspectPngBytes (syntheticPng true) |> ignore)

        let reference = syntheticPng false

        let replaceIhdrByte offset value =
            let result = Array.copy reference
            result[offset] <- value
            let chunkType = ReadOnlySpan<byte>(result, 12, 4)
            let data = ReadOnlySpan<byte>(result, 16, 13)
            BinaryPrimitives.WriteUInt32BigEndian(result.AsSpan(29, 4), crc32 (chunkType.ToArray()) (data.ToArray()))
            result

        let wrongWidth = Array.copy reference
        BinaryPrimitives.WriteUInt32BigEndian(wrongWidth.AsSpan(16, 4), 959u)

        BinaryPrimitives.WriteUInt32BigEndian(wrongWidth.AsSpan(29, 4), crc32 wrongWidth[12..15] wrongWidth[16..28])

        let wrongCrc = Array.copy reference
        wrongCrc[29] <- wrongCrc[29] ^^^ 1uy

        let metadata =
            Array.concat [| reference[0..32]; pngChunk "tEXt" [| 0uy |]; reference[33..] |]

        let wrongOrder =
            Array.concat [| reference[0..32]; pngChunk "IEND" Array.empty; reference[33..] |]

        for corrupt in
            [ wrongWidth
              replaceIhdrByte 24 16uy
              replaceIhdrByte 25 2uy
              wrongCrc
              metadata
              wrongOrder ] do
            expectInspectionFailure "INVALID_ARTIFACT" (fun () -> Asset3dInspector.inspectPngBytes corrupt |> ignore)

    let completeInspectionRoundTripIsAccepted () =
        withFixture (fun root fixture ->
            let result =
                Asset3dInspector.inspect
                    root
                    fixture.Validated
                    fixture.GlbRelative
                    fixture.PreviewRelative
                    fixture.ReportRelative

            if
                result.FamilyId <> fixture.Validated.Spec.FamilyId
                || result.SpecSha256 <> fixture.Validated.SpecSha256
                || result.DecodedGeometryBytes <> fixture.Validated.FamilyDecodedGeometryBytes
                || result.RenderPrimitiveCount <> fixture.Validated.RenderPrimitiveCount
                || result.MaterialCount <> 2
            then
                failwith "Complete inspector result does not bind the reference fixture.")

    let techniqueReportCrossFieldMatrixIsRejected () =
        withFixture (fun root fixture ->
            let reportPath = Path.Combine(root, fixture.ReportRelative)
            let baseline = File.ReadAllBytes(reportPath)

            let mutations: (JsonObject -> unit) list =
                [ (fun report -> report["specSha256"] <- node (String('0', 64)))
                  (fun report ->
                      let artifacts = report["artifacts"].AsObject()
                      let artifact = artifacts["glb"].AsObject()
                      artifact["sha256"] <- node (String('1', 64)))
                  (fun report ->
                      let artifacts = report["artifacts"].AsObject()
                      let artifact = artifacts["glb"].AsObject()
                      artifact["bytes"] <- node (artifact["bytes"].GetValue<int64>() + 1L))
                  (fun report ->
                      let artifacts = report["artifacts"].AsObject()
                      let artifact = artifacts["glb"].AsObject()
                      artifact["path"] <- node "assets/quarantine/3d/WRONG/family.glb")
                  (fun report ->
                      let artifacts = report["artifacts"].AsObject()
                      let artifact = artifacts["preview"].AsObject()
                      artifact["sha256"] <- node (String('4', 64)))
                  (fun report ->
                      let artifacts = report["artifacts"].AsObject()
                      let artifact = artifacts["preview"].AsObject()
                      artifact["bytes"] <- node (artifact["bytes"].GetValue<int64>() + 1L))
                  (fun report ->
                      let metrics = report["familyMetrics"].AsObject()
                      metrics["decodedGeometryBytes"] <- node (metrics["decodedGeometryBytes"].GetValue<int64>() + 1L))
                  (fun report ->
                      let modules = report["modules"].AsArray()
                      let firstModule = modules[0].AsObject()
                      let metric = firstModule["lod0"].AsObject()
                      metric["vertices"] <- node (metric["vertices"].GetValue<int>() + 1))
                  (fun report ->
                      let modules = report["modules"].AsArray()
                      let firstModule = modules[0].AsObject()
                      let boundsObject = firstModule["boundsMicrometres"].AsObject()
                      let bounds = boundsObject["min"].AsArray()
                      bounds[0] <- node (bounds[0].GetValue<int64>() + 1L))
                  (fun report ->
                      let materials = report["materials"].AsArray()
                      let first = materials[0].DeepClone()
                      let second = materials[1].DeepClone()
                      materials[0] <- second
                      materials[1] <- first)
                  (fun report ->
                      let sources = report["generatorSources"].AsArray()
                      let firstSource = sources[0].AsObject()
                      firstSource["path"] <- node "tools/RiftHarness/unknown.fs")
                  (fun report ->
                      let sources = report["generatorSources"].AsArray()
                      let firstSource = sources[0].AsObject()
                      firstSource["sha256"] <- node (String('5', 64)))
                  (fun report -> report["generatorSourceSha256"] <- node (String('2', 64)))
                  (fun report -> report["toolchainPinSha256"] <- node (String('3', 64)))
                  (fun report -> report["generatedAtUtc"] <- node "2026-08-13T00:00:00Z") ]

            for mutate in mutations do
                let parsed = JsonNode.Parse(Constants.Utf8NoBom.GetString(baseline)).AsObject()
                mutate parsed
                File.WriteAllBytes(reportPath, canonicalNodeBytes parsed)

                expectInspectionFailure "INVALID_ARTIFACT" (fun () ->
                    Asset3dInspector.inspect
                        root
                        fixture.Validated
                        fixture.GlbRelative
                        fixture.PreviewRelative
                        fixture.ReportRelative
                    |> ignore)

            File.WriteAllBytes(reportPath, Array.append baseline [| byte ' ' |])

            expectInspectionFailure "INVALID_ARTIFACT" (fun () ->
                Asset3dInspector.inspect
                    root
                    fixture.Validated
                    fixture.GlbRelative
                    fixture.PreviewRelative
                    fixture.ReportRelative
                |> ignore)

            let negativeZero =
                let text = Constants.Utf8NoBom.GetString(baseline)
                let marker = "\"rotationQuarterTurns\":0"
                let markerIndex = text.IndexOf(marker, StringComparison.Ordinal)

                if markerIndex < 0 then
                    failwith "Synthetic report does not contain a zero quarter-turn."

                text.Remove(markerIndex, marker.Length).Insert(markerIndex, "\"rotationQuarterTurns\":-0")
                |> Constants.Utf8NoBom.GetBytes

            File.WriteAllBytes(reportPath, negativeZero)

            expectInspectionFailure "INVALID_ARTIFACT" (fun () ->
                Asset3dInspector.inspect
                    root
                    fixture.Validated
                    fixture.GlbRelative
                    fixture.PreviewRelative
                    fixture.ReportRelative
                |> ignore))

    let unsafeUnicodeArtifactPathsAreRejected () =
        withFixture (fun root fixture ->
            let invalidPath = "tests/Fixtures/Asset3d/" + String([| '\uD800' |])

            for glbPath, previewPath, reportPath in
                [ invalidPath, fixture.PreviewRelative, fixture.ReportRelative
                  fixture.GlbRelative, invalidPath, fixture.ReportRelative
                  fixture.GlbRelative, fixture.PreviewRelative, invalidPath ] do
                expectPathFailure (fun () ->
                    Asset3dInspector.inspect root fixture.Validated glbPath previewPath reportPath
                    |> ignore))

    let malformedToolchainPinIsArtifactFailure () =
        withFixture (fun root fixture ->
            for malformed in [ "{"; "[]"; "{\"tools\":{}}"; "{\"tools\":[null]}" ] do
                File.WriteAllText(Path.Combine(root, "toolchain.lock.json"), malformed, Constants.Utf8NoBom)

                expectInspectionFailure "INVALID_ARTIFACT" (fun () ->
                    Asset3dInspector.inspect
                        root
                        fixture.Validated
                        fixture.GlbRelative
                        fixture.PreviewRelative
                        fixture.ReportRelative
                    |> ignore))
