using System.Diagnostics;
using Riftward.Platform;
using Riftward.Platform.Interop;

namespace Riftward.App;

/// <summary>
/// Plattform-Smoke (AC-T010-03): Fenster oeffnen, OpenGL 3.3 Core als bgfx-
/// Backend initialisieren, mindestens einen fehlerfreien Dreiecksframe rendern,
/// Fenster- und Beendigungsereignisse entgegennehmen, kontrolliert beenden und
/// einen maschinenlesbaren Report schreiben. Feste Zeitgrenze; Exitcode 0 nur
/// bei sauberem Lauf.
/// </summary>
internal static class SmokeRunner
{
    public static int Run(CommandLineArgs arguments)
    {
        var reportPath = arguments.Option("--report");
        var timeLimitMs = (int)Math.Clamp(arguments.NumberOption("--time-limit-ms", 4000), 250, 60_000);
        var width = (int)Math.Clamp(arguments.NumberOption("--width", 1280), 64, 4096);
        var height = (int)Math.Clamp(arguments.NumberOption("--height", 720), 64, 4096);

        var environment = SystemInfo.Capture();
        var processStart = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        double startupToFirstFrameMs = double.NaN;
        long framesRendered = 0;
        uint quitEvents = 0;
        uint windowEvents = 0;
        string glVersion;
        string glRenderer;
        string glSlVersion;
        uint gpuIds;

        var context = HostBootstrap.Start(arguments, width, height, vsync: true);

        try
        {
            var api = NativeApi.Instance;
            (glVersion, glRenderer, glSlVersion) = api.GlStrings();
            gpuIds = api.GpuIds();

            var deadlineTicks = Environment.TickCount64 + timeLimitMs;
            SdlEventBuffer buffer = default;
            var firstFrameDone = false;
            var quitRequested = false;

            while (!quitRequested && Environment.TickCount64 < deadlineTicks)
            {
                while (api.PollEvent(ref buffer))
                {
                    switch (buffer.Type)
                    {
                        case SdlEventCodes.Quit:
                            quitEvents++;
                            quitRequested = true;
                            break;

                        case SdlEventCodes.WindowCloseRequested:
                            windowEvents++;
                            quitRequested = true;
                            break;

                        case SdlEventCodes.WindowResized:
                        case SdlEventCodes.WindowPixelSizeChanged:
                        case SdlEventCodes.WindowExposed:
                            windowEvents++;
                            break;

                        default:
                            break;
                    }
                }

                if (quitRequested)
                {
                    break;
                }

                context.Triangle.Submit();
                context.Device.RenderFrame();
                framesRendered++;

                if (!firstFrameDone)
                {
                    startupToFirstFrameMs = (DateTime.UtcNow - processStart).TotalMilliseconds;
                    firstFrameDone = true;
                }
            }

            if (framesRendered == 0)
            {
                throw new PlatformException(new PlatformError(
                    PlatformErrorCode.SmokeNoFrame,
                    "Smoke endete ohne gerenderten Frame."));
            }
        }
        finally
        {
            HostBootstrap.Stop(context);
        }

        if (reportPath is not null)
        {
            ReportWriter.Write(reportPath, new
            {
                schemaVersion = 1,
                mode = "plattformsmoke",
                command = "Riftward.App plattformsmoke",
                startedAtUtc = processStart,
                finishedAtUtc = DateTime.UtcNow,
                exitCode = 0,
                os = new
                {
                    type = environment.OsType,
                    kernelRelease = environment.KernelRelease,
                },
                cpu = new { model = environment.CpuModel, flagsExcerpt = environment.CpuFlagsExcerpt },
                gpu = new
                {
                    renderer = glRenderer,
                    vendorId = gpuIds >> 16,
                    deviceId = gpuIds & 0xFFFFu,
                },
                gl = new { version = glVersion, slVersion = glSlVersion },
                backend = new { name = "OpenGL", id = BgfxDevice.RendererOpenGL, profile = "3.3 Core" },
                shimApiVersion = NativeApi.Instance.ApiVersion(),
                framesRendered,
                eventsHandled = new { quit = quitEvents, window = windowEvents },
                startupToFirstFrameMs = Math.Round(startupToFirstFrameMs, 1),
                timeLimitMs,
                pins = context.Pins,
                artifacts = new
                {
                    checkedFiles = context.ArtifactReport.Checks.Count,
                    manifestSha256 = context.ManifestSha256,
                },
            });
        }

        return ExitCodes.Ok;
    }
}
