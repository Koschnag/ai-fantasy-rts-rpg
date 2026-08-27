namespace Riftward.App.Command;

/// <summary>
/// Opt-in Frame-Evidenzartefakt der Kommandoschleife (T-032/T-033,
/// Kommandovertrag Abschnitt 10, Modevertrag Abschnitt 8): mit Flag genau
/// ein Abgriffpaar von je einem 1920x1080-Einzelabgriff pro Modus über
/// demselben Weltzustand am selben Tick, strikt nach dem Messfenster, je
/// hashgebunden im Report. Die Aussagegrenze ist ausschließlich die
/// Graybox-Zustandsbelegung (Auswahl-/Befehls-/Heldmarker über der
/// Vertragswelt) — niemals Gameplay-, Atmosphären- oder Shipping-Aussage;
/// öffentliche Verwendung bleibt an docs/communication/MEDIA_LAB.md plus
/// Projektleitungsautorisierung gebunden. Ohne Flag entsteht keine Datei.
/// </summary>
public static class CommandFrameEvidence
{
    /// <summary>Gebundene Aussagegrenze des Artefakts (maschinenlesbar im Report).</summary>
    public const string StatementLimit = "graybox-state-occupancy-not-gameplay-atmosphere-or-shipping";

    /// <summary>Grundkennung fuer „kein Abgriff angefordert“.</summary>
    public const string ReasonNotRequested = "capture-not-requested";
}
