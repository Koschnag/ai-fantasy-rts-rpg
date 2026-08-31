namespace Riftward.Session;

/// <summary>
/// Versionierte Kennungen und fixierte Vertragswerte des kleinsten
/// spielbaren Abschluss- und Wiederholungsschritts (T-039). Jede Kennung ist
/// in <c>docs/ABSCHLUSSVERTRAG.md</c> (Abschnitt 0, gatender Vertragsspike)
/// mit Alternativen, Gruenden, Playtestkriterien und Rueckrollweg
/// dokumentiert. Die Werte hier sind die maschinenlesbare Spiegelung des
/// Vertrags; ein Test haelt beide Seiten konsistent. Kein Wert dieses
/// Vertrags antwortet auf eine offene Produktfrage (Q-GAM-001 bis Q-GAM-007,
/// Q-GAM-010, Q-NAR-002, Q-NAR-004, Q-TEC-004, Q-TEC-006, Q-TEC-010 bleiben
/// offen).
/// </summary>
public static class MissionContract
{
    /// <summary>Pfad des versionierenden Vertragsdokuments.</summary>
    public const string DocumentPath = "docs/ABSCHLUSSVERTRAG.md";

    /// <summary>Vertragsversion des Dokuments.</summary>
    public const string ContractVersion = "1";

    /// <summary>Kennung der Opt-in Aktivierung (Vertrag Abschnitt 7).</summary>
    public const string ActivationId = "opt-in-mission-activation-v1";

    /// <summary>Kennung der abgeleiteten Abschlussableitung (Vertrag Abschnitt 2).</summary>
    public const string CompletionModelId = "derived-completion-state-pure-function-v1";

    /// <summary>Kennung der Abschlussgrenzen-Beobachtung (Vertrag Abschnitt 2).</summary>
    public const string CompletionBoundaryModelId = "derived-completion-first-boundary-observation-v1";

    /// <summary>Kennung der Wiederholen-Aktivierungsform (Vertrag Abschnitt 3).</summary>
    public const string RepeatActivationModelId = "script-v4-plus-keymap-repeat-action-v1";

    /// <summary>Kennung des Reset-Umfangs (Vertrag Abschnitt 4).</summary>
    public const string ResetScopeId = "full-chain-restart-including-visit-protocol-v1";

    /// <summary>
    /// Versionierte Persistenzaussage der Kettenlaufwahrheit (Vertrag
    /// Abschnitt 5; Savevertrag V3 Abschnitt 15): die Kettenlauf-Anzahl ist
    /// über die additive Sektionsversion 2 in Save/Load fortsetzbar; die
    /// abgeleitete Abschlusswahrheit selbst trägt kein Persistenzbyte.
    /// </summary>
    public const string PersistenceStatementId = "mission-chain-run-counter-persisted-v1";

    /// <summary>Vertragliche Persistenzwahrheit der Kettenlaufzählung (maschinenlesbar).</summary>
    public const bool Persisted = true;

    /// <summary>Ausdrückliche Replay-Ausnahme der Persistenz (Replay setzt nicht fort).</summary>
    public const bool ReplayContinued = false;

    /// <summary>Vertraglicher saveLoad-Ausweis der Kettenlaufzählung.</summary>
    public const string SaveLoadContinuation = "continued";

    /// <summary>Vertraglicher replay-Ausweis der Replay-Ausnahme.</summary>
    public const string ReplayNotContinued = "not-continued";

    /// <summary>Ehrlicher Ausweis der abgeleiteten Abschlusswahrheit (kein Persistenzbyte).</summary>
    public const bool CompletionStatePersisted = false;

    /// <summary>Kennung der Titel-HUD-Erweiterung (Vertrag Abschnitt 6).</summary>
    public const string HudModelId = "title-hud-mission-completion-v1";

    /// <summary>Formatkennung der Abschluss-Skriptgrammatik (strikte Obermenge von v3).</summary>
    public const string ScriptFormatIdV4 = "graybox-input-script-v4";

    /// <summary>Skriptverb der Wiederholen-Aktion (parameterlos).</summary>
    public const string RepeatScriptAction = "repeat";

    /// <summary>Semantischer Aktionsname der Keymap-Wiederholen-Aktion (Vertrag Abschnitt 3).</summary>
    public const string RepeatActionName = "repeat-mission";

    /// <summary>Vertragliche Standardbelegung der Wiederholen-Aktion: F7 (Scancode 64, Bestandsstand unbesetzt).</summary>
    public const int RepeatDefaultScancode = 64;

    /// <summary>Vertraglicher Reportblockname (Vertrag Abschnitt 8).</summary>
    public const string ReportBlockId = "missionSession";

    /// <summary>Schemaversion des Reports mit Missionsaktivierung (rein additiv).</summary>
    public const int ReportSchemaVersionWithMission = 7;

    /// <summary>Abschlusszustand: die abgeleitete Funktion der Kette gilt.</summary>
    public const string CompletionStateCompleted = "completed";

    /// <summary>Abschlusszustand: kein Zykluserfolg der aktuellen Kette im Lauf.</summary>
    public const string CompletionStateOpen = "open";

    /// <summary>
    /// Ehrlicher, maschinenlesbarer Grund des Abschlusszustands open (Vertrag
    /// Abschnitt 2): kein Zykluserfolg der aktuellen Kette innerhalb des Laufs.
    /// </summary>
    public const string OpenReasonNoCycleSuccess = "no-cycle-success-within-run";

    /// <summary>Dispositionsname einer wirksamen Wiederholung (Vertrag Abschnitt 8).</summary>
    public const string RepeatDispositionApplied = "applied";

    /// <summary>Dispositionsname einer vor dem Abschluss abgewiesenen Wiederholung.</summary>
    public const string RepeatDispositionRejectedBeforeCompletion = "rejected-before-completion";

    /// <summary>
    /// Vertraglich benannte Disposition (Vertrag Abschnitt 3,
    /// Wirksamkeitsregel): Wiederholen-Aktion vor dem abgeleiteten
    /// Abschlusszustand; die Abweisung verändert nachweislich nichts.
    /// </summary>
    public const string RejectReasonRepeatBeforeCompletion = "mission-repeat-before-completion";

    /// <summary>
    /// Vertraglich benannte Disposition (Auswertungsordnung Stufe 1):
    /// Wiederholen-Aktion ohne aktivierte Abschluss- und Wiederholungsschicht.
    /// </summary>
    public const string RejectReasonMissionNotActivated = "mission-not-activated";

    /// <summary>
    /// Vertraglich benannter Ausweisgrund des headless Laufs: Titel-HUD und
    /// Keymap-Ausweis sind fensterpflichtig und werden headless ausdrücklich
    /// nicht gemessen statt still behauptet (Vertrag Abschnitte 6 und 8).
    /// </summary>
    public const string HeadlessMeasurementReason = "headless-run-without-window";
}
