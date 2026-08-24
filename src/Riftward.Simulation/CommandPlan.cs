using System.Security.Cryptography;

namespace Riftward.Simulation;

/// <summary>
/// Deterministischer Gruppenbefehlsplan (Vertrag Abschnitt 0c,
/// <c>xorshift64star-group-script-v1</c>): Aus dem Szenario-Seed wird eine
/// aufsteigend nach Ticks sortierte Folge von Gruppenbewegungen abgeleitet.
/// Der Planhash bindet jede Kodierung kanonisch; der Plan enthaelt keine
/// Uhr-, Umwelt- oder Zufallsbeitraege jenseits des Seeds.
/// </summary>
public static class CommandPlan
{
    /// <summary>Tickabstand zwischen zwei Gruppenbefehlen.</summary>
    public const int IntervalTicks = 300;

    /// <summary>Erster Plan-Tick.</summary>
    public const int FirstCommandTick = 240;

    /// <summary>Erzeugt den vollstaendigen Plan fuer die Tick-Horizonte.</summary>
    public static SimCommand[] Generate(uint seed, int totalTicks)
    {
        if (totalTicks < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(totalTicks), "Planhorizont benoetigt mindestens einen Tick.");
        }

        var random = new SimRandom(seed);
        Span<int> currentZone = stackalloc int[SimulationContract.GroupCount];

        // Startzonen wie in SimWorld.ScatterAgents: gerade Agenten Westen,
        // ungerade Osten; der erste Planbefehl zieht davon weg.
        currentZone[0] = 0;
        currentZone[1] = 1;
        currentZone[2] = 0;
        currentZone[3] = 1;
        currentZone[4] = 0;

        var commands = new List<SimCommand>(capacity: (totalTicks / IntervalTicks) + 2);

        for (var tick = FirstCommandTick; tick < totalTicks; tick += IntervalTicks)
        {
            for (var group = 0; group < SimulationContract.GroupCount; group++)
            {
                // Neues Ziel ungleich dem aktuellen, deterministisch gezogen.
                int candidate;

                do
                {
                    candidate = random.NextInt(NavWorld.ZoneCount);
                }
                while (candidate == currentZone[group]);

                currentZone[group] = candidate;
                commands.Add(new SimCommand(tick, group, SimCommandKind.GroupMoveToZone, candidate));
            }
        }

        var ordered = commands.ToArray();
        Array.Sort(ordered);
        return ordered;
    }

    /// <summary>Kanonische Kodierung eines Befehls fuer den Planhash.</summary>
    public static void Encode(SimCommand command, Span<byte> target)
    {
        WriteInt32(target, 0, command.Tick);
        target[4] = (byte)command.ScopeGroup;
        target[5] = (byte)command.Kind;
        WriteInt32(target, 6, command.ZoneIndex);
    }

    public const int EncodedSize = 10;

    /// <summary>
    /// FNV-1a-64-Planhash ueber alle kanonisch kodierten Befehle
    /// (Little-Endian-Kodierung ohne Fuellbytes).
    /// </summary>
    public static ulong Hash(ReadOnlySpan<SimCommand> commands)
    {
        const ulong fnvPrime = 0x100000001B3UL;
        var hash = 0xCBF29CE484222325UL;
        Span<byte> buffer = stackalloc byte[EncodedSize];

        foreach (var command in commands)
        {
            Encode(command, buffer);

            foreach (var value in buffer)
            {
                hash = (hash ^ value) * fnvPrime;
            }
        }

        return hash;
    }

    private static void WriteInt32(Span<byte> target, int offset, int value)
    {
        target[offset] = (byte)value;
        target[offset + 1] = (byte)(value >> 8);
        target[offset + 2] = (byte)(value >> 16);
        target[offset + 3] = (byte)(value >> 24);
    }
}
