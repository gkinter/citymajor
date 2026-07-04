using System.Diagnostics;
using Forge.Engine.Core;
using Forge.Engine.Data;
using Forge.Engine.UI;
using Silk.NET.OpenGL;

namespace Forge.Engine.Rendering;

/// <summary>
/// OpenGL renderer manager. Owns the GL context state, manages viewport,
/// and coordinates sub-renderers through an 8-pass rendering pipeline:
///   Pass 1: Terrain tiles (chunked, frustum culled)
///   Pass 2: Buildings (sorted by Y for depth, GPU instanced)
///   Pass 3: Vehicles (GPU instanced, pooled)
///   Pass 4: Citizens (GPU instanced, pooled)
///   Pass 5: Weather particles
///   Pass 6: Overlays (semi-transparent heatmaps)
///   Pass 7: Post-processing (bloom, vignette, palette swap for day/night)
///   Pass 8: ImGui UI (on top of everything)
/// Each pass is profiled independently for frame timing diagnostics.
/// </summary>
public sealed class Renderer : IDisposable
{
    private readonly Config _config;
    private GL? _gl;
    private int _viewportWidth;
    private int _viewportHeight;

    // Sub-renderers (set externally after Init so consumers wire them up)
    private ChunkRenderer? _chunkRenderer;
    private SpriteRenderer? _buildingSprites;
    private SpriteRenderer? _vehicleSprites;
    private SpriteRenderer? _citizenSprites;
    private OverlayRenderer? _overlayRenderer;
    private PostProcess? _postProcess;
    private ShaderProgram? _postShader;

    // Per-pass timing (microseconds, rolling average)
    private const int PassCount = 8;
    private readonly double[] _passTimesUs = new double[PassCount];
    private readonly double[] _passTimesAccum = new double[PassCount];
    private readonly int[] _passSampleCount = new int[PassCount];
    private readonly string[] _passNames =
    [
        "Terrain", "Buildings", "Vehicles", "Citizens",
        "Weather", "Overlays", "PostProcess", "ImGui"
    ];
    private const int ProfileSampleWindow = 60;
    private readonly Stopwatch _passStopwatch = new();

    // Frame counter for chunk manager LRU
    private int _frameNumber;

    // Day/night cycle factor (0 = midnight, 0.5 = noon, 1 = midnight)
    private float _dayNightFactor = 0.5f;

    // Global lighting system for time-of-day driven lighting
    private LightingSystem? _lightingSystem;

    public GL? GL => _gl;
    public int ViewportWidth => _viewportWidth;
    public int ViewportHeight => _viewportHeight;
    public int FrameNumber => _frameNumber;

    /// <summary>Per-pass timing in microseconds (averaged over last 60 frames).</summary>
    public ReadOnlySpan<double> PassTimings => _passTimesUs;

    /// <summary>Pass names for debug display.</summary>
    public ReadOnlySpan<string> PassNames => _passNames;

    public float DayNightFactor
    {
        get => _dayNightFactor;
        set => _dayNightFactor = System.Math.Clamp(value, 0f, 1f);
    }

    public LightingSystem? LightingSystem
    {
        get => _lightingSystem;
        set => _lightingSystem = value;
    }

    public Renderer(Config config)
    {
        _config = config;
        _viewportWidth = config.WindowWidth;
        _viewportHeight = config.WindowHeight;
    }

    public void Init()
    {
        _gl = Silk.NET.OpenGL.GL.GetApi(SDL2.SDL.SDL_GL_GetProcAddress);

        _gl.Enable(EnableCap.Blend);
        _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        _gl.Disable(EnableCap.DepthTest);
        _gl.Disable(EnableCap.CullFace);

        _gl.Viewport(0, 0, (uint)_viewportWidth, (uint)_viewportHeight);
        _gl.ClearColor(0.05f, 0.05f, 0.08f, 1.0f);

        Console.WriteLine($"[Renderer] OpenGL Version: {_gl.GetStringS(StringName.Version)}");
        Console.WriteLine($"[Renderer] Renderer: {_gl.GetStringS(StringName.Renderer)}");
    }

    /// <summary>
    /// Register all sub-renderers. Call after Init() once sub-renderers are constructed.
    /// </summary>
    public void RegisterSubRenderers(
        ChunkRenderer? chunkRenderer,
        SpriteRenderer? buildingSprites,
        SpriteRenderer? vehicleSprites,
        SpriteRenderer? citizenSprites,
        OverlayRenderer? overlayRenderer,
        PostProcess? postProcess,
        ShaderProgram? postShader)
    {
        _chunkRenderer = chunkRenderer;
        _buildingSprites = buildingSprites;
        _vehicleSprites = vehicleSprites;
        _citizenSprites = citizenSprites;
        _overlayRenderer = overlayRenderer;
        _postProcess = postProcess;
        _postShader = postShader;
    }

    public void Resize(int width, int height)
    {
        _viewportWidth = width;
        _viewportHeight = height;
        _gl?.Viewport(0, 0, (uint)width, (uint)height);
        _postProcess?.Resize(width, height);
    }

    public void BeginFrame()
    {
        _frameNumber++;
        _gl?.Clear(ClearBufferMask.ColorBufferBit);
    }

    /// <summary>
    /// Execute the full 8-pass rendering pipeline. Call between BeginFrame and EndFrame.
    /// </summary>
    /// <param name="camera">The isometric camera for view transforms.</param>
    /// <param name="terrainAtlas">Atlas texture for terrain tiles.</param>
    /// <param name="buildingAtlas">Atlas texture for buildings.</param>
    /// <param name="vehicleAtlas">Atlas texture for vehicles.</param>
    /// <param name="citizenAtlas">Atlas texture for citizens.</param>
    /// <param name="buildingInstances">Callback to populate building sprite batch (sorted by Y).</param>
    /// <param name="vehicleInstances">Callback to populate vehicle sprite batch.</param>
    /// <param name="citizenInstances">Callback to populate citizen sprite batch.</param>
    /// <param name="weatherCallback">Callback for weather particle rendering (pass 5).</param>
    /// <param name="overlayData">Float array for active overlay heatmap, or null.</param>
    /// <param name="imguiRender">Callback that executes ImGui frame (BeginFrame + widgets + EndFrame).</param>
    public void RenderPipeline(
        IsometricCamera camera,
        TextureAtlas terrainAtlas,
        TextureAtlas? buildingAtlas = null,
        TextureAtlas? vehicleAtlas = null,
        TextureAtlas? citizenAtlas = null,
        Action<SpriteRenderer, IsometricCamera>? buildingInstances = null,
        Action<SpriteRenderer, IsometricCamera>? vehicleInstances = null,
        Action<SpriteRenderer, IsometricCamera>? citizenInstances = null,
        Action? weatherCallback = null,
        float[]? overlayData = null,
        Action? imguiRender = null)
    {
        if (_gl == null) return;

        // Begin post-process capture if enabled
        bool usePostProcess = _postProcess != null && _postShader != null;
        if (usePostProcess)
        {
            _postProcess!.BeginCapture();
        }

        // --- Pass 1: Terrain tiles (chunked, frustum culled) ---
        BeginPass(0);
        if (_chunkRenderer != null)
        {
            terrainAtlas.Bind(0);
            _chunkRenderer.Render(camera, terrainAtlas);
        }
        EndPass(0);

        // --- Pass 2: Buildings (sorted by Y, GPU instanced) ---
        BeginPass(1);
        if (_buildingSprites != null && buildingInstances != null)
        {
            buildingAtlas?.Bind(0);
            _buildingSprites.Begin();
            buildingInstances(_buildingSprites, camera);
            _buildingSprites.End();
        }
        EndPass(1);

        // --- Pass 3: Vehicles (GPU instanced, pooled) ---
        BeginPass(2);
        if (_vehicleSprites != null && vehicleInstances != null)
        {
            vehicleAtlas?.Bind(0);
            _vehicleSprites.Begin();
            vehicleInstances(_vehicleSprites, camera);
            _vehicleSprites.End();
        }
        EndPass(2);

        // --- Pass 4: Citizens (GPU instanced, pooled) ---
        BeginPass(3);
        if (_citizenSprites != null && citizenInstances != null)
        {
            citizenAtlas?.Bind(0);
            _citizenSprites.Begin();
            citizenInstances(_citizenSprites, camera);
            _citizenSprites.End();
        }
        EndPass(3);

        // --- Pass 5: Weather particles ---
        BeginPass(4);
        weatherCallback?.Invoke();
        EndPass(4);

        // --- Pass 6: Overlays (semi-transparent heatmaps) ---
        BeginPass(5);
        if (_overlayRenderer != null && _overlayRenderer.ActiveOverlay != OverlayRenderer.OverlayType.None && overlayData != null)
        {
            _gl.Enable(EnableCap.Blend);
            _gl.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            _overlayRenderer.Upload(overlayData);
            _overlayRenderer.Bind(1);
            // Overlay rendering is driven by the overlay renderer's own draw call
            // which maps the texture over the visible tile area
        }
        EndPass(5);

        // --- Pass 7: Post-processing (bloom, vignette, tilt-shift, fog) ---
        BeginPass(6);
        if (usePostProcess)
        {
            // Apply day/night tint to post-process uniforms
            if (_postShader != null)
            {
                float nightStrength;
                if (_lightingSystem != null)
                {
                    nightStrength = _lightingSystem.NightStrength;

                    // Sync fog color with ambient lighting
                    var ambient = _lightingSystem.AmbientColor;
                    _postProcess!.FogColor = (ambient.R, ambient.G, ambient.B);

                    // Tint bloom based on time of day: warm at night (window glow), neutral by day
                    float warmth = nightStrength * 0.3f;
                    _postProcess.BloomTint = (1.0f, 0.95f - warmth * 0.15f, 0.85f - warmth * 0.25f);
                }
                else
                {
                    nightStrength = 1f - MathF.Sin(_dayNightFactor * MathF.PI);
                }

                _postShader.SetUniform("u_nightStrength", nightStrength);
            }
            _postProcess!.Apply(_postShader!);
        }
        EndPass(6);

        // --- Pass 8: ImGui UI (on top of everything) ---
        BeginPass(7);
        imguiRender?.Invoke();
        EndPass(7);

        // Update rolling averages
        UpdateProfilingAverages();
    }

    private void BeginPass(int passIndex)
    {
        _passStopwatch.Restart();
    }

    private void EndPass(int passIndex)
    {
        _passStopwatch.Stop();
        double us = _passStopwatch.Elapsed.TotalMicroseconds;
        _passTimesAccum[passIndex] += us;
        _passSampleCount[passIndex]++;
    }

    private void UpdateProfilingAverages()
    {
        for (int i = 0; i < PassCount; i++)
        {
            if (_passSampleCount[i] >= ProfileSampleWindow)
            {
                _passTimesUs[i] = _passTimesAccum[i] / _passSampleCount[i];
                _passTimesAccum[i] = 0;
                _passSampleCount[i] = 0;
            }
        }
    }

    /// <summary>
    /// Get the total frame time across all render passes in microseconds.
    /// </summary>
    public double TotalPassTimeUs()
    {
        double total = 0;
        for (int i = 0; i < PassCount; i++)
            total += _passTimesUs[i];
        return total;
    }

    /// <summary>
    /// Format profiling data as a multi-line string for debug display.
    /// </summary>
    public string GetProfilingReport()
    {
        var sb = new System.Text.StringBuilder(256);
        sb.AppendLine("Render Pipeline Profiling (us avg):");
        for (int i = 0; i < PassCount; i++)
        {
            sb.AppendLine($"  Pass {i + 1} [{_passNames[i]}]: {_passTimesUs[i]:F1} us");
        }
        sb.AppendLine($"  Total: {TotalPassTimeUs():F1} us");
        return sb.ToString();
    }

    public void Dispose()
    {
        _gl?.Dispose();
    }
}
