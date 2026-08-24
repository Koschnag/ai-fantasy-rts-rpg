namespace Riftward.Simulation;

/// <summary>
/// Synthetische Navigationswelt der Simulationsbaseline
/// (<c>riftward-simworld-graybox-v1</c>): festes Kachelraster mit
/// wandartigen Blockaden und je drei Toren pro Wandreihe, darunterliegender
/// Blockgraph (10x10-Kachelbloecke) als obere Hierarchieebene und
/// korridorbeschraenkte Breitensuche auf Kachelebene als untere Ebene.
/// Die Geometrie ist seedunabhaengig fixiert; der Seed beeinflusst nur
/// Streuung und Befehlsplan. Kein Spielinhalt, keine kreativen Assets.
///
/// Die beiden Wandreihen lassen Randwege und Tore zu, sodass zwischen
/// gegenueberliegenden Zonen mehrere konkurrierende Routen entstehen.
/// </summary>
public sealed class NavWorld
{
    /// <summary>1 Kachel = 1 m in Q16.16.</summary>
    public const int TileSizeQ16 = 1 << FixedPoint.FractionBits;

    public const int TilesX = 160;
    public const int TilesY = 90;
    public const int TileCount = TilesX * TilesY;

    public const int BlockSize = 10;
    public const int BlocksX = TilesX / BlockSize;
    public const int BlocksY = TilesY / BlockSize;
    public const int BlockCount = BlocksX * BlocksY;

    /// <summary>Anzahl Zielgebiete (Zonen) der Welt.</summary>
    public const int ZoneCount = 6;

    /// <summary>Maximale Wegpunktanzahl je Fertigpfad.</summary>
    public const int MaxWaypointsPerAgent = 512;

    // Zonen als geschlossene Kachelbereiche [X0..X1] x [Y0..Y1]; bewusst in
    // getrennten Weltvierteln, damit Gruppen konkurrierende laengere Wege
    // durch die Tore oder ueber die Randkorridore nehmen.
    private static readonly int[] ZoneBoundsTable =
    [
        /* West      */ 3, 38, 15, 52,
        /* Ost       */ 144, 37, 156, 51,
        /* Nord      */ 74, 5, 86, 15,
        /* Sued      */ 74, 74, 86, 84,
        /* West-Mitte*/ 44, 40, 54, 50,
        /* Ost-Mitte */ 105, 39, 115, 49,
    ];

    private static readonly bool[] Walkable = BuildWalkability();
    private static readonly int[] BlockNeighborsTable = BuildBlockGraph(Walkable);

    /// <summary>Baut die Geometrie einmal je Prozess deterministisch auf.</summary>
    static NavWorld() => ValidateZones(Walkable);

    public static bool IsWalkable(int tileX, int tileY) =>
        tileX >= 0 && tileX < TilesX && tileY >= 0 && tileY < TilesY
        && Walkable[(tileY * TilesX) + tileX];

    public static bool IsWalkableIndex(int tileIndex) => Walkable[tileIndex];

    public static int TileIndexOfPosition(long positionQ16) => (int)(positionQ16 >> FixedPoint.FractionBits);

    public static long ZoneCenterXQ16(int zone) =>
        ((ZoneBoundsTable[(zone * 4)] + ZoneBoundsTable[(zone * 4) + 2] + 1L) * TileSizeQ16) >> 1;

    public static long ZoneCenterYQ16(int zone) =>
        ((ZoneBoundsTable[(zone * 4) + 1] + ZoneBoundsTable[(zone * 4) + 3] + 1L) * TileSizeQ16) >> 1;

    /// <summary>Liegt eine Kachel innerhalb des Zielgebiets?</summary>
    public static bool IsInsideZone(int zone, int tileX, int tileY)
    {
        var x0 = ZoneBoundsTable[(zone * 4)];
        var y0 = ZoneBoundsTable[(zone * 4) + 1];
        var x1 = ZoneBoundsTable[(zone * 4) + 2];
        var y1 = ZoneBoundsTable[(zone * 4) + 3];
        return tileX >= x0 && tileX <= x1 && tileY >= y0 && tileY <= y1;
    }

    /// <summary>Nachbar eines Blocks in fester Richtung N,O,S,W (-1 = keine Kante).</summary>
    public static int BlockNeighbor(int block, int direction) => BlockNeighborsTable[(block * 4) + direction];

    /// <summary>Anzahl vorhandener Kanten eines Blocks.</summary>
    public static int BlockNeighborCount(int block)
    {
        var count = 0;

        for (var direction = 0; direction < 4; direction++)
        {
            if (BlockNeighborsTable[(block * 4) + direction] >= 0)
            {
                count++;
            }
        }

        return count;
    }

    public static int BlockOfTile(int tileIndex)
    {
        var x = tileIndex % TilesX;
        var y = tileIndex / TilesX;
        return ((y / BlockSize) * BlocksX) + (x / BlockSize);
    }

    public static int RandomFreeTileInZone(int zone, ref SimRandom random, Func<int, bool> isOccupied)
    {
        var x0 = ZoneBoundsTable[(zone * 4)];
        var y0 = ZoneBoundsTable[(zone * 4) + 1];
        var x1 = ZoneBoundsTable[(zone * 4) + 2];
        var y1 = ZoneBoundsTable[(zone * 4) + 3];
        var width = x1 - x0 + 1;
        var height = y1 - y0 + 1;

        for (var attempt = 0; attempt < 256; attempt++)
        {
            var x = x0 + random.NextInt(width);
            var y = y0 + random.NextInt(height);
            var index = (y * TilesX) + x;

            if (!isOccupied(index))
            {
                return index;
            }
        }

        // Deterministischer Rueckfall: erste freie Kachel der Zone in fester Ordnung.
        for (var y = y0; y <= y1; y++)
        {
            for (var x = x0; x <= x1; x++)
            {
                var index = (y * TilesX) + x;

                if (!isOccupied(index))
                {
                    return index;
                }
            }
        }

        throw new InvalidOperationException($"Zone {zone} bietet keinen freien Startplatz.");
    }

    private static void ValidateZones(bool[] walkable)
    {
        for (var zone = 0; zone < ZoneCount; zone++)
        {
            var x0 = ZoneBoundsTable[(zone * 4)];
            var y0 = ZoneBoundsTable[(zone * 4) + 1];
            var x1 = ZoneBoundsTable[(zone * 4) + 2];
            var y1 = ZoneBoundsTable[(zone * 4) + 3];

            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    if (!Walkable[(y * TilesX) + x])
                    {
                        throw new InvalidOperationException(
                            $"Zone {zone} enthaelt unbetretbare Kacheln ({x},{y}); Weltlayout verletzt den Vertrag.");
                    }
                }
            }
        }
    }

    /// <summary>
    /// Feste Geometrie ohne Zufall: Randmauern, zwei horizontale Wandreihen
    /// mit je drei Toren und vier Rechteckblockaden. Identische Aufrufe
    /// liefern identische Felder.
    /// </summary>
    private static bool[] BuildWalkability()
    {
        var walkable = new bool[TileCount];
        Array.Fill(walkable, true);

        void FillRect(int x0, int y0, int x1, int y1)
        {
            for (var y = y0; y <= y1; y++)
            {
                for (var x = x0; x <= x1; x++)
                {
                    walkable[(y * TilesX) + x] = false;
                }
            }
        }

        // Rand.
        FillRect(0, 0, TilesX - 1, 0);
        FillRect(0, TilesY - 1, TilesX - 1, TilesY - 1);
        FillRect(0, 0, 0, TilesY - 1);
        FillRect(TilesX - 1, 0, TilesX - 1, TilesY - 1);

        // Zwei Wandreihen mit je drei Toren (je zwei Kacheln breit).
        foreach (var wallY in (Span<int>)stackalloc int[] { 29, 30, 59, 60 })
        {
            FillRect(20, wallY, 140, wallY);

            walkable[(wallY * TilesX) + 38] = true;
            walkable[(wallY * TilesX) + 39] = true;
            walkable[(wallY * TilesX) + 78] = true;
            walkable[(wallY * TilesX) + 79] = true;
            walkable[(wallY * TilesX) + 118] = true;
            walkable[(wallY * TilesX) + 119] = true;
        }

        // Rechteckblockaden (korridorbildend, schneiden keine Zone).
        FillRect(60, 8, 66, 24);
        FillRect(94, 66, 100, 82);
        FillRect(28, 62, 36, 70);
        FillRect(124, 18, 132, 26);

        return walkable;
    }

    /// <summary>
    /// Blockgraph als obere Hierarchieebene: eine Kante existiert genau dann,
    /// wenn an der gemeinsamen Blockgrenze mindestens ein beidseitig
    /// betretbares Kachelpaar liegt. Nachbarn in fester Reihenfolge N,O,S,W
    /// (-1 = keine Kante).
    /// </summary>
    private static int[] BuildBlockGraph(bool[] walkable)
    {
        var neighbors = new int[BlockCount * 4];
        Array.Fill(neighbors, -1);

        bool EdgeNorth(int blockX, int blockY)
        {
            if (blockY == 0)
            {
                return false;
            }

            var baseX = blockX * BlockSize;
            var yA = blockY * BlockSize;
            var yB = yA - 1;

            for (var offset = 0; offset < BlockSize; offset++)
            {
                if (walkable[(yA * TilesX) + baseX + offset] && walkable[(yB * TilesX) + baseX + offset])
                {
                    return true;
                }
            }

            return false;
        }

        bool EdgeSouth(int blockX, int blockY)
        {
            if (blockY == BlocksY - 1)
            {
                return false;
            }

            var baseX = blockX * BlockSize;
            var yA = (blockY * BlockSize) + BlockSize - 1;
            var yB = yA + 1;

            for (var offset = 0; offset < BlockSize; offset++)
            {
                if (walkable[(yA * TilesX) + baseX + offset] && walkable[(yB * TilesX) + baseX + offset])
                {
                    return true;
                }
            }

            return false;
        }

        bool EdgeWest(int blockX, int blockY)
        {
            if (blockX == 0)
            {
                return false;
            }

            var baseY = blockY * BlockSize;
            var xA = blockX * BlockSize;
            var xB = xA - 1;

            for (var offset = 0; offset < BlockSize; offset++)
            {
                if (walkable[((baseY + offset) * TilesX) + xA] && walkable[((baseY + offset) * TilesX) + xB])
                {
                    return true;
                }
            }

            return false;
        }

        bool EdgeEast(int blockX, int blockY)
        {
            if (blockX == BlocksX - 1)
            {
                return false;
            }

            var baseY = blockY * BlockSize;
            var xA = (blockX * BlockSize) + BlockSize - 1;
            var xB = xA + 1;

            for (var offset = 0; offset < BlockSize; offset++)
            {
                if (walkable[((baseY + offset) * TilesX) + xA] && walkable[((baseY + offset) * TilesX) + xB])
                {
                    return true;
                }
            }

            return false;
        }

        for (var blockY = 0; blockY < BlocksY; blockY++)
        {
            for (var blockX = 0; blockX < BlocksX; blockX++)
            {
                var block = (blockY * BlocksX) + blockX;
                neighbors[(block * 4) + 0] = EdgeNorth(blockX, blockY) ? block - BlocksX : -1;
                neighbors[(block * 4) + 1] = EdgeEast(blockX, blockY) ? block + 1 : -1;
                neighbors[(block * 4) + 2] = EdgeSouth(blockX, blockY) ? block + BlocksX : -1;
                neighbors[(block * 4) + 3] = EdgeWest(blockX, blockY) ? block - 1 : -1;
            }
        }

        return neighbors;
    }
}
