namespace Riftward.Session;

/// <summary>
/// Sitzungsmodus des Hybrid-Prototyps (T-033, Modevertrag Abschnitt 1):
/// rein darstellseitiger Sitzungszustand, niemals Teil des Simulations-
/// zustands oder Hashes. Die numerische Reihenfolge ist vertraglich stabil.
/// </summary>
public enum SessionMode : byte
{
    /// <summary>Strategische RTS-Sicht (T-032-Baseline).</summary>
    Strategic = 0,

    /// <summary>Persoenlicher Third-Person-Heldenmodus.</summary>
    Personal = 1,
}

/// <summary>
/// Maschinenlesbare Spiegelung eines Moduswechsels je Wechselgrenze
/// (Modevertrag Abschnitt 4/7): gebundener Intent-Tick S, Auswertung an der
/// Vorgrenze von S, Wirksamkeit an der uebernaechsten Gültigkeitsprüfung
/// M = S + 2, und Heldenstatus von Agentenindex 0 an der Wirksamkeitsgrenze.
/// Ein Wechsel nahe dem Horizont kann innerhalb des Laufs unwirksam bleiben
/// (EffectiveInRun = false); der Endmodus des Reports bildet dann die
/// Wahrheit des Laufs ab.
/// </summary>
public sealed record ModeSwitchEvent(
    long IntentTick,
    long EvaluatedBoundaryTick,
    long EffectiveBoundaryTick,
    SessionMode PreviousMode,
    SessionMode NewMode,
    bool EffectiveInRun,
    long SwitchReactionTicks,
    long HeroPositionXMm,
    long HeroPositionYMm,
    int HeroZoneIndex,
    byte HeroPathState);

/// <summary>
/// Aggregierte Modus-Telemetrie eines Sitzungslaufs (alle Felder diagnostisch
/// mit Ausnahme der Wechselreaktionsgrenze, die die Gatematrix als Kriterium 6
/// fail-closed bindet).
/// </summary>
public sealed record ModeTelemetry(
    SessionMode InitialMode,
    SessionMode FinalMode,
    IReadOnlyList<ModeSwitchEvent> SwitchProtocol,
    long StrategyIntentsRejectedInPersonalMode,
    long SteerIntentsRejectedInStrategyMode,
    long SteerIdleDedupes,
    long MaxSwitchReactionTicks,
    long SwitchReactionP50Ticks,
    long SwitchReactionP95Ticks,
    long SwitchReactionP99Ticks,
    int SwitchReactionSampleCount)
{
    public static ModeTelemetry Empty { get; } = new(
        SessionMode.Strategic,
        SessionMode.Strategic,
        Array.Empty<ModeSwitchEvent>(),
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0);
}

/// <summary>
/// Schreibgeschützte Heldenansicht des unveränderten Kerns: Position,
/// Zone und Pfadstatus von Agentenindex 0 (Modevertrag Abschnitt 2/7).
/// Alle Konversionen sind deterministisch; keine Fließkommaanteile im
/// Ausweis.
/// </summary>
public static class HeroTracker
{
    /// <summary>Kaufmaennische Q16.16-zu-Millimeter-Konversion (half-up, positive Werte).</summary>
    public static long MillimetersFromQ16(long positionQ16)
    {
        var scaled = positionQ16 * 1000L;
        var halfUnit = Riftward.Simulation.FixedPoint.One / 2;
        return (scaled + halfUnit) / Riftward.Simulation.FixedPoint.One;
    }

    /// <summary>X-Position des Vertragshelden in Millimetern (Vorgrenzenansicht).</summary>
    public static long PositionXMm(Riftward.Simulation.SimWorld world) =>
        MillimetersFromQ16(world.PositionXOf(ModeContract.HeroAgentIndex));

    /// <summary>Y-Position (Suedachse) des Vertragshelden in Millimetern.</summary>
    public static long PositionYMm(Riftward.Simulation.SimWorld world) =>
        MillimetersFromQ16(world.PositionYOf(ModeContract.HeroAgentIndex));

    /// <summary>Zonenindex der Heldenposition; -1 außerhalb aller Zonen.</summary>
    public static int ZoneIndexOf(Riftward.Simulation.SimWorld world)
    {
        var tileX = Riftward.Simulation.NavWorld.TileIndexOfPosition(world.PositionXOf(ModeContract.HeroAgentIndex));
        var tileY = Riftward.Simulation.NavWorld.TileIndexOfPosition(world.PositionYOf(ModeContract.HeroAgentIndex));

        for (var zone = 0; zone < Riftward.Simulation.NavWorld.ZoneCount; zone++)
        {
            if (Riftward.Simulation.NavWorld.IsInsideZone(zone, tileX, tileY))
            {
                return zone;
            }
        }

        return -1;
    }

    /// <summary>Pfadstatus von Agentenindex 0 als Byteausweis.</summary>
    public static byte PathStateOf(Riftward.Simulation.SimWorld world) =>
        (byte)world.PathStateOf(ModeContract.HeroAgentIndex);
}

/// <summary>
/// Deterministische Auflösung der interaktiven Lenkrichtung gegen die sechs
/// Zonenzentren (Modevertrag Abschnitt 3): Ziel ist die Zone mit dem groessten
/// normierten Richtungstreue-Skalarprodukt; ohne Richtungstreue-Kandidat
/// (-1) wird der Impuls mit steer-direction-without-zone abgewiesen. Die
/// Entscheidung faellt ausschließlich in exakter Ganzzahlarithmetik (Q16-Kern,
/// Int128-Kreuzmultiplikation) — ohne Fließkommaanteil, ohne Uhr- oder
/// Umgebungsbeitrag; bei Gleichstand gewinnt ausdrücklich die niedrigste
/// Zonennummer. Die Richtungskomponenten sind kleine ganzzahlige
/// Einheitskomponenten (−1/0/+1 je Achse).
/// </summary>
public static class HeroDirectionSteering
{
    /// <summary>
    /// Löst eine Himmelsrichtung (dx nach Osten, dy nach Sueden, kleine
    /// ganzzahlige Einheitskomponenten) gegen die Zonenzentren auf.
    /// Rueckgabe -1 ohne richtungstreuen Kandidaten.
    /// </summary>
    public static int ResolveZone(Riftward.Simulation.SimWorld world, long dx, long dy) =>
        ResolveZoneFrom(world.PositionXOf(ModeContract.HeroAgentIndex), world.PositionYOf(ModeContract.HeroAgentIndex), dx, dy);

    /// <summary>
    /// Exakte Ganzzahl-Auflösung über einer Heldenposition (Q16.16): Kandidat
    /// ist ausschließlich jede Zone mit positivem Richtungstreue-Skalarprodukt
    /// (Zentrum − Held) · Richtung (Modevertrag Abschnitt 3: „jedes
    /// Skalarprodukt ≤ 0" ist kein Kandidat — dies schließt den Fall ein, dass
    /// der Held exakt auf einem Zonenzentrum steht: der Vektor ist dort 0, das
    /// normierte Skalarprodukt undefined, die Zone also kein Kandidat und die
    /// Auflösung fällt auf die nächste richtungstreue Zone oder -1). Der
    /// Vergleich zweier Kandidaten i, j ist exakt als Kreuzmultiplikation
    /// dot_i² · d2_j gegen dot_j² · d2_i gebunden (Int128, ohne Überlauf), und
    /// bei Gleichstand gewinnt die niedrigste Zonennummer.
    /// </summary>
    public static int ResolveZoneFrom(long heroPositionXQ16, long heroPositionYQ16, long dx, long dy)
    {
        if (dx == 0 && dy == 0)
        {
            return -1;
        }

        var bestZone = -1;
        var bestDot = 0L;
        var bestDistanceSquared = 0L;

        for (var zone = 0; zone < Riftward.Simulation.NavWorld.ZoneCount; zone++)
        {
            var toCenterX = Riftward.Simulation.NavWorld.ZoneCenterXQ16(zone) - heroPositionXQ16;
            var toCenterY = Riftward.Simulation.NavWorld.ZoneCenterYQ16(zone) - heroPositionYQ16;
            var dot = (toCenterX * dx) + (toCenterY * dy);

            if (dot <= 0)
            {
                continue;
            }

            var distanceSquared = (toCenterX * toCenterX) + (toCenterY * toCenterY);

            if (bestZone < 0 || StrictlyBeats(dot, distanceSquared, bestDot, bestDistanceSquared))
            {
                bestZone = zone;
                bestDot = dot;
                bestDistanceSquared = distanceSquared;
            }
        }

        return bestZone;
    }

    /// <summary>
    /// Exakter Mengenvergleich der normierten Richtungstreue: Kandidat schlägt
    /// den Bestwert genau dann, wenn dot_c² · d2_b &gt; dot_b² · d2_c; Gleichstand
    /// (==) waehlt bewusst nicht, sodass die niedrigste Zonennummer gewinnt.
    /// </summary>
    internal static bool StrictlyBeats(long dotCandidate, long distanceSquaredCandidate, long dotBest, long distanceSquaredBest) =>
        ((Int128)dotCandidate * dotCandidate * distanceSquaredBest)
        > ((Int128)dotBest * dotBest * distanceSquaredCandidate);
}