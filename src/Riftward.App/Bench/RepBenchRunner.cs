using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using Riftward.App.Bench;
using Riftward.Platform;
using Riftward.Platform.Interop;
using Riftward.Simulation;

namespace Riftward.App;

/// <summary>
/// BENCH-REPRESENTATIVE (T-023): integrierter deterministischer
/// Belastungsframe nativ auf linux-x64 im bestehenden Host (1920x1080,
/// Low-Anzeigeprofil, OpenGL-3.3-Core-Pflichtpfad ohne stillen Fallback,
/// VSync-Policy wie die Effizienzbaseline). Verbindet genau die 250
/// vollstaendig simulierten Agenten aus Riftward.Simulation (Vertrag V1,
/// unveraendert wiederverwendet) mit 100 skriptgesteuerten Hintergrund-
/// akteuren, repraesentativer Landschaft ueber der Graybox-Welt, Sonne plus
/// vier lokalen Schattenlichtern mit aktiven Schattenpaessen, dem
/// 48-Bone-Skinningpfad je sichtbarer Einheit und einer nichtdegenerativen
/// Partikelspitze bis hoechstens 5000 transparenter Partikel. Der Report
/// bindet je Kennzahl Einheit und Methode, den Zustands-Hashkettenanker der
/// Simulation und die Umgebungsbinding; das Budgetgate entscheidet
/// fail-closed ausschliesslich gegen dokumentierte Grenzwerte.
/// Budgetverletzungen ergeben einen definierten Exitcode und schreiben den
/// Report trotzdem. Laeufe auf dem Entwickler-PC sind diagnostische
/// Baseline gemaess dem Q-OPS-001-Klaerungsprotokoll; Pflichtprofile
/// bleiben ohne benannte Referenzhardware NOT-MEASURED und eskalieren.
/// </summary>
internal static class RepBenchRunner
{
    public const string CommandName = "./scripts/rift.sh bench --scenario bench-representative";

    /// <summary>Views 0 bis 3: aktive Schattenpaesse der lokalen Lichter.</summary>
    public const byte ViewShadowBase = 0;

    /// <summary>View 4: zusammengesetzter Hauptdurchgang.</summary>
    public const byte ViewMain = 4;

    /// <summary>View 5: opt-in Einzelabgriff (nur nach dem Messfenster).</summary>
    public const byte ViewCapture = 5;

    /// <summary>View 6: Blit-/Readback-Durchgang des Abgriffs.</summary>
    public const byte ViewBlit = 6;

    private sealed record SceneResources(
        ushort TerrainVertexBuffer,
        ushort TerrainIndexBuffer,
        ushort UnitVertexBuffer,
        ushort UnitIndexBuffer,
        ushort ParticleQuadBuffer,
        int TerrainTriangleCount,
        ushort PaletteTexture,
        IReadOnlyList<ushort> ShadowTextures,
        IReadOnlyList<ushort> ShadowFrameBuffers,
        ushort ProgramTerrainLit,
        ushort ProgramUnitLit,
        ushort ProgramUnitDepth,
        ushort ProgramTerrainDepth,
        ushort ProgramParticle,
        ushort UniformSunDirection,
        ushort UniformSunColor,
        ushort UniformLightPosRadius,
        ushort UniformLightColorInner,
        ushort UniformShadowParams,
        ushort UniformBonePaletteSampler,
        ushort UniformShadowMap0,
        ushort UniformShadowMap1,
        ushort UniformShadowMap2,
        ushort UniformShadowMap3,
        IReadOnlyList<ushort> UniformLightViewProj,
        ushort UniformCameraRight,
        ushort UniformCameraUp)
    {
        /// <summary>Feste Freigabereihenfolge: Programme, Framebuffer vor
        /// Texturen, dann Buffer und Uniforms; gesamt vor bgfx-Shutdown.</summary>
        public void Dispose(BgfxDevice device)
        {
            device.DestroyProgram(ProgramTerrainLit);
            device.DestroyProgram(ProgramUnitLit);
            device.DestroyProgram(ProgramUnitDepth);
            device.DestroyProgram(ProgramTerrainDepth);
            device.DestroyProgram(ProgramParticle);

            foreach (var frameBuffer in ShadowFrameBuffers)
            {
                device.DestroyFrameBuffer(frameBuffer);
            }

            foreach (var texture in ShadowTextures)
            {
                device.DestroyTexture(texture);
            }

            device.DestroyTexture(PaletteTexture);
            device.DestroyIndexBuffer(TerrainIndexBuffer);
            device.DestroyIndexBuffer(UnitIndexBuffer);
            device.DestroyVertexBuffer(TerrainVertexBuffer);
            device.DestroyVertexBuffer(UnitVertexBuffer);
            device.DestroyVertexBuffer(ParticleQuadBuffer);

            device.DestroyUniform(UniformSunDirection);
            device.DestroyUniform(UniformSunColor);
            device.DestroyUniform(UniformLightPosRadius);
            device.DestroyUniform(UniformLightColorInner);
            device.DestroyUniform(UniformShadowParams);
            device.DestroyUniform(UniformBonePaletteSampler);
            device.DestroyUniform(UniformShadowMap0);
            device.DestroyUniform(UniformShadowMap1);
            device.DestroyUniform(UniformShadowMap2);
            device.DestroyUniform(UniformShadowMap3);

            foreach (var matrix in UniformLightViewProj)
            {
                device.DestroyUniform(matrix);
            }

            device.DestroyUniform(UniformCameraRight);
            device.DestroyUniform(UniformCameraUp);
        }
    }

    /// <summary>Vorab festgepinnte Arbeitspuffer (keine Hotpath-Allokation).</summary>
    private sealed class PinnedBuffers : IDisposable
    {
        public const int UnitFloats =
            RepresentativeScenario.VisibleUnitsTarget * (RepresentativeMesh.UnitInstanceStrideBytes / sizeof(float));

        public const int ParticleFloats =
            RepresentativeScenario.ParticlePeakTarget * (RepresentativeMesh.ParticleInstanceStrideBytes / sizeof(float));

        public const int PaletteRowFloats = RepresentativeScenario.BonesPerNormalUnit * 3 * 4;

        public readonly float[] Units = new float[UnitFloats];
        public readonly float[] Particles = new float[ParticleFloats];
        public readonly float[] Palette = new float[RepresentativeScenario.VisibleUnitsTarget * PaletteRowFloats];

        private readonly GCHandle _unitsHandle;
        private readonly GCHandle _particlesHandle;

        public PinnedBuffers()
        {
            _unitsHandle = GCHandle.Alloc(Units, GCHandleType.Pinned);
            _particlesHandle = GCHandle.Alloc(Particles, GCHandleType.Pinned);
        }

        public nint UnitsPointer => _unitsHandle.AddrOfPinnedObject();

        public nint ParticlesPointer => _particlesHandle.AddrOfPinnedObject();

        public void Dispose()
        {
            _unitsHandle.Free();
            _particlesHandle.Free();
        }
    }

    /// <summary>Befehlsplan mit fortlaufendem Cursor (deterministisch).</summary>
    private sealed class PlanCursor
    {
        public PlanCursor(SimCommand[] commands, ulong hash, int totalTicks)
        {
            Commands = commands;
            Hash = hash;
            TotalTicks = totalTicks;
        }

        public SimCommand[] Commands { get; }

        public ulong Hash { get; }

        public int TotalTicks { get; }

        public int Cursor { get; set; }
    }

    public static int Run(CommandLineArgs arguments)
    {
        var reportPath = arguments.Option("--report");

        if (string.IsNullOrWhiteSpace(reportPath))
        {
            Console.Error.WriteLine("bench: --report PFAD ist erforderlich.");
            return ExitCodes.Usage;
        }

        var capturePath = arguments.Option("--capture-frame");
        var seed = (uint)Math.Clamp(arguments.NumberOption("--seed", CameraFlight.DefaultSeed), 0, uint.MaxValue);
        var warmupFrames = (int)Math.Clamp(arguments.NumberOption("--warmup-frames", RepresentativeScenario.DefaultWarmupFrames), 120, 5_000);
        var sampleFrames = (int)Math.Clamp(arguments.NumberOption("--sample-frames", RepresentativeScenario.DefaultSampleFrames), 240, 20_000);
        var claimedBindings = BenchRunner.ParseProfileBindings(arguments);

        var warmupTicks = RepresentativeScenario.WarmupTicks(warmupFrames);
        var totalTicks = RepresentativeScenario.TotalTicks(warmupFrames, sampleFrames);

        if (totalTicks <= CommandPlan.FirstCommandTick)
        {
            Console.Error.WriteLine(
                "bench: Framehorizont muss hinter dem ersten Planbefehl liegen "
                + $"(Simulationstick {CommandPlan.FirstCommandTick}).");
            return ExitCodes.Usage;
        }

        var environment = SystemInfo.Capture();
        var processStart = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        var commit = BenchEnvironment.CommitId();
        var buildMode = BenchEnvironment.BuildMode();
        var pins = ToolchainLockReader.ReadNativeComponents(arguments.Option("--lock") ?? "toolchain.lock.json");

        HostBootstrap.Context? context = null;
        SceneResources? resources = null;
        PinnedBuffers? buffers = null;
        string glVersion;
        string glRenderer;
        uint gpuIds;
        PlanCursor plan;
        SimWorld world;
        SimulatedAgentView agentView;
        RepresentativeRig.PoseEvaluator poseEvaluator;
        double sceneSetupTimeMs;

        try
        {
            context = HostBootstrap.Start(arguments, BenchRunner.DefaultWidth, BenchRunner.DefaultHeight, vsync: true);

            var api = NativeApi.Instance;
            (glVersion, glRenderer, _) = api.GlStrings();
            gpuIds = api.GpuIds();

            var setupStopwatch = Stopwatch.StartNew();

            resources = BuildResources(context.Device, arguments);
            buffers = new PinnedBuffers();

            world = new SimWorld(seed);
            var commands = CommandPlan.Generate(seed, totalTicks);
            plan = new PlanCursor(commands, CommandPlan.Hash(commands), totalTicks);
            agentView = new SimulatedAgentView(world);
            poseEvaluator = new RepresentativeRig.PoseEvaluator();

            // Kamerapfad einmal je Prozess vorrechnen (keine Prefix-Regeneration
            // im Frame-Hotpath). Der Strom ist praefixstabil: identische Indizes
            // liefern in jedem Zuschnitt identische Samples. Ohne opt-in Abgriff
            // endet der Horizont am Messfenster, mit Abgriff hinter dem festen
            // Captureframeindex; das Messverhalten bleibt dadurch identisch.
            var cameraSamples = RepresentativeCameraFlight.Samples(
                seed,
                RepresentativeScenario.TotalFrames(warmupFrames, sampleFrames)
                + (capturePath is null ? 0 : RepresentativeScenario.CaptureLeadFrames + 1));
            var scratch = new FrameScratch();

            ConfigureViews(context.Device, resources);
            ApplyCamera(context.Device, RepresentativeCameraFlight.Samples(seed, 1)[0]);

            // Erster zusammengesetzter Frame schliesst den Szenenaufbau ab.
            PresentFrame(
                context.Device, resources, buffers, world, agentView, poseEvaluator, plan, seed,
                cameraSamples, scratch, frameIndex: 0, accumulator: null);
            sceneSetupTimeMs = setupStopwatch.Elapsed.TotalMilliseconds;

            var measurement = Measure(
                context.Device, resources, buffers, world, agentView, poseEvaluator, plan, seed,
                cameraSamples, scratch, warmupFrames, sampleFrames);

            CaptureOutcome capture;

            if (capturePath is null)
            {
                capture = new CaptureOutcome(false, false, false, FrameEvidence.ReasonNotRequested, -1, measurement.LastMeasuredFrameIndex, null);
            }
            else
            {
                capture = ExecuteCapture(
                    context.Device, resources, buffers, world, agentView, poseEvaluator, plan, seed,
                    cameraSamples, scratch, warmupFrames, sampleFrames, capturePath!);
            }

            var verdict = RepresentativeBudgetGate.Evaluate(
                RepresentativeScenario.BudgetLimits.Documented,
                new RepresentativeBudgetInputs(
                    P99FrameTimeMs: measurement.Metrics.FrameBand.P99Ms,
                    P99GpuTimeMs: measurement.Metrics.GpuTimeP99Ms,
                    GpuTimeMeasured: measurement.Metrics.GpuTimeMeasured,
                    P99TickTimeMs: measurement.Metrics.TickBand.P99Ms,
                    ManagedAllocationsPerWarmFrameBytes: measurement.Metrics.AllocationsPerWarmFrameBytes,
                    DrawSubmitCallsPerFrameMax: measurement.Metrics.DrawSubmitCallsMax,
                    VisibleTrianglesMainViewMax: measurement.Metrics.MainViewTriangles,
                    ConcurrentParticlesObserved: measurement.Metrics.ConcurrentParticlesObserved,
                    RuntimeShaderCompilationObserved: false,
                    RssMinKiB: measurement.Metrics.RssMinKiB,
                    RssMaxKiB: measurement.Metrics.RssMaxKiB,
                    RssEndKiB: measurement.Metrics.RssEndKiB));

            var budgetExitCode = verdict.Pass ? ExitCodes.Ok : ExitCodes.Map(PlatformErrorCode.BenchBudgetViolated);
            var exitCode = capture.Failed ? ExitCodes.Map(PlatformErrorCode.FrameArtifactFailed) : budgetExitCode;

            var reportJson = JsonSerializer.Serialize(BuildReport(new ReportContext(
                Seed: seed,
                WarmupFrames: warmupFrames,
                SampleFrames: sampleFrames,
                WarmupTicks: warmupTicks,
                SampleTicks: totalTicks - warmupTicks,
                ProcessStart: processStart,
                Commit: commit,
                BuildMode: buildMode,
                Pins: pins,
                Environment: environment,
                GlVersion: glVersion,
                GlRenderer: glRenderer,
                GpuIds: gpuIds,
                Metrics: measurement.Metrics,
                SceneSetupTimeMs: sceneSetupTimeMs,
                Verdict: verdict,
                ExitCode: exitCode,
                Capture: capture,
                ClaimedBindings: claimedBindings)), BenchRunner.ReportJsonOptions) + "\n";

            var schemaErrors = RepresentativeReportSchema.Validate(reportJson);

            if (schemaErrors.Count > 0)
            {
                Console.Error.WriteLine($"bench: Report widerspricht dem Schemavertrag: {string.Join("; ", schemaErrors)}");
                BenchRunner.WriteReportOrDiagnose(reportPath!, reportJson);
                return ExitCodes.Map(PlatformErrorCode.TelemetryInvalid);
            }

            if (!BenchRunner.WriteReportOrDiagnose(reportPath!, reportJson))
            {
                return ExitCodes.Map(PlatformErrorCode.ReportNotWritable);
            }

            return exitCode;
        }
        finally
        {
            if (context is not null)
            {
                resources?.Dispose(context.Device);
                buffers?.Dispose();
                HostBootstrap.Stop(context);
            }
        }
    }

    /* --------------------------------------------------------------- Aufbau */

    /// <summary>
    /// Wiederverwendete Frame-Arbeitspuffer (Viewmatrix in double/float,
    /// Billboard-Basis). Im Runner erzeugt und durch alle Frames gereicht,
    /// damit der Frame-Hotpath keine Heapallokation erzeugt.
    /// </summary>
    private sealed class FrameScratch
    {
        public readonly double[] ViewMatrix = new double[16];

        public readonly float[] View16 = new float[16];

        public readonly float[] Right4 = new float[4];

        public readonly float[] Up4 = new float[4];
    }

    /// <summary>
    /// Feste Hauptprojektion des Szenarios (nur Konstanten); einmal je
    /// Prozess aufgebaut statt je Frame.
    /// </summary>
    private static readonly float[] MainProjectionFloats =
        CameraMath.ToFloat16(CameraMath.PerspectiveFov(BenchRunner.FieldOfViewDegrees, BenchRunner.DefaultWidth / (double)BenchRunner.DefaultHeight, 0.5, 300.0));

    private static float[] MainProjection() => MainProjectionFloats;

    private static byte[] ReadShaderArtifact(CommandLineArgs arguments, string name)
    {
        var artifactsDir = arguments.Option("--artifacts-dir") ?? ".ai/runtime/cache/native/dist";
        return File.ReadAllBytes(Path.Combine(artifactsDir, "shaders", name));
    }

    private static SceneResources BuildResources(BgfxDevice device, CommandLineArgs arguments)
    {
        var terrain = RepresentativeMesh.BuildTerrain();
        var units = RepresentativeMesh.BuildUnitMesh();
        var quad = RepresentativeMesh.BuildParticleQuad();

        var terrainVertexBuffer = device.CreateLayoutVertexBuffer(terrain.Vertices, BgfxDevice.LayoutTerrain);
        var terrainIndexBuffer = device.CreateIndexBuffer(terrain.Indices, uint32Indices: false);
        var unitVertexBuffer = device.CreateLayoutVertexBuffer(units.Vertices, BgfxDevice.LayoutUnitMesh);
        var unitIndexBuffer = device.CreateIndexBuffer(units.Indices, uint32Indices: false);
        var particleQuadBuffer = device.CreateLayoutVertexBuffer(quad.Vertices, BgfxDevice.LayoutParticleQuad);

        var paletteTexture = device.CreateTexture2D(
            RepresentativeScenario.BonesPerNormalUnit * 3,
            RepresentativeScenario.VisibleUnitsTarget,
            BgfxSceneApi.TextureFormatRgba32F,
            flags: 0,
            initialData: default);

        var shadowTextures = new List<ushort>(RepresentativeLandscape.LocalLightCount);
        var shadowFrameBuffers = new List<ushort>(RepresentativeLandscape.LocalLightCount);

        for (var light = 0; light < RepresentativeLandscape.LocalLightCount; light++)
        {
            shadowTextures.Add(device.CreateTexture2D(
                RepresentativeScenario.ShadowMapSizePixels,
                RepresentativeScenario.ShadowMapSizePixels,
                BgfxSceneApi.TextureFormatRgba32F,
                BgfxSceneApi.TextureFlagRt | BgfxSceneApi.SamplerClampU | BgfxSceneApi.SamplerClampV,
                initialData: default));
            shadowFrameBuffers.Add(device.CreateFrameBufferFromTexture(shadowTextures[light]));
        }

        var lightMatrixUniforms = new List<ushort>(RepresentativeLandscape.LocalLightCount);

        for (var light = 0; light < RepresentativeLandscape.LocalLightCount; light++)
        {
            lightMatrixUniforms.Add(device.CreateUniform($"u_lightViewProj{light}", UniformType.Mat4, 1));
        }

        return new SceneResources(
            TerrainVertexBuffer: terrainVertexBuffer,
            TerrainIndexBuffer: terrainIndexBuffer,
            UnitVertexBuffer: unitVertexBuffer,
            UnitIndexBuffer: unitIndexBuffer,
            ParticleQuadBuffer: particleQuadBuffer,
            TerrainTriangleCount: terrain.TriangleCount,
            PaletteTexture: paletteTexture,
            ShadowTextures: shadowTextures,
            ShadowFrameBuffers: shadowFrameBuffers,
            ProgramTerrainLit: CreateProgram(device, arguments, "terrain.vs.bin", "lit_terrain.fs.bin"),
            ProgramUnitLit: CreateProgram(device, arguments, "unit.vs.bin", "lit_unit.fs.bin"),
            ProgramUnitDepth: CreateProgram(device, arguments, "depth_instanced.vs.bin", "depth.fs.bin"),
            ProgramTerrainDepth: CreateProgram(device, arguments, "depth_static.vs.bin", "depth.fs.bin"),
            ProgramParticle: CreateProgram(device, arguments, "particle.vs.bin", "particle.fs.bin"),
            UniformSunDirection: device.CreateUniform("u_sunDirection", UniformType.Vec4, 1),
            UniformSunColor: device.CreateUniform("u_sunColor", UniformType.Vec4, 1),
            UniformLightPosRadius: device.CreateUniform("u_lightPosRadius", UniformType.Vec4, RepresentativeLandscape.LocalLightCount),
            UniformLightColorInner: device.CreateUniform("u_lightColorInner", UniformType.Vec4, RepresentativeLandscape.LocalLightCount),
            UniformShadowParams: device.CreateUniform("u_shadowParams", UniformType.Vec4, 1),
            UniformBonePaletteSampler: device.CreateUniform("s_bonePalette", UniformType.Sampler, 1),
            UniformShadowMap0: device.CreateUniform("s_shadowMap0", UniformType.Sampler, 1),
            UniformShadowMap1: device.CreateUniform("s_shadowMap1", UniformType.Sampler, 1),
            UniformShadowMap2: device.CreateUniform("s_shadowMap2", UniformType.Sampler, 1),
            UniformShadowMap3: device.CreateUniform("s_shadowMap3", UniformType.Sampler, 1),
            UniformLightViewProj: lightMatrixUniforms,
            UniformCameraRight: device.CreateUniform("u_camRight", UniformType.Vec4, 1),
            UniformCameraUp: device.CreateUniform("u_camUp", UniformType.Vec4, 1));
    }

    private static ushort CreateProgram(BgfxDevice device, CommandLineArgs arguments, string vertexShaderName, string fragmentShaderName)
    {
        var vertexShader = device.CreateShader(ReadShaderArtifact(arguments, vertexShaderName));
        var fragmentShader = device.CreateShader(ReadShaderArtifact(arguments, fragmentShaderName));

        try
        {
            return device.CreateProgramFromShaders(vertexShader, fragmentShader);
        }
        finally
        {
            device.DestroyShader(vertexShader);
            device.DestroyShader(fragmentShader);
        }
    }

    private static void ConfigureViews(BgfxDevice device, SceneResources resources)
    {
        for (var light = 0; light < resources.ShadowFrameBuffers.Count; light++)
        {
            device.SetViewFrameBuffer((byte)(ViewShadowBase + light), resources.ShadowFrameBuffers[light]);
            device.ConfigureRenderTargetView((byte)(ViewShadowBase + light), 0x000000FFu, RepresentativeScenario.ShadowMapSizePixels, RepresentativeScenario.ShadowMapSizePixels);
        }

        device.ConfigureRenderTargetView(ViewMain, HostBootstrap.ClearColorRgba, BenchRunner.DefaultWidth, BenchRunner.DefaultHeight);
        device.ConfigureRenderTargetView(ViewCapture, HostBootstrap.ClearColorRgba, BenchRunner.DefaultWidth, BenchRunner.DefaultHeight);
        device.ConfigureRenderTargetView(ViewBlit, 0x000000FFu, 1, 1);
    }

    /* ---------------------------------------------------------- Lichter */

    internal sealed record LightTransform(float[] View16, float[] Proj16, float[] MatrixColumnMajor);

    internal static readonly double[] SunDirection = [-0.35, -0.80, 0.45];

    internal static readonly double[] SunColor = [1.00, 0.96, 0.88];

    internal static readonly float[][] LightColors =
    [
        [0.95f, 0.72f, 0.48f],
        [0.55f, 0.70f, 0.92f],
        [0.85f, 0.60f, 0.75f],
        [0.62f, 0.86f, 0.62f],
    ];

    internal static LightTransform[] BuildLightTransforms()
    {
        var placements = RepresentativeLandscape.LightPlacements();
        var transforms = new LightTransform[placements.Length];

        for (var light = 0; light < placements.Length; light++)
        {
            var placement = placements[light];
            var view = CameraMath.ToFloat16(CameraMath.LookAt(new CameraMath.Vec3(placement.X, placement.Y, placement.Z), new CameraMath.Vec3(placement.X, 0.0, placement.Z), new CameraMath.Vec3(0, 1, 0)));
            var proj = CameraMath.ToFloat16(CameraMath.PerspectiveFov(80.0, 1.0, 1.0, 80.0));
            transforms[light] = new LightTransform(view, proj, MultiplyMatrix(view, proj));
        }

        return transforms;
    }

    /// <summary>Spaltenmajor-Multiplikation view*proj (je 16 floats, bx-Layout).</summary>
    internal static float[] MultiplyMatrix(float[] left, float[] right)
    {
        var result = new float[16];

        for (var column = 0; column < 4; column++)
        {
            for (var row = 0; row < 4; row++)
            {
                float value = 0;

                for (var k = 0; k < 4; k++)
                {
                    value += left[(k * 4) + row] * right[(column * 4) + k];
                }

                result[(column * 4) + row] = value;
            }
        }

        return result;
    }

    /* ------------------------------------------------------- Rahmenarbeit */

    private static void ApplyCamera(BgfxDevice device, RepresentativeCameraSample sample)
    {
        var pose = RepresentativeCameraFlight.Pose(sample);
        var view = CameraMath.ToFloat16(CameraMath.LookAt(pose.Eye, pose.Center, new CameraMath.Vec3(0, 1, 0)));
        device.SetViewTransform(ViewMain, view, MainProjection());
    }

    /// <summary>
    /// Berechnet Viewmatrix und Billboard-Basis eines Kameraframes in die
    /// wiederverwendeten Scratch-Puffer (keine Heapallokation im Hotpath).
    /// Rueckgabe: die float16-Viewmatrix aus dem Scratch.
    /// </summary>
    private static float[] ComposeCameraFrame(RepresentativeCameraSample sample, FrameScratch scratch)
    {
        var pose = RepresentativeCameraFlight.Pose(sample);
        CameraMath.LookAt(pose.Eye, pose.Center, new CameraMath.Vec3(0, 1, 0), scratch.ViewMatrix);
        CameraMath.ToFloat16(scratch.ViewMatrix, scratch.View16);
        ComputeOrthogonalBasis(pose.Eye, pose.Center, scratch.Right4, scratch.Up4);
        return scratch.View16;
    }

    private static void ThrowIfQuitRequested()
    {
        var api = NativeApi.Instance;
        SdlEventBuffer eventBuffer = default;

        while (api.PollEvent(ref eventBuffer))
        {
            if (eventBuffer.Type is SdlEventCodes.Quit or SdlEventCodes.WindowCloseRequested)
            {
                throw new PlatformException(new PlatformError(
                    PlatformErrorCode.Internal,
                    "Benchmarklauf durch Beendigungsereignis abgebrochen."));
            }
        }
    }

    private static void AdvanceState(
        PinnedBuffers buffers,
        SimWorld world,
        SimulatedAgentView agentView,
        RepresentativeRig.PoseEvaluator poseEvaluator,
        PlanCursor plan,
        uint seed,
        long tickIndex,
        MeasurementAccumulator? accumulator)
    {
        if (plan.Cursor < plan.Commands.Length && plan.Commands[plan.Cursor].Tick == world.TickIndex)
        {
            var due = plan.Cursor;

            while (plan.Cursor < plan.Commands.Length && plan.Commands[plan.Cursor].Tick == world.TickIndex)
            {
                plan.Cursor++;
            }

            world.ApplyCommands(plan.Commands.AsSpan(due, plan.Cursor - due));
        }

        long startTimestamp = 0;
        var tickDue = world.TickIndex < plan.TotalTicks;

        if (tickDue && accumulator is not null)
        {
            // Messfenster umfasst ausschliesslich world.Tick() (Methode
            // konsistent zur T-021-Praxis: Stoppuhr-Delta je Tick); die
            // Allokationsermittlung laeuft fensterbasiert ausserhalb dieses
            // Zeitmesspunkts (keine praezise GC-Scan-Instrumentierung im
            // Tickzeitpfad, siehe Reportmethode managedAllocationsBytes).
            startTimestamp = Stopwatch.GetTimestamp();
        }

        if (tickDue)
        {
            world.Tick();
        }

        var tickEndTimestamp = tickDue && accumulator is not null ? Stopwatch.GetTimestamp() : 0;

        agentView.WriteInstances(buffers.Units, world, tickIndex);
        RepresentativeActors.WriteBackgroundInstances(buffers.Units, RepresentativeScenario.SimulatedAgents, tickIndex, seed);
        var particles = RepresentativeActors.WriteParticleInstances(buffers.Particles, tickIndex, seed);
        FillPalette(buffers.Palette, poseEvaluator, tickIndex, seed);

        if (tickDue && accumulator is not null)
        {
            accumulator.RecordTick(
                world.TickIndex,
                Measurement.TimestampDeltaToMilliseconds(startTimestamp, tickEndTimestamp),
                particles);
        }
        else if (accumulator is not null)
        {
            accumulator.RecordCompositionOnly(particles);
        }
    }

    private static void FillPalette(float[] palette, RepresentativeRig.PoseEvaluator evaluator, long tickIndex, uint seed)
    {
        var rowFloats = PinnedBuffers.PaletteRowFloats;

        for (var row = 0; row < RepresentativeScenario.VisibleUnitsTarget; row++)
        {
            var walkPhase = (tickIndex * RepresentativeRig.WalkPhasePerTick * ((row % 2) == 0 ? 1.1 : 1.05))
                + (row * 0.29);
            var unitSeed = (uint)((seed ^ ((ulong)row * 0x9E3779B97F4A7C15UL)) & 0xFFFFFFFFUL);
            evaluator.EvaluateRow(unitSeed, walkPhase, palette.AsSpan(row * rowFloats, rowFloats));
        }
    }

    private static void SubmitShadowPasses(BgfxDevice device, SceneResources resources, PinnedBuffers buffers, LightTransform[] lights)
    {
        for (var light = 0; light < lights.Length; light++)
        {
            var viewId = (byte)(ViewShadowBase + light);
            device.SetViewTransform(viewId, lights[light].View16, lights[light].Proj16);
            device.SetTexture(5, resources.UniformBonePaletteSampler, resources.PaletteTexture, samplerFlags: 0);

            device.DrawSubmit(
                viewId,
                resources.ProgramTerrainDepth,
                resources.TerrainVertexBuffer,
                resources.TerrainIndexBuffer,
                (uint)(resources.TerrainTriangleCount * 3),
                instanceData: 0,
                instanceCount: 0,
                instanceStride: 0,
                state: BgfxSceneApi.StateOpaque);

            device.DrawSubmit(
                viewId,
                resources.ProgramUnitDepth,
                resources.UnitVertexBuffer,
                resources.UnitIndexBuffer,
                (uint)(RepresentativeMesh.TrianglesPerUnit * 3),
                buffers.UnitsPointer,
                RepresentativeScenario.VisibleUnitsTarget,
                RepresentativeMesh.UnitInstanceStrideBytes,
                state: BgfxSceneApi.StateOpaque);
        }
    }

    private static void SubmitCompositePass(
        BgfxDevice device,
        SceneResources resources,
        PinnedBuffers buffers,
        byte viewId,
        float[] view16,
        float[] proj16,
        LightTransform[] lights,
        float cameraRightX,
        float cameraRightY,
        float cameraRightZ,
        float cameraUpX,
        float cameraUpY,
        float cameraUpZ)
    {
        device.SetViewTransform(viewId, view16, proj16);

        Span<float> sunDirection = [(float)-SunDirection[0], (float)-SunDirection[1], (float)-SunDirection[2], 1.0f];
        Span<float> sunColor = [(float)SunColor[0], (float)SunColor[1], (float)SunColor[2], 1.0f];
        Span<float> shadowParams = [RepresentativeScenario.ShadowMapSizePixels, 0f, 0f, 0f];

        device.SetUniformVec4(resources.UniformSunDirection, sunDirection);
        device.SetUniformVec4(resources.UniformSunColor, sunColor);
        device.SetUniformVec4(resources.UniformShadowParams, shadowParams);
        device.SetUniformVec4(
            resources.UniformCameraRight,
            [cameraRightX, cameraRightY, cameraRightZ, 0f]);
        device.SetUniformVec4(
            resources.UniformCameraUp,
            [cameraUpX, cameraUpY, cameraUpZ, 0f]);

        var placements = RepresentativeLandscape.CachedPlacements();
        Span<float> lightPositions = stackalloc float[placements.Length * 4];

        for (var light = 0; light < placements.Length; light++)
        {
            lightPositions[(light * 4) + 0] = (float)placements[light].X;
            lightPositions[(light * 4) + 1] = (float)placements[light].Y;
            lightPositions[(light * 4) + 2] = (float)placements[light].Z;
            lightPositions[(light * 4) + 3] = (float)placements[light].Radius;
        }

        device.SetUniformVec4(resources.UniformLightPosRadius, lightPositions);

        Span<float> lightColorValues = stackalloc float[LightColors.Length * 4];

        for (var light = 0; light < LightColors.Length; light++)
        {
            lightColorValues[(light * 4) + 0] = LightColors[light][0];
            lightColorValues[(light * 4) + 1] = LightColors[light][1];
            lightColorValues[(light * 4) + 2] = LightColors[light][2];
            lightColorValues[(light * 4) + 3] = 1f;
        }

        device.SetUniformVec4(resources.UniformLightColorInner, lightColorValues);

        for (var light = 0; light < lights.Length; light++)
        {
            device.SetUniformMat4(resources.UniformLightViewProj[light], lights[light].MatrixColumnMajor);
        }

        device.SetTexture(5, resources.UniformBonePaletteSampler, resources.PaletteTexture, samplerFlags: 0);
        device.SetTexture(6, resources.UniformShadowMap0, resources.ShadowTextures[0], samplerFlags: 0);
        device.SetTexture(7, resources.UniformShadowMap1, resources.ShadowTextures[1], samplerFlags: 0);
        device.SetTexture(8, resources.UniformShadowMap2, resources.ShadowTextures[2], samplerFlags: 0);
        device.SetTexture(9, resources.UniformShadowMap3, resources.ShadowTextures[3], samplerFlags: 0);

        device.DrawSubmit(
            viewId,
            resources.ProgramTerrainLit,
            resources.TerrainVertexBuffer,
            resources.TerrainIndexBuffer,
            (uint)(resources.TerrainTriangleCount * 3),
            instanceData: 0,
            instanceCount: 0,
            instanceStride: 0,
            state: BgfxSceneApi.StateOpaque);

        device.DrawSubmit(
            viewId,
            resources.ProgramUnitLit,
            resources.UnitVertexBuffer,
            resources.UnitIndexBuffer,
            (uint)(RepresentativeMesh.TrianglesPerUnit * 3),
            buffers.UnitsPointer,
            RepresentativeScenario.VisibleUnitsTarget,
            RepresentativeMesh.UnitInstanceStrideBytes,
            state: BgfxSceneApi.StateOpaque);

        device.DrawSubmit(
            viewId,
            resources.ProgramParticle,
            resources.ParticleQuadBuffer,
            BgfxDevice.NoIndexBuffer,
            4,
            buffers.ParticlesPointer,
            RepresentativeScenario.ParticlePeakTarget,
            RepresentativeMesh.ParticleInstanceStrideBytes,
            state: BgfxSceneApi.StateBlendAlpha);
    }

    private static void PresentFrame(
        BgfxDevice device,
        SceneResources resources,
        PinnedBuffers buffers,
        SimWorld world,
        SimulatedAgentView agentView,
        RepresentativeRig.PoseEvaluator poseEvaluator,
        PlanCursor plan,
        uint seed,
        IReadOnlyList<RepresentativeCameraSample> cameraSamples,
        FrameScratch scratch,
        int frameIndex,
        MeasurementAccumulator? accumulator)
    {
        ThrowIfQuitRequested();

        AdvanceState(buffers, world, agentView, poseEvaluator, plan, seed, world.TickIndex, accumulator);

        device.UpdateTexture2DRgba32F(
            resources.PaletteTexture,
            0,
            0,
            RepresentativeScenario.BonesPerNormalUnit * 3,
            RepresentativeScenario.VisibleUnitsTarget,
            buffers.Palette);

        SubmitShadowPasses(device, resources, buffers, CachedLights);

        var view16 = ComposeCameraFrame(cameraSamples[frameIndex], scratch);

        SubmitCompositePass(
            device, resources, buffers, ViewMain, view16, MainProjection(), CachedLights,
            scratch.Right4[0], scratch.Right4[1], scratch.Right4[2],
            scratch.Up4[0], scratch.Up4[1], scratch.Up4[2]);

        device.RenderFrame();
    }

    private static LightTransform[]? _cachedLights;

    private static LightTransform[] CachedLights => _cachedLights ??= BuildLightTransforms();

    private static void ComputeOrthogonalBasis(CameraMath.Vec3 eye, CameraMath.Vec3 center, Span<float> right4, Span<float> up4)
    {
        var forwardX = center.X - eye.X;
        var forwardY = center.Y - eye.Y;
        var forwardZ = center.Z - eye.Z;
        var forwardLength = Math.Sqrt((forwardX * forwardX) + (forwardY * forwardY) + (forwardZ * forwardZ));

        forwardX /= forwardLength;
        forwardY /= forwardLength;
        forwardZ /= forwardLength;

        // Rechtsvektor = Kreuzprodukt von Weltoben (0,1,0) mit der
        // Blickrichtung (wie CameraMath.LookAt, Handedness::Left).
        var rightX = forwardZ;
        var rightZ = -forwardX;
        var rightLength = Math.Sqrt((rightX * rightX) + (rightZ * rightZ));

        if (rightLength > 1e-9)
        {
            rightX /= rightLength;
            rightZ /= rightLength;
        }
        else
        {
            rightX = 1.0;
            rightZ = 0.0;
        }

        var upX = -forwardY * rightZ;
        var upY = (forwardZ * rightX) - (forwardX * rightZ);
        var upZ = forwardY * rightX;

        right4[0] = (float)rightX;
        right4[1] = 0f;
        right4[2] = (float)rightZ;
        right4[3] = 0f;

        up4[0] = (float)upX;
        up4[1] = (float)upY;
        up4[2] = (float)upZ;
        up4[3] = 0f;
    }

    /* ------------------------------------------------------------- Messung */

    internal sealed record MeasurementMetrics(
        FrameTimeBand FrameBand,
        FrameTimeBand TickBand,
        bool GpuTimeMeasured,
        double GpuTimeP99Ms,
        long GpuTimerFrequencyHz,
        double AllocationsPerWarmFrameBytes,
        double GcPauseSumMs,
        long GcPauseCount,
        bool WorkingSetMeasured,
        long? RssMinKiB,
        long? RssMaxKiB,
        long? RssEndKiB,
        string? RssReason,
        bool GpuMemoryMeasured,
        long GpuMemoryBytesUsed,
        long TextureMemoryUsedBytes,
        long DrawSubmitCallsMax,
        long TrianglesGlobalMax,
        long MainViewTriangles,
        long ConcurrentParticlesObserved,
        long VisibleUnitsObserved,
        long PaletteRowsBound,
        IReadOnlyList<long> HashSampleTicks,
        IReadOnlyList<string> HashSamplesHex,
        string StartHashHex,
        string EndHashHex,
        double MeasurementWindowMs,
        long CommandCount,
        string CommandPlanHashHex);

    internal sealed record CaptureOutcome(
        bool Requested,
        bool Captured,
        bool Failed,
        string Reason,
        int CapturedAtFrameIndex,
        int LastMeasuredFrameIndex,
        string? ArtifactSha256);

    private sealed class MeasurementResult
    {
        public required MeasurementMetrics Metrics { get; init; }

        public required int LastMeasuredFrameIndex { get; init; }
    }

    private sealed class MeasurementAccumulator(int sampleFrames, int sampleTicks)
    {
        public double[] FrameTimes { get; } = new double[sampleFrames];

        public List<double> TickTimes { get; } = new(sampleTicks);

        public long ParticlesObserved { get; private set; }

        public void RecordTick(long tickIndex, double tickMilliseconds, long particles)
        {
            TickTimes.Add(tickMilliseconds);
            ParticlesObserved = Math.Max(ParticlesObserved, particles);
        }

        public void RecordCompositionOnly(long particles) =>
            ParticlesObserved = Math.Max(ParticlesObserved, particles);
    }

    private static MeasurementResult Measure(
        BgfxDevice device,
        SceneResources resources,
        PinnedBuffers buffers,
        SimWorld world,
        SimulatedAgentView agentView,
        RepresentativeRig.PoseEvaluator poseEvaluator,
        PlanCursor plan,
        uint seed,
        IReadOnlyList<RepresentativeCameraSample> cameraSamples,
        FrameScratch scratch,
        int warmupFrames,
        int sampleFrames)
    {
        // Messinfrastruktur (Akkumulator, GPU-Zeitliste, Hashstichprobenlisten,
        // RSS-Stichprobennehmer) wird vollstaendig vor dem Fensterstart
        // aufgebaut; ihre Einrichtungsallokation zaehlt nicht gegen das
        // Allokationsbudget je warmem Frame.
        var warmupTicks = RepresentativeScenario.WarmupTicks(warmupFrames);
        var sampleTicks = RepresentativeScenario.TotalTicks(warmupFrames, sampleFrames) - warmupTicks;
        var accumulator = new MeasurementAccumulator(sampleFrames, sampleTicks);
        var gpuTimes = new List<double>(sampleFrames);
        using var rssSampler = RssSampler.TryCreate();
        var hashSampleTicks = new List<long>();
        var hashSamples = new List<string>();

        // Warmphase ohne Messung; Kameraflug, Simulation und Lastaufbau sind
        // ab Frame 1 gebunden (Frame 0 gehoert zum Szenenaufbau).
        for (var frame = 1; frame < warmupFrames; frame++)
        {
            PresentFrame(device, resources, buffers, world, agentView, poseEvaluator, plan, seed, cameraSamples, scratch, frame, accumulator: null);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var pauseSumBefore = GC.GetTotalPauseDuration();
        var collectionCountBefore = TotalCollectionCount();
        var allocationStartBytes = GC.GetTotalAllocatedBytes(precise: true);
        var startStateHash = world.ComputeStateHash();

        var hashInterval = RepresentativeScenario.HashSampleIntervalTicks;

        long drawCallsMax = 0;
        long trianglesGlobalMax = 0;
        long gpuMemoryBytes = -1;
        long textureMemoryBytes = 0;
        bool gpuTimeMeasured = false;
        long gpuTimerFrequencyHz = 0;

        var mainViewTriangles = ((long)resources.TerrainTriangleCount
            + ((long)RepresentativeMesh.TrianglesPerUnit * RepresentativeScenario.VisibleUnitsTarget)
            + (2L * RepresentativeScenario.ParticlePeakTarget));

        var windowStopwatch = Stopwatch.StartNew();

        for (var measured = 0; measured < sampleFrames; measured++)
        {
            var frameIndex = warmupFrames + measured;
            ThrowIfQuitRequested();

            var frameStartTimestamp = Stopwatch.GetTimestamp();
            PresentFrame(device, resources, buffers, world, agentView, poseEvaluator, plan, seed, cameraSamples, scratch, frameIndex, accumulator);
            accumulator.FrameTimes[measured] = Measurement.TimestampDeltaToMilliseconds(frameStartTimestamp, Stopwatch.GetTimestamp());

            if (world.TickIndex > 0
                && world.TickIndex % hashInterval == 0
                && (hashSampleTicks.Count == 0 || hashSampleTicks[^1] != world.TickIndex))
            {
                hashSampleTicks.Add(world.TickIndex);
                hashSamples.Add(FormatHash(world.ComputeStateHash()));
            }

            if (device.TryReadStats(out var stats))
            {
                drawCallsMax = Math.Max(drawCallsMax, stats.NumDraw);
                trianglesGlobalMax = Math.Max(trianglesGlobalMax, stats.TrianglesRendered);
                gpuMemoryBytes = stats.ManagedGpuMemoryUsedBytes;
                textureMemoryBytes = stats.TextureMemoryUsedBytes;

                if (stats.GpuTimerFrequency > 0)
                {
                    gpuTimeMeasured = true;
                    gpuTimerFrequencyHz = stats.GpuTimerFrequency;
                    gpuTimes.Add((stats.GpuTimeEndTicks - stats.GpuTimeBeginTicks) * 1000.0 / stats.GpuTimerFrequency);
                }
            }

            if (measured % RepresentativeScenario.RssSampleIntervalFrames == 0)
            {
                rssSampler?.Sample();
            }
        }

        windowStopwatch.Stop();

        var endStateHash = world.ComputeStateHash();
        var allocationsPerWarmFrame = (GC.GetTotalAllocatedBytes(precise: true) - allocationStartBytes) / (double)sampleFrames;
        var gcPauseSumMs = (GC.GetTotalPauseDuration() - pauseSumBefore).TotalMilliseconds;
        var gcPauseCount = TotalCollectionCount() - collectionCountBefore;
        var workingSet = rssSampler?.Snapshot() ?? default;

        var metrics = new MeasurementMetrics(
            FrameBand: TelemetryMath.Band(accumulator.FrameTimes),
            TickBand: TelemetryMath.Band(accumulator.TickTimes),
            GpuTimeMeasured: gpuTimeMeasured,
            GpuTimeP99Ms: gpuTimeMeasured ? TelemetryMath.Percentile(gpuTimes, 0.99) : 0.0,
            GpuTimerFrequencyHz: gpuTimerFrequencyHz,
            AllocationsPerWarmFrameBytes: allocationsPerWarmFrame,
            GcPauseSumMs: gcPauseSumMs,
            GcPauseCount: gcPauseCount,
            WorkingSetMeasured: workingSet.Measured,
            RssMinKiB: workingSet.MinKiB,
            RssMaxKiB: workingSet.MaxKiB,
            RssEndKiB: workingSet.EndKiB,
            RssReason: workingSet.Reason,
            GpuMemoryMeasured: gpuMemoryBytes >= 0,
            GpuMemoryBytesUsed: gpuMemoryBytes,
            TextureMemoryUsedBytes: textureMemoryBytes,
            DrawSubmitCallsMax: drawCallsMax,
            TrianglesGlobalMax: trianglesGlobalMax,
            MainViewTriangles: mainViewTriangles,
            ConcurrentParticlesObserved: accumulator.ParticlesObserved,
            VisibleUnitsObserved: RepresentativeScenario.VisibleUnitsTarget,
            PaletteRowsBound: RepresentativeScenario.VisibleUnitsTarget,
            HashSampleTicks: hashSampleTicks,
            HashSamplesHex: hashSamples,
            StartHashHex: FormatHash(startStateHash),
            EndHashHex: FormatHash(endStateHash),
            MeasurementWindowMs: windowStopwatch.Elapsed.TotalMilliseconds,
            CommandCount: plan.Commands.Length,
            CommandPlanHashHex: FormatHash(plan.Hash));

        return new MeasurementResult
        {
            Metrics = metrics,
            LastMeasuredFrameIndex = warmupFrames + sampleFrames - 1,
        };
    }

    /* -------------------------------------------------------------- Abgriff */

    private static CaptureOutcome ExecuteCapture(
        BgfxDevice device,
        SceneResources resources,
        PinnedBuffers buffers,
        SimWorld world,
        SimulatedAgentView agentView,
        RepresentativeRig.PoseEvaluator poseEvaluator,
        PlanCursor plan,
        uint seed,
        IReadOnlyList<RepresentativeCameraSample> cameraSamples,
        FrameScratch scratch,
        int warmupFrames,
        int sampleFrames,
        string artifactPath)
    {
        var lastMeasuredFrameIndex = warmupFrames + sampleFrames - 1;
        var captureFrameIndex = RepresentativeScenario.CaptureFrameIndex(warmupFrames, sampleFrames);

        if (!device.IsReadBackSupported() || !device.IsBlitSupported())
        {
            Console.Error.WriteLine("bench: Frameabgriff nicht unterstuetzt (Backend ohne Readback/Blit).");
            return new CaptureOutcome(true, false, true, "readback-or-blit-unsupported-by-backend", captureFrameIndex, lastMeasuredFrameIndex, null);
        }

        if (captureFrameIndex >= cameraSamples.Count)
        {
            // Vertragswache: der vorberechnete Kamerahorizont muss den festen
            // Abgriffindex abdecken; andernfalls kontrolliert abbrechen statt
            // auf einen Ausnahmefall zu vertrauen.
            Console.Error.WriteLine("bench: Abgriffindex liegt hinter dem vorrechneten Kamerahorizont.");
            return new CaptureOutcome(true, false, true, "capture-frame-index-out-of-horizon", captureFrameIndex, lastMeasuredFrameIndex, null);
        }

        try
        {
            // Zwischenframes nach dem Messfenster bis zum festen Abgriffindex.
            for (var frameIndex = lastMeasuredFrameIndex + 1; frameIndex < captureFrameIndex; frameIndex++)
            {
                PresentFrame(device, resources, buffers, world, agentView, poseEvaluator, plan, seed, cameraSamples, scratch, frameIndex, accumulator: null);
            }

            ThrowIfQuitRequested();
            AdvanceState(buffers, world, agentView, poseEvaluator, plan, seed, world.TickIndex, accumulator: null);

            device.UpdateTexture2DRgba32F(
                resources.PaletteTexture,
                0,
                0,
                RepresentativeScenario.BonesPerNormalUnit * 3,
                RepresentativeScenario.VisibleUnitsTarget,
                buffers.Palette);

            SubmitShadowPasses(device, resources, buffers, CachedLights);

            var rtTexture = device.CreateTexture2D(
                BenchRunner.DefaultWidth,
                BenchRunner.DefaultHeight,
                BgfxSceneApi.TextureFormatRgba8,
                BgfxSceneApi.TextureFlagRt,
                initialData: default);
            var frameBuffer = device.CreateFrameBufferFromTexture(rtTexture);
            var readBackTexture = device.CreateTexture2D(
                BenchRunner.DefaultWidth,
                BenchRunner.DefaultHeight,
                BgfxSceneApi.TextureFormatRgba8,
                BgfxSceneApi.TextureFlagBlitDst | BgfxSceneApi.TextureFlagReadBack,
                initialData: default);

            try
            {
                var view16 = ComposeCameraFrame(cameraSamples[captureFrameIndex], scratch);

                // Hauptdurchgang des Frames normal beibehalten; der Abgriff
                // entsteht parallel in einem eigenen Renderziel-View.
                SubmitCompositePass(
                    device, resources, buffers, ViewMain, view16, MainProjection(), CachedLights,
                    scratch.Right4[0], scratch.Right4[1], scratch.Right4[2],
                    scratch.Up4[0], scratch.Up4[1], scratch.Up4[2]);

                device.SetViewFrameBuffer(ViewCapture, frameBuffer);
                device.ConfigureRenderTargetView(ViewCapture, HostBootstrap.ClearColorRgba, BenchRunner.DefaultWidth, BenchRunner.DefaultHeight);
                SubmitCompositePass(
                    device, resources, buffers, ViewCapture, view16, MainProjection(), CachedLights,
                    scratch.Right4[0], scratch.Right4[1], scratch.Right4[2],
                    scratch.Up4[0], scratch.Up4[1], scratch.Up4[2]);

                device.RenderFrame();

                device.BlitFull(ViewBlit, readBackTexture, rtTexture, BenchRunner.DefaultWidth, BenchRunner.DefaultHeight);
                device.RenderFrame();

                var captureBytes = new byte[BenchRunner.DefaultWidth * BenchRunner.DefaultHeight * 4];
                var captureHandle = GCHandle.Alloc(captureBytes, GCHandleType.Pinned);

                try
                {
                    var readyFrame = device.ReadTextureBegin(readBackTexture, captureHandle.AddrOfPinnedObject(), (uint)captureBytes.Length);
                    uint currentFrame;

                    do
                    {
                        ThrowIfQuitRequested();
                        currentFrame = device.RenderFrame();
                    }
                    while (currentFrame < readyFrame);
                }
                finally
                {
                    captureHandle.Free();
                }

                var bmp = FrameEvidence.EncodeBmpFromRgbaTopDown(captureBytes, BenchRunner.DefaultWidth, BenchRunner.DefaultHeight);
                File.WriteAllBytes(artifactPath, bmp);
                Console.WriteLine($"frame-artifact={artifactPath}");

                return new CaptureOutcome(true, true, false, string.Empty, captureFrameIndex, lastMeasuredFrameIndex, FrameEvidence.Sha256Hex(bmp));
            }
            finally
            {
                device.DestroyFrameBuffer(frameBuffer);
                device.DestroyTexture(readBackTexture);
                device.DestroyTexture(rtTexture);
                device.SetViewFrameBuffer(ViewCapture, BgfxDevice.InvalidIndex);
            }
        }
        catch (PlatformException exception)
        {
            Console.Error.WriteLine($"bench: Frameabgriff fehlgeschlagen: {exception.Error}");
            return new CaptureOutcome(true, false, true, "capture-failed-controlled", captureFrameIndex, lastMeasuredFrameIndex, null);
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"bench: Frameabgriff fehlgeschlagen: {exception.Message}");
            return new CaptureOutcome(true, false, true, "artifact-not-writable", captureFrameIndex, lastMeasuredFrameIndex, null);
        }
        catch (UnauthorizedAccessException exception)
        {
            Console.Error.WriteLine($"bench: Frameabgriff fehlgeschlagen: {exception.Message}");
            return new CaptureOutcome(true, false, true, "artifact-forbidden", captureFrameIndex, lastMeasuredFrameIndex, null);
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"bench: Frameabgriff fehlgeschlagen: {exception.Message}");
            return new CaptureOutcome(true, false, true, "artifact-path-invalid", captureFrameIndex, lastMeasuredFrameIndex, null);
        }
    }

    /* -------------------------------------------------------------- Report */

    internal sealed record ReportContext(
        uint Seed,
        int WarmupFrames,
        int SampleFrames,
        int WarmupTicks,
        int SampleTicks,
        DateTime ProcessStart,
        string Commit,
        string BuildMode,
        IReadOnlyList<ToolchainPin> Pins,
        SystemInfo.Environment Environment,
        string GlVersion,
        string GlRenderer,
        uint GpuIds,
        MeasurementMetrics Metrics,
        double SceneSetupTimeMs,
        RepresentativeBudgetVerdict Verdict,
        int ExitCode,
        CaptureOutcome Capture,
        IReadOnlyList<(string ProfileId, string ClaimedClass)> ClaimedBindings);

    private static string FormatHash(ulong hash) => hash.ToString(SimReportSchema.HashFormat, System.Globalization.CultureInfo.InvariantCulture);

    private static long TotalCollectionCount() =>
        GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);

    internal static object BuildReport(ReportContext ctx)
    {
        var limits = RepresentativeScenario.BudgetLimits.Documented;
        var metrics = ctx.Metrics;
        var capture = ctx.Capture;

        object frameEvidence = !capture.Requested
            ? new { captured = false, reason = FrameEvidence.ReasonNotRequested }
            : capture.Captured
                ? (object)new
                {
                    captured = true,
                    afterMeasurementWindow = true,
                    capturedAtFrameIndex = capture.CapturedAtFrameIndex,
                    lastMeasuredFrameIndex = capture.LastMeasuredFrameIndex,
                    width = BenchRunner.DefaultWidth,
                    height = BenchRunner.DefaultHeight,
                    format = FrameEvidence.FormatId,
                    sha256 = capture.ArtifactSha256,
                    statementLimit = FrameEvidence.StatementLimit,
                }
                : new { captured = false, reason = capture.Reason };

        return new
        {
            schemaVersion = RepresentativeReportSchema.CurrentVersion,
            mode = BenchReportSchema.ModeBench,
            command = $"{CommandName} --report <PFAD>",
            scenario = new
            {
                id = BenchScenarios.Representative,
                seed = ctx.Seed,
                resolution = new { width = BenchRunner.DefaultWidth, height = BenchRunner.DefaultHeight },
                displayProfile = "low",
                vsync = true,
                content = RepresentativeScenario.ContentId,
            },
            compositionTargets = new
            {
                visibleUnits = RepresentativeScenario.VisibleUnitsTarget,
                simulatedAgents = RepresentativeScenario.SimulatedAgents,
                backgroundActors = RepresentativeScenario.BackgroundActors,
                bonesPerNormalUnit = RepresentativeScenario.BonesPerNormalUnit,
                sunLights = RepresentativeScenario.SunLights,
                localShadowLights = RepresentativeScenario.LocalShadowLights,
                activeShadowPasses = RepresentativeScenario.LocalShadowLights,
                particlePeak = RepresentativeScenario.ParticlePeakTarget,
                shadowMapSizePx = RepresentativeScenario.ShadowMapSizePixels,
                framesPerSimTick = RepresentativeScenario.FramesPerSimTick,
            },
            cameraPath = new
            {
                algorithm = RepresentativeCameraFlight.AlgorithmId,
                samples = ctx.WarmupFrames + ctx.SampleFrames,
                hash = RepresentativeCameraFlight.HashHex(RepresentativeCameraFlight.Samples(ctx.Seed, ctx.WarmupFrames + ctx.SampleFrames)),
                firstSample = new
                {
                    frameIndex = 0,
                    yawDegrees = RepresentativeCameraFlight.Samples(ctx.Seed, 1)[0].YawDegrees.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                    pitchDegrees = RepresentativeCameraFlight.Samples(ctx.Seed, 1)[0].PitchDegrees.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                    radiusMeters = RepresentativeCameraFlight.Samples(ctx.Seed, 1)[0].RadiusMeters.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                    centerHeightMeters = RepresentativeCameraFlight.Samples(ctx.Seed, 1)[0].CenterHeightMeters.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                },
            },
            startedAtUtc = ctx.ProcessStart,
            finishedAtUtc = DateTime.UtcNow,
            environment = new
            {
                os = new { type = ctx.Environment.OsType, kernelRelease = ctx.Environment.KernelRelease },
                cpu = new { model = ctx.Environment.CpuModel },
                gpu = new { renderer = ctx.GlRenderer, vendorId = ctx.GpuIds >> 16, deviceId = ctx.GpuIds & 0xFFFFu },
                gl = new { version = ctx.GlVersion },
                backend = new { name = "OpenGL", id = BgfxDevice.RendererOpenGL, profile = "3.3 Core", vsync = true },
                rid = BenchEnvironment.Rid(),
                commit = ctx.Commit,
                buildMode = ctx.BuildMode,
                pins = ctx.Pins.Select(pin => new Dictionary<string, string>
                {
                    ["id"] = pin.Id,
                    ["refType"] = pin.RefType,
                    ["ref"] = pin.Ref,
                    ["commit"] = pin.Commit,
                    ["sourceSha256"] = pin.SourceSha256,
                    ["licenseSpdx"] = pin.LicenseSpdx,
                }),
            },
            measurement = new
            {
                warmupFrames = ctx.WarmupFrames,
                sampleFrames = ctx.SampleFrames,
                framesRendered = (long)ctx.WarmupFrames + ctx.SampleFrames,
                warmupTicks = (long)ctx.WarmupTicks,
                sampleTicks = (long)ctx.SampleTicks,
                rssSampleIntervalFrames = (long)RepresentativeScenario.RssSampleIntervalFrames,
                hashSampleIntervalTicks = (long)RepresentativeScenario.HashSampleIntervalTicks,
                measurementWindowMs = Math.Round(metrics.MeasurementWindowMs, 3),
            },
            metrics = new
            {
                frameTimeMs = new
                {
                    unit = "ms",
                    method = "stopwatch-frame-delta-including-shadow-and-composite-passes",
                    p50 = Math.Round(metrics.FrameBand.P50Ms, 3),
                    p95 = Math.Round(metrics.FrameBand.P95Ms, 3),
                    p99 = Math.Round(metrics.FrameBand.P99Ms, 3),
                },
                gpuTimeMs = metrics.GpuTimeMeasured
                    ? (object)new
                    {
                        measured = true,
                        unit = "ms",
                        method = "bgfx-stats-gpu-timer-p99",
                        p99 = Math.Round(metrics.GpuTimeP99Ms, 3),
                        timerFreqHz = metrics.GpuTimerFrequencyHz,
                    }
                    : new
                    {
                        measured = false,
                        reason = "backend-gpu-timer-unavailable",
                    },
                tickTimeMs = new
                {
                    unit = "ms",
                    method = "stopwatch-tick-delta-inside-frame",
                    p50 = Math.Round(metrics.TickBand.P50Ms, 3),
                    p95 = Math.Round(metrics.TickBand.P95Ms, 3),
                    p99 = Math.Round(metrics.TickBand.P99Ms, 3),
                },
                managedAllocationsBytes = new
                {
                    unit = "bytes",
                    method = "gc-total-allocated-bytes-precise-delta-per-measurement-window",
                    perWarmFrame = Math.Round(metrics.AllocationsPerWarmFrameBytes, 1),
                },
                gcPauseSumMs = new
                {
                    unit = "ms",
                    method = "gc-get-total-pause-duration-delta",
                    value = Math.Round(metrics.GcPauseSumMs, 3),
                },
                gcPauseCount = new
                {
                    unit = "count",
                    method = "gc-collection-count-gen0-to2-delta",
                    value = metrics.GcPauseCount,
                },
                workingSetKiB = metrics.WorkingSetMeasured
                    ? (object)new
                    {
                        measured = true,
                        unit = "KiB",
                        method = "proc-self-status-vmrss-samples",
                        min = metrics.RssMinKiB!.Value,
                        max = metrics.RssMaxKiB!.Value,
                        end = metrics.RssEndKiB!.Value,
                    }
                    : new
                    {
                        measured = false,
                        reason = metrics.RssReason ?? "rss-sampler-unavailable",
                    },
                gpuMemoryBytes = metrics.GpuMemoryMeasured
                    ? (object)new
                    {
                        measured = true,
                        unit = "bytes",
                        method = "bgfx-managed-memory-texture-rt-transient-end",
                        value = metrics.GpuMemoryBytesUsed,
                        textureMemoryUsed = metrics.TextureMemoryUsedBytes,
                    }
                    : new
                    {
                        measured = false,
                        reason = "bgfx-stats-unavailable",
                    },
                discreteVramBytes = new
                {
                    measured = false,
                    reason = "not-exposed-by-bgfx-stats-on-opengl",
                },
                drawSubmitCallsPerFrame = new
                {
                    unit = "count",
                    method = "bgfx-stats-numdraw-max-including-shadow-passes",
                    value = metrics.DrawSubmitCallsMax,
                },
                visibleTrianglesPerFrameGlobal = new
                {
                    unit = "count",
                    method = "bgfx-stats-numprims-trilist-max-including-shadow-passes",
                    value = metrics.TrianglesGlobalMax,
                },
                visibleTrianglesMainView = new
                {
                    unit = "count",
                    method = "composition-derived-main-view-without-shadow-repeat",
                    value = metrics.MainViewTriangles,
                },
                concurrentParticles = new
                {
                    unit = "count",
                    method = "particle-instance-count-max-per-frame",
                    value = metrics.ConcurrentParticlesObserved,
                },
                sceneSetupTimeMs = new
                {
                    unit = "ms",
                    method = "stopwatch-from-runner-start-to-first-composed-frame",
                    value = Math.Round(ctx.SceneSetupTimeMs, 3),
                },
                cardLoadBudgetLine = new
                {
                    applicable = false,
                    owner = BenchScenarios.Load,
                    reason = "card-load-budget-owned-by-bench-load-scenario",
                },
                runtimeShaderCompilation = new
                {
                    unit = "bool",
                    method = "offline-shaderc-binaries-only",
                    value = false,
                },
            },
            simulation = new
            {
                contractDocument = SimulationContract.DocumentPath,
                contractVersion = SimulationContract.ContractVersion,
                numericModel = SimulationContract.NumericModelId,
                hashAlgorithm = SimulationContract.HashAlgorithmId,
                worldId = SimulationContract.WorldId,
                tickRateHz = SimulationContract.TickRateHz,
                agentCount = SimulationContract.AgentCount,
                commandPlanAlgorithm = SimulationContract.CommandPlanAlgorithmId,
                commandPlanHash = metrics.CommandPlanHashHex,
                commandCount = metrics.CommandCount,
                stateHashChain = new
                {
                    unit = "hex64",
                    method = SimulationContract.HashAlgorithmId,
                    start = metrics.StartHashHex,
                    intervalSampleTicks = metrics.HashSampleTicks,
                    intervalHashes = metrics.HashSamplesHex,
                    end = metrics.EndHashHex,
                },
            },
            compositionObserved = new
            {
                visibleUnitsRendered = Counted(metrics.VisibleUnitsObserved),
                simulatedAgentsMapped = Counted(RepresentativeScenario.SimulatedAgents),
                backgroundActorsWritten = Counted(RepresentativeScenario.BackgroundActors),
                paletteRowsBound = Counted(metrics.PaletteRowsBound),
                sunLightsConfigured = Counted(RepresentativeScenario.SunLights),
                localShadowLightsWithActivePasses = Counted(RepresentativeScenario.LocalShadowLights),
                mainViewTrianglesDerived = Counted(metrics.MainViewTriangles),
            },
            gate = new
            {
                limits = new
                {
                    p99FrameTimeMsMax = limits.P99FrameTimeLimitMs,
                    p99GpuTimeHardLimitMs = limits.P99GpuTimeHardLimitMs,
                    p99GpuTimeTargetMs = limits.P99GpuTimeTargetMs,
                    p99TickTimeHardLimitMs = limits.P99TickTimeHardLimitMs,
                    p99TickTimeTargetMs = limits.P99TickTimeTargetMs,
                    managedAllocationsPerWarmFrameBytesMax = limits.ManagedAllocationsPerWarmFrameLimitBytes,
                    drawSubmitCallsPerFrameMax = limits.DrawSubmitCallsPerFrameLimit,
                    visibleTrianglesMainViewLimit = limits.VisibleTrianglesMainViewLimit,
                    concurrentParticlesLimit = limits.ConcurrentParticlesLimit,
                    sunLightsMax = limits.SunLightsMax,
                    localShadowLightsMax = limits.LocalShadowLightsMax,
                    runtimeShaderCompilationAllowed = false,
                    workingSetTargetMiB = limits.WorkingSetTargetMiB,
                    workingSetHardLimitMiB = limits.WorkingSetHardLimitMiB,
                },
                pass = ctx.Verdict.Pass,
                gpuTimeTargetMet = ctx.Verdict.GpuTimeTargetMet,
                tickTimeTargetMet = ctx.Verdict.TickTimeTargetMet,
                rssTargetMet = ctx.Verdict.RssTargetMet,
                violations = ctx.Verdict.Violations,
            },
            profiles = ProfileBinding.MandatoryWithoutReferenceHardware()
                .Concat(ctx.ClaimedBindings.Select(binding => ProfileBinding.EvaluateClaim(
                    binding.ProfileId,
                    new HardwareDescriptor(ctx.GlRenderer, ctx.Environment.CpuModel, IsDeveloperWorkstation: true),
                    binding.ClaimedClass,
                    referenceMachinesNamed: false)))
                .Select(status => new
                {
                    id = status.ProfileId,
                    status = status.Status,
                    boundReferenceClass = status.BoundReferenceClass,
                    reason = status.Reason,
                })
                .ToArray(),
            baseline = new
            {
                classification = "diagnostic-developer-workstation",
                protocol = "qops001-2026-08-24",
            },
            frameEvidence,
            exitCode = ctx.ExitCode,
        };
    }

    private static object Counted(long value) => new { unit = "count", method = "scenario-config-or-runtime-counter", value };
}
