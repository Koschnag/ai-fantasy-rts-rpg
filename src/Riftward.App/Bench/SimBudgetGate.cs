using Riftward.Simulation;

namespace Riftward.App.Bench;

/// <summary>
/// Dokumentierte Grenzwerte des bench-sim-Budgetgates (AC-T021-05). Die
/// Werte stammen ausschliesslich aus docs/PERFORMANCE_BUDGET.md (8 ms Ziel,
/// 16 ms harte Grenze bei 20 Hz) und dem Abschnitt-0-Spike
/// (docs/SIMULATIONSVERTRAG.md, Allokationsgrenze 0 Bytes je warmem Tick
/// innerhalb der Auftragsobergrenze von 1 KiB). Jede Aenderung benoetigt
/// eine dokumentierte Entscheidung; Lockerungen eskalieren.
/// </summary>
public sealed record SimBudgetLimits(
    double P99TickTimeHardLimitMs,
    double P99TickTimeTargetMs,
    long AllocationsPerWarmTickLimitBytes)
{
    public static SimBudgetLimits Documented { get; } = new(
        SimulationContract.P99TickTimeHardLimitMs,
        SimulationContract.P99TickTimeTargetMs,
        SimulationContract.AllocationLimitBytesPerWarmTick);
}

/// <summary>Messwerte, gegen die das Simulationsbudgetgate entscheidet.</summary>
public sealed record SimBudgetInputs(
    double P99TickTimeMs,
    double ManagedAllocationsPerWarmTickBytes);

/// <summary>Ergebnis der Gateauswertung mit Verletzungsklassen und Zielausweisung.</summary>
public sealed record SimBudgetVerdict(
    bool Pass,
    bool P99TargetMet,
    IReadOnlyList<string> Violations);

/// <summary>
/// Fail-closed-Evaluator des Simulationsbudgetgates: nicht messbare oder
/// ungueltige Eingaben zaehlen als Verletzung; entschieden wird
/// ausschliesslich gegen die dokumentierten Grenzwerte. Das 8-ms-Ziel wird
/// ausgewiesen, seine Verfehlung allein faltet das Gate nicht (AC-T010-07-
/// Praezedenz); die harte 16-ms-Grenze ist verbindlich.
/// </summary>
public static class SimBudgetGate
{
    public static SimBudgetVerdict Evaluate(SimBudgetLimits limits, SimBudgetInputs inputs)
    {
        var violations = new List<string>();

        if (!IsFiniteNonNegative(inputs.P99TickTimeMs)
            || inputs.P99TickTimeMs > limits.P99TickTimeHardLimitMs)
        {
            violations.Add($"p99-tick-time-ms:{Format(inputs.P99TickTimeMs)}>{limits.P99TickTimeHardLimitMs}");
        }

        if (!IsFiniteNonNegative(inputs.ManagedAllocationsPerWarmTickBytes)
            || inputs.ManagedAllocationsPerWarmTickBytes > limits.AllocationsPerWarmTickLimitBytes)
        {
            violations.Add(
                $"managed-allocations-per-warm-tick-bytes:{Format(inputs.ManagedAllocationsPerWarmTickBytes)}>{limits.AllocationsPerWarmTickLimitBytes}");
        }

        var targetMet = IsFiniteNonNegative(inputs.P99TickTimeMs)
            && inputs.P99TickTimeMs <= limits.P99TickTimeTargetMs;

        return new SimBudgetVerdict(violations.Count == 0, targetMet, violations);
    }

    private static bool IsFiniteNonNegative(double value) =>
        !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0.0;

    private static string Format(double value) =>
        double.IsNaN(value) || double.IsInfinity(value)
            ? value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : TelemetryMath.Canonical(value);
}
