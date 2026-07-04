using Silk.NET.OpenGL;

namespace Forge.Engine.Rendering;

/// <summary>
/// Configuration for a particle emitter defining visual and behavioral properties.
/// </summary>
public struct ParticleConfig
{
    public float LifetimeMin, LifetimeMax;
    public float SpeedMin, SpeedMax;
    public float DirectionMin, DirectionMax; // radians
    public float GravityY;
    public float SizeStart, SizeEnd;
    public uint ColorStart, ColorEnd; // RGBA packed (0xRRGGBBAA)
    public float AlphaStart, AlphaEnd;
    public int MaxParticles;
    public float EmitRate; // particles per second
}

/// <summary>
/// A particle emitter that spawns and manages a pool of particles.
/// </summary>
public sealed class ParticleEmitter
{
    public float X, Y;           // world position
    public bool IsActive;
    public float EmitRate;       // particles per second
    public int MaxParticles;     // pool size

    internal ParticleConfig Config;
    internal int Id;

    // Particle pools (SoA layout for cache efficiency)
    internal float[] PosX;
    internal float[] PosY;
    internal float[] VelX;
    internal float[] VelY;
    internal float[] Life;        // remaining lifetime
    internal float[] MaxLife;     // initial lifetime (for lerp)
    internal float[] Size;
    internal float[] ColorR;
    internal float[] ColorG;
    internal float[] ColorB;
    internal float[] ColorA;
    internal bool[] Alive;
    internal int AliveCount;

    // Emission accumulator
    internal float EmitAccumulator;

    // Random for this emitter
    internal Random Rng;

    internal ParticleEmitter(int id, ParticleConfig config)
    {
        Id = id;
        Config = config;
        MaxParticles = config.MaxParticles;
        EmitRate = config.EmitRate;
        IsActive = false;

        int cap = MaxParticles;
        PosX = new float[cap];
        PosY = new float[cap];
        VelX = new float[cap];
        VelY = new float[cap];
        Life = new float[cap];
        MaxLife = new float[cap];
        Size = new float[cap];
        ColorR = new float[cap];
        ColorG = new float[cap];
        ColorB = new float[cap];
        ColorA = new float[cap];
        Alive = new bool[cap];

        Rng = new Random(id * 31337 + Environment.TickCount);
    }

    /// <summary>Start continuous emission.</summary>
    public void Start()
    {
        IsActive = true;
        EmitAccumulator = 0f;
    }

    /// <summary>Stop continuous emission. Existing particles continue to their end of life.</summary>
    public void Stop()
    {
        IsActive = false;
    }

    /// <summary>Emit a one-time burst of particles regardless of IsActive state.</summary>
    public void Burst(int count)
    {
        int spawned = 0;
        for (int i = 0; i < MaxParticles && spawned < count; i++)
        {
            if (!Alive[i])
            {
                SpawnParticle(i);
                spawned++;
            }
        }
    }

    internal void SpawnParticle(int index)
    {
        ref var config = ref Config;

        float lifetime = Lerp(config.LifetimeMin, config.LifetimeMax, (float)Rng.NextDouble());
        float speed = Lerp(config.SpeedMin, config.SpeedMax, (float)Rng.NextDouble());
        float direction = Lerp(config.DirectionMin, config.DirectionMax, (float)Rng.NextDouble());

        PosX[index] = X;
        PosY[index] = Y;
        VelX[index] = MathF.Cos(direction) * speed;
        VelY[index] = MathF.Sin(direction) * speed;
        Life[index] = lifetime;
        MaxLife[index] = lifetime;
        Size[index] = config.SizeStart;
        Alive[index] = true;

        // Unpack start color
        UnpackColor(config.ColorStart, out ColorR[index], out ColorG[index], out ColorB[index]);
        ColorA[index] = config.AlphaStart;
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    internal static void UnpackColor(uint packed, out float r, out float g, out float b)
    {
        r = ((packed >> 24) & 0xFF) / 255f;
        g = ((packed >> 16) & 0xFF) / 255f;
        b = ((packed >> 8) & 0xFF) / 255f;
    }

    internal static float UnpackAlpha(uint packed) => (packed & 0xFF) / 255f;
}

/// <summary>
/// GPU-accelerated particle system for weather effects, construction dust, fire, and smoke.
/// Particles are CPU-simulated (position, velocity, lifetime) but rendered via
/// the existing SpriteRenderer's GPU instancing infrastructure for single-draw-call performance.
/// </summary>
public sealed class ParticleSystem : IDisposable
{
    private readonly List<ParticleEmitter> _emitters = new();
    private int _nextEmitterId;

    public ParticleSystem()
    {
    }

    /// <summary>
    /// Create a new particle emitter with the given configuration.
    /// The emitter is inactive until Start() is called.
    /// </summary>
    public ParticleEmitter CreateEmitter(ParticleConfig config)
    {
        var emitter = new ParticleEmitter(_nextEmitterId++, config);
        _emitters.Add(emitter);
        return emitter;
    }

    /// <summary>
    /// Remove an emitter from the system. All its particles are immediately destroyed.
    /// </summary>
    public void DestroyEmitter(ParticleEmitter emitter)
    {
        _emitters.Remove(emitter);
    }

    /// <summary>
    /// Update all active emitters: emit new particles, simulate physics, cull dead particles.
    /// Call once per frame.
    /// </summary>
    public void Update(float dt)
    {
        for (int e = 0; e < _emitters.Count; e++)
        {
            var emitter = _emitters[e];
            UpdateEmitter(emitter, dt);
        }
    }

    private static void UpdateEmitter(ParticleEmitter emitter, float dt)
    {
        ref var config = ref emitter.Config;

        // Update existing particles
        int aliveCount = 0;
        for (int i = 0; i < emitter.MaxParticles; i++)
        {
            if (!emitter.Alive[i])
                continue;

            emitter.Life[i] -= dt;
            if (emitter.Life[i] <= 0f)
            {
                emitter.Alive[i] = false;
                continue;
            }

            // Physics: velocity + gravity
            emitter.VelY[i] += config.GravityY * dt;
            emitter.PosX[i] += emitter.VelX[i] * dt;
            emitter.PosY[i] += emitter.VelY[i] * dt;

            // Interpolate properties based on lifetime progress
            float t = 1f - (emitter.Life[i] / emitter.MaxLife[i]); // 0 at spawn, 1 at death

            // Size interpolation
            emitter.Size[i] = Lerp(config.SizeStart, config.SizeEnd, t);

            // Color interpolation
            ParticleEmitter.UnpackColor(config.ColorStart, out float sr, out float sg, out float sb);
            ParticleEmitter.UnpackColor(config.ColorEnd, out float er, out float eg, out float eb);
            emitter.ColorR[i] = Lerp(sr, er, t);
            emitter.ColorG[i] = Lerp(sg, eg, t);
            emitter.ColorB[i] = Lerp(sb, eb, t);

            // Alpha interpolation
            emitter.ColorA[i] = Lerp(config.AlphaStart, config.AlphaEnd, t);

            aliveCount++;
        }
        emitter.AliveCount = aliveCount;

        // Continuous emission
        if (emitter.IsActive && emitter.EmitRate > 0f)
        {
            emitter.EmitAccumulator += emitter.EmitRate * dt;
            int toSpawn = (int)emitter.EmitAccumulator;
            emitter.EmitAccumulator -= toSpawn;

            int spawned = 0;
            for (int i = 0; i < emitter.MaxParticles && spawned < toSpawn; i++)
            {
                if (!emitter.Alive[i])
                {
                    emitter.SpawnParticle(i);
                    spawned++;
                }
            }
        }
    }

    /// <summary>
    /// Render all particles from all emitters using the SpriteRenderer's GPU instancing.
    /// Each emitter's particles are batched into a single draw call.
    /// Particles are rendered as small quads with position, size, and color as instance data.
    /// </summary>
    public void Render(IsometricCamera camera, SpriteRenderer sprites)
    {
        var (offsetX, offsetY) = camera.GetRenderOffset();
        float zoom = camera.SmoothZoom;

        for (int e = 0; e < _emitters.Count; e++)
        {
            var emitter = _emitters[e];
            if (emitter.AliveCount == 0)
                continue;

            for (int i = 0; i < emitter.MaxParticles; i++)
            {
                if (!emitter.Alive[i])
                    continue;

                float screenX = emitter.PosX[i] * zoom + offsetX;
                float screenY = emitter.PosY[i] * zoom + offsetY;
                float size = emitter.Size[i] * zoom;

                // Center the particle quad
                float drawX = screenX - size * 0.5f;
                float drawY = screenY - size * 0.5f;

                sprites.Draw(
                    drawX, drawY, size, size,
                    0f, 0f, 1f, 1f, // Full UV for a white pixel texture
                    emitter.ColorR[i],
                    emitter.ColorG[i],
                    emitter.ColorB[i],
                    emitter.ColorA[i]
                );
            }
        }
    }

    /// <summary>
    /// Get the total number of alive particles across all emitters.
    /// </summary>
    public int TotalAliveParticles
    {
        get
        {
            int total = 0;
            for (int e = 0; e < _emitters.Count; e++)
                total += _emitters[e].AliveCount;
            return total;
        }
    }

    /// <summary>
    /// Get the number of active emitters.
    /// </summary>
    public int EmitterCount => _emitters.Count;

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;

    // =========================================================================
    // Preset configurations for common effects
    // =========================================================================

    /// <summary>
    /// Rain: 200-400 fast diagonal particles, blue-white, short lifetime.
    /// </summary>
    public static ParticleConfig RainPreset => new()
    {
        LifetimeMin = 0.3f,
        LifetimeMax = 0.5f,
        SpeedMin = 400f,
        SpeedMax = 500f,
        DirectionMin = 1.2f,  // ~69 degrees (downward-right diagonal)
        DirectionMax = 1.5f,  // ~86 degrees
        GravityY = 200f,
        SizeStart = 2f,
        SizeEnd = 1f,
        ColorStart = 0xAABBFFFF, // light blue
        ColorEnd = 0x8899DDFF,   // slightly darker blue
        AlphaStart = 0.6f,
        AlphaEnd = 0.1f,
        MaxParticles = 400,
        EmitRate = 300f,
    };

    /// <summary>
    /// Snow: 100-200 slow drifting particles, white, long lifetime with horizontal oscillation via low gravity.
    /// </summary>
    public static ParticleConfig SnowPreset => new()
    {
        LifetimeMin = 2.0f,
        LifetimeMax = 4.0f,
        SpeedMin = 20f,
        SpeedMax = 50f,
        DirectionMin = 1.2f,  // mostly downward
        DirectionMax = 1.9f,
        GravityY = 10f,
        SizeStart = 3f,
        SizeEnd = 2f,
        ColorStart = 0xFFFFFFFF, // white
        ColorEnd = 0xEEEEFFFF,   // slightly blue-white
        AlphaStart = 0.8f,
        AlphaEnd = 0.0f,
        MaxParticles = 200,
        EmitRate = 60f,
    };

    /// <summary>
    /// Construction dust: 8-12 per burst, earth tones, rise and fade quickly.
    /// </summary>
    public static ParticleConfig DustPreset => new()
    {
        LifetimeMin = 0.3f,
        LifetimeMax = 0.6f,
        SpeedMin = 30f,
        SpeedMax = 80f,
        DirectionMin = -2.0f,  // upward spread
        DirectionMax = -1.1f,
        GravityY = -20f,       // slight upward drift
        SizeStart = 3f,
        SizeEnd = 6f,
        ColorStart = 0xC4A86BFF, // sandy brown
        ColorEnd = 0x8B7355FF,   // darker brown
        AlphaStart = 0.7f,
        AlphaEnd = 0.0f,
        MaxParticles = 24,
        EmitRate = 0f, // burst only
    };

    /// <summary>
    /// Fire: bright orange-yellow particles rising quickly with turbulence.
    /// </summary>
    public static ParticleConfig FirePreset => new()
    {
        LifetimeMin = 0.2f,
        LifetimeMax = 0.5f,
        SpeedMin = 60f,
        SpeedMax = 120f,
        DirectionMin = -1.8f,  // upward
        DirectionMax = -1.3f,
        GravityY = -100f,      // strong upward pull
        SizeStart = 4f,
        SizeEnd = 1f,
        ColorStart = 0xFFAA22FF, // bright orange
        ColorEnd = 0xFF3300FF,   // deep red
        AlphaStart = 0.9f,
        AlphaEnd = 0.0f,
        MaxParticles = 128,
        EmitRate = 80f,
    };

    /// <summary>
    /// Smoke: dark grey particles rising slowly and expanding.
    /// </summary>
    public static ParticleConfig SmokePreset => new()
    {
        LifetimeMin = 1.0f,
        LifetimeMax = 2.5f,
        SpeedMin = 15f,
        SpeedMax = 40f,
        DirectionMin = -1.8f,  // upward
        DirectionMax = -1.3f,
        GravityY = -15f,       // gentle rise
        SizeStart = 4f,
        SizeEnd = 12f,
        ColorStart = 0x555555FF, // medium grey
        ColorEnd = 0x222222FF,   // dark grey
        AlphaStart = 0.5f,
        AlphaEnd = 0.0f,
        MaxParticles = 64,
        EmitRate = 20f,
    };

    /// <summary>
    /// Confetti: colorful particles with random directions and slow gravity.
    /// </summary>
    public static ParticleConfig ConfettiPreset => new()
    {
        LifetimeMin = 1.5f,
        LifetimeMax = 3.0f,
        SpeedMin = 80f,
        SpeedMax = 200f,
        DirectionMin = -3.14159f, // full 360 degrees
        DirectionMax = 3.14159f,
        GravityY = 60f,          // fall back down
        SizeStart = 3f,
        SizeEnd = 2f,
        ColorStart = 0xFF44AAFF, // pink (will look varied due to direction randomization)
        ColorEnd = 0x44AAFFFF,   // light blue
        AlphaStart = 1.0f,
        AlphaEnd = 0.2f,
        MaxParticles = 256,
        EmitRate = 0f, // burst only
    };

    public void Dispose()
    {
        _emitters.Clear();
    }
}
