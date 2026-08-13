namespace RiftHarness

open System
open System.Buffers.Binary
open System.Collections.Generic
open System.Globalization
open System.IO
open System.IO.Compression
open System.Text
open System.Text.Json

/// A stable artifact failure code suitable for the public CLI envelope.
exception AssetInspectionError of string

/// A stable safe-path failure code suitable for exit code 2.
exception AssetInspectionPathError of string

type InspectedModule =
    { Id: string
      Bounds: CalibrationBox
      Lod0: PrimitiveMetrics
      Lod1: PrimitiveMetrics
      Lod2: PrimitiveMetrics
      Collision: PrimitiveMetrics }

type GlbInspection =
    { Sha256: string
      Bytes: int64
      Modules: InspectedModule array
      DecodedGeometryBytes: int64
      RenderPrimitiveCount: int
      MaterialCount: int }

type PngInspection =
    { Sha256: string
      Bytes: int64
      Width: int
      Height: int }

type Asset3dInspectionResult =
    { FamilyId: string
      SpecSha256: string
      GlbPath: string
      GlbSha256: string
      GlbBytes: int64
      PreviewPath: string
      PreviewSha256: string
      PreviewBytes: int64
      ReportPath: string
      ReportSha256: string
      ReportBytes: int64
      DecodedGeometryBytes: int64
      RenderPrimitiveCount: int
      MaterialCount: int }

[<RequireQualifiedAccess>]
module Asset3dInspector =
    [<Literal>]
    let MaxGlbBytes = 2097152

    [<Literal>]
    let MaxGlbJsonBytes = 1048576

    [<Literal>]
    let MaxPngBytes = 8388608

    [<Literal>]
    let MaxReportBytes = 1048576

    [<Literal>]
    let MaxDecodedGeometryBytes = 2097152L

    [<Literal>]
    let ToolchainPinSha256 =
        "840ca3968e7f20d9e525a2d3a0337e8ba81fad50800942ef299496ae18677d4b"

    let private invalid () =
        raise (AssetInspectionError "INVALID_ARTIFACT")

    let private budget () =
        raise (AssetInspectionError "BUDGET_EXCEEDED")

    let private unsafePath () =
        raise (AssetInspectionPathError "UNSAFE_PATH")

    let private finite (value: float32) =
        not (Single.IsNaN(value) || Single.IsInfinity(value))
        && BitConverter.SingleToInt32Bits(value) <> Int32.MinValue

    let private near tolerance (left: float) (right: float) =
        Double.IsFinite(left)
        && Double.IsFinite(right)
        && abs (left - right) <= tolerance

    let private exactFields (required: string array) (optional: string array) (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then
            invalid ()

        let requiredSet = HashSet<string>(required, StringComparer.Ordinal)
        let allowed = HashSet<string>(required, StringComparer.Ordinal)
        optional |> Array.iter (allowed.Add >> ignore)
        let seen = HashSet<string>(StringComparer.Ordinal)

        for property in element.EnumerateObject() do
            if not (allowed.Contains(property.Name)) || not (seen.Add(property.Name)) then
                invalid ()

        if requiredSet |> Seq.exists (seen.Contains >> not) then
            invalid ()

    let private property (name: string) (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then
            invalid ()

        match element.TryGetProperty(name) with
        | true, value -> value
        | _ -> invalid ()

    let private stringValue (name: string) (element: JsonElement) =
        let value = property name element

        if value.ValueKind <> JsonValueKind.String || isNull (value.GetString()) then
            invalid ()

        value.GetString()

    let private integer (name: string) (element: JsonElement) =
        let value = property name element

        if value.ValueKind <> JsonValueKind.Number then
            invalid ()

        match value.TryGetInt32() with
        | true, parsed when
            value.GetRawText().IndexOfAny([| '.'; 'e'; 'E' |]) < 0
            && value.GetRawText() <> "-0"
            ->
            parsed
        | _ -> invalid ()

    let private integer64 (name: string) (element: JsonElement) =
        let value = property name element

        if value.ValueKind <> JsonValueKind.Number then
            invalid ()

        match value.TryGetInt64() with
        | true, parsed when
            value.GetRawText().IndexOfAny([| '.'; 'e'; 'E' |]) < 0
            && value.GetRawText() <> "-0"
            ->
            parsed
        | _ -> invalid ()

    let private integerElement (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Number then
            invalid ()

        match element.TryGetInt32() with
        | true, parsed when
            element.GetRawText().IndexOfAny([| '.'; 'e'; 'E' |]) < 0
            && element.GetRawText() <> "-0"
            ->
            parsed
        | _ -> invalid ()

    let private singleValue (name: string) (element: JsonElement) =
        let value = property name element

        if value.ValueKind <> JsonValueKind.Number then
            invalid ()

        match value.TryGetSingle() with
        | true, parsed when finite parsed -> parsed
        | _ -> invalid ()

    let private integerArray (expectedLength: int) (element: JsonElement) =
        if
            element.ValueKind <> JsonValueKind.Array
            || element.GetArrayLength() <> expectedLength
        then
            invalid ()

        element.EnumerateArray()
        |> Seq.map (fun value ->
            if value.ValueKind <> JsonValueKind.Number then
                invalid ()

            match value.TryGetInt64() with
            | true, parsed when
                value.GetRawText().IndexOfAny([| '.'; 'e'; 'E' |]) < 0
                && value.GetRawText() <> "-0"
                ->
                parsed
            | _ -> invalid ())
        |> Seq.toArray

    let private int32Array (expectedLength: int) (element: JsonElement) =
        integerArray expectedLength element
        |> Array.map (fun value ->
            if value < int64 Int32.MinValue || value > int64 Int32.MaxValue then
                invalid ()

            int value)

    let private singleArray (expectedLength: int) (element: JsonElement) =
        if
            element.ValueKind <> JsonValueKind.Array
            || element.GetArrayLength() <> expectedLength
        then
            invalid ()

        element.EnumerateArray()
        |> Seq.map (fun value ->
            if value.ValueKind <> JsonValueKind.Number then
                invalid ()

            match value.TryGetSingle() with
            | true, parsed when finite parsed -> parsed
            | _ -> invalid ())
        |> Seq.toArray

    let private ensureUniqueJsonKeys maxDepth (bytes: byte array) =
        let options =
            JsonReaderOptions(
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = maxDepth
            )

        let mutable reader = Utf8JsonReader(ReadOnlySpan<byte>(bytes), options)
        let keys = Stack<HashSet<string>>()

        while reader.Read() do
            match reader.TokenType with
            | JsonTokenType.StartObject -> keys.Push(HashSet<string>(StringComparer.Ordinal))
            | JsonTokenType.EndObject -> keys.Pop() |> ignore
            | JsonTokenType.PropertyName ->
                if keys.Count = 0 || not (keys.Peek().Add(reader.GetString())) then
                    invalid ()
            | _ -> ()

    let private isCanonicalRelativePath (relativePath: string) =
        if String.IsNullOrEmpty(relativePath) then
            false
        else
            let segments = relativePath.Split('/')

            Constants.Utf8NoBom.GetByteCount(relativePath) <= 240
            && not (Path.IsPathRooted(relativePath))
            && not (relativePath.Contains('\\'))
            && not (relativePath.Contains(':'))
            && relativePath = relativePath.Normalize(NormalizationForm.FormC)
            && segments
               |> Array.forall (fun segment ->
                   not (String.IsNullOrEmpty(segment))
                   && segment <> "."
                   && segment <> ".."
                   && Constants.Utf8NoBom.GetByteCount(segment) <= 80
                   && segment |> Seq.forall (Char.IsControl >> not))

    let private isAllowedArtifactPath (relativePath: string) =
        if not (isCanonicalRelativePath relativePath) then
            false
        elif
            relativePath.StartsWith("tests/Fixtures/Asset3d/", StringComparison.Ordinal)
            || relativePath.StartsWith("assets/quarantine/3d/", StringComparison.Ordinal)
        then
            true
        elif relativePath.StartsWith(".ai/runtime/asset-jobs/", StringComparison.Ordinal) then
            let segments = relativePath.Split('/')
            segments.Length >= 6 && Internal.isRunId segments[3]
        else
            false

    let private validateWorkspaceRoot (root: string) =
        try
            if String.IsNullOrWhiteSpace(root) then
                unsafePath ()

            let absolute = Path.GetFullPath(root)
            let pathRoot = Path.GetPathRoot(absolute)

            if String.IsNullOrEmpty(pathRoot) then
                unsafePath ()

            let relative = Path.GetRelativePath(pathRoot, absolute)
            let mutable current = pathRoot

            for segment in relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) do
                current <- Path.Combine(current, segment)
                let info = DirectoryInfo(current)

                if
                    not info.Exists
                    || not (isNull info.LinkTarget)
                    || info.Attributes.HasFlag(FileAttributes.ReparsePoint)
                then
                    unsafePath ()

            absolute
        with
        | AssetInspectionPathError _ -> reraise ()
        | :? IOException
        | :? UnauthorizedAccessException
        | :? NotSupportedException
        | :? System.Security.SecurityException -> unsafePath ()

    let private readRegularFile root relativePath maximumBytes =
        try
            if not (isAllowedArtifactPath relativePath) then
                unsafePath ()

            let safeRoot = validateWorkspaceRoot root
            let locations = Workspace.paths safeRoot
            let absolute = Path.Combine(locations.Root, relativePath)
            let safe = Workspace.requireSafePath locations "3D-Artefakt" false absolute
            let initial = File.GetAttributes(safe)

            if
                initial.HasFlag(FileAttributes.Directory)
                || initial.HasFlag(FileAttributes.ReparsePoint)
            then
                unsafePath ()

            use stream =
                new FileStream(
                    safe,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    64 * 1024,
                    FileOptions.SequentialScan
                )

            let expected = stream.Length

            if expected <= 0L || expected > int64 maximumBytes then
                if expected > int64 maximumBytes && maximumBytes = MaxGlbBytes then
                    budget ()
                else
                    invalid ()

            let bytes = Array.zeroCreate<byte> (int expected)
            let mutable offset = 0

            while offset < bytes.Length do
                let count = stream.Read(bytes, offset, bytes.Length - offset)

                if count = 0 then
                    unsafePath ()

                offset <- offset + count

            if stream.ReadByte() <> -1 || stream.Length <> expected then
                unsafePath ()

            let final = File.GetAttributes(safe)
            let finalSafe = Workspace.requireSafePath locations "3D-Artefakt" false absolute

            if
                not (String.Equals(safe, finalSafe, StringComparison.Ordinal))
                || not (String.Equals(Path.GetFullPath(absolute), finalSafe, StringComparison.Ordinal))
                || stream.Length <> expected
                || final.HasFlag(FileAttributes.Directory)
                || final.HasFlag(FileAttributes.ReparsePoint)
                || not (File.Exists(safe))
            then
                unsafePath ()

            bytes
        with
        | AssetInspectionError _
        | AssetInspectionPathError _ -> reraise ()
        | HarnessException _ -> unsafePath ()
        | :? ArgumentException
        | :? EncoderFallbackException -> unsafePath ()
        | :? IOException
        | :? UnauthorizedAccessException
        | :? NotSupportedException
        | :? System.Security.SecurityException -> unsafePath ()

    type private BufferViewInfo =
        { Buffer: int
          Offset: int
          Length: int
          Target: int option }

    type private AccessorInfo =
        { View: int
          ComponentType: int
          Count: int
          Kind: string
          Minimum: float32 array option
          Maximum: float32 array option }

    type private BoundsAccumulator() =
        let mutable hasValue = false
        let mutable minX = Double.PositiveInfinity
        let mutable minY = Double.PositiveInfinity
        let mutable minZ = Double.PositiveInfinity
        let mutable maxX = Double.NegativeInfinity
        let mutable maxY = Double.NegativeInfinity
        let mutable maxZ = Double.NegativeInfinity

        member _.AddGltf(x: float32, y: float32, z: float32) =
            let bx = float x * 1000000.0
            let by = float -z * 1000000.0
            let bz = float y * 1000000.0
            hasValue <- true
            minX <- min minX bx
            minY <- min minY by
            minZ <- min minZ bz
            maxX <- max maxX bx
            maxY <- max maxY by
            maxZ <- max maxZ bz

        member _.Compare(expected: CalibrationBox) =
            if
                not hasValue
                || not (near 1.0 minX (float expected.Min.X))
                || not (near 1.0 minY (float expected.Min.Y))
                || not (near 1.0 minZ (float expected.Min.Z))
                || not (near 1.0 maxX (float expected.Max.X))
                || not (near 1.0 maxY (float expected.Max.Y))
                || not (near 1.0 maxZ (float expected.Max.Z))
            then
                invalid ()

    let private checkedRange (offset: int) (length: int) (limit: int) =
        if offset < 0 || length < 0 || offset > limit || length > limit - offset then
            invalid ()

    let private accessorComponents (kind: string) =
        match kind with
        | "SCALAR" -> 1
        | "VEC2" -> 2
        | "VEC3" -> 3
        | _ -> invalid ()

    let private componentBytes (componentType: int) =
        match componentType with
        | 5123 -> 2
        | 5126 -> 4
        | _ -> invalid ()

    let private expectedAccessorBytes (accessor: AccessorInfo) =
        let size =
            int64 accessor.Count
            * int64 (accessorComponents accessor.Kind)
            * int64 (componentBytes accessor.ComponentType)

        if size > int64 Int32.MaxValue then
            invalid ()

        int size

    let private validateMetric (expected: PrimitiveMetrics) (actual: PrimitiveMetrics) (lod: string) =
        let overBudget =
            match lod with
            | "lod0" -> actual.Vertices > 3072 || actual.Triangles > 4096
            | "lod1" -> actual.Vertices > 1024 || actual.Triangles > 1024
            | "lod2" -> actual.Vertices > 256 || actual.Triangles > 192
            | "collision" -> actual.Triangles > 48
            | _ -> invalid ()

        if overBudget || (lod <> "collision" && actual.Primitives > 2) then
            budget ()

        if actual <> expected then
            invalid ()

    let private addMetric (left: PrimitiveMetrics) (right: PrimitiveMetrics) =
        { Vertices = left.Vertices + right.Vertices
          Indices = left.Indices + right.Indices
          Triangles = left.Triangles + right.Triangles
          Primitives = left.Primitives + right.Primitives
          DecodedGeometryBytes = left.DecodedGeometryBytes + right.DecodedGeometryBytes }

    let private emptyMetric =
        { Vertices = 0
          Indices = 0
          Triangles = 0
          Primitives = 0
          DecodedGeometryBytes = 0L }

    let private parseGlbDocument (bytes: byte array) =
        if isNull bytes || bytes.Length < 28 then
            invalid ()

        if bytes.Length > MaxGlbBytes then
            budget ()

        let span = ReadOnlySpan<byte>(bytes)

        if
            BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(0, 4)) <> 0x46546C67u
            || BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(4, 4)) <> 2u
            || BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(8, 4)) <> uint32 bytes.Length
        then
            invalid ()

        let jsonLength = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(12, 4))

        if
            jsonLength = 0u
            || jsonLength > uint32 MaxGlbJsonBytes
            || jsonLength % 4u <> 0u
            || BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(16, 4)) <> 0x4E4F534Au
        then
            invalid ()

        let binHeader = 20L + int64 jsonLength

        if binHeader > int64 bytes.Length - 8L then
            invalid ()

        let binHeaderInt = int binHeader
        let binLength = BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(binHeaderInt, 4))

        if
            binLength % 4u <> 0u
            || BinaryPrimitives.ReadUInt32LittleEndian(span.Slice(binHeaderInt + 4, 4))
               <> 0x004E4942u
            || binHeader + 8L + int64 binLength <> int64 bytes.Length
        then
            invalid ()

        let rawJson = bytes.AsSpan(20, int jsonLength).ToArray()
        let mutable jsonEnd = rawJson.Length

        let isJsonWhitespace value =
            value = byte ' ' || value = byte '\t' || value = byte '\r' || value = byte '\n'

        while jsonEnd > 0 && isJsonWhitespace rawJson[jsonEnd - 1] do
            if rawJson[jsonEnd - 1] <> byte ' ' then
                invalid ()

            jsonEnd <- jsonEnd - 1

        if jsonEnd = 0 then
            invalid ()

        // GLB 2.0 permits only ASCII space (0x20) as JSON-chunk padding.
        // JsonDocument would otherwise also accept tab/CR/LF as JSON whitespace.
        for index = jsonEnd to rawJson.Length - 1 do
            if rawJson[index] <> byte ' ' then
                invalid ()

        let json = rawJson.AsSpan(0, jsonEnd).ToArray()
        ensureUniqueJsonKeys 32 json

        let document =
            JsonDocument.Parse(
                ReadOnlyMemory<byte>(json),
                JsonDocumentOptions(
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 32
                )
            )

        let bin = bytes.AsSpan(binHeaderInt + 8, int binLength).ToArray()
        document, bin

    let private parseBufferViews (root: JsonElement) (binLength: int) (declaredBufferLength: int) =
        let views = property "bufferViews" root

        if views.ValueKind <> JsonValueKind.Array || views.GetArrayLength() > 128 then
            invalid ()

        let parsed =
            views.EnumerateArray()
            |> Seq.map (fun (view: JsonElement) ->
                exactFields [| "buffer"; "byteLength" |] [| "byteOffset"; "target" |] view
                let buffer = integer "buffer" view
                let length = integer "byteLength" view

                let offset =
                    match view.TryGetProperty("byteOffset") with
                    | true, value ->
                        let parsed = integer "byteOffset" view

                        if parsed < 0 then
                            invalid ()

                        parsed
                    | _ -> 0

                let target =
                    match view.TryGetProperty("target") with
                    | true, _ -> Some(integer "target" view)
                    | _ -> None

                if buffer <> 0 || length <= 0 then
                    invalid ()

                checkedRange offset length declaredBufferLength

                { Buffer = buffer
                  Offset = offset
                  Length = length
                  Target = target })
            |> Seq.toArray

        parsed
        |> Array.sortBy (fun item -> item.Offset)
        |> Array.pairwise
        |> Array.iter (fun (left, right) ->
            if left.Offset + left.Length > right.Offset then
                invalid ())

        if declaredBufferLength > binLength || binLength - declaredBufferLength > 3 then
            invalid ()

        parsed

    let private parseAccessors (root: JsonElement) =
        let accessors = property "accessors" root

        if accessors.ValueKind <> JsonValueKind.Array || accessors.GetArrayLength() > 128 then
            invalid ()

        accessors.EnumerateArray()
        |> Seq.map (fun (accessor: JsonElement) ->
            exactFields [| "bufferView"; "componentType"; "count"; "type" |] [| "min"; "max" |] accessor

            let view = integer "bufferView" accessor
            let componentType = integer "componentType" accessor
            let count = integer "count" accessor
            let kind = stringValue "type" accessor

            if view < 0 || count <= 0 then
                invalid ()

            let components = accessorComponents kind
            componentBytes componentType |> ignore

            let readOptional (name: string) =
                match accessor.TryGetProperty(name) with
                | true, value -> Some(singleArray components value)
                | _ -> None

            { View = view
              ComponentType = componentType
              Count = count
              Kind = kind
              Minimum = readOptional "min"
              Maximum = readOptional "max" })
        |> Seq.toArray

    let private validateMaterial
        (expectedName: string)
        (expectedColor: int array)
        (metallic: int)
        (roughness: int)
        (element: JsonElement)
        =
        exactFields [| "name"; "pbrMetallicRoughness" |] Array.empty element

        if stringValue "name" element <> expectedName then
            invalid ()

        let pbr = property "pbrMetallicRoughness" element

        exactFields [| "baseColorFactor"; "metallicFactor"; "roughnessFactor" |] Array.empty pbr

        let factor = singleArray 4 (property "baseColorFactor" pbr)

        for index = 0 to 2 do
            let expected = float32 (BlenderCalibration.srgb8ToLinear expectedColor[index])

            if not (near 0.000001 (float factor[index]) (float expected)) then
                invalid ()

        if
            not (near 0.000001 (float factor[3]) 1.0)
            || not (near 0.000001 (float (singleValue "metallicFactor" pbr)) (float metallic / 1000.0))
            || not (near 0.000001 (float (singleValue "roughnessFactor" pbr)) (float roughness / 1000.0))
        then
            invalid ()

    let private inspectGlbCore (validated: ValidatedCalibrationSpec) (bytes: byte array) =
        try
            let document, bin = parseGlbDocument bytes

            use document = document
            let root = document.RootElement

            exactFields
                [| "accessors"
                   "asset"
                   "bufferViews"
                   "buffers"
                   "materials"
                   "meshes"
                   "nodes"
                   "scene"
                   "scenes" |]
                Array.empty
                root

            let asset = property "asset" root
            exactFields [| "generator"; "version" |] Array.empty asset

            if
                stringValue "version" asset <> "2.0"
                || stringValue "generator" asset <> "Riftward .NET Asset Generator v1"
                || integer "scene" root <> 0
            then
                invalid ()

            let buffers = property "buffers" root

            if buffers.ValueKind <> JsonValueKind.Array || buffers.GetArrayLength() <> 1 then
                invalid ()

            let buffer = buffers[0]
            exactFields [| "byteLength" |] Array.empty buffer
            let declaredBufferLength = integer "byteLength" buffer

            if declaredBufferLength <= 0 then
                invalid ()

            let views = parseBufferViews root bin.Length declaredBufferLength

            for index = declaredBufferLength to bin.Length - 1 do
                if bin[index] <> 0uy then
                    invalid ()

            let accessors = parseAccessors root

            if accessors.Length = 0 then
                invalid ()

            for accessor in accessors do
                if
                    accessor.View >= views.Length
                    || expectedAccessorBytes accessor <> views[accessor.View].Length
                    || views[accessor.View].Offset % componentBytes accessor.ComponentType <> 0
                then
                    invalid ()

            let viewUse = HashSet<int>()

            for accessor in accessors do
                if not (viewUse.Add(accessor.View)) then
                    invalid ()

            let materials = property "materials" root

            if materials.ValueKind <> JsonValueKind.Array then
                invalid ()

            if materials.GetArrayLength() > 2 then
                budget ()

            if materials.GetArrayLength() <> 2 then
                invalid ()

            validateMaterial
                "MAT_CAL_STONE"
                validated.Spec.Materials.StoneBaseColorSrgb8
                validated.Spec.Materials.StoneMetallicPermille
                validated.Spec.Materials.StoneRoughnessPermille
                materials[0]

            validateMaterial
                "MAT_CAL_WOOD"
                validated.Spec.Materials.WoodBaseColorSrgb8
                validated.Spec.Materials.WoodMetallicPermille
                validated.Spec.Materials.WoodRoughnessPermille
                materials[1]

            let meshes = property "meshes" root

            if meshes.ValueKind <> JsonValueKind.Array || meshes.GetArrayLength() > 16 then
                invalid ()

            if meshes.GetArrayLength() <> 12 then
                invalid ()

            let expectedMeshNames =
                [| for moduleId in BlenderCalibration.moduleOrder do
                       let token = moduleId.Replace('-', '_')
                       yield $"MESH_{token}_LOD0"
                       yield $"MESH_{token}_LOD1"
                       yield $"MESH_{token}_LOD2"
                       yield $"COL_{token}" |]

            let meshByName = Dictionary<string, int>(StringComparer.Ordinal)

            for index = 0 to meshes.GetArrayLength() - 1 do
                let mesh = meshes[index]
                exactFields [| "name"; "primitives" |] Array.empty mesh
                let name = stringValue "name" mesh

                if
                    not (Array.contains name expectedMeshNames)
                    || not (meshByName.TryAdd(name, index))
                then
                    invalid ()

            let nodes = property "nodes" root

            if nodes.ValueKind <> JsonValueKind.Array || nodes.GetArrayLength() > 64 then
                invalid ()

            if nodes.GetArrayLength() <> 21 then
                invalid ()

            let expectedNodeNames =
                [| for moduleId in BlenderCalibration.moduleOrder do
                       let token = moduleId.Replace('-', '_')
                       yield $"MOD_{token}"
                       yield $"MESH_{token}_LOD0"
                       yield $"MESH_{token}_LOD1"
                       yield $"MESH_{token}_LOD2"
                       yield $"COL_{token}"
                       yield $"SNAP_{token}_A"
                       yield $"SNAP_{token}_B" |]

            let nodeByName = Dictionary<string, int>(StringComparer.Ordinal)

            for index = 0 to nodes.GetArrayLength() - 1 do
                let node = nodes[index]
                let name = stringValue "name" node

                if
                    not (Array.contains name expectedNodeNames)
                    || not (nodeByName.TryAdd(name, index))
                then
                    invalid ()

            let parents = Array.zeroCreate<int> (nodes.GetArrayLength())

            for index = 0 to nodes.GetArrayLength() - 1 do
                let node = nodes[index]

                match node.TryGetProperty("children") with
                | true, children ->
                    if children.ValueKind <> JsonValueKind.Array then
                        invalid ()

                    for child in children.EnumerateArray() do
                        let childIndex = integerElement child

                        if childIndex >= 0 && childIndex < nodes.GetArrayLength() then
                            parents[childIndex] <- parents[childIndex] + 1

                            if parents[childIndex] > 1 || childIndex = index then
                                invalid ()
                        else
                            invalid ()
                | _ -> ()

            let scenes = property "scenes" root

            if scenes.ValueKind <> JsonValueKind.Array || scenes.GetArrayLength() <> 1 then
                invalid ()

            let scene = scenes[0]
            exactFields [| "name"; "nodes" |] Array.empty scene

            if stringValue "name" scene <> "SCENE_CAL_STONEWOOD_V1" then
                invalid ()

            let sceneNodes = property "nodes" scene

            if sceneNodes.ValueKind <> JsonValueKind.Array || sceneNodes.GetArrayLength() <> 3 then
                invalid ()

            let rootIndices =
                BlenderCalibration.moduleOrder
                |> Array.map (fun id -> nodeByName[$"MOD_{id.Replace('-', '_')}"])

            for index = 0 to 2 do
                if
                    integerElement sceneNodes[index] <> rootIndices[index]
                    || parents[rootIndices[index]] <> 0
                then
                    invalid ()

            for index = 0 to parents.Length - 1 do
                if not (Array.contains index rootIndices) && parents[index] <> 1 then
                    invalid ()

            let accessorUse = HashSet<int>()

            let accessor index =
                if index < 0 || index >= accessors.Length || not (accessorUse.Add(index)) then
                    invalid ()

                accessors[index]

            let readFloats (semantic: string) (accessorIndex: int) (expectedKind: string) (expectedTarget: int) =
                let item = accessor accessorIndex

                if item.ComponentType <> 5126 || item.Kind <> expectedKind then
                    invalid ()

                match views[item.View].Target with
                | Some target when target <> expectedTarget -> invalid ()
                | Some _
                | None -> ()

                let view = views[item.View]
                let components = accessorComponents item.Kind
                let values = Array.zeroCreate<float32> (item.Count * components)

                for index = 0 to values.Length - 1 do
                    let offset = view.Offset + index * 4

                    values[index] <-
                        BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(bin.AsSpan(offset, 4)))

                    if not (finite values[index]) then
                        invalid ()

                if semantic = "POSITION" then
                    match item.Minimum, item.Maximum with
                    | Some minimum, Some maximum ->
                        for axisIndex = 0 to 2 do
                            let actualMinimum =
                                [| for index in 0..components .. values.Length - components -> values[index + axisIndex] |]
                                |> Array.min

                            let actualMaximum =
                                [| for index in 0..components .. values.Length - components -> values[index + axisIndex] |]
                                |> Array.max

                            if
                                not (near 0.000001 (float minimum[axisIndex]) (float actualMinimum))
                                || not (near 0.000001 (float maximum[axisIndex]) (float actualMaximum))
                            then
                                invalid ()
                    | _ -> invalid ()
                elif item.Minimum.IsSome || item.Maximum.IsSome then
                    invalid ()

                item, values

            let readIndices (accessorIndex: int) (vertexCount: int) =
                let item = accessor accessorIndex

                if
                    item.ComponentType <> 5123
                    || item.Kind <> "SCALAR"
                    || item.Minimum.IsSome
                    || item.Maximum.IsSome
                then
                    invalid ()

                match views[item.View].Target with
                | Some target when target <> 34963 -> invalid ()
                | Some _
                | None -> ()

                if item.Count % 3 <> 0 then
                    invalid ()

                let view = views[item.View]
                let indices = Array.zeroCreate<int> item.Count

                for index = 0 to indices.Length - 1 do
                    indices[index] <-
                        int (BinaryPrimitives.ReadUInt16LittleEndian(bin.AsSpan(view.Offset + index * 2, 2)))

                    if indices[index] >= vertexCount then
                        invalid ()

                for triangle = 0 to indices.Length / 3 - 1 do
                    let offset = triangle * 3

                    if
                        indices[offset] = indices[offset + 1]
                        || indices[offset] = indices[offset + 2]
                        || indices[offset + 1] = indices[offset + 2]
                    then
                        invalid ()

                item

            let validateBoxTopology
                (expectedBoxes: CalibrationBox array)
                (positions: float32 array)
                (normals: float32 array)
                (uvs: float32 array option)
                (indices: int array)
                =
                if
                    positions.Length <> expectedBoxes.Length * 24 * 3
                    || normals.Length <> positions.Length
                    || indices.Length <> expectedBoxes.Length * 36
                    || uvs
                       |> Option.exists (fun values -> values.Length <> expectedBoxes.Length * 24 * 2)
                then
                    invalid ()

                let blenderPosition vertex =
                    let offset = vertex * 3

                    float positions[offset] * 1000000.0,
                    float -positions[offset + 2] * 1000000.0,
                    float positions[offset + 1] * 1000000.0

                let blenderNormal vertex =
                    let offset = vertex * 3
                    float normals[offset], float -normals[offset + 2], float normals[offset + 1]

                let coordinates (x, y, z) = [| x; y; z |]
                let faceKeys = Array.init expectedBoxes.Length (fun _ -> HashSet<int * int>())

                let triangleCounts =
                    Array.init expectedBoxes.Length (fun _ -> Array.zeroCreate<int> 6)

                let triangleVertices =
                    Array.init expectedBoxes.Length (fun _ -> Array.init 6 (fun _ -> HashSet<int>()))

                let faceEdges =
                    Array.init expectedBoxes.Length (fun _ -> Array.init 6 (fun _ -> Dictionary<int * int, int>()))

                for boxIndex = 0 to expectedBoxes.Length - 1 do
                    let expected = expectedBoxes[boxIndex]
                    let minimum = [| float expected.Min.X; float expected.Min.Y; float expected.Min.Z |]
                    let maximum = [| float expected.Max.X; float expected.Max.Y; float expected.Max.Z |]

                    for faceIndex = 0 to 5 do
                        let vertices = [| for local in 0..3 -> boxIndex * 24 + faceIndex * 4 + local |]
                        let points = vertices |> Array.map (blenderPosition >> coordinates)

                        for point in points do
                            for axisIndex = 0 to 2 do
                                if
                                    not (near 1.0 point[axisIndex] minimum[axisIndex])
                                    && not (near 1.0 point[axisIndex] maximum[axisIndex])
                                then
                                    invalid ()

                        let constantAxes =
                            [| for axisIndex = 0 to 2 do
                                   if
                                       points
                                       |> Array.forall (fun point -> near 1.0 point.[axisIndex] points.[0].[axisIndex])
                                   then
                                       yield axisIndex |]

                        if constantAxes.Length <> 1 then
                            invalid ()

                        let axisIndex = constantAxes[0]

                        let sign =
                            if near 1.0 points.[0].[axisIndex] minimum.[axisIndex] then
                                -1
                            elif near 1.0 points.[0].[axisIndex] maximum.[axisIndex] then
                                1
                            else
                                invalid ()

                        if not (faceKeys[boxIndex].Add((axisIndex, sign))) then
                            invalid ()

                        let varyingAxes = [| 0; 1; 2 |] |> Array.filter ((<>) axisIndex)
                        let corners = HashSet<int * int>()

                        for point in points do
                            let corner axis =
                                if near 1.0 point[axis] minimum[axis] then 0
                                elif near 1.0 point[axis] maximum[axis] then 1
                                else invalid ()

                            corners.Add((corner varyingAxes[0], corner varyingAxes[1])) |> ignore

                        if corners.Count <> 4 then
                            invalid ()

                        for vertex in vertices do
                            let actual = blenderNormal vertex |> coordinates

                            for normalAxis = 0 to 2 do
                                let expectedNormal = if normalAxis = axisIndex then float sign else 0.0

                                if not (near 0.000001 actual[normalAxis] expectedNormal) then
                                    invalid ()

                        match uvs with
                        | Some values ->
                            let uvCorners = HashSet<int * int>()

                            for vertex in vertices do
                                let offset = vertex * 2

                                let bit value =
                                    if near 0.000001 (float value) 0.0 then 0
                                    elif near 0.000001 (float value) 1.0 then 1
                                    else invalid ()

                                uvCorners.Add((bit values[offset], bit values[offset + 1])) |> ignore

                            if uvCorners.Count <> 4 then
                                invalid ()
                        | None -> ()

                    if faceKeys[boxIndex].Count <> 6 then
                        invalid ()

                let uniqueTriangles = HashSet<int * int * int>()

                for triangle = 0 to indices.Length / 3 - 1 do
                    let first = indices[triangle * 3]
                    let second = indices[triangle * 3 + 1]
                    let third = indices[triangle * 3 + 2]
                    let boxIndex = first / 24
                    let faceIndex = (first % 24) / 4

                    if
                        boxIndex >= expectedBoxes.Length
                        || second / 24 <> boxIndex
                        || third / 24 <> boxIndex
                        || (second % 24) / 4 <> faceIndex
                        || (third % 24) / 4 <> faceIndex
                    then
                        invalid ()

                    let sorted = [| first; second; third |] |> Array.sort

                    if not (uniqueTriangles.Add((sorted[0], sorted[1], sorted[2]))) then
                        invalid ()

                    triangleCounts.[boxIndex].[faceIndex] <- triangleCounts.[boxIndex].[faceIndex] + 1
                    triangleVertices.[boxIndex].[faceIndex].Add(first) |> ignore
                    triangleVertices.[boxIndex].[faceIndex].Add(second) |> ignore
                    triangleVertices.[boxIndex].[faceIndex].Add(third) |> ignore

                    for left, right in [| first, second; second, third; third, first |] do
                        let edge = if left < right then left, right else right, left
                        let edges = faceEdges.[boxIndex].[faceIndex]

                        match edges.TryGetValue(edge) with
                        | true, count -> edges[edge] <- count + 1
                        | _ -> edges.Add(edge, 1)

                    let ax, ay, az = blenderPosition first
                    let bx, by, bz = blenderPosition second
                    let cx, cy, cz = blenderPosition third
                    let ux, uy, uz = bx - ax, by - ay, bz - az
                    let vx, vy, vz = cx - ax, cy - ay, cz - az
                    let cross = uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx
                    let nx, ny, nz = blenderNormal first

                    let dot =
                        let crossX, crossY, crossZ = cross
                        crossX * nx + crossY * ny + crossZ * nz

                    if not (Double.IsFinite(dot)) || dot <= 0.0 then
                        invalid ()

                for boxIndex = 0 to expectedBoxes.Length - 1 do
                    for faceIndex = 0 to 5 do
                        if
                            triangleCounts.[boxIndex].[faceIndex] <> 2
                            || triangleVertices.[boxIndex].[faceIndex].Count <> 4
                        then
                            invalid ()

                        let edges = faceEdges.[boxIndex].[faceIndex]
                        let counts = edges.Values |> Seq.sort |> Seq.toArray

                        if counts <> [| 1; 1; 1; 1; 2 |] then
                            invalid ()

                        let diagonal = edges |> Seq.find (fun pair -> pair.Value = 2)
                        let firstPoint = diagonal.Key |> fst |> blenderPosition |> coordinates
                        let secondPoint = diagonal.Key |> snd |> blenderPosition |> coordinates

                        let differingAxes =
                            [| for axisIndex = 0 to 2 do
                                   if not (near 1.0 firstPoint.[axisIndex] secondPoint.[axisIndex]) then
                                       yield axisIndex |]

                        if differingAxes.Length <> 2 then
                            invalid ()

            let expectedGeometry = BlenderCalibration.deriveReferenceGeometry validated.Spec

            let snapReferences = BlenderCalibration.snapPoints validated.Spec |> dict
            let inspected = ResizeArray<InspectedModule>()
            let mutable renderPrimitiveCount = 0

            for moduleIndex = 0 to BlenderCalibration.moduleOrder.Length - 1 do
                let moduleId = BlenderCalibration.moduleOrder[moduleIndex]
                let token = moduleId.Replace('-', '_')
                let rootIndex = nodeByName[$"MOD_{token}"]
                let rootNode = nodes[rootIndex]
                exactFields [| "children"; "name" |] Array.empty rootNode
                let children = property "children" rootNode

                if children.ValueKind <> JsonValueKind.Array || children.GetArrayLength() <> 6 then
                    invalid ()

                let childNames =
                    [| $"MESH_{token}_LOD0"
                       $"MESH_{token}_LOD1"
                       $"MESH_{token}_LOD2"
                       $"COL_{token}"
                       $"SNAP_{token}_A"
                       $"SNAP_{token}_B" |]

                for childIndex = 0 to 5 do
                    let actualIndex = integerElement children[childIndex]

                    if actualIndex <> nodeByName[childNames[childIndex]] then
                        invalid ()

                let expectedModule = validated.Modules[moduleIndex]
                let bounds = BoundsAccumulator()

                let inspectPrimitive
                    (isCollision: bool)
                    (expectedMaterial: int)
                    (expectedBoxes: CalibrationBox array)
                    (primitive: JsonElement)
                    =
                    if isCollision then
                        exactFields [| "attributes"; "indices" |] Array.empty primitive
                    else
                        exactFields [| "attributes"; "indices"; "material" |] Array.empty primitive

                    let attributes = property "attributes" primitive

                    if isCollision then
                        exactFields [| "NORMAL"; "POSITION" |] Array.empty attributes
                    else
                        exactFields [| "NORMAL"; "POSITION"; "TEXCOORD_0" |] Array.empty attributes

                    if not isCollision && integer "material" primitive <> expectedMaterial then
                        invalid ()

                    let positionAccessor, positions =
                        readFloats "POSITION" (integer "POSITION" attributes) "VEC3" 34962

                    let normalAccessor, normals =
                        readFloats "NORMAL" (integer "NORMAL" attributes) "VEC3" 34962

                    if normalAccessor.Count <> positionAccessor.Count then
                        invalid ()

                    for index = 0 to normalAccessor.Count - 1 do
                        let x = normals[index * 3]
                        let y = normals[index * 3 + 1]
                        let z = normals[index * 3 + 2]
                        let lengthSquared = float x * float x + float y * float y + float z * float z

                        let axisComponents =
                            [| abs x; abs y; abs z |] |> Array.filter (fun value -> value > 0.000001f)

                        if not (near 0.0001 lengthSquared 1.0) || axisComponents.Length <> 1 then
                            invalid ()

                    let uvs =
                        if not isCollision then
                            let uvAccessor, values =
                                readFloats "TEXCOORD_0" (integer "TEXCOORD_0" attributes) "VEC2" 34962

                            if uvAccessor.Count <> positionAccessor.Count then
                                invalid ()

                            if values |> Array.exists (fun value -> value < 0.0f || value > 1.0f) then
                                invalid ()

                            Some values
                        else
                            None

                    let indexAccessor = readIndices (integer "indices" primitive) positionAccessor.Count
                    let indexView = views[indexAccessor.View]
                    let indices = Array.zeroCreate<int> indexAccessor.Count

                    for index = 0 to indices.Length - 1 do
                        indices[index] <-
                            int (BinaryPrimitives.ReadUInt16LittleEndian(bin.AsSpan(indexView.Offset + index * 2, 2)))

                    validateBoxTopology expectedBoxes positions normals uvs indices

                    for vertex = 0 to positionAccessor.Count - 1 do
                        bounds.AddGltf(positions[vertex * 3], positions[vertex * 3 + 1], positions[vertex * 3 + 2])

                    let decoded =
                        if isCollision then
                            int64 positionAccessor.Count * 24L + int64 indexAccessor.Count * 2L
                        else
                            int64 positionAccessor.Count * 32L + int64 indexAccessor.Count * 2L

                    { Vertices = positionAccessor.Count
                      Indices = indexAccessor.Count
                      Triangles = indexAccessor.Count / 3
                      Primitives = 1
                      DecodedGeometryBytes = decoded }

                let inspectMesh
                    (meshName: string)
                    (isCollision: bool)
                    (stoneBoxes: CalibrationBox array)
                    (woodBoxes: CalibrationBox array)
                    (maximumVertices: int)
                    (maximumTriangles: int)
                    =
                    let node = nodes[nodeByName[meshName]]
                    exactFields [| "mesh"; "name" |] Array.empty node
                    let meshIndex = integer "mesh" node

                    if meshIndex <> meshByName[meshName] then
                        invalid ()

                    let mesh = meshes[meshIndex]
                    let primitives = property "primitives" mesh

                    if primitives.ValueKind <> JsonValueKind.Array then
                        invalid ()

                    if not isCollision && primitives.GetArrayLength() > 2 then
                        budget ()

                    let expectedCount = if isCollision then 1 else 2

                    if primitives.GetArrayLength() <> expectedCount then
                        invalid ()

                    let mutable preflightVertices = 0L
                    let mutable preflightTriangles = 0L

                    for primitiveIndex = 0 to expectedCount - 1 do
                        let primitive = primitives[primitiveIndex]

                        if isCollision then
                            exactFields [| "attributes"; "indices" |] Array.empty primitive
                        else
                            exactFields [| "attributes"; "indices"; "material" |] Array.empty primitive

                        let attributes = property "attributes" primitive

                        if isCollision then
                            exactFields [| "NORMAL"; "POSITION" |] Array.empty attributes
                        else
                            exactFields [| "NORMAL"; "POSITION"; "TEXCOORD_0" |] Array.empty attributes

                        if not isCollision && integer "material" primitive <> primitiveIndex then
                            invalid ()

                        let positionIndex = integer "POSITION" attributes
                        let indexIndex = integer "indices" primitive

                        if
                            positionIndex < 0
                            || positionIndex >= accessors.Length
                            || indexIndex < 0
                            || indexIndex >= accessors.Length
                        then
                            invalid ()

                        let positionAccessor = accessors[positionIndex]
                        let indexAccessor = accessors[indexIndex]

                        if
                            positionAccessor.ComponentType <> 5126
                            || positionAccessor.Kind <> "VEC3"
                            || indexAccessor.ComponentType <> 5123
                            || indexAccessor.Kind <> "SCALAR"
                            || indexAccessor.Count % 3 <> 0
                        then
                            invalid ()

                        preflightVertices <- preflightVertices + int64 positionAccessor.Count
                        preflightTriangles <- preflightTriangles + int64 indexAccessor.Count / 3L

                    if
                        preflightVertices > int64 maximumVertices
                        || preflightTriangles > int64 maximumTriangles
                    then
                        budget ()

                    if not isCollision then
                        renderPrimitiveCount <- renderPrimitiveCount + expectedCount

                    [| for primitiveIndex = 0 to expectedCount - 1 do
                           let expectedBoxes =
                               if isCollision || primitiveIndex = 0 then
                                   stoneBoxes
                               else
                                   woodBoxes

                           yield inspectPrimitive isCollision primitiveIndex expectedBoxes primitives[primitiveIndex] |]
                    |> Array.fold addMetric emptyMetric

                let geometry = expectedGeometry[moduleIndex]

                let lod0 =
                    inspectMesh childNames[0] false geometry.Lod0StoneBoxes geometry.WoodBoxes 3072 4096

                let lod1 =
                    inspectMesh childNames[1] false geometry.Lod1StoneBoxes geometry.WoodBoxes 1024 1024

                let lod2 =
                    inspectMesh childNames[2] false geometry.Lod2StoneBoxes geometry.WoodBoxes 256 192

                let collision =
                    inspectMesh childNames[3] true geometry.CollisionBoxes Array.empty Int32.MaxValue 48

                validateMetric expectedModule.Lod0 lod0 "lod0"
                validateMetric expectedModule.Lod1 lod1 "lod1"
                validateMetric expectedModule.Lod2 lod2 "lod2"
                validateMetric expectedModule.Collision collision "collision"
                bounds.Compare(expectedGeometry[moduleIndex].Bounds)

                let expectedSnaps = snapReferences[moduleId]

                for snapIndex = 0 to 1 do
                    let snap = nodes[nodeByName[childNames[4 + snapIndex]]]
                    let reference = expectedSnaps[snapIndex]

                    let required =
                        if reference.RotationQuarterTurns = 0 then
                            [| "name"; "translation" |]
                        else
                            [| "name"; "rotation"; "translation" |]

                    exactFields required Array.empty snap
                    let x, y, z = reference.TranslationMm

                    let gx, gy, gz =
                        BlenderCalibration.blenderToGltfMicrometres (x * 1000L, y * 1000L, z * 1000L)

                    let actualTranslation = singleArray 3 (property "translation" snap)

                    let expectedTranslation =
                        [| float32 gx / 1000000.0f; float32 gy / 1000000.0f; float32 gz / 1000000.0f |]

                    for axisIndex = 0 to 2 do
                        if
                            not (
                                near
                                    0.000001
                                    (float actualTranslation[axisIndex])
                                    (float expectedTranslation[axisIndex])
                            )
                        then
                            invalid ()

                    if reference.RotationQuarterTurns <> 0 then
                        let qx, qy, qz, qw =
                            BlenderCalibration.quarterTurnQuaternion reference.RotationQuarterTurns

                        let actualRotation = singleArray 4 (property "rotation" snap)
                        let expectedRotation = [| qx; qy; qz; qw |]

                        for axisIndex = 0 to 3 do
                            if
                                not (
                                    near 0.000001 (float actualRotation[axisIndex]) (float expectedRotation[axisIndex])
                                )
                            then
                                invalid ()

                inspected.Add(
                    { Id = moduleId
                      Bounds = expectedGeometry[moduleIndex].Bounds
                      Lod0 = lod0
                      Lod1 = lod1
                      Lod2 = lod2
                      Collision = collision }
                )

            if accessorUse.Count <> accessors.Length || viewUse.Count <> views.Length then
                invalid ()

            let decoded =
                inspected
                |> Seq.sumBy (fun item ->
                    item.Lod0.DecodedGeometryBytes
                    + item.Lod1.DecodedGeometryBytes
                    + item.Lod2.DecodedGeometryBytes
                    + item.Collision.DecodedGeometryBytes)

            if decoded > MaxDecodedGeometryBytes then
                budget ()

            if
                decoded <> validated.FamilyDecodedGeometryBytes
                || renderPrimitiveCount <> validated.RenderPrimitiveCount
            then
                invalid ()

            { Sha256 = Internal.sha256Hex bytes
              Bytes = int64 bytes.Length
              Modules = inspected.ToArray()
              DecodedGeometryBytes = decoded
              RenderPrimitiveCount = renderPrimitiveCount
              MaterialCount = materials.GetArrayLength() }
        with
        | AssetInspectionError _ -> reraise ()
        | :? JsonException
        | :? InvalidOperationException
        | :? InvalidDataException
        | :? OverflowException
        | :? ArgumentOutOfRangeException
        | :? IndexOutOfRangeException
        | :? KeyNotFoundException -> invalid ()

    /// Inspects in-memory GLB bytes without Blender or another process.
    let inspectGlbBytes validated bytes = inspectGlbCore validated bytes

    let private crc32 (chunkType: ReadOnlySpan<byte>) (data: ReadOnlySpan<byte>) =
        let mutable crc = 0xFFFFFFFFu

        let update (value: byte) =
            crc <- crc ^^^ uint32 value

            for _ = 0 to 7 do
                let mask = 0u - (crc &&& 1u)
                crc <- (crc >>> 1) ^^^ (0xEDB88320u &&& mask)

        for value in chunkType do
            update value

        for value in data do
            update value

        ~~~crc

    let private adler32 (bytes: byte array) =
        let mutable first = 1u
        let mutable second = 0u

        for value in bytes do
            first <- (first + uint32 value) % 65521u
            second <- (second + first) % 65521u

        (second <<< 16) ||| first

    type private DeflateBitReader(bytes: byte array) =
        let mutable bitPosition = 0

        member _.BitPosition = bitPosition

        member _.ReadBits(count: int) =
            if count < 0 || count > 16 || bitPosition > bytes.Length * 8 - count then
                invalid ()

            let mutable result = 0

            for index = 0 to count - 1 do
                let source = bitPosition + index
                let bit = (int bytes[source / 8] >>> (source % 8)) &&& 1
                result <- result ||| (bit <<< index)

            bitPosition <- bitPosition + count
            result

        member _.AlignToByte() =
            bitPosition <- (bitPosition + 7) &&& ~~~7

    type private HuffmanTable =
        { Entries: Dictionary<struct (int * int), int>
          MaximumLength: int }

    let private reverseBits value length =
        let mutable input = value
        let mutable result = 0

        for _ = 1 to length do
            result <- (result <<< 1) ||| (input &&& 1)
            input <- input >>> 1

        result

    let private huffmanTable (lengths: int array) =
        if
            lengths.Length = 0
            || lengths |> Array.exists (fun length -> length < 0 || length > 15)
        then
            invalid ()

        let maximum = Array.max lengths

        if maximum = 0 then
            invalid ()

        let counts = Array.zeroCreate<int> (maximum + 1)

        for length in lengths do
            if length > 0 then
                counts[length] <- counts[length] + 1

        let mutable available = 1

        for length = 1 to maximum do
            available <- available * 2 - counts[length]

            if available < 0 then
                invalid ()

        let next = Array.zeroCreate<int> (maximum + 1)
        let mutable code = 0

        for length = 1 to maximum do
            code <- (code + counts[length - 1]) <<< 1
            next[length] <- code

        let entries = Dictionary<struct (int * int), int>()

        for symbol = 0 to lengths.Length - 1 do
            let length = lengths[symbol]

            if length > 0 then
                let transmitted = reverseBits next[length] length
                next[length] <- next[length] + 1

                if not (entries.TryAdd(struct (length, transmitted), symbol)) then
                    invalid ()

        { Entries = entries
          MaximumLength = maximum }

    let private decodeSymbol (reader: DeflateBitReader) (table: HuffmanTable) =
        let mutable code = 0
        let mutable result = -1
        let mutable length = 1

        while result < 0 && length <= table.MaximumLength do
            code <- code ||| (reader.ReadBits(1) <<< (length - 1))

            match table.Entries.TryGetValue(struct (length, code)) with
            | true, symbol -> result <- symbol
            | _ -> length <- length + 1

        if result < 0 then
            invalid ()

        result

    let private fixedLiteralLengths =
        [| for symbol = 0 to 287 do
               if symbol <= 143 then 8
               elif symbol <= 255 then 9
               elif symbol <= 279 then 7
               else 8 |]

    let private fixedDistanceLengths = Array.create 32 5

    let private lengthBases =
        [| 3
           4
           5
           6
           7
           8
           9
           10
           11
           13
           15
           17
           19
           23
           27
           31
           35
           43
           51
           59
           67
           83
           99
           115
           131
           163
           195
           227
           258 |]

    let private lengthExtras =
        [| 0
           0
           0
           0
           0
           0
           0
           0
           1
           1
           1
           1
           2
           2
           2
           2
           3
           3
           3
           3
           4
           4
           4
           4
           5
           5
           5
           5
           0 |]

    let private distanceBases =
        [| 1
           2
           3
           4
           5
           7
           9
           13
           17
           25
           33
           49
           65
           97
           129
           193
           257
           385
           513
           769
           1025
           1537
           2049
           3073
           4097
           6145
           8193
           12289
           16385
           24577 |]

    let private distanceExtras =
        [| 0
           0
           0
           0
           1
           1
           2
           2
           3
           3
           4
           4
           5
           5
           6
           6
           7
           7
           8
           8
           9
           9
           10
           10
           11
           11
           12
           12
           13
           13 |]

    let private dynamicTables (reader: DeflateBitReader) =
        let literalCount = reader.ReadBits(5) + 257
        let distanceCount = reader.ReadBits(5) + 1
        let codeLengthCount = reader.ReadBits(4) + 4

        if literalCount > 286 || distanceCount > 32 then
            invalid ()

        let order = [| 16; 17; 18; 0; 8; 7; 9; 6; 10; 5; 11; 4; 12; 3; 13; 2; 14; 1; 15 |]
        let codeLengths = Array.zeroCreate<int> 19

        for index = 0 to codeLengthCount - 1 do
            codeLengths[order[index]] <- reader.ReadBits(3)

        let codeTable = huffmanTable codeLengths
        let target = literalCount + distanceCount
        let decoded = ResizeArray<int>(target)

        while decoded.Count < target do
            match decodeSymbol reader codeTable with
            | value when value <= 15 -> decoded.Add(value)
            | 16 ->
                if decoded.Count = 0 then
                    invalid ()

                let repeat = reader.ReadBits(2) + 3
                let previous = decoded[decoded.Count - 1]

                if decoded.Count + repeat > target then
                    invalid ()

                for _ = 1 to repeat do
                    decoded.Add(previous)
            | 17 ->
                let repeat = reader.ReadBits(3) + 3

                if decoded.Count + repeat > target then
                    invalid ()

                for _ = 1 to repeat do
                    decoded.Add(0)
            | 18 ->
                let repeat = reader.ReadBits(7) + 11

                if decoded.Count + repeat > target then
                    invalid ()

                for _ = 1 to repeat do
                    decoded.Add(0)
            | _ -> invalid ()

        let all = decoded.ToArray()
        let literals = all[0 .. literalCount - 1]
        let distances = all[literalCount..]

        if literals[256] = 0 then
            invalid ()

        huffmanTable literals, huffmanTable distances

    let private validateDeflate (deflate: byte array) expectedBytes =
        if deflate.Length = 0 then
            invalid ()

        let reader = DeflateBitReader(deflate)
        let mutable finalBlock = false
        let mutable produced = 0

        while not finalBlock do
            finalBlock <- reader.ReadBits(1) = 1

            match reader.ReadBits(2) with
            | 0 ->
                reader.AlignToByte()
                let length = reader.ReadBits(16)
                let complement = reader.ReadBits(16)

                if (length ^^^ complement) <> 0xFFFF then
                    invalid ()

                for _ = 1 to length do
                    reader.ReadBits(8) |> ignore

                produced <- produced + length
            | blockType when blockType = 1 || blockType = 2 ->
                let literals, distances =
                    if blockType = 1 then
                        huffmanTable fixedLiteralLengths, huffmanTable fixedDistanceLengths
                    else
                        dynamicTables reader

                let mutable ended = false

                while not ended do
                    let symbol = decodeSymbol reader literals

                    if symbol < 256 then
                        produced <- produced + 1
                    elif symbol = 256 then
                        ended <- true
                    elif symbol >= 257 && symbol <= 285 then
                        let lengthIndex = symbol - 257
                        let length = lengthBases[lengthIndex] + reader.ReadBits(lengthExtras[lengthIndex])
                        let distanceSymbol = decodeSymbol reader distances

                        if distanceSymbol < 0 || distanceSymbol >= distanceBases.Length then
                            invalid ()

                        let distance =
                            distanceBases[distanceSymbol] + reader.ReadBits(distanceExtras[distanceSymbol])

                        if distance > produced then
                            invalid ()

                        produced <- produced + length
                    else
                        invalid ()

                    if produced > expectedBytes then
                        invalid ()
            | _ -> invalid ()

            if produced > expectedBytes then
                invalid ()

        if produced <> expectedBytes || (reader.BitPosition + 7) / 8 <> deflate.Length then
            invalid ()

        for bitPosition = reader.BitPosition to deflate.Length * 8 - 1 do
            if ((int deflate[bitPosition / 8] >>> (bitPosition % 8)) &&& 1) <> 0 then
                invalid ()

    let private inspectPngCore (bytes: byte array) =
        try
            if isNull bytes || bytes.Length < 57 then
                invalid ()

            if bytes.Length > MaxPngBytes then
                invalid ()

            let signature = [| 137uy; 80uy; 78uy; 71uy; 13uy; 10uy; 26uy; 10uy |]

            if not (bytes.AsSpan(0, 8).SequenceEqual(signature.AsSpan())) then
                invalid ()

            let mutable offset = 8
            let mutable stage = 0
            let mutable idatCount = 0
            use compressed = new MemoryStream()

            while offset < bytes.Length do
                if bytes.Length - offset < 12 then
                    invalid ()

                let lengthValue = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset, 4))

                if lengthValue > uint32 Int32.MaxValue then
                    invalid ()

                let length = int lengthValue
                checkedRange (offset + 8) length bytes.Length

                if bytes.Length - (offset + 8 + length) < 4 then
                    invalid ()

                let kindSpan = ReadOnlySpan<byte>(bytes, offset + 4, 4)
                let kind = Encoding.ASCII.GetString(kindSpan)
                let data = ReadOnlySpan<byte>(bytes, offset + 8, length)

                let expectedCrc =
                    BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(offset + 8 + length, 4))

                if crc32 kindSpan data <> expectedCrc then
                    invalid ()

                match kind with
                | "IHDR" when stage = 0 ->
                    if length <> 13 then
                        invalid ()

                    if
                        BinaryPrimitives.ReadUInt32BigEndian(data.Slice(0, 4)) <> 960u
                        || BinaryPrimitives.ReadUInt32BigEndian(data.Slice(4, 4)) <> 540u
                        || data[8] <> 8uy
                        || data[9] <> 6uy
                        || data[10] <> 0uy
                        || data[11] <> 0uy
                        || data[12] <> 0uy
                    then
                        invalid ()

                    stage <- 1
                | "IDAT" when stage = 1 || stage = 2 ->
                    if length = 0 then
                        invalid ()

                    compressed.Write(data)
                    idatCount <- idatCount + 1
                    stage <- 2
                | "IEND" when stage = 2 ->
                    if length <> 0 || idatCount = 0 || offset + 12 <> bytes.Length then
                        invalid ()

                    stage <- 3
                | _ -> invalid ()

                offset <- offset + 12 + length

            if stage <> 3 || offset <> bytes.Length then
                invalid ()

            let compressedBytes = compressed.ToArray()

            if compressedBytes.Length < 6 then
                invalid ()

            let cmf = compressedBytes[0]
            let flg = compressedBytes[1]

            if
                cmf &&& 0x0Fuy <> 8uy
                || cmf >>> 4 > 7uy
                || (int cmf * 256 + int flg) % 31 <> 0
                || flg &&& 0x20uy <> 0uy
            then
                invalid ()

            let expectedDecodedBytes = 3841 * 540
            let deflateBytes = compressedBytes.AsSpan(2, compressedBytes.Length - 6).ToArray()
            validateDeflate deflateBytes expectedDecodedBytes

            use input = new MemoryStream(compressedBytes, false)
            use zlib = new ZLibStream(input, CompressionMode.Decompress, true)
            let rowLength = 3841
            let decoded = Array.zeroCreate<byte> expectedDecodedBytes
            let mutable read = 0

            while read < decoded.Length do
                let count = zlib.Read(decoded, read, decoded.Length - read)

                if count = 0 then
                    invalid ()

                read <- read + count

            if zlib.ReadByte() <> -1 then
                invalid ()

            for row = 0 to 539 do
                if decoded[row * rowLength] > 4uy then
                    invalid ()

            let storedAdler =
                BinaryPrimitives.ReadUInt32BigEndian(compressedBytes.AsSpan(compressedBytes.Length - 4, 4))

            if storedAdler <> adler32 decoded then
                invalid ()

            { Sha256 = Internal.sha256Hex bytes
              Bytes = int64 bytes.Length
              Width = 960
              Height = 540 }
        with
        | AssetInspectionError _ -> reraise ()
        | :? InvalidDataException
        | :? IOException
        | :? OverflowException
        | :? ArgumentOutOfRangeException
        | :? IndexOutOfRangeException -> invalid ()

    /// Inspects normalized in-memory PNG bytes and bounded zlib scanlines.
    let inspectPngBytes bytes = inspectPngCore bytes

    let private readToolchainPin (root: string) =
        try
            let safeRoot = validateWorkspaceRoot root
            let locations = Workspace.paths safeRoot

            let path =
                Workspace.requireSafePath
                    locations
                    "Toolchain-Lock"
                    false
                    (Path.Combine(locations.Root, "toolchain.lock.json"))

            let attributes = File.GetAttributes(path)

            if
                attributes.HasFlag(FileAttributes.Directory)
                || attributes.HasFlag(FileAttributes.ReparsePoint)
            then
                unsafePath ()

            use stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read)

            if stream.Length <= 0L || stream.Length > 65536L then
                invalid ()

            let bytes = Array.zeroCreate<byte> (int stream.Length)
            let mutable offset = 0

            while offset < bytes.Length do
                let count = stream.Read(bytes, offset, bytes.Length - offset)

                if count = 0 then
                    unsafePath ()

                offset <- offset + count

            if stream.ReadByte() <> -1 || stream.Length <> int64 bytes.Length then
                unsafePath ()

            let finalPath =
                Workspace.requireSafePath
                    locations
                    "Toolchain-Lock"
                    false
                    (Path.Combine(locations.Root, "toolchain.lock.json"))

            let finalAttributes = File.GetAttributes(finalPath)

            if
                not (String.Equals(path, finalPath, StringComparison.Ordinal))
                || finalAttributes.HasFlag(FileAttributes.Directory)
                || finalAttributes.HasFlag(FileAttributes.ReparsePoint)
                || stream.Length <> int64 bytes.Length
            then
                unsafePath ()

            ensureUniqueJsonKeys 8 bytes

            use document =
                JsonDocument.Parse(ReadOnlyMemory<byte>(bytes), JsonDocumentOptions(MaxDepth = 8))

            let tools = property "tools" document.RootElement

            if tools.ValueKind <> JsonValueKind.Array then
                invalid ()

            let matches =
                tools.EnumerateArray()
                |> Seq.filter (fun (tool: JsonElement) ->
                    if tool.ValueKind <> JsonValueKind.Object then
                        invalid ()

                    match tool.TryGetProperty("id") with
                    | true, value when value.ValueKind = JsonValueKind.String -> value.GetString() = "dotnet-sdk"
                    | _ -> false)
                |> Seq.toArray

            if matches.Length <> 1 then
                invalid ()

            let dotnetSdk = matches[0]

            exactFields [| "id"; "install"; "integrity"; "license"; "version" |] Array.empty dotnetSdk

            if
                stringValue "id" dotnetSdk <> "dotnet-sdk"
                || stringValue "install" dotnetSdk <> "scripts/bootstrap-dotnet.sh"
                || stringValue "integrity" dotnetSdk
                   <> "platform-specific SHA-512 values embedded in bootstrap script"
                || stringValue "license" dotnetSdk <> "MIT"
                || stringValue "version" dotnetSdk <> "10.0.110"
            then
                invalid ()

            let canonical = Array.append (Internal.canonicalElement dotnetSdk) [| byte '\n' |]
            let hash = Internal.sha256Hex canonical

            if canonical.Length <> 173 || hash <> ToolchainPinSha256 then
                invalid ()

            hash
        with
        | AssetInspectionError _
        | AssetInspectionPathError _ -> reraise ()
        | HarnessException _ -> unsafePath ()
        | :? JsonException
        | :? InvalidOperationException -> invalid ()
        | :? ArgumentException
        | :? EncoderFallbackException
        | :? IOException
        | :? UnauthorizedAccessException
        | :? NotSupportedException
        | :? System.Security.SecurityException -> unsafePath ()

    let private requireSha (value: string) =
        if not (Internal.isSha256 value) then
            invalid ()

        value

    let private compareMetric (expected: PrimitiveMetrics) (element: JsonElement) =
        exactFields [| "decodedGeometryBytes"; "indices"; "primitives"; "triangles"; "vertices" |] Array.empty element

        if
            integer64 "decodedGeometryBytes" element <> expected.DecodedGeometryBytes
            || integer "indices" element <> expected.Indices
            || integer "primitives" element <> expected.Primitives
            || integer "triangles" element <> expected.Triangles
            || integer "vertices" element <> expected.Vertices
        then
            invalid ()

    let private compareBounds (expected: CalibrationBox) (element: JsonElement) =
        exactFields [| "max"; "min" |] Array.empty element
        let minimum = integerArray 3 (property "min" element)
        let maximum = integerArray 3 (property "max" element)

        if
            minimum <> [| expected.Min.X; expected.Min.Y; expected.Min.Z |]
            || maximum <> [| expected.Max.X; expected.Max.Y; expected.Max.Z |]
        then
            invalid ()

    let private compareReport
        (rootPath: string)
        (validated: ValidatedCalibrationSpec)
        (glb: GlbInspection)
        (png: PngInspection)
        (bytes: byte array)
        =
        try
            if isNull bytes || bytes.Length = 0 || bytes.Length > MaxReportBytes then
                invalid ()

            ensureUniqueJsonKeys 8 bytes

            use document =
                JsonDocument.Parse(
                    ReadOnlyMemory<byte>(bytes),
                    JsonDocumentOptions(
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow,
                        MaxDepth = 8
                    )
                )

            let report = document.RootElement

            exactFields
                [| "artifacts"
                   "familyId"
                   "familyMetrics"
                   "generatorSourceSha256"
                   "generatorSources"
                   "limits"
                   "materials"
                   "modules"
                   "profile"
                   "schemaVersion"
                   "seed"
                   "specSha256"
                   "toolchainPinSha256" |]
                Array.empty
                report

            let canonical = Array.append (Internal.canonicalElement report) [| byte '\n' |]

            if not (bytes.AsSpan().SequenceEqual(canonical.AsSpan())) then
                invalid ()

            if
                integer "schemaVersion" report <> 1
                || stringValue "profile" report <> validated.Spec.Profile
                || stringValue "familyId" report <> validated.Spec.FamilyId
                || integer64 "seed" report <> int64 validated.Spec.Seed
                || requireSha (stringValue "specSha256" report) <> validated.SpecSha256
            then
                invalid ()

            let sourcePaths =
                [| "tools/RiftHarness/AssetJobJournal.fs"
                   "tools/RiftHarness/BlenderCalibration.fs"
                   "tools/RiftHarness/DotnetAssetGenerator.fs" |]

            let sources = property "generatorSources" report

            if
                sources.ValueKind <> JsonValueKind.Array
                || sources.GetArrayLength() <> sourcePaths.Length
            then
                invalid ()

            use sourceBinding = new MemoryStream()

            for index = 0 to sourcePaths.Length - 1 do
                let source = sources[index]
                exactFields [| "path"; "sha256" |] Array.empty source
                let path = stringValue "path" source
                let hash = requireSha (stringValue "sha256" source)

                if path <> sourcePaths[index] then
                    invalid ()

                let binding = Constants.Utf8NoBom.GetBytes(path + "\n" + hash + "\n")
                sourceBinding.Write(binding)

            let sourceHash = Internal.sha256Hex (sourceBinding.ToArray())

            if requireSha (stringValue "generatorSourceSha256" report) <> sourceHash then
                invalid ()

            let toolchainHash = readToolchainPin rootPath

            if requireSha (stringValue "toolchainPinSha256" report) <> toolchainHash then
                invalid ()

            let assetId =
                validated.Spec.FamilyId
                + "-"
                + validated.SpecSha256.Substring(0, 12).ToUpperInvariant()

            let expectedGlbPath = $"assets/quarantine/3d/{assetId}/family.glb"
            let expectedPreviewPath = $"assets/quarantine/3d/{assetId}/preview.png"
            let artifacts = property "artifacts" report
            exactFields [| "glb"; "preview" |] Array.empty artifacts

            let compareArtifact (name: string) (expectedPath: string) (expectedHash: string) (expectedBytes: int64) =
                let artifact = property name artifacts
                exactFields [| "bytes"; "path"; "sha256" |] Array.empty artifact

                if
                    stringValue "path" artifact <> expectedPath
                    || requireSha (stringValue "sha256" artifact) <> expectedHash
                    || integer64 "bytes" artifact <> expectedBytes
                then
                    invalid ()

            compareArtifact "glb" expectedGlbPath glb.Sha256 glb.Bytes
            compareArtifact "preview" expectedPreviewPath png.Sha256 png.Bytes

            let materials = property "materials" report

            if materials.ValueKind <> JsonValueKind.Array || materials.GetArrayLength() <> 2 then
                invalid ()

            let compareReportMaterial (index: int) (name: string) (color: int array) (metallic: int) (roughness: int) =
                let material = materials[index]

                exactFields [| "baseColorSrgb8"; "metallicPermille"; "name"; "roughnessPermille" |] Array.empty material

                if
                    stringValue "name" material <> name
                    || int32Array 3 (property "baseColorSrgb8" material) <> color
                    || integer "metallicPermille" material <> metallic
                    || integer "roughnessPermille" material <> roughness
                then
                    invalid ()

            compareReportMaterial
                0
                "MAT_CAL_STONE"
                validated.Spec.Materials.StoneBaseColorSrgb8
                validated.Spec.Materials.StoneMetallicPermille
                validated.Spec.Materials.StoneRoughnessPermille

            compareReportMaterial
                1
                "MAT_CAL_WOOD"
                validated.Spec.Materials.WoodBaseColorSrgb8
                validated.Spec.Materials.WoodMetallicPermille
                validated.Spec.Materials.WoodRoughnessPermille

            let modules = property "modules" report

            if
                modules.ValueKind <> JsonValueKind.Array
                || modules.GetArrayLength() <> glb.Modules.Length
            then
                invalid ()

            let snapReferences = BlenderCalibration.snapPoints validated.Spec |> dict

            for index = 0 to glb.Modules.Length - 1 do
                let expected = glb.Modules[index]
                let moduleReport = modules[index]

                exactFields
                    [| "boundsMicrometres"
                       "collision"
                       "id"
                       "lod0"
                       "lod1"
                       "lod2"
                       "snapPoints" |]
                    Array.empty
                    moduleReport

                if stringValue "id" moduleReport <> expected.Id then
                    invalid ()

                compareBounds expected.Bounds (property "boundsMicrometres" moduleReport)
                compareMetric expected.Lod0 (property "lod0" moduleReport)
                compareMetric expected.Lod1 (property "lod1" moduleReport)
                compareMetric expected.Lod2 (property "lod2" moduleReport)
                compareMetric expected.Collision (property "collision" moduleReport)

                let snaps = property "snapPoints" moduleReport
                let expectedSnaps = snapReferences[expected.Id]

                if snaps.ValueKind <> JsonValueKind.Array || snaps.GetArrayLength() <> 2 then
                    invalid ()

                for snapIndex = 0 to 1 do
                    let snap = snaps[snapIndex]
                    let reference = expectedSnaps[snapIndex]
                    exactFields [| "id"; "rotationQuarterTurns"; "translationMm" |] Array.empty snap
                    let x, y, z = reference.TranslationMm

                    if
                        stringValue "id" snap <> reference.Id
                        || integer "rotationQuarterTurns" snap <> reference.RotationQuarterTurns
                        || integerArray 3 (property "translationMm" snap) <> [| x; y; z |]
                    then
                        invalid ()

            let familyMetrics = property "familyMetrics" report

            exactFields
                [| "decodedGeometryBytes"
                   "glbBytes"
                   "materialCount"
                   "renderPrimitiveCount" |]
                Array.empty
                familyMetrics

            if
                integer64 "decodedGeometryBytes" familyMetrics <> glb.DecodedGeometryBytes
                || integer64 "glbBytes" familyMetrics <> glb.Bytes
                || integer "materialCount" familyMetrics <> glb.MaterialCount
                || integer "renderPrimitiveCount" familyMetrics <> glb.RenderPrimitiveCount
            then
                invalid ()

            let limits = property "limits" report

            exactFields
                [| "collisionTriangles"
                   "decodedGeometryBytes"
                   "glbBytes"
                   "lod0Triangles"
                   "lod0Vertices"
                   "lod1Triangles"
                   "lod1Vertices"
                   "lod2Triangles"
                   "lod2Vertices"
                   "materials"
                   "renderPrimitivesPerLod" |]
                Array.empty
                limits

            let expectedLimits =
                [| "collisionTriangles", 48L
                   "decodedGeometryBytes", MaxDecodedGeometryBytes
                   "glbBytes", int64 MaxGlbBytes
                   "lod0Triangles", 4096L
                   "lod0Vertices", 3072L
                   "lod1Triangles", 1024L
                   "lod1Vertices", 1024L
                   "lod2Triangles", 192L
                   "lod2Vertices", 256L
                   "materials", 2L
                   "renderPrimitivesPerLod", 2L |]

            for name, expected in expectedLimits do
                if integer64 name limits <> expected then
                    invalid ()

            Internal.sha256Hex bytes
        with
        | AssetInspectionError _
        | AssetInspectionPathError _ -> reraise ()
        | :? JsonException
        | :? InvalidOperationException
        | :? InvalidDataException
        | :? OverflowException
        | :? ArgumentOutOfRangeException
        | :? IndexOutOfRangeException
        | :? KeyNotFoundException -> invalid ()

    /// Inspects all three bounded artifacts and cross-validates the canonical report.
    let inspect root validated glbRelative previewRelative reportRelative =
        let glbBytes = readRegularFile root glbRelative MaxGlbBytes
        let previewBytes = readRegularFile root previewRelative MaxPngBytes
        let reportBytes = readRegularFile root reportRelative MaxReportBytes
        let glb = inspectGlbCore validated glbBytes
        let preview = inspectPngCore previewBytes
        let reportSha = compareReport root validated glb preview reportBytes

        { FamilyId = validated.Spec.FamilyId
          SpecSha256 = validated.SpecSha256
          GlbPath = glbRelative
          GlbSha256 = glb.Sha256
          GlbBytes = glb.Bytes
          PreviewPath = previewRelative
          PreviewSha256 = preview.Sha256
          PreviewBytes = preview.Bytes
          ReportPath = reportRelative
          ReportSha256 = reportSha
          ReportBytes = int64 reportBytes.Length
          DecodedGeometryBytes = glb.DecodedGeometryBytes
          RenderPrimitiveCount = glb.RenderPrimitiveCount
          MaterialCount = glb.MaterialCount }
