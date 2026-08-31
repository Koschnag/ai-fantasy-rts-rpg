namespace Riftward.Session;

/// <summary>
/// Maschinenlesbarer Protokolleintrag der Wiederholungsaktion (Vertrag
/// Abschnitt 8): Vorgrenze, Disposition und der Kettenlaufstand nach dem
/// Eintrag. Rein diagnostisch (gateCoupled=false) und nie Teil des
/// Simulationszustands oder Hashes.
/// </summary>
public sealed record MissionRepeatEvent(
    long BoundaryTick,
    string Disposition,
    long ChainRunAfter);

/// <summary>
/// Maschinenlesbarer schreibgeschuetzter Ausweis des sitzungslokalen
/// Abschluss- und Wiederholungszustands (Vertrag Abschnitt 8). Nicht
/// gesetzte Grenzen tragen den Sentinel
/// <see cref="UnsetBoundaryTick"/>, nicht gefallene Angaben null. Niemals
/// Teil von Simulationszustand oder Hash; die Kettenlauf-Anzahl ist als
/// versioniertes Sektionsfeld fortsetzbar (Savevertrag V3 Abschnitt 15),
/// die Abschlusswahrheit selbst ist abgeleitet und trägt kein
/// Persistenzbyte.
/// </summary>
public sealed record MissionTelemetry(
    string CompletionState,
    long CompletionBoundaryTick,
    string? CompletionStateReason,
    long ChainRunCount,
    IReadOnlyList<MissionRepeatEvent> RepeatProtocol,
    long RepeatRejectionsBeforeCompletion)
{
    /// <summary>Sentinel fuer nicht gesetzte Grenzen.</summary>
    public const long UnsetBoundaryTick = -1;
}

/// <summary>
/// Sitzungslokale Abschluss- und Wiederholungsschicht (T-039,
/// Abschlussvertrag Abschnitte 2 bis 5): rein sitzungsseitige Beobachtung
/// und Semantik an der Vorgrenze über der Kette T-034 bis T-036. Sie
/// erzeugt niemals einen Kernbefehl, verändert keinen Befehlszustand, liest
/// ausschließlich die bestehenden Schichtwahrheiten schreibgeschützt und
/// ist zu keinem Zeitpunkt Teil des Simulationszustands oder Hashes.
///
/// Der Abschlusszustand ist die abgeleitete, reine Funktion der bestehenden
/// Schichtwahrheiten der aktuellen Kette
/// (<c>derived-completion-state-pure-function-v1</c>): Druckendstatus
/// <c>success</c> des aktuellen Zyklus plus abgeschlossene Entscheidung
/// plus abgeschlossene Erkundung. Die Abschlussgrenze ist die erste
/// Auswertungsgrenze der Kette, an der die Ableitung gilt
/// (<c>derived-completion-first-boundary-observation-v1</c>). Die
/// Wiederholen-Aktion ist ausschließlich im Abschlusszustand wirksam
/// (<c>mission-repeat-completion-only-v1</c>) und setzt die gesamte
/// sitzungslokale Kette kontrolliert zurück
/// (<c>full-chain-restart-including-visit-protocol-v1</c>), ohne Welt-,
/// Simulations-, Kernbefehls- oder Hashänderung (ADR 008); die
/// Kettenlauf-Anzahl erhöht sich um genau eins.
/// </summary>
public sealed class MissionSession
{
    /// <summary>Sentinel fuer nicht gesetzte Grenzen.</summary>
    public const long UnsetBoundaryTick = MissionTelemetry.UnsetBoundaryTick;

    private readonly List<MissionRepeatEvent> _repeatEvents = new();
    private IReadOnlyList<MissionRepeatEvent>? _repeatEventView;

    private long _chainRunCount = 1;
    private long _completionBoundaryTick = UnsetBoundaryTick;
    private long _repeatRejectionsBeforeCompletion;

    /// <summary>Kettenlauf-Anzahl der aktuellen Kette; beginnt bei 1.</summary>
    public long ChainRunCount => _chainRunCount;

    /// <summary>
    /// Beobachtete Abschlussgrenze der aktuellen Kette; Sentinel, solange
    /// die abgeleitete Funktion an keiner beobachteten Grenze galt.
    /// </summary>
    public long CompletionBoundaryTick => _completionBoundaryTick;

    /// <summary>Wiederholungsprotokoll in Eintrittsfolge; echte read-only View.</summary>
    public IReadOnlyList<MissionRepeatEvent> RepeatProtocol =>
        _repeatEventView ??= _repeatEvents.AsReadOnly();

    /// <summary>
    /// Abweisungen der Wiederholen-Aktion vor dem abgeleiteten Abschluss
    /// (Sitzungsgesamtwert; der Kettenneustart setzt ihn nicht zurück,
    /// Präzedenz Druckvertrag Abschnitt 4).
    /// </summary>
    public long RepeatRejectionsBeforeCompletion => _repeatRejectionsBeforeCompletion;

    /// <summary>
    /// Abschlussbeobachtung an einer Auswertungsgrenze (Vorgrenze T) in der
    /// vertraglichen Ordnung nach der Druckbeobachtung (T-036): bindet die
    /// erste Grenze der aktuellen Kette, an der die abgeleitete Funktion
    /// über den Schichtwahrheiten gilt. Die Beobachtung liest ausschließlich
    /// schreibgeschützt und erzeugt niemals einen Kernbefehl.
    /// </summary>
    public void Observe(
        long boundaryTick,
        ExplorationSession exploration,
        DecisionSession decision,
        PressureSession pressure)
    {
        ArgumentNullException.ThrowIfNull(exploration);
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(pressure);

        if (_completionBoundaryTick == UnsetBoundaryTick
            && IsDerivedCompleted(exploration, decision, pressure))
        {
            _completionBoundaryTick = boundaryTick;
        }
    }

    /// <summary>
    /// Abgeleitete Abschlussfunktion des Vertrags (Abschnitt 2,
    /// <c>derived-completion-state-pure-function-v1</c>): totale, reine
    /// Funktion der drei Schichtwahrheiten der aktuellen Kette. Rein
    /// sitzungsseitig, ohne Kernberührung.
    /// </summary>
    internal static bool IsDerivedCompleted(
        ExplorationSession exploration,
        DecisionSession decision,
        PressureSession pressure)
    {
        var (pressureEndStatus, _) = pressure.ResolveEndStatus(decision);

        return pressureEndStatus == PressureContract.EndStatusSuccess
            && decision.FollowUpCompleted
            && exploration.Completed;
    }

    /// <summary>
    /// Auswertung der Wiederholen-Aktion an ihrer Vorgrenze
    /// (<c>mission-repeat-completion-only-v1</c>): ausschließlich im
    /// abgeleiteten Abschlusszustand wirksam; vor dem Abschluss wird die
    /// Aktion mit der unterscheidbaren Klasse
    /// <see cref="MissionContract.RejectReasonRepeatBeforeCompletion"/>
    /// abgewiesen und verändert nachweislich nichts außer dem
    /// Abweisungszähler und dem Protokolleintrag. Die wirksame Aktion
    /// erhöht die Kettenlauf-Anzahl um genau eins und löscht die
    /// Abschlussgrenzen-Beobachtung der zurückgesetzten Kette; die
    /// Schichtresets ruft der Aufrufer (Pipeline) in vertraglicher Ordnung.
    /// </summary>
    public bool TryRepeat(long boundaryTick)
    {
        if (_completionBoundaryTick == UnsetBoundaryTick)
        {
            _repeatRejectionsBeforeCompletion++;
            _repeatEvents.Add(new MissionRepeatEvent(
                BoundaryTick: boundaryTick,
                Disposition: MissionContract.RepeatDispositionRejectedBeforeCompletion,
                ChainRunAfter: _chainRunCount));
            return false;
        }

        _chainRunCount++;
        _completionBoundaryTick = UnsetBoundaryTick;
        _repeatEvents.Add(new MissionRepeatEvent(
            BoundaryTick: boundaryTick,
            Disposition: MissionContract.RepeatDispositionApplied,
            ChainRunAfter: _chainRunCount));
        return true;
    }

    /// <summary>
    /// Wiederherstellung aus der Sitzungssektion (Savevertrag V3, Abschnitt
    /// 15; Abschlussvertrag Abschnitt 5): rekonstruiert die Kettenlauf-
    /// Anzahl exakt; die Abschlussgrenzen-Beobachtung ist eine Laufoptik
    /// und leitet sich an der ersten beobachteten Grenze des Fortsetzungs-
    /// laufs erneut ab. Struktur und Relationswahrheiten der Sektion sind
    /// bereits durch den Loader geprüft.
    /// </summary>
    public static MissionSession Restore(Riftward.Save.SessionSectionState section)
    {
        ArgumentNullException.ThrowIfNull(section);

        return new MissionSession
        {
            _chainRunCount = section.MissionChainRunCount,
        };
    }

    /// <summary>
    /// Schreibgeschuetzter Ausweis des Laufs fuer den Report (Vertrag
    /// Abschnitt 8): Momentaufnahme des Abschluss- und Wiederholungs-
    /// zustands mit ehrlichem Abschlusszustand. Der abgeleitete Zustand
    /// wird als Fallback aus den Schichtwahrheiten gelesen, falls die
    /// Grenzenbeobachtung im Lauf noch keine Gelegenheit hatte (ehrlicher
    /// Teilreportpfad).
    /// </summary>
    public MissionTelemetry ToTelemetry(bool derivedCompleted)
    {
        var completed = _completionBoundaryTick != UnsetBoundaryTick || derivedCompleted;

        return new MissionTelemetry(
            CompletionState: completed
                ? MissionContract.CompletionStateCompleted
                : MissionContract.CompletionStateOpen,
            CompletionBoundaryTick: completed && _completionBoundaryTick != UnsetBoundaryTick
                ? _completionBoundaryTick
                : UnsetBoundaryTick,
            CompletionStateReason: completed
                ? null
                : MissionContract.OpenReasonNoCycleSuccess,
            ChainRunCount: _chainRunCount,
            RepeatProtocol: Array.AsReadOnly(_repeatEvents.ToArray()),
            RepeatRejectionsBeforeCompletion: _repeatRejectionsBeforeCompletion);
    }
}
