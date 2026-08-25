using System.Text.Json;
using Riftward.Platform;
using Riftward.Simulation;

namespace Riftward.App.Bench;

/// <summary>
/// Maschinenpruefbarer Evidenzvertrag des bench-representative-Reports
/// (AC-T023-03), konsistent zum T-020-/T-021-Schema als Schemaversion 3
/// weiterentwickelt. Fail-closed: fehlende Pflichtfelder, falsche Typen,
/// erfundene Kennzahlen ohne Methodenkennung und nicht begruendete
/// unavailable-Kennzeichnungen lassen die Pruefung fehlschlagen; unbekannte
/// Felder werden abgelehnt. Die Kompositionsziele sind zeichenweise an die
/// codegebundene Szenariokonfiguration gebunden; beobachtete Lastzaehler
/// muessen die Ziele exakt oder nichtdegenerativ erreichen.
/// </summary>
public static class RepresentativeReportSchema
{
    public const int CurrentVersion = 3;

    internal static readonly SimReportSchema.RHex Hex = new();

    /// <summary>Gesamtschema des von RepBenchRunner geschriebenen Reports.</summary>
    internal static RObj Root { get; } = BuildRoot();

    private static RObj Counted(int minimum, int maximum) => RMetric.Numeric(
        true,
        ("value", new RInt(minimum, maximum)));

    private static RObj Metrics() => new(
        ("frameTimeMs", RMetric.Numeric(true,
            ("p50", new RNum(true)), ("p95", new RNum(true)), ("p99", new RNum(true)))),
        ("gpuTimeMs", RMetric.Measurable("p99", new RNum(true), "timerFreqHz", new RInt(0))),
        ("tickTimeMs", RMetric.Numeric(true,
            ("p50", new RNum(true)), ("p95", new RNum(true)), ("p99", new RNum(true)))),
        ("managedAllocationsBytes", RMetric.Numeric(true,
            ("perWarmFrame", new RNum(true)))),
        ("gcPauseSumMs", RMetric.Numeric(true,
            ("value", new RNum(true)))),
        ("gcPauseCount", RMetric.Numeric(true,
            ("value", new RInt(0)))),
        ("workingSetKiB", WorkingSet()),
        ("gpuMemoryBytes", RMetric.Measurable(
            "value", new RInt(0),
            "textureMemoryUsed", new RInt(0))),
        ("discreteVramBytes", MeasurableOrUnavailable()),
        ("drawSubmitCallsPerFrame", RMetric.Numeric(true,
            ("value", new RInt(0)))),
        ("visibleTrianglesPerFrameGlobal", RMetric.Numeric(true,
            ("value", new RInt(0)))),
        ("visibleTrianglesMainView", RMetric.Numeric(true,
            ("value", new RInt(1)))),
        ("concurrentParticles", RMetric.Numeric(true,
            ("value", new RInt(1, RepresentativeScenario.ParticlePeakTarget)))),
        ("sceneSetupTimeMs", RMetric.Numeric(true,
            ("value", new RNum(true)))),
        ("cardLoadBudgetLine", new RObj(
            ("applicable", new RBool(false)),
            ("owner", new RLit(BenchScenarios.Load)),
            ("reason", new RStr()))),
        ("runtimeShaderCompilation", RMetric.Numeric(true,
            ("value", new RBool(false)))));

    private static MeasuredAlternative MeasurableOrUnavailable() => new MeasuredAlternative(
        new RObj(
            ("measured", new RBool(true)),
            ("unit", new RStr()),
            ("method", new RStr()),
            ("value", new RInt(0))),
        new RObj(
            ("measured", new RBool(false)),
            ("reason", new RStr())));

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

    private sealed class MeasuredAlternative(RObj measuredShape, RObj unavailableShape) : ReportNode
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

    private static RObj CompositionTargets() => new(
        ("visibleUnits", new RInt(RepresentativeScenario.VisibleUnitsTarget, RepresentativeScenario.VisibleUnitsTarget)),
        ("simulatedAgents", new RInt(RepresentativeScenario.SimulatedAgents, RepresentativeScenario.SimulatedAgents)),
        ("backgroundActors", new RInt(RepresentativeScenario.BackgroundActors, RepresentativeScenario.BackgroundActors)),
        ("bonesPerNormalUnit", new RInt(RepresentativeScenario.BonesPerNormalUnit, RepresentativeScenario.BonesPerNormalUnit)),
        ("sunLights", new RInt(RepresentativeScenario.SunLights, RepresentativeScenario.SunLights)),
        ("localShadowLights", new RInt(RepresentativeScenario.LocalShadowLights, RepresentativeScenario.LocalShadowLights)),
        ("activeShadowPasses", new RInt(RepresentativeScenario.LocalShadowLights, RepresentativeScenario.LocalShadowLights)),
        ("particlePeak", new RInt(RepresentativeScenario.ParticlePeakTarget, RepresentativeScenario.ParticlePeakTarget)),
        ("shadowMapSizePx", new RInt(RepresentativeScenario.ShadowMapSizePixels, RepresentativeScenario.ShadowMapSizePixels)),
        ("framesPerSimTick", new RInt(RepresentativeScenario.FramesPerSimTick, RepresentativeScenario.FramesPerSimTick)));

    private static RObj CompositionObserved() => new(
        ("visibleUnitsRendered", Counted(RepresentativeScenario.VisibleUnitsTarget, int.MaxValue)),
        ("simulatedAgentsMapped", Counted(RepresentativeScenario.SimulatedAgents, RepresentativeScenario.SimulatedAgents)),
        ("backgroundActorsWritten", Counted(RepresentativeScenario.BackgroundActors, RepresentativeScenario.BackgroundActors)),
        ("paletteRowsBound", Counted(RepresentativeScenario.VisibleUnitsTarget, RepresentativeScenario.VisibleUnitsTarget)),
        ("sunLightsConfigured", Counted(RepresentativeScenario.SunLights, RepresentativeScenario.SunLights)),
        ("localShadowLightsWithActivePasses", Counted(RepresentativeScenario.LocalShadowLights, RepresentativeScenario.LocalShadowLights)),
        ("mainViewTrianglesDerived", Counted(1, int.MaxValue)));

    private static RObj Simulation() => new(
        ("contractDocument", new RLit(SimulationContract.DocumentPath)),
        ("contractVersion", new RLit(SimulationContract.ContractVersion)),
        ("numericModel", new RLit(SimulationContract.NumericModelId)),
        ("hashAlgorithm", new RLit(SimulationContract.HashAlgorithmId)),
        ("worldId", new RLit(SimulationContract.WorldId)),
        ("tickRateHz", new RInt(SimulationContract.TickRateHz, SimulationContract.TickRateHz)),
        ("agentCount", new RInt(SimulationContract.AgentCount, SimulationContract.AgentCount)),
        ("commandPlanAlgorithm", new RLit(SimulationContract.CommandPlanAlgorithmId)),
        ("commandPlanHash", Hex),
        ("commandCount", new RInt(1)),
        ("stateHashChain", new RObj(
            ("unit", new RLit("hex64")),
            ("method", new RLit(SimulationContract.HashAlgorithmId)),
            ("start", Hex),
            ("intervalSampleTicks", new RArr(new RInt(0), 1)),
            ("intervalHashes", new RArr(Hex, 1)),
            ("end", Hex))));

    private static FrameEvidenceAlternative FrameEvidenceShape() => new FrameEvidenceAlternative(
        new RObj(
            ("captured", new RBool(true)),
            ("afterMeasurementWindow", new RBool(true)),
            ("capturedAtFrameIndex", new RInt(1)),
            ("lastMeasuredFrameIndex", new RInt(0)),
            ("width", new RInt(BenchRunner.DefaultWidth, BenchRunner.DefaultWidth)),
            ("height", new RInt(BenchRunner.DefaultHeight, BenchRunner.DefaultHeight)),
            ("format", new RLit(FrameEvidence.FormatId)),
            ("sha256", new Sha256HexNode()),
            ("statementLimit", new RLit(FrameEvidence.StatementLimit))),
        new RObj(
            ("captured", new RBool(false)),
            ("reason", new RStr())));

    private sealed class FrameEvidenceAlternative(RObj capturedShape, RObj skippedShape) : ReportNode
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

            var shape = flag.GetBoolean() ? capturedShape : skippedShape;
            var before = errors.Count;
            shape.Check(path, element, errors);

            if (!flag.GetBoolean() || errors.Count > before)
            {
                return;
            }

            // Messfenster-Reihenfolge: Capture strikt nach dem letzten
            // gemessenen Frame (AC-T023-08).
            var capturedAt = element.GetProperty("capturedAtFrameIndex").GetInt32();
            var lastMeasured = element.GetProperty("lastMeasuredFrameIndex").GetInt32();

            if (capturedAt <= lastMeasured)
            {
                errors.Add($"{path}: Captureframe muss hinter dem Messfenster liegen.");
            }
        }
    }

    /// <summary>64-stelliger Kleinbuchstaben-SHA-256.</summary>
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
                var isLowerLetter = character is >= 'a' and <= 'f';

                if (!isDigit && !isLowerLetter)
                {
                    return false;
                }
            }

            return true;
        }
    }

    private static RObj BuildRoot() => new(
        ("schemaVersion", new RInt(CurrentVersion, CurrentVersion)),
        ("mode", new RLit(BenchReportSchema.ModeBench)),
        ("command", new RStr()),
        ("scenario", new RObj(
            ("id", new RLit(BenchScenarios.Representative)),
            ("seed", new RInt(0, uint.MaxValue)),
            ("resolution", new RObj(
                ("width", new RInt(BenchRunner.DefaultWidth, BenchRunner.DefaultWidth)),
                ("height", new RInt(BenchRunner.DefaultHeight, BenchRunner.DefaultHeight)))),
            ("displayProfile", new RLit("low")),
            ("vsync", new RBool(true)),
            ("content", new RLit(RepresentativeScenario.ContentId)))),
        ("compositionTargets", CompositionTargets()),
        ("cameraPath", new RObj(
            ("algorithm", new RLit(RepresentativeCameraFlight.AlgorithmId)),
            ("samples", new RInt(1)),
            ("hash", new RStr()),
            ("firstSample", new RObj(
                ("frameIndex", new RInt(0)),
                ("yawDegrees", new RStr()),
                ("pitchDegrees", new RStr()),
                ("radiusMeters", new RStr()),
                ("centerHeightMeters", new RStr()))))),
        ("startedAtUtc", new RStr()),
        ("finishedAtUtc", new RStr()),
        ("environment", new RObj(
            ("os", new RObj(("type", new RStr()), ("kernelRelease", new RStr()))),
            ("cpu", new RObj(("model", new RStr()))),
            ("gpu", new RObj(("renderer", new RStr()), ("vendorId", new RInt(0)), ("deviceId", new RInt(0)))),
            ("gl", new RObj(("version", new RStr()))),
            ("backend", new RObj(
                ("name", new RLit("OpenGL")),
                ("id", new RInt(BgfxDevice.RendererOpenGL, BgfxDevice.RendererOpenGL)),
                ("profile", new RLit("3.3 Core")),
                ("vsync", new RBool(true)))),
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
            ("warmupFrames", new RInt(1)),
            ("sampleFrames", new RInt(1)),
            ("framesRendered", new RInt(2)),
            ("warmupTicks", new RInt(0)),
            ("sampleTicks", new RInt(1)),
            ("rssSampleIntervalFrames", new RInt(1)),
            ("hashSampleIntervalTicks", new RInt(1)),
            ("measurementWindowMs", new RNum(true)))),
        ("metrics", Metrics()),
        ("simulation", Simulation()),
        ("compositionObserved", CompositionObserved()),
        ("gate", new RObj(
            ("limits", new RObj(
                ("p99FrameTimeMsMax", new RNum(true)),
                ("p99GpuTimeHardLimitMs", new RNum(true)),
                ("p99GpuTimeTargetMs", new RNum(true)),
                ("p99TickTimeHardLimitMs", new RNum(true)),
                ("p99TickTimeTargetMs", new RNum(true)),
                ("managedAllocationsPerWarmFrameBytesMax", new RNum(true)),
                ("drawSubmitCallsPerFrameMax", new RInt(0)),
                ("visibleTrianglesMainViewLimit", new RInt(0)),
                ("concurrentParticlesLimit", new RInt(0)),
                ("sunLightsMax", new RInt(0)),
                ("localShadowLightsMax", new RInt(0)),
                ("runtimeShaderCompilationAllowed", new RBool(false)),
                ("workingSetTargetMiB", new RInt(1)),
                ("workingSetHardLimitMiB", new RInt(1)))),
            ("pass", new RBool()),
            ("gpuTimeTargetMet", new RBool()),
            ("tickTimeTargetMet", new RBool()),
            ("rssTargetMet", new RBool()),
            ("violations", new RArr(new RStr())))),
        ("profiles", new RArr(new RObj(
            ("id", new RStr()),
            ("status", new RStr()),
            ("boundReferenceClass", new RNullableStr()),
            ("reason", new RStr())), 3)),
        ("baseline", new RObj(
            ("classification", new RLit("diagnostic-developer-workstation")),
            ("protocol", new RLit("qops001-2026-08-24")))),
        ("frameEvidence", FrameEvidenceShape()),
        ("exitCode", new RInt(int.MinValue, int.MaxValue)));

    /// <summary>Prueft einen Reporttext; Rueckgabe ist die Fehlerliste (leer == gueltig).</summary>
    public static IReadOnlyList<string> Validate(string json) =>
        BenchReportSchema.ValidateWith(Root, json);
}
