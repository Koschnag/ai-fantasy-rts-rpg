namespace Riftward.Session;

/// <summary>
/// Maschinenlesbarer Protokolleintrag einer Fensterinstanz (Vertrag
/// Abschnitt 8). Eine geoeffnete Instanz erscheint sofort im Protokoll; die
/// Endgrenze und der Endgrund tragen den Sentinel <see cref="UnsetBoundaryTick"/>
/// beziehungsweise null, solange das Fenster offen ist. Rein diagnostisch
/// (gateCoupled=false) und nie Teil des Simulationszustands oder Hashes.
/// </summary>
public sealed record PressureWindowEvent(
    long Instance,
    long Cycle,
    long StartBoundaryTick,
    long EndBoundaryTick,
    string? EndReason,
    long ArrivalBoundaryTick,
    string? ArrivalMode,
    string? FailureCause)
{
    /// <summary>Sentinel fuer die noch offene (noch nicht beendete) Instanz.</summary>
    public const long UnsetBoundaryTick = -1;
}

/// <summary>
/// Maschinenlesbarer schreibgeschuetzter Ausweis des sitzungslokalen
/// Druckzustands (Vertrag Abschnitt 8). Nicht gesetzte Grenzen tragen den
/// Sentinel <see cref="PressureWindowEvent.UnsetBoundaryTick"/>, nicht
/// gefallene Angaben null. Niemals Teil von Simulationszustand oder Hash;
/// niemals persistiert.
/// </summary>
public sealed record PressureTelemetry(
    long WindowLengthTicks,
    long CycleCount,
    IReadOnlyList<PressureWindowEvent> Windows,
    long LastFailureBoundaryTick,
    string? LastFailureCause,
    long LastReopenBoundaryTick,
    string EndStatus,
    string? EndStatusReason)
{
    /// <summary>Sentinel fuer nicht gesetzte Grenzen.</summary>
    public const long UnsetBoundaryTick = -1;
}

/// <summary>
/// Sitzungslokale Druckschicht (T-036, Druckvertrag Abschnitte 2 bis 5):
/// rein sitzungsseitige Beobachtung und Semantik an der Vorgrenze ueber dem
/// abgenommenen T-035-Entscheidungsabschluss. Sie erzeugt niemals einen
/// Kernbefehl, veraendert keinen Befehlszustand, liest ausschließlich
/// Entscheidungszustand, Heldenzone und wirksamen Sitzungsmodus
/// schreibgeschützt und ist zu keinem Zeitpunkt Teil des Simulationszustands
/// oder Hashes.
///
/// Die erste Fensterinstanz startet genau an der Vorgrenze, an der die
/// gueltige T-035-Entscheidung wirksam wird, und jede weitere genau an der
/// erneut wirksamen Wahl nach Wiederauffrischung
/// (<c>decision-coupled-window-v1</c>). Die persoenliche Ankunft innerhalb
/// des offenen Fensters schliesst den Zyklus als Erfolg ab; der Ablauf an
/// der Ablaufgrenze ohne Ankunft erzeugt den definierten Fehlschlag mit
/// Ursache (<c>defined-failure-automatic-reopen-v1</c>), setzt den
/// sitzungslokalen Auftragszyklus kontrolliert zurueck und laesst das
/// Entscheidungsangebot an der naechsten Vorgrenze deterministisch erneut
/// oeffnen (autorisierte additive Zyklus-Praezisierung des
/// Entscheidungsvertrags V2). Die Ankunft an der Ablaufgrenze selbst ist die
/// letzte Gelegenheit innerhalb des Fensters.
/// </summary>
public sealed class PressureSession
{
    private readonly List<PressureWindowEvent> _windows = new();
    private IReadOnlyList<PressureWindowEvent>? _windowView;

    private long _cycleCount;
    private long _lastFailureBoundaryTick = PressureTelemetry.UnsetBoundaryTick;
    private string? _lastFailureCause;
    private int _lastFailureFollowUpZoneIndex = -1;
    private long _lastReopenBoundaryTick = PressureTelemetry.UnsetBoundaryTick;
    private bool _reopenPendingRecording;

    /// <summary>Anzahl begonnener Auftragszyklen (eindeutige Zykluszaehlung).</summary>
    public long CycleCount => _cycleCount;

    /// <summary>Fensterprotokoll in Instanzfolge; echte read-only View.</summary>
    public IReadOnlyList<PressureWindowEvent> Windows => _windowView ??= _windows.AsReadOnly();

    /// <summary>Vorgrenze des letzten definierten Fehlschlags; Sentinel ohne Fehlschlag.</summary>
    public long LastFailureBoundaryTick => _lastFailureBoundaryTick;

    /// <summary>Vertragliche Ursachenkennung des letzten Fehlschlags; null ohne Fehlschlag.</summary>
    public string? LastFailureCause => _lastFailureCause;

    /// <summary>
    /// Folgenzone des fehlgeschlagenen Zyklus als Ankerbindung der
    /// darstellseitigen Neustartanzeige (Druckvertrag Abschnitt 6); Sentinel
    /// -1 ohne Fehlschlag. Der Wert bleibt über den Fehlschlags-/
    /// Neustartzeitraum erhalten, obwohl der Zykluszuruecksetzen die
    /// Folgenzone der Entscheidungsschicht bereits geloescht hat.
    /// </summary>
    public int LastFailureFollowUpZoneIndex => _lastFailureFollowUpZoneIndex;

    /// <summary>Vorgrenze der letzten Angebots-Wiederauffrischung; Sentinel ohne Neustart.</summary>
    public long LastReopenBoundaryTick => _lastReopenBoundaryTick;

    /// <summary>
    /// Offene Wiederauffrischungsregistrierung (Druckvertrag Abschnitt 4):
    /// der Fehlschlag liegt, die Angebots-Wiederauffrischung wurde an der
    /// nächsten Vorgrenze noch nicht registriert. Grundlage der Sektions-
    /// erfassung (Savevertrag V2 Abschnitt 13.1).
    /// </summary>
    public bool RestartPendingRecording => _reopenPendingRecording;

    /// <summary>
    /// Ehrlicher Fehlschlags-/Neustartzustand (Druckvertrag Abschnitt 6,
    /// Anzeigezeitraum der Neustartanzeige): mindestens ein Zyklus begonnen,
    /// kein Fenster offen und die letzte Instanz mit Ablauf geendet — also
    /// ab der Fehlschlagsgrenze bis zur naechsten wirksamen Wahl, nicht mehr
    /// nach einem Erfolg eines Folgazyklus. Die darstellseitige Neustart-
    /// anzeige bindet genau diesen Zustand statt der historischen
    /// Fehlschlagsursache allein, damit ein spaeterer Erfolg niemals eine
    /// rote Neustartanzeige erzeugt.
    /// </summary>
    public bool RestartPending =>
        _cycleCount > 0
        && OpenWindow is null
        && _windows.Count > 0
        && _windows[^1].EndReason == PressureContract.WindowEndReasonExpired;

    /// <summary>Offene Fensterinstanz (null, solange kein Fenster offen ist).</summary>
    public PressureWindowEvent? OpenWindow { get; private set; }

    /// <summary>
    /// Druckbeobachtung an einer Auswertungsgrenze (Vorgrenze T;
    /// <paramref name="boundaryTick"/> ist der Vorgrenze-Tick), in der
    /// vertraglichen Ordnung nach der Entscheidungsbeobachtung (T-035):
    /// (1) beginnt eine neue Fensterinstanz genau an der Vorgrenze einer
    /// wirksamen Entscheidung; (2) endet die offene Instanz an der
    /// persoenlichen Ankunft (Erfolg) oder an der Ablaufgrenze ohne Ankunft
    /// (definierter Fehlschlag mit Ursache und kontrolliertem Zyklus-
    /// zuruecksetzen); (3) wird die Angebots-Wiederauffrischung an der
    /// naechsten Vorgrenze registriert. Die Beobachtung liest ausschließlich
    /// schreibgeschützt und erzeugt niemals einen Kernbefehl.
    /// </summary>
    public void Observe(
        long boundaryTick,
        Riftward.Simulation.SimWorld world,
        SessionMode effectiveMode,
        DecisionSession decision)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(decision);

        // (1) Instanzoeffnung: genau an der Vorgrenze der wirksamen Wahl.
        if (OpenWindow is null
            && decision.Decided
            && decision.DecisionBoundaryTick == boundaryTick)
        {
            _cycleCount++;
            OpenWindow = new PressureWindowEvent(
                Instance: _cycleCount,
                Cycle: _cycleCount,
                StartBoundaryTick: boundaryTick,
                EndBoundaryTick: PressureWindowEvent.UnsetBoundaryTick,
                EndReason: null,
                ArrivalBoundaryTick: PressureWindowEvent.UnsetBoundaryTick,
                ArrivalMode: null,
                FailureCause: null);
            _windows.Add(OpenWindow);
        }

        if (OpenWindow is { } window)
        {
            // (2a) Erfolg: die unveraenderte T-035-Ankunftsregel hat an
            // dieser Vorgrenze abgeschlossen (Beobachtungsordnung: die
            // Entscheidungsbeobachtung lief bereits); die Ankunft an der
            // Ablaufgrenze selbst ist die letzte Gelegenheit im Fenster.
            if (decision.FollowUpCompleted
                && decision.ArrivalBoundaryTick == boundaryTick)
            {
                CloseWindow(
                    window,
                    boundaryTick,
                    PressureContract.WindowEndReasonSuccess,
                    arrivalBoundaryTick: boundaryTick,
                    arrivalMode: effectiveMode == SessionMode.Personal
                        ? ModeContract.ModePersonalId
                        : ModeContract.ModeStrategicId,
                    failureCause: null);
            }
            else if (boundaryTick - window.StartBoundaryTick >= PressureContract.WindowLengthTicks)
            {
                // (2b) Definierter Fehlschlag mit Ursache an der
                // Ablaufgrenze; kontrolliertes Zykluszuruecksetzen schliesst
                // das Angebot, sodass es an der naechsten Vorgrenze
                // deterministisch erneut oeffnet (Entscheidungsvertrag V2).
                CloseWindow(
                    window,
                    boundaryTick,
                    PressureContract.WindowEndReasonExpired,
                    arrivalBoundaryTick: PressureWindowEvent.UnsetBoundaryTick,
                    arrivalMode: null,
                    failureCause: PressureContract.FailureCauseWindowExpired);
                _lastFailureBoundaryTick = boundaryTick;
                _lastFailureCause = PressureContract.FailureCauseWindowExpired;
                _lastFailureFollowUpZoneIndex = decision.FollowUpZoneIndex;
                _reopenPendingRecording = true;
                decision.RestartCycle();
            }
        }

        // (3) Wiederauffrischungsregistrierung: die erste Angebots-Oeffnung
        // nach einem definierten Fehlschlag ist die dokumentierte
        // Wiederauffrischungsgrenze; die urspruengliche Erstoeffnung nach
        // Erkundungsabschluss wird bewusst nicht als Neustart gezaehlt.
        if (_reopenPendingRecording
            && decision.OfferOpened
            && decision.OfferBoundaryTick == boundaryTick)
        {
            _lastReopenBoundaryTick = boundaryTick;
            _reopenPendingRecording = false;
        }
    }

    private void CloseWindow(
        PressureWindowEvent window,
        long endBoundaryTick,
        string endReason,
        long arrivalBoundaryTick,
        string? arrivalMode,
        string? failureCause)
    {
        var closed = window with
        {
            EndBoundaryTick = endBoundaryTick,
            EndReason = endReason,
            ArrivalBoundaryTick = arrivalBoundaryTick,
            ArrivalMode = arrivalMode,
            FailureCause = failureCause,
        };
        _windows[^1] = closed;
        OpenWindow = null;
    }

    /// <summary>
    /// Wiederherstellung aus der Sitzungssektion (Savevertrag V2, Abschnitt
    /// 13; Druckvertrag V2 Abschnitt 14): rekonstruiert Fensterinstanzen,
    /// Zykluszählung, letzten Fehlschlag, Wiederauffrischung und den Offen-
    /// zustand exakt; eine offene Instanz (Endgrenze −1) ist die letzte im
    /// Protokoll. Struktur und Relationswahrheiten der Sektion sind bereits
    /// durch den Loader geprüft.
    /// </summary>
    public static PressureSession Restore(Riftward.Save.SessionSectionState section)
    {
        ArgumentNullException.ThrowIfNull(section);

        var session = new PressureSession();

        foreach (var window in section.PressureWindows)
        {
            var restored = new PressureWindowEvent(
                Instance: window.Instance,
                Cycle: window.Cycle,
                StartBoundaryTick: window.StartBoundaryTick,
                EndBoundaryTick: window.EndBoundaryTick,
                EndReason: window.EndReasonKind switch
                {
                    Riftward.Save.SessionSectionCodec.EndReasonOpen => null,
                    Riftward.Save.SessionSectionCodec.EndReasonSuccess => PressureContract.WindowEndReasonSuccess,
                    Riftward.Save.SessionSectionCodec.EndReasonExpired => PressureContract.WindowEndReasonExpired,
                    _ => throw new InvalidOperationException(
                        "Unbekannter Fensterendgrund erreicht die Wiederherstellung."),
                },
                ArrivalBoundaryTick: window.ArrivalBoundaryTick,
                ArrivalMode: window.ArrivalModeKind switch
                {
                    Riftward.Save.SessionSectionCodec.ArrivalModeNone => null,
                    Riftward.Save.SessionSectionCodec.ModePersonal => ModeContract.ModePersonalId,
                    Riftward.Save.SessionSectionCodec.ModeStrategic => ModeContract.ModeStrategicId,
                    _ => throw new InvalidOperationException(
                        "Unbekannter Ankunftsmodus erreicht die Wiederherstellung."),
                },
                FailureCause: window.FailureCauseKind == Riftward.Save.SessionSectionCodec.CauseKindWindowExpired
                    ? PressureContract.FailureCauseWindowExpired
                    : null);
            session._windows.Add(restored);

            if (restored.EndBoundaryTick == PressureWindowEvent.UnsetBoundaryTick)
            {
                session.OpenWindow = restored;
            }
        }

        session._cycleCount = section.PressureCycleCount;
        session._lastFailureBoundaryTick = section.PressureLastFailureBoundaryTick;
        session._lastFailureCause = section.PressureHasLastFailure != 0
            ? PressureContract.FailureCauseWindowExpired
            : null;
        session._lastFailureFollowUpZoneIndex = section.PressureLastFailureFollowUpZoneIndex;
        session._lastReopenBoundaryTick = section.PressureLastReopenBoundaryTick;
        session._reopenPendingRecording = section.PressureReopenPendingRecording != 0;
        return session;
    }

    /// <summary>
    /// Ehrlicher Endstatus des Laufs (Vertrag Abschnitt 8): nach Erfolg
    /// <c>success</c>, mit offenem Fenster <c>window-open</c>, nach
    /// Fehlschlag ohne erneute Wahl <c>restart-pending</c> und ohne
    /// wirksamen Entscheidungsstand <c>not-started</c> mit Grund.
    /// </summary>
    public (string EndStatus, string? Reason) ResolveEndStatus(DecisionSession decision) =>
        ResolveEndStatusCore(decision.OfferOpened);

    /// <summary>Endstatus ohne Entscheidungsobjekt (Teilreportpfad).</summary>
    internal (string EndStatus, string? Reason) ResolveEndStatus(bool offerOpened) =>
        ResolveEndStatusCore(offerOpened);

    private (string EndStatus, string? Reason) ResolveEndStatusCore(bool offerOpened)
    {
        if (_cycleCount == 0)
        {
            return offerOpened
                ? (PressureContract.EndStatusNotStarted, PressureContract.NotStartedReasonOfferWithoutChoice)
                : (PressureContract.EndStatusNotStarted, PressureContract.NotStartedReasonDecisionNotReached);
        }

        if (OpenWindow is not null)
        {
            return (PressureContract.EndStatusWindowOpen, null);
        }

        return RestartPending
            ? (PressureContract.EndStatusRestartPending, null)
            : (PressureContract.EndStatusSuccess, null);
    }

    /// <summary>
    /// Schreibgeschuetzter Ausweis des Laufs fuer den Report (Vertrag
    /// Abschnitt 8): Momentaufnahme des Druckzustands mit ehrlichem
    /// Endstatus.
    /// </summary>
    public PressureTelemetry ToTelemetry(DecisionSession decision) =>
        ToTelemetryCore(decision.OfferOpened);

    /// <summary>Telemetrie ohne Entscheidungsobjekt (ehrlicher Teilreportpfad).</summary>
    internal PressureTelemetry ToTelemetry(bool offerOpened) => ToTelemetryCore(offerOpened);

    private PressureTelemetry ToTelemetryCore(bool offerOpened)
    {
        var (endStatus, endStatusReason) = ResolveEndStatusCore(offerOpened);

        return new PressureTelemetry(
            WindowLengthTicks: PressureContract.WindowLengthTicks,
            CycleCount: _cycleCount,
            Windows: Array.AsReadOnly(_windows.ToArray()),
            LastFailureBoundaryTick: _lastFailureBoundaryTick,
            LastFailureCause: _lastFailureCause,
            LastReopenBoundaryTick: _lastReopenBoundaryTick,
            EndStatus: endStatus,
            EndStatusReason: endStatusReason);
    }
}
