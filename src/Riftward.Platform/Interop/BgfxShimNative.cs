using System.Runtime.InteropServices;

namespace Riftward.Platform.Interop;

/// <summary>
/// Zentrale LibraryImport-Deklarationen fuer den eigenen bgfx-C-Shim
/// (libriftbgfx.so, ABI in src/Riftward.Native/riftbgfx_shim.h fixiert).
/// bgfx besitzt keine stabile C-ABI; der Shim ist die einzige native Grenze.
/// </summary>
internal static partial class BgfxShimNative
{
    public const string LibraryName = "riftbgfx";

    /// <summary>bgfx::RendererType::OpenGL im gepinnten Stand (Zaehlung ab Noop=0).</summary>
    public const int RendererOpenGL = 8;

    [LibraryImport(LibraryName)]
    internal static partial uint rift_bgfx_api_version();

    [LibraryImport(LibraryName)]
    internal static partial int rift_bgfx_init(in RiftBgfxInitParams parameters);

    [LibraryImport(LibraryName)]
    internal static partial int rift_bgfx_renderer_type();

    [LibraryImport(LibraryName)]
    internal static partial void rift_bgfx_gl_strings(
        out nint outVersion,
        out nint outRenderer,
        out nint outSlVersion);

    [LibraryImport(LibraryName)]
    internal static partial uint rift_bgfx_gpu_ids();

    [LibraryImport(LibraryName)]
    internal static partial void rift_bgfx_shutdown();

    [LibraryImport(LibraryName)]
    internal static partial uint rift_bgfx_frame();

    [LibraryImport(LibraryName)]
    internal static partial uint rift_bgfx_stats_draw_calls();

    [LibraryImport(LibraryName)]
    internal static partial int rift_bgfx_stats_snapshot(ref RiftBgfxStats stats);

    [LibraryImport(LibraryName)]
    internal static partial void rift_view_setup(byte viewId, uint clearColorRgba, ushort width, ushort height);

    [LibraryImport(LibraryName)]
    internal static partial void rift_view_transform(byte viewId, ReadOnlySpan<float> view16, ReadOnlySpan<float> proj16);

    [LibraryImport(LibraryName)]
    internal static partial ushort rift_tri_create_vertex_buffer(ReadOnlySpan<byte> data, uint sizeInBytes);

    [LibraryImport(LibraryName)]
    internal static partial ushort rift_tri_create_shader(ReadOnlySpan<byte> data, uint sizeInBytes);

    [LibraryImport(LibraryName)]
    [return: MarshalAs(UnmanagedType.U1)]
    internal static partial bool rift_tri_shader_is_valid(ushort shaderIndex);

    [LibraryImport(LibraryName)]
    internal static partial ushort rift_tri_create_program(ushort vertexShaderIndex, ushort fragmentShaderIndex);

    [LibraryImport(LibraryName)]
    internal static partial void rift_tri_destroy_program(ushort programIndex);

    [LibraryImport(LibraryName)]
    internal static partial void rift_tri_destroy_shader(ushort shaderIndex);

    [LibraryImport(LibraryName)]
    internal static partial void rift_tri_destroy_vertex_buffer(ushort vertexBufferIndex);

    [LibraryImport(LibraryName)]
    internal static partial void rift_tri_submit(byte viewId, ushort programIndex, ushort vertexBufferIndex);
}

/// <summary>Blittable Abbildung von rift_bgfx_init_params_t (siehe riftbgfx_shim.h).</summary>
[StructLayout(LayoutKind.Sequential)]
public struct RiftBgfxInitParams
{
    public nint Ndt;
    public nint Nwh;
    public uint Width;
    public uint Height;
    public uint ResetFlags;
}

/// <summary>
/// Blittable Abbildung von rift_bgfx_stats_t (T-020-Telemetrie; siehe
/// riftbgfx_shim.h). Alle Felder sind direkt von bgfx gemessene Groessen.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public struct RiftBgfxStats
{
    public uint NumDraw;
    public uint NumCompute;
    public uint TrianglesRendered;
    public long GpuTimeBegin;
    public long GpuTimeEnd;
    public long GpuTimerFreq;
    public long TextureMemoryUsed;
    public long RtMemoryUsed;
    public int TransientVbUsed;
    public int TransientIbUsed;
}
