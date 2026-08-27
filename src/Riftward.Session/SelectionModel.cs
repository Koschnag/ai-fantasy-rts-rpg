namespace Riftward.Session;

/// <summary>
/// Auswahlauswertung V0 (Kommandovertrag Abschnitt 3): Punktwahl selektiert
/// die Gruppe des naechstgelegenen Agenten im Radius, Rahmenauswahl die
/// Vereinigung der Gruppen aller Agenten im kanonisierten Rechteck, Klick ins
/// Leere hebt die Auswahl auf.
///
/// Die Gruppenzugehoerigkeit der Agenten ist ein zeitinvarianter Anteil des
/// Startzustands; sie wird einmal je Sitzung aus einem schreibgeschuetzten
/// Snapshot des unveränderten Kerns gelesen und hier zwischengehalten. Die
/// Auswahl selbst ist rein darstellseitig: Sie ist niemals Teil von
/// Simulationszustand oder Hash und begrenzt auf die fuenf Vertragsgruppen.
/// </summary>
public sealed class SelectionModel
{
    private readonly byte[] _agentGroups;
    private readonly bool[] _groups = new bool[Riftward.Simulation.SimulationContract.GroupCount];

    /// <summary>Erzeugt das Modell mit den zeitinvarianten Agentengruppen aus dem Kernsnapshot.</summary>
    public SelectionModel(ReadOnlySpan<byte> agentGroups)
    {
        _agentGroups = agentGroups.ToArray();
    }

    /// <summary>Anzahl aktuell ausgewaehlter Gruppen.</summary>
    public int SelectedCount { get; private set; }

    /// <summary>Ist die Gruppe ausgewaehlt?</summary>
    public bool IsSelected(int group) => _groups[group];

    /// <summary>Belegung als Kopie fuer Reports und Tests.</summary>
    public bool[] Snapshot() => (bool[])_groups.Clone();

    public void Clear()
    {
        Array.Clear(_groups);
        SelectedCount = 0;
    }

    /// <summary>
    /// Punktwahl: naechstgelegener Agent innerhalb des Vertragsradius; ohne
    /// Treffer wird die Auswahl gehoben (Klick ins Leere). Rueckgabe true bei
    /// Agenttreffer, false bei Leer-Klick.
    /// </summary>
    public bool EvaluatePoint(Riftward.Simulation.SimWorld world, long pointXQ16, long pointYQ16)
    {
        Clear();

        var radiusQ16 = GrayboxIntent.MillimetersToQ16(SessionContract.SelectRadiusMillimeters);
        var radiusSquared = radiusQ16 * radiusQ16;
        var nearestAgent = -1;
        var nearestDistanceSquared = long.MaxValue;

        for (var agent = 0; agent < world.AgentCount; agent++)
        {
            var deltaX = world.PositionXOf(agent) - pointXQ16;
            var deltaY = world.PositionYOf(agent) - pointYQ16;
            var distanceSquared = (deltaX * deltaX) + (deltaY * deltaY);

            if (distanceSquared <= radiusSquared && distanceSquared < nearestDistanceSquared)
            {
                nearestDistanceSquared = distanceSquared;
                nearestAgent = agent;
            }
        }

        if (nearestAgent < 0)
        {
            return false;
        }

        SelectGroupOnly(_agentGroups[nearestAgent]);
        return true;
    }

    /// <summary>
    /// Rahmenwahl: Vereinigung der Gruppen aller Agenten, deren Position im
    /// kanonisierten (bereits min/max-geordneten) Rechteck liegt. Ein leeres
    /// Ergebnis hebt die Auswahl.
    /// </summary>
    public void EvaluateBox(Riftward.Simulation.SimWorld world, long x0Q16, long y0Q16, long x1Q16, long y1Q16)
    {
        Clear();

        for (var agent = 0; agent < world.AgentCount; agent++)
        {
            var positionX = world.PositionXOf(agent);

            if (positionX < x0Q16 || positionX > x1Q16)
            {
                continue;
            }

            var positionY = world.PositionYOf(agent);

            if (positionY < y0Q16 || positionY > y1Q16)
            {
                continue;
            }

            var group = _agentGroups[agent];

            if (!_groups[group])
            {
                _groups[group] = true;
                SelectedCount++;
            }
        }
    }

    private void SelectGroupOnly(int group)
    {
        _groups[group] = true;
        SelectedCount = 1;
    }
}
