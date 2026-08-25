using System.Globalization;

namespace Riftward.App.Bench;

/// <summary>
/// Deterministische Kamera-Mathematik fuer BENCH-EMPTY (T-020). Die Formeln
/// bilden exakt die gepinnten bx-Funkten mtxLookAt/mtxProj (Handedness::Left,
/// homogenes NDC wie im OpenGL-Pflichtpfad) in skalarer Arithmetik ab, damit
/// dasselbe Rechenwerk offline testbar bleibt. Kein Uhr- oder Umgebungszzufall.
/// </summary>
public static class CameraMath
{
    public readonly record struct Vec3(double X, double Y, double Z)
    {
        public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);

        public double Dot(Vec3 b) => (X * b.X) + (Y * b.Y) + (Z * b.Z);
    }

    private static Vec3 Cross(Vec3 a, Vec3 b) => new((a.Y * b.Z) - (a.Z * b.Y), (a.Z * b.X) - (a.X * b.Z), (a.X * b.Y) - (a.Y * b.X));

    private static Vec3 Normalize(Vec3 value)
    {
        var length = Math.Sqrt(value.Dot(value));
        return length == 0.0 ? new Vec3(0, 0, 0) : new Vec3(value.X / length, value.Y / length, value.Z / length);
    }

    /// <summary>
    /// Look-At-Viewmatrix nach bx::mtxLookAt(Handedness::Left); 16 doubles im
    /// bx-Speicherlayout (Spaltenfolge der Basisvektoren).
    /// </summary>
    public static double[] LookAt(Vec3 eye, Vec3 at, Vec3 up)
    {
        var result = new double[16];
        LookAt(eye, at, up, result);
        return result;
    }

    /// <summary>
    /// Allokationsfreie Variante von <see cref="LookAt"/> fuer Frame-Hotpaths:
    /// schreibt in den vom Aufrufer gestellten Zielpuffer (16 Elemente).
    /// </summary>
    public static void LookAt(Vec3 eye, Vec3 at, Vec3 up, Span<double> target16)
    {
        if (target16.Length != 16)
        {
            throw new ArgumentException("Zielpuffer benoetigt genau 16 Elemente.", nameof(target16));
        }

        // Left-handed: Blickrichtung zeigt von eye Richtung at.
        var view = Normalize(at - eye);
        var uxv = Cross(up, view);
        var right = uxv.Dot(uxv) == 0.0 ? new Vec3(1, 0, 0) : Normalize(uxv);
        var up2 = Cross(view, right);

        target16[0] = right.X;
        target16[1] = up2.X;
        target16[2] = view.X;
        target16[3] = 0.0;
        target16[4] = right.Y;
        target16[5] = up2.Y;
        target16[6] = view.Y;
        target16[7] = 0.0;
        target16[8] = right.Z;
        target16[9] = up2.Z;
        target16[10] = view.Z;
        target16[11] = 0.0;
        target16[12] = -right.Dot(eye);
        target16[13] = -up2.Dot(eye);
        target16[14] = -view.Dot(eye);
        target16[15] = 1.0;
    }

    /// <summary>
    /// Perspektivprojektion nach bx::mtxProj(_fovy, aspect, near, far,
    /// homogeneousNdc: true, Handedness::Left).
    /// </summary>
    public static double[] PerspectiveFov(double fovyDegrees, double aspect, double nearPlane, double farPlane)
    {
        if (aspect <= 0.0 || fovyDegrees <= 0.0 || farPlane <= nearPlane || nearPlane <= 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(fovyDegrees), "Projektionsparameter ausserhalb des definierten Bereichs.");
        }

        var height = 1.0 / Math.Tan((fovyDegrees * Math.PI / 180.0) * 0.5);
        var width = height * (1.0 / aspect);
        var diff = farPlane - nearPlane;
        var aa = (farPlane + nearPlane) / diff;
        var bb = (2.0 * farPlane * nearPlane) / diff;

        var result = new double[16];
        result[0] = width;
        result[5] = height;

        // Left-handed Vorzeichenkonvention aus mtxProjXYWH bei _x=_y=0.
        result[8] = 0.0;
        result[9] = 0.0;
        result[10] = aa;
        result[11] = 1.0;
        result[14] = -bb;
        return result;
    }

    public static float[] ToFloat16(double[] matrix16)
    {
        if (matrix16.Length != 16)
        {
            throw new ArgumentException("Matrix benoetigt genau 16 Elemente.", nameof(matrix16));
        }

        var result = new float[16];

        for (var index = 0; index < 16; index++)
        {
            result[index] = (float)matrix16[index];
        }

        return result;
    }

    /// <summary>
    /// Allokationsfreie Konversion fuer Frame-Hotpaths: schreibt die
    /// double-Matrix in den vom Aufrufer gestellten float-Zielpuffer.
    /// </summary>
    public static void ToFloat16(ReadOnlySpan<double> matrix16, Span<float> target16)
    {
        if (matrix16.Length != 16 || target16.Length != 16)
        {
            throw new ArgumentException("Matrix und Zielpuffer benoetigen genau 16 Elemente.");
        }

        for (var index = 0; index < 16; index++)
        {
            target16[index] = (float)matrix16[index];
        }
    }

    public static string FormatInvariant(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
