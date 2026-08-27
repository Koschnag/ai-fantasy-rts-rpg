using Riftward.App.Bench;
using Riftward.Platform;
using Riftward.Platform.Interop;
using Riftward.Session;
using Riftward.Simulation;

namespace Riftward.App.Command;

/// <summary>Feste View-Ids der Kommandoschleife (T-023-Muster).</summary>
internal static class InteractiveViews
{
    public const byte ViewShadowBase = 0;
    public const byte ViewMain = 4;
    public const byte ViewCapture = 5;
    public const byte ViewBlit = 6;
}

/// <summary>
/// GPU-Ressourcen und Durchgaenge der Graybox-Kommandoschleife. Die Shader
/// sind dieselben offline mit dem gepinnten shaderc uebersetzten Binaries wie
/// T-023; es findet weiterhin keine Shaderkompilierung zur Laufzeit statt.
/// Besitzregeln gemäß docs/NATIVE_UNTERBAU.md: Freigabe Programme →
/// Framebuffer → Texturen → Buffer/Uniforms, gesamt vor dem bgfx-Shutdown.
/// </summary>
internal sealed class InteractiveSceneResources : IDisposable
{
    public ushort TerrainVertexBuffer;
    public ushort TerrainIndexBuffer;
    public ushort UnitVertexBuffer;
    public ushort UnitIndexBuffer;
    public ushort MarkerQuadBuffer;
    public int TerrainTriangleCount;
    public ushort PaletteTexture;
    public IReadOnlyList<ushort> ShadowTextures = [];
    public IReadOnlyList<ushort> ShadowFrameBuffers = [];
    public ushort ProgramTerrainLit;
    public ushort ProgramUnitLit;
    public ushort ProgramUnitDepth;
    public ushort ProgramTerrainDepth;
    public ushort ProgramMarker;
    public ushort UniformSunDirection;
    public ushort UniformSunColor;
    public ushort UniformLightPosRadius;
    public ushort UniformLightColorInner;
    public ushort UniformShadowParams;
    public ushort UniformBonePaletteSampler;
    public ushort UniformShadowMap0;
    public ushort UniformShadowMap1;
    public ushort UniformShadowMap2;
    public ushort UniformShadowMap3;
    public IReadOnlyList<ushort> UniformLightViewProj = [];
    public ushort UniformCameraRight;
    public ushort UniformCameraUp;

    private readonly BgfxDevice _device;
    private bool _disposed;

    private InteractiveSceneResources(BgfxDevice device) => _device = device;

    public static InteractiveSceneResources Build(BgfxDevice device, CommandLineArgs arguments)
    {
        var terrain = RepresentativeMesh.BuildTerrain();
        var units = RepresentativeMesh.BuildUnitMesh();
        var quad = RepresentativeMesh.BuildParticleQuad();

        var resources = new InteractiveSceneResources(device)
        {
            TerrainVertexBuffer = device.CreateLayoutVertexBuffer(terrain.Vertices, BgfxDevice.LayoutTerrain),
            TerrainIndexBuffer = device.CreateIndexBuffer(terrain.Indices, uint32Indices: false),
            UnitVertexBuffer = device.CreateLayoutVertexBuffer(units.Vertices, BgfxDevice.LayoutUnitMesh),
            UnitIndexBuffer = device.CreateIndexBuffer(units.Indices, uint32Indices: false),
            MarkerQuadBuffer = device.CreateLayoutVertexBuffer(quad.Vertices, BgfxDevice.LayoutParticleQuad),
            TerrainTriangleCount = terrain.TriangleCount,
        };

        try
        {
            var shadowTextures = new List<ushort>(RepresentativeLandscape.LocalLightCount);
            var shadowFrameBuffers = new List<ushort>(RepresentativeLandscape.LocalLightCount);

            for (var light = 0; light < RepresentativeLandscape.LocalLightCount; light++)
            {
                shadowTextures.Add(device.CreateTexture2D(
                    RepresentativeScenario.ShadowMapSizePixels,
                    RepresentativeScenario.ShadowMapSizePixels,
                    BgfxSceneApi.TextureFormatRgba32F,
                    BgfxSceneApi.TextureFlagRt | BgfxSceneApi.SamplerClampU | BgfxSceneApi.SamplerClampV,
                    initialData: default));
                shadowFrameBuffers.Add(device.CreateFrameBufferFromTexture(shadowTextures[light]));
            }

            var lightMatrixUniforms = new List<ushort>(RepresentativeLandscape.LocalLightCount);

            for (var light = 0; light < RepresentativeLandscape.LocalLightCount; light++)
            {
                lightMatrixUniforms.Add(device.CreateUniform($"u_lightViewProj{light}", UniformType.Mat4, 1));
            }

            resources.ShadowTextures = shadowTextures;
            resources.ShadowFrameBuffers = shadowFrameBuffers;
            resources.PaletteTexture = device.CreateTexture2D(
                RepresentativeScenario.BonesPerNormalUnit * 3,
                SimulationContract.AgentCount,
                BgfxSceneApi.TextureFormatRgba32F,
                flags: 0,
                initialData: default);
            resources.ProgramTerrainLit = CreateProgram(device, arguments, "terrain.vs.bin", "lit_terrain.fs.bin");
            resources.ProgramUnitLit = CreateProgram(device, arguments, "unit.vs.bin", "lit_unit.fs.bin");
            resources.ProgramUnitDepth = CreateProgram(device, arguments, "depth_instanced.vs.bin", "depth.fs.bin");
            resources.ProgramTerrainDepth = CreateProgram(device, arguments, "depth_static.vs.bin", "depth.fs.bin");
            resources.ProgramMarker = CreateProgram(device, arguments, "particle.vs.bin", "particle.fs.bin");
            resources.UniformSunDirection = device.CreateUniform("u_sunDirection", UniformType.Vec4, 1);
            resources.UniformSunColor = device.CreateUniform("u_sunColor", UniformType.Vec4, 1);
            resources.UniformLightPosRadius = device.CreateUniform("u_lightPosRadius", UniformType.Vec4, RepresentativeLandscape.LocalLightCount);
            resources.UniformLightColorInner = device.CreateUniform("u_lightColorInner", UniformType.Vec4, RepresentativeLandscape.LocalLightCount);
            resources.UniformShadowParams = device.CreateUniform("u_shadowParams", UniformType.Vec4, 1);
            resources.UniformBonePaletteSampler = device.CreateUniform("s_bonePalette", UniformType.Sampler, 1);
            resources.UniformShadowMap0 = device.CreateUniform("s_shadowMap0", UniformType.Sampler, 1);
            resources.UniformShadowMap1 = device.CreateUniform("s_shadowMap1", UniformType.Sampler, 1);
            resources.UniformShadowMap2 = device.CreateUniform("s_shadowMap2", UniformType.Sampler, 1);
            resources.UniformShadowMap3 = device.CreateUniform("s_shadowMap3", UniformType.Sampler, 1);
            resources.UniformLightViewProj = lightMatrixUniforms;
            resources.UniformCameraRight = device.CreateUniform("u_camRight", UniformType.Vec4, 1);
            resources.UniformCameraUp = device.CreateUniform("u_camUp", UniformType.Vec4, 1);
            return resources;
        }
        catch
        {
            resources.Dispose();
            throw;
        }
    }

    private static ushort CreateProgram(BgfxDevice device, CommandLineArgs arguments, string vertexName, string fragmentName)
    {
        var artifactsDir = arguments.Option("--artifacts-dir") ?? ".ai/runtime/cache/native/dist";
        var vertexShader = device.CreateShader(File.ReadAllBytes(Path.Combine(artifactsDir, "shaders", vertexName)));
        var fragmentShader = device.CreateShader(File.ReadAllBytes(Path.Combine(artifactsDir, "shaders", fragmentName)));

        try
        {
            return device.CreateProgramFromShaders(vertexShader, fragmentShader);
        }
        finally
        {
            device.DestroyShader(vertexShader);
            device.DestroyShader(fragmentShader);
        }
    }

    /// <summary>Konfiguriert Schatten-, Haupt-, Abgriffs- und Blitviews.</summary>
    public void ConfigureViews(BgfxDevice device)
    {
        for (var light = 0; light < ShadowFrameBuffers.Count; light++)
        {
            device.SetViewFrameBuffer((byte)(InteractiveViews.ViewShadowBase + light), ShadowFrameBuffers[light]);
            device.ConfigureRenderTargetView(
                (byte)(InteractiveViews.ViewShadowBase + light),
                0x000000FFu,
                RepresentativeScenario.ShadowMapSizePixels,
                RepresentativeScenario.ShadowMapSizePixels);
        }

        device.ConfigureRenderTargetView(InteractiveViews.ViewMain, HostBootstrap.ClearColorRgba, BenchRunner.DefaultWidth, BenchRunner.DefaultHeight);
        device.ConfigureRenderTargetView(InteractiveViews.ViewCapture, HostBootstrap.ClearColorRgba, BenchRunner.DefaultWidth, BenchRunner.DefaultHeight);
        device.ConfigureRenderTargetView(InteractiveViews.ViewBlit, 0x000000FFu, 1, 1);
    }

    /// <summary>Die vier aktiven Schattenpaesse (Terrain plus Einheiten).</summary>
    public void SubmitShadowPasses(BgfxDevice device, nint unitsPointer, uint unitInstanceCount)
    {
        var lights = RepBenchRunnerLightTransforms();

        for (var light = 0; light < lights.Length; light++)
        {
            var viewId = (byte)(InteractiveViews.ViewShadowBase + light);
            device.SetViewTransform(viewId, lights[light].View16, lights[light].Proj16);
            device.SetTexture(5, UniformBonePaletteSampler, PaletteTexture, samplerFlags: 0);

            device.DrawSubmit(
                viewId,
                ProgramTerrainDepth,
                TerrainVertexBuffer,
                TerrainIndexBuffer,
                (uint)(TerrainTriangleCount * 3),
                instanceData: 0,
                instanceCount: 0,
                instanceStride: 0,
                state: BgfxSceneApi.StateOpaque);

            device.DrawSubmit(
                viewId,
                ProgramUnitDepth,
                UnitVertexBuffer,
                UnitIndexBuffer,
                (uint)(RepresentativeMesh.TrianglesPerUnit * 3),
                unitsPointer,
                unitInstanceCount,
                RepresentativeMesh.UnitInstanceStrideBytes,
                state: BgfxSceneApi.StateOpaque);
        }
    }

    /// <summary>Zusammengesetzter Hauptdurchgang: Terrain, Einheiten, Marker.</summary>
    public void SubmitCompositePass(
        BgfxDevice device,
        byte viewId,
        float[] view16,
        float[] proj16,
        double[] cameraBasisRightUp,
        nint unitsPointer,
        uint unitInstanceCount,
        nint markersPointer,
        uint markerCount)
    {
        device.SetViewTransform(viewId, view16, proj16);

        Span<float> sunDirection =
            [(float)-RepBenchRunnerLighting.SunDirection[0], (float)-RepBenchRunnerLighting.SunDirection[1], (float)-RepBenchRunnerLighting.SunDirection[2], 1.0f];
        Span<float> sunColor =
            [(float)RepBenchRunnerLighting.SunColor[0], (float)RepBenchRunnerLighting.SunColor[1], (float)RepBenchRunnerLighting.SunColor[2], 1.0f];
        Span<float> shadowParams = [RepresentativeScenario.ShadowMapSizePixels, 0f, 0f, 0f];

        device.SetUniformVec4(UniformSunDirection, sunDirection);
        device.SetUniformVec4(UniformSunColor, sunColor);
        device.SetUniformVec4(UniformShadowParams, shadowParams);

        // Billboard-Basis der Marker (Kamera rechts/oben); dieselbe
        // Orthogonalisierung wie im T-023-Partikelpfad.
        device.SetUniformVec4(UniformCameraRight,
            [(float)cameraBasisRightUp[0], 0f, (float)cameraBasisRightUp[2], 0f]);
        device.SetUniformVec4(UniformCameraUp,
            [(float)cameraBasisRightUp[3], (float)cameraBasisRightUp[4], (float)cameraBasisRightUp[5], 0f]);

        var placements = RepresentativeLandscape.CachedPlacements();
        Span<float> lightPositions = stackalloc float[placements.Length * 4];

        for (var light = 0; light < placements.Length; light++)
        {
            lightPositions[(light * 4) + 0] = (float)placements[light].X;
            lightPositions[(light * 4) + 1] = (float)placements[light].Y;
            lightPositions[(light * 4) + 2] = (float)placements[light].Z;
            lightPositions[(light * 4) + 3] = (float)placements[light].Radius;
        }

        device.SetUniformVec4(UniformLightPosRadius, lightPositions);

        Span<float> lightColorValues = stackalloc float[RepresentativeLandscape.LocalLightCount * 4];

        for (var light = 0; light < RepresentativeLandscape.LocalLightCount; light++)
        {
            lightColorValues[(light * 4) + 0] = RepBenchRunnerLighting.LightColors[light][0];
            lightColorValues[(light * 4) + 1] = RepBenchRunnerLighting.LightColors[light][1];
            lightColorValues[(light * 4) + 2] = RepBenchRunnerLighting.LightColors[light][2];
            lightColorValues[(light * 4) + 3] = 1f;
        }

        device.SetUniformVec4(UniformLightColorInner, lightColorValues);

        var lights = RepBenchRunnerLightTransforms();

        for (var light = 0; light < lights.Length; light++)
        {
            device.SetUniformMat4(UniformLightViewProj[light], lights[light].MatrixColumnMajorFloat16);
        }

        device.SetTexture(5, UniformBonePaletteSampler, PaletteTexture, samplerFlags: 0);
        device.SetTexture(6, UniformShadowMap0, ShadowTextures[0], samplerFlags: 0);
        device.SetTexture(7, UniformShadowMap1, ShadowTextures[1], samplerFlags: 0);
        device.SetTexture(8, UniformShadowMap2, ShadowTextures[2], samplerFlags: 0);
        device.SetTexture(9, UniformShadowMap3, ShadowTextures[3], samplerFlags: 0);

        device.DrawSubmit(
            viewId,
            ProgramTerrainLit,
            TerrainVertexBuffer,
            TerrainIndexBuffer,
            (uint)(TerrainTriangleCount * 3),
            instanceData: 0,
            instanceCount: 0,
            instanceStride: 0,
            state: BgfxSceneApi.StateOpaque);

        device.DrawSubmit(
            viewId,
            ProgramUnitLit,
            UnitVertexBuffer,
            UnitIndexBuffer,
            (uint)(RepresentativeMesh.TrianglesPerUnit * 3),
            unitsPointer,
            unitInstanceCount,
            RepresentativeMesh.UnitInstanceStrideBytes,
            state: BgfxSceneApi.StateOpaque);

        device.DrawSubmit(
            viewId,
            ProgramMarker,
            MarkerQuadBuffer,
            BgfxDevice.NoIndexBuffer,
            4,
            markersPointer,
            markerCount,
            RepresentativeMesh.ParticleInstanceStrideBytes,
            state: BgfxSceneApi.StateBlendAlpha);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _device.DestroyProgram(ProgramTerrainLit);
        _device.DestroyProgram(ProgramUnitLit);
        _device.DestroyProgram(ProgramUnitDepth);
        _device.DestroyProgram(ProgramTerrainDepth);
        _device.DestroyProgram(ProgramMarker);

        foreach (var frameBuffer in ShadowFrameBuffers)
        {
            _device.DestroyFrameBuffer(frameBuffer);
        }

        foreach (var texture in ShadowTextures)
        {
            _device.DestroyTexture(texture);
        }

        _device.DestroyTexture(PaletteTexture);
        _device.DestroyIndexBuffer(TerrainIndexBuffer);
        _device.DestroyIndexBuffer(UnitIndexBuffer);
        _device.DestroyVertexBuffer(TerrainVertexBuffer);
        _device.DestroyVertexBuffer(UnitVertexBuffer);
        _device.DestroyVertexBuffer(MarkerQuadBuffer);

        _device.DestroyUniform(UniformSunDirection);
        _device.DestroyUniform(UniformSunColor);
        _device.DestroyUniform(UniformLightPosRadius);
        _device.DestroyUniform(UniformLightColorInner);
        _device.DestroyUniform(UniformShadowParams);
        _device.DestroyUniform(UniformBonePaletteSampler);
        _device.DestroyUniform(UniformShadowMap0);
        _device.DestroyUniform(UniformShadowMap1);
        _device.DestroyUniform(UniformShadowMap2);
        _device.DestroyUniform(UniformShadowMap3);

        foreach (var matrix in UniformLightViewProj)
        {
            _device.DestroyUniform(matrix);
        }

        _device.DestroyUniform(UniformCameraRight);
        _device.DestroyUniform(UniformCameraUp);

        _disposed = true;
    }

    /* Feste Lichttransformen und -farben der T-023-Greyboxszene; lokal
     * gespiegelt, damit dieser Slice die Belastungsframe-Dateien nicht
     * anfassen muss. */

    private static RepBenchRunnerLightTransform[]? _cachedLights;

    internal static RepBenchRunnerLightTransform[] RepBenchRunnerLightTransforms() =>
        _cachedLights ??= BuildLightTransforms();

    private static RepBenchRunnerLightTransform[] BuildLightTransforms()
    {
        var placements = RepresentativeLandscape.LightPlacements();
        var transforms = new RepBenchRunnerLightTransform[placements.Length];

        for (var light = 0; light < placements.Length; light++)
        {
            var placement = placements[light];
            var view = CameraMath.ToFloat16(CameraMath.LookAt(
                new CameraMath.Vec3(placement.X, placement.Y, placement.Z),
                new CameraMath.Vec3(placement.X, 0.0, placement.Z),
                new CameraMath.Vec3(0, 1, 0)));
            var proj = CameraMath.ToFloat16(CameraMath.PerspectiveFov(80.0, 1.0, 1.0, 80.0));
            var viewDouble = CameraMath.LookAt(
                new CameraMath.Vec3(placement.X, placement.Y, placement.Z),
                new CameraMath.Vec3(placement.X, 0.0, placement.Z),
                new CameraMath.Vec3(0, 1, 0));
            var projDouble = CameraMath.PerspectiveFov(80.0, 1.0, 1.0, 80.0);
            transforms[light] = new RepBenchRunnerLightTransform(
                view,
                proj,
                CameraMath.ToFloat16(InteractiveCameraMath.Multiply(viewDouble, projDouble)));
        }

        return transforms;
    }
}

internal sealed record RepBenchRunnerLightTransform(float[] View16, float[] Proj16, float[] MatrixColumnMajorFloat16);

internal static class RepBenchRunnerLighting
{
    public const string SunDirectionSource = "repbench-shared";

    public static readonly double[] SunDirection = [-0.35, -0.80, 0.45];

    public static readonly double[] SunColor = [1.00, 0.96, 0.88];

    public static readonly float[][] LightColors =
    [
        [0.95f, 0.72f, 0.48f],
        [0.55f, 0.70f, 0.92f],
        [0.85f, 0.60f, 0.75f],
        [0.62f, 0.86f, 0.62f],
    ];
}
