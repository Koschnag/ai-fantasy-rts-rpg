namespace Riftward.Session;

/// <summary>
/// Eine Graybox-Landmarke des Erkundungsvertrags (T-034, Abschnitt 2):
/// ausschließlich das Tripel (Zonenindex, Ankerkachel, Aufsuchzustand) ohne
/// Namens-, Lore-, Text- oder Assetinhalte. Der Anker ist die erste
/// betretbare Kachel der Zone in zeilenmajoritischer Scanreihenfolge
/// (aufsteigend y, dann aufsteigend x) innerhalb der Vertragszonen-
/// Schranken; die Ableitung ist rein geometrisch und seedunabhaengig.
/// </summary>
public sealed record ExplorationLandmark(
    int ZoneIndex,
    int AnchorTileX,
    int AnchorTileY,
    bool Walkable);

/// <summary>
/// Maschinenlesbarer Protokolleintrag einer Registrierung (Vertrag
/// Abschnitt 4): Auswertungsgrenze (Vorgrenze-Tick), Landmarkenzone,
/// vertraglicher Modusname an dieser Grenze (ausschließlich
/// <c>strategic</c>/<c>personal</c>) und die 1-basierte Registrierungs-
/// reihenfolge. Rein diagnostisch (gateCoupled=false) und nie Teil des
/// Simulationszustands oder Hashes.
/// </summary>
public sealed record ExplorationVisit(
    long EvaluationBoundaryTick,
    int ZoneIndex,
    string Mode,
    long VisitOrder);

/// <summary>
/// Schreibgeschützter Ausweis des sitzungslokalen Erkundungsfortschritts:
/// Landmarkenmenge in fester Zonenordnung, Aufsuchprotokoll in kanonischer
/// Registrierungsfolge und der Fortschritt. Niemals Teil von
/// Simulationszustand oder Hash; niemals persistiert.
/// </summary>
public sealed record ExplorationTelemetry(
    IReadOnlyList<ExplorationLandmark> Landmarks,
    IReadOnlyList<ExplorationVisit> VisitProtocol,
    int VisitedCount,
    int LandmarkCount,
    bool Completed);

/// <summary>
/// Deterministische, assetfreie Landmarken-Ableitung aus der bestehenden
/// Vertragswelt (Vertrag Abschnitt 2, <c>graybox-landmark-zone-anchor-v1</c>):
/// genau eine Landmarke je Vertragszone; der Anker ist die erste betretbare
/// Kachel der Zone in zeilenmajoritischer Scanreihenfolge (aufsteigend y,
/// dann aufsteigend x). Die Ableitung konsumiert ausschließlich die
/// fixierte Zonen-/Kachelgeometrie, keinen Sitzungsseed, keine Assets und
/// keine Ortsemantik. Fail-closed: besitzt eine Zone keine betretbare
/// Kachel, bricht die Ableitung kontrolliert mit
/// <see cref="ExplorationContract.RejectReasonZoneWithoutWalkableTile"/> ab
/// statt einen undefinierten Anker zu bilden; der gebundene Vertragsweltstand
/// erzwingt die Zonendeckung 0–5 bereits pro Prozessstart
/// (<c>NavWorld.ValidateZones</c>).
/// </summary>
public static class ExplorationAnchors
{
    /// <summary>
    /// Leitet die fixierte Landmarkenmenge ab: genau eine Landmarke je
    /// Vertragszone in fester Zonenordnung, seedunabhaengig und rein
    /// geometrisch.
    /// </summary>
    public static ExplorationLandmark[] DeriveLandmarks() =>
        DeriveFrom(
            NavWorld.ZoneCount,
            NavWorld.TilesX,
            NavWorld.TilesY,
            NavWorld.IsInsideZone,
            NavWorld.IsWalkable);

    /// <summary>
    /// Ableitung über einer Zonenzuordnung und Begehbarkeit (Testbindung):
    /// zeilenmajoritischer Scan (aufsteigend y, dann aufsteigend x) je Zone;
    /// ohne betretbare Kachel kontrollierter Vertragsfehler
    /// (<see cref="ExplorationContract.RejectReasonZoneWithoutWalkableTile"/>)
    /// statt undefinierter Ableitung.
    /// </summary>
    internal static ExplorationLandmark[] DeriveFrom(
        int zoneCount,
        int tilesX,
        int tilesY,
        Func<int, int, int, bool> isInsideZone,
        Func<int, int, bool> isWalkable)
    {
        var landmarks = new ExplorationLandmark[zoneCount];

        for (var zone = 0; zone < zoneCount; zone++)
        {
            var anchorTileX = -1;
            var anchorTileY = -1;

            for (var y = 0; y < tilesY && anchorTileX < 0; y++)
            {
                for (var x = 0; x < tilesX; x++)
                {
                    if (!isInsideZone(zone, x, y) || !isWalkable(x, y))
                    {
                        continue;
                    }

                    anchorTileX = x;
                    anchorTileY = y;
                    break;
                }
            }

            if (anchorTileX < 0)
            {
                throw new InvalidOperationException(
                    $"{ExplorationContract.RejectReasonZoneWithoutWalkableTile}: Zone {zone} bietet keine betretbare Ankerkachel; kontrollierter Vertragsfehler.");
            }

            landmarks[zone] = new ExplorationLandmark(
                ZoneIndex: zone,
                AnchorTileX: anchorTileX,
                AnchorTileY: anchorTileY,
                Walkable: isWalkable(anchorTileX, anchorTileY));
        }

        return landmarks;
    }
}

/// <summary>
/// Sitzungslokaler Erkundungsauftrag (T-034, Vertrag Abschnitte 2 bis 4):
/// deterministische, seedunabhaengige Landmarkenmenge (je Vertragszone
/// genau eine geometrisch abgeleitete Landmarke) plus die Aufsuch- und
/// Moduskopplungsregel. Die Beobachtung ist rein sitzungsseitig an der
/// Vorgrenze: Sie liest ausschließlich Heldenzone und Sitzungsmodus
/// schreibgeschützt, erzeugt niemals einen Kernbefehl, verändert keinen
/// Befehlszustand und ist zu keinem Zeitpunkt Teil des Simulationszustands
/// oder Hashes. Registriert wird genau dann, wenn an einer Auswertungsgrenze
/// (i) der Vertragsheld physisch in der Landmarkenzone ist, (ii) die
/// Sitzung an dieser Vorgrenze im persönlichen Modus ist und (iii) die
/// Landmarke in dieser Sitzung noch nicht registriert ist.
/// </summary>
public sealed class ExplorationSession
{
    private readonly ExplorationLandmark[] _landmarks;
    private readonly bool[] _registered;
    private readonly List<ExplorationVisit> _visits = new();
    private int _visitedCount;

    /// <summary>Erzeugt die Sitzung mit der seedunabhaengigen Ankerableitung.</summary>
    public ExplorationSession()
    {
        _landmarks = ExplorationAnchors.DeriveLandmarks();
    }

    /// <summary>Landmarkenmenge in fester Zonenordnung 0 bis ZoneCount-1.</summary>
    public IReadOnlyList<ExplorationLandmark> Landmarks => _landmarks;

    /// <summary>Aufsuchprotokoll in kanonischer Registrierungsfolge.</summary>
    public IReadOnlyList<ExplorationVisit> VisitProtocol => _visits;

    /// <summary>Anzahl registrierter Landmarken dieser Sitzung.</summary>
    public int VisitedCount => _visitedCount;

    /// <summary>Feste Landmarkenmenge (je Vertragszone eine Landmarke).</summary>
    public int LandmarkCount => _landmarks.Length;

    /// <summary>Abschlussstatus: vollständige Landmarkenmenge registriert.</summary>
    public bool Completed => _visitedCount == _landmarks.Length;

    /// <summary>Registrierungszustand einer Landmarke in dieser Sitzung.</summary>
    public bool IsRegistered(int zoneIndex) => _registered[zoneIndex];

    /// <summary>
    /// Beobachtung an einer Auswertungsgrenze (Vorgrenze T,
    /// <paramref name="boundaryTick"/> ist der Vorgrenze-Tick): liest
    /// ausschließlich Heldenzone und Sitzungsmodus schreibgeschützt und
    /// registriert genau nach der vertraglichen Dreifachbedingung. Die
    /// Beobachtung erzeugt niemals einen Kernbefehl, verändert keinen
    /// Befehlszustand und ist niemals Teil des Simulationszustands oder
    /// Hashes. Doppelbesuche registrieren in derselben Sitzung nie erneut;
    /// strategische Anwesenheit bleibt bewusst ungezaehlt (kein stiller
    /// Zaehler, keine Nachwirkung).
    /// </summary>
    public void Observe(long boundaryTick, SimWorld world, SessionMode effectiveMode)
    {
        ArgumentNullException.ThrowIfNull(world);

        var heroZone = HeroTracker.ZoneIndexOf(world);

        if (heroZone < 0 || heroZone >= _landmarks.Length)
        {
            return;
        }

        if (effectiveMode != SessionMode.Personal || _registered[heroZone])
        {
            // Bewusste strategische Nichtregistrierung ohne stillen Zaehler,
            // ohne Puffer und ohne Nachwirkung (Vertrag Abschnitt 3).
            return;
        }

        _registered[heroZone] = true;
        _visitedCount++;
        _visits.Add(new ExplorationVisit(
            EvaluationBoundaryTick: boundaryTick,
            ZoneIndex: heroZone,
            Mode: effectiveMode == SessionMode.Personal
                ? ModeContract.ModePersonalId
                : ModeContract.ModeStrategicId,
            VisitOrder: _visitedCount));
    }

    /// <summary>Schreibgeschützter Ausweis des Laufs für den Report.</summary>
    public ExplorationTelemetry ToTelemetry() => new(
        Landmarks: _landmarks,
        VisitProtocol: _visits.ToArray(),
        VisitedCount: _visitedCount,
        LandmarkCount: _landmarks.Length,
        Completed: Completed);
}
