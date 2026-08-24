using System.Text.Json;
using Riftward.Platform;

namespace Riftward.App.Bench;

/// <summary>Vertragsoffene Beschreibung eines Reportknotens (closed shape).</summary>
internal abstract class ReportNode
{
    public abstract void Check(string path, JsonElement element, List<string> errors);
}

internal sealed class RObj : ReportNode
{
    private readonly (string Name, ReportNode Node)[] _fields;

    public RObj(params (string Name, ReportNode Node)[] fields) => _fields = fields;

    public IReadOnlyList<(string Name, ReportNode Node)> Fields => _fields;

    public override void Check(string path, JsonElement element, List<string> errors)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add($"{path}: Objekt erwartet.");
            return;
        }

        var seen = new HashSet<string>();

        foreach (var property in element.EnumerateObject())
        {
            var match = _fields.FirstOrDefault(field => field.Name == property.Name);

            if (match.Node is null)
            {
                errors.Add($"{path}.{property.Name}: unbekanntes Feld.");
                continue;
            }

            seen.Add(property.Name);
            match.Node.Check($"{path}.{property.Name}", property.Value, errors);
        }

        foreach (var (name, _) in _fields)
        {
            if (!seen.Contains(name))
            {
                errors.Add($"{path}.{name}: Pflichtfeld fehlt.");
            }
        }
    }
}

internal sealed class RArr : ReportNode
{
    private readonly ReportNode _item;
    private readonly int _minItems;

    public RArr(ReportNode item, int minItems = 0) => (_item, _minItems) = (item, minItems);

    public override void Check(string path, JsonElement element, List<string> errors)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            errors.Add($"{path}: Array erwartet.");
            return;
        }

        var count = 0;

        foreach (var item in element.EnumerateArray())
        {
            _item.Check($"{path}[{count}]", item, errors);
            count++;
        }

        if (count < _minItems)
        {
            errors.Add($"{path}: mindestens {_minItems} Eintraege erwartet.");
        }
    }
}

internal sealed class RStr : ReportNode
{
    public override void Check(string path, JsonElement element, List<string> errors)
    {
        if (element.ValueKind != JsonValueKind.String || element.GetString()?.Length == 0)
        {
            errors.Add($"{path}: nichtleere Zeichenkette erwartet.");
        }
    }
}

internal sealed class RInt : ReportNode
{
    private readonly long _min;
    private readonly long _max;

    public RInt(long min = long.MinValue, long max = long.MaxValue) => (_min, _max) = (min, max);

    public override void Check(string path, JsonElement element, List<string> errors)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetInt64(out var value))
        {
            errors.Add($"{path}: ganzzahliger Wert erwartet.");
            return;
        }

        if (value < _min || value > _max)
        {
            errors.Add($"{path}: Wert {value} ausserhalb [{_min}, {_max}].");
        }
    }
}

internal sealed class RNum : ReportNode
{
    private readonly bool _nonNegative;

    public RNum(bool nonNegative = false) => _nonNegative = nonNegative;

    public override void Check(string path, JsonElement element, List<string> errors)
    {
        if (element.ValueKind != JsonValueKind.Number || !element.TryGetDouble(out var value))
        {
            errors.Add($"{path}: numerischer Wert erwartet.");
            return;
        }

        if ((_nonNegative && value < 0) || double.IsNaN(value) || double.IsInfinity(value))
        {
            errors.Add($"{path}: endlicher, nichtnegativer Wert erwartet.");
        }
    }
}

internal sealed class RNullableStr : ReportNode
{
    public override void Check(string path, JsonElement element, List<string> errors)
    {
        if (element.ValueKind is JsonValueKind.Null or JsonValueKind.String)
        {
            return;
        }

        errors.Add($"{path}: Zeichenkette oder null erwartet.");
    }
}

internal sealed class RBool : ReportNode
{
    private readonly bool? _expected;

    public RBool(bool? expected = null) => _expected = expected;

    public override void Check(string path, JsonElement element, List<string> errors)
    {
        if (element.ValueKind != JsonValueKind.True && element.ValueKind != JsonValueKind.False)
        {
            errors.Add($"{path}: boolescher Wert erwartet.");
            return;
        }

        if (_expected is { } expected && element.GetBoolean() != expected)
        {
            errors.Add($"{path}: erwarteter Wert {expected.ToString().ToLowerInvariant()}.");
        }
    }
}

internal sealed class RLit : ReportNode
{
    private readonly string _literal;

    public RLit(string literal) => _literal = literal;

    public override void Check(string path, JsonElement element, List<string> errors)
    {
        if (element.ValueKind != JsonValueKind.String || !string.Equals(element.GetString(), _literal, StringComparison.Ordinal))
        {
            errors.Add($"{path}: konstanter Wert '{_literal}' erwartet.");
        }
    }
}

/// <summary>Kennzahl mit Pflicht-Einheit und -Methodenkennung plus Werten.</summary>
internal static class RMetric
{
    /// <summary>Numerische Kennzahl mit unit/method und weiteren Zahlenfeldern.</summary>
    public static RObj Numeric(bool nonNegative, params (string Name, ReportNode Node)[] values)
    {
        var fields = new List<(string Name, ReportNode Node)>
        {
            ("unit", new RStr()),
            ("method", new RStr()),
        };
        fields.AddRange(values.Select(value => (value.Name, value.Node)));
        return new RObj(fields.ToArray());
    }

    /// <summary>
    /// Messwert mit Methodenkennung oder ausdruecklicher unavailable-Kennzeichnung
    /// mit maschinenlesbarem Grund; beides gleichzeitig ist unzulaessig.
    /// </summary>
    public static ReportNode Measurable(string firstName, ReportNode firstField, string secondName, ReportNode secondField) =>
        new MeasuredAlternative(
            new RObj(
                ("measured", new RBool(true)),
                ("unit", new RStr()),
                ("method", new RStr()),
                (firstName, firstField),
                (secondName, secondField)),
            new RObj(
                ("measured", new RBool(false)),
                ("reason", new RStr())));

    private sealed class MeasuredAlternative(RObj measuredShape, RObj unavailableShape) : ReportNode
    {
        public override void Check(string path, JsonElement element, List<string> errors)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                errors.Add($"{path}: Objekt erwartet.");
                return;
            }

            if (!element.TryGetProperty("measured", out var flag)
                || flag.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            {
                errors.Add($"{path}.measured: boolesche Messkennung erwartet.");
                return;
            }

            if (flag.GetBoolean())
            {
                measuredShape.Check(path, element, errors);
            }
            else
            {
                unavailableShape.Check(path, element, errors);
            }
        }
    }
}

/// <summary>
/// Maschinenpruefbarer Evidenzvertrag des BENCH-EMPTY-Reports (AC-T020-02).
/// Die Pruefung ist fail-closed: fehlende Pflichtfelder, falsche Typen,
/// erfundene Kennzahlen ohne Methodenkennung und nicht begruendete
/// unavailable-Kennzeichnungen lassen die Pruefung fehlschlagen; unbekannte
/// Felder werden abgelehnt.
/// </summary>
public static class BenchReportSchema
{
    public const int CurrentVersion = 1;
    public const string ModeBench = "bench";

    /// <summary>Gesamtschema des von BenchRunner geschriebenen Reports.</summary>
    internal static RObj Root { get; } = BuildRoot();

    private static RObj BuildRoot() => new(
        ("schemaVersion", new RInt(CurrentVersion, CurrentVersion)),
        ("mode", new RLit(ModeBench)),
        ("command", new RStr()),
        ("scenario", new RObj(
            ("id", new RLit(BenchScenarios.Empty)),
            ("seed", new RInt(0, uint.MaxValue)),
            ("resolution", new RObj(
                ("width", new RInt(1, ushort.MaxValue)),
                ("height", new RInt(1, ushort.MaxValue)))),
            ("displayProfile", new RLit("low")),
            ("vsync", new RBool(true)),
            ("content", new RLit("clear-pass-plus-technical-test-pattern")))),
        ("cameraPath", new RObj(
            ("algorithm", new RLit(CameraFlight.AlgorithmId)),
            ("samples", new RInt(1)),
            ("hash", new RStr()),
            ("firstSample", new RObj(
                ("frameIndex", new RInt(0)),
                ("yawDegrees", new RStr()),
                ("pitchDegrees", new RStr()),
                ("radiusMeters", new RStr()))))),
        ("environment", new RObj(
            ("os", new RObj(("type", new RStr()), ("kernelRelease", new RStr()))),
            ("cpu", new RObj(("model", new RStr()))),
            ("gpu", new RObj(("renderer", new RStr()), ("vendorId", new RInt(0)), ("deviceId", new RInt(0)))),
            ("gl", new RObj(("version", new RStr()))),
            ("backend", new RObj(
                ("name", new RLit("OpenGL")),
                ("id", new RInt(BgfxDevice.RendererOpenGL, BgfxDevice.RendererOpenGL)),
                ("profile", new RLit("3.3 Core")),
                ("vsync", new RBool(true)))),
            ("rid", new RLit("linux-x64")),
            ("commit", new RStr()),
            ("buildMode", new RStr()),
            ("pins", new RArr(new RObj(
                ("id", new RStr()),
                ("refType", new RStr()),
                ("ref", new RStr()),
                ("commit", new RStr()),
                ("sourceSha256", new RStr()),
                ("licenseSpdx", new RStr())), 4)))),
        ("measurement", new RObj(
            ("warmupFrames", new RInt(1)),
            ("sampleFrames", new RInt(1)),
            ("framesRendered", new RInt(2)),
            ("rssSampleIntervalFrames", new RInt(1)))),
        ("metrics", Metrics()),
        ("gate", new RObj(
            ("limits", new RObj(
                ("p99FrameTimeMsMax", new RNum(true)),
                ("managedAllocationsPerWarmFrameBytesMax", new RNum(true)),
                ("drawSubmitCallsPerFrameMax", new RInt(0)),
                ("runtimeShaderCompilationAllowed", new RBool(false)),
                ("rssTargetMiB", new RInt(1)),
                ("rssHardLimitMiB", new RInt(1)))),
            ("pass", new RBool()),
            ("rssTargetMet", new RBool()),
            ("violations", new RArr(new RStr())))),
        ("profiles", new RArr(new RObj(
            ("id", new RStr()),
            ("status", new RStr()),
            ("boundReferenceClass", new RNullableStr()),
            ("reason", new RStr())), 3)),
        ("baseline", new RObj(
            ("classification", new RLit("diagnostic-developer-workstation")),
            ("protocol", new RLit("qops001-2026-08-24")))),
        ("startedAtUtc", new RStr()),
        ("finishedAtUtc", new RStr()),
        ("exitCode", new RInt(int.MinValue, int.MaxValue)));

    private static RObj Metrics() => new(
        ("frameTimeMs", RMetric.Numeric(true,
            ("p50", new RNum(true)), ("p95", new RNum(true)), ("p99", new RNum(true)))),
        ("managedAllocationsBytes", RMetric.Numeric(true,
            ("perWarmFrame", new RNum(true)))),
        ("gcPauseSumMs", RMetric.Numeric(true,
            ("value", new RNum(true)))),
        ("gcPauseCount", RMetric.Numeric(true,
            ("value", new RInt(0)))),
        ("workingSetKiB", RMetric.Numeric(true,
            ("min", new RNum(true)), ("max", new RNum(true)), ("end", new RNum(true)))),
        ("drawSubmitCallsPerFrame", RMetric.Numeric(true,
            ("value", new RInt(0)))),
        ("visibleTrianglesPerFrame", RMetric.Numeric(true,
            ("value", new RInt(0)))),
        ("gpuTimeMs", RMetric.Measurable("p99", new RNum(true), "timerFreqHz", new RInt(0))),
        ("vramBytes", RMetric.Measurable("value", new RNum(true), "textureMemoryUsed", new RInt(0))),
        ("runtimeShaderCompilation", RMetric.Numeric(true,
            ("value", new RBool(false)))));

    /// <summary>Prueft einen Reporttext; Rueckgabe ist die Fehlerliste (leer == gueltig).</summary>
    public static IReadOnlyList<string> Validate(string json)
    {
        var errors = new List<string>();
        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            return [$"Report ist kein gueltiges JSON: {exception.Message}"];
        }

        using (document)
        {
            Root.Check("$", document.RootElement, errors);
        }

        return errors;
    }

    /// <summary>Vergleicht zwei Reports auf Strukturgleichheit (AC-T020-03); Messwerte duerfen variieren.</summary>
    public static IReadOnlyList<string> StructureDifferences(string leftJson, string rightJson)
    {
        using var left = JsonDocument.Parse(leftJson);
        using var right = JsonDocument.Parse(rightJson);
        var differences = new List<string>();
        CollectStructure("$", left.RootElement, right.RootElement, differences);
        return differences;
    }

    private static void CollectStructure(string path, JsonElement left, JsonElement right, List<string> differences)
    {
        switch (left.ValueKind, right.ValueKind)
        {
            case (JsonValueKind.Object, JsonValueKind.Object):
                {
                    var leftNames = left.EnumerateObject().Select(property => property.Name).ToHashSet();
                    var rightNames = right.EnumerateObject().Select(property => property.Name).ToHashSet();

                    foreach (var missing in leftNames.Except(rightNames))
                    {
                        differences.Add($"{path}.{missing}: fehlt im zweiten Report.");
                    }

                    foreach (var additional in rightNames.Except(leftNames))
                    {
                        differences.Add($"{path}.{additional}: nur im zweiten Report.");
                    }

                    foreach (var property in left.EnumerateObject())
                    {
                        if (right.TryGetProperty(property.Name, out var counterpart))
                        {
                            CollectStructure($"{path}.{property.Name}", property.Value, counterpart, differences);
                        }
                    }

                    break;
                }

            case (JsonValueKind.Array, JsonValueKind.Array):
                {
                    var leftItems = left.EnumerateArray().ToArray();
                    var rightItems = right.EnumerateArray().ToArray();

                    if (leftItems.Length != rightItems.Length)
                    {
                        differences.Add($"{path}: Laenge {leftItems.Length} != {rightItems.Length}.");
                        break;
                    }

                    for (var index = 0; index < leftItems.Length; index++)
                    {
                        CollectStructure($"{path}[{index}]", leftItems[index], rightItems[index], differences);
                    }

                    break;
                }

            default:
                if (left.ValueKind != right.ValueKind)
                {
                    differences.Add($"{path}: Typabweichung {left.ValueKind} != {right.ValueKind}.");
                }

                break;
        }
    }
}
