using System.Security.Cryptography;
using System.Text.Json;

namespace Riftward.App.Package;

/// <summary>Ergebnis der Verifikation eines entpackten Pakets.</summary>
public sealed record PackageDirectoryVerification(
    bool Valid,
    IReadOnlyList<PackageViolation> Violations,
    IReadOnlyList<PackageArtifactCheck> ArtifactChecks);

/// <summary>Einzelne Verletzung der Paketverifikation.</summary>
public sealed record PackageViolation(string Class, string Path, string Detail);

/// <summary>Gebundener Bestandsgrund der Host-Artefaktprüfung.</summary>
public sealed record PackageArtifactCheck(string RelativePath, bool Valid, string? Reason = null, string? Detail = null);

/// <summary>
/// Prüft ein entpacktes Paket gegen die Manifestkette und das gebündelte
/// Native-Artefaktmanifest (bestehende Host-Prüfung). Verletzungen sind
/// unterscheidbar (Paketvertrag Abschnitt 4); es wird nichts verändert.
/// </summary>
public static class PackageVerifier
{
    /// <summary>Prüft das entpackte Paket im Wurzelverzeichnis <paramref name="packageRoot"/>.</summary>
    public static PackageDirectoryVerification VerifyDirectory(string packageRoot)
    {
        var violations = new List<PackageViolation>();
        var artifactChecks = new List<PackageArtifactCheck>();

        PackageManifest manifest;

        try
        {
            manifest = PackageManifestCodec.Parse(Path.Combine(packageRoot, PackageContract.ManifestFileName));
        }
        catch (PackageVerificationException exception)
        {
            violations.Add(new PackageViolation(exception.ViolationClass, exception.Path, exception.Detail));
            return new PackageDirectoryVerification(false, violations, artifactChecks);
        }
        catch (Exception exception) when (exception is FormatException or InvalidOperationException or JsonException)
        {
            // Ein in der Struktur gueltiges, aber vertraglich falsch typisiertes
            // Manifest darf den Verifikator nicht unkontrolliert beenden.
            violations.Add(new PackageViolation(
                PackageViolationClassNames.Of(PackageViolationClass.ManifestMalformed),
                PackageContract.ManifestFileName,
                $"Manifest nicht lesbar: {exception.Message}"));
            return new PackageDirectoryVerification(false, violations, artifactChecks);
        }

        // Wurzelverzeichnisname an die Version gebunden (sichere Entpackform).
        var actualRootName = new DirectoryInfo(packageRoot).Name;

        if (actualRootName != manifest.RootDirectoryName)
        {
            violations.Add(new PackageViolation(
                PackageViolationClassNames.Of(PackageViolationClass.ManifestMalformed),
                actualRootName,
                $"Wurzelverzeichnis {actualRootName} widerspricht der gebundenen Version {manifest.Package.Version}."));
        }

        // Ankerprüfung.
        var anchorPath = Path.Combine(packageRoot, PackageContract.AnchorFileName);

        if (!File.Exists(anchorPath))
        {
            violations.Add(new PackageViolation(
                PackageViolationClassNames.Of(PackageViolationClass.AnchorMissing),
                PackageContract.AnchorFileName,
                "Paketanker fehlt."));
        }
        else
        {
            var anchorText = File.ReadAllText(anchorPath).TrimEnd();
            var anchorParts = anchorText.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (anchorParts.Length != 2
                || anchorParts[1] != PackageContract.ManifestFileName
                || !IsLowerHex64(anchorParts[0]))
            {
                violations.Add(new PackageViolation(
                    PackageViolationClassNames.Of(PackageViolationClass.AnchorMismatch),
                    PackageContract.AnchorFileName,
                    "Paketanker besitzt keine gueltige sha256sum-Form ueber das Manifest."));
            }
            else
            {
                var actualManifestHash = PackageManifestCodec.Sha256File(Path.Combine(packageRoot, PackageContract.ManifestFileName));

                if (!string.Equals(actualManifestHash, anchorParts[0], StringComparison.Ordinal))
                {
                    violations.Add(new PackageViolation(
                        PackageViolationClassNames.Of(PackageViolationClass.AnchorMismatch),
                        PackageContract.AnchorFileName,
                        $"Anker {anchorParts[0]} statt Manifesthash {actualManifestHash}."));
                }
            }
        }

        // Artefaktmanifestbindung: die gebündelte Datei muss dem im Manifest
        // gebundenen Hash entsprechen, bevor die Host-Prüfung sie verwendet.
        var artifactManifestFullPath = Path.Combine(packageRoot, manifest.ArtifactManifest.Path.Replace('/', Path.DirectorySeparatorChar));

        if (!File.Exists(artifactManifestFullPath))
        {
            violations.Add(new PackageViolation(
                PackageViolationClassNames.Of(PackageViolationClass.EntryMissing),
                manifest.ArtifactManifest.Path,
                "Gebuendeltes Native-Artefaktmanifest fehlt."));
        }
        else
        {
            var actualArtifactManifestHash = PackageManifestCodec.Sha256File(artifactManifestFullPath);

            if (!string.Equals(actualArtifactManifestHash, manifest.ArtifactManifest.Sha256, StringComparison.Ordinal))
            {
                violations.Add(new PackageViolation(
                    PackageViolationClassNames.Of(PackageViolationClass.EntryHashMismatch),
                    manifest.ArtifactManifest.Path,
                    $"Hash {actualArtifactManifestHash} statt gebunden {manifest.ArtifactManifest.Sha256}."));
            }
        }

        // Bestehende Host-Artefaktprüfung über das gebündelte Manifest.
        try
        {
            var report = Riftward.Platform.NativeArtifacts.Validate(packageRoot, artifactManifestFullPath);

            foreach (var check in report.Checks)
            {
                artifactChecks.Add(new PackageArtifactCheck(check.RelativePath, check.Valid, check.FailureCode, check.Detail));
            }

            if (!report.Valid && violations.All(static violation => violation.Class != PackageViolationClassNames.Of(PackageViolationClass.EntryHashMismatch)))
            {
                var firstFailure = report.Checks.First(static check => !check.Valid);
                violations.Add(new PackageViolation(
                    "ARTIFACT_MANIFEST_REJECTED",
                    firstFailure.RelativePath,
                    firstFailure.Detail ?? "Host-Artefaktpruefung hat das gebuendelte Paket abgewiesen."));
            }
        }
        catch (Riftward.Platform.PlatformException exception)
        {
            artifactChecks.Add(new PackageArtifactCheck(manifest.ArtifactManifest.Path, false, "ARTIFACT_MANIFEST_INVALID", exception.Error.Detail));

            violations.Add(new PackageViolation(
                "ARTIFACT_MANIFEST_REJECTED",
                manifest.ArtifactManifest.Path,
                exception.Error.Message));
        }

        // Eintragmatrix: jeder manifestierte Pfad existiert mit gebundener Form,
        // jeder tatsächliche Inhalt ist manifestiert.
        VerifyEntries(packageRoot, manifest, violations);

        return new PackageDirectoryVerification(violations.Count == 0, violations, artifactChecks);
    }

    private static bool IsLowerHex64(string value) =>
        value.Length == 64 && value.All(static character => (character >= '0' && character <= '9') || (character >= 'a' && character <= 'f'));

    private static void VerifyEntries(string packageRoot, PackageManifest manifest, List<PackageViolation> violations)
    {
        var manifestPaths = new HashSet<string>(StringComparer.Ordinal);

        foreach (var entry in manifest.Entries)
        {
            manifestPaths.Add(entry.Path);
            var fullPath = Path.Combine(packageRoot, entry.Path.Replace('/', Path.DirectorySeparatorChar));

            if (entry.Kind == PackageEntryKind.Symlink)
            {
                var info = FileSystemInfoExists(fullPath);

                if (info is null)
                {
                    violations.Add(new PackageViolation(
                        PackageViolationClassNames.Of(PackageViolationClass.EntryMissing),
                        entry.Path,
                        "Manifestierter Symlink fehlt."));
                    continue;
                }

                if (info.LinkTarget is null || !string.Equals(info.LinkTarget, entry.LinkTarget, StringComparison.Ordinal))
                {
                    violations.Add(new PackageViolation(
                        PackageViolationClassNames.Of(PackageViolationClass.EntrySymlinkMismatch),
                        entry.Path,
                        $"Symlinkziel {info.LinkTarget ?? "<kein Symlink>"} statt gebunden {entry.LinkTarget}."));
                }

                continue;
            }

            if (!File.Exists(fullPath))
            {
                violations.Add(new PackageViolation(
                    PackageViolationClassNames.Of(PackageViolationClass.EntryMissing),
                    entry.Path,
                    "Manifestierte Datei fehlt."));
                continue;
            }

            var length = new FileInfo(fullPath).Length;

            if (length != entry.Bytes!.Value)
            {
                violations.Add(new PackageViolation(
                    PackageViolationClassNames.Of(PackageViolationClass.EntryIncomplete),
                    entry.Path,
                    $"Groesse {length} statt {entry.Bytes.Value} Bytes."));
                continue;
            }

            var hash = PackageManifestCodec.Sha256File(fullPath);

            if (!string.Equals(hash, entry.Sha256, StringComparison.Ordinal))
            {
                violations.Add(new PackageViolation(
                    PackageViolationClassNames.Of(PackageViolationClass.EntryHashMismatch),
                    entry.Path,
                    $"SHA-256 {hash} statt {entry.Sha256}."));
            }
        }

        // Kein unmanifestierter Inhalt im Wurzelverzeichnis.
        foreach (var relative in EnumerateRelativePaths(packageRoot))
        {
            if (relative == PackageContract.ManifestFileName || relative == PackageContract.AnchorFileName)
            {
                continue;
            }

            if (!manifestPaths.Contains(relative))
            {
                violations.Add(new PackageViolation(
                    PackageViolationClassNames.Of(PackageViolationClass.UnmanifestedFile),
                    relative,
                    "Paketinhalt ist nicht manifestiert."));
            }
        }
    }

    private static FileSystemInfo? FileSystemInfoExists(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            return new FileInfo(fullPath);
        }

        if (Directory.Exists(fullPath))
        {
            return new DirectoryInfo(fullPath);
        }

        return null;
    }

    /// <summary>Prüft ein Archiv samt Sidecar und entpackt es für die Verzeichnisprüfung.</summary>
    public static PackageDirectoryVerification VerifyArchive(string archivePath, string workDirectory)
    {
        var violations = new List<PackageViolation>();
        var sideCarPath = archivePath + ".sha256";

        if (!File.Exists(archivePath))
        {
            violations.Add(new PackageViolation(
                PackageViolationClassNames.Of(PackageViolationClass.SideCarMissing),
                archivePath,
                "Archiv fehlt."));
            return new PackageDirectoryVerification(false, violations, Array.Empty<PackageArtifactCheck>());
        }

        if (!File.Exists(sideCarPath))
        {
            violations.Add(new PackageViolation(
                PackageViolationClassNames.Of(PackageViolationClass.SideCarMissing),
                Path.GetFileName(sideCarPath),
                "Sidecar-Prüfsumme fehlt neben dem Archiv."));
            return new PackageDirectoryVerification(false, violations, Array.Empty<PackageArtifactCheck>());
        }

        var expectedArchiveHash = File.ReadAllText(sideCarPath).TrimEnd().Split(' ', StringSplitOptions.RemoveEmptyEntries) is { Length: 2 } parts
            ? parts[0]
            : string.Empty;
        var actualArchiveHash = PackageManifestCodec.Sha256File(archivePath);

        if (expectedArchiveHash.Length != 64 || !string.Equals(expectedArchiveHash, actualArchiveHash, StringComparison.Ordinal))
        {
            violations.Add(new PackageViolation(
                PackageViolationClassNames.Of(PackageViolationClass.SideCarMismatch),
                Path.GetFileName(sideCarPath),
                $"Sidecar {expectedArchiveHash} statt Archivhash {actualArchiveHash}."));
            return new PackageDirectoryVerification(false, violations, Array.Empty<PackageArtifactCheck>());
        }

        if (Directory.Exists(workDirectory))
        {
            Directory.Delete(workDirectory, recursive: true);
        }

        try
        {
            PackageArchive.Extract(archivePath, workDirectory);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            // Korrupte oder nicht entpackbare Archivbytes mit konsistentem
            // Sidecar bleiben ein kontrollierter, unterscheidbarer Befund
            // (Exit 40 mit Pruefreport), niemals ein unkontrollierter Abbruch.
            violations.Add(new PackageViolation(
                PackageViolationClassNames.Of(PackageViolationClass.ArchiveUnreadable),
                Path.GetFileName(archivePath),
                $"Archiv konnte nicht entpackt werden: {exception.Message}"));
            return new PackageDirectoryVerification(false, violations, Array.Empty<PackageArtifactCheck>());
        }

        var directories = Directory.GetDirectories(workDirectory);

        if (directories.Length != 1)
        {
            violations.Add(new PackageViolation(
                PackageViolationClassNames.Of(PackageViolationClass.ManifestMalformed),
                workDirectory,
                $"Archiv enthaelt {directories.Length} statt genau einem Wurzelverzeichnis."));
            return new PackageDirectoryVerification(false, violations, Array.Empty<PackageArtifactCheck>());
        }

        return VerifyDirectory(directories[0]);
    }

    private static IEnumerable<string> EnumerateRelativePaths(string root)
    {
        foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            yield return Path.GetRelativePath(root, file).Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
