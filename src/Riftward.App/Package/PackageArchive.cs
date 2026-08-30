using System.Formats.Tar;
using System.IO.Compression;
using Riftward.Platform;
using System.Security.Cryptography;

namespace Riftward.App.Package;

/// <summary>
/// Deterministische Ustar-/gzip-Archivierung gemäß Paketvertrag Abschnitt 3:
/// fixierte mtime/uid/gid/uname/gname/mode, ordinal sortierte Einträge,
/// gzip ohne Zeitstempel. Keine Laufzeitinformation wandert ins Archiv.
/// </summary>
public static class PackageArchive
{
    /// <summary>Schreibt den Verzeichnisbaum <paramref name="rootDirectory"/> als
    /// tar.gz mit dem gegebenen Wurzelnamen und liefert den SHA-256 des Archivs.</summary>
    public static string Write(string rootDirectory, string rootName, string archivePath)
    {
        var rootFullPath = Path.GetFullPath(rootDirectory);
        var entries = CollectEntries(rootFullPath);

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(archivePath))!);

        using var fileStream = File.Create(archivePath);
        using (var gzip = new GZipStream(fileStream, CompressionLevel.Optimal, leaveOpen: true))
        using (var writer = new TarWriter(gzip, TarEntryFormat.Ustar, leaveOpen: false))
        {
            WriteDirectoryEntry(writer, rootName);

            foreach (var (relativePath, kind, sourcePath, linkTarget) in entries)
            {
                var entryName = rootName + "/" + relativePath.Replace(Path.DirectorySeparatorChar, '/');

                if (kind == PackageEntryKind.Directory)
                {
                    WriteDirectoryEntry(writer, entryName);
                }
                else if (kind == PackageEntryKind.Symlink)
                {
                    var entry = new UstarTarEntry(TarEntryType.SymbolicLink, entryName)
                    {
                        LinkName = linkTarget!,
                        Mode = UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute
                            | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute
                            | UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute,
                        Uid = 0,
                        Gid = 0,
                        UserName = string.Empty,
                        GroupName = string.Empty,
                    };

                    ApplyFixedTime(entry);
                    writer.WriteEntry(entry);
                }
                else
                {
                    var entry = new UstarTarEntry(TarEntryType.RegularFile, entryName);
                    ApplyFixedTime(entry);
                    entry.Mode = IsExecutable(sourcePath!)
                        ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                            | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                            | UnixFileMode.OtherRead | UnixFileMode.OtherExecute
                        : UnixFileMode.UserRead | UnixFileMode.UserWrite
                            | UnixFileMode.GroupRead | UnixFileMode.OtherRead;
                    entry.Uid = 0;
                    entry.Gid = 0;
                    entry.UserName = string.Empty;
                    entry.GroupName = string.Empty;

                    using var content = File.OpenRead(sourcePath!);
                    entry.DataStream = content;
                    writer.WriteEntry(entry);
                }
            }
        }

        fileStream.Flush();
        fileStream.Position = 0;
        return Convert.ToHexString(SHA256.HashData(fileStream)).ToLowerInvariant();
    }

    /// <summary>Entpackt ein tar.gz-Archiv in ein Zielverzeichnis (bestehende Inhalte werden überschrieben).</summary>
    public static void Extract(string archivePath, string targetDirectory)
    {
        Directory.CreateDirectory(targetDirectory);
        using var stream = File.OpenRead(archivePath);
        TarFile.ExtractToDirectory(stream, targetDirectory, overwriteFiles: true);
    }

    /// <summary>Liefert alle Einträge des Baums deterministisch (ordinal sortierte Pfade, Verzeichnisse ohne Namenstrenner am Ende).</summary>
    private static List<(string RelativePath, PackageEntryKind Kind, string? SourcePath, string? LinkTarget)> CollectEntries(string root)
    {
        var collected = new List<(string, PackageEntryKind, string?, string?)>();
        Walk(root, string.Empty, collected);
        return collected
            .OrderBy(static item => item.Item1, StringComparer.Ordinal)
            .ToList();
    }

    private static void Walk(
        string directory,
        string relativePrefix,
        List<(string RelativePath, PackageEntryKind Kind, string? SourcePath, string? LinkTarget)> collected)
    {
        foreach (var entry in new DirectoryInfo(directory).EnumerateFileSystemInfos())
        {
            var relative = relativePrefix.Length == 0
                ? entry.Name
                : relativePrefix + "/" + entry.Name;

            if (entry is DirectoryInfo subDirectory)
            {
                collected.Add((relative, PackageEntryKind.Directory, null, null));
                Walk(subDirectory.FullName, relative, collected);
            }
            else if (entry.LinkTarget is not null)
            {
                collected.Add((relative, PackageEntryKind.Symlink, null, entry.LinkTarget));
            }
            else
            {
                collected.Add((relative, PackageEntryKind.File, entry.FullName, null));
            }
        }
    }

    private static void WriteDirectoryEntry(TarWriter writer, string entryName)
    {
        var entry = new UstarTarEntry(TarEntryType.Directory, entryName)
        {
            Mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
                | UnixFileMode.GroupRead | UnixFileMode.GroupExecute
                | UnixFileMode.OtherRead | UnixFileMode.OtherExecute,
            Uid = 0,
            Gid = 0,
            UserName = string.Empty,
            GroupName = string.Empty,
        };

        ApplyFixedTime(entry);
        writer.WriteEntry(entry);
    }

    private static void ApplyFixedTime(UstarTarEntry entry) =>
        entry.ModificationTime = DateTimeOffset.FromUnixTimeSeconds(PackageContract.SourceDateEpoch).ToUniversalTime();

    private static bool IsExecutable(string filePath)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.UnsupportedPlatform,
                "Der Paketbau unterstützt nur linux-x64.",
                filePath));
        }

        var mode = File.GetUnixFileMode(filePath);
        return (mode & UnixFileMode.UserExecute) != 0
            || (mode & UnixFileMode.GroupExecute) != 0
            || (mode & UnixFileMode.OtherExecute) != 0;
    }
}
