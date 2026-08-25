namespace Riftward.App.Soak;

/// <summary>
/// Vorallokierte Fensterstichproben eines Soaklaufs: Working Set,
/// verwaltete Allokationen und Tickzeitpercentile je Fenster. Alle Puffer
/// entstehen vor dem Messbeginn; die Erfassung im Lauf fuehrt keine
/// verwaltete Allokation durch und liegt bewusst ausserhalb der
/// Allokationszaehler-Klammern je Fenster (Erfassung ausserhalb der
/// Heisspfadfenster gemaess Auftrag).
/// </summary>
internal sealed class SoakWindowSeries
{
    private readonly long[] _rssKiB;
    private readonly long[] _allocationDeltaBytes;
    private readonly double[] _tickP50Ms;
    private readonly double[] _tickP95Ms;
    private readonly double[] _tickP99Ms;
    private readonly double[] _tickScratch;
    private readonly long _windowTickStride;
    private int _count;

    public SoakWindowSeries(int windowCount, long windowTickStride)
    {
        if (windowCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(windowCount), "Mindestens ein Fenster erforderlich.");
        }

        if (windowTickStride < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(windowTickStride), "Fensterstride benoetigt mindestens einen Tick.");
        }

        _rssKiB = new long[windowCount];
        _allocationDeltaBytes = new long[windowCount];
        _tickP50Ms = new double[windowCount];
        _tickP95Ms = new double[windowCount];
        _tickP99Ms = new double[windowCount];
        _tickScratch = new double[windowTickStride];
        _windowTickStride = windowTickStride;
    }

    public int Count => _count;

    public int Capacity => _rssKiB.Length;

    public IReadOnlyList<long> RssKiB => _rssKiB;

    public IReadOnlyList<long> AllocationDeltaBytes => _allocationDeltaBytes;

    /// <summary>
    /// Schliesst ein Fenster mit Stichproben ab; Tickzeiten werden
    /// allokationsfrei aus dem vorallokierten Zeitpuffer des Laufs
    /// uebernommen und in-place sortiert.
    /// </summary>
    public void CloseWindow(
        int windowIndex,
        long rssKiBValue,
        long allocationDeltaBytes,
        ReadOnlySpan<double> windowTickTimesMs)
    {
        if (windowIndex != _count || windowIndex >= _rssKiB.Length)
        {
            throw new InvalidOperationException("Fenster muessen in aufsteigender Reihenfolge geschlossen werden.");
        }

        if (windowTickTimesMs.Length > _tickScratch.Length)
        {
            throw new InvalidOperationException("Fenster enthaelt mehr Ticks als der vereinbarte Stride.");
        }

        _rssKiB[windowIndex] = rssKiBValue;
        _allocationDeltaBytes[windowIndex] = allocationDeltaBytes;

        windowTickTimesMs.CopyTo(_tickScratch);
        Array.Sort(_tickScratch, 0, windowTickTimesMs.Length);

        _tickP50Ms[windowIndex] = PercentileFromSorted(_tickScratch, 0.50, windowTickTimesMs.Length);
        _tickP95Ms[windowIndex] = PercentileFromSorted(_tickScratch, 0.95, windowTickTimesMs.Length);
        _tickP99Ms[windowIndex] = PercentileFromSorted(_tickScratch, 0.99, windowTickTimesMs.Length);
        _count++;
    }

    private static double PercentileFromSorted(double[] sorted, double fraction, int count)
    {
        if (count == 0)
        {
            return double.NaN;
        }

        var index = (int)Math.Ceiling(fraction * count) - 1;
        return sorted[Math.Clamp(index, 0, count - 1)];
    }

    /// <summary>Per-Fenster-p50-Werte (nur lesend, nach dem Lauf).</summary>
    public IReadOnlyList<double> WindowP50Ms => _tickP50Ms;

    /// <summary>Per-Fenster-p95-Werte (nur lesend, nach dem Lauf).</summary>
    public IReadOnlyList<double> WindowP95Ms => _tickP95Ms;

    /// <summary>Per-Fenster-p99-Werte (nur lesend, nach dem Lauf).</summary>
    public IReadOnlyList<double> WindowP99Ms => _tickP99Ms;

    /// <summary>
    /// Aggregiert ein Percentil über die Fenster eines Laufdrittels
    /// (Anfang/Mitte/Ende). Rein diagnostisch ohne Gatekopplung; die
    /// Aggregation geschieht nach dem Lauf im Reportabschnitt und darf
    /// daher allozieren.
    /// </summary>
    public static double ThirdPercentile(IReadOnlyList<double> windowValues, int thirdIndex, double fraction)
    {
        var total = windowValues.Count;

        if (total == 0)
        {
            return double.NaN;
        }

        var start = (int)((long)total * thirdIndex / 3);
        var end = (int)((long)total * (thirdIndex + 1) / 3);

        if (end <= start)
        {
            start = Math.Clamp(start, 0, total - 1);
            end = start + 1;
        }

        var copy = new double[end - start];

        for (var index = 0; index < copy.Length; index++)
        {
            copy[index] = windowValues[start + index];
        }

        Array.Sort(copy);

        var position = Math.Clamp((int)Math.Ceiling(fraction * copy.Length) - 1, 0, copy.Length - 1);
        return copy[position];
    }
}
