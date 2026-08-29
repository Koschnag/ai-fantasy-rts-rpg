namespace Riftward.Session;

/// <summary>
/// Versionierte Kennungen und fixierte Vertragswerte des kleinsten Hybrid-
/// Mode-Switch-Prototyps (T-033). Jede Kennung ist in
/// <c>docs/MODEVERTRAG.md</c> (Abschnitt 0, gatender Vertragsspike) mit
/// Alternativen, Gruenden, Playtestkriterien und Rueckrollweg dokumentiert.
/// Die Werte hier sind die maschinenlesbare Spiegelung des Vertrags; ein
/// Test haelt beide Seiten konsistent. Kein Wert dieses Vertrags antwortet
/// auf eine offene Produktfrage (Q-GAM-001 bis Q-GAM-007, Q-NAR-002,
/// Q-GAM-010, Q-TEC-004, Q-TEC-006, Q-TEC-010 bleiben offen).
/// </summary>
public static class ModeContract
{
    /// <summary>Pfad des versionierenden Vertragsdokuments.</summary>
    public const string DocumentPath = "docs/MODEVERTRAG.md";

    /// <summary>Vertragsversion des Dokuments (V2: additive Persistenz-Präzisierung, T-037).</summary>
    public const string ContractVersion = "2";

    /// <summary>
    /// Versionierte Sitzungsbezeichnung des Vertragshelden: stabiler
    /// Agentenindex 0 in der bestehenden Vertragsgruppe 0; eine Kern- oder
    /// Bestandsvertragssemantik ist damit nicht behauptet (Modevertrag
    /// Abschnitt 2).
    /// </summary>
    public const string HeroDesignationId = "session-hero-agent-index-0-group-0-v1";

    /// <summary>Agentenindex des Vertragshelden (Sitzungsbezeichnung).</summary>
    public const int HeroAgentIndex = 0;

    /// <summary>Vertragsgruppe des Vertragshelden (Codebeleg: Modulo-Zuordnung).</summary>
    public const int HeroGroupIndex = 0;

    /// <summary>Kennung des Lenkmodells im persoenlichen Modus.</summary>
    public const string SteeringModelId = "hero-direction-steering-zones-v1";

    /// <summary>Kennung der darstellseitigen Verfolgungskamera.</summary>
    public const string CameraModelId = "hero-chase-camera-v1";

    /// <summary>Kennung des Badge-Modusindikators (zwei visuelle Kanaele).</summary>
    public const string BadgeModelId = "hero-mode-badge-v1";

    /// <summary>Kennung des Mindest-HUD in der Fenstertitelzeile.</summary>
    public const string HudModelId = "title-hud-mode-herozone-v1";

    /// <summary>Kennung der Wechselaktionsfamilie in der Keymap.</summary>
    public const string SwitchActionId = "mode-toggle-keymap-action-v1";

    /// <summary>Semantischer Aktionsname der Umschaltaktion in der Keymap.</summary>
    public const string SwitchActionName = "mode-switch";

    /// <summary>
    /// Kennung der kanonischen Same-Tick-Regel: Wechsel wird kanonisch nach
    /// allen anderen Intents desselben Ticks ausgewertet und wirkt erstmals
    /// an der uebernächsten Gültigkeitsprüfung (M = S + 2).
    /// </summary>
    public const string SwitchRuleId = "same-tick-switch-last-effective-next-next-v1";

    /// <summary>Kennung der Modus-Scoping-Regel der Eingabesemantik.</summary>
    public const string ScopingRuleId = "mode-scoping-v1";

    /// <summary>
    /// Kennung der interaktiven Kontextabweisung: kontextfalsche Impulse
    /// erhalten eine sichtbare, maschinenlesbare Abweisung statt stiller
    /// Wirkung (reversible Hypothese mit Rueckrollweg).
    /// </summary>
    public const string ContextRejectionPolicyId = "context-visible-rejection-v1";

    /// <summary>Formatkennung der erweiterten Skriptgrammatik (Obermenge).</summary>
    public const string ScriptFormatIdV2 = "graybox-input-script-v2";

    /// <summary>Zieltickgrenze der Wechselreaktion (100 ms / 50 ms je Tick).</summary>
    public const int SwitchReactionTargetTicks = 2;

    /// <summary>Harte Tickgrenze der Wechselreaktion (150 ms / 50 ms je Tick).</summary>
    public const int SwitchReactionHardLimitTicks = 3;

    /// <summary>
    /// Vertraglich benannter fachlicher Ablehnungsgrund (Modevertrag
    /// Abschnitt 5): strategischer Intent im persoenlichen Modus. Die
    /// Kennung erscheint am Live-Pfad als UF-001-Fehlerzeile und als
    /// Reportzaehler, bevor ein Kernbefehl entstünde.
    /// </summary>
    public const string RejectReasonStrategyIntentInPersonalMode = "strategy-intent-in-personal-mode";

    /// <summary>
    /// Vertraglich benannter fachlicher Ablehnungsgrund (Modevertrag
    /// Abschnitt 5): persoenliche Lenkung im strategischen Modus.
    /// </summary>
    public const string RejectReasonSteerIntentInStrategyMode = "steer-intent-in-strategy-mode";

    /// <summary>
    /// Vertraglich benannter fachlicher Ablehnungsgrund (Modevertrag
    /// Abschnitt 3): interaktive Lenkrichtung ohne richtungstreue Zone.
    /// </summary>
    public const string RejectReasonSteerDirectionWithoutZone = "steer-direction-without-zone";

    /// <summary>Vertragliche Modusnamen des Reports.</summary>
    public const string ModeStrategicId = "strategic";

    /// <summary>Vertragliche Modusnamen (persoenlicher Modus).</summary>
    public const string ModePersonalId = "personal";
}