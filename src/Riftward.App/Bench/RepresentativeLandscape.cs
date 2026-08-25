using Riftward.Simulation;

namespace Riftward.App.Bench;

/// <summary>
/// Deterministische Graybox-Landschaft des integrierten Belastungsframes
/// (T-023): Hoehenfeld ueber der Simulationswelt (160 x 90 Kacheln),
/// Wand-/Blockadenabbildung ueber die oeffentlichen, schreibgeschuetzten
/// Navigationszugriffe und Lichtplatzierung ueber Zonenzentren. Kein
/// Spielinhalt, keine Namen, keine Fremdbezuege (Clean-Room).
/// </summary>
public static class RepresentativeLandscape
{
    /// <summary>Weltgroesse in Metern (1 Kachel == 1 m).</summary>
    public const int WidthMeters = NavWorld.TilesX;
    public const int DepthMeters = NavWorld.TilesY;

    /// <summary>Unterteilung je Kachel (2 x 2 Quads) der Landschaftsmesh.</summary>
    public const int SubTilesPerTile = 2;

    public const double WallHeightMeters = 2.4;
    public const double MaxRollingAmplitudeMeters = 1.5;

    /// <summary>Kacheln mit Schattenlichtern (Zonen 1 bis 4 der Graybox-Welt).</summary>
    public static readonly IReadOnlyList<int> LightZones = [1, 2, 3, 4];

    /// <summary>Lichthoehe ueber Grund und Reichweite in Metern.</summary>
    public const double LightHeightMeters = 6.0;
    public const double LightRadiusMeters = 11.0;

    public static double ToWorldX(double simXMeters) => simXMeters - (WidthMeters / 2.0);

    public static double ToWorldZ(double simYMeters) => simYMeters - (DepthMeters / 2.0);

    public static (double X, double Z) ZoneCenterWorld(int zone)
    {
        var xQ16 = NavWorld.ZoneCenterXQ16(zone);
        var zQ16 = NavWorld.ZoneCenterYQ16(zone);
        return (
            ToWorldX(xQ16 / (double)FixedPoint.One),
            ToWorldZ(zQ16 / (double)FixedPoint.One));
    }

    /// <summary>Ganzezzahl-Hash als deterministisches Rauschfundament ([0,1)).</summary>
    public static double Lattice01(int latticeX, int latticeZ)
    {
        unchecked
        {
            ulong state = (ulong)(uint)(latticeX * 374761393 + latticeZ * 668265263) + 0x9E3779B97F4A7C15UL;
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            state *= 0x2545F4914F6CDD1DUL;
            state ^= state >> 33;
            return (state >> 40) / (double)(1UL << 24);
        }
    }

    private static double ValueNoise(double x, double z)
    {
        var ix = (int)Math.Floor(x);
        var iz = (int)Math.Floor(z);
        var fx = x - ix;
        var fz = z - iz;

        // Smoothstep-Gewichte halten das Feld stetig differenzierbar genug
        // fuer stabile Zentraldifferenzen-Normalen.
        var sx = fx * fx * (3.0 - (2.0 * fx));
        var sz = fz * fz * (3.0 - (2.0 * fz));

        var n00 = Lattice01(ix, iz);
        var n10 = Lattice01(ix + 1, iz);
        var n01 = Lattice01(ix, iz + 1);
        var n11 = Lattice01(ix + 1, iz + 1);

        return (n00 * (1.0 - sx) + n10 * sx) * (1.0 - sz)
            + (n01 * (1.0 - sx) + n11 * sx) * sz;
    }

    /// <summary>Sanft gewelltes Basisfeld ohne Wandaufbau (Meter).</summary>
    public static double RollingHeight(double worldX, double worldZ) =>
        (MaxRollingAmplitudeMeters * ValueNoise((worldX + 400.0) / 13.0, (worldZ + 400.0) / 11.0))
        + (0.55 * MaxRollingAmplitudeMeters * ValueNoise((worldX + 900.0) / 5.3, (worldZ + 700.0) / 6.1));

    /// <summary>
    /// Gesamthoehe an Weltkoordinaten: rollendes Feld plus Plateau auf
    /// nicht begehbaren Kacheln (Waende und Blockaden der Graybox-Welt).
    /// </summary>
    public static double HeightAt(double worldX, double worldZ)
    {
        var base_ = RollingHeight(worldX, worldZ);

        var tileX = (int)Math.Floor(worldX + (WidthMeters / 2.0));
        var tileZ = (int)Math.Floor(worldZ + (DepthMeters / 2.0));

        if (tileX < 0 || tileX >= WidthMeters || tileZ < 0 || tileZ >= DepthMeters)
        {
            return base_;
        }

        return NavWorld.IsWalkable(tileX, tileZ) ? base_ : Math.Max(base_, WallHeightMeters);
    }

    /// <summary>Zentrale Differenzen der Gesamthoehe (Einheitsnormalen).</summary>
    public static (double X, double Y, double Z) NormalAt(double worldX, double worldZ)
    {
        const double epsilon = 0.05;
        var hL = HeightAt(worldX - epsilon, worldZ);
        var hR = HeightAt(worldX + epsilon, worldZ);
        var hD = HeightAt(worldX, worldZ - epsilon);
        var hU = HeightAt(worldX, worldZ + epsilon);

        var nx = hL - hR;
        var nz = hD - hU;
        var ny = 2.0 * epsilon;
        var length = Math.Sqrt((nx * nx) + (ny * ny) + (nz * nz));

        if (length <= 0.0)
        {
            return (0.0, 1.0, 0.0);
        }

        return (nx / length, ny / length, nz / length);
    }

    /// <summary>Deterministisch platziertes lokales Schattenlicht.</summary>
    public sealed record LightPlacement(double X, double Y, double Z, double Radius);

    /// <summary>Deterministische Platzierung der vier lokalen Schattenlichter.</summary>
    public static LightPlacement[] LightPlacements()
    {
        var placements = new LightPlacement[LocalLightCount];

        for (var index = 0; index < LocalLightCount; index++)
        {
            var (x, z) = ZoneCenterWorld(LightZones[index]);
            placements[index] = new LightPlacement(x, LightHeightMeters, z, LightRadiusMeters);
        }

        return placements;
    }

    private static LightPlacement[]? _cachedPlacements;

    /// <summary>
    /// Frame-Hotpath-Variante von <see cref="LightPlacements"/>: gibt die
    /// einmal aufgebaute, unveraenderliche Platzierung zurueck (keine
    /// Arrayallokation je Frame; Inhalt ist deterministisch identisch).
    /// </summary>
    public static LightPlacement[] CachedPlacements() => _cachedPlacements ??= LightPlacements();

    /// <summary>Y-Anteil der Flaechennormale (Testnaht ohne Tupelzugriff).</summary>
    public static double NormalUpComponent(double worldX, double worldZ) => NormalAt(worldX, worldZ).Item2;

    public const int LocalLightCount = RepresentativeScenario.LocalShadowLights;
}
