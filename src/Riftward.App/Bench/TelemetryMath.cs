namespace Riftward.App.Bench;

/// <summary>p50/p95/p99-Band einer Framezeitmessreihe in Millisekunden.</summary>
public sealed record FrameTimeBand(double P50Ms, double P95Ms, double P99Ms);

/// <summary>
/// Rein rechnerische Telemetriehilfen (T-020). Alle Funktionen arbeiten auf
/// Kopien und sind ohne Uhr-, Netz- oder Hardwarebezug testbar.
/// </summary>
public static class TelemetryMath
{
    /// <summary>Percentil als naechstgroessere Ordnungsstatistik (Verfahren wie T-010).</summary>
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

    public static FrameTimeBand Band(IReadOnlyList<double> frameTimesMs) => new(
        Percentile(frameTimesMs, 0.50),
        Percentile(frameTimesMs, 0.95),
        Percentile(frameTimesMs, 0.99));

    /// <summary>Deterministische Kanonisierung von Messwerten fuer Hashfixtures.</summary>
    public static string Canonical(double value) => CameraMath.FormatInvariant(value);
}
