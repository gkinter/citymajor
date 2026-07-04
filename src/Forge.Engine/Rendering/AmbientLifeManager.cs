using Forge.Engine.Core;
using Forge.Engine.Math;
using Forge.Engine.Simulation;

namespace Forge.Engine.Rendering;

/// <summary>
/// Coordinates ALL visual life systems and adds environmental ambient details.
/// This is the top-level orchestrator that makes the city feel alive.
///
/// It manages:
///   - TrafficRenderer (vehicles on roads)
///   - CitizenRenderer (pedestrians on sidewalks)
///   - BuildingLifeRenderer (smoke, glow, construction)
///   - Environmental ambience (birds, cloud shadows, leaves, flags, steam vents, street lights)
///
/// Each sub-system has its own update rate:
///   - Traffic + Citizens: every sim tick via UpdateFromSimulation
///   - Building life: every sim tick
///   - Environmental details: every frame (lightweight procedural effects)
///
/// All rendering funnels through a single SpriteRenderer (GPU instanced)
/// and a ParticleSystem for smoke/weather effects.
/// </summary>
public sealed class AmbientLifeManager : IDisposable
{
    private readonly Config _config;
    private readonly TrafficRenderer _traffic;
    private readonly CitizenRenderer _citizens;
    private readonly BuildingLifeRenderer _buildings;

    // Environmental ambient state
    private readonly AmbientBird[] _birds;
    private int _activeBirdCount;
    private readonly AmbientCloud[] _clouds;
    private int _activeCloudCount;
    private readonly Random _rng;

    // Cached sim state for frame updates
    private float _timeOfDay;
    private int _season;
    private int _weather;
    private float _windSpeed;
    private float _windDirection;
    private int _era;
    private SimSnapshot? _lastSnapshot;

    // Street light animation
    private float _streetLightProgress; // 0-1 spread of lights turning on
    private bool _lightsOn;

    // Particle emitters managed by this system
    private readonly List<ManagedEmitter> _managedEmitters = new();

    // Sub-system limits
    private const int MaxBirds = 30;
    private const int MaxClouds = 8;

    /// <summary>Traffic visualization sub-system.</summary>
    public TrafficRenderer Traffic => _traffic;

    /// <summary>Citizen/pedestrian visualization sub-system.</summary>
    public CitizenRenderer Citizens => _citizens;

    /// <summary>Building life visualization sub-system.</summary>
    public BuildingLifeRenderer Buildings => _buildings;

    /// <summary>Current time of day (0-24).</summary>
    public float TimeOfDay => _timeOfDay;

    /// <summary>Whether street lights are currently on.</summary>
    public bool StreetLightsOn => _lightsOn;

    public AmbientLifeManager(Config config)
    {
        _config = config;
        _traffic = new TrafficRenderer(config);
        _citizens = new CitizenRenderer(config);
        _buildings = new BuildingLifeRenderer(config);
        _birds = new AmbientBird[MaxBirds];
        _clouds = new AmbientCloud[MaxClouds];
        _rng = new Random(99999);
    }

    /// <summary>
    /// Full update from simulation snapshot. Called once per sim tick.
    /// Dispatches to all sub-systems and manages environmental spawning.
    /// </summary>
    public void UpdateFromSimulation(SimSnapshot snapshot, IsometricCamera camera, ParticleSystem particles)
    {
        _lastSnapshot = snapshot;
        _timeOfDay = snapshot.TimeOfDay;
        _season = snapshot.Season;
        _weather = snapshot.WeatherCondition;
        _windSpeed = snapshot.WindSpeed;
        _windDirection = snapshot.WindDirection;
        _era = snapshot.Era;

        // Dispatch to sub-systems
        _traffic.UpdateFromSimulation(snapshot, camera);
        _citizens.UpdateFromSimulation(snapshot, camera);
        _buildings.UpdateFromSimulation(snapshot);

        // Manage smoke emitters for industrial buildings
        UpdateSmokeEmitters(snapshot, particles);

        // Spawn/despawn birds based on environment
        UpdateBirdPopulation(snapshot, camera);

        // Spawn/despawn cloud shadows
        UpdateCloudPopulation(camera);
    }

    /// <summary>
    /// Per-frame update. Smooth interpolation for all visual systems and
    /// environmental animation (birds, clouds, leaves, steam).
    /// </summary>
    public void Update(float dt, float timeOfDay, int season, int weather,
        SimSnapshot snapshot, IsometricCamera camera)
    {
        _timeOfDay = timeOfDay;
        _season = season;
        _weather = weather;

        // Sub-system frame updates
        _traffic.Update(dt);
        _citizens.Update(dt);
        _buildings.Update(dt, timeOfDay, weather);

        // Environmental updates
        UpdateBirds(dt, camera);
        UpdateClouds(dt);
        UpdateStreetLights(dt, timeOfDay);
    }

    /// <summary>
    /// Render all visual life systems. Order matters for depth sorting:
    /// 1. Cloud shadows (behind everything)
    /// 2. Building glow + effects
    /// 3. Traffic (vehicles)
    /// 4. Citizens (pedestrians)
    /// 5. Birds (in front/above)
    /// 6. Environmental overlays (leaves, steam, street lights)
    /// </summary>
    public void Render(IsometricCamera camera, SpriteRenderer sprites, ParticleSystem particles)
    {
        // 1. Cloud shadows
        RenderCloudShadows(camera, sprites);

        // 2. Building life effects (glow, neon, construction, deterioration)
        RenderBuildingEffects(camera, sprites, particles);

        // 3. Traffic
        _traffic.Render(camera, sprites, _timeOfDay);

        // 4. Citizens
        _citizens.Render(camera, sprites);

        // 5. Birds
        RenderBirds(camera, sprites);

        // 6. Street lights
        RenderStreetLights(camera, sprites);

        // 7. Seasonal effects (autumn leaves)
        if (_season == 2) // autumn
        {
            RenderAutumnLeaves(camera, sprites);
        }

        // 8. Steam vents in cold weather
        if (_weather == 4 || _weather == 7 || _season == 3) // snow, blizzard, or winter
        {
            RenderSteamVents(camera, sprites);
        }
    }

    // =========================================================================
    // Bird system -- V-shaped sprites flying across the sky
    // =========================================================================

    private struct AmbientBird
    {
        public float WorldX, WorldY;
        public float SpeedX, SpeedY;
        public float FlapTimer;
        public byte FlapFrame; // 0-1 wing up/down
        public bool Active;
    }

    private void UpdateBirdPopulation(SimSnapshot snapshot, IsometricCamera camera)
    {
        // More birds near parks/forests, fewer at night, none in blizzards
        int targetBirds = _weather == 7 ? 0 : // blizzard
                          _weather == 3 ? 2 :  // storm
                          (_timeOfDay >= 6f && _timeOfDay <= 20f) ? 15 : 3;

        // Spawn birds if needed
        var frustum = camera.GetFrustumBounds();
        for (int i = _activeBirdCount; i < targetBirds && i < MaxBirds; i++)
        {
            _birds[i] = new AmbientBird
            {
                WorldX = frustum.MinX + (float)_rng.NextDouble() * frustum.Width,
                WorldY = frustum.MinY - 50f, // start above viewport
                SpeedX = 20f + (float)_rng.NextDouble() * 30f,
                SpeedY = 5f + (float)_rng.NextDouble() * 15f,
                FlapTimer = (float)_rng.NextDouble() * MathF.PI * 2f,
                FlapFrame = 0,
                Active = true,
            };
        }
        _activeBirdCount = System.Math.Min(targetBirds, MaxBirds);
    }

    private void UpdateBirds(float dt, IsometricCamera camera)
    {
        var frustum = camera.GetFrustumBounds();
        for (int i = 0; i < _activeBirdCount; i++)
        {
            ref var bird = ref _birds[i];
            if (!bird.Active) continue;

            bird.WorldX += bird.SpeedX * dt;
            bird.WorldY += bird.SpeedY * dt;

            // Wing flap animation
            bird.FlapTimer += dt * 6f;
            bird.FlapFrame = (byte)(((int)(bird.FlapTimer / MathF.PI) % 2));

            // Despawn if out of view
            if (bird.WorldX > frustum.MaxX + 100f || bird.WorldY > frustum.MaxY + 100f)
            {
                bird.Active = false;
                // Respawn at edge
                bird.WorldX = frustum.MinX - 50f;
                bird.WorldY = frustum.MinY + (float)_rng.NextDouble() * frustum.Height * 0.5f;
                bird.Active = true;
            }
        }
    }

    private void RenderBirds(IsometricCamera camera, SpriteRenderer sprites)
    {
        // Only visible at zoom 3+
        if (camera.ZoomLevel < 3) return;

        var (offsetX, offsetY) = camera.GetRenderOffset();
        float zoom = camera.SmoothZoom;

        for (int i = 0; i < _activeBirdCount; i++)
        {
            ref var bird = ref _birds[i];
            if (!bird.Active) continue;

            float screenX = bird.WorldX * zoom + offsetX;
            float screenY = bird.WorldY * zoom + offsetY;
            float size = 4f * zoom;

            // Bird as a small V shape (two triangles = one sprite with V UV)
            float wingAngle = bird.FlapFrame == 0 ? 0.8f : 0.5f;
            sprites.DrawInstanced(
                screenX - size * 0.5f, screenY - size * 0.5f,
                size, size * wingAngle,
                3f, // atlas layer 3 for ambient details
                0f, 0f, 0.125f, 0.125f, // bird UV
                0.15f, 0.12f, 0.1f, 0.7f, // dark brown, semi-transparent
                screenY - 1000f // birds are high up, sort behind everything
            );
        }
    }

    // =========================================================================
    // Cloud shadow system
    // =========================================================================

    private struct AmbientCloud
    {
        public float WorldX, WorldY;
        public float SpeedX;
        public float Width, Height;
        public float Alpha;
        public bool Active;
    }

    private void UpdateCloudPopulation(IsometricCamera camera)
    {
        int targetClouds = _weather switch
        {
            0 => 2,  // clear
            1 => 5,  // cloudy
            2 => 6,  // rain
            3 => 8,  // storm
            4 => 4,  // snow
            5 => 8,  // fog
            _ => 3,
        };

        var frustum = camera.GetFrustumBounds();
        for (int i = _activeCloudCount; i < targetClouds && i < MaxClouds; i++)
        {
            _clouds[i] = new AmbientCloud
            {
                WorldX = frustum.MinX - 200f + (float)_rng.NextDouble() * (frustum.Width + 400f),
                WorldY = frustum.MinY + (float)_rng.NextDouble() * frustum.Height,
                SpeedX = 5f + (float)_rng.NextDouble() * 15f,
                Width = 80f + (float)_rng.NextDouble() * 160f,
                Height = 40f + (float)_rng.NextDouble() * 80f,
                Alpha = 0.08f + (float)_rng.NextDouble() * 0.12f,
                Active = true,
            };
        }
        _activeCloudCount = System.Math.Min(targetClouds, MaxClouds);
    }

    private void UpdateClouds(float dt)
    {
        for (int i = 0; i < _activeCloudCount; i++)
        {
            ref var cloud = ref _clouds[i];
            if (!cloud.Active) continue;

            cloud.WorldX += cloud.SpeedX * dt;

            // Wind influence
            cloud.SpeedX += MathF.Cos(_windDirection) * _windSpeed * 0.1f * dt;
        }
    }

    private void RenderCloudShadows(IsometricCamera camera, SpriteRenderer sprites)
    {
        // Subtle darkening patches on the ground
        if (_weather == 0 && _activeCloudCount <= 1) return; // skip in clear weather with few clouds

        var (offsetX, offsetY) = camera.GetRenderOffset();
        float zoom = camera.SmoothZoom;

        for (int i = 0; i < _activeCloudCount; i++)
        {
            ref var cloud = ref _clouds[i];
            if (!cloud.Active) continue;

            float screenX = cloud.WorldX * zoom + offsetX;
            float screenY = cloud.WorldY * zoom + offsetY;
            float w = cloud.Width * zoom;
            float h = cloud.Height * zoom;

            sprites.DrawInstanced(
                screenX - w * 0.5f, screenY - h * 0.5f,
                w, h,
                3f,
                0.125f, 0f, 0.25f, 0.125f, // cloud shadow UV
                0f, 0f, 0f, cloud.Alpha, // pure black with low alpha
                screenY - 2000f // behind everything
            );
        }
    }

    // =========================================================================
    // Street light system
    // =========================================================================

    private void UpdateStreetLights(float dt, float timeOfDay)
    {
        bool shouldBeOn = timeOfDay >= 18f || timeOfDay < 6f;

        if (shouldBeOn && !_lightsOn)
        {
            // Lights turning on: spread from center outward over ~2 real seconds
            _streetLightProgress = MathF.Min(_streetLightProgress + dt * 0.5f, 1f);
            if (_streetLightProgress >= 1f) _lightsOn = true;
        }
        else if (!shouldBeOn && _lightsOn)
        {
            // Lights turning off
            _streetLightProgress = MathF.Max(_streetLightProgress - dt * 0.8f, 0f);
            if (_streetLightProgress <= 0f) _lightsOn = false;
        }
        else if (shouldBeOn)
        {
            _streetLightProgress = 1f;
        }
        else
        {
            _streetLightProgress = 0f;
        }
    }

    private void RenderStreetLights(IsometricCamera camera, SpriteRenderer sprites)
    {
        if (_streetLightProgress <= 0f) return;
        if (camera.ZoomLevel >= 4) return; // too zoomed out to see

        // Street lights are rendered at road tile positions
        // We iterate visible road tiles and place small glow sprites
        if (_lastSnapshot == null) return;

        var (offsetX, offsetY) = camera.GetRenderOffset();
        float zoom = camera.SmoothZoom;
        var frustum = camera.GetFrustumBounds();
        int worldSize = _lastSnapshot.WorldSize;
        int tw = _config.TileWidth;
        int th = _config.TileHeight;
        float halfW = tw * 0.5f;
        float halfH = th * 0.5f;

        // Only check tiles within frustum (approximate grid bounds)
        int margin = 2;
        int startGX = System.Math.Max(0, (int)((frustum.MinX / halfW + frustum.MinY / halfH) * 0.5f) - margin);
        int endGX = System.Math.Min(worldSize, (int)((frustum.MaxX / halfW + frustum.MaxY / halfH) * 0.5f) + margin);
        int startGY = System.Math.Max(0, (int)((frustum.MinY / halfH - frustum.MaxX / halfW) * 0.5f) - margin);
        int endGY = System.Math.Min(worldSize, (int)((frustum.MaxY / halfH - frustum.MinX / halfW) * 0.5f) + margin);

        float glowSize = 6f * zoom;
        int lightCount = 0;
        int maxLights = 200; // cap for performance

        for (int gy = startGY; gy < endGY && lightCount < maxLights; gy++)
        {
            for (int gx = startGX; gx < endGX && lightCount < maxLights; gx++)
            {
                if (gx < 0 || gy < 0 || gx >= worldSize || gy >= worldSize) continue;
                int idx = gy * worldSize + gx;

                byte roadFlags = _lastSnapshot.TileRoadFlags[idx];
                if (roadFlags == 0) continue; // no road

                // Place street light every 3 tiles on roads
                if ((gx + gy) % 3 != 0) continue;

                // Distance from center determines stagger delay
                float centerDist = MathF.Abs(gx - worldSize * 0.5f) + MathF.Abs(gy - worldSize * 0.5f);
                float normalizedDist = centerDist / worldSize;
                if (normalizedDist > _streetLightProgress) continue;

                var (sx, sy) = IsometricMath.GridToScreen(gx, gy, tw, th, camera.Rotation);
                float screenX = sx * zoom + offsetX;
                float screenY = sy * zoom + offsetY;

                // Warm street light glow
                sprites.DrawInstanced(
                    screenX - glowSize * 0.5f,
                    screenY - glowSize * 0.5f - th * zoom * 0.3f,
                    glowSize, glowSize,
                    3f,
                    0.25f, 0f, 0.375f, 0.125f, // street light UV
                    1f, 0.9f, 0.6f, 0.5f * _streetLightProgress, // warm glow
                    screenY
                );

                lightCount++;
            }
        }
    }

    // =========================================================================
    // Building life rendering (coordinated pass with grid positions)
    // =========================================================================

    private void RenderBuildingEffects(IsometricCamera camera, SpriteRenderer sprites, ParticleSystem particles)
    {
        if (_lastSnapshot == null) return;

        var (offsetX, offsetY) = camera.GetRenderOffset();
        float zoom = camera.SmoothZoom;
        var frustum = camera.GetFrustumBounds();
        int tw = _config.TileWidth;
        int th = _config.TileHeight;

        for (int i = 0; i < _lastSnapshot.BuildingCount && i < BuildingLifeRenderer.MaxTrackedBuildings; i++)
        {
            ref var bSnap = ref _lastSnapshot.Buildings[i];

            // Get screen position
            var (sx, sy) = IsometricMath.GridToScreen(bSnap.GridX, bSnap.GridY, tw, th, camera.Rotation);

            // Frustum cull
            if (!frustum.Contains(sx, sy)) continue;

            float screenX = sx * zoom + offsetX;
            float screenY = sy * zoom + offsetY;
            float buildingW = tw * zoom;
            float buildingH = th * zoom * 2f; // buildings are taller than a tile

            _buildings.RenderBuildingGlow(i, screenX, screenY, buildingW, buildingH, zoom, sprites, _era);
        }
    }

    // =========================================================================
    // Smoke emitter management
    // =========================================================================

    private struct ManagedEmitter
    {
        public int BuildingIndex;
        public ParticleEmitter? Emitter;
        public bool Heavy; // heavy vs light smoke
    }

    private void UpdateSmokeEmitters(SimSnapshot snapshot, ParticleSystem particles)
    {
        // Reconcile emitters with building smoke states
        // Remove emitters for buildings that no longer smoke
        for (int i = _managedEmitters.Count - 1; i >= 0; i--)
        {
            var me = _managedEmitters[i];
            float intensity = _buildings.GetSmokeIntensity(me.BuildingIndex);
            if (intensity <= 0.01f && me.Emitter != null)
            {
                me.Emitter.Stop();
                particles.DestroyEmitter(me.Emitter);
                _managedEmitters.RemoveAt(i);
            }
            else if (me.Emitter != null)
            {
                // Update emitter position and rate based on building position
                if (me.BuildingIndex < snapshot.BuildingCount)
                {
                    ref var bSnap = ref snapshot.Buildings[me.BuildingIndex];
                    var (sx, sy) = IsometricMath.GridToScreen(
                        bSnap.GridX, bSnap.GridY,
                        _config.TileWidth, _config.TileHeight, 0);
                    me.Emitter.X = sx;
                    me.Emitter.Y = sy - _config.TileHeight; // chimney is above building
                    me.Emitter.EmitRate = (me.Heavy ? 12f : 6f) * intensity;
                }
            }
        }

        // Add emitters for newly smoking buildings (limit to 50 emitters total)
        if (_managedEmitters.Count < 50)
        {
            for (int i = 0; i < snapshot.BuildingCount && _managedEmitters.Count < 50; i++)
            {
                float intensity = _buildings.GetSmokeIntensity(i);
                if (intensity <= 0.01f) continue;

                // Check if we already have an emitter for this building
                bool found = false;
                for (int j = 0; j < _managedEmitters.Count; j++)
                {
                    if (_managedEmitters[j].BuildingIndex == i) { found = true; break; }
                }
                if (found) continue;

                // Create new emitter
                bool heavy = intensity > 0.6f;
                var config = heavy
                    ? BuildingLifeRenderer.GetHeavySmokeConfig()
                    : BuildingLifeRenderer.GetLightSmokeConfig();
                var emitter = particles.CreateEmitter(config);
                emitter.Start();

                _managedEmitters.Add(new ManagedEmitter
                {
                    BuildingIndex = i,
                    Emitter = emitter,
                    Heavy = heavy,
                });
            }
        }
    }

    // =========================================================================
    // Seasonal effects
    // =========================================================================

    private void RenderAutumnLeaves(IsometricCamera camera, SpriteRenderer sprites)
    {
        // Procedural falling leaves near forest tiles
        if (camera.ZoomLevel >= 4) return;
        if (_lastSnapshot == null) return;

        var (offsetX, offsetY) = camera.GetRenderOffset();
        float zoom = camera.SmoothZoom;

        // Use a deterministic pattern based on time for leaf positions
        // This avoids storing per-leaf state -- leaves are procedural
        float time = _timeOfDay * 100f; // cycle over the day
        int leafCount = System.Math.Min(40, 40 / System.Math.Max(1, camera.ZoomLevel));

        for (int i = 0; i < leafCount; i++)
        {
            // Pseudo-random but deterministic leaf position
            float seed = i * 7.31f + time * 0.02f;
            float lx = (MathF.Sin(seed * 1.3f) * 0.5f + 0.5f) * _lastSnapshot.WorldSize;
            float ly = (MathF.Cos(seed * 0.7f) * 0.5f + 0.5f) * _lastSnapshot.WorldSize;

            // Drift with wind
            float drift = MathF.Sin(time * 0.1f + i * 2.1f) * 2f;

            float halfW = _config.TileWidth * 0.5f;
            float halfH = _config.TileHeight * 0.5f;
            float isoX = ((lx + drift) - ly) * halfW;
            float isoY = ((lx + drift) + ly) * halfH;

            float screenX = isoX * zoom + offsetX;
            float screenY = isoY * zoom + offsetY;
            float leafSize = 2f * zoom;

            // Autumn colors: red, orange, yellow, brown
            int colorIdx = i % 4;
            var (cr, cg, cb) = colorIdx switch
            {
                0 => (0.8f, 0.2f, 0.1f),  // red
                1 => (0.9f, 0.6f, 0.1f),  // orange
                2 => (0.9f, 0.8f, 0.2f),  // yellow
                _ => (0.5f, 0.3f, 0.1f),  // brown
            };

            sprites.DrawInstanced(
                screenX, screenY, leafSize, leafSize,
                3f,
                0.375f, 0f, 0.5f, 0.125f, // leaf UV
                cr, cg, cb, 0.6f,
                screenY + 500f // in front of most things
            );
        }
    }

    private void RenderSteamVents(IsometricCamera camera, SpriteRenderer sprites)
    {
        // Small steam puffs from random road tiles in cold weather
        if (camera.ZoomLevel >= 4) return;
        if (_lastSnapshot == null) return;

        var (offsetX, offsetY) = camera.GetRenderOffset();
        float zoom = camera.SmoothZoom;
        int tw = _config.TileWidth;
        int th = _config.TileHeight;

        float time = _timeOfDay * 50f;
        int ventCount = System.Math.Min(15, 15 / System.Math.Max(1, camera.ZoomLevel));

        for (int i = 0; i < ventCount; i++)
        {
            // Deterministic vent positions
            int gx = (i * 37 + 13) % _lastSnapshot.WorldSize;
            int gy = (i * 53 + 7) % _lastSnapshot.WorldSize;

            // Only on road tiles
            int idx = gy * _lastSnapshot.WorldSize + gx;
            if (idx >= _lastSnapshot.TileRoadFlags.Length || _lastSnapshot.TileRoadFlags[idx] == 0)
                continue;

            var (sx, sy) = IsometricMath.GridToScreen(gx, gy, tw, th, camera.Rotation);
            float screenX = sx * zoom + offsetX;
            float screenY = sy * zoom + offsetY;

            // Animated steam puff (rising and fading)
            float phase = MathF.Sin(time * 0.05f + i * 1.7f) * 0.5f + 0.5f;
            float steamSize = (3f + phase * 3f) * zoom;
            float steamAlpha = 0.3f * (1f - phase);

            sprites.DrawInstanced(
                screenX - steamSize * 0.5f,
                screenY - steamSize - phase * 8f * zoom,
                steamSize, steamSize,
                3f,
                0.5f, 0f, 0.625f, 0.125f, // steam UV
                0.9f, 0.9f, 0.95f, steamAlpha,
                screenY - 100f
            );
        }
    }

    public void Dispose()
    {
        _traffic.Dispose();
        _citizens.Dispose();
        _buildings.Dispose();
    }
}
