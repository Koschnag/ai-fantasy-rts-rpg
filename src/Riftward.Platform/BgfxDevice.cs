using Riftward.Platform.Interop;

namespace Riftward.Platform;

/// <summary>
/// Besitzregeln bgfx (explizit, siehe docs/NATIVE_UNTERBAU.md):
/// - Genau ein <see cref="BgfxDevice"/> pro Prozess initialisiert bgfx.
/// - Shader-, Programm- und Vertex-Buffer-Handles gehoeren
///   <see cref="TriangleResources"/>; Freigabe ausschließlich dort und in der
///   festen Reihenfolge Programm -> Shader -> Vertex-Buffer.
/// - bgfx-Shutdown erst nach allen Ressourcen; sonst kontrollierter Fehler
///   (<see cref="PlatformErrorCode.WrongShutdownOrder"/>).
/// - Ungueltige Handleindizes (0xFFFF) werden nie weitergereicht; sie erzeugen
///   kontrollierte Fehler (<see cref="PlatformErrorCode.InvalidHandle"/>).
/// </summary>
public sealed class BgfxDevice : IDisposable
{
    public const byte ViewId = 0;
    public const ushort InvalidIndex = 0xFFFF;

    /// <summary>bgfx::RendererType::OpenGL im gepinnten Stand (Zaehlung ab Noop=0).</summary>
    public const int RendererOpenGL = Interop.BgfxShimNative.RendererOpenGL;

    private readonly IBgfxApi _api;
    private readonly List<TriangleResources> _resources = new();
    private uint _clearColorRgba;
    private int _width;
    private int _height;
    private bool _initialized;
    private bool _shutdown;

    public BgfxDevice(IBgfxApi? api = null)
    {
        _api = api ?? NativeApi.Instance;
    }

    /// <summary>bgfx-API-Version des gebundenen Shims (fuer Reports).</summary>
    public uint ApiVersion => _api.ApiVersion();

    public static BgfxDevice Initialize(BgfxInitRequest request, IBgfxApi? api = null)
    {
        var device = new BgfxDevice(api);
        device.Initialize(request);
        return device;
    }

    public void Initialize(BgfxInitRequest request)
    {
        if (_initialized)
        {
            return;
        }

        var parameters = new RiftBgfxInitParams
        {
            Ndt = request.NativeDisplay,
            Nwh = request.NativeWindow,
            Width = (uint)request.Width,
            Height = (uint)request.Height,
            ResetFlags = request.ResetFlags,
        };

        // Der Shim schaltet bgfx vor init() selbst in den Single-Threaded-Modus
        // (renderFrame im rift_bgfx_init); der GL-Kontext bleibt damit am
        // aufrufenden Thread und die Renderer-Diagnose lesbar.
        if (_api.Init(parameters) != 0)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.BackendInitFailed,
                "bgfx konnte mit dem angeforderten Backend nicht initialisiert werden."));
        }

        var rendererType = _api.RendererType();

        if (rendererType != RendererOpenGL)
        {
            _api.Shutdown();
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.BackendInitFailed,
                $"Unerwartetes Renderer-Backend aktiv ({rendererType}); OpenGL 3.3 Core ist Pflichtpfad."));
        }

        _api.ViewSetup(ViewId, request.ClearColorRgba, (ushort)request.Width, (ushort)request.Height);
        _clearColorRgba = request.ClearColorRgba;
        _width = request.Width;
        _height = request.Height;
        _initialized = true;
    }

    /// <summary>Passt View-Rechteck und Clear nach einer Fenstergroessenaenderung an.</summary>
    public void Resize(int width, int height)
    {
        ThrowIfNotInitialized();

        if (width <= 0 || height <= 0 || width > ushort.MaxValue || height > ushort.MaxValue)
        {
            return;
        }

        _width = width;
        _height = height;
        _api.ViewSetup(ViewId, _clearColorRgba, (ushort)width, (ushort)height);
    }

    /// <summary>Aktuell gesetzte Renderaufloesung.</summary>
    public (int Width, int Height) Resolution => (_width, _height);

    /// <summary>Legt Dreiecksressourcen an. Daten stammen aus geprueften Artefakten.</summary>
    public TriangleResources CreateTriangleResources(ReadOnlySpan<byte> vertexData, ReadOnlySpan<byte> vertexShader, ReadOnlySpan<byte> fragmentShader)
    {
        ThrowIfNotInitialized();

        var vertexBuffer = _api.CreateVertexBuffer(vertexData, (uint)vertexData.Length);

        if (vertexBuffer == InvalidIndex)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.InvalidHandle,
                "Vertex-Buffer konnte nicht angelegt werden (ungueltiges Handle)."));
        }

        var vertexShaderHandle = _api.CreateShader(vertexShader, (uint)vertexShader.Length);
        var fragmentShaderHandle = _api.CreateShader(fragmentShader, (uint)fragmentShader.Length);

        if (vertexShaderHandle == InvalidIndex || fragmentShaderHandle == InvalidIndex)
        {
            DestroySafely(vertexShaderHandle, fragmentShaderHandle, vertexBuffer);
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.InvalidHandle,
                "Shader konnte nicht geladen werden (ungueltiges Handle)."));
        }

        if (!_api.ShaderIsValid(vertexShaderHandle) || !_api.ShaderIsValid(fragmentShaderHandle))
        {
            DestroySafely(vertexShaderHandle, fragmentShaderHandle, vertexBuffer);
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.InvalidHandle,
                "Geladener Shader ist ungueltig."));
        }

        var program = _api.CreateProgram(vertexShaderHandle, fragmentShaderHandle);

        if (program == InvalidIndex)
        {
            DestroySafely(vertexShaderHandle, fragmentShaderHandle, vertexBuffer);
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.InvalidHandle,
                "Programm konnte nicht erzeugt werden (ungueltiges Handle)."));
        }

        var resources = new TriangleResources(this, _api, program, vertexShaderHandle, fragmentShaderHandle, vertexBuffer);
        _resources.Add(resources);
        return resources;
    }

    public uint DrawCalls => _api.DrawCalls();

    /// <summary>
    /// Liest die bgfx-Statistik des letzten Frames (T-020-Telemetrie).
    /// Liefert false, wenn der Shim noch keine Statistik liefern kann.
    /// </summary>
    public bool TryReadStats(out BgfxFrameStats stats) => _api.TryReadStats(out stats);

    /// <summary>Setzt View-/Projektionsmatrix eines Views (je 16 floats, bx-Layout).</summary>
    public void SetViewTransform(byte viewId, ReadOnlySpan<float> view16, ReadOnlySpan<float> proj16)
    {
        ThrowIfNotInitialized();
        _api.ViewTransform(viewId, view16, proj16);
    }

    /// <summary>Schliesst den aktuellen Frame ab (bgfx::frame, Single-Threaded).</summary>
    public uint RenderFrame() => _api.Frame();

    internal void Forget(TriangleResources resources) => _resources.Remove(resources);

    public void Dispose()
    {
        if (_shutdown)
        {
            return;
        }

        if (_resources.Count > 0)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.WrongShutdownOrder,
                $"bgfx-Shutdown erst nach allen Ressourcen ({_resources.Count} offen); Reihenfolge: Programm, Shader, Vertex-Buffer."));
        }

        if (_initialized)
        {
            _api.Shutdown();
            _initialized = false;
        }

        _shutdown = true;
    }

    private void DestroySafely(ushort vertexShader, ushort fragmentShader, ushort vertexBuffer)
    {
        if (fragmentShader != InvalidIndex)
        {
            _api.DestroyShader(fragmentShader);
        }

        if (vertexShader != InvalidIndex)
        {
            _api.DestroyShader(vertexShader);
        }

        if (vertexBuffer != InvalidIndex)
        {
            _api.DestroyVertexBuffer(vertexBuffer);
        }
    }

    private void ThrowIfNotInitialized()
    {
        if (!_initialized || _shutdown)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.InvalidHandle,
                "bgfx ist nicht initialisiert oder bereits beendet."));
        }
    }
}

/// <summary>
/// Besitzt die vier Handles eines Skeleton-Dreiecks. Dispose gibt in fester
/// Reihenfolge frei: Programm -> Fragment-Shader -> Vertex-Shader ->
/// Vertex-Buffer. Doppeltes Dispose ist definiert als No-op.
/// </summary>
public sealed class TriangleResources : IDisposable
{
    private readonly BgfxDevice _device;
    private readonly IBgfxApi _api;
    private readonly ushort _program;
    private readonly ushort _vertexShader;
    private readonly ushort _fragmentShader;
    private readonly ushort _vertexBuffer;
    private bool _disposed;

    internal TriangleResources(
        BgfxDevice device,
        IBgfxApi api,
        ushort program,
        ushort vertexShader,
        ushort fragmentShader,
        ushort vertexBuffer)
    {
        _device = device;
        _api = api;
        _program = program;
        _vertexShader = vertexShader;
        _fragmentShader = fragmentShader;
        _vertexBuffer = vertexBuffer;
    }

    public ushort ProgramIndex
    {
        get
        {
            ThrowIfDisposed();
            return _program;
        }
    }

    public ushort VertexBufferIndex
    {
        get
        {
            ThrowIfDisposed();
            return _vertexBuffer;
        }
    }

    /// <summary>Reicht das Dreieck fuer den aktuellen Frame ein.</summary>
    public void Submit()
    {
        ThrowIfDisposed();
        _api.Submit(BgfxDevice.ViewId, _program, _vertexBuffer);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        // Feste Freigabereihenfolge laut Shim-Vertrag.
        _api.DestroyProgram(_program);
        _api.DestroyShader(_fragmentShader);
        _api.DestroyShader(_vertexShader);
        _api.DestroyVertexBuffer(_vertexBuffer);

        _disposed = true;
        _device.Forget(this);
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.InvalidHandle,
                "Dreiecksressourcen wurden bereits freigegeben."));
        }
    }
}

/// <summary>Initialisierungsanfrage fuer <see cref="BgfxDevice"/>.</summary>
public readonly record struct BgfxInitRequest(
    nint NativeDisplay,
    nint NativeWindow,
    int Width,
    int Height,
    uint ResetFlags,
    uint ClearColorRgba);

/// <summary>
/// Verwaltete Momentaufnahme der bgfx-Framestatistik (T-020-Telemetrie).
/// Alle Werte stammen unveraendert aus bgfx::Stats; keine eigene Berechnung.
/// GpuTimerFreq == 0 bedeutet: Das Backend stellt keine GPU-Zeit bereit.
/// </summary>
public readonly record struct BgfxFrameStats(
    uint NumDraw,
    uint NumCompute,
    uint TrianglesRendered,
    long GpuTimeBeginTicks,
    long GpuTimeEndTicks,
    long GpuTimerFrequency,
    long TextureMemoryUsedBytes,
    long RtMemoryUsedBytes,
    int TransientVbUsedBytes,
    int TransientIbUsedBytes)
{
    /// <summary>Summe der bgfx-verwalteten GPU-Speicherbytes (Textur + Renderziel + transient).</summary>
    public long ManagedGpuMemoryUsedBytes => TextureMemoryUsedBytes + RtMemoryUsedBytes + TransientVbUsedBytes + TransientIbUsedBytes;

    internal static BgfxFrameStats FromNative(Interop.RiftBgfxStats native) => new(
        native.NumDraw,
        native.NumCompute,
        native.TrianglesRendered,
        native.GpuTimeBegin,
        native.GpuTimeEnd,
        native.GpuTimerFreq,
        native.TextureMemoryUsed,
        native.RtMemoryUsed,
        native.TransientVbUsed,
        native.TransientIbUsed);
}
