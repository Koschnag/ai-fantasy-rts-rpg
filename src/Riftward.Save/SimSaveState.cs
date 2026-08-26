namespace Riftward.Save;

/// <summary>
/// Kanonischer simrelevanter Relevantzustand eines Snapshot-Ticks
/// (Savevertrag Abschnitt 3): exakt die von
/// <c>SimWorld.ComputeStateHash()</c> gehashten Felder des
/// Simulationsvertrags V1 in fester Ordnung. Transiente Sucharbeitsplätze
/// und diagnostische Zähler gehören bewusst nicht dazu.
/// </summary>
public sealed record SimSaveState
{
    public required long TickIndex { get; init; }

    public required uint Seed { get; init; }

    public required int[] TargetZoneByGroup { get; init; }

    public required long[] PositionXQ16 { get; init; }

    public required long[] PositionYQ16 { get; init; }

    public required long[] VelocityXQ16 { get; init; }

    public required long[] VelocityYQ16 { get; init; }

    public required int[] GoalTile { get; init; }

    public required byte[] Group { get; init; }

    public required byte[] PathState { get; init; }

    public required short[] PlannedZone { get; init; }

    public required int[] WaypointCursor { get; init; }

    public required int[] WaypointCount { get; init; }

    /// <summary>Ausstehende Wegpunkte je Agent ab Cursor (kanonisch aufsteigend).</summary>
    public required int[][] PendingWaypoints { get; init; }
}
