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
/// (-1) wird der Impuls mit steer-direction-without-zone abgewiesen. Doppelte
/// Genauigkeit bleibt darstellseitige Diagnostik; das Ergebnis ist allein
/// durch Heldenposition und Richtung bestimmt.
/// </summary>
public static class HeroDirectionSteering
{
    /// <summary>
    /// Löst eine Himmelsrichtung (dirX nach Osten, dirY nach Sueden,
    /// nicht normiert) gegen die Zonenzentren auf. Rueckgabe -1 ohne
    /// richtungstreuen Kandidaten.
    /// </summary>
    public static int ResolveZone(Riftward.Simulation.SimWorld world, double dirX, double dirY)
    {
        var length = Math.Sqrt((dirX * dirX) + (dirY * dirY));

        if (length < 1e-12)
        {
            return -1;
        }

        var heroX = world.PositionXOf(ModeContract.HeroAgentIndex) / (double)Riftward.Simulation.FixedPoint.One;
        var heroY = world.PositionYOf(ModeContract.HeroAgentIndex) / (double)Riftward.Simulation.FixedPoint.One;
        var unitX = dirX / length;
        var unitY = dirY / length;

        var bestZone = -1;
        var bestAlignment = 0.0;

        for (var zone = 0; zone < Riftward.Simulation.NavWorld.ZoneCount; zone++)
        {
            var centerX = Riftward.Simulation.NavWorld.ZoneCenterXQ16(zone) / (double)Riftward.Simulation.FixedPoint.One;
            var centerY = Riftward.Simulation.NavWorld.ZoneCenterYQ16(zone) / (double)Riftward.Simulation.FixedPoint.One;
            var toCenterX = centerX - heroX;
            var toCenterY = centerY - heroY;
            var distance = Math.Sqrt((toCenterX * toCenterX) + (toCenterY * toCenterY));

            if (distance < 1e-12)
            {
                // Held steht exakt im Zentrum: jede Richtung ist streugetreu.
                return zone;
            }

            var alignment = ((toCenterX * unitX) + (toCenterY * unitY)) / distance;

            if (alignment > 0.0 && alignment > bestAlignment)
            {
                bestAlignment = alignment;
                bestZone = zone;
            }
        }

        return bestZone;
    }
}