using System.Runtime.CompilerServices;
using Forge.Engine.Core;
using Forge.Engine.Math;
using Forge.Engine.Simulation;

namespace Forge.Engine.Rendering;

/// <summary>
/// Visual pedestrian system. Citizens walking on sidewalks, waiting at bus stops,
/// gathering in parks -- small moving dots that make the city feel inhabited.
///
/// Citizens are entirely cosmetic: spawned proportional to population density and
/// building occupancy, not individually tracked in the simulation. This keeps the
/// visual system lightweight (max 2,000 sprites) while the simulation tracks
/// households at a higher level.
///
/// Density scales with zoom: individuals at zoom 1-2, clusters at zoom 3-4, hidden at zoom 5+.
/// </summary>
public sealed class CitizenRenderer : IDisposable
{
    /// <summary>
    /// Cosmetic pedestrian state. Not linked to any simulation entity.
    /// </summary>
    private struct VisualCitizen
    {
        public float WorldX, WorldY;          // current interpolated world position
        public float TargetX, TargetY;        // destination (next waypoint)
        public float Speed;                   // world units per second
        public byte Direction;                // 0-7 octants
        public byte Activity;                 // 0=walking, 1=waiting, 2=sitting, 3=shopping, 4=working
        public byte AppearanceType;           // 0=child, 1=adult, 2=elderly, 3=worker, 4=businessperson
        public byte WealthClass;              // 0-3 clothing quality
        public byte AnimationFrame;           // walk cycle frame (0-3)
        public float AnimationTimer;          // accumulates dt for frame advance
        public bool HasUmbrella;              // during rain
        public bool IsRunning;               // late for work or rain without umbrella
        public float TintR, TintG, TintB;    // clothing color
        public float Alpha;                  // for fade in/out
        public bool Active;
        public float Lifetime;               // seconds remaining before despawn
    }

    private readonly Config _config;
    private readonly VisualCitizen[] _pool;
    private int _activeCount;
    private readonly Random _rng;

    // Pool limit
    public const int MaxCitizens = 2_000;

    // Sprite dimensions at zoom 1
    private const float CitizenWidth = 6f;
    private const float CitizenHeight = 10f;

    // Walk cycle timing
    private const float WalkFrameDuration = 0.15f; // seconds per animation frame
    private const float RunFrameDuration = 0.08f;
    private const int WalkFrameCount = 4;

    // Speed constants (world units per second)
    private const float ChildSpeed = 0.8f;
    private const float AdultSpeed = 1.2f;
    private const float ElderlySpeed = 0.6f;
    private const float BusinessSpeed = 1.5f;
    private const float RunSpeed = 2.5f;

    // Lifetime range (seconds before despawn and respawn elsewhere)
    private const float MinLifetime = 8f;
    private const float MaxLifetime = 30f;

    // Fade
    private const float FadeDuration = 0.3f;
    private const float FadeOutThreshold = 0.5f;

    /// <summary>Number of active visual citizens.</summary>
    public int ActiveCount => _activeCount;

    public CitizenRenderer(Config config)
    {
        _config = config;
        _pool = new VisualCitizen[MaxCitizens];
        _rng = new Random(12345);
    }

    /// <summary>
    /// Synchronize citizen density from the simulation snapshot.
    /// Citizens are spawned near occupied buildings proportional to population.
    /// Called once per sim tick.
    /// </summary>
    public void UpdateFromSimulation(SimSnapshot snapshot, IsometricCamera camera)
    {
        // Target citizen count based on population, time of day, and weather
        int baseTarget = System.Math.Min(snapshot.Population / 10, MaxCitizens);
        float timeMultiplier = ComputePedestrianTimeMultiplier(snapshot.TimeOfDay);
        float weatherMultiplier = ComputeWeatherMultiplier(snapshot.WeatherCondition);
        int targetCount = (int)(baseTarget * timeMultiplier * weatherMultiplier);
        targetCount = System.Math.Clamp(targetCount, 0, MaxCitizens);

        // Spawn new citizens near buildings to reach target count
        int spawned = 0;
        for (int i = _activeCount; i < targetCount && spawned < 50; i++) // max 50 spawns per tick to spread load
        {
            if (i >= MaxCitizens) break;

            // Pick a random building to spawn near
            if (snapshot.BuildingCount > 0)
            {
                int bIdx = _rng.Next(snapshot.BuildingCount);
                ref var building = ref snapshot.Buildings[bIdx];

                // Only spawn near operational buildings with occupants
                if (building.State == 1 && building.Occupants > 0)
                {
                    SpawnCitizen(ref _pool[i], building.GridX, building.GridY,
                        snapshot.WeatherCondition, snapshot.Era);
                    spawned++;
                }
            }
        }

        // Fade out excess citizens
        for (int i = targetCount; i < _activeCount; i++)
        {
            _pool[i].Lifetime = MathF.Min(_pool[i].Lifetime, FadeOutThreshold);
        }

        _activeCount = System.Math.Max(_activeCount, System.Math.Min(_activeCount + spawned, MaxCitizens));
    }

    /// <summary>
    /// Per-frame update: advance walk animations, move citizens toward targets,
    /// handle lifetime and fading.
    /// </summary>
    public void Update(float dt)
    {
        int compacted = 0;
        for (int i = 0; i < _activeCount; i++)
        {
            ref var c = ref _pool[i];
            if (!c.Active) continue;

            // Decrement lifetime
            c.Lifetime -= dt;

            // Fade out when lifetime is low
            if (c.Lifetime <= FadeOutThreshold)
            {
                c.Alpha = MathF.Max(0f, c.Lifetime / FadeOutThreshold);
                if (c.Lifetime <= 0f)
                {
                    c.Active = false;
                    continue;
                }
            }
            else if (c.Alpha < 1f)
            {
                // Fade in
                c.Alpha = MathF.Min(c.Alpha + dt / FadeDuration, 1f);
            }

            // Move toward target
            if (c.Activity == 0 || c.Activity == 3) // walking or shopping
            {
                float speed = c.IsRunning ? RunSpeed : c.Speed;
                float dx = c.TargetX - c.WorldX;
                float dy = c.TargetY - c.WorldY;
                float dist = MathF.Sqrt(dx * dx + dy * dy);

                if (dist > 0.05f)
                {
                    float move = speed * dt;
                    if (move >= dist)
                    {
                        c.WorldX = c.TargetX;
                        c.WorldY = c.TargetY;
                        // Pick new random target nearby
                        PickNewTarget(ref c);
                    }
                    else
                    {
                        float invDist = 1f / dist;
                        c.WorldX += dx * invDist * move;
                        c.WorldY += dy * invDist * move;
                    }

                    // Update direction from movement
                    c.Direction = VectorToDirection(dx, dy);

                    // Advance walk animation
                    float frameDur = c.IsRunning ? RunFrameDuration : WalkFrameDuration;
                    c.AnimationTimer += dt;
                    if (c.AnimationTimer >= frameDur)
                    {
                        c.AnimationTimer -= frameDur;
                        c.AnimationFrame = (byte)((c.AnimationFrame + 1) % WalkFrameCount);
                    }
                }
                else
                {
                    PickNewTarget(ref c);
                }
            }
            else
            {
                // Waiting/sitting: no movement, occasional idle animation
                c.AnimationFrame = 0;
            }

            compacted++;
        }
    }

    /// <summary>
    /// Render all visible citizens via GPU instancing.
    /// At zoom 3-4, citizens are rendered as small colored dots (cluster mode).
    /// At zoom 5+, citizens are hidden.
    /// </summary>
    public void Render(IsometricCamera camera, SpriteRenderer sprites)
    {
        // Cull at high zoom levels
        if (camera.ZoomLevel >= 5) return;

        var (offsetX, offsetY) = camera.GetRenderOffset();
        float zoom = camera.SmoothZoom;
        var frustum = camera.GetFrustumBounds();
        int tw = _config.TileWidth;
        int th = _config.TileHeight;

        // At zoom 3-4, render as small dots instead of detailed sprites
        bool clusterMode = camera.ZoomLevel >= 3;

        float cw = (clusterMode ? 3f : CitizenWidth) * zoom;
        float ch = (clusterMode ? 3f : CitizenHeight) * zoom;

        for (int i = 0; i < _activeCount; i++)
        {
            ref var c = ref _pool[i];
            if (!c.Active || c.Alpha <= 0f) continue;

            // Convert world grid position to isometric screen position
            float halfW = tw * 0.5f;
            float halfH = th * 0.5f;
            float isoX = (c.WorldX - c.WorldY) * halfW;
            float isoY = (c.WorldX + c.WorldY) * halfH;

            // Frustum cull
            if (!frustum.Contains(isoX, isoY)) continue;

            float screenX = isoX * zoom + offsetX - cw * 0.5f;
            float screenY = isoY * zoom + offsetY - ch;

            if (clusterMode)
            {
                // Simple colored dot
                sprites.DrawInstanced(
                    screenX, screenY, cw, ch,
                    0f,
                    0f, 0f, 1f, 1f,
                    c.TintR, c.TintG, c.TintB, c.Alpha * 0.8f,
                    screenY + ch
                );
            }
            else
            {
                // UV for appearance type + direction + animation frame
                float typeV = c.AppearanceType * (1f / 5f);
                float dirU = c.Direction * (1f / 8f);
                float frameOffset = c.AnimationFrame * (1f / (8f * WalkFrameCount));

                sprites.DrawInstanced(
                    screenX, screenY, cw, ch,
                    1f, // atlas layer 1 for citizens
                    dirU + frameOffset, typeV, dirU + frameOffset + (1f / (8f * WalkFrameCount)), typeV + (1f / 5f),
                    c.TintR, c.TintG, c.TintB, c.Alpha,
                    screenY + ch
                );

                // Umbrella overlay during rain
                if (c.HasUmbrella)
                {
                    float umbrellaSize = 5f * zoom;
                    sprites.DrawInstanced(
                        screenX + cw * 0.5f - umbrellaSize * 0.5f,
                        screenY - umbrellaSize * 0.6f,
                        umbrellaSize, umbrellaSize * 0.6f,
                        1f,
                        0f, 0.8f, 0.125f, 1f, // umbrella UV region
                        0.3f, 0.3f, 0.6f, c.Alpha * 0.9f, // dark blue umbrella
                        screenY + ch + 0.1f
                    );
                }
            }
        }
    }

    /// <summary>
    /// Pedestrian density multiplier based on time of day.
    /// High during commute hours, low at night, medium during day.
    /// </summary>
    public static float ComputePedestrianTimeMultiplier(float timeOfDay)
    {
        // Night (22:00 - 05:00): very low pedestrians
        if (timeOfDay >= 22f || timeOfDay < 5f)
            return 0.1f;

        // Early morning (5-7): ramp up
        if (timeOfDay < 7f)
            return Lerp(0.1f, 0.8f, (timeOfDay - 5f) / 2f);

        // Morning rush (7-9): peak
        if (timeOfDay < 9f)
            return 1.0f;

        // Mid-day (9-17): steady moderate
        if (timeOfDay < 17f)
            return 0.6f;

        // Evening rush (17-19): peak
        if (timeOfDay < 19f)
            return 1.0f;

        // Evening (19-22): wind down
        return Lerp(1.0f, 0.1f, (timeOfDay - 19f) / 3f);
    }

    /// <summary>
    /// Weather reduces pedestrian density. Blizzards clear the streets entirely.
    /// </summary>
    public static float ComputeWeatherMultiplier(int weatherCondition)
    {
        return weatherCondition switch
        {
            0 => 1.0f,  // clear
            1 => 0.9f,  // cloudy
            2 => 0.5f,  // rain
            3 => 0.2f,  // storm
            4 => 0.4f,  // snow
            5 => 0.7f,  // fog
            6 => 0.8f,  // heatwave
            7 => 0.05f, // blizzard -- no one walks in a blizzard
            _ => 1.0f,
        };
    }

    private void SpawnCitizen(ref VisualCitizen c, int buildingX, int buildingY,
        int weather, int era)
    {
        c.Active = true;
        c.Alpha = 0f;

        // Spawn on the sidewalk adjacent to the building (offset by 0.5-1.5 tiles)
        float offsetX = (float)(_rng.NextDouble() * 2.0 - 0.5);
        float offsetY = (float)(_rng.NextDouble() * 2.0 - 0.5);
        c.WorldX = buildingX + offsetX;
        c.WorldY = buildingY + offsetY;

        // Random appearance
        c.AppearanceType = (byte)_rng.Next(5);
        c.WealthClass = (byte)_rng.Next(4);

        // Speed based on appearance type
        c.Speed = c.AppearanceType switch
        {
            0 => ChildSpeed,
            1 => AdultSpeed,
            2 => ElderlySpeed,
            3 => AdultSpeed * 1.1f, // worker
            4 => BusinessSpeed,
            _ => AdultSpeed,
        };

        // Activity: mostly walking, some waiting
        c.Activity = _rng.Next(10) < 7 ? (byte)0 : (byte)1;

        // Umbrella in rain (70% of citizens have one)
        c.HasUmbrella = weather == 2 && _rng.Next(10) < 7;

        // Running: in rain without umbrella, or random business people
        c.IsRunning = (weather == 2 && !c.HasUmbrella) ||
                      (c.AppearanceType == 4 && _rng.Next(10) < 2);

        // Clothing color variation
        AssignClothingColor(ref c);

        // Lifetime
        c.Lifetime = MinLifetime + (float)_rng.NextDouble() * (MaxLifetime - MinLifetime);

        // Initial target
        PickNewTarget(ref c);

        // Animation
        c.AnimationFrame = (byte)_rng.Next(WalkFrameCount);
        c.AnimationTimer = (float)_rng.NextDouble() * WalkFrameDuration;
    }

    private void PickNewTarget(ref VisualCitizen c)
    {
        // Walk to a random nearby point (within 2-5 tiles)
        float range = 2f + (float)_rng.NextDouble() * 3f;
        float angle = (float)_rng.NextDouble() * MathF.PI * 2f;
        c.TargetX = c.WorldX + MathF.Cos(angle) * range;
        c.TargetY = c.WorldY + MathF.Sin(angle) * range;
    }

    private void AssignClothingColor(ref VisualCitizen c)
    {
        int colorIndex = _rng.Next(10);
        (c.TintR, c.TintG, c.TintB) = colorIndex switch
        {
            0 => (0.2f, 0.2f, 0.25f),   // dark suit
            1 => (0.15f, 0.25f, 0.4f),   // navy
            2 => (0.5f, 0.15f, 0.15f),   // red jacket
            3 => (0.3f, 0.45f, 0.2f),    // olive
            4 => (0.8f, 0.75f, 0.6f),    // khaki
            5 => (0.6f, 0.6f, 0.65f),    // light grey
            6 => (0.35f, 0.2f, 0.45f),   // purple
            7 => (0.2f, 0.4f, 0.5f),     // teal
            8 => (0.7f, 0.4f, 0.15f),    // orange
            _ => (0.4f, 0.3f, 0.2f),     // brown
        };

        // Workers: high-vis yellow/orange
        if (c.AppearanceType == 3)
        {
            c.TintR = 0.9f;
            c.TintG = 0.8f;
            c.TintB = 0.15f;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static byte VectorToDirection(float dx, float dy)
    {
        float angle = MathF.Atan2(dy, dx);
        if (angle < 0f) angle += MathF.PI * 2f;
        int sector = (int)((angle + MathF.PI / 8f) / (MathF.PI / 4f)) % 8;
        return (byte)sector;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    public void Dispose()
    {
        // Pool is managed; nothing to release.
    }
}
