namespace Riftward.App.Command;

/// <summary>
/// Opt-in Frame-Evidenzartefakt der Kommandoschleife (T-032/T-033,
/// Kommandovertrag Abschnitt 10, Modevertrag Abschnitt 8): mit Flag genau
/// ein Abgriffpaar von je einem 1920x1080-Einzelabgriff pro Modus über
/// demselben Weltzustand am selben Tick, strikt nach dem Messfenster, je
/// hashgebunden im Report. Die Aussagegrenze ist ausschließlich die
/// Graybox-Zustandsbelegung (Auswahl-/Befehls-/Heldmarker über der
/// Vertragswelt) — niemals Gameplay-, Atmosphären- oder Shipping-Aussage;
/// öffentliche Verwendung bleibt an docs/communication/MEDIA_LAB.md plus
/// Projektleitungsautorisierung gebunden. Ohne Flag entsteht keine Datei.
/// </summary>
public static class CommandFrameEvidence
{
    /// <summary>Gebundene Aussagegrenze des Artefakts (maschinenlesbar im Report).</summary>
    public const string StatementLimit = "graybox-state-occupancy-not-gameplay-atmosphere-or-shipping";

    /// <summary>Grundkennung fuer „kein Abgriff angefordert“.</summary>
    public const string ReasonNotRequested = "capture-not-requested";

    /// <summary>
    /// Grundkennung fuer ein byteidentisches Moduspaar (T-033): beide Abgriffe
    /// über demselben Weltzustand müssen sich durch Kamera- und Badgekanal
    /// unterscheidbar sein; Gleichheit ist ein Capturedefekt, kein Beleg.
    /// </summary>
    public const string ReasonPairFramesIdentical = "capture-pair-frames-identical";

    /// <summary>
    /// Grundkennung fuer einen uniformen Einzelabgriff (T-033): ein Frame ohne
    /// jede Pixelvariation — insbesondere vollständig schwarz (BGRA 0/0/0/255
    /// wie beim ungebindenen Renderziel beobachtet) — belegt keine
    /// Graybox-Szene und ist ein Capturedefekt, kein Beleg.
    /// </summary>
    public const string ReasonFrameUniform = "capture-frame-uniform";

    /// <summary>
    /// Grundkennung fuer malformed oder zu kurze Abgriffbytes (T-033,
    /// fail-closed): der Guard validiert Kopf- und Längenstruktur, bevor er
    /// pixelweise vergleicht.
    /// </summary>
    public const string ReasonFrameMalformed = "capture-frame-malformed";

    /// <summary>
    /// Fail-closed Paarprüfung vor dem Schreiben und vor jedem Erfolgsausweis:
    /// byteidentische Frames, uniforme Einzelframes (pixelweise kanalgleich,
    /// BGRA-interleaved inklusive Alpha) und malformed/zu kurze Bytes werden
    /// mit vertraglicher Grundkennung abgewiesen; Rückgabe null bei
    /// unterscheidbaren, belegbaren Frames.
    /// </summary>
    public static string? AnalyzeCapturePair(byte[] first, byte[] second)
    {
        if (!IsWellFormedBitmap(first) || !IsWellFormedBitmap(second))
        {
            return ReasonFrameMalformed;
        }

        if (first.AsSpan().SequenceEqual(second))
        {
            return ReasonPairFramesIdentical;
        }

        if (IsUniform(first) || IsUniform(second))
        {
            return ReasonFrameUniform;
        }

        return null;
    }

    /// <summary>
    /// Strukturprüfung (fail-closed): BMP-Kennung, 54-Byte-Kopf und ein
    /// ganzzahliges Vielfaches eines 4-Byte-BGRA-Pixels hinter dem Kopf.
    /// </summary>
    internal static bool IsWellFormedBitmap(byte[] bitmap) =>
        bitmap.Length > 54
        && bitmap[0] == (byte)'B'
        && bitmap[1] == (byte)'M'
        && ((bitmap.Length - 54) % 4) == 0;

    /// <summary>
    /// Ein Frame ist uniform, wenn jeder weitere 4-Byte-BGRA-Pixel kanalgleich
    /// zum ersten Pixel ist (BGRA ist interleaved; der bytegenaue Vergleich
    /// gegen ein Referenzbyte würde uniform schwarze Frames mit gefülltem
    /// X-/Alpha-Kanal fälschlich durchlassen). Die Graybox-Szene variiert
    /// durch Landschaft, Einheiten und Marker stets.
    /// </summary>
    internal static bool IsUniform(byte[] bitmap)
    {
        for (var pixel = 58; pixel + 4 <= bitmap.Length; pixel += 4)
        {
            if (bitmap[pixel] != bitmap[54]
                || bitmap[pixel + 1] != bitmap[55]
                || bitmap[pixel + 2] != bitmap[56]
                || bitmap[pixel + 3] != bitmap[57])
            {
                return false;
            }
        }

        return true;
    }
}
