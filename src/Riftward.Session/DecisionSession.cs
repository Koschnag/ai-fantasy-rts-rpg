namespace Riftward.Session;

/// <summary>Die genau zwei vertraglichen Angebotsoptionen (Vertrag Abschnitt 3).</summary>
public enum DecisionChoiceOption : byte
{
    /// <summary>Option A: Zone der zuerst registrierten Landmarke.</summary>
    A = 0,

    /// <summary>Option B: Zone der zuletzt registrierten Landmarke.</summary>
    B = 1,
}

/// <summary>
/// Maschinenlesbarer schreibgeschützter Ausweis des sitzungslokalen
/// Entscheidungszustands (Vertrag Abschnitt 8). Nicht gesetzte Grenzen und
/// Zonen tragen den Sentinel -1, nicht gefallene Angaben null. Niemals Teil
/// von Simulationszustand oder Hash; niemals persistiert.
/// </summary>
public sealed record DecisionTelemetry(
    bool OfferOpened,
    long OfferBoundaryTick,
    int OptionZoneA,
    int OptionZoneB,
    bool Decided,
    long DecisionBoundaryTick,
    string? Choice,
    string? DecisionMode,
    int FollowUpZoneIndex,
    bool FollowUpCompleted,
    long ArrivalBoundaryTick,
    long ChooseRejectionsBeforeOffer,
    long ChooseRejectionsInStrategicMode,
    long ChooseRejectionsAfterDecision)
{
    /// <summary>Sentinel für nicht gesetzte Grenzen und Zonen.</summary>
    public const long UnsetBoundaryTick = -1;

    /// <summary>Sentinel für nicht gesetzte Zonen.</summary>
    public const int UnsetZoneIndex = -1;
}

/// <summary>
/// Sitzungslokale Entscheidungsschicht (T-035, Entscheidungsvertrag
/// Abschnitte 2 bis 5): rein sitzungsseitige Beobachtung und Semantik an der
/// Vorgrenze über dem abgenommenen T-034-Erkundungsabschluss. Sie erzeugt
/// niemals einen Kernbefehl, verändert keinen Befehlszustand, liest
/// ausschließlich Heldenzone und wirksamen Sitzungsmodus schreibgeschützt
/// und ist zu keinem Zeitpunkt Teil des Simulationszustands oder Hashes.
/// Das Angebot öffnet genau an der ersten Auswertungsgrenze mit
/// abgeschlossenem Erkundungsauftrag, genau einmal je Sitzung
/// (<c>completion-gated-decision-offer-v1</c>); die zwei Optionen sind die
/// Zone der zuerst und die der zuletzt registrierten Landmarke des
/// Aufsuchprotokolls (<c>visit-protocol-zone-options-v1</c>, fail-closed
/// gegen den Degenerationsfall); die Wahl ist nur bei offenem Angebot im
/// persönlichen Modus und vor gefallener Entscheidung wirksam
/// (<c>decision-choose-personal-mode-only-v1</c>, Auswertungsordnung
/// <c>decision-choice-evaluation-order-v1</c>); die gewählte Zone wird
/// einmaliges, sichtbares Folgeziel, dessen Abschluss die persönliche
/// Anwesenheit des Vertragshelden an einer Vorgrenze im persönlichen Modus
/// beobachtet (<c>boundary-arrival-personal-mode-only-v1</c>).
/// </summary>
public sealed class DecisionSession
{
    private bool _offerOpened;
    private long _offerBoundaryTick = DecisionTelemetry.UnsetBoundaryTick;
    private int _optionZoneA = DecisionTelemetry.UnsetZoneIndex;
    private int _optionZoneB = DecisionTelemetry.UnsetZoneIndex;

    private bool _decided;
    private long _decisionBoundaryTick = DecisionTelemetry.UnsetBoundaryTick;
    private string? _choice;
    private string? _decisionMode;
    private int _followUpZoneIndex = DecisionTelemetry.UnsetZoneIndex;

    private bool _followUpCompleted;
    private long _arrivalBoundaryTick = DecisionTelemetry.UnsetBoundaryTick;

    private long _chooseRejectionsBeforeOffer;
    private long _chooseRejectionsInStrategicMode;
    private long _chooseRejectionsAfterDecision;

    /// <summary>Angebotsoffenzustand; genau einmal je Sitzung öffnend.</summary>
    public bool OfferOpened => _offerOpened;

    /// <summary>Vorgrenze der Angebotsöffnung; Sentinel vor der Öffnung.</summary>
    public long OfferBoundaryTick => _offerBoundaryTick;

    /// <summary>Optionszone A (zuerst registrierte Landmarke); Sentinel vor der Öffnung.</summary>
    public int OptionZoneA => _optionZoneA;

    /// <summary>Optionszone B (zuletzt registrierte Landmarke); Sentinel vor der Öffnung.</summary>
    public int OptionZoneB => _optionZoneB;

    /// <summary>Entscheidungszustand; nach der Wahl unwiderruflich (keine zweite Wahl).</summary>
    public bool Decided => _decided;

    /// <summary>Vorgrenze der wirksamen Wahl; Sentinel vor der Wahl.</summary>
    public long DecisionBoundaryTick => _decisionBoundaryTick;

    /// <summary>Vertragliche Wahlkennung (<c>a</c>/<c>b</c>); null vor der Wahl.</summary>
    public string? Choice => _choice;

    /// <summary>Vertraglicher Modusname der wirksamen Wahl; null vor der Wahl.</summary>
    public string? DecisionMode => _decisionMode;

    /// <summary>Folgenzone (gewählte Zone); Sentinel vor der Wahl.</summary>
    public int FollowUpZoneIndex => _followUpZoneIndex;

    /// <summary>Abschlusskennung der Folge; genau einmal je Sitzung.</summary>
    public bool FollowUpCompleted => _followUpCompleted;

    /// <summary>Vorgrenze des Folgeabschlusses; Sentinel vor dem Abschluss.</summary>
    public long ArrivalBoundaryTick => _arrivalBoundaryTick;

    /// <summary>Abweisungen vor der Angebotsöffnung (Auswertungsordnung Stufe 2).</summary>
    public long ChooseRejectionsBeforeOffer => _chooseRejectionsBeforeOffer;

    /// <summary>Abweisungen im strategischen Modus (Auswertungsordnung Stufe 3).</summary>
    public long ChooseRejectionsInStrategicMode => _chooseRejectionsInStrategicMode;

    /// <summary>Abweisungen nach gefallener Wahl (Auswertungsordnung Stufe 4).</summary>
    public long ChooseRejectionsAfterDecision => _chooseRejectionsAfterDecision;

    /// <summary>
    /// Entscheidungsbeobachtung an einer Auswertungsgrenze (Vorgrenze T;
    /// <paramref name="boundaryTick"/> ist der Vorgrenze-Tick), in der
    /// vertraglichen Ordnung nach der Intentverarbeitung und der
    /// Erkundungsbeobachtung: öffnet das Angebot genau an der ersten Grenze
    /// mit abgeschlossenem Erkundungsauftrag und beobachtet danach die
    /// persönliche Ankunft in der Folgenzone. Die Beobachtung liest
    /// ausschließlich Heldenzone und Sitzungsmodus schreibgeschützt und
    /// erzeugt niemals einen Kernbefehl.
    /// </summary>
    public void Observe(
        long boundaryTick,
        Riftward.Simulation.SimWorld world,
        SessionMode effectiveMode,
        ExplorationSession exploration)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(exploration);

        if (!_offerOpened && exploration.Completed)
        {
            OpenOffer(boundaryTick, exploration);
        }

        if (_decided
            && !_followUpCompleted
            && effectiveMode == SessionMode.Personal
            && HeroTracker.ZoneIndexOf(world) == _followUpZoneIndex)
        {
            _followUpCompleted = true;
            _arrivalBoundaryTick = boundaryTick;
        }
    }

    /// <summary>
    /// Öffnet das Angebot genau einmal: Optionsableitung als reine Funktion
    /// des Aufsuchprotokolls (zuerst und zuletzt registrierte Zone);
    /// weniger als zwei verschiedene Zonen brechen kontrolliert mit dem
    /// vertraglichen Vertragsfehler ab (Vertrag Abschnitt 3), statt ein
    /// entwertetes Angebot zu öffnen.
    /// </summary>
    private void OpenOffer(long boundaryTick, ExplorationSession exploration)
    {
        var (firstZone, lastZone) = DeriveOptions(exploration.VisitProtocol);

        _offerOpened = true;
        _offerBoundaryTick = boundaryTick;
        _optionZoneA = firstZone;
        _optionZoneB = lastZone;
    }

    /// <summary>
    /// Reine Optionsableitung des Vertrags (<c>visit-protocol-zone-options-
    /// v1</c>, Vertrag Abschnitt 3): Option A ist die Zone der zuerst, Option
    /// B die der zuletzt registrierten Landmarke; weniger als zwei
    /// verschiedene Zonen brechen kontrolliert mit dem vertraglichen
    /// Vertragsfehler ab. Interne Testbindung des Fail-closed-Randfalls, der
    /// im gebundenen Erkundungsvertrag (jede Landmarke registriert
    /// hoechstens einmal, der Abschluss verlangt saemtliche Zonen)
    /// unerreichbar ist.
    /// </summary>
    internal static (int OptionZoneA, int OptionZoneB) DeriveOptions(
        IReadOnlyList<ExplorationVisit> protocol)
    {
        ArgumentNullException.ThrowIfNull(protocol);

        if (protocol.Count < 2)
        {
            throw new InvalidOperationException(
                $"{DecisionContract.RejectReasonInsufficientDistinctZones}: das Aufsuchprotokoll traegt {protocol.Count} Registrierungen; kontrollierter Vertragsfehler statt Angebotsöffnung.");
        }

        var firstZone = protocol[0].ZoneIndex;
        var lastZone = protocol[^1].ZoneIndex;

        if (firstZone == lastZone)
        {
            throw new InvalidOperationException(
                $"{DecisionContract.RejectReasonInsufficientDistinctZones}: zuerst ({firstZone}) und zuletzt ({lastZone}) registrierte Zone sind identisch; kontrollierter Vertragsfehler statt Angebotsöffnung.");
        }

        return (firstZone, lastZone);
    }

    /// <summary>
    /// Auswertung einer Entscheidungsaktion an ihrer Vorgrenze in der
    /// vertraglichen Auswertungsordnung (<c>decision-choice-evaluation-order-v1</c>):
    /// Rückgabe false bei kontrollierter, unterscheidbarer Abweisung ohne
    /// Kernaenderung; Rückgabe true bindet die gewählte Zone einmalig als
    /// Folgeziel. Die Wahl ist unwiderruflich (keine zweite Wahl in
    /// derselben Sitzung).
    /// </summary>
    public bool TryChoose(DecisionChoiceOption option, long boundaryTick, SessionMode effectiveMode)
    {
        if (!_offerOpened)
        {
            _chooseRejectionsBeforeOffer++;
            return false;
        }

        if (effectiveMode != SessionMode.Personal)
        {
            _chooseRejectionsInStrategicMode++;
            return false;
        }

        if (_decided)
        {
            _chooseRejectionsAfterDecision++;
            return false;
        }

        var chosenZone = option == DecisionChoiceOption.A ? _optionZoneA : _optionZoneB;
        _decided = true;
        _decisionBoundaryTick = boundaryTick;
        _choice = option == DecisionChoiceOption.A
            ? DecisionContract.ChoiceOptionAId
            : DecisionContract.ChoiceOptionBId;
        _decisionMode = ModeContract.ModePersonalId;
        _followUpZoneIndex = chosenZone;
        return true;
    }

    /// <summary>
    /// Interne Testbindung (Präzedenz <see cref="DeriveOptions"/>): öffnet
    /// das Angebot mit fixierten Optionszonen ohne Erkundungslauf, damit
    /// die Druckordnung (Ablaufgrenze exakt an Start + WindowLengthTicks,
    /// Ankunft an der Ablaufgrenze als letzte Gelegenheit) unabhängig von
    /// der Erkundungsdauer vertraglich gebunden werden kann. Kein
    /// Produktionspfad ruft diese Methode; die Pipeline öffnet das Angebot
    /// ausschließlich über <see cref="Observe"/> mit abgeschlossenem
    /// Erkundungsauftrag.
    /// </summary>
    internal void OpenOfferForContractTest(long boundaryTick, int optionZoneA, int optionZoneB)
    {
        _offerOpened = true;
        _offerBoundaryTick = boundaryTick;
        _optionZoneA = optionZoneA;
        _optionZoneB = optionZoneB;
    }

    /// <summary>
    /// Autorisierte additive Zyklus-Praezisierung (Entscheidungsvertrag V2,
    /// Abschnitt 13; Druckvertrag Abschnitt 4): beendet den abgelaufenen
    /// Auftragszyklus kontrolliert nach definiertem Fehlschlag — Angebot,
    /// Wahl, Folge und Ankunft des Zyklus werden sitzungsseitig
    /// zurueckgesetzt, sodass das Angebot an der naechsten Vorgrenze
    /// deterministisch mit unveraenderter Optionsableitung erneut oeffnet.
    /// Die Sitzungsabweisungszaehler bleiben unverändert Sitzungsgesamtwerte.
    /// Nur von der Druckschicht aufrufbar; ohne Druckschicht bleibt das
    /// Verhalten exakt dem Entscheidungsvertrag V2-Einmalzyklus.
    /// </summary>
    internal void RestartCycle()
    {
        _offerOpened = false;
        _offerBoundaryTick = DecisionTelemetry.UnsetBoundaryTick;
        _optionZoneA = DecisionTelemetry.UnsetZoneIndex;
        _optionZoneB = DecisionTelemetry.UnsetZoneIndex;
        _decided = false;
        _decisionBoundaryTick = DecisionTelemetry.UnsetBoundaryTick;
        _choice = null;
        _decisionMode = null;
        _followUpZoneIndex = DecisionTelemetry.UnsetZoneIndex;
        _followUpCompleted = false;
        _arrivalBoundaryTick = DecisionTelemetry.UnsetBoundaryTick;
    }

    /// <summary>
    /// Schreibgeschützter Ausweis des Laufs für den Report (Vertrag
    /// Abschnitt 8): Momentaufnahme des Entscheidungszustands.
    /// </summary>
    public DecisionTelemetry ToTelemetry() => new(
        OfferOpened: _offerOpened,
        OfferBoundaryTick: _offerBoundaryTick,
        OptionZoneA: _optionZoneA,
        OptionZoneB: _optionZoneB,
        Decided: _decided,
        DecisionBoundaryTick: _decisionBoundaryTick,
        Choice: _choice,
        DecisionMode: _decisionMode,
        FollowUpZoneIndex: _followUpZoneIndex,
        FollowUpCompleted: _followUpCompleted,
        ArrivalBoundaryTick: _arrivalBoundaryTick,
        ChooseRejectionsBeforeOffer: _chooseRejectionsBeforeOffer,
        ChooseRejectionsInStrategicMode: _chooseRejectionsInStrategicMode,
        ChooseRejectionsAfterDecision: _chooseRejectionsAfterDecision);
}
