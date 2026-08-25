using System.Text.Json;
using Riftward.App.Bench;
using Riftward.Platform;

namespace Riftward.App.Soak;

/// <summary>
/// Abschnitt-0-Kalibrierung des gatenden Vertragsspikes (T-022): fuehrt die
/// unveränderte Simulation als Realzeitlauf ueber eine vereinbarte
/// Wanduhrdauer aus und zeichnet rohe Fensterstichproben des Arbeitssatzes,
/// Fortschrittsluecken, GC-Pausen und Warm-tick-Allokationen auf. Der Lauf
/// ist rein diagnostische Spike-Arbeit: Er entscheidet keine Grenzwerte und
/// ist niemals NF-002-Evidenz; die Schwellwertableitung geschieht
/// dokumentiert im versionierten Soakvertrag.
/// </summary>
internal static class SoakCalibrationRunner
{
    public const string CommandName = "./scripts/rift.sh soak --scenario soak-calibration";
    public const int DefaultCalibrationSeconds = 1800;
    public const int DefaultWindowSeconds = 30;

    /// <summary>Generoeses Spike-Watchdogfenster (Obergrenze des späteren
    /// Vertragsbands), damit Kalibrierlaeufe nicht spurlos abbrechen; der
    /// vertragliche Wert wird aus den Kalibrierdaten abgeleitet.</summary>
    internal const double SpikeWatchdogWindowSeconds = 300.0;

    public static int Run(CommandLineArgs arguments)
    {
        var reportPath = arguments.Option("--report");

        if (string.IsNullOrWhiteSpace(reportPath))
        {
            Console.Error.WriteLine("soak: --report PFAD ist erforderlich.");
            return ExitCodes.Usage;
        }

        var calibrationSeconds = (int)Math.Clamp(
            arguments.NumberOption("--calibration-seconds", DefaultCalibrationSeconds), 60, 7200);
        var windowSeconds = (int)Math.Clamp(
            arguments.NumberOption("--window-seconds", DefaultWindowSeconds), 10, 300);
        var seed = unchecked((uint)arguments.NumberOption("--seed", SoakContractDefaults.SpikeSeed));

        var environment = SystemInfo.Capture();
        var processStart = ProcessStartUtc();
        var commit = BenchEnvironment.CommitId();
        var buildMode = BenchEnvironment.BuildMode();

        IReadOnlyList<ToolchainPin> pins;

        try
        {
            pins = ToolchainLockReader.ReadNativeComponents(arguments.Option("--lock") ?? "toolchain.lock.json");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"soak: Toolchain-Lock nicht lesbar: {exception.Message}");
            return ExitCodes.Map(PlatformErrorCode.ArtifactManifestInvalid);
        }

        var options = new SoakEngineOptions(
            Seed: seed,
            TotalTicks: (long)calibrationSeconds * Riftward.Simulation.SimulationContract.TickRateHz,
            Paced: true,
            WarmupTicks: SoakPlan.WarmupTicks,
            WindowSeconds: windowSeconds,
            WatchdogWindowSeconds: SpikeWatchdogWindowSeconds,
            HashSampleIntervalTicks: SoakPlan.HashSampleIntervalTicks,
            StrictAllocationVerificationTicks: SoakContract.StrictAllocationVerificationTicks);

        SoakExecutionResult result;

        try
        {
            result = SoakEngine.Run(options, new RealtimePacingClock());
        }
        catch (PlatformException exception)
        {
            Console.Error.WriteLine(exception.Error.ToString());
            return ExitCodes.Map(exception.Error.Code);
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"soak: Kalibrierlauf vorzeitig beendet ({exception.GetType().Name}); kein Report.");
            return ExitCodes.Map(PlatformErrorCode.Internal);
        }

        // Analyse nach dem Lauf; darf allozieren.
        var closedWindows = result.Series.Count;
        var rssSeries = new long[closedWindows];

        for (var index = 0; index < closedWindows; index++)
        {
            rssSeries[index] = result.Series.RssKiB[index];
        }

        var noise = SoakMemoryAnalysis.Analyse(rssSeries);

        var reportJson = JsonSerializer.Serialize(new
        {
            schemaVersion = 1,
            mode = "soak",
            command = $"{CommandName} --report <PFAD>",
            scenario = new
            {
                id = SoakScenarios.Calibration,
                seed,
                calibrationSeconds,
                windowSeconds,
                tickRateHz = Riftward.Simulation.SimulationContract.TickRateHz,
                agentCount = result.World.AgentCount,
                worldId = Riftward.Simulation.SimulationContract.WorldId,
                content = "synthetic-graybox-movement-world",
            },
            spike = new
            {
                purpose = "abschnitt-0-kalibrierung-threshold-derivation-only",
                isEvidenceForNf002 = false,
                watchdogWindowSeconds = SpikeWatchdogWindowSeconds,
            },
            commandPlan = new
            {
                algorithm = Riftward.Simulation.SimulationContract.CommandPlanAlgorithmId,
                commands = result.CommandCount,
                hash = result.PlanHashHex,
            },
            startedAtUtc = processStart,
            finishedAtUtc = DateTime.UtcNow,
            environment = new
            {
                os = new { type = environment.OsType, kernelRelease = environment.KernelRelease },
                cpu = new { model = environment.CpuModel },
                rid = BenchEnvironment.Rid(),
                commit,
                buildMode,
                pins = pins.Select(pin => new Dictionary<string, string>
                {
                    ["id"] = pin.Id,
                    ["refType"] = pin.RefType,
                    ["ref"] = pin.Ref,
                    ["commit"] = pin.Commit,
                    ["sourceSha256"] = pin.SourceSha256,
                    ["licenseSpdx"] = pin.LicenseSpdx,
                }),
            },
            execution = new
            {
                paced = true,
                wallDurationSeconds = Math.Round(result.WallSeconds, 3),
                ticksExecuted = result.MeasuredTicksExecuted + SoakPlan.WarmupTicks,
                warmupTicks = SoakPlan.WarmupTicks,
            },
            result = new
            {
                workingSetKiB = result.RssMeasured
                    ? (object)new
                    {
                        measured = true,
                        unit = "KiB",
                        method = "proc-self-status-vmrss-window-samples",
                        first = result.RssFirstKiB,
                        min = result.RssMinKiB,
                        max = result.RssMaxKiB,
                        end = result.RssEndKiB,
                    }
                    : new { measured = false, reason = result.RssReason ?? "rss-sampler-unavailable" },
                windowMeanRssKiB = rssSeries,
                noise = new
                {
                    unit = "KiB",
                    method = "least-squares-linear-trend-residuals",
                    swing = noise.SwingKiB,
                    maxAbsResidual = Math.Round(noise.MaxAbsResidualKiB, 3),
                    medianAbsResidual = Math.Round(noise.MedianAbsResidualKiB, 3),
                    slopePerWindow = Math.Round(noise.SlopeKiBPerWindow, 6),
                },
                watchdog = new
                {
                    unit = "seconds",
                    method = "progress-watchdog-tick-index-window",
                    checks = result.Watchdog.Observations,
                    maxObservedProgressGapSeconds = Math.Round(result.Watchdog.MaxObservedProgressGapSeconds, 3),
                    stalled = result.StallDetected,
                },
                managedAllocationsBytes = new
                {
                    unit = "bytes",
                    method = "gc-total-allocated-bytes-precise-window-delta-sum-over-warm-ticks",
                    perWarmTick = Math.Round(result.AllocationsPerWarmTickBytes, 6),
                },
                gcPauseSumMs = new
                {
                    unit = "ms",
                    method = "gc-get-total-pause-duration-delta",
                    value = Math.Round(result.GcPauseSumMs, 3),
                },
                gcPauseCount = new
                {
                    unit = "count",
                    method = "gc-collection-count-gen0-to2-delta",
                    value = result.GcPauseCount,
                },
            },
            exitCode = 0,
        }, BenchRunner.ReportJsonOptions) + "\n";

        if (!BenchRunner.WriteReportOrDiagnose(reportPath!, reportJson))
        {
            return ExitCodes.Map(PlatformErrorCode.ReportNotWritable);
        }

        return ExitCodes.Ok;
    }

    private static DateTime ProcessStartUtc() =>
        System.Diagnostics.Process.GetCurrentProcess().StartTime.ToUniversalTime();
}

/// <summary>Nur fuer den Abschnitt-0-Spike verwendete Konstantwerte.</summary>
internal static class SoakContractDefaults
{
    /// <summary>Standardseed der Baseline (Praezedenz T-020/T-021).</summary>
    public const uint SpikeSeed = CameraFlight.DefaultSeed;
}
