using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftward.App.Bench;
using Riftward.Platform;
using Riftward.Save;
using Riftward.Simulation;

namespace Riftward.App;

/// <summary>
/// Öffentliche Eingangsweiche des savecheck-Befehls (T-031): vertragliche
/// Vorprüfung der Argumente, Ausführung des Phasenkerns und maschinen-
/// lesbarer NF-007-Report mit fail-closed Gate. Verletzungen ergeben
/// Exitcode 33 bei trotzdem geschriebenen, klar als nicht bestanden
/// markierten Report; ein unvollständiger Lauf ergibt Exitcode 34 mit einem
/// ausdrücklich als keine Evidenz markierten Teilreport. Schemawidersprüche
/// nutzen Code 27, nicht schreibbare Reportpfade Code 28.
/// </summary>
internal static class SavecheckRunner
{
    public const string CommandName = "./scripts/rift.sh savecheck";
    public const string ScenarioId = "savecheck-sim-state-v1";

    private static readonly long[] IncompleteCalibrationBytes = [0L, 0L];

    public static int Run(CommandLineArgs arguments)
    {
        var reportPath = arguments.Option("--report");

        if (string.IsNullOrWhiteSpace(reportPath))
        {
            Console.Error.WriteLine("savecheck: --report PFAD ist erforderlich.");
            return ExitCodes.Usage;
        }

        var seed = unchecked((uint)arguments.NumberOption("--seed", SaveContract.DefaultSeed));
        var planTicks = (int)Math.Clamp(arguments.NumberOption("--plan-ticks", SaveContract.DefaultPlanTicks), 600, 40_000);
        var sampleIntervalTicks =
            (int)Math.Clamp(arguments.NumberOption("--sample-interval-ticks", SaveContract.ChainSampleIntervalTicks), 30, 3_600);
        var workDirectory = arguments.Option("--work") ?? Path.Combine(".ai", "runtime", "savecheck");
        var safeTick = (int)Math.Clamp(
            arguments.NumberOption("--safe-tick", planTicks / 2),
            CommandPlan.FirstCommandTick + 1,
            planTicks - 1);
        var continuationTicks = planTicks - safeTick;

        if (!SavecheckGate.ContinuationMeetsContractMinimum(planTicks, safeTick, out _))
        {
            Console.Error.WriteLine(
                $"savecheck: Fortsetzungshorizont {continuationTicks} Ticks verletzt den Mindestanteil "
                + $"{SaveContract.MinContinuationFractionNumerator}/{SaveContract.MinContinuationFractionDenominator} "
                + $"des Planhorizonts ({planTicks} Ticks bei sicherem Tick {safeTick}).");
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            Console.Error.WriteLine($"savecheck: Toolchain-Lock nicht lesbar: {exception.Message}");
            return ExitCodes.Map(PlatformErrorCode.ArtifactManifestInvalid);
        }

        var plan = CommandPlan.Generate(seed, planTicks);

        SavecheckEngine.Result engineResult;

        try
        {
            engineResult = SavecheckEngine.Execute(seed, planTicks, safeTick, sampleIntervalTicks, workDirectory, commit);
        }
        catch (Exception exception)
        {
            // Vollständige Diagnose auf der Fehlerausgabe; der Report selbst
            // bleibt auf eine verständliche Ursache ohne interne Pfade begrenzt.
            Console.Error.WriteLine(exception.ToString());

            // Unvollständiger Lauf: Teilreport ist ausdrücklich keine Evidenz.
            return WriteIncompleteReport(
                reportPath!,
                processStart,
                environment,
                pins,
                seed,
                planTicks,
                safeTick,
                continuationTicks,
                sampleIntervalTicks,
                plan,
                $"{exception.GetType().Name}: {exception.Message}");
        }

        var verdict = SavecheckGate.Evaluate(engineResult.Checks);
        var gateExitCode = verdict.Pass ? ExitCodes.Ok : SaveContract.ExitCodeGateViolated;

        var sanityLimitBytes = engineResult.CalibrationFirstBytes == engineResult.CalibrationSecondBytes
            ? checked(engineResult.CalibrationFirstBytes * SaveContract.SizeSanityFactor)
            : 0L;

        var reportJson = JsonSerializer.Serialize(new
        {
            schemaVersion = SaveReportSchema.CurrentVersion,
            mode = SaveReportSchema.ModeSavecheck,
            command = $"{CommandName} --report <PFAD>",
            scenario = new
            {
                id = ScenarioId,
                seed,
                planTicks,
                safeTick,
                continuationTicks,
                sampleIntervalTicks,
                tickRateHz = SimulationContract.TickRateHz,
                agentCount = SimulationContract.AgentCount,
                worldId = SimulationContract.WorldId,
            },
            saveContract = new
            {
                document = SaveContract.DocumentPath,
                version = SaveContract.ContractVersion,
                encodingId = SaveContract.EncodingId,
                simulationContractDocument = SimulationContract.DocumentPath,
                simulationContractVersion = SimulationContract.ContractVersion,
                hashAlgorithm = SimulationContract.HashAlgorithmId,
            },
            commandPlan = new
            {
                algorithm = SimulationContract.CommandPlanAlgorithmId,
                commands = plan.Length,
                hash = FormatHash(CommandPlan.Hash(plan)),
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
            execution = new
            {
                complete = true,
                isEvidence = true,
                incompleteReason = (string?)null,
            },
            metrics = new
            {
                snapshotBytes = new
                {
                    unit = "bytes",
                    method = "serialized-canonical-payload-at-safe-tick",
                    value = engineResult.SnapshotBytes,
                },
                calibrationRuns = new
                {
                    unit = "bytes",
                    method = "fresh-world-capture-at-safe-tick-two-runs",
                    runs = SaveContract.CalibrationMinimumRuns,
                    bytesPerRun = new[]
                    {
                        engineResult.CalibrationFirstBytes,
                        engineResult.CalibrationSecondBytes,
                    },
                    consistent = engineResult.CalibrationFirstBytes == engineResult.CalibrationSecondBytes,
                },
                sizeSanityLimit = new
                {
                    unit = "bytes",
                    method = "calibrated-multiple-band-2-to-16-savevertrag-section-6",
                    factor = SaveContract.SizeSanityFactor,
                    bandMinimum = SaveContract.SizeSanityFactorMinimum,
                    bandMaximum = SaveContract.SizeSanityFactorMaximum,
                    limitBytes = sanityLimitBytes,
                },
                payloadHash = new
                {
                    unit = "hex64",
                    method = "sha256-canonical-payload-bytes",
                    value = Convert.ToHexStringLower(SHA256.HashData(engineResult.PayloadBytes)),
                },
                slotFileSha256 = new
                {
                    unit = "hex64",
                    method = "sha256-slot-file-bytes",
                    value = Convert.ToHexStringLower(SHA256.HashData(engineResult.DocumentBytes)),
                },
                phaseDurationsMs = engineResult.Phases.Select(phase => new
                {
                    phase = phase.Name,
                    durationMs = Math.Round(phase.DurationMs, 3),
                    gateCoupled = false,
                }),
            },
            checks = engineResult.Checks.Select(check => check.ToJson()),
            continuationChain = new
            {
                unit = "hex64",
                method = SimulationContract.HashAlgorithmId,
                samplesAfterSafeTick = engineResult.ContinuationSamplesAfterSafeTick.Select(sample => new
                {
                    tick = sample.Tick,
                    hash = FormatHash(sample.Hash),
                }),
                end = FormatHash(engineResult.ContinuationEndHash),
                referenceEnd = FormatHash(engineResult.ReferenceEndHash),
                identical = engineResult.ContinuationIdentical,
            },
            gate = new
            {
                limits = new
                {
                    sizeSanityFactorMinimum = SaveContract.SizeSanityFactorMinimum,
                    sizeSanityFactorMaximum = SaveContract.SizeSanityFactorMaximum,
                    absoluteMaxSaveBytes = SaveContract.AbsoluteMaxSaveBytes,
                    minContinuationFractionNumerator = SaveContract.MinContinuationFractionNumerator,
                    minContinuationFractionDenominator = SaveContract.MinContinuationFractionDenominator,
                },
                violations = verdict.Violations,
                pass = verdict.Pass,
            },
            statements = new
            {
                qtec006 = SaveContract.Qtec006Statement,
                f005Partial = SaveContract.F005PartialStatement,
                finalityFixtures = SaveContract.FinalityFixtureDeferralStatement,
            },
            profiles = ProfileBinding.MandatoryWithoutReferenceHardware()
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

        // Selbstprüfung gegen den Evidenzvertrag vor jeder Gültigkeit; ein
        // Schemawiderspruch wird nie still durchgereicht.
        var schemaErrors = SaveReportSchema.Validate(reportJson);

        if (schemaErrors.Count > 0)
        {
            Console.Error.WriteLine(
                $"savecheck: Report widerspricht dem Schemavertrag: {string.Join("; ", schemaErrors)}");
            BenchRunner.WriteReportOrDiagnose(reportPath!, reportJson);
            return ExitCodes.Map(PlatformErrorCode.TelemetryInvalid);
        }

        if (!BenchRunner.WriteReportOrDiagnose(reportPath!, reportJson))
        {
            return ExitCodes.Map(PlatformErrorCode.ReportNotWritable);
        }

        Console.WriteLine(
            verdict.Pass
                ? "savecheck: alle Prüfklassen bestanden."
                : $"savecheck: Gate verletzt ({verdict.Violations.Count} Klasse(n)).");

        return gateExitCode;
    }

    private static int WriteIncompleteReport(
        string reportPath,
        DateTimeOffset processStart,
        SystemInfo.Environment environment,
        IReadOnlyList<ToolchainPin> pins,
        uint seed,
        int planTicks,
        int safeTick,
        int continuationTicks,
        int sampleIntervalTicks,
        SimCommand[] plan,
        string reason)
    {
        var reportJson = JsonSerializer.Serialize(new
        {
            schemaVersion = SaveReportSchema.CurrentVersion,
            mode = SaveReportSchema.ModeSavecheck,
            command = $"{CommandName} --report <PFAD>",
            scenario = new
            {
                id = ScenarioId,
                seed,
                planTicks,
                safeTick,
                continuationTicks,
                sampleIntervalTicks,
                tickRateHz = SimulationContract.TickRateHz,
                agentCount = SimulationContract.AgentCount,
                worldId = SimulationContract.WorldId,
            },
            saveContract = new
            {
                document = SaveContract.DocumentPath,
                version = SaveContract.ContractVersion,
                encodingId = SaveContract.EncodingId,
                simulationContractDocument = SimulationContract.DocumentPath,
                simulationContractVersion = SimulationContract.ContractVersion,
                hashAlgorithm = SimulationContract.HashAlgorithmId,
            },
            commandPlan = new
            {
                algorithm = SimulationContract.CommandPlanAlgorithmId,
                commands = plan.Length,
                hash = FormatHash(plan.Length > 0 ? CommandPlan.Hash(plan) : 0UL),
                firstCommand = plan.Length > 0
                    ? new
                    {
                        tick = plan[0].Tick,
                        scopeGroup = (int)plan[0].ScopeGroup,
                        kind = plan[0].Kind.ToString(),
                        zoneIndex = plan[0].ZoneIndex,
                    }
                    : new
                    {
                        tick = 0,
                        scopeGroup = 0,
                        kind = "GroupMoveToZone",
                        zoneIndex = 0,
                    },
            },
            startedAtUtc = processStart,
            finishedAtUtc = DateTime.UtcNow,
            environment = new
            {
                os = new { type = environment.OsType, kernelRelease = environment.KernelRelease },
                cpu = new { model = environment.CpuModel },
                rid = BenchEnvironment.Rid(),
                commit = BenchEnvironment.CommitId(),
                buildMode = BenchEnvironment.BuildMode(),
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
                complete = false,
                isEvidence = false,
                incompleteReason = reason,
            },
            metrics = new
            {
                snapshotBytes = new
                {
                    unit = "bytes",
                    method = "serialized-canonical-payload-at-safe-tick",
                    value = 0,
                },
                calibrationRuns = new
                {
                    unit = "bytes",
                    method = "fresh-world-capture-at-safe-tick-two-runs",
                    runs = SaveContract.CalibrationMinimumRuns,
                    bytesPerRun = IncompleteCalibrationBytes,
                    consistent = false,
                },
                sizeSanityLimit = new
                {
                    unit = "bytes",
                    method = "calibrated-multiple-band-2-to-16-savevertrag-section-6",
                    factor = SaveContract.SizeSanityFactor,
                    bandMinimum = SaveContract.SizeSanityFactorMinimum,
                    bandMaximum = SaveContract.SizeSanityFactorMaximum,
                    limitBytes = 1,
                },
                payloadHash = new
                {
                    unit = "hex64",
                    method = "sha256-canonical-payload-bytes",
                    value = new string('0', 64),
                },
                slotFileSha256 = new
                {
                    unit = "hex64",
                    method = "sha256-slot-file-bytes",
                    value = new string('0', 64),
                },
                phaseDurationsMs = Array.Empty<object>(),
            },
            checks = Array.Empty<object>(),
            continuationChain = new
            {
                unit = "hex64",
                method = SimulationContract.HashAlgorithmId,
                samplesAfterSafeTick = Array.Empty<object>(),
                end = FormatHash(0UL),
                referenceEnd = FormatHash(0UL),
                identical = false,
            },
            gate = new
            {
                limits = new
                {
                    sizeSanityFactorMinimum = SaveContract.SizeSanityFactorMinimum,
                    sizeSanityFactorMaximum = SaveContract.SizeSanityFactorMaximum,
                    absoluteMaxSaveBytes = SaveContract.AbsoluteMaxSaveBytes,
                    minContinuationFractionNumerator = SaveContract.MinContinuationFractionNumerator,
                    minContinuationFractionDenominator = SaveContract.MinContinuationFractionDenominator,
                },
                violations = new[] { $"lauf-unvollstaendig: {reason}" },
                pass = false,
            },
            statements = new
            {
                qtec006 = SaveContract.Qtec006Statement,
                f005Partial = SaveContract.F005PartialStatement,
                finalityFixtures = SaveContract.FinalityFixtureDeferralStatement,
            },
            profiles = ProfileBinding.MandatoryWithoutReferenceHardware()
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
            exitCode = SaveContract.ExitCodeRunIncomplete,
        }, BenchRunner.ReportJsonOptions) + "\n";

        if (!BenchRunner.WriteReportOrDiagnose(reportPath, reportJson))
        {
            return ExitCodes.Map(PlatformErrorCode.ReportNotWritable);
        }

        Console.Error.WriteLine($"savecheck: Lauf unvollständig; Teilreport ist keine Evidenz. Grund: {reason}");
        return SaveContract.ExitCodeRunIncomplete;
    }

    private static string FormatHash(ulong hash) => hash.ToString("x16", CultureInfo.InvariantCulture);
}
