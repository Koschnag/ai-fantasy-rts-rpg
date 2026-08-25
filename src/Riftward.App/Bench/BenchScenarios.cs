namespace Riftward.App.Bench;

/// <summary>
/// Oeffentlicher Szenariokatalog des bench-Befehls (T-020/T-021/T-023).
/// Implementiert sind <see cref="Empty"/> (leere Renderer-Szene),
/// <see cref="Sim"/> (headless Simulationsbaseline) und
/// <see cref="Representative"/> (integrierter Belastungsframe); die uebrigen
/// Pflichtszenarien sind registriert und schlagen mit definiertem Exitcode
/// explizit fehl, statt einen kosmetischen Gruenerfolg zu liefern.
/// </summary>
public static class BenchScenarios
{
    public const string Empty = "bench-empty";
    public const string Sim = "bench-sim";
    public const string Representative = "bench-representative";
    public const string Army = "bench-army";
    public const string Battle = "bench-battle";
    public const string Base = "bench-base";
    public const string Path = "bench-path";
    public const string Load = "bench-load";

    /// <summary>Alle bekannten Szenario-IDs in berichteter Reihenfolge.</summary>
    public static readonly IReadOnlyList<string> Known =
    [
        Empty,
        Sim,
        Representative,
        Army,
        Battle,
        Base,
        Path,
        Load,
    ];

    public enum Support
    {
        /// <summary>Szenario existiert und ist in diesem Auftrag umgesetzt.</summary>
        Implemented,

        /// <summary>Szenario ist als Pflichtbenchmark bekannt, aber noch nicht implementiert.</summary>
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

        if (string.Equals(scenarioId, Empty, StringComparison.Ordinal)
            || string.Equals(scenarioId, Sim, StringComparison.Ordinal)
            || string.Equals(scenarioId, Representative, StringComparison.Ordinal))
        {
            return Support.Implemented;
        }

        return Known.Contains(scenarioId, StringComparer.Ordinal)
            ? Support.RegisteredNotImplemented
            : Support.Unknown;
    }
}
