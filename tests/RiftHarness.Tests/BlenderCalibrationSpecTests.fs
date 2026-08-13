namespace RiftHarness.Tests

open System
open System.Collections.Generic
open System.IO
open System.Text.Json
open System.Text.Json.Nodes
open RiftHarness
open global.Json.Schema

[<RequireQualifiedAccess>]
module BlenderCalibrationSpecTests =
    let private repositoryRoot =
        let rec findRoot path =
            if File.Exists(Path.Combine(path, "Riftward.slnx")) then
                path
            else
                let parent = Directory.GetParent(path)

                if isNull parent then
                    failwith "Repository root not found."

                findRoot parent.FullName

        findRoot Environment.CurrentDirectory

    let private assertTrue condition message =
        if not condition then
            failwith message

    let private assertEqual expected actual message =
        if actual <> expected then
            failwith $"{message} Expected {expected}, got {actual}."

    let private assertNear (tolerance: float) (expected: float) (actual: float) message =
        if abs (actual - expected) > tolerance then
            failwith $"{message} Expected {expected}, got {actual}."

    let private referencePath =
        Path.Combine(repositoryRoot, "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json")

    let private reference () =
        File.ReadAllBytes(referencePath) |> BlenderCalibration.parseSpecBytes

    let private expectInvalidBytes label bytes =
        let mutable rejected = false

        try
            BlenderCalibration.parseSpecBytes bytes |> ignore
        with CalibrationSpecError code when code = "INVALID_SPEC" ->
            rejected <- true

        assertTrue rejected $"Invalid calibration spec was accepted: {label}."

    let private expectInvalidSpec label spec =
        BlenderCalibration.canonicalSpecBytes spec |> expectInvalidBytes label

    let private canonicalNodeBytes (node: JsonNode) =
        let bytes = Internal.canonicalJson false (node.ToJsonString())
        Array.append bytes [| byte '\n' |]

    let private asObject () =
        JsonNode.Parse(File.ReadAllBytes(referencePath)).AsObject()

    let schemasAreOfflineStrictAndReferenceValid () =
        let schemaPaths =
            [ Path.Combine(repositoryRoot, ".ai/schemas/blender-calibration-v1.schema.json")
              Path.Combine(repositoryRoot, ".ai/schemas/blender-technique-report.schema.json") ]

        let rec inspectSchemaNode (node: JsonNode) =
            match node with
            | :? JsonObject as item ->
                match item["$ref"] with
                | null -> ()
                | reference ->
                    assertTrue
                        (reference.GetValue<string>().StartsWith("#/", StringComparison.Ordinal))
                        "Calibration schemas must resolve all references offline."

                match item["type"] with
                | null -> ()
                | schemaType when schemaType.GetValue<string>() = "object" ->
                    assertTrue
                        (not (isNull item["additionalProperties"])
                         && not (item["additionalProperties"].GetValue<bool>()))
                        "Every calibration schema object must be closed."
                | _ -> ()

                for property in item do
                    if not (isNull property.Value) then
                        inspectSchemaNode property.Value
            | :? JsonArray as items ->
                for item in items do
                    if not (isNull item) then
                        inspectSchemaNode item
            | _ -> ()

        for path in schemaPaths do
            JsonNode.Parse(File.ReadAllBytes(path)) |> inspectSchemaNode

        let calibrationSchema =
            JsonSchema.FromText(File.ReadAllText(schemaPaths[0], Constants.Utf8NoBom))

        use referenceDocument = JsonDocument.Parse(File.ReadAllBytes(referencePath))

        let result =
            calibrationSchema.Evaluate(
                referenceDocument.RootElement,
                EvaluationOptions(OutputFormat = OutputFormat.List)
            )

        assertTrue result.IsValid "Reference calibration spec does not satisfy its JSON Schema."

        let reportSchema = JsonNode.Parse(File.ReadAllBytes(schemaPaths[1])).AsObject()

        let properties = reportSchema["properties"].AsObject()
        let generatorSources = properties["generatorSources"].AsObject()

        let sourceRefs =
            generatorSources["prefixItems"].AsArray()
            |> Seq.map (fun item -> item["$ref"].GetValue<string>())
            |> Seq.toArray

        let expectedSources =
            [| "#/$defs/sourceAssetJobJournal"
               "#/$defs/sourceBlenderCalibration"
               "#/$defs/sourceDotnetAssetGenerator" |]

        assertTrue (sourceRefs = expectedSources) "Technique-report generator source order is not ordinal."

    let canonicalReferenceSpecIsAccepted () =
        let bytes = File.ReadAllBytes(referencePath)
        let validated = BlenderCalibration.parseSpecBytes bytes

        assertTrue
            (bytes.AsSpan().SequenceEqual(validated.CanonicalBytes.AsSpan()))
            "Reference spec is not byte-canonical."

        assertEqual
            "39faae34c4cd515cb724a8ef1e2e4bee159a232136218fbb8afd8edd52db2cf8"
            validated.SpecSha256
            "Reference spec SHA-256 changed."

        assertEqual "CAL-STONEWOOD-V1" validated.Spec.FamilyId "Family ID changed."
        assertEqual "calibration-v1" validated.Spec.Profile "Profile changed."
        assertEqual 1592594996u validated.Spec.Seed "Reference seed changed."
        assertEqual 255048L validated.FamilyDecodedGeometryBytes "Family decoded byte formula changed."
        assertEqual 18 validated.RenderPrimitiveCount "Render primitive count changed."

    let malformedNoncanonicalAndClosedShapeMatrixIsRejected () =
        let original = File.ReadAllBytes(referencePath)

        [ "missing-final-lf", original[0 .. original.Length - 2]
          "leading-bom", Array.concat [| [| 0xEFuy; 0xBBuy; 0xBFuy |]; original |]
          "trailing-space", Array.concat [| original[0 .. original.Length - 2]; [| byte ' '; byte '\n' |] |]
          "pretty-json", Constants.Utf8NoBom.GetBytes("{\n  \"schemaVersion\": 1\n}\n")
          "not-json", Constants.Utf8NoBom.GetBytes("not-json\n")
          "empty", Array.empty<byte>
          "oversized", Array.create (BlenderCalibration.MaxSpecBytes + 1) (byte 'x') ]
        |> List.iter (fun (label, bytes) -> expectInvalidBytes label bytes)

        let rootFields =
            [| "familyId"; "geometry"; "materials"; "profile"; "schemaVersion"; "seed" |]

        let geometryFields =
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

        let materialFields =
            [| "stoneBaseColorSrgb8"
               "stoneMetallicPermille"
               "stoneRoughnessPermille"
               "woodBaseColorSrgb8"
               "woodMetallicPermille"
               "woodRoughnessPermille" |]

        for field in rootFields do
            let candidate = asObject ()
            candidate.Remove(field) |> ignore
            expectInvalidBytes $"missing-root-{field}" (canonicalNodeBytes candidate)

        for section, fields in [| "geometry", geometryFields; "materials", materialFields |] do
            for field in fields do
                let candidate = asObject ()
                candidate[section].AsObject().Remove(field) |> ignore
                expectInvalidBytes $"missing-{section}-{field}" (canonicalNodeBytes candidate)

        for section in [| None; Some "geometry"; Some "materials" |] do
            let candidate = asObject ()

            match section with
            | None -> candidate["unexpected"] <- JsonValue.Create(1)
            | Some name -> candidate[name].AsObject()["unexpected"] <- JsonValue.Create(1)

            expectInvalidBytes $"extra-{section}" (canonicalNodeBytes candidate)

        for field, value in
            [| "schemaVersion", JsonValue.Create(2) :> JsonNode
               "profile", JsonValue.Create("calibration-v2") :> JsonNode
               "familyId", JsonValue.Create("CAL-OTHER-V1") :> JsonNode |] do
            let candidate = asObject ()
            candidate[field] <- value
            expectInvalidBytes $"fixed-{field}" (canonicalNodeBytes candidate)

        for field in [| "stoneBaseColorSrgb8"; "woodBaseColorSrgb8" |] do
            for length in [| 0; 2; 4 |] do
                let candidate = asObject ()
                let colors = JsonArray()

                for _ in 1..length do
                    colors.Add(JsonValue.Create(0))

                candidate["materials"].AsObject()[field] <- colors
                expectInvalidBytes $"{field}-length-{length}" (canonicalNodeBytes candidate)

        let wrongType = asObject ()
        wrongType["seed"] <- JsonValue.Create("1592594996")
        expectInvalidBytes "wrong-type" (canonicalNodeBytes wrongType)

        let exponent =
            Constants.Utf8NoBom.GetString(original).Replace("1592594996", "1.592594996e9")

        expectInvalidBytes "exponent-number" (Constants.Utf8NoBom.GetBytes(exponent))

        let duplicate =
            Constants.Utf8NoBom
                .GetString(original)
                .Replace("{\"familyId\"", "{\"familyId\":\"CAL-STONEWOOD-V1\",\"familyId\"", StringComparison.Ordinal)

        expectInvalidBytes "duplicate-key" (Constants.Utf8NoBom.GetBytes(duplicate))

        let tooManyProperties = asObject ()

        for index in 0..64 do
            tooManyProperties[$"extra{index:D2}"] <- JsonValue.Create(index)

        expectInvalidBytes "property-count" (canonicalNodeBytes tooManyProperties)

        let deep = asObject ()
        deep["unexpected"] <- JsonNode.Parse("{\"a\":{\"b\":{\"c\":{\"d\":{\"e\":{\"f\":1}}}}}}")
        expectInvalidBytes "json-depth" (canonicalNodeBytes deep)

    let fieldBoundaryMatrixIsEnforced () =
        let baseline = (reference ()).Spec
        let geometry = baseline.Geometry
        let materials = baseline.Materials

        let validCases =
            [ "seed-min", { baseline with Seed = 0u }
              "seed-max", { baseline with Seed = UInt32.MaxValue }
              "height-min",
              { baseline with
                  Geometry =
                      { geometry with
                          ModuleHeightMm = 2400
                          StoneCourseHeightMm = 300
                          OpeningHeightMm = 1800
                          LintelHeightMm = 300 } }
              "height-max",
              { baseline with
                  Geometry =
                      { geometry with
                          ModuleHeightMm = 3600
                          StoneCourseHeightMm = 300
                          OpeningHeightMm = 1800
                          LintelHeightMm = 300 } }
              "thickness-min",
              { baseline with
                  Geometry = { geometry with WallThicknessMm = 300 } }
              "thickness-max",
              { baseline with
                  Geometry = { geometry with WallThicknessMm = 600 } }
              "opening-width-min",
              { baseline with
                  Geometry = { geometry with OpeningWidthMm = 1200 } }
              "opening-width-max",
              { baseline with
                  Geometry = { geometry with OpeningWidthMm = 2000 } }
              "opening-height-min",
              { baseline with
                  Geometry =
                      { geometry with
                          OpeningHeightMm = 1800
                          StoneCourseHeightMm = 300
                          LintelHeightMm = 300 } }
              "opening-height-max",
              { baseline with
                  Geometry =
                      { geometry with
                          OpeningHeightMm = 2400
                          StoneCourseHeightMm = 300
                          LintelHeightMm = 300 } }
              "lintel-min",
              { baseline with
                  Geometry = { geometry with LintelHeightMm = 250 } }
              "course-and-lintel-max",
              { baseline with
                  Geometry =
                      { geometry with
                          ModuleHeightMm = 3200
                          StoneCourseHeightMm = 400
                          LintelHeightMm = 400 } }
              "gap-min",
              { baseline with
                  Geometry = { geometry with MortarGapMm = 10 } }
              "gap-max",
              { baseline with
                  Geometry = { geometry with MortarGapMm = 40 } }
              "timber-width-min",
              { baseline with
                  Geometry = { geometry with TimberWidthMm = 120 } }
              "timber-width-max",
              { baseline with
                  Geometry = { geometry with TimberWidthMm = 240 } }
              "timber-depth-min",
              { baseline with
                  Geometry = { geometry with TimberDepthMm = 100 } }
              "timber-depth-max",
              { baseline with
                  Geometry = { geometry with TimberDepthMm = 240 } }
              "length-jitter-min",
              { baseline with
                  Geometry =
                      { geometry with
                          StoneLengthJitterMm = 0 } }
              "length-jitter-max",
              { baseline with
                  Geometry =
                      { geometry with
                          StoneLengthJitterMm = 80 } }
              "depth-jitter-min",
              { baseline with
                  Geometry = { geometry with StoneDepthJitterMm = 0 } }
              "depth-jitter-max",
              { baseline with
                  Geometry =
                      { geometry with
                          StoneDepthJitterMm = 60 } }
              "offset-jitter-min",
              { baseline with
                  Geometry =
                      { geometry with
                          StoneOffsetJitterMm = 0 } }
              "offset-jitter-max",
              { baseline with
                  Geometry =
                      { geometry with
                          StoneOffsetJitterMm = 40 } }
              "colors-min",
              { baseline with
                  Materials =
                      { materials with
                          StoneBaseColorSrgb8 = [| 0; 0; 0 |]
                          WoodBaseColorSrgb8 = [| 0; 0; 0 |] } }
              "colors-max",
              { baseline with
                  Materials =
                      { materials with
                          StoneBaseColorSrgb8 = [| 255; 255; 255 |]
                          WoodBaseColorSrgb8 = [| 255; 255; 255 |] } }
              "roughness-min",
              { baseline with
                  Materials =
                      { materials with
                          StoneRoughnessPermille = 500
                          WoodRoughnessPermille = 500 } }
              "roughness-max",
              { baseline with
                  Materials =
                      { materials with
                          StoneRoughnessPermille = 1000
                          WoodRoughnessPermille = 1000 } }
              "metallic-min",
              { baseline with
                  Materials =
                      { materials with
                          StoneMetallicPermille = 0
                          WoodMetallicPermille = 0 } }
              "metallic-max",
              { baseline with
                  Materials =
                      { materials with
                          StoneMetallicPermille = 100
                          WoodMetallicPermille = 100 } } ]

        for label, spec in validCases do
            try
                spec
                |> BlenderCalibration.canonicalSpecBytes
                |> BlenderCalibration.parseSpecBytes
                |> ignore
            with CalibrationSpecError _ ->
                failwith $"Valid boundary calibration spec was rejected: {label}."

        let invalidCases =
            [ "height-below",
              { baseline with
                  Geometry = { geometry with ModuleHeightMm = 2399 } }
              "height-above",
              { baseline with
                  Geometry = { geometry with ModuleHeightMm = 3601 } }
              "width-not-fixed",
              { baseline with
                  Geometry = { geometry with ModuleWidthMm = 3999 } }
              "thickness-below",
              { baseline with
                  Geometry = { geometry with WallThicknessMm = 299 } }
              "thickness-above",
              { baseline with
                  Geometry = { geometry with WallThicknessMm = 601 } }
              "opening-width-below",
              { baseline with
                  Geometry = { geometry with OpeningWidthMm = 1199 } }
              "opening-width-odd",
              { baseline with
                  Geometry = { geometry with OpeningWidthMm = 1601 } }
              "opening-width-above",
              { baseline with
                  Geometry = { geometry with OpeningWidthMm = 2001 } }
              "opening-height-below",
              { baseline with
                  Geometry = { geometry with OpeningHeightMm = 1799 } }
              "opening-height-above",
              { baseline with
                  Geometry = { geometry with OpeningHeightMm = 2401 } }
              "lintel-below",
              { baseline with
                  Geometry = { geometry with LintelHeightMm = 249 } }
              "lintel-above",
              { baseline with
                  Geometry = { geometry with LintelHeightMm = 401 } }
              "course-below",
              { baseline with
                  Geometry =
                      { geometry with
                          StoneCourseHeightMm = 249 } }
              "course-above",
              { baseline with
                  Geometry =
                      { geometry with
                          StoneCourseHeightMm = 401 } }
              "gap-below",
              { baseline with
                  Geometry = { geometry with MortarGapMm = 9 } }
              "gap-above",
              { baseline with
                  Geometry = { geometry with MortarGapMm = 41 } }
              "timber-width-below",
              { baseline with
                  Geometry = { geometry with TimberWidthMm = 119 } }
              "timber-width-above",
              { baseline with
                  Geometry = { geometry with TimberWidthMm = 241 } }
              "timber-depth-below",
              { baseline with
                  Geometry = { geometry with TimberDepthMm = 99 } }
              "timber-depth-above",
              { baseline with
                  Geometry = { geometry with TimberDepthMm = 241 } }
              "length-jitter-below",
              { baseline with
                  Geometry =
                      { geometry with
                          StoneLengthJitterMm = -1 } }
              "length-jitter-above",
              { baseline with
                  Geometry =
                      { geometry with
                          StoneLengthJitterMm = 81 } }
              "depth-jitter-below",
              { baseline with
                  Geometry =
                      { geometry with
                          StoneDepthJitterMm = -1 } }
              "depth-jitter-above",
              { baseline with
                  Geometry =
                      { geometry with
                          StoneDepthJitterMm = 61 } }
              "offset-jitter-below",
              { baseline with
                  Geometry =
                      { geometry with
                          StoneOffsetJitterMm = -1 } }
              "offset-jitter-above",
              { baseline with
                  Geometry =
                      { geometry with
                          StoneOffsetJitterMm = 41 } }
              "color-below",
              { baseline with
                  Materials =
                      { materials with
                          StoneBaseColorSrgb8 = [| -1; 92; 82 |] } }
              "color-above",
              { baseline with
                  Materials =
                      { materials with
                          WoodBaseColorSrgb8 = [| 256; 58; 32 |] } }
              "roughness-below",
              { baseline with
                  Materials =
                      { materials with
                          StoneRoughnessPermille = 499 } }
              "roughness-above",
              { baseline with
                  Materials =
                      { materials with
                          WoodRoughnessPermille = 1001 } }
              "metallic-below",
              { baseline with
                  Materials =
                      { materials with
                          StoneMetallicPermille = -1 } }
              "metallic-above",
              { baseline with
                  Materials =
                      { materials with
                          WoodMetallicPermille = 101 } } ]

        invalidCases |> List.iter (fun (label, spec) -> expectInvalidSpec label spec)

        let seedBelow = asObject ()
        seedBelow["seed"] <- JsonValue.Create(-1L)
        expectInvalidBytes "seed-below" (canonicalNodeBytes seedBelow)
        let seedAbove = asObject ()
        seedAbove["seed"] <- JsonValue.Create(4294967296L)
        expectInvalidBytes "seed-above" (canonicalNodeBytes seedAbove)

    let crossFieldFormulaMatrixIsEnforced () =
        let baseline = (reference ()).Spec
        let geometry = baseline.Geometry

        [ "height-course-divisibility",
          { baseline with
              Geometry = { geometry with ModuleHeightMm = 3001 } }
          "opening-course-divisibility",
          { baseline with
              Geometry = { geometry with OpeningHeightMm = 2001 } }
          "lintel-course-divisibility",
          { baseline with
              Geometry = { geometry with LintelHeightMm = 251 } }
          "opening-lintel-height",
          { baseline with
              Geometry =
                  { geometry with
                      ModuleHeightMm = 2400
                      StoneCourseHeightMm = 300
                      OpeningHeightMm = 2100
                      LintelHeightMm = 300 } }
          "opening-timber-side-budget",
          { baseline with
              Geometry =
                  { geometry with
                      ModuleHeightMm = 3200
                      StoneCourseHeightMm = 400
                      LintelHeightMm = 400
                      OpeningWidthMm = 2000
                      TimberWidthMm = 240 } }
          "timber-depth",
          { baseline with
              Geometry =
                  { geometry with
                      WallThicknessMm = 300
                      TimberDepthMm = 301 } }
          "reachable-corner-vertex-budget",
          { baseline with
              Geometry = { geometry with ModuleHeightMm = 3500 } } ]
        |> List.iter (fun (label, spec) -> expectInvalidSpec label spec)

        [ "TIMBER_DEPTH",
          { geometry with
              WallThicknessMm = 300
              TimberDepthMm = 301 }
          "MORTAR_COURSE_BUDGET", { geometry with MortarGapMm = 63 }
          "TANGENT_JITTER_BUDGET",
          { geometry with
              StoneOffsetJitterMm = 100 }
          "DEPTH_JITTER_BUDGET",
          { geometry with
              WallThicknessMm = 300
              StoneDepthJitterMm = 151 } ]
        |> List.iter (fun (expected, candidate) ->
            assertTrue
                (BlenderCalibration.relationViolations candidate = [| expected |])
                $"Pure relation evaluator did not isolate {expected}.")

        let sliver =
            { baseline with
                Geometry =
                    { geometry with
                        ModuleHeightMm = 3000
                        StoneCourseHeightMm = 300
                        OpeningHeightMm = 1800
                        LintelHeightMm = 300
                        OpeningWidthMm = 1598 } }

        assertTrue
            (BlenderCalibration.relationViolations sliver.Geometry
             |> Array.contains "STONE_SEGMENT_SLIVER")
            "Sliver relation was not detected."

        expectInvalidSpec "opening-side-sliver" sliver

        for _, candidate in
            [ "reference", baseline
              "alternative-seed", { baseline with Seed = 1592594997u } ] do
            let validated =
                candidate
                |> BlenderCalibration.canonicalSpecBytes
                |> BlenderCalibration.parseSpecBytes

            BlenderCalibration.deriveReferenceGeometry validated.Spec |> ignore

    let pcg32MatchesPublishedVectors () =
        let expected =
            [| 2931784231u
               1733122091u
               677491881u
               1055047052u
               458198092u
               2644956477u
               2758542496u
               1581573961u
               1174968268u
               55324810u |]

        let random = Pcg32(1592594996u)
        let actual = Array.init expected.Length (fun _ -> random.NextUInt32())
        assertTrue (actual = expected) "PCG32 reference vector changed."

        let bounded = Pcg32(1592594996u)
        let bounds = [| 1u; 2u; 3u; 41u; 61u; 0x80000000u |]
        let boundedExpected = [| 0u; 1u; 0u; 38u; 8u; 497472829u |]
        let boundedActual = bounds |> Array.map bounded.Bounded
        assertTrue (boundedActual = boundedExpected) "PCG32 bounded vector changed."

        let signed = Pcg32(1592594996u)
        let jitters = [| 0; 30; 20; 40; 1; 60 |]
        let signedExpected = [| 0; 4; -11; -20; 0; -7 |]
        let signedActual = jitters |> Array.map signed.Signed
        assertTrue (signedActual = signedExpected) "PCG32 signed vector changed."

        let alternative = Pcg32(1592594997u)

        assertEqual 3523413489u (alternative.NextUInt32()) "Alternative seed must have a fixed, distinct sequence."

    let referenceMetricsCandidatesAndBoundsMatchContract () =
        let validated = reference ()
        let metrics = validated.Modules

        let expected =
            [| "WALL-STRAIGHT", (1344, 672, 336, 168, 72, 36, 12, 61968L)
               "WALL-CORNER", (2664, 1332, 648, 324, 120, 60, 24, 121416L)
               "WALL-OPENING", (1272, 636, 576, 288, 144, 72, 36, 71664L) |]

        for index in 0 .. expected.Length - 1 do
            let id, (lod0V, lod0T, lod1V, lod1T, lod2V, lod2T, collisionT, decoded) =
                expected[index]

            let actual = metrics[index]
            assertEqual id actual.Id "Module order changed."
            assertEqual lod0V actual.Lod0.Vertices $"{id} LOD0 vertices changed."
            assertEqual lod0T actual.Lod0.Triangles $"{id} LOD0 triangles changed."
            assertEqual lod1V actual.Lod1.Vertices $"{id} LOD1 vertices changed."
            assertEqual lod1T actual.Lod1.Triangles $"{id} LOD1 triangles changed."
            assertEqual lod2V actual.Lod2.Vertices $"{id} LOD2 vertices changed."
            assertEqual lod2T actual.Lod2.Triangles $"{id} LOD2 triangles changed."
            assertEqual collisionT actual.Collision.Triangles $"{id} collision triangles changed."

            let actualDecoded =
                actual.Lod0.DecodedGeometryBytes
                + actual.Lod1.DecodedGeometryBytes
                + actual.Lod2.DecodedGeometryBytes
                + actual.Collision.DecodedGeometryBytes

            assertEqual decoded actualDecoded $"{id} decoded byte formula changed."

        let geometry = BlenderCalibration.deriveReferenceGeometry validated.Spec
        assertEqual 54 geometry[0].Lod0StoneBoxes.Length "Straight candidate count changed."
        assertEqual 108 geometry[1].Lod0StoneBoxes.Length "Corner candidate count changed."
        assertEqual 50 geometry[2].Lod0StoneBoxes.Length "Opening candidate count changed."
        assertEqual 12 geometry[0].Lod1StoneBoxes.Length "Straight LOD1 group count changed."
        assertEqual 24 geometry[1].Lod1StoneBoxes.Length "Corner LOD1 group count changed."
        assertEqual 21 geometry[2].Lod1StoneBoxes.Length "Opening LOD1 group count changed."

        let expectedReferenceBounds =
            [| { Min = { X = -2000000L; Y = -215000L; Z = 0L }
                 Max =
                   { X = 2000000L
                     Y = 215000L
                     Z = 3000000L } }
               { Min = { X = -230000L; Y = -229000L; Z = 0L }
                 Max =
                   { X = 4000000L
                     Y = 4000000L
                     Z = 3000000L } }
               { Min = { X = -2000000L; Y = -214000L; Z = 0L }
                 Max =
                   { X = 2000000L
                     Y = 214000L
                     Z = 3000000L } } |]

        assertTrue
            (geometry |> Array.map (fun item -> item.Bounds) = expectedReferenceBounds)
            "Reference-seed module bounds changed."

        let alternativeSpec =
            { validated.Spec with
                Seed = 1592594997u }

        let alternative = BlenderCalibration.deriveReferenceGeometry alternativeSpec

        let expectedAlternativeBounds =
            [| { Min = { X = -2000000L; Y = -214500L; Z = 0L }
                 Max =
                   { X = 2000000L
                     Y = 214500L
                     Z = 3000000L } }
               { Min = { X = -230000L; Y = -230000L; Z = 0L }
                 Max =
                   { X = 4000000L
                     Y = 4000000L
                     Z = 3000000L } }
               { Min = { X = -2000000L; Y = -214500L; Z = 0L }
                 Max =
                   { X = 2000000L
                     Y = 214500L
                     Z = 3000000L } } |]

        assertTrue
            (alternative |> Array.map (fun item -> item.Bounds) = expectedAlternativeBounds)
            "Alternative-seed module bounds changed."

        let corner = geometry[1]
        let seam = int64 validated.Spec.Geometry.WallThicknessMm * 500L

        assertTrue
            (corner.Lod0StoneBoxes[0].Max.Y = seam && corner.Lod0StoneBoxes[54].Max.X = seam)
            "Corner depth jitter moved a seam-facing face."

    let snapAxisQuaternionAndColorMathMatchContract () =
        let spec = (reference ()).Spec
        let snaps = BlenderCalibration.snapPoints spec
        assertTrue (snaps |> Array.map fst = BlenderCalibration.moduleOrder) "Snap module order changed."
        let _, corner = snaps[1]
        assertEqual (4000L, 0L, 0L) corner[0].TranslationMm "Corner snap A changed."
        assertEqual (0L, 4000L, 0L) corner[1].TranslationMm "Corner snap B changed."
        assertEqual 1 corner[1].RotationQuarterTurns "Corner snap B rotation changed."

        assertEqual
            (1000000L, 3000000L, -2000000L)
            (BlenderCalibration.blenderToGltfMicrometres (1000000L, 2000000L, 3000000L))
            "Axis conversion changed."

        let x, y, z, w = BlenderCalibration.quarterTurnQuaternion 1
        assertEqual 0.0f x "Quarter-turn quaternion X changed."
        assertNear 0.000001 (sqrt 0.5) (float y) "Quarter-turn quaternion Y changed."
        assertEqual 0.0f z "Quarter-turn quaternion Z changed."
        assertNear 0.000001 (sqrt 0.5) (float w) "Quarter-turn quaternion W changed."
        assertNear 1e-12 0.0 (BlenderCalibration.srgb8ToLinear 0) "sRGB black conversion changed."
        assertNear 1e-12 1.0 (BlenderCalibration.srgb8ToLinear 255) "sRGB white conversion changed."

    let safeSpecFileBoundaryIsEnforced () =
        let root =
            Path.Combine(Path.GetTempPath(), "Riftward.CalibrationSpec-" + Guid.NewGuid().ToString("N"))

        try
            let relative = "assets/specs/3d/CAL-STONEWOOD-V1.calibration-v1.json"
            let target = Path.Combine(root, relative)
            Directory.CreateDirectory(Path.GetDirectoryName(target)) |> ignore
            File.Copy(referencePath, target)
            let valid = BlenderCalibration.validateSpecFile root relative
            assertEqual "CAL-STONEWOOD-V1" valid.Spec.FamilyId "Safe spec file was rejected."

            for unsafe in
                [ Path.GetFullPath(target)
                  "../CAL-STONEWOOD-V1.calibration-v1.json"
                  "assets\\specs\\3d\\CAL-STONEWOOD-V1.calibration-v1.json"
                  "assets/specs/3d/./CAL-STONEWOOD-V1.calibration-v1.json"
                  "README.md" ] do
                let mutable rejected = false

                try
                    BlenderCalibration.validateSpecFile root unsafe |> ignore
                with CalibrationSpecError code when code = "UNSAFE_PATH" ->
                    rejected <- true

                assertTrue rejected "Unsafe spec path was accepted."

            let directoryPath =
                Path.Combine(root, "assets/specs/3d/directory.calibration-v1.json")

            Directory.CreateDirectory(directoryPath) |> ignore
            let mutable directoryRejected = false

            try
                BlenderCalibration.validateSpecFile root "assets/specs/3d/directory.calibration-v1.json"
                |> ignore
            with CalibrationSpecError code when code = "UNSAFE_PATH" ->
                directoryRejected <- true

            assertTrue directoryRejected "Directory disguised as a spec file was accepted."

            let oversizedPath =
                Path.Combine(root, "assets/specs/3d/oversized.calibration-v1.json")

            File.WriteAllBytes(oversizedPath, Array.zeroCreate<byte> (BlenderCalibration.MaxSpecBytes + 1))
            let mutable oversizedRejected = false

            try
                BlenderCalibration.validateSpecFile root "assets/specs/3d/oversized.calibration-v1.json"
                |> ignore
            with CalibrationSpecError code when code = "INVALID_SPEC" ->
                oversizedRejected <- true

            assertTrue oversizedRejected "Oversized spec file was accepted."

            let external = Path.Combine(root, "external.json")
            File.Copy(referencePath, external)
            let link = Path.Combine(root, "assets/specs/3d/link.calibration-v1.json")
            File.CreateSymbolicLink(link, external) |> ignore
            let mutable symlinkRejected = false

            try
                BlenderCalibration.validateSpecFile root "assets/specs/3d/link.calibration-v1.json"
                |> ignore
            with CalibrationSpecError code when code = "UNSAFE_PATH" ->
                symlinkRejected <- true

            assertTrue symlinkRejected "Symlinked spec file was accepted."
        finally
            if Directory.Exists(root) then
                Directory.Delete(root, true)
