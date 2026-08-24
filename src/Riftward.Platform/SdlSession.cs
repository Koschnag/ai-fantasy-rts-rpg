using Riftward.Platform.Interop;

namespace Riftward.Platform;

/// <summary>
/// Besitzregeln SDL3 (explizit, siehe docs/NATIVE_UNTERBAU.md):
/// - Genau ein <see cref="SdlSession"/> pro Prozess initialisiert SDL_INIT_VIDEO.
/// - <see cref="Window"/>-Handles gehoeren der Sitzung; Freigabe nur durch
///   Dispose des Fensters, danach ist das Handle ungueltig.
/// - Dispose-Reihenfolge: alle Fenster vor der Sitzung; die Sitzung verweigert
///   sonst kontrolliert (<see cref="PlatformErrorCode.WrongShutdownOrder"/>).
/// </summary>
public sealed class SdlSession : IDisposable
{
    private readonly ISdlApi _api;
    private readonly List<Window> _windows = new();
    private bool _initialized;
    private bool _disposed;

    public SdlSession(ISdlApi? api = null)
    {
        _api = api ?? NativeApi.Instance;
    }

    /// <summary>Anzahl derzeit lebender Fenster (fuer Reports und Tests).</summary>
    public int LiveWindowCount => _windows.Count;

    public static SdlSession Start(ISdlApi? api = null)
    {
        var session = new SdlSession(api);
        session.Initialize();
        return session;
    }

    public void Initialize()
    {
        ThrowIfDisposed();

        if (_initialized)
        {
            return;
        }

        if (!_api.Init(Sdl3Native.InitVideo))
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.WindowFailed,
                "SDL3-Videoinitialisierung fehlgeschlagen.",
                _api.GetError()));
        }

        _initialized = true;
    }

    public Window CreateWindow(string title, int width, int height, bool resizable = true)
    {
        ThrowIfDisposed();

        if (!_initialized)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.InvalidHandle,
                "Fenstererzeugung vor Initialisierung der Video-Sitzung ist ungueltig."));
        }

        var flags = resizable ? Sdl3Native.WindowResizable : 0ul;
        var handle = _api.CreateWindow(title, width, height, flags);

        if (handle == 0)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.WindowFailed,
                "SDL3-Fenster konnte nicht erzeugt werden.",
                _api.GetError()));
        }

        var window = new Window(this, _api, handle);
        _windows.Add(window);
        return window;
    }

    internal void Forget(Window window) => _windows.Remove(window);

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_windows.Count > 0)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.WrongShutdownOrder,
                $"SDL-Sitzung darf erst nach allen Fenstern beendet werden ({_windows.Count} offen)."));
        }

        if (_initialized)
        {
            _api.Quit();
            _initialized = false;
        }

        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.InvalidHandle,
                "SDL-Sitzung wurde bereits beendet."));
        }
    }
}

/// <summary>Besitzt genau einen nativen SDL-Fensterhandle.</summary>
public sealed class Window : IDisposable
{
    private readonly SdlSession _session;
    private readonly ISdlApi _api;
    private nint _handle;
    private bool _disposed;

    internal Window(SdlSession session, ISdlApi api, nint handle)
    {
        _session = session;
        _api = api;
        _handle = handle;
    }

    /// <summary>Roher Handle fuer Plattformdaten; 0 nach Freigabe.</summary>
    public nint Handle
    {
        get
        {
            ThrowIfDisposed();
            return _handle;
        }
    }

    /// <summary>(X11-Display, X11-Fenster-ID) als bgfx-plattformdaten.</summary>
    public (nint Display, ulong WindowId) NativeDisplayAndWindow =>
        (_api.GetPointerProperty(_api.GetWindowProperties(Handle), Sdl3Native.PropX11Display, 0),
         checked((ulong)Math.Max(0, _api.GetNumberProperty(_api.GetWindowProperties(Handle), Sdl3Native.PropX11Window, 0))));

    public void Dispose()
    {
        if (_disposed)
        {
            // Doppelte Freigabe ist definiert als No-op.
            return;
        }

        _api.DestroyWindow(_handle);
        _handle = 0;
        _disposed = true;
        _session.Forget(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.InvalidHandle,
                "Fensterhandle wurde bereits freigegeben."));
        }
    }
}
