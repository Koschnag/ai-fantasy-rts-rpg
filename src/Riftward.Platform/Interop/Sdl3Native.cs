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
