using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Riftward.App.Bench;
using Riftward.Platform;
using Riftward.Platform.Interop;

namespace Riftward.App;

/// <summary>
/// BENCH-EMPTY (T-020): deterministische leere Szene bei 1920x1080 Low auf
/// dem GL-3.3-Core-Pflichtpfad mit festem Kameraflugskript, maschinenlesbarer
/// Telemetrie nach NF-007 und fail-closed Budgetgate. Budgetverletzungen
/// ergeben einen definierten Exitcode, schreiben den Report aber trotzdem.
/// Laeufe auf dem Entwickler-PC sind diagnostische Baseline gemaeß dem
/// Q-OPS-001-Klaerungsprotokoll; Pflichtprofile bleiben ohne benannte
/// Referenzhardware NOT-MEASURED und werden eskaliert statt ersetzt.
/// </summary>
internal static class BenchRunner
{
    internal static readonly JsonSerializerOptions ReportJsonOptions = new() { WriteIndented = false };

    public const int DefaultWidth = 1920;
    public const int DefaultHeight = 1080;
    public const int DefaultWarmupFrames = 180;
    public const int DefaultSampleFrames = 900;
    public const int RssSampleIntervalFrames = 30;
    public const double FieldOfViewDegrees = 60.0;
    public const string CommandName = "./scripts/rift.sh bench --scenario bench-empty";

    public static int Run(CommandLineArgs arguments)
    {
        // Szenariopruefung vor jedem teuren Schritt: Ein unbekanntes oder noch
        // nicht implementiertes Szenario bricht ab, ohne einen Report zu
        // vortaeuschen (AC-T020-01).
        var scenarioId = arguments.Option("--scenario");

        switch (BenchScenarios.Classify(scenarioId))
        {
            case BenchScenarios.Support.Implemented:
                if (string.Equals(scenarioId, BenchScenarios.Sim, StringComparison.Ordinal))
                {
                    // Headless CPU-Baseline ohne Fenster/Renderer (T-021).
                    return SimBenchRunner.Run(arguments);
                }

                break;

            case BenchScenarios.Support.RegisteredNotImplemented:
                Console.Error.WriteLine(
                    $"bench: Szenario '{scenarioId}' ist als Pflichtbenchmark registriert, aber in diesem Auftrag nicht implementiert (kein Report).");
                return ExitCodes.Map(PlatformErrorCode.BenchScenarioUnavailable);

            default:
                Console.Error.WriteLine(
                    $"bench: unbekanntes Szenario '{scenarioId ?? "<fehlt>"}'. Bekannte Szenarien: {string.Join(", ", BenchScenarios.Known)}.");
                return ExitCodes.Map(PlatformErrorCode.BenchScenarioUnavailable);
        }

        var reportPath = arguments.Option("--report");

        if (string.IsNullOrWhiteSpace(reportPath))
        {
            Console.Error.WriteLine("bench: --report PFAD ist erforderlich.");
            return ExitCodes.Usage;
        }

        var seed = (uint)Math.Clamp(arguments.NumberOption("--seed", CameraFlight.DefaultSeed), 0, uint.MaxValue);
        var warmupFrames = (int)Math.Clamp(arguments.NumberOption("--warmup-frames", DefaultWarmupFrames), 30, 5_000);
        var sampleFrames = (int)Math.Clamp(arguments.NumberOption("--sample-frames", DefaultSampleFrames), 60, 20_000);
        var claimedBindings = ParseProfileBindings(arguments);

        var environment = SystemInfo.Capture();
        var processStart = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        var commit = BenchEnvironment.CommitId();
        var buildMode = BenchEnvironment.BuildMode();

        var context = HostBootstrap.Start(arguments, DefaultWidth, DefaultHeight, vsync: true);
        string glVersion;
        string glRenderer;
        uint gpuIds;

        double p50FrameTimeMs = 0;
        double p95FrameTimeMs = 0;
        double p99FrameTimeMs = 0;
        double allocationsPerWarmFrameBytes = 0;
        double gcPauseSumMs = 0;
        long gcPauseCount = 0;
        long rssMinKiB = 0;
        long rssMaxKiB = 0;
        long rssEndKiB = 0;
        uint drawSubmitCallsPerFrameMax = 0;
        uint visibleTrianglesPerFrameMax = 0;
        bool gpuTimeMeasured = false;
        double gpuTimeP99Ms = 0;
        long gpuTimerFrequencyHz = 0;
        long vramBytesUsed = -1;
        long textureMemoryBytesUsed = 0;
        long framesRendered = 0;

        IReadOnlyList<CameraSample> cameraSamples = [];
        string cameraPathHash = string.Empty;

        try
        {
            var api = NativeApi.Instance;
            (glVersion, glRenderer, _) = api.GlStrings();
            gpuIds = api.GpuIds();

            var artifactsDir = arguments.Option("--artifacts-dir") ?? ".ai/runtime/cache/native/dist";
            var vertexShader = File.ReadAllBytes(Path.Combine(artifactsDir, "shaders", "bench_empty.vs.bin"));
            var fragmentShader = File.ReadAllBytes(Path.Combine(artifactsDir, "shaders", "triangle.fs.bin"));
            var benchTriangle = context.Device.CreateTriangleResources(BenchScene.Vertices, vertexShader, fragmentShader);

            try
            {
                cameraSamples = CameraFlight.Samples(seed, warmupFrames + sampleFrames);
                cameraPathHash = CameraFlight.HashHex(cameraSamples);

                var projection = CameraMath.ToFloat16(
                    CameraMath.PerspectiveFov(FieldOfViewDegrees, DefaultWidth / (double)DefaultHeight, 0.1, 100.0));

                SdlEventBuffer eventBuffer = default;

                // Warmphase ohne Messung; Kameraflug ist von Frame 0 an gebunden.
                for (var frame = 0; frame < warmupFrames; frame++)
                {
                    ThrowIfQuitRequested(api, ref eventBuffer);
                    ApplyCamera(context.Device, projection, cameraSamples[frame]);
                    benchTriangle.Submit();
                    context.Device.RenderFrame();
                }

                // Messphase: Framezeiten, bgfx-Statistik, RSS-Stichproben,
                // verwaltete Allokationen und GC-Pausen (AC-T020-02).
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();

                var pauseSumBefore = GC.GetTotalPauseDuration();
                var collectionCountBefore = TotalCollectionCount();
                var allocationStartBytes = GC.GetTotalAllocatedBytes(precise: true);

                var frameTimes = new double[sampleFrames];
                var gpuTimes = new List<double>(sampleFrames);
                drawSubmitCallsPerFrameMax = 0;
                visibleTrianglesPerFrameMax = 0;
                var rssSamples = new List<long>(sampleFrames / RssSampleIntervalFrames + 2);
                gpuTimeMeasured = false;

                for (var frame = 0; frame < sampleFrames; frame++)
                {
                    ThrowIfQuitRequested(api, ref eventBuffer);
                    ApplyCamera(context.Device, projection, cameraSamples[warmupFrames + frame]);

                    var startTimestamp = Stopwatch.GetTimestamp();
                    benchTriangle.Submit();
                    context.Device.RenderFrame();
                    frameTimes[frame] = Measurement.TimestampDeltaToMilliseconds(startTimestamp, Stopwatch.GetTimestamp());

                    if (context.Device.TryReadStats(out var stats))
                    {
                        drawSubmitCallsPerFrameMax = Math.Max(drawSubmitCallsPerFrameMax, stats.NumDraw);
                        visibleTrianglesPerFrameMax = Math.Max(visibleTrianglesPerFrameMax, stats.TrianglesRendered);
                        vramBytesUsed = stats.ManagedGpuMemoryUsedBytes;
                        textureMemoryBytesUsed = stats.TextureMemoryUsedBytes;

                        if (stats.GpuTimerFrequency > 0)
                        {
                            gpuTimeMeasured = true;
                            gpuTimerFrequencyHz = stats.GpuTimerFrequency;
                            gpuTimes.Add((stats.GpuTimeEndTicks - stats.GpuTimeBeginTicks) * 1000.0 / stats.GpuTimerFrequency);
                        }
                    }

                    if (frame % RssSampleIntervalFrames == 0 && SystemInfo.RssKiB() is { } rss)
                    {
                        rssSamples.Add(rss);
                    }
                }

                var allocationEndBytes = GC.GetTotalAllocatedBytes(precise: true);
                var pauseSumAfter = GC.GetTotalPauseDuration();
                allocationsPerWarmFrameBytes = (allocationEndBytes - allocationStartBytes) / (double)sampleFrames;
                gcPauseSumMs = (pauseSumAfter - pauseSumBefore).TotalMilliseconds;
                gcPauseCount = TotalCollectionCount() - collectionCountBefore;

                var band = TelemetryMath.Band(frameTimes);
                p50FrameTimeMs = band.P50Ms;
                p95FrameTimeMs = band.P95Ms;
                p99FrameTimeMs = band.P99Ms;

                if (gpuTimeMeasured)
                {
                    gpuTimeP99Ms = TelemetryMath.Percentile(gpuTimes, 0.99);
                }

                framesRendered = warmupFrames + sampleFrames;

                if (rssSamples.Count < 2)
                {
                    throw new PlatformException(new PlatformError(
                        PlatformErrorCode.Internal,
                        "Arbeitssatz-Stichproben konnten nicht ausreichend gesammelt werden."));
                }

                rssMinKiB = rssSamples.Min();
                rssMaxKiB = rssSamples.Max();
                rssEndKiB = rssSamples[^1];

                // Speicher-/GPU-Kennung des letzten vollstaendigen Frames;
                // fehlende Statistik bleibt als unavailable sichtbar statt
                // geschaetzt zu werden.
                if (!context.Device.TryReadStats(out var lastStats))
                {
                    vramBytesUsed = -1;
                }
                else
                {
                    vramBytesUsed = lastStats.ManagedGpuMemoryUsedBytes;
                    textureMemoryBytesUsed = lastStats.TextureMemoryUsedBytes;
                }
            }
            finally
            {
                benchTriangle.Dispose();
            }
        }
        finally
        {
            HostBootstrap.Stop(context);
        }

        var verdict = BudgetGate.Evaluate(BenchBudgetLimits.Documented, new BenchBudgetInputs(
            P99FrameTimeMs: p99FrameTimeMs,
            ManagedAllocationsPerWarmFrameBytes: allocationsPerWarmFrameBytes,
            DrawSubmitCallsPerFrameMax: drawSubmitCallsPerFrameMax,
            RuntimeShaderCompilationObserved: false,
            RssMinKiB: rssMinKiB,
            RssMaxKiB: rssMaxKiB,
            RssEndKiB: rssEndKiB));

        var gateExitCode = verdict.Pass ? ExitCodes.Ok : ExitCodes.Map(PlatformErrorCode.BenchBudgetViolated);
        var limits = BenchBudgetLimits.Documented;

        var reportJson = JsonSerializer.Serialize(new
        {
            schemaVersion = BenchReportSchema.CurrentVersion,
            mode = BenchReportSchema.ModeBench,
            command = $"{CommandName} --report <PFAD>",
            scenario = new
            {
                id = BenchScenarios.Empty,
                seed,
                resolution = new { width = DefaultWidth, height = DefaultHeight },
                displayProfile = "low",
                vsync = true,
                content = "clear-pass-plus-technical-test-pattern",
            },
            cameraPath = new
            {
                algorithm = CameraFlight.AlgorithmId,
                samples = warmupFrames + sampleFrames,
                hash = cameraPathHash,
                firstSample = new
                {
                    frameIndex = cameraSamples[0].FrameIndex,
                    yawDegrees = cameraSamples[0].YawDegrees.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                    pitchDegrees = cameraSamples[0].PitchDegrees.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                    radiusMeters = cameraSamples[0].RadiusMeters.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
                },
            },
            startedAtUtc = processStart,
            finishedAtUtc = DateTime.UtcNow,
            environment = new
            {
                os = new { type = environment.OsType, kernelRelease = environment.KernelRelease },
                cpu = new { model = environment.CpuModel },
                gpu = new { renderer = glRenderer, vendorId = gpuIds >> 16, deviceId = gpuIds & 0xFFFFu },
                gl = new { version = glVersion },
                backend = new { name = "OpenGL", id = BgfxDevice.RendererOpenGL, profile = "3.3 Core", vsync = true },
                rid = BenchEnvironment.Rid(),
                commit,
                buildMode,
                pins = context.Pins.Select(pin => new Dictionary<string, string>
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
                warmupFrames,
                sampleFrames,
                framesRendered,
                rssSampleIntervalFrames = (long)RssSampleIntervalFrames,
            },
            metrics = new
            {
                frameTimeMs = new
                {
                    unit = "ms",
                    method = "stopwatch-frame-delta",
                    p50 = Math.Round(p50FrameTimeMs, 3),
                    p95 = Math.Round(p95FrameTimeMs, 3),
                    p99 = Math.Round(p99FrameTimeMs, 3),
                },
                managedAllocationsBytes = new
                {
                    unit = "bytes",
                    method = "gc-total-allocated-bytes-precise-delta",
                    perWarmFrame = Math.Round(allocationsPerWarmFrameBytes, 1),
                },
                gcPauseSumMs = new
                {
                    unit = "ms",
                    method = "gc-get-total-pause-duration-delta",
                    value = Math.Round(gcPauseSumMs, 3),
                },
                gcPauseCount = new
                {
                    unit = "count",
                    method = "gc-collection-count-gen0-to2-delta",
                    value = gcPauseCount,
                },
                workingSetKiB = new
                {
                    unit = "KiB",
                    method = "proc-self-status-vmrss-samples",
                    min = rssMinKiB,
                    max = rssMaxKiB,
                    end = rssEndKiB,
                },
                drawSubmitCallsPerFrame = new
                {
                    unit = "count",
                    method = "bgfx-stats-numdraw-max",
                    value = drawSubmitCallsPerFrameMax,
                },
                visibleTrianglesPerFrame = new
                {
                    unit = "count",
                    method = "bgfx-stats-numprims-trilist-max",
                    value = visibleTrianglesPerFrameMax,
                },
                gpuTimeMs = gpuTimeMeasured
                    ? (object)new
                    {
                        measured = true,
                        unit = "ms",
                        method = "bgfx-stats-gpu-timer-p99",
                        p99 = Math.Round(gpuTimeP99Ms, 3),
                        timerFreqHz = gpuTimerFrequencyHz,
                    }
                    : new
                    {
                        measured = false,
                        reason = "backend-gpu-timer-unavailable",
                    },
                vramBytes = vramBytesUsed >= 0
                    ? (object)new
                    {
                        measured = true,
                        unit = "bytes",
                        method = "bgfx-managed-memory-texture-rt-transient-end",
                        value = vramBytesUsed,
                        textureMemoryUsed = textureMemoryBytesUsed,
                    }
                    : new
                    {
                        measured = false,
                        reason = "bgfx-stats-unavailable",
                    },
                runtimeShaderCompilation = new
                {
                    unit = "bool",
                    method = "offline-shaderc-binaries-only",
                    value = false,
                },
            },
            gate = new
            {
                limits = new
                {
                    p99FrameTimeMsMax = limits.P99FrameTimeLimitMs,
                    managedAllocationsPerWarmFrameBytesMax = limits.ManagedAllocationsPerWarmFrameLimitBytes,
                    drawSubmitCallsPerFrameMax = limits.DrawSubmitCallsPerFrameLimit,
                    runtimeShaderCompilationAllowed = false,
                    rssTargetMiB = limits.RssTargetMiB,
                    rssHardLimitMiB = limits.RssHardLimitMiB,
                },
                pass = verdict.Pass,
                rssTargetMet = verdict.RssTargetMet,
                violations = verdict.Violations,
            },
            profiles = ProfileBinding.MandatoryWithoutReferenceHardware()
                .Concat(claimedBindings.Select(binding => ProfileBinding.EvaluateClaim(
                    binding.ProfileId,
                    new HardwareDescriptor(glRenderer, environment.CpuModel, IsDeveloperWorkstation: true),
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
            exitCode = gateExitCode,
        }, ReportJsonOptions) + "\n";

        // Selbstpruefung gegen den Evidenzvertrag, bevor der Report gilt
        // (AC-T020-02): ein Vertragsverstoß wird nie still durchgereicht.
        var schemaErrors = BenchReportSchema.Validate(reportJson);

        if (schemaErrors.Count > 0)
        {
            Console.Error.WriteLine($"bench: Report widerspricht dem Schemavertrag: {string.Join("; ", schemaErrors)}");
            WriteReportOrDiagnose(reportPath!, reportJson);
            return ExitCodes.Map(PlatformErrorCode.TelemetryInvalid);
        }

        if (!WriteReportOrDiagnose(reportPath!, reportJson))
        {
            return ExitCodes.Map(PlatformErrorCode.ReportNotWritable);
        }

        return gateExitCode;
    }

    /// <summary>
    /// Wendet das Kameraflugskript eines Frames als Viewtransformation an.
    /// Die Szene enthaelt bewusst keine Weltgeometrie jense des Testmusters;
    /// die Bahn ist die deterministische Timeline des Szenarios.
    /// </summary>
    private static void ApplyCamera(BgfxDevice device, float[] projection, CameraSample sample)
    {
        var pose = CameraFlight.Pose(sample);
        var view = CameraMath.ToFloat16(CameraMath.LookAt(pose.Eye, pose.Center, new CameraMath.Vec3(0, 1, 0)));
        device.SetViewTransform(BgfxDevice.ViewId, view, projection);
    }

    private static void ThrowIfQuitRequested(NativeApi api, ref SdlEventBuffer eventBuffer)
    {
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

    private static long TotalCollectionCount() => GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);

    internal static List<(string ProfileId, string ClaimedClass)> ParseProfileBindings(CommandLineArgs arguments)
    {
        var bindings = new List<(string ProfileId, string ClaimedClass)>();

        foreach (var raw in arguments.AllOptions("--bind-profile"))
        {
            var separator = raw.IndexOf('=');

            if (separator <= 0 || separator == raw.Length - 1)
            {
                Console.Error.WriteLine($"bench: --bind-profile erwartet PROFILE=REFERENZKLASSE, erhalten '{raw}'.");
                continue;
            }

            bindings.Add((raw[..separator], raw[(separator + 1)..]));
        }

        return bindings;
    }

    internal static bool WriteReportOrDiagnose(string reportPath, string json)
    {
        try
        {
            File.WriteAllText(reportPath, json);
            Console.WriteLine($"report={reportPath}");
            return true;
        }
        catch (UnauthorizedAccessException exception)
        {
            Console.Error.WriteLine($"bench: Reportpfad nicht schreibbar: {exception.Message}");
            return false;
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"bench: Report konnte nicht geschrieben werden: {exception.Message}");
            return false;
        }
        catch (ArgumentException exception)
        {
            Console.Error.WriteLine($"bench: Reportpfad ungueltig: {exception.Message}");
            return false;
        }
    }
}
