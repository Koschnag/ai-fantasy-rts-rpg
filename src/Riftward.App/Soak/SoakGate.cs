namespace Riftward.App.Soak;

using Riftward.App.Bench;

/// <summary>
/// Dokumentierte Grenzwerte des Soakgates (AC-T022-06); gespiegelt aus
/// <c>docs/SOAKVERTRAG.md</c> V1 beziehungsweise unveraenderlich gebunden an
/// den Simulationsvertrag V1 Abschnitt 5. Jede Aenderung benoetigt eine neue
/// Vertragsversion; Lockerungen eskalieren an die Projektleitung.
/// </summary>
public sealed record SoakBudgetLimits(
    double AbsoluteGrowthLimitMiB,
    double TrendLimitKiBPerHour,
    long AllocationsPerWarmTickLimitBytes,
    double WatchdogWindowSeconds)
{
    public static SoakBudgetLimits Documented { get; } = new(
        SoakContract.AbsoluteGrowthLimitMiB,
        SoakContract.TrendLimitKiBPerHour,
        SoakContract.AllocationLimitBytesPerWarmTick,
        SoakContract.WatchdogWindowSeconds);
}

/// <summary>Messwerte und Vollstaendigkeitskennungen der Gateentscheidung.</summary>
public sealed record SoakGateInputs(
    bool RssMeasured,
    double AbsoluteGrowthMiB,
    double TrendDeltaKiBPerHour,
    double StrictAllocationsPerTickBytes,
    bool ChainMatched,
    bool StallDetected,
    bool Paced,
    double WallSeconds,
    long TicksExecuted,
    long RequiredTicks,
    double RequiredWallSeconds);

/// <summary>Ergebnis der Gateauswertung mit Verletzungsklassen.</summary>
public sealed record SoakVerdict(
    bool Pass,
    bool AbsoluteGrowthMet,
    bool TrendMet,
    bool AllocationBudgetMet,
    bool ChainIntact,
    IReadOnlyList<string> Violations);

/// <summary>
/// Fail-closed-Evaluator des Soakgates: nicht messbare oder ungueltige
/// Eingaben zaehlen als Verletzung; entschieden wird ausschliesslich gegen
/// die dokumentierten absoluten Grenzwerte des Soakvertrags. Die
/// fensterweise Tickzeitdrift geht zu keinem Zeitpunkt in diese Entscheidung
/// ein; die tolerierte Benchmarkstreuung (Q-TEC-010) wird nicht beruehrt.
/// </summary>
public static class SoakGate
{
    public static SoakVerdict Evaluate(SoakBudgetLimits limits, SoakGateInputs inputs)
    {
        var violations = new List<string>();
        var rssMeasured = inputs.RssMeasured;
        var absoluteGrowthMet = false;
        var trendMet = false;
        var allocationBudgetMet = false;
        var chainIntact = inputs.ChainMatched;

        // Konsistenzbedingung des Vertrags (Abschnitt 1): fail-closed vor jeder Bewertung.
        if (!TrendIsConsistent(limits))
        {
            violations.Add(
                $"trend-consistency:{Format(limits.TrendLimitKiBPerHour)}KiBh-x8h>{Format(limits.AbsoluteGrowthLimitMiB * 1024)}KiB");
        }

        if (!rssMeasured)
        {
            violations.Add("working-set-unmeasured:rss-sampler-unavailable");
        }
        else
        {
            absoluteGrowthMet = IsFiniteNonNegative(inputs.AbsoluteGrowthMiB)
                && inputs.AbsoluteGrowthMiB <= limits.AbsoluteGrowthLimitMiB;

            if (!absoluteGrowthMet)
            {
                violations.Add($"absolute-growth-mib:{Format(inputs.AbsoluteGrowthMiB)}>{limits.AbsoluteGrowthLimitMiB}");
            }

            trendMet = IsFinite(inputs.TrendDeltaKiBPerHour)
                && inputs.TrendDeltaKiBPerHour <= limits.TrendLimitKiBPerHour;

            if (!trendMet)
            {
                violations.Add($"trend-ki-b-per-hour:{Format(inputs.TrendDeltaKiBPerHour)}>{limits.TrendLimitKiBPerHour}");
            }
        }

        allocationBudgetMet = IsFiniteNonNegative(inputs.StrictAllocationsPerTickBytes)
            && inputs.StrictAllocationsPerTickBytes <= limits.AllocationsPerWarmTickLimitBytes;

        if (!allocationBudgetMet)
        {
            violations.Add(
                $"managed-allocations-per-warm-tick-bytes:{Format(inputs.StrictAllocationsPerTickBytes)}>{limits.AllocationsPerWarmTickLimitBytes}");
        }

        if (!chainIntact)
        {
            violations.Add("state-hash-chain-mismatch:golden-fixture");
        }

        if (inputs.StallDetected)
        {
            violations.Add($"watchdog-progress-stall:{limits.WatchdogWindowSeconds}s-window");
        }

        if (inputs.Paced)
        {
            if (inputs.TicksExecuted != inputs.RequiredTicks)
            {
                violations.Add($"tick-count:{inputs.TicksExecuted}!={inputs.RequiredTicks}");
            }

            if (!IsFiniteNonNegative(inputs.WallSeconds) || inputs.WallSeconds < inputs.RequiredWallSeconds)
            {
                violations.Add($"wall-duration-seconds:{Format(inputs.WallSeconds)}<{inputs.RequiredWallSeconds}");
            }
        }

        return new SoakVerdict(
            Pass: violations.Count == 0,
            AbsoluteGrowthMet: absoluteGrowthMet,
            TrendMet: trendMet,
            AllocationBudgetMet: allocationBudgetMet,
            ChainIntact: chainIntact,
            Violations: violations);
    }

    /// <summary>Prueft die vertragliche Konsistenzbedingung Trendschwelle mal 8 h kleiner gleich absoluter Schwelle.</summary>
    public static bool TrendIsConsistent(SoakBudgetLimits limits) =>
        double.IsFinite(limits.TrendLimitKiBPerHour)
        && double.IsFinite(limits.AbsoluteGrowthLimitMiB)
        && (limits.TrendLimitKiBPerHour * 8.0 * 1024.0) <= (limits.AbsoluteGrowthLimitMiB * 1024.0 * 1024.0);

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    private static bool IsFiniteNonNegative(double value) =>
        IsFinite(value) && value >= 0.0;

    private static string Format(double value) =>
        double.IsNaN(value) || double.IsInfinity(value)
            ? value.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : TelemetryMath.Canonical(value);
}

/// <summary>
/// Reine Abbildung des Beendigungszustands auf den dokumentierten Exitcode
/// (docs/NATIVE_UNTERBAU.md): ein Watchdog-Stall und jede Gateverletzung
/// ergeben 30 bei trotzdem geschriebenem, nicht bestandenem Report; jeder
/// andere Unvollstaendigkeitsgrund ergibt 31 mit einem Teilreport ohne
/// Evidenz; ein vollstaendiger bestandener Lauf ergibt 0.
/// </summary>
internal static class SoakExitMapping
{
    public static int Map(bool stallDetected, bool complete, bool pass)
    {
        if (!complete && !stallDetected)
        {
            return Platform.PlatformErrorCode.SoakRunIncomplete.AsExitCode();
        }

        return pass
            ? Platform.ExitCodes.Ok
            : Platform.PlatformErrorCode.SoakGateViolated.AsExitCode();
    }
}

/// <summary>Entscheidung der Evidenzeinheiten-Klassifikation (Soakvertrag V2).</summary>
public sealed record SoakEvidenceDecision(bool IsUnit, string? Reason);

/// <summary>
/// Reine Klassifikation einer Evidenzeinheit des Soakvertrags V2
/// (Projektleitungsentscheidung 2026-08-25): nur ein vollständiger Lauf ueber
/// den kompletten Planhorizont in Release-naher Konfiguration mit intakter
/// Golden-Fixture-Kette im ausdruecklichen Wiederholungsmodus und bestandenem
/// fail-closed Gate ist eine Evidenzeinheit. Eine Referenzemission kann sich
/// nicht selbst bestaetigen und bleibt immer diagnostisch; ebenso werden jede
/// Verkuerzung, Unvollstaendigkeit oder Abweichung maschinenlesbar mit Grund
/// abgewiesen. Keine Uhr-, I/O- oder Zustandsabhaengigkeit.
/// </summary>
public static class SoakEvidenceUnit
{
    public const string ReferenceEmissionDiagnosticReason =
        "golden-fixture-reference-emission-diagnostic";

    public static SoakEvidenceDecision Decide(
        string executionModeId,
        bool fullHorizon,
        bool complete,
        bool releaseBuild,
        bool goldenFixtureCompared,
        bool chainMatched,
        bool gatePass,
        string? incompleteReason)
    {
        if (!goldenFixtureCompared
            || string.Equals(
                executionModeId,
                SoakContract.ReferenceEmissionDiagnosticModeId,
                StringComparison.Ordinal))
        {
            return new SoakEvidenceDecision(false, ReferenceEmissionDiagnosticReason);
        }

        if (!fullHorizon)
        {
            return new SoakEvidenceDecision(false, "horizon-shortened-diagnostic");
        }

        if (!complete)
        {
            return new SoakEvidenceDecision(false, $"incomplete:{incompleteReason ?? "unbekannt"}");
        }

        if (!releaseBuild)
        {
            return new SoakEvidenceDecision(false, "non-release-build");
        }

        if (!string.Equals(
            executionModeId,
            SoakContract.AcceleratedEvidenceModeId,
            StringComparison.Ordinal))
        {
            return new SoakEvidenceDecision(false, "execution-mode-diagnostic");
        }

        if (!chainMatched)
        {
            return new SoakEvidenceDecision(false, "state-hash-chain-mismatch");
        }

        return gatePass
            ? new SoakEvidenceDecision(true, null)
            : new SoakEvidenceDecision(false, "gate-violated");
    }
}

file static class ExitCodeExtensions
{
    public static int AsExitCode(this Platform.PlatformErrorCode code) => Platform.ExitCodes.Map(code);
}
