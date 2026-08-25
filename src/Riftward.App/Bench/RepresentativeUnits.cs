using Riftward.Simulation;

namespace Riftward.App.Bench;

/// <summary>
/// Schreibgeschuetzte Ansicht auf die 250 vollstaendig simulierten Agenten
/// fuer den Belastungsframe (T-023): Die Darstellung liest ausschliesslich
/// die oeffentlichen, nicht mutierenden Zugriffe des Simulationskerns
/// (ARCHITEKTUR-Laufzeitvertrag); sie veraendert den Weltzustand nie.
/// Blickrichtungen folgen der Positionsveraenderung des letzten Ticks;
/// alle Arbeitsfelder entstehen im Konstruktor (keine Hotpath-Allokation).
/// </summary>
public sealed class SimulatedAgentView
{
    private readonly double[] _previousX;
    private readonly double[] _previousZ;
    private readonly double[] _yaw;

    public SimulatedAgentView(SimWorld world)
    {
        var count = world.AgentCount;
        _previousX = new double[count];
        _previousZ = new double[count];
        _yaw = new double[count];

        for (var agent = 0; agent < count; agent++)
        {
            _previousX[agent] = world.PositionXOf(agent);
            _previousZ[agent] = world.PositionYOf(agent);
            _yaw[agent] = 0.0;
        }
    }

    /// <summary>
    /// Aktualisiert die Blickrichtungen aus der letzten Tickbewegung und
    /// schreibt die Instanzslots der simulierten Agenten. Rueckgabe: Anzahl.
    /// </summary>
    public int WriteInstances(float[] target, SimWorld world, long tickIndex)
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
                target,
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

        return world.AgentCount;
    }
}

/// <summary>
/// Deterministische Instanzfuellung des Belastungsframes (T-023):
/// Hintergrundakteure ohne Simulation und Partikel mit geschlossen
/// formuliertem Lebenszyklus. Keine Allokation im Aufrufpfad, keine
/// Uhr-, Netz- oder Zufallsbeitraege jenseits der Seeds.
/// </summary>
public static class RepresentativeActors
{
    /// <summary>Partikellebenszyklus in Ticks (eine volle Welle).</summary>
    public const int ParticleCycleTicks = 300;

    /// <summary>
    /// Fuellt die Slots der Hintergrundakteure (ohne Simulation).
    /// </summary>
    public static void WriteBackgroundInstances(
        float[] target,
        int firstSlot,
        long tickIndex,
        uint seed)
    {
        var backgroundSeed = (uint)((seed ^ 0xDEECE66Du) & 0xFFFFFFFFu);

        for (var actor = 0; actor < RepresentativeScenario.BackgroundActors; actor++)
        {
            var slot = firstSlot + actor;
            var zone = RepresentativeLandscape.LightZones[actor % RepresentativeLandscape.LightZones.Count];
            var (centerX, centerZ) = RepresentativeLandscape.ZoneCenterWorld(zone);

            var radiusX = 2.4 + (4.2 * Hash01(backgroundSeed, actor, 11));
            var radiusZ = 2.4 + (4.2 * Hash01(backgroundSeed, actor, 23));
            var angularSpeed = (0.35 + (0.5 * Hash01(backgroundSeed, actor, 37))) / 20.0;
            var direction = (actor & 1) == 0 ? 1.0 : -1.0;
            var phase0 = Hash01(backgroundSeed, actor, 53) * 2.0 * Math.PI;

            var angle = phase0 + (direction * angularSpeed * tickIndex);
            var worldX = centerX + (radiusX * Math.Cos(angle));
            var worldZ = centerZ + (radiusZ * Math.Sin(angle));
            var groundY = RepresentativeLandscape.HeightAt(worldX, worldZ);

            // Tangentenrichtung der Ellipsenbahn als Blickrichtung.
            var tangentX = -radiusX * Math.Sin(angle) * direction;
            var tangentZ = radiusZ * Math.Cos(angle) * direction;
            var yaw = Math.Atan2(tangentZ, tangentX);

            var walkPhase = (tickIndex * RepresentativeRig.WalkPhasePerTick * 1.05) + (actor * 0.61);
            var scale = 0.86f + (0.28f * (float)Hash01(backgroundSeed, actor, 71));

            RepresentativeMesh.WriteUnitInstance(
                target,
                slot,
                worldX,
                groundY,
                worldZ,
                yaw,
                walkPhase,
                scale,
                paletteRow: slot,
                pathState: 0);
        }
    }

    /// <summary>
    /// Fuellt die Partikelinstanz und liefert die gleichzeitige Partikelzahl
    /// (Peak: alle 5000, weil die Welle zyklisch ueber den Horizont verteilt
    /// ist und damit waehrend des Messfensters konstant am Budgetpeak faehrt).
    /// </summary>
    public static int WriteParticleInstances(
        float[] target,
        long tickIndex,
        uint seed)
    {
        Span<double> groundByEmitter = stackalloc double[RepresentativeLandscape.LocalLightCount];
        Span<(double X, double Z)> centers = stackalloc (double, double)[RepresentativeLandscape.LocalLightCount];

        for (var emitter = 0; emitter < RepresentativeLandscape.LocalLightCount; emitter++)
        {
            centers[emitter] = RepresentativeLandscape.ZoneCenterWorld(RepresentativeLandscape.LightZones[emitter]);
            groundByEmitter[emitter] = RepresentativeLandscape.HeightAt(centers[emitter].Item1, centers[emitter].Item2);
        }

        var total = RepresentativeScenario.ParticlePeakTarget;
        var stagger = ParticleCycleTicks / (double)total;

        for (var particle = 0; particle < total; particle++)
        {
            var emitter = particle % RepresentativeLandscape.LocalLightCount;
            var ageTicks = (tickIndex + (particle * stagger)) % ParticleCycleTicks;
            var ageNorm = ageTicks / (double)ParticleCycleTicks;

            var phi = Hash01(seed, particle, 13) * 2.0 * Math.PI;
            var swirlRadius = 0.35 + (2.4 * ageNorm);
            var swirlAngle = phi + (ageNorm * 4.0 * Math.PI);

            var worldX = centers[emitter].Item1 + (swirlRadius * Math.Cos(swirlAngle));
            var worldZ = centers[emitter].Item2 + (swirlRadius * Math.Sin(swirlAngle));
            var worldY = groundByEmitter[emitter] + 0.25
                + (3.6 * ageNorm)
                + (0.15 * Math.Sin((phi * 7.0) + (ageNorm * 9.0)));

            var alpha = Math.Pow(Math.Sin(Math.PI * ageNorm), 0.75);
            var size = 0.16f + (0.55f * (float)ageNorm);

            ReadOnlySpan<float> tint = EmitterTint(emitter);

            RepresentativeMesh.WriteParticleInstance(
                target,
                particle,
                worldX,
                worldY,
                worldZ,
                size,
                rotation: (float)(phi + (ageNorm * Math.PI)),
                tint[0],
                tint[1],
                tint[2],
                (float)alpha);
        }

        return total;
    }

    /* Feste, deterministische Emittertoene; als statische Felder gehalten,
     * damit der Instanzpfad je Partikel keine Arrayallokation erzeugt. */
    private static readonly float[] Tint0 = [0.85f, 0.78f, 0.62f];
    private static readonly float[] Tint1 = [0.62f, 0.72f, 0.80f];
    private static readonly float[] Tint2 = [0.80f, 0.66f, 0.58f];
    private static readonly float[] Tint3 = [0.68f, 0.82f, 0.64f];

    private static ReadOnlySpan<float> EmitterTint(int emitter) =>
        emitter switch
        {
            0 => Tint0,
            1 => Tint1,
            2 => Tint2,
            _ => Tint3,
        };

    /// <summary>Deterministischer Doppelwort-Hash als [0,1)-Verteilung.</summary>
    public static double Hash01(uint seedA, int seedB, int salt)
    {
        unchecked
        {
            ulong state = ((ulong)seedA << 32) ^ ((ulong)(uint)seedB << 8) ^ ((ulong)(uint)salt * 0x9E3779B97F4A7C15UL);
            state ^= state >> 12;
            state ^= state << 25;
            state ^= state >> 27;
            state *= 0x2545F4914F6CDD1DUL;
            state ^= state >> 33;
            return (state >> 40) / (double)(1UL << 24);
        }
    }
}
