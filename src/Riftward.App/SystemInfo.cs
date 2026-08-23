using System.Diagnostics;
using System.Text.Json;

namespace Riftward.App;

/// <summary>
/// Lokale, minimale Umgebungserfassung fuer Smoke- und Effizienzreports
/// (linux-x64-Pflichtpfad). Liest nur /proc-Dateien; es erfolgt kein Schreib-
/// zugriff und keine Netzwerkkommunikation.
/// </summary>
public static class SystemInfo
{
    public sealed record Environment(
        string Platform,
        string OsType,
        string KernelRelease,
        string CpuModel,
        string CpuFlagsExcerpt,
        int LogicalCores);

    public static Environment Capture()
    {
        var osType = ReadFirstLine("/proc/sys/kernel/ostype") ?? "Linux";
        var kernelRelease = ReadFirstLine("/proc/sys/kernel/osrelease") ?? "unbekannt";
        var (model, flags) = ReadCpuModelAndFlags();
        var platform = $"{System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}-"
            + System.Runtime.InteropServices.RuntimeInformation.OSDescription.Trim();

        return new Environment(
            platform,
            osType,
            kernelRelease,
            model,
            flags,
            System.Environment.ProcessorCount);
    }

    /// <summary>Residenter Speicher des eigenen Prozesses in KiB (VmRSS) oder null.</summary>
    public static long? RssKiB()
    {
        try
        {
            foreach (var line in File.ReadLines("/proc/self/status"))
            {
                if (line.StartsWith("VmRSS:", StringComparison.Ordinal))
                {
                    var digits = line.SkipWhile(static character => character is not ('0' or >= '1' and <= '9'))
                        .TakeWhile(static character => char.IsDigit(character))
                        .ToArray();

                    return long.TryParse(digits, out var value) ? value : null;
                }
            }
        }
        catch (FileNotFoundException)
        {
        }
        catch (IOException)
        {
        }

        return null;
    }

    private static (string Model, string Flags) ReadCpuModelAndFlags()
    {
        try
        {
            string? model = null;
            string? flags = null;

            foreach (var line in File.ReadLines("/proc/cpuinfo"))
            {
                if (model is null && line.StartsWith("model name", StringComparison.Ordinal))
                {
                    model = ValueAfterColon(line);
                }

                if (flags is null && line.StartsWith("flags", StringComparison.Ordinal))
                {
                    // Kompakter Auszug: die ersten 24 Flags reichen als Nachweis.
                    var all = ValueAfterColon(line);
                    flags = string.Join(' ', all.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(24));
                }

                if (model is not null && flags is not null)
                {
                    break;
                }
            }

            return (model ?? "unbekannt", flags ?? "unbekannt");
        }
        catch (FileNotFoundException)
        {
            return ("unbekannt", "unbekannt");
        }
        catch (IOException)
        {
            return ("unbekannt", "unbekannt");
        }
    }

    private static string ValueAfterColon(string line)
    {
        var index = line.IndexOf(':');
        return index < 0 ? line : line[(index + 1)..].Trim();
    }

    private static string? ReadFirstLine(string path)
    {
        try
        {
            using var reader = new StreamReader(path);
            return reader.ReadLine()?.Trim();
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (IOException)
        {
            return null;
        }
    }
}

/// <summary>Gelesene Pin-/Artefaktdaten aus toolchain.lock.json und Hashmanifest.</summary>
public sealed record ToolchainPin(
    string Id,
    string RefType,
    string Ref,
    string Commit,
    string SourceSha256,
    string LicenseSpdx);

public static class ToolchainLockReader
{
    public static IReadOnlyList<ToolchainPin> ReadNativeComponents(string lockPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(lockPath));
        var components = document.RootElement.GetProperty("nativeComponents");
        var pins = new List<ToolchainPin>();

        foreach (var component in components.EnumerateArray())
        {
            pins.Add(new ToolchainPin(
                component.GetProperty("id").GetString() ?? "?",
                component.TryGetProperty("refType", out var refType) ? refType.GetString() ?? "?" : "commit",
                component.TryGetProperty("ref", out var reference) ? reference.GetString() ?? component.GetProperty("commit").GetString()! : component.GetProperty("commit").GetString()!,
                component.GetProperty("commit").GetString() ?? "?",
                component.GetProperty("sourceSha256").GetString() ?? "?",
                component.GetProperty("licenseSpdx").GetString() ?? "?"));
        }

        return pins;
    }
}

/// <summary>Kleine Hilfen fuer Reportmessungen.</summary>
public static class Measurement
{
    /// <summary>p99 der Frametime-Messreihe in Millisekunden (naechstgroessere Ordnungsstatistik).</summary>
    public static double Percentile99(double[] sortedFrameTimesMs)
    {
        if (sortedFrameTimesMs.Length == 0)
        {
            return double.NaN;
        }

        Array.Sort(sortedFrameTimesMs);
        var index = (int)Math.Ceiling(0.99 * sortedFrameTimesMs.Length) - 1;
        return sortedFrameTimesMs[Math.Clamp(index, 0, sortedFrameTimesMs.Length - 1)];
    }

    public static double TimestampDeltaToMilliseconds(long startTimestamp, long endTimestamp) =>
        (endTimestamp - startTimestamp) * 1000.0 / Stopwatch.Frequency;
}
