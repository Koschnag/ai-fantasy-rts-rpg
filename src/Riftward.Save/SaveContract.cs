namespace Riftward.Save;

/// <summary>
/// Versionierte Kennungen und fixierte Werte des Savevertrags (T-031,
/// Abschnitt 0; V2-Erweiterung T-037, Abschnitt 13). Jede Wahl ist in
/// <c>docs/SAVEVERTRAG.md</c> mit Alternativen, Gruenden und Rueckrollweg
/// dokumentiert; ein Test haelt beide Seiten konsistent. Der Vertrag
/// entscheidet Q-TEC-006 nur im Teilaspekt Save-Umschlag/Persistenzformat
/// des Simulationszustands und der additiven Sitzungssektion; Cooked-Paket-,
/// Definitions- und Replayformate bleiben OFFEN.
/// </summary>
public static class SaveContract
{
    /// <summary>Pfad des versionierenden Vertragsdokuments.</summary>
    public const string DocumentPath = "docs/SAVEVERTRAG.md";

    /// <summary>Vertragsversion des Dokuments (V2: additive Sitzungssektion, T-037).</summary>
    public const string ContractVersion = "2";

    /// <summary>Kennung der kanonischen Binärcodierung.</summary>
    public const string EncodingId = "riftward-save-canonical-binary-v1";

    /// <summary>Dateimagie (Bytes) eines Saves.</summary>
    public const byte Magic0 = (byte)'R';
    public const byte Magic1 = (byte)'W';
    public const byte Magic2 = (byte)'S';
    public const byte Magic3 = (byte)'D';

    /// <summary>
    /// Aktuelle Produkt-Schemaversion (V2: Umschlag mit additiver
    /// Sitzungssektion, Savevertrag V2 Abschnitt 13.5).
    /// </summary>
    public const ushort CurrentSaveSchemaVersion = 2;

    /// <summary>
    /// Unterstuetzte Legacy-Schemaversion (V1): laedt unveraendert mit
    /// ehrlicher, maschinenlesbarer Sitzungsleere ohne Migrationserfindung
    /// (Savevertrag V2 Abschnitt 13.5).
    /// </summary>
    public const ushort LegacySaveSchemaVersion = 1;

    /// <summary>Länge des Dateimagics in Bytes.</summary>
    public const int MagicLength = 4;

    /// <summary>Länge eines SHA-256-Ankers in Bytes.</summary>
    public const int HashLength = 32;

    /// <summary>Länge des opaken saveId-Metadatums in Bytes.</summary>
    public const int SaveIdLength = 16;

    /// <summary>
    /// Additive Kopffelder V2 am Kopfende: sessionSectionLength (u64) und
    /// sessionSectionHash (SHA-256 über exakt die Sektionsbytes).
    /// </summary>
    public const int SessionSectionHeaderFieldsBytes = sizeof(ulong) + HashLength;

    /// <summary>Absolutes Vorab-Limit der Sitzungssektion (DoS-Grenze) in Bytes.</summary>
    public const long MaxSessionSectionBytes = 1024L * 1024;

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

    /// <summary>
    /// Vertraglicher Slotname der interaktiven Speicher-/Ladeaktion (Savevertrag
    /// V2 Abschnitt 13.3): genau ein Slot der laufenden Sitzung.
    /// </summary>
    public const string InteractiveSlotName = "slot-interactive.rwsaved";

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

    /* ------------------------ V2-Erweiterung (T-037, Abschnitt 13) -------- */

    /// <summary>Aktivierungsform des headless Speicherlaufs (Abschnitt 13.2).</summary>
    public const string HeadlessSaveActivationId = "opt-in-continuation-save-v2";

    /// <summary>Aktivierungsform des headless Fortsetzungslaufs (Abschnitt 13.2).</summary>
    public const string HeadlessLoadActivationId = "opt-in-continuation-load-v2";

    /// <summary>Aktivierungsform der interaktiven Slot-Aktionen (Abschnitt 13.3).</summary>
    public const string InteractiveSlotActivationId = "opt-in-interactive-slot-capability-v2";

    /// <summary>Sektionsaufbaukennung (Abschnitt 13.1).</summary>
    public const string SessionSectionModelId = "session-section-full-state-v1";

    /// <summary>Headless Aktivierungsmodellkennung (Abschnitt 13.2).</summary>
    public const string HeadlessActivationModelId = "opt-in-continuation-flags-v2";

    /// <summary>Interaktive Aktivierungsmodellkennung (Abschnitt 13.3).</summary>
    public const string InteractiveActivationModelId = "opt-in-interactive-slot-actions-v2";

    /// <summary>Codec-Modulgrenzkennung (Abschnitt 13.4).</summary>
    public const string CodecBoundaryModelId = "session-section-codec-boundary-v2";

    /// <summary>V1-Kompatibilitätskennung (Abschnitt 13.5).</summary>
    public const string LegacyEmptinessModelId = "legacy-v1-session-emptiness-v2";

    /// <summary>Aktivierungsgrenzkennung für untrusted Slots (Abschnitt 13.5).</summary>
    public const string ActivationGuardModelId = "untrusted-slot-activation-guards-v2";

    /// <summary>Semantische Aktionsnamen der interaktiven Slot-Aktionen (Abschnitt 13.3).</summary>
    public const string SaveSlotActionName = "save-slot";
    public const string LoadSlotActionName = "load-slot";

    /// <summary>Vertragliche Standardbelegung: F5 (62) speichert, F9 (66) lädt.</summary>
    public const int SaveSlotDefaultScancode = 62;
    public const int LoadSlotDefaultScancode = 66;

    /// <summary>Report-Schemaversion mit Save-/Ladeaktivierung (rein additiv).</summary>
    public const int ReportSchemaVersionWithContinuation = 6;

    /// <summary>Maschinenlesbare Aussage zur V1-Legacy-Sitzungsleere.</summary>
    public const string LegacyV1SessionEmptinessStatement =
        "legacy-v1-slot-loads-with-honest-machine-readable-session-emptiness-and-unchanged-chain";

    /// <summary>Maschinenlesbare Aussage zur Replay-Ausnahme der Sitzungssektion.</summary>
    public const string SessionPersistenceReplayExceptionStatement =
        "session-section-persisted-in-save-load-with-explicit-replay-exception-t037";

    /// <summary>Maschinenlesbare Ablehnungskennung: Weltkennung des Slots widerspricht der Vertragswelt.</summary>
    public const string RejectionForeignWorldId = "foreign-world-id";

    /// <summary>Maschinenlesbare Ablehnungskennung: Seed des Slots widerspricht dem Laufseed.</summary>
    public const string RejectionForeignSeed = "foreign-seed";

    /// <summary>Maschinenlesbare Ablehnungskennung: Schemaversion des Slots wird ohne Migration nicht unterstützt.</summary>
    public const string RejectionUnsupportedSchemaVersion = "unsupported-schema-version";

    /// <summary>Maschinenlesbare Ablehnungskennung: kein Slotverzeichnis konfiguriert.</summary>
    public const string RejectionSlotDirectoryNotConfigured = "slot-directory-not-configured";

    /// <summary>Maschinenlesbare Ablehnungskennung: Slot existiert nicht oder ist unlesbar.</summary>
    public const string RejectionSlotUnreadable = "slot-unreadable";
}
