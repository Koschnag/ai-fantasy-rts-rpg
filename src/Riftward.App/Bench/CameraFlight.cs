using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Riftward.App.Bench;

/// <summary>Ein deterministisches Kamerapfad-Sample des Flugskripts.</summary>
public readonly record struct CameraSample(
    int FrameIndex,
    double YawDegrees,
    double PitchDegrees,
    double RadiusMeters)
{
    public string CanonicalText() => string.Create(
        CultureInfo.InvariantCulture,
        $"f={FrameIndex};yaw={(YawDegrees.ToString("F3", CultureInfo.InvariantCulture))};pitch={(PitchDegrees.ToString("F3", CultureInfo.InvariantCulture))};r={(RadiusMeters.ToString("F3", CultureInfo.InvariantCulture))}");
}

/// <summary>
/// Festes Kameraflugskript mit Seed (T-020, AC-T020-03): Die Bahn ist eine
/// quantisierte Orbitbewegung um den Szenenursprung. Alle Werte entstehen aus
/// einer ganzzahligen Xorshift-Permutation (kein Uhr- oder Umgebungszzufall);
/// identische Konfiguration liefert byteidentische Samplefolgen.
/// </summary>
public static class CameraFlight
{
    public const string AlgorithmId = "xorshift64star-fixedpoint-v1";

    /// <summary>Standardseed dieses Auftrags; im Report maschinenlesbar gebunden.</summary>
    public const uint DefaultSeed = 20260824u;

    public static IReadOnlyList<CameraSample> Samples(uint seed, int frameCount)
    {
        if (frameCount < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(frameCount), "Der Kamerapfad benoetigt mindestens ein Sample.");
        }

        var samples = new CameraSample[frameCount];
        ulong state = Mix(seed);

        for (var frame = 0; frame < frameCount; frame++)
        {
            state = Next(state);

            // Feste Quantisierung: 1/1000 Grad bzw. Meter halten die Werte
            // exakt reproduzierbar und von JIT-/Libm-Unterschieden frei.
            var yawMilli = (long)(state % 720_000UL) - 360_000L;
            state = Next(state);
            var pitchMilli = (long)(state % 60_000UL) - 15_000L;
            state = Next(state);
            var radiusMilli = 2_500L + (long)(state % 1_500UL);

            samples[frame] = new CameraSample(
                frame,
                yawMilli / 1000.0,
                pitchMilli / 1000.0,
                radiusMilli / 1000.0);
        }

        return samples;
    }

    /// <summary>Blickpunkt und Auge eines Samples in Weltkoordinaten.</summary>
    public readonly record struct CameraPose(CameraMath.Vec3 Eye, CameraMath.Vec3 Center);

    /// <summary>Blickpunkt und Auge eines Samples in Weltkoordinaten.</summary>
    public static CameraPose Pose(CameraSample sample)
    {
        var yaw = sample.YawDegrees * Math.PI / 180.0;
        var pitch = sample.PitchDegrees * Math.PI / 180.0;
        var horizontal = sample.RadiusMeters * Math.Cos(pitch);

        return new CameraPose(
            new CameraMath.Vec3(
                horizontal * Math.Sin(yaw),
                sample.RadiusMeters * Math.Sin(pitch),
                horizontal * Math.Cos(yaw)),
            new CameraMath.Vec3(0, 0, 0));
    }

    /// <summary>Stabiler Hash ueber die kanonische Samplefolge (SHA-256, Hex).</summary>
    public static string HashHex(IReadOnlyList<CameraSample> samples)
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
