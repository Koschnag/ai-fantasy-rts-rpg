using System.Text.Json;
using Riftward.App.Bench;
using Riftward.Session;

namespace Riftward.App.Command;

/// <summary>
/// Maschinenpruefbarer Evidenzvertrag des Kommandoschleifen-Reports
/// (Schemaversion 1, NF-007-Linie). Fail-closed: fehlende Pflichtfelder,
/// falsche Typen, erfundene Messwerte ohne Methodenkennung, nicht begruendete
/// unavailable-Kennzeichnungen und unbekannte Felder lassen die Pruefung
/// fehlschlagen. Die Ausfuehrungsart (headless/interaktiv) waehlt strikte
/// Alternativformen: Headless kann Renderer-/GPU-Werte nicht messen und darf
/// sie nur als unavailable mit Grund ausweisen; der Interaktivmodus muss sie
/// messend ausweisen. Gategekoppelte Felder tragen keine Diagnosemarke; alle
/// uebrigen Messfelder sind verpflichtend gateCoupled=false.
/// </summary>
public static class CommandReportSchema
{
    public const int CurrentVersion = 1;
    public const string ModeCommandLoop = "kommandoschleife";
    public const string ExecutionHeadless = "headless";
    public const string ExecutionInteractive = "interactive";

    /// <summary>Hex64-Darstellung eines 64-Bit-Zustands-Hashs.</summary>
    internal static readonly HexNode Hex = new();

    /// <summary>Hex256-Darstellung eines Artefakthashs.</summary>
    internal static readonly Sha256HexNode Sha256 = new();

    internal static RObj HeadlessBody { get; } = BuildBody(ExecutionHeadless);

    internal static RObj InteractiveBody { get; } = BuildBody(ExecutionInteractive);

    private sealed class ModeDispatch : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (!element.TryGetProperty("executionMode", out var mode)
                || mode.ValueKind != JsonValueKind.String)
            {
                errors.Add("$.executionMode: Ausfuehrungsart erwartet.");
                return;
            }

            switch (mode.GetString())
            {
                case ExecutionHeadless:
                    HeadlessBody.Check(path, element, errors);
                    break;

                case ExecutionInteractive:
                    InteractiveBody.Check(path, element, errors);
                    break;

                default:
                    errors.Add("$.executionMode: unbekannte Ausfuehrungsart.");
                    break;
            }
        }
    }

    /// <summary>Gesamtschema des von CommandLoopRunner geschriebenen Reports.</summary>
    internal static ReportNode Root { get; } = new ModeDispatch();

    private static RObj BuildBody(string executionMode) => new(
        ("schemaVersion", new RInt(CurrentVersion, CurrentVersion)),
        ("mode", new RLit(ModeCommandLoop)),
        ("executionMode", new RLit(executionMode)),
        ("command", new RStr()),
        ("scenario", new RObj(
            ("id", new RLit(SessionContract.ScenarioId)),
            ("seed", new RInt(0, uint.MaxValue)),
            ("tickRateHz", new RInt(Riftward.Simulation.SimulationContract.TickRateHz, Riftward.Simulation.SimulationContract.TickRateHz)),
            ("agentCount", new RInt(Riftward.Simulation.SimulationContract.AgentCount, Riftward.Simulation.SimulationContract.AgentCount)),
            ("worldId", new RLit(Riftward.Simulation.SimulationContract.WorldId)),
            ("content", new RLit(SessionContract.ContentId)))),
        ("commandContract", new RObj(
            ("document", new RLit(SessionContract.DocumentPath)),
            ("version", new RLit(SessionContract.ContractVersion)),
            ("scriptFormat", new RLit(SessionContract.ScriptFormatId)),
            ("selectionModel", new RLit(SessionContract.SelectionModelId)),
            ("cameraModel", new RLit(SessionContract.CameraModelId)),
            ("diagnosticOnlyReplayDisclaimer", new RBool(true)))),
        ("simulationContract", new RObj(
            ("document", new RLit(Riftward.Simulation.SimulationContract.DocumentPath)),
            ("version", new RLit(Riftward.Simulation.SimulationContract.ContractVersion)),
            ("numericModel", new RLit(Riftward.Simulation.SimulationContract.NumericModelId)),
            ("hashAlgorithm", new RLit(Riftward.Simulation.SimulationContract.HashAlgorithmId)),
            ("allocationLimitBytesPerWarmTick", new RInt(0)))),
        ("inputScript", new RObj(
            ("scriptSha256", Sha256),
            ("intentPlanHash", Hex),
            ("horizonTicks", new RInt(1)),
            ("warmupTicks", new RInt(30)),
            ("intentsTotal", new RInt(0)),
            ("appliedTotal", new RInt(0)),
            ("rejectedTotal", new RInt(0)),
            ("emptyPointDeselects", new RInt(0)),
            ("moveWithoutSelectionRejects", new RInt(0)),
            ("noZoneRejects", new RInt(0)),
            ("kernelCommandsTotal", new RInt(0)))),
        ("startedAtUtc", new RStr()),
        ("finishedAtUtc", new RStr()),
        ("environment", new RObj(
            ("os", new RObj(("type", new RStr()), ("kernelRelease", new RStr()))),
            ("cpu", new RObj(("model", new RStr()))),
            ("rid", new RLit("linux-x64")),
            ("commit", new RStr()),
            ("buildMode", new RStr()),
            ("display", Display(executionMode)),
            ("pins", new RArr(new RObj(
                ("id", new RStr()),
                ("refType", new RStr()),
                ("ref", new RStr()),
                ("commit", new RStr()),
                ("sourceSha256", new RStr()),
                ("licenseSpdx", new RStr())), 4)))),
        ("measurement", new RObj(
            ("warmupTicks", new RInt(30)),
            ("sampleTicks", new RInt(1)),
            ("ticksExecuted", new RInt(2)),
            ("hashSampleIntervalTicks", new RInt(1)),
            ("rssSampleIntervalTicks", new RInt(1)),
            ("windowCompleted", new RBool()))),
        ("metrics", Metrics(executionMode)),
        ("stateHashChain", new RObj(
            ("unit", new RLit("hex64")),
            ("method", new RLit(Riftward.Simulation.SimulationContract.HashAlgorithmId)),
            ("start", Hex),
            ("intervalSampleTicks", new RArr(new RInt(0), 1)),
            ("intervalHashes", new RArr(Hex, 1)),
            ("end", Hex))),
        ("gate", new RObj(
            ("limits", new RObj(
                ("p99TickTimeHardLimitMs", new RNum(true)),
                ("p99TickTimeTargetMs", new RNum(true)),
                ("allocationsPerWarmTickBytesMax", new RInt(0)),
                ("reactionHardLimitTicks", new RInt(SessionContract.ReactionHardLimitTicks, SessionContract.ReactionHardLimitTicks)),
                ("reactionTargetTicks", new RInt(SessionContract.ReactionTargetTicks, SessionContract.ReactionTargetTicks)),
                ("runtimeShaderCompilationAllowed", new RBool(false)))),
            ("stateChainSelfConsistency", ChainConsistencyAlternative()),
            ("pass", new RBool()),
            ("tickTimeTargetMet", new RBool()),
            ("reactionTargetMet", new RBool()),
            ("violations", new RArr(new RStr())))),
        ("openQuestions", new RObj(
            ("qtec004", new RLit("open")),
            ("qtec006", new RLit("open")),
            ("qtec010", new RLit("open")),
            ("qgam001", new RLit("open")),
            ("qgam002", new RLit("open")),
            ("qgam003", new RLit("open")),
            ("qgam004", new RLit("open")),
            ("qgam005", new RLit("open")),
            ("qgam006", new RLit("open")),
            ("qgam007", new RLit("open")),
            ("qnar002", new RLit("open")))),
        ("profiles", new RArr(new RObj(
            ("id", new RStr()),
            ("status", new RStr()),
            ("boundReferenceClass", new RNullableStr()),
            ("reason", new RStr())), 3)),
        ("baseline", new RObj(
            ("classification", new RLit("diagnostic-developer-workstation")),
            ("protocol", new RLit("qops001-2026-08-24")))),
        ("frameEvidence", new FrameEvidenceAlternative()),
        ("exitCode", new RInt(int.MinValue, int.MaxValue)));

    /// <summary>Anzeigebindung: im Interaktivmodus messend, headless unavailable mit Grund.</summary>
    private static ReportNode Display(string executionMode) =>
        executionMode == ExecutionInteractive
            ? RMetric.Measurable("renderer", new RStr(), "glVersion", new RStr())
            : new UnavailableOnly();

    private static RObj Metrics(string executionMode)
    {
        var renderDependentHeadless = new UnavailableOnly();
        var frameBand = NumericBand();
        return new RObj(
            ("tickTimeMs", RMetric.Numeric(true,
                ("p50", new RNum(true)), ("p95", new RNum(true)), ("p99", new RNum(true)))),
            ("managedAllocationsBytes", RMetric.Numeric(true,
                ("perWarmTick", new RNum(true)))),
            ("reactionTicks", new RObj(
                ("unit", new RLit("ticks")),
                ("method", new RLit("command-submission-tick-to-first-effect-state-hash-delta")),
                ("p50", new RInt(0)),
                ("p95", new RInt(0)),
                ("p99", new RInt(0)),
                ("max", new RInt(0)),
                ("count", new RInt(0)),
                ("target", new RInt(SessionContract.ReactionTargetTicks, SessionContract.ReactionTargetTicks)),
                ("hardLimit", new RInt(SessionContract.ReactionHardLimitTicks, SessionContract.ReactionHardLimitTicks)))),
            ("runtimeShaderCompilation", RMetric.Numeric(true,
                ("value", new RBool(false)))),
            ("gcPauseSumMs", Diagnostic(RMetric.Numeric(true, ("value", new RNum(true))))),
            ("gcPauseCount", Diagnostic(RMetric.Numeric(true, ("value", new RInt(0))))),
            ("activeAgents", Diagnostic(RMetric.Numeric(true, ("value", new RInt(1))))),
            ("workingSetKiB", new MeasuredAlternative([
                new RObj(
                    ("measured", new RBool(true)),
                    ("unit", new RStr()),
                    ("method", new RStr()),
                    ("min", new RInt(1)),
                    ("max", new RInt(1)),
                    ("end", new RInt(1)),
                    ("gateCoupled", new RBool(false))),
                new RObj(
                    ("measured", new RBool(false)),
                    ("reason", new RStr())),
            ])),
            ("frameTimeMs",
                executionMode == ExecutionInteractive
                    ? Diagnostic(frameBand)
                    : renderDependentHeadless),
            ("gpuTimeMs",
                executionMode == ExecutionInteractive
                    ? new MeasuredAlternative([
                        new RObj(
                            ("measured", new RBool(true)),
                            ("unit", new RStr()),
                            ("method", new RStr()),
                            ("p99", new RNum(true)),
                            ("timerFreqHz", new RInt(0)),
                            ("gateCoupled", new RBool(false))),
                        new RObj(
                            ("measured", new RBool(false)),
                            ("reason", new RStr())),
                    ])
                    : renderDependentHeadless),
            ("drawSubmitCallsPerFrame",
                executionMode == ExecutionInteractive
                    ? Diagnostic(Counted())
                    : renderDependentHeadless),
            ("visibleTrianglesPerFrame",
                executionMode == ExecutionInteractive
                    ? Diagnostic(Counted())
                    : renderDependentHeadless),
            ("concurrentMarkers",
                executionMode == ExecutionInteractive
                    ? Diagnostic(new RObj(
                        ("unit", new RStr()),
                        ("method", new RStr()),
                        ("peak", new RInt(0)),
                        ("gateCoupled", new RBool(false))))
                    : renderDependentHeadless));
    }

    private static RObj NumericBand() => RMetric.Numeric(true,
        ("p50", new RNum(true)), ("p95", new RNum(true)), ("p99", new RNum(true)));

    private static RObj Counted() => RMetric.Numeric(true, ("value", new RInt(0)));

    /// <summary>Kennzahl mit zwingender Diagnosemarke gateCoupled=false.</summary>
    private static RObj Diagnostic(RObj metric)
    {
        var fields = new List<(string Name, ReportNode Node)>(metric.Fields)
        {
            ("gateCoupled", new RBool(false)),
        };
        return new RObj(fields.ToArray());
    }

    /// <summary>
    /// Ausweisschema des Ketten-Selbstkonsistenzkriteriums (Kommandovertrag
    /// §7): entweder ausgewertet ({ evaluated: true }) oder ausdrücklich
    /// nicht auswertbar mit maschinenlesbarem Grund; eine Behauptung ohne
    /// Auswertung ist unzulaessig.
    /// </summary>
    private static EvaluatedAlternative ChainConsistencyAlternative() =>
        new(
        [
            new RObj(("evaluated", new RBool(true))),
            new RObj(
                ("evaluated", new RBool(false)),
                ("reason", new RStr())),
        ]);

    /// <summary>Alternativnode, der auf dem booleschen Feld "evaluated" dispatcht.</summary>
    private sealed class EvaluatedAlternative(IReadOnlyList<RObj> shapes) : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{path}: Objekt erwartet.");
                return;
            }

            if (!element.TryGetProperty("evaluated", out var flag)
                || flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                errors.Add($"{path}.evaluated: boolesche Auswertungskennung erwartet.");
                return;
            }

            shapes[flag.GetBoolean() ? 0 : 1].Check(path, element, errors);
        }
    }

    private sealed class UnavailableOnly : ReportNode
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

    private sealed class MeasuredAlternative(IReadOnlyList<RObj> shapes) : ReportNode
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

            shapes[flag.GetBoolean() ? 0 : 1].Check(path, element, errors);
        }
    }

    private sealed class FrameEvidenceAlternative : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{path}: Objekt erwartet.");
                return;
            }

            if (!element.TryGetProperty("captured", out var flag)
                || flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                errors.Add($"{path}.captured: boolesche Kennung erwartet.");
                return;
            }

            if (flag.GetBoolean())
            {
                new RObj(
                    ("captured", new RBool(true)),
                    ("afterMeasurementWindow", new RBool(true)),
                    ("width", new RInt(1920, 1920)),
                    ("height", new RInt(1080, 1080)),
                    ("format", new RLit(Bench.FrameEvidence.FormatId)),
                    ("sha256", Sha256),
                    ("statementLimit", new RLit(CommandFrameEvidence.StatementLimit))).Check(path, element, errors);
            }
            else
            {
                new RObj(
                    ("captured", new RBool(false)),
                    ("reason", new RStr())).Check(path, element, errors);
            }
        }
    }

    internal sealed class HexNode : ReportNode
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

        internal static bool IsLowerHex(string value)
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

    internal sealed class Sha256HexNode : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.String)
            {
                errors.Add($"{path}: Hexzeichenkette erwartet.");
                return;
            }

            var value = element.GetString();

            if (value is null || value.Length != 64 || !HexNode.IsLowerHex(value))
            {
                errors.Add($"{path}: 64-stelliger Kleinbuchstaben-Hexwert erwartet.");
            }
        }
    }

    /// <summary>Prueft einen Reporttext; Rueckgabe ist die Fehlerliste (leer == gueltig).</summary>
    public static IReadOnlyList<string> Validate(string json) =>
        BenchReportSchema.ValidateWith(Root, json);
}
