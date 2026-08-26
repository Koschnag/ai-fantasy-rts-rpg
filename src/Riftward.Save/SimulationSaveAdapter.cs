using System.Runtime.CompilerServices;
using Riftward.Simulation;

namespace Riftward.Save;

/// <summary>
/// Zustandszugriff des Savekerns auf den unveränderten Simulationskern
/// (Savevertrag Abschnitt 9): Kompilierungszeit-Accessoren
/// (<see cref="UnsafeAccessorAttribute"/>) auf die privaten SoA-Felder von
/// <see cref="SimWorld"/>. Damit bleibt Riftward.Simulation byteidentisch
/// unverändert und seine öffentliche Fläche unberührt; der Ladepfad bleibt
/// reflectionsfrei, ohne dynamische Codegenerierung und BCL-only.
///
/// Fail-closed: Nach jeder Wiederherstellung prüft der Aufrufer den
/// Zustandshash der rekonstruierten Welt gegen den Kopfanker des Saves; jede
/// Bindungsabweichung wird damit kontrolliert abgewiesen. Die Feldnamen hier
/// werden von einem Architekturtest gegen die Simulationsquellen gebunden.
///
/// Bewusst nicht Teil des Relevantzustands (Simulationsvertrag V1): die
/// transienten Sucharbeitsplätze samt Serialzähler sowie diagnostische
/// Erweiterungszähler; ein frischer Prozess beginnt mit nullierten
/// Stempeln, was vertraglich äquivalent ist.
/// </summary>
public static class SimulationSaveAdapter
{
    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_tickIndex")]
    private static extern ref long TickIndexRef(SimWorld world);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_targetZoneByGroup")]
    private static extern ref int[] TargetZoneByGroup(SimWorld world);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_posX")]
    private static extern ref long[] PosX(SimWorld world);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_posY")]
    private static extern ref long[] PosY(SimWorld world);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_velX")]
    private static extern ref long[] VelX(SimWorld world);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_velY")]
    private static extern ref long[] VelY(SimWorld world);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_goalTile")]
    private static extern ref int[] GoalTile(SimWorld world);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_group")]
    private static extern ref byte[] Group(SimWorld world);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_pathState")]
    private static extern ref byte[] PathState(SimWorld world);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_plannedZone")]
    private static extern ref short[] PlannedZone(SimWorld world);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_waypointCursor")]
    private static extern ref int[] WaypointCursor(SimWorld world);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_waypointCount")]
    private static extern ref int[] WaypointCount(SimWorld world);

    [UnsafeAccessor(UnsafeAccessorKind.Field, Name = "_waypointTile")]
    private static extern ref int[] WaypointTile(SimWorld world);

    /// <summary>
    /// Erfasst den vollständigen simrelevanten Relevantzustand als Kopie
    /// außerhalb des Heisspfads (sicherer Tick: nach Abschluss von
    /// <see cref="SimWorld.Tick"/>).
    /// </summary>
    public static SimSaveState Capture(SimWorld world)
    {
        var agents = world.AgentCount;
        var cursor = WaypointCursor(world);
        var count = WaypointCount(world);
        var tiles = WaypointTile(world);
        var maxWaypoints = NavWorld.MaxWaypointsPerAgent;
        var pending = new int[agents][];

        for (var agent = 0; agent < agents; agent++)
        {
            var from = cursor[agent];
            var to = count[agent];

            // Beide Grenzen müssen einzeln im Vertragsrahmen liegen. Ein
            // transientes Paar Cursor>Anzahl ist ein legitimer Zustand des
            // Kerns (Erschöpfung vor leerem Neupfad), wird gehasht und daher
            // bytegetreu übernommen; sein Schwanz ist kanonisch leer.
            if (from < 0 || to < 0 || from > maxWaypoints || to > maxWaypoints)
            {
                throw new InvalidOperationException(
                    "Wegpunktgrenzen des Simulationskerns verletzt; Snapshot verweigert.");
            }

            var length = Math.Max(0, to - from);
            var slice = new int[length];

            for (var index = from; index < from + length; index++)
            {
                slice[index - from] = tiles[(agent * maxWaypoints) + index];
            }

            pending[agent] = slice;
        }

        return new SimSaveState
        {
            TickIndex = world.TickIndex,
            Seed = world.Seed,
            TargetZoneByGroup = TargetZoneByGroup(world).ToArray(),
            PositionXQ16 = PosX(world).ToArray(),
            PositionYQ16 = PosY(world).ToArray(),
            VelocityXQ16 = VelX(world).ToArray(),
            VelocityYQ16 = VelY(world).ToArray(),
            GoalTile = GoalTile(world).ToArray(),
            Group = Group(world).ToArray(),
            PathState = PathState(world).ToArray(),
            PlannedZone = PlannedZone(world).ToArray(),
            WaypointCursor = cursor.ToArray(),
            WaypointCount = count.ToArray(),
            PendingWaypoints = pending,
        };
    }

    /// <summary>
    /// Stellt in einer frischen <see cref="SimWorld"/>-Instanz den
    /// Relevantzustand wieder her und verifiziert sie fail-closed gegen den
    /// erwarteten Zustandshash. Eine fehlgeschlagene Verifikation weist den
    /// Kandidaten kontrolliert ab; der Aufrufer aktiviert nur bei Erfolg.
    /// </summary>
    public static bool TryRestore(
        SimSaveState state,
        ulong expectedStateHash,
        out SimWorld? world,
        out string? failure)
    {
        world = null;
        failure = null;

        var agents = state.Group.Length;
        var maxWaypoints = NavWorld.MaxWaypointsPerAgent;

        if (state.PositionXQ16.Length != agents
            || state.PositionYQ16.Length != agents
            || state.VelocityXQ16.Length != agents
            || state.VelocityYQ16.Length != agents
            || state.GoalTile.Length != agents
            || state.PathState.Length != agents
            || state.PlannedZone.Length != agents
            || state.WaypointCursor.Length != agents
            || state.WaypointCount.Length != agents
            || state.PendingWaypoints.Length != agents)
        {
            failure = "Zustandsfelder haben unterschiedliche Agentenlängen.";
            return false;
        }

        var restored = new SimWorld(state.Seed);

        if (restored.AgentCount != agents)
        {
            failure = "Agentenzahl des Simulationskerns weicht vom Save ab.";
            return false;
        }

        Fill(TargetZoneByGroup(restored), state.TargetZoneByGroup, SimulationContract.GroupCount);
        Fill(PosX(restored), state.PositionXQ16, agents);
        Fill(PosY(restored), state.PositionYQ16, agents);
        Fill(VelX(restored), state.VelocityXQ16, agents);
        Fill(VelY(restored), state.VelocityYQ16, agents);
        Fill(GoalTile(restored), state.GoalTile, agents);
        Fill(Group(restored), state.Group, agents);
        Fill(PathState(restored), state.PathState, agents);
        Fill(PlannedZone(restored), state.PlannedZone, agents);
        Fill(WaypointCursor(restored), state.WaypointCursor, agents);
        Fill(WaypointCount(restored), state.WaypointCount, agents);

        var tiles = WaypointTile(restored);

        for (var agent = 0; agent < agents; agent++)
        {
            var slice = state.PendingWaypoints[agent];
            var writeCursor = agent * maxWaypoints;

            // Ausstehende Wegpunkte liegen im Kern ab Cursor; der Payload
            // speichert den Schwanz kompakt und wird hier wieder an der
            // vertraglichen Position abgelegt.
            for (var index = 0; index < slice.Length; index++)
            {
                tiles[writeCursor + state.WaypointCursor[agent] + index] = slice[index];
            }
        }

        TickIndexRef(restored) = state.TickIndex;

        if (restored.ComputeStateHash() != expectedStateHash)
        {
            failure = "Wiederhergestellte Welt widerspricht dem Kopfanker (Zustandshash).";
            return false;
        }

        world = restored;
        return true;
    }

    private static void Fill<T>(T[] target, T[] source, int expectedLength)
    {
        if (source.Length != expectedLength || target.Length != expectedLength)
        {
            throw new InvalidOperationException("Feldlängen des Relevantzustands passen nicht zum Simulationskern.");
        }

        Array.Copy(source, target, expectedLength);
    }
}
