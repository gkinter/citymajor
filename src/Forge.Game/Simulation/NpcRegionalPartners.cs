namespace Forge.Game.Simulation;

/// <summary>
/// SB-3728 — fixed NPC towns on the regional overview (512×512) for bilateral trade.
/// Player claim is the center 256×256; markers are read-only profiles (supply/demand).
/// Multi-city save linking / diplomacy remain deferred.
/// </summary>
public static class NpcRegionalPartners
{
    /// <summary>Regional overview extent in world tiles (Phase 2 v1.5).</summary>
    public const float RegionalMapSize = 512f;

    /// <summary>Player city claim size nested in the regional overview.</summary>
    public const float PlayerClaimSize = 256f;

    /// <summary>Normalized player claim center on the regional map (0–1).</summary>
    public const float PlayerCenterX = 0.5f;

    /// <summary>Normalized player claim center on the regional map (0–1).</summary>
    public const float PlayerCenterY = 0.5f;

    /// <summary>Catalog size (3–5 NPC towns per WEB_V1_SCOPE §12).</summary>
    public const int Count = 5;

    static readonly NpcPartnerMarker[] Catalog =
    {
        // NW mining hub — coal surplus, food deficit
        new(0, "Coal Ridge", 0.22f, 0.78f, Good.Coal, Good.Food),
        // East port — finished goods surplus, fuel deficit
        new(1, "Harbor Vale", 0.82f, 0.48f, Good.Electronics, Good.Fuel),
        // North ore town — iron surplus, coal for smelting
        new(2, "Ironhaven", 0.48f, 0.88f, Good.IronOre, Good.Coal),
        // South ag belt — grain surplus, steel for tools
        new(3, "Grain Crossing", 0.52f, 0.18f, Good.Wheat, Good.Steel),
        // Southwest forest — timber surplus, fuel for mills
        new(4, "Timber Reach", 0.18f, 0.28f, Good.Timber, Good.Fuel),
    };

    /// <summary>Read-only NPC partner catalog (ids 0..Count-1).</summary>
    public static IReadOnlyList<NpcPartnerMarker> All => Catalog;

    /// <summary>True when <paramref name="partnerCityId"/> is a catalog NPC town.</summary>
    public static bool IsKnownPartner(int partnerCityId)
        => partnerCityId >= 0 && partnerCityId < Count;

    /// <summary>Lookup by id. Returns false for unknown / global-market ids.</summary>
    public static bool TryGet(int partnerCityId, out NpcPartnerMarker marker)
    {
        if (!IsKnownPartner(partnerCityId))
        {
            marker = default;
            return false;
        }

        marker = Catalog[partnerCityId];
        return true;
    }

    /// <summary>Display name or <c>Partner {id}</c> fallback.</summary>
    public static string NameOrFallback(int partnerCityId)
        => TryGet(partnerCityId, out var m) ? m.Name : $"Partner {partnerCityId}";

    /// <summary>
    /// Euclidean distance on the normalized regional map from the player claim center.
    /// Unknown partners fall back to a mild mid-range distance.
    /// </summary>
    public static float DistanceFromPlayer(int partnerCityId)
    {
        if (!TryGet(partnerCityId, out var m))
            return 0.35f;

        float dx = m.RegionalX - PlayerCenterX;
        float dy = m.RegionalY - PlayerCenterY;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Freight base months from regional distance (1–3) before congestion add-on.
    /// Closer towns settle faster; corner mining hubs take longer.
    /// </summary>
    public static int FreightBaseMonths(int partnerCityId)
    {
        if (partnerCityId < 0)
            return 1;

        float dist = DistanceFromPlayer(partnerCityId);
        // Typical catalog distances ~0.20–0.45 → base 1–3
        int months = 1 + (int)MathF.Round(dist * 4.5f);
        return Math.Clamp(months, 1, 3);
    }

    /// <summary>Writes <see cref="WorldState.NpcPartnerCount"/> for snapshot / HUD.</summary>
    public static void PublishCount(Forge.Engine.Simulation.WorldState state)
    {
        if (state is null) return;
        state.NpcPartnerCount = Count;
    }
}

/// <summary>One NPC town marker on the regional map (SB-3728).</summary>
public readonly struct NpcPartnerMarker
{
    public NpcPartnerMarker(
        int id,
        string name,
        float regionalX,
        float regionalY,
        Good exportSpecialty,
        Good importDemand)
    {
        Id = id;
        Name = name;
        RegionalX = regionalX;
        RegionalY = regionalY;
        ExportSpecialty = exportSpecialty;
        ImportDemand = importDemand;
    }

    /// <summary>Stable partner id used as <c>TradeRoute.PartnerCityId</c>.</summary>
    public int Id { get; }

    /// <summary>Player-facing town name.</summary>
    public string Name { get; }

    /// <summary>Normalized regional X (0 = west, 1 = east).</summary>
    public float RegionalX { get; }

    /// <summary>Normalized regional Y (0 = south, 1 = north).</summary>
    public float RegionalY { get; }

    /// <summary>Good this town typically exports (surplus specialty).</summary>
    public Good ExportSpecialty { get; }

    /// <summary>Good this town typically imports (demand specialty).</summary>
    public Good ImportDemand { get; }
}
