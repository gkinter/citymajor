namespace Forge.SimWasm;

/// <summary>
/// Shared cosmetic-life math (rush curve, happiness tint). Linked into Forge.SimCore for Unity + WASM.
/// </summary>
public static class LifeSimMath
{
    /// <summary>1.0 off-peak, ~3.0 morning/evening rush, 0.65 overnight.</summary>
    public static float RushHourMultiplier(float timeOfDay)
    {
        var hour = timeOfDay % 24f;
        if (hour >= 7f && hour < 9f)
            return Lerp(1f, 3f, (hour - 7f) / 2f);
        if (hour >= 9f && hour < 17f)
            return 1f;
        if (hour >= 17f && hour < 19f)
            return Lerp(3f, 1f, (hour - 17f) / 2f);
        return 0.65f;
    }

    /// <summary>Maps 0–1 happiness to a green→amber→red tint.</summary>
    public static (float R, float G, float B) HappinessRgb(float happiness)
    {
        var h = Clamp01(happiness);
        if (h >= 0.6f)
        {
            var t = (h - 0.6f) / 0.4f;
            return (Lerp(0.95f, 0.25f, t), Lerp(0.75f, 0.85f, t), Lerp(0.35f, 0.35f, t));
        }

        var u = h / 0.6f;
        return (Lerp(0.9f, 0.95f, u), Lerp(0.35f, 0.75f, u), Lerp(0.3f, 0.35f, u));
    }

    static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;

    static float Lerp(float a, float b, float t)
    {
        if (t < 0f) t = 0f;
        if (t > 1f) t = 1f;
        return a + (b - a) * t;
    }
}
