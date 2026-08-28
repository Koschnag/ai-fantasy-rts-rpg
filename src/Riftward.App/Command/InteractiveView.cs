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

    /// <summary>Hoechstzahl gleichzeitiger Markerinstanzen (Glyphen, Pulse, Held-Badge plus Landmarkenkanal).</summary>
    public const int MarkerCapacity =
        SimulationContract.AgentCount
        + SimulationContract.GroupCount
        + 1
        + 2
        + (2 * NavWorld.ZoneCount);

    /// <summary>Ankerhoehe des unbesuchten Landmarkenmarkers in Metern (Vertrag Abschnitt 5).</summary>
    public const double LandmarkMarkerHeightMeters = 1.6;

    /// <summary>Groesse des ruhenden unbesuchten Landmarkenmarkers.</summary>
    public const float LandmarkMarkerSize = 1.15f;

    /// <summary>Hoehen der zweistufigen registrierten Markiersaeule.</summary>
    public const double RegisteredLandmarkLowerHeightMeters = 1.4;

    public const double RegisteredLandmarkUpperHeightMeters = 3.6;

    /// <summary>Groessen der zweistufigen registrierten Markiersaeule.</summary>
    public const float RegisteredLandmarkLowerSize = 1.25f;

    public const float RegisteredLandmarkUpperSize = 1.05f;

    /// <summary>
    /// Hoehe des heldennahen Zustands-Echos fuer die aktuelle, noch nicht
    /// registrierte Zone. Es liegt deutlich ueber Agenten und Modus-Badge.
    /// </summary>
    public const double HeroLandmarkCueUnvisitedHeightMeters = 4.2;

    /// <summary>Hoehen des heldennahen Zweistufen-Echos einer registrierten Zone.</summary>
    public const double HeroLandmarkCueRegisteredLowerHeightMeters = 3.7;

    public const double HeroLandmarkCueRegisteredUpperHeightMeters = 4.9;

    /// <summary>
    /// Eigene Groessen des heldennahen Echos. Die festen Ankermarker bleiben
    /// groesser; vor der nahen persoenlichen Kamera wuerden dieselben Werte
    /// den Heldenkanal dominieren und die obere Stufe an den Bildrand treiben.
    /// </summary>
    public const float HeroLandmarkCueRegisteredLowerSize = 0.90f;

    public const float HeroLandmarkCueRegisteredUpperSize = 0.75f;

    /// <summary>Lesbare Groesse des ruhenden strategischen Helden-Badges.</summary>
    public const float StrategicHeroBadgeSize = 0.80f;

    /// <summary>Basisgroesse des atmenden persoenlichen Helden-Badges.</summary>
    public const float PersonalHeroBadgeBaseSize = 0.65f;

    /// <summary>Deterministische Atemamplitude des persoenlichen Helden-Badges.</summary>
    public const double PersonalHeroBadgeBreath = 0.10;

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

    private ExplorationSession? _exploration;

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

    /// <summary>
    /// Bindet die optionale Erkundungssitzung für den Landmarkenzustandskanal
    /// (T-034, Vertrag Abschnitt 5): ohne Aktivierung null (Bestandsdarstellung
    /// byteidentisch); die Marker lesen ausschließlich Anker und Aufsuch-
    /// zustand schreibgeschützt und sind niemals Teil von Simulationszustand
    /// oder Hash.
    /// </summary>
    public void BindExploration(ExplorationSession? exploration) => _exploration = exploration;

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
    /// <paramref name="visualMode"/> ist die rein darstellseitige Modusanzeige
    /// des Badge-Kanals (T-033); im Abgriffpaar darf sie vom Sitzungsmodus
    /// abweichen, ohne den Weltzustand zu beruehren.
    /// Rueckgabe: Anzahl geschriebener Markerinstanzen.
    /// </summary>
    public int WriteFrameState(SimWorld world, long tickIndex, SessionMode visualMode)
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

        return WriteMarkers(world, tickIndex, visualMode);
    }

    /// <summary>Zweikanalmarker gemäß Vertrag Abschnitt 3 der Rueckmeldung.</summary>
    private int WriteMarkers(SimWorld world, long tickIndex, SessionMode visualMode)
    {
        var markerCount = 0;

        // Kanal 1: Auswahlglyphe (Form ueber der Einheit, warmton). Die
        // Auswahl bleibt beim Moduswechsel als Sitzungszustand erhalten,
        // wird im persoenlichen Modus aber nicht dargestellt: Dort ist die
        // strategische Auswahlsemantik nicht gebunden, und insbesondere eine
        // grosse Armee darf den Helden-/Landmarkenkanal nicht verdecken.
        for (var agent = 0;
            visualMode == SessionMode.Strategic
            && agent < world.AgentCount
            && markerCount < MarkerCapacity;
            agent++)
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

        // Kanal 3 (T-033, Modevertrag Abschnitt 8, hero-mode-badge-v1):
        // heldenverankerter Modus-Badge über Agentenindex 0 mit zwei
        // unterscheidbaren visuellen Kanaelen (NF-005, nie reine Farbcodierung):
        // strategisch — ruhender Diamant (feste Orientierung pi/4), cyan
        // (0.45/0.85/1.0), Hoehe 2.6 m, Groesse 0.80; persoenlich —
        // pulsierender Diamant (Groesse atmet deterministisch mit der
        // Tickzahl), warmes Orange (1.0/0.45/0.20), Basisgroesse 0.65. Die
        // pulsende Groesse und beide Farbkanäle trennen den Badge von der
        // Auswahlglyphe (warmes Amber, Groesse 0.42, ruhend) und vom
        // Befehlspuls (wachsend, kaltton, bodenverankert).
        if (markerCount < MarkerCapacity)
        {
            var heroX = RepresentativeLandscape.ToWorldX(world.PositionXOf(ModeContract.HeroAgentIndex) / (double)FixedPoint.One);
            var heroZ = RepresentativeLandscape.ToWorldZ(world.PositionYOf(ModeContract.HeroAgentIndex) / (double)FixedPoint.One);
            var heroGroundY = RepresentativeLandscape.HeightAt(heroX, heroZ);
            var badgeRotation = (float)(Math.PI / 4.0);

            if (visualMode == SessionMode.Personal)
            {
                var breath = PersonalHeroBadgeBreath * Math.Sin(tickIndex * 0.35);
                RepresentativeMesh.WriteDiamondInstance(
                    _markers,
                    markerCount++,
                    heroX,
                    heroGroundY + 2.6,
                    heroZ,
                    size: (float)(PersonalHeroBadgeBaseSize + breath),
                    rotation: badgeRotation,
                    red: 1.00f,
                    green: 0.45f,
                    blue: 0.20f,
                    alpha: 0.95f);
            }
            else
            {
                RepresentativeMesh.WriteDiamondInstance(
                    _markers,
                    markerCount++,
                    heroX,
                    heroGroundY + 2.6,
                    heroZ,
                    size: StrategicHeroBadgeSize,
                    rotation: badgeRotation,
                    red: 0.45f,
                    green: 0.85f,
                    blue: 1.00f,
                    alpha: 0.95f);
            }
        }

        // Kanal 4 (T-034, Erkundungsvertrag Abschnitt 5,
        // landmark-state-channel-v1): darstellseitige Landmarkenmarker am
        // Anker mit zwei unterscheidbaren visuellen Kanaelen (NF-005, nie
        // reine Farbcodierung). Unbesucht: ruhender Einzel-Diamant (feste
        // Orientierung pi/4), kuehles Blaugrau (0.55/0.75/0.95), Groesse 1.15,
        // Hoehe 1.6 m. Registriert: zweistufige Markiersaeule — unten
        // ruhend, oben rotierend mit der Tickzahl — kuehles Gruen
        // (0.40/0.90/0.60). Ohne Aktivierung entsteht kein Marker.
        if (_exploration is { } exploration)
        {
            // Der eigentliche Landmarkenmarker bleibt am vertraglichen Anker.
            // Da die Registrierung jedoch zonenweit gilt, kann dieser Anker
            // weit vom Helden und damit ausserhalb eines heldenzentrierten
            // Abgriffs liegen. Ein rein darstellseitiges Zustands-Echo ueber
            // dem Helden macht die aktuelle Zonenlandmarke in beiden Modi
            // ablesbar, ohne Anker, Besuchsregel oder Kernzustand zu aendern.
            var heroZone = HeroTracker.ZoneIndexOf(world);

            if (heroZone >= 0 && heroZone < exploration.LandmarkCount)
            {
                var heroX = RepresentativeLandscape.ToWorldX(world.PositionXOf(ModeContract.HeroAgentIndex) / (double)FixedPoint.One);
                var heroZ = RepresentativeLandscape.ToWorldZ(world.PositionYOf(ModeContract.HeroAgentIndex) / (double)FixedPoint.One);
                var heroGroundY = RepresentativeLandscape.HeightAt(heroX, heroZ);
                var currentLandmarkRegistered = exploration.IsRegistered(heroZone);

                if (!currentLandmarkRegistered && markerCount < MarkerCapacity)
                {
                    RepresentativeMesh.WriteDiamondInstance(
                        _markers,
                        markerCount++,
                        heroX,
                        heroGroundY + HeroLandmarkCueUnvisitedHeightMeters,
                        heroZ,
                        size: LandmarkMarkerSize,
                        rotation: (float)(Math.PI / 4.0),
                        red: 0.55f,
                        green: 0.75f,
                        blue: 0.95f,
                        alpha: 0.95f);
                }
                else if (markerCount + 2 <= MarkerCapacity)
                {
                    RepresentativeMesh.WriteDiamondInstance(
                        _markers,
                        markerCount++,
                        heroX,
                        heroGroundY + HeroLandmarkCueRegisteredLowerHeightMeters,
                        heroZ,
                        size: HeroLandmarkCueRegisteredLowerSize,
                        rotation: (float)(Math.PI / 4.0),
                        red: 0.40f,
                        green: 0.90f,
                        blue: 0.60f,
                        alpha: 0.95f);
                    RepresentativeMesh.WriteDiamondInstance(
                        _markers,
                        markerCount++,
                        heroX,
                        heroGroundY + HeroLandmarkCueRegisteredUpperHeightMeters,
                        heroZ,
                        size: HeroLandmarkCueRegisteredUpperSize,
                        rotation: (float)(tickIndex * 0.12),
                        red: 0.40f,
                        green: 0.90f,
                        blue: 0.60f,
                        alpha: 0.95f);
                }
            }

            foreach (var landmark in exploration.Landmarks)
            {
                if (markerCount + 2 > MarkerCapacity)
                {
                    break;
                }

                var landmarkWorldX = RepresentativeLandscape.ToWorldX(landmark.AnchorTileX + 0.5);
                var landmarkWorldZ = RepresentativeLandscape.ToWorldZ(landmark.AnchorTileY + 0.5);
                var landmarkGroundY = RepresentativeLandscape.HeightAt(landmarkWorldX, landmarkWorldZ);

                if (!exploration.IsRegistered(landmark.ZoneIndex))
                {
                    // Unbesucht: ruhender Einzel-Diamant, feste Orientierung
                    // pi/4, kuehles Blaugrau, Groesse 1.15, Hoehe 1.6 m.
                    RepresentativeMesh.WriteDiamondInstance(
                        _markers,
                        markerCount++,
                        landmarkWorldX,
                        landmarkGroundY + LandmarkMarkerHeightMeters,
                        landmarkWorldZ,
                        size: LandmarkMarkerSize,
                        rotation: (float)(Math.PI / 4.0),
                        red: 0.55f,
                        green: 0.75f,
                        blue: 0.95f,
                        alpha: 0.95f);
                }
                else
                {
                    // Registriert: zweistufige Markiersaeule (unten ruhend,
                    // oben rotierend mit der Tickzahl), kuehles Gruen; die
                    // Gesamtform ist klar zweigeteilt (NF-005).
                    RepresentativeMesh.WriteDiamondInstance(
                        _markers,
                        markerCount++,
                        landmarkWorldX,
                        landmarkGroundY + RegisteredLandmarkLowerHeightMeters,
                        landmarkWorldZ,
                        size: RegisteredLandmarkLowerSize,
                        rotation: (float)(Math.PI / 4.0),
                        red: 0.40f,
                        green: 0.90f,
                        blue: 0.60f,
                        alpha: 0.95f);
                    RepresentativeMesh.WriteDiamondInstance(
                        _markers,
                        markerCount++,
                        landmarkWorldX,
                        landmarkGroundY + RegisteredLandmarkUpperHeightMeters,
                        landmarkWorldZ,
                        size: RegisteredLandmarkUpperSize,
                        rotation: (float)(tickIndex * 0.12),
                        red: 0.40f,
                        green: 0.90f,
                        blue: 0.60f,
                        alpha: 0.95f);
                }
            }
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
/// (graybox-camera-model-v0 und der T-033-Verfolgungskamera
/// hero-chase-camera-v1): geneigte Ansichten mit fester Nordausrichtung,
/// Bildschirm-zu-Bodenstrahlen fuer Auswahl- und Befehlsintents. Reine
/// Double-Arithmetik ohne Uhr- oder Umgebungsbeitrag.
/// </summary>
public static class InteractiveCameraMath
{
    /// <summary>Bodenschnitt eines Bildschirmstrahls in Simulationsmetern.</summary>
    public readonly record struct GroundPoint(double SimX, double SimZ);

    /// <summary>
    /// Aktiver Kamerazustand eines Frames (T-033): Kamerazentrum, Distanz und
    /// Nickwinkel entkoppelt vom konkreten Kameratyp, sodass der strategische
    /// Graybox-Stand und die Verfolgungskamera denselben Render-/Pickingpfad
    /// teilen. Rein darstellseitig, niemals Teil von Simulationszustand.
    /// </summary>
    public readonly record struct ActiveCamera(
        double CenterXMeters,
        double CenterZMeters,
        double DistanceMeters,
        double PitchRadians)
    {
        /// <summary>Aktiver Stand der strategischen Graybox-Kamera.</summary>
        public static ActiveCamera From(GrayboxCamera camera) =>
            new(camera.CenterXMeters, camera.CenterZMeters, camera.DistanceMeters, InteractiveCameraMath.PitchRadians);

        /// <summary>Aktiver Stand der Verfolgungskamera (55°, Modevertrag Abschnitt 8).</summary>
        public static ActiveCamera From(HeroChaseCamera camera)
        {
            var desired = new ActiveCamera(
                camera.CenterXMeters,
                camera.CenterZMeters,
                camera.DistanceMeters,
                HeroChaseCamera.PitchDegrees * Math.PI / 180.0);
            var fitted = FitHorizontalWorld(desired, DefaultViewportAspectRatio, HeroChaseCamera.DistanceMinMeters);
            return ClampToWorldFootprint(fitted, DefaultViewportAspectRatio);
        }
    }

    public const double PitchRadians = GrayboxCamera.PitchDegrees * Math.PI / 180.0;

    public const double FieldOfViewDegrees = BenchRunner.FieldOfViewDegrees;

    public const double DefaultViewportAspectRatio = BenchRunner.DefaultWidth / (double)BenchRunner.DefaultHeight;

    /// <summary>
    /// Bodenabdruck einer nordgerichteten Kamera relativ zu ihrem Blickpunkt.
    /// X ist die maximale halbe Breite an den fernen oberen Bodenecken,
    /// LookPlaneX die halbe Breite auf der Blickpunktebene; NorthZ und SouthZ
    /// sind die Schnittweiten der oberen beziehungsweise unteren Frustumkante.
    /// </summary>
    public readonly record struct GroundFootprintMargins(double X, double LookPlaneX, double NorthZ, double SouthZ);

    /// <summary>
    /// Berechnet den endlichen Bodenabdruck eines Kamera-Frustums. Eine
    /// Postur, deren obere Kante den Horizont beruehrt, wird fail-closed
    /// abgewiesen: Sie kann keinen ehrlich begrenzbaren Weltrandabdruck haben.
    /// </summary>
    public static GroundFootprintMargins GroundFootprint(ActiveCamera camera, double aspectRatio)
    {
        var verticalHalfFov = FieldOfViewDegrees * Math.PI / 360.0;
        var upperRay = camera.PitchRadians - verticalHalfFov;
        var lowerRay = camera.PitchRadians + verticalHalfFov;

        if (!double.IsFinite(aspectRatio)
            || aspectRatio <= 0.0
            || !double.IsFinite(camera.DistanceMeters)
            || camera.DistanceMeters <= 0.0
            || upperRay <= 0.0
            || lowerRay >= Math.PI / 2.0)
        {
            throw new ArgumentOutOfRangeException(nameof(camera), "Kamerapostur hat keinen endlichen Bodenabdruck.");
        }

        var tangentHalfFov = Math.Tan(verticalHalfFov);
        var eyeHeight = Math.Sin(camera.PitchRadians) * camera.DistanceMeters;
        var eyeSouth = Math.Cos(camera.PitchRadians) * camera.DistanceMeters;
        var upperDown = Math.Sin(camera.PitchRadians) - (tangentHalfFov * Math.Cos(camera.PitchRadians));
        var distanceToUpperGround = eyeHeight / upperDown;
        var halfWidth = distanceToUpperGround * tangentHalfFov * aspectRatio;
        var lookPlaneHalfWidth = camera.DistanceMeters * tangentHalfFov * aspectRatio;
        var north = (distanceToUpperGround
                * (Math.Cos(camera.PitchRadians) + (tangentHalfFov * Math.Sin(camera.PitchRadians))))
            - eyeSouth;
        var south = eyeSouth - (eyeHeight / Math.Tan(lowerRay));

        if (!double.IsFinite(halfWidth)
            || !double.IsFinite(lookPlaneHalfWidth)
            || !double.IsFinite(north)
            || !double.IsFinite(south)
            || halfWidth < 0.0
            || lookPlaneHalfWidth < 0.0
            || north < 0.0
            || south < 0.0)
        {
            throw new ArgumentOutOfRangeException(nameof(camera), "Kamerapostur hat keinen gueltigen Bodenabdruck.");
        }

        return new GroundFootprintMargins(halfWidth, lookPlaneHalfWidth, north, south);
    }

    /// <summary>
    /// Reduziert nur die wirksame Darstellungsdistanz, falls der volle
    /// horizontale Bodenabdruck und der Fokus am Weltrand sonst unvereinbar
    /// waeren. Die vorgegebene Mindestdistanz verhindert einen unlesbar
    /// nahen Evidenzzoom; der Sitzungszoom selbst bleibt unangetastet.
    /// </summary>
    public static ActiveCamera FitHorizontalWorld(ActiveCamera camera, double aspectRatio, double minimumDistance)
    {
        if (!double.IsFinite(minimumDistance)
            || minimumDistance <= 0.0
            || minimumDistance > camera.DistanceMeters)
        {
            throw new ArgumentOutOfRangeException(nameof(minimumDistance));
        }

        var unit = GroundFootprint(camera with { DistanceMeters = 1.0 }, aspectRatio);
        var wideningPerMeter = unit.X - unit.LookPlaneX;
        var edgeDistance = Math.Min(camera.CenterXMeters, NavWorld.TilesX - camera.CenterXMeters);
        var fittedDistance = camera.DistanceMeters;

        if (wideningPerMeter > 0.0)
        {
            fittedDistance = Math.Min(fittedDistance, Math.Max(0.0, edgeDistance) / wideningPerMeter);
        }

        return camera with { DistanceMeters = Math.Clamp(fittedDistance, minimumDistance, camera.DistanceMeters) };
    }

    /// <summary>
    /// Verschiebt nur den darstellseitigen Blickpunkt so weit ins Vertragsfeld,
    /// wie es der Bodenabdruck erlaubt. Der eigentliche Kamera-/Sitzungszustand
    /// und die Simulation bleiben unveraendert.
    /// </summary>
    public static ActiveCamera ClampToWorldFootprint(ActiveCamera camera, double aspectRatio)
    {
        var margins = GroundFootprint(camera, aspectRatio);

        static double ClampAxis(double desired, double minimum, double maximum, double fallback) =>
            minimum <= maximum ? Math.Clamp(desired, minimum, maximum) : fallback;

        var worldBoundCenterX =
            ClampAxis(camera.CenterXMeters, margins.X, NavWorld.TilesX - margins.X, NavWorld.TilesX / 2.0);
        // Der Fokus bleibt in der mittleren Bildschirmhaelfte. An extremen
        // Raendern ist das der kleinste ehrliche Kompromiss: Die gesamte
        // ferne Frustumecke und ein zentrierter Fokus sind geometrisch nicht
        // gleichzeitig moeglich, ohne unter den vertraglichen Mindestzoom zu
        // fallen.
        var centralFocusHalfWidth = margins.LookPlaneX * 0.5;
        var focusVisibleCenterX = Math.Clamp(
            worldBoundCenterX,
            camera.CenterXMeters - centralFocusHalfWidth,
            camera.CenterXMeters + centralFocusHalfWidth);

        return new ActiveCamera(
            focusVisibleCenterX,
            ClampAxis(camera.CenterZMeters, margins.NorthZ, NavWorld.TilesY - margins.SouthZ, NavWorld.TilesY / 2.0),
            camera.DistanceMeters,
            camera.PitchRadians);
    }

    public const double NearPlane = 0.5;

    public const double FarPlane = 500.0;

    /// <summary>
    /// Auge-Punkt der Kamera fuer einen Kamerazustand. Die Kamera lebt im
    /// Render-Raum der Szene (T-020/T-023-Praezedenz: Landschafts-, Einheiten-
    /// und Marker-Meshes sind um den Ursprung zentriert); Simulationsmeter
    /// werden ueber <see cref="RepresentativeLandscape.ToWorldX"/> und
    /// <see cref="RepresentativeLandscape.ToWorldZ"/> konvertiert. Die
    /// Terrainhoehe wird am konvertierten Punkt gesampelt; ein Sampling mit
    /// Sim-Koordinaten wuerde ausserhalb des Kachelrasters lesen.
    /// </summary>
    public static (double X, double Y, double Z) EyePosition(GrayboxCamera camera) =>
        EyePosition(ActiveCamera.From(camera));

    /// <summary>Auge-Punkt fuer einen beliebigen aktiven Kamerazustand (T-033).</summary>
    public static (double X, double Y, double Z) EyePosition(ActiveCamera camera)
    {
        var worldX = RepresentativeLandscape.ToWorldX(camera.CenterXMeters);
        var worldZ = RepresentativeLandscape.ToWorldZ(camera.CenterZMeters);
        var groundY = RepresentativeLandscape.HeightAt(worldX, worldZ);
        return (
            worldX,
            groundY + (Math.Sin(camera.PitchRadians) * camera.DistanceMeters),
            worldZ + (Math.Cos(camera.PitchRadians) * camera.DistanceMeters));
    }

    /// <summary>Blickziel (Mittelpunkt am Boden) im Render-Raum der Szene.</summary>
    public static (double X, double Y, double Z) CenterPosition(GrayboxCamera camera) =>
        CenterPosition(ActiveCamera.From(camera));

    /// <summary>Blickziel fuer einen beliebigen aktiven Kamerazustand (T-033).</summary>
    public static (double X, double Y, double Z) CenterPosition(ActiveCamera camera)
    {
        var worldX = RepresentativeLandscape.ToWorldX(camera.CenterXMeters);
        var worldZ = RepresentativeLandscape.ToWorldZ(camera.CenterZMeters);
        return (worldX, RepresentativeLandscape.HeightAt(worldX, worldZ), worldZ);
    }

    /// <summary>Projektion (float16) fuer das aktuelle Seitenverhaeltnis.</summary>
    public static float[] Projection(int width, int height) =>
        CameraMath.ToFloat16(CameraMath.PerspectiveFov(FieldOfViewDegrees, width / (double)height, NearPlane, FarPlane));

    /// <summary>Viewmatrix (float16) fuer einen Kamerazustand.</summary>
    public static float[] View16(GrayboxCamera camera) => View16(ActiveCamera.From(camera));

    /// <summary>Viewmatrix (float16) fuer einen beliebigen aktiven Kamerazustand (T-033).</summary>
    public static float[] View16(ActiveCamera camera)
    {
        var eye = EyePosition(camera);
        var center = CenterPosition(camera);
        return CameraMath.ToFloat16(CameraMath.LookAt(
            new CameraMath.Vec3(eye.Item1, eye.Item2, eye.Item3),
            new CameraMath.Vec3(center.Item1, center.Item2, center.Item3),
            new CameraMath.Vec3(0, 1, 0)));
    }

    public static GroundPoint? ScreenToGround(
        GrayboxCamera camera, int width, int height, double pixelX, double pixelY) =>
        ScreenToGround(ActiveCamera.From(camera), width, height, pixelX, pixelY);

    /// <summary>
    /// Projiziert eine Bildschirmposition fuer einen beliebigen aktiven
    /// Kamerazustand (T-033) auf die Bodenebene y=0 und liefert
    /// Simulationsmeter (x nach Osten, z nach Sueden). Rueckgabe null bei
    /// singulaerer Matrix oder Strahl ohne Bodenschnitt.
    /// </summary>
    public static GroundPoint? ScreenToGround(
        ActiveCamera camera, int width, int height, double pixelX, double pixelY)
    {
        var eye = EyePosition(camera);
        var center = CenterPosition(camera);
        var view = CameraMath.LookAt(
            new CameraMath.Vec3(eye.Item1, eye.Item2, eye.Item3),
            new CameraMath.Vec3(center.Item1, center.Item2, center.Item3),
            new CameraMath.Vec3(0, 1, 0));
        var projection = CameraMath.PerspectiveFov(FieldOfViewDegrees, width / (double)height, NearPlane, FarPlane);
        // Die gepinnte bgfx-Kette komponiert clip = proj*view (renderer.h:
        // float4x4_mul(m_viewProj, view, proj) mit bx-Zeilenvektor-Semantik)
        // und wendet sie im Shader als mul(u_viewProj, pos) an. Die inverse
        // Abbildung braucht daher exakt diese Kombinationsreihenfolge; die
        // vertauschte Reihenfolge entartet zu einem nahezu konstanten
        // Bodenpunkt und waere als Auswahl unklickbar.
        var inverse = Invert(Multiply(projection, view));

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

    public static double[] BillboardBasis(GrayboxCamera camera) => BillboardBasis(ActiveCamera.From(camera));

    /// <summary>
    /// Billboard-Basis (rechts/oben) des Kameraframes in der Reihenfolge
    /// [rightX, rightY, rightZ, upX, upY, upZ] fuer einen beliebigen aktiven
    /// Kamerazustand (T-033); dieselbe Orthogonalisierung wie im
    /// T-023-Partikelpfad.
    /// </summary>
    public static double[] BillboardBasis(ActiveCamera camera)
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
        // up = cross(forward, right). Das Z-Glied lautet
        // forwardX*rightY - forwardY*rightX; rightY ist hier null. Das
        // fehlende Minus spiegelte die Achse bislang in die Bodenebene und
        // stauchte kreisrunde Billboards bei 55 Grad zu schmalen Strichen.
        var upZ = -forwardY * rightX;

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
