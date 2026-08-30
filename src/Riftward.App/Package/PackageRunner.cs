using System.Diagnostics;
using System.Text.Json;
using Riftward.Platform;

namespace Riftward.App.Package;

/// <summary>Kontrollierter Usage-Fehler des package-Befehls (bestehende Bedeutung 2).</summary>
public sealed class PackageUsageException : Exception
{
    /// <summary>Erzeugt einen Usage-Fehler.</summary>
    public PackageUsageException(string message)
        : base(message)
    {
    }
}

/// <summary>
/// Öffentlicher package-Befehl (T-038, Paketvertrag V1): baut für genau
/// linux-x64 ein versioniertes, reproduzierbares Alphapaket oder verifiziert
/// ein bestehendes. Der Bau schreibt ausschließlich in den vertraglich
/// erlaubten Ausgabe-/Arbeitsbereich, benötigt kein Netzwerk und fügt keine
/// neue Abhängigkeit hinzu.
/// </summary>
public static class PackageRunner
{
    private const string DefaultOutputDir = "artifacts/package";

    /// <summary>Einstieg des Befehls.</summary>
    public static int Run(CommandLineArgs arguments)
    {
        try
        {
            return Dispatch(arguments);
        }
        catch (PackageUsageException exception)
        {
            Console.Error.WriteLine(exception.Message);
            Console.Error.WriteLine("Verwendung: Riftward.App package [--output-dir VERZ] [--work VERZ] [--rid linux-x64]");
            Console.Error.WriteLine("            Riftward.App package --verify ARCHIV.tar.gz [--work VERZ]");
            return ExitCodes.Usage;
        }
    }

    private static int Dispatch(CommandLineArgs arguments)
    {
        string? outputDir = null;
        string? workDir = null;
        string? rid = null;
        string? verifyArchive = null;

        while (true)
        {
            var argument = arguments.Next();

            if (argument is null)
            {
                break;
            }

            switch (argument)
            {
                case "--output-dir" when outputDir is null:
                    outputDir = RequireValue(arguments, argument);
                    break;
                case "--work" when workDir is null:
                    workDir = RequireValue(arguments, argument);
                    break;
                case "--rid" when rid is null:
                    rid = RequireValue(arguments, argument);

                    if (rid != PackageContract.SupportedRid)
                    {
                        throw new PackageUsageException($"Unbekannte RID '{rid}'; der Paketvertrag umfasst genau {PackageContract.SupportedRid}.");
                    }

                    break;
                case "--verify" when verifyArchive is null:
                    verifyArchive = RequireValue(arguments, argument);
                    break;
                default:
                    throw new PackageUsageException($"Unbekannte Option '{argument}' im package-Befehl.");
            }
        }

        if (verifyArchive is not null)
        {
            if (outputDir is not null || rid is not null)
            {
                throw new PackageUsageException("--verify schließt --output-dir und --rid aus.");
            }

            return Verify(verifyArchive, workDir);
        }

        return Build(outputDir ?? DefaultOutputDir, workDir);
    }

    private static string RequireValue(CommandLineArgs arguments, string option)
    {
        var value = arguments.Next();

        if (string.IsNullOrEmpty(value) || value.StartsWith("--", StringComparison.Ordinal))
        {
            throw new PackageUsageException($"Option {option} benötigt einen Wert.");
        }

        return value!;
    }

    private static int Build(string outputDir, string? workDir)
    {
        var repoRoot = FindRepositoryRoot();
        var outputFullPath = Path.GetFullPath(outputDir);
        Directory.CreateDirectory(outputFullPath);

        var buildWorkDir = Path.GetFullPath(workDir ?? Path.Combine(outputFullPath, "work"));
        Directory.CreateDirectory(buildWorkDir);

        try
        {
            // 1. Ehrliche Quellbindung (privater Index, echter Index unberührt).
            var binding = PackageSourceReader.Read(repoRoot, buildWorkDir);
            var version = PackageContract.VersionBase + binding.TreeSha256[..8];

            // 2. Locked RID-Restore und selbstenthaltener Publish (offline).
            var publishDir = Path.Combine(buildWorkDir, "publish");
            Publish(repoRoot, publishDir);

            // 3. Native-Dist vertraglich gebunden vorhanden?
            var nativeDistDir = Path.Combine(repoRoot, PackageContract.NativeDistSourceDir);
            var nativeManifestPath = Path.Combine(repoRoot, PackageContract.NativeManifestSourcePath);

            if (!File.Exists(nativeManifestPath))
            {
                throw new PlatformException(new PlatformError(
                    PlatformErrorCode.PackageBuildFailed,
                    "Native-Artefaktmanifest fehlt; zuerst scripts/native-build-linux-x64.sh ausführen.",
                    PackageContract.NativeManifestSourcePath));
            }

            // 4. Staging, Manifest, Anker.
            var composition = PackageComposer.Compose(buildWorkDir, new PackageComposer.CompositionInput(
                repoRoot,
                publishDir,
                nativeDistDir,
                nativeManifestPath,
                binding.CommitSha256,
                binding.TreeSha256,
                PackageDocs.ReadPinCohort(Path.Combine(repoRoot, "toolchain.lock.json")),
                PackageDocs.DotnetRuntimeVersion()));

            // 5. Deterministisches Archiv + Sidecar.
            var archivePath = Path.Combine(outputFullPath, composition.RootName + ".tar.gz");
            var archiveSha256 = PackageArchive.Write(composition.StageRoot, composition.RootName, archivePath);
            File.WriteAllText(
                archivePath + ".sha256",
                archiveSha256 + "  " + composition.RootName + ".tar.gz\n",
                new System.Text.UTF8Encoding(false));

            // 6. Sofortige Eigenverifikation: ein gebauter Null-Vertrauenspfad.
            var verification = PackageVerifier.VerifyArchive(archivePath, Path.Combine(buildWorkDir, "verify"));

            WriteReport(new
            {
                schemaVersion = PackageContract.SchemaVersion,
                command = "package",
                mode = "build",
                rid = PackageContract.SupportedRid,
                version,
                archivePath,
                archiveSha256,
                sideCarPath = archivePath + ".sha256",
                manifestSha256 = composition.ManifestSha256,
                entryCount = composition.EntryCount,
                totalBytes = composition.TotalBytes,
                sourceCommitSha256 = binding.CommitSha256,
                sourceTreeSha256 = binding.TreeSha256,
                sourceDateEpoch = PackageContract.SourceDateEpoch,
                selfVerificationOk = verification.Valid,
            });

            if (!verification.Valid)
            {
                Console.Error.WriteLine("Paketbau abgeschlossen, aber die Eigenverifikation schlug fehl; das Paket ist keine Evidenz.");

                foreach (var violation in verification.Violations)
                {
                    Console.Error.WriteLine($"{violation.Class}: {violation.Path}: {violation.Detail}");
                }

                return (int)PlatformErrorCode.PackageVerificationFailed;
            }

            return ExitCodes.Ok;
        }
        finally
        {
            CleanDirectoryIfExists(Path.Combine(buildWorkDir, "verify"));
        }
    }

    private static int Verify(string archivePath, string? workDir)
    {
        var archiveFullPath = Path.GetFullPath(archivePath);
        var verifyWorkDir = Path.GetFullPath(workDir ?? Path.Combine(
            Path.GetDirectoryName(archiveFullPath) ?? ".",
            "package-verify-work"));

        PackageDirectoryVerification verification;

        try
        {
            verification = PackageVerifier.VerifyArchive(archiveFullPath, verifyWorkDir);
        }
        finally
        {
            if (Directory.Exists(verifyWorkDir))
            {
                Directory.Delete(verifyWorkDir, recursive: true);
            }
        }

        WriteReport(new
        {
            schemaVersion = PackageContract.SchemaVersion,
            command = "package",
            mode = "verify",
            archivePath = archiveFullPath,
            archiveSha256 = File.Exists(archiveFullPath) ? PackageManifestCodec.Sha256File(archiveFullPath) : null,
            ok = verification.Valid,
            violations = verification.Violations,
            artifactChecks = verification.ArtifactChecks,
        });

        return verification.Valid ? ExitCodes.Ok : ExitCodes.Map(PlatformErrorCode.PackageVerificationFailed);
    }

    private static void Publish(string repoRoot, string publishDir)
    {
        var projectPath = Path.Combine(repoRoot, "src", "Riftward.App", "Riftward.App.csproj");

        if (!File.Exists(projectPath))
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.PackageBuildFailed,
                "Anwendungsprojekt fehlt im Quellbaum.",
                projectPath));
        }

        // Der RID-Restore leitet die Lockdateien in das gitignorierte obj-gebiet
        // um (Restore-Regel des Paketvertrags Abschnitt 3): die versionierten
        // packages.lock.json bleiben unberührt, ein RID-Abschnitt würde den
        // vertraglichen locked Restore der Solution brechen.
        RunDotnet(
            repoRoot,
            $"restore \"{projectPath}\" -r linux-x64 -p:NuGetLockFilePath=obj/restore/packages.lock.json",
            "RID-Restore");
        RunDotnet(
            repoRoot,
            $"publish \"{projectPath}\" -c Release -r linux-x64 --self-contained true --no-restore -o \"{publishDir}\"",
            "Selbstenthaltener Publish");
    }

    private static void RunDotnet(string workingDirectory, string arguments, string label)
    {
        var startInfo = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };

        foreach (var token in Tokenize(arguments))
        {
            startInfo.ArgumentList.Add(token);
        }

        using var process = Process.Start(startInfo);
        var stdout = process!.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
        {
            var detail = (stderr.Length > 0 ? stderr : stdout);
            detail = detail.Length > 4000 ? detail[^4000..] : detail;

            throw new PlatformException(new PlatformError(
                PlatformErrorCode.PackageBuildFailed,
                $"{label} schlug fehl (Exitcode {process.ExitCode}).",
                detail));
        }
    }

    private static IEnumerable<string> Tokenize(string arguments)
    {
        var token = new System.Text.StringBuilder();
        var inQuotes = false;

        foreach (var character in arguments)
        {
            switch (character)
            {
                case '"':
                    inQuotes = !inQuotes;
                    break;
                case ' ' when !inQuotes:
                    if (token.Length > 0)
                    {
                        yield return token.ToString();
                        token.Clear();
                    }

                    break;
                default:
                    token.Append(character);
                    break;
            }
        }

        if (token.Length > 0)
        {
            yield return token.ToString();
        }
    }

    private static readonly JsonSerializerOptions ReportOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static void WriteReport(object report)
    {
        var json = JsonSerializer.Serialize(report, ReportOptions);
        Console.WriteLine(json);
    }

    private static void CleanDirectoryIfExists(string path)
    {
        if (Directory.Exists(path))
        {
            Directory.Delete(path, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(Environment.CurrentDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "Riftward.slnx")))
            {
                return current.FullName;
            }

            current = current.Parent;
        }

        throw new PlatformException(new PlatformError(
            PlatformErrorCode.PackageBuildFailed,
            "Paketbau erfordert den Quellbaum (Riftward.slnx wurde nicht gefunden).",
            Environment.CurrentDirectory));
    }
}
