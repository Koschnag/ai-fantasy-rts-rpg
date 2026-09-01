namespace Riftward.Session;

/// <summary>
/// Versionierte Kennungen und fixierte Vertragswerte des kleinsten
/// spielbaren Entscheidungsschritts (T-035). Jede Kennung ist in
/// <c>docs/ENTSCHEIDUNGSVERTRAG.md</c> (Abschnitt 0, gatender
/// Vertragsspike) mit Alternativen, Gruenden, Playtestkriterien und
/// Rueckrollweg dokumentiert. Die Werte hier sind die maschinenlesbare
/// Spiegelung des Vertrags; ein Test haelt beide Seiten konsistent. Kein
/// Wert dieses Vertrags antwortet auf eine offene Produktfrage (Q-GAM-001
/// bis Q-GAM-007, Q-GAM-010, Q-NAR-002, Q-NAR-004, Q-TEC-004, Q-TEC-006,
/// Q-TEC-010 bleiben offen).
/// </summary>
public static class DecisionContract
{
    /// <summary>Pfad des versionierenden Vertragsdokuments.</summary>
    public const string DocumentPath = "docs/ENTSCHEIDUNGSVERTRAG.md";

    /// <summary>Vertragsversion des Dokuments (V4: additive Ketten-Präzisierung, T-039; V3: Persistenz-Präzisierung, T-037; V2: Zyklus-Präzisierung).</summary>
    public const string ContractVersion = "4";

    /// <summary>Kennung der Opt-in Aktivierung (Vertrag Abschnitt 7).</summary>
    public const string ActivationId = "opt-in-decision-activation-v1";

    /// <summary>Kennung der abschlussgekoppelten Ausloeseregel (Vertrag Abschnitt 2).</summary>
    public const string OfferRuleId = "completion-gated-decision-offer-v1";

    /// <summary>Kennung der Optionsableitung aus dem Aufsuchprotokoll (Vertrag Abschnitt 3).</summary>
    public const string OptionsModelId = "visit-protocol-zone-options-v1";

    /// <summary>Kennung des Modus-Scopings der Entscheidungseingabe (Vertrag Abschnitt 4).</summary>
    public const string ChoiceScopingRuleId = "decision-choose-personal-mode-only-v1";

    /// <summary>Kennung der vertraglichen Auswertungsordnung einer Wahl (Vertrag Abschnitt 4).</summary>
    public const string ChoiceEvaluationOrderId = "decision-choice-evaluation-order-v1";

    /// <summary>Kennung der Folgeregel (Vertrag Abschnitt 5).</summary>
    public const string FollowUpRuleId = "chosen-zone-follow-up-objective-v1";

    /// <summary>Kennung der Ankunfts- und Moduskopplungsregel (Vertrag Abschnitt 5).</summary>
    public const string ArrivalRuleId = "boundary-arrival-personal-mode-only-v1";

    /// <summary>
    /// Versionierte historische Nichtpersistenzaussage (Vertrag V1,
    /// Abschnitt 7): bleibt als dokumentierte Vorgeschichte der V3-
    /// Präzisierung im Vertrag enthalten und wird im Report nicht mehr
    /// als aktuelle Wahrheit ausgegeben.
    /// </summary>
    public const string NotPersistedStatementId = "decision-session-local-not-persisted-v1";

    /// <summary>
    /// Versionierte Save/Load-Persistenzaussage (Vertrag V3, Abschnitt 14;
    /// Savevertrag V2 Abschnitt 13.6): Angebot, Entscheidung, Folge und
    /// Zykluszustand sind über die additive Sitzungssektion in Save/Load
    /// fortsetzbar.
    /// </summary>
    public const string SaveLoadPersistenceStatementId = "decision-session-local-save-load-persisted-v3";

    /// <summary>Vertragliche Persistenzwahrheit im Report (maschinenlesbar, V3).</summary>
    public const bool Persisted = true;

    /// <summary>Ausdrückliche Replay-Ausnahme der Persistenz (V3; Replay setzt nicht fort).</summary>
    public const bool ReplayContinued = false;

    /// <summary>Vertraglicher saveLoad-Ausweis der Persistenz (V3).</summary>
    public const string SaveLoadContinuation = "continued";

    /// <summary>Vertraglicher replay-Ausweis der Replay-Ausnahme (V3).</summary>
    public const string ReplayNotContinued = "not-continued";

    /// <summary>Kennung der Titel-HUD-Erweiterung (Vertrag Abschnitt 6).</summary>
    public const string HudModelId = "title-hud-decision-objective-v1";

    /// <summary>Kennung des darstellseitigen Folgezielkanals.</summary>
    public const string FollowUpChannelModelId = "follow-up-marker-channel-v1";

    /// <summary>Formatkennung der Entscheidungs-Skriptgrammatik (strikte Obermenge von v2).</summary>
    public const string ScriptFormatIdV3 = "graybox-input-script-v3";

    /// <summary>Vertraglicher Reportblockname (Vertrag Abschnitt 8).</summary>
    public const string ReportBlockId = "decisionSession";

    /// <summary>Schemaversion des Reports mit Entscheidungsaktivierung (rein additiv).</summary>
    public const int ReportSchemaVersionWithDecision = 4;

    /// <summary>
    /// Vertraglich benannter kontrollierter Vertragsfehler (Vertrag
    /// Abschnitt 3, Totalitaet): weniger als zwei verschiedene Optionszonen
    /// brechen die Optionsableitung kontrolliert ab, statt ein entwertetes
    /// Angebot mit gleichzeitigen Optionen zu oeffnen. Im gebundenen
    /// Erkundungsvertrag unerreichbar (jede Landmarke registriert
    /// hoechstens einmal, der Abschluss verlangt alle Zonen); der
    /// Fail-closed-Randfall ist Testbindung.
    /// </summary>
    public const string RejectReasonInsufficientDistinctZones = "decision-offer-insufficient-distinct-zones";

    /// <summary>
    /// Ehrlicher, maschinenlesbarer Nichtoeffnungsgrund (Vertrag
    /// Abschnitt 2): Der Erkundungsauftrag war innerhalb des Laufs nicht
    /// abgeschlossen; es wurde kein Angebot geoeffnet statt stiller Leere.
    /// </summary>
    public const string OfferNotOpenedReason = "exploration-not-completed-within-run";

    /// <summary>
    /// Vertraglich benannte Disposition (Vertrag Abschnitt 4,
    /// Auswertungsordnung Stufe 1): Entscheidungsaktion ohne aktivierte
    /// Entscheidungsschicht.
    /// </summary>
    public const string RejectReasonDecisionNotActivated = "decision-not-activated";

    /// <summary>
    /// Vertraglich benannte Disposition (Vertrag Abschnitt 4,
    /// Auswertungsordnung Stufe 2): Entscheidung vor der Angebotsöffnung.
    /// </summary>
    public const string RejectReasonChooseBeforeOffer = "decision-choose-before-offer";

    /// <summary>
    /// Vertraglich benannte Disposition (Vertrag Abschnitt 4,
    /// Auswertungsordnung Stufe 3): Entscheidung im strategischen Modus
    /// (Modus-Scoping, spiegelbildlich zur persoenlichen Aufsuchregel).
    /// </summary>
    public const string RejectReasonChooseInStrategicMode = "decision-choose-in-strategic-mode";

    /// <summary>
    /// Vertraglich benannte Disposition (Vertrag Abschnitt 4,
    /// Auswertungsordnung Stufe 4): Entscheidung nach bereits gefallener
    /// Wahl (keine zweite Wahl in derselben Sitzung).
    /// </summary>
    public const string RejectReasonChooseAfterDecision = "decision-choose-after-decision";

    /// <summary>Vertragliche Wahlkennung fuer Option A (Skript und Report).</summary>
    public const string ChoiceOptionAId = "a";

    /// <summary>Vertragliche Wahlkennung fuer Option B (Skript und Report).</summary>
    public const string ChoiceOptionBId = "b";
}
