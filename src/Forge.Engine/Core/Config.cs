namespace Forge.Engine.Core;

/// <summary>
/// Engine-wide configuration. Loaded at startup, immutable during runtime.
/// </summary>
public sealed class Config
{
    public string WindowTitle { get; init; } = "Forge Engine";
    public int WindowWidth { get; init; } = 1280;
    public int WindowHeight { get; init; } = 720;
    public bool Fullscreen { get; init; } = false;
    public bool VSync { get; init; } = true;
    public int TargetFps { get; init; } = 60;

    /// <summary>Fixed timestep for simulation in seconds (default: 1/60).</summary>
    public double FixedTimestep { get; init; } = 1.0 / 60.0;

    /// <summary>Maximum number of fixed updates per frame to prevent spiral of death.</summary>
    public int MaxUpdatesPerFrame { get; init; } = 5;

    /// <summary>World size in tiles (square). Must be power of 2, max 1024.</summary>
    public int WorldSize { get; init; } = 512;

    /// <summary>Chunk size in tiles (square). Must evenly divide WorldSize.</summary>
    public int ChunkSize { get; init; } = 64;

    /// <summary>Isometric tile dimensions in pixels.</summary>
    public int TileWidth { get; init; } = 128;
    public int TileHeight { get; init; } = 64;

    /// <summary>Hero tile dimensions for landmark buildings (2x normal).</summary>
    public int HeroTileWidth { get; init; } = 256;
    public int HeroTileHeight { get; init; } = 128;

    // ─── Visual quality ─────────────────────────────────────────────────

    /// <summary>Enable normal-mapped directional lighting on sprites.</summary>
    public bool EnableNormalMapping { get; init; } = true;

    /// <summary>Enable screen-space ambient occlusion (ground contact shadows).</summary>
    public bool EnableSSAO { get; init; } = true;

    /// <summary>Enable tilt-shift depth-of-field post-processing.</summary>
    public bool EnableTiltShift { get; init; } = true;

    /// <summary>Enable bloom post-processing.</summary>
    public bool EnableBloom { get; init; } = true;

    /// <summary>Shadow edge softness (0 = hard, 1 = very soft).</summary>
    public float ShadowSoftness { get; init; } = 0.5f;

    /// <summary>Ambient light color (R, G, B) — modulated by time of day.</summary>
    public (float R, float G, float B) AmbientLightColor { get; init; } = (0.85f, 0.85f, 0.9f);

    /// <summary>Sun direction as normalized vector (X, Y, Z) — overridden by LightingSystem at runtime.</summary>
    public (float X, float Y, float Z) SunDirection { get; init; } = (0.5f, -0.7f, 0.5f);

    /// <summary>Base path for game data (assets, mods, saves).</summary>
    public string BasePath { get; init; } = "base";

    /// <summary>Enable ImGui debug overlay.</summary>
    public bool DebugOverlay { get; init; } = true;

    /// <summary>Audio master volume (0.0 - 1.0).</summary>
    public float MasterVolume { get; init; } = 0.8f;

    /// <summary>Network listen port for server mode.</summary>
    public int NetworkPort { get; init; } = 7777;

    // ─── Simulation timing ────────────────────────────────────────────────

    /// <summary>Interval in seconds between traffic simulation updates.</summary>
    public float SimTrafficInterval { get; init; } = 0.5f;

    /// <summary>Interval in seconds between day advances.</summary>
    public float SimDayInterval { get; init; } = 10.0f;

    /// <summary>Fixed simulation step rate in Hz.</summary>
    public int SimFixedStepHz { get; init; } = 30;

    // ─── Rendering limits ─────────────────────────────────────────────────

    /// <summary>Maximum number of chunk GPU buffers cached simultaneously.</summary>
    public int MaxCachedChunks { get; init; } = 64;

    /// <summary>Maximum number of sprites that can be batched in a single draw call.</summary>
    public int MaxSprites { get; init; } = 65536;

    // ─── Camera / input ───────────────────────────────────────────────────

    /// <summary>Edge scroll detection zone in pixels from viewport edge.</summary>
    public int EdgeScrollZone { get; init; } = 20;

    /// <summary>Base camera pan speed in pixels per second.</summary>
    public float BasePanSpeed { get; init; } = 400f;

    /// <summary>Duration in milliseconds for pan acceleration to reach full speed.</summary>
    public float AccelDuration { get; init; } = 200f;

    // ─── Entity pool limits ───────────────────────────────────────────────

    /// <summary>Maximum number of entities in the object pool.</summary>
    public int MaxEntityPoolSize { get; init; } = 100000;

    public int ChunksPerAxis => WorldSize / ChunkSize;
    public int TotalChunks => ChunksPerAxis * ChunksPerAxis;
    public int TotalTiles => WorldSize * WorldSize;

    /// <summary>
    /// Validate configuration values. Throws ArgumentException if any constraint is violated.
    /// Called during engine initialization.
    /// </summary>
    public void Validate()
    {
        if (WorldSize <= 0 || WorldSize > 4096)
            throw new ArgumentException($"WorldSize must be between 1 and 4096, got {WorldSize}.");

        if ((WorldSize & (WorldSize - 1)) != 0)
            throw new ArgumentException($"WorldSize must be a power of 2, got {WorldSize}.");

        if (ChunkSize <= 0)
            throw new ArgumentException($"ChunkSize must be positive, got {ChunkSize}.");

        if (WorldSize % ChunkSize != 0)
            throw new ArgumentException($"ChunkSize ({ChunkSize}) must evenly divide WorldSize ({WorldSize}).");

        if (TileWidth <= 0 || TileHeight <= 0)
            throw new ArgumentException($"Tile dimensions must be positive, got {TileWidth}x{TileHeight}.");

        if (WindowWidth <= 0 || WindowHeight <= 0)
            throw new ArgumentException($"Window dimensions must be positive, got {WindowWidth}x{WindowHeight}.");

        if (TargetFps <= 0)
            throw new ArgumentException($"TargetFps must be positive, got {TargetFps}.");

        if (FixedTimestep <= 0)
            throw new ArgumentException($"FixedTimestep must be positive, got {FixedTimestep}.");

        if (MaxUpdatesPerFrame <= 0)
            throw new ArgumentException($"MaxUpdatesPerFrame must be positive, got {MaxUpdatesPerFrame}.");

        if (MasterVolume < 0f || MasterVolume > 1f)
            throw new ArgumentException($"MasterVolume must be between 0.0 and 1.0, got {MasterVolume}.");

        if (SimTrafficInterval <= 0f)
            throw new ArgumentException($"SimTrafficInterval must be positive, got {SimTrafficInterval}.");

        if (SimDayInterval <= 0f)
            throw new ArgumentException($"SimDayInterval must be positive, got {SimDayInterval}.");

        if (SimFixedStepHz <= 0)
            throw new ArgumentException($"SimFixedStepHz must be positive, got {SimFixedStepHz}.");

        if (MaxCachedChunks <= 0)
            throw new ArgumentException($"MaxCachedChunks must be positive, got {MaxCachedChunks}.");

        if (MaxSprites <= 0)
            throw new ArgumentException($"MaxSprites must be positive, got {MaxSprites}.");

        if (EdgeScrollZone < 0)
            throw new ArgumentException($"EdgeScrollZone must be non-negative, got {EdgeScrollZone}.");

        if (BasePanSpeed <= 0f)
            throw new ArgumentException($"BasePanSpeed must be positive, got {BasePanSpeed}.");

        if (AccelDuration < 0f)
            throw new ArgumentException($"AccelDuration must be non-negative, got {AccelDuration}.");

        if (MaxEntityPoolSize <= 0)
            throw new ArgumentException($"MaxEntityPoolSize must be positive, got {MaxEntityPoolSize}.");

        if (HeroTileWidth <= 0 || HeroTileHeight <= 0)
            throw new ArgumentException($"Hero tile dimensions must be positive, got {HeroTileWidth}x{HeroTileHeight}.");

        if (ShadowSoftness < 0f || ShadowSoftness > 1f)
            throw new ArgumentException($"ShadowSoftness must be between 0.0 and 1.0, got {ShadowSoftness}.");
    }
}
