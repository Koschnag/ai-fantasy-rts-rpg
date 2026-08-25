using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using Riftward.App.Bench;
using Riftward.Platform;
using Riftward.Simulation;

namespace Riftward.App.Soak;

/// <summary>
/// soak-replay (T-022): deterministischer Replay-Soak nativ auf linux-x64 im
/// bestehenden Host, rein CPU-seitig ohne Fenster, Renderer und Netzwerk.
/// Evidenzmodell Soakvertrag V2 (Projektleitungsentscheidung 2026-08-25):
/// NF-002 wird durch mindestens drei unabhaengige Fresh-Prozess-Laueufe ueber
/// den kompletten skriptierten Planhorizont des Simulationsvertrags V1
/// nachgewiesen (beschleunigte Taktung zulässig, Pacing-Unabhängigkeit durch
/// Test belegt); jeder Lauf entscheidet fail-closed nur gegen die absoluten
/// Grenzwerte des Soakvertrags und ist je Report als Evidenzeinheit markiert.
/// Das Restrisiko des nicht nachgewiesenen zusammenhaengenden
/// Achtstunden-Echtzeitbetriebs ist vertraglich ausgewiesen; Budgetverletzungen
/// ergeben einen definierten Exitcode und schreiben den Report trotzdem.
/// Laeufe auf dem Entwickler-PC sind diagnostische Baseline gemaess Q-OPS-001;
/// Pflichtprofile bleiben NOT-MEASURED.
/// </summary>
internal static class SoakReplayRunner
{
    public const string CommandName = "./scripts/rift.sh soak --scenario soak-replay";

    public static int Run(CommandLineArgs arguments)
    {
        var reportPath = arguments.Option("--report");

        if (string.IsNullOrWhiteSpace(reportPath))
        {
            Console.Error.WriteLine("soak: --report PFAD ist erforderlich.");
            return ExitCodes.Usage;
        }

        var accelerated = arguments.HasFlag("--diagnostic-accelerated");
        var referenceOut = arguments.Option("--reference-out");
        var horizonOption = arguments.Option("--horizon-ticks");

        if (!accelerated && horizonOption is not null)
        {
            Console.Error.WriteLine(
                "soak: --horizon-ticks ist nur zusammen mit --diagnostic-accelerated erlaubt; der autoritative Horizont ist unverrueckbar 576000 Ticks.");
            return ExitCodes.Usage;
        }

        if (!accelerated && referenceOut is not null)
        {
            Console.Error.WriteLine(
                "soak: --reference-out ist eine Diagnosefunktion des beschleunigten Modus.");
            return ExitCodes.Usage;
        }

        var horizonTicks = SoakPlan.AuthoritativeTickCount;

        if (accelerated)
        {
            var requested = arguments.NumberOption("--horizon-ticks", SoakPlan.AuthoritativeTickCount);

            if (requested < SoakContract.MinAcceleratedHorizonTicks || requested > SoakPlan.AuthoritativeTickCount)
            {
                Console.Error.WriteLine(
                    $"soak: --horizon-ticks muss zwischen {SoakContract.MinAcceleratedHorizonTicks} "
                    + $"und {SoakPlan.AuthoritativeTickCount} liegen (erhalten {requested}).");
                return ExitCodes.Usage;
            }

            horizonTicks = requested;
        }

        var seed = unchecked((uint)arguments.NumberOption("--seed", SoakContract.DefaultSeed));
        var claimedBindings = BenchRunner.ParseProfileBindings(arguments);

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

        // Golden-Fixture fail-closed laden: fehlende oder beschädigte
        // Fixtures machen einen vertragskonformen Lauf unmoeglich; es wird
        // kontrolliert abgebrochen, ohne einen Report vorzutaeuschen.
        // Ausnahme: ein beschleunigter Referenzlauf mit --reference-out darf
        // die Fixture erzeugen (Emissionsmodus, rein diagnostisch).
        SoakChainFixture.Loaded? fixture;
        var emissionMode = accelerated && referenceOut is not null;

        try
        {
            if (emissionMode)
            {
                // Referenzemission: die Fixture entsteht aus genau diesem Lauf.
                fixture = null!;
            }
            else
            {
                // Bewusst ohne Pfad-Override: Der vertragsmassgebliche Vergleich
                // erfolgt ausschliesslich gegen die versionierte Fixture im
                // Repository beziehungsweise Hostausgabeverzeichnis.
                var fixturePath = SoakChainFixture.ResolvePath(null)
                    ?? throw new FileNotFoundException(
                        $"Soak-Golden-Fixture nicht gefunden ({SoakChainFixture.RepositoryPath}); Referenzlauf erforderlich.");

                fixture = SoakChainFixture.Load(fixturePath);
            }
        }
        catch (Exception exception) when (exception is IOException or FormatException or FileNotFoundException)
        {
            Console.Error.WriteLine($"soak: {exception.Message}");
            return ExitCodes.Map(PlatformErrorCode.TelemetryInvalid);
        }

        if (!emissionMode && fixture!.Seed != seed)
        {
            Console.Error.WriteLine(
                $"soak: Seed {seed} widerspricht der Golden-Fixture ({fixture.Seed}); Fremdseed-Laueufe sind als Negativfall erwartet fehlzuschlagen.");
            // Bewusst weiterfahren: Die Hashabweichung wird als
            // Gateverletzung mit Report belegt (Negativnachweis).
        }

        var environment = SystemInfo.Capture();
        var processStart = Process.GetCurrentProcess().StartTime.ToUniversalTime();
        var commit = BenchEnvironment.CommitId();
        var buildMode = BenchEnvironment.BuildMode();

        var options = new SoakEngineOptions(
            Seed: seed,
            TotalTicks: horizonTicks,
            Paced: !accelerated,
            WarmupTicks: SoakPlan.WarmupTicks,
            WindowSeconds: SoakContract.WindowSeconds,
            WatchdogWindowSeconds: SoakContract.WatchdogWindowSeconds,
            HashSampleIntervalTicks: SoakPlan.HashSampleIntervalTicks,
            StrictAllocationVerificationTicks: SoakContract.StrictAllocationVerificationTicks);

        SoakExecutionResult? result = null;
        string? abortReason = null;

        try
        {
            result = SoakEngine.Run(options, new RealtimePacingClock());
        }
        catch (PlatformException exception)
        {
            abortReason = $"platform-error:{exception.Error.Code}";
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch (Exception exception)
        {
            // Kontrollierter Abbruch ohne Prozessabsturz: Teilreport als
            // ausdruecklich keine Evidenz markieren.
            abortReason = $"early-abort:{exception.GetType().Name}";
        }

        var finishedAtUtc = DateTime.UtcNow;

        if (result is null)
        {
            WritePartialReport(reportPath!, CommandName, seed, horizonTicks + SoakPlan.WarmupTicks, accelerated, commit, buildMode, abortReason!);
            return ExitCodes.Map(PlatformErrorCode.SoakRunIncomplete);
        }

        var run = result;

        // Kettenvergleich gegen die Golden-Fixture (byteidentisch je Stichprobe);
        // im Emissionsmodus entsteht die Fixture aus genau diesem Lauf.
        var samplesMatched = 0;
        var sampleMismatches = 0;
        var sampleSkipped = 0;

        if (!emissionMode)
        {
            for (var index = 0; index < run.ChainSampleCount; index++)
            {
                var tick = run.ChainSampleTicks[index];
                var hash = run.ChainSampleHashes[index];
                var fixtureIndex = FindSampleIndex(fixture!, tick);

                if (fixtureIndex < 0)
                {
                    sampleSkipped++;
                    continue;
                }

                if (fixture.Samples[fixtureIndex].Hash == hash)
                {
                    samplesMatched++;
                }
                else
                {
                    sampleMismatches++;
                }
            }
        }

        var fullFixtureComparison = sampleMismatches == 0
            && sampleSkipped == 0
            && samplesMatched == run.ChainSampleCount
            && fixture?.Samples.Count == run.ChainSampleCount
            && string.Equals(fixture.PlanHashHex, run.PlanHashHex, StringComparison.Ordinal);
        var diagnosticPrefixComparison = sampleMismatches == 0 && samplesMatched >= 1;
        var chainMatched = emissionMode
            || (horizonTicks == SoakPlan.AuthoritativeTickCount
                ? fullFixtureComparison
                : diagnosticPrefixComparison);

        // Speicherkennzahlen aus der Fensterserie (Vertrag Abschnitt 1).
        var absoluteGrowthKiB = run.RssMeasured ? run.RssEndKiB - run.RssFirstKiB : double.NaN;
        var absoluteGrowthMiB = run.RssMeasured ? absoluteGrowthKiB / 1024.0 : double.NaN;

        var closedValues = new long[run.Series.Count];

        for (var index = 0; index < run.Series.Count; index++)
        {
            closedValues[index] = run.Series.RssKiB[index];
        }

        var trendDeltaKiBPerHour = run.RssMeasured && closedValues.Length >= 3
            ? (SoakMemoryAnalysis.ThirdSlope(closedValues, 2) - SoakMemoryAnalysis.ThirdSlope(closedValues, 0))
                * (3600.0 / SoakContract.WindowSeconds)
            : double.NaN;

        // Vollstaendigkeit und Evidenzeinheiten-Kennung (Soakvertrag V2,
        // Projektleitungsentscheidung 2026-08-25): Evidenz entsteht aus
        // reproduzierten, vollständigen Wiederholungslaeufen ueber den
        // kompletten Planhorizont, nicht aus der Wanduhrdauer eines
        // Einzelprozesses.
        var ticksComplete = run.MeasuredTicksExecuted == horizonTicks;
        var wallOk = !options.Paced || run.WallSeconds >= SoakPlan.RequiredWallSeconds;
        var complete = ticksComplete && wallOk && !run.StallDetected;
        string? incompleteReason = null;

        if (!complete)
        {
            incompleteReason =
                run.StallDetected ? "watchdog-stall"
                : !ticksComplete ? "ticks-incomplete"
                : "wall-duration-below-contract";
        }

        var fullHorizon = horizonTicks == SoakPlan.AuthoritativeTickCount;
        var releaseBuild = string.Equals(buildMode, "Release", StringComparison.OrdinalIgnoreCase);
        var executionModeId = emissionMode
            ? SoakContract.ReferenceEmissionDiagnosticModeId
            : !accelerated
                ? SoakContract.RealtimeAuthoritativeModeId
                : fullHorizon
                    ? SoakContract.AcceleratedEvidenceModeId
                    : SoakContract.AcceleratedDiagnosticModeId;

        // Fail-closed Gateentscheidung ausschliesslich gegen Vertragswerte.
        var limits = SoakBudgetLimits.Documented;

        if (!SoakGate.TrendIsConsistent(limits))
        {
            throw new InvalidOperationException(
                "Soakvertrag: Konsistenzbedingung Trendschwelle mal 8 h kleiner gleich absoluter Schwelle verletzt.");
        }

        var verdict = SoakGate.Evaluate(limits, new SoakGateInputs(
            RssMeasured: run.RssMeasured,
            AbsoluteGrowthMiB: absoluteGrowthMiB,
            TrendDeltaKiBPerHour: trendDeltaKiBPerHour,
            StrictAllocationsPerTickBytes: strictBytes(run),
            ChainMatched: chainMatched,
            StallDetected: run.StallDetected,
            Paced: options.Paced,
            WallSeconds: run.WallSeconds,
            TicksExecuted: run.MeasuredTicksExecuted,
            RequiredTicks: horizonTicks,
            RequiredWallSeconds: SoakPlan.RequiredWallSeconds));

        // Evidenzeinheiten-Kennung (Soakvertrag V2, Projektleitungs-
        // entscheidung 2026-08-25): Evidenz entsteht aus reproduzierten,
        // vollständigen Wiederholungslaeufen ueber den kompletten
        // Planhorizont in Release-naher Konfiguration, nicht aus der
        // Wanduhrdauer eines Einzelprozesses.
        var evidenceDecision = SoakEvidenceUnit.Decide(
            executionModeId,
            fullHorizon,
            complete,
            releaseBuild,
            goldenFixtureCompared: !emissionMode,
            chainMatched,
            verdict.Pass,
            incompleteReason);
        var evidenceUnit = evidenceDecision.IsUnit;
        var evidenceReason = evidenceDecision.Reason;

        // Beschleunigter Referenzlauf darf die Golden-Fixture erneuern
        // (unabhaengiger Referenzlauf ueber den identischen Plan).
        string? emittedFixtureSha256 = null;

        if (referenceOut is not null)
        {
            emittedFixtureSha256 = WriteReferenceFixture(referenceOut, seed, horizonTicks, run.PlanHashHex, run);
        }

        var goldenFixtureBlock = emissionMode
            ? (object)new
            {
                emitted = true,
                path = referenceOut ?? string.Empty,
                sha256 = emittedFixtureSha256 ?? string.Empty,
                schemaId = SoakChainFixture.Kind,
                sampleCount = run.ChainSampleCount,
                note = "emission-mode-reference-run-diagnostic-only",
            }
            : new
            {
                emitted = false,
                path = fixture!.FilePath,
                sha256 = fixture.Sha256,
                schemaId = SoakChainFixture.Kind,
                sampleCount = fixture.Samples.Count,
                samplesMatched,
                sampleMismatches,
                sampleSkipped,
                matched = chainMatched,
            };

        var gateExitCode = SoakExitMapping.Map(run.StallDetected, complete, verdict.Pass);

        var driftBeginP50 = SoakWindowSeries.ThirdPercentile(run.Series.WindowP50Ms, 0, 0.50);
        var driftMiddleP99 = SoakWindowSeries.ThirdPercentile(run.Series.WindowP99Ms, 1, 0.99);
        var driftEndP95 = SoakWindowSeries.ThirdPercentile(run.Series.WindowP95Ms, 2, 0.95);

        var firstCommand = FirstCommandOf(seed, horizonTicks);

        var reportJson = JsonSerializer.Serialize(new
        {
            schemaVersion = SoakReportSchema.CurrentVersion,
            mode = SoakReportSchema.ModeSoak,
            command = $"{CommandName} --report <PFAD>",
            scenario = new
            {
                id = SoakScenarios.Replay,
                seed,
                tickRateHz = SimulationContract.TickRateHz,
                agentCount = run.World.AgentCount,
                worldId = SimulationContract.WorldId,
                content = "synthetic-graybox-movement-world",
                executionModeId,
            },
            reliabilityContract = new
            {
                document = SoakContract.DocumentPath,
                version = SoakContract.ContractVersion,
                simulationContractDocument = SimulationContract.DocumentPath,
                simulationContractVersion = SimulationContract.ContractVersion,
                hashAlgorithm = SimulationContract.HashAlgorithmId,
                commandPlanAlgorithm = SimulationContract.CommandPlanAlgorithmId,
                evidenceUnitId = SoakContract.EvidenceUnitId,
                minimumEvidenceRepetitions = SoakContract.MinimumEvidenceRepetitions,
                allocationLimitBytesPerWarmTick = limits.AllocationsPerWarmTickLimitBytes,
                absoluteGrowthLimitMiB = limits.AbsoluteGrowthLimitMiB,
                trendLimitKiBPerHour = limits.TrendLimitKiBPerHour,
                watchdogWindowSeconds = limits.WatchdogWindowSeconds,
                windowSeconds = (long)SoakContract.WindowSeconds,
                calibrationReference = SoakContract.CalibrationReference,
            },
            commandPlan = new
            {
                algorithm = SimulationContract.CommandPlanAlgorithmId,
                commands = run.CommandCount,
                hash = run.PlanHashHex,
                firstCommand,
            },
            startedAtUtc = processStart,
            finishedAtUtc,
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
                evidenceUnit,
                evidenceReason,
                paced = options.Paced,
                wallDurationSeconds = Math.Round(run.WallSeconds, 3),
                requiredWallSeconds = SoakPlan.RequiredWallSeconds,
                // Simulierte Ticks einschliesslich Warm-up; der gatende
                // Messhorizont ohne Warm-up steht im gate.limits-Abschnitt.
                ticksExecuted = run.MeasuredTicksExecuted + SoakPlan.WarmupTicks,
                requiredTicks = horizonTicks + SoakPlan.WarmupTicks,
                warmupTicks = (long)SoakPlan.WarmupTicks,
                complete,
                isEvidence = evidenceUnit,
                incompleteReason,
            },
            metrics = new
            {
                workingSetKiB = run.RssMeasured
                    ? (object)new
                    {
                        measured = true,
                        unit = "KiB",
                        method = "proc-self-status-vmrss-window-samples",
                        first = run.RssFirstKiB,
                        min = run.RssMinKiB,
                        max = run.RssMaxKiB,
                        end = run.RssEndKiB,
                        windowMeans = WindowMeans(run),
                    }
                    : new { measured = false, reason = run.RssReason ?? "rss-sampler-unavailable" },
                managedAllocationsPerWarmTick = new
                {
                    unit = "bytes",
                    method = "gc-total-allocated-bytes-precise-delta-per-tick-sum",
                    perWarmTick = Math.Round(strictBytes(run), 6),
                    verificationTicks = run.StrictVerificationTickCount,
                    bursts = run.StrictVerificationBurstCount,
                },
                managedAllocationWindowDeltasDiagnostic = new
                {
                    unit = "bytes",
                    method = "gc-total-allocated-bytes-precise-window-delta-sum-over-warm-ticks",
                    perWarmTick = Math.Round(run.AllocationsPerWarmTickBytes, 6),
                    gateCoupled = false,
                    windowDeltaBytes = run.Series.AllocationDeltaBytes.Take(run.Series.Count),
                },
                gcPauseSumMs = new
                {
                    unit = "ms",
                    method = "gc-get-total-pause-duration-delta",
                    value = Math.Round(run.GcPauseSumMs, 3),
                },
                gcPauseCount = new
                {
                    unit = "count",
                    method = "gc-collection-count-gen0-to2-delta",
                    value = run.GcPauseCount,
                },
                activeAgents = new
                {
                    unit = "count",
                    method = "soa-agent-count-fixed",
                    value = run.World.AgentCount,
                },
                stateHashChain = new
                {
                    unit = "hex64",
                    method = SimulationContract.HashAlgorithmId,
                    start = FormatHash(run.StartStateHash),
                    intervalSampleTicks = IntervalTicks(run),
                    intervalHashes = IntervalHashes(run).Select(FormatHash),
                    end = FormatHash(run.EndStateHash),
                },
                goldenFixture = goldenFixtureBlock,
                watchdog = new
                {
                    unit = "seconds",
                    method = "progress-watchdog-tick-index-window",
                    windowSeconds = limits.WatchdogWindowSeconds,
                    checks = run.Watchdog.Observations,
                    maxObservedProgressGapSeconds = Math.Round(run.Watchdog.MaxObservedProgressGapSeconds, 3),
                    stalled = run.StallDetected,
                },
                tickTimeDriftDiagnostic = new
                {
                    unit = "ms",
                    method = "stopwatch-tick-delta-per-window-percentile-aggregate",
                    gateCoupled = false,
                    beginP50Ms = Round(driftBeginP50),
                    beginP95Ms = Round(SoakWindowSeries.ThirdPercentile(run.Series.WindowP95Ms, 0, 0.95)),
                    beginP99Ms = Round(SoakWindowSeries.ThirdPercentile(run.Series.WindowP99Ms, 0, 0.99)),
                    middleP50Ms = Round(SoakWindowSeries.ThirdPercentile(run.Series.WindowP50Ms, 1, 0.50)),
                    middleP95Ms = Round(SoakWindowSeries.ThirdPercentile(run.Series.WindowP95Ms, 1, 0.95)),
                    middleP99Ms = Round(driftMiddleP99),
                    endP50Ms = Round(SoakWindowSeries.ThirdPercentile(run.Series.WindowP50Ms, 2, 0.50)),
                    endP95Ms = Round(driftEndP95),
                    endP99Ms = Round(SoakWindowSeries.ThirdPercentile(run.Series.WindowP99Ms, 2, 0.99)),
                },
                drawSubmitCallsPerFrame = HeadlessUnavailable("headless-cpu-scenario-no-renderer"),
                visibleTrianglesPerFrame = HeadlessUnavailable("headless-cpu-scenario-no-renderer"),
                gpuTimeMs = HeadlessUnavailable("headless-cpu-scenario-no-renderer"),
            },
            qtec010 = new
            {
                statement = SoakContract.Qtec010Statement,
            },
            gate = new
            {
                limits = new
                {
                    absoluteGrowthLimitMiB = limits.AbsoluteGrowthLimitMiB,
                    trendLimitKiBPerHour = limits.TrendLimitKiBPerHour,
                    allocationsPerWarmTickLimitBytes = limits.AllocationsPerWarmTickLimitBytes,
                    watchdogWindowSeconds = limits.WatchdogWindowSeconds,
                    requiredWallSeconds = SoakPlan.RequiredWallSeconds,
                    requiredTicks = horizonTicks,
                },
                violations = verdict.Violations,
                pass = verdict.Pass,
                complete,
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

        var schemaErrors = SoakReportSchema.Validate(reportJson);

        if (schemaErrors.Count > 0)
        {
            Console.Error.WriteLine($"soak: Report widerspricht dem Schemavertrag: {string.Join("; ", schemaErrors)}");
            BenchRunner.WriteReportOrDiagnose(reportPath!, reportJson);
            return ExitCodes.Map(PlatformErrorCode.TelemetryInvalid);
        }

        if (!BenchRunner.WriteReportOrDiagnose(reportPath!, reportJson))
        {
            return ExitCodes.Map(PlatformErrorCode.ReportNotWritable);
        }

        return gateExitCode;
    }

    private static double strictBytes(SoakExecutionResult run) =>
        run.StrictVerificationTickCount > 0 ? run.StrictAllocationsPerTickBytes : double.NaN;

    private static object HeadlessUnavailable(string reason) => new { measured = false, reason };

    private static IEnumerable<long> WindowMeans(SoakExecutionResult run)
    {
        for (var index = 0; index < run.Series.Count; index++)
        {
            yield return run.Series.RssKiB[index];
        }
    }

    private static double Round(double value) => Math.Round(value, 3, MidpointRounding.AwayFromZero);

    private static string FormatHash(ulong hash) => hash.ToString(SoakReportSchema.HashFormat, CultureInfo.InvariantCulture);

    private static IEnumerable<long> IntervalTicks(SoakExecutionResult run)
    {
        for (var index = 1; index < run.ChainSampleCount - 1; index++)
        {
            yield return run.ChainSampleTicks[index];
        }
    }

    private static IEnumerable<ulong> IntervalHashes(SoakExecutionResult run)
    {
        for (var index = 1; index < run.ChainSampleCount - 1; index++)
        {
            yield return run.ChainSampleHashes[index];
        }
    }

    private static int FindSampleIndex(SoakChainFixture.Loaded fixture, long tick)
    {
        var low = 0;
        var high = fixture.Samples.Count - 1;

        while (low <= high)
        {
            var mid = low + ((high - low) / 2);
            var midTick = fixture.Samples[mid].Tick;

            if (midTick == tick)
            {
                return mid;
            }

            if (midTick < tick)
            {
                low = mid + 1;
            }
            else
            {
                high = mid - 1;
            }
        }

        return -1;
    }

    private static object FirstCommandOf(uint seed, long horizonTicks)
    {
        var plan = CommandPlan.Generate(
            seed,
            checked((int)Math.Min(int.MaxValue - 1L, SoakPlan.WarmupTicks + horizonTicks)));
        var command = plan.Length > 0 ? plan[0] : default;
        return new
        {
            tick = (long)(plan.Length > 0 ? command.Tick : 0),
            scopeGroup = (int)command.ScopeGroup,
            kind = command.Kind.ToString(),
            zoneIndex = command.ZoneIndex,
        };
    }

    private static string? WriteReferenceFixture(string path, uint seed, long tickCount, string planHashHex, SoakExecutionResult run)
    {
        try
        {
            var model = new SoakChainFixture.FixtureModel
            {
                Seed = seed,
                TickCount = SoakPlan.WarmupTicks + tickCount,
                SampleIntervalTicks = SoakPlan.HashSampleIntervalTicks,
                PlanHashHex = planHashHex,
                Samples = Enumerable.Range(0, run.ChainSampleCount)
                    .Select(index => new SoakChainFixture.FixtureSample(
                        run.ChainSampleTicks[index],
                        FormatHash(run.ChainSampleHashes[index])))
                    .ToList(),
            };

            var payload = SoakChainFixture.Serialize(model);
            File.WriteAllText(path, payload);
            var sha256 = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload)));
            Console.WriteLine($"reference-fixture={path}");
            return sha256;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            Console.Error.WriteLine($"soak: Referenz-Fixture konnte nicht geschrieben werden ({path}): {exception.Message}");
            return null;
        }
    }

    /// <summary>
    /// Teilreport nach vorzeitigem Abbruch: maschinenlesbar, aber
    /// ausdruecklich als unvollstaendig und keine Evidenz markiert.
    /// </summary>
    private static void WritePartialReport(
        string reportPath,
        string command,
        uint seed,
        long requiredTicks,
        bool accelerated,
        string commit,
        string buildMode,
        string reason)
    {
        var partial = JsonSerializer.Serialize(new
        {
            schemaVersion = SoakReportSchema.CurrentVersion,
            mode = SoakReportSchema.ModeSoak,
            command,
            scenario = new { id = SoakScenarios.Replay, seed },
            execution = new
            {
                evidenceUnit = false,
                evidenceReason = (string?)$"early-abort:{reason}",
                paced = !accelerated,
                wallDurationSeconds = 0.0,
                requiredWallSeconds = SoakPlan.RequiredWallSeconds,
                ticksExecuted = 0L,
                requiredTicks,
                complete = false,
                isEvidence = false,
                incompleteReason = (string?)reason,
            },
            environment = new { rid = BenchEnvironment.Rid(), commit, buildMode },
            partial = true,
            note = "Teilreport eines vorzeitig beendeten Laufs; niemals Evidenz.",
        }, BenchRunner.ReportJsonOptions) + "\n";

        if (!BenchRunner.WriteReportOrDiagnose(reportPath, partial))
        {
            Console.Error.WriteLine("soak: Auch der Teilreport konnte nicht geschrieben werden.");
        }
    }
}
