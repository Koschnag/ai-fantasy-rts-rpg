module BenchRepresentativeTests

open System
open System.Collections.Generic
open System.IO
open System.Text.Json
open Riftward.App
open Riftward.App.Bench
open Riftward.Platform
open Riftward.Simulation

let private repositoryRoot =
    let rec findRoot path =
        if File.Exists(Path.Combine(path, "Riftward.slnx")) then
            path
        else
            match Directory.GetParent(path) with
            | null -> failwith "Repository-Wurzel nicht gefunden."
            | parent -> findRoot parent.FullName

    findRoot Environment.CurrentDirectory

let private defaultSeed = 20260824u

/// Der Szenariospiegel haelt Abschnitt 0 an PERFORMANCE_BUDGET.md und den Auftrag fest (AC-T023-02).
let scenarioConfigMirrorsDocumentedBudgetTable () =
    if RepresentativeScenario.VisibleUnitsTarget <> 350 then
        failwith "Sichtbare Einheiten weichen vom Szenebudget (350) ab."

    if
        RepresentativeScenario.SimulatedAgents <> 250
        || SimulationContract.AgentCount <> 250
    then
        failwith "Simulierte Agenten muessen exakt der Vertragszahl 250 entsprechen."

    if
        RepresentativeScenario.BackgroundActors
        <> RepresentativeScenario.VisibleUnitsTarget
           - RepresentativeScenario.SimulatedAgents
    then
        failwith "Hintergrundakteure ergeben keine nichtdegenerative Aufteilung auf 350 sichtbare Einheiten."

    if RepresentativeScenario.BonesPerNormalUnit < 48 then
        failwith "Knochen je normaler Einheit unterschreiten das Szenebudget von 48."

    if RepresentativeScenario.BonesPerNormalUnit <> RepresentativeRig.BoneCount then
        failwith "Rig-Knochenzahl spiegelt die Szenariokonfiguration nicht."

    if
        RepresentativeScenario.SunLights <> 1
        || RepresentativeScenario.LocalShadowLights <> 4
    then
        failwith "Lichtbudget entspricht nicht der Szenebudgettabelle (eine Sonne plus vier lokale Lichter)."

    if
        RepresentativeScenario.ParticlePeakTarget <> 5000
        || RepresentativeScenario.ParticlePeakTarget > 5000
    then
        failwith "Partikelspitze verletzt die dokumentierte Obergrenze von 5000."

    if RepresentativeScenario.FramesPerSimTick < 1 then
        failwith "Tick-Zuordnung degeneriert."

    let limits = RepresentativeScenario.BudgetLimits.Documented

    if limits.P99FrameTimeLimitMs <> 33.3 then
        failwith "Frame-Grenzwert weicht von PERFORMANCE_BUDGET.md ab."

    if limits.P99GpuTimeHardLimitMs <> 30.0 || limits.P99GpuTimeTargetMs <> 14.0 then
        failwith "GPU-Grenzen entsprechen nicht der Dokumentzeile (14 ms Ziel, 30 ms hart)."

    if limits.P99TickTimeHardLimitMs <> 16.0 || limits.P99TickTimeTargetMs <> 8.0 then
        failwith "Tick-Grenzen entsprechen nicht der Dokumentzeile (8 ms Ziel, 16 ms hart)."

    if limits.ManagedAllocationsPerWarmFrameLimitBytes <> 1024.0 then
        failwith "Allokationsgrenze verletzt den AC-T010-07/T-020-Praezedenz (1 KiB)."

    if limits.DrawSubmitCallsPerFrameLimit <> 1200 then
        failwith "Draw-/Submit-Grenze weicht vom Szenebudget ab."

    if limits.VisibleTrianglesMainViewLimit <> 2_000_000L then
        failwith "Dreiecksgrenze weicht vom Szenebudget Low ab."

    if limits.ConcurrentParticlesLimit <> 5000L then
        failwith "Partikelgrenze spiegelt die Szenebudgettabelle nicht."

    if limits.RuntimeShaderCompilationAllowed then
        failwith "Laufzeitshaderkompilierung bleibt verboten."

    if
        limits.WorkingSetTargetMiB
        <> RepresentativeScenario.BudgetDocumentation.WorkingSetTargetMiB
        || limits.WorkingSetTargetMiB <> 3500L
        || limits.WorkingSetHardLimitMiB
           <> RepresentativeScenario.BudgetDocumentation.WorkingSetHardLimitMiB
        || limits.WorkingSetHardLimitMiB <> 4500L
    then
        failwith "Arbeitssatzgrenzen spiegeln die Dokumentzeile 3,5 GB Ziel / 4,5 GB hart nicht."

    // Das Budgetdokument existiert und nennt die gebundenen Kennzahlen.
    let document =
        File.ReadAllText(Path.Combine(repositoryRoot, "docs", "PERFORMANCE_BUDGET.md"))

    for marker in [ "350"; "250"; "48"; "5.000"; "1 Sonne + 4 lokale"; "1.200"; "2 Mio." ] do
        if not (document.Contains(marker, StringComparison.Ordinal)) then
            failwith $"PERFORMANCE_BUDGET.md nennt die Kompositionsgroesse {marker} nicht."

/// Geometrie und Instanzfuellung erzeugen nichtdegenerative Lastklassen (AC-T023-02).
let compositionTargetsProduceNonDegenerateGeometry () =
    let units = RepresentativeMesh.BuildUnitMesh()

    if units.TriangleCount <> RepresentativeMesh.TrianglesPerUnit then
        failwith $"Einheitenmesh hat {units.TriangleCount} Dreiecke statt {RepresentativeMesh.TrianglesPerUnit}."

    if
        RepresentativeMesh.TrianglesPerUnit
        <> RepresentativeScenario.BonesPerNormalUnit * 12
    then
        failwith "Je Knochen fehlt ein Boxsegment des Skinningpfads."

    if units.VertexCount * RepresentativeMesh.UnitVertexStride <> units.Vertices.Length then
        failwith "Einheiten-VB-Laenge widerspricht dem festen Layout."

    if units.Indices.Length <> units.TriangleCount * 3 * 2 then
        failwith "Indexbuffer des Einheitenmesh ist unvollstaendig."

    let highestIndex =
        seq {
            for chunk = 0 to (units.Indices.Length / 2) - 1 do
                int units.Indices[(chunk * 2)] ||| (int units.Indices[(chunk * 2) + 1] <<< 8)
        }
        |> Seq.max

    if highestIndex >= units.VertexCount then
        failwith "Einheiten-Indizes verweisen hinter den Vertexbuffer."

    let terrain = RepresentativeMesh.BuildTerrain()

    if terrain.TriangleCount < 100_000 then
        failwith "Landschaftsanteil ist kuenstlich leer."

    if terrain.TriangleCount > 2_000_000 then
        failwith "Landschaft sprengt das Low-Dreiecksbudget allein."

    let quad = RepresentativeMesh.BuildParticleQuad()

    if quad.Vertices.Length <> 4 * RepresentativeMesh.ParticleVertexStride then
        failwith "Partikelquad-Geometrie degeneriert."

    let mainViewTriangles =
        int64 terrain.TriangleCount
        + ((int64) RepresentativeMesh.TrianglesPerUnit
           * (int64) RepresentativeScenario.VisibleUnitsTarget)
        + (2L * (int64) RepresentativeScenario.ParticlePeakTarget)

    if mainViewTriangles >= 2_000_000L then
        failwith $"Komposition ueberschreitet das Dreiecksbudget bereits statisch ({mainViewTriangles})."

/// Hoehenfeld, Normalen und Lichtplatzierung sind deterministisch und weltgebunden (AC-T023-02).
let landscapeHeightFieldIsDeterministicAndWalkableAware () =
    let rollingSample = RepresentativeLandscape.HeightAt(7.3, -11.9)

    if RepresentativeLandscape.HeightAt(7.3, -11.9) <> rollingSample then
        failwith "Hoehenfeld ist nicht deterministisch."

    // Randkacheln der Graybox-Welt sind Waende; dort gilt das Plateau.
    let wallWorldX = RepresentativeLandscape.ToWorldX 0.0
    let wallWorldZ = RepresentativeLandscape.ToWorldZ 45.0

    if NavWorld.IsWalkable(0, 45) then
        failwith "Fixture erwartet eine Randwand bei (0,45)."

    if RepresentativeLandscape.HeightAt(wallWorldX, wallWorldZ) < RepresentativeLandscape.WallHeightMeters then
        failwith "Wandhoehe wurde nicht auf nicht begehbare Kacheln angewandt."

    if abs (RepresentativeLandscape.NormalUpComponent(0.0, 0.0) - 1.0) > 0.5 then
        failwith "Normalen des rollenden Feldes degenerieren."

    let placements = RepresentativeLandscape.LightPlacements()

    if placements.Length <> RepresentativeScenario.LocalShadowLights then
        failwith "Lichtplatzierung erzeugt nicht genau vier lokale Lichter."

    let secondPass = RepresentativeLandscape.LightPlacements()

    if placements <> secondPass then
        failwith "Lichtplatzierung ist nicht deterministisch."

    for placement in placements do
        if placement.Radius <= 0.0 then
            failwith "Lichtreichweite degeneriert."

/// Die 48-Bone-Palette ist deterministisch, voll gross und phasenabhängig (AC-T023-02/05).
let rigEvaluatesFortyEightBonePaletteDeterministically () =
    let evaluator = RepresentativeRig.PoseEvaluator()
    let rowLength = RepresentativeScenario.BonesPerNormalUnit * 3 * 4
    let first = Array.zeroCreate<float32> rowLength
    let second = Array.zeroCreate<float32> rowLength
    let third = Array.zeroCreate<float32> rowLength

    evaluator.EvaluateRow(defaultSeed, 1.75, Span<float32>(first))
    evaluator.EvaluateRow(defaultSeed, 1.75, Span<float32>(second))
    evaluator.EvaluateRow(defaultSeed, 2.50, Span<float32>(third))

    if first <> second then
        failwith "Identische Pose lieferte unterschiedliche Palettenbytes."

    if first = third then
        failwith "Verschiedene Posen ergaben identische Palettenbytes."

    // Bind-Pose-Naehe: kleine Amplituden halten die Hautmatrix nahe der Ruhepose.
    let bindLike = Array.zeroCreate<float32> rowLength
    evaluator.EvaluateRow(defaultSeed, 0.0, Span<float32>(bindLike))
    let mutable maxAbs = 0.0

    for value in bindLike do
        maxAbs <- Math.Max(maxAbs, Math.Abs(float value))

    if maxAbs > 2.0 then
        failwith $"Palettenwerte verlassen den Graybox-Bereich ({maxAbs})."

/// Das Kameraflugskript bleibt deterministisch und hashgebunden (AC-T023-01/03).
let cameraFlightIsDeterministicAndCanonical () =
    let samples = RepresentativeCameraFlight.Samples(defaultSeed, 64)
    let repeat = RepresentativeCameraFlight.Samples(defaultSeed, 64)

    if samples.Count <> 64 then
        failwith "Kameraflug-Samples unvollstaendig."

    if samples[17] <> repeat[17] then
        failwith "Identischer Seed lieferte unterschiedliche Kamerabahnen."

    let foreign = RepresentativeCameraFlight.Samples(defaultSeed + 1u, 64)

    if foreign[17] = samples[17] then
        failwith "Fremder Seed liess die Bahn unveraendert."

    let hash = RepresentativeCameraFlight.HashHex(samples)

    if hash.Length <> 64 || hash <> hash.ToLowerInvariant() then
        failwith "Kamerahash ist kein kanonischer SHA-256-Hexwert."

    if hash <> RepresentativeCameraFlight.HashHex(repeat) then
        failwith "Kamerahash ist instabil."

    // Quantisierte Kanonform bleibt im Graybox-Fenster (Radius 52 bis 78 m,
    // Erhoehung 27 bis 62 Grad; das Auge bleibt damit stets ueber der
    // Landschaft, siehe AC-T023-02).
    for sample in samples do
        if sample.RadiusMeters < 51.0 || sample.RadiusMeters > 79.0 then
            failwith $"Orbitradius {sample.RadiusMeters} verlaesst das Szenariofenster."

        if sample.PitchDegrees < 26.0 || sample.PitchDegrees > 63.0 then
            failwith "Neigungswinkel verlaesst das Szenariofenster."

    // Die Augenposition bleibt ueber der Landschaft (nicht kuenstlich leere
    // Hauptansicht): Augehoehe = cy + r * sin(pitch) > Wandhoehe.
    for sample in samples do
        let pose = RepresentativeCameraFlight.Pose(sample)
        let eyeHeightMeters = pose.Eye.Y

        if eyeHeightMeters <= RepresentativeLandscape.WallHeightMeters then
            failwith $"Augenhoehe {eyeHeightMeters} faellt unter die Landschaft."

/// Tick-Zuordnung, Warm-up-Ableitung und Abgriffindex folgen dem Framevertrag (AC-T023-01/08).
let scheduleMathBindsTicksToFramesAndCaptureAfterWindow () =
    let warmupFrames = 240
    let sampleFrames = 1200

    if RepresentativeScenario.WarmupTicks(warmupFrames) <> 120 then
        failwith "Warm-up-Ticks widersprechen der Frames-per-Tick-Zuordnung."

    if RepresentativeScenario.TotalTicks(warmupFrames, sampleFrames) <> 720 then
        failwith "Gesamtticks widersprechen der Frames-per-Tick-Zuordnung."

    if
        RepresentativeScenario.SampleTicks(warmupFrames, sampleFrames)
        <> RepresentativeScenario.TotalTicks(warmupFrames, sampleFrames)
           - RepresentativeScenario.WarmupTicks(warmupFrames)
    then
        failwith "Messsticks widersprechen der Gesamtsumme."

    let captureIndex =
        RepresentativeScenario.CaptureFrameIndex(warmupFrames, sampleFrames)

    let lastMeasured = warmupFrames + sampleFrames - 1

    if captureIndex <= warmupFrames + sampleFrames then
        failwith "Abgriffindex liegt nicht strikt hinter dem Messfenster."

    if not (FrameEvidence.IsCaptureFrameAllowed(captureIndex, warmupFrames, sampleFrames)) then
        failwith "Abgriff am erlaubten Index wurde abgelehnt."

    if FrameEvidence.IsCaptureFrameAllowed(lastMeasured, warmupFrames, sampleFrames) then
        failwith "Abgriff innerhalb des Messfensters wurde zugelassen."

/// Gate-Evaluator je Bestehens- und Verletzungsklasse fail-closed mit Zielausweisung (AC-T023-04).
let representativeBudgetGateCoversEveryClassFailClosed () =
    let limits = RepresentativeScenario.BudgetLimits.Documented
    let rssMin, rssMax, rssEnd = 48000L, 52000L, 51000L

    let passCase =
        RepresentativeBudgetGate.Evaluate(
            limits,
            RepresentativeBudgetInputs(
                P99FrameTimeMs = 20.0,
                P99GpuTimeMs = 12.0,
                GpuTimeMeasured = true,
                P99TickTimeMs = 2.0,
                ManagedAllocationsPerWarmFrameBytes = 512.0,
                DrawSubmitCallsPerFrameMax = 40L,
                VisibleTrianglesMainViewMax = 400_000L,
                ConcurrentParticlesObserved = 5000L,
                RuntimeShaderCompilationObserved = false,
                RssMinKiB = rssMin,
                RssMaxKiB = rssMax,
                RssEndKiB = rssEnd
            )
        )

    if
        not passCase.Pass
        || not passCase.GpuTimeTargetMet
        || not passCase.TickTimeTargetMet
        || not passCase.RssTargetMet
        || passCase.Violations.Count <> 0
    then
        failwith "Bestehensklasse des integrierten Gates schlug fehl."

    let hasViolation (fragment: string) (verdict: RepresentativeBudgetVerdict) =
        if verdict.Pass then
            failwith $"Verletzungsklasse {fragment} wurde nicht erkannt."

        verdict.Violations
        |> Seq.exists (fun violation -> violation.Contains(fragment, StringComparison.Ordinal))

    let evaluate frame gpu gpuMeasured tick allocations draws triangles particles shader rssMin' rssMax' rssEnd' =
        RepresentativeBudgetGate.Evaluate(
            limits,
            RepresentativeBudgetInputs(
                P99FrameTimeMs = frame,
                P99GpuTimeMs = gpu,
                GpuTimeMeasured = gpuMeasured,
                P99TickTimeMs = tick,
                ManagedAllocationsPerWarmFrameBytes = allocations,
                DrawSubmitCallsPerFrameMax = draws,
                VisibleTrianglesMainViewMax = triangles,
                ConcurrentParticlesObserved = particles,
                RuntimeShaderCompilationObserved = shader,
                RssMinKiB = rssMin',
                RssMaxKiB = rssMax',
                RssEndKiB = rssEnd'
            )
        )

    evaluate 33.31 12.0 true 2.0 512.0 40L 400_000L 5000L false (Nullable rssMin) (Nullable rssMax) (Nullable rssEnd)
    |> hasViolation "p99-frame-time-ms"
    |> ignore

    evaluate 20.0 29.9 true 2.0 512.0 40L 400_000L 5000L false (Nullable rssMin) (Nullable rssMax) (Nullable rssEnd)
    |> fun verdict ->
        if not verdict.Pass || verdict.GpuTimeTargetMet then
            failwith "GPU-Ziel-/Grenzbeziehung wurde falsch ausgewiesen."

    evaluate 20.0 0.0 false 2.0 512.0 40L 400_000L 5000L false (Nullable rssMin) (Nullable rssMax) (Nullable rssEnd)
    |> hasViolation "p99-gpu-time-ms"
    |> ignore

    evaluate 20.0 12.0 true 16.001 512.0 40L 400_000L 5000L false (Nullable rssMin) (Nullable rssMax) (Nullable rssEnd)
    |> hasViolation "p99-tick-time-ms"
    |> ignore

    evaluate 20.0 12.0 true 2.0 1025.0 40L 400_000L 5000L false (Nullable rssMin) (Nullable rssMax) (Nullable rssEnd)
    |> hasViolation "managed-allocations-per-warm-frame-bytes"
    |> ignore

    evaluate 20.0 12.0 true 2.0 512.0 1201L 400_000L 5000L false (Nullable rssMin) (Nullable rssMax) (Nullable rssEnd)
    |> hasViolation "draw-submit-per-frame"
    |> ignore

    evaluate 20.0 12.0 true 2.0 512.0 40L 2_000_001L 5000L false (Nullable rssMin) (Nullable rssMax) (Nullable rssEnd)
    |> hasViolation "visible-triangles-main-view"
    |> ignore

    evaluate 20.0 12.0 true 2.0 512.0 40L 400_000L 5001L false (Nullable rssMin) (Nullable rssMax) (Nullable rssEnd)
    |> hasViolation "concurrent-particles"
    |> ignore

    evaluate 20.0 12.0 true 2.0 512.0 40L 400_000L 5000L true (Nullable rssMin) (Nullable rssMax) (Nullable rssEnd)
    |> hasViolation "runtime-shader-compilation"
    |> ignore

    evaluate 20.0 12.0 true 2.0 512.0 40L 400_000L 5000L false (Nullable()) (Nullable()) (Nullable())
    |> hasViolation "working-set-kib:not-measurable"
    |> ignore

    evaluate
        20.0
        12.0
        true
        2.0
        512.0
        40L
        400_000L
        5000L
        false
        (Nullable rssMin)
        (Nullable(4501L * 1024L))
        (Nullable rssEnd)
    |> hasViolation "working-set-max-mib"
    |> ignore

    let nanCase =
        evaluate
            Double.NaN
            12.0
            true
            2.0
            512.0
            40L
            400_000L
            5000L
            false
            (Nullable rssMin)
            (Nullable rssMax)
            (Nullable rssEnd)

    if nanCase.Pass then
        failwith "NaN-Framezeit wurde nicht als fail-closed erkannt."

/// BMP-Encoding, Hashbindung und Messfensterregel des Abgriffs sind deterministisch (AC-T023-08).
let frameEvidenceEncoderAndPolicyAreBound () =
    // 2x2 RGBA, Zeilen von oben nach unten; BMP speichert unterste Zeile zuerst.
    let rgba =
        [| 255uy
           0uy
           0uy
           255uy
           0uy
           255uy
           0uy
           255uy
           0uy
           0uy
           255uy
           255uy
           255uy
           255uy
           255uy
           128uy |]

    let bmp = FrameEvidence.EncodeBmpFromRgbaTopDown(rgba, 2, 2)

    if
        bmp.Length
        <> FrameEvidence.FileHeaderBytes + FrameEvidence.InfoHeaderBytes + (2 * 2 * 4)
    then
        failwith "BMP-Groesse widerspricht dem Header."

    if bmp[0] <> byte 'B' || bmp[1] <> byte 'M' then
        failwith "BMP-Signatur fehlt."

    let read32 (offset: int) =
        int bmp[offset]
        ||| (int bmp[offset + 1] <<< 8)
        ||| (int bmp[offset + 2] <<< 16)
        ||| (int bmp[offset + 3] <<< 24)

    if read32 18 <> 2 || read32 22 <> 2 then
        failwith "BMP-Abmessungen falsch."

    if bmp[28] <> 32uy then
        failwith "BMP-Tiefe ist nicht 32 Bit."

    // Unterste Quellzeile (blau/weiss) zuerst: erster Pixel = Blaukanal vorn.
    let pixelStart = FrameEvidence.FileHeaderBytes + FrameEvidence.InfoHeaderBytes

    if bmp[pixelStart] <> 255uy && bmp[pixelStart + 2] <> 0uy then
        failwith "Zeilenflip oder Kanalreihenfolge fehlerhaft."

    if bmp[pixelStart + 3] <> 0uy then
        failwith "Alpha-Byte des BMP ist nicht gefuellt."

    try
        FrameEvidence.EncodeBmpFromRgbaTopDown(ReadOnlySpan<byte>(rgba), 3, 2) |> ignore

        failwith "Falsche Puffergroesse wurde akzeptiert."
    with :? ArgumentException ->
        ()

    let hash = FrameEvidence.Sha256Hex(bmp)

    if hash.Length <> 64 || hash <> FrameEvidence.Sha256Hex(bmp) then
        failwith "Artefakthash ist nicht stabil oder nicht kanonisch."

/// Reportvertrag akzeptiert das echte Producer-Shape und lehnt Faelschungen ab (AC-T023-03).
let reportSchemaAcceptsGoldenAndRejectsFabricationMatrix () =
    // Golden-Grundlage: das echte Producer-Shape aus dem Runner mit
    // synthetischen Messwerten; Abweichung zwischen Produzent und Vertrag
    // wird so sofort sichtbar.
    let metrics =
        RepBenchRunner.MeasurementMetrics(
            FrameBand = FrameTimeBand(12.0, 18.5, 21.4),
            TickBand = FrameTimeBand(1.1, 2.1, 2.4),
            GpuTimeMeasured = true,
            GpuTimeP99Ms = 11.5,
            GpuTimerFrequencyHz = 1_000_000_000L,
            AllocationsPerWarmFrameBytes = 0.0,
            GcPauseSumMs = 0.0,
            GcPauseCount = 0L,
            WorkingSetMeasured = true,
            RssMinKiB = 48000L,
            RssMaxKiB = 52000L,
            RssEndKiB = 51000L,
            RssReason = null,
            GpuMemoryMeasured = true,
            GpuMemoryBytesUsed = 24_000_000L,
            TextureMemoryUsedBytes = 20_000_000L,
            DrawSubmitCallsMax = 14L,
            TrianglesGlobalMax = 1_800_000L,
            MainViewTriangles = 360_000L,
            ConcurrentParticlesObserved = 5000L,
            VisibleUnitsObserved = 350L,
            PaletteRowsBound = 350L,
            HashSampleTicks = [ 30L; 60L ],
            HashSamplesHex = [ "0011223344556677"; "8899aabbccddeeff" ],
            StartHashHex = "0123456789abcdef",
            EndHashHex = "fedcba9876543210",
            MeasurementWindowMs = 20000.0,
            CommandCount = 55,
            CommandPlanHashHex = "abcdef0123456789"
        )

    let verdict =
        RepresentativeBudgetVerdict(true, true, true, true, [ "irrelevant-wird-ersetzt" ])

    let context =
        RepBenchRunner.ReportContext(
            defaultSeed,
            240,
            1200,
            120,
            600,
            DateTime(2026, 8, 24, 12, 0, 0, DateTimeKind.Utc),
            "0123456789abcdef0123456789abcdef01234567",
            "Release",
            ([ ToolchainPin("sdl3", "tag", "release-3.4.14", "a1", "h1", "zlib")
               ToolchainPin("bgfx", "commit", "b2", "b2", "h2", "BSD-2-Clause")
               ToolchainPin("bx", "commit", "c3", "c3", "h3", "BSD-2-Clause")
               ToolchainPin("bimg", "commit", "d4", "d4", "h4", "BSD-2-Clause") ]
            :> IReadOnlyList<ToolchainPin>),
            SystemInfo.Environment("fixture", "Linux", "6.8.0-fixture", "fixture-cpu", "flags", 8),
            "GL 3.3 fixture",
            "fixture-gpu",
            (0x1002u <<< 16) ||| 0x6958u,
            metrics,
            250.5,
            verdict,
            0,
            RepBenchRunner.CaptureOutcome(
                true,
                true,
                false,
                "",
                1470,
                1419,
                "a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90"
            ),
            (ResizeArray<struct (string * string)>() :> IReadOnlyList<struct (string * string)>)
        )

    let goldenReport =
        System.Text.Json.JsonSerializer.Serialize(RepBenchRunner.BuildReport(context), BenchRunner.ReportJsonOptions)
        + "\n"

    let goldenErrors = RepresentativeReportSchema.Validate(goldenReport)

    if goldenErrors.Count > 0 then
        failwith ("Goldenreport wurde abgelehnt: " + String.Join("; ", goldenErrors))

    let rejects (fragment: string) (mutate: string -> string) (message: string) =
        let errors = RepresentativeReportSchema.Validate(mutate goldenReport)

        if errors.Count = 0 then
            failwith (message + ": Schemapruefung akzeptierte den Report unerwartet.")

        let joined = String.Join("; ", errors)

        if not (joined.Contains(fragment, StringComparison.Ordinal)) then
            failwith (message + ": Fehler '" + joined + "' enthaelt nicht '" + fragment + "'.")

    rejects
        "schemaVersion"
        (fun text -> text.Replace("\"schemaVersion\":3", "\"schemaVersion\":2"))
        "Fremde Schemaversion"

    rejects
        "method"
        (fun text -> text.Replace("\"method\":\"stopwatch-tick-delta-inside-frame\",", ""))
        "Kennzahl ohne Methodenkennung"

    rejects
        "ganzzahliger Wert"
        (fun text -> text.Replace("\"timerFreqHz\":1000000000", "\"timerFreqHz\":\"x\""))
        "Typenfremder Messwert"

    rejects
        "reason"
        (fun text ->
            text.Replace(
                "\"discreteVramBytes\":{\"measured\":false,\"reason\":\"not-exposed-by-bgfx-stats-on-opengl\"}",
                "\"discreteVramBytes\":{\"measured\":false}"
            ))
        "Unavailable ohne Grund"

    rejects
        "unbekanntes Feld"
        (fun text -> text.Replace("\"compositionTargets\":{", "\"compositionTargets\":{\"extra\":1,"))
        "Unbekanntes Feld"

    rejects
        "Wert"
        (fun text ->
            text.Replace(
                "\"visibleUnitsRendered\":{\"unit\":\"count\",\"method\":\"scenario-config-or-runtime-counter\",\"value\":350}",
                "\"visibleUnitsRendered\":{\"unit\":\"count\",\"method\":\"scenario-config-or-runtime-counter\",\"value\":349}"
            ))
        "Istzaehler unter Ziel"

    rejects
        "concurrentParticles"
        (fun text ->
            text.Replace(
                "\"concurrentParticles\":{\"unit\":\"count\",\"method\":\"particle-instance-count-max-per-frame\",\"value\":5000}",
                "\"concurrentParticles\":{\"unit\":\"count\",\"method\":\"particle-instance-count-max-per-frame\",\"value\":0}"
            ))
        "Degenerative Partikellast"

    rejects
        "applicable"
        (fun text ->
            text.Replace(
                "\"cardLoadBudgetLine\":{\"applicable\":false,",
                "\"cardLoadBudgetLine\":{\"applicable\":true,"
            ))
        "Kartenlade-Zeile wurde still beansprucht"

    rejects
        "hinter dem Messfenster"
        (fun text -> text.Replace("\"capturedAtFrameIndex\":1470,", "\"capturedAtFrameIndex\":100,"))
        "Capture vor dem Messfenster"

    rejects
        "sha256"
        (fun text ->
            text.Replace(
                "\"sha256\":\"a1b2c3d4e5f60718293a4b5c6d7e8f90a1b2c3d4e5f60718293a4b5c6d7e8f90\"",
                "\"sha256\":\"A1B2C3D4E5F60718293A4B5C6D7E8F90A1B2C3D4E5F60718293A4B5C6D7E8F90\""
            ))
        "Grossbuchstaben-Hash im Artefakt"

    rejects "gueltiges JSON" (fun _ -> "{beschädigt") "Beschaedigtes Dokument"

/// Profil-Ehrlichkeitsregel bleibt fuer den Belastungsframe aktiv (AC-T023-06).
let profileBindingStaysHonestForRepresentativeScenario () =
    let mandatory = ProfileBinding.MandatoryWithoutReferenceHardware()

    if mandatory.Count <> 3 then
        failwith "Pflichtprofilliste unvollstaendig."

    for status in mandatory do
        if status.Status <> ProfileStatus.NotMeasured then
            failwith "Ohne Referenzhardware darf kein Profilbestehen entstehen."

    let diagnosticClaim =
        ProfileBinding.EvaluateClaim(
            HardwareProfiles.PcMinimum,
            HardwareDescriptor("NVIDIA GTX 660", "Intel i7-3770", IsDeveloperWorkstation = true),
            "gtx 660",
            referenceMachinesNamed = false
        )

    if
        diagnosticClaim.Status <> ProfileStatus.NotMeasured
        || diagnosticClaim.Reason <> ProfileBinding.DeveloperWorkstationDiagnosticReason
    then
        failwith "Entwickler-PC-Lauf wurde nicht als diagnostische Baseline gekennzeichnet."

    let mismatch =
        ProfileBinding.EvaluateClaim(
            HardwareProfiles.MacMinimum,
            HardwareDescriptor("AMD RX 580", "Apple M1", IsDeveloperWorkstation = false),
            "rx 580",
            referenceMachinesNamed = false
        )

    if mismatch.Status <> ProfileStatus.NotMeasured then
        failwith "Klassenferne Bindung wurde akzeptiert."

/// CLI-Vertrag: unbekannte/nicht implementierte Szenarien und Argumentfehler bleiben kontrolliert (AC-T023-01/09).
let cliContractKeepsRegistryAndErrorPathsControlled () =
    if
        BenchScenarios.Classify(BenchScenarios.Representative)
        <> BenchScenarios.Support.Implemented
    then
        failwith "bench-representative ist nicht implementiert klassifiziert."

    for pending in
        [ BenchScenarios.Army
          BenchScenarios.Battle
          BenchScenarios.Base
          BenchScenarios.Path
          BenchScenarios.Load ] do
        if
            BenchScenarios.Classify(pending)
            <> BenchScenarios.Support.RegisteredNotImplemented
        then
            failwith $"Szenario {pending} ist nicht mehr registriert-unimplementiert."

    if BenchScenarios.Classify("bench-nope") <> BenchScenarios.Support.Unknown then
        failwith "Fremde ID wurde nicht abgewiesen."

    if ExitCodes.Map(PlatformErrorCode.FrameArtifactFailed) <> 29 then
        failwith "Neuer Exitcode 29 ist nicht an den dokumentierten Vertrag gebunden."

    for code, expected in
        [ PlatformErrorCode.BenchScenarioUnavailable, 25
          PlatformErrorCode.BenchBudgetViolated, 26
          PlatformErrorCode.TelemetryInvalid, 27
          PlatformErrorCode.ReportNotWritable, 28 ] do
        if ExitCodes.Map(code) <> expected then
            failwith $"Bestehende Exitcodebedeutung fuer {code} wurde veraendert."

/// Architektur: Snapshot-Richtung, Tick-Eigentum und allokationsarme Hotpaths bleiben intakt (AC-T023-07).
let architectureKeepsSnapshotDirectionAndHotPathDiscipline () =
    let appDirectory = Path.Combine(repositoryRoot, "src", "Riftward.App")

    let mutable sawDriver = false

    for file in Directory.GetFiles(appDirectory, "*.cs", SearchOption.AllDirectories) do
        let fileName = Path.GetFileName(file)
        let content = File.ReadAllText(file)

        if
            fileName.EndsWith("Runner.cs", StringComparison.Ordinal)
            && content.Contains("world.Tick()", StringComparison.Ordinal)
        then
            sawDriver <- true

        let tickDriverAllowlist = [ "RepBenchRunner.cs"; "SimBenchRunner.cs" ]

        if
            not (List.contains fileName tickDriverAllowlist)
            && content.Contains(".Tick()", StringComparison.Ordinal)
        then
            failwith $"Simulationstick ausserhalb der Lauf-Treiber aufgerufen: {fileName}"

        let commandDriverAllowlist = [ "RepBenchRunner.cs"; "SimBenchRunner.cs" ]

        if List.contains fileName commandDriverAllowlist then
            ()
        elif
            content.Contains(".ApplyCommands(", StringComparison.Ordinal)
            && fileName.EndsWith("Tests.fs", StringComparison.Ordinal) |> not
        then
            failwith $"Befehlsanwendung ausserhalb der Lauf-Treiber: {fileName}"

    if not sawDriver then
        failwith "Kein Lauf-Treiber fuehrt den Simulationstick aus."

    // Ansicht der simulierten Agenten liest nur; sie schreibt nie Zustand.
    let viewSource =
        File.ReadAllText(Path.Combine(appDirectory, "Bench", "RepresentativeUnits.cs"))

    if not (viewSource.Contains("PositionXOf", StringComparison.Ordinal)) then
        failwith "Agentenansicht nutzt nicht die schreibgeschuetzten Zugriffe."

    if viewSource.Contains("CreateSnapshot()", StringComparison.Ordinal) then
        failwith "Hotpath-Ansicht alloziert Snapshots pro Frame."

    // Kein LINQ/Boxing in den Palette- und Instanzfuellungen.
    for hotFile in [ "RepresentativeUnits.cs"; "RepresentativeRig.cs" ] do
        let source = File.ReadAllText(Path.Combine(appDirectory, "Bench", hotFile))

        for token in [ ".Select("; ".Where("; ".ToArray()"; ".ToList()" ] do
            if source.Contains(token, StringComparison.Ordinal) then
                failwith $"LINQ-artiger Ausdruck im Instanz-Hotpath gefunden: {hotFile} ({token})."

    // Simulationsprojekt bleibt frei von Praesentationstypen.
    let simulationDirectory = Path.Combine(repositoryRoot, "src", "Riftward.Simulation")

    for file in Directory.GetFiles(simulationDirectory, "*.cs") do
        let content = File.ReadAllText(file)

        for token in [ "SDL"; "bgfx"; "Riftward.Platform"; "Riftward.App" ] do
            if content.Contains(token, StringComparison.Ordinal) then
                failwith $"Praesentationstyp in Simulation referenziert: {Path.GetFileName(file)} ({token})."
