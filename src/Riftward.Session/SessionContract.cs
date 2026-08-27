namespace Riftward.Session;

/// <summary>
/// Versionierte Kennungen und fixierte Vertragswerte der interaktiven
/// Graybox-Kommandoschleife (T-032). Jede Kennung ist in
/// <c>docs/KOMMANDOVERTRAG.md</c> (Abschnitt 0, gatender Vertragsspike) mit
/// Alternativen, Gruenden, Playtestkriterien und Rueckrollweg dokumentiert.
/// Die Werte hier sind die maschinenlesbare Spiegelung des Vertrags; ein
/// Test haelt beide Seiten konsistent. Kein Wert dieses Vertrags antwortet
/// auf eine offene Produktfrage (Q-GAM-001 bis Q-GAM-007, Q-NAR-002,
/// Q-TEC-006 bleiben offen).
/// </summary>
public static class SessionContract
{
    /// <summary>Pfad des versionierenden Vertragsdokuments.</summary>
    public const string DocumentPath = "docs/KOMMANDOVERTRAG.md";

    /// <summary>Vertragsversion des Dokuments.</summary>
    public const string ContractVersion = "1";

    /// <summary>Kennung des Eingabeskript-Diagnoseformats (nicht shippingbestimmt).</summary>
    public const string ScriptFormatId = "graybox-input-script-v1";

    /// <summary>Einzige implementierte Szenariokennung des Befehls kommandoschleife.</summary>
    public const string ScenarioId = "kommando-graybox";

    /// <summary>Inhaltskennung der Graybox-Kommandoschleife (Clean-Room).</summary>
    public const string ContentId = "synthetic-graybox-command-loop";

    /// <summary>Kennung des Auswahlmodells V0 (vorregistrierte Hypothese).</summary>
    public const string SelectionModelId = "graybox-selection-model-v0";

    /// <summary>Kennung des Kameramodells V0 (vorregistrierte Hypothese).</summary>
    public const string CameraModelId = "graybox-camera-model-v0";

    /// <summary>Auswahlradius in Millimetern (Vertrag Abschnitt 3: 3000 mm).</summary>
    public const long SelectRadiusMillimeters = 3000;

    /// <summary>Hoechstzahl Intents je Tick (Vertrag Abschnitt 5).</summary>
    public const int IntentsPerTickMax = 4;

    /// <summary>Hoechstzahl Intents je Skript (Vertrag Abschnitt 5).</summary>
    public const int TotalIntentsMax = 4096;

    /// <summary>Hoechstzahl Skriptbytes (untrusted Eingabegroesse, Vertrag Abschnitt 5).</summary>
    public const long ScriptBytesMax = 262_144;

    /// <summary>Standard-Horizont in Ticks (Vertrag Abschnitt 5).</summary>
    public const int DefaultHorizonTicks = 1200;

    /// <summary>Hoechstgrenze des Skripthorizonts in Ticks.</summary>
    public const int HorizonTicksMax = 20_000;

    /// <summary>Hoechstgrenze des Warm-ups in Ticks.</summary>
    public const int WarmupTicksMax = 10_000;

    /// <summary>Standard-Warm-up in Ticks; alle Intentticks liegen im Messfenster.</summary>
    public const int DefaultWarmupTicks = 240;

    /// <summary>Hashketten-Stichprobenintervall je Tick (T-021-Praezedenz).</summary>
    public const int HashSampleIntervalTicks = 60;

    /// <summary>RSS-Stichprobenintervall je Tick (T-021-Praezedenz).</summary>
    public const int RssSampleIntervalTicks = 60;

    /// <summary>Zieltickgrenze der Reaktion (100 ms / 50 ms, Vertrag Abschnitt 6).</summary>
    public const int ReactionTargetTicks = 2;

    /// <summary>Harte Tickgrenze der Reaktion (150 ms / 50 ms, Vertrag Abschnitt 6).</summary>
    public const int ReactionHardLimitTicks = 3;

    /// <summary>
    /// Vertraglich benannter fachlicher Ablehnungsgrund (Vertrag Abschnitt 2):
    /// Bewegungsintent ohne ausgewaehlte Gruppe. Die Kennung ist Bestandteil
    /// des maschinenlesbaren Vertrags und wird bei der Live-Abweisung als
    /// UF-001-Fehlerzeile ausgegeben statt nur als Zaehler zu erscheinen.
    /// </summary>
    public const string RejectReasonMoveWithoutSelection = "move-without-selection";

    /// <summary>
    /// Vertraglich benannter fachlicher Ablehnungsgrund (Vertrag Abschnitt 9):
    /// Befehlsziel liegt in keiner Zone. Die Kennung wird bei der Live-
    /// Abweisung als UF-001-Fehlerzeile ausgegeben (UF-001-Fehlerfall
    /// „Befehl hat kein gueltiges Ziel“).
    /// </summary>
    public const string RejectReasonTargetNotInZone = "target-not-in-zone";

    /// <summary>Weltbreite in Millimetern fuer Skriptbereichspruefungen (160 m).</summary>
    public const long WorldWidthMillimeters = Riftward.Simulation.NavWorld.TilesX * 1000L;

    /// <summary>Welthoehe in Millimetern fuer Skriptbereichspruefungen (90 m).</summary>
    public const long WorldHeightMillimeters = Riftward.Simulation.NavWorld.TilesY * 1000L;
}
