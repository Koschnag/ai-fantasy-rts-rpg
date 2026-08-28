using System.Buffers.Binary;

namespace Riftward.App.Bench;

/// <summary>
/// Deterministische Graybox-Geometrie des Belastungsframes (T-023):
/// Einheitenmesh mit exakt einem Boxsegment je Knochen (48 Bones, 576
/// Dreiecke je Einheit), Landschaftsmesh ueber der Simulationswelt und
/// Partikelquad. Alle Bytes entstehen reproduzierbar; keine Uhr-, Netz- oder
/// Zufallsbeitraege. Kein Spielinhalt und kein Shipping-Asset.
/// </summary>
public static class RepresentativeMesh
{
    /// <summary>Vertexstride des Einheitenmesh: pos 3f, normal 4u8n, indices 4u8, weight 4u8n.</summary>
    public const int UnitVertexStride = 24;

    /// <summary>Dreiecke je Einheit (48 Segmente x 12 Dreiecke).</summary>
    public const int TrianglesPerUnit = RepresentativeScenario.BonesPerNormalUnit * 12;

    /// <summary>Instanzstride der Einheiten: 3 x vec4 (Position/Yaw, Pose/Zeile/Scale, Farbe).</summary>
    public const int UnitInstanceStrideBytes = 48;

    /// <summary>Instanzstride der Partikel: 3 x vec4.</summary>
    public const int ParticleInstanceStrideBytes = 48;

    public sealed record UnitMesh(byte[] Vertices, byte[] Indices, int VertexCount, int TriangleCount);

    /// <summary>
    /// Baut das Einheitenmesh aus den Bind-Pose-Segmenten: je Knochen eine
    /// Box vom Elternursprung zum Knochenursprung (Wurzel erhaelt ein
    /// Beckensegment zur Ursprungsebene).
    /// </summary>
    public static UnitMesh BuildUnitMesh()
    {
        var bindPositions = RepresentativeRig.BindPositions();

        var vertices = new List<byte>(RepresentativeScenario.BonesPerNormalUnit * 24 * UnitVertexStride);
        var indices = new List<byte>(RepresentativeScenario.BonesPerNormalUnit * 36 * 2);
        var vertexCount = 0;

        for (var bone = 0; bone < RepresentativeScenario.BonesPerNormalUnit; bone++)
        {
            var parent = RepresentativeRig.ParentOf(bone);
            var end = bindPositions[bone];
            var start = parent >= 0 ? bindPositions[parent] : (0.0, end.Item2 - RootSegmentHeight, 0.0);
            // Das sichtbare Segment beginnt am Elterngelenk und endet am
            // Kindgelenk. Es muss deshalb starr vom Elternknochen bewegt
            // werden: Dessen Rotation schwenkt das Segment um seinen Start.
            // Eine Bindung an den Kindknochen rotiert stattdessen um das
            // Segmentende und zerreisst die Silhouette in der Gehpose.
            var skinningBone = parent >= 0 ? parent : bone;

            var radius = SegmentRadius(bone);
            vertexCount += AppendBox(
                vertices,
                indices,
                start.Item1,
                start.Item2,
                start.Item3,
                end.Item1,
                end.Item2,
                end.Item3,
                radius,
                skinningBone);
        }

        return new UnitMesh(
            vertices.ToArray(),
            indices.ToArray(),
            vertexCount,
            RepresentativeScenario.BonesPerNormalUnit * 12);
    }

    private const double RootSegmentHeight = 0.18;

    private static double SegmentRadius(int bone) =>
        bone switch
        {
            >= 1 and <= 6 => 0.085,
            7 => 0.05,
            8 => 0.075,
            >= 9 and <= 16 => 0.042,
            >= 17 and <= 26 => 0.055,
            >= 27 and <= 31 => 0.034,
            >= 32 and <= 33 => 0.05,
            >= 34 and <= 39 => 0.02,
            >= 40 and <= 47 => 0.03,
            _ => 0.06,
        };

    /// <summary>
    /// Haengt eine achsenausgerichtete Box zwischen zwei Punkten an. Die
    /// Normalen zeigen von der Segmentachse weg; alle Ecken tragen den
    /// Boneindex mit vollem Gewicht.
    /// </summary>
    private static int AppendBox(
        List<byte> vertices,
        List<byte> indices,
        double startX,
        double startY,
        double startZ,
        double endX,
        double endY,
        double endZ,
        double radius,
        int boneIndex)
    {
        var axisX = endX - startX;
        var axisY = endY - startY;
        var axisZ = endZ - startZ;
        var length = Math.Sqrt((axisX * axisX) + (axisY * axisY) + (axisZ * axisZ));

        if (length < 1e-6)
        {
            return 0;
        }

        var ax = axisX / length;
        var ay = axisY / length;
        var az = axisZ / length;

        // Zwei Orthogonalen zur Achse (stabil fuer Achsen nahe +/-Y).
        double refX = Math.Abs(ay) > 0.9 ? 1.0 : 0.0;
        double refY = Math.Abs(ay) > 0.9 ? 0.0 : 1.0;
        double refZ = 0.0;

        double ux = (refY * az) - (refZ * ay);
        double uy = (refZ * ax) - (refX * az);
        double uz = (refX * ay) - (refY * ax);
        var uLength = Math.Sqrt((ux * ux) + (uy * uy) + (uz * uz));
        ux /= uLength;
        uy /= uLength;
        uz /= uLength;

        double vx = (ay * uz) - (az * uy);
        double vy = (az * ux) - (ax * uz);
        double vz = (ax * uy) - (ay * ux);

        var baseVertex = CountVertices(vertices);
        var cursor = 0;

        // Acht Ecken: untere Ringkante (Start) und obere Ringkante (Ende).
        for (var half = 0; half < 2; half++)
        {
            var cx = half == 0 ? startX : endX;
            var cy = half == 0 ? startY : endY;
            var cz = half == 0 ? startZ : endZ;

            // Beide Ringe muessen dieselbe Orientierung besitzen. Ein vom
            // Ringende abhaengiges Vorzeichen verdrehte bislang jedes
            // Segment um 180 Grad: Die Mantelquads kreuzten sich und die
            // Figur zerfiel im Bild in sternfoermige Dreieckssplitter.
            cornerBuffer[cursor++] = [cx + ((ux + vx) * radius), cy + ((uy + vy) * radius), cz + ((uz + vz) * radius)];
            cornerBuffer[cursor++] = [cx + ((-ux + vx) * radius), cy + ((-uy + vy) * radius), cz + ((-uz + vz) * radius)];
            cornerBuffer[cursor++] = [cx + ((-ux - vx) * radius), cy + ((-uy - vy) * radius), cz + ((-uz - vz) * radius)];
            cornerBuffer[cursor++] = [cx + ((ux - vx) * radius), cy + ((uy - vy) * radius), cz + ((uz - vz) * radius)];
        }

        // Vier Mantelflaechen mit aus der Geometrie abgeleiteten Normalen.
        for (var side = 0; side < 4; side++)
        {
            var next = (side + 1) & 3;
            EmitQuad(
                vertices, indices,
                cornerBuffer[side], cornerBuffer[next],
                cornerBuffer[4 + next], cornerBuffer[4 + side],
                boneIndex,
                invertNormal: true);
        }

        // Kappen.
        EmitQuad(vertices, indices, cornerBuffer[4], cornerBuffer[5], cornerBuffer[6], cornerBuffer[7], boneIndex);
        EmitQuad(vertices, indices, cornerBuffer[3], cornerBuffer[2], cornerBuffer[1], cornerBuffer[0], boneIndex);

        return CountVertices(vertices) - baseVertex;
    }

    /// <summary>Acht Eckpuffer des laufenden Boxbaus (nur Aufbauzeit).</summary>
    private static readonly double[][] cornerBuffer =
    [
        new double[3], new double[3], new double[3], new double[3],
        new double[3], new double[3], new double[3], new double[3],
    ];

    /// <summary>
    /// Haengt eine Viererkette als zwei Dreiecke an; die Flaechnormale
    /// entsteht aus dem Kreuzprodukt der Kanten (ausserhalb sichtbar).
    /// </summary>
    private static int EmitQuad(
        List<byte> vertices,
        List<byte> indices,
        double[] a,
        double[] b,
        double[] c,
        double[] d,
        int boneIndex,
        bool invertNormal = false)
    {
        var e1x = b[0] - a[0];
        var e1y = b[1] - a[1];
        var e1z = b[2] - a[2];
        var e2x = d[0] - a[0];
        var e2y = d[1] - a[1];
        var e2z = d[2] - a[2];

        var nx = (e1y * e2z) - (e1z * e2y);
        var ny = (e1z * e2x) - (e1x * e2z);
        var nz = (e1x * e2y) - (e1y * e2x);
        var length = Math.Sqrt((nx * nx) + (ny * ny) + (nz * nz));

        if (length > 1e-9)
        {
            nx /= length;
            ny /= length;
            nz /= length;
        }
        else
        {
            nx = 0.0;
            ny = 1.0;
            nz = 0.0;
        }

        var normalX = (byte)Math.Clamp(((nx * 0.5) + 0.5) * 255.0, 0, 255);
        var normalY = (byte)Math.Clamp(((ny * 0.5) + 0.5) * 255.0, 0, 255);
        var normalZ = (byte)Math.Clamp(((nz * 0.5) + 0.5) * 255.0, 0, 255);

        if (invertNormal)
        {
            (normalX, normalY, normalZ) = ((byte)(255 - normalX), (byte)(255 - normalY), (byte)(255 - normalZ));
        }

        var baseVertex = CountVertices(vertices);

        AppendVertex(vertices, a, normalX, normalY, normalZ, boneIndex);
        AppendVertex(vertices, b, normalX, normalY, normalZ, boneIndex);
        AppendVertex(vertices, c, normalX, normalY, normalZ, boneIndex);
        AppendVertex(vertices, d, normalX, normalY, normalZ, boneIndex);
        AppendTriangleIndices(indices, baseVertex);

        return 4;
    }

    private static int CountVertices(List<byte> vertices) => vertices.Count / UnitVertexStride;

    private static void AppendTriangleIndices(List<byte> indices, int baseVertex)
    {
        Span<ushort> triangles = [(ushort)baseVertex, (ushort)(baseVertex + 1), (ushort)(baseVertex + 2),
            (ushort)baseVertex, (ushort)(baseVertex + 2), (ushort)(baseVertex + 3)];

        foreach (var index in triangles)
        {
            indices.Add((byte)(index & 0xFF));
            indices.Add((byte)(index >> 8));
        }
    }

    private static void AppendVertex(
        List<byte> vertices,
        double[] position,
        byte normalX,
        byte normalY,
        byte normalZ,
        int boneIndex)
    {
        Span<byte> bytes = stackalloc byte[UnitVertexStride];

        BinaryPrimitives.WriteSingleLittleEndian(bytes[0..], (float)position[0]);
        BinaryPrimitives.WriteSingleLittleEndian(bytes[4..], (float)position[1]);
        BinaryPrimitives.WriteSingleLittleEndian(bytes[8..], (float)position[2]);

        bytes[12] = normalX;
        bytes[13] = normalY;
        bytes[14] = normalZ;
        bytes[15] = 0;

        bytes[16] = (byte)boneIndex;
        bytes[17] = 0;
        bytes[18] = 0;
        bytes[19] = 0;

        // Das prozedurale Segment ist starr genau einem Knochen zugeordnet.
        // Vier wiederholte Graustufenbytes ergaben zuvor als normalisierte
        // Gewichte eine Summe weit ueber 1 und skalierten jede Hautmatrix.
        bytes[20] = byte.MaxValue;
        bytes[21] = 0;
        bytes[22] = 0;
        bytes[23] = 0;

        for (var index = 0; index < UnitVertexStride; index++)
        {
            vertices.Add(bytes[index]);
        }
    }

    /* ---------------------------------------------------------- Terrain */

    public sealed record TerrainMesh(byte[] Vertices, byte[] Indices, int VertexCount, int TriangleCount);

    /// <summary>Vertexstride der Landschaft: pos 3f, normal 4u8n, color 4u8n.</summary>
    public const int TerrainVertexStride = 20;

    public static TerrainMesh BuildTerrain()
    {
        var gridX = (RepresentativeLandscape.WidthMeters * RepresentativeLandscape.SubTilesPerTile) + 1;
        var gridZ = (RepresentativeLandscape.DepthMeters * RepresentativeLandscape.SubTilesPerTile) + 1;

        var vertices = new byte[gridX * gridZ * TerrainVertexStride];
        var heights = new double[gridX * gridZ];

        for (var z = 0; z < gridZ; z++)
        {
            for (var x = 0; x < gridX; x++)
            {
                var worldX = RepresentativeLandscape.ToWorldX(x / (double)RepresentativeLandscape.SubTilesPerTile);
                var worldZ = RepresentativeLandscape.ToWorldZ(z / (double)RepresentativeLandscape.SubTilesPerTile);

                var height = RepresentativeLandscape.HeightAt(worldX, worldZ);
                heights[(z * gridX) + x] = height;

                var normal = RepresentativeLandscape.NormalAt(worldX, worldZ);
                var offset = ((z * gridX) + x) * TerrainVertexStride;

                WriteFloat(vertices, offset + 0, (float)worldX);
                WriteFloat(vertices, offset + 4, (float)height);
                WriteFloat(vertices, offset + 8, (float)worldZ);

                (vertices[offset + 12], vertices[offset + 13], vertices[offset + 14]) = PackNormal(normal.X, normal.Y, normal.Z);
                vertices[offset + 15] = 0;

                var isWall = height >= RepresentativeLandscape.WallHeightMeters - 0.001;
                var tone = isWall ? (byte)96 : (byte)(140 + (int)(40.0 * RepresentativeLandscape.Lattice01(x / 7, z / 11)));
                vertices[offset + 16] = tone;
                vertices[offset + 17] = isWall ? tone : (byte)(tone + 12);
                vertices[offset + 18] = isWall ? (byte)(tone + 8) : (byte)(tone >> 1);
                vertices[offset + 19] = 255;
            }
        }

        var quadCount = (gridX - 1) * (gridZ - 1);
        var indices = new byte[quadCount * 6 * 2];
        var indexCursor = 0;

        for (var z = 0; z < gridZ - 1; z++)
        {
            for (var x = 0; x < gridX - 1; x++)
            {
                ushort v00 = (ushort)((z * gridX) + x);
                ushort v10 = (ushort)(v00 + 1);
                ushort v01 = (ushort)(((z + 1) * gridX) + x);
                ushort v11 = (ushort)(v01 + 1);

                Span<ushort> triangles = [v00, v10, v11, v00, v11, v01];

                foreach (var index in triangles)
                {
                    indices[indexCursor++] = (byte)(index & 0xFF);
                    indices[indexCursor++] = (byte)(index >> 8);
                }
            }
        }

        return new TerrainMesh(vertices, indices, gridX * gridZ, quadCount * 2);
    }

    private static void WriteFloat(byte[] target, int offset, float value) =>
        BinaryPrimitives.WriteSingleLittleEndian(target.AsSpan(offset, 4), value);

    private static (byte X, byte Y, byte Z) PackNormal(double x, double y, double z) => (
        (byte)Math.Clamp(((x * 0.5) + 0.5) * 255.0, 0, 255),
        (byte)Math.Clamp(((y * 0.5) + 0.5) * 255.0, 0, 255),
        (byte)Math.Clamp(((z * 0.5) + 0.5) * 255.0, 0, 255));

    /* -------------------------------------------------------- Partikel */

    public sealed record ParticleQuad(byte[] Vertices, int VertexCount, int TrianglesPerInstance);

    /// <summary>Vertexstride des Quads: pos 2f, uv 2f.</summary>
    public const int ParticleVertexStride = 16;

    public static ParticleQuad BuildParticleQuad()
    {
        var vertices = new byte[4 * ParticleVertexStride];

        Span<(float X, float Y, float U, float V)> corners =
        [
            (-0.5f, -0.5f, 0.0f, 0.0f),
            (0.5f, -0.5f, 1.0f, 0.0f),
            (0.5f, 0.5f, 1.0f, 1.0f),
            (-0.5f, 0.5f, 0.0f, 1.0f),
        ];

        for (var corner = 0; corner < 4; corner++)
        {
            var offset = corner * ParticleVertexStride;
            WriteFloat(vertices, offset + 0, corners[corner].X);
            WriteFloat(vertices, offset + 4, corners[corner].Y);
            WriteFloat(vertices, offset + 8, corners[corner].U);
            WriteFloat(vertices, offset + 12, corners[corner].V);
        }

        return new ParticleQuad(vertices, 4, 2);
    }

    /* --------------------------------------------------- Instanzfuellung */

    /// <summary>
    /// Schreibt eine Einheiteninstanz (Position/Yaw, Pose/Zeile/Scale,
    /// Farbe) in den Instanzpuffer. Keine Allokation; die Farbe entsteht
    /// deterministisch aus Pfadstatus und Index.
    /// </summary>
    public static void WriteUnitInstance(
        float[] target,
        int instanceIndex,
        double worldX,
        double groundY,
        double worldZ,
        double yawRadians,
        double walkPhase,
        float scale,
        int paletteRow,
        byte pathState)
    {
        var offset = instanceIndex * (UnitInstanceStrideBytes / sizeof(float));

        target[offset + 0] = (float)worldX;
        target[offset + 1] = (float)groundY;
        target[offset + 2] = (float)worldZ;
        target[offset + 3] = (float)yawRadians;

        target[offset + 4] = (float)walkPhase;
        target[offset + 5] = scale;
        target[offset + 6] = paletteRow;
        target[offset + 7] = 0f;

        // Pfadstatus bestimmt die Grauton-Familie; simulierte Agenten sind
        // damit sichtbar von Hintergrundakteuren unterscheidbar.
        byte red = pathState switch
        {
            2 => 214,
            1 => 168,
            3 => 128,
            _ => 186,
        };
        byte green = pathState switch
        {
            2 => 178,
            1 => 190,
            3 => 132,
            _ => 196,
        };

        target[offset + 8] = red / 255f;
        target[offset + 9] = green / 255f;
        target[offset + 10] = 172 / 255f;
        target[offset + 11] = 1f;
    }

    /// <summary>Schreibt eine Partikelinstanz (Position/Groesse, Drehung/Farbe).</summary>
    public static void WriteParticleInstance(
        float[] target,
        int slot,
        double worldX,
        double worldY,
        double worldZ,
        float size,
        float rotation,
        float red,
        float green,
        float blue,
        float alpha)
    {
        var offset = slot * (ParticleInstanceStrideBytes / sizeof(float));

        target[offset + 0] = (float)worldX;
        target[offset + 1] = (float)worldY;
        target[offset + 2] = (float)worldZ;
        target[offset + 3] = size;

        target[offset + 4] = rotation;
        target[offset + 5] = alpha;
        target[offset + 6] = 0f;
        target[offset + 7] = 0f;

        target[offset + 8] = red;
        target[offset + 9] = green;
        target[offset + 10] = blue;
        target[offset + 11] = 1f;
    }
}
