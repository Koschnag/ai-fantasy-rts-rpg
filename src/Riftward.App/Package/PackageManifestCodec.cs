using System.Security.Cryptography;
using System.Text.Json;

namespace Riftward.App.Package;

/// <summary>Eintragstyp des Paketmanifests und der Archivierung.</summary>
public enum PackageEntryKind
{
    /// <summary>Reguläre Datei mit SHA-256- und Bytebindung.</summary>
    File,

    /// <summary>Symlink mit exakt einem gebundenen Ziel.</summary>
    Symlink,

    /// <summary>Verzeichnis (nur archivintern; Verzeichnisse werden nie manifestiert).</summary>
    Directory,
}

/// <summary>Ein manifestierter Paketinhalt.</summary>
public sealed record PackageEntry(
    string Path,
    PackageEntryKind Kind,
    string? Sha256,
    long? Bytes,
    string? LinkTarget,
    string UnixMode);

/// <summary>Quellbindung des Pakets.</summary>
public sealed record PackageSourceBinding(string CommitSha256, string TreeSha256, long SourceDateEpoch);

/// <summary>Gebundener Schutzmechanismus über die bestehende Host-Artefaktprüfung.</summary>
public sealed record PackageProtection(string Kind, string ArtifactsDir, string ManifestPath, IReadOnlyList<int> RejectsExitCodes);

/// <summary>Artefaktmanifestbindung im Paketmanifest.</summary>
public sealed record PackageArtifactManifestBinding(string Path, string Sha256, string PinCohort);

/// <summary>Kopfdaten des Pakets.</summary>
public sealed record PackageHeader(
    string Id,
    string Version,
    string Rid,
    string RuntimeForm,
    string AlphaMarker);

/// <summary>Vollständiges Paketmanifest.</summary>
public sealed record PackageManifest(
    PackageHeader Package,
    PackageSourceBinding Source,
    PackageArtifactManifestBinding ArtifactManifest,
    PackageProtection Protection,
    IReadOnlyList<PackageEntry> Entries)
{
    /// <summary>Erwarteter Wurzelverzeichnisname aus der Version (sichere Entpackform).</summary>
    public string RootDirectoryName => PackageContract.ArchiveRootPrefix + Package.Version + PackageContract.ArchiveRootSuffix;
}

/// <summary>
/// Kanonischer Schreiber und Parser des Paketmanifests. Das JSON ist
/// deterministisch: sortierte Pfade, feste Feldreihenfolge, einzeilige
/// UTF-8-Kodierung ohne BOM.
/// </summary>
public static class PackageManifestCodec
{
    private const long MaxManifestBytes = 4_194_304;

    /// <summary>Berechnet den SHA-256 einer Datei (kleingeschrieben, hex).</summary>
    public static string Sha256File(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    /// <summary>Ordnet die Einträge vertraglich (Pfad, ordinal, strikt aufsteigend).</summary>
    public static IReadOnlyList<PackageEntry> Sort(IReadOnlyList<PackageEntry> entries) =>
        entries.OrderBy(static entry => entry.Path, StringComparer.Ordinal).ToArray();

    /// <summary>Schreibt das Manifest kanonisch in eine Datei und liefert den SHA-256.</summary>
    public static string Write(string targetPath, PackageManifest manifest)
    {
        var bytes = Encode(manifest);
        File.WriteAllBytes(targetPath, bytes);
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }

    /// <summary>Kanonische Manifestbytes (einzeilig, UTF-8, keine BOM).</summary>
    public static byte[] Encode(PackageManifest manifest)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", PackageContract.SchemaVersion);
            writer.WriteString("contract", PackageContract.ContractId);

            writer.WriteStartObject("package");
            writer.WriteString("id", manifest.Package.Id);
            writer.WriteString("version", manifest.Package.Version);
            writer.WriteString("rid", manifest.Package.Rid);
            writer.WriteString("runtimeForm", manifest.Package.RuntimeForm);
            writer.WriteString("alphaMarker", manifest.Package.AlphaMarker);
            writer.WriteEndObject();

            writer.WriteStartObject("source");
            writer.WriteString("commitSha256", manifest.Source.CommitSha256);
            writer.WriteString("treeSha256", manifest.Source.TreeSha256);
            writer.WriteNumber("sourceDateEpoch", manifest.Source.SourceDateEpoch);
            writer.WriteEndObject();

            writer.WriteStartObject("artifactManifest");
            writer.WriteString("path", manifest.ArtifactManifest.Path);
            writer.WriteString("sha256", manifest.ArtifactManifest.Sha256);
            writer.WriteString("pinCohort", manifest.ArtifactManifest.PinCohort);
            writer.WriteEndObject();

            writer.WriteStartObject("protection");
            writer.WriteString("kind", manifest.Protection.Kind);
            writer.WriteString("artifactsDir", manifest.Protection.ArtifactsDir);
            writer.WriteString("manifestPath", manifest.Protection.ManifestPath);
            writer.WriteStartArray("rejectsExitCodes");
            foreach (var code in manifest.Protection.RejectsExitCodes)
            {
                writer.WriteNumberValue(code);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteStartArray("entries");
            foreach (var entry in manifest.Entries)
            {
                writer.WriteStartObject();
                writer.WriteString("path", entry.Path);
                writer.WriteString("type", entry.Kind == PackageEntryKind.File ? "file" : "symlink");
                if (entry.Kind == PackageEntryKind.File)
                {
                    writer.WriteString("sha256", entry.Sha256);
                    writer.WriteNumber("bytes", entry.Bytes!.Value);
                }
                else
                {
                    writer.WriteString("target", entry.LinkTarget);
                }

                writer.WriteString("unixMode", entry.UnixMode);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// Liest ein Paketmanifest strikt ein; Verletzungen der Vertragsform
    /// (Felder, Sortierung, Pfadsicherheit, Hashform) werfen eine
    /// <see cref="PackageVerificationException"/> mit unterscheidbarer Klasse.
    /// </summary>
    public static PackageManifest Parse(string path)
    {
        if (!File.Exists(path))
        {
            throw new PackageVerificationException(
                PackageViolationClass.ManifestMissing,
                PackageContract.ManifestFileName,
                "Paketmanifest fehlt.");
        }

        var info = new FileInfo(path);
        if (info.Length is 0 or > MaxManifestBytes)
        {
            throw new PackageVerificationException(
                PackageViolationClass.ManifestMalformed,
                PackageContract.ManifestFileName,
                $"Unzulaessige Manifestgroesse ({info.Length} Bytes).");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException exception)
        {
            throw new PackageVerificationException(
                PackageViolationClass.ManifestMalformed,
                PackageContract.ManifestFileName,
                $"Manifest ist kein gueltiges JSON: {exception.Message}");
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                throw Malformed("Manifestwurzel ist kein Objekt.");
            }

            if (!root.TryGetProperty("schemaVersion", out var schemaVersion) || schemaVersion.GetInt32() != PackageContract.SchemaVersion)
            {
                throw Malformed("schemaVersion fehlt oder widerspricht dem Vertrag.");
            }

            if (!root.TryGetProperty("contract", out var contract) || contract.GetString() != PackageContract.ContractId)
            {
                throw Malformed("Vertragskennung fehlt oder weicht ab.");
            }

            var package = ParseHeader(root);
            var source = ParseSource(root);
            var artifactManifest = ParseArtifactManifest(root);
            var protection = ParseProtection(root);
            var entries = ParseEntries(root);

            return new PackageManifest(package, source, artifactManifest, protection, entries);
        }
    }

    /// <summary>Prüft einen Manifestpfad gegen Pfadsicherheitsregeln (POSIX, relativ, ohne ..).</summary>
    public static bool IsSafeRelativePath(string relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath)
            || relativePath.Contains('\\', StringComparison.Ordinal)
            || relativePath.StartsWith('/', StringComparison.Ordinal)
            || relativePath.EndsWith('/', StringComparison.Ordinal)
            || relativePath.Split('/').Any(static segment => segment is ".." or "."))
        {
            return false;
        }

        return relativePath.Split('/').All(static segment => segment.Length > 0);
    }

    private static PackageVerificationException Malformed(string detail) =>
        new(PackageViolationClass.ManifestMalformed, PackageContract.ManifestFileName, detail);

    private static PackageHeader ParseHeader(JsonElement root)
    {
        if (!root.TryGetProperty("package", out var element) || element.ValueKind != JsonValueKind.Object)
        {
            throw Malformed("package-Kopf fehlt.");
        }

        var id = RequiredString(element, "id");
        var version = RequiredString(element, "version");
        var rid = RequiredString(element, "rid");
        var runtimeForm = RequiredString(element, "runtimeForm");
        var alphaMarker = RequiredString(element, "alphaMarker");

        if (id != PackageContract.PackageId)
        {
            throw Malformed($"Paketkennung {id} widerspricht dem Vertrag.");
        }

        if (!version.StartsWith(PackageContract.VersionBase, StringComparison.Ordinal) || version.Length != PackageContract.VersionBase.Length + 8)
        {
            throw Malformed($"Versionsform {version} widerspricht dem Vertrag.");
        }

        if (rid != PackageContract.SupportedRid)
        {
            throw Malformed($"RID {rid} ist nicht Teil dieses Paketvertrags.");
        }

        if (runtimeForm != PackageContract.RuntimeForm)
        {
            throw Malformed("Runtimeform widerspricht dem Vertrag.");
        }

        if (alphaMarker != PackageContract.AlphaMarker)
        {
            throw Malformed("Alpha-Marker widerspricht dem Vertrag.");
        }

        return new PackageHeader(id, version, rid, runtimeForm, alphaMarker);
    }

    private static PackageSourceBinding ParseSource(JsonElement root)
    {
        if (!root.TryGetProperty("source", out var element) || element.ValueKind != JsonValueKind.Object)
        {
            throw Malformed("source-Bindung fehlt.");
        }

        var commit = RequiredString(element, "commitSha256");
        var tree = RequiredString(element, "treeSha256");

        if (!IsLowerHex64(commit) || !IsLowerHex64(tree))
        {
            throw Malformed("Commit-/Baumbindung besitzt keine gueltige SHA-256-Form.");
        }

        if (!element.TryGetProperty("sourceDateEpoch", out var epoch) || epoch.GetInt64() != PackageContract.SourceDateEpoch)
        {
            throw Malformed("sourceDateEpoch widerspricht dem Vertrag.");
        }

        return new PackageSourceBinding(commit, tree, epoch.GetInt64());
    }

    private static PackageArtifactManifestBinding ParseArtifactManifest(JsonElement root)
    {
        if (!root.TryGetProperty("artifactManifest", out var element) || element.ValueKind != JsonValueKind.Object)
        {
            throw Malformed("artifactManifest-Bindung fehlt.");
        }

        var path = RequiredString(element, "path");
        var sha256 = RequiredString(element, "sha256");
        var cohort = RequiredString(element, "pinCohort");

        if (path != PackageContract.NativeManifestTargetPath)
        {
            throw Malformed("Artefaktmanifestpfad widerspricht dem Vertrag.");
        }

        if (!IsLowerHex64(sha256))
        {
            throw Malformed("Artefaktmanifestbindungs-Hash besitzt keine gueltige SHA-256-Form.");
        }

        if (cohort.Length == 0)
        {
            throw Malformed("Pin-Kohorte fehlt.");
        }

        return new PackageArtifactManifestBinding(path, sha256, cohort);
    }

    private static PackageProtection ParseProtection(JsonElement root)
    {
        if (!root.TryGetProperty("protection", out var element) || element.ValueKind != JsonValueKind.Object)
        {
            throw Malformed("Schutzabschnitt fehlt.");
        }

        var kind = RequiredString(element, "kind");
        var artifactsDir = RequiredString(element, "artifactsDir");
        var manifestPath = RequiredString(element, "manifestPath");

        if (kind != PackageContract.ProtectionKind)
        {
            throw Malformed("Schutzmechanismus widerspricht dem Vertrag.");
        }

        if (artifactsDir != "native" || manifestPath != PackageContract.NativeManifestTargetPath)
        {
            throw Malformed("Schutzpfade widersprechen dem Vertrag.");
        }

        if (!element.TryGetProperty("rejectsExitCodes", out var codes) || codes.ValueKind != JsonValueKind.Array)
        {
            throw Malformed("Schutzcodes fehlen.");
        }

        var boundCodes = new List<int>();
        foreach (var code in codes.EnumerateArray())
        {
            boundCodes.Add(code.GetInt32());
        }

        if (boundCodes.Count != PackageContract.ProtectionExitCodes.Count
            || !boundCodes.SequenceEqual(PackageContract.ProtectionExitCodes))
        {
            throw Malformed("Schutzcodes widersprechen der gebundenen Bestandsmatrix 14-17.");
        }

        return new PackageProtection(kind, artifactsDir, manifestPath, PackageContract.ProtectionExitCodes);
    }

    private static IReadOnlyList<PackageEntry> ParseEntries(JsonElement root)
    {
        if (!root.TryGetProperty("entries", out var element) || element.ValueKind != JsonValueKind.Array)
        {
            throw Malformed("entries fehlt oder ist kein Array.");
        }

        var entries = new List<PackageEntry>();
        string? previousPath = null;

        foreach (var item in element.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                throw Malformed("Manifesteintrag ist kein Objekt.");
            }

            var path = RequiredString(item, "path");

            if (!IsSafeRelativePath(path))
            {
                throw new PackageVerificationException(
                    PackageViolationClass.ManifestMalformed,
                    path,
                    "Unzulaessiger Manifestpfad.");
            }

            if (previousPath is not null && string.CompareOrdinal(previousPath, path) >= 0)
            {
                throw new PackageVerificationException(
                    PackageViolationClass.ManifestMalformed,
                    path,
                    "Manifesteinträge sind nicht strikt aufsteigend sortiert.");
            }

            previousPath = path;

            var type = RequiredString(item, "type");
            var mode = RequiredString(item, "unixMode");

            if (mode != PackageContract.UnixModeExecutable
                && mode != PackageContract.UnixModeRegular
                && mode != PackageContract.UnixModeSymlink)
            {
                throw new PackageVerificationException(
                    PackageViolationClass.ManifestMalformed,
                    path,
                    $"Unzulaessiger Unix-Modus {mode}.");
            }

            if (type == "file")
            {
                var sha256 = RequiredString(item, "sha256");

                if (!IsLowerHex64(sha256))
                {
                    throw new PackageVerificationException(
                        PackageViolationClass.ManifestHashInvalid,
                        path,
                        "Manifesteintrag besitzt keine gueltige SHA-256-Form.");
                }

                if (!item.TryGetProperty("bytes", out var bytes) || bytes.GetInt64() < 0)
                {
                    throw new PackageVerificationException(
                        PackageViolationClass.ManifestMalformed,
                        path,
                        "Manifesteintrag besitzt keine nichtnegative Bytegroesse.");
                }

                entries.Add(new PackageEntry(path, PackageEntryKind.File, sha256, bytes.GetInt64(), null, mode));
            }
            else if (type == "symlink")
            {
                var target = RequiredString(item, "target");

                if (string.IsNullOrWhiteSpace(target) || target.Contains('/', StringComparison.Ordinal) || target.StartsWith('/', StringComparison.Ordinal))
                {
                    throw new PackageVerificationException(
                        PackageViolationClass.ManifestMalformed,
                        path,
                        "Symlinkeintrag besitzt kein relativ, einzelnes Ziel.");
                }

                entries.Add(new PackageEntry(path, PackageEntryKind.Symlink, null, null, target, mode));
            }
            else
            {
                throw new PackageVerificationException(
                    PackageViolationClass.ManifestMalformed,
                    path,
                    $"Unbekannter Eintragstyp {type}.");
            }
        }

        return entries;
    }

    private static string RequiredString(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value) || value.ValueKind != JsonValueKind.String)
        {
            throw Malformed($"Pflichtfeld {name} fehlt oder ist kein Text.");
        }

        var text = value.GetString();
        if (string.IsNullOrEmpty(text))
        {
            throw Malformed($"Pflichtfeld {name} ist leer.");
        }

        return text!;
    }

    private static bool IsLowerHex64(string value) =>
        value.Length == 64 && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
