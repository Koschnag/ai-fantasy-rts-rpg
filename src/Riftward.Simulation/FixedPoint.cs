namespace Riftward.Simulation;

/// <summary>
/// Reine Ganzzahl-Festkommaarithmetik Q16.16 (Vertrag: Numerikmodell
/// <c>q16-16-fixed-point-intonly-v1</c>). Alle Operationen sind ganzahlige
/// Maschinenoperationen ohne Fließkomma, ohne libm und ohne Reihenfolge-
/// freiheiten; identische Eingaben liefern im selben Binary bitidentische
/// Ergebnisse. Multiplikationen halten ihre Operanden so, dass Zwischen-
/// produkte 64 Bit nicht ueberlaufen (Positionsbereich der Welt ist in
/// SimWorld begrenzt und wird dort erzwungen).
/// </summary>
public static class FixedPoint
{
    /// <summary>1.0 in Q16.16.</summary>
    public const long One = 1L << 16;

    /// <summary>Anzahl Bruchbits.</summary>
    public const int FractionBits = 16;

    /// <summary>Festkomma-Multiplikation (a*b) mit nachtraeglicher Skalierung.</summary>
    public static long Mul(long a, long b) => (a * b) >> FractionBits;

    /// <summary>
    /// Exakte ganzzahliege Quadratwurzel (floor) per Newton-Iteration auf
    /// unsigned 64 Bit; deterministisch, iterationsbeschränkt, ohne
    /// Fließkomma. Fuer Eingaben &lt; 2^62 terminiert die Iteration.
    /// </summary>
    public static ulong ISqrt(ulong value)
    {
        if (value < 2UL)
        {
            return value;
        }

        var current = value;
        var next = (current + value / current) >> 1;

        while (next < current)
        {
            current = next;
            next = (current + value / current) >> 1;
        }

        return current;
    }

    /// <summary>Ganzzahliges Minimum ohne Math-Bibliothek (Heisspfadvertrag).</summary>
    public static long Min(long a, long b) => a < b ? a : b;

    /// <summary>Ganzzahlige Bereichsgrenze ohne Math-Bibliothek (Heisspfadvertrag).</summary>
    public static long Clamp(long value, long minimum, long maximum) =>
        value < minimum ? minimum : value > maximum ? maximum : value;

    /// <summary>Quadratdistanz zweier Festkommapunkte (nicht skaliert, zum Vergleichen).</summary>
    public static ulong DistanceSquared(long ax, long ay, long bx, long by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return (ulong)(dx * dx + dy * dy);
    }
}
