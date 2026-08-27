namespace Riftward.Session;

/// <summary>
/// Kamerazustand V0 (Kommandovertrag Abschnitt 4, rein darstellseitig):
/// geneigte Top-Down-Ansicht mit fester Nordausrichtung, geclipptem Zoom und
/// Weltrandbegrenzung auf das 160x90-Meter-Vertragsraster. Der Zustand ist
/// niemals Teil von Simulationszustand oder Hash; Projektions- und
/// Pickingmathematik bleiben in der Darstellungsschicht (Riftward.App).
/// Alle Werte sind dokumentierte Hypothesenkonstanten mit Rueckrollweg.
/// </summary>
public sealed class GrayboxCamera
{
    /// <summary>Nickwinkel in Grad (Hypothese V0).</summary>
    public const double PitchDegrees = 55.0;

    /// <summary>Anzeigedistanz-Zoomminimum in Metern (naeher ran).</summary>
    public const double DistanceMinMeters = 12.0;

    /// <summary>Anzeigedistanz-Zoommaximum in Metern (weiter weg).</summary>
    public const double DistanceMaxMeters = 60.0;

    /// <summary>Zoomschrittweite als Multiplikator pro Rad-/Tastenschritt.</summary>
    public const double ZoomStepFactor = 1.15;

    /// <summary>Schwenkgeschwindigkeit je Tastenschritt in Metern bei Referenzdistanz.</summary>
    public const double PanStepMeters = 2.0;

    public GrayboxCamera()
    {
        // Start: Weltmitte der Vertragswelt.
        CenterXMeters = Riftward.Simulation.NavWorld.TilesX / 2.0;
        CenterZMeters = Riftward.Simulation.NavWorld.TilesY / 2.0;
        DistanceMeters = 32.0;
        Clamp();
    }

    /// <summary>Mittelpunkt des Sichtfensters auf dem Boden, X-Achse (Meter).</summary>
    public double CenterXMeters { get; private set; }

    /// <summary>Mittelpunkt des Sichtfensters auf dem Boden, Z-Achse (Meter; entspricht Simulations-Y).</summary>
    public double CenterZMeters { get; private set; }

    /// <summary>Aktuelle Anzeigedistanz in Metern.</summary>
    public double DistanceMeters { get; private set; }

    /// <summary>Schwenkt das Fenster um die gegebene Meterdifferenz (bereits richtungsrichtig).</summary>
    public void Pan(double deltaX, double deltaZ)
    {
        CenterXMeters += deltaX;
        CenterZMeters += deltaZ;
        Clamp();
    }

    /// <summary>Skaliertes Schwenken: naehere Kamera bewegt sich pro Schritt weniger weit.</summary>
    public void PanSteps(double stepsRight, double stepsDown)
    {
        var scale = DistanceMeters / ((DistanceMinMeters + DistanceMaxMeters) * 0.5);
        Pan(stepsRight * PanStepMeters * scale, stepsDown * PanStepMeters * scale);
    }

    /// <summary>Zoomschritt: positiv hinein, negativ heraus; immer geclippt.</summary>
    public void ZoomSteps(int steps)
    {
        if (steps == 0)
        {
            return;
        }

        var factor = Math.Pow(ZoomStepFactor, -steps);
        DistanceMeters = Math.Clamp(DistanceMeters * factor, DistanceMinMeters, DistanceMaxMeters);
        Clamp();
    }

    /// <summary>Setzt die Anzeigedistanz direkt (Interaktivmodus); immer geclippt.</summary>
    public void SetDistance(double meters)
    {
        DistanceMeters = meters;
        Clamp();
    }

    private void Clamp()
    {
        CenterXMeters = Math.Clamp(CenterXMeters, 0.0, Riftward.Simulation.NavWorld.TilesX);
        CenterZMeters = Math.Clamp(CenterZMeters, 0.0, Riftward.Simulation.NavWorld.TilesY);
        DistanceMeters = Math.Clamp(DistanceMeters, DistanceMinMeters, DistanceMaxMeters);
    }
}
