namespace Forge.Engine.Rendering;

/// <summary>
/// Manages global lighting state for the day/night cycle.
/// Computes sun direction, sun color, ambient color, and night strength
/// from a 0-24 hour time-of-day value and pushes uniforms to all lit shaders.
///
/// Time ranges:
///   05:00-06:00  Blue hour (dawn)
///   06:00-07:30  Golden hour (dawn)
///   07:30-17:30  Daylight
///   17:30-19:00  Golden hour (dusk)
///   19:00-20:00  Blue hour (dusk)
///   20:00-05:00  Night
/// </summary>
public sealed class LightingSystem
{
    /// <summary>Sun direction in world space (normalized). Changes with time of day.</summary>
    public (float X, float Y, float Z) SunDirection { get; private set; } = (0.5f, -0.7f, 0.5f);

    /// <summary>Sun color (RGB 0-1). Warm at dawn/dusk, white at noon, zero at night.</summary>
    public (float R, float G, float B) SunColor { get; private set; } = (1f, 1f, 1f);

    /// <summary>Ambient light color (RGB 0-1). Dark blue at night, warm white by day.</summary>
    public (float R, float G, float B) AmbientColor { get; private set; } = (0.85f, 0.85f, 0.9f);

    /// <summary>Night strength: 0 = full daylight, 1 = full night. Smooth sinusoidal transition.</summary>
    public float NightStrength { get; private set; }

    /// <summary>Current time of day in hours (0-24).</summary>
    public float TimeOfDay { get; private set; } = 12f;

    /// <summary>
    /// Recalculate all lighting values from a time-of-day value.
    /// </summary>
    /// <param name="timeOfDay">Hour of day, 0-24 (e.g. 6.0 = 6:00 AM, 13.5 = 1:30 PM).</param>
    public void Update(float timeOfDay)
    {
        TimeOfDay = timeOfDay % 24f;
        if (TimeOfDay < 0f) TimeOfDay += 24f;

        float t = TimeOfDay;

        // Night strength: sinusoidal curve, 0 at noon, 1 at midnight
        // Map 0-24 to 0-2pi with midnight at pi (peak night)
        // noon (12) -> 0 night, midnight (0/24) -> 1 night
        NightStrength = ComputeNightStrength(t);

        // Sun direction: rotates east to west across the sky
        SunDirection = ComputeSunDirection(t);

        // Sun color: warm at golden hours, white at noon, zero at night
        SunColor = ComputeSunColor(t);

        // Ambient color: blue at night, neutral at day, warm tint at golden hours
        AmbientColor = ComputeAmbientColor(t);
    }

    /// <summary>
    /// Set lighting uniforms on a shader program. Call after Use() on the shader.
    /// </summary>
    public void SetUniforms(ShaderProgram shader)
    {
        shader.SetUniform("u_sunDirection", SunDirection.X, SunDirection.Y, SunDirection.Z);
        shader.SetUniform("u_sunColor", SunColor.R, SunColor.G, SunColor.B);
        shader.SetUniform("u_ambientColor", AmbientColor.R, AmbientColor.G, AmbientColor.B);
        shader.SetUniform("u_nightStrength", NightStrength);
    }

    private static float ComputeNightStrength(float hour)
    {
        // Smooth sinusoidal: peak at 0/24 (midnight), trough at 12 (noon)
        // sin maps: 12 -> sin(pi) = 0 -> nightStrength = 1 - 1 = 0
        //           0 -> sin(0) = 0 -> nightStrength = 1 - 0 = 1
        // Use cosine instead: cos(0) = 1 (noon bright), cos(pi) = -1 (midnight dark)
        float radians = (hour - 12f) / 12f * MathF.PI;
        float cosVal = MathF.Cos(radians);
        // cosVal: +1 at noon, -1 at midnight
        // nightStrength: 0 at noon, 1 at midnight
        return MathF.Max(0f, (1f - cosVal) * 0.5f);
    }

    private static (float X, float Y, float Z) ComputeSunDirection(float hour)
    {
        // Sun travels east-to-west: at 6:00 from the east, at 12:00 overhead, at 18:00 from the west
        // Below horizon (Y > 0) at night
        float dayProgress = (hour - 6f) / 12f; // 0 at 6AM, 1 at 6PM
        float angle = dayProgress * MathF.PI;   // 0 to PI across the day arc

        float x = MathF.Cos(angle);             // east (+1) at dawn, west (-1) at dusk
        float y = -MathF.Sin(angle);             // negative = shining down, positive = below horizon
        float z = 0.3f;                          // slight forward tilt for isometric perspective

        // Normalize
        float len = MathF.Sqrt(x * x + y * y + z * z);
        if (len > 0.001f)
        {
            x /= len;
            y /= len;
            z /= len;
        }

        return (x, y, z);
    }

    private static (float R, float G, float B) ComputeSunColor(float hour)
    {
        // Night: no sun
        if (hour < 5f || hour > 20f) return (0f, 0f, 0f);

        // Blue hour dawn: 5:00-6:00 — faint cool light
        if (hour < 6f)
        {
            float t = (hour - 5f); // 0 to 1
            return (0.3f * t, 0.3f * t, 0.5f * t);
        }

        // Golden hour dawn: 6:00-7:30 — warm orange
        if (hour < 7.5f)
        {
            float t = (hour - 6f) / 1.5f; // 0 to 1
            float r = Lerp(0.95f, 1.0f, t);
            float g = Lerp(0.55f, 0.95f, t);
            float b = Lerp(0.3f, 0.9f, t);
            return (r, g, b);
        }

        // Daylight: 7:30-17:30 — white/slightly warm
        if (hour < 17.5f)
        {
            return (1.0f, 0.98f, 0.92f);
        }

        // Golden hour dusk: 17:30-19:00 — warm orange
        if (hour < 19f)
        {
            float t = (hour - 17.5f) / 1.5f; // 0 to 1
            float r = Lerp(1.0f, 0.95f, t);
            float g = Lerp(0.95f, 0.55f, t);
            float b = Lerp(0.9f, 0.3f, t);
            return (r, g, b);
        }

        // Blue hour dusk: 19:00-20:00 — faint cool light
        {
            float t = (hour - 19f); // 0 to 1
            return (0.3f * (1f - t), 0.3f * (1f - t), 0.5f * (1f - t));
        }
    }

    private static (float R, float G, float B) ComputeAmbientColor(float hour)
    {
        // Night: dark blue
        if (hour < 5f || hour > 20f)
            return (0.08f, 0.08f, 0.18f);

        // Blue hour dawn: 5:00-6:00
        if (hour < 6f)
        {
            float t = (hour - 5f);
            return (Lerp(0.08f, 0.25f, t), Lerp(0.08f, 0.22f, t), Lerp(0.18f, 0.35f, t));
        }

        // Golden hour dawn: 6:00-7:30
        if (hour < 7.5f)
        {
            float t = (hour - 6f) / 1.5f;
            return (Lerp(0.25f, 0.85f, t), Lerp(0.22f, 0.82f, t), Lerp(0.35f, 0.85f, t));
        }

        // Daylight: 7:30-17:30
        if (hour < 17.5f)
            return (0.85f, 0.85f, 0.9f);

        // Golden hour dusk: 17:30-19:00
        if (hour < 19f)
        {
            float t = (hour - 17.5f) / 1.5f;
            return (Lerp(0.85f, 0.25f, t), Lerp(0.85f, 0.22f, t), Lerp(0.9f, 0.35f, t));
        }

        // Blue hour dusk: 19:00-20:00
        {
            float t = (hour - 19f);
            return (Lerp(0.25f, 0.08f, t), Lerp(0.22f, 0.08f, t), Lerp(0.35f, 0.18f, t));
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
