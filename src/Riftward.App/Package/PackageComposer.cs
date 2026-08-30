using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Riftward.App.Package;

/// <summary>
/// Staging und Manifestbildung gemäß Paketvertrag Abschnitt 1 und 4. Der
/// Composer schreibt ausschließlich in das ihm übergebene Stagingverzeichnis
/// und ist eine deterministische Funktion seiner Eingaben.
/// </summary>
public static class PackageComposer
{
    /// <summary>Eingaben des Paketbaus.</summary>
    public sealed record CompositionInput(
        string RepoRoot,
        string PublishDir,
        string NativeDistDir,
        string NativeManifestSourcePath,
        string SourceCommitSha256,
        string SourceTreeSha256,
        string PinCohort,
        string DotnetRuntimeVersion);

    /// <summary>Ergebnis des Stagings.</summary>
    public sealed record CompositionResult(
        string StageRoot,
        string RootName,
        PackageManifest Manifest,
        string ManifestSha256,
        int EntryCount,
        long TotalBytes);

    /// <summary>Baut das Stagingverzeichnis, das Paketmanifest und den Anker.</summary>
    public static CompositionResult Compose(string stageParentDir, CompositionInput input)
    {
        var version = PackageContract.VersionBase + input.SourceTreeSha256[..8];
        var rootName = PackageContract.ArchiveRootPrefix + version + PackageContract.ArchiveRootSuffix;
        var stageRoot = Path.Combine(stageParentDir, rootName);

        if (Directory.Exists(stageRoot))
        {
            Directory.Delete(stageRoot, recursive: true);
        }

        Directory.CreateDirectory(stageRoot);

        // 1. Selbstenthaltener Publish-Ausgabesatz, bytegetreu.
        CopyDirectoryTree(input.PublishDir, stageRoot);

        // 2. Toolchain-Lock unverändert.
        File.Copy(Path.Combine(input.RepoRoot, "toolchain.lock.json"), Path.Combine(stageRoot, "toolchain.lock.json"), overwrite: true);

        // 3. Native Laufzeitartefakte + deterministisch umgeschriebenes Manifest.
        var rewrittenManifestSha256 = StageNativeArtifacts(stageRoot, input);

        // 4. Versionierte Bestandsfixtures bytegetreu.
        StageFixtures(stageRoot, input.RepoRoot);

        // 5. Lizenz-/Attributionsmanifest deterministisch erzeugen.
        var nativeComponents = PackageDocs.ReadNativeComponents(Path.Combine(input.RepoRoot, "toolchain.lock.json"));
        var licensePath = Path.Combine(stageRoot, PackageContract.LicensesTargetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(licensePath)!);
        PackageDocs.WriteLicenses(licensePath, input.RepoRoot, nativeComponents, input.DotnetRuntimeVersion);

        // 6. Manifesteinträge über den gesamten Stagingbaum (ohne Manifest/Anker).
        var entries = CollectEntries(stageRoot);

        // 7. Manifest + Anker schreiben; dann Release Notes mit Manifestwahrheit.
        var manifest = new PackageManifest(
            new PackageHeader(
                PackageContract.PackageId,
                version,
                PackageContract.SupportedRid,
                PackageContract.RuntimeForm,
                PackageContract.AlphaMarker),
            new PackageSourceBinding(input.SourceCommitSha256, input.SourceTreeSha256, PackageContract.SourceDateEpoch),
            new PackageArtifactManifestBinding(PackageContract.NativeManifestTargetPath, rewrittenManifestSha256, input.PinCohort),
            new PackageProtection(PackageContract.ProtectionKind, "native", PackageContract.NativeManifestTargetPath, PackageContract.ProtectionExitCodes),
            entries);

        // Release Notes referenzieren die Manifestwahrheit (nicht das Archiv;
        // die Archivprüfungsumme steht ausschließlich in der Sidecar-Datei).
        var notesPath = Path.Combine(stageRoot, PackageContract.ReleaseNotesTargetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(notesPath)!);
        PackageDocs.WriteReleaseNotes(notesPath, manifest, rootName + ".tar.gz");

        // Nach den Notes erneut sammeln, damit sie manifestiert sind.
        entries = CollectEntries(stageRoot);
        manifest = manifest with { Entries = entries };

        var manifestPath = Path.Combine(stageRoot, PackageContract.ManifestFileName);
        var manifestSha256 = PackageManifestCodec.Write(manifestPath, manifest);

        var anchorPath = Path.Combine(stageRoot, PackageContract.AnchorFileName);
        File.WriteAllText(anchorPath, manifestSha256 + "  " + PackageContract.ManifestFileName + "\n", new UTF8Encoding(false));

        var totalBytes = entries.Where(static entry => entry.Bytes is not null).Sum(static entry => entry.Bytes!.Value);

        return new CompositionResult(stageRoot, rootName, manifest, manifestSha256, entries.Count, totalBytes);
    }

    /// <summary>Bündelt die manifestierten Native-Artefakte und schreibt das umpräfixte Manifest.</summary>
    private static string StageNativeArtifacts(string stageRoot, CompositionInput input)
    {
        var sourceManifest = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(input.NativeManifestSourcePath));

        if (sourceManifest is null || sourceManifest.Count == 0)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.PackageBuildFailed,
                "Native-Artefaktmanifest fehlt oder ist leer; zuerst scripts/native-build-linux-x64.sh ausführen.",
                input.NativeManifestSourcePath));
        }

        var nativeRoot = Path.Combine(stageRoot, "native");
        var bundled = new SortedDictionary<string, (string Sha256, long Bytes)>(StringComparer.Ordinal);

        foreach (var (sourceKey, element) in sourceManifest)
        {
            if (!sourceKey.StartsWith(PackageContract.NativeManifestSourcePrefix, StringComparison.Ordinal))
            {
                throw new PlatformException(new PlatformError(
                    PlatformErrorCode.PackageBuildFailed,
                    "Native-Artefaktmanifest enthält einen Schlüssel außerhalb des Dist-Präfix.",
                    sourceKey));
            }

            var remainder = sourceKey[PackageContract.NativeManifestSourcePrefix.Length..];

            if (!PackageManifestCodec.IsSafeRelativePath(remainder))
            {
                throw new PlatformException(new PlatformError(
                    PlatformErrorCode.PackageBuildFailed,
                    "Native-Artefaktmanifest enthält einen unsicheren Restpfad.",
                    sourceKey));
            }

            var sourceFile = Path.Combine(input.NativeDistDir, remainder.Replace('/', Path.DirectorySeparatorChar));

            if (!File.Exists(sourceFile))
            {
                throw new PlatformException(new PlatformError(
                    PlatformErrorCode.PackageBuildFailed,
                    "Manifestiertes Native-Artefakt fehlt im Dist.",
                    sourceFile));
            }

            var sha256 = element.GetProperty("sha256").GetString()!;
            var bytes = element.GetProperty("bytes").GetInt64();
            var targetFile = Path.Combine(nativeRoot, remainder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(targetFile)!);
            File.Copy(sourceFile, targetFile, overwrite: true);

            bundled[PackageContract.NativeManifestTargetPath.Replace("artifact-hashes.json", string.Empty, StringComparison.Ordinal) + remainder] = (sha256, bytes);
        }

        // Symlinks direkt in dist/lib (Laufzeitloader-Ziele), relativ gebunden.
        var libDir = Path.Combine(input.NativeDistDir, "lib");

        foreach (var entry in new DirectoryInfo(libDir).EnumerateFileSystemInfos())
        {
            if (entry is DirectoryInfo)
            {
                continue;
            }

            var linkTarget = entry.LinkTarget;

            if (linkTarget is null)
            {
                continue;
            }

            if (linkTarget.Contains('/', StringComparison.Ordinal) || linkTarget.StartsWith('/', StringComparison.Ordinal))
            {
                throw new PlatformException(new PlatformError(
                    PlatformErrorCode.PackageBuildFailed,
                    "Nativer Dist-Symlink besitzt ein nicht relativ einzelnes Ziel.",
                    entry.FullName));
            }

            File.CreateSymbolicLink(Path.Combine(nativeRoot, "lib", entry.Name), linkTarget);
        }

        // Kanonisches gebündeltes Manifest: exakt die manifestierten Artefakte,
        // gleiche sha256/bytes-Bindung, Pfadpräfix native/.
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            foreach (var (path, binding) in bundled)
            {
                if (binding.Bytes < 0)
                {
                    // Symlinks sind Teil des Paketmanifests, nie des Artefaktmanifests.
                    continue;
                }

                writer.WriteStartObject(path);
                writer.WriteString("sha256", binding.Sha256);
                writer.WriteNumber("bytes", binding.Bytes);
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
        }

        var rewrittenPath = Path.Combine(stageRoot, PackageContract.NativeManifestTargetPath);
        Directory.CreateDirectory(Path.GetDirectoryName(rewrittenPath)!);
        File.WriteAllBytes(rewrittenPath, buffer.ToArray());

        return Convert.ToHexString(SHA256.HashData(buffer.ToArray())).ToLowerInvariant();
    }

    private static void StageFixtures(string stageRoot, string repoRoot)
    {
        var sourceDir = Path.Combine(repoRoot, PackageContract.FixtureSourceDir);

        if (!Directory.Exists(sourceDir))
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.PackageBuildFailed,
                "Bestandsfixtures fehlen im Quellbaum.",
                sourceDir));
        }

        var scripts = Directory.GetFiles(sourceDir, "*.graybox");

        if (scripts.Length == 0)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.PackageBuildFailed,
                "Keine Bestandsfixtures im Quellbaum.",
                sourceDir));
        }

        var targetDir = Path.Combine(stageRoot, PackageContract.FixtureTargetDir);
        Directory.CreateDirectory(targetDir);

        foreach (var script in scripts)
        {
            File.Copy(script, Path.Combine(targetDir, Path.GetFileName(script)), overwrite: true);
        }
    }

    private static void CopyDirectoryTree(string sourceDir, string targetDir)
    {
        foreach (var directory in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(targetDir, Path.GetRelativePath(sourceDir, directory)));
        }

        foreach (var file in Directory.GetFiles(sourceDir, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(targetDir, Path.GetRelativePath(sourceDir, file));
            var linkTarget = File.ResolveLinkTarget(file, returnFinalTarget: false);

            if (linkTarget is not null)
            {
                throw new PlatformException(new PlatformError(
                    PlatformErrorCode.PackageBuildFailed,
                    "Publish-Ausgabe enthält einen unerwarteten Symlink.",
                    file));
            }

            File.Copy(file, target, overwrite: true);
        }
    }

    /// <summary>Sammelt manifestierte Einträge über den Stagingbaum (ohne Manifest/Anker).</summary>
    private static IReadOnlyList<PackageEntry> CollectEntries(string stageRoot)
    {
        var entries = new List<PackageEntry>();
        Walk(stageRoot, string.Empty, entries);
        return PackageManifestCodec.Sort(entries);
    }

    private static void Walk(string directory, string relativePrefix, List<PackageEntry> entries)
    {
        foreach (var info in new DirectoryInfo(directory).EnumerateFileSystemInfos().OrderBy(static item => item.Name, StringComparer.Ordinal))
        {
            var relative = relativePrefix.Length == 0 ? info.Name : relativePrefix + "/" + info.Name;

            if (relative == PackageContract.ManifestFileName || relative == PackageContract.AnchorFileName)
            {
                continue;
            }

            if (info is DirectoryInfo subDirectory)
            {
                Walk(subDirectory.FullName, relative, entries, stageRoot);
                continue;
            }

            if (info.LinkTarget is not null)
            {
                entries.Add(new PackageEntry(relative, PackageEntryKind.Symlink, null, null, info.LinkTarget, PackageContract.UnixModeSymlink));
                continue;
            }

            var executable = (File.GetUnixFileMode(info.FullName) & UnixFileMode.UserExecute) != 0;
            var length = new FileInfo(info.FullName).Length;

            entries.Add(new PackageEntry(
                relative,
                PackageEntryKind.File,
                PackageManifestCodec.Sha256File(info.FullName),
                length,
                null,
                executable ? PackageContract.UnixModeExecutable : PackageContract.UnixModeRegular));
        }
    }
}
