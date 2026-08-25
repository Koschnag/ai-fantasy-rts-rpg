using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Riftward.App.Bench;

/// <summary>Ein deterministisches Kamerapfad-Sample des Belastungsframes.</summary>
public readonly record struct RepresentativeCameraSample(
    int FrameIndex,
    double YawDegrees,
    double PitchDegrees,
    double RadiusMeters,
    double CenterHeightMeters)
{
    public string CanonicalText() => string.Create(
        CultureInfo.InvariantCulture,
        $"f={FrameIndex};yaw={YawDegrees.ToString("F3", CultureInfo.InvariantCulture)}"
        + $";pitch={PitchDegrees.ToString("F3", CultureInfo.InvariantCulture)}"
        + $";r={RadiusMeters.ToString("F3", CultureInfo.InvariantCulture)}"
        + $";cy={CenterHeightMeters.ToString("F3", CultureInfo.InvariantCulture)}");
}

/// <summary>
/// Festes Kameraflugskript des integrierten Belastungsframes (T-023,
/// AC-T023-01): quantisierte Orbitbewegung ueber die Graybox-Welt, deren
/// Werte ausschliesslich aus dem Seed per ganzzahliger Xorshift-Permutation
/// entstehen (kein Uhr- oder Umgebungszzufall). Identische Konfiguration
/// liefert byteidentische Samplefolgen.
/// </summary>
public static class RepresentativeCameraFlight
{
    public const string AlgorithmId = "xorshift64star-representative-orbit-v1";

    /// <summary>Orbitzentrum: Weltmitte der Graybox-Landschaft.</summary>
    public const double CenterXMeters = 0.0;
    public const double CenterZMeters = 0.0;

    public static IReadOnlyList<RepresentativeCameraSample> Samples(uint seed, int frameCount)
    {
        if (frameCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(frameCount), "Der Kamerapfad benoetigt mindestens ein Sample.");
        }

        var samples = new RepresentativeCameraSample[frameCount];
        ulong state = Mix(seed);

        for (var frame = 0; frame < frameCount; frame++)
        {
            state = Next(state);

            // Grundschwenk: drei volle Umlaeufe ueber den Horizont plus
            // seedgebundener Jitter; alles auf Milligrau quantisiert.
            var sweepMilli = (long)((long)frame * 1_080_000L / frameCount);
            var jitterMilli = (long)(state % 40_000UL) - 20_000L;
            var yawMilli = sweepMilli + jitterMilli;

            // Neigung ueber dem Horizont: das Auge bleibt mit 27 bis 62 Grad
            // Erhoehung und Radius 52 bis 78 Metern stets deutlich ueber der
            // Landschaft (Augehoehe >= rund 24 Meter) und ueberschaut die
            // Graybox-Szene; die Lastverteilung darf nicht kuenstlich leer
            // sein (AC-T023-02).
            state = Next(state);
            var pitchMilli = 27_000L + (long)(state % 35_000UL);

            state = Next(state);
            var radiusMilli = 52_000L + (long)(state % 26_000UL);

            state = Next(state);
            var centerHeightMilli = 1_000L + (long)(state % 1_500UL);

            samples[frame] = new RepresentativeCameraSample(
                frame,
                yawMilli / 1000.0,
                pitchMilli / 1000.0,
                radiusMilli / 1000.0,
                centerHeightMilli / 1000.0);
        }

        return samples;
    }

    /// <summary>Auge und Blickpunkt eines Samples in Weltkoordinaten.</summary>
    public readonly record struct CameraPose(CameraMath.Vec3 Eye, CameraMath.Vec3 Center);

    public static CameraPose Pose(RepresentativeCameraSample sample)
    {
        var yaw = sample.YawDegrees * Math.PI / 180.0;
        var pitch = sample.PitchDegrees * Math.PI / 180.0;
        var horizontal = sample.RadiusMeters * Math.Cos(pitch);
        var center = new CameraMath.Vec3(CenterXMeters, sample.CenterHeightMeters, CenterZMeters);

        return new CameraPose(
            new CameraMath.Vec3(
                center.X + horizontal * Math.Sin(yaw),
                center.Y + sample.RadiusMeters * Math.Sin(pitch),
                center.Z + horizontal * Math.Cos(yaw)),
            center);
    }

    /// <summary>Stabiler Hash ueber die kanonische Samplefolge (SHA-256, Hex).</summary>
    public static string HashHex(IReadOnlyList<RepresentativeCameraSample> samples)
    {
        var builder = new StringBuilder();

        foreach (var sample in samples)
        {
            builder.Append(sample.CanonicalText());
            builder.Append('\n');
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static ulong Mix(uint seed)
    {
        var state = (ulong)seed + 0x9E3779B97F4A7C15UL;

        for (var round = 0; round < 4; round++)
        {
            state = Next(state);
        }

        return state;
    }

    private static ulong Next(ulong state)
    {
        state ^= state >> 12;
        state ^= state << 25;
        state ^= state >> 27;
        return state * 0x2545F4914F6CDD1DUL;
    }
}
