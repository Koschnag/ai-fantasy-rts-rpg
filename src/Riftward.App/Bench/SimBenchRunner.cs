using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.Json;
using Riftward.App.Bench;
using Riftward.Platform;
using Riftward.Simulation;

namespace Riftward.App;

/// <summary>
/// BENCH-SIM (T-021): deterministische headless Simulationsbaseline mit
/// festem 20-Hz-Tick und genau 250 gleichzeitig vollstaendig simulierten
/// mobilen Testagenten, rein CPU-seitig nativ auf linux-x64 im bestehenden
/// Host. Kein Fenster, kein Renderer, kein Netzwerkzugriff; der Report ist
/// die pruefbare Evidenz (NF-007/Evidenzvertrag), ein fail-closed Budgetgate
/// entscheidet ausschliesslich gegen docs/PERFORMANCE_BUDGET.md und den in
/// docs/SIMULATIONSVERTRAG.md fixierten Abschnitt-0-Werten. Budgetverletzungen
/// ergeben einen definierten Exitcode und schreiben den Report trotzdem.
/// Laeufe auf dem Entwickler-PC sind diagnostische Baseline gemaess dem
/// Q-OPS-001-Klaerungsprotokoll; Pflichtprofile bleiben ohne benannte
/// Referenzhardware NOT-MEASURED und werden eskaliert statt ersetzt.
/// </summary>
internal static class SimBenchRunner
{
    public const string CommandName = "./scripts/rift.sh bench --scenario bench-sim";

    public const int DefaultWarmupTicks = 480;
    public const int DefaultSampleTicks = 1200;
    public const int RssSampleIntervalTicks = 60;
    public const int HashSampleIntervalTicks = 60;

    public static int Run(CommandLineArgs arguments)
    {
        var reportPath = arguments.Option("--report");

        if (string.IsNullOrWhiteSpace(reportPath))
        {
            Console.Error.WriteLine("bench: --report PFAD ist erforderlich.");
            return ExitCodes.Usage;
        }

        var seedValue = arguments.NumberOption("--seed", BenchRunnerDefaultSeed);
        var warmupTicks = (int)Math.Clamp(arguments.NumberOption("--warmup-ticks", DefaultWarmupTicks), 30, 20_000);
        var sampleTicks = (int)Math.Clamp(arguments.NumberOption("--sample-ticks", DefaultSampleTicks), 60, 50_000);
        var claimedBindings = BenchRunner.ParseProfileBindings(arguments);
        var seed = unchecked((uint)seedValue);

        if (warmupTicks + sampleTicks <= CommandPlan.FirstCommandTick)
        {
            Console.Error.WriteLine(
                "bench: Warm-up plus Messsticks muessen hinter dem ersten Planbefehl liegen "
                + $"(Tick {CommandPlan.FirstCommandTick}).");
            return ExitCodes.Usage;
        }

        var environment = SystemInfo.Capture();
        var processStart = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        var commit = BenchEnvironment.CommitId();
        var buildMode = BenchEnvironment.BuildMode();

        IReadOnlyList<ToolchainPin> pins;

        try
        {
            pins = ToolchainLockReader.ReadNativeComponents(arguments.Option("--lock") ?? "toolchain.lock.json");
        }
        catch (IOException exception)
        {
            Console.Error.WriteLine($"bench: Toolchain-Lock nicht lesbar: {exception.Message}");
            return ExitCodes.Map(PlatformErrorCode.ArtifactManifestInvalid);
        }
        catch (UnauthorizedAccessException exception)
        {
            Console.Error.WriteLine($"bench: Toolchain-Lock nicht lesbar: {exception.Message}");
            return ExitCodes.Map(PlatformErrorCode.ArtifactManifestInvalid);
        }

        // Abschnitt 0 ist gatend vor dem Heisspfad abgeschlossen; Welt,
        // Plan und Vertragskennungen entstehen aus dem versionierten Vertrag.
        var world = new SimWorld(seed);
        var plan = CommandPlan.Generate(seed, warmupTicks + sampleTicks);
        var planHash = CommandPlan.Hash(plan);
        var startStateHash = world.ComputeStateHash();

        var planIndex = 0;

        void ApplyDueCommands()
        {
            var firstDue = planIndex;
            var tick = world.TickIndex;

            while (planIndex < plan.Length && plan[planIndex].Tick <= tick)
            {
                planIndex++;
            }

            if (planIndex > firstDue)
            {
                world.ApplyCommands(plan.AsSpan(firstDue, planIndex - firstDue));
            }
        }

        for (var tick = 0; tick < warmupTicks; tick++)
        {
            ApplyDueCommands();
            world.Tick();
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var pauseSumBefore = GC.GetTotalPauseDuration();
        var collectionCountBefore = TotalCollectionCount();

        var tickTimes = new double[sampleTicks];
        long allocationSumBytes = 0;
        var hashSampleCount = (sampleTicks / HashSampleIntervalTicks) + 1;
        var intervalHashes = new ulong[hashSampleCount];
        var intervalSampleTicks = new long[hashSampleCount];
        intervalHashes[0] = startStateHash;
        intervalSampleTicks[0] = world.TickIndex;
        var hashCursor = 1;

        using var rssSampler = RssSampler.TryCreate();

        for (var index = 0; index < sampleTicks; index++)
        {
            ApplyDueCommands();

            var allocationBefore = GC.GetTotalAllocatedBytes(precise: true);
            var startTimestamp = Stopwatch.GetTimestamp();
            world.Tick();
            var endTimestamp = Stopwatch.GetTimestamp();
            var allocationAfter = GC.GetTotalAllocatedBytes(precise: true);

            tickTimes[index] = Measurement.TimestampDeltaToMilliseconds(startTimestamp, endTimestamp);
            allocationSumBytes += allocationAfter - allocationBefore;

            if (index % HashSampleIntervalTicks == HashSampleIntervalTicks - 1 && hashCursor < hashSampleCount)
            {
                intervalHashes[hashCursor] = world.ComputeStateHash();
                intervalSampleTicks[hashCursor] = world.TickIndex;
                hashCursor++;
            }

            if (rssSampler is not null && index % RssSampleIntervalTicks == RssSampleIntervalTicks - 1)
            {
                rssSampler.Sample();
            }
        }

        var pauseSumAfter = GC.GetTotalPauseDuration();
        var gcPauseCount = TotalCollectionCount() - collectionCountBefore;
        var gcPauseSumMs = (pauseSumAfter - pauseSumBefore).TotalMilliseconds;

        var endStateHash = world.ComputeStateHash();
        var band = TelemetryMath.Band(tickTimes);
        var allocationsPerWarmTick = allocationSumBytes / (double)sampleTicks;

        var verdict = SimBudgetGate.Evaluate(SimBudgetLimits.Documented, new SimBudgetInputs(
            P99TickTimeMs: band.P99Ms,
            ManagedAllocationsPerWarmTickBytes: allocationsPerWarmTick));

        var gateExitCode = verdict.Pass ? ExitCodes.Ok : ExitCodes.Map(PlatformErrorCode.BenchBudgetViolated);
        var limits = SimBudgetLimits.Documented;
        var workingSet = rssSampler?.Snapshot() ?? default;

        var reportJson = JsonSerializer.Serialize(new
        {
            schemaVersion = SimReportSchema.CurrentVersion,
            mode = BenchReportSchema.ModeBench,
            command = $"{CommandName} --report <PFAD>",
            scenario = new
            {
                id = BenchScenarios.Sim,
                seed,
                tickRateHz = SimulationContract.TickRateHz,
                agentCount = world.AgentCount,
                worldId = SimulationContract.WorldId,
                content = "synthetic-graybox-movement-world",
            },
            simulationContract = new
            {
                document = SimulationContract.DocumentPath,
                version = SimulationContract.ContractVersion,
                numericModel = SimulationContract.NumericModelId,
                hashAlgorithm = SimulationContract.HashAlgorithmId,
                commandPlanAlgorithm = SimulationContract.CommandPlanAlgorithmId,
                allocationLimitBytesPerWarmTick = limits.AllocationsPerWarmTickLimitBytes,
            },
            commandPlan = new
            {
                algorithm = SimulationContract.CommandPlanAlgorithmId,
                commands = plan.Length,
                hash = FormatHash(planHash),
                firstCommand = new
                {
                    tick = plan[0].Tick,
                    scopeGroup = (int)plan[0].ScopeGroup,
                    kind = plan[0].Kind.ToString(),
                    zoneIndex = plan[0].ZoneIndex,
                },
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
            measurement = new
            {
                warmupTicks,
                sampleTicks,
                ticksExecuted = warmupTicks + sampleTicks,
                rssSampleIntervalTicks = (long)RssSampleIntervalTicks,
                hashSampleIntervalTicks = (long)HashSampleIntervalTicks,
            },
            metrics = new
            {
                tickTimeMs = new
                {
                    unit = "ms",
                    method = "stopwatch-tick-delta",
                    p50 = Math.Round(band.P50Ms, 3),
                    p95 = Math.Round(band.P95Ms, 3),
                    p99 = Math.Round(band.P99Ms, 3),
                },
                managedAllocationsBytes = new
                {
                    unit = "bytes",
                    method = "gc-total-allocated-bytes-precise-delta-per-tick-sum",
                    perWarmTick = Math.Round(allocationsPerWarmTick, 3),
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
                activeAgents = new
                {
                    unit = "count",
                    method = "soa-agent-count-fixed",
                    value = world.AgentCount,
                },
                stateHashChain = new
                {
                    unit = "hex64",
                    method = SimulationContract.HashAlgorithmId,
                    start = FormatHash(startStateHash),
                    intervalSampleTicks = intervalSampleTicks.Take(hashCursor),
                    intervalHashes = intervalHashes.Take(hashCursor).Select(FormatHash),
                    end = FormatHash(endStateHash),
                },
                workingSetKiB = workingSet.Measured
                    ? (object)new
                    {
                        measured = true,
                        unit = "KiB",
                        method = "proc-self-status-vmrss-samples",
                        min = workingSet.MinKiB!.Value,
                        max = workingSet.MaxKiB!.Value,
                        end = workingSet.EndKiB!.Value,
                    }
                    : new
                    {
                        measured = false,
                        reason = workingSet.Reason ?? "rss-sampler-unavailable",
                    },
                drawSubmitCallsPerFrame = HeadlessUnavailable("headless-cpu-scenario-no-renderer"),
                visibleTrianglesPerFrame = HeadlessUnavailable("headless-cpu-scenario-no-renderer"),
                gpuTimeMs = HeadlessUnavailable("headless-cpu-scenario-no-renderer"),
            },
            gate = new
            {
                limits = new
                {
                    p99TickTimeHardLimitMs = limits.P99TickTimeHardLimitMs,
                    p99TickTimeTargetMs = limits.P99TickTimeTargetMs,
                    allocationsPerWarmTickBytesMax = limits.AllocationsPerWarmTickLimitBytes,
                },
                pass = verdict.Pass,
                p99TargetMet = verdict.P99TargetMet,
                violations = verdict.Violations,
            },
            profiles = ProfileBinding.MandatoryWithoutReferenceHardware()
                .Concat(claimedBindings.Select(binding => ProfileBinding.EvaluateClaim(
                    binding.ProfileId,
                    new HardwareDescriptor(binding.ClaimedClass, environment.CpuModel, IsDeveloperWorkstation: true),
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
        }, BenchRunner.ReportJsonOptions) + "\n";

        // Selbstpruefung gegen den Evidenzvertrag, bevor der Report gilt
        // (AC-T021-04): ein Vertragsverstoß wird nie still durchgereicht.
        var schemaErrors = SimReportSchema.Validate(reportJson);

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

        return gateExitCode;
    }

    private const uint BenchRunnerDefaultSeed = CameraFlight.DefaultSeed;

    private static object HeadlessUnavailable(string reason) => new { measured = false, reason };

    private static string FormatHash(ulong hash) => hash.ToString(SimReportSchema.HashFormat, CultureInfo.InvariantCulture);

    private static long TotalCollectionCount() =>
        GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);

    /// <summary>
    /// Allokationsarmer Working-Set-Stichprobennehmer: persistent geöffneter
    /// Dateihandle auf /proc/self/status mit wiederverwendetem Bytepuffer;
    /// Stichproben waehrend der Messphase verursachen keine verwaltete
    /// Allokation und faelschen damit das Tickallokationsbudget nicht.
    /// </summary>
    private sealed class RssSampler : IDisposable
    {
        private const string StatusPath = "/proc/self/status";
        private const string Marker = "VmRSS:";

        private readonly FileStream? _stream;
        private readonly byte[] _buffer = new byte[8192];

        private RssSampler(FileStream? stream) => _stream = stream;

        public bool Measured { get; private set; }

        public long? MinKiB { get; private set; }

        public long? MaxKiB { get; private set; }

        public long? EndKiB { get; private set; }

        public string? Reason { get; private set; }

        public static RssSampler? TryCreate()
        {
            try
            {
                return new RssSampler(
                    new FileStream(StatusPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
            }
            catch (FileNotFoundException)
            {
                return Unavailable("proc-self-status-unavailable");
            }
            catch (IOException)
            {
                return Unavailable("proc-self-status-unavailable");
            }
            catch (UnauthorizedAccessException)
            {
                return Unavailable("proc-self-status-forbidden");
            }
        }

        private static RssSampler Unavailable(string reason) => new(null)
        {
            Reason = reason,
        };

        public void Sample()
        {
            if (Reason is not null || _stream is null)
            {
                return;
            }

            try
            {
                _stream.Seek(0, SeekOrigin.Begin);
                var length = _stream.Read(_buffer, 0, _buffer.Length);
                var value = ParseVmRssKiB(_buffer.AsSpan(0, length));

                if (value is not { } kiB)
                {
                    Reason ??= "vmrss-line-missing";
                    return;
                }

                Measured = true;
                MinKiB = MinKiB is { } minimum ? Math.Min(minimum, kiB) : kiB;
                MaxKiB = MaxKiB is { } maximum ? Math.Max(maximum, kiB) : kiB;
                EndKiB = kiB;
            }
            catch (IOException exception)
            {
                Reason ??= $"proc-read-failed:{exception.GetType().Name}";
            }
        }

        internal (bool Measured, long? MinKiB, long? MaxKiB, long? EndKiB, string? Reason) Snapshot() =>
            (Measured, MinKiB, MaxKiB, EndKiB, Reason);

        /// <summary>Parst die VmRSS-Zeile ohne Stringallokation direkt aus Bytes.</summary>
        private static long? ParseVmRssKiB(ReadOnlySpan<byte> source)
        {
            var marker = Marker;

            for (var index = 0; index <= source.Length - marker.Length; index++)
            {
                var matches = true;

                for (var offset = 0; offset < marker.Length; offset++)
                {
                    if ((char)source[index + offset] != marker[offset])
                    {
                        matches = false;
                        break;
                    }
                }

                if (!matches)
                {
                    continue;
                }

                var cursor = index + marker.Length;
                long value = 0;
                var digits = 0;

                while (cursor < source.Length)
                {
                    var character = source[cursor];

                    if (character is >= (byte)'0' and <= (byte)'9')
                    {
                        value = (value * 10) + (character - (byte)'0');
                        digits++;
                        cursor++;
                        continue;
                    }

                    if (digits > 0)
                    {
                        break;
                    }

                    cursor++;
                }

                return digits > 0 && digits <= 12 ? value : null;
            }

            return null;
        }

        public void Dispose() => _stream?.Dispose();
    }
}
