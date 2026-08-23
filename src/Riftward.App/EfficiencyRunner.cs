using System.Diagnostics;
using Riftward.Platform;
using Riftward.Platform.Interop;

namespace Riftward.App;

/// <summary>
/// Effizienzbaseline (AC-T010-07): misst Startzeit bis erstem Frame, Idle-RSS,
/// p99-Frametime bei aktivem VSync, verwaltete Allokationen je warmem Frame,
/// RSS-Drift im Idle-Fenster und Draw-Aufrufe je Frame. Jede harte Grenzwert-
/// verletzung laesst das Gate fehlschlagen; der Report wird trotzdem
/// geschrieben. Keine Shaderkompilierung zur Laufzeit (nur geladene Binaer-
/// dateien aus dem geprueften Artefaktverzeichnis).
/// </summary>
internal static class EfficiencyRunner
{
    private const double StartupLimitMs = 5000.0;
    private const long IdleRssTargetMiB = 300;
    private const long IdleRssHardMiB = 450;
    private const double P99FrameTimeLimitMs = 33.3;
    private const long ManagedAllocationsPerFrameLimitBytes = 1024;
    private const long RssDriftLimitMiB = 16;
    private const uint DrawCallsPerFrameLimit = 8;

    public static int Run(CommandLineArgs arguments)
    {
        var reportPath = arguments.Option("--report");

        if (string.IsNullOrWhiteSpace(reportPath))
        {
            Console.Error.WriteLine("effizienzbaseline: --report PFAD ist erforderlich.");
            return ExitCodes.Usage;
        }

        var idleWindowSeconds = Math.Clamp(arguments.NumberOption("--idle-window-seconds", 600), 10, 3600);
        var warmupFrames = (int)Math.Clamp(arguments.NumberOption("--warmup-frames", 180), 30, 5_000);
        var sampleFrames = (int)Math.Clamp(arguments.NumberOption("--sample-frames", 900), 60, 20_000);
        var width = (int)Math.Clamp(arguments.NumberOption("--width", 1280), 64, 4096);
        var height = (int)Math.Clamp(arguments.NumberOption("--height", 720), 64, 4096);

        var environment = SystemInfo.Capture();
        var processStart = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        double startupToFirstFrameMs = double.NaN;

        var context = HostBootstrap.Start(arguments, width, height, vsync: true);

        string glVersion;
        string glRenderer;
        uint gpuIds;
        double p99FrameTimeMs;
        double allocationsPerFrameBytes;
        uint maxDrawCallsPerFrame;
        long rssDriftMiB;
        long idleRssMedianKiB;
        long framesRendered;

        try
        {
            var api = NativeApi.Instance;
            (glVersion, glRenderer, _) = api.GlStrings();
            gpuIds = api.GpuIds();

            // Warmphase ohne Messung; die Startzeit bezieht sich auf den ersten
            // gerenderten Frame.
            for (var frame = 0; frame < warmupFrames; frame++)
            {
                context.Triangle.Submit();
                context.Device.RenderFrame();

                if (frame == 0)
                {
                    startupToFirstFrameMs = (DateTime.UtcNow - processStart).TotalMilliseconds;
                }
            }

            // Aktive Messphase: p99-Frametime + Allokationen je Frame.
            var frameTimes = new double[sampleFrames];
            SdlEventBuffer eventBuffer = default;
            maxDrawCallsPerFrame = 0;

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            var allocationStartBytes = GC.GetTotalAllocatedBytes(precise: true);

            for (var frame = 0; frame < sampleFrames; frame++)
            {
                while (api.PollEvent(ref eventBuffer))
                {
                    if (eventBuffer.Type is SdlEventCodes.Quit or SdlEventCodes.WindowCloseRequested)
                    {
                        throw new PlatformException(new PlatformError(
                            PlatformErrorCode.Internal,
                            "Effizienzlauf durch Beendigungsereignis abgebrochen."));
                    }
                }

                var startTimestamp = Stopwatch.GetTimestamp();
                context.Triangle.Submit();
                context.Device.RenderFrame();
                var endTimestamp = Stopwatch.GetTimestamp();

                frameTimes[frame] = Measurement.TimestampDeltaToMilliseconds(startTimestamp, endTimestamp);
                maxDrawCallsPerFrame = Math.Max(maxDrawCallsPerFrame, context.Device.DrawCalls);
            }

            var allocationEndBytes = GC.GetTotalAllocatedBytes(precise: true);
            allocationsPerFrameBytes = (allocationEndBytes - allocationStartBytes) / (double)sampleFrames;
            p99FrameTimeMs = Measurement.Percentile99(frameTimes);
            framesRendered = warmupFrames + sampleFrames;

            // Idle-Fenster: kontinuierlich weiterrendern, RSS periodisch samplen.
            var rssSamples = new List<long>((int)(idleWindowSeconds / 5) + 2);
            var windowDeadlineTicks = Environment.TickCount64 + (idleWindowSeconds * 1000);
            var nextSampleTicks = Environment.TickCount64;

            while (Environment.TickCount64 < windowDeadlineTicks)
            {
                context.Triangle.Submit();
                context.Device.RenderFrame();
                framesRendered++;

                if (Environment.TickCount64 >= nextSampleTicks)
                {
                    var rss = SystemInfo.RssKiB();

                    if (rss.HasValue)
                    {
                        rssSamples.Add(rss.Value);
                    }

                    nextSampleTicks = Environment.TickCount64 + 5_000;
                }
            }

            if (rssSamples.Count < 2)
            {
                throw new PlatformException(new PlatformError(
                    PlatformErrorCode.Internal,
                    "Idle-RSS konnte nicht ausreichend gemessen werden."));
            }

            var sorted = rssSamples.ToArray();
            Array.Sort(sorted);
            idleRssMedianKiB = sorted[sorted.Length / 2];
            rssDriftMiB = (sorted[^1] - sorted[0]) / 1024;
            rssDriftMiB = Math.Abs(rssDriftMiB);
        }
        finally
        {
            HostBootstrap.Stop(context);
        }

        var idleRssMiB = idleRssMedianKiB / 1024;
        var budgetsPass =
            startupToFirstFrameMs <= StartupLimitMs
            && idleRssMiB <= IdleRssHardMiB
            && p99FrameTimeMs <= P99FrameTimeLimitMs
            && allocationsPerFrameBytes <= ManagedAllocationsPerFrameLimitBytes
            && rssDriftMiB <= RssDriftLimitMiB
            && maxDrawCallsPerFrame <= DrawCallsPerFrameLimit;

        ReportWriter.Write(reportPath!, new
        {
            schemaVersion = 1,
            mode = "effizienzbaseline",
            command = "Riftward.App effizienzbaseline",
            startedAtUtc = processStart,
            finishedAtUtc = DateTime.UtcNow,
            exitCode = budgetsPass ? 0 : ExitCodes.Map(PlatformErrorCode.EfficiencyBudgetViolated),
            os = new { type = environment.OsType, kernelRelease = environment.KernelRelease },
            cpu = new { model = environment.CpuModel, flagsExcerpt = environment.CpuFlagsExcerpt },
            gpu = new { renderer = glRenderer, vendorId = gpuIds >> 16, deviceId = gpuIds & 0xFFFFu },
            gl = new { version = glVersion },
            backend = new { name = "OpenGL", id = BgfxDevice.RendererOpenGL, profile = "3.3 Core", vsync = true },
            measurement = new
            {
                warmupFrames,
                sampleFrames,
                idleWindowSeconds,
                framesRendered,
            },
            results = new
            {
                startupToFirstFrameMs = Math.Round(startupToFirstFrameMs, 1),
                idleRssMiB,
                idleRssTargetOk = idleRssMiB <= IdleRssTargetMiB,
                p99FrameTimeMs = Math.Round(p99FrameTimeMs, 3),
                managedAllocationsPerWarmFrameBytes = Math.Round(allocationsPerFrameBytes, 1),
                rssDriftMiB,
                drawCallsPerFrameMax = maxDrawCallsPerFrame,
                runtimeShaderCompilation = false,
            },
            budgets = new
            {
                startupToFirstFrameMsMax = StartupLimitMs,
                idleRssTargetMiB = IdleRssTargetMiB,
                idleRssHardMiB = IdleRssHardMiB,
                p99FrameTimeMsMax = P99FrameTimeLimitMs,
                managedAllocationsPerFrameBytesMax = ManagedAllocationsPerFrameLimitBytes,
                rssDriftMiBMax = RssDriftLimitMiB,
                drawCallsPerFrameMax = DrawCallsPerFrameLimit,
                pass = budgetsPass,
            },
            pins = context.Pins,
            artifacts = new
            {
                checkedFiles = context.ArtifactReport.Checks.Count,
                manifestSha256 = context.ManifestSha256,
            },
        });

        return budgetsPass ? ExitCodes.Ok : ExitCodes.Map(PlatformErrorCode.EfficiencyBudgetViolated);
    }
}
