namespace Riftward.App.Soak;

/// <summary>
/// Rein rechnerische Auswertung von Fensterstichproben des Arbeitssatzes
/// fuer den Abschnitt-0-Spike: lineare Trendanpassung (kleinste Quadrate)
/// ueber die Fensterwerte und Residualkennzahlen als Messrauschbasis der
/// Schwellwertableitung. Keine Uhr-, I/O- oder Zustandsabhaengigkeit; die
/// Klasse entscheidet nichts, sie liefert nur Kennzahlen.
/// </summary>
public static class SoakMemoryAnalysis
{
    /// <summary>Kennzahlen einer Fenstserie in KiB.</summary>
    public sealed record SeriesNoise(
        long SwingKiB,
        double SlopeKiBPerWindow,
        double InterceptKiB,
        double MaxAbsResidualKiB,
        double MedianAbsResidualKiB);

    public static SeriesNoise Analyse(IReadOnlyList<long> windowValuesKiB)
    {
        if (windowValuesKiB.Count < 2)
        {
            throw new ArgumentException("Rauschanalyse benoetigt mindestens zwei Fensterstichproben.", nameof(windowValuesKiB));
        }

        var count = windowValuesKiB.Count;
        long minimum = long.MaxValue;
        long maximum = long.MinValue;
        double sumX = 0;
        double sumY = 0;

        for (var index = 0; index < count; index++)
        {
            var value = windowValuesKiB[index];
            minimum = Math.Min(minimum, value);
            maximum = Math.Max(maximum, value);
            sumX += index;
            sumY += value;
        }

        var meanX = sumX / count;
        var meanY = sumY / count;

        double covariance = 0;
        double variance = 0;

        for (var index = 0; index < count; index++)
        {
            var deltaX = index - meanX;
            var deltaY = windowValuesKiB[index] - meanY;
            covariance += deltaX * deltaY;
            variance += deltaX * deltaX;
        }

        // Konstante Serie oder eine Stichprobe liefern Steigung null.
        var slope = variance > 0 ? covariance / variance : 0;
        var intercept = meanY - (slope * meanX);

        var residuals = new double[count];

        for (var index = 0; index < count; index++)
        {
            residuals[index] = Math.Abs(windowValuesKiB[index] - ((slope * index) + intercept));
        }

        Array.Sort(residuals);
        var median = count % 2 == 1
            ? residuals[count / 2]
            : 0.5 * (residuals[(count / 2) - 1] + residuals[count / 2]);

        return new SeriesNoise(
            SwingKiB: maximum - minimum,
            SlopeKiBPerWindow: slope,
            InterceptKiB: intercept,
            MaxAbsResidualKiB: residuals[count - 1],
            MedianAbsResidualKiB: median);
    }

    /// <summary>
    /// Steigung eines Laufdrittels (KiB je Fenster) auf Basis derselben
    /// kleinsten-Quadrate-Anpassung; Grundlage des Trendkriteriums
    /// (letzte gegen erste Stunde).
    /// </summary>
    public static double ThirdSlope(IReadOnlyList<long> windowValuesKiB, int thirdIndex)
    {
        var total = windowValuesKiB.Count;

        if (total == 0)
        {
            throw new ArgumentException("Serie ohne Fenster.", nameof(windowValuesKiB));
        }

        var start = (int)((long)total * thirdIndex / 3);
        var end = (int)((long)total * (thirdIndex + 1) / 3);

        if (end - start < 2)
        {
            start = Math.Clamp(start - 1, 0, Math.Max(0, total - 2));
            end = Math.Min(total, start + 2);

            if (end - start < 2)
            {
                return 0;
            }
        }

        double sumX = 0;
        double sumY = 0;

        for (var index = start; index < end; index++)
        {
            sumX += index;
            sumY += windowValuesKiB[index];
        }

        var count = end - start;
        var meanX = sumX / count;
        var meanY = sumY / count;

        double covariance = 0;
        double variance = 0;

        for (var index = start; index < end; index++)
        {
            var deltaX = index - meanX;
            covariance += deltaX * (windowValuesKiB[index] - meanY);
            variance += deltaX * deltaX;
        }

        return variance > 0 ? covariance / variance : 0;
    }
}
