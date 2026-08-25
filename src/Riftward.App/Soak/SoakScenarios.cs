namespace Riftward.App.Soak;

/// <summary>
/// Oeffentlicher Szenariokatalog des soak-Befehls (T-022). Implementiert sind
/// <see cref="Replay"/> (deterministischer Replay-Soak) und
/// <see cref="Calibration"/> (Abschnitt-0-Kalibrierung des gatenden
/// Vertragsspikes, rein diagnostisch). Unbekannte oder noch nicht
/// implementierte Szenarien brechen mit definiertem Exitcode ab, ohne einen
/// Report vorzutaeuschen.
/// </summary>
public static class SoakScenarios
{
    public const string Replay = "soak-replay";
    public const string Calibration = "soak-calibration";

    /// <summary>Alle bekannten Szenario-IDs in berichteter Reihenfolge.</summary>
    public static readonly IReadOnlyList<string> Known =
    [
        Replay,
        Calibration,
    ];

    public enum Support
    {
        /// <summary>Szenario existiert und ist umgesetzt.</summary>
        Implemented,

        /// <summary>Szenario ist bekannt, aber noch nicht implementiert.</summary>
        RegisteredNotImplemented,

        /// <summary>Szenario-ID ist unbekannt.</summary>
        Unknown,
    }

    public static Support Classify(string? scenarioId)
    {
        if (string.IsNullOrEmpty(scenarioId))
        {
            return Support.Unknown;
        }

        if (string.Equals(scenarioId, Replay, StringComparison.Ordinal)
            || string.Equals(scenarioId, Calibration, StringComparison.Ordinal))
        {
            return Support.Implemented;
        }

        return Known.Contains(scenarioId, StringComparer.Ordinal)
            ? Support.RegisteredNotImplemented
            : Support.Unknown;
    }
}
