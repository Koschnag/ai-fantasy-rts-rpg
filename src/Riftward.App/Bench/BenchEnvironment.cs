using System.Reflection;
using System.Runtime.InteropServices;

namespace Riftward.App.Bench;

/// <summary>
/// Umgebungsbinding fuer den Benchmarkreport: Commit- und Buildmodus-Kennung
/// ohne Unterprozess und ohne Schreibzugriff; fehlende Anteile bleiben als
/// solche sichtbar statt ersetzt zu werden.
/// </summary>
public static class BenchEnvironment
{
    /// <summary>Liest die Commitkennung direkt aus .git ohne git-Unterprozess.</summary>
    public static string CommitId()
    {
        try
        {
            var gitDirectory = FindGitDirectory(Directory.GetCurrentDirectory());

            if (gitDirectory is null)
            {
                return "unresolved";
            }

            var head = File.ReadAllText(Path.Combine(gitDirectory, "HEAD")).Trim();

            if (head.StartsWith("ref:", StringComparison.Ordinal))
            {
                var reference = head[4..].Trim();
                var directPath = Path.Combine(gitDirectory, reference.Replace('/', Path.DirectorySeparatorChar));

                if (File.Exists(directPath))
                {
                    return File.ReadAllText(directPath).Trim();
                }

                return ResolvePackedReference(gitDirectory, reference) ?? "unresolved";
            }

            // Detached HEAD enthaelt bereits die Commitkennung.
            return head.Length == 40 ? head : "unresolved";
        }
        catch (IOException)
        {
            return "unresolved";
        }
        catch (UnauthorizedAccessException)
        {
            return "unresolved";
        }
    }

    /// <summary>Buildmodus aus der Assemblykonfiguration (Release/Debug).</summary>
    public static string BuildMode() =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyConfigurationAttribute>()?.Configuration ?? "unknown";

    /// <summary>Systeminterne Ressourcenkennung des Laufzeitprozesses.</summary>
    public static string Rid() =>
        OperatingSystem.IsLinux() && RuntimeInformation.ProcessArchitecture == Architecture.X64
            ? "linux-x64"
            : $"unsupported-{RuntimeInformation.OSDescription.Trim()}";

    private static string? FindGitDirectory(string startDirectory)
    {
        var current = new DirectoryInfo(startDirectory);

        while (current is not null)
        {
            var candidate = Path.Combine(current.FullName, ".git");

            if (Directory.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        return null;
    }

    private static string? ResolvePackedReference(string gitDirectory, string reference)
    {
        var packedReferences = Path.Combine(gitDirectory, "packed-refs");

        if (!File.Exists(packedReferences))
        {
            return null;
        }

        foreach (var line in File.ReadLines(packedReferences))
        {
            if (line.Length > 41 && line[40] == ' ' && line[41..].Equals(reference, StringComparison.Ordinal))
            {
                return line[..40];
            }
        }

        return null;
    }
}
