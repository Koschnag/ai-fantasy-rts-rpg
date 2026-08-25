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
    internal static partial ulong rift_bgfx_caps();

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
    internal static partial ushort rift_vb_create_layout(ReadOnlySpan<byte> data, uint sizeInBytes, byte layoutId);

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

    // ---------------------------------------------------- T-023-Shim-Erweiterung

    [LibraryImport(LibraryName)]
    internal static partial ushort rift_tex_create_2d(
        ushort width,
        ushort height,
        int format,
        ulong flags,
        ReadOnlySpan<byte> data,
        uint sizeInBytes);

    [LibraryImport(LibraryName)]
    internal static partial void rift_tex_update_2d(
        ushort textureIndex,
        ushort x,
        ushort y,
        ushort width,
        ushort height,
        ReadOnlySpan<float> data,
        uint sizeInBytes,
        ushort pitch);

    [LibraryImport(LibraryName)]
    internal static partial void rift_tex_destroy(ushort textureIndex);

    [LibraryImport(LibraryName)]
    internal static partial ushort rift_fb_create_single(ushort textureIndex);

    [LibraryImport(LibraryName)]
    internal static partial void rift_fb_destroy(ushort frameBufferIndex);

    [LibraryImport(LibraryName)]
    internal static partial void rift_view_frame_buffer(byte viewId, ushort frameBufferIndex);

    [LibraryImport(LibraryName)]
    internal static partial void rift_blit_full(
        byte viewId,
        ushort destinationIndex,
        ushort sourceIndex,
        ushort width,
        ushort height);

    [LibraryImport(LibraryName)]
    internal static partial uint rift_read_texture_begin(
        ushort textureIndex,
        nint outBuffer,
        uint bufferSizeBytes);

    [LibraryImport(LibraryName, StringMarshalling = StringMarshalling.Utf8)]
    internal static partial ushort rift_uniform_create(
        string name,
        int type,
        ushort count);

    [LibraryImport(LibraryName)]
    internal static partial void rift_uniform_destroy(ushort uniformIndex);

    [LibraryImport(LibraryName)]
    internal static partial void rift_set_uniform_vec4(
        ushort uniformIndex,
        ReadOnlySpan<float> values,
        ushort count);

    [LibraryImport(LibraryName)]
    internal static partial void rift_set_uniform_mat4(
        ushort uniformIndex,
        ReadOnlySpan<float> values16);

    [LibraryImport(LibraryName)]
    internal static partial void rift_set_texture(
        byte stage,
        ushort uniformIndex,
        ushort textureIndex,
        uint samplerFlags);

    [LibraryImport(LibraryName)]
    internal static partial ushort rift_ib_create(
        ReadOnlySpan<byte> data,
        uint sizeInBytes,
        int uint32Indices);

    [LibraryImport(LibraryName)]
    internal static partial void rift_ib_destroy(ushort indexBufferIndex);

    [LibraryImport(LibraryName)]
    internal static partial void rift_draw_submit(
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
