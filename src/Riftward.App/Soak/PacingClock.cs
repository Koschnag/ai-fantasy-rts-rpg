using System.Diagnostics;

namespace Riftward.App.Soak;

/// <summary>
/// Injizierbare monotone Taktquelle des Soaks. Sie treibt ausschliesslich
/// die Pacingentscheidung (wie viele Ticks faellig sind), die
/// Watchdogbeobachtung und die Telemetrie; der Simulationszustand selbst
/// bleibt vollstaendig uhrfrei (Vertrag: entkoppeltes, deterministisches
/// Schrittwerk ohne Uhrabhaengigkeit).
/// </summary>
internal interface ITickPacingClock
{
    /// <summary>Startet die Monotonuhr des Laufs.</summary>
    void Start();

    /// <summary>Monotone verstrichene Sekunden seit <see cref="Start"/>.</summary>
    double ElapsedSeconds { get; }

    /// <summary>
    /// Wartet, bis mindestens <paramref name="targetSeconds"/> verstrichen sind;
    /// Implementierungen duerfen frueher zurueckkehren, der Treiber prueft die
    /// Faelligkeitsbedingung erneut.
    /// </summary>
    void WaitUntil(double targetSeconds);
}

/// <summary>
/// Hochaufgeloeste Tickzeitmessung. Uhr- und Schlafprimitive des Soaks
/// liegen ausschliesslich in dieser Quelldatei (<c>PacingClock.cs</c>);
/// alle anderen Soakdateien beziehen Zeitstempel nur ueber sie oder ueber
/// die injizierte <see cref="ITickPacingClock"/>. Der Simulationszustand
/// bleibt von allen hier erzeugten Werten unberuehrt.
/// </summary>
internal static class TickTiming
{
    private static readonly double TicksToMs = 1000.0 / Stopwatch.Frequency;

    public static long Timestamp() => Stopwatch.GetTimestamp();

    public static double DeltaMs(long startTimestamp, long endTimestamp) =>
        (endTimestamp - startTimestamp) * TicksToMs;
}

/// <summary>
/// Realzeit-Taktquelle: Stopuhr plus kooperatives Schlafen. Der
/// beschleunigte Diagnosemodus nutzt dieselbe Quelle zur Messung, ruft aber
/// kein <see cref="ITickPacingClock.WaitUntil"/> auf und arbeitet den Plan
/// damit so schnell wie moeglich ab; solche Laeufe sind im Report als
/// diagnostisch markiert und zaehlen zu keinem Zeitpunkt als NF-002-Nachweis.
/// </summary>
internal sealed class RealtimePacingClock : ITickPacingClock
{
    private const double SpinThresholdSeconds = 0.002;
    private readonly Stopwatch _stopwatch = new();

    public void Start() => _stopwatch.Restart();

    public double ElapsedSeconds => _stopwatch.Elapsed.TotalSeconds;

    public void WaitUntil(double targetSeconds)
    {
        while (true)
        {
            var remaining = targetSeconds - ElapsedSeconds;

            if (remaining <= 0)
            {
                return;
            }

            if (remaining > SpinThresholdSeconds)
            {
                // Grobes Schlafen bis kurz vor die Faeligkeit; die Restzeit
                // wird spinnend erreicht, ohne die Tickfolge zu ueberschiessen.
                Thread.Sleep(1);
                continue;
            }

            Thread.SpinWait(32);
        }
    }
}
