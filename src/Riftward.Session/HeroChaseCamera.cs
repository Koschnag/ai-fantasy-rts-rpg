namespace Riftward.Session;

/// <summary>
/// Darstellseitige Verfolgungskamera des persönlichen Modus (T-033,
/// Modevertrag Abschnitt 8, <c>hero-chase-camera-v1</c>): geneigte
/// Verfolgungsansicht hinter der Heldenfigur; Blickpunkt ist die
/// Heldenposition (Agentenindex 0), die Kamera sitzt südlich der Figur
/// (feste Nordausrichtung konsistent zur §4-Konvention des Kommandovertrags),
/// Nickwinkel 55°, Anzeigedistanz 9 m geclippt auf 5–16 m (Zoom-Schritte),
/// Blickpunkt an die Weltränder des 160x90-Meter-Vertragsrasters geclampt.
/// Der Zustand ist rein darstellseitiger Sitzungszustand und niemals Teil von
/// Simulationszustand oder Hash; die Projektions- und Pickingmathematik bleibt
/// in der Darstellungsschicht (Riftward.App). Alle Werte sind dokumentierte
/// Hypothesenkonstanten mit Rückrollweg (Modevertrag Abschnitt 8).
/// </summary>
public sealed class HeroChaseCamera
{
    /// <summary>Nickwinkel in Grad (Modevertrag Abschnitt 8).</summary>
    public const double PitchDegrees = 55.0;

    /// <summary>Anzeigedistanz-Zoomminimum in Metern (naeher ran).</summary>
    public const double DistanceMinMeters = 5.0;

    /// <summary>Anzeigedistanz-Zoommaximum in Metern (weiter weg).</summary>
    public const double DistanceMaxMeters = 16.0;

    /// <summary>Zoomschrittweite als Multiplikator pro Rad-/Tastenschritt (wie GrayboxCamera).</summary>
    public const double ZoomStepFactor = 1.15;

    /// <summary>Vertragliche Anzeigedistanz (Modevertrag Abschnitt 8).</summary>
    public const double DefaultDistanceMeters = 9.0;

    public HeroChaseCamera()
    {
        DistanceMeters = DefaultDistanceMeters;
    }

    /// <summary>Blickpunkt (Heldenposition) X-Achse in Metern, geclampt.</summary>
    public double CenterXMeters { get; private set; }

    /// <summary>Blickpunkt (Heldenposition) Z-Achse (Suedachse) in Metern, geclampt.</summary>
    public double CenterZMeters { get; private set; }

    /// <summary>Aktuelle Anzeigedistanz in Metern, immer geclippt.</summary>
    public double DistanceMeters { get; private set; }

    /// <summary>
    /// Folgt schreibgeschützt der Heldenposition (Agentenindex 0) des
    /// unveränderten Kerns; der Blickpunkt wird an die Weltränder geclampt.
    /// Die Kamera mutiert den Kern nie.
    /// </summary>
    public void Follow(Riftward.Simulation.SimWorld world)
    {
        CenterXMeters = world.PositionXOf(ModeContract.HeroAgentIndex) / (double)Riftward.Simulation.FixedPoint.One;
        CenterZMeters = world.PositionYOf(ModeContract.HeroAgentIndex) / (double)Riftward.Simulation.FixedPoint.One;
        Clamp();
    }

    /// <summary>Zoomschritt der Verfolgungsdistanz: positiv hinein, negativ heraus; immer geclippt.</summary>
    public void ZoomSteps(int steps)
    {
        if (steps == 0)
        {
            return;
        }

        var factor = Math.Pow(ZoomStepFactor, -steps);
        DistanceMeters = Math.Clamp(DistanceMeters * factor, DistanceMinMeters, DistanceMaxMeters);
    }

    private void Clamp()
    {
        CenterXMeters = Math.Clamp(CenterXMeters, 0.0, Riftward.Simulation.NavWorld.TilesX);
        CenterZMeters = Math.Clamp(CenterZMeters, 0.0, Riftward.Simulation.NavWorld.TilesY);
        DistanceMeters = Math.Clamp(DistanceMeters, DistanceMinMeters, DistanceMaxMeters);
    }
}
