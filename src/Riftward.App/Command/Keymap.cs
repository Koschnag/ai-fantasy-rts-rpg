namespace Riftward.App.Command;

/// <summary>
/// Datengetriebene Keymap der Graybox-Kommandoschleife (Kommandovertrag
/// Abschnitt 9): Semantische Aktionsnamen werden gegen die dokumentierten
/// SDL-Scancode-Defaults validiert; ein Test haelt Tabelle und Vertrag
/// konsistent. Die Maussemantik ist vertraglich fixiert und hier nicht
/// belegbar. Scancodes entsprechen dem gepinnten SDL3-Stand release-3.4.14.
/// </summary>
public static class Keymap
{
    /// <summary>
    /// Semantischer Aktionsname der Entscheidung fuer Option A (T-035,
    /// Entscheidungsvertrag Abschnitt 4): frei belegbare, datengetriebene
    /// Wahltaste; Standardbelegung ist die Zifferntaste `1` (Scancode 30),
    /// die im T-033-Stand unbesetzt war.
    /// </summary>
    public const string ChooseAActionName = "choose-a";

    /// <summary>Semantischer Aktionsname der Entscheidung fuer Option B (T-035).</summary>
    public const string ChooseBActionName = "choose-b";

    /// <summary>
    /// Semantischer Aktionsname des Speicherns (T-037, Savevertrag V2
    /// Abschnitt 13.3): frei belegbar; Standardbelegung ist F5 (Scancode 58),
    /// das im Bestandsstand unbesetzt war.
    /// </summary>
    public const string SaveSlotActionName = "save-slot";

    /// <summary>Semantischer Aktionsname des Ladens (T-037, Savevertrag V2 Abschnitt 13.3); Standard F9 (62).</summary>
    public const string LoadSlotActionName = "load-slot";

    /// <summary>
    /// Semantische Aktionsfamilie des Vertrags. <c>mode-switch</c> ist die
    /// T-033-Erweiterung (Modevertrag Abschnitt 4): frei belegbare,
    /// datengetriebene Umschaltaktion; der Standard belegt Tab (Scancode 43),
    /// das im T-032-Stand unbesetzt war. <c>choose-a</c>/<c>choose-b</c> sind
    /// die T-035-Erweiterung (Entscheidungsvertrag Abschnitt 4).
    /// <c>save-slot</c>/<c>load-slot</c> sind die T-037-Erweiterung
    /// (Savevertrag V2 Abschnitt 13.3).
    /// </summary>
    public static readonly string[] SemanticActions =
    [
        "quit",
        "pan-up",
        "pan-down",
        "pan-left",
        "pan-right",
        "zoom-in",
        "zoom-out",
        "mode-switch",
        ChooseAActionName,
        ChooseBActionName,
        SaveSlotActionName,
        LoadSlotActionName,
    ];

    /// <summary>Defaultbelegung: Aktion → SDL-Scancodes (gepinnter Stand).</summary>
    public static readonly IReadOnlyDictionary<string, int[]> Defaults = new Dictionary<string, int[]>(StringComparer.Ordinal)
    {
        ["quit"] = [41], // Escape
        ["pan-up"] = [26, 82], // W, Up
        ["pan-down"] = [22, 81], // S, Down
        ["pan-left"] = [4, 80], // A, Left
        ["pan-right"] = [7, 79], // D, Right
        ["zoom-in"] = [8, 46], // E, Equals
        ["zoom-out"] = [20, 45], // Q, Minus
        ["mode-switch"] = [43], // Tab (T-033 Modevertrag Abschnitt 4)
        [ChooseAActionName] = [30], // Zifferntaste 1 (T-035 Entscheidungsvertrag Abschnitt 4)
        [ChooseBActionName] = [31], // Zifferntaste 2 (T-035 Entscheidungsvertrag Abschnitt 4)
        [SaveSlotActionName] = [58], // F5 (T-037 Savevertrag V2 Abschnitt 13.3)
        [LoadSlotActionName] = [62], // F9 (T-037 Savevertrag V2 Abschnitt 13.3)
    };

    /// <summary>
    /// Validiert eine Belegungstabelle: Jede semantische Aktion besitzt
    /// mindestens eine Bindung, keine Bindung ist doppelt oder unbekannt,
    /// und kein Scancode ist mehreren Aktionen zugeordnet.
    /// </summary>
    public static bool Validate(IReadOnlyDictionary<string, int[]> bindings, out string error)
    {
        error = string.Empty;

        foreach (var action in SemanticActions)
        {
            if (!bindings.TryGetValue(action, out var scancodes)
                || scancodes.Length == 0)
            {
                error = $"Semantische Aktion '{action}' besitzt keine Bindung.";
                return false;
            }
        }

        var seen = new HashSet<int>();

        foreach (var pair in bindings)
        {
            if (Array.IndexOf(SemanticActions, pair.Key) < 0)
            {
                error = $"Unbekannter semantischer Aktionsname '{pair.Key}'.";
                return false;
            }

            foreach (var scancode in pair.Value)
            {
                if (scancode <= 0)
                {
                    error = $"Aktion '{pair.Key}' enthaelt einen ungueltigen Scancode {scancode}.";
                    return false;
                }

                if (!seen.Add(scancode))
                {
                    error = $"Scancode {scancode} ist mehrfach gebunden.";
                    return false;
                }
            }
        }

        return true;
    }

    /// <summary>Löst einen Scancode zur semantischen Aktion auf; null ohne Treffer.</summary>
    public static string? Resolve(int scancode)
    {
        foreach (var action in SemanticActions)
        {
            if (Array.IndexOf(Defaults[action], scancode) >= 0)
            {
                return action;
            }
        }

        return null;
    }
}
