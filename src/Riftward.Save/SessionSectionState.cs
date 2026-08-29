namespace Riftward.Save;

/// <summary>
/// Kanonischer Sitzungszustand einer Vorgrenze (Savevertrag V2, Abschnitt 13):
/// aktiver Sitzungsmodus samt schwebender Moduswechsel, Aufsuchprotokoll samt
/// Erkundungsfortschritt, Entscheidungsangebot/Wahl/Folgenzustand samt
/// Sitzungsabweisungszaehlern und Druckfensterinstanzen samt Zykluszustand.
/// Die Struktur ist reine BCL-Daten ohne SDL3-, bgfx- oder
/// Betriebssystemtypen; nicht gesetzte Grenzen und Zonen tragen die
/// vertraglichen Negativeinsichten (−1), nicht gefallene Angaben die
/// definierten Aufzaehlungssentinel. Kein Feld dieser Sektion ist Teil des
/// Simulationszustands oder Hashes.
/// </summary>
public sealed record SessionSectionState
{
    /// <summary>Aktiver Sitzungsmodus an der Vorgrenze (0 = strategisch, 1 = persoenlich).</summary>
    public required byte ActiveMode { get; init; }

    /// <summary>Ausgewertete, aber noch nicht wirksame Moduswechsel (Modevertrag Abschnitt 4).</summary>
    public required IReadOnlyList<SessionSectionPendingSwitch> PendingSwitches { get; init; }

    public required byte ExplorationActive { get; init; }

    /// <summary>Aufsuchprotokoll in kanonischer Registrierungsfolge (Besuchsrang ist die Listenposition).</summary>
    public required IReadOnlyList<SessionSectionVisit> ExplorationVisits { get; init; }

    public required byte DecisionActive { get; init; }

    public required byte DecisionOfferOpened { get; init; }

    public required long DecisionOfferBoundaryTick { get; init; }

    public required int DecisionOptionZoneA { get; init; }

    public required int DecisionOptionZoneB { get; init; }

    public required byte DecisionDecided { get; init; }

    public required long DecisionBoundaryTick { get; init; }

    /// <summary>0 = Option a, 1 = Option b, 255 = nicht gefallen.</summary>
    public required byte DecisionChoiceKind { get; init; }

    /// <summary>1 = persoenlich (vertraglich einzige wirksame Wahlart), 0 = nicht gefallen.</summary>
    public required byte DecisionModeKind { get; init; }

    public required int DecisionFollowUpZoneIndex { get; init; }

    public required byte DecisionFollowUpCompleted { get; init; }

    public required long DecisionArrivalBoundaryTick { get; init; }

    public required long DecisionRejectionsBeforeOffer { get; init; }

    public required long DecisionRejectionsInStrategicMode { get; init; }

    public required long DecisionRejectionsAfterDecision { get; init; }

    public required byte PressureActive { get; init; }

    public required long PressureCycleCount { get; init; }

    /// <summary>Fensterinstanzen in Instanzfolge; eine offene Instanz traegt die Offeneinsicht (Endgrenze −1).</summary>
    public required IReadOnlyList<SessionSectionWindow> PressureWindows { get; init; }

    public required long PressureLastFailureBoundaryTick { get; init; }

    public required byte PressureHasLastFailure { get; init; }

    public required int PressureLastFailureFollowUpZoneIndex { get; init; }

    public required long PressureLastReopenBoundaryTick { get; init; }

    public required byte PressureReopenPendingRecording { get; init; }

    /// <summary>
    /// Ehrliche kanonische Sitzungsleere: strategischer Modus ohne schwebende
    /// Wechsel und ohne aktivierte Schichtzustand. Sie traegt die V1-Legacy-
    /// Kompatibilitaet (Savevertrag V2 Abschnitt 13.5) und den Zustand vor
    /// jeder Schichtaktivierung.
    /// </summary>
    public static SessionSectionState Empty { get; } = new()
    {
        ActiveMode = 0,
        PendingSwitches = Array.Empty<SessionSectionPendingSwitch>(),
        ExplorationActive = 0,
        ExplorationVisits = Array.Empty<SessionSectionVisit>(),
        DecisionActive = 0,
        DecisionOfferOpened = 0,
        DecisionOfferBoundaryTick = -1,
        DecisionOptionZoneA = -1,
        DecisionOptionZoneB = -1,
        DecisionDecided = 0,
        DecisionBoundaryTick = -1,
        DecisionChoiceKind = SessionSectionCodec.ChoiceKindUnset,
        DecisionModeKind = 0,
        DecisionFollowUpZoneIndex = -1,
        DecisionFollowUpCompleted = 0,
        DecisionArrivalBoundaryTick = -1,
        DecisionRejectionsBeforeOffer = 0,
        DecisionRejectionsInStrategicMode = 0,
        DecisionRejectionsAfterDecision = 0,
        PressureActive = 0,
        PressureCycleCount = 0,
        PressureWindows = Array.Empty<SessionSectionWindow>(),
        PressureLastFailureBoundaryTick = -1,
        PressureHasLastFailure = 0,
        PressureLastFailureFollowUpZoneIndex = -1,
        PressureLastReopenBoundaryTick = -1,
        PressureReopenPendingRecording = 0,
    };
}

/// <summary>Schwebender Moduswechsel an der Vorgrenze (kein Kettenzustand).</summary>
public sealed record SessionSectionPendingSwitch(
    long IntentTick,
    long EffectiveBoundaryTick,
    byte NewMode);

/// <summary>Aufsuchprotokolleintrag der Sektion (Besuchsrang ist die Listenposition).</summary>
public sealed record SessionSectionVisit(
    long BoundaryTick,
    int ZoneIndex,
    byte Mode);

/// <summary>Fensterinstanz der Sektion mit vertraglichen Aufzaehlungssentinel.</summary>
public sealed record SessionSectionWindow(
    long Instance,
    long Cycle,
    long StartBoundaryTick,
    long EndBoundaryTick,
    byte EndReasonKind,
    long ArrivalBoundaryTick,
    byte ArrivalModeKind,
    byte FailureCauseKind);
