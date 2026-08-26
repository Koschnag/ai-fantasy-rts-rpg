namespace Riftward.Save;

/// <summary>
/// Versionierte Kennungen und fixierte Werte des Savevertrags (T-031,
/// Abschnitt 0). Jede Wahl ist in <c>docs/SAVEVERTRAG.md</c> mit
/// Alternativen, Gruenden und Rueckrollweg dokumentiert; ein Test haelt
/// beide Seiten konsistent. Der Vertrag entscheidet Q-TEC-006 nur im
/// Teilaspekt Save-Umschlag/Persistenzformat des Simulationszustands;
/// Cooked-Paket-, Definitions- und Replayformate bleiben OFFEN.
/// </summary>
public static class SaveContract
{
    /// <summary>Pfad des versionierenden Vertragsdokuments.</summary>
    public const string DocumentPath = "docs/SAVEVERTRAG.md";

    /// <summary>Vertragsversion des Dokuments.</summary>
    public const string ContractVersion = "1";

    /// <summary>Kennung der kanonischen Binärcodierung.</summary>
    public const string EncodingId = "riftward-save-canonical-binary-v1";

    /// <summary>Dateimagie (Bytes) eines Saves V1.</summary>
    public const byte Magic0 = (byte)'R';
    public const byte Magic1 = (byte)'W';
    public const byte Magic2 = (byte)'S';
    public const byte Magic3 = (byte)'D';

    /// <summary>Einzige Produkt-Schemaversion dieses Vertrags.</summary>
    public const ushort CurrentSaveSchemaVersion = 1;

    /// <summary>Länge des Dateimagics in Bytes.</summary>
    public const int MagicLength = 4;

    /// <summary>Länge eines SHA-256-Ankers in Bytes.</summary>
    public const int HashLength = 32;

    /// <summary>Länge des opaken saveId-Metadatums in Bytes.</summary>
    public const int SaveIdLength = 16;

    /// <summary>
    /// Fester Bytestrang je Agent vor dem variablen Wegpunktschwanz:
    /// 4×i64 Position/Geschwindigkeit, i32 Zielkachel, u8 Gruppe,
    /// u8 Pfadstatus, i16 geplante Zone, i32 Cursor, i32 Anzahl.
    /// </summary>
    public const int AgentStrideBytes = 48;

    /// <summary>Fester Payloadkopf: Tickindex, Seed, fünf Gruppenziele.</summary>
    public const int PayloadFixedPrefixBytes = 8 + 4 + (5 * 4);

    /// <summary>Obergrenze der Kopflänge als Framingschutz vor Zuweisungen.</summary>
    public const int MaxHeaderBytes = 4096;

    /// <summary>Absolutes Vorab-Größenlimit (DoS-Grenze) in Bytes.</summary>
    public const long AbsoluteMaxSaveBytes = 64L * 1024 * 1024;

    /// <summary>Gewählter Faktor des Größen-Sanity-Schwellwerts (Band 2 bis 16).</summary>
    public const int SizeSanityFactor = 4;

    /// <summary>Untergrenze des Faktorbands laut Auftrag.</summary>
    public const int SizeSanityFactorMinimum = 2;

    /// <summary>Obergrenze des Faktorbands laut Auftrag.</summary>
    public const int SizeSanityFactorMaximum = 16;

    /// <summary>Mindestanzahl übereinstimmender Kalibrierläufe je savecheck.</summary>
    public const int CalibrationMinimumRuns = 2;

    /// <summary>Zähler des Mindestfortsetzungsteils am Planhorizont (1/2).</summary>
    public const int MinContinuationFractionNumerator = 1;

    /// <summary>Nenner des Mindestfortsetzungsteils am Planhorizont.</summary>
    public const int MinContinuationFractionDenominator = 2;

    /// <summary>Standardseed (Präzedenz T-020/T-021/T-022/T-023).</summary>
    public const uint DefaultSeed = 20260824u;

    /// <summary>Standardplanhorizont in Ticks.</summary>
    public const int DefaultPlanTicks = 3600;

    /// <summary>Standard-Kettenstichprobenabstand in Ticks.</summary>
    public const int ChainSampleIntervalTicks = 300;

    /// <summary>Dateiname des Prüflots innerhalb des Arbeitsverzeichnisses.</summary>
    public const string SlotFileName = "slot-current.rwsaved";

    /// <summary>Exitcode: Save-Gate verletzt; Report dennoch geschrieben und nicht bestanden.</summary>
    public const int ExitCodeGateViolated = 33;

    /// <summary>Exitcode: Lauf unvollständig; Teilreport ist keine Evidenz.</summary>
    public const int ExitCodeRunIncomplete = 34;

    /// <summary>Maschinenlesbare Aussage zur verbleibenden Q-TEC-006-Restoffenheit.</summary>
    public const string Qtec006Statement =
        "cooked-package-definition-and-replay-formats-remain-open-qtec006-not-decided-in-this-task";

    /// <summary>Maschinenlesbare Aussage zum anteiligen Charakter von F-005.</summary>
    public const string F005PartialStatement =
        "f005-partial-sim-state-envelope-only-full-worldstate-payload-deferred-to-t030-t051-content";

    /// <summary>Maschinenlesbare Zurückstellung der DATENMODELL-Fixturliste „finalitätsnah gültig“.</summary>
    public const string FinalityFixtureDeferralStatement =
        "datenmodell-fixture-class-finality-valid-deferred-to-content-stage-documented-postponement-no-weakening";
}
