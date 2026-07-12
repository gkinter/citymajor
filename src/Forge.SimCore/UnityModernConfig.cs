using System.Text.Json;

namespace Forge.Engine;

/// <summary>
/// Unity v1 scope lock: modern era only (present-day city builder).
/// Filters content from <c>technologies.json</c> and <c>buildings.json</c>.
/// Manifest: <c>base/data/tech/unity-modern-unlocks.json</c>.
/// </summary>
public static class UnityModernConfig
{
    public const string DefaultEraTag = "modern";

    /// <summary>Era index aligned with <see cref="ResearchSystem.EraRequirements"/> and buildings.json.</summary>
    public const int DefaultEraIndex = 3;

    public const int StartingYear = 2026;

    public static readonly string[] AllowedEraTags = [DefaultEraTag];

    public static readonly string[] DeferredEraTags =
        ["frontier", "industrial", "postwar", "future"];

    public static readonly int[] DeferredEraIndices = [0, 1, 2, 4];

    // TypeId bands — must match buildingArchetypes.ts / DATA_BRIDGE.md
    public const int ResLowModernMin = 160;
    public const int ResLowModernMax = 179;
    public const int ResHighModernMin = 260;
    public const int ResHighModernMax = 279;
    public const int ComModernMin = 360;
    public const int ComModernMax = 379;
    public const int IndModernMin = 460;
    public const int IndModernMax = 479;
    public const int SvcTypeIdMin = 500;
    public const int SvcTypeIdMax = 599;
    public const int SvcModernJsonMin = 560;
    public const int SvcModernJsonMax = 579;

    public static bool IsAllowedEraTag(string? eraTag) =>
        !string.IsNullOrWhiteSpace(eraTag) &&
        eraTag.Equals(DefaultEraTag, StringComparison.OrdinalIgnoreCase);

    public static bool IsDeferredEraTag(string? eraTag) =>
        !string.IsNullOrWhiteSpace(eraTag) &&
        DeferredEraTags.Contains(eraTag, StringComparer.OrdinalIgnoreCase);

    public static bool IsModernEraIndex(int eraIndex) => eraIndex == DefaultEraIndex;

    /// <summary>True when a zone TypeId falls in a modern-era band (160–179, 260–279, 360–379, 460–479).</summary>
    public static bool IsModernZoneTypeId(int typeId) =>
        typeId is >= ResLowModernMin and <= ResLowModernMax
            or >= ResHighModernMin and <= ResHighModernMax
            or >= ComModernMin and <= ComModernMax
            or >= IndModernMin and <= IndModernMax;

    /// <summary>Service buildings always render as <c>svc_modern_*</c>; JSON modern band is 560–579.</summary>
    public static bool IsModernServiceTypeId(int typeId) =>
        typeId is >= SvcModernJsonMin and <= SvcModernJsonMax;

    public static bool IsPlayableTypeId(int typeId) =>
        IsModernZoneTypeId(typeId) || IsModernServiceTypeId(typeId);

    /// <summary>Filter technologies.json entries where <c>era</c> is <c>modern</c>.</summary>
    public static bool IsModernTechnology(JsonElement techElem)
    {
        if (!techElem.TryGetProperty("era", out var eraElem)) return false;
        return eraElem.ValueKind switch
        {
            JsonValueKind.String => IsAllowedEraTag(eraElem.GetString()),
            JsonValueKind.Number => eraElem.GetInt32() == DefaultEraIndex,
            _ => false,
        };
    }

    /// <summary>Filter buildings.json entries where <c>era</c> is modern (index 3).</summary>
    public static bool IsModernBuilding(JsonElement buildingElem)
    {
        if (!buildingElem.TryGetProperty("era", out var eraElem)) return false;
        return eraElem.ValueKind switch
        {
            JsonValueKind.Number => eraElem.GetInt32() == DefaultEraIndex,
            JsonValueKind.String => IsAllowedEraTag(eraElem.GetString()),
            _ => false,
        };
    }

    /// <summary>Keep only modern-era technology objects from a technologies.json array.</summary>
    public static string FilterTechnologiesJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return "[]";

        var filtered = doc.RootElement.EnumerateArray()
            .Where(IsModernTechnology)
            .Select(e => e.Clone())
            .ToArray();

        return JsonSerializer.Serialize(filtered, JsonFilterOptions);
    }

    /// <summary>Keep only modern-era building objects from a buildings.json array.</summary>
    public static string FilterBuildingsJson(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return "[]";

        var filtered = doc.RootElement.EnumerateArray()
            .Where(IsModernBuilding)
            .Select(e => e.Clone())
            .ToArray();

        return JsonSerializer.Serialize(filtered, JsonFilterOptions);
    }

    private static readonly JsonSerializerOptions JsonFilterOptions = new()
    {
        WriteIndented = true,
    };
}
