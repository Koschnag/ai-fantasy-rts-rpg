using Riftward.Simulation;

namespace Riftward.App.Soak;

/// <summary>
/// Reine Planmathematik des Soaks (T-022): Tickhorizonte, Fenster- und
/// Stichprobenschemata entstehen ausschliesslich aus Konstanten. Keine Uhr,
/// kein Umgebungszugriff, keine Zufallsbeitraege; der Simulationszustand
/// selbst haengt an keiner Stelle von diesen Werten ab.
/// </summary>
public static class SoakPlan
{
    /// <summary>
    /// Wanduhrdauer des Realzeit-Taktmodus (8 Stunden); unter Soakvertrag V2
    /// rein diagnostische Kennung, NF-002-Evidenz entsteht aus dem
    /// Wiederholungsbuendel des kompletten Planhorizonts.
    /// </summary>
    public const double RequiredWallSeconds = 8.0 * 60.0 * 60.0;

    /// <summary>
    /// Vollständiger Planhorizont: 8 h bei festem 20-Hz-Tick
    /// (= <see cref="RequiredWallSeconds"/> mal
    /// <see cref="SimulationContract.TickRateHz"/>); Messsticks ohne Warm-up.
    /// </summary>
    public const long AuthoritativeTickCount =
        (long)RequiredWallSeconds * SimulationContract.TickRateHz;

    /// <summary>Endtick der Simulation inklusive Warm-up.</summary>
    public static long TotalSimulationTick => WarmupTicks + AuthoritativeTickCount;

    /// <summary>
    /// Kanonischer Kettenstichprobenplan: alle Vielfachen des Intervalls im
    /// Simulationsverlauf plus der Endsimulationstick; aufsteigend sortiert.
    /// </summary>
    public static long[] ChainSchedule(long totalSimulationTick, long sampleIntervalTicks)
    {
        if (totalSimulationTick < 1 || sampleIntervalTicks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleIntervalTicks), "Horizont und Intervall benoetigen mindestens einen Tick.");
        }

        var schedule = new List<long>((int)(totalSimulationTick / sampleIntervalTicks) + 2);

        for (var tick = sampleIntervalTicks; tick <= totalSimulationTick; tick += sampleIntervalTicks)
        {
            schedule.Add(tick);
        }

        schedule.Add(totalSimulationTick);
        return schedule.ToArray();
    }

    /// <summary>Warm-up vor dem Messbeginn (Praezedenz T-021).</summary>
    public const int WarmupTicks = 480;

    /// <summary>
    /// Kettenstichprobenintervall: alle 30 Minuten Simulationszeit
    /// (= 1800 s mal 20 Hz); Start- und Endstichprobe kommen hinzu.
    /// </summary>
    public const long HashSampleIntervalTicks = 1800L * SimulationContract.TickRateHz;

    /// <summary>Berechnet die Anzahl Kettenstichproben inklusive Start und Ende.</summary>
    public static int ChainSampleCount(long totalTicks, long sampleIntervalTicks)
    {
        if (totalTicks < 1 || sampleIntervalTicks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(sampleIntervalTicks), "Horizont und Intervall benoetigen mindestens einen Tick.");
        }

        return checked((int)((totalTicks / sampleIntervalTicks) + 2));
    }

    /// <summary>
    /// Fensteranzahl eines Horizonts: jedes Fenster umfasst exakt
    /// <paramref name="windowSeconds"/> Simulationstick(s) mal Tickrate.
    /// </summary>
    public static int WindowCount(long totalTicks, int windowSeconds)
    {
        if (totalTicks < 1 || windowSeconds < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSeconds), "Fenstergroesse benoetigt mindestens eine Sekunde.");
        }

        var stride = (long)windowSeconds * SimulationContract.TickRateHz;
        return checked((int)((totalTicks + stride - 1) / stride));
    }

    /// <summary>Ticks je Fenster bei der gegebenen Fenstergroesse.</summary>
    public static long WindowTickStride(int windowSeconds) =>
        (long)windowSeconds * SimulationContract.TickRateHz;
}
