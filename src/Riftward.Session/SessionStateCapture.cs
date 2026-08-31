using Riftward.Save;

namespace Riftward.Session;

/// <summary>
/// Erfassung des vollstaendigen Sitzungszustands an einer Vorgrenze
/// (Savevertrag V2, Abschnitt 13.1): aktiver Modus samt schwebender
/// Moduswechsel, Aufsuchprotokoll samt Fortschritt, Entscheidungsangebot/
/// Wahl/Folgenzustand samt Zyklusstand und Druckfenster samt Zykluszustand.
/// Die Erfassung ist rein sitzungsseitig, liest ausschließlich schreib-
/// geschützt und erzeugt niemals einen Kernbefehl; kein Feld ist Teil des
/// Simulationszustands oder Hashes.
/// </summary>
public static class SessionStateCapture
{
    /// <summary>
    /// Erfasst die Kettenwahrheit der fünf Sitzungsschichten plus des
    /// Modusflags und seiner schwebenden Wechsel aus der laufenden Pipeline.
    /// Die Schichten werden genau dann erfasst, wenn sie aktiviert sind; ohne
    /// Aktivierung trägt die Sektion die ehrliche Schichtleere.
    /// <paramref name="pendingSwitches"/> ist die Momentaufnahme der
    /// schwebenden Wechsel an der Vorgrenze (Savevertrag V2 Abschnitt 13.1);
    /// ohne sie liest die Erfassung die noch nicht geflushten Wechsel direkt
    /// aus der Pipeline.
    /// </summary>
    public static SessionSectionState Capture(
        SessionPipeline pipeline,
        ExplorationSession? exploration,
        DecisionSession? decision,
        PressureSession? pressure,
        MissionSession? mission = null,
        IReadOnlyList<ModeSwitchEvent>? pendingSwitches = null)
    {
        ArgumentNullException.ThrowIfNull(pipeline);

        return new SessionSectionState
        {
            ActiveMode = pipeline.CurrentEffectiveMode == SessionMode.Personal
                ? SessionSectionCodec.ModePersonal
                : SessionSectionCodec.ModeStrategic,
            PendingSwitches = (pendingSwitches ?? pipeline.PendingSwitches)
                .Select(pending => new SessionSectionPendingSwitch(
                    IntentTick: pending.IntentTick,
                    EffectiveBoundaryTick: pending.EffectiveBoundaryTick,
                    PreviousMode: pending.PreviousMode == SessionMode.Personal
                        ? SessionSectionCodec.ModePersonal
                        : SessionSectionCodec.ModeStrategic,
                    NewMode: pending.NewMode == SessionMode.Personal
                        ? SessionSectionCodec.ModePersonal
                        : SessionSectionCodec.ModeStrategic))
                .ToArray(),
            ExplorationActive = exploration is not null ? (byte)1 : (byte)0,
            ExplorationVisits = exploration is null
                ? Array.Empty<SessionSectionVisit>()
                : exploration.VisitProtocol
                    .Select(visit => new SessionSectionVisit(
                        BoundaryTick: visit.EvaluationBoundaryTick,
                        ZoneIndex: visit.ZoneIndex,
                        Mode: SessionSectionCodec.ModePersonal))
                    .ToArray(),
            DecisionActive = decision is not null ? (byte)1 : (byte)0,
            DecisionOfferOpened = decision is { OfferOpened: true } ? (byte)1 : (byte)0,
            DecisionOfferBoundaryTick = decision?.OfferBoundaryTick ?? -1,
            DecisionOptionZoneA = decision?.OptionZoneA ?? -1,
            DecisionOptionZoneB = decision?.OptionZoneB ?? -1,
            DecisionDecided = decision is { Decided: true } ? (byte)1 : (byte)0,
            DecisionBoundaryTick = decision?.DecisionBoundaryTick ?? -1,
            DecisionChoiceKind = decision is { Decided: true, Choice: var choice }
                ? (choice == DecisionContract.ChoiceOptionAId
                    ? SessionSectionCodec.ChoiceKindA
                    : SessionSectionCodec.ChoiceKindB)
                : SessionSectionCodec.ChoiceKindUnset,
            DecisionModeKind = decision is { Decided: true } ? SessionSectionCodec.ModePersonal : (byte)0,
            DecisionFollowUpZoneIndex = decision?.FollowUpZoneIndex ?? -1,
            DecisionFollowUpCompleted = decision is { FollowUpCompleted: true } ? (byte)1 : (byte)0,
            DecisionArrivalBoundaryTick = decision?.ArrivalBoundaryTick ?? -1,
            DecisionRejectionsBeforeOffer = decision?.ChooseRejectionsBeforeOffer ?? 0,
            DecisionRejectionsInStrategicMode = decision?.ChooseRejectionsInStrategicMode ?? 0,
            DecisionRejectionsAfterDecision = decision?.ChooseRejectionsAfterDecision ?? 0,
            PressureActive = pressure is not null ? (byte)1 : (byte)0,
            PressureCycleCount = pressure?.CycleCount ?? 0,
            PressureWindows = pressure is null
                ? Array.Empty<SessionSectionWindow>()
                : pressure.Windows
                    .Select(window => new SessionSectionWindow(
                        Instance: window.Instance,
                        Cycle: window.Cycle,
                        StartBoundaryTick: window.StartBoundaryTick,
                        EndBoundaryTick: window.EndBoundaryTick,
                        EndReasonKind: window.EndReason switch
                        {
                            null => SessionSectionCodec.EndReasonOpen,
                            PressureContract.WindowEndReasonSuccess => SessionSectionCodec.EndReasonSuccess,
                            PressureContract.WindowEndReasonExpired => SessionSectionCodec.EndReasonExpired,
                            _ => throw new InvalidOperationException(
                                "Unbekannter Fensterendgrund erreicht die Sektionserfassung."),
                        },
                        ArrivalBoundaryTick: window.ArrivalBoundaryTick,
                        ArrivalModeKind: window.ArrivalMode switch
                        {
                            null => SessionSectionCodec.ArrivalModeNone,
                            ModeContract.ModePersonalId => SessionSectionCodec.ArrivalModePersonal,
                            ModeContract.ModeStrategicId => SessionSectionCodec.ArrivalModeStrategic,
                            _ => throw new InvalidOperationException(
                                "Unbekannter Ankunftsmodus erreicht die Sektionserfassung."),
                        },
                        FailureCauseKind: window.FailureCause is null
                            ? SessionSectionCodec.CauseKindNone
                            : SessionSectionCodec.CauseKindWindowExpired))
                    .ToArray(),
            PressureLastFailureBoundaryTick = pressure?.LastFailureBoundaryTick ?? -1,
            PressureHasLastFailure = pressure is { LastFailureCause: not null } ? (byte)1 : (byte)0,
            PressureLastFailureFollowUpZoneIndex = pressure?.LastFailureFollowUpZoneIndex ?? -1,
            PressureLastReopenBoundaryTick = pressure?.LastReopenBoundaryTick ?? -1,
            PressureReopenPendingRecording = pressure is { RestartPendingRecording: true } ? (byte)1 : (byte)0,
            MissionActive = mission is not null ? (byte)1 : (byte)0,
            MissionChainRunCount = mission?.ChainRunCount ?? 0,
        };
    }
}
