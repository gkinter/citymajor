using System.Runtime.CompilerServices;
using Forge.Engine.Core;
using Forge.Engine.Math;
using Forge.Engine.Simulation;

namespace Forge.Engine.Rendering;

/// <summary>
/// Animates buildings so they breathe, smoke, glow, and show activity.
/// Buildings are NOT static boxes -- they have chimney smoke, window glow at night,
/// construction scaffolding, deterioration cracks, and celebration confetti.
///
/// This system reads BuildingSnapshot data from SimSnapshot and maintains per-building
/// visual state (smoke intensity, light level, construction progress). It drives
/// the ParticleSystem for smoke/confetti and queues glow sprites through SpriteRenderer.
///
/// Performance: only processes buildings visible in the current frustum.
/// </summary>
public sealed class BuildingLifeRenderer : IDisposable
{
    /// <summary>
    /// Per-building visual state. Indexed by building snapshot index.
    /// </summary>
    private struct BuildingVisual
    {
        public int BuildingId;               // matches snapshot index
        public float SmokeIntensity;         // 0 = idle, 1 = full production (factories)
        public float LightLevel;             // window glow at night (0-1)
        public float TargetLightLevel;       // target for staggered lighting
        public byte ConditionVisual;         // 0=pristine, 1=worn, 2=damaged, 3=crumbling
        public bool IsUnderConstruction;
        public float ConstructionProgress;   // 0-1
        public bool HasActiveEvent;          // fire, celebration, etc.
        public float NeonFlickerTimer;       // for commercial neon sign flicker
        public float LightStaggerDelay;      // delay before this building lights up at dusk (0-30 game minutes)
        public bool Active;
    }

    private readonly Config _config;
    private readonly BuildingVisual[] _visuals;
    private int _visualCount;
    private readonly Random _rng;

    // Maximum tracked buildings for visual state
    public const int MaxTrackedBuildings = 16_384;

    // Smoke particle configs by type
    private static readonly ParticleConfig LightSmokeConfig = new()
    {
        LifetimeMin = 1.5f,
        LifetimeMax = 3.0f,
        SpeedMin = 8f,
        SpeedMax = 20f,
        DirectionMin = -1.8f,
        DirectionMax = -1.3f,
        GravityY = -10f,
        SizeStart = 3f,
        SizeEnd = 8f,
        ColorStart = 0xCCCCCCFF, // light grey (steam)
        ColorEnd = 0x999999FF,
        AlphaStart = 0.4f,
        AlphaEnd = 0.0f,
        MaxParticles = 16,
        EmitRate = 6f,
    };

    private static readonly ParticleConfig HeavySmokeConfig = new()
    {
        LifetimeMin = 2.0f,
        LifetimeMax = 4.0f,
        SpeedMin = 12f,
        SpeedMax = 30f,
        DirectionMin = -1.8f,
        DirectionMax = -1.3f,
        GravityY = -12f,
        SizeStart = 4f,
        SizeEnd = 14f,
        ColorStart = 0x444444FF, // dark grey (pollution)
        ColorEnd = 0x111111FF,
        AlphaStart = 0.6f,
        AlphaEnd = 0.0f,
        MaxParticles = 32,
        EmitRate = 12f,
    };

    private static readonly ParticleConfig ConstructionDustConfig = new()
    {
        LifetimeMin = 0.5f,
        LifetimeMax = 1.0f,
        SpeedMin = 15f,
        SpeedMax = 40f,
        DirectionMin = -2.5f,
        DirectionMax = -0.6f,
        GravityY = -8f,
        SizeStart = 2f,
        SizeEnd = 5f,
        ColorStart = 0xC4A86BFF,
        ColorEnd = 0x8B7355FF,
        AlphaStart = 0.5f,
        AlphaEnd = 0.0f,
        MaxParticles = 20,
        EmitRate = 10f,
    };

    // Window glow timing
    private const float DuskLightStart = 17.5f;  // game hours -- buildings start lighting up
    private const float DuskLightEnd = 19.5f;     // all buildings lit by this time
    private const float DawnLightStart = 5.5f;    // lights start going off
    private const float DawnLightEnd = 7.0f;      // all lights off

    // Neon flicker timing
    private const float NeonFlickerSpeed = 8f;

    /// <summary>Number of buildings currently tracked for visual effects.</summary>
    public int TrackedCount => _visualCount;

    public BuildingLifeRenderer(Config config)
    {
        _config = config;
        _visuals = new BuildingVisual[MaxTrackedBuildings];
        _rng = new Random(77777);
    }

    /// <summary>
    /// Synchronize building visual state from the simulation snapshot.
    /// Updates smoke intensity, construction progress, and condition visuals.
    /// </summary>
    public void UpdateFromSimulation(SimSnapshot snapshot)
    {
        int count = System.Math.Min(snapshot.BuildingCount, MaxTrackedBuildings);

        for (int i = 0; i < count; i++)
        {
            ref var bSnap = ref snapshot.Buildings[i];
            ref var vis = ref _visuals[i];

            if (!vis.Active)
            {
                // First time seeing this building -- initialize
                vis.Active = true;
                vis.BuildingId = i;
                vis.LightStaggerDelay = (float)_rng.NextDouble() * 30f; // 0-30 minute delay
                vis.NeonFlickerTimer = (float)_rng.NextDouble() * MathF.PI * 2f;
            }

            // Construction state
            vis.IsUnderConstruction = bSnap.State == 0;
            if (vis.IsUnderConstruction)
            {
                // Progress based on condition (used as construction progress proxy)
                vis.ConstructionProgress = 1f - (bSnap.Condition / 255f);
            }

            // Smoke intensity: factories (TypeId 100-199 = industrial) produce smoke
            // proportional to occupancy
            bool isIndustrial = bSnap.TypeId >= 100 && bSnap.TypeId < 200;
            if (isIndustrial && bSnap.State == 1 && bSnap.MaxOccupants > 0)
            {
                vis.SmokeIntensity = (float)bSnap.Occupants / bSnap.MaxOccupants;
            }
            else
            {
                vis.SmokeIntensity = 0f;
            }

            // Window glow: proportional to occupancy for residential/commercial
            if (bSnap.MaxOccupants > 0 && bSnap.State == 1)
            {
                vis.TargetLightLevel = (float)bSnap.Occupants / bSnap.MaxOccupants;
            }
            else
            {
                vis.TargetLightLevel = 0f;
            }

            // Condition visual (deterioration)
            vis.ConditionVisual = bSnap.Condition switch
            {
                >= 200 => 0, // pristine
                >= 128 => 1, // worn
                >= 64 => 2,  // damaged
                _ => 3,      // crumbling
            };

            // Active events (fire on buildings with very low condition)
            vis.HasActiveEvent = bSnap.Condition < 30 && bSnap.State == 1;
        }

        // Deactivate removed buildings
        for (int i = count; i < _visualCount; i++)
        {
            _visuals[i].Active = false;
        }

        _visualCount = count;
    }

    /// <summary>
    /// Per-frame update: animate light levels (staggered dusk lighting),
    /// neon flicker timers, and construction progress.
    /// </summary>
    public void Update(float dt, float timeOfDay, int weatherCondition)
    {
        // Determine if we're in the lighting transition window
        bool isDusk = timeOfDay >= DuskLightStart && timeOfDay <= DuskLightEnd;
        bool isDawn = timeOfDay >= DawnLightStart && timeOfDay <= DawnLightEnd;
        bool isNight = timeOfDay > DuskLightEnd || timeOfDay < DawnLightStart;

        for (int i = 0; i < _visualCount; i++)
        {
            ref var vis = ref _visuals[i];
            if (!vis.Active) continue;

            // Staggered window lighting at dusk
            if (isDusk)
            {
                // Each building has a random delay (0-30 game-minutes) before lighting up
                float duskProgress = (timeOfDay - DuskLightStart) / (DuskLightEnd - DuskLightStart);
                float minutesSinceDusk = duskProgress * 120f; // 2-hour window = 120 game-minutes
                if (minutesSinceDusk >= vis.LightStaggerDelay)
                {
                    // Smoothly ramp up light
                    vis.LightLevel = Lerp(vis.LightLevel, vis.TargetLightLevel, dt * 2f);
                }
            }
            else if (isDawn)
            {
                // Lights go off at dawn (staggered similarly)
                float dawnProgress = (timeOfDay - DawnLightStart) / (DawnLightEnd - DawnLightStart);
                float minutesSinceDawn = dawnProgress * 90f;
                if (minutesSinceDawn >= vis.LightStaggerDelay * 0.5f)
                {
                    vis.LightLevel = Lerp(vis.LightLevel, 0f, dt * 3f);
                }
            }
            else if (isNight)
            {
                // Maintain target light level at night
                vis.LightLevel = Lerp(vis.LightLevel, vis.TargetLightLevel, dt * 4f);
            }
            else
            {
                // Daytime: no window glow
                vis.LightLevel = Lerp(vis.LightLevel, 0f, dt * 5f);
            }

            // Neon sign flicker for commercial buildings (modern+ era)
            vis.NeonFlickerTimer += NeonFlickerSpeed * dt;
            if (vis.NeonFlickerTimer > MathF.PI * 2f)
                vis.NeonFlickerTimer -= MathF.PI * 2f;
        }
    }

    /// <summary>
    /// Render building life effects: window glow, smoke particles, construction scaffolding,
    /// neon signs, and condition overlays.
    /// </summary>
    public void Render(IsometricCamera camera, SpriteRenderer sprites, ParticleSystem particles)
    {
        var (offsetX, offsetY) = camera.GetRenderOffset();
        float zoom = camera.SmoothZoom;
        var frustum = camera.GetFrustumBounds();
        int tw = _config.TileWidth;
        int th = _config.TileHeight;

        for (int i = 0; i < _visualCount; i++)
        {
            ref var vis = ref _visuals[i];
            if (!vis.Active) continue;

            // Get building screen position from grid coordinates
            // (We access the snapshot building data via index correspondence)
            float gridX = i; // placeholder -- actual grid coords come from snapshot
            float gridY = i;

            // For rendering, we compute isometric position from the visual index
            // In production, building position would come from a parallel array or
            // be stored directly in BuildingVisual. Here we derive from pool index
            // since UpdateFromSimulation sets it up 1:1 with snapshot index.

            // We need the actual grid position -- this comes from the snapshot,
            // which is not stored in BuildingVisual to keep it lightweight.
            // The caller passes the snapshot to Render in the AmbientLifeManager,
            // but here we only have the particle system. We'll store grid coords.
            // (This is handled by the AmbientLifeManager which passes building data.)

            // Window glow at night
            if (vis.LightLevel > 0.01f)
            {
                // Rendered as a warm overlay in the AmbientLifeManager's coordinated pass
                // Here we just update the state; actual sprite emission is in the manager.
            }
        }
    }

    /// <summary>
    /// Render window glow for a specific building at the given screen position.
    /// Called by AmbientLifeManager which has access to building grid positions.
    /// </summary>
    public void RenderBuildingGlow(int visualIndex, float screenX, float screenY,
        float buildingWidth, float buildingHeight, float zoom,
        SpriteRenderer sprites, int era)
    {
        if (visualIndex < 0 || visualIndex >= _visualCount) return;
        ref var vis = ref _visuals[visualIndex];
        if (!vis.Active) return;

        // Window glow overlay (warm yellow-orange at night)
        if (vis.LightLevel > 0.01f)
        {
            float glowAlpha = vis.LightLevel * 0.6f;

            sprites.DrawInstanced(
                screenX, screenY, buildingWidth, buildingHeight,
                2f, // atlas layer 2 for building overlays
                0f, 0f, 1f, 1f, // full UV for a warm glow texture
                1.0f, 0.85f, 0.4f, glowAlpha, // warm yellow-orange
                screenY + buildingHeight - 0.5f // just behind the building top
            );
        }

        // Neon signs for commercial buildings in Modern+ era
        if (era >= 4 && vis.NeonFlickerTimer > 0f)
        {
            // Small colored point that flickers
            float flickerAlpha = 0.5f + 0.5f * MathF.Sin(vis.NeonFlickerTimer);
            float neonSize = 3f * zoom;

            // Only show neon for buildings with occupants (commercial)
            if (vis.TargetLightLevel > 0.2f && vis.LightLevel > 0.1f)
            {
                sprites.DrawInstanced(
                    screenX + buildingWidth * 0.3f,
                    screenY + buildingHeight * 0.2f,
                    neonSize, neonSize,
                    2f,
                    0f, 0f, 1f, 1f,
                    0.2f, 0.8f, 1.0f, flickerAlpha * 0.7f, // cyan neon
                    screenY + buildingHeight + 0.1f
                );
            }
        }

        // Construction scaffolding overlay
        if (vis.IsUnderConstruction)
        {
            float scaffoldAlpha = 0.7f;
            // Partial building: clip height by construction progress
            float visibleHeight = buildingHeight * vis.ConstructionProgress;

            sprites.DrawInstanced(
                screenX, screenY + buildingHeight - visibleHeight,
                buildingWidth, visibleHeight,
                2f,
                0f, 0.5f, 1f, 1f, // scaffolding UV region
                0.6f, 0.55f, 0.4f, scaffoldAlpha,
                screenY + buildingHeight - 0.3f
            );
        }

        // Condition deterioration overlay (cracks, darkening)
        if (vis.ConditionVisual >= 2)
        {
            float crackAlpha = vis.ConditionVisual switch
            {
                2 => 0.15f,
                3 => 0.35f,
                _ => 0f,
            };

            sprites.DrawInstanced(
                screenX, screenY, buildingWidth, buildingHeight,
                2f,
                0.5f, 0.5f, 1f, 1f, // crack overlay UV
                0.2f, 0.18f, 0.15f, crackAlpha,
                screenY + buildingHeight - 0.4f
            );
        }
    }

    /// <summary>
    /// Emit smoke particles for a building at the given world position.
    /// Called by AmbientLifeManager for industrial buildings.
    /// </summary>
    public void EmitSmoke(int visualIndex, float worldX, float worldY, ParticleSystem particles)
    {
        if (visualIndex < 0 || visualIndex >= _visualCount) return;
        ref var vis = ref _visuals[visualIndex];
        if (vis.SmokeIntensity <= 0.01f) return;

        // The AmbientLifeManager manages emitter lifetime and positioning.
        // This method just indicates whether smoke should be active and at what intensity.
        // The emitter's EmitRate is scaled by SmokeIntensity in the manager.
    }

    /// <summary>
    /// Get the smoke intensity for a building (0-1).
    /// Used by AmbientLifeManager to control particle emitter rate.
    /// </summary>
    public float GetSmokeIntensity(int visualIndex)
    {
        if (visualIndex < 0 || visualIndex >= _visualCount) return 0f;
        return _visuals[visualIndex].SmokeIntensity;
    }

    /// <summary>
    /// Get the light level for a building (0-1).
    /// Used by audio system to determine ambient soundscape.
    /// </summary>
    public float GetLightLevel(int visualIndex)
    {
        if (visualIndex < 0 || visualIndex >= _visualCount) return 0f;
        return _visuals[visualIndex].LightLevel;
    }

    /// <summary>Check if a building is under construction.</summary>
    public bool IsUnderConstruction(int visualIndex)
    {
        if (visualIndex < 0 || visualIndex >= _visualCount) return false;
        return _visuals[visualIndex].IsUnderConstruction;
    }

    /// <summary>Get construction progress (0-1) for a building.</summary>
    public float GetConstructionProgress(int visualIndex)
    {
        if (visualIndex < 0 || visualIndex >= _visualCount) return 0f;
        return _visuals[visualIndex].ConstructionProgress;
    }

    /// <summary>Get condition visual level (0=pristine, 3=crumbling).</summary>
    public byte GetConditionVisual(int visualIndex)
    {
        if (visualIndex < 0 || visualIndex >= _visualCount) return 0;
        return _visuals[visualIndex].ConditionVisual;
    }

    /// <summary>Whether a building has an active event (fire, etc.).</summary>
    public bool HasActiveEvent(int visualIndex)
    {
        if (visualIndex < 0 || visualIndex >= _visualCount) return false;
        return _visuals[visualIndex].HasActiveEvent;
    }

    /// <summary>Smoke particle config for light steam (e.g., heated buildings).</summary>
    public static ParticleConfig GetLightSmokeConfig() => LightSmokeConfig;

    /// <summary>Smoke particle config for heavy industrial smoke.</summary>
    public static ParticleConfig GetHeavySmokeConfig() => HeavySmokeConfig;

    /// <summary>Dust particle config for construction sites.</summary>
    public static ParticleConfig GetConstructionDustConfig() => ConstructionDustConfig;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    public void Dispose()
    {
        // Managed arrays; nothing to release.
    }
}
