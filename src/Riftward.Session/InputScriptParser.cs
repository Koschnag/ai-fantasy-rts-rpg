using System.Security.Cryptography;
using System.Text;

namespace Riftward.Session;

/// <summary>Unterscheidbare kontrollierte Ablehnungsklassen des Skriptparsers.</summary>
public enum InputScriptRejectReason : byte
{
    /// <summary>Kopfzeile entspricht nicht dem Format graybox-input-script-v1.</summary>
    HeaderMalformed = 1,

    /// <summary>Zeile syntaktisch ungueltig (Tokenzahl, Ganzzahlformat, unbekanntes Schluesselwort).</summary>
    LineMalformed = 2,

    /// <summary>Unbekannte Intentaktion.</summary>
    UnknownAction = 3,

    /// <summary>Parameter ausserhalb der Vertragswertebereiche.</summary>
    RangeViolation = 4,

    /// <summary>Doppelter identischer Intent innerhalb eines Ticks.</summary>
    DuplicateIntent = 5,

    /// <summary>Mehr Intents je Tick als vertraglich zulaessig.</summary>
    IntentLimitPerTick = 6,

    /// <summary>Mehr Intents insgesamt als vertraglich zulaessig.</summary>
    IntentLimitTotal = 7,

    /// <summary>Intenttick liegt ausserhalb des Messfensters [warmupTicks, horizonTicks).</summary>
    IntentOutsideWindow = 8,

    /// <summary>Skript ueberschreitet die Bytegrenze fuer untrusted Eingaben.</summary>
    ScriptTooLarge = 9,

    /// <summary>Inhalt vor dem Kopf, nach dem Abschluss oder fehlender Abschluss.</summary>
    TrailingContent = 10,
}

/// <summary>
/// Kontrollierte Ablehnung eines Eingabeskripts mit unterscheidbarer Klasse,
/// Zeilennummer und verstaendlicher Meldung (UF-001-Fehlerzeile). Sie verlaesst
/// den Parser, bevor irgendein Zustand oder Prozess beeinflusst wird
/// (Vertrauensgrenze NF-003).
/// </summary>
public sealed class InputScriptException : Exception
{
    public InputScriptException(InputScriptRejectReason reason, int lineNumber, string message)
        : base($"graybox-input-script: {reason} in Zeile {lineNumber}: {message}")
    {
        Reason = reason;
        LineNumber = lineNumber;
    }

    public InputScriptRejectReason Reason { get; }

    public int LineNumber { get; }
}

/// <summary>Fensterregeln der Validierung (aus Befehlsargumenten abgeleitet).</summary>
public readonly record struct ScriptWindowRules(int WarmupTicks, int HorizonTicks)
{
    public static ScriptWindowRules FromDefaults() =>
        new(SessionContract.DefaultWarmupTicks, SessionContract.DefaultHorizonTicks);
}

/// <summary>Ergebnis einer erfolgreichen Skriptanalyse.</summary>
public sealed record ParsedInputScript(
    GrayboxIntent[] Intents,
    int HorizonTicks,
    long WarmupTicks,
    string ScriptSha256Hex,
    ulong IntentPlanHash,
    string IntentPlanHashHex,
    string FormatId);

/// <summary>
/// Strenger Einzelpass-Parser des Diagnoseformats graybox-input-script-v1
/// (Kommandovertrag Abschnitt 5). Alle Eingaben sind untrusted: Groesse,
/// Syntax, Wertebereiche, Duplikate und Fensterzugehoerigkeit werden geprueft,
/// bevor ein Intent den Kern erreicht; Pfade oder Befehle werden niemals
/// ausgefuehrt.
/// </summary>
public static class InputScriptParser
{
    private const string HeaderPrefixV1 = "graybox-input-script-v1 ";
    private const string HeaderPrefixV2 = "graybox-input-script-v2 ";
    private const string HeaderPrefixV3 = "graybox-input-script-v3 ";
    private const string HeaderPrefixV4 = "graybox-input-script-v4 ";
    private const string EndLine = "end";

    public static ParsedInputScript Parse(byte[] rawBytes, ScriptWindowRules rules)
    {
        if ((long)rawBytes.Length > SessionContract.ScriptBytesMax)
        {
            throw new InputScriptException(
                InputScriptRejectReason.ScriptTooLarge,
                0,
                $"Skript ist {rawBytes.Length} Bytes; erlaubt sind hoechstens {SessionContract.ScriptBytesMax}.");
        }

        string content;

        try
        {
            content = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(rawBytes);
        }
        catch (DecoderFallbackException exception)
        {
            throw new InputScriptException(
                InputScriptRejectReason.HeaderMalformed,
                1,
                $"Skript ist kein gueltiges UTF-8 an Byte {exception.Index}: Das Format graybox-input-script-v1 verlangt UTF-8.");
        }

        return ParseCore(
            content,
            Convert.ToHexString(SHA256.HashData(rawBytes)).ToLowerInvariant(),
            rules);
    }

    private static ParsedInputScript ParseCore(string content, string scriptSha256Hex, ScriptWindowRules rules)
    {
        // Zeilenende-Normalisierung nur fuer die Analyse; der Rohbytehash
        // bleibt an die unveränderte Eingabe gebunden.
        var lines = content.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Split('\n');

        string headerPrefix;
        string formatId;
        var allowsModeActions = false;
        var allowsDecisionActions = false;
        var allowsRepeatAction = false;

        if (lines.Length > 0 && lines[0].StartsWith(HeaderPrefixV1, StringComparison.Ordinal))
        {
            headerPrefix = HeaderPrefixV1;
            formatId = SessionContract.ScriptFormatId;
        }
        else if (lines.Length > 0 && lines[0].StartsWith(HeaderPrefixV2, StringComparison.Ordinal))
        {
            // T-033: erweiterte Obermengengrammatik; die v1-Grammatik bleibt
            // unverändert (keine stille Formatdrift innerhalb einer Version).
            headerPrefix = HeaderPrefixV2;
            formatId = ModeContract.ScriptFormatIdV2;
            allowsModeActions = true;
        }
        else if (lines.Length > 0 && lines[0].StartsWith(HeaderPrefixV3, StringComparison.Ordinal))
        {
            // T-035: Entscheidungs-Obermengengrammatik; die v1-/v2-Grammatiken
            // bleiben byteidentisch (keine stille Formatdrift innerhalb einer
            // Version; choose-Aktionen unter einem v1-/v2-Kopf sind
            // UnknownAction).
            headerPrefix = HeaderPrefixV3;
            formatId = DecisionContract.ScriptFormatIdV3;
            allowsModeActions = true;
            allowsDecisionActions = true;
        }
        else if (lines.Length > 0 && lines[0].StartsWith(HeaderPrefixV4, StringComparison.Ordinal))
        {
            // T-039: Abschluss-Obermengengrammatik; die v1-/v2-/v3-Grammatiken
            // bleiben byteidentisch (keine stille Formatdrift innerhalb einer
            // Version; repeat-Aktionen unter einem v1-/v2-/v3-Kopf sind
            // UnknownAction).
            headerPrefix = HeaderPrefixV4;
            formatId = MissionContract.ScriptFormatIdV4;
            allowsModeActions = true;
            allowsDecisionActions = true;
            allowsRepeatAction = true;
        }
        else
        {
            throw new InputScriptException(InputScriptRejectReason.HeaderMalformed, 1, "Kopfzeile fehlt oder ist malformed.");
        }

        if (!TryParseInt32(lines[0].AsSpan(headerPrefix.Length), out var horizonTicks))
        {
            throw new InputScriptException(InputScriptRejectReason.HeaderMalformed, 1, "Horizont ist keine nichtnegative Ganzzahl.");
        }

        ValidateHorizon(horizonTicks, rules);
        var intents = new List<GrayboxIntent>(capacity: 64);
        var ended = false;

        for (var index = 1; index < lines.Length; index++)
        {
            var line = lines[index];
            var lineNumber = index + 1;

            if (line.Length == 0 && index == lines.Length - 1)
            {
                // Zulaessiges abschliessendes Zeilenende nach 'end'.
                continue;
            }

            if (ended)
            {
                throw new InputScriptException(
                    InputScriptRejectReason.TrailingContent,
                    lineNumber,
                    "Inhalt nach der Abschlusszeile 'end' ist unzulaessig.");
            }

            if (string.Equals(line, EndLine, StringComparison.Ordinal))
            {
                ended = true;
                continue;
            }

            if (line.Length == 0)
            {
                throw new InputScriptException(InputScriptRejectReason.LineMalformed, lineNumber, "Leerzeile im Skriptkoerper.");
            }

            intents.Add(ParseIntentLine(line, lineNumber, rules, allowsModeActions, allowsDecisionActions, allowsRepeatAction));
        }

        if (!ended)
        {
            throw new InputScriptException(
                InputScriptRejectReason.TrailingContent,
                lines.Length,
                "Abschlusszeile 'end' fehlt.");
        }

        if (intents.Count > SessionContract.TotalIntentsMax)
        {
            throw new InputScriptException(
                InputScriptRejectReason.IntentLimitTotal,
                0,
                $"Skript enthaelt {intents.Count} Intents; erlaubt sind hoechstens {SessionContract.TotalIntentsMax}.");
        }

        var perTickCounts = new Dictionary<int, int>();

        foreach (var intent in intents)
        {
            perTickCounts[intent.Tick] = perTickCounts.TryGetValue(intent.Tick, out var known)
                ? known + 1
                : 1;
        }

        foreach (var pair in perTickCounts)
        {
            if (pair.Value > SessionContract.IntentsPerTickMax)
            {
                throw new InputScriptException(
                    InputScriptRejectReason.IntentLimitPerTick,
                    0,
                    $"Tick {pair.Key} enthaelt {pair.Value} Intents; erlaubt sind hoechstens {SessionContract.IntentsPerTickMax}.");
            }
        }

        var ordered = intents.ToArray();
        Array.Sort(ordered);

        for (var index = 1; index < ordered.Length; index++)
        {
            if (ordered[index].Equals(ordered[index - 1]))
            {
                throw new InputScriptException(
                    InputScriptRejectReason.DuplicateIntent,
                    0,
                    $"Identischer Intent erscheint mehrfach in Tick {ordered[index].Tick}.");
            }
        }

        var planHash = IntentCodec.Hash(ordered);
        return new ParsedInputScript(
            Intents: ordered,
            HorizonTicks: horizonTicks,
            WarmupTicks: rules.WarmupTicks,
            ScriptSha256Hex: scriptSha256Hex,
            IntentPlanHash: planHash,
            IntentPlanHashHex: planHash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture),
            FormatId: formatId);
    }

    private static void ValidateHorizon(int horizonTicks, ScriptWindowRules rules)
    {
        // Der Skriptkopf bindet den Horizont; eine Abweichung vom erwarteten
        // Befehlswert ist ein kontrollierter Vertragswiderspruch statt stiller
        // Neuinterpretation.
        if (horizonTicks != rules.HorizonTicks)
        {
            throw new InputScriptException(
                InputScriptRejectReason.HeaderMalformed,
                1,
                $"Skriptkopf bindet Horizont {horizonTicks}, erwartet wird {rules.HorizonTicks}.");
        }

        if (horizonTicks > SessionContract.HorizonTicksMax)
        {
            throw new InputScriptException(
                InputScriptRejectReason.HeaderMalformed,
                1,
                $"Horizont {horizonTicks} ueberschreitet die Vertragsgrenze {SessionContract.HorizonTicksMax}.");
        }

        if (horizonTicks <= rules.WarmupTicks)
        {
            throw new InputScriptException(
                InputScriptRejectReason.HeaderMalformed,
                1,
                $"Horizont {horizonTicks} muss hinter dem Warm-up ({rules.WarmupTicks}) liegen.");
        }
    }

    private static GrayboxIntent ParseIntentLine(
        string line,
        int lineNumber,
        ScriptWindowRules rules,
        bool allowsModeActions,
        bool allowsDecisionActions,
        bool allowsRepeatAction)
    {
        var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length < 3 || !string.Equals(tokens[0], "intent", StringComparison.Ordinal))
        {
            throw new InputScriptException(
                InputScriptRejectReason.LineMalformed,
                lineNumber,
                "Zeile muss mit 'intent <tick> <aktion>' beginnen.");
        }

        if (!TryParseInt32(tokens[1], out var tick) || tick < 0)
        {
            throw new InputScriptException(InputScriptRejectReason.LineMalformed, lineNumber, "Tick ist keine nichtnegative Ganzzahl.");
        }

        if (tick < rules.WarmupTicks || tick >= rules.HorizonTicks)
        {
            throw new InputScriptException(
                InputScriptRejectReason.IntentOutsideWindow,
                lineNumber,
                $"Tick {tick} liegt ausserhalb des Messfensters [{rules.WarmupTicks}, {rules.HorizonTicks}).");
        }

        var action = tokens[2];

        return action switch
        {
            "clear" => BuildClear(tokens, lineNumber, tick),
            "point" => BuildPoint(tokens, lineNumber, tick),
            "box" => BuildBox(tokens, lineNumber, tick),
            "move" => BuildMove(tokens, lineNumber, tick),
            "steer" when allowsModeActions => BuildSteer(tokens, lineNumber, tick),
            "switch" when allowsModeActions => BuildSwitch(tokens, lineNumber, tick),
            "choose-a" when allowsDecisionActions => BuildChoose(tokens, lineNumber, tick, GrayboxIntentKind.ChooseA),
            "choose-b" when allowsDecisionActions => BuildChoose(tokens, lineNumber, tick, GrayboxIntentKind.ChooseB),
            "repeat" when allowsRepeatAction => BuildRepeat(tokens, lineNumber, tick),
            _ => throw new InputScriptException(
                InputScriptRejectReason.UnknownAction,
                lineNumber,
                $"Aktion '{action}' gehoert nicht zur Vertragsverbmenge dieses Formats."),
        };
    }

    /// <summary>
    /// Sitzungsseitige Wiederholen-Aktion ohne Parameter (T-039,
    /// Abschlussvertrag Abschnitt 3); kontextfrei grammatisch gültig, der
    /// abgeleitete Abschlusszustand entscheidet erst die Pipeline.
    /// </summary>
    private static GrayboxIntent BuildRepeat(string[] tokens, int lineNumber, int tick)
    {
        RequireTokenCount(tokens, 3, lineNumber);
        return new GrayboxIntent(tick, GrayboxIntentKind.RepeatMission);
    }

    /// <summary>
    /// Sitzungsseitige Entscheidungsaktion ohne Parameter (T-035,
    /// Entscheidungsvertrag Abschnitt 4); kontextfrei grammatisch gueltig,
    /// Angebot, Modus und Entscheidungsstand entscheidet erst die Pipeline.
    /// </summary>
    private static GrayboxIntent BuildChoose(string[] tokens, int lineNumber, int tick, GrayboxIntentKind kind)
    {
        RequireTokenCount(tokens, 3, lineNumber);
        return new GrayboxIntent(tick, kind);
    }

    /// <summary>
    /// Moduswechsel ohne Parameter; kontextfrei grammatisch gültig, der
    /// Moduskontext eines Ticks entscheidet erst die Pipeline (Modevertrag
    /// Abschnitt 6).
    /// </summary>
    private static GrayboxIntent BuildSwitch(string[] tokens, int lineNumber, int tick)
    {
        RequireTokenCount(tokens, 3, lineNumber);
        return new GrayboxIntent(tick, GrayboxIntentKind.SwitchMode);
    }

    /// <summary>
    /// Auswahlwiderruf ohne Parameter; die Tokenzahl wird wie bei allen
    /// Verben gegen die Vertragsgrammatik erzwungen, sodass Zusatztokens
    /// kontrolliert als <c>LineMalformed</c> abgewiesen werden.
    /// </summary>
    private static GrayboxIntent BuildClear(string[] tokens, int lineNumber, int tick)
    {
        RequireTokenCount(tokens, 3, lineNumber);
        return new GrayboxIntent(tick, GrayboxIntentKind.Clear);
    }

    private static GrayboxIntent BuildSteer(string[] tokens, int lineNumber, int tick)
    {
        RequireTokenCount(tokens, 4, lineNumber);

        if (!TryParseInt32(tokens[3], out var zone)
            || zone < 0
            || zone >= Riftward.Simulation.NavWorld.ZoneCount)
        {
            throw new InputScriptException(
                InputScriptRejectReason.RangeViolation,
                lineNumber,
                $"Zonenindex muss in [0, {Riftward.Simulation.NavWorld.ZoneCount - 1}] liegen.");
        }

        return new GrayboxIntent(tick, GrayboxIntentKind.SteerGroupToZone, zone);
    }

    private static GrayboxIntent BuildPoint(string[] tokens, int lineNumber, int tick)
    {
        RequireTokenCount(tokens, 5, lineNumber);

        var x = ParseMillimeters(tokens[3], lineNumber);
        var y = ParseMillimeters(tokens[4], lineNumber);
        ValidateCoordinate(x, SessionContract.WorldWidthMillimeters, lineNumber);
        ValidateCoordinate(y, SessionContract.WorldHeightMillimeters, lineNumber);
        return new GrayboxIntent(tick, GrayboxIntentKind.PointSelect, x, y);
    }

    private static GrayboxIntent BuildBox(string[] tokens, int lineNumber, int tick)
    {
        RequireTokenCount(tokens, 7, lineNumber);

        var x0 = ParseMillimeters(tokens[3], lineNumber);
        var y0 = ParseMillimeters(tokens[4], lineNumber);
        var x1 = ParseMillimeters(tokens[5], lineNumber);
        var y1 = ParseMillimeters(tokens[6], lineNumber);
        ValidateCoordinate(x0, SessionContract.WorldWidthMillimeters, lineNumber);
        ValidateCoordinate(y0, SessionContract.WorldHeightMillimeters, lineNumber);
        ValidateCoordinate(x1, SessionContract.WorldWidthMillimeters, lineNumber);
        ValidateCoordinate(y1, SessionContract.WorldHeightMillimeters, lineNumber);

        // Kanonisierung: Rechteck wird vor Auswertung auf min/max gebracht,
        // sodass Eckreihenfolge nie das Ergebnis bestimmt.
        return new GrayboxIntent(
            tick,
            GrayboxIntentKind.BoxSelect,
            Math.Min(x0, x1),
            Math.Min(y0, y1),
            Math.Max(x0, x1),
            Math.Max(y0, y1));
    }

    private static GrayboxIntent BuildMove(string[] tokens, int lineNumber, int tick)
    {
        RequireTokenCount(tokens, 4, lineNumber);

        if (!TryParseInt32(tokens[3], out var zone)
            || zone < 0
            || zone >= Riftward.Simulation.NavWorld.ZoneCount)
        {
            throw new InputScriptException(
                InputScriptRejectReason.RangeViolation,
                lineNumber,
                $"Zonenindex muss in [0, {Riftward.Simulation.NavWorld.ZoneCount - 1}] liegen.");
        }

        return new GrayboxIntent(tick, GrayboxIntentKind.GroupMoveToZone, zone);
    }

    private static void RequireTokenCount(string[] tokens, int expected, int lineNumber)
    {
        if (tokens.Length != expected)
        {
            throw new InputScriptException(
                InputScriptRejectReason.LineMalformed,
                lineNumber,
                $"Aktion erwartet {expected - 3} Parameter, erhalten {(tokens.Length > 3 ? tokens.Length - 3 : 0)}.");
        }
    }

    private static long ParseMillimeters(string token, int lineNumber)
    {
        if (!long.TryParse(token, System.Globalization.CultureInfo.InvariantCulture, out var value) || value < 0)
        {
            throw new InputScriptException(
                InputScriptRejectReason.LineMalformed,
                lineNumber,
                $"Koordinate '{token}' ist keine nichtnegative Ganzzahl in Millimetern.");
        }

        return value;
    }

    private static void ValidateCoordinate(long millimeters, long axisMaximum, int lineNumber)
    {
        if (millimeters > axisMaximum)
        {
            throw new InputScriptException(
                InputScriptRejectReason.RangeViolation,
                lineNumber,
                $"Koordinate {millimeters} mm liegt ausserhalb des Weltmasses ({axisMaximum} mm).");
        }
    }

    private static bool TryParseInt32(ReadOnlySpan<char> token, out int value) =>
        int.TryParse(token, System.Globalization.CultureInfo.InvariantCulture, out value)
        && value >= 0;
}
