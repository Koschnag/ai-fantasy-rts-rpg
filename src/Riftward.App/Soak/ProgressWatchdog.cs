namespace Riftward.App.Soak;

/// <summary>
/// Fortschritts-Watchdog (Hangkriterium des Soakvertrags): Beobachtet
/// auesserlich des Heisspfads einen Fortschrittszaehler (Tickindex) gegen
/// injizierte Monotonzeit. Ein Stall liegt vor, wenn der Zaehler laenger als
/// die vertragliche Fensterbreite nicht gestiegen ist. Die Klasse enthaelt
/// keine Uhr- und keinen Zustandszugriff; Zeit- und Fortschrittswerte werden
/// vom Treiber injiziert und sind in Tests frei wählbar.
/// </summary>
internal sealed class ProgressWatchdog
{
    private readonly double _windowSeconds;
    private bool _started;
    private double _lastChangeSeconds;
    private long _lastProgressValue;

    public ProgressWatchdog(double windowSeconds, int maxGapSamples)
    {
        if (windowSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSeconds), "Watchdogfenster benoetigt positive Sekunden.");
        }

        if (maxGapSamples < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maxGapSamples), "Gap-Historie benoetigt mindestens einen Platz.");
        }

        _windowSeconds = windowSeconds;
        _ = maxGapSamples; // Reservekapazität für zukünftige Gap-Statistik; bewusst vorallokiert nicht erforderlich.
    }

    /// <summary>Vertragliche Fensterbreite in Sekunden.</summary>
    public double WindowSeconds => _windowSeconds;

    /// <summary>Größtes beobachtetes Intervall ohne Tickfortschritt in Sekunden.</summary>
    public double MaxObservedProgressGapSeconds { get; private set; }

    /// <summary>Anzahl Watchdogbeobachtungen.</summary>
    public long Observations => _observationCount;

    private long _observationCount;

    /// <summary>
    /// Setzt den Ausgangszustand; der erste Aufruf von <see cref="Observe"/>
    /// nach <see cref="Reset"/> startet die Gap-Messung.
    /// </summary>
    public void Reset(double nowSeconds, long progressValue)
    {
        _started = true;
        _lastChangeSeconds = nowSeconds;
        _lastProgressValue = progressValue;
        MaxObservedProgressGapSeconds = 0;
        _observationCount = 0;
    }

    /// <summary>
    /// Nimmt eine Beobachtung auf und meldet ueber <see cref="IsStalled"/>,
    /// ob der Fortschritt seit mehr als der Fensterbreite stehen geblieben
    /// ist. Fortschrittsspruenge aktualisieren die Gap-Statistik.
    /// </summary>
    public void Observe(double nowSeconds, long progressValue)
    {
        if (!_started)
        {
            Reset(nowSeconds, progressValue);
            return;
        }

        _observationCount++;

        if (progressValue != _lastProgressValue)
        {
            var gap = nowSeconds - _lastChangeSeconds;

            if (gap > MaxObservedProgressGapSeconds)
            {
                MaxObservedProgressGapSeconds = gap;
            }

            _lastChangeSeconds = nowSeconds;
            _lastProgressValue = progressValue;
        }

        // Kein Fortschritt: Stall nur melden, wenn die Fensterbreite
        // ueberschritten ist (Striktheit: exakt am Limit gilt noch nicht als
        // Stall; jede Verlaengerung darueber schon).
    }

    public bool IsStalled(double nowSeconds) =>
        _started && (nowSeconds - _lastChangeSeconds) > _windowSeconds;

    /// <summary>Sekunden ohne Fortschritt zum Beobachtungszeitpunkt.</summary>
    public double SecondsWithoutProgress(double nowSeconds) =>
        _started ? nowSeconds - _lastChangeSeconds : 0;
}
