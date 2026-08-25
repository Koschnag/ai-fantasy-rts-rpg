using System.IO;
using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Riftward.Simulation;

namespace Riftward.App.Soak;

/// <summary>
/// Versionierte Golden-Fixture der Zustands-Hashketten-Stichproben
/// (AC-T022-07). Sie entsteht aus einem unabhaengigen Referenzlauf ueber den
/// identischen skriptierten Plan und traegt Schema- und Contentkennung im
/// Dokument selbst. Der Soaklauf vergleicht seine eigenen Kettenstichproben
/// byteidentisch gegen diese Fixture; die Fixturehash-Bindung (SHA-256 der
/// Dateibytes) steht im Soakreport. Die Fixture begruendet keine
/// Save-/Replayformatfestlegung (interne Pruefinfrastruktur).
/// </summary>
public static class SoakChainFixture
{
    public const string Kind = "riftward-soak-chain-fixture-v1";
    public const int SchemaVersion = 1;

    /// <summary>Relativer Pfad der versionierten Fixture im Repository.</summary>
    public const string RepositoryPath = "src/Riftward.App/Soak/soak-replay-chain-v1.json";

    /// <summary>Eine einzelne Kettenstichprobe.</summary>
    public sealed record ChainSample(long Tick, ulong Hash);

    /// <summary>Geladene Fixture mit Dateihash-Bindung.</summary>
    public sealed class Loaded
    {
        public required string FilePath { get; init; }

        public required string Sha256 { get; init; }

        public required uint Seed { get; init; }

        public required long TickCount { get; init; }

        public required long SampleIntervalTicks { get; init; }

        public required string PlanHashHex { get; init; }

        public required IReadOnlyList<ChainSample> Samples { get; init; }
    }

    /// <summary>
    /// Laedt die Fixture fail-closed: fehlende, beschädigte, fremdversionierte
    /// oder vertragswidrige Fixtures werden mit einer verstaendlichen Meldung
    /// abgelehnt statt still ignoriert.
    /// </summary>
    public static Loaded Load(string? explicitPath = null)
    {
        var path = ResolvePath(explicitPath);

        if (path is null)
        {
            throw new FileNotFoundException(
                $"Soak-Golden-Fixture nicht gefunden ({RepositoryPath}); Referenzlauf erforderlich.");
        }

        byte[] bytes;

        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (IOException exception)
        {
            throw new IOException($"Soak-Golden-Fixture nicht lesbar ({path}): {exception.Message}", exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw new IOException($"Soak-Golden-Fixture nicht lesbar ({path}): {exception.Message}", exception);
        }

        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));

        JsonDocument document;

        try
        {
            document = JsonDocument.Parse(bytes);
        }
        catch (JsonException exception)
        {
            throw new FormatException($"Soak-Golden-Fixture ist kein gueltiges JSON ({path}): {exception.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            RequireExactProperties(
                root,
                "$",
                "schemaVersion",
                "kind",
                "scenarioId",
                "seed",
                "tickCount",
                "sampleIntervalTicks",
                "simulationContract",
                "planHash",
                "samples");

            var schemaVersionElement = RequireKind(root, "schemaVersion", JsonValueKind.Number);
            if (!schemaVersionElement.TryGetInt64(out var schemaVersion) || schemaVersion != SchemaVersion)
            {
                throw new FormatException($"Soak-Golden-Fixture hat Fremdschemaversion {schemaVersion} (erwartet {SchemaVersion}).");
            }

            if (!string.Equals(RequireString(root, "kind"), Kind, StringComparison.Ordinal))
            {
                throw new FormatException("Soak-Golden-Fixture hat fremde Contentkennung (kind).");
            }

            if (!string.Equals(RequireString(root, "scenarioId"), SoakScenarios.Replay, StringComparison.Ordinal))
            {
                throw new FormatException("Soak-Golden-Fixture gehoert zu einem anderen Szenario.");
            }

            var contractElement = RequireKind(root, "simulationContract", JsonValueKind.Object);
            RequireExactProperties(
                contractElement,
                "$.simulationContract",
                "document",
                "version",
                "hashAlgorithm",
                "commandPlanAlgorithm",
                "worldId");

            if (!string.Equals(
                RequireString(contractElement, "document"),
                SimulationContract.DocumentPath,
                StringComparison.Ordinal))
            {
                throw new FormatException("Soak-Golden-Fixture bindet ein fremdes Simulationsvertragsdokument.");
            }

            if (!string.Equals(RequireString(contractElement, "hashAlgorithm"), SimulationContract.HashAlgorithmId, StringComparison.Ordinal))
            {
                throw new FormatException("Soak-Golden-Fixture nutzt einen anderen Hashvertrag.");
            }

            if (!string.Equals(RequireString(contractElement, "commandPlanAlgorithm"), SimulationContract.CommandPlanAlgorithmId, StringComparison.Ordinal))
            {
                throw new FormatException("Soak-Golden-Fixture nutzt einen anderen Befehlsplanvertrag.");
            }

            if (!string.Equals(RequireString(contractElement, "worldId"), SimulationContract.WorldId, StringComparison.Ordinal))
            {
                throw new FormatException("Soak-Golden-Fixture gehoert zu einer anderen Welt.");
            }

            if (!string.Equals(RequireString(contractElement, "version"), SimulationContract.ContractVersion, StringComparison.Ordinal))
            {
                throw new FormatException("Soak-Golden-Fixture bindet eine fremde Simulationsvertragsversion.");
            }

            var seedElement = RequireKind(root, "seed", JsonValueKind.Number);
            if (!seedElement.TryGetUInt32(out var seed))
            {
                throw new FormatException("Soak-Golden-Fixture: Seed unlesbar.");
            }

            if (seed != SoakContract.DefaultSeed)
            {
                throw new FormatException($"Soak-Golden-Fixture bindet einen fremden Seed ({seed}).");
            }

            var tickCountElement = RequireKind(root, "tickCount", JsonValueKind.Number);
            if (!tickCountElement.TryGetInt64(out var tickCount) || tickCount != SoakPlan.TotalSimulationTick)
            {
                throw new FormatException($"Soak-Golden-Fixture bindet einen fremden Horizont ({tickCount}).");
            }

            var intervalElement = RequireKind(root, "sampleIntervalTicks", JsonValueKind.Number);
            if (!intervalElement.TryGetInt64(out var sampleInterval) || sampleInterval != SoakPlan.HashSampleIntervalTicks)
            {
                throw new FormatException("Soak-Golden-Fixture nutzt ein fremdes Kettenintervall.");
            }

            var planHash = RequireString(root, "planHash");
            ValidateHex16(planHash, "planHash");

            var expectedPlanHash = CommandPlan
                .Hash(CommandPlan.Generate(seed, checked((int)tickCount)))
                .ToString("x16", CultureInfo.InvariantCulture);

            if (!string.Equals(planHash, expectedPlanHash, StringComparison.Ordinal))
            {
                throw new FormatException("Soak-Golden-Fixture bindet einen fremden Befehlsplan.");
            }

            var samplesElement = RequireKind(root, "samples", JsonValueKind.Array);
            var samples = new List<ChainSample>();

            foreach (var sample in samplesElement.EnumerateArray())
            {
                RequireExactProperties(sample, "$.samples[]", "tick", "hash");
                var tickElement = RequireKind(sample, "tick", JsonValueKind.Number);
                if (!tickElement.TryGetInt64(out var tick) || tick < 0 || tick > tickCount)
                {
                    throw new FormatException("Soak-Golden-Fixture: ungueltiger Stichprobentick.");
                }

                var hashValue = RequireString(sample, "hash");
                ValidateHex16(hashValue, "sample.hash");
                samples.Add(new ChainSample(tick, Convert.ToUInt64(hashValue, 16)));
            }

            var scheduledTicks = SoakPlan.ChainSchedule(tickCount, sampleInterval);
            var expectedSampleCount = checked(scheduledTicks.Length + 1);

            if (samples.Count != expectedSampleCount)
            {
                throw new FormatException(
                    $"Soak-Golden-Fixture benoetigt den vollstaendigen kanonischen Stichprobenplan ({expectedSampleCount} Eintraege)."
                );
            }

            if (samples[0].Tick != 0)
            {
                throw new FormatException("Soak-Golden-Fixture muss bei Tick 0 beginnen.");
            }

            for (var index = 0; index < scheduledTicks.Length; index++)
            {
                if (samples[index + 1].Tick != scheduledTicks[index])
                {
                    throw new FormatException(
                        "Soak-Golden-Fixture: Stichprobenticks entsprechen nicht dem kanonischen Stichprobenplan."
                    );
                }
            }

            return new Loaded
            {
                FilePath = path,
                Sha256 = sha256,
                Seed = seed,
                TickCount = tickCount,
                SampleIntervalTicks = sampleInterval,
                PlanHashHex = planHash,
                Samples = samples,
            };
        }
    }

    public sealed class FixtureModel
    {
        public const int FixtureSchemaVersion = SoakChainFixture.SchemaVersion;

        public const string FixtureKind = SoakChainFixture.Kind;

        public int SchemaVersion { get; set; } = FixtureSchemaVersion;

        public string Kind { get; set; } = FixtureKind;

        public string ScenarioId { get; set; } = SoakScenarios.Replay;

        public uint Seed { get; set; }

        public long TickCount { get; set; }

        public long SampleIntervalTicks { get; set; }

        public string HashAlgorithm { get; set; } = SimulationContract.HashAlgorithmId;

        public string CommandPlanAlgorithm { get; set; } = SimulationContract.CommandPlanAlgorithmId;

        public string WorldId { get; set; } = SimulationContract.WorldId;

        public string SimulationContractDocument { get; set; } = SimulationContract.DocumentPath;

        public string SimulationContractVersion { get; set; } = SimulationContract.ContractVersion;

        public string PlanHashHex { get; set; } = string.Empty;

        public List<FixtureSample> Samples { get; set; } = [];
    }

    public sealed record FixtureSample(long Tick, string Hash);

    private static readonly JsonSerializerOptions CanonicalFixtureOptions = new() { WriteIndented = true };

    /// <summary>Schreibt die Fixture kanonisch formatiert (eine Zeile je Eintrag).</summary>
    public static string Serialize(FixtureModel model)
    {
        return JsonSerializer.Serialize(
            new
            {
                schemaVersion = model.SchemaVersion,
                kind = model.Kind,
                scenarioId = model.ScenarioId,
                seed = model.Seed,
                tickCount = model.TickCount,
                sampleIntervalTicks = model.SampleIntervalTicks,
                simulationContract = new
                {
                    document = model.SimulationContractDocument,
                    version = model.SimulationContractVersion,
                    hashAlgorithm = model.HashAlgorithm,
                    commandPlanAlgorithm = model.CommandPlanAlgorithm,
                    worldId = model.WorldId,
                },
                planHash = model.PlanHashHex,
                samples = model.Samples.Select(sample => new { tick = sample.Tick, hash = sample.Hash }),
            },
            CanonicalFixtureOptions) + "\n";
    }

    private static JsonElement Require(JsonElement parent, string name)
    {
        if (parent.ValueKind != JsonValueKind.Object || !parent.TryGetProperty(name, out var value))
        {
            throw new FormatException($"Soak-Golden-Fixture: Pflichtfeld '{name}' fehlt.");
        }

        return value;
    }

    private static JsonElement RequireKind(JsonElement parent, string name, JsonValueKind expectedKind)
    {
        var value = Require(parent, name);

        if (value.ValueKind != expectedKind)
        {
            throw new FormatException(
                $"Soak-Golden-Fixture: Pflichtfeld '{name}' hat Typ {value.ValueKind}, erwartet {expectedKind}."
            );
        }

        return value;
    }

    private static string RequireString(JsonElement parent, string name) =>
        RequireKind(parent, name, JsonValueKind.String).GetString()
        ?? throw new FormatException($"Soak-Golden-Fixture: Pflichtfeld '{name}' ist leer.");

    private static void RequireExactProperties(JsonElement element, string path, params string[] expectedNames)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException($"Soak-Golden-Fixture: '{path}' muss ein Objekt sein.");
        }

        var expected = new HashSet<string>(expectedNames, StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var property in element.EnumerateObject())
        {
            if (!seen.Add(property.Name))
            {
                throw new FormatException(
                    $"Soak-Golden-Fixture: doppeltes Feld '{property.Name}' unter '{path}'."
                );
            }

            if (!expected.Remove(property.Name))
            {
                throw new FormatException(
                    $"Soak-Golden-Fixture: unbekanntes Feld '{property.Name}' unter '{path}'."
                );
            }
        }

        if (expected.Count > 0)
        {
            throw new FormatException(
                $"Soak-Golden-Fixture: Pflichtfeld '{expected.Order(StringComparer.Ordinal).First()}' fehlt unter '{path}'."
            );
        }
    }

    private static void ValidateHex16(string value, string field)
    {
        if (value.Length != 16 || !value.All(IsLowerHexDigit))
        {
            throw new FormatException($"Soak-Golden-Fixture: {field} ist kein 16-stelliger Kleinbuchstaben-Hexwert.");
        }
    }

    private static bool IsLowerHexDigit(char character) =>
        character is >= '0' and <= '9' or >= 'a' and <= 'f';

    /// <summary>
    /// Löst den Fixturepfad auf: erst explizit, dann Ausgabeverzeichnis des
    /// Hosts, dann das Repositorywurzelverzeichnis (erkennbar an
    /// <c>Riftward.slnx</c>). Rein lesend und ohne Netzwerk.
    /// </summary>
    public static string? ResolvePath(string? explicitPath)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return File.Exists(explicitPath) ? explicitPath : null;
        }

        var baseDirectory = AppContext.BaseDirectory;
        var candidate = Path.Combine(baseDirectory, "soak-replay-chain-v1.json");

        if (File.Exists(candidate))
        {
            return candidate;
        }

        var current = new DirectoryInfo(baseDirectory);

        while (current is not null)
        {
            candidate = Path.Combine(current.FullName, RepositoryPath.Replace('/', Path.DirectorySeparatorChar));

            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }
}
