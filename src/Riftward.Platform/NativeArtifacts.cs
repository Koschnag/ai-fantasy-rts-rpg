using System.Security.Cryptography;
using System.Text.Json;

namespace Riftward.Platform;

/// <summary>Ergebnis der Pruefung einer einzelnen Artefaktdatei.</summary>
public sealed record ArtifactCheck(string RelativePath, bool Valid, string? FailureCode = null, string? Detail = null);

/// <summary>Gesamtergebnis der Artefaktpruefung gegen das Hashmanifest.</summary>
public sealed record ArtifactCatalogReport(bool Valid, IReadOnlyList<ArtifactCheck> Checks)
{
    public PlatformError? FirstFailure()
    {
        foreach (var check in Checks)
        {
            if (check.Valid)
            {
                continue;
            }

            var code = check.FailureCode switch
            {
                "ARTIFACT_MISSING" => PlatformErrorCode.ArtifactMissing,
                "ARTIFACT_INCOMPLETE" => PlatformErrorCode.ArtifactIncomplete,
                _ => PlatformErrorCode.ArtifactHashMismatch,
            };

            return new PlatformError(code, $"Native-Artefakt unbrauchbar: {check.RelativePath}", check.Detail);
        }

        return null;
    }
}

/// <summary>
/// Prueft native Laufzeitartefakte (SDL3, bgfx-Shim, Shaderbinaerdateien) gegen
/// das vom Native-Build aufgezeichnete SHA-256-Manifest. Fehlende,
/// unvollstaendige oder hashbeschaedigte Artefakte werden als kontrollierte
/// Fehler gemeldet; es wird nichts geschrieben und nichts geladen.
/// </summary>
public static class NativeArtifacts
{
    private const long MaxManifestBytes = 1_048_576;

    /// <summary>Aufgezeichnetes Eintragformat des Native-Buildmanifests.</summary>
    public sealed record ManifestEntry(string Sha256, long Bytes);

    public static Dictionary<string, ManifestEntry> ReadManifest(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.ArtifactManifestInvalid,
                "Artefakthash-Manifest fehlt; zuerst scripts/native-build-linux-x64.sh ausführen.",
                manifestPath));
        }

        var info = new FileInfo(manifestPath);
        if (info.Length is 0 or > MaxManifestBytes)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.ArtifactManifestInvalid,
                "Artefakthash-Manifest hat ungueltige Groesse.",
                $"{manifestPath} ({info.Length} Bytes)"));
        }

        Dictionary<string, JsonElement>? parsed;
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            parsed = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(document.RootElement.GetRawText());
        }
        catch (JsonException exception)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.ArtifactManifestInvalid,
                "Artefakthash-Manifest ist kein gueltiges JSON.",
                exception.Message));
        }

        if (parsed is null || parsed.Count == 0)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.ArtifactManifestInvalid,
                "Artefakthash-Manifest enthaelt keine Eintraege.",
                manifestPath));
        }

        var entries = new Dictionary<string, ManifestEntry>(StringComparer.Ordinal);
        foreach (var (relativePath, element) in parsed)
        {
            string? sha256 = null;
            long bytes = -1;

            if (element.ValueKind == JsonValueKind.Object)
            {
                if (element.TryGetProperty("sha256", out var hashElement) && hashElement.ValueKind == JsonValueKind.String)
                {
                    sha256 = hashElement.GetString();
                }

                if (element.TryGetProperty("bytes", out var bytesElement) && bytesElement.ValueKind == JsonValueKind.Number)
                {
                    bytes = bytesElement.GetInt64();
                }
            }

            if (string.IsNullOrWhiteSpace(sha256) || sha256!.Length != 64 || !IsLowerHex(sha256) || bytes < 0)
            {
                throw new PlatformException(new PlatformError(
                    PlatformErrorCode.ArtifactManifestInvalid,
                    "Manifesteintrag besitzt kein gueltiges sha256/bytes-Paar.",
                    relativePath));
            }

            entries.Add(relativePath, new ManifestEntry(sha256, bytes));
        }

        return entries;
    }

    /// <summary>
    /// Prueft alle Manifesteintrage. Manifestpfade sind workspace-relativ
    /// (POSIX, wie vom Native-Build aufgezeichnet) und werden gegen die
    /// angegebene Workspacewurzel aufgeloest; Pfade ausserhalb der Wurzel
    /// werden abgelehnt. Es wird nichts geschrieben.
    /// </summary>
    public static ArtifactCatalogReport Validate(string workspaceRoot, string manifestPath)
    {
        var entries = ReadManifest(manifestPath);
        var rootFullPath = Path.GetFullPath(workspaceRoot);
        var prefix = rootFullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var checks = new List<ArtifactCheck>(entries.Count);

        foreach (var (relativePath, entry) in entries)
        {
            if (relativePath.Contains('\\')
                || relativePath.StartsWith('/')
                || relativePath.Split('/').Any(static segment => segment is ".." or "."))
            {
                checks.Add(new ArtifactCheck(relativePath, false, "ARTIFACT_HASH_MISMATCH", "Unzulaessiger Manifestpfad."));
                continue;
            }

            var fullRelative = Path.GetFullPath(
                Path.Combine(rootFullPath, relativePath.Replace('/', Path.DirectorySeparatorChar)));

            if (!fullRelative.StartsWith(prefix, StringComparison.Ordinal))
            {
                checks.Add(new ArtifactCheck(relativePath, false, "ARTIFACT_HASH_MISMATCH", "Pfad liegt ausserhalb des Workspace."));
                continue;
            }

            if (!File.Exists(fullRelative))
            {
                checks.Add(new ArtifactCheck(relativePath, false, "ARTIFACT_MISSING", $"Erwartet unter {fullRelative}"));
                continue;
            }

            var size = new FileInfo(fullRelative).Length;
            if (size != entry.Bytes)
            {
                checks.Add(new ArtifactCheck(relativePath, false, "ARTIFACT_INCOMPLETE", $"Groesse {size} statt {entry.Bytes} Bytes."));
                continue;
            }

            using var stream = File.OpenRead(fullRelative);
            var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();

            if (!string.Equals(hash, entry.Sha256, StringComparison.Ordinal))
            {
                checks.Add(new ArtifactCheck(relativePath, false, "ARTIFACT_HASH_MISMATCH", $"SHA-256 {hash} statt {entry.Sha256}."));
                continue;
            }

            checks.Add(new ArtifactCheck(relativePath, true));
        }

        return new ArtifactCatalogReport(checks.All(static check => check.Valid), checks);
    }

    private static bool IsLowerHex(string value)
    {
        foreach (var character in value)
        {
            var isHex = character is >= '0' and <= '9' or >= 'a' and <= 'f';
            if (!isHex)
            {
                return false;
            }
        }

        return true;
    }
}
