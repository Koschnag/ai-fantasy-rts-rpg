namespace Riftward.App.Bench;

/// <summary>Messwerte, gegen die das integrierte Budgetgate entscheidet.</summary>
public sealed record RepresentativeBudgetInputs(
    double P99FrameTimeMs,
    double P99GpuTimeMs,
    bool GpuTimeMeasured,
    double P99TickTimeMs,
    double ManagedAllocationsPerWarmFrameBytes,
    long DrawSubmitCallsPerFrameMax,
    long VisibleTrianglesMainViewMax,
    long ConcurrentParticlesObserved,
    bool RuntimeShaderCompilationObserved,
    long? RssMinKiB,
    long? RssMaxKiB,
    long? RssEndKiB);

/// <summary>Ergebnis der Gateauswertung mit Zielausweisung je Kennzahl.</summary>
public sealed record RepresentativeBudgetVerdict(
    bool Pass,
    bool GpuTimeTargetMet,
    bool TickTimeTargetMet,
    bool RssTargetMet,
    IReadOnlyList<string> Violations);

/// <summary>
/// Fail-closed-Evaluator des integrierten Budgetgates (AC-T023-04): nicht
/// messbare oder ungueltige Eingaben zaehlen als Verletzung; entschieden
/// wird ausschliesslich gegen die dokumentierten Grenzwerte aus
/// docs/PERFORMANCE_BUDGET.md, dem AC-T010-07/T-020/T-021-Praezedenz und
/// der Szenebudgettabelle. Ziele (GPU 14 ms, Tick 8 ms, RSS-Zielzeile)
/// werden getrennt ausgewiesen und falten das Gate allein nicht; harte
/// Grenzen sind verbindlich.
/// </summary>
public static class RepresentativeBudgetGate
{
    public static RepresentativeBudgetVerdict Evaluate(
        RepresentativeScenario.BudgetLimits limits,
        RepresentativeBudgetInputs inputs)
    {
        var violations = new List<string>();

        if (!IsFiniteNonNegative(inputs.P99FrameTimeMs) || inputs.P99FrameTimeMs > limits.P99FrameTimeLimitMs)
        {
            violations.Add($"p99-frame-time-ms:{Format(inputs.P99FrameTimeMs)}>{limits.P99FrameTimeLimitMs}");
        }

        if (!inputs.GpuTimeMeasured
            || !IsFiniteNonNegative(inputs.P99GpuTimeMs)
            || inputs.P99GpuTimeMs > limits.P99GpuTimeHardLimitMs)
        {
            var measured = inputs.GpuTimeMeasured ? Format(inputs.P99GpuTimeMs) : "not-measurable";
            violations.Add($"p99-gpu-time-ms:{measured}>{limits.P99GpuTimeHardLimitMs}");
        }

        if (!IsFiniteNonNegative(inputs.P99TickTimeMs) || inputs.P99TickTimeMs > limits.P99TickTimeHardLimitMs)
        {
            violations.Add($"p99-tick-time-ms:{Format(inputs.P99TickTimeMs)}>{limits.P99TickTimeHardLimitMs}");
        }

        if (!IsFiniteNonNegative(inputs.ManagedAllocationsPerWarmFrameBytes)
            || inputs.ManagedAllocationsPerWarmFrameBytes > limits.ManagedAllocationsPerWarmFrameLimitBytes)
        {
            violations.Add(
                $"managed-allocations-per-warm-frame-bytes:{Format(inputs.ManagedAllocationsPerWarmFrameBytes)}>{limits.ManagedAllocationsPerWarmFrameLimitBytes}");
        }

        if (inputs.DrawSubmitCallsPerFrameMax < 0 || inputs.DrawSubmitCallsPerFrameMax > limits.DrawSubmitCallsPerFrameLimit)
        {
            violations.Add($"draw-submit-per-frame:{inputs.DrawSubmitCallsPerFrameMax}>{limits.DrawSubmitCallsPerFrameLimit}");
        }

        if (inputs.VisibleTrianglesMainViewMax < 0
            || inputs.VisibleTrianglesMainViewMax > limits.VisibleTrianglesMainViewLimit)
        {
            violations.Add(
                $"visible-triangles-main-view:{inputs.VisibleTrianglesMainViewMax}>{limits.VisibleTrianglesMainViewLimit}");
        }

        if (inputs.ConcurrentParticlesObserved < 0 || inputs.ConcurrentParticlesObserved > limits.ConcurrentParticlesLimit)
        {
            violations.Add(
                $"concurrent-particles:{inputs.ConcurrentParticlesObserved}>{limits.ConcurrentParticlesLimit}");
        }

        if (inputs.RuntimeShaderCompilationObserved)
        {
            violations.Add("runtime-shader-compilation:observed");
        }

        if (inputs.RssMinKiB is null || inputs.RssMaxKiB is null || inputs.RssEndKiB is null
            || inputs.RssMinKiB <= 0 || inputs.RssMaxKiB <= 0 || inputs.RssEndKiB <= 0
            || inputs.RssMinKiB > inputs.RssMaxKiB)
        {
            violations.Add("working-set-kib:not-measurable");
        }
        else if ((long)(inputs.RssMaxKiB.Value / 1024) > limits.WorkingSetHardLimitMiB)
        {
            violations.Add(
                $"working-set-max-mib:{inputs.RssMaxKiB.Value / 1024}>{limits.WorkingSetHardLimitMiB}");
        }

        var gpuTargetMet = inputs.GpuTimeMeasured
            && IsFiniteNonNegative(inputs.P99GpuTimeMs)
            && inputs.P99GpuTimeMs <= limits.P99GpuTimeTargetMs;

        var tickTargetMet = IsFiniteNonNegative(inputs.P99TickTimeMs)
            && inputs.P99TickTimeMs <= limits.P99TickTimeTargetMs;

        var rssTargetMet = inputs.RssMaxKiB is > 0
            && (long)(inputs.RssMaxKiB.Value / 1024) <= limits.WorkingSetTargetMiB;

        return new RepresentativeBudgetVerdict(
            violations.Count == 0,
            gpuTargetMet,
            tickTargetMet,
            rssTargetMet,
            violations);
    }

    private static bool IsFiniteNonNegative(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0.0;

    private static string Format(double value) =>
        double.IsNaN(value) || double.IsInfinity(value)
            ? value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : TelemetryMath.Canonical(value);
}
