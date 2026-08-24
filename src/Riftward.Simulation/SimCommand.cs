namespace Riftward.Simulation;

/// <summary>Befehlstypen des Baseline-Plans (keine fachlichen Spielregeln).</summary>
public enum SimCommandKind : byte
{
    /// <summary>Setzt das Zielgebiet einer Gruppe auf eine Zone der Welt.</summary>
    GroupMoveToZone = 1,
}

/// <summary>
/// Ein tickbezogener Simulationsbefehl. Kanonische Ordnung ist
/// (Tick, ScopeGroup, Kind, ZoneIndex) per Ordinalvergleich; die Welt
/// sortiert eingehende Befehle eines Ticks vor der Anwendung, sodass
/// Eingabereihenfolge nie das Ergebnis bestimmt (Vertrag Abschnitt 0c).
/// </summary>
public readonly struct SimCommand : IComparable<SimCommand>, IEquatable<SimCommand>
{
    public SimCommand(int tick, int scopeGroup, SimCommandKind kind, int zoneIndex)
    {
        Tick = tick;
        ScopeGroup = scopeGroup;
        Kind = kind;
        ZoneIndex = zoneIndex;
    }

    public int Tick { get; }

    public int ScopeGroup { get; }

    public SimCommandKind Kind { get; }

    public int ZoneIndex { get; }

    public int CompareTo(SimCommand other)
    {
        if (Tick != other.Tick)
        {
            return Tick.CompareTo(other.Tick);
        }

        if (ScopeGroup != other.ScopeGroup)
        {
            return ScopeGroup.CompareTo(other.ScopeGroup);
        }

        if (Kind != other.Kind)
        {
            return ((byte)Kind).CompareTo((byte)other.Kind);
        }

        return ZoneIndex.CompareTo(other.ZoneIndex);
    }

    public bool Equals(SimCommand other) =>
        Tick == other.Tick
        && ScopeGroup == other.ScopeGroup
        && Kind == other.Kind
        && ZoneIndex == other.ZoneIndex;

    public override bool Equals(object? obj) => obj is SimCommand other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Tick, ScopeGroup, Kind, ZoneIndex);

    public static bool operator ==(SimCommand left, SimCommand right) => left.Equals(right);

    public static bool operator !=(SimCommand left, SimCommand right) => !left.Equals(right);

    public static bool operator <(SimCommand left, SimCommand right) => left.CompareTo(right) < 0;

    public static bool operator <=(SimCommand left, SimCommand right) => left.CompareTo(right) <= 0;

    public static bool operator >(SimCommand left, SimCommand right) => left.CompareTo(right) > 0;

    public static bool operator >=(SimCommand left, SimCommand right) => left.CompareTo(right) >= 0;
}
