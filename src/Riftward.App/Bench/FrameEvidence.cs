using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Riftward.App.Bench;

/// <summary>
/// Opt-in Frame-Evidenzartefakt des Belastungsframes (T-023,
/// AC-T023-08): genau ein 1920x1080-Einzelabgriff einer deterministischen
/// Kameraposition an festem Frameindex strikt nach dem Messfenster. Die
/// Aussagegrenze ist ausschliesslich die Graybox-Lastbelegung - niemals
/// Gameplay-, Atmosphaeren- oder Shipping-Aussage; eine oeffentliche
/// Verwendung bleibt an die Oeffentlichexportbedingungen von
/// docs/communication/MEDIA_LAB.md plus Projektleitungsautorisierung
/// gebunden. Ohne Flag entsteht kein Bild.
/// </summary>
public static class FrameEvidence
{
    public const string FormatId = "bmp-32bpp-bottom-up";

    public const int BytesPerPixel = 4;
    public const int FileHeaderBytes = 14;
    public const int InfoHeaderBytes = 40;

    /// <summary>Gebundene Aussagegrenze des Artefakts (maschinenlesbar im Report).</summary>
    public const string StatementLimit = "graybox-load-composition-not-gameplay-atmosphere-or-shipping";

    /// <summary>Grundkennung fuer „kein Abgriff angefordert“.</summary>
    public const string ReasonNotRequested = "capture-not-requested";

    /// <summary>
    /// Kodiert RGBA-Oberflaeche (von oben nach unten) als unkomprimiertes
    /// 32-Bit-BMP mit unterster Zeile zuerst. Deterministische Bytes.
    /// </summary>
    public static byte[] EncodeBmpFromRgbaTopDown(ReadOnlySpan<byte> rgbaTopDown, int width, int height)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Abmessungen muessen positiv sein.");
        }

        var expected = width * height * BytesPerPixel;

        if (rgbaTopDown.Length != expected)
        {
            throw new ArgumentException(
                $"RGBA-Oberflaeche benoetigt exakt {expected} Bytes, erhalten {rgbaTopDown.Length}.",
                nameof(rgbaTopDown));
        }

        var stride = width * BytesPerPixel;
        var pixelBytes = stride * height;
        var file = new byte[FileHeaderBytes + InfoHeaderBytes + pixelBytes];

        // BITMAPFILEHEADER
        file[0] = (byte)'B';
        file[1] = (byte)'M';
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(2), (uint)file.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(10), (uint)(FileHeaderBytes + InfoHeaderBytes));

        // BITMAPINFOHEADER
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(14), InfoHeaderBytes);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(18), width);
        BinaryPrimitives.WriteInt32LittleEndian(file.AsSpan(22), height);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(26), 1);
        BinaryPrimitives.WriteUInt16LittleEndian(file.AsSpan(28), (ushort)(BytesPerPixel * 8));
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(30), 0u /* BI_RGB */);
        BinaryPrimitives.WriteUInt32LittleEndian(file.AsSpan(34), (uint)pixelBytes);

        for (var row = 0; row < height; row++)
        {
            var sourceOffset = row * stride;
            var targetOffset = FileHeaderBytes + InfoHeaderBytes + ((height - 1 - row) * stride);

            for (var pixel = 0; pixel < width; pixel++)
            {
                var source = sourceOffset + (pixel * BytesPerPixel);
                var target = targetOffset + (pixel * BytesPerPixel);

                // RGBA -> BGRA(X): Kanalreihenfolge drehen, Alpha als Fuelle.
                file[target + 0] = rgbaTopDown[source + 2];
                file[target + 1] = rgbaTopDown[source + 1];
                file[target + 2] = rgbaTopDown[source + 0];
                file[target + 3] = 0;
            }
        }

        return file;
    }

    public static string Sha256Hex(byte[] content) =>
        Convert.ToHexString(SHA256.HashData(content)).ToLowerInvariant();

    /// <summary>Entscheidungsregel der Messfenster-Reihenfolge (testseitig gebunden).</summary>
    public static bool IsCaptureFrameAllowed(int frameIndex, int warmupFrames, int sampleFrames) =>
        frameIndex >= RepresentativeScenario.CaptureFrameIndex(warmupFrames, sampleFrames);
}
