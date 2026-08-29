namespace Riftward.Session;

/// <summary>
/// Versionierte Kennungen und fixierte Vertragswerte des kleinsten
/// Erkundungsauftrag-Loops (T-034). Jede Kennung ist in
/// <c>docs/ERKUNDUNGSVERTRAG.md</c> (Abschnitt 0, gatender Vertragsspike)
/// mit Alternativen, Gruenden, Playtestkriterien und Rueckrollweg
/// dokumentiert. Die Werte hier sind die maschinenlesbare Spiegelung des
/// Vertrags; ein Test haelt beide Seiten konsistent. Kein Wert dieses
/// Vertrags antwortet auf eine offene Produktfrage (Q-GAM-001 bis Q-GAM-007,
/// Q-GAM-010, Q-NAR-002, Q-NAR-004, Q-TEC-004, Q-TEC-006, Q-TEC-010 bleiben
/// offen).
/// </summary>
public static class ExplorationContract
{
    /// <summary>Pfad des versionierenden Vertragsdokuments.</summary>
    public const string DocumentPath = "docs/ERKUNDUNGSVERTRAG.md";

    /// <summary>Vertragsversion des Dokuments (V2: additive Persistenz-Präzisierung, T-037).</summary>
    public const string ContractVersion = "2";

    /// <summary>Kennung der Opt-in Aktivierung (Vertrag Abschnitt 6).</summary>
    public const string ActivationId = "opt-in-exploration-activation-v1";

    /// <summary>Kennung des Landmarkenmodells (Vertrag Abschnitt 2).</summary>
    public const string LandmarkModelId = "graybox-landmark-zone-anchor-v1";

    /// <summary>Kennung der Aufsuch- und Moduskopplungsregel (Vertrag Abschnitt 3).</summary>
    public const string VisitRuleId = "boundary-visit-personal-mode-only-v1";

    /// <summary>Kennung des sitzungslokalen Fortschrittszaehlers (Vertrag Abschnitt 4).</summary>
    public const string CounterModelId = "session-local-visit-counter-v1";

    /// <summary>
    /// Versionierte historische Nichtpersistenzaussage (Vertrag V1,
    /// Abschnitt 4): bleibt als dokumentierte Vorgeschichte der V2-
    /// Präzisierung im Vertrag enthalten und wird im Report nicht mehr
    /// als aktuelle Wahrheit ausgegeben.
    /// </summary>
    public const string NotPersistedStatementId = "session-local-not-persisted-v1";

    /// <summary>
    /// Versionierte Save/Load-Persistenzaussage (Vertrag V2, Abschnitt 10;
    /// Savevertrag V2 Abschnitt 13.6): Aufsuchprotokoll, Fortschritt und
    /// Abschluss sind über die additive Sitzungssektion in Save/Load
    /// fortsetzbar.
    /// </summary>
    public const string SaveLoadPersistenceStatementId = "session-local-save-load-persisted-v2";

    /// <summary>Vertragliche Persistenzwahrheit im Report (maschinenlesbar, V2).</summary>
    public const bool Persisted = true;

    /// <summary>Ausdrückliche Replay-Ausnahme der Persistenz (V2; Replay setzt nicht fort).</summary>
    public const bool ReplayContinued = false;

    /// <summary>Vertraglicher saveLoad-Ausweis der Persistenz (V2).</summary>
    public const string SaveLoadContinuation = "continued";

    /// <summary>Vertraglicher replay-Ausweis der Replay-Ausnahme (V2).</summary>
    public const string ReplayNotContinued = "not-continued";

    /// <summary>Kennung der Titel-HUD-Erweiterung (Vertrag Abschnitt 5).</summary>
    public const string HudModelId = "title-hud-expedition-progress-v1";

    /// <summary>Kennung des darstellseitigen Landmarkenzustandskanals.</summary>
    public const string LandmarkChannelModelId = "landmark-state-channel-v1";

    /// <summary>
    /// Vertraglich benannter kontrollierter Vertragsfehler (Vertrag
    /// Abschnitt 2, Totalitaet): Eine Zone ohne betretbare Kachel bricht die
    /// Landmarken-Ableitung kontrolliert ab, statt einen undefinierten Anker
    /// zu bilden. Im gebundenen Vertragsweltstand unerreichbar
    /// (<c>NavWorld.ValidateZones</c> erzwingt die Zonendeckung bereits bei
    /// Prozessstart); der doppelte Fail-closed-Randfall ist Testbindung.
    /// </summary>
    public const string RejectReasonZoneWithoutWalkableTile = "exploration-landmark-zone-without-walkable-tile";

    /// <summary>Vertraglicher Kennungsanteil des Reportblocks.</summary>
    public const string ReportBlockId = "explorationSession";

    /// <summary>Schemaversion des Reports ohne Aktivierung (Bestandsstand).</summary>
    public const int ReportSchemaVersionWithoutExploration = 2;

    /// <summary>Schemaversion des Reports bei Aktivierung (rein additiv).</summary>
    public const int ReportSchemaVersionWithExploration = 3;

    /// <summary>
    /// Vertraglich benannter Ausweisgrund des headless Laufs: Titel-HUD und
    /// Landmarkenzustandskanal sind fensterpflichtig und werden headless
    /// ausdruecklich nicht gemessen statt still behauptet (Vertrag
    /// Abschnitte 5 und 7).
    /// </summary>
    public const string HeadlessMeasurementReason = "headless-run-without-window";
}
