namespace Riftward.Simulation;

/// <summary>Pfadsuchphasen eines Agenten (kanonisch gehashte Persistenzfelder).</summary>
public enum SimAgentPathState : byte
{
    /// <summary>Wartet ohne aktuelles Ziel oder steht bereits am Ziel.</summary>
    Idle = 0,

    /// <summary>Pfadsuche laeuft oder wartet auf ein Dienstfenster.</summary>
    Seeking = 1,

    /// <summary>Ein Pfad liegt vor und wird abgelaufen.</summary>
    Following = 2,

    /// <summary>Ziel ist unerreichbar; Agent verharrt bis zum naechsten Befehl.</summary>
    Unreachable = 3,
}

/// <summary>
/// Lesbarer Snapshot des Simulationszustands (Entkopplung von Darstellung,
/// ARCHITEKTUR.md). Kopie ausserhalb des Heisspfads erzeugt; Aenderungen am
/// Snapshot wirken sich nie auf die Simulation aus.
/// </summary>
public sealed class SimSnapshot
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

    public int AgentCount => Group.Length;
}

/// <summary>
/// Deterministischer headless Simulationskern der Baseline T-021: fester
/// 20-Hz-Tick, genau <see cref="SimulationContract.AgentCount"/> gleichzeitig
/// vollstaendig simulierte mobile Testagenten mit Fortbewegung, Ausweich-
/// verhalten und Gruppenbefehlen auf einer synthetischen Navigationswelt,
/// hierarchisch budgetierte Pfadsuche und kanonischer Zustands-Hash
/// (<c>fnv1a64-canonical-chain-v1</code>) ueber den sim-relevanten Zustand.
///
/// Kanonische Ordnung (Vertrag Abschnitt 0c): Befehle werden vor der
/// Anwendung sortiert; Agenten, Kacheln, Bloecke und Buckets werden
/// ausschliesslich in aufsteigender Indexreihenfolge iteriert; es gibt keine
/// Hashtabellen-, Thread-, Dateisystem- oder Uhrabhaengigkeit. Reine
/// Ganzzahlarithmetik Q16.16 ohne Fließkommaoperationen.
///
/// Pfadhaushalt (Vertrag Abschnitt 0d): je Tick werden Anfragen in
/// aufsteigender Agentenreihenfolge bedient; je Agent und Abschnitt sind
/// <see cref="SimulationContract.PathExpansionBudgetPerAgentTick"/>
/// Knotenerweiterungen moeglich, global je Tick
/// <see cref="SimulationContract.PathGlobalExpansionBudgetPerTick"/>. Eine
/// nicht abgeschlossene Feinsuche liefert ihren besten erreichten Teilpfad
/// ("best effort"); da der Agent dadurch fortbewegt wird, terminieren auch
/// sehr lange Routen deterministisch ueber mehrere Anfragen. Unerreichbare
/// Ziele erkennt die Grobsuche vollstaendig innerhalb eines Abschnitts
/// (maximal <see cref="NavWorld.BlockCount"/> Bloecke) und meldet sie als
/// <see cref="SimAgentPathState.Unreachable"/>.
///
/// Allokationsfreiheit (Vertrag Abschnitt 0e): alle Heisspfadstrukturen
/// entstehen im Konstruktor; der Tick-Pfad fuehrt keine verwaltete
/// Allokation durch. Nur die hier dokumentierten Persistenzfelder gehoeren
/// zum gehashten Relevantzustand; transiente Sucharbeitsplaetze werden je
/// Abschnitt neu gestempelt und hinterlassen keinen Zustand.
/// </summary>
public sealed class SimWorld
{
    /// <summary>Sollgeschwindigkeit gerader Agenten: 1,40 m/s bei 20 Hz (ganzzahlig: floor(0,07 m * 65536)).</summary>
    public const long SpeedEvenPerTickQ16 = 4587;

    /// <summary>Sollgeschwindigkeit ungerader Agenten: 1,25 m/s bei 20 Hz (exakt 0,0625 m * 65536).</summary>
    public const long SpeedOddPerTickQ16 = 4096;

    /// <summary>Ausweichradius in Q16 (0,9 m).</summary>
    public const long SeparationRadiusQ16 = 58982;

    /// <summary>Wegpunkterreichungsradius in Q16 (0,35 m).</summary>
    public const long WaypointRadiusQ16 = 22938;

    /// <summary>Kantenlaenge eines Nachbarschaftsbuckets in Kacheln (2 m).</summary>
    public const int BucketTileSpan = 2;

    public const int BucketsX = (NavWorld.TilesX + BucketTileSpan - 1) / BucketTileSpan;
    public const int BucketsY = (NavWorld.TilesY + BucketTileSpan - 1) / BucketTileSpan;
    public const int BucketCount = BucketsX * BucketsY;

    private const int OpenListPerAgent = 2048;
    private const int CoarseOpenPerAgent = NavWorld.BlockCount + 8;
    private const int CommandBufferCapacity = 32;

    private readonly uint _seed;

    // Persistente Agentenzustaende (SoA; kanonisch gehasht).
    private readonly long[] _posX;
    private readonly long[] _posY;
    private readonly long[] _velX;
    private readonly long[] _velY;
    private readonly int[] _goalTile;
    private readonly byte[] _group;
    private readonly byte[] _pathState;
    private readonly short[] _plannedZone;
    private readonly int[] _waypointTile;
    private readonly int[] _waypointCount;
    private readonly int[] _waypointCursor;

    // Gruppenziele.
    private readonly int[] _targetZoneByGroup;

    // Transiente Sucharbeitsplaetze (nicht gehasht, je Serial neu gestempelt).
    private readonly int[] _fineOpen;
    private readonly int[] _coarseOpen;
    private readonly int[] _fineParent;
    private readonly int[] _fineStamp;
    private readonly int[] _corridorStamp;
    private readonly int[] _coarseParent;
    private readonly int[] _coarseStamp;
    private readonly int[] _reverseScratch;
    private int _serial;

    // Nachbarschaftsbuckets (Zaehlsortierung, allokationsfrei).
    private readonly int[] _bucketCounts;
    private readonly int[] _bucketStarts;
    private readonly int[] _bucketFill;
    private readonly int[] _bucketItems;

    // Befehlspuffer des aktuellen Ticks.
    private readonly SimCommand[] _commandBuffer;

    private long _tickIndex;
    private long _totalExpansions;
    private int _lastTickExpansions;

    public SimWorld(uint seed)
    {
        _seed = seed;

        var agents = SimulationContract.AgentCount;
        _posX = new long[agents];
        _posY = new long[agents];
        _velX = new long[agents];
        _velY = new long[agents];
        _goalTile = new int[agents];
        _group = new byte[agents];
        _pathState = new byte[agents];
        _plannedZone = new short[agents];
        _waypointTile = new int[agents * NavWorld.MaxWaypointsPerAgent];
        _waypointCount = new int[agents];
        _waypointCursor = new int[agents];

        _targetZoneByGroup = new int[SimulationContract.GroupCount];

        _fineOpen = new int[OpenListPerAgent];
        _coarseOpen = new int[CoarseOpenPerAgent];
        _fineParent = new int[NavWorld.TileCount];
        _fineStamp = new int[NavWorld.TileCount];
        _corridorStamp = new int[NavWorld.TileCount];
        _coarseParent = new int[NavWorld.BlockCount];
        _coarseStamp = new int[NavWorld.BlockCount];
        _reverseScratch = new int[NavWorld.TileCount];

        _bucketCounts = new int[BucketCount];
        _bucketStarts = new int[BucketCount];
        _bucketFill = new int[BucketCount];
        _bucketItems = new int[agents];

        _commandBuffer = new SimCommand[CommandBufferCapacity];

        ScatterAgents(seed);
    }


    public uint Seed => _seed;

    public int AgentCount => _group.Length;

    public long TickIndex => _tickIndex;

    public long TotalNodeExpansions => _totalExpansions;

    /// <summary>Globale Knotenerweiterungen des letzten Ticks (Pfadhaushaltsnachweis).</summary>
    public int LastTickNodeExpansions => _lastTickExpansions;

    public int TargetZoneOfGroup(int group) => _targetZoneByGroup[group];

    public SimAgentPathState PathStateOf(int agent) => (SimAgentPathState)_pathState[agent];

    public long PositionXOf(int agent) => _posX[agent];

    public long PositionYOf(int agent) => _posY[agent];

    private void ScatterAgents(uint seed)
    {
        var random = new SimRandom(seed);
        var occupied = new bool[NavWorld.TileCount];

        for (var agent = 0; agent < AgentCount; agent++)
        {
            var group = agent % SimulationContract.GroupCount;
            _group[agent] = (byte)group;

            // Gerade Agenten starten im Westen, ungerade im Osten; die
            // Gruppen verteilen sich damit ueber beide Startzonen und der
            // Plan erzeugt Kreuzverkehr mit konkurrierenden langen Wegen.
            var spawnZone = agent % 2 == 0 ? 0 : 1;
            var tile = NavWorld.RandomFreeTileInZone(spawnZone, ref random, candidate => occupied[candidate]);

            occupied[tile] = true;
            var tileX = tile % NavWorld.TilesX;
            var tileY = tile / NavWorld.TilesX;
            _posX[agent] = (tileX * NavWorld.TileSizeQ16) + (NavWorld.TileSizeQ16 >> 1);
            _posY[agent] = (tileY * NavWorld.TileSizeQ16) + (NavWorld.TileSizeQ16 >> 1);
            _plannedZone[agent] = (short)spawnZone;
            _pathState[agent] = (byte)SimAgentPathState.Idle;
        }
    }

    /// <summary>
    /// Nimmt Befehle fuer den aktuellen Tick entgegen und sortiert sie vor
    /// der Anwendung kanonisch; die Eingabereihenfolge bestimmt nie das
    /// Ergebnis (Negativtests nutzen diese Garantie).
    /// </summary>
    public void ApplyCommands(ReadOnlySpan<SimCommand> commands)
    {
        if (commands.Length > CommandBufferCapacity)
        {
            throw new ArgumentException("Mehr Befehle je Tick als vertraglich vorgesehen.", nameof(commands));
        }

        for (var index = 0; index < commands.Length; index++)
        {
            _commandBuffer[index] = commands[index];
        }

        for (var index = 1; index < commands.Length; index++)
        {
            var key = _commandBuffer[index];
            var swap = index - 1;

            while (swap >= 0 && _commandBuffer[swap].CompareTo(key) > 0)
            {
                _commandBuffer[swap + 1] = _commandBuffer[swap];
                swap--;
            }

            _commandBuffer[swap + 1] = key;
        }

        for (var index = 0; index < commands.Length; index++)
        {
            Apply(_commandBuffer[index]);
        }
    }

    private void Apply(SimCommand command)
    {
        switch (command.Kind)
        {
            case SimCommandKind.GroupMoveToZone:
                ValidateGroup(command.ScopeGroup);

                if (command.ZoneIndex < 0 || command.ZoneIndex >= NavWorld.ZoneCount)
                {
                    throw new ArgumentOutOfRangeException(nameof(command), "Zonenindex ausserhalb der Welt.");
                }

                _targetZoneByGroup[command.ScopeGroup] = command.ZoneIndex;
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(command), "Unbekannter Befehlstyp.");
        }
    }

    private static void ValidateGroup(int group)
    {
        if (group < 0 || group >= SimulationContract.GroupCount)
        {
            throw new ArgumentOutOfRangeException(nameof(group), "Gruppe ausserhalb des Vertragsbereichs.");
        }
    }

    /// <summary>Fuehrt genau einen festen Simulationstick (dt = 50 ms) aus.</summary>
    public void Tick()
    {
        _lastTickExpansions = 0;
        RebuildBuckets();

        var globalBudget = SimulationContract.PathGlobalExpansionBudgetPerTick;

        for (var agent = 0; agent < AgentCount; agent++)
        {
            UpdateAgent(agent, ref globalBudget);
        }

        _tickIndex++;
    }

    private void UpdateAgent(int agent, ref int globalBudget)
    {
        var state = _pathState[agent];
        var targetZone = _targetZoneByGroup[_group[agent]];

        // Neue Anfrage bei Zielwechsel oder nach unerreichbarem Ziel.
        if ((state == (byte)SimAgentPathState.Idle || state == (byte)SimAgentPathState.Unreachable)
            && _plannedZone[agent] != targetZone)
        {
            BeginRequest(agent, targetZone);
            state = (byte)SimAgentPathState.Seeking;
        }

        switch (state)
        {
            case (byte)SimAgentPathState.Seeking:
                ServiceRequest(agent, ref globalBudget);
                break;

            case (byte)SimAgentPathState.Following:
                AdvanceAlongPath(agent);
                break;

            default:
                break;
        }

        MoveWithSeparation(agent);
        CheckArrival(agent);
    }

    private void BeginRequest(int agent, int targetZone)
    {
        _plannedZone[agent] = (short)targetZone;
        _goalTile[agent] = ZoneCenterTile(targetZone);
        _pathState[agent] = (byte)SimAgentPathState.Seeking;
    }

    private static int ZoneCenterTile(int zone)
    {
        var centerX = NavWorld.ZoneCenterXQ16(zone);
        var centerY = NavWorld.ZoneCenterYQ16(zone);
        return (NavWorld.TileIndexOfPosition(centerY) * NavWorld.TilesX) + NavWorld.TileIndexOfPosition(centerX);
    }

    /// <summary>
    /// Bedient die offene Anfrage eines Agenten mit einem begrenzten
    /// Erweiterungshaushalt. Die Grobsuche muss innerhalb ihres Abschnitts
    /// abschliessen (oder meldet unerreichbar); die Feinsuche liefert bei
    /// Erschoepfung des Haushalts ihren besten erreichten Teilpfad.
    /// </summary>
    private void ServiceRequest(int agent, ref int globalBudget)
    {
        var budget = (int)FixedPoint.Min(SimulationContract.PathExpansionBudgetPerAgentTick, globalBudget);

        if (budget <= 0)
        {
            return;
        }

        _serial++;
        var startTile = CurrentTileOf(agent);

        if (!NavWorld.IsWalkableIndex(startTile))
        {
            _pathState[agent] = (byte)SimAgentPathState.Unreachable;
            return;
        }

        var goalTile = _goalTile[agent];
        var expansionsUsed = 0;

        Array.Clear(_coarseStamp);

        var coarseOutcome = SearchCoarse(startTile, goalTile, budget, ref expansionsUsed);

        switch (coarseOutcome)
        {
            case CoarseSearchOutcome.BudgetExhausted:
                // Haushalt dieses Abschnitts vor Abschluss verbraucht; der
                // naechste Tick startet die Anfrage neu (kein Residuum).
                CommitExpansions(expansionsUsed, ref globalBudget);
                return;

            case CoarseSearchOutcome.Unreachable:
                CommitExpansions(expansionsUsed, ref globalBudget);
                _pathState[agent] = (byte)SimAgentPathState.Unreachable;
                return;
        }

        var fineComplete = SearchFine(
            startTile,
            goalTile,
            budget - expansionsUsed,
            ref expansionsUsed,
            out var deepestTile);

        CommitExpansions(expansionsUsed, ref globalBudget);

        if (!fineComplete && deepestTile == startTile)
        {
            // Kein Fortschritt in diesem Abschnitt; naechster Tick versucht
            // es erneut (kein residueller Zustand, neue Serial).
            return;
        }

        BuildWaypoints(agent, deepestTile);

        if (_waypointCount[agent] == 0)
        {
            return;
        }

        _waypointCursor[agent] = 0;
        _pathState[agent] = (byte)SimAgentPathState.Following;
    }

    private void CommitExpansions(int expansionsUsed, ref int globalBudget)
    {
        _totalExpansions += expansionsUsed;
        _lastTickExpansions += expansionsUsed;
        globalBudget -= expansionsUsed;
    }

    private int CurrentTileOf(int agent) =>
        (NavWorld.TileIndexOfPosition(_posY[agent]) * NavWorld.TilesX) + NavWorld.TileIndexOfPosition(_posX[agent]);

    /// <summary>Ausgang einer Grobsuche im Dienstabschnitt.</summary>
    private enum CoarseSearchOutcome : byte
    {
        /// <summary>Zielblock erreicht; Feinkorridor kann gestempelt werden.</summary>
        ReachedGoal = 0,

        /// <summary>Offene Liste vollstaendig erschöpft: Ziel unerreichbar.</summary>
        Unreachable = 1,

        /// <summary>Haushalt vor Abschluss verbraucht; Anfrage wird neu gestartet.</summary>
        BudgetExhausted = 2,
    }

    /// <summary>Breitensuche auf dem Blockgraphen (obere Hierarchieebene).</summary>
    private CoarseSearchOutcome SearchCoarse(int startTile, int goalTile, int budget, ref int expansionsUsed)
    {
        var startBlock = NavWorld.BlockOfTile(startTile);
        var goalBlock = NavWorld.BlockOfTile(goalTile);

        if (startBlock == goalBlock)
        {
            return CoarseSearchOutcome.ReachedGoal;
        }

        var head = 0;
        var tail = 0;
        _coarseOpen[tail++] = startBlock;
        _coarseStamp[startBlock] = _serial;
        _coarseParent[startBlock] = -1;

        while (head < tail)
        {
            if (expansionsUsed >= budget)
            {
                return CoarseSearchOutcome.BudgetExhausted;
            }

            var block = _coarseOpen[head++];
            expansionsUsed++;

            for (var direction = 0; direction < 4; direction++)
            {
                var neighbor = NavWorld.BlockNeighbor(block, direction);

                if (neighbor >= 0 && _coarseStamp[neighbor] != _serial)
                {
                    _coarseStamp[neighbor] = _serial;
                    _coarseParent[neighbor] = block;

                    if (neighbor == goalBlock)
                    {
                        return CoarseSearchOutcome.ReachedGoal;
                    }

                    _coarseOpen[tail++] = neighbor;
                }
            }
        }

        // Offene Liste erschöpft: Zielblock ist unerreichbar.
        return CoarseSearchOutcome.Unreachable;
    }

    /// <summary>
    /// Korridorbeschraenkte Breitensuche auf Kachelebene: nur Kacheln, die
    /// laut Korridorstempel dieses Serials auf der groben Route liegen.
    /// Liefert den tiefsten erreichten Kachelpunkt, damit bei Haushalts-
    /// erschoepfung ein Teilpfad den Agenten trotzdem fortbewegt.
    /// </summary>
    private bool SearchFine(
        int startTile,
        int goalTile,
        int budget,
        ref int expansionsUsed,
        out int deepestTile)
    {
        deepestTile = startTile;

        if (startTile == goalTile)
        {
            StampCorridorFromRoute(startTile, goalTile);
            return true;
        }

        StampCorridorFromRoute(startTile, goalTile);

        var head = 0;
        var tail = 0;
        _fineOpen[tail++] = startTile;
        _fineStamp[startTile] = _serial;
        _fineParent[startTile] = -1;

        while (head < tail && expansionsUsed < budget)
        {
            var tile = _fineOpen[head++];
            deepestTile = tile;
            expansionsUsed++;

            for (var direction = 0; direction < 4; direction++)
            {
                var neighbor = NeighborTile(tile, direction);

                if (neighbor < 0
                    || _fineStamp[neighbor] == _serial
                    || !NavWorld.IsWalkableIndex(neighbor)
                    || _corridorStamp[neighbor] != _serial)
                {
                    continue;
                }

                _fineStamp[neighbor] = _serial;
                _fineParent[neighbor] = tile;

                if (neighbor == goalTile)
                {
                    return true;
                }

                if (tail >= OpenListPerAgent)
                {
                    // Vertragliche Obergrenze des Arbeitsplatzes; deterministischer Abbruch.
                    return false;
                }

                _fineOpen[tail++] = neighbor;
            }
        }

        return false;
    }

    /// <summary>
    /// Stemplt die Kacheln der groben Route des aktuellen Serials als
    /// Feinkorridor: Elternkette vom Zielblock rueckwaarts bis zum
    /// Startblock, je Block alle betretbaren Kacheln.
    /// </summary>
    private void StampCorridorFromRoute(int startTile, int goalTile)
    {
        var startBlock = NavWorld.BlockOfTile(startTile);
        var cursor = NavWorld.BlockOfTile(goalTile);

        while (cursor >= 0 && _coarseStamp[cursor] == _serial)
        {
            StampCorridorBlock(cursor);

            if (cursor == startBlock)
            {
                break;
            }

            cursor = _coarseParent[cursor];
        }
    }

    private void StampCorridorBlock(int block)
    {
        var baseX = (block % NavWorld.BlocksX) * NavWorld.BlockSize;
        var baseY = (block / NavWorld.BlocksX) * NavWorld.BlockSize;

        for (var y = baseY; y < baseY + NavWorld.BlockSize; y++)
        {
            var row = y * NavWorld.TilesX;

            for (var x = baseX; x < baseX + NavWorld.BlockSize; x++)
            {
                if (NavWorld.IsWalkableIndex(row + x))
                {
                    _corridorStamp[row + x] = _serial;
                }
            }
        }
    }

    private static int NeighborTile(int tileIndex, int direction)
    {
        var x = tileIndex % NavWorld.TilesX;
        var y = tileIndex / NavWorld.TilesX;

        switch (direction)
        {
            case 0: return y > 0 ? tileIndex - NavWorld.TilesX : -1;
            case 1: return x < NavWorld.TilesX - 1 ? tileIndex + 1 : -1;
            case 2: return y < NavWorld.TilesY - 1 ? tileIndex + NavWorld.TilesX : -1;
            default: return x > 0 ? tileIndex - 1 : -1;
        }
    }

    /// <summary>
    /// Rekonstruiert aus der Elternkette den Weg Start..bisTile und legt bis
    /// zu <see cref="NavWorld.MaxWaypointsPerAgent"/> Wegpunkte ab Start ab.
    /// Laengere Ketten werden als Teilpfad gefuehrt; die Erneuerungsanfrage
    /// nach Puffererschoepfung nutzt die fortgeschrittene Position.
    /// </summary>
    private void BuildWaypoints(int agent, int endTile)
    {
        var depth = 0;
        var cursor = endTile;

        while (cursor >= 0 && _fineStamp[cursor] == _serial)
        {
            _reverseScratch[depth++] = cursor;
            cursor = _fineParent[cursor];
        }

        var offset = agent * NavWorld.MaxWaypointsPerAgent;
        var count = depth < NavWorld.MaxWaypointsPerAgent ? depth : NavWorld.MaxWaypointsPerAgent;

        for (var index = 0; index < count; index++)
        {
            _waypointTile[offset + index] = _reverseScratch[depth - 1 - index];
        }

        _waypointCount[agent] = count;
    }

    private void AdvanceAlongPath(int agent)
    {
        var offset = agent * NavWorld.MaxWaypointsPerAgent;
        var cursor = _waypointCursor[agent];

        while (cursor < _waypointCount[agent])
        {
            var tile = _waypointTile[offset + cursor];
            var centerX = ((tile % NavWorld.TilesX) * NavWorld.TileSizeQ16) + (NavWorld.TileSizeQ16 >> 1);
            var centerY = ((tile / NavWorld.TilesX) * NavWorld.TileSizeQ16) + (NavWorld.TileSizeQ16 >> 1);

            var distanceSquared = FixedPoint.DistanceSquared(_posX[agent], _posY[agent], centerX, centerY);

            if (distanceSquared > (ulong)(WaypointRadiusQ16 * WaypointRadiusQ16))
            {
                _waypointCursor[agent] = cursor;
                return;
            }

            cursor++;
        }

        // Puffer erschoepft: erneute Anfrage ab aktueller Position.
        _waypointCursor[agent] = cursor;
        _pathState[agent] = (byte)SimAgentPathState.Seeking;
    }

    /// <summary>
    /// Fortbewegung mit Ausweichverhalten: Sollrichtung zum aktuellen
    /// Wegpunkt (bzw. Gruppenzielzentrum ohne Pfad), ganzzahliger Abstands-
    /// druck aus dem 3x3-Bucket-Umfeld, achsenweise Kollisionsaufloesung
    /// gegen die Weltgeometrie.
    /// </summary>
    private void MoveWithSeparation(int agent)
    {
        long targetX;
        long targetY;

        if (_pathState[agent] == (byte)SimAgentPathState.Following
            && _waypointCursor[agent] < _waypointCount[agent])
        {
            var tile = _waypointTile[(agent * NavWorld.MaxWaypointsPerAgent) + _waypointCursor[agent]];
            targetX = ((tile % NavWorld.TilesX) * NavWorld.TileSizeQ16) + (NavWorld.TileSizeQ16 >> 1);
            targetY = ((tile / NavWorld.TilesX) * NavWorld.TileSizeQ16) + (NavWorld.TileSizeQ16 >> 1);
        }
        else
        {
            targetX = NavWorld.ZoneCenterXQ16(_targetZoneByGroup[_group[agent]]);
            targetY = NavWorld.ZoneCenterYQ16(_targetZoneByGroup[_group[agent]]);
        }

        var speed = (_group[agent] & 1) == 0 ? SpeedEvenPerTickQ16 : SpeedOddPerTickQ16;

        var deltaX = targetX - _posX[agent];
        var deltaY = targetY - _posY[agent];
        var distanceSquared = FixedPoint.DistanceSquared(_posX[agent], _posY[agent], targetX, targetY);
        var distance = (long)FixedPoint.ISqrt(distanceSquared);

        var stepX = 0L;
        var stepY = 0L;

        // Totzone unterhalb 1/16 m verhindert ganzzahliges Standjitter.
        if (distance > FixedPoint.One >> 4)
        {
            var step = distance < speed ? distance : speed;
            stepX = FixedPoint.Mul((deltaX << FixedPoint.FractionBits) / distance, step);
            stepY = FixedPoint.Mul((deltaY << FixedPoint.FractionBits) / distance, step);
        }

        var pushX = SeparationPush(agent, _posX[agent], _posY[agent], out var pushY);

        var candidateX = _posX[agent] + stepX + pushX;
        var candidateY = _posY[agent] + stepY + pushY;

        if (!IsFreePosition(candidateX, _posY[agent]))
        {
            candidateX = _posX[agent];
        }

        if (!IsFreePosition(candidateX, candidateY))
        {
            candidateY = _posY[agent];
        }

        var maxX = (NavWorld.TilesX * NavWorld.TileSizeQ16) - 1;
        var maxY = (NavWorld.TilesY * NavWorld.TileSizeQ16) - 1;
        candidateX = FixedPoint.Clamp(candidateX, 0L, maxX);
        candidateY = FixedPoint.Clamp(candidateY, 0L, maxY);

        _velX[agent] = candidateX - _posX[agent];
        _velY[agent] = candidateY - _posY[agent];
        _posX[agent] = candidateX;
        _posY[agent] = candidateY;
    }

    /// <summary>Gedaempfte ganzzahlige Abstossung innerhalb des Ausweichradius.</summary>
    private long SeparationPush(int agent, long positionX, long positionY, out long pushY)
    {
        long accumulatedX = 0;
        pushY = 0;

        var bucketX = (int)(positionX / (BucketTileSpan * NavWorld.TileSizeQ16));
        var bucketY = (int)(positionY / (BucketTileSpan * NavWorld.TileSizeQ16));
        var radiusSquared = (ulong)(SeparationRadiusQ16 * SeparationRadiusQ16);

        for (var offsetY = -1; offsetY <= 1; offsetY++)
        {
            var neighborY = bucketY + offsetY;

            if (neighborY < 0 || neighborY >= BucketsY)
            {
                continue;
            }

            for (var offsetX = -1; offsetX <= 1; offsetX++)
            {
                var neighborX = bucketX + offsetX;

                if (neighborX < 0 || neighborX >= BucketsX)
                {
                    continue;
                }

                var bucket = (neighborY * BucketsX) + neighborX;
                var start = _bucketStarts[bucket];
                var end = start + _bucketCounts[bucket];

                for (var item = start; item < end; item++)
                {
                    var other = _bucketItems[item];

                    if (other == agent)
                    {
                        continue;
                    }

                    var deltaX = positionX - _posX[other];
                    var deltaY = positionY - _posY[other];
                    var otherDistanceSquared = FixedPoint.DistanceSquared(positionX, positionY, _posX[other], _posY[other]);

                    if (otherDistanceSquared >= radiusSquared || otherDistanceSquared == 0)
                    {
                        continue;
                    }

                    var otherDistance = (long)FixedPoint.ISqrt(otherDistanceSquared);
                    var weight = SeparationRadiusQ16 - otherDistance;
                    accumulatedX += (deltaX * weight) / otherDistance;
                    pushY += (deltaY * weight) / otherDistance;
                }
            }
        }

        accumulatedX /= 8;
        pushY /= 8;
        return accumulatedX;
    }

    private static bool IsFreePosition(long positionX, long positionY) =>
        NavWorld.IsWalkable(
            NavWorld.TileIndexOfPosition(positionX),
            NavWorld.TileIndexOfPosition(positionY));

    private void CheckArrival(int agent)
    {
        if (_pathState[agent] != (byte)SimAgentPathState.Following)
        {
            return;
        }

        var zone = _targetZoneByGroup[_group[agent]];

        if (NavWorld.IsInsideZone(
                zone,
                NavWorld.TileIndexOfPosition(_posX[agent]),
                NavWorld.TileIndexOfPosition(_posY[agent])))
        {
            _pathState[agent] = (byte)SimAgentPathState.Idle;
            _waypointCount[agent] = 0;
            _waypointCursor[agent] = 0;
        }
    }

    private static int BucketIndexOf(long positionX, long positionY)
    {
        var bucketX = (int)(positionX / (BucketTileSpan * NavWorld.TileSizeQ16));

        if (bucketX >= BucketsX)
        {
            bucketX = BucketsX - 1;
        }

        var bucketY = (int)(positionY / (BucketTileSpan * NavWorld.TileSizeQ16));

        if (bucketY >= BucketsY)
        {
            bucketY = BucketsY - 1;
        }

        return (bucketY * BucketsX) + bucketX;
    }

    private void RebuildBuckets()
    {
        Array.Clear(_bucketCounts);

        for (var agent = 0; agent < AgentCount; agent++)
        {
            _bucketCounts[BucketIndexOf(_posX[agent], _posY[agent])]++;
        }

        var runningSum = 0;

        for (var bucket = 0; bucket < BucketCount; bucket++)
        {
            _bucketStarts[bucket] = runningSum;
            _bucketFill[bucket] = runningSum;
            runningSum += _bucketCounts[bucket];
        }

        // Aufsteigende Platzierung: innerhalb eines Buckets stehen die
        // Agenten damit in kanonischer Indexreihenfolge.
        for (var agent = 0; agent < AgentCount; agent++)
        {
            var bucket = BucketIndexOf(_posX[agent], _posY[agent]);
            _bucketItems[_bucketFill[bucket]] = agent;
            _bucketFill[bucket]++;
        }
    }

    /// <summary>
    /// Kanonischer Zustands-Hash (FNV-1a 64) ueber den sim-relevanten
    /// Zustand in fester Feldordnung: Tick, Seed, Gruppenziele, je Agent
    /// Position, Geschwindigkeit, Zielkachel, Pfadstatus, geplante Zone,
    /// Wegpunktcursor/-anzahl sowie die ausstehenden Wegpunkte ab Cursor.
    /// Transiente Sucharbeitsplaetze sind vertraglich nicht Teil des
    /// Relevantzustands (Neustart je Abschnitt, kein Residuum).
    /// </summary>
    public ulong ComputeStateHash()
    {
        var hash = 0xCBF29CE484222325UL;

        MixLong(ref hash, _tickIndex);
        MixLong(ref hash, _seed);

        for (var group = 0; group < SimulationContract.GroupCount; group++)
        {
            MixLong(ref hash, _targetZoneByGroup[group]);
        }

        for (var agent = 0; agent < AgentCount; agent++)
        {
            MixLong(ref hash, _posX[agent]);
            MixLong(ref hash, _posY[agent]);
            MixLong(ref hash, _velX[agent]);
            MixLong(ref hash, _velY[agent]);
            MixLong(ref hash, _goalTile[agent]);
            MixLong(ref hash, _pathState[agent]);
            MixLong(ref hash, _plannedZone[agent]);

            var count = _waypointCount[agent];
            var cursor = _waypointCursor[agent];
            MixLong(ref hash, cursor);
            MixLong(ref hash, count);

            var offset = agent * NavWorld.MaxWaypointsPerAgent;

            for (var index = cursor; index < count; index++)
            {
                MixLong(ref hash, _waypointTile[offset + index]);
            }
        }

        return hash;
    }

    private static void MixLong(ref ulong hash, long value)
    {
        const ulong fnvPrime = 0x100000001B3UL;

        hash = (hash ^ (byte)value) * fnvPrime;
        hash = (hash ^ (byte)(value >> 8)) * fnvPrime;
        hash = (hash ^ (byte)(value >> 16)) * fnvPrime;
        hash = (hash ^ (byte)(value >> 24)) * fnvPrime;
        hash = (hash ^ (byte)(value >> 32)) * fnvPrime;
        hash = (hash ^ (byte)(value >> 40)) * fnvPrime;
        hash = (hash ^ (byte)(value >> 48)) * fnvPrime;
        hash = (hash ^ (byte)(value >> 56)) * fnvPrime;
    }

    public SimSnapshot CreateSnapshot()
    {
        return new SimSnapshot
        {
            TickIndex = _tickIndex,
            Seed = _seed,
            TargetZoneByGroup = _targetZoneByGroup.ToArray(),
            PositionXQ16 = _posX.ToArray(),
            PositionYQ16 = _posY.ToArray(),
            VelocityXQ16 = _velX.ToArray(),
            VelocityYQ16 = _velY.ToArray(),
            GoalTile = _goalTile.ToArray(),
            Group = _group.ToArray(),
            PathState = _pathState.ToArray(),
        };
    }
}
