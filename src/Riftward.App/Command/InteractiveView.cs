using System.Runtime.InteropServices;
using Riftward.App.Bench;
using Riftward.Platform;
using Riftward.Session;
using Riftward.Simulation;

namespace Riftward.App.Command;

/// <summary>
/// Graybox-Darstellung der Kommandoschleife nach T-023-Rendermustern:
/// Vertragslandschaft, die 250 vollständig simulierten Agenten (schreib-
/// geschützte Ansicht), vier aktive Schattenpaesse und die vertragliche
/// Zweikanal-Rueckmeldung (NF-005: Form plus Farbe, nie reine Farbcodierung):
/// (1) Auswahlmarker als schwebende Glyphen ueber den Agenten ausgewaehlter
/// Gruppen (Formkanal, warmton); (2) Befehlsrueckmeldung als wachsender
/// Bodenpuls am Zielzonenzentrum (Groessen-/Bewegungskanal, kaltton).
/// </summary>
internal sealed class InteractiveView : IDisposable
{
    /// <summary>Lebensdauer eines Befehlspulses in Ticks.</summary>
    public const int CommandPulseTicks = 40;

    /// <summary>Hoechstzahl gleichzeitiger Markerinstanzen (Glyphen plus Pulse).</summary>
    public const int MarkerCapacity =
        SimulationContract.AgentCount + SimulationContract.GroupCount;

    private const int PaletteRowFloats = RepresentativeScenario.BonesPerNormalUnit * 3 * 4;

    private readonly float[] _units =
        new float[SimulationContract.AgentCount * (RepresentativeMesh.UnitInstanceStrideBytes / sizeof(float))];

    private readonly float[] _markers = new float[MarkerCapacity * (RepresentativeMesh.ParticleInstanceStrideBytes / sizeof(float))];

    private readonly float[] _palette = new float[SimulationContract.AgentCount * PaletteRowFloats];

    private readonly GCHandle _unitsHandle;
    private readonly GCHandle _markersHandle;

    private readonly byte[] _agentGroups = new byte[SimulationContract.AgentCount];
    private readonly double[] _previousX = new double[SimulationContract.AgentCount];
    private readonly double[] _previousZ = new double[SimulationContract.AgentCount];
    private readonly double[] _yaw = new double[SimulationContract.AgentCount];
    private readonly RepresentativeRig.PoseEvaluator _poseEvaluator = new();
    private readonly (long IssueTick, int Zone)[] _pulses =
        new (long IssueTick, int Zone)[SimulationContract.GroupCount];

    private SelectionModel? _selection;

    public InteractiveView()
    {
        _unitsHandle = GCHandle.Alloc(_units, GCHandleType.Pinned);
        _markersHandle = GCHandle.Alloc(_markers, GCHandleType.Pinned);

        for (var slot = 0; slot < _pulses.Length; slot++)
        {
            _pulses[slot] = (-1, -1);
        }
    }

    public nint UnitsPointer => _unitsHandle.AddrOfPinnedObject();

    public nint MarkersPointer => _markersHandle.AddrOfPinnedObject();

    /// <summary>Palettenzeilen aller Agenten fuer den Texturupload.</summary>
    public ReadOnlySpan<float> Palette => _palette;

    /// <summary>Initialisiert die zeitinvarianten Agentengruppen aus dem Kernsnapshot.</summary>
    public void BindAgentGroups(ReadOnlySpan<byte> agentGroups) => agentGroups.CopyTo(_agentGroups);

    /// <summary>Bindet das Auswahlmodell fuer die Glyphenentscheidung.</summary>
    public void BindSelection(SelectionModel selection) => _selection = selection;

    /// <summary>Meldet einen abgesetzten Gruppenbefehl fuer die Puls-Rueckmeldung.</summary>
    public void NotifyCommandIssued(long tickIndex, int zone)
    {
        var replaceSlot = -1;
        var oldestSlot = 0;

        for (var slot = 0; slot < _pulses.Length; slot++)
        {
            var (issueTick, _) = _pulses[slot];

            if (issueTick < 0 || tickIndex - issueTick >= CommandPulseTicks)
            {
                replaceSlot = slot;
                break;
            }

            if (issueTick < _pulses[oldestSlot].IssueTick)
            {
                oldestSlot = slot;
            }
        }

        _pulses[replaceSlot >= 0 ? replaceSlot : oldestSlot] = (tickIndex, zone);
    }

    /// <summary>
    /// Schreibt Einheiten-, Paletten- und Markerdaten des aktuellen Ticks.
    /// Rueckgabe: Anzahl geschriebener Markerinstanzen.
    /// </summary>
    public int WriteFrameState(SimWorld world, long tickIndex)
    {
        for (var agent = 0; agent < world.AgentCount; agent++)
        {
            var positionX = world.PositionXOf(agent);
            var positionZ = world.PositionYOf(agent);

            var deltaX = positionX - _previousX[agent];
            var deltaZ = positionZ - _previousZ[agent];
            _previousX[agent] = positionX;
            _previousZ[agent] = positionZ;

            if ((deltaX * deltaX) + (deltaZ * deltaZ) > 4)
            {
                // Mindestbewegung von 2/65536 m: Richtung ist belastbar;
                // ruhende Agenten behalten ihre letzte Blickrichtung.
                _yaw[agent] = Math.Atan2(deltaZ, deltaX);
            }

            var worldX = RepresentativeLandscape.ToWorldX(positionX / (double)FixedPoint.One);
            var worldZ = RepresentativeLandscape.ToWorldZ(positionZ / (double)FixedPoint.One);
            var groundY = RepresentativeLandscape.HeightAt(worldX, worldZ);

            var walkPhase = (tickIndex * RepresentativeRig.WalkPhasePerTick * ((agent % 2) == 0 ? 1.1 : 1.0))
                + (agent * 0.37);

            RepresentativeMesh.WriteUnitInstance(
                _units,
                agent,
                worldX,
                groundY,
                worldZ,
                _yaw[agent],
                walkPhase,
                scale: 1.0f,
                paletteRow: agent,
                pathState: (byte)world.PathStateOf(agent));
        }

        for (var row = 0; row < SimulationContract.AgentCount; row++)
        {
            var walkPhase = (tickIndex * RepresentativeRig.WalkPhasePerTick * ((row % 2) == 0 ? 1.1 : 1.05))
                + (row * 0.29);
            var unitSeed = (uint)((0x5EEDu ^ ((ulong)row * 0x9E3779B97F4A7C15UL)) & 0xFFFFFFFFUL);
            _poseEvaluator.EvaluateRow(unitSeed, walkPhase, _palette.AsSpan(row * PaletteRowFloats, PaletteRowFloats));
        }

        return WriteMarkers(world, tickIndex);
    }

    /// <summary>Zweikanalmarker gemäß Vertrag Abschnitt 3 der Rueckmeldung.</summary>
    private int WriteMarkers(SimWorld world, long tickIndex)
    {
        var markerCount = 0;

        // Kanal 1: Auswahlglyphe (Form ueber der Einheit, warmton).
        for (var agent = 0; agent < world.AgentCount && markerCount < MarkerCapacity; agent++)
        {
            if (_selection is null || !_selection.IsSelected(_agentGroups[agent]))
            {
                continue;
            }

            var worldX = RepresentativeLandscape.ToWorldX(world.PositionXOf(agent) / (double)FixedPoint.One);
            var worldZ = RepresentativeLandscape.ToWorldZ(world.PositionYOf(agent) / (double)FixedPoint.One);
            var groundY = RepresentativeLandscape.HeightAt(worldX, worldZ);
            var bobbing = Math.Sin((tickIndex * 0.35) + (agent * 0.61)) * 0.12;

            RepresentativeMesh.WriteParticleInstance(
                _markers,
                markerCount++,
                worldX,
                groundY + 2.05 + bobbing,
                worldZ,
                size: 0.42f,
                rotation: (float)(tickIndex * 0.08),
                red: 1.00f,
                green: 0.78f,
                blue: 0.30f,
                alpha: 0.92f);
        }

        // Kanal 2: Befehlspuls am Zielzonenzentrum (wachsende Groesse, kaltton).
        for (var slot = 0; slot < _pulses.Length && markerCount < MarkerCapacity; slot++)
        {
            var (issueTick, zone) = _pulses[slot];

            if (issueTick < 0 || zone < 0 || tickIndex - issueTick >= CommandPulseTicks)
            {
                continue;
            }

            var growth = (tickIndex - issueTick) / (double)CommandPulseTicks;
            var (centerX, centerZ) = RepresentativeLandscape.ZoneCenterWorld(zone);

            RepresentativeMesh.WriteParticleInstance(
                _markers,
                markerCount++,
                centerX,
                RepresentativeLandscape.HeightAt(centerX, centerZ) + 0.25,
                centerZ,
                size: (float)(1.2 + (7.5 * growth)),
                rotation: 0f,
                red: 0.30f,
                green: 0.80f,
                blue: 0.95f,
                alpha: (float)(0.85 * (1.0 - growth)));
        }

        return markerCount;
    }

    public void Dispose()
    {
        _unitsHandle.Free();
        _markersHandle.Free();
    }
}

/// <summary>
/// Deterministische Kamera- und Pickingmathematik des Interaktivmodus
/// (graybox-camera-model-v0): geneigte Top-Down-Ansicht mit fester
/// Nordausrichtung, Bildschirm-zu-Bodenstrahlen fuer Auswahl- und
/// Befehlsintents. Reine Double-Arithmetik ohne Uhr- oder Umgebungsbeitrag.
/// </summary>
public static class InteractiveCameraMath
{
    /// <summary>Bodenschnitt eines Bildschirmstrahls in Simulationsmetern.</summary>
    public readonly record struct GroundPoint(double SimX, double SimZ);

    public const double PitchRadians = GrayboxCamera.PitchDegrees * Math.PI / 180.0;

    public const double FieldOfViewDegrees = BenchRunner.FieldOfViewDegrees;

    public const double NearPlane = 0.5;

    public const double FarPlane = 500.0;

    /// <summary>Auge-Punkt der Kamera fuer einen Kamerazustand.</summary>
    public static (double X, double Y, double Z) EyePosition(GrayboxCamera camera)
    {
        var groundY = RepresentativeLandscape.HeightAt(camera.CenterXMeters, camera.CenterZMeters);
        return (
            camera.CenterXMeters,
            groundY + (Math.Sin(PitchRadians) * camera.DistanceMeters),
            camera.CenterZMeters + (Math.Cos(PitchRadians) * camera.DistanceMeters));
    }

    /// <summary>Blickziel (Mittelpunkt am Boden).</summary>
    public static (double X, double Y, double Z) CenterPosition(GrayboxCamera camera) =>
        (camera.CenterXMeters, RepresentativeLandscape.HeightAt(camera.CenterXMeters, camera.CenterZMeters), camera.CenterZMeters);

    /// <summary>Projektion (float16) fuer das aktuelle Seitenverhaeltnis.</summary>
    public static float[] Projection(int width, int height) =>
        CameraMath.ToFloat16(CameraMath.PerspectiveFov(FieldOfViewDegrees, width / (double)height, NearPlane, FarPlane));

    /// <summary>Viewmatrix (float16) fuer einen Kamerazustand.</summary>
    public static float[] View16(GrayboxCamera camera)
    {
        var eye = EyePosition(camera);
        var center = CenterPosition(camera);
        return CameraMath.ToFloat16(CameraMath.LookAt(
            new CameraMath.Vec3(eye.Item1, eye.Item2, eye.Item3),
            new CameraMath.Vec3(center.Item1, center.Item2, center.Item3),
            new CameraMath.Vec3(0, 1, 0)));
    }

    /// <summary>
    /// Projiziert eine Bildschirmposition auf die Bodenebene y=0 und liefert
    /// Simulationsmeter (x nach Osten, z nach Sueden). Rueckgabe null bei
    /// singulaerer Matrix oder Strahl ohne Bodenschnitt.
    /// </summary>
    public static GroundPoint? ScreenToGround(
        GrayboxCamera camera, int width, int height, double pixelX, double pixelY)
    {
        var eye = EyePosition(camera);
        var center = CenterPosition(camera);
        var view = CameraMath.LookAt(
            new CameraMath.Vec3(eye.Item1, eye.Item2, eye.Item3),
            new CameraMath.Vec3(center.Item1, center.Item2, center.Item3),
            new CameraMath.Vec3(0, 1, 0));
        var projection = CameraMath.PerspectiveFov(FieldOfViewDegrees, width / (double)height, NearPlane, FarPlane);
        var inverse = Invert(Multiply(view, projection));

        if (inverse is null)
        {
            return null;
        }

        var ndcX = ((pixelX / width) * 2.0) - 1.0;
        var ndcY = 1.0 - ((pixelY / height) * 2.0);
        var nearPoint = TransformPoint(inverse, ndcX, ndcY, -1.0);
        var farPoint = TransformPoint(inverse, ndcX, ndcY, +1.0);

        var directionY = farPoint.Y - nearPoint.Y;

        if (Math.Abs(directionY) < 1e-9)
        {
            return null;
        }

        var t = -nearPoint.Y / directionY;

        if (t < 0.0)
        {
            return null;
        }

        var simX = nearPoint.X + (t * (farPoint.X - nearPoint.X)) + (RepresentativeLandscape.WidthMeters / 2.0);
        var simZ = nearPoint.Z + (t * (farPoint.Z - nearPoint.Z)) + (RepresentativeLandscape.DepthMeters / 2.0);
        return new GroundPoint(simX, simZ);
    }

    /// <summary>Ermittelt den Zonenindex eines Bodenspunkts in Simulationsmetern; -1 ohne Zone.</summary>
    public static int ZoneAtGroundPoint(double simXMeters, double simZMeters)
    {
        var tileX = (int)Math.Floor(simXMeters);
        var tileY = (int)Math.Floor(simZMeters);

        for (var zone = 0; zone < NavWorld.ZoneCount; zone++)
        {
            if (NavWorld.IsInsideZone(zone, tileX, tileY))
            {
                return zone;
            }
        }

        return -1;
    }

    /// <summary>
    /// Billboard-Basis (rechts/oben) des Kameraframes in der Reihenfolge
    /// [rightX, rightY, rightZ, upX, upY, upZ]; dieselbe Orthogonalisierung
    /// wie im T-023-Partikelpfad.
    /// </summary>
    public static double[] BillboardBasis(GrayboxCamera camera)
    {
        var eye = EyePosition(camera);
        var center = CenterPosition(camera);

        var forwardX = center.Item1 - eye.Item1;
        var forwardY = center.Item2 - eye.Item2;
        var forwardZ = center.Item3 - eye.Item3;
        var forwardLength = Math.Sqrt((forwardX * forwardX) + (forwardY * forwardY) + (forwardZ * forwardZ));

        forwardX /= forwardLength;
        forwardY /= forwardLength;
        forwardZ /= forwardLength;

        var rightX = forwardZ;
        var rightZ = -forwardX;
        var rightLength = Math.Sqrt((rightX * rightX) + (rightZ * rightZ));

        if (rightLength > 1e-9)
        {
            rightX /= rightLength;
            rightZ /= rightLength;
        }
        else
        {
            rightX = 1.0;
            rightZ = 0.0;
        }

        var upX = (-forwardY * rightZ);
        var upY = (forwardZ * rightX) - (forwardX * rightZ);
        var upZ = forwardY * rightX;

        return [rightX, 0.0, rightZ, upX, upY, upZ];
    }

    private static (double X, double Y, double Z) TransformPoint(double[] matrix16, double x, double y, double z)
    {
        var w = (matrix16[3] * x) + (matrix16[7] * y) + (matrix16[11] * z) + matrix16[15];

        if (w == 0.0)
        {
            w = 1e-12;
        }

        return (
            ((matrix16[0] * x) + (matrix16[4] * y) + (matrix16[8] * z) + matrix16[12]) / w,
            ((matrix16[1] * x) + (matrix16[5] * y) + (matrix16[9] * z) + matrix16[13]) / w,
            ((matrix16[2] * x) + (matrix16[6] * y) + (matrix16[10] * z) + matrix16[14]) / w);
    }

    /// <summary>Spaltenmajor-Multiplikation view*proj (bx-Layout, 16 doubles).</summary>
    public static double[] Multiply(double[] left, double[] right)
    {
        var result = new double[16];

        for (var column = 0; column < 4; column++)
        {
            for (var row = 0; row < 4; row++)
            {
                double value = 0;

                for (var k = 0; k < 4; k++)
                {
                    value += left[(k * 4) + row] * right[(column * 4) + k];
                }

                result[(column * 4) + row] = value;
            }
        }

        return result;
    }

    /// <summary>Inverse einer 4x4-Matrix via Gauss-Jordan mit Pivotisierung; null bei Singularitaet.</summary>
    public static double[]? Invert(double[] matrix)
    {
        var augmented = new double[4, 8];

        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                // Spaltenmajorquelle: Element (Zeile r, Spalte c) an Index c*4+r.
                augmented[row, column] = matrix[(column * 4) + row];
            }

            augmented[row, 4 + row] = 1.0;
        }

        for (var pivot = 0; pivot < 4; pivot++)
        {
            var bestRow = pivot;

            for (var candidate = pivot + 1; candidate < 4; candidate++)
            {
                if (Math.Abs(augmented[candidate, pivot]) > Math.Abs(augmented[bestRow, pivot]))
                {
                    bestRow = candidate;
                }
            }

            if (Math.Abs(augmented[bestRow, pivot]) < 1e-300)
            {
                return null;
            }

            if (bestRow != pivot)
            {
                for (var column = 0; column < 8; column++)
                {
                    (augmented[pivot, column], augmented[bestRow, column]) =
                        (augmented[bestRow, column], augmented[pivot, column]);
                }
            }

            var diagonal = augmented[pivot, pivot];

            for (var column = 0; column < 8; column++)
            {
                augmented[pivot, column] /= diagonal;
            }

            for (var row = 0; row < 4; row++)
            {
                if (row == pivot)
                {
                    continue;
                }

                var factor = augmented[row, pivot];

                if (factor == 0.0)
                {
                    continue;
                }

                for (var column = 0; column < 8; column++)
                {
                    augmented[row, column] -= factor * augmented[pivot, column];
                }
            }
        }

        var result = new double[16];

        for (var row = 0; row < 4; row++)
        {
            for (var column = 0; column < 4; column++)
            {
                result[(column * 4) + row] = augmented[row, 4 + column];
            }
        }

        return result;
    }
}
