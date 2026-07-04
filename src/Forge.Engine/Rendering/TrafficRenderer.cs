using System.Runtime.CompilerServices;
using Forge.Engine.Core;
using Forge.Engine.Math;
using Forge.Engine.Simulation;

namespace Forge.Engine.Rendering;

/// <summary>
/// Visual vehicle system that makes traffic the HEARTBEAT of a living city.
/// Vehicles are purely cosmetic -- they interpolate smoothly between sim ticks at 60fps,
/// match congestion speed from the simulation, and show headlights/brake lights at night.
///
/// Key design: separate visual pool from simulation. Simulation has logical vehicles on
/// a road graph; this system has ~10,000 visual dots moving smoothly on screen.
/// The pool is synchronized from SimSnapshot each sim tick, but interpolated every frame.
///
/// Performance budget: single GPU-instanced draw call for all vehicles via SpriteRenderer.
/// At zoom 5+, vehicles are hidden entirely (too small to see).
/// </summary>
public sealed class TrafficRenderer : IDisposable
{
    /// <summary>
    /// Visual-only vehicle state. Separate from simulation -- these are COSMETIC sprites.
    /// Packed for cache-friendly iteration during Update().
    /// </summary>
    private struct VisualVehicle
    {
        public float WorldX, WorldY;           // current interpolated world position
        public float PrevWorldX, PrevWorldY;   // position at last sim tick (for lerp)
        public float TargetX, TargetY;         // position at current sim tick
        public float Speed;                    // pixels per second (matches road congestion)
        public float Progress;                 // 0-1 interpolation between prev and target
        public byte Direction;                 // 0-7 (N, NE, E, SE, S, SW, W, NW)
        public byte VehicleType;               // 0=car, 1=truck, 2=bus, 3=emergency, 4=bicycle
        public byte WealthClass;               // 0-3 affects sprite variant
        public byte Era;                       // affects sprite variant
        public float TintR, TintG, TintB;      // subtle color variation per vehicle
        public bool HeadlightsOn;              // night time
        public bool BrakeLightsOn;             // slowing down
        public float AnimationTimer;           // wheel rotation cycle
        public float Alpha;                    // for fade-in/fade-out at spawn/despawn
        public bool Active;                    // slot in use
    }

    private readonly Config _config;
    private readonly VisualVehicle[] _pool;
    private int _activeCount;
    private float _rushHourMultiplier = 1f;
    private readonly Random _rng = new(42);

    // Vehicle sprite dimensions at zoom 1 (isometric scale)
    private const float VehicleWidth = 12f;
    private const float VehicleHeight = 8f;

    // Pool limits
    public const int MaxVehicles = 10_000;

    // Fade duration in seconds for spawn/despawn
    private const float FadeDuration = 0.4f;

    // Night threshold (game hours)
    private const float DuskStart = 18f;
    private const float DuskEnd = 20f;
    private const float DawnStart = 5f;
    private const float DawnEnd = 7f;

    // Rush hour ranges
    private const float MorningRushStart = 7f;
    private const float MorningRushEnd = 9f;
    private const float EveningRushStart = 17f;
    private const float EveningRushEnd = 19f;
    private const float RushHourMaxMultiplier = 3f;

    // Brake light detection: speed drop threshold
    private const float BrakeThresholdRatio = 0.5f;

    // Cached time state
    private float _timeOfDay;
    private bool _isNightTime;

    /// <summary>Number of currently active visual vehicles.</summary>
    public int ActiveCount => _activeCount;

    /// <summary>Current rush hour multiplier (1.0 = normal, 3.0 = peak).</summary>
    public float RushHourMultiplier => _rushHourMultiplier;

    /// <summary>Whether headlights should be on.</summary>
    public bool IsNightTime => _isNightTime;

    public TrafficRenderer(Config config)
    {
        _config = config;
        _pool = new VisualVehicle[MaxVehicles];
    }

    /// <summary>
    /// Synchronize visual vehicles from the simulation snapshot.
    /// Called once per sim tick (not every frame). Maps sim vehicles to visual pool,
    /// spawning/despawning with fades, and computing rush hour density.
    /// </summary>
    public void UpdateFromSimulation(SimSnapshot snapshot, IsometricCamera camera)
    {
        _timeOfDay = snapshot.TimeOfDay;
        _isNightTime = _timeOfDay >= DuskEnd || _timeOfDay < DawnStart;

        // Compute rush hour multiplier (smooth ramp up/down)
        _rushHourMultiplier = ComputeRushHourMultiplier(_timeOfDay);

        // Map simulation vehicles to visual pool
        int simCount = snapshot.VehicleCount;
        int targetVisualCount = System.Math.Min(
            (int)(simCount * _rushHourMultiplier),
            MaxVehicles);

        // Update existing vehicles from snapshot
        int mapped = 0;
        for (int i = 0; i < simCount && mapped < targetVisualCount; i++)
        {
            ref var sim = ref snapshot.Vehicles[i];
            if ((sim.Flags & 1) == 0) continue; // inactive in sim

            if (mapped < _pool.Length)
            {
                ref var vis = ref _pool[mapped];

                // Store previous position for interpolation
                vis.PrevWorldX = vis.Active ? vis.TargetX : sim.WorldX;
                vis.PrevWorldY = vis.Active ? vis.TargetY : sim.WorldY;
                vis.TargetX = sim.WorldX;
                vis.TargetY = sim.WorldY;
                vis.Progress = 0f;

                vis.Speed = sim.Speed;

                // Compute direction from heading (0-7 octants)
                vis.Direction = HeadingToDirection(sim.Heading);

                // Vehicle type from TypeId
                vis.VehicleType = (byte)(sim.TypeId % 5);
                vis.Era = (byte)snapshot.Era;

                // Detect braking: speed significantly below max
                float speedRatio = sim.MaxSpeed > 0f ? sim.Speed / sim.MaxSpeed : 1f;
                vis.BrakeLightsOn = speedRatio < BrakeThresholdRatio && vis.Active;

                // Headlights
                vis.HeadlightsOn = _isNightTime || _timeOfDay >= DuskStart || _timeOfDay < DawnEnd;

                // Assign tint color on first spawn
                if (!vis.Active)
                {
                    AssignVehicleTint(ref vis);
                    vis.WorldX = sim.WorldX;
                    vis.WorldY = sim.WorldY;
                    vis.Alpha = 0f; // start fading in
                    vis.AnimationTimer = (float)_rng.NextDouble() * MathF.PI * 2f;
                }

                vis.Active = true;
                mapped++;
            }
        }

        // Deactivate excess vehicles (fade out)
        for (int i = mapped; i < _activeCount; i++)
        {
            if (_pool[i].Active)
            {
                _pool[i].Alpha = MathF.Max(_pool[i].Alpha - 0.1f, 0f);
                if (_pool[i].Alpha <= 0f)
                    _pool[i].Active = false;
            }
        }

        _activeCount = mapped;
    }

    /// <summary>
    /// Smooth interpolation every frame (NOT every sim tick).
    /// This is what makes vehicles move at sub-pixel precision at 60fps.
    /// </summary>
    public void Update(float dt)
    {
        for (int i = 0; i < _activeCount; i++)
        {
            ref var v = ref _pool[i];
            if (!v.Active) continue;

            // Advance interpolation progress
            // Speed of interpolation based on vehicle speed (faster vehicles = faster lerp)
            float lerpSpeed = v.Speed > 0.01f ? v.Speed * 2f : 4f;
            v.Progress = MathF.Min(v.Progress + lerpSpeed * dt, 1f);

            // Smooth cubic interpolation between previous and target positions
            float t = SmoothStep(v.Progress);
            v.WorldX = Lerp(v.PrevWorldX, v.TargetX, t);
            v.WorldY = Lerp(v.PrevWorldY, v.TargetY, t);

            // Fade in/out
            if (v.Alpha < 1f)
            {
                v.Alpha = MathF.Min(v.Alpha + dt / FadeDuration, 1f);
            }

            // Animation timer (wheel rotation)
            v.AnimationTimer += dt * v.Speed * 0.5f;
            if (v.AnimationTimer > MathF.PI * 2f)
                v.AnimationTimer -= MathF.PI * 2f;
        }
    }

    /// <summary>
    /// Render all visible vehicles via the SpriteRenderer's GPU instancing.
    /// Vehicles are drawn as small colored rectangles with direction-dependent sprites.
    /// At zoom 5+, vehicles are culled (too small to see, saves draw calls).
    /// </summary>
    public void Render(IsometricCamera camera, SpriteRenderer sprites, float timeOfDay)
    {
        // Cull at high zoom levels (vehicles too small to see)
        if (camera.ZoomLevel >= 5) return;

        var (offsetX, offsetY) = camera.GetRenderOffset();
        float zoom = camera.SmoothZoom;
        var frustum = camera.GetFrustumBounds();
        int tw = _config.TileWidth;
        int th = _config.TileHeight;

        float vw = VehicleWidth * zoom;
        float vh = VehicleHeight * zoom;

        for (int i = 0; i < _activeCount; i++)
        {
            ref var v = ref _pool[i];
            if (!v.Active || v.Alpha <= 0f) continue;

            // Convert world position to screen via isometric projection
            float halfW = tw * 0.5f;
            float halfH = th * 0.5f;
            float isoX = (v.WorldX - v.WorldY) * halfW;
            float isoY = (v.WorldX + v.WorldY) * halfH;

            // Frustum cull in world space
            if (!frustum.Contains(isoX, isoY)) continue;

            // Apply camera transform
            float screenX = isoX * zoom + offsetX - vw * 0.5f;
            float screenY = isoY * zoom + offsetY - vh * 0.5f;

            // Base tint color
            float r = v.TintR;
            float g = v.TintG;
            float b = v.TintB;
            float a = v.Alpha;

            // Brake lights: add red tint when braking
            if (v.BrakeLightsOn)
            {
                r = MathF.Min(r + 0.3f, 1f);
                g *= 0.5f;
                b *= 0.5f;
            }

            // UV coordinates for vehicle type + direction sprite in the atlas
            // Layout: each vehicle type is a row, each direction is a column (8 directions)
            float uvSize = 1f / 8f;
            float u0 = v.Direction * uvSize;
            float v0 = v.VehicleType * (1f / 5f);
            float u1 = u0 + uvSize;
            float v1 = v0 + (1f / 5f);

            sprites.DrawInstanced(
                screenX, screenY, vw, vh,
                0f, // atlas layer 0 for vehicles
                u0, v0, u1, v1,
                r, g, b, a,
                screenY + vh // sort Y: bottom of vehicle for correct depth
            );

            // Headlight glow: 1px yellow point in front of vehicle at night
            if (v.HeadlightsOn)
            {
                float glowSize = 2f * zoom;
                var (frontDx, frontDy) = DirectionToOffset(v.Direction);
                float glowX = screenX + vw * 0.5f + frontDx * vw * 0.4f - glowSize * 0.5f;
                float glowY = screenY + vh * 0.5f + frontDy * vh * 0.4f - glowSize * 0.5f;

                sprites.DrawInstanced(
                    glowX, glowY, glowSize, glowSize,
                    0f,
                    0f, 0f, 1f, 1f, // full white UV for glow
                    1f, 0.95f, 0.6f, 0.7f * a, // warm yellow
                    screenY + vh + 0.1f
                );
            }

            // Brake light glow: 1px red behind vehicle when braking
            if (v.BrakeLightsOn)
            {
                float glowSize = 2f * zoom;
                var (frontDx, frontDy) = DirectionToOffset(v.Direction);
                float glowX = screenX + vw * 0.5f - frontDx * vw * 0.4f - glowSize * 0.5f;
                float glowY = screenY + vh * 0.5f - frontDy * vh * 0.4f - glowSize * 0.5f;

                sprites.DrawInstanced(
                    glowX, glowY, glowSize, glowSize,
                    0f,
                    0f, 0f, 1f, 1f,
                    1f, 0.1f, 0.05f, 0.8f * a, // bright red
                    screenY + vh + 0.1f
                );
            }
        }
    }

    /// <summary>
    /// Get the aggregate traffic intensity as a normalized value (0-1) based on
    /// active vehicle count relative to pool capacity. Used for audio volume scaling.
    /// </summary>
    public float GetTrafficIntensity()
    {
        return (float)_activeCount / MaxVehicles;
    }

    /// <summary>
    /// Convert heading angle (radians, 0=north clockwise) to 8-direction octant index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte HeadingToDirection(float heading)
    {
        // Normalize to 0-2pi
        float h = heading % (MathF.PI * 2f);
        if (h < 0f) h += MathF.PI * 2f;

        // Divide into 8 sectors of 45 degrees each, offset by 22.5 degrees
        int sector = (int)((h + MathF.PI / 8f) / (MathF.PI / 4f)) % 8;
        return (byte)sector;
    }

    /// <summary>
    /// Get the unit offset for a direction (used for headlight/brake light placement).
    /// Returns (dx, dy) where each is -1, 0, or 1.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static (float dx, float dy) DirectionToOffset(byte direction)
    {
        return direction switch
        {
            0 => (0f, -1f),   // N
            1 => (1f, -1f),   // NE
            2 => (1f, 0f),    // E
            3 => (1f, 1f),    // SE
            4 => (0f, 1f),    // S
            5 => (-1f, 1f),   // SW
            6 => (-1f, 0f),   // W
            7 => (-1f, -1f),  // NW
            _ => (0f, 0f),
        };
    }

    /// <summary>
    /// Compute rush hour density multiplier. Smooth ramp from 1.0 to 3.0 during
    /// morning (7-9am) and evening (5-7pm) commute windows.
    /// </summary>
    public static float ComputeRushHourMultiplier(float timeOfDay)
    {
        float morning = ComputeWindowStrength(timeOfDay, MorningRushStart, MorningRushEnd);
        float evening = ComputeWindowStrength(timeOfDay, EveningRushStart, EveningRushEnd);
        float peak = MathF.Max(morning, evening);
        return 1f + (RushHourMaxMultiplier - 1f) * peak;
    }

    /// <summary>
    /// Bell-curve-like strength within a time window.
    /// 0.0 outside the window, peaks at 1.0 at the center.
    /// </summary>
    private static float ComputeWindowStrength(float time, float start, float end)
    {
        if (time < start || time > end) return 0f;
        float mid = (start + end) * 0.5f;
        float halfWidth = (end - start) * 0.5f;
        float dist = MathF.Abs(time - mid) / halfWidth;
        // Smooth bell: 1 - dist^2
        return MathF.Max(0f, 1f - dist * dist);
    }

    private void AssignVehicleTint(ref VisualVehicle v)
    {
        // Generate varied but realistic vehicle colors
        int colorIndex = _rng.Next(12);
        (v.TintR, v.TintG, v.TintB) = colorIndex switch
        {
            0 => (0.15f, 0.15f, 0.18f),  // dark grey
            1 => (0.85f, 0.85f, 0.88f),  // white/silver
            2 => (0.12f, 0.12f, 0.14f),  // black
            3 => (0.7f, 0.15f, 0.12f),   // red
            4 => (0.15f, 0.3f, 0.6f),    // blue
            5 => (0.3f, 0.5f, 0.2f),     // green
            6 => (0.9f, 0.8f, 0.3f),     // yellow (taxis)
            7 => (0.5f, 0.35f, 0.2f),    // brown
            8 => (0.6f, 0.4f, 0.15f),    // copper
            9 => (0.4f, 0.4f, 0.42f),    // medium grey
            10 => (0.85f, 0.5f, 0.15f),  // orange
            _ => (0.5f, 0.1f, 0.4f),     // maroon
        };

        // Buses always yellow-orange
        if (v.VehicleType == 2)
        {
            v.TintR = 0.9f;
            v.TintG = 0.7f;
            v.TintB = 0.15f;
        }
        // Emergency vehicles: red and white
        else if (v.VehicleType == 3)
        {
            v.TintR = 0.95f;
            v.TintG = 0.2f;
            v.TintB = 0.15f;
        }
        // Trucks: muted earth tones
        else if (v.VehicleType == 1)
        {
            v.TintR = MathF.Min(v.TintR + 0.1f, 1f);
            v.TintG = MathF.Min(v.TintG + 0.05f, 1f);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    /// <summary>
    /// Hermite interpolation (smooth step): 3t^2 - 2t^3. Gives smooth acceleration
    /// and deceleration, eliminating the "linear slide" look.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float SmoothStep(float t)
    {
        t = MathF.Max(0f, MathF.Min(1f, t));
        return t * t * (3f - 2f * t);
    }

    public void Dispose()
    {
        // Pool is a managed array; no unmanaged resources to free.
    }
}
