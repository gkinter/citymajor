using Forge.Engine.Simulation;
using Silk.NET.OpenGL;

namespace Forge.Engine.Rendering;

/// <summary>
/// Weather conditions matching WorldState.WeatherCondition values.
/// </summary>
public enum WeatherCondition
{
    Clear = 0,
    Cloudy = 1,
    Rain = 2,
    Storm = 3,
    Snow = 4,
    Fog = 5,
    Heatwave = 6,
    Blizzard = 7,
}

/// <summary>
/// Seasons matching WorldState.Season values.
/// </summary>
public enum Season
{
    Spring = 0,
    Summer = 1,
    Autumn = 2,
    Winter = 3,
}

/// <summary>
/// Terrain type IDs used for seasonal color tinting.
/// Values must match the engine's terrain type byte encoding.
/// </summary>
public static class TerrainTypes
{
    public const int Grass = 0;
    public const int Dirt = 1;
    public const int Water = 2;
    public const int Forest = 3;
    public const int Sand = 4;
    public const int Rock = 5;
}

/// <summary>
/// RGBA color stored as normalized floats for GPU-friendly operations.
/// </summary>
public readonly struct ColorF
{
    public readonly float R, G, B, A;

    public ColorF(float r, float g, float b, float a = 1f)
    {
        R = r; G = g; B = b; A = a;
    }

    /// <summary>Parse a hex color like 0x66BB6AFF into normalized floats.</summary>
    public static ColorF FromHex(uint hex)
    {
        float r = ((hex >> 24) & 0xFF) / 255f;
        float g = ((hex >> 16) & 0xFF) / 255f;
        float b = ((hex >> 8) & 0xFF) / 255f;
        float a = (hex & 0xFF) / 255f;
        return new ColorF(r, g, b, a);
    }

    public static ColorF Lerp(ColorF a, ColorF b, float t) => new(
        a.R + (b.R - a.R) * t,
        a.G + (b.G - a.G) * t,
        a.B + (b.B - a.B) * t,
        a.A + (b.A - a.A) * t
    );

    public static readonly ColorF White = new(1f, 1f, 1f, 1f);
}

/// <summary>
/// Comprehensive weather and seasonal visual system for the Forge Engine.
///
/// Manages particle emitters for rain, snow, leaves, flowers, and rain splashes.
/// Provides shader uniforms for fog, heat shimmer, and seasonal tinting.
/// Computes ambient light modifiers based on weather conditions.
///
/// Design:
///   - Reads SimSnapshot data (weather, season, wind, temperature) each frame
///   - Owns ParticleEmitter instances via the shared ParticleSystem
///   - Transitions between weather states smoothly over 2 seconds
///   - All color/brightness values lerp to targets (no harsh pops)
///
/// Usage:
///   weatherRenderer.Update(dt, snapshot);
///   weatherRenderer.Render(camera, spriteRenderer);       // particles
///   weatherRenderer.ApplyFogPass(fogShader, camera);      // fullscreen fog
///   weatherRenderer.ApplyHeatShimmer(shimmerShader, camera); // fullscreen shimmer
///   float ambient = weatherRenderer.GetWeatherAmbientModifier();
///   ColorF tint = weatherRenderer.GetSeasonalTerrainTint(terrainType, season);
/// </summary>
public sealed class WeatherRenderer : IDisposable
{
    private readonly ParticleSystem _particles;
    private readonly Random _rng = new(42);

    // =========================================================================
    // Particle emitters
    // =========================================================================

    private ParticleEmitter? _rainEmitter;
    private ParticleEmitter? _rainSplashEmitter;
    private ParticleEmitter? _snowEmitter;
    private ParticleEmitter? _leafEmitter;
    private ParticleEmitter? _flowerEmitter;

    // =========================================================================
    // Current state (lerped toward targets for smooth transitions)
    // =========================================================================

    private WeatherCondition _currentWeather = WeatherCondition.Clear;
    private Season _currentSeason = Season.Spring;
    private float _windSpeed;
    private float _windDirection;
    private float _timeOfDay;

    // Smooth transition tracking
    private float _ambientModifier = 1f;
    private float _targetAmbientModifier = 1f;
    private float _fogIntensity;
    private float _targetFogIntensity;
    private float _shimmerIntensity;
    private float _targetShimmerIntensity;
    private float _snowAccumulation;    // 0.0 = no snow, 1.0 = full cover
    private float _targetSnowAccumulation;

    // Lightning state
    private float _lightningTimer;       // seconds until next flash
    private float _lightningFlashAlpha;  // current flash brightness (0-0.6)
    private float _lightningFlashTimer;  // progress through 150ms flash
    private bool _lightningActive;
    private float _thunderDelay;         // seconds until thunder sound triggers
    private bool _thunderPending;

    // Puddle state
    private float _puddleIntensity;      // 0.0 = dry, 1.0 = full puddles
    private float _targetPuddleIntensity;

    // Accumulated time for shader animations
    private float _totalTime;

    // Transition speed (seconds to reach target)
    private const float TransitionSpeed = 2f;
    private const float SnowAccumulationRate = 1f / 30f; // 30 seconds to full cover
    private const float SnowMeltRate = 1f / 60f;         // 60 seconds to melt

    // Viewport dimensions (cached from camera)
    private int _viewportWidth;
    private int _viewportHeight;

    // =========================================================================
    // Seasonal color palettes
    // =========================================================================

    // Grass colors per season
    private static readonly ColorF SpringGrass = ColorF.FromHex(0x66BB6AFF);
    private static readonly ColorF SummerGrass = ColorF.FromHex(0x2E7D32FF);
    private static readonly ColorF AutumnGrass = ColorF.FromHex(0xFF8F00FF);
    private static readonly ColorF WinterGrass = new(0.85f, 0.88f, 0.92f, 1f); // white tint

    // Forest colors per season
    private static readonly ColorF SpringForest = ColorF.FromHex(0x388E3CFF);
    private static readonly ColorF SummerForest = ColorF.FromHex(0x1B5E20FF);
    private static readonly ColorF AutumnForest = ColorF.FromHex(0xE65100FF);
    private static readonly ColorF WinterForest = new(0.47f, 0.33f, 0.28f, 1f); // desaturated bare

    // Water colors for winter ice
    private static readonly ColorF NormalWater = new(1f, 1f, 1f, 1f);  // no tint
    private static readonly ColorF WinterWater = new(0.85f, 0.92f, 0.98f, 1f); // icy tint

    // Fog colors
    private static readonly ColorF DayFogColor = ColorF.FromHex(0xC8C8C8FF);
    private static readonly ColorF NightFogColor = ColorF.FromHex(0x404040FF);

    // =========================================================================
    // Particle configs
    // =========================================================================

    private static ParticleConfig LightRainConfig => new()
    {
        LifetimeMin = 0.4f,
        LifetimeMax = 0.7f,
        SpeedMin = 380f,
        SpeedMax = 420f,
        DirectionMin = 1.25f, // ~72 degrees (downward with slight diagonal)
        DirectionMax = 1.40f,
        GravityY = 150f,
        SizeStart = 2f,
        SizeEnd = 1f,
        ColorStart = 0xAABBFFFF,
        ColorEnd = 0x8899DDFF,
        AlphaStart = 0.3f,
        AlphaEnd = 0.05f,
        MaxParticles = 200,
        EmitRate = 200f,
    };

    private static ParticleConfig HeavyRainConfig => new()
    {
        LifetimeMin = 0.25f,
        LifetimeMax = 0.5f,
        SpeedMin = 550f,
        SpeedMax = 650f,
        DirectionMin = 1.20f,
        DirectionMax = 1.45f,
        GravityY = 250f,
        SizeStart = 2f,
        SizeEnd = 1f,
        ColorStart = 0x99AAEEFF,
        ColorEnd = 0x6688CCFF,
        AlphaStart = 0.5f,
        AlphaEnd = 0.1f,
        MaxParticles = 500,
        EmitRate = 400f,
    };

    private static ParticleConfig RainSplashConfig => new()
    {
        LifetimeMin = 0.1f,
        LifetimeMax = 0.2f,
        SpeedMin = 30f,
        SpeedMax = 80f,
        DirectionMin = -MathF.PI,     // full circle
        DirectionMax = 0f,            // upper half
        GravityY = 200f,
        SizeStart = 1f,
        SizeEnd = 2f,
        ColorStart = 0xCCDDFFFF,
        ColorEnd = 0xAABBEEFF,
        AlphaStart = 0.4f,
        AlphaEnd = 0.0f,
        MaxParticles = 100,
        EmitRate = 60f,
    };

    private static ParticleConfig SnowConfig => new()
    {
        LifetimeMin = 3.0f,
        LifetimeMax = 6.0f,
        SpeedMin = 60f,
        SpeedMax = 100f,
        DirectionMin = 1.2f,
        DirectionMax = 1.9f,
        GravityY = 5f,
        SizeStart = 4f,
        SizeEnd = 2f,
        ColorStart = 0xFFFFFFFF,
        ColorEnd = 0xEEEEFFFF,
        AlphaStart = 1.0f,
        AlphaEnd = 0.0f,
        MaxParticles = 300,
        EmitRate = 80f,
    };

    private static ParticleConfig BlizzardSnowConfig => new()
    {
        LifetimeMin = 1.5f,
        LifetimeMax = 3.0f,
        SpeedMin = 120f,
        SpeedMax = 200f,
        DirectionMin = 0.6f,    // more horizontal due to wind
        DirectionMax = 1.5f,
        GravityY = 15f,
        SizeStart = 4f,
        SizeEnd = 2f,
        ColorStart = 0xFFFFFFFF,
        ColorEnd = 0xDDDDFFFF,
        AlphaStart = 0.9f,
        AlphaEnd = 0.0f,
        MaxParticles = 500,
        EmitRate = 200f,
    };

    private static ParticleConfig AutumnLeafConfig => new()
    {
        LifetimeMin = 3.0f,
        LifetimeMax = 5.0f,
        SpeedMin = 15f,
        SpeedMax = 40f,
        DirectionMin = 0.5f,   // mostly downward with drift
        DirectionMax = 2.5f,
        GravityY = 8f,
        SizeStart = 3f,
        SizeEnd = 2f,
        ColorStart = 0xE65100FF, // orange
        ColorEnd = 0xBF360CFF,   // deep red-brown
        AlphaStart = 0.9f,
        AlphaEnd = 0.0f,
        MaxParticles = 150,
        EmitRate = 20f,
    };

    private static ParticleConfig SpringFlowerConfig => new()
    {
        LifetimeMin = 2.0f,
        LifetimeMax = 4.0f,
        SpeedMin = 10f,
        SpeedMax = 25f,
        DirectionMin = -MathF.PI,
        DirectionMax = MathF.PI,
        GravityY = 3f,
        SizeStart = 2f,
        SizeEnd = 1f,
        ColorStart = 0xFFB6C1FF, // pink
        ColorEnd = 0xFFFF99FF,   // yellow
        AlphaStart = 0.8f,
        AlphaEnd = 0.0f,
        MaxParticles = 80,
        EmitRate = 12f,
    };

    // =========================================================================
    // Construction
    // =========================================================================

    public WeatherRenderer(ParticleSystem particles)
    {
        _particles = particles;
        _lightningTimer = RandomRange(3f, 8f);
    }

    // =========================================================================
    // Update
    // =========================================================================

    /// <summary>
    /// Update weather visuals based on the current simulation snapshot.
    /// Call once per frame before Render().
    /// </summary>
    public void Update(float deltaTime, SimSnapshot snapshot)
    {
        _totalTime += deltaTime;
        _timeOfDay = snapshot.TimeOfDay;
        _viewportWidth = 1920;  // will be overridden by camera in Render
        _viewportHeight = 1080;

        var newWeather = (WeatherCondition)snapshot.WeatherCondition;
        var newSeason = (Season)snapshot.Season;
        float newWindSpeed = snapshot.WindSpeed;
        float newWindDir = snapshot.WindDirection;

        // Detect weather change and reconfigure emitters
        if (newWeather != _currentWeather)
        {
            TransitionWeather(newWeather);
            _currentWeather = newWeather;
        }

        // Detect season change
        if (newSeason != _currentSeason)
        {
            TransitionSeason(newSeason);
            _currentSeason = newSeason;
        }

        _windSpeed = newWindSpeed;
        _windDirection = newWindDir;

        // Compute wind push vector (normalized 0-1 wind speed, typical range 0-20 m/s)
        float windNorm = MathF.Min(_windSpeed / 20f, 1f);
        float windPushX = windNorm * MathF.Cos(_windDirection) * 100f;
        float windPushY = windNorm * MathF.Sin(_windDirection) * 100f;

        // Apply wind to all active particle emitters
        ApplyWindToEmitter(_rainEmitter, windPushX, windPushY, deltaTime);
        ApplyWindToEmitter(_snowEmitter, windPushX * 0.5f, windPushY * 0.5f, deltaTime);
        ApplyWindToEmitter(_leafEmitter, windPushX * 0.8f, windPushY * 0.8f, deltaTime);
        ApplyWindToEmitter(_flowerEmitter, windPushX * 0.3f, windPushY * 0.3f, deltaTime);

        // Apply sine-wave wobble to snow particles for drift effect
        if (_snowEmitter is { IsActive: true })
        {
            ApplySnowWobble(_snowEmitter, deltaTime);
        }

        // Smooth transitions toward target values
        float lerpFactor = 1f - MathF.Exp(-deltaTime / TransitionSpeed * 5f);
        _ambientModifier = Lerp(_ambientModifier, _targetAmbientModifier, lerpFactor);
        _fogIntensity = Lerp(_fogIntensity, _targetFogIntensity, lerpFactor);
        _shimmerIntensity = Lerp(_shimmerIntensity, _targetShimmerIntensity, lerpFactor);
        _puddleIntensity = Lerp(_puddleIntensity, _targetPuddleIntensity, lerpFactor);

        // Snow accumulation
        bool snowing = _currentWeather is WeatherCondition.Snow or WeatherCondition.Blizzard;
        if (snowing)
        {
            _targetSnowAccumulation = 1f;
            _snowAccumulation = MathF.Min(_snowAccumulation + SnowAccumulationRate * deltaTime, 1f);
        }
        else
        {
            _targetSnowAccumulation = 0f;
            _snowAccumulation = MathF.Max(_snowAccumulation - SnowMeltRate * deltaTime, 0f);
        }

        // Lightning updates for storm
        UpdateLightning(deltaTime);

        // Update sunset time based on season for ambient calculations
        // (actual day/night is handled by the caller, we just modify the multiplier)
    }

    // =========================================================================
    // Render — particles
    // =========================================================================

    /// <summary>
    /// Render all weather particles via the SpriteRenderer.
    /// The ParticleSystem.Update() should be called before this (it is shared).
    /// This method only renders emitters owned by the weather system.
    /// Call between spriteRenderer.Begin() and spriteRenderer.End().
    /// </summary>
    public void Render(IsometricCamera camera, SpriteRenderer spriteRenderer)
    {
        _viewportWidth = camera.ViewportWidth;
        _viewportHeight = camera.ViewportHeight;

        // Lightning flash overlay: draw a screen-filling white quad
        if (_lightningFlashAlpha > 0.001f)
        {
            spriteRenderer.Draw(
                0f, 0f,
                _viewportWidth, _viewportHeight,
                0f, 0f, 1f, 1f,
                1f, 1f, 1f, _lightningFlashAlpha
            );
        }

        // Rain splash particles need position scatter across the visible area
        if (_rainSplashEmitter is { IsActive: true })
        {
            var frustum = camera.GetFrustumBounds();
            _rainSplashEmitter.X = frustum.MinX + (frustum.Width * 0.5f);
            _rainSplashEmitter.Y = frustum.MaxY - 20f; // near ground level
        }

        // Particle rendering is handled by ParticleSystem.Render(),
        // which iterates all emitters. We just need to position ours.
        PositionScreenEmitters(camera);
    }

    /// <summary>
    /// Get the fog shader uniforms. Apply these to a fullscreen quad shader pass.
    /// Returns false if fog is not active (skip the pass).
    /// </summary>
    /// <param name="fogColor">RGB fog color (day/night interpolated).</param>
    /// <param name="fogStart">Normalized screen distance where fog begins (0-1).</param>
    /// <param name="fogEnd">Normalized screen distance where fog is fully opaque (0-1).</param>
    /// <param name="intensity">Overall fog intensity (0-1).</param>
    public bool GetFogUniforms(out ColorF fogColor, out float fogStart, out float fogEnd, out float intensity)
    {
        intensity = _fogIntensity;
        if (intensity < 0.001f)
        {
            fogColor = default;
            fogStart = 1f;
            fogEnd = 1f;
            return false;
        }

        // Interpolate fog color between day and night based on time of day
        float nightFactor = ComputeNightFactor(_timeOfDay);
        fogColor = ColorF.Lerp(DayFogColor, NightFogColor, nightFactor);

        // Fog starts at 60% screen distance from center, fully opaque at 95%
        fogStart = 0.6f;
        fogEnd = 0.95f;
        return true;
    }

    /// <summary>
    /// Get heat shimmer shader uniforms.
    /// Returns false if shimmer is not active.
    /// </summary>
    /// <param name="intensity">Shimmer distortion strength (0-1).</param>
    /// <param name="time">Accumulated time for animation.</param>
    public bool GetHeatShimmerUniforms(out float intensity, out float time)
    {
        intensity = _shimmerIntensity;
        time = _totalTime;
        return intensity > 0.001f;
    }

    /// <summary>
    /// Get the seasonal tint color to multiply onto a terrain tile's base color.
    /// Returns ColorF.White (no tint) for terrain types without seasonal variation.
    /// </summary>
    /// <param name="terrainType">Terrain type byte from the tile data.</param>
    /// <param name="season">Current season enum value.</param>
    public ColorF GetSeasonalTerrainTint(int terrainType, Season season)
    {
        // Snow accumulation overrides base seasonal tint
        float snow = _snowAccumulation;

        switch (terrainType)
        {
            case TerrainTypes.Grass:
            {
                ColorF baseTint = season switch
                {
                    Season.Spring => SpringGrass,
                    Season.Summer => SummerGrass,
                    Season.Autumn => AutumnGrass,
                    Season.Winter => WinterGrass,
                    _ => ColorF.White,
                };
                if (snow > 0.01f)
                {
                    // Blend toward white with snow accumulation (+30% white blend at full)
                    ColorF snowTint = new(
                        baseTint.R + (1f - baseTint.R) * snow * 0.3f,
                        baseTint.G + (1f - baseTint.G) * snow * 0.3f,
                        baseTint.B + (1f - baseTint.B) * snow * 0.3f,
                        1f
                    );
                    return snowTint;
                }
                return baseTint;
            }

            case TerrainTypes.Forest:
            {
                ColorF baseTint = season switch
                {
                    Season.Spring => SpringForest,
                    Season.Summer => SummerForest,
                    Season.Autumn => AutumnForest,
                    Season.Winter => WinterForest,
                    _ => ColorF.White,
                };
                if (snow > 0.01f)
                {
                    return new ColorF(
                        baseTint.R + (1f - baseTint.R) * snow * 0.2f,
                        baseTint.G + (1f - baseTint.G) * snow * 0.2f,
                        baseTint.B + (1f - baseTint.B) * snow * 0.2f,
                        1f
                    );
                }
                return baseTint;
            }

            case TerrainTypes.Dirt:
            {
                if (snow > 0.01f)
                {
                    return new ColorF(
                        1f * snow * 0.3f + (1f - snow * 0.3f),
                        1f * snow * 0.3f + (1f - snow * 0.3f),
                        1f * snow * 0.3f + (1f - snow * 0.3f),
                        1f
                    );
                }
                return ColorF.White;
            }

            case TerrainTypes.Water:
            {
                if (season == Season.Winter || snow > 0.01f)
                {
                    float iceFactor = season == Season.Winter ? 0.5f + snow * 0.5f : snow * 0.3f;
                    return ColorF.Lerp(NormalWater, WinterWater, iceFactor);
                }
                return NormalWater;
            }

            default:
                return ColorF.White;
        }
    }

    /// <summary>
    /// Get the weather-based ambient light multiplier.
    /// 1.0 = no change, lower values = darker.
    /// Factor in time-of-day sunset/sunrise shifts for seasonal day length.
    /// </summary>
    public float GetWeatherAmbientModifier()
    {
        return _ambientModifier;
    }

    /// <summary>
    /// Get current snow accumulation level (0-1) for building rooftop tinting.
    /// Buildings should tint their top 20% of sprite toward white by this factor.
    /// </summary>
    public float GetSnowAccumulation() => _snowAccumulation;

    /// <summary>
    /// Get current puddle intensity (0-1) for ground tile rendering.
    /// Puddle spots should be rendered as semi-transparent circles on ground tiles.
    /// </summary>
    public float GetPuddleIntensity() => _puddleIntensity;

    /// <summary>
    /// Get the sunset hour for the current season.
    /// Used by the day/night cycle to adjust daylight duration.
    /// </summary>
    public float GetSeasonalSunsetHour(Season season) => season switch
    {
        Season.Spring => 19f,
        Season.Summer => 20f,
        Season.Autumn => 17.5f,
        Season.Winter => 16f,
        _ => 18f,
    };

    /// <summary>
    /// Get the sunrise hour for the current season.
    /// </summary>
    public float GetSeasonalSunriseHour(Season season) => season switch
    {
        Season.Spring => 6f,
        Season.Summer => 5f,
        Season.Autumn => 6.5f,
        Season.Winter => 7.5f,
        _ => 6f,
    };

    /// <summary>
    /// Whether a thunder sound should play this frame. Resets after being read.
    /// The caller should trigger a sound effect when this returns true.
    /// </summary>
    public bool ConsumeThunderEvent()
    {
        if (_thunderPending)
        {
            _thunderPending = false;
            return true;
        }
        return false;
    }

    /// <summary>
    /// Get the seasonal warm/cool ambient color shift.
    /// Returns an additive RGB modifier:
    ///   Summer: slight warm (+orange)
    ///   Winter: slight cool (+blue)
    ///   Spring/Autumn: neutral
    /// </summary>
    public ColorF GetSeasonalAmbientTint(Season season) => season switch
    {
        Season.Summer => new ColorF(1.05f, 1.02f, 0.95f, 1f),   // +5% warm
        Season.Winter => new ColorF(0.95f, 0.97f, 1.05f, 1f),   // +5% cool
        Season.Autumn => new ColorF(1.02f, 0.98f, 0.95f, 1f),   // slight warm
        _ => ColorF.White,                                        // neutral
    };

    // =========================================================================
    // Weather transitions
    // =========================================================================

    private void TransitionWeather(WeatherCondition newWeather)
    {
        // Tear down old emitters
        DestroyWeatherEmitters();

        // Set targets based on new weather
        switch (newWeather)
        {
            case WeatherCondition.Clear:
                _targetAmbientModifier = 1f;
                _targetFogIntensity = 0f;
                _targetShimmerIntensity = 0f;
                _targetPuddleIntensity = 0f;
                break;

            case WeatherCondition.Cloudy:
                _targetAmbientModifier = 0.85f;
                _targetFogIntensity = 0f;
                _targetShimmerIntensity = 0f;
                _targetPuddleIntensity = 0f;
                break;

            case WeatherCondition.Rain:
                _rainEmitter = _particles.CreateEmitter(LightRainConfig);
                _rainEmitter.Start();
                _targetAmbientModifier = 0.8f;  // -20%
                _targetFogIntensity = 0.15f;     // light ground mist
                _targetShimmerIntensity = 0f;
                _targetPuddleIntensity = 0.6f;
                break;

            case WeatherCondition.Storm:
                _rainEmitter = _particles.CreateEmitter(HeavyRainConfig);
                _rainEmitter.Start();
                _rainSplashEmitter = _particles.CreateEmitter(RainSplashConfig);
                _rainSplashEmitter.Start();
                _targetAmbientModifier = 0.6f;  // -40%
                _targetFogIntensity = 0.25f;     // heavier mist
                _targetShimmerIntensity = 0f;
                _targetPuddleIntensity = 1f;
                _lightningTimer = RandomRange(3f, 8f);
                break;

            case WeatherCondition.Snow:
                _snowEmitter = _particles.CreateEmitter(SnowConfig);
                _snowEmitter.Start();
                _targetAmbientModifier = 0.9f;  // -10% overcast
                _targetFogIntensity = 0.1f;
                _targetShimmerIntensity = 0f;
                _targetPuddleIntensity = 0f;
                break;

            case WeatherCondition.Fog:
                _targetAmbientModifier = 0.85f;
                _targetFogIntensity = 1f;
                _targetShimmerIntensity = 0f;
                _targetPuddleIntensity = 0f;
                break;

            case WeatherCondition.Heatwave:
                _targetAmbientModifier = 1.1f;   // slightly brighter (+10% warm)
                _targetFogIntensity = 0f;
                _targetShimmerIntensity = 1f;
                _targetPuddleIntensity = 0f;
                break;

            case WeatherCondition.Blizzard:
                _snowEmitter = _particles.CreateEmitter(BlizzardSnowConfig);
                _snowEmitter.Start();
                _targetAmbientModifier = 0.65f;  // dark
                _targetFogIntensity = 0.6f;       // whiteout
                _targetShimmerIntensity = 0f;
                _targetPuddleIntensity = 0f;
                break;
        }
    }

    private void TransitionSeason(Season newSeason)
    {
        // Destroy seasonal particle emitters
        if (_leafEmitter != null)
        {
            _leafEmitter.Stop();
            _particles.DestroyEmitter(_leafEmitter);
            _leafEmitter = null;
        }
        if (_flowerEmitter != null)
        {
            _flowerEmitter.Stop();
            _particles.DestroyEmitter(_flowerEmitter);
            _flowerEmitter = null;
        }

        // Create seasonal particle effects
        switch (newSeason)
        {
            case Season.Spring:
                _flowerEmitter = _particles.CreateEmitter(SpringFlowerConfig);
                _flowerEmitter.Start();
                break;

            case Season.Autumn:
                _leafEmitter = _particles.CreateEmitter(AutumnLeafConfig);
                _leafEmitter.Start();
                break;
        }
    }

    private void DestroyWeatherEmitters()
    {
        if (_rainEmitter != null)
        {
            _rainEmitter.Stop();
            _particles.DestroyEmitter(_rainEmitter);
            _rainEmitter = null;
        }
        if (_rainSplashEmitter != null)
        {
            _rainSplashEmitter.Stop();
            _particles.DestroyEmitter(_rainSplashEmitter);
            _rainSplashEmitter = null;
        }
        if (_snowEmitter != null)
        {
            _snowEmitter.Stop();
            _particles.DestroyEmitter(_snowEmitter);
            _snowEmitter = null;
        }

        // Reset lightning
        _lightningFlashAlpha = 0f;
        _lightningActive = false;
        _thunderPending = false;
    }

    // =========================================================================
    // Lightning
    // =========================================================================

    private void UpdateLightning(float deltaTime)
    {
        if (_currentWeather != WeatherCondition.Storm)
        {
            _lightningFlashAlpha = 0f;
            return;
        }

        // Count down to next flash
        if (!_lightningActive)
        {
            _lightningTimer -= deltaTime;
            if (_lightningTimer <= 0f)
            {
                _lightningActive = true;
                _lightningFlashTimer = 0f;
                _thunderDelay = RandomRange(1f, 3f);
            }
        }

        // Animate flash: 0 -> 0.6 -> 0 over 150ms
        if (_lightningActive)
        {
            _lightningFlashTimer += deltaTime;
            float progress = _lightningFlashTimer / 0.15f; // 150ms duration

            if (progress < 0.3f)
            {
                // Ramp up (first 30% of 150ms = 45ms)
                _lightningFlashAlpha = progress / 0.3f * 0.6f;
            }
            else if (progress < 1f)
            {
                // Ramp down (remaining 70%)
                _lightningFlashAlpha = (1f - (progress - 0.3f) / 0.7f) * 0.6f;
            }
            else
            {
                // Flash complete
                _lightningFlashAlpha = 0f;
                _lightningActive = false;
                _lightningTimer = RandomRange(3f, 8f);
            }

            // Thunder sound delay
            _thunderDelay -= deltaTime;
            if (_thunderDelay <= 0f && !_thunderPending)
            {
                _thunderPending = true;
            }
        }
    }

    // =========================================================================
    // Emitter positioning
    // =========================================================================

    /// <summary>
    /// Position weather emitters to cover the visible screen area.
    /// Rain/snow spawn from above the viewport and drift down.
    /// </summary>
    private void PositionScreenEmitters(IsometricCamera camera)
    {
        var frustum = camera.GetFrustumBounds();
        float centerX = frustum.MinX + frustum.Width * 0.5f;
        float topY = frustum.MinY - 50f; // above viewport

        if (_rainEmitter != null)
        {
            _rainEmitter.X = centerX;
            _rainEmitter.Y = topY;
        }
        if (_snowEmitter != null)
        {
            _snowEmitter.X = centerX;
            _snowEmitter.Y = topY;
        }
        if (_leafEmitter != null)
        {
            _leafEmitter.X = centerX;
            _leafEmitter.Y = topY + frustum.Height * 0.3f; // leaves fall from mid-height
        }
        if (_flowerEmitter != null)
        {
            _flowerEmitter.X = centerX;
            _flowerEmitter.Y = frustum.MaxY - frustum.Height * 0.2f; // near ground
        }
    }

    /// <summary>
    /// Apply wind velocity offset to all alive particles in an emitter.
    /// This pushes existing particles horizontally each frame for a wind effect.
    /// </summary>
    private static void ApplyWindToEmitter(ParticleEmitter? emitter, float pushX, float pushY, float dt)
    {
        if (emitter == null || emitter.AliveCount == 0)
            return;

        for (int i = 0; i < emitter.MaxParticles; i++)
        {
            if (!emitter.Alive[i])
                continue;

            emitter.VelX[i] += pushX * dt;
            emitter.VelY[i] += pushY * dt * 0.2f; // less vertical push from wind
        }
    }

    /// <summary>
    /// Apply a sine-wave horizontal wobble to snow particles for a natural drift.
    /// Each particle wobbles at a slightly different frequency based on its index.
    /// </summary>
    private void ApplySnowWobble(ParticleEmitter emitter, float dt)
    {
        for (int i = 0; i < emitter.MaxParticles; i++)
        {
            if (!emitter.Alive[i])
                continue;

            float freq = 1.5f + (i % 7) * 0.3f; // vary frequency 1.5-3.6 Hz
            float wobble = MathF.Sin(_totalTime * freq + i * 0.7f) * 40f; // ±40px/s
            emitter.VelX[i] += (wobble - emitter.VelX[i]) * dt * 2f; // smooth toward wobble target
        }
    }

    // =========================================================================
    // Helpers
    // =========================================================================

    /// <summary>
    /// Compute a 0-1 factor representing how "night" it is.
    /// 0 = full daylight, 1 = full night. Smooth transition at dawn/dusk.
    /// </summary>
    private static float ComputeNightFactor(float timeOfDay)
    {
        // Sunrise ~6:00, sunset ~18:00 (base, modified by season externally)
        if (timeOfDay < 5f) return 1f;
        if (timeOfDay < 7f) return 1f - (timeOfDay - 5f) / 2f; // dawn
        if (timeOfDay < 17f) return 0f;
        if (timeOfDay < 19f) return (timeOfDay - 17f) / 2f;     // dusk
        return 1f;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    private float RandomRange(float min, float max) =>
        min + (float)_rng.NextDouble() * (max - min);

    // =========================================================================
    // Dispose
    // =========================================================================

    public void Dispose()
    {
        DestroyWeatherEmitters();

        if (_leafEmitter != null)
        {
            _particles.DestroyEmitter(_leafEmitter);
            _leafEmitter = null;
        }
        if (_flowerEmitter != null)
        {
            _particles.DestroyEmitter(_flowerEmitter);
            _flowerEmitter = null;
        }
    }
}
