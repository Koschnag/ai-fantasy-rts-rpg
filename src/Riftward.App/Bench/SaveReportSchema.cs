using System.Text.Json;
using Riftward.Save;
using Riftward.Simulation;

namespace Riftward.App.Bench;

/// <summary>
/// Maschinenprüfbarer Evidenzvertrag des savecheck-Reports (T-031,
/// NF-007). Fail-closed: fehlende Pflichtfelder, falsche Typen,
/// nicht als diagnostisch gekennzeichnete Dauerfelder und unbekannte Felder
/// lassen die Prüfung fehlschlagen. Der Report weist die Q-TEC-006-
/// Restoffenheit und den anteiligen Charakter von F-005 maschinenlesbar aus.
/// </summary>
public static class SaveReportSchema
{
    public const int CurrentVersion = 1;

    public const string ModeSavecheck = "savecheck";

    /// <summary>Hex16-Darstellung eines 64-Bit-Zustands-Hashs.</summary>
    internal static SimReportSchema.RHex Hex { get; } = new();

    internal static RObj Root { get; } = BuildRoot();

    private static RObj BuildRoot() => new(
        ("schemaVersion", new RInt(CurrentVersion, CurrentVersion)),
        ("mode", new RLit(ModeSavecheck)),
        ("command", new RStr()),
        ("scenario", new RObj(
            ("id", new RLit("savecheck-sim-state-v1")),
            ("seed", new RInt(0, uint.MaxValue)),
            ("planTicks", new RInt(1)),
            ("safeTick", new RInt(1)),
            ("continuationTicks", new RInt(1)),
            ("sampleIntervalTicks", new RInt(1)),
            ("tickRateHz", new RInt(SimulationContract.TickRateHz, SimulationContract.TickRateHz)),
            ("agentCount", new RInt(SimulationContract.AgentCount, SimulationContract.AgentCount)),
            ("worldId", new RLit(SimulationContract.WorldId)))),
        ("saveContract", new RObj(
            ("document", new RLit(SaveContract.DocumentPath)),
            ("version", new RLit(SaveContract.ContractVersion)),
            ("encodingId", new RLit(SaveContract.EncodingId)),
            ("simulationContractDocument", new RLit(SimulationContract.DocumentPath)),
            ("simulationContractVersion", new RLit(SimulationContract.ContractVersion)),
            ("hashAlgorithm", new RLit(SimulationContract.HashAlgorithmId)))),
        ("commandPlan", new RObj(
            ("algorithm", new RLit(SimulationContract.CommandPlanAlgorithmId)),
            ("commands", new RInt(0)),
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
            ("complete", new RBool(true)),
            ("isEvidence", new RBool(true)),
            ("incompleteReason", new RNullableStr()))),
            ("metrics", new RObj(
            ("snapshotBytes", RMetric.Numeric(true, ("value", new RInt(0)))),
            ("calibrationRuns", new RObj(
                ("unit", new RLit("bytes")),
                ("method", new RStr()),
                ("runs", new RInt(SaveContract.CalibrationMinimumRuns)),
                ("bytesPerRun", new RArr(new RInt(0), SaveContract.CalibrationMinimumRuns)),
                ("consistent", new RBool()))),
            ("sizeSanityLimit", new RObj(
                ("unit", new RLit("bytes")),
                ("method", new RStr()),
                ("factor",
                    new RInt(SaveContract.SizeSanityFactorMinimum, SaveContract.SizeSanityFactorMaximum)),
                ("bandMinimum", new RInt(SaveContract.SizeSanityFactorMinimum, SaveContract.SizeSanityFactorMinimum)),
                ("bandMaximum", new RInt(SaveContract.SizeSanityFactorMaximum, SaveContract.SizeSanityFactorMaximum)),
                ("limitBytes", new RInt(0)))),
            ("payloadHash", ShaHexMetric("sha256-canonical-payload-bytes")),
            ("slotFileSha256", ShaHexMetric("sha256-slot-file-bytes")),
            ("phaseDurationsMs", new RArr(new RObj(
                ("phase", new RStr()),
                ("durationMs", new RNum(nonNegative: true)),
                ("gateCoupled", new RBool(false))))))),
        ("checks", new RArr(new RObj(
            ("class", new RStr()),
            ("pass", new RBool()),
            ("detail", new RNullableStr())))),
        ("continuationChain", new RObj(
            ("unit", new RLit("hex64")),
            ("method", new RLit(SimulationContract.HashAlgorithmId)),
            ("samplesAfterSafeTick", new RArr(new RObj(
                ("tick", new RInt(1)),
                ("hash", Hex)), 1)),
            ("end", Hex),
            ("referenceEnd", Hex),
            ("identical", new RBool()))),
        ("gate", new RObj(
            ("limits", new RObj(
                ("sizeSanityFactorMinimum",
                    new RInt(SaveContract.SizeSanityFactorMinimum, SaveContract.SizeSanityFactorMinimum)),
                ("sizeSanityFactorMaximum",
                    new RInt(SaveContract.SizeSanityFactorMaximum, SaveContract.SizeSanityFactorMaximum)),
                ("absoluteMaxSaveBytes", new RInt(1)),
                ("minContinuationFractionNumerator",
                    new RInt(SaveContract.MinContinuationFractionNumerator, SaveContract.MinContinuationFractionNumerator)),
                ("minContinuationFractionDenominator",
                    new RInt(SaveContract.MinContinuationFractionDenominator, SaveContract.MinContinuationFractionDenominator)))),
            ("violations", new RArr(new RStr())),
            ("pass", new RBool()))),
        ("statements", new RObj(
            ("qtec006", new RLit(SaveContract.Qtec006Statement)),
            ("f005Partial", new RLit(SaveContract.F005PartialStatement)),
            ("finalityFixtures", new RLit(SaveContract.FinalityFixtureDeferralStatement)))),
        ("profiles", new RArr(new RObj(
            ("id", new RStr()),
            ("status", new RStr()),
            ("boundReferenceClass", new RNullableStr()),
            ("reason", new RStr())), 3)),
        ("baseline", new RObj(
            ("classification", new RLit("diagnostic-developer-workstation")),
            ("protocol", new RLit("qops001-2026-08-24")))),
        ("exitCode", new RInt(int.MinValue, int.MaxValue)));

    private static RObj ShaHexMetric(string method) => new(
        ("unit", new RLit("hex64")),
        ("method", new RLit(method)),
        ("value", new RShaHex()));

    /// <summary>Hex64-Darstellung eines SHA-256-Ankers.</summary>
    internal sealed class RShaHex : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                errors.Add($"{path}: Hexzeichenkette erwartet.");
                return;
            }

            var value = element.GetString();

            if (value is null || value.Length != 64 || !IsLowerHex(value))
            {
                errors.Add($"{path}: 64-stelliger Kleinbuchstaben-Hexwert erwartet.");
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

    /// <summary>Prüft einen Reporttext; Rückgabe ist die Fehlerliste (leer == gültig).</summary>
    public static IReadOnlyList<string> Validate(string json) =>
        BenchReportSchema.ValidateWith(Root, json);
}
