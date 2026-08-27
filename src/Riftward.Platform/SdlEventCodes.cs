namespace Riftward.Platform;

/// <summary>
/// SDL3-Ereigniscodes des gepinnten release-3.4.14-Stands als oeffentliche
/// Konstanten des Plattform-Layers. Die nativen Importdeklarationen bleiben
/// projektintern; ausserhalb des Plattform-Layers erscheinen nur diese Werte.
/// Die T-032-Erweiterung ergänzt die Maus-/Tastaturereignisse der
/// Graybox-Kommandoschleife (docs/KOMMANDOVERTRAG.md); Strukturoffsets sind
/// gegen den gepinnten Header <c>SDL_events.h</c> verifiziert.
/// </summary>
public static class SdlEventCodes
{
    public const uint Quit = 0x100u;
    public const uint WindowExposed = 0x204u;
    public const uint WindowResized = 0x206u;
    public const uint WindowPixelSizeChanged = 0x207u;
    public const uint WindowCloseRequested = 0x210u;

    /// <summary>SDL_EVENT_KEY_DOWN (SDL_events.h, gepinnt 0x300).</summary>
    public const uint KeyDown = 0x300u;

    /// <summary>SDL_EVENT_KEY_UP (gepinnt: 0x301).</summary>
    public const uint KeyUp = 0x301u;

    /// <summary>SDL_EVENT_MOUSE_MOTION (gepinnt: 0x400).</summary>
    public const uint MouseMotion = 0x400u;

    /// <summary>SDL_EVENT_MOUSE_BUTTON_DOWN (gepinnt: 0x401).</summary>
    public const uint MouseButtonDown = 0x401u;

    /// <summary>SDL_EVENT_MOUSE_BUTTON_UP (gepinnt: 0x402).</summary>
    public const uint MouseButtonUp = 0x402u;

    /// <summary>SDL_EVENT_MOUSE_WHEEL (gepinnt: 0x403).</summary>
    public const uint MouseWheel = 0x403u;
}

/// <summary>
/// SDL3-Maustastenindizes des gepinnten Standes (SDL_mouse.h): Links 1,
/// Mitte 2, Rechts 3. Die Maussemantik der Kommandoschleife ist im
/// Kommandovertrag Abschnitt 9 fixiert und nicht umbelegbar.
/// </summary>
public static class SdlMouseButtons
{
    public const byte Left = 1;
    public const byte Middle = 2;
    public const byte Right = 3;
}
