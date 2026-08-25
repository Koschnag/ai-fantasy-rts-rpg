using System.Globalization;
using System.Text.Json;
using Riftward.App.Bench;
using Riftward.Simulation;

namespace Riftward.App.Soak;

/// <summary>
/// Maschinenpruefbarer Evidenzvertrag des soak-replay-Reports (AC-T022-02
/// bis AC-T022-10), konsistent zur bestehenden Reportlinie versioniert
/// weiterentwickelt (Eigene Schemaversion 2 mit mode=soak; V2 ersetzt die
/// Autoritativkennung des Einzelprozesses durch die Evidenzeinheiten-Kennung
/// des wiederholungsbasierten Soakvertrags V2). Fail-closed: fehlende
/// Pflichtfelder, falsche Typen, erfundene Kennzahlen ohne Methodenkennung,
/// nicht begruendete unavailable-Kennzeichnungen und fehlende
/// Diagnosekennzeichnungen lassen die Pruefung fehlschlagen; unbekannte
/// Felder werden abgelehnt.
/// </summary>
public static class SoakReportSchema
{
    public const int CurrentVersion = 2;
    public const string ModeSoak = "soak";

    /// <summary>Hex64-Darstellung eines 64-Bit-Zustands-Hashs.</summary>
    public const string HashFormat = "x16";

    internal static SoakHex Hex { get; } = new();

    internal static SoakHex64 FixtureSha { get; } = new();

    /// <summary>Gesamtschema des von SoakReplayRunner geschriebenen Reports.</summary>
    internal static RObj Root { get; } = BuildRoot();

    internal sealed class SoakHex : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                errors.Add($"{path}: Hexzeichenkette erwartet.");
                return;
            }

            var value = element.GetString() ?? string.Empty;

            if (value.Length != 16 || !IsLowerHex(value))
            {
                errors.Add($"{path}: 16-stelliger Kleinbuchstaben-Hexwert erwartet.");
            }
        }
    }

    internal sealed class SoakHex64 : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                errors.Add($"{path}: Hexzeichenkette erwartet.");
                return;
            }

            var value = element.GetString() ?? string.Empty;

            if (value.Length != 64 || !IsLowerHex(value))
            {
                errors.Add($"{path}: 64-stelliger Kleinbuchstaben-Hexwert erwartet.");
            }
        }
    }

    private static bool IsLowerHex(string value)
    {
        foreach (var character in value)
        {
            if (character is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Konstante mit mehreren erlaubten Literalwerten.</summary>
    private sealed class AnyLit(params string[] literals) : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.String
                || !literals.Any(literal => string.Equals(element.GetString(), literal, StringComparison.Ordinal)))
            {
                errors.Add($"{path}: einer der Konstantwerte [{string.Join(", ", literals)}] erwartet.");
            }
        }
    }

    /// <summary>
    /// Headless-Kennzahl: ausschliesslich die unavailable-Form ist gueltig.
    /// Ein angeblich messender GPU-/Draw-Wert ohne Messquelle wird abgewiesen.
    /// </summary>
    private sealed class HeadlessUnavailable : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{path}: Objekt erwartet.");
                return;
            }

            if (!element.TryGetProperty("measured", out var flag)
                || flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                errors.Add($"{path}.measured: boolesche Messkennung erwartet.");
                return;
            }

            if (flag.GetBoolean())
            {
                errors.Add(
                    $"{path}.measured: headless Szenario kann diesen Wert nicht messen; nur unavailable erlaubt.");
                return;
            }

            if (!element.TryGetProperty("reason", out var reason)
                || reason.ValueKind != JsonValueKind.String
                || reason.GetString()?.Length == 0)
            {
                errors.Add($"{path}.reason: maschinenlesbarer Grund erforderlich.");
            }
        }
    }

    /// <summary>
    /// Golden-Fixture-Bindung: Vergleichsform gegen die geladene Fixture
    /// oder Emissionsform eines beschleunigten Referenzlaufs (diagnostisch).
    /// </summary>
    private sealed class GoldenFixtureAlternative : ReportNode
    {
        private static readonly RObj CompareShape = new(
            ("emitted", new RBool(false)),
            ("path", new RStr()),
            ("sha256", FixtureSha),
            ("schemaId", new RLit(SoakChainFixture.Kind)),
            ("sampleCount", new RInt(2)),
            ("samplesMatched", new RInt(0)),
            ("sampleMismatches", new RInt(0)),
            ("sampleSkipped", new RInt(0)),
            ("matched", new RBool()));

        private static readonly RObj EmissionShape = new(
            ("emitted", new RBool(true)),
            ("path", new RStr()),
            ("sha256", FixtureSha),
            ("schemaId", new RLit(SoakChainFixture.Kind)),
            ("sampleCount", new RInt(2)),
            ("note", new RLit("emission-mode-reference-run-diagnostic-only")));

        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{path}: Objekt erwartet.");
                return;
            }

            if (!element.TryGetProperty("emitted", out var flag)
                || flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                errors.Add($"{path}.emitted: boolesche Emissionskennung erwartet.");
                return;
            }

            if (flag.GetBoolean())
            {
                EmissionShape.Check(path, element, errors);
            }
            else
            {
                CompareShape.Check(path, element, errors);
            }
        }
    }

    /// <summary>Working-Set-Kennzahl: Messform oder unavailable mit Grund.</summary>
    private sealed class WorkingSetAlternative : ReportNode
    {
        private static readonly RObj MeasuredShape = new(
            ("measured", new RBool(true)),
            ("unit", new RLit("KiB")),
            ("method", new RLit("proc-self-status-vmrss-window-samples")),
            ("first", new RInt(0)),
            ("min", new RInt(0)),
            ("max", new RInt(0)),
            ("end", new RInt(0)),
            ("windowMeans", new RArr(new RInt(0), 1)));

        private static readonly RObj UnavailableShape = new(
            ("measured", new RBool(false)),
            ("reason", new RStr()));

        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{path}: Objekt erwartet.");
                return;
            }

            if (!element.TryGetProperty("measured", out var flag)
                || flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                errors.Add($"{path}.measured: boolesche Messkennung erwartet.");
                return;
            }

            if (flag.GetBoolean())
            {
                MeasuredShape.Check(path, element, errors);
            }
            else
            {
                UnavailableShape.Check(path, element, errors);
            }
        }
    }

    private static RObj BuildRoot() => new(
        ("schemaVersion", new RInt(CurrentVersion, CurrentVersion)),
        ("mode", new RLit(ModeSoak)),
        ("command", new RStr()),
        ("scenario", new RObj(
            ("id", new RLit(SoakScenarios.Replay)),
            ("seed", new RInt(0, uint.MaxValue)),
            ("tickRateHz", new RInt(SimulationContract.TickRateHz, SimulationContract.TickRateHz)),
            ("agentCount", new RInt(SimulationContract.AgentCount, SimulationContract.AgentCount)),
            ("worldId", new RLit(SimulationContract.WorldId)),
            ("content", new RLit("synthetic-graybox-movement-world")),
            ("executionModeId", new AnyLit(
                SoakContract.RealtimeAuthoritativeModeId,
                SoakContract.AcceleratedDiagnosticModeId,
                SoakContract.ReferenceEmissionDiagnosticModeId,
                SoakContract.AcceleratedEvidenceModeId)))),
        ("reliabilityContract", new RObj(
            ("document", new RLit(SoakContract.DocumentPath)),
            ("version", new RLit(SoakContract.ContractVersion)),
            ("simulationContractDocument", new RLit(SimulationContract.DocumentPath)),
            ("simulationContractVersion", new RLit(SimulationContract.ContractVersion)),
            ("hashAlgorithm", new RLit(SimulationContract.HashAlgorithmId)),
            ("commandPlanAlgorithm", new RLit(SimulationContract.CommandPlanAlgorithmId)),
            ("evidenceUnitId", new RLit(SoakContract.EvidenceUnitId)),
            ("minimumEvidenceRepetitions", new RInt(SoakContract.MinimumEvidenceRepetitions, SoakContract.MinimumEvidenceRepetitions)),
            ("allocationLimitBytesPerWarmTick", new RInt(0, long.MaxValue)),
            ("absoluteGrowthLimitMiB", new RNum(true)),
            ("trendLimitKiBPerHour", new RNum(true)),
            ("watchdogWindowSeconds", new RNum(true)),
            ("windowSeconds", new RInt(1)),
            ("calibrationReference", new RStr()))),
        ("commandPlan", new RObj(
            ("algorithm", new RLit(SimulationContract.CommandPlanAlgorithmId)),
            ("commands", new RInt(1)),
            ("hash", Hex),
            ("firstCommand", new RObj(
                ("tick", new RInt(0)),
                ("scopeGroup", new RInt(0, SimulationContract.GroupCount - 1)),
                ("kind", new RLit("GroupMoveToZone")),
                ("zoneIndex", new RInt(0, NavWorld.ZoneCount - 1)))))),
        ("startedAtUtc", new RStr()),
        ("finishedAtUtc", new RStr()),
        ("environment", new RObj(
            ("os", new RObj(("type", new RStr()), ("kernelRelease", new RStr()))),
            ("cpu", new RObj(("model", new RStr()))),
            ("rid", new RLit("linux-x64")),
            ("commit", new RStr()),
            ("buildMode", new RStr()),
            ("pins", new RArr(new RObj(
                ("id", new RStr()),
                ("refType", new RStr()),
                ("ref", new RStr()),
                ("commit", new RStr()),
                ("sourceSha256", new RStr()),
                ("licenseSpdx", new RStr())), 4)))),
        ("execution", new RObj(
            ("evidenceUnit", new RBool()),
            ("evidenceReason", new RNullableStr()),
            ("paced", new RBool()),
            ("wallDurationSeconds", new RNum(true)),
            ("requiredWallSeconds", new RNum(true)),
            ("ticksExecuted", new RInt(0)),
            ("requiredTicks", new RInt(1)),
            ("warmupTicks", new RInt(0)),
            ("complete", new RBool()),
            ("isEvidence", new RBool()),
            ("incompleteReason", new RNullableStr()))),
        ("metrics", Metrics()),
        ("qtec010", new RObj(
            ("statement", new RLit(SoakContract.Qtec010Statement)))),
        ("gate", new RObj(
            ("limits", new RObj(
                ("absoluteGrowthLimitMiB", new RNum(true)),
                ("trendLimitKiBPerHour", new RNum(true)),
                ("allocationsPerWarmTickLimitBytes", new RInt(0)),
                ("watchdogWindowSeconds", new RNum(true)),
                ("requiredWallSeconds", new RNum(true)),
                ("requiredTicks", new RInt(1)))),
            ("violations", new RArr(new RStr())),
            ("pass", new RBool()),
            ("complete", new RBool()))),
        ("profiles", new RArr(new RObj(
            ("id", new RStr()),
            ("status", new RStr()),
            ("boundReferenceClass", new RNullableStr()),
            ("reason", new RStr())), 3)),
        ("baseline", new RObj(
            ("classification", new RLit("diagnostic-developer-workstation")),
            ("protocol", new RLit("qops001-2026-08-24")))),
        ("exitCode", new RInt(int.MinValue, int.MaxValue)));

    private static RObj Metrics() => new(
        ("workingSetKiB", new WorkingSetAlternative()),
        ("managedAllocationsPerWarmTick", RMetric.Numeric(true,
            ("perWarmTick", new RNum(true)),
            ("verificationTicks", new RInt(1)),
            ("bursts", new RInt(1)))),
        ("managedAllocationWindowDeltasDiagnostic", RMetric.Numeric(true,
            ("perWarmTick", new RNum(true)),
            ("gateCoupled", new RBool(false)),
            ("windowDeltaBytes", new RArr(new RInt(0), 1)))),
        ("gcPauseSumMs", RMetric.Numeric(true,
            ("value", new RNum(true)))),
        ("gcPauseCount", RMetric.Numeric(true,
            ("value", new RInt(0)))),
        ("activeAgents", RMetric.Numeric(true,
            ("value", new RInt(1)))),
        ("stateHashChain", new RObj(
            ("unit", new RLit("hex64")),
            ("method", new RLit(SimulationContract.HashAlgorithmId)),
            ("start", Hex),
            ("intervalSampleTicks", new RArr(new RInt(0))),
            ("intervalHashes", new RArr(Hex)),
            ("end", Hex))),
        ("goldenFixture", new GoldenFixtureAlternative()),
        ("watchdog", RMetric.Numeric(true,
            ("windowSeconds", new RNum(true)),
            ("checks", new RInt(0)),
            ("maxObservedProgressGapSeconds", new RNum(true)),
            ("stalled", new RBool()))),
        ("tickTimeDriftDiagnostic", RMetric.Numeric(true,
            ("gateCoupled", new RBool(false)),
            ("beginP50Ms", new RNum(true)), ("beginP95Ms", new RNum(true)), ("beginP99Ms", new RNum(true)),
            ("middleP50Ms", new RNum(true)), ("middleP95Ms", new RNum(true)), ("middleP99Ms", new RNum(true)),
            ("endP50Ms", new RNum(true)), ("endP95Ms", new RNum(true)), ("endP99Ms", new RNum(true)))),
        ("drawSubmitCallsPerFrame", new HeadlessUnavailable()),
        ("visibleTrianglesPerFrame", new HeadlessUnavailable()),
        ("gpuTimeMs", new HeadlessUnavailable()));

    /// <summary>
    /// Prueft Struktur und beweisrelevante Querbeziehungen eines Reporttexts;
    /// Rueckgabe ist die Fehlerliste (leer == gueltig).
    /// </summary>
    public static IReadOnlyList<string> Validate(string json)
    {
        var errors = BenchReportSchema.ValidateWith(Root, json).ToList();

        if (errors.Count > 0)
        {
            return errors;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        var execution = root.GetProperty("execution");
        var fixture = root.GetProperty("metrics").GetProperty("goldenFixture");
        var gate = root.GetProperty("gate");
        var evidenceUnit = execution.GetProperty("evidenceUnit").GetBoolean();
        var isEvidence = execution.GetProperty("isEvidence").GetBoolean();
        var emitted = fixture.GetProperty("emitted").GetBoolean();
        var executionModeId = root.GetProperty("scenario").GetProperty("executionModeId").GetString();
        var reason = execution.GetProperty("evidenceReason");

        if (evidenceUnit != isEvidence)
        {
            errors.Add("$.execution: evidenceUnit und isEvidence muessen identisch sein.");
        }

        if (evidenceUnit)
        {
            if (reason.ValueKind != JsonValueKind.Null)
            {
                errors.Add("$.execution.evidenceReason: Evidenzeinheit darf keinen Ablehnungsgrund tragen.");
            }

            if (!string.Equals(
                executionModeId,
                SoakContract.AcceleratedEvidenceModeId,
                StringComparison.Ordinal))
            {
                errors.Add("$.scenario.executionModeId: Evidenzeinheit erfordert den beschleunigten Wiederholungsmodus.");
            }

            if (!execution.GetProperty("complete").GetBoolean()
                || !gate.GetProperty("complete").GetBoolean()
                || !gate.GetProperty("pass").GetBoolean())
            {
                errors.Add("$.execution.evidenceUnit: Evidenz erfordert einen vollstaendigen bestandenen Gate-Lauf.");
            }

            if (emitted || !fixture.GetProperty("matched").GetBoolean())
            {
                errors.Add("$.metrics.goldenFixture: Evidenz erfordert einen bestandenen Vergleich gegen eine bestehende Fixture.");
            }

            if (!emitted)
            {
                var expectedSampleCount = SoakPlan.ChainSampleCount(
                    SoakPlan.TotalSimulationTick,
                    SoakPlan.HashSampleIntervalTicks);

                if (fixture.GetProperty("sampleCount").GetInt64() != expectedSampleCount
                    || fixture.GetProperty("samplesMatched").GetInt64() != expectedSampleCount
                    || fixture.GetProperty("sampleMismatches").GetInt64() != 0
                    || fixture.GetProperty("sampleSkipped").GetInt64() != 0)
                {
                    errors.Add("$.metrics.goldenFixture: Evidenz erfordert die vollstaendige kanonische Stichprobenabdeckung.");
                }
            }

            var stateHashChain = root.GetProperty("metrics").GetProperty("stateHashChain");
            var intervalTicks = stateHashChain.GetProperty("intervalSampleTicks");
            var intervalHashes = stateHashChain.GetProperty("intervalHashes");
            var canonicalSchedule = SoakPlan.ChainSchedule(
                SoakPlan.TotalSimulationTick,
                SoakPlan.HashSampleIntervalTicks);

            if (intervalTicks.GetArrayLength() != canonicalSchedule.Length - 1
                || intervalHashes.GetArrayLength() != intervalTicks.GetArrayLength())
            {
                errors.Add("$.metrics.stateHashChain: Evidenz erfordert die vollstaendige kanonische Intervallkette.");
            }
            else
            {
                var index = 0;

                foreach (var tick in intervalTicks.EnumerateArray())
                {
                    if (tick.GetInt64() != canonicalSchedule[index])
                    {
                        errors.Add("$.metrics.stateHashChain.intervalSampleTicks: nichtkanonischer Stichprobenplan.");
                        break;
                    }

                    index++;
                }
            }

            if (!string.Equals(
                root.GetProperty("environment").GetProperty("buildMode").GetString(),
                "Release",
                StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("$.environment.buildMode: Evidenzeinheit erfordert Release-nahe Konfiguration.");
            }

            var requiredTicks = execution.GetProperty("requiredTicks").GetInt64();
            var ticksExecuted = execution.GetProperty("ticksExecuted").GetInt64();
            var authoritativeTicks = SoakPlan.AuthoritativeTickCount + SoakPlan.WarmupTicks;

            if (requiredTicks != authoritativeTicks || ticksExecuted != requiredTicks)
            {
                errors.Add("$.execution.requiredTicks: Evidenzeinheit erfordert den vollstaendigen Planhorizont.");
            }

            var seed = root.GetProperty("scenario").GetProperty("seed").GetUInt32();
            var expectedPlanHash = CommandPlan
                .Hash(CommandPlan.Generate(seed, checked((int)authoritativeTicks)))
                .ToString("x16", CultureInfo.InvariantCulture);

            if (seed != SoakContract.DefaultSeed
                || !string.Equals(
                    root.GetProperty("commandPlan").GetProperty("hash").GetString(),
                    expectedPlanHash,
                    StringComparison.Ordinal))
            {
                errors.Add("$.commandPlan.hash: Evidenzeinheit erfordert Vertragssseed und kanonischen Befehlsplan.");
            }

            if (gate.GetProperty("violations").GetArrayLength() != 0
                || root.GetProperty("exitCode").GetInt32() != 0)
            {
                errors.Add("$.gate: Evidenzeinheit erfordert leere Verletzungsliste und Exitcode 0.");
            }
        }
        else if (reason.ValueKind != JsonValueKind.String || reason.GetString()?.Length == 0)
        {
            errors.Add("$.execution.evidenceReason: abgelehnte Evidenzeinheit erfordert einen Grund.");
        }

        if (emitted)
        {
            if (evidenceUnit || isEvidence)
            {
                errors.Add("$.metrics.goldenFixture.emitted: Referenzemission ist niemals Evidenz.");
            }

            if (!string.Equals(
                executionModeId,
                SoakContract.ReferenceEmissionDiagnosticModeId,
                StringComparison.Ordinal))
            {
                errors.Add("$.scenario.executionModeId: Referenzemission erfordert den eigenen Diagnosemodus.");
            }

            if (reason.ValueKind != JsonValueKind.String
                || !string.Equals(
                    reason.GetString(),
                    SoakEvidenceUnit.ReferenceEmissionDiagnosticReason,
                    StringComparison.Ordinal))
            {
                errors.Add("$.execution.evidenceReason: Referenzemission erfordert den diagnostischen Emissionsgrund.");
            }
        }
        else if (string.Equals(
            executionModeId,
            SoakContract.ReferenceEmissionDiagnosticModeId,
            StringComparison.Ordinal))
        {
            errors.Add("$.scenario.executionModeId: Emissionsmodus erfordert goldenFixture.emitted=true.");
        }

        return errors;
    }
}
