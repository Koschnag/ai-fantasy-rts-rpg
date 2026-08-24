using System.Text.Json;
using Riftward.Simulation;

namespace Riftward.App.Bench;

/// <summary>
/// Maschinenpruefbarer Evidenzvertrag des bench-sim-Reports (AC-T021-04),
/// konsistent zum T-020-Schema versioniert weiterentwickelt (Schemaversion 2,
/// gleicher RMetric-Kern mit Pflicht-Einheit und -Methodenkennung). Fail-
/// closed: fehlende Pflichtfelder, falsche Typen, erfundene Kennzahlen ohne
/// Methodenkennung, nicht begruendete unavailable-Kennzeichnungen und
/// headless-fremde GPU-/Draw-Messwerte lassen die Pruefung fehlschlagen;
/// unbekannte Felder werden abgelehnt.
/// </summary>
public static class SimReportSchema
{
    public const int CurrentVersion = 2;

    /// <summary>Hex64-Darstellung eines 64-Bit-Zustands-Hashs.</summary>
    public const string HashFormat = "x16";

    internal static RHex Hex { get; } = new();

    /// <summary>Gesamtschema des von SimBenchRunner geschriebenen Reports.</summary>
    internal static RObj Root { get; } = BuildRoot();

    internal sealed class RHex : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                errors.Add($"{path}: Hexzeichenkette erwartet.");
                return;
            }

            var value = element.GetString();

            if (value is null || value.Length != 16 || !IsLowerHex(value))
            {
                errors.Add($"{path}: 16-stelliger Kleinbuchstaben-Hexwert erwartet.");
            }
        }

        private static bool IsLowerHex(string value)
        {
            foreach (var character in value)
            {
                var isDigit = character is >= '0' and <= '9';
                var isLowerHexLetter = character is >= 'a' and <= 'f';

                if (!isDigit && !isLowerHexLetter)
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// Headless-Kennzahl: ausschliesslich die unavailable-Form ist gueltig.
    /// Ein angeblich messender GPU-/Draw-Wert ohne Messquelle wird abgewiesen
    /// statt still akzeptiert.
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

            if (!element.TryGetProperty("measured", out var flag))
            {
                errors.Add($"{path}.measured: boolesche Messkennung erwartet.");
                return;
            }

            if (flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
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

    private static RObj BuildRoot() => new(
        ("schemaVersion", new RInt(CurrentVersion, CurrentVersion)),
        ("mode", new RLit(BenchReportSchema.ModeBench)),
        ("command", new RStr()),
        ("scenario", new RObj(
            ("id", new RLit(BenchScenarios.Sim)),
            ("seed", new RInt(0, uint.MaxValue)),
            ("tickRateHz", new RInt(SimulationContract.TickRateHz, SimulationContract.TickRateHz)),
            ("agentCount", new RInt(SimulationContract.AgentCount, SimulationContract.AgentCount)),
            ("worldId", new RLit(SimulationContract.WorldId)),
            ("content", new RLit("synthetic-graybox-movement-world")))),
        ("simulationContract", new RObj(
            ("document", new RLit(SimulationContract.DocumentPath)),
            ("version", new RLit(SimulationContract.ContractVersion)),
            ("numericModel", new RLit(SimulationContract.NumericModelId)),
            ("hashAlgorithm", new RLit(SimulationContract.HashAlgorithmId)),
            ("commandPlanAlgorithm", new RLit(SimulationContract.CommandPlanAlgorithmId)),
            ("allocationLimitBytesPerWarmTick", new RInt(0)))),
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
        ("measurement", new RObj(
            ("warmupTicks", new RInt(1)),
            ("sampleTicks", new RInt(1)),
            ("ticksExecuted", new RInt(2)),
            ("rssSampleIntervalTicks", new RInt(1)),
            ("hashSampleIntervalTicks", new RInt(1)))),
        ("metrics", Metrics()),
        ("gate", new RObj(
            ("limits", new RObj(
                ("p99TickTimeHardLimitMs", new RNum(true)),
                ("p99TickTimeTargetMs", new RNum(true)),
                ("allocationsPerWarmTickBytesMax", new RInt(0)))),
            ("pass", new RBool()),
            ("p99TargetMet", new RBool()),
            ("violations", new RArr(new RStr())))),
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
        ("tickTimeMs", RMetric.Numeric(true,
            ("p50", new RNum(true)), ("p95", new RNum(true)), ("p99", new RNum(true)))),
        ("managedAllocationsBytes", RMetric.Numeric(true,
            ("perWarmTick", new RNum(true)))),
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
            ("intervalSampleTicks", new RArr(new RInt(0), 1)),
            ("intervalHashes", new RArr(Hex, 1)),
            ("end", Hex))),
        ("workingSetKiB", WorkingSet()),
        ("drawSubmitCallsPerFrame", new HeadlessUnavailable()),
        ("visibleTrianglesPerFrame", new HeadlessUnavailable()),
        ("gpuTimeMs", new HeadlessUnavailable()));

    private static WorkingSetAlternative WorkingSet() => new(
        new RObj(
            ("measured", new RBool(true)),
            ("unit", new RStr()),
            ("method", new RStr()),
            ("min", new RInt(1)),
            ("max", new RInt(1)),
            ("end", new RInt(1))),
        new RObj(
            ("measured", new RBool(false)),
            ("reason", new RStr())));

    private sealed class WorkingSetAlternative(RObj measuredShape, RObj unavailableShape) : ReportNode
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
                measuredShape.Check(path, element, errors);
            }
            else
            {
                unavailableShape.Check(path, element, errors);
            }
        }
    }

    /// <summary>Prueft einen Reporttext; Rueckgabe ist die Fehlerliste (leer == gueltig).</summary>
    public static IReadOnlyList<string> Validate(string json) =>
        BenchReportSchema.ValidateWith(Root, json);
}
