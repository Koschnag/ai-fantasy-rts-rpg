using System.Runtime.InteropServices;

namespace Riftward.Platform.Interop;
/// <summary>
/// Zentrale SDL3-LibraryImport-Deklarationen (nur der fuer das Walking-Skeleton
/// benoetigte Video-/Ereignis-Subset). Diese Typen verlassen den Plattform-Layer
/// nicht; die Fassaden in Riftward.Platform uebersetzen sie in verwaltete,
/// besitzgerechte Objekte.
///
/// Konstanten entsprechen dem gepinnten SDL3-Stand release-3.4.14
/// (Commit 147a8ee32dbf9ac02f3794964490687b6bbda1bc) und sind dort verifiziert.
/// </summary>
internal static partial class Sdl3Native
{
    /// <summary>Importname, unter dem die Resolver-Verankerung sucht.</summary>
    public const string LibraryKey = "SDL3";

    /// <summary>SDL_INIT_VIDEO aus SDL_init.h (0x20, impliziert Ereignis-Subsystem).</summary>
    public const uint InitVideo = 0x0000_0020u;

    /// <summary>SDL_WINDOW_RESIZABLE aus SDL_video.h.</summary>
    public const ulong WindowResizable = 0x0000_0000_0000_0020ul;

    public const uint EventQuit = 0x100u;
    public const uint EventWindowExposed = 0x204u;
    public const uint EventWindowResized = 0x206u;
    public const uint EventWindowPixelSizeChanged = 0x207u;
    public const uint EventWindowCloseRequested = 0x210u;

    // T-032: Maus-/Tastaturereignisse des gepinnten release-3.4.14-Stands
    // (SDL_events.h; Werte dort gegen die Aufzaehlung verifiziert).
    public const uint EventKeyDown = 0x300u;
    public const uint EventKeyUp = 0x301u;
    public const uint EventMouseMotion = 0x400u;
    public const uint EventMouseButtonDown = 0x401u;
    public const uint EventMouseButtonUp = 0x402u;
    public const uint EventMouseWheel = 0x403u;

    public const string PropX11Display = "SDL.window.x11.display";
    public const string PropX11Window = "SDL.window.x11.window";

    [LibraryImport("SDL3")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool SDL_Init(uint flags);

    [LibraryImport("SDL3")]
    internal static partial void SDL_Quit();

    [LibraryImport("SDL3", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint SDL_CreateWindow(string title, int width, int height, ulong flags);

    [LibraryImport("SDL3")]
    internal static partial void SDL_DestroyWindow(nint window);

    [LibraryImport("SDL3")]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool SDL_PollEvent(ref SdlEventBuffer @event);

    [LibraryImport("SDL3")]
    internal static partial uint SDL_GetWindowProperties(nint window);

    [LibraryImport("SDL3", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial long SDL_GetNumberProperty(uint properties, string name, long defaultValue);

    [LibraryImport("SDL3", StringMarshalling = StringMarshalling.Utf8)]
    internal static partial nint SDL_GetPointerProperty(uint properties, string name, nint defaultValue);

    [LibraryImport("SDL3")]
    internal static partial nint SDL_GetError();
}

/// <summary>
/// Speichergenaue Abbildung des Kopfes von SDL_Event (Union, 128 Bytes Gesamtgroesse).
/// Nur das type-Feld am Offset 0 wird ausgewertet; die restlichen Bytes bleiben
/// uninterpretiert. Damit ist kein SDL-Typ im verwalteten Objektmodell noetig.
/// </summary>
[StructLayout(LayoutKind.Sequential, Size = 128)]
public struct SdlEventBuffer
{
    /// <summary>SDL_EventType (Uint32) liegt im gepinnten Stand am Offset 0.</summary>
    public uint Type;
}

/// <summary>
/// T-032: Interpretationssicht fuer die Maus-/Tastaturereignisse der
/// Graybox-Kommandoschleife auf demselben 128-Byte-Unionpuffer. Die Offsets
/// sind gegen den gepinnten SDL3-Stand (release-3.4.14) verifiziert und
/// speichern keine zweiten Wahrheiten: Sie lesen dieselben Bytes wie der
/// native Unionzugriff.
///
/// Verifizierte Layouts (SDL_events.h):
/// - SDL_KeyboardEvent:   type@0, reserved@4, timestamp@8, windowID@16,
///   which@20, scancode@24 (Int32), key@28 (Uint32), mod@32 (Uint16),
///   raw@34 (Uint16), down@36 (bool), repeat@37 (bool).
/// - SDL_MouseMotionEvent: type@0, ..., which@20, state@24 (Uint32),
///   x@28 (float), y@32 (float), xrel@36, yrel@40.
/// - SDL_MouseButtonEvent: type@0, ..., button@24 (Uint8), down@25 (Uint8),
///   clicks@26 (Uint8), padding@27, x@28 (float), y@32 (float).
/// - SDL_MouseWheelEvent:  type@0, ..., x@24 (float), y@28 (float).
/// </summary>
[StructLayout(LayoutKind.Explicit, Size = 128)]
public struct SdlInputEventView
{
    [FieldOffset(0)] public uint Type;

    // ---- SDL_KeyboardEvent-Sicht -------------------------------------
    [FieldOffset(24)] public int Scancode;
    [FieldOffset(28)] public uint Key;
    [FieldOffset(36)] public byte KeyDownByte;
    [FieldOffset(37)] public byte KeyRepeatByte;

    // ---- SDL_MouseButtonEvent-Sicht ----------------------------------
    [FieldOffset(24)] public byte ButtonIndex;
    [FieldOffset(25)] public byte ButtonDownByte;
    [FieldOffset(26)] public byte ClickCount;

    // ---- SDL_MouseMotionEvent-/SDL_MouseButtonEvent-Koordinaten ------
    [FieldOffset(28)] public float PositionX;
    [FieldOffset(32)] public float PositionY;

    // ---- SDL_MouseWheelEvent-Sicht -----------------------------------
    [FieldOffset(24)] public float WheelX;
    [FieldOffset(28)] public float WheelY;

    /// <summary>Taste ist gedrueckt (nur KEY_DOWN/KEY_UP interpretierbar).</summary>
    public readonly bool KeyIsDown => KeyDownByte != 0;

    /// <summary>Tastenwiederholung (bool repeat im gepinnten Stand).</summary>
    public readonly bool KeyIsRepeat => KeyRepeatByte != 0;

    /// <summary>Maustaste ist gedrueckt (nur BUTTON_DOWN/BUTTON_UP).</summary>
    public readonly bool ButtonIsDown => ButtonDownByte != 0;

    /// <summary>
    /// Liest die Unionbytes eines abgefragten Ereignisses als Eingabesicht.
    /// Beide Strukturen sind exakt 128 Bytes gross; der Kopiervorgang ist
    /// bytegenau und interpretiert nichts ueber die deklarierten Felder hinaus.
    /// </summary>
    public static SdlInputEventView FromBuffer(ref SdlEventBuffer buffer)
    {
        var view = default(SdlInputEventView);
        MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref buffer, 1))
            .CopyTo(MemoryMarshal.AsBytes(MemoryMarshal.CreateSpan(ref view, 1)));
        return view;
    }
}
