namespace Riftward.Platform;

/// <summary>
/// SDL3-Ereigniscodes des gepinnten release-3.4.14-Stands als oeffentliche
/// Konstanten des Plattform-Layers. Die nativen Importdeklarationen bleiben
/// projektintern; ausserhalb des Plattform-Layers erscheinen nur diese Werte.
/// </summary>
public static class SdlEventCodes
{
    public const uint Quit = 0x100u;
    public const uint WindowExposed = 0x204u;
    public const uint WindowResized = 0x206u;
    public const uint WindowPixelSizeChanged = 0x207u;
    public const uint WindowCloseRequested = 0x210u;
}
