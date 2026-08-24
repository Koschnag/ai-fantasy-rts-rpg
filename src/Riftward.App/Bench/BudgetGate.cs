namespace Riftward.App.Bench;

/// <summary>
/// Dokumentierte Grenzwerte des BENCH-EMPTY-Budgetgates (AC-T020-04). Die
/// Werte sind ausschliesslich aus docs/PERFORMANCE_BUDGET.md und AC-T010-07
/// geerbt; jede Aenderung benoetigt eine dokumentierte Entscheidung.
/// </summary>
public sealed record BenchBudgetLimits(
    double P99FrameTimeLimitMs = 33.3,
    double ManagedAllocationsPerWarmFrameLimitBytes = 1024.0,
    long DrawSubmitCallsPerFrameLimit = 8,
    long RssTargetMiB = 300,
    long RssHardLimitMiB = 450)
{
    public static BenchBudgetLimits Documented { get; } = new();
}

/// <summary>Messwerte, gegen die das Budgetgate fail-closed entscheidet.</summary>
public sealed record BenchBudgetInputs(
    double P99FrameTimeMs,
    double ManagedAllocationsPerWarmFrameBytes,
    long DrawSubmitCallsPerFrameMax,
    bool RuntimeShaderCompilationObserved,
    long? RssMinKiB,
    long? RssMaxKiB,
    long? RssEndKiB);

/// <summary>Ergebnis der Gateauswertung inklusive maschinenlesbarer Verletzungsklassen.</summary>
public sealed record BenchBudgetVerdict(
    bool Pass,
    bool RssTargetMet,
    IReadOnlyList<string> Violations);

/// <summary>
/// Fail-closed-Evaluator: Nicht messbare oder ungueltige Eingaben zaehlen als
/// Verletzung. Es werden ausschliesslich die dokumentierten Grenzwerte
/// geprueft; das Gate kann nichts lockern oder umgehen.
/// </summary>
public static class BudgetGate
{
    public static BenchBudgetVerdict Evaluate(BenchBudgetLimits limits, BenchBudgetInputs inputs)
    {
        var violations = new List<string>();

        if (!IsFiniteNonNegative(inputs.P99FrameTimeMs) || inputs.P99FrameTimeMs > limits.P99FrameTimeLimitMs)
        {
            violations.Add($"p99-frame-time-ms:{Format(inputs.P99FrameTimeMs)}>{limits.P99FrameTimeLimitMs}");
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
        else
        {
            var maxMiB = inputs.RssMaxKiB.Value / 1024;

            if (maxMiB > limits.RssHardLimitMiB)
            {
                violations.Add($"working-set-max-mib:{maxMiB}>{limits.RssHardLimitMiB}");
            }
        }

        var rssTargetMet = inputs.RssMaxKiB is > 0 && inputs.RssMaxKiB.Value / 1024 <= limits.RssTargetMiB;
        return new BenchBudgetVerdict(violations.Count == 0, rssTargetMet, violations);
    }

    private static bool IsFiniteNonNegative(double value) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0.0;

    private static string Format(double value) =>
        double.IsNaN(value) || double.IsInfinity(value) ? value.ToString(System.Globalization.CultureInfo.InvariantCulture) : TelemetryMath.Canonical(value);
}
