using System.Security.Cryptography;
using System.Text.Json;
using Riftward.Platform;

namespace Riftward.App;

/// <summary>
/// Gemeinsame Startprozedur: Artefakte hashpruefen, native Bibliotheken laden,
/// Fenster und OpenGL-3.3-Core-Backend initialisieren, Dreieck anlegen.
/// Fehler werden als <see cref="PlatformException"/> kontrolliert gemeldet;
/// Teilaufbauten werden in umgekehrter Reihenfolge wieder abgebaut.
/// </summary>
public static class HostBootstrap
{
    /// <summary>VSync-Bit aus bgfx BGFX_RESET_VSYNC (gepinnter Stand).</summary>
    public const uint ResetVSync = 0x80u;

    /// <summary>Eigenes neutrales Clear-Farbmuster des Skeletons (RGBA).</summary>
    public const uint ClearColorRgba = 0x1018_20FFu;

    public sealed record Context(
        SdlSession Session,
        Window Window,
        BgfxDevice Device,
        TriangleResources Triangle,
        IReadOnlyList<ToolchainPin> Pins,
        ArtifactCatalogReport ArtifactReport,
        string ManifestSha256);

    public static Context Start(CommandLineArgs arguments, int width, int height, bool vsync)
    {
        var workspaceRoot = arguments.Option("--workspace") ?? ".";
        var artifactsDir = arguments.Option("--artifacts-dir") ?? ".ai/runtime/cache/native/dist";
        var manifestPath = arguments.Option("--manifest") ?? ".ai/runtime/cache/native/artifact-hashes.json";
        var lockPath = arguments.Option("--lock") ?? "toolchain.lock.json";

        var artifactReport = NativeArtifacts.Validate(workspaceRoot, manifestPath);

        if (!artifactReport.Valid)
        {
            throw new PlatformException(artifactReport.FirstFailure()!);
        }

        var sdlPath = Path.Combine(artifactsDir, "lib", "libSDL3.so.0");
        var shimPath = Path.Combine(artifactsDir, "lib", "libriftbgfx.so");
        NativeLibraryLoader.EnsureLoaded(sdlPath, shimPath);

        var session = SdlSession.Start();
        Window window;

        try
        {
            window = session.CreateWindow("Riftward T-010 Skeleton", width, height);
        }
        catch
        {
            session.Dispose();
            throw;
        }

        try
        {
            var (display, windowId) = window.NativeDisplayAndWindow;

            if (display == 0 || windowId == 0)
            {
                throw new PlatformException(new PlatformError(
                    PlatformErrorCode.WindowFailed,
                    "X11-Fensterdaten fehlen; der bgfx-OpenGL-Pflichtpfad benoetigt sie."));
            }

            var device = BgfxDevice.Initialize(
                new BgfxInitRequest(
                    display,
                    unchecked((nint)windowId),
                    width,
                    height,
                    vsync ? ResetVSync : 0u,
                    ClearColorRgba));

            try
            {
                var vertexShader = File.ReadAllBytes(Path.Combine(artifactsDir, "shaders", "triangle.vs.bin"));
                var fragmentShader = File.ReadAllBytes(Path.Combine(artifactsDir, "shaders", "triangle.fs.bin"));
                var triangle = device.CreateTriangleResources(TriangleGeometry.Vertices, vertexShader, fragmentShader);

                return new Context(
                    session,
                    window,
                    device,
                    triangle,
                    ToolchainLockReader.ReadNativeComponents(lockPath),
                    artifactReport,
                    HashFile(manifestPath));
            }
            catch
            {
                device.Dispose();
                throw;
            }
        }
        catch
        {
            window.Dispose();
            session.Dispose();
            throw;
        }
    }

    /// <summary>Kontrollierter Abbau in fester Reihenfolge: Ressourcen, Device, Fenster, Sitzung.</summary>
    public static void Stop(Context context)
    {
        context.Triangle.Dispose();
        context.Device.Dispose();
        context.Window.Dispose();
        context.Session.Dispose();
    }

    public static string HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}

/// <summary>Festes Vertexlayout des Skeleton-Dreiecks: pos 3xf32 + color0 4xu8 (Stride 16 Bytes).</summary>
public static class TriangleGeometry
{
    public static byte[] Vertices { get; } = Build();

    private static byte[] Build()
    {
        var data = new byte[3 * 16];

        Span<float> positions =
        [
            -0.5f, -0.5f, 0f,
             0.5f, -0.5f, 0f,
             0.0f,  0.5f, 0f,
        ];

        ReadOnlySpan<byte> colors = [230, 80, 70, 255, 90, 200, 120, 255, 70, 130, 240, 255];

        for (var vertexIndex = 0; vertexIndex < 3; vertexIndex++)
        {
            var offset = vertexIndex * 16;

            for (var component = 0; component < 3; component++)
            {
                BitConverter.GetBytes(positions[(vertexIndex * 3) + component]).CopyTo(data.AsSpan(offset + (component * 4)));
            }

            colors.Slice(vertexIndex * 4, 4).CopyTo(data.AsSpan(offset + 12));
        }

        return data;
    }
}

/// <summary>Schreibt Maschinenreports als einzeiliges UTF8-JSON.</summary>
public static class ReportWriter
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };

    public static void Write(string path, object report)
    {
        var json = JsonSerializer.Serialize(report, Options) + "\n";
        File.WriteAllText(path, json);
        Console.WriteLine($"report={path}");
    }
}
