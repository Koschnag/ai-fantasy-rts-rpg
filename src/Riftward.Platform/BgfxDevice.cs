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

    // -------------------------------------------------- T-023-Fassadenaufrufe

    /// <summary>bgfx-Caps-Bitmaske des aktiven Backends (gepinnter Stand).</summary>
    public ulong Caps => _api.Caps();

    /// <summary>BGFX_CAPS_TEXTURE_READ_BACK des gepinnten bgfx-Stands.</summary>
    public const ulong CapsTextureReadBack = 0x0000_0000_0200_0000ul;

    /// <summary>BGFX_CAPS_TEXTURE_BLIT des gepinnten bgfx-Stands.</summary>
    public const ulong CapsTextureBlit = 0x0000_0000_0004_0000ul;

    public bool IsReadBackSupported() => (_api.Caps() & CapsTextureReadBack) != 0;

    public bool IsBlitSupported() => (_api.Caps() & CapsTextureBlit) != 0;

    /// <summary>Legt einen Shader aus offline uebersetzten Bytes an.</summary>
    public ushort CreateShader(ReadOnlySpan<byte> shaderBytes)
    {
        ThrowIfNotInitialized();
        var handle = _api.CreateShader(shaderBytes, (uint)shaderBytes.Length);
        return EnsureHandle(handle, "Shader konnte nicht angelegt werden.");
    }

    public void DestroyShader(ushort shaderIndex)
    {
        ThrowIfNotInitialized();

        if (shaderIndex != InvalidIndex)
        {
            _api.DestroyShader(shaderIndex);
        }
    }

    /// <summary>Erzeugt ein Programm; Besitz der Shader verbleibt beim Aufrufer.</summary>
    public ushort CreateProgramFromShaders(ushort vertexShaderIndex, ushort fragmentShaderIndex)
    {
        ThrowIfNotInitialized();
        var handle = _api.CreateProgram(vertexShaderIndex, fragmentShaderIndex);
        return EnsureHandle(handle, "Programm konnte nicht erzeugt werden.");
    }

    public void DestroyProgram(ushort programIndex)
    {
        ThrowIfNotInitialized();

        if (programIndex != InvalidIndex)
        {
            _api.DestroyProgram(programIndex);
        }
    }

    /// <summary>
    /// Feste Graybox-Layoutkennungen der Shim-Grenze (riftbgfx_shim.h):
    /// 0 = Einheitenmesh (pos 3f, normal 4u8n, indices 4u8, weight 4u8n,
    /// Stride 24), 1 = Landschaft (pos 3f, normal 4u8n, color0 4u8n,
    /// Stride 20), 2 = Partikelquad (pos 2f, texcoord0 2f, Stride 16).
    /// </summary>
    public const byte LayoutUnitMesh = 0;
    public const byte LayoutTerrain = 1;
    public const byte LayoutParticleQuad = 2;

    public ushort CreateLayoutVertexBuffer(ReadOnlySpan<byte> vertexData, byte layoutId)
    {
        ThrowIfNotInitialized();
        var handle = _api.CreateLayoutVertexBuffer(vertexData, layoutId);
        return EnsureHandle(handle, "Vertex-Buffer konnte nicht angelegt werden.");
    }

    public void DestroyVertexBuffer(ushort vertexBufferIndex)
    {
        ThrowIfNotInitialized();

        if (vertexBufferIndex != InvalidIndex)
        {
            _api.DestroyVertexBuffer(vertexBufferIndex);
        }
    }

    /// <summary>Setzt Viewrechteck und Clear eines beliebigen Views.</summary>
    public void ConfigureRenderTargetView(byte viewId, uint clearColorRgba, int width, int height)
    {
        ThrowIfNotInitialized();
        _api.ViewSetup(viewId, clearColorRgba, (ushort)Math.Clamp(width, 1, ushort.MaxValue), (ushort)Math.Clamp(height, 1, ushort.MaxValue));
    }

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

    // -------------------------------------------------- T-023-Szenenressourcen

    /// <summary>Legt eine 2D-Textur an; ohne initiale Daten bleibt sie uninitialisiert.</summary>
    public ushort CreateTexture2D(int width, int height, int format, ulong flags, ReadOnlySpan<byte> initialData)
    {
        ThrowIfNotInitialized();
        var handle = _api.CreateTexture2D(width, height, format, flags, initialData);
        return EnsureHandle(handle, "Textur konnte nicht angelegt werden.");
    }

    /// <summary>Aktualisiert einen RGBA32F-Teilbereich einer dynamischen Textur.</summary>
    public void UpdateTexture2DRgba32F(ushort textureIndex, int x, int y, int width, int height, ReadOnlySpan<float> data)
    {
        ThrowIfNotInitialized();

        if (textureIndex == InvalidIndex)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.InvalidHandle,
                "Update auf ungueltige Texturhandle verweigert."));
        }

        _api.UpdateTexture2DRgba32F(textureIndex, x, y, width, height, data);
    }

    public void DestroyTexture(ushort textureIndex)
    {
        ThrowIfNotInitialized();

        if (textureIndex != InvalidIndex)
        {
            _api.DestroyTexture(textureIndex);
        }
    }

    public ushort CreateFrameBufferFromTexture(ushort textureIndex)
    {
        ThrowIfNotInitialized();

        if (textureIndex == InvalidIndex)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.InvalidHandle,
                "Framebuffer aus ungueltiger Texturhandle verweigert."));
        }

        var handle = _api.CreateFrameBufferFromTexture(textureIndex);
        return EnsureHandle(handle, "Framebuffer konnte nicht angelegt werden.");
    }

    public void DestroyFrameBuffer(ushort frameBufferIndex)
    {
        ThrowIfNotInitialized();

        if (frameBufferIndex != InvalidIndex)
        {
            _api.DestroyFrameBuffer(frameBufferIndex);
        }
    }

    public void SetViewFrameBuffer(byte viewId, ushort frameBufferIndex)
    {
        ThrowIfNotInitialized();
        _api.SetViewFrameBuffer(viewId, frameBufferIndex);
    }

    public void BlitFull(byte viewId, ushort destinationIndex, ushort sourceIndex, int width, int height)
    {
        ThrowIfNotInitialized();

        if (destinationIndex == InvalidIndex || sourceIndex == InvalidIndex)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.InvalidHandle,
                "Blit mit ungueltigem Texturhandle verweigert."));
        }

        _api.BlitFull(viewId, destinationIndex, sourceIndex, width, height);
    }

    public uint ReadTextureBegin(ushort textureIndex, nint outBuffer, uint bufferSizeBytes)
    {
        ThrowIfNotInitialized();

        if (textureIndex == InvalidIndex || outBuffer == 0 || bufferSizeBytes == 0)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.InvalidHandle,
                "Readback-Parameter ungueltig."));
        }

        return _api.ReadTextureBegin(textureIndex, outBuffer, bufferSizeBytes);
    }

    public ushort CreateUniform(string name, UniformType type, ushort count)
    {
        ThrowIfNotInitialized();
        var handle = _api.CreateUniform(name, type, count);
        return EnsureHandle(handle, $"Uniform '{name}' konnte nicht angelegt werden.");
    }

    public void DestroyUniform(ushort uniformIndex)
    {
        ThrowIfNotInitialized();

        if (uniformIndex != InvalidIndex)
        {
            _api.DestroyUniform(uniformIndex);
        }
    }

    public void SetUniformVec4(ushort uniformIndex, ReadOnlySpan<float> values)
    {
        ThrowIfNotInitialized();

        if (uniformIndex == InvalidIndex)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.InvalidHandle,
                "Uniform-Set mit ungueltigem Handle verweigert."));
        }

        _api.SetUniformVec4(uniformIndex, values);
    }

    public void SetUniformMat4(ushort uniformIndex, ReadOnlySpan<float> values16)
    {
        ThrowIfNotInitialized();

        if (uniformIndex == InvalidIndex)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.InvalidHandle,
                "Matrix-Uniform-Set mit ungueltigem Handle verweigert."));
        }

        if (values16.Length != 16)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.InvalidHandle,
                "Matrix-Uniform benoetigt genau 16 floats."));
        }

        _api.SetUniformMat4(uniformIndex, values16);
    }

    public void SetTexture(byte stage, ushort samplerUniformIndex, ushort textureIndex, ulong samplerFlags)
    {
        ThrowIfNotInitialized();

        if (samplerUniformIndex == InvalidIndex || textureIndex == InvalidIndex)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.InvalidHandle,
                "Texturbinding mit ungueltigem Handle verweigert."));
        }

        _api.SetTexture(stage, samplerUniformIndex, textureIndex, (uint)samplerFlags);
    }

    public ushort CreateIndexBuffer(ReadOnlySpan<byte> data, bool uint32Indices)
    {
        ThrowIfNotInitialized();
        var handle = _api.CreateIndexBuffer(data, uint32Indices);
        return EnsureHandle(handle, "Index-Buffer konnte nicht angelegt werden.");
    }

    public void DestroyIndexBuffer(ushort indexBufferIndex)
    {
        ThrowIfNotInitialized();

        if (indexBufferIndex != InvalidIndex)
        {
            _api.DestroyIndexBuffer(indexBufferIndex);
        }
    }

    /// <summary>
    /// Flexibler Draw: Vertex- und optionaler Index-Buffer, optionale
    /// Instanzdaten aus bereits festgepinntem Speicher, Renderzustand.
    /// </summary>
    public void DrawSubmit(
        byte viewId,
        ushort programIndex,
        ushort vertexBufferIndex,
        ushort indexBufferIndex,
        uint elementCount,
        nint instanceData,
        uint instanceCount,
        ushort instanceStride,
        ulong state)
    {
        ThrowIfNotInitialized();

        if (vertexBufferIndex == InvalidIndex || programIndex == InvalidIndex)
        {
            throw new PlatformException(new PlatformError(
                PlatformErrorCode.InvalidHandle,
                "Draw-Submit mit ungueltigem Handle verweigert."));
        }

        _api.DrawSubmit(
            viewId,
            programIndex,
            vertexBufferIndex,
            indexBufferIndex,
            elementCount,
            instanceData,
            instanceCount,
            instanceStride,
            state);
    }

    /// <summary>Kennzeichnung „kein Index-Buffer“ fuer <see cref="DrawSubmit"/>.</summary>
    public const ushort NoIndexBuffer = InvalidIndex;

    private static ushort EnsureHandle(ushort handle, string message)
    {
        if (handle == InvalidIndex)
        {
            throw new PlatformException(new PlatformError(PlatformErrorCode.InvalidHandle, message));
        }

        return handle;
    }

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

/// <summary>
/// T-023: Besitzregeln der erweiterten Renderressourcen (Texturen,
/// Framebuffer, Uniforms, Index-Buffer). Handles werden ausschliesslich
/// ueber <see cref="BgfxDevice"/> angelegt; die Freigabereihenfolge ist
/// verpflichtend Framebuffer -> Textur -> Uniform -> Index-Buffer und
/// gesamt vor dem bgfx-Shutdown (siehe docs/NATIVE_UNTERBAU.md).
/// Ungueltige Handleindizes (0xFFFF) werden nie weitergereicht.
/// </summary>
public static class BgfxSceneApi
{
    public const int TextureFormatRgba8 = 0;
    public const int TextureFormatRgba32F = 1;

    public const ulong TextureFlagRt = 0x0000_0010_0000_0000ul;
    public const ulong TextureFlagBlitDst = 0x0000_4000_0000_0000ul;
    public const ulong TextureFlagReadBack = 0x0000_8000_0000_0000ul;

    public const ulong SamplerClampU = 0x2ul;
    public const ulong SamplerClampV = 0x8ul;

    /* Spiegel des gepinnten bgfx-Stands (defines.h), testseitig gebunden.
     * Quellen: BGFX_STATE_WRITE_Z 0x0000_4000_0000_0000,
     * BGFX_STATE_DEPTH_TEST_LEQUAL 0x20, BGFX_STATE_CULL_CW
     * 0x0000_0010_0000_0000 sowie BGFX_STATE_BLEND_ALPHA
     * (SRC_ALPHA, INV_SRC_ALPHA) = 0x0656_5000 des Pins
     * bgfx 35a98dd6453cf25dc75c68e233abb400836d5920. */
    public const ulong StateWriteRgb = 0x7ul;
    public const ulong StateWriteAlpha = 0x8ul;
    public const ulong StateWriteZ = 0x4000_0000_0000ul;
    public const ulong StateDepthTestLequal = 0x20ul;
    public const ulong StateCullCw = 0x10_0000_0000ul;
    public const ulong StateBlendAlphaBits = 0x0656_5000ul;

    /// <summary>Renderzustand opake Geometrie: Depth-Test/-Write, Backface-Cull, Schreibmaske.</summary>
    public const ulong StateOpaque =
        StateWriteRgb | StateWriteAlpha | StateWriteZ | StateDepthTestLequal | StateCullCw;

    /// <summary>Renderzustand transparente Partikel: Alpha-Blend, kein Tiefenschreiben, kein Cull.</summary>
    public const ulong StateBlendAlpha =
        StateBlendAlphaBits | StateDepthTestLequal | StateWriteRgb | StateWriteAlpha;
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
