namespace RiftHarness

open System
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Text
open System.Text.Json

exception CalibrationSpecError of string

type CalibrationGeometry =
    { LintelHeightMm: int
      ModuleHeightMm: int
      ModuleWidthMm: int
      MortarGapMm: int
      OpeningHeightMm: int
      OpeningWidthMm: int
      StoneCourseHeightMm: int
      StoneDepthJitterMm: int
      StoneLengthJitterMm: int
      StoneOffsetJitterMm: int
      TimberDepthMm: int
      TimberWidthMm: int
      WallThicknessMm: int }

type CalibrationMaterials =
    { StoneBaseColorSrgb8: int array
      StoneMetallicPermille: int
      StoneRoughnessPermille: int
      WoodBaseColorSrgb8: int array
      WoodMetallicPermille: int
      WoodRoughnessPermille: int }

type CalibrationSpec =
    { SchemaVersion: int
      Profile: string
      FamilyId: string
      Seed: uint32
      Geometry: CalibrationGeometry
      Materials: CalibrationMaterials }

type PrimitiveMetrics =
    { Vertices: int
      Indices: int
      Triangles: int
      Primitives: int
      DecodedGeometryBytes: int64 }

type ModuleReferenceMetrics =
    { Id: string
      Lod0: PrimitiveMetrics
      Lod1: PrimitiveMetrics
      Lod2: PrimitiveMetrics
      Collision: PrimitiveMetrics }

type MicrometrePoint = { X: int64; Y: int64; Z: int64 }

type CalibrationBox =
    { Min: MicrometrePoint
      Max: MicrometrePoint }

type ModuleReferenceGeometry =
    { Id: string
      Lod0StoneBoxes: CalibrationBox array
      Lod1StoneBoxes: CalibrationBox array
      Lod2StoneBoxes: CalibrationBox array
      WoodBoxes: CalibrationBox array
      CollisionBoxes: CalibrationBox array
      Bounds: CalibrationBox }

type SnapPointReference =
    { Id: string
      TranslationMm: int64 * int64 * int64
      RotationQuarterTurns: int }

type ValidatedCalibrationSpec =
    { Spec: CalibrationSpec
      CanonicalBytes: byte array
      SpecSha256: string
      Modules: ModuleReferenceMetrics array
      FamilyDecodedGeometryBytes: int64
      RenderPrimitiveCount: int }

[<Sealed>]
type Pcg32(seed: uint32) =
    let multiplier = 6364136223846793005UL
    let increment = 1442695040888963407UL
    let mutable state = 0UL

    let nextValue () =
        let previous = state
        state <- previous * multiplier + increment
        let xorshifted = uint32 (((previous >>> 18) ^^^ previous) >>> 27)
        let rotation = int (previous >>> 59)
        (xorshifted >>> rotation) ||| (xorshifted <<< ((-rotation) &&& 31))

    do
        nextValue () |> ignore
        state <- state + uint64 seed
        nextValue () |> ignore

    member _.NextUInt32() = nextValue ()

    member _.Bounded(bound: uint32) =
        if bound = 0u || bound > 0x80000000u then
            invalidArg (nameof bound) "PCG32 bound must be in 1..2^31."

        let threshold = (~~~bound + 1u) % bound
        let mutable value = nextValue ()

        while value < threshold do
            value <- nextValue ()

        value % bound

    member this.Signed(jitter: int) =
        if jitter < 0 || jitter > 1073741823 then
            invalidArg (nameof jitter) "PCG32 signed jitter is outside its supported range."

        int (this.Bounded(uint32 (2 * jitter + 1))) - jitter

[<RequireQualifiedAccess>]
module BlenderCalibration =
    [<Literal>]
    let MaxSpecBytes = 16 * 1024

    [<Literal>]
    let MaxJsonDepth = 6

    [<Literal>]
    let MaxPropertyCount = 64

    [<Literal>]
    let MaxGlbBytes = 2097152

    [<Literal>]
    let MaxDecodedGeometryBytes = 2097152L

    let moduleOrder = [| "WALL-STRAIGHT"; "WALL-CORNER"; "WALL-OPENING" |]

    let private invalidSpec () =
        raise (CalibrationSpecError "INVALID_SPEC")

    let private unsafePath () =
        raise (CalibrationSpecError "UNSAFE_PATH")

    let private exactFields (expected: string array) (element: JsonElement) =
        if element.ValueKind <> JsonValueKind.Object then
            invalidSpec ()

        let expectedSet = HashSet<string>(expected, StringComparer.Ordinal)
        let seen = HashSet<string>(StringComparer.Ordinal)

        for property in element.EnumerateObject() do
            if not (expectedSet.Contains(property.Name)) || not (seen.Add(property.Name)) then
                invalidSpec ()

        if seen.Count <> expected.Length then
            invalidSpec ()

    let private property (name: string) (element: JsonElement) =
        match element.TryGetProperty(name) with
        | true, value -> value
        | _ -> invalidSpec ()

    let private integer (name: string) (element: JsonElement) =
        let value = property name element

        if value.ValueKind <> JsonValueKind.Number then
            invalidSpec ()

        match value.TryGetInt32() with
        | true, parsed when value.GetRawText().IndexOfAny([| '.'; 'e'; 'E' |]) < 0 -> parsed
        | _ -> invalidSpec ()

    let private uint32Integer (name: string) (element: JsonElement) =
        let value = property name element

        if value.ValueKind <> JsonValueKind.Number then
            invalidSpec ()

        match value.TryGetUInt32() with
        | true, parsed when value.GetRawText().IndexOfAny([| '.'; 'e'; 'E' |]) < 0 -> parsed
        | _ -> invalidSpec ()

    let private fixedString (name: string) (expected: string) (element: JsonElement) =
        let value = property name element

        if
            value.ValueKind <> JsonValueKind.String
            || not (String.Equals(value.GetString(), expected, StringComparison.Ordinal))
        then
            invalidSpec ()

        expected

    let private integerTriple (name: string) (element: JsonElement) =
        let value = property name element

        if value.ValueKind <> JsonValueKind.Array || value.GetArrayLength() <> 3 then
            invalidSpec ()

        value.EnumerateArray()
        |> Seq.map (fun item ->
            if item.ValueKind <> JsonValueKind.Number then
                invalidSpec ()

            match item.TryGetInt32() with
            | true, parsed when item.GetRawText().IndexOfAny([| '.'; 'e'; 'E' |]) < 0 -> parsed
            | _ -> invalidSpec ())
        |> Seq.toArray

    let private requireRange (minimum: int) (maximum: int) (value: int) =
        if value < minimum || value > maximum then
            invalidSpec ()

    let private ensureJsonLimits (bytes: byte array) =
        let options =
            JsonReaderOptions(
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
                MaxDepth = MaxJsonDepth
            )

        let mutable reader = Utf8JsonReader(ReadOnlySpan<byte>(bytes), options)
        let objectKeys = Stack<HashSet<string>>()
        let mutable propertyCount = 0

        while reader.Read() do
            match reader.TokenType with
            | JsonTokenType.StartObject -> objectKeys.Push(HashSet<string>(StringComparer.Ordinal))
            | JsonTokenType.EndObject -> objectKeys.Pop() |> ignore
            | JsonTokenType.PropertyName ->
                propertyCount <- propertyCount + 1

                if
                    propertyCount > MaxPropertyCount
                    || objectKeys.Count = 0
                    || not (objectKeys.Peek().Add(reader.GetString()))
                then
                    invalidSpec ()
            | _ -> ()

    let private ceilDiv positiveNumerator positiveDenominator =
        (positiveNumerator + positiveDenominator - 1) / positiveDenominator

    let private ceilDiv64 positiveNumerator positiveDenominator =
        (positiveNumerator + positiveDenominator - 1L) / positiveDenominator

    let private cellCount courseHeightMm lengthMicrometres parity =
        let courseHeightMicrometres = int64 courseHeightMm * 1000L

        ceilDiv64 (lengthMicrometres + int64 parity * 2L * courseHeightMicrometres) (4L * courseHeightMicrometres)
        |> int

    let private minimumClippedLengthMicrometres courseHeightMm lengthMicrometres parity =
        let courseHeightMicrometres = int64 courseHeightMm * 1000L
        let cellWidth = 4L * courseHeightMicrometres
        let parityOffset = int64 parity * 2L * courseHeightMicrometres
        let mutable cellMinimum = -parityOffset
        let mutable minimum = Int64.MaxValue

        while cellMinimum < lengthMicrometres do
            let clippedMinimum = max 0L cellMinimum
            let clippedMaximum = min lengthMicrometres (cellMinimum + cellWidth)

            if clippedMaximum > clippedMinimum then
                minimum <- min minimum (clippedMaximum - clippedMinimum)

            cellMinimum <- cellMinimum + cellWidth

        minimum

    let private metric render boxes primitives =
        let vertices = boxes * 24
        let triangles = boxes * 12
        let indices = triangles * 3

        let decoded =
            if render then
                int64 vertices * 32L + int64 indices * 2L
            else
                int64 vertices * 24L + int64 indices * 2L

        { Vertices = vertices
          Indices = indices
          Triangles = triangles
          Primitives = primitives
          DecodedGeometryBytes = decoded }

    let referenceMetrics (spec: CalibrationSpec) =
        let geometry = spec.Geometry
        let courses = geometry.ModuleHeightMm / geometry.StoneCourseHeightMm

        let upperStart =
            (geometry.OpeningHeightMm + geometry.LintelHeightMm)
            / geometry.StoneCourseHeightMm

        let moduleWidth = int64 geometry.ModuleWidthMm * 1000L
        let cornerYLength = moduleWidth - int64 geometry.WallThicknessMm * 500L
        let sideLength = int64 (geometry.ModuleWidthMm - geometry.OpeningWidthMm) * 500L

        let sumCells length firstCourse lastCourse grouped =
            [ firstCourse..lastCourse ]
            |> List.sumBy (fun course ->
                let cells = cellCount geometry.StoneCourseHeightMm length (course % 2)
                if grouped then ceilDiv cells 8 else cells)

        let straight grouped =
            sumCells moduleWidth 0 (courses - 1) grouped

        let corner grouped =
            straight grouped + sumCells cornerYLength 0 (courses - 1) grouped

        let opening grouped =
            2 * sumCells sideLength 0 (upperStart - 1) grouped
            + sumCells moduleWidth upperStart (courses - 1) grouped

        let create id lod0Stone lod1Stone lod2Stone wood collision =
            { Id = id
              Lod0 = metric true (lod0Stone + wood) 2
              Lod1 = metric true (lod1Stone + wood) 2
              Lod2 = metric true (lod2Stone + wood) 2
              Collision = metric false collision 1 }

        [| create moduleOrder[0] (straight false) (straight true) 1 2 1
           create moduleOrder[1] (corner false) (corner true) 2 3 2
           create moduleOrder[2] (opening false) (opening true) 3 3 3 |]

    let relationViolations (geometry: CalibrationGeometry) =
        let violations = ResizeArray<string>()
        let course = geometry.StoneCourseHeightMm

        if course <= 0 then
            violations.Add("STONE_COURSE_NONPOSITIVE")
        else
            if geometry.ModuleHeightMm % course <> 0 then
                violations.Add("HEIGHT_COURSE_DIVISIBILITY")

            if geometry.OpeningHeightMm % course <> 0 then
                violations.Add("OPENING_COURSE_DIVISIBILITY")

            if geometry.LintelHeightMm % course <> 0 then
                violations.Add("LINTEL_COURSE_DIVISIBILITY")

            if
                int64 geometry.OpeningHeightMm + int64 geometry.LintelHeightMm > int64 geometry.ModuleHeightMm
                                                                                 - int64 course
            then
                violations.Add("OPENING_LINTEL_HEIGHT")

            if
                int64 geometry.OpeningWidthMm + 2L * int64 geometry.TimberWidthMm > int64 geometry.ModuleWidthMm
                                                                                    - 4L * int64 course
            then
                violations.Add("OPENING_TIMBER_SIDE_BUDGET")

            if 4L * int64 geometry.MortarGapMm > int64 course then
                violations.Add("MORTAR_COURSE_BUDGET")

            if
                2L * int64 geometry.StoneOffsetJitterMm
                + int64 geometry.StoneLengthJitterMm
                + int64 geometry.MortarGapMm > int64 course
            then
                violations.Add("TANGENT_JITTER_BUDGET")

            let segmentLengths =
                [| int64 geometry.ModuleWidthMm * 1000L
                   int64 geometry.ModuleWidthMm * 1000L - int64 geometry.WallThicknessMm * 500L
                   int64 (geometry.ModuleWidthMm - geometry.OpeningWidthMm) * 500L |]

            let minimumAllowed =
                int64 (geometry.MortarGapMm + geometry.StoneLengthJitterMm) * 1000L

            if
                segmentLengths
                |> Array.exists (fun length ->
                    [| 0; 1 |]
                    |> Array.exists (fun parity ->
                        minimumClippedLengthMicrometres course length parity <= minimumAllowed))
            then
                violations.Add("STONE_SEGMENT_SLIVER")

        if geometry.TimberDepthMm > geometry.WallThicknessMm then
            violations.Add("TIMBER_DEPTH")

        if 2L * int64 geometry.StoneDepthJitterMm > int64 geometry.WallThicknessMm then
            violations.Add("DEPTH_JITTER_BUDGET")

        violations.ToArray()

    let private validateRangesAndRelations (spec: CalibrationSpec) =
        let geometry = spec.Geometry
        let materials = spec.Materials
        requireRange 2400 3600 geometry.ModuleHeightMm

        if geometry.ModuleWidthMm <> 4000 then
            invalidSpec ()

        requireRange 300 600 geometry.WallThicknessMm
        requireRange 1200 2000 geometry.OpeningWidthMm

        if geometry.OpeningWidthMm % 2 <> 0 then
            invalidSpec ()

        requireRange 1800 2400 geometry.OpeningHeightMm
        requireRange 250 400 geometry.LintelHeightMm
        requireRange 250 400 geometry.StoneCourseHeightMm
        requireRange 10 40 geometry.MortarGapMm
        requireRange 120 240 geometry.TimberWidthMm
        requireRange 100 240 geometry.TimberDepthMm
        requireRange 0 80 geometry.StoneLengthJitterMm
        requireRange 0 60 geometry.StoneDepthJitterMm
        requireRange 0 40 geometry.StoneOffsetJitterMm

        for value in Array.append materials.StoneBaseColorSrgb8 materials.WoodBaseColorSrgb8 do
            requireRange 0 255 value

        requireRange 500 1000 materials.StoneRoughnessPermille
        requireRange 500 1000 materials.WoodRoughnessPermille
        requireRange 0 100 materials.StoneMetallicPermille
        requireRange 0 100 materials.WoodMetallicPermille

        if relationViolations geometry |> Array.isEmpty |> not then
            invalidSpec ()

        let modules = referenceMetrics spec

        if
            modules
            |> Array.exists (fun item ->
                item.Lod0.Vertices > 3072
                || item.Lod0.Triangles > 4096
                || item.Lod1.Vertices > 1024
                || item.Lod1.Triangles > 1024
                || item.Lod2.Vertices > 256
                || item.Lod2.Triangles > 192
                || item.Collision.Triangles > 48
                || item.Lod0.Primitives > 2
                || item.Lod1.Primitives > 2
                || item.Lod2.Primitives > 2)
        then
            invalidSpec ()

        let decoded =
            modules
            |> Array.sumBy (fun item ->
                item.Lod0.DecodedGeometryBytes
                + item.Lod1.DecodedGeometryBytes
                + item.Lod2.DecodedGeometryBytes
                + item.Collision.DecodedGeometryBytes)

        if decoded > MaxDecodedGeometryBytes then
            invalidSpec ()

    let canonicalSpecBytes (spec: CalibrationSpec) =
        use stream = new MemoryStream()
        use writer = new Utf8JsonWriter(stream, JsonWriterOptions(Indented = false))
        writer.WriteStartObject()
        writer.WriteString("familyId", spec.FamilyId)
        writer.WritePropertyName("geometry")
        writer.WriteStartObject()
        writer.WriteNumber("lintelHeightMm", spec.Geometry.LintelHeightMm)
        writer.WriteNumber("moduleHeightMm", spec.Geometry.ModuleHeightMm)
        writer.WriteNumber("moduleWidthMm", spec.Geometry.ModuleWidthMm)
        writer.WriteNumber("mortarGapMm", spec.Geometry.MortarGapMm)
        writer.WriteNumber("openingHeightMm", spec.Geometry.OpeningHeightMm)
        writer.WriteNumber("openingWidthMm", spec.Geometry.OpeningWidthMm)
        writer.WriteNumber("stoneCourseHeightMm", spec.Geometry.StoneCourseHeightMm)
        writer.WriteNumber("stoneDepthJitterMm", spec.Geometry.StoneDepthJitterMm)
        writer.WriteNumber("stoneLengthJitterMm", spec.Geometry.StoneLengthJitterMm)
        writer.WriteNumber("stoneOffsetJitterMm", spec.Geometry.StoneOffsetJitterMm)
        writer.WriteNumber("timberDepthMm", spec.Geometry.TimberDepthMm)
        writer.WriteNumber("timberWidthMm", spec.Geometry.TimberWidthMm)
        writer.WriteNumber("wallThicknessMm", spec.Geometry.WallThicknessMm)
        writer.WriteEndObject()
        writer.WritePropertyName("materials")
        writer.WriteStartObject()
        writer.WritePropertyName("stoneBaseColorSrgb8")
        writer.WriteStartArray()
        spec.Materials.StoneBaseColorSrgb8 |> Array.iter writer.WriteNumberValue
        writer.WriteEndArray()
        writer.WriteNumber("stoneMetallicPermille", spec.Materials.StoneMetallicPermille)
        writer.WriteNumber("stoneRoughnessPermille", spec.Materials.StoneRoughnessPermille)
        writer.WritePropertyName("woodBaseColorSrgb8")
        writer.WriteStartArray()
        spec.Materials.WoodBaseColorSrgb8 |> Array.iter writer.WriteNumberValue
        writer.WriteEndArray()
        writer.WriteNumber("woodMetallicPermille", spec.Materials.WoodMetallicPermille)
        writer.WriteNumber("woodRoughnessPermille", spec.Materials.WoodRoughnessPermille)
        writer.WriteEndObject()
        writer.WriteString("profile", spec.Profile)
        writer.WriteNumber("schemaVersion", spec.SchemaVersion)
        writer.WriteNumber("seed", spec.Seed)
        writer.WriteEndObject()
        writer.Flush()
        let json = stream.ToArray()
        Array.append json [| byte '\n' |]

    let specSha256 spec =
        spec |> canonicalSpecBytes |> Internal.sha256Hex

    let parseSpecBytes (bytes: byte array) =
        try
            if isNull bytes || bytes.Length = 0 || bytes.Length > MaxSpecBytes then
                invalidSpec ()

            ensureJsonLimits bytes

            use document =
                JsonDocument.Parse(
                    ReadOnlyMemory<byte>(bytes),
                    JsonDocumentOptions(
                        AllowTrailingCommas = false,
                        CommentHandling = JsonCommentHandling.Disallow,
                        MaxDepth = MaxJsonDepth
                    )
                )

            let root = document.RootElement

            exactFields [| "familyId"; "geometry"; "materials"; "profile"; "schemaVersion"; "seed" |] root

            let geometryElement = property "geometry" root

            exactFields
                [| "lintelHeightMm"
                   "moduleHeightMm"
                   "moduleWidthMm"
                   "mortarGapMm"
                   "openingHeightMm"
                   "openingWidthMm"
                   "stoneCourseHeightMm"
                   "stoneDepthJitterMm"
                   "stoneLengthJitterMm"
                   "stoneOffsetJitterMm"
                   "timberDepthMm"
                   "timberWidthMm"
                   "wallThicknessMm" |]
                geometryElement

            let materialsElement = property "materials" root

            exactFields
                [| "stoneBaseColorSrgb8"
                   "stoneMetallicPermille"
                   "stoneRoughnessPermille"
                   "woodBaseColorSrgb8"
                   "woodMetallicPermille"
                   "woodRoughnessPermille" |]
                materialsElement

            let spec =
                { SchemaVersion = integer "schemaVersion" root
                  Profile = fixedString "profile" "calibration-v1" root
                  FamilyId = fixedString "familyId" "CAL-STONEWOOD-V1" root
                  Seed = uint32Integer "seed" root
                  Geometry =
                    { LintelHeightMm = integer "lintelHeightMm" geometryElement
                      ModuleHeightMm = integer "moduleHeightMm" geometryElement
                      ModuleWidthMm = integer "moduleWidthMm" geometryElement
                      MortarGapMm = integer "mortarGapMm" geometryElement
                      OpeningHeightMm = integer "openingHeightMm" geometryElement
                      OpeningWidthMm = integer "openingWidthMm" geometryElement
                      StoneCourseHeightMm = integer "stoneCourseHeightMm" geometryElement
                      StoneDepthJitterMm = integer "stoneDepthJitterMm" geometryElement
                      StoneLengthJitterMm = integer "stoneLengthJitterMm" geometryElement
                      StoneOffsetJitterMm = integer "stoneOffsetJitterMm" geometryElement
                      TimberDepthMm = integer "timberDepthMm" geometryElement
                      TimberWidthMm = integer "timberWidthMm" geometryElement
                      WallThicknessMm = integer "wallThicknessMm" geometryElement }
                  Materials =
                    { StoneBaseColorSrgb8 = integerTriple "stoneBaseColorSrgb8" materialsElement
                      StoneMetallicPermille = integer "stoneMetallicPermille" materialsElement
                      StoneRoughnessPermille = integer "stoneRoughnessPermille" materialsElement
                      WoodBaseColorSrgb8 = integerTriple "woodBaseColorSrgb8" materialsElement
                      WoodMetallicPermille = integer "woodMetallicPermille" materialsElement
                      WoodRoughnessPermille = integer "woodRoughnessPermille" materialsElement } }

            if spec.SchemaVersion <> 1 then
                invalidSpec ()

            validateRangesAndRelations spec
            let canonical = canonicalSpecBytes spec

            if not (bytes.AsSpan().SequenceEqual(canonical.AsSpan())) then
                invalidSpec ()

            let modules = referenceMetrics spec

            let decoded =
                modules
                |> Array.sumBy (fun item ->
                    item.Lod0.DecodedGeometryBytes
                    + item.Lod1.DecodedGeometryBytes
                    + item.Lod2.DecodedGeometryBytes
                    + item.Collision.DecodedGeometryBytes)

            { Spec = spec
              CanonicalBytes = canonical
              SpecSha256 = Internal.sha256Hex canonical
              Modules = modules
              FamilyDecodedGeometryBytes = decoded
              RenderPrimitiveCount = modules.Length * 3 * 2 }
        with
        | CalibrationSpecError _ -> reraise ()
        | :? JsonException
        | :? FormatException
        | :? OverflowException -> invalidSpec ()

    let private validRelativeSpecPath (relativePath: string) =
        try
            if isNull relativePath || relativePath.Length = 0 then
                false
            else
                let utf8 = Constants.Utf8NoBom.GetByteCount(relativePath)
                let segments = relativePath.Split('/')

                utf8 <= 240
                && (relativePath.StartsWith("assets/specs/3d/", StringComparison.Ordinal)
                    || relativePath.StartsWith("tests/Fixtures/Asset3d/", StringComparison.Ordinal))
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
        with
        | :? ArgumentException
        | :? EncoderFallbackException -> false

    let validateSpecFile (root: string) (relativePath: string) =
        try
            if not (validRelativeSpecPath relativePath) then
                unsafePath ()

            let locations = Workspace.paths root
            let absolute = Path.Combine(locations.Root, relativePath)
            let safe = Workspace.requireSafePath locations "Kalibrierungsspec" false absolute
            let attributes = File.GetAttributes(safe)

            if
                attributes.HasFlag(FileAttributes.Directory)
                || attributes.HasFlag(FileAttributes.ReparsePoint)
            then
                unsafePath ()

            use stream = new FileStream(safe, FileMode.Open, FileAccess.Read, FileShare.Read)

            if stream.Length <= 0L || stream.Length > int64 MaxSpecBytes then
                invalidSpec ()

            let bytes = Array.zeroCreate<byte> (int stream.Length)
            let mutable offset = 0

            while offset < bytes.Length do
                let read = stream.Read(bytes, offset, bytes.Length - offset)

                if read = 0 then
                    unsafePath ()

                offset <- offset + read

            if stream.ReadByte() <> -1 then
                unsafePath ()

            let finalAttributes = File.GetAttributes(safe)

            let safeAfterRead =
                Workspace.requireSafePath locations "Kalibrierungsspec" false absolute

            if
                not (String.Equals(safe, safeAfterRead, StringComparison.Ordinal))
                || stream.Length <> int64 bytes.Length
                || finalAttributes.HasFlag(FileAttributes.Directory)
                || finalAttributes.HasFlag(FileAttributes.ReparsePoint)
                || not (File.Exists(safe))
            then
                unsafePath ()

            parseSpecBytes bytes
        with
        | CalibrationSpecError _ -> reraise ()
        | HarnessException _ -> unsafePath ()
        | :? IOException
        | :? UnauthorizedAccessException
        | :? NotSupportedException
        | :? System.Security.SecurityException -> unsafePath ()

    let private point x y z = { X = x; Y = y; Z = z }
    let private box minimum maximum = { Min = minimum; Max = maximum }
    let private millimetres value = int64 value * 1000L
    let private halfMillimetres value = int64 value * 500L

    let private enclosingBox (boxes: CalibrationBox array) =
        if boxes.Length = 0 then
            invalidSpec ()

        { Min =
            { X = boxes |> Array.minBy (fun item -> item.Min.X) |> (fun item -> item.Min.X)
              Y = boxes |> Array.minBy (fun item -> item.Min.Y) |> (fun item -> item.Min.Y)
              Z = boxes |> Array.minBy (fun item -> item.Min.Z) |> (fun item -> item.Min.Z) }
          Max =
            { X = boxes |> Array.maxBy (fun item -> item.Max.X) |> (fun item -> item.Max.X)
              Y = boxes |> Array.maxBy (fun item -> item.Max.Y) |> (fun item -> item.Max.Y)
              Z = boxes |> Array.maxBy (fun item -> item.Max.Z) |> (fun item -> item.Max.Z) } }

    let private chunksOfEight (boxes: CalibrationBox array) =
        boxes |> Array.chunkBySize 8 |> Array.map enclosingBox

    type private SegmentAxis =
        | AlongX
        | AlongY

    type private NormalRule =
        | Centered
        | CornerSeam

    let private stoneSegment
        (random: Pcg32)
        (geometry: CalibrationGeometry)
        axis
        normalRule
        course
        segmentMin
        segmentMax
        =
        let courseHeight = millimetres geometry.StoneCourseHeightMm
        let gap = millimetres geometry.MortarGapMm
        let cellWidth = 4L * courseHeight
        let parityOffset = if course % 2 = 0 then 0L else 2L * courseHeight
        let mutable cellMin = segmentMin - parityOffset
        let results = ResizeArray<CalibrationBox>()

        while cellMin < segmentMax do
            let cellMax = cellMin + cellWidth
            let clipMin = max cellMin segmentMin
            let clipMax = min cellMax segmentMax

            if clipMax > clipMin then
                let lengthReduction =
                    millimetres (int (random.Bounded(uint32 (geometry.StoneLengthJitterMm + 1))))

                let depthDelta = random.Signed(geometry.StoneDepthJitterMm)
                let tangentOffset = millimetres (random.Signed(geometry.StoneOffsetJitterMm))
                let stoneLength = clipMax - clipMin - gap - lengthReduction

                if stoneLength <= 0L then
                    invalidSpec ()

                let centerMin = clipMin + gap / 2L + stoneLength / 2L
                let centerMax = clipMax - gap / 2L - stoneLength / 2L
                let clippedCenter = (clipMin + clipMax) / 2L
                let center = min centerMax (max centerMin (clippedCenter + tangentOffset))
                let tangentMin = center - stoneLength / 2L
                let tangentMax = center + stoneLength / 2L
                let depth = millimetres (geometry.WallThicknessMm + depthDelta)

                let normalMin, normalMax =
                    match normalRule with
                    | Centered -> -depth / 2L, depth / 2L
                    | CornerSeam ->
                        let seam = halfMillimetres geometry.WallThicknessMm
                        seam - depth, seam

                let zMin = int64 course * courseHeight + gap / 2L
                let zMax = int64 (course + 1) * courseHeight - gap / 2L

                match axis with
                | AlongX -> results.Add(box (point tangentMin normalMin zMin) (point tangentMax normalMax zMax))
                | AlongY -> results.Add(box (point normalMin tangentMin zMin) (point normalMax tangentMax zMax))

            cellMin <- cellMin + cellWidth

        results.ToArray()

    let deriveReferenceGeometry (spec: CalibrationSpec) =
        validateRangesAndRelations spec
        let geometry = spec.Geometry
        let random = Pcg32(spec.Seed)
        let courses = geometry.ModuleHeightMm / geometry.StoneCourseHeightMm
        let widthHalf = halfMillimetres geometry.ModuleWidthMm
        let thicknessHalf = halfMillimetres geometry.WallThicknessMm
        let width = millimetres geometry.ModuleWidthMm
        let height = millimetres geometry.ModuleHeightMm
        let openingHalf = halfMillimetres geometry.OpeningWidthMm
        let openingTop = millimetres (geometry.OpeningHeightMm + geometry.LintelHeightMm)

        let collectSegments definitions =
            let lod0 = ResizeArray<CalibrationBox>()
            let lod1 = ResizeArray<CalibrationBox>()

            for axis, normal, course, minimum, maximum in definitions do
                let segment = stoneSegment random geometry axis normal course minimum maximum
                segment |> Array.iter lod0.Add
                segment |> chunksOfEight |> Array.iter lod1.Add

            lod0.ToArray(), lod1.ToArray()

        let straightDefinitions =
            [| for course in 0 .. courses - 1 -> AlongX, Centered, course, -widthHalf, widthHalf |]

        let straight0, straight1 = collectSegments straightDefinitions

        let cornerDefinitions =
            [| for course in 0 .. courses - 1 -> AlongX, CornerSeam, course, 0L, width
               for course in 0 .. courses - 1 -> AlongY, CornerSeam, course, thicknessHalf, width |]

        let corner0, corner1 = collectSegments cornerDefinitions

        let openingCourse =
            (geometry.OpeningHeightMm + geometry.LintelHeightMm)
            / geometry.StoneCourseHeightMm

        let openingDefinitions =
            [| for course in 0 .. openingCourse - 1 do
                   yield AlongX, Centered, course, -widthHalf, -openingHalf
                   yield AlongX, Centered, course, openingHalf, widthHalf

               for course in openingCourse .. courses - 1 do
                   yield AlongX, Centered, course, -widthHalf, widthHalf |]

        let opening0, opening1 = collectSegments openingDefinitions
        let timberHalfDepth = halfMillimetres geometry.TimberDepthMm
        let timberWidth = millimetres geometry.TimberWidthMm
        let timberHalfWidth = halfMillimetres geometry.TimberWidthMm
        let openingHeight = millimetres geometry.OpeningHeightMm
        let lintelHeight = millimetres geometry.LintelHeightMm

        let straightWood =
            [| box (point -widthHalf -timberHalfDepth 0L) (point (-widthHalf + timberWidth) timberHalfDepth height)
               box (point (widthHalf - timberWidth) -timberHalfDepth 0L) (point widthHalf timberHalfDepth height) |]

        let cornerWood =
            [| box (point 0L 0L 0L) (point timberWidth timberWidth height)
               box (point (width - timberWidth) -timberHalfDepth 0L) (point width timberHalfDepth height)
               box (point -timberHalfDepth (width - timberWidth) 0L) (point timberHalfDepth width height) |]

        let openingWood =
            [| box
                   (point (-openingHalf - timberWidth) -timberHalfDepth 0L)
                   (point -openingHalf timberHalfDepth openingHeight)
               box
                   (point openingHalf -timberHalfDepth 0L)
                   (point (openingHalf + timberWidth) timberHalfDepth openingHeight)
               box
                   (point (-openingHalf - timberWidth) -timberHalfDepth openingHeight)
                   (point (openingHalf + timberWidth) timberHalfDepth (openingHeight + lintelHeight)) |]

        let straight2 =
            [| box (point -widthHalf -thicknessHalf 0L) (point widthHalf thicknessHalf height) |]

        let corner2 =
            [| box (point 0L -thicknessHalf 0L) (point width thicknessHalf height)
               box (point -thicknessHalf thicknessHalf 0L) (point thicknessHalf width height) |]

        let opening2 =
            [| box (point -widthHalf -thicknessHalf 0L) (point -openingHalf thicknessHalf openingTop)
               box (point openingHalf -thicknessHalf 0L) (point widthHalf thicknessHalf openingTop)
               box (point -widthHalf -thicknessHalf openingTop) (point widthHalf thicknessHalf height) |]

        let result id lod0 lod1 lod2 wood =
            let allBoxes = Array.concat [| lod0; lod1; lod2; wood; lod2 |]

            { Id = id
              Lod0StoneBoxes = lod0
              Lod1StoneBoxes = lod1
              Lod2StoneBoxes = lod2
              WoodBoxes = wood
              CollisionBoxes = lod2
              Bounds = enclosingBox allBoxes }

        [| result moduleOrder[0] straight0 straight1 straight2 straightWood
           result moduleOrder[1] corner0 corner1 corner2 cornerWood
           result moduleOrder[2] opening0 opening1 opening2 openingWood |]

    let snapPoints (spec: CalibrationSpec) =
        let halfWidth = int64 spec.Geometry.ModuleWidthMm / 2L
        let width = int64 spec.Geometry.ModuleWidthMm

        [| moduleOrder[0],
           [| { Id = "SNAP_WALL_STRAIGHT_A"
                TranslationMm = -halfWidth, 0L, 0L
                RotationQuarterTurns = 2 }
              { Id = "SNAP_WALL_STRAIGHT_B"
                TranslationMm = halfWidth, 0L, 0L
                RotationQuarterTurns = 0 } |]
           moduleOrder[1],
           [| { Id = "SNAP_WALL_CORNER_A"
                TranslationMm = width, 0L, 0L
                RotationQuarterTurns = 0 }
              { Id = "SNAP_WALL_CORNER_B"
                TranslationMm = 0L, width, 0L
                RotationQuarterTurns = 1 } |]
           moduleOrder[2],
           [| { Id = "SNAP_WALL_OPENING_A"
                TranslationMm = -halfWidth, 0L, 0L
                RotationQuarterTurns = 2 }
              { Id = "SNAP_WALL_OPENING_B"
                TranslationMm = halfWidth, 0L, 0L
                RotationQuarterTurns = 0 } |] |]

    let blenderToGltfMicrometres (x: int64, y: int64, z: int64) = x, z, -y

    let quarterTurnQuaternion quarterTurns =
        if quarterTurns < 0 || quarterTurns > 3 then
            invalidArg (nameof quarterTurns) "Quarter turns must be in 0..3."

        let angle = float quarterTurns * Math.PI / 4.0
        let clean value = if value = 0.0f then 0.0f else value
        clean 0.0f, clean (float32 (Math.Sin(angle))), clean 0.0f, clean (float32 (Math.Cos(angle)))

    let srgb8ToLinear channel =
        if channel < 0 || channel > 255 then
            invalidArg (nameof channel) "sRGB8 channel must be in 0..255."

        let value = float channel / 255.0

        if value <= 0.04045 then
            value / 12.92
        else
            Math.Pow((value + 0.055) / 1.055, 2.4)
