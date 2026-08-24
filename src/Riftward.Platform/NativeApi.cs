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

    void Shutdown();

    uint Frame();

    uint DrawCalls();

    bool TryReadStats(out BgfxFrameStats stats);

    void ViewSetup(byte viewId, uint clearColorRgba, ushort width, ushort height);

    void ViewTransform(byte viewId, ReadOnlySpan<float> view16, ReadOnlySpan<float> proj16);

    ushort CreateVertexBuffer(ReadOnlySpan<byte> data, uint sizeInBytes);

    ushort CreateShader(ReadOnlySpan<byte> data, uint sizeInBytes);

    bool ShaderIsValid(ushort shaderIndex);

    ushort CreateProgram(ushort vertexShaderIndex, ushort fragmentShaderIndex);

    void DestroyProgram(ushort programIndex);

    void DestroyShader(ushort shaderIndex);

    void DestroyVertexBuffer(ushort vertexBufferIndex);

    void Submit(byte viewId, ushort programIndex, ushort vertexBufferIndex);
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

    public ushort CreateShader(ReadOnlySpan<byte> data, uint sizeInBytes) =>
        BgfxShimNative.rift_tri_create_shader(data, sizeInBytes);

    public bool ShaderIsValid(ushort shaderIndex) => BgfxShimNative.rift_tri_shader_is_valid(shaderIndex);

    public ushort CreateProgram(ushort vertexShaderIndex, ushort fragmentShaderIndex) =>
        BgfxShimNative.rift_tri_create_program(vertexShaderIndex, fragmentShaderIndex);

    public void DestroyProgram(ushort programIndex) => BgfxShimNative.rift_tri_destroy_program(programIndex);

    public void DestroyShader(ushort shaderIndex) => BgfxShimNative.rift_tri_destroy_shader(shaderIndex);

    public void DestroyVertexBuffer(ushort vertexBufferIndex) =>
        BgfxShimNative.rift_tri_destroy_vertex_buffer(vertexBufferIndex);

    public void Submit(byte viewId, ushort programIndex, ushort vertexBufferIndex) =>
        BgfxShimNative.rift_tri_submit(viewId, programIndex, vertexBufferIndex);
}
