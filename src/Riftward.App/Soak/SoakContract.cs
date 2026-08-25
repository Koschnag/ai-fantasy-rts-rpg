namespace Riftward.App.Soak;

/// <summary>
/// Versionierte Kennungen und fixierte Werte des Soakvertrags (T-022,
/// Abschnitt 0). Jede Wahl ist in <c>docs/SOAKVERTRAG.md</c> mit
/// Ableitungsbasis, Alternativen, Gruenden und Rueckrollweg dokumentiert;
/// ein Test haelt beide Seiten konsistent. Die tolerierte Benchmarkstreuung
/// (Rest von Q-TEC-010) wird an keiner Stelle definiert oder verbraucht.
/// </summary>
public static class SoakContract
{
    /// <summary>Pfad des versionierenden Vertragsdokuments.</summary>
    public const string DocumentPath = "docs/SOAKVERTRAG.md";

    /// <summary>Vertragsversion des Dokuments (Schema-/Contentkennung).</summary>
    public const string ContractVersion = "2";

    /// <summary>Szenario-ID des autoritativen Soaks.</summary>
    public const string ReplayScenarioId = "soak-replay";

    /// <summary>Kennung des Realzeit-Taktmodus (diagnostischer Dauermodule unter Vertrag V2).</summary>
    public const string RealtimeAuthoritativeModeId = "realtime-authoritative-v1";

    /// <summary>Kennung des beschleunigten Diagnosemodus mit verkuerztem Horizont.</summary>
    public const string AcceleratedDiagnosticModeId = "accelerated-diagnostic-v1";

    /// <summary>
    /// Kennung einer Golden-Fixture-Emission. Die Emission erzeugt erst den
    /// Vergleichsanker und ist daher unabhaengig vom Horizont niemals Evidenz.
    /// </summary>
    public const string ReferenceEmissionDiagnosticModeId =
        "accelerated-reference-emission-diagnostic-v1";

    /// <summary>Kennung des beschleunigten Wiederholungsmodus ueber den kompletten Planhorizont (Vertrag V2).</summary>
    public const string AcceleratedEvidenceModeId = "accelerated-repetition-evidence-v2";

    /// <summary>
    /// Mindestanzahl unabhaengiger Fresh-Prozess-Wiederholungslaeufe des
    /// kompletten Planhorizonts im Evidenzbuendel des Soakvertrags V2
    /// (Projektleitungsentscheidung 2026-08-25).
    /// </summary>
    public const int MinimumEvidenceRepetitions = 3;

    /// <summary>Kennung einer einzelnen Evidenzeinheit des Vertrags V2.</summary>
    public const string EvidenceUnitId = "deterministic-full-plan-repetition-v2";

    /// <summary>Absolutes Wachstumsziel der Speicherkennzahl in MiB (Vertrag Abschnitt 1).</summary>
    public const double AbsoluteGrowthLimitMiB = 16.0;

    /// <summary>Trendgrenzwert letzte gegen erste Stunde, KiB je Stunde (Vertrag Abschnitt 1).</summary>
    public const double TrendLimitKiBPerHour = 1024.0;

    /// <summary>Konsistenzbedingung: Trendschwelle mal 8 h kleiner gleich absoluter Schwelle (KiB-Basis).</summary>
    public static bool TrendConsistencyHolds =>
        (TrendLimitKiBPerHour * 8.0 * 1024.0)
            <= AbsoluteGrowthLimitMiB * 1024.0 * 1024.0;

    /// <summary>Watchdogfenster in Sekunden (Vertrag Abschnitt 2, Band 30 bis 300).</summary>
    public const double WatchdogWindowSeconds = 120.0;

    /// <summary>Fenstergroesse der Erfassung in Sekunden (Vertrag Abschnitt 3).</summary>
    public const int WindowSeconds = 30;

    /// <summary>
    /// Allokationsgrenze je warmem Tick; unveraenderlich an Simulationsvertrag
    /// V1 Abschnitt 5 gebunden (0 Bytes).
    /// </summary>
    public const long AllocationLimitBytesPerWarmTick =
        Riftward.Simulation.SimulationContract.AllocationLimitBytesPerWarmTick;

    /// <summary>Anzahl Ticks der strengen Per-Tick-Allokationspruefung je Pruefburst (Vertrag Abschnitt 3).</summary>
    public const int StrictAllocationVerificationTicks = 1200;

    /// <summary>Maschinenlesbare Aussage zur offenen Benchmarkstreuung (AC-T022-08).</summary>
    public const string Qtec010Statement =
        "tolerated-benchmark-variance-qtec010-remains-open-not-defined-not-consumed-in-this-task";

    /// <summary>Diagnosekennzeichnung der Driftfelder: keine Gatekopplung.</summary>
    public const bool DiagnosticDriftGateCoupled = false;

    /// <summary>Standardseed der Baseline (Praezedenz T-020/T-021).</summary>
    public const uint DefaultSeed = 20260824u;

    /// <summary>Mindesthorizont eines beschleunigten Diagnoselaufs (ueberdeckt Planbeginn).</summary>
    public const long MinAcceleratedHorizonTicks = 600;

    /// <summary>Vertragliche Kalibrierreferenz (Ableitungsbasis, Vertrag Abschnitt 0).</summary>
    public const string CalibrationReference = "calibration-run-a+calibration-run-b@1800s-each-2026-08-25";
}
