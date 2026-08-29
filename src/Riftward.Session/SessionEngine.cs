using System.Diagnostics;

namespace Riftward.Session;

/// <summary>Disposition eines Intents im Sitzungsdurchlauf (UF-001-konform).</summary>
public enum IntentDisposition : byte
{
    /// <summary>Intent wurde ausgefuehrt (einschliesslich definierter Leer-Klick-Deselektion).</summary>
    Applied = 0,

    /// <summary>Bewegung ohne ausgewaehlte Gruppe abgewiesen (fachliche Ursache).</summary>
    RejectedNoSelection = 1,

    /// <summary>Zielpunkt liegt in keiner Zone (nur Interaktivmodus moeglich).</summary>
    RejectedNoZoneAtPoint = 2,

    /// <summary>Intent traf erst nach seiner Zielvorgrenze ein (nur Live-Eingaben).</summary>
    RejectedLate = 3,

    /// <summary>Strategischer Intent im persoenlichen Modus abgewiesen (T-033, Modevertrag Abschnitt 5).</summary>
    RejectedStrategyIntentInPersonalMode = 4,

    /// <summary>Persoenliche Lenkung im strategischen Modus abgewiesen (T-033, Modevertrag Abschnitt 5).</summary>
    RejectedSteerIntentInStrategyMode = 5,

    /// <summary>Entscheidung vor der Angebotsöffnung abgewiesen (T-035, Auswertungsordnung Stufe 2).</summary>
    RejectedDecisionBeforeOffer = 6,

    /// <summary>Entscheidung im strategischen Modus abgewiesen (T-035, Auswertungsordnung Stufe 3).</summary>
    RejectedDecisionInStrategicMode = 7,

    /// <summary>Entscheidung nach gefallener Wahl abgewiesen (T-035, Auswertungsordnung Stufe 4).</summary>
    RejectedDecisionAfterDecision = 8,

    /// <summary>Entscheidung ohne aktivierte Entscheidungsschicht abgewiesen (T-035, Auswertungsordnung Stufe 1).</summary>
    RejectedDecisionNotActivated = 9,
}

/// <summary>Ergebnis einer Vorgrenzenverarbeitung (Befehlstick).</summary>
public readonly struct BoundaryOutcome
{
    public int AppliedCount { get; init; }

    public int RejectedCount { get; init; }

    /// <summary>Anteil der Abweisungen mit vertraglichem Grund move-without-selection.</summary>
    public int RejectedMoveWithoutSelection { get; init; }

    public int EmptyPointDeselects { get; init; }

    /// <summary>Anzahl an den Kern uebergebener Befehle dieses Ticks.</summary>
    public int CommandCount { get; init; }

    /// <summary>Strategische Intents, die im persoenlichen Modus abgewiesen wurden (T-033).</summary>
    public int RejectedStrategyInPersonal { get; init; }

    /// <summary>Persoenliche Lenkungen, die im strategischen Modus abgewiesen wurden (T-033).</summary>
    public int RejectedSteerInStrategy { get; init; }

    /// <summary>An dieser Vorgrenze ausgewertete Moduswechsel (T-033, diagnostisch).</summary>
    public int SwitchIntentsEvaluated { get; init; }

    /// <summary>An dieser Vorgrenze abgewiesene Entscheidungen vor der Angebotsöffnung (T-035).</summary>
    public int RejectedDecisionBeforeOffer { get; init; }

    /// <summary>An dieser Vorgrenze abgewiesene Entscheidungen im strategischen Modus (T-035).</summary>
    public int RejectedDecisionInStrategicMode { get; init; }

    /// <summary>An dieser Vorgrenze abgewiesene Entscheidungen nach gefallener Wahl (T-035).</summary>
    public int RejectedDecisionAfterDecision { get; init; }

    /// <summary>An dieser Vorgrenze ohne Aktivierung abgewiesene Entscheidungen (T-035).</summary>
    public int RejectedDecisionNotActivated { get; init; }
}

/// <summary>Aggregierte Kennzahlen eines Sitzungslaufs.</summary>
public sealed record SessionMetrics(
    double P50TickTimeMs,
    double P95TickTimeMs,
    double P99TickTimeMs,
    double AllocationsPerWarmTickBytes,
    double GcPauseSumMs,
    long GcPauseCount,
    long MaxReactionTicks,
    long ReactionP50Ticks,
    long ReactionP95Ticks,
    long ReactionP99Ticks,
    long ReactionSampleCount);

/// <summary>Ergebnis eines vollstaendigen headless Sitzungslaufs.</summary>
public sealed record SessionRunResult(
    ulong StartStateHash,
    ulong EndStateHash,
    long[] IntervalSampleTicks,
    ulong[] IntervalHashes,
    bool? StateChainSelfConsistent,
    string[] SelfInconsistencyReasons,
    SessionMetrics Metrics,
    int AppliedIntents,
    int RejectedIntents,
    int EmptyPointDeselects,
    int MoveWithoutSelectionRejects,
    int KernelCommandsTotal,
    int TotalTicksExecuted,
    ModeTelemetry Telemetry,
    ExplorationTelemetry? Exploration = null,
    DecisionTelemetry? Decision = null,
    PressureTelemetry? Pressure = null);

/// <summary>Deterministische Percentil- und Zeitberechnung (Verfahren wie T-010/T-020/T-021).</summary>
public static class SessionMath
{
    /// <summary>Percentil als naechstgroessere Ordnungsstatistik.</summary>
    public static double Percentile(IReadOnlyList<double> valuesInAnyOrder, double fraction)
    {
        if (valuesInAnyOrder.Count == 0)
        {
            return double.NaN;
        }

        if (fraction is <= 0.0 or > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(fraction), "Percentilanteil muss in (0,1] liegen.");
        }

        var sorted = valuesInAnyOrder.ToArray();
        Array.Sort(sorted);
        var index = (int)Math.Ceiling(fraction * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    /// <summary>Percentil ueber Ganzzahlverteilungen (Reaktionsticks), identisches Verfahren.</summary>
    public static long Percentile(IReadOnlyList<long> valuesInAnyOrder, double fraction)
    {
        if (valuesInAnyOrder.Count == 0)
        {
            return 0;
        }

        var sorted = valuesInAnyOrder.ToArray();
        Array.Sort(sorted);
        var index = (int)Math.Ceiling(fraction * sorted.Length) - 1;
        return sorted[Math.Clamp(index, 0, sorted.Length - 1)];
    }

    /// <summary>Stoppuhr-Delta in Millisekunden (Methode identisch zur T-021-Linie).</summary>
    public static double TimestampDeltaToMilliseconds(long startTimestamp, long endTimestamp) =>
        (endTimestamp - startTimestamp) * 1000.0 / Stopwatch.Frequency;
}

/// <summary>
/// Deterministische Sitzungspipeline ueber dem unveränderten Simulationskern
/// (Kommandovertrag Abschnitt 2): verarbeitet an jeder Tickvorgrenze die
/// faelligen Skript- und Live-Intents in kanonischer Ordnung, bildet
/// Bewegungen auf die oeffentliche Kernbefehlsflaeche ab und weist
/// ungueltige Befehle mit fachlicher Ursache ab, bevor der Kern erreicht
/// wird. Es gibt keine endlos pendelnde Order: Jeder Intent wird angewendet
/// oder mit unterscheidbarer Disposition abgewiesen.
/// </summary>
public sealed class SessionPipeline
{
    private readonly GrayboxIntent[] _scriptedIntents;
    private readonly List<GrayboxIntent> _liveQueue = new();
    private readonly List<GrayboxIntent> _boundaryBatch = new();
    private readonly List<int> _dispatchedMoveZones = new();
    private readonly List<ModeSwitchEvent> _switchProtocol = new();
    private readonly List<ModeSwitchEvent> _pendingSwitches = new();
    private readonly ExplorationSession? _exploration;
    private readonly DecisionSession? _decision;
    private readonly PressureSession? _pressure;
    private int _scriptCursor;
    private SessionMode _effectiveMode = SessionMode.Strategic;

    /// <summary>
    /// Zielzonen der an derselben Vorgrenze tatsaechlich an den Kern
    /// uebergebenen Bewegungsbefehle (ein Eintrag je angewendeter Move- oder
    /// Steer-Intent, nicht je Gruppenbefehl). Wird je Vorgrenze geleert;
    /// Abgewiesene und dedupe-Regelte erscheinen nie. Diagnostische Rueckgabe
    /// fuer die darstellseitige Befehlsrueckmeldung (Kommandovertrag
    /// Abschnitt 3, Zweikanal); sie ist niemals Teil des Simulationszustands
    /// oder Hashes.
    /// </summary>
    public IReadOnlyList<int> DispatchedMoveZonesOfLastBoundary => _dispatchedMoveZones;

    public SessionPipeline(
        Riftward.Simulation.SimWorld world,
        SelectionModel selection,
        GrayboxIntent[] scriptedIntents,
        ExplorationSession? exploration = null,
        DecisionSession? decision = null,
        PressureSession? pressure = null)
        : this(world, selection, scriptedIntents, SessionMode.Strategic, Array.Empty<ModeSwitchEvent>(), exploration, decision, pressure)
    {
    }

    /// <summary>
    /// Wiederherstellender Konstruktor (Savevertrag V2, Abschnitt 13): der
    /// Modus und ausgewertete, aber noch nicht wirksame Wechsel werden aus
    /// der Sitzungssektion fortgesetzt, sodass die Vorgrenzenverarbeitung
    /// ab der Ladegrenze exakt dem ununterbrochenen Lauf entspricht. Ohne
    /// Restaurierung gilt der Bestandskonstruktor (Start strategisch).
    /// </summary>
    public SessionPipeline(
        Riftward.Simulation.SimWorld world,
        SelectionModel selection,
        GrayboxIntent[] scriptedIntents,
        SessionMode initialMode,
        IReadOnlyList<ModeSwitchEvent> restoredPendingSwitches,
        ExplorationSession? exploration = null,
        DecisionSession? decision = null,
        PressureSession? pressure = null)
    {
        World = world;
        Selection = selection;
        _scriptedIntents = scriptedIntents;
        _exploration = exploration;
        _decision = decision;
        _pressure = pressure;
        _effectiveMode = initialMode;
        _initialMode = initialMode;
        _pendingSwitches.AddRange(restoredPendingSwitches);

        for (var index = 1; index < scriptedIntents.Length; index++)
        {
            if (scriptedIntents[index].CompareTo(scriptedIntents[index - 1]) < 0)
            {
                throw new ArgumentException("Skriptintents muessen kanonisch vorsortiert sein.", nameof(scriptedIntents));
            }
        }
    }

    private readonly SessionMode _initialMode = SessionMode.Strategic;

    /// <summary>Vertraglicher Startmodus des Laufs (Sitzungszustand; restauriert aus der Sektion).</summary>
    public SessionMode InitialMode => _initialMode;

    /// <summary>
    /// Ausgewertete, aber noch nicht wirksame Wechsel als defensive
    /// Momentaufnahme (Sitzungszustand, niemals Simulationszustand oder
    /// Hash); Grundlage der Sektionserfassung (Savevertrag V2 Abschnitt 13.1).
    /// </summary>
    public IReadOnlyList<ModeSwitchEvent> PendingSwitches => _pendingSwitches.ToArray();

    public Riftward.Simulation.SimWorld World { get; }

    public SelectionModel Selection { get; }

    /// <summary>
    /// Optionale Erkundungssitzung (T-034): rein sitzungsseitige Beobachtung
    /// an jeder Auswertungsgrenze; ohne Aktivierung null (Bestandsverhalten
    /// byteidentisch). Die Beobachtung liest ausschließlich Heldenzone und
    /// Sitzungsmodus schreibgeschützt und erzeugt niemals einen Kernbefehl.
    /// </summary>
    public ExplorationSession? Exploration => _exploration;

    /// <summary>
    /// Optionale Entscheidungssitzung (T-035): rein sitzungsseitige
    /// Beobachtung und Semantik an jeder Auswertungsgrenze; ohne Aktivierung
    /// null (Bestandsverhalten byteidentisch). Die Schicht liest
    /// ausschließlich Heldenzone und Sitzungsmodus schreibgeschützt und
    /// erzeugt niemals einen Kernbefehl. Vertraglich an die
    /// Erkundungsaktivierung gekoppelt: eine Entscheidungsschicht ohne
    /// Erkundungssitzung ist ein Vertragswiderspruch und wird fail-closed
    /// abgewiesen.
    /// </summary>
    public DecisionSession? Decision => _decision;

    /// <summary>
    /// Optionale Druckschicht (T-036): rein sitzungsseitige Beobachtung und
    /// Semantik an jeder Auswertungsgrenze; ohne Aktivierung null
    /// (Bestandsverhalten byteidentisch). Die Schicht liest ausschließlich
    /// Entscheidungszustand, Heldenzone und Sitzungsmodus schreibgeschützt,
    /// erzeugt niemals einen Kernbefehl und ist vertraglich an die
    /// Entscheidungsaktivierung gekoppelt.
    /// </summary>
    public PressureSession? Pressure => _pressure;

    /// <summary>Stellt einen validierten Live-Intent fuer die naechste Vorgrenze bereit.</summary>
    public void EnqueueLiveIntent(GrayboxIntent intent) => _liveQueue.Add(intent);

    /// <summary>Anzahl noch ausstehender Skriptintents.</summary>
    public int RemainingScripted => _scriptedIntents.Length - _scriptCursor;

    /// <summary>
    /// Modus, der nach der zuletzt verarbeiteten Vorgrenze fachlich gilt
    /// (Sitzungszustand, niemals Simulationszustand oder Hash). Ein
    /// ausgewerteter, aber noch nicht wirksamer Wechsel aendert diesen Wert
    /// erst an seiner Wirksamkeitsgrenze M = S + 2.
    /// </summary>
    public SessionMode CurrentEffectiveMode => _effectiveMode;

    /// <summary>Verschmelzte, vertraglich ausgewiesene Wechselprotokoll-Eintraege (T-033):
    /// wirksame Wechsel mit gemessener Wechselreaktion und Heldenstatus,
    /// sowie — nach <see cref="FlushPendingSwitches"/> — ausdrücklich
    /// unwirksam gebliebene Wechsel nahe dem Laufhorizont (EffectiveInRun=false,
    /// Endmoduswahrheit bleibt der Reportendmodus).</summary>
    public IReadOnlyList<ModeSwitchEvent> SwitchProtocol => _switchProtocol;

    /// <summary>Gesamtzaehler strategischer Intents, die im persoenlichen Modus abgewiesen wurden.</summary>
    public long StrategyIntentsRejectedInPersonalModeTotal { get; private set; }

    /// <summary>Gesamtzaehler persoenlicher Lenkungen, die im strategischen Modus abgewiesen wurden.</summary>
    public long SteerIntentsRejectedInStrategyModeTotal { get; private set; }

    /// <summary>Gesamtzaehler ruhezustandsregelter Lenkungen ohne Kernbefehl (Dedupe).</summary>
    public long SteerIdleDedupeTotal { get; private set; }

    /// <summary>
    /// Verarbeitet die Vorgrenze von <paramref name="tick"/>: Skript- und
    /// Live-Intents dieses Ticks werden vereinigt, kanonisch geordnet und
    /// ausgewertet. Zu spaet eingetroffene Live-Intents werden kontrolliert
    /// abgewiesen (RejectedLate); Skriptintents sind durch Validierung nie
    /// zu spaet. Moduswechsel werden kanonisch nach allen anderen Intents
    /// ihres Ticks ausgewertet (Modevertrag Abschnitt 4).
    /// </summary>
    public BoundaryOutcome ProcessBoundary(long tick)
    {
        PromoteDueSwitches(tick);
        _boundaryBatch.Clear();
        _dispatchedMoveZones.Clear();

        while (_scriptCursor < _scriptedIntents.Length && _scriptedIntents[_scriptCursor].Tick <= tick)
        {
            _boundaryBatch.Add(_scriptedIntents[_scriptCursor++]);
        }

        var lateRejected = 0;

        foreach (var live in _liveQueue)
        {
            if (live.Tick < tick)
            {
                // Zielvorgrenze bereits verpasst: kontrollierte fachliche
                // Abweisung statt nachtraeglicher Ausfuehrung.
                Journal(live, IntentDisposition.RejectedLate);
                LateRejectedTotal++;
                lateRejected++;
            }
            else if (live.Tick == tick)
            {
                _boundaryBatch.Add(live);
            }

            // Zukunftsgebundene Live-Intents bleiben bis zu ihrem Tick liegen.
        }

        _liveQueue.RemoveAll(live => live.Tick <= tick);
        _boundaryBatch.Sort();

        var applied = 0;
        var rejected = 0;
        var rejectedMoveWithoutSelection = 0;
        var emptyDeselects = 0;
        var commandTotal = 0;
        var rejectedStrategyInPersonal = 0;
        var rejectedSteerInStrategy = 0;
        var rejectedDecisionBeforeOffer = 0;
        var rejectedDecisionInStrategicMode = 0;
        var rejectedDecisionAfterDecision = 0;
        var rejectedDecisionNotActivated = 0;
        var switchIntentsEvaluated = 0;
        Span<Riftward.Simulation.SimCommand> commands = stackalloc Riftward.Simulation.SimCommand[
            Riftward.Simulation.SimulationContract.GroupCount];

        foreach (var intent in _boundaryBatch)
        {
            // Kontexttrennung (Modevertrag Abschnitt 5): Der Modus, der an
            // dieser Vorgrenze gilt, entscheidet vor jeder Kernuebergabe und
            // vor jeder Auswahlwirkung. Wechsel-Intents sind in beiden Modi
            // gültig und werden kanonisch zuletzt ausgewertet.
            switch (intent.Kind)
            {
                case GrayboxIntentKind.Clear:
                case GrayboxIntentKind.PointSelect:
                case GrayboxIntentKind.BoxSelect:
                case GrayboxIntentKind.GroupMoveToZone:
                    if (_effectiveMode == SessionMode.Personal)
                    {
                        Journal(intent, IntentDisposition.RejectedStrategyIntentInPersonalMode);
                        StrategyIntentsRejectedInPersonalModeTotal++;
                        rejected++;
                        rejectedStrategyInPersonal++;
                        continue;
                    }

                    break;

                case GrayboxIntentKind.SteerGroupToZone:
                    if (_effectiveMode == SessionMode.Strategic)
                    {
                        Journal(intent, IntentDisposition.RejectedSteerIntentInStrategyMode);
                        SteerIntentsRejectedInStrategyModeTotal++;
                        rejected++;
                        rejectedSteerInStrategy++;
                        continue;
                    }

                    break;

                case GrayboxIntentKind.ChooseA:
                case GrayboxIntentKind.ChooseB:
                    // Entscheidungsaktionen sind in beiden Kontexten
                    // grammatisch gueltig; Angebot, Modus und
                    // Entscheidungsstand entscheidet die vertragliche
                    // Auswertungsordnung in EvaluateDecisionChoice
                    // (Entscheidungsvertrag Abschnitt 4).
                    break;

                case GrayboxIntentKind.SwitchMode:
                    break;

                default:
                    throw new InvalidOperationException("Unbekannte Intentart erreicht die Pipeline.");
            }

            switch (intent.Kind)
            {
                case GrayboxIntentKind.Clear:
                    Selection.Clear();
                    Journal(intent, IntentDisposition.Applied);
                    applied++;
                    break;

                case GrayboxIntentKind.PointSelect:
                    var hit = Selection.EvaluatePoint(
                        World,
                        GrayboxIntent.MillimetersToQ16(intent.A),
                        GrayboxIntent.MillimetersToQ16(intent.B));

                    if (!hit)
                    {
                        emptyDeselects++;
                        EmptyPointDeselectTotal++;
                    }

                    Journal(intent, IntentDisposition.Applied);
                    applied++;
                    break;

                case GrayboxIntentKind.BoxSelect:
                    Selection.EvaluateBox(
                        World,
                        GrayboxIntent.MillimetersToQ16(intent.A),
                        GrayboxIntent.MillimetersToQ16(intent.B),
                        GrayboxIntent.MillimetersToQ16(intent.C),
                        GrayboxIntent.MillimetersToQ16(intent.D));
                    Journal(intent, IntentDisposition.Applied);
                    applied++;
                    break;

                case GrayboxIntentKind.GroupMoveToZone:
                    if (Selection.SelectedCount == 0)
                    {
                        Journal(intent, IntentDisposition.RejectedNoSelection);
                        rejected++;
                        rejectedMoveWithoutSelection++;
                        MoveWithoutSelectionTotal++;
                        break;
                    }

                    var commandCount = 0;

                    for (var group = 0; group < Riftward.Simulation.SimulationContract.GroupCount; group++)
                    {
                        if (Selection.IsSelected(group))
                        {
                            commands[commandCount++] = new Riftward.Simulation.SimCommand(
                                (int)tick,
                                group,
                                Riftward.Simulation.SimCommandKind.GroupMoveToZone,
                                checked((int)intent.A));
                        }
                    }

                    // Der Kern ordnet vor der Anwendung kanonisch; die
                    // Uebergabereihenfolge bestimmt niemals das Ergebnis.
                    World.ApplyCommands(commands.Slice(0, commandCount));
                    commandTotal += commandCount;
                    AppliedCommandsTotal += commandCount;
                    _dispatchedMoveZones.Add(checked((int)intent.A));

                    Journal(intent, IntentDisposition.Applied);
                    applied++;
                    break;

                case GrayboxIntentKind.SteerGroupToZone:
                    applied += ApplySteering(intent, tick, ref commandTotal);
                    break;

                case GrayboxIntentKind.ChooseA:
                case GrayboxIntentKind.ChooseB:
                    {
                        var option = intent.Kind == GrayboxIntentKind.ChooseA
                            ? DecisionChoiceOption.A
                            : DecisionChoiceOption.B;
                        var (decisionApplied, decisionDisposition) = EvaluateDecisionChoice(intent, tick, option);

                        switch (decisionDisposition)
                        {
                            case IntentDisposition.RejectedDecisionBeforeOffer:
                                rejectedDecisionBeforeOffer++;
                                break;

                            case IntentDisposition.RejectedDecisionInStrategicMode:
                                rejectedDecisionInStrategicMode++;
                                break;

                            case IntentDisposition.RejectedDecisionAfterDecision:
                                rejectedDecisionAfterDecision++;
                                break;

                            case IntentDisposition.RejectedDecisionNotActivated:
                                rejectedDecisionNotActivated++;
                                break;
                        }

                        if (decisionApplied)
                        {
                            applied++;
                        }
                        else
                        {
                            rejected++;
                        }
                    }

                    break;

                case GrayboxIntentKind.SwitchMode:
                    EvaluateModeSwitch(tick);
                    switchIntentsEvaluated++;
                    Journal(intent, IntentDisposition.Applied);
                    applied++;
                    break;

                default:
                    throw new InvalidOperationException("Unbekannte Intentart erreicht die Pipeline.");
            }
        }

        // Rein sitzungsseitige Erkundungsbeobachtung an dieser Vorgrenze
        // (T-034, Vertrag Abschnitt 3): liest ausschließlich Heldenzone und
        // Sitzungsmodus schreibgeschützt, nach der Intentverarbeitung und
        // vor dem Tick; Heldenposition und Modus sind an dieser Vorgrenze
        // stabil. Ohne Aktivierung null — Bestandsverhalten unverändert.
        _exploration?.Observe(tick, World, _effectiveMode);

        // Rein sitzungsseitige Entscheidungsbeobachtung an derselben
        // Vorgrenze (T-035, Entscheidungsvertrag Abschnitt 2), in der
        // vertraglichen Ordnung nach der Erkundungsbeobachtung: öffnet das
        // Angebot an der ersten Abschlussgrenze und beobachtet die
        // persönliche Ankunft in der Folgenzone. Ohne Aktivierung null —
        // Bestandsverhalten unverändert.
        if (_decision is { } decision && _exploration is not null)
        {
            decision.Observe(tick, World, _effectiveMode, _exploration);
        }

        // Rein sitzungsseitige Druckbeobachtung an derselben Vorgrenze
        // (T-036, Druckvertrag Abschnitt 2 und 9), in der vertraglichen
        // Ordnung nach der Entscheidungsbeobachtung: startet die
        // Fensterinstanz an der Wahlgrenze und beobachtet Erfolg, Fehlschlag
        // mit Ursache und Wiederauffrischung. Ohne Aktivierung null —
        // Bestandsverhalten unverändert.
        if (_pressure is { } pressure && _decision is { } boundDecision && _exploration is not null)
        {
            pressure.Observe(tick, World, _effectiveMode, boundDecision);
        }

        return new BoundaryOutcome
        {
            AppliedCount = applied,
            RejectedCount = rejected + lateRejected,
            RejectedMoveWithoutSelection = rejectedMoveWithoutSelection,
            EmptyPointDeselects = emptyDeselects,
            CommandCount = commandTotal,
            RejectedStrategyInPersonal = rejectedStrategyInPersonal,
            RejectedSteerInStrategy = rejectedSteerInStrategy,
            SwitchIntentsEvaluated = switchIntentsEvaluated,
            RejectedDecisionBeforeOffer = rejectedDecisionBeforeOffer,
            RejectedDecisionInStrategicMode = rejectedDecisionInStrategicMode,
            RejectedDecisionAfterDecision = rejectedDecisionAfterDecision,
            RejectedDecisionNotActivated = rejectedDecisionNotActivated,
        };
    }

    /// <summary>
    /// Auswertung einer Entscheidungsaktion an ihrer Vorgrenze (T-035,
    /// Entscheidungsvertrag Abschnitt 4, Auswertungsordnung
    /// decision-choice-evaluation-order-v1): rein sitzungsseitig, ohne
    /// Kernbefehl, ohne Zustandsaenderung am Kern; jede Verletzung wird mit
    /// unterscheidbarer Disposition abgewiesen und gezaehlt. Rueckgabe ist
    /// das Paar (wirksam, Disposition).
    /// </summary>
    private (bool Applied, IntentDisposition Disposition) EvaluateDecisionChoice(
        GrayboxIntent intent,
        long tick,
        DecisionChoiceOption option)
    {
        if (_decision is not { } decision)
        {
            ChooseIntentsRejectedWithoutActivationTotal++;
            return (false, Journal(intent, IntentDisposition.RejectedDecisionNotActivated));
        }

        if (decision.TryChoose(option, tick, _effectiveMode))
        {
            return (true, Journal(intent, IntentDisposition.Applied));
        }

        if (decision.OfferBoundaryTick == DecisionTelemetry.UnsetBoundaryTick)
        {
            ChooseIntentsRejectedBeforeOfferTotal++;
            return (false, Journal(intent, IntentDisposition.RejectedDecisionBeforeOffer));
        }

        if (_effectiveMode != SessionMode.Personal)
        {
            ChooseIntentsRejectedInStrategicModeTotal++;
            return (false, Journal(intent, IntentDisposition.RejectedDecisionInStrategicMode));
        }

        ChooseIntentsRejectedAfterDecisionTotal++;
        return (false, Journal(intent, IntentDisposition.RejectedDecisionAfterDecision));
    }

    /// <summary>
    /// Fuehrt eine durchgelassene persoenliche Lenkung aus: Dedupe-Regel gegen
    /// das aktuelle Kernziel der Heldengruppe, sonst exakt ein Kernbefehl
    /// GroupMoveToZone auf Gruppe 0 (Modevertrag Abschnitt 3).
    /// </summary>
    private int ApplySteering(GrayboxIntent intent, long tick, ref int commandTotal)
    {
        var zone = checked((int)intent.A);

        if (World.TargetZoneOfGroup(ModeContract.HeroGroupIndex) == zone)
        {
            SteerIdleDedupeTotal++;
            Journal(intent, IntentDisposition.Applied);
            return 1;
        }

        Span<Riftward.Simulation.SimCommand> steeringCommand = stackalloc Riftward.Simulation.SimCommand[1];
        steeringCommand[0] = new Riftward.Simulation.SimCommand(
            (int)tick,
            ModeContract.HeroGroupIndex,
            Riftward.Simulation.SimCommandKind.GroupMoveToZone,
            zone);
        World.ApplyCommands(steeringCommand);
        commandTotal++;
        AppliedCommandsTotal++;
        _dispatchedMoveZones.Add(zone);
        Journal(intent, IntentDisposition.Applied);
        return 1;
    }

    /// <summary>
    /// Auswertung eines Wechsel-Intents an der Vorgrenze seines Ticks: kein
    /// Kernbefehl, kein Simulationszustand; die Wirkung tritt erstmals an der
    /// uebernächsten Gültigkeitsprüfung M = S + 2 in Kraft (Modevertrag
    /// Abschnitt 4).
    /// </summary>
    private void EvaluateModeSwitch(long tick)
    {
        var newMode = _effectiveMode == SessionMode.Strategic ? SessionMode.Personal : SessionMode.Strategic;
        _pendingSwitches.Add(new ModeSwitchEvent(
            IntentTick: tick,
            EvaluatedBoundaryTick: tick,
            EffectiveBoundaryTick: tick + ModeContract.SwitchReactionTargetTicks,
            PreviousMode: _effectiveMode,
            NewMode: newMode,
            EffectiveInRun: false,
            SwitchReactionTicks: 0,
            HeroPositionXMm: 0,
            HeroPositionYMm: 0,
            HeroZoneIndex: -1,
            HeroPathState: 0));
    }

    /// <summary>
    /// Fördert fällige Wechsel an ihre Wirksamkeitsgrenze: ab der Vorgrenze
    /// M = S + 2 bildet der neue Modus den Kontext; der Ausweis liest den
    /// Heldenstatus (Position, Zone, Pfadstatus) von Agentenindex 0 am
    /// Wirksamkeitszeitpunkt schreibgeschützt aus dem Kern.
    /// </summary>
    private void PromoteDueSwitches(long tick)
    {
        for (var index = _pendingSwitches.Count - 1; index >= 0; index--)
        {
            var pending = _pendingSwitches[index];

            if (pending.EffectiveBoundaryTick != tick)
            {
                continue;
            }

            _pendingSwitches.RemoveAt(index);
            _effectiveMode = pending.NewMode;
            _switchProtocol.Add(pending with
            {
                EffectiveInRun = true,
                SwitchReactionTicks = tick - pending.IntentTick,
                HeroPositionXMm = HeroTracker.PositionXMm(World),
                HeroPositionYMm = HeroTracker.PositionYMm(World),
                HeroZoneIndex = HeroTracker.ZoneIndexOf(World),
                HeroPathState = HeroTracker.PathStateOf(World),
            });
        }
    }

    /// <summary>
    /// Schließt den Lauf ab: ausgewertete Wechsel, deren Wirksamkeitsgrenze
    /// M = S + 2 hinter dem Laufhorizont läge, werden ausdrücklich mit
    /// EffectiveInRun=false ins Wechselprotokoll übernommen, statt still zu
    /// verschwinden (Modevertrag Abschnitt 4); der Sitzungsmodus ändert sich
    /// an keiner Wirksamkeitsgrenze mehr, der Reportendmodus bleibt die
    /// Wahrheit des Laufs. Headless ruft <see cref="SessionEngine.Run"/> diese
    /// Methode nach dem Messfenster, der Interaktivpfad nach dem Loop-Ende.
    /// </summary>
    public void FlushPendingSwitches()
    {
        foreach (var pending in _pendingSwitches)
        {
            _switchProtocol.Add(pending with
            {
                EffectiveInRun = false,
                SwitchReactionTicks = 0,
            });
        }

        _pendingSwitches.Clear();
    }

    /// <summary>Gesamtzaehler der angewendeten Intents (diagnostisch).</summary>
    public long AppliedIntentsTotal { get; private set; }

    /// <summary>Gesamtzaehler abgewiesener Intents (diagnostisch, UF-001-Zeilen).</summary>
    public long RejectedIntentsTotal { get; private set; }

    /// <summary>Gesamtzaehler Leer-Klick-Deselektionen (definierte Semantik).</summary>
    public long EmptyPointDeselectTotal { get; private set; }

    /// <summary>Gesamtzaehler Bewegungsabweisungen wegen leerer Auswahl.</summary>
    public long MoveWithoutSelectionTotal { get; private set; }

    /// <summary>Gesamtzaehler zu spaet eingetroffener, abgewiesener Live-Intents.</summary>
    public long LateRejectedTotal { get; private set; }

    /// <summary>Gesamtzahl an den Kern uebergebener Befehle.</summary>
    public long AppliedCommandsTotal { get; private set; }

    /// <summary>Gesamtzaehler Entscheidungen ohne aktivierte Schicht (T-035, Auswertungsordnung Stufe 1).</summary>
    public long ChooseIntentsRejectedWithoutActivationTotal { get; private set; }

    /// <summary>Gesamtzaehler Entscheidungen vor der Angebotsöffnung (T-035, Stufe 2).</summary>
    public long ChooseIntentsRejectedBeforeOfferTotal { get; private set; }

    /// <summary>Gesamtzaehler Entscheidungen im strategischen Modus (T-035, Stufe 3).</summary>
    public long ChooseIntentsRejectedInStrategicModeTotal { get; private set; }

    /// <summary>Gesamtzaehler Entscheidungen nach gefallener Wahl (T-035, Stufe 4).</summary>
    public long ChooseIntentsRejectedAfterDecisionTotal { get; private set; }

    private IntentDisposition Journal(GrayboxIntent intent, IntentDisposition disposition)
    {
        if (disposition == IntentDisposition.Applied)
        {
            AppliedIntentsTotal++;
        }
        else
        {
            RejectedIntentsTotal++;
        }

        return disposition;
    }
}

/// <summary>Anfrage eines headless Sitzungslaufs.</summary>
public sealed record SessionRunRequest(
    uint Seed,
    GrayboxIntent[] ScriptedIntents,
    int WarmupTicks,
    int HorizonTicks,
    bool RunSelfConsistencyPass = true,
    bool ExplorationEnabled = false,
    bool DecisionEnabled = false,
    bool PressureEnabled = false);

/// <summary>
/// In-Prozess-Träger eines Speichers an einer Vorgrenze (Savevertrag V2,
/// Abschnitt 13.2): Simulationszustand und Sitzungszustand an derselben
/// Vorgrenze samt Zustandsanker. Der App-Layer verpackt diese Erfassung in
/// ein V2-Dokument; der Fortsetzungslauf stellt aus ihr Welt und Sitzungs-
/// schicht wieder her. Kein Feld dieser Erfassung ist Teil des Simulations-
/// zustands oder Hashes außer dem vom Kern selbst gehashten Simulationszustand.
/// </summary>
public sealed record SessionSaveCapture(
    long BoundaryTick,
    ulong BoundaryStateHash,
    Riftward.Save.SimSaveState Simulation,
    Riftward.Save.SessionSectionState Session);

/// <summary>Ergebnis eines Fortsetzungslaufs ab der Ladegrenze.</summary>
public sealed record SessionContinuationResult(
    SessionRunResult Result,
    bool ChainContinuityVerified,
    IReadOnlyList<string> ContinuityReasons,
    ulong ReferenceEndHash,
    int ComparedSampleCount);

/// <summary>
/// Headless Sitzungslauf (Kommandovertrag Abschnitt 7): fester 20-Hz-Tick
/// des unveränderten Kerns, Messfenster nach T-021-Methode (Stoppuhr-Delta
/// je Tick ausschliesslich um world.Tick(); praezises GC-Allokationsdelta je
/// Tick, summiert), Zustands-Hashketten-Stichproben und abschliessender
/// Selbstkonsistenznachweis durch einen zweiten frischen Durchlauf im selben
/// Prozess. Kein Fenster, kein Renderer, kein Netzwerk.
/// </summary>
public static class SessionEngine
{
    public static SessionRunResult Run(SessionRunRequest request) =>
        RunCore(request, request.HorizonTicks, saveCapture: false).Result;

    /// <summary>
    /// Speicherlauf (Savevertrag V2, Abschnitt 13.2): fährt die Kette bis zur
    /// Vorgrenze <paramref name="saveBoundaryTick"/> (Zustand nach Abschluss
    /// des Ticks <c>saveBoundaryTick − 1</c>), misst das Fenster
    /// <c>[warmupTicks, saveBoundaryTick)</c> vertragsgleich und erfasst dort
    /// Simulation plus Sitzungsschicht. Der gespeicherte Prozess endet mit
    /// dieser Erfassung.
    /// </summary>
    public static (SessionRunResult Result, SessionSaveCapture Capture) RunWithSaveBoundary(
        SessionRunRequest request,
        long saveBoundaryTick)
    {
        if (saveBoundaryTick <= request.WarmupTicks || saveBoundaryTick >= request.HorizonTicks)
        {
            throw new ArgumentOutOfRangeException(
                nameof(saveBoundaryTick),
                "Die Speichervorgrenze muss innerhalb des Messfensters liegen ([warmupTicks+1, horizonTicks-1]).");
        }

        var (result, capture) = RunCore(request, saveBoundaryTick, saveCapture: true);
        return (result, capture!);
    }

    /// <summary>
    /// Fortsetzungslauf (Savevertrag V2, Abschnitt 13.2): frischer
    /// Weltzustand aus der Speichererfassung, vollständig restaurierte
    /// Sitzungsschicht und Modus, Fortsetzung der Skriptausführung ab der
    /// Ladegrenze bis zum Horizont. Die Fortsetzungskette wird gegen einen
    /// unterbrochenen Referenzlauf im selben Prozess verglichen (alle
    /// Stichproben nach der Ladegrenze sowie das Kettenende, T-031-
    /// Fortsetzungsketten-Präzedenz); die Session-Schicht der Referenz ist
    /// bewusst leer, weil kein Sitzungszustand je die Kette berührt.
    /// </summary>
    public static SessionContinuationResult RunFromSessionSave(
        SessionRunRequest request,
        SessionSaveCapture capture)
    {
        ArgumentNullException.ThrowIfNull(capture);

        if (capture.BoundaryTick <= request.WarmupTicks || capture.BoundaryTick >= request.HorizonTicks)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capture),
                "Die Ladegrenze muss innerhalb des Messfensters liegen.");
        }

        if (!Riftward.Save.SimulationSaveAdapter.TryRestore(
                capture.Simulation, capture.BoundaryStateHash, out var world, out var restoreFailure)
            || world is null)
        {
            throw new InvalidOperationException($"Wiederherstellung wurde kontrolliert abgewiesen: {restoreFailure}");
        }

        var exploration = capture.Session.ExplorationActive != 0
            ? ExplorationSession.Restore(capture.Session.ExplorationVisits)
            : null;

        if (request.DecisionEnabled && exploration is null)
        {
            throw new ArgumentException(
                "Die Entscheidungsaktivierung ist vertraglich an die Erkundungsaktivierung gekoppelt.",
                nameof(request));
        }

        if (request.PressureEnabled && !request.DecisionEnabled)
        {
            throw new ArgumentException(
                "Die Druckaktivierung ist vertraglich an die Entscheidungsaktivierung gekoppelt.",
                nameof(request));
        }

        var decision = request.DecisionEnabled && capture.Session.DecisionActive != 0
            ? DecisionSession.Restore(capture.Session)
            : null;
        var pressure = request.PressureEnabled && capture.Session.PressureActive != 0
            ? PressureSession.Restore(capture.Session)
            : null;

        // Skriptintents vor der Ladegrenze sind im Speicherlauf verbraucht;
        // die Fortsetzung setzt ab der Ladegrenze fort (kanonische Reihen-
        // folge bleibt erhalten, da das Array vorsortiert ist).
        var continuationIntents = request.ScriptedIntents
            .Where(intent => intent.Tick >= capture.BoundaryTick)
            .ToArray();
        var selection = new SelectionModel(ReadAgentGroups(world));
        var pendingSwitches = capture.Session.PendingSwitches
            .Select(pending => new ModeSwitchEvent(
                IntentTick: pending.IntentTick,
                EvaluatedBoundaryTick: pending.IntentTick,
                EffectiveBoundaryTick: pending.EffectiveBoundaryTick,
                PreviousMode: pending.PreviousMode == Riftward.Save.SessionSectionCodec.ModePersonal
                    ? SessionMode.Personal
                    : SessionMode.Strategic,
                NewMode: pending.NewMode == Riftward.Save.SessionSectionCodec.ModePersonal
                    ? SessionMode.Personal
                    : SessionMode.Strategic,
                EffectiveInRun: false,
                SwitchReactionTicks: 0,
                HeroPositionXMm: 0,
                HeroPositionYMm: 0,
                HeroZoneIndex: -1,
                HeroPathState: 0))
            .ToArray();
        var restoredMode = capture.Session.ActiveMode == 1 ? SessionMode.Personal : SessionMode.Strategic;
        var pipeline = new SessionPipeline(
            world, selection, continuationIntents, restoredMode, pendingSwitches, exploration, decision, pressure);

        var result = RunMeasuredWindow(
            request, world, pipeline, capture.BoundaryTick, request.HorizonTicks,
            restored: true);

        // Unterbrochener Referenzlauf im selben Prozess (K2-Anker-Präzedenz):
        // identische Stichprobenregel, voller Horizont, ohne Sitzungsschicht.
        var reference = RunUninterruptedReference(request);
        var reasons = CompareContinuation(reference, capture, result);
        var continuity = reasons.Count == 0;

        return new SessionContinuationResult(
            Result: result,
            ChainContinuityVerified: continuity,
            ContinuityReasons: reasons,
            ReferenceEndHash: reference.EndHash,
            ComparedSampleCount: reference.Samples.Count(sample => sample.Tick > capture.BoundaryTick));
    }

    /// <summary>
    /// Vergleich der Fortsetzungskette mit der unterbrochenen Referenz
    /// (T-031-Präzedenz): sämtliche Stichproben nach der Ladegrenze, der
    /// aligned Anker an der Ladegrenze (falls die Referenz ihn trägt) und das
    /// Kettenende sind byteidentisch; Abweichungen werden als Gründe benannt
    /// statt still geglättet.
    /// </summary>
    private static List<string> CompareContinuation(
        ReferenceRun reference,
        SessionSaveCapture capture,
        SessionRunResult continuation)
    {
        var reasons = new List<string>();

        var expected = reference.Samples.Where(sample => sample.Tick > capture.BoundaryTick).ToList();
        var actual = continuation.IntervalSampleTicks
            .Zip(continuation.IntervalHashes, (tick, hash) => (Tick: tick, Hash: hash))
            .Where(sample => sample.Tick > capture.BoundaryTick)
            .ToList();

        if (actual.Count != expected.Count)
        {
            reasons.Add("continuation-sample-count-mismatch");
        }

        for (var index = 0; index < Math.Min(actual.Count, expected.Count); index++)
        {
            if (actual[index].Tick != expected[index].Tick
                || actual[index].Hash != expected[index].Hash)
            {
                reasons.Add($"continuation-chain-mismatch-at-{actual[index].Tick}");
                break;
            }
        }

        var alignedAnchor = reference.Samples.FirstOrDefault(sample => sample.Tick == capture.BoundaryTick);

        if (alignedAnchor.Tick == capture.BoundaryTick && alignedAnchor.Hash != capture.BoundaryStateHash)
        {
            reasons.Add("boundary-anchor-mismatch");
        }

        if (continuation.EndStateHash != reference.EndHash)
        {
            reasons.Add("continuation-end-hash-mismatch");
        }

        return reasons;
    }

    private sealed record ReferenceRun(
        IReadOnlyList<(long Tick, ulong Hash)> Samples,
        ulong EndHash);

    /// <summary>Unterbrochener Referenzlauf über den vollen Horizont ohne Sitzungsschicht.</summary>
    private static ReferenceRun RunUninterruptedReference(SessionRunRequest request)
    {
        var world = new Riftward.Simulation.SimWorld(request.Seed);
        var selection = new SelectionModel(ReadAgentGroups(world));
        var pipeline = new SessionPipeline(world, selection, request.ScriptedIntents);

        for (var tick = 0L; tick < request.WarmupTicks; tick++)
        {
            pipeline.ProcessBoundary(tick);
            world.Tick();
        }

        var samples = new List<(long Tick, ulong Hash)> { (world.TickIndex, world.ComputeStateHash()) };

        for (var tick = request.WarmupTicks; tick < request.HorizonTicks; tick++)
        {
            pipeline.ProcessBoundary(tick);
            world.Tick();

            if (world.TickIndex % SessionContract.HashSampleIntervalTicks == 0)
            {
                samples.Add((world.TickIndex, world.ComputeStateHash()));
            }
        }

        return new ReferenceRun(samples, world.ComputeStateHash());
    }

    /// <summary>
    /// Gemeinsamer Laufkern: frischer Lauf über [Warmup, windowEndExclusive)
    /// mit optionalem Speicher an der Vorgrenze; der Bestandslauf ist der
    /// Sonderfall windowEndExclusive == HorizonTicks ohne Speicher.
    /// </summary>
    private static (SessionRunResult Result, SessionSaveCapture? Capture) RunCore(
        SessionRunRequest request,
        long windowEndExclusive,
        bool saveCapture)
    {
        if (request.WarmupTicks < 30 || request.WarmupTicks >= request.HorizonTicks)
        {
            throw new ArgumentOutOfRangeException(nameof(request), "Messfenster muss hinter dem Warm-up liegen.");
        }

        var world = new Riftward.Simulation.SimWorld(request.Seed);
        var selectionGroups = ReadAgentGroups(world);
        var selection = new SelectionModel(selectionGroups);
        // Erkundung ist rein sitzungsseitig (T-034): ohne Aktivierung null,
        // mit Aktivierung eine Beobachtung, die niemals einen Kernbefehl
        // erzeugt und niemals Simulationszustand oder Hash berührt.
        var exploration = request.ExplorationEnabled ? new ExplorationSession() : null;

        if (request.DecisionEnabled && exploration is null)
        {
            // Vertragskopplung (Entscheidungsvertrag Abschnitt 7): die
            // Entscheidungsaktivierung ist an --exploration gekoppelt; ein
            // Entscheidungszustand ohne seinen vertraglichen Ausloesertraeger
            // ist ein Vertragswiderspruch und wird fail-closed abgewiesen.
            throw new ArgumentException(
                "Die Entscheidungsaktivierung ist vertraglich an die Erkundungsaktivierung gekoppelt.",
                nameof(request));
        }

        if (request.PressureEnabled && !request.DecisionEnabled)
        {
            // Vertragskopplung (Druckvertrag Abschnitt 7): die
            // Druckaktivierung ist an --decision gekoppelt; ein
            // Druckzustand ohne seinen vertraglichen Ausloesertraeger
            // ist ein Vertragswiderspruch und wird fail-closed abgewiesen.
            throw new ArgumentException(
                "Die Druckaktivierung ist vertraglich an die Entscheidungsaktivierung gekoppelt.",
                nameof(request));
        }

        // Entscheidung ist rein sitzungsseitig (T-035): ohne Aktivierung
        // null; die Schicht erzeugt niemals einen Kernbefehl und ist nie
        // Simulationszustand oder Hash.
        var decision = request.DecisionEnabled ? new DecisionSession() : null;

        // Druck ist rein sitzungsseitig (T-036): ohne Aktivierung null; die
        // Schicht erzeugt niemals einen Kernbefehl und ist nie
        // Simulationszustand oder Hash.
        var pressure = request.PressureEnabled && decision is not null ? new PressureSession() : null;
        var pipeline = new SessionPipeline(world, selection, request.ScriptedIntents, exploration, decision, pressure);

        // Warmphase ohne Messung; es gibt keine Intents vor dem Fenster.
        for (var tick = 0L; tick < request.WarmupTicks; tick++)
        {
            pipeline.ProcessBoundary(tick);
            world.Tick();
        }

        var result = RunMeasuredWindow(
            request, world, pipeline, request.WarmupTicks, windowEndExclusive, restored: false);

        SessionSaveCapture? capture = null;

        if (saveCapture)
        {
            capture = new SessionSaveCapture(
                BoundaryTick: world.TickIndex,
                BoundaryStateHash: world.ComputeStateHash(),
                Simulation: Riftward.Save.SimulationSaveAdapter.Capture(world),
                Session: SessionStateCapture.Capture(pipeline, exploration, decision, pressure));
        }

        return (result, capture);
    }

    /// <summary>
    /// Gemessenes Fenster [firstBoundaryTick, windowEndExclusive) über einer
    /// bereitgestellten Pipeline (frisch oder restauriert): Messfenster nach
    /// T-021-Methode, Hashkettenstichproben, Reaktionsverteilung und der
    /// Ehrlichkeitsnachweis — beim frischen Lauf als Selbstkonsistenzpass,
    /// beim Fortsetzungslauf als Kettenfortsetzungsvergleich durch den
    /// Aufrufer.
    /// </summary>
    private static SessionRunResult RunMeasuredWindow(
        SessionRunRequest request,
        Riftward.Simulation.SimWorld world,
        SessionPipeline pipeline,
        long firstBoundaryTick,
        long windowEndExclusive,
        bool restored)
    {
        var windowTicks = checked((int)(windowEndExclusive - firstBoundaryTick));
        var tickTimes = new double[windowTicks];

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var pauseSumBefore = GC.GetTotalPauseDuration();
        var collectionCountBefore = GcCollectionTotal();
        var startStateHash = world.ComputeStateHash();

        var hashSampleCapacity = (windowTicks / SessionContract.HashSampleIntervalTicks) + 2;
        var intervalSampleTicks = new long[hashSampleCapacity];
        var intervalHashes = new ulong[hashSampleCapacity];
        var hashCursor = 0;
        intervalSampleTicks[hashCursor] = world.TickIndex;
        intervalHashes[hashCursor] = startStateHash;
        hashCursor++;

        long allocationSumBytes = 0;
        long maxReactionTicks = 0;
        var appliedWithReaction = new List<long>(request.ScriptedIntents.Length);

        for (var tick = firstBoundaryTick; tick < windowEndExclusive; tick++)
        {
            var outcome = pipeline.ProcessBoundary(tick);
            var consumedCommands = outcome.AppliedCount > 0;

            var allocationBefore = GC.GetTotalAllocatedBytes(precise: true);
            var startTimestamp = Stopwatch.GetTimestamp();
            world.Tick();
            var endTimestamp = Stopwatch.GetTimestamp();
            var allocationAfter = GC.GetTotalAllocatedBytes(precise: true);

            tickTimes[tick - firstBoundaryTick] =
                SessionMath.TimestampDeltaToMilliseconds(startTimestamp, endTimestamp);
            allocationSumBytes += allocationAfter - allocationBefore;

            if (consumedCommands)
            {
                // Effektsnapshot: erster kanonischer Zustands-Hash nach dem
                // verbrauchenden Tick (Kommandovertrag Abschnitt 6). Nach
                // Abschluss des Ticks V ist TickIndex == V + 1; die
                // Reaktionsdauer ist damit (V + 1) - S.
                var reactionTicks = world.TickIndex - tick;
                maxReactionTicks = Math.Max(maxReactionTicks, reactionTicks);

                for (var slot = 0; slot < outcome.AppliedCount; slot++)
                {
                    appliedWithReaction.Add(reactionTicks);
                }
            }

            if (world.TickIndex % SessionContract.HashSampleIntervalTicks == 0
                && hashCursor < hashSampleCapacity)
            {
                intervalSampleTicks[hashCursor] = world.TickIndex;
                intervalHashes[hashCursor] = world.ComputeStateHash();
                hashCursor++;
            }
        }

        var endStateHash = world.ComputeStateHash();
        pipeline.FlushPendingSwitches();
        var gcPauseSumMs = (GC.GetTotalPauseDuration() - pauseSumBefore).TotalMilliseconds;
        var gcPauseCount = GcCollectionTotal() - collectionCountBefore;

        bool? selfConsistent = null;
        var inconsistencyReasons = Array.Empty<string>();

        if (request.RunSelfConsistencyPass && !restored)
        {
            selfConsistent = RunSelfConsistencyPass(
                request,
                intervalSampleTicks.AsSpan(0, hashCursor).ToArray(),
                intervalHashes.AsSpan(0, hashCursor).ToArray(),
                endStateHash,
                out inconsistencyReasons);
        }

        var metrics = new SessionMetrics(
            P50TickTimeMs: SessionMath.Percentile(tickTimes, 0.50),
            P95TickTimeMs: SessionMath.Percentile(tickTimes, 0.95),
            P99TickTimeMs: SessionMath.Percentile(tickTimes, 0.99),
            AllocationsPerWarmTickBytes: allocationSumBytes / (double)windowTicks,
            GcPauseSumMs: Math.Round(gcPauseSumMs, 3),
            GcPauseCount: gcPauseCount,
            MaxReactionTicks: maxReactionTicks,
            ReactionP50Ticks: SessionMath.Percentile(appliedWithReaction, 0.50),
            ReactionP95Ticks: SessionMath.Percentile(appliedWithReaction, 0.95),
            ReactionP99Ticks: SessionMath.Percentile(appliedWithReaction, 0.99),
            ReactionSampleCount: appliedWithReaction.Count);

        return new SessionRunResult(
            StartStateHash: startStateHash,
            EndStateHash: endStateHash,
            IntervalSampleTicks: intervalSampleTicks.AsSpan(0, hashCursor).ToArray(),
            IntervalHashes: intervalHashes.AsSpan(0, hashCursor).ToArray(),
            StateChainSelfConsistent: selfConsistent,
            SelfInconsistencyReasons: inconsistencyReasons,
            Metrics: metrics,
            AppliedIntents: (int)pipeline.AppliedIntentsTotal,
            RejectedIntents: (int)pipeline.RejectedIntentsTotal,
            EmptyPointDeselects: (int)pipeline.EmptyPointDeselectTotal,
            MoveWithoutSelectionRejects: (int)pipeline.MoveWithoutSelectionTotal,
            KernelCommandsTotal: (int)pipeline.AppliedCommandsTotal,
            TotalTicksExecuted: checked((int)windowEndExclusive),
            Telemetry: BuildModeTelemetry(pipeline),
            Exploration: explorationTelemetryOrNull(pipeline),
            Decision: decisionTelemetryOrNull(pipeline),
            Pressure: pressureTelemetryOrNull(pipeline));
    }

    private static ExplorationTelemetry? explorationTelemetryOrNull(SessionPipeline pipeline) =>
        pipeline.Exploration?.ToTelemetry();

    private static DecisionTelemetry? decisionTelemetryOrNull(SessionPipeline pipeline) =>
        pipeline.Decision?.ToTelemetry();

    private static PressureTelemetry? pressureTelemetryOrNull(SessionPipeline pipeline) =>
        pipeline.Pressure is { } pressure && pipeline.Decision is { } decision
            ? pressure.ToTelemetry(decision)
            : null;

    /// <summary>
    /// Aggregiert die Modus-Telemetrie der Pipeline (T-033): Wechselprotokoll,
    /// Kontextabweisungszähler, Lenk-Dedupe und die Wechselreaktions-
    /// verteilung ausschließlich über die innerhalb des Laufs wirksamen
    /// Wechsel (Modevertrag Abschnitte 4 und 7). Öffentlich, damit der
    /// interaktive Lauf desselben Befehls dieselbe Aggregation über denselben
    /// Pipelinepfad erzeugt wie der headless Lauf.
    /// </summary>
    public static ModeTelemetry BuildModeTelemetry(SessionPipeline pipeline)
    {
        var effective = new List<long>();

        foreach (var entry in pipeline.SwitchProtocol)
        {
            if (entry.EffectiveInRun)
            {
                effective.Add(entry.SwitchReactionTicks);
            }
        }

        var maxSwitchReaction = effective.Count == 0 ? 0 : effective.Max();
        return new ModeTelemetry(
            InitialMode: pipeline.InitialMode,
            FinalMode: pipeline.CurrentEffectiveMode,
            SwitchProtocol: pipeline.SwitchProtocol.ToArray(),
            StrategyIntentsRejectedInPersonalMode: pipeline.StrategyIntentsRejectedInPersonalModeTotal,
            SteerIntentsRejectedInStrategyMode: pipeline.SteerIntentsRejectedInStrategyModeTotal,
            SteerIdleDedupes: pipeline.SteerIdleDedupeTotal,
            MaxSwitchReactionTicks: maxSwitchReaction,
            SwitchReactionP50Ticks: SessionMath.Percentile(effective, 0.50),
            SwitchReactionP95Ticks: SessionMath.Percentile(effective, 0.95),
            SwitchReactionP99Ticks: SessionMath.Percentile(effective, 0.99),
            SwitchReactionSampleCount: effective.Count);
    }

    /// <summary>Liest die zeitinvarianten Agentengruppen schreibgeschützt aus dem Kernsnapshot.</summary>
    public static byte[] ReadAgentGroups(Riftward.Simulation.SimWorld world) =>
        world.CreateSnapshot().Group;

    private static long GcCollectionTotal() =>
        GC.CollectionCount(0) + GC.CollectionCount(1) + GC.CollectionCount(2);

    /// <summary>
    /// Zweiter frischer Durchlauf im selben Prozess (K2-Anker des Kommando-
    /// vertrags): identische Kette an denselben Stichprobenticks und
    /// identischer Endhash sind Pflicht; Abweichungen werden als Gründe
    /// zurueckgegeben statt still geglättet.
    /// </summary>
    private static bool RunSelfConsistencyPass(
        SessionRunRequest request,
        long[] expectedSampleTicks,
        ulong[] expectedSampleHashes,
        ulong expectedEndHash,
        out string[] reasons)
    {
        var found = new List<string>(2);
        var world = new Riftward.Simulation.SimWorld(request.Seed);
        var selection = new SelectionModel(ReadAgentGroups(world));
        var pipeline = new SessionPipeline(world, selection, request.ScriptedIntents);

        for (var tick = 0L; tick < request.WarmupTicks; tick++)
        {
            pipeline.ProcessBoundary(tick);
            world.Tick();
        }

        var replayStartHash = world.ComputeStateHash();

        if (replayStartHash != expectedSampleHashes[0])
        {
            found.Add("start-hash-mismatch");
        }

        var cursor = 1;

        for (var tick = request.WarmupTicks; tick < request.HorizonTicks; tick++)
        {
            pipeline.ProcessBoundary(tick);
            world.Tick();

            if (cursor < expectedSampleTicks.Length
                && world.TickIndex == expectedSampleTicks[cursor])
            {
                if (world.ComputeStateHash() != expectedSampleHashes[cursor])
                {
                    found.Add($"chain-mismatch-at-{tick}");
                }

                cursor++;
            }
        }

        if (world.ComputeStateHash() != expectedEndHash)
        {
            found.Add("end-hash-mismatch");
        }

        reasons = found.ToArray();
        return found.Count == 0;
    }
}
