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
        // Left-handed: Blickrichtung zeigt von eye Richtung at.
        var view = Normalize(at - eye);
        var uxv = Cross(up, view);
        var right = uxv.Dot(uxv) == 0.0 ? new Vec3(1, 0, 0) : Normalize(uxv);
        var up2 = Cross(view, right);

        return
        [
            right.X, up2.X, view.X, 0.0,
            right.Y, up2.Y, view.Y, 0.0,
            right.Z, up2.Z, view.Z, 0.0,
            -right.Dot(eye), -up2.Dot(eye), -view.Dot(eye), 1.0,
        ];
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

    public static string FormatInvariant(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
