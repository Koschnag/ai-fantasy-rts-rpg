namespace Riftward.Session;

/// <summary>
/// Versionierte Kennungen und fixierte Vertragswerte des kleinsten
/// spielbaren Druck- und Neustartschritts (T-036). Jede Kennung ist in
/// <c>docs/DRUCKVERTRAG.md</c> (Abschnitt 0, gatender Vertragsspike) mit
/// Alternativen, Gruenden, Playtestkriterien und Rueckrollweg dokumentiert.
/// Die Werte hier sind die maschinenlesbare Spiegelung des Vertrags; ein
/// Test haelt beide Seiten konsistent. Kein Wert dieses Vertrags antwortet
/// auf eine offene Produktfrage (Q-GAM-001 bis Q-GAM-007, Q-GAM-010,
/// Q-NAR-002, Q-NAR-004, Q-TEC-004, Q-TEC-006, Q-TEC-010 bleiben offen).
/// </summary>
public static class PressureContract
{
    /// <summary>Pfad des versionierenden Vertragsdokuments.</summary>
    public const string DocumentPath = "docs/DRUCKVERTRAG.md";

    /// <summary>Vertragsversion des Dokuments.</summary>
    public const string ContractVersion = "1";

    /// <summary>Kennung der Opt-in Aktivierung (Vertrag Abschnitt 7).</summary>
    public const string ActivationId = "opt-in-pressure-activation-v1";

    /// <summary>Kennung der entscheidungsgekoppelten Ausloeseregel (Vertrag Abschnitt 2).</summary>
    public const string TriggerId = "decision-coupled-window-v1";

    /// <summary>Kennung der Zeitbasis (Vertrag Abschnitt 3).</summary>
    public const string TimeBasisId = "fixed-deterministic-tick-window-v1";

    /// <summary>
    /// Fixierte Fensterlaenge in Vorgrenzen (Vertrag Abschnitt 3,
    /// vorregistrierte Hypothese: 600 Vorgrenzen = 30 s bei 20 Hz; der
    /// akzeptierte T-035-Referenzfluss brauchte 557 Vorgrenzen von Wahl bis
    /// persoenlicher Ankunft). Reversible Hypothesenkonstante; Aenderung
    /// erfordert Vertragsversion 2 mit Fixture-Regeneration.
    /// </summary>
    public const int WindowLengthTicks = 600;

    /// <summary>Kennung der Fehlschlags- und Neustartregel (Vertrag Abschnitt 4).</summary>
    public const string FailureRuleId = "defined-failure-automatic-reopen-v1";

    /// <summary>Kennung des sitzungslokalen Auftragszyklus-Neustarts (Vertrag Abschnitt 4).</summary>
    public const string RestartModelId = "session-local-cycle-restart-v1";

    /// <summary>Kennung der Erfolgsregel (Vertrag Abschnitt 5).</summary>
    public const string SuccessRuleId = "unchanged-decision-arrival-within-window-v1";

    /// <summary>
    /// Versionierte maschinenlesbare Nichtpersistenzaussage (Vertrag
    /// Abschnitt 8): Fenster, Fehlschlag, Zyklus und Neustart sind
    /// sitzungslokal, werden weder in Save/Load noch in Replay fortgesetzt
    /// und bleiben einer spaeteren Savevertrags-Erweiterung vorbehalten
    /// (ADR 008).
    /// </summary>
    public const string NotPersistedStatementId = "pressure-session-local-not-persisted-v1";

    /// <summary>Vertragliche Nichtpersistenzaussage im Report (maschinenlesbar).</summary>
    public const bool Persisted = false;

    /// <summary>Kennung der Titel-HUD-Erweiterung (Vertrag Abschnitt 6).</summary>
    public const string HudModelId = "title-hud-pressure-window-v1";

    /// <summary>Kennung des darstellseitigen Neustartkanals (Vertrag Abschnitt 6).</summary>
    public const string RestartChannelModelId = "pressure-restart-indicator-channel-v1";

    /// <summary>Vertraglicher Reportblockname (Vertrag Abschnitt 8).</summary>
    public const string ReportBlockId = "pressureSession";

    /// <summary>Schemaversion des Reports mit Druckaktivierung (rein additiv).</summary>
    public const int ReportSchemaVersionWithPressure = 5;

    /// <summary>
    /// Vertragliche Ursachenkennung des definierten Fehlschlags (Vertrag
    /// Abschnitt 4): Das offene Fenster lief an der Ablaufgrenze ohne
    /// persoenliche Ankunft in der Folgenzone ab.
    /// </summary>
    public const string FailureCauseWindowExpired = "window-expired-without-arrival";

    /// <summary>Endgrund einer erfolgreich abgeschlossenen Fensterinstanz.</summary>
    public const string WindowEndReasonSuccess = "success";

    /// <summary>Endgrund einer ohne Ankunft abgelaufenen Fensterinstanz.</summary>
    public const string WindowEndReasonExpired = "expired";

    /// <summary>Endstatus: kein wirksamer Entscheidungsstand im Lauf.</summary>
    public const string EndStatusNotStarted = "not-started";

    /// <summary>Endstatus: ein Fenster ist zum Laufende noch offen.</summary>
    public const string EndStatusWindowOpen = "window-open";

    /// <summary>Endstatus: Fehlschlag liegt, neue Wahl steht noch aus.</summary>
    public const string EndStatusRestartPending = "restart-pending";

    /// <summary>Endstatus: der letzte Zyklus schloss als Erfolg ab.</summary>
    public const string EndStatusSuccess = "success";

    /// <summary>
    /// Ehrlicher, maschinenlesbarer Grund des Endstatus not-started (Vertrag
    /// Abschnitt 2): Der Erkundungsauftrag war innerhalb des Laufs nicht
    /// abgeschlossen, es wurde kein Angebot geoeffnet und kein Fenster
    /// gestartet.
    /// </summary>
    public const string NotStartedReasonDecisionNotReached = "decision-not-reached-within-run";

    /// <summary>
    /// Ehrlicher, maschinenlesbarer Grund des Endstatus not-started (Vertrag
    /// Abschnitt 2): Das Angebot war offen, aber es fiel keine Wahl; ohne
    /// wirksame Entscheidung existiert kein Fenster.
    /// </summary>
    public const string NotStartedReasonOfferWithoutChoice = "decision-offer-open-without-choice-within-run";
}
