using System.Runtime.InteropServices;
using Riftward.Platform.Interop;

namespace Riftward.Platform;

/// <summary>
/// Verwaltete Abbildung der nativen SDL3-Aufrufe fuer den Plattform-Layer.
/// Die Schnittstelle ist die Testnaht: Produktion verwendet
/// <see cref="Default"/>; Fixtures ersetzen sie durch kontrollierte Fakes.
/// </summary>
public interface ISdlApi
{
    bool Init(uint flags);

    void Quit();

    nint CreateWindow(string title, int width, int height, ulong flags);

    void DestroyWindow(nint window);

    /// <summary>T-033: Fenstertitel des Mindest-HUD setzen (Modevertrag Abschnitt 8).</summary>
    bool SetWindowTitle(nint window, string title);

    bool PollEvent(ref SdlEventBuffer eventBuffer);

    uint GetWindowProperties(nint window);

    long GetNumberProperty(uint properties, string name, long defaultValue);

    nint GetPointerProperty(uint properties, string name, nint defaultValue);

    string GetError();
}

public interface IBgfxApi
{
    uint ApiVersion();

    int Init(RiftBgfxInitParams parameters);

    int RendererType();

    (string Version, string Renderer, string SlVersion) GlStrings();

    uint GpuIds();

    ulong Caps();

    void Shutdown();

    uint Frame();

    uint DrawCalls();

    bool TryReadStats(out BgfxFrameStats stats);

    void ViewSetup(byte viewId, uint clearColorRgba, ushort width, ushort height);

    void ViewTransform(byte viewId, ReadOnlySpan<float> view16, ReadOnlySpan<float> proj16);

    ushort CreateVertexBuffer(ReadOnlySpan<byte> data, uint sizeInBytes);

    ushort CreateLayoutVertexBuffer(ReadOnlySpan<byte> data, byte layoutId);

    ushort CreateShader(ReadOnlySpan<byte> shaderBytes, uint sizeInBytes);

    bool ShaderIsValid(ushort shaderIndex);

    ushort CreateProgram(ushort vertexShaderIndex, ushort fragmentShaderIndex);

    void DestroyProgram(ushort programIndex);

    void DestroyShader(ushort shaderIndex);

    void DestroyVertexBuffer(ushort vertexBufferIndex);

    void Submit(byte viewId, ushort programIndex, ushort vertexBufferIndex);

    // ------------------------------------------------- T-023-Shim-Erweiterung

    ushort CreateTexture2D(int width, int height, int format, ulong flags, ReadOnlySpan<byte> initialData);

    void UpdateTexture2DRgba32F(ushort textureIndex, int x, int y, int width, int height, ReadOnlySpan<float> data);

    void DestroyTexture(ushort textureIndex);

    ushort CreateFrameBufferFromTexture(ushort textureIndex);

    void DestroyFrameBuffer(ushort frameBufferIndex);

    void SetViewFrameBuffer(byte viewId, ushort frameBufferIndex);

    void BlitFull(byte viewId, ushort destinationIndex, ushort sourceIndex, int width, int height);

    uint ReadTextureBegin(ushort textureIndex, nint outBuffer, uint bufferSizeBytes);

    ushort CreateUniform(string name, UniformType type, ushort count);

    void DestroyUniform(ushort uniformIndex);

    void SetUniformVec4(ushort uniformIndex, ReadOnlySpan<float> values);

    void SetUniformMat4(ushort uniformIndex, ReadOnlySpan<float> values16);

    void SetTexture(byte stage, ushort samplerUniformIndex, ushort textureIndex, uint samplerFlags);

    ushort CreateIndexBuffer(ReadOnlySpan<byte> data, bool uint32Indices);

    void DestroyIndexBuffer(ushort indexBufferIndex);

    void DrawSubmit(
        byte viewId,
        ushort programIndex,
        ushort vertexBufferIndex,
        ushort indexBufferIndex,
        uint elementCount,
        nint instanceData,
        uint instanceCount,
        ushort instanceStride,
        ulong state);
}

/// <summary>Shim-kompatible Uniformtypkennungen (riftbgfx_shim.h).</summary>
public enum UniformType
{
    Vec4 = 0,
    Mat4 = 1,
    Sampler = 2,
}

/// <summary>
/// Produktionsbindung der Interfaces an die LibraryImport-Deklarationen.
/// Enthaelt keinerlei Zustand; Besitz regeln ausschließlich die Fassaden.
/// </summary>
public sealed class NativeApi : ISdlApi, IBgfxApi
{
    public static NativeApi Instance { get; } = new();

    public bool Init(uint flags) => Sdl3Native.SDL_Init(flags);

    public void Quit() => Sdl3Native.SDL_Quit();

    public nint CreateWindow(string title, int width, int height, ulong flags) =>
        Sdl3Native.SDL_CreateWindow(title, width, height, flags);

    public void DestroyWindow(nint window) => Sdl3Native.SDL_DestroyWindow(window);

    public bool SetWindowTitle(nint window, string title) => Sdl3Native.SDL_SetWindowTitle(window, title);

    public bool PollEvent(ref SdlEventBuffer eventBuffer) => Sdl3Native.SDL_PollEvent(ref eventBuffer);

    public uint GetWindowProperties(nint window) => Sdl3Native.SDL_GetWindowProperties(window);

    public long GetNumberProperty(uint properties, string name, long defaultValue) =>
        Sdl3Native.SDL_GetNumberProperty(properties, name, defaultValue);

    public nint GetPointerProperty(uint properties, string name, nint defaultValue) =>
        Sdl3Native.SDL_GetPointerProperty(properties, name, defaultValue);

    public string GetError() => Marshal.PtrToStringUTF8(Sdl3Native.SDL_GetError()) ?? string.Empty;

    public uint ApiVersion() => BgfxShimNative.rift_bgfx_api_version();

    public int Init(RiftBgfxInitParams parameters) => BgfxShimNative.rift_bgfx_init(in parameters);

    public int RendererType() => BgfxShimNative.rift_bgfx_renderer_type();

    public (string Version, string Renderer, string SlVersion) GlStrings()
    {
        BgfxShimNative.rift_bgfx_gl_strings(out var version, out var renderer, out var slVersion);
        return (
            Marshal.PtrToStringUTF8(version) ?? string.Empty,
            Marshal.PtrToStringUTF8(renderer) ?? string.Empty,
            Marshal.PtrToStringUTF8(slVersion) ?? string.Empty);
    }

    public uint GpuIds() => BgfxShimNative.rift_bgfx_gpu_ids();

    public ulong Caps() => BgfxShimNative.rift_bgfx_caps();

    public void Shutdown() => BgfxShimNative.rift_bgfx_shutdown();

    public uint Frame() => BgfxShimNative.rift_bgfx_frame();

    public uint DrawCalls() => BgfxShimNative.rift_bgfx_stats_draw_calls();

    public bool TryReadStats(out BgfxFrameStats stats)
    {
        var native = new RiftBgfxStats();

        if (BgfxShimNative.rift_bgfx_stats_snapshot(ref native) == 0)
        {
            stats = BgfxFrameStats.FromNative(native);
            return true;
        }

        stats = default;
        return false;
    }

    public void ViewSetup(byte viewId, uint clearColorRgba, ushort width, ushort height) =>
        BgfxShimNative.rift_view_setup(viewId, clearColorRgba, width, height);

    public void ViewTransform(byte viewId, ReadOnlySpan<float> view16, ReadOnlySpan<float> proj16) =>
        BgfxShimNative.rift_view_transform(viewId, view16, proj16);

    public ushort CreateVertexBuffer(ReadOnlySpan<byte> data, uint sizeInBytes) =>
        BgfxShimNative.rift_tri_create_vertex_buffer(data, sizeInBytes);

    public ushort CreateLayoutVertexBuffer(ReadOnlySpan<byte> data, byte layoutId) =>
        BgfxShimNative.rift_vb_create_layout(data, (uint)data.Length, layoutId);

    public ushort CreateShader(ReadOnlySpan<byte> shaderBytes, uint sizeInBytes) =>
        BgfxShimNative.rift_tri_create_shader(shaderBytes, sizeInBytes);

    public bool ShaderIsValid(ushort shaderIndex) => BgfxShimNative.rift_tri_shader_is_valid(shaderIndex);

    public ushort CreateProgram(ushort vertexShaderIndex, ushort fragmentShaderIndex) =>
        BgfxShimNative.rift_tri_create_program(vertexShaderIndex, fragmentShaderIndex);

    public void DestroyProgram(ushort programIndex) => BgfxShimNative.rift_tri_destroy_program(programIndex);

    public void DestroyShader(ushort shaderIndex) => BgfxShimNative.rift_tri_destroy_shader(shaderIndex);

    public void DestroyVertexBuffer(ushort vertexBufferIndex) =>
        BgfxShimNative.rift_tri_destroy_vertex_buffer(vertexBufferIndex);

    public void Submit(byte viewId, ushort programIndex, ushort vertexBufferIndex) =>
        BgfxShimNative.rift_tri_submit(viewId, programIndex, vertexBufferIndex);

    // -------------------------------------------------- T-023-Shim-Erweiterung

    public ushort CreateTexture2D(int width, int height, int format, ulong flags, ReadOnlySpan<byte> initialData) =>
        BgfxShimNative.rift_tex_create_2d(
            (ushort)width,
            (ushort)height,
            format,
            flags,
            initialData,
            (uint)initialData.Length);

    public void UpdateTexture2DRgba32F(ushort textureIndex, int x, int y, int width, int height, ReadOnlySpan<float> data) =>
        BgfxShimNative.rift_tex_update_2d(
            textureIndex,
            (ushort)x,
            (ushort)y,
            (ushort)width,
            (ushort)height,
            data,
            (uint)(data.Length * sizeof(float)),
            (ushort)(width * 4 * sizeof(float)));

    public void DestroyTexture(ushort textureIndex) => BgfxShimNative.rift_tex_destroy(textureIndex);

    public ushort CreateFrameBufferFromTexture(ushort textureIndex) =>
        BgfxShimNative.rift_fb_create_single(textureIndex);

    public void DestroyFrameBuffer(ushort frameBufferIndex) => BgfxShimNative.rift_fb_destroy(frameBufferIndex);

    public void SetViewFrameBuffer(byte viewId, ushort frameBufferIndex) =>
        BgfxShimNative.rift_view_frame_buffer(viewId, frameBufferIndex);

    public void BlitFull(byte viewId, ushort destinationIndex, ushort sourceIndex, int width, int height) =>
        BgfxShimNative.rift_blit_full(viewId, destinationIndex, sourceIndex, (ushort)width, (ushort)height);

    public uint ReadTextureBegin(ushort textureIndex, nint outBuffer, uint bufferSizeBytes) =>
        BgfxShimNative.rift_read_texture_begin(textureIndex, outBuffer, bufferSizeBytes);

    public ushort CreateUniform(string name, UniformType type, ushort count) =>
        BgfxShimNative.rift_uniform_create(name, (int)type, count);

    public void DestroyUniform(ushort uniformIndex) => BgfxShimNative.rift_uniform_destroy(uniformIndex);

    public void SetUniformVec4(ushort uniformIndex, ReadOnlySpan<float> values) =>
        BgfxShimNative.rift_set_uniform_vec4(uniformIndex, values, (ushort)(values.Length / 4));

    public void SetUniformMat4(ushort uniformIndex, ReadOnlySpan<float> values16) =>
        BgfxShimNative.rift_set_uniform_mat4(uniformIndex, values16);

    public void SetTexture(byte stage, ushort samplerUniformIndex, ushort textureIndex, uint samplerFlags) =>
        BgfxShimNative.rift_set_texture(stage, samplerUniformIndex, textureIndex, samplerFlags);

    public ushort CreateIndexBuffer(ReadOnlySpan<byte> data, bool uint32Indices) =>
        BgfxShimNative.rift_ib_create(data, (uint)data.Length, uint32Indices ? 1 : 0);

    public void DestroyIndexBuffer(ushort indexBufferIndex) => BgfxShimNative.rift_ib_destroy(indexBufferIndex);

    public void DrawSubmit(
        byte viewId,
        ushort programIndex,
        ushort vertexBufferIndex,
        ushort indexBufferIndex,
        uint elementCount,
        nint instanceData,
        uint instanceCount,
        ushort instanceStride,
        ulong state) => BgfxShimNative.rift_draw_submit(
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
