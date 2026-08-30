using System.Globalization;
using System.Text;
using System.Text.Json;
using Riftward.Platform;

namespace Riftward.App.Package;

/// <summary>
/// Erzeugt die gebündelten Dokumente deterministisch: Release Notes mit
/// ehrlicher Alpha-Kennzeichnung und das Lizenz-/Attributionsmanifest aus
/// <c>toolchain.lock.json</c> und <c>THIRD_PARTY_NOTICES.md</c>. Kein Text
/// behauptet eine Projektlizenz (Q-PRD-001 bleibt OFFEN).
/// </summary>
public static class PackageDocs
{
    /// <summary>Schreibt docs/RELEASE_NOTES.md in das Stagingverzeichnis.</summary>
    public static void WriteReleaseNotes(
        string targetPath,
        PackageManifest manifest,
        string archiveFileName)
    {
        var text = new StringBuilder();
        text.AppendLine("# Riftward Interne Alpha — Release Notes");
        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"- **Paket:** {PackageContract.PackageId} {manifest.Package.Version}");
        text.AppendLine(CultureInfo.InvariantCulture, $"- **Archiv:** {archiveFileName} (Prüfsumme in der Sidecar-Datei {archiveFileName}.sha256)");
        text.AppendLine(CultureInfo.InvariantCulture, $"- **RID:** {manifest.Package.Rid}");
        text.AppendLine(CultureInfo.InvariantCulture, $"- **Runtimeform:** {manifest.Package.RuntimeForm}");
        text.AppendLine(CultureInfo.InvariantCulture, $"- **Quellbindung:** Commit {manifest.Source.CommitSha256}, Baum {manifest.Source.TreeSha256}");
        text.AppendLine(CultureInfo.InvariantCulture, $"- **Alpha-Marker:** {manifest.Package.AlphaMarker}");
        text.AppendLine();
        text.AppendLine("## Aussagegrenze (verbindlich)");
        text.AppendLine();
        text.AppendLine("Dieses Paket ist eine **interne Alpha** des Forschungsprojekts Riftward.");
        text.AppendLine("Es enthält ausschließlich die Graybox-Verifikationsschleife: Kommando-,");
        text.AppendLine("Mode-Switch-, Erkundungs-, Entscheidungs-, Druck- und Fortsetzungspfad");
        text.AppendLine("über der deterministischen Simulation. Es ist **kein** Gameplay-,");
        text.AppendLine("Atmosphären-, Performance- oder Shipping-Beleg. Es existiert kein fertiges");
        text.AppendLine("Spiel, kein Content und keine Musik. Pflichtprofile bleiben `NOT-MEASURED`");
        text.AppendLine("(Q-OPS-001). Die Projektlizenz ist nicht entschieden (Q-PRD-001); dieses");
        text.AppendLine("Paket begründet keine Weitergaberechte.");
        text.AppendLine();
        text.AppendLine("## Installation und Start (offline)");
        text.AppendLine();
        text.AppendLine("1. Archiv entpacken: `tar xzf " + archiveFileName + "`");
        text.AppendLine("2. In das entpackte Verzeichnis wechseln: `cd riftward-"
            + manifest.Package.Version + PackageContract.ArchiveRootSuffix + "`");
        text.AppendLine("3. Headless-Verifikation ohne Fenster: `./Riftward.App bench --scenario bench-sim --report PFAD`");
        text.AppendLine("4. Fensterpflichtiger Smoke auf einer Linux-Displaysession:");
        text.AppendLine("   `./Riftward.App plattformsmoke --artifacts-dir native --manifest native/artifact-hashes.json --report PFAD`");
        text.AppendLine("5. Interaktive Graybox-Schleife (Moduswechsel Tab, Speichern F5, Laden F9):");
        text.AppendLine("   `./Riftward.App kommandoschleife --scenario kommando-graybox --input-script fixtures/command/t036-pressure-restart.graybox --seed 20260826 --report PFAD --interactive --auto-exit-at-horizon --horizon-ticks 11000 --artifacts-dir native --manifest native/artifact-hashes.json --exploration --decision --pressure`");
        text.AppendLine();
        text.AppendLine("Das Paket benötigt kein installiertes .NET-SDK, kein Repository und kein");
        text.AppendLine("Netzwerk. Es werden nur Dateien im von Ihnen gewählten Verzeichnis");
        text.AppendLine("geschrieben (Reportpfade, Slots).");
        text.AppendLine();
        text.AppendLine("## Paketintegrität");
        text.AppendLine();
        text.AppendLine("- Dateiebene: `package-manifest.json` (SHA-256 je Datei) mit Anker");
        text.AppendLine("  `package-manifest.sha256`.");
        text.AppendLine("- Native Laufzeitartefakte: `native/artifact-hashes.json` wird vor jedem");
        text.AppendLine("  Fensterstart durch die eingebaute Host-Prüfung gegengebunden;");
        text.AppendLine("  Manipulationen werden kontrolliert abgewiesen (Exitcodes 14–17).");
        text.AppendLine("- Prüfkommando: `Riftward.App package --verify <archiv>`.");
        text.AppendLine();

        File.WriteAllText(targetPath, text.ToString(), new UTF8Encoding(false));
    }

    /// <summary>Schreibt docs/LIZENZEN.md deterministisch aus Lockfile und Notices.</summary>
    public static void WriteLicenses(
        string targetPath,
        string repoRoot,
        IReadOnlyList<NativeComponentInfo> nativeComponents,
        string dotnetRuntimeVersion)
    {
        var notices = File.ReadAllText(Path.Combine(repoRoot, "THIRD_PARTY_NOTICES.md"));

        var text = new StringBuilder();
        text.AppendLine("# Lizenz- und Attributionsmanifest (Gebündelte Komponenten)");
        text.AppendLine();
        text.AppendLine("Dieses Manifest wurde beim Paketbau deterministisch aus");
        text.AppendLine("`toolchain.lock.json` (nativeComponents) und `THIRD_PARTY_NOTICES.md`");
        text.AppendLine("des Quellbaums abgeleitet. Es lizenziert **nicht** den Projektcode oder");
        text.AppendLine("die Projektassets; die Projektlizenz ist bewusst offen (Q-PRD-001).");
        text.AppendLine();
        text.AppendLine("## Gebündelte native Komponenten");
        text.AppendLine();
        text.AppendLine("| Komponente | Pin | Lizenz | Zweck |");
        text.AppendLine("|---|---|---|---|");

        foreach (var component in nativeComponents)
        {
            var pin = component.RefType == "tag"
                ? $"Tag `{component.Ref}`, Commit `{component.Commit}`"
                : $"Commit `{component.Commit}`";
            text.AppendLine(CultureInfo.InvariantCulture, $"| {component.Id} | {pin} | {component.LicenseSpdx} | {component.Purpose} |");
        }

        text.AppendLine();
        text.AppendLine(CultureInfo.InvariantCulture, $"## Gebündelte .NET-Runtime ({dotnetRuntimeVersion})");
        text.AppendLine();
        text.AppendLine("Das Paket bündelt die Microsoft .NET-Runtime (CoreCLR, selbstenthalten,");
        text.AppendLine("ohne AOT und Trimming). Die Runtime steht unter der MIT-Lizenz:");
        text.AppendLine();
        text.AppendLine("MIT License");
        text.AppendLine();
        text.AppendLine("Copyright (c) .NET Foundation and Contributors");
        text.AppendLine();
        text.AppendLine("Permission is hereby granted, free of charge, to any person obtaining a copy");
        text.AppendLine("of this software and associated documentation files (the \"Software\"), to deal");
        text.AppendLine("in the Software without restriction, including without limitation the rights");
        text.AppendLine("to use, copy, modify, merge, publish, distribute, sublicense, and/or sell");
        text.AppendLine("copies of the Software, and to permit persons to whom the Software is");
        text.AppendLine("furnished to do so, subject to the following conditions:");
        text.AppendLine();
        text.AppendLine("The above copyright notice and this permission notice shall be included in all");
        text.AppendLine("copies or substantial portions of the Software.");
        text.AppendLine();
        text.AppendLine("THE SOFTWARE IS PROVIDED \"AS IS\", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR");
        text.AppendLine("IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,");
        text.AppendLine("FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE");
        text.AppendLine("AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER");
        text.AppendLine("LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,");
        text.AppendLine("OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE");
        text.AppendLine("SOFTWARE.");
        text.AppendLine();
        text.AppendLine("## Vollständige Drittanbieterhinweise des Quellbaums");
        text.AppendLine();
        text.AppendLine("Die folgenden Abschnitte sind der unveränderte Inhalt von");
        text.AppendLine("`THIRD_PARTY_NOTICES.md` des gebundenen Quellbaums:");
        text.AppendLine();
        text.AppendLine("---");
        text.AppendLine();
        text.Append(notices);

        File.WriteAllText(targetPath, text.ToString(), new UTF8Encoding(false));
    }

    /// <summary>Liest die Native-Komponenten aus dem Toolchain-Lock (versioniert, klauselgebunden).</summary>
    public static IReadOnlyList<NativeComponentInfo> ReadNativeComponents(string toolchainLockPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(toolchainLockPath));
        var root = document.RootElement;

        if (!root.TryGetProperty("nativeComponents", out var components) || components.ValueKind != JsonValueKind.Array)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.PackageBuildFailed,
                "Toolchain-Lock besitzt keine nativeComponents-Liste.",
                toolchainLockPath));
        }

        var purposeById = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["sdl3"] = "Fenster, Ereignisse, Eingabe",
            ["bgfx"] = "Renderabstraktion (OpenGL-3.3-Core-Pflichtpfad)",
            ["bx"] = "bgfx-Grundbibliothek",
            ["bimg"] = "bgfx-Bildbibliothek",
        };

        var result = new List<NativeComponentInfo>();
        foreach (var element in components.EnumerateArray())
        {
            var id = element.GetProperty("id").GetString()!;
            result.Add(new NativeComponentInfo(
                id,
                element.TryGetProperty("refType", out var refType) ? refType.GetString() ?? "commit" : "commit",
                element.TryGetProperty("ref", out var reference) ? reference.GetString() ?? element.GetProperty("commit").GetString()! : element.GetProperty("commit").GetString()!,
                element.GetProperty("commit").GetString()!,
                element.GetProperty("licenseSpdx").GetString()!,
                purposeById.TryGetValue(id, out var purpose) ? purpose : id));
        }

        if (result.Count == 0)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.PackageBuildFailed,
                "Toolchain-Lock listet keine gebündelten Native-Komponenten.",
                toolchainLockPath));
        }

        return result;
    }

    /// <summary>Version der gebündelten .NET-Runtime aus dem laufenden Prozess.</summary>
    public static string DotnetRuntimeVersion() =>
        System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription;

    /// <summary>
    /// Liest die gebundene Pin-Kohorte aus dem Toolchain-Lock; alle
    /// kompatibilitätsgebundenen Komponenten müssen exakt eine Kohorte teilen.
    /// </summary>
    public static string ReadPinCohort(string toolchainLockPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(toolchainLockPath));
        var root = document.RootElement;

        if (!root.TryGetProperty("nativeComponents", out var components) || components.ValueKind != JsonValueKind.Array)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.PackageBuildFailed,
                "Toolchain-Lock besitzt keine nativeComponents-Liste.",
                toolchainLockPath));
        }

        var cohorts = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var element in components.EnumerateArray())
        {
            if (element.TryGetProperty("compatibilityKey", out var key) && key.ValueKind == JsonValueKind.String)
            {
                cohorts.Add(key.GetString()!);
            }
        }

        if (cohorts.Count != 1)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.PackageBuildFailed,
                "Toolchain-Lock bindet keine eindeutige Native-Pin-Kohorte.",
                string.Join(',', cohorts)));
        }

        return cohorts.Single();
    }

    /// <summary>Information über eine gebündelte native Komponente.</summary>
    public sealed record NativeComponentInfo(
        string Id,
        string RefType,
        string Ref,
        string Commit,
        string LicenseSpdx,
        string Purpose);
}
