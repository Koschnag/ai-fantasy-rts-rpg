using System.Security.Cryptography;
using System.Diagnostics;
using System.Text;
using Riftward.Platform;

namespace Riftward.App.Package;

/// <summary>
/// Ehrliche Quellbindung: Commit-SHA-256 und SHA-256 des hypothetischen
/// Add-A-Baums (Kandidatenidentität, privater Temporärindex im vertraglichen
/// Arbeitsbereich). Der echte Git-Index wird niemals berührt.
/// </summary>
public static class PackageSourceReader
{
    /// <summary>Ermittelt Commit- und Baum_DIGEST aus dem Repository um <paramref name="repoRoot"/>.</summary>
    public static PackageSourceBinding Read(string repoRoot, string privateIndexDir)
    {
        var commit = RunGit(repoRoot, "rev-parse HEAD");

        if (commit is null || !IsLowerHex(commit, 40))
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.PackageBuildFailed,
                "Quellbindung fehlgeschlagen: git rev-parse HEAD lieferte keinen Commit.",
                repoRoot));
        }

        var indexPath = Path.Combine(privateIndexDir, "package-source-tree.index");
        var tree = RunGit(repoRoot, "add -A", indexPath);
        if (tree is null)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.PackageBuildFailed,
                "Quellbindung fehlgeschlagen: privater Kandidatenindex konnte nicht beschrieben werden.",
                repoRoot));
        }

        tree = RunGit(repoRoot, "write-tree", indexPath);

        if (tree is null || !IsLowerHex(tree, 40))
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.PackageBuildFailed,
                "Quellbindung fehlgeschlagen: git write-tree lieferte keinen Baum.",
                repoRoot));
        }

        return new PackageSourceBinding(commit, Convert.ToHexString(SHA256.HashData(Convert.FromHexString(tree))).ToLowerInvariant(), PackageContract.SourceDateEpoch);
    }

    private static string? RunGit(string workingDirectory, string arguments, string? indexFile = null)
    {
        var startInfo = new ProcessStartInfo("git")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var token in arguments.Split(' '))
        {
            startInfo.ArgumentList.Add(token);
        }

        if (indexFile is not null)
        {
            startInfo.EnvironmentVariables["GIT_INDEX_FILE"] = indexFile;
        }

        using var process = Process.Start(startInfo);
        if (process is null)
        {
            return null;
        }

        var stdout = process.StandardOutput.ReadToEnd().TrimEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();

        return process.ExitCode == 0 ? stdout : null;
    }

    private static bool IsLowerHex(string value, int length) =>
        value.Length == length && value.All(static character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
}
