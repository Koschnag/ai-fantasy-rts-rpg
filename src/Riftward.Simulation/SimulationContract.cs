namespace Riftward.Simulation;

/// <summary>
/// Versionierte Kennungen und fixierte Vertragswerte der headless
/// Simulationsbaseline (T-021). Jede Kennung ist in
/// <c>docs/SIMULATIONSVERTRAG.md</c> (Abschnitt 0, gatender Vertragsspike)
/// mit Alternativen, Gruenden und Rueckrollweg dokumentiert. Die Werte hier
/// sind die maschinenlesbare Spiegelung des Vertrags; ein Test haelt beide
/// Seiten konsistent.
/// </summary>
public static class SimulationContract
{
    /// <summary>Pfad des versionierenden Vertragsdokuments.</summary>
    public const string DocumentPath = "docs/SIMULATIONSVERTRAG.md";

    /// <summary>Vertragsversion des Dokuments (Schema-/Contentkennung fuer Fixtures).</summary>
    public const string ContractVersion = "1";

    /// <summary>Kennung der synthetischen Benchmarkwelt ohne Spielinhalt.</summary>
    public const string WorldId = "riftward-simworld-graybox-v1";

    /// <summary>Numerikmodell: reine Ganzzahl-Festkommaarithmetik Q16.16.</summary>
    public const string NumericModelId = "q16-16-fixed-point-intonly-v1";

    /// <summary>Hashalgorithmus ueber den kanonischen sim-relevanten Zustand.</summary>
    public const string HashAlgorithmId = "fnv1a64-canonical-chain-v1";

    /// <summary>Deterministischer Befehlsplan (Gruppenziele aus dem Seed abgeleitet).</summary>
    public const string CommandPlanAlgorithmId = "xorshift64star-group-script-v1";

    /// <summary>Feste Simulationstickrate (PERFORMANCE_BUDGET.md: 20 Hz).</summary>
    public const int TickRateHz = 20;

    /// <summary>Gleichzeitig vollstaendig simulierte mobile Testagenten.</summary>
    public const int AgentCount = 250;

    /// <summary>Anzahl Gruppen des Befehlsplans.</summary>
    public const int GroupCount = 5;

    /// <summary>Hierarchischer Pfadhaushalt: Knotenerweiterungen je Anfrageabschnitt.</summary>
    public const int PathExpansionBudgetPerAgentTick = 768;

    /// <summary>Hierarchischer Pfadhaushalt: globale Knotenerweiterungen je Tick.</summary>
    public const int PathGlobalExpansionBudgetPerTick = 2048;

    /// <summary>
    /// Allokationsgrenze je warmem Tick in verwalteten Bytes (Abschnitt 0:
    /// auf null verschaeft gegenueber der 1-KiB-Obergrenze des Auftrags).
    /// </summary>
    public const long AllocationLimitBytesPerWarmTick = 0;

    /// <summary>Zieltickzeit in Millisekunden (PERFORMANCE_BUDGET.md).</summary>
    public const double P99TickTimeTargetMs = 8.0;

    /// <summary>Harte Tickzeitgrenze in Millisekunden (PERFORMANCE_BUDGET.md).</summary>
    public const double P99TickTimeHardLimitMs = 16.0;
}
