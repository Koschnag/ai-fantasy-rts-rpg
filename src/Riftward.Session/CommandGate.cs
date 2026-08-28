namespace Riftward.Session;

/// <summary>
/// Dokumentierte Grenzwerte des Kommandoschleifen-Gates (Kommandovertrag
/// Abschnitt 7, T-033 erweitert um Kriterium 6 der Gatematrix im Modevertrag
/// Abschnitt 7). Fail-closed: Es entscheidet ausschließlich gegen diese
/// absoluten Grenzwerte; alle uebrigen Messfelder sind diagnostisch
/// (gateCoupled=false). Die Werte stammen unveraendert aus dem
/// Simulationsvertrag V1, docs/PERFORMANCE_BUDGET.md, der Reaktions-
/// ableitung des Kommandovertrags Abschnitt 6 und der Wechselreaktions-
/// ableitung des Modevertrags Abschnitt 4.
/// </summary>
public sealed record CommandGateLimits(
    double P99TickTimeHardLimitMs = Riftward.Simulation.SimulationContract.P99TickTimeHardLimitMs,
    double P99TickTimeTargetMs = Riftward.Simulation.SimulationContract.P99TickTimeTargetMs,
    long AllocationsPerWarmTickLimitBytes = Riftward.Simulation.SimulationContract.AllocationLimitBytesPerWarmTick,
    int ReactionHardLimitTicks = SessionContract.ReactionHardLimitTicks,
    int ReactionTargetTicks = SessionContract.ReactionTargetTicks,
    bool RuntimeShaderCompilationAllowed = false,
    int SwitchReactionHardLimitTicks = ModeContract.SwitchReactionHardLimitTicks,
    int SwitchReactionTargetTicks = ModeContract.SwitchReactionTargetTicks)
{
    public static CommandGateLimits Documented { get; } = new();
}

/// <summary>Messwerteingaben einer Gateentscheidung.</summary>
public readonly record struct CommandGateInputs(
    double P99TickTimeMs,
    double ManagedAllocationsPerWarmTickBytes,
    long MaxReactionTicks,
    long ReactionSampleCount,
    bool RuntimeShaderCompilationObserved,
    /// <summary>Null im Interaktivmodus: Kriterium nicht auswertbar (live Eingaben), wird nicht behauptet.</summary>
    bool? StateChainSelfConsistent,
    long MaxSwitchReactionTicks = 0,
    int SwitchReactionSampleCount = 0);

/// <summary>Ergebnis einer Gateentscheidung mit stabilen Verletzungskennungen.</summary>
public sealed record CommandGateVerdict(
    bool Pass,
    bool TickTimeTargetMet,
    bool ReactionTargetMet,
    IReadOnlyList<string> Violations,
    /// <summary>Kriterium 6 war mit mindestens einem wirksamen Wechsel messbar.</summary>
    bool SwitchReactionEvaluated = false,
    /// <summary>Zielerfuellung von Kriterium 6; ohne Messung ohne Aussage (false).</summary>
    bool SwitchReactionTargetMet = false)
{
    public static readonly CommandGateVerdict Empty = new(true, true, true, [], false, false);
}

/// <summary>
/// Fail-closed-Bewertung des Kommandoschleifen-Gates. Jede Verletzung ergibt
/// eine stabile maschinenlesbare Kennung; das Ziel wird ausgewiesen, seine
/// Verfehlung allein faltet das Gate nicht (AC-T010-07/T-020/T-021-Praezedenz).
/// </summary>
public static class CommandGate
{
    public const string ViolationTickTime =
        "tick-time-p99-above-hard-limit";

    public const string ViolationAllocations =
        "allocations-per-warm-tick-above-limit";

    public const string ViolationReaction =
        "reaction-ticks-above-hard-limit";

    public const string ViolationShaderCompilation =
        "runtime-shader-compilation-observed";

    public const string ViolationChainInconsistent =
        "state-chain-self-inconsistent";

    public const string ViolationSwitchReaction =
        "switch-reaction-ticks-above-hard-limit";

    public static CommandGateVerdict Evaluate(CommandGateLimits limits, CommandGateInputs inputs)
    {
        var violations = new List<string>(6);
        var tickTimeTargetMet = inputs.P99TickTimeMs <= limits.P99TickTimeTargetMs;
        var reactionTargetMet = true;

        if (inputs.P99TickTimeMs > limits.P99TickTimeHardLimitMs)
        {
            violations.Add(ViolationTickTime);
        }

        if (inputs.ManagedAllocationsPerWarmTickBytes > limits.AllocationsPerWarmTickLimitBytes)
        {
            violations.Add(ViolationAllocations);
        }

        if (inputs.ReactionSampleCount > 0 && inputs.MaxReactionTicks > limits.ReactionHardLimitTicks)
        {
            violations.Add(ViolationReaction);
            reactionTargetMet = false;
        }
        else
        {
            reactionTargetMet = inputs.ReactionSampleCount <= 0 || inputs.MaxReactionTicks <= limits.ReactionTargetTicks;
        }

        if (inputs.RuntimeShaderCompilationObserved)
        {
            violations.Add(ViolationShaderCompilation);
        }

        if (inputs.StateChainSelfConsistent.HasValue && !inputs.StateChainSelfConsistent.Value)
        {
            violations.Add(ViolationChainInconsistent);
        }

        // Kriterium 6 (Modevertrag Abschnitt 7): Die Wechselreaktion ist
        // ausschließlich über die innerhalb des Laufs wirksamen Wechsel
        // messbar. Ohne wirksamen Wechsel ist das Kriterium ausdrücklich
        // NICHT auswertbar (SwitchReactionEvaluated = false) und wird nie
        // als gemessener Pass ausgegeben; erst eine Messung über der harten
        // Grenze erzeugt die fail-closed Verletzung.
        var switchReactionEvaluated = inputs.SwitchReactionSampleCount > 0;
        var switchReactionTargetMet = false;

        if (switchReactionEvaluated)
        {
            switchReactionTargetMet = inputs.MaxSwitchReactionTicks <= limits.SwitchReactionTargetTicks;

            if (inputs.MaxSwitchReactionTicks > limits.SwitchReactionHardLimitTicks)
            {
                violations.Add(ViolationSwitchReaction);
            }
        }

        return new CommandGateVerdict(
            Pass: violations.Count == 0,
            TickTimeTargetMet: tickTimeTargetMet,
            ReactionTargetMet: reactionTargetMet,
            Violations: violations,
            SwitchReactionEvaluated: switchReactionEvaluated,
            SwitchReactionTargetMet: switchReactionTargetMet);
    }
}
