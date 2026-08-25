using System.IO;

namespace Riftward.App.Bench;

/// <summary>
/// Allokationsarmer Working-Set-Stichprobennehmer: persistent geoeffneter
/// Dateihandle auf /proc/self/status mit wiederverwendetem Bytepuffer;
/// Stichproben waehrend der Messphase verursachen keine verwaltete
/// Allokation und faelschen damit das Allokationsbudget nicht.
/// Gemeinsam von bench-sim (T-021) und bench-representative (T-023) genutzt.
/// </summary>
internal sealed class RssSampler : IDisposable
{
    private const string StatusPath = "/proc/self/status";
    private const string Marker = "VmRSS:";

    private readonly FileStream? _stream;
    private readonly byte[] _buffer = new byte[8192];

    private RssSampler(FileStream? stream) => _stream = stream;

    public bool Measured { get; private set; }

    public long? MinKiB { get; private set; }

    public long? MaxKiB { get; private set; }

    public long? EndKiB { get; private set; }

    public string? Reason { get; private set; }

    public static RssSampler? TryCreate()
    {
        try
        {
            return new RssSampler(
                new FileStream(StatusPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite));
        }
        catch (FileNotFoundException)
        {
            return Unavailable("proc-self-status-unavailable");
        }
        catch (IOException)
        {
            return Unavailable("proc-self-status-unavailable");
        }
        catch (UnauthorizedAccessException)
        {
            return Unavailable("proc-self-status-forbidden");
        }
    }

    private static RssSampler Unavailable(string reason) => new(null)
    {
        Reason = reason,
    };

    public void Sample()
    {
        if (Reason is not null || _stream is null)
        {
            return;
        }

        try
        {
            _stream.Seek(0, SeekOrigin.Begin);
            var length = _stream.Read(_buffer, 0, _buffer.Length);
            var value = ParseVmRssKiB(_buffer.AsSpan(0, length));

            if (value is not { } kiB)
            {
                Reason ??= "vmrss-line-missing";
                return;
            }

            Measured = true;
            MinKiB = MinKiB is { } minimum ? Math.Min(minimum, kiB) : kiB;
            MaxKiB = MaxKiB is { } maximum ? Math.Max(maximum, kiB) : kiB;
            EndKiB = kiB;
        }
        catch (IOException exception)
        {
            Reason ??= $"proc-read-failed:{exception.GetType().Name}";
        }
    }

    internal (bool Measured, long? MinKiB, long? MaxKiB, long? EndKiB, string? Reason) Snapshot() =>
        (Measured, MinKiB, MaxKiB, EndKiB, Reason);

    /// <summary>Parst die VmRSS-Zeile ohne Stringallokation direkt aus Bytes.</summary>
    private static long? ParseVmRssKiB(ReadOnlySpan<byte> source)
    {
        var marker = Marker;

        for (var index = 0; index <= source.Length - marker.Length; index++)
        {
            var matches = true;

            for (var offset = 0; offset < marker.Length; offset++)
            {
                if ((char)source[index + offset] != marker[offset])
                {
                    matches = false;
                    break;
                }
            }

            if (!matches)
            {
                continue;
            }

            var cursor = index + marker.Length;
            long value = 0;
            var digits = 0;

            while (cursor < source.Length)
            {
                var character = source[cursor];

                if (character is >= (byte)'0' and <= (byte)'9')
                {
                    value = (value * 10) + (character - (byte)'0');
                    digits++;
                    cursor++;
                    continue;
                }

                if (digits > 0)
                {
                    break;
                }

                cursor++;
            }

            return digits > 0 && digits <= 12 ? value : null;
        }

        return null;
    }

    public void Dispose() => _stream?.Dispose();
}
